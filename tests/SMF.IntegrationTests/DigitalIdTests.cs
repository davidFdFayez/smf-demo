using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SMF.Application.Common.Interfaces;
using SMF.Application.Features.Admissions.Commands.VerifyDigitalId;
using SMF.Application.Features.Members.Commands.IssueDigitalId;
using SMF.Application.Features.Members.Commands.RegisterMember;
using SMF.Application.Features.Members.Queries.GetMemberById;
using SMF.Domain.Enums;
using SMF.Infrastructure.Persistence;
using Xunit;

namespace SMF.IntegrationTests;

public class DigitalIdTests : IClassFixture<SmfWebApplicationFactory>
{
    private readonly SmfWebApplicationFactory _factory;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public DigitalIdTests(SmfWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task GetMemberById_ReturnsMember_WhenExists()
    {
        using var client = _factory.CreateClient();
        var member = await RegisterAdultAthlete(client, "Getter Member");

        var response = await client.GetAsync($"/api/members/{member.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var details = await response.Content.ReadFromJsonAsync<MemberDetails>(JsonOptions);
        details.Should().NotBeNull();
        details!.Id.Should().Be(member.Id);
        details.SMF_ID.Should().Be(member.SMF_ID);
    }

    [Fact]
    public async Task GetMemberById_Returns404_WhenUnknown()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/members/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task IssueDigitalId_ReturnsSignedToken_WithMatchingValidityWindow()
    {
        using var client = _factory.CreateClient();
        var member = await RegisterAdultAthlete(client, "Issuee");

        var response = await client.PostAsync($"/api/members/{member.Id}/digital-id", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var issued = await response.Content.ReadFromJsonAsync<IssueDigitalIdResult>(JsonOptions);
        issued.Should().NotBeNull();
        issued!.Token.Should().NotBeNullOrEmpty();
        issued.Token.Split('.').Should().HaveCount(2,
            "token format is base64url(payload).base64url(hmac) — exactly one dot separator");
        issued.ValidUntilUtc.Should().BeAfter(issued.IssuedAtUtc);
    }

    [Fact]
    public async Task Verify_ReturnsAdmitted_ForFreshlyIssuedApprovedMember()
    {
        using var client = _factory.CreateClient();
        var member = await RegisterAdultAthlete(client, "Admitted Member");
        await client.PostAsync($"/api/members/{member.Id}/approve", content: null);
        var issued = await IssueDigitalId(client, member.Id);

        var verify = await Verify(client, issued.Token);

        verify.Outcome.Should().Be(AdmissionOutcome.Admitted);
        verify.CurrentStatus.Should().Be(RegistrationStatus.Approved);
        verify.SmfId.Should().Be(member.SMF_ID);
    }

    [Fact]
    public async Task Verify_DeniesPendingMember_EvenWithValidToken()
    {
        using var client = _factory.CreateClient();
        // Registered but NOT approved — status stays Pending.
        var member = await RegisterAdultAthlete(client, "Pending Member");
        var issued = await IssueDigitalId(client, member.Id);

        var verify = await Verify(client, issued.Token);

        verify.Outcome.Should().Be(AdmissionOutcome.MemberNotInGoodStanding);
        verify.CurrentStatus.Should().Be(RegistrationStatus.Pending);
    }

    [Fact]
    public async Task Verify_DeniesRevokedMember_WithinValidityWindow()
    {
        using var client = _factory.CreateClient();
        var member = await RegisterAdultAthlete(client, "Revoked Member");
        await client.PostAsync($"/api/members/{member.Id}/approve", content: null);
        var issued = await IssueDigitalId(client, member.Id);

        // Simulate a post-issue revocation by mutating the status
        // directly. In a real system this is done via an admin action;
        // the point of the test is: token is still signed & in-window,
        // but live status check must catch the downgrade.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var entity = await db.Members.FindAsync(member.Id);
            entity.Should().NotBeNull();
            typeof(SMF.Domain.Entities.Member)
                .GetProperty(nameof(SMF.Domain.Entities.Member.RegistrationStatus))!
                .SetValue(entity, RegistrationStatus.Pending);
            await db.SaveChangesAsync();
        }

        var verify = await Verify(client, issued.Token);

        verify.Outcome.Should().Be(AdmissionOutcome.MemberNotInGoodStanding);
        verify.CurrentStatus.Should().Be(RegistrationStatus.Pending);
    }

    [Fact]
    public async Task Verify_DeniesTamperedPayload_WithSignatureMismatch()
    {
        using var client = _factory.CreateClient();
        var member = await RegisterAdultAthlete(client, "Tampered Member");
        var issued = await IssueDigitalId(client, member.Id);

        // Flip one character inside the payload — easiest "attacker-
        // on-a-screenshot" scenario. Signature verification must fail
        // before any DB lookup.
        var parts = issued.Token.Split('.');
        var tampered = FlipOneChar(parts[0]) + "." + parts[1];

        var verify = await Verify(client, tampered);

        verify.Outcome.Should().Be(AdmissionOutcome.SignatureMismatch);
        verify.MemberId.Should().BeNull();
    }

    [Fact]
    public async Task Verify_DeniesMalformed_WhenTokenHasNoSeparator()
    {
        using var client = _factory.CreateClient();

        var verify = await Verify(client, "this-is-not-a-token");

        verify.Outcome.Should().Be(AdmissionOutcome.TokenMalformed);
    }

    [Fact]
    public async Task Verify_DeniesExpiredToken_AfterLifetimeElapses()
    {
        // Lifetime in the test factory is configured to 10s and clock
        // skew to 2s, so a 15s wait reliably pushes us past exp + skew.
        using var client = _factory.CreateClient();
        var member = await RegisterAdultAthlete(client, "Expirer");
        await client.PostAsync($"/api/members/{member.Id}/approve", content: null);

        // Issue via the service directly to avoid relying on network
        // timing through the HTTP pipeline for the 10-second window.
        using var scope = _factory.Services.CreateScope();
        var tokenService = scope.ServiceProvider.GetRequiredService<IDigitalIdTokenService>();
        var clock = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();

        var token = tokenService.Issue(new DigitalIdClaims(
            MemberId: member.Id,
            SmfId: member.SMF_ID,
            FullName: "Expirer",
            StatusAtIssue: RegistrationStatus.Approved));

        // Shift system perception forward by manipulating the token's
        // clock via the real wait — but 10s+2s = 12s is faster than the
        // overall test suite, which is fine.
        await Task.Delay(TimeSpan.FromSeconds(13));

        var verify = await Verify(client, token.Token);

        verify.Outcome.Should().Be(AdmissionOutcome.Expired);

        // Sanity — the issued-at / valid-until are still surfaced so
        // the gate UI can show "expired at 14:22".
        verify.ValidUntilUtc.Should().NotBeNull();
        clock.UtcNow.Should().BeAfter(verify.ValidUntilUtc!.Value);
    }

    // ─────────────────────── helpers ───────────────────────

    private static async Task<RegisterMemberResult> RegisterAdultAthlete(HttpClient client, string name)
    {
        var slug = Guid.NewGuid().ToString("N")[..8];
        var response = await client.PostAsJsonAsync("/api/members", new
        {
            fullName = name,
            dateOfBirth = "1993-04-21",
            role = "Athlete",
            guardianConsent = false,
            email = $"digital-id-{slug}@smf.local",
            phoneNumber = "+966500000000",
            nationalId = $"DID-{slug}",
            acceptTerms = true,
            acceptPrivacyPolicy = true,
            acceptCodeOfConduct = true
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RegisterMemberResult>(JsonOptions))!;
    }

    private static async Task<IssueDigitalIdResult> IssueDigitalId(HttpClient client, Guid memberId)
    {
        var response = await client.PostAsync($"/api/members/{memberId}/digital-id", content: null);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IssueDigitalIdResult>(JsonOptions))!;
    }

    private static async Task<VerifyDigitalIdResult> Verify(HttpClient client, string token)
    {
        var response = await client.PostAsJsonAsync("/api/admissions/verify", new { token });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<VerifyDigitalIdResult>(JsonOptions))!;
    }

    private static string FlipOneChar(string s)
    {
        // Flip the char in the middle — guaranteed not to be the
        // base64url padding boundary, which we stripped on encode.
        var mid = s.Length / 2;
        var c = s[mid];
        var replacement = c == 'a' ? 'b' : 'a';
        return s[..mid] + replacement + s[(mid + 1)..];
    }
}
