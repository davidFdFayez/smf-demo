using Microsoft.AspNetCore.Mvc;
using SMF.Api.Auth;

namespace SMF.Api.Endpoints;

/// <summary>
/// Developer/test-only token endpoint. In a real deployment the federation's
/// identity provider (OpenID Connect, Azure AD, etc.) issues tokens instead.
/// </summary>
public static class AuthEndpoints
{
    public sealed record DevTokenRequest(Guid RefereeId, string DisplayName, string[]? Roles = null);
    public sealed record DevTokenResponse(string AccessToken, DateTimeOffset ExpiresAt);

    public static void MapAuthEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/dev-token", (
            [FromBody] DevTokenRequest req,
            JwtTokenService tokens,
            IHostEnvironment env) =>
        {
            // Absolutely refuse to issue unsigned dev tokens outside of
            // Development / Testing — a safety net for misconfigured deploys.
            if (!env.IsDevelopment() && !env.IsEnvironment("Testing"))
                return Results.NotFound();

            if (req.RefereeId == Guid.Empty)
                return Results.BadRequest(new { error = "refereeId is required." });

            var token = tokens.Issue(
                req.RefereeId,
                string.IsNullOrWhiteSpace(req.DisplayName) ? "Dev User" : req.DisplayName,
                req.Roles ?? Array.Empty<string>());

            return Results.Ok(new DevTokenResponse(
                token,
                DateTimeOffset.UtcNow.AddMinutes(480)));
        })
        .WithName("IssueDevToken")
        .WithSummary("Issues a JWT for local development. Disabled outside Development/Testing environments.");
    }
}
