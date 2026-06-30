using MediatR;
using Microsoft.AspNetCore.Mvc;
using SMF.Application.Common.Interfaces;
using SMF.Application.Features.Tenants;

namespace SMF.Api.Endpoints;

public static class TenantsEndpoints
{
    public static IEndpointRouteBuilder MapTenantsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tenants").WithTags("Scoring Tenants");

        group.MapGet("/", async (ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new ListTenantsQuery(), ct)))
            .WithName("ListTenants");

        // Public lookup used by the white-labelled scoreboard to resolve
        // its theme from the ?tenant=CODE query parameter.
        group.MapGet("/by-code/{code}", async (
                string code, ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new GetTenantByCodeQuery(code), ct)))
            .WithName("GetTenantByCode")
            .ProducesProblem(StatusCodes.Status404NotFound);

        // Returns whatever tenant the request resolved to (host header,
        // X-Tenant header, or ?tenant=). Used by the web shell to theme
        // itself before any other API call is made. Returns 204 when no
        // tenant matches — clients should fall back to defaults.
        group.MapGet("/current", (ITenantContext tenant) =>
            tenant.IsResolved
                ? Results.Ok(new
                {
                    id = tenant.TenantId,
                    code = tenant.Code,
                    displayName = tenant.DisplayName,
                    primaryColor = tenant.PrimaryColor,
                    accentColor = tenant.AccentColor,
                    logoUrl = tenant.LogoUrl,
                    customDomain = tenant.CustomDomain,
                })
                : Results.NoContent())
            .WithName("GetCurrentTenant")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status204NoContent);

        group.MapPost("/", async (
                [FromBody] ProvisionTenantCommand cmd,
                ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(cmd, ct);
            return Results.Created($"/api/tenants/by-code/{result.Code}", result);
        }).WithName("ProvisionTenant").ProducesValidationProblem();

        group.MapPut("/{id:guid}", async (
                Guid id, [FromBody] TenantBrandBody body,
                ISender sender, CancellationToken ct) =>
            Results.Ok(await sender.Send(new UpdateTenantBrandCommand(
                id, body.DisplayName, body.PrimaryColor, body.AccentColor,
                body.LogoUrl, body.ContactEmail,
                body.CustomDomain, body.WebsiteUrl, body.DarkLogoUrl), ct)))
            .WithName("UpdateTenantBrand");

        group.MapPost("/{id:guid}/active", async (
                Guid id, [FromBody] ActiveBody body,
                ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new SetTenantActiveCommand(id, body.IsActive), ct);
            return Results.NoContent();
        }).WithName("SetTenantActive");

        return app;
    }

    public sealed record TenantBrandBody(
        string DisplayName, string PrimaryColor, string AccentColor,
        string? LogoUrl, string ContactEmail,
        string? CustomDomain = null, string? WebsiteUrl = null, string? DarkLogoUrl = null);

    public sealed record ActiveBody(bool IsActive);
}
