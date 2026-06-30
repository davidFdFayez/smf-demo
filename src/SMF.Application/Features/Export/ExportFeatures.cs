using System.Globalization;
using System.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SMF.Application.Common.Interfaces;

namespace SMF.Application.Features.Export;

/// <summary>
/// A CSV document ready to be streamed to the client (PDF §4 "Data Export").
/// We keep the format simple and portable (UTF-8 with BOM so Excel picks up
/// Arabic/Unicode correctly) and let the API layer stamp the HTTP headers.
/// </summary>
public sealed record CsvDocument(string FileName, byte[] Content)
{
    public string ContentType => "text/csv; charset=utf-8";
}

internal static class CsvWriter
{
    /// <summary>
    /// Escapes a single cell per RFC 4180: any value containing a comma,
    /// double-quote or line break is wrapped in double-quotes, with embedded
    /// quotes doubled. Null becomes an empty cell.
    /// </summary>
    public static string Escape(object? v)
    {
        if (v is null) return string.Empty;
        var s = v switch
        {
            DateTime dt   => dt.ToString("O", CultureInfo.InvariantCulture),
            DateOnly d    => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            decimal dec   => dec.ToString(CultureInfo.InvariantCulture),
            bool b        => b ? "true" : "false",
            _             => v.ToString() ?? string.Empty,
        };
        var needsQuote = s.Contains(',') || s.Contains('"') || s.Contains('\n') || s.Contains('\r');
        if (!needsQuote) return s;
        return "\"" + s.Replace("\"", "\"\"") + "\"";
    }

    public static byte[] ToBytes(string[] headers, IEnumerable<object?[]> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", headers.Select(Escape)));
        foreach (var row in rows)
            sb.AppendLine(string.Join(",", row.Select(Escape)));

        // BOM so Excel reliably detects UTF-8.
        var bom = new byte[] { 0xEF, 0xBB, 0xBF };
        var body = Encoding.UTF8.GetBytes(sb.ToString());
        var buf = new byte[bom.Length + body.Length];
        Buffer.BlockCopy(bom, 0, buf, 0, bom.Length);
        Buffer.BlockCopy(body, 0, buf, bom.Length, body.Length);
        return buf;
    }
}

// ─── Members CSV ────────────────────────────────────────────────────────────

public sealed record ExportMembersCsvQuery : IRequest<CsvDocument>;

public sealed class ExportMembersCsvQueryHandler
    : IRequestHandler<ExportMembersCsvQuery, CsvDocument>
{
    private readonly IApplicationDbContext _db;

    public ExportMembersCsvQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<CsvDocument> Handle(
        ExportMembersCsvQuery request,
        CancellationToken cancellationToken)
    {
        var rows = await _db.Members
            .AsNoTracking()
            .OrderBy(m => m.CreatedAtUtc)
            .Select(m => new object?[]
            {
                m.SMF_ID, m.FullName, m.Role, m.RegistrationStatus,
                m.DateOfBirth, m.Email, m.PhoneNumber, m.NationalId,
                m.AffiliatedClubId, m.LicenseLevel, m.YearsOfExperience,
                m.WeightCategoryKg, m.MedicalCleared, m.MedicalClearedAtUtc,
                m.CreatedAtUtc,
            })
            .ToListAsync(cancellationToken);

        var headers = new[]
        {
            "SMF_ID","FullName","Role","Status","DateOfBirth","Email",
            "PhoneNumber","NationalId","AffiliatedClubId","LicenseLevel",
            "YearsOfExperience","WeightCategoryKg","MedicalCleared",
            "MedicalClearedAtUtc","CreatedAtUtc",
        };

        return new CsvDocument(
            $"members-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv",
            CsvWriter.ToBytes(headers, rows));
    }
}

// ─── Clubs CSV ──────────────────────────────────────────────────────────────

public sealed record ExportClubsCsvQuery : IRequest<CsvDocument>;

public sealed class ExportClubsCsvQueryHandler
    : IRequestHandler<ExportClubsCsvQuery, CsvDocument>
{
    private readonly IApplicationDbContext _db;

    public ExportClubsCsvQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<CsvDocument> Handle(
        ExportClubsCsvQuery request,
        CancellationToken cancellationToken)
    {
        var rows = await _db.Clubs
            .AsNoTracking()
            .OrderBy(c => c.CreatedAtUtc)
            .Select(c => new object?[]
            {
                c.Name, c.Slug, c.City, c.ContactEmail, c.ContactPhone,
                c.WebsiteUrl, c.Status, c.CreatedAtUtc,
            })
            .ToListAsync(cancellationToken);

        var headers = new[]
        {
            "Name","Slug","City","ContactEmail","ContactPhone",
            "WebsiteUrl","Status","CreatedAtUtc",
        };

        return new CsvDocument(
            $"clubs-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv",
            CsvWriter.ToBytes(headers, rows));
    }
}

// ─── Events CSV ─────────────────────────────────────────────────────────────

public sealed record ExportEventsCsvQuery : IRequest<CsvDocument>;

public sealed class ExportEventsCsvQueryHandler
    : IRequestHandler<ExportEventsCsvQuery, CsvDocument>
{
    private readonly IApplicationDbContext _db;

    public ExportEventsCsvQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<CsvDocument> Handle(
        ExportEventsCsvQuery request,
        CancellationToken cancellationToken)
    {
        var rows = await _db.Events
            .AsNoTracking()
            .OrderBy(e => e.StartsAtUtc)
            .Select(e => new object?[]
            {
                e.Title, e.Location, e.Status, e.StartsAtUtc, e.EndsAtUtc,
                e.RegistrationOpensAtUtc, e.RegistrationClosesAtUtc,
                e.EntryFeeMinor, e.Currency, e.Capacity,
                e.Registrations.Count, e.CreatedAtUtc,
            })
            .ToListAsync(cancellationToken);

        var headers = new[]
        {
            "Title","Location","Status","StartsAtUtc","EndsAtUtc",
            "RegistrationOpensAtUtc","RegistrationClosesAtUtc",
            "EntryFeeMinor","Currency","Capacity","Registrations","CreatedAtUtc",
        };

        return new CsvDocument(
            $"events-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv",
            CsvWriter.ToBytes(headers, rows));
    }
}
