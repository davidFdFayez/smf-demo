using MediatR;
using Microsoft.AspNetCore.Mvc;
using SMF.Application.Features.Certificates;

namespace SMF.Api.Endpoints;

public static class CertificatesEndpoints
{
    public static IEndpointRouteBuilder MapCertificatesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/certificates").WithTags("Certificates");

        group.MapPost("/", async ([FromBody] IssueCertificateCommand cmd, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(cmd, ct);
            return Results.Created($"/api/certificates/{result.Id}", result);
        }).WithName("IssueCertificate").ProducesValidationProblem();

        group.MapGet("/by-member/{memberId:guid}", async (
                Guid memberId, ISender sender, CancellationToken ct)
            => Results.Ok(await sender.Send(new ListMemberCertificatesQuery(memberId), ct)))
            .WithName("ListMemberCertificates");

        group.MapGet("/verify/{code}", async (string code, ISender sender, CancellationToken ct)
            => Results.Ok(await sender.Send(new VerifyCertificateQuery(code), ct)))
            .WithName("VerifyCertificate").ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/download", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var doc = await sender.Send(new DownloadCertificateQuery(id), ct);
            return Results.File(doc.Content, doc.ContentType, doc.FileName);
        }).WithName("DownloadCertificate").ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
