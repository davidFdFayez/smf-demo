using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SMF.Application.Common.Interfaces;
using SMF.Domain.Enums;

namespace SMF.Infrastructure.Payments;

/// <summary>
/// Tap Payments gateway adapter. Uses the <c>/v2/charges</c> API
/// (https://developers.tap.company/reference/create-a-charge) — POSTs the
/// charge, returns the hosted-checkout <c>transaction.url</c> back to the
/// browser, then re-verifies status with a GET on the charge id.
///
/// Authentication: Bearer the secret API key (<c>sk_test_*</c> / <c>sk_live_*</c>)
/// on every request. Never log this header.
///
/// The webhook body is HMAC-SHA256 keyed by <see cref="PaymentGatewayOptions.WebhookSigningSecret"/>;
/// configure the matching value in the Tap dashboard so
/// <see cref="WebhookSignatureVerifier"/> accepts incoming deliveries.
/// </summary>
public sealed class TapPaymentGatewayService : IPaymentGatewayService
{
    /// <summary>HttpClient name used by AddHttpClient and the resolver below.</summary>
    public const string HttpClientName = "tap-payments";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<PaymentGatewayOptions> _options;
    private readonly ILogger<TapPaymentGatewayService> _logger;

    public TapPaymentGatewayService(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<PaymentGatewayOptions> options,
        ILogger<TapPaymentGatewayService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
    }

    public async Task<PaymentInitializationResult> InitializePayment(
        PaymentRequest request, CancellationToken cancellationToken = default)
    {
        var opts = _options.CurrentValue;
        EnsureConfigured(opts);

        var client = BuildClient(opts);

        // Tap expects amounts in major units (e.g. 199.50 SAR), not minor units.
        // We store minor units everywhere internally so divide here.
        var amountMajor = request.AmountMinor / 100m;

        var body = new TapChargeRequest
        {
            Amount = amountMajor,
            Currency = request.Currency.ToUpperInvariant(),
            Description = request.Description ?? $"SMF {request.Purpose}",
            Reference = new TapReference { Order = request.MemberId.ToString("N") },
            Customer = new TapCustomer
            {
                FirstName = "SMF",
                LastName = request.Purpose.ToString(),
                Email = $"{request.MemberId:N}@smf.local"
            },
            Source = new TapSource { Id = MapProviderToSourceId(request.Provider) },
            Redirect = new TapRedirect { Url = opts.TapPostUrl },
            Post = new TapPost { Url = request.CallbackUrl }
        };

        var response = await client.PostAsJsonAsync(
            "/v2/charges", body, JsonOptions, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "Tap charge create failed: {Status} {Body}", response.StatusCode, errBody);
            throw new InvalidOperationException(
                $"Tap Payments rejected the charge ({(int)response.StatusCode}). See server logs.");
        }

        var charge = await response.Content.ReadFromJsonAsync<TapChargeResponse>(JsonOptions, cancellationToken)
                     ?? throw new InvalidOperationException("Tap returned an empty body.");

        if (string.IsNullOrWhiteSpace(charge.Id))
            throw new InvalidOperationException("Tap response is missing charge id.");
        if (charge.Transaction is null || string.IsNullOrWhiteSpace(charge.Transaction.Url))
            throw new InvalidOperationException("Tap response is missing transaction.url.");

        _logger.LogInformation(
            "Tap charge created: id={ChargeId}, status={Status}, redirect ready.",
            charge.Id, charge.Status);

