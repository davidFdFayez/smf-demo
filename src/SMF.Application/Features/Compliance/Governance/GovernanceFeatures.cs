using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SMF.Application.Common.Interfaces;
using SMF.Application.Features.Compliance.Common;
using SMF.Domain.Entities;
using SMF.Domain.Enums;

namespace SMF.Application.Features.Compliance.Governance;

/// <summary>
/// Governance document management — SOPC transparency requirement. Admins
/// upload PDFs (annual reports, anti-doping policies, athlete protection,
/// etc.). The public-facing list returns only published rows; admin queries
/// see drafts too.
/// </summary>
public static class GovernanceFeatures
{
    // ──────────────── upload ─────────────────────────────
    public sealed record UploadGovernanceDocumentCommand(
        string Title,
        string? Description,
        GovernanceDocumentType DocumentType,
        int? CoveringYear,
        Stream Content,
        string OriginalFileName,
        string ContentType,
        Guid? UploadedByMemberId
    ) : IRequest<GovernanceDocumentDto>;

    public sealed class UploadValidator : AbstractValidator<UploadGovernanceDocumentCommand>
    {
        public UploadValidator()
        {
            RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Description).MaximumLength(2000);
            RuleFor(x => x.OriginalFileName).NotEmpty().MaximumLength(260);
            RuleFor(x => x.ContentType).NotEmpty().MaximumLength(120);
            RuleFor(x => x.CoveringYear).InclusiveBetween(1900, 2200).When(x => x.CoveringYear.HasValue);
        }
    }

    public sealed class UploadHandler : IRequestHandler<UploadGovernanceDocumentCommand, GovernanceDocumentDto>
    {
        private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "application/pdf",
            "application/msword",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "application/vnd.ms-excel",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "image/png",
            "image/jpeg"
        };

        private readonly IApplicationDbContext _db;
        private readonly IFileStorage _storage;

        public UploadHandler(IApplicationDbContext db, IFileStorage storage)
        {
            _db = db; _storage = storage;
        }

        public async Task<GovernanceDocumentDto> Handle(UploadGovernanceDocumentCommand request, CancellationToken ct)
        {
            if (!AllowedContentTypes.Contains(request.ContentType))
                throw new InvalidOperationException(
                    $"Unsupported content type '{request.ContentType}'. " +
                    "Governance documents must be PDF, Word, Excel, PNG, or JPEG.");

            var stored = await _storage.UploadAsync(
                request.Content, "governance", request.OriginalFileName, request.ContentType, ct);

            var doc = GovernanceDocument.Upload(
                request.Title, request.Description, request.DocumentType,
                stored.Key, request.OriginalFileName, request.ContentType,
                stored.SizeBytes, stored.Sha256, request.CoveringYear,
                request.UploadedByMemberId);

            _db.GovernanceDocuments.Add(doc);
            await _db.SaveChangesAsync(ct);

            return Map(doc, _storage);
        }
    }

    // ──────────────── publish toggles + delete + edit ─────
    public sealed record PublishGovernanceDocumentCommand(Guid Id, bool Publish) : IRequest<GovernanceDocumentDto>;
    public sealed record UpdateGovernanceDocumentCommand(
        Guid Id, string Title, string? Description, GovernanceDocumentType DocumentType, int? CoveringYear) : IRequest<GovernanceDocumentDto>;
    public sealed record DeleteGovernanceDocumentCommand(Guid Id) : IRequest<Unit>;

    public sealed class PublishHandler : IRequestHandler<PublishGovernanceDocumentCommand, GovernanceDocumentDto>
    {
        private readonly IApplicationDbContext _db;
        private readonly IFileStorage _storage;
        public PublishHandler(IApplicationDbContext db, IFileStorage storage) { _db = db; _storage = storage; }

        public async Task<GovernanceDocumentDto> Handle(PublishGovernanceDocumentCommand request, CancellationToken ct)
        {
            var doc = await _db.GovernanceDocuments.FirstOrDefaultAsync(d => d.Id == request.Id, ct)
                      ?? throw new KeyNotFoundException($"Governance document {request.Id} not found.");
            if (request.Publish) doc.Publish(); else doc.Unpublish();
            await _db.SaveChangesAsync(ct);
            return Map(doc, _storage);
        }
    }

    public sealed class UpdateHandler : IRequestHandler<UpdateGovernanceDocumentCommand, GovernanceDocumentDto>
    {
        private readonly IApplicationDbContext _db;
        private readonly IFileStorage _storage;
        public UpdateHandler(IApplicationDbContext db, IFileStorage storage) { _db = db; _storage = storage; }

        public async Task<GovernanceDocumentDto> Handle(UpdateGovernanceDocumentCommand request, CancellationToken ct)
        {
            var doc = await _db.GovernanceDocuments.FirstOrDefaultAsync(d => d.Id == request.Id, ct)
                      ?? throw new KeyNotFoundException($"Governance document {request.Id} not found.");
            doc.UpdateMetadata(request.Title, request.Description, request.DocumentType, request.CoveringYear);
            await _db.SaveChangesAsync(ct);
            return Map(doc, _storage);
        }
    }

    public sealed class DeleteHandler : IRequestHandler<DeleteGovernanceDocumentCommand, Unit>
    {
        private readonly IApplicationDbContext _db;
        private readonly IFileStorage _storage;
        public DeleteHandler(IApplicationDbContext db, IFileStorage storage) { _db = db; _storage = storage; }

        public async Task<Unit> Handle(DeleteGovernanceDocumentCommand request, CancellationToken ct)
        {
            var doc = await _db.GovernanceDocuments.FirstOrDefaultAsync(d => d.Id == request.Id, ct);
            if (doc is null) return Unit.Value;

            // Best-effort blob cleanup. The DB row is the source of truth so
            // even a storage error shouldn't block deletion of metadata —
            // operators can sweep orphans separately.
            try { await _storage.DeleteAsync(doc.StorageKey, ct); } catch { /* swallow */ }
            _db.GovernanceDocuments.Remove(doc);
            await _db.SaveChangesAsync(ct);
            return Unit.Value;
        }
    }

    // ──────────────── queries ─────────────────────────────
    public sealed record ListGovernanceDocumentsQuery(
        bool PublishedOnly, GovernanceDocumentType? DocumentType, int? CoveringYear) : IRequest<IReadOnlyList<GovernanceDocumentDto>>;

    public sealed class ListHandler : IRequestHandler<ListGovernanceDocumentsQuery, IReadOnlyList<GovernanceDocumentDto>>
    {
        private readonly IApplicationDbContext _db;
        private readonly IFileStorage _storage;
        public ListHandler(IApplicationDbContext db, IFileStorage storage) { _db = db; _storage = storage; }

        public async Task<IReadOnlyList<GovernanceDocumentDto>> Handle(
            ListGovernanceDocumentsQuery request, CancellationToken ct)
        {
            IQueryable<GovernanceDocument> q = _db.GovernanceDocuments.AsNoTracking();
            if (request.PublishedOnly) q = q.Where(d => d.IsPublished);
            if (request.DocumentType.HasValue) q = q.Where(d => d.DocumentType == request.DocumentType.Value);
            if (request.CoveringYear.HasValue) q = q.Where(d => d.CoveringYear == request.CoveringYear.Value);

            var rows = await q
                .OrderByDescending(d => d.CoveringYear ?? 0)
                .ThenByDescending(d => d.UploadedAtUtc)
                .ToListAsync(ct);

            return rows.Select(d => Map(d, _storage)).ToList();
        }
    }

    public sealed record GetGovernanceDocumentQuery(Guid Id, bool PublishedOnly) : IRequest<GovernanceDocumentDto?>;
    public sealed class GetHandler : IRequestHandler<GetGovernanceDocumentQuery, GovernanceDocumentDto?>
    {
        private readonly IApplicationDbContext _db;
        private readonly IFileStorage _storage;
        public GetHandler(IApplicationDbContext db, IFileStorage storage) { _db = db; _storage = storage; }

        public async Task<GovernanceDocumentDto?> Handle(GetGovernanceDocumentQuery request, CancellationToken ct)
        {
            var doc = await _db.GovernanceDocuments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == request.Id, ct);
            if (doc is null) return null;
            if (request.PublishedOnly && !doc.IsPublished) return null;
            return Map(doc, _storage);
        }
    }

    internal static GovernanceDocumentDto Map(GovernanceDocument d, IFileStorage storage) =>
        new(d.Id, d.Title, d.Description, d.DocumentType, d.OriginalFileName,
            d.ContentType, d.FileSizeBytes, d.Sha256, d.CoveringYear,
            d.IsPublished, d.PublishedAtUtc, d.UploadedAtUtc,
            storage.GetPublicUrl(d.StorageKey));
}
