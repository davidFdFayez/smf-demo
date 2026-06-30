using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SMF.Application.Common.Interfaces;
using SMF.Domain.Entities;
using SMF.Domain.Enums;
using SMF.Infrastructure.Payments;
using SMF.Infrastructure.Persistence;
using Xunit;

namespace SMF.IntegrationTests;

/// <summary>
/// Exercises the full payment flow: initialize → webhook delivery →
/// PaymentSuccessfulEvent → Member promoted to Active.
/// Also verifies the security-critical guarantees of the callback:
/// bad signatures are rejected, duplicate deliveries are idempotent.
/// </summary>
public sealed class PaymentCallbackTests : IClassFixture<SmfWebApplicationFactory>, IAsyncLifetime
{
    private const string SignatureHeader = "X-Smf-Signature";

    private readonly SmfWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private Guid _memberId;

    public PaymentCallbackTests(SmfWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        // Seed an Approved member — only that status can be promoted to Active
        // by the ActivateMemberOnPaymentSucceededHandler.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        _memberId = Guid.NewGuid();
        var member = Member.Seed(
            _memberId,
            "Pending Activation",
            new DateOnly(1990, 1, 1),
            MemberRole.Athlete,
            $"#SMF-PAY-{Guid.NewGuid():N}".Substring(0, 18));
        // Member.Seed sets the status to Approved, which is exactly what we want.

        db.Members.Add(member);
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task InitializePayment_ReturnsRedirectUrlAndPersistsPendingRow()
    {
        var result = await InitializeMembershipPaymentAsync();

        result.Should().NotBeNull();
        result!.ProviderTransactionId.Should().StartWith("SBX-MADA-");
        result.RedirectUrl.Should().StartWith("https://");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var payment = await db.Payments.FirstAsync(p => p.Id == result.PaymentId);
        payment.Status.Should().Be(PaymentStatus.Pending);
        payment.MemberId.Should().Be(_memberId);
    }

    [Fact]
    public async Task Callback_WithValidSignature_MarksSuccessAndActivatesMember()
    {
        var init = await InitializeMembershipPaymentAsync();

        var response = await PostCallbackAsync(init!.ProviderTransactionId, sign: true);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // The webhook handler enqueues PaymentSuccessfulEvent on the outbox
        // but does NOT dispatch it synchronously. Drive the processor here so
        // the assertion is deterministic (the hosted poller is off in tests).
        await DrainOutboxAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var payment = await db.Payments.FirstAsync(p => p.Id == init.PaymentId);
        payment.Status.Should().Be(PaymentStatus.Succeeded);

        var member = await db.Members.FirstAsync(m => m.Id == _memberId);
        member.RegistrationStatus.Should().Be(
            RegistrationStatus.Active,
            "PaymentSuccessfulEvent should have triggered ActivateMemberOnPaymentSucceededHandler");
    }

    [Fact]
    public async Task Callback_WritesOutboxRow_InSameTransactionAsPaymentStateChange()
    {
        var init = await InitializeMembershipPaymentAsync();

        var response = await PostCallbackAsync(init!.ProviderTransactionId, sign: true);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Scope outbox queries to this payment only — the class fixture
        // shares the in-memory DB across tests, so prior tests have likely
        // already left their own rows in OutboxMessages.
        var paymentIdToken = init.PaymentId.ToString();

        // Before the processor runs there must be exactly one pending outbox
        // row — proof that the webhook handler did NOT publish synchronously.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var pendingForThisPayment = await db.OutboxMessages
                .Where(m => m.ProcessedAtUtc == null && m.Payload.Contains(paymentIdToken))
                .ToListAsync();

            pendingForThisPayment.Should().HaveCount(1,
                "ConfirmPaymentCallbackCommandHandler should stage exactly one PaymentSuccessfulEvent via the outbox");
            pendingForThisPayment[0].Type.Should().Contain("PaymentSuccessfulEvent");

            var payment = await db.Payments.FirstAsync(p => p.Id == init.PaymentId);
            payment.Status.Should().Be(PaymentStatus.Succeeded,
                "the payment row must have been committed alongside the outbox message");

            var member = await db.Members.FirstAsync(m => m.Id == _memberId);
            member.RegistrationStatus.Should().Be(RegistrationStatus.Approved,
                "member must not be Active until the outbox processor fans the event out");
        }

        // Drain the outbox — the event now reaches ActivateMemberOnPaymentSucceededHandler.
        var dispatched = await DrainOutboxAsync();
        dispatched.Should().BeGreaterOrEqualTo(1);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var processed = await db.OutboxMessages
                .SingleAsync(m => m.Payload.Contains(paymentIdToken));
            processed.ProcessedAtUtc.Should().NotBeNull();
            processed.Attempts.Should().Be(0, "successful dispatch must not register an attempt failure");
            processed.LastError.Should().BeNull();

            var member = await db.Members.FirstAsync(m => m.Id == _memberId);
            member.RegistrationStatus.Should().Be(RegistrationStatus.Active);
        }
    }

    [Fact]
    public async Task Callback_WithInvalidSignature_Returns400_AndDoesNotActivateMember()
    {
        var init = await InitializeMembershipPaymentAsync();

        var body = JsonSerializer.Serialize(new { transactionId = init!.ProviderTransactionId });
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/payments/callback")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        req.Headers.Add(SignatureHeader, "deadbeef-not-a-valid-signature");

        var response = await _client.SendAsync(req);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var member = await db.Members.FirstAsync(m => m.Id == _memberId);
        member.RegistrationStatus.Should().Be(
            RegistrationStatus.Approved,
            "a rejected signature must not flip the member to Active");
    }

    [Fact]
    public async Task Callback_DuplicateDelivery_IsIdempotent()
    {
        var init = await InitializeMembershipPaymentAsync();

        var first = await PostCallbackAsync(init!.ProviderTransactionId, sign: true);
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        var firstBody = await first.Content.ReadFromJsonAsync<CallbackResponse>();
        firstBody!.Transitioned.Should().BeTrue();

        var second = await PostCallbackAsync(init.ProviderTransactionId, sign: true);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        var secondBody = await second.Content.ReadFromJsonAsync<CallbackResponse>();
        secondBody!.Transitioned.Should().BeFalse(
            "the second delivery must short-circuit instead of enqueueing another PaymentSuccessfulEvent");

        // Only the first delivery should have produced an outbox row.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var outboxRowsForThisPayment = await db.OutboxMessages
            .Where(m => m.Payload.Contains(init.PaymentId.ToString()))
            .ToListAsync();
        outboxRowsForThisPayment.Should().HaveCount(1,
            "duplicate callbacks must be idempotent end-to-end, not just at the payment state level");

        await DrainOutboxAsync();

        var member = await db.Members.FirstAsync(m => m.Id == _memberId);
        member.RegistrationStatus.Should().Be(RegistrationStatus.Active);
    }

    [Fact]
    public async Task Callback_WhenGatewayReportsFailure_MarksPaymentFailed_AndMemberStaysApproved()
    {
        var init = await InitializeMembershipPaymentAsync();

        // Flip the sandbox's stored verification to Failed before the webhook
        // arrives. The controller re-asks the gateway, so the handler will
        // see Failed regardless of what the webhook body claims.
        var gateway = _factory.Services.GetRequiredService<MadaPaymentGatewayService>();
        gateway.SetVerificationResult(new SMF.Application.Common.Interfaces.PaymentVerificationResult(
            init!.ProviderTransactionId,
            PaymentStatus.Failed,
            AmountMinor: 0,
            Currency: "SAR",
            FailureReason: "card_declined"));

        var response = await PostCallbackAsync(init.ProviderTransactionId, sign: true);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var payment = await db.Payments.FirstAsync(p => p.Id == init.PaymentId);
        payment.Status.Should().Be(PaymentStatus.Failed);
        payment.FailureReason.Should().Contain("card_declined");

        var member = await db.Members.FirstAsync(m => m.Id == _memberId);
        member.RegistrationStatus.Should().Be(RegistrationStatus.Approved);
    }

    // ─────────────────────────────────────────── helpers ───────────────────

    /// <summary>
    /// Forces a synchronous drain of the outbox. The hosted poller is
    /// disabled in the Testing environment so callers can assert
    /// deterministically when downstream handlers should have run.
    /// </summary>
    private async Task<int> DrainOutboxAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<IOutboxProcessor>();
        return await processor.ProcessPendingAsync();
    }

    private async Task<InitializeResponse?> InitializeMembershipPaymentAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/payments", new
        {
            memberId = _memberId,
            provider = "Mada",
            purpose = "MembershipFee",
            amountMinor = 50_000L, // 500.00 SAR
            currency = "SAR",
            callbackUrl = "https://smf.local/api/payments/callback"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return await response.Content.ReadFromJsonAsync<InitializeResponse>();
    }

    private async Task<HttpResponseMessage> PostCallbackAsync(string transactionId, bool sign)
    {
        var body = JsonSerializer.Serialize(new
        {
            transactionId,
            status = "Succeeded"
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/payments/callback")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

        if (sign)
        {
            var signature = WebhookSignatureVerifier.Compute(
                body, SmfWebApplicationFactory.TestingWebhookSecret);
            request.Headers.Add(SignatureHeader, signature);
        }

        return await _client.SendAsync(request);
    }

    private sealed record InitializeResponse(
        Guid PaymentId,
        string ProviderTransactionId,
        string RedirectUrl);

    private sealed record CallbackResponse(
        Guid PaymentId,
        string Status,
        bool Transitioned);
}
