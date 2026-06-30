using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SMF.Infrastructure.Communication.Push;

/// <summary>
/// Mints + caches an OAuth2 access token for the Firebase Cloud Messaging
/// HTTP v1 API. Builds the standard Google "service account" JWT
/// (RS256 over a header.payload), trades it at <c>oauth2.googleapis.com</c>
/// for a bearer token, and reuses it until shortly before its 1-hour expiry.
///
/// Implementing this manually avoids pulling the Google.Apis SDK (~7 MB of
/// transitive dependencies) for a single HTTP call. Code is short because the
/// token format is well-defined.
/// </summary>
internal sealed class FcmAccessTokenProvider
{
    public const string HttpClientName = "smf.push.googleoauth";
    private const string Scope = "https://www.googleapis.com/auth/firebase.messaging";
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<PushOptions> _options;
    private readonly ILogger<FcmAccessTokenProvider> _logger;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _cachedToken;
    private DateTime _cachedUntilUtc;
    private string? _cachedClientEmail;
    private string? _cachedProjectId;

    public FcmAccessTokenProvider(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<PushOptions> options,
        ILogger<FcmAccessTokenProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
    }

    /// <summary>Resolved Firebase project id (either from configuration or
    /// taken straight from the service-account JSON). Used by the sender to
    /// build the per-project FCM v1 endpoint.</summary>
    public string? ProjectId => _cachedProjectId ?? _options.CurrentValue.ProjectId;

    public async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        var sa = LoadServiceAccount();
        if (sa is null) return null;
        _cachedClientEmail = sa.ClientEmail;
        _cachedProjectId   = sa.ProjectId;

        if (!string.IsNullOrEmpty(_cachedToken) && DateTime.UtcNow < _cachedUntilUtc)
            return _cachedToken;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!string.IsNullOrEmpty(_cachedToken) && DateTime.UtcNow < _cachedUntilUtc)
                return _cachedToken;

            var jwt = BuildJwt(sa);

            using var http = _httpClientFactory.CreateClient(HttpClientName);
            using var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
                ["assertion"]  = jwt
            });

            using var resp = await http.PostAsync(TokenEndpoint, form, cancellationToken);
            var body = await resp.Content.ReadAsStringAsync(cancellationToken);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Google OAuth token endpoint returned {Status}: {Body}",
                    (int)resp.StatusCode, body);
                return null;
            }

            using var doc = JsonDocument.Parse(body);
            var token = doc.RootElement.GetProperty("access_token").GetString();
            var expiresIn = doc.RootElement.GetProperty("expires_in").GetInt32();

            _cachedToken    = token;
            // Refresh ~5 minutes before expiry to avoid edge-of-life calls.
            _cachedUntilUtc = DateTime.UtcNow.AddSeconds(Math.Max(60, expiresIn - 300));
            return token;
        }
        finally
        {
            _gate.Release();
        }
    }

    // ── service-account loading + JWT building ─────────────────────────

    private record ServiceAccount(string ClientEmail, string PrivateKeyPem, string ProjectId);

    private ServiceAccount? LoadServiceAccount()
    {
        var opts = _options.CurrentValue;
        string? json = opts.ServiceAccountJson;

        if (string.IsNullOrWhiteSpace(json) && !string.IsNullOrWhiteSpace(opts.ServiceAccountJsonPath))
        {
            try
            {
                json = File.ReadAllText(opts.ServiceAccountJsonPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not read FCM service-account file at {Path}", opts.ServiceAccountJsonPath);
                return null;
            }
        }

        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var email   = doc.RootElement.GetProperty("client_email").GetString();
            var pem     = doc.RootElement.GetProperty("private_key").GetString();
            var project = doc.RootElement.TryGetProperty("project_id", out var pid)
                ? pid.GetString()
                : opts.ProjectId;

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(pem) ||
                string.IsNullOrWhiteSpace(project))
                return null;

            return new ServiceAccount(email!, pem!, project!);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not parse FCM service-account JSON.");
            return null;
        }
    }

    private static string BuildJwt(ServiceAccount sa)
    {
        var headerJson = """{"alg":"RS256","typ":"JWT"}""";

        var iat = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var exp = iat + 3600;
        var payloadJson = JsonSerializer.Serialize(new
        {
            iss   = sa.ClientEmail,
            scope = Scope,
            aud   = TokenEndpoint,
            iat,
            exp
        });

        var headerB64  = Base64Url(Encoding.UTF8.GetBytes(headerJson));
        var payloadB64 = Base64Url(Encoding.UTF8.GetBytes(payloadJson));
        var signingInput = $"{headerB64}.{payloadB64}";

        using var rsa = RSA.Create();
        rsa.ImportFromPem(sa.PrivateKeyPem.AsSpan());
        var signature = rsa.SignData(
            Encoding.UTF8.GetBytes(signingInput),
            HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        return $"{signingInput}.{Base64Url(signature)}";
    }

    private static string Base64Url(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
