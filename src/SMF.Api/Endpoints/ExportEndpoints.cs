using MediatR;
using SMF.Application.Features.Export;

namespace SMF.Api.Endpoints;

public static class ExportEndpoints
{
    public static IEndpointRouteBuilder MapExportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/export").WithTags("Export");

        group.MapGet("/members.csv", async (ISender sender, CancellationToken ct) =>
            await ReturnCsv(await sender.Send(new ExportMembersCsvQuery(), ct)))
            .WithName("ExportMembersCsv");

        group.MapGet("/clubs.csv", async (ISender sender, CancellationToken ct) =>
            await ReturnCsv(await sender.Send(new ExportClubsCsvQuery(), ct)))
            .WithName("ExportClubsCsv");

        group.MapGet("/events.csv", async (ISender sender, CancellationToken ct) =>
            await ReturnCsv(await sender.Send(new ExportEventsCsvQuery(), ct)))
            .WithName("ExportEventsCsv");

        return app;
    }

    private static Task<IResult> ReturnCsv(CsvDocument doc) =>
        Task.FromResult(Results.File(doc.Content, doc.ContentType, doc.FileName));
}