        return new PaymentInitializationResult(charge.Id, charge.Transaction.Url);
    }

    public async Task<PaymentVerificationResult> VerifyPayment(
        string transactionId, CancellationToken cancellationToken = default)
    {
        var opts = _options.CurrentValue;
        EnsureConfigured(opts);

        var client = BuildClient(opts);
        var response = await client.GetAsync($"/v2/charges/{Uri.EscapeDataString(transactionId)}", cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "Tap charge fetch failed for {Txn}: {Status} {Body}", transactionId, response.StatusCode, errBody);
            return new PaymentVerificationResult(
                transactionId, PaymentStatus.Failed, 0, "SAR",
                FailureReason: $"Tap returned {(int)response.StatusCode}");
        }

        var charge = await response.Content.ReadFromJsonAsync<TapChargeResponse>(JsonOptions, cancellationToken)
                     ?? throw new InvalidOperationException("Tap returned empty body during verify.");

        var status = MapTapStatus(charge.Status);
        var amountMinor = (long)Math.Round((charge.Amount ?? 0m) * 100m, MidpointRounding.AwayFromZero);
        var currency = string.IsNullOrWhiteSpace(charge.Currency) ? "SAR" : charge.Currency!.ToUpperInvariant();
        var failure = status == PaymentStatus.Failed
            ? charge.Response?.Message ?? charge.Status
            : null;

        return new PaymentVerificationResult(transactionId, status, amountMinor, currency, failure);
    }

    private HttpClient BuildClient(PaymentGatewayOptions opts)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        client.BaseAddress ??= new Uri(opts.TapApiBaseUrl, UriKind.Absolute);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", opts.TapSecretKey);
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    private static void EnsureConfigured(PaymentGatewayOptions opts)
    {
        if (string.IsNullOrWhiteSpace(opts.TapSecretKey))
            throw new InvalidOperationException(
                "Tap Payments secret key is not configured. Set 'Payments:TapSecretKey' " +
                "(or the matching user-secret) before driving the Tap provider.");
        if (string.IsNullOrWhiteSpace(opts.TapApiBaseUrl))
            throw new InvalidOperationException("Payments:TapApiBaseUrl must be set.");
    }

    /// <summary>
    /// Maps our internal <see cref="PaymentProvider"/> to Tap's source id.
    /// <c>src_all</c> shows the buyer all enabled methods; specific source ids
    /// (<c>src_sa.mada</c>, <c>src_apple_pay</c>) lock the page to one rail.
    /// </summary>
    private static string MapProviderToSourceId(PaymentProvider provider) => provider switch
    {
        PaymentProvider.Mada => "src_sa.mada",
        PaymentProvider.ApplePay => "src_apple_pay",
        PaymentProvider.Visa => "src_card",
        _ => "src_all"
    };

    /// <summary>
    /// Maps Tap charge status strings (per their docs: INITIATED, IN_PROGRESS,
    /// CAPTURED, AUTHORIZED, FAILED, DECLINED, CANCELLED, RESTRICTED, ABANDONED,
    /// VOID, EXPIRED, TIMEDOUT) onto our domain status.
    /// </summary>
    private static PaymentStatus MapTapStatus(string? tapStatus) => (tapStatus ?? string.Empty).ToUpperInvariant() switch
    {
        "CAPTURED" or "AUTHORIZED" => PaymentStatus.Succeeded,
        "FAILED" or "DECLINED" or "CANCELLED" or "VOID" or "EXPIRED"
            or "TIMEDOUT" or "ABANDONED" or "RESTRICTED" => PaymentStatus.Failed,
        _ => PaymentStatus.Pending
    };

    // ---- Internal DTOs mirroring just the fields we use --------------------

    private sealed class TapChargeRequest
    {
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "SAR";
        public string? Description { get; set; }
        public TapReference? Reference { get; set; }
        public TapCustomer? Customer { get; set; }
        public TapSource? Source { get; set; }
        public TapRedirect? Redirect { get; set; }
        public TapPost? Post { get; set; }
    }

    private sealed class TapReference { public string? Order { get; set; } }
    private sealed class TapCustomer
    {
        [JsonPropertyName("first_name")] public string? FirstName { get; set; }
        [JsonPropertyName("last_name")] public string? LastName { get; set; }
        public string? Email { get; set; }
    }
    private sealed class TapSource { public string? Id { get; set; } }
    private sealed class TapRedirect { public string? Url { get; set; } }
    private sealed class TapPost { public string? Url { get; set; } }

    private sealed class TapChargeResponse
    {
        public string? Id { get; set; }
        public string? Status { get; set; }
        public decimal? Amount { get; set; }
        public string? Currency { get; set; }
        public TapTransaction? Transaction { get; set; }
        public TapResponseMeta? Response { get; set; }
    }
    private sealed class TapTransaction { public string? Url { get; set; } }
    private sealed class TapResponseMeta
    {
        public string? Code { get; set; }
        public string? Message { get; set; }
    }
}
