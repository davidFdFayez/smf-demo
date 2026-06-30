using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SMF.Application.Common.Exceptions;
using SMF.Application.Common.Interfaces;
using SMF.Domain.Entities;
using SMF.Domain.Enums;

namespace SMF.Application.Features.Certificates;

public sealed record CertificateSummary(
    Guid Id,
    Guid MemberId,
    CertificateType Type,
    string Title,
    string? IssuingAuthority,
    string VerificationCode,
    DateTime IssuedAtUtc,
    DateTime? ExpiresAtUtc,
    bool IsRevoked);

// ─── Issue ─────────────────────────────────────────────────────────────────

public sealed record IssueCertificateCommand(
    Guid MemberId,
    CertificateType Type,
    string Title,
    string? IssuingAuthority,
    DateTime? ExpiresAtUtc) : IRequest<CertificateSummary>;

public sealed class IssueCertificateCommandValidator : AbstractValidator<IssueCertificateCommand>
{
    public IssueCertificateCommandValidator()
    {
        RuleFor(x => x.MemberId).NotEqual(Guid.Empty);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.IssuingAuthority).MaximumLength(200)
            .When(x => !string.IsNullOrWhiteSpace(x.IssuingAuthority));
    }
}

public sealed class IssueCertificateCommandHandler
    : IRequestHandler<IssueCertificateCommand, CertificateSummary>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public IssueCertificateCommandHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<CertificateSummary> Handle(IssueCertificateCommand request, CancellationToken ct)
    {
        var memberExists = await _db.Members.AnyAsync(m => m.Id == request.MemberId, ct);
        if (!memberExists) throw new NotFoundException("Member", request.MemberId);

        var cert = Certificate.Issue(
            request.MemberId, request.Type, request.Title,
            request.IssuingAuthority, _clock.UtcNow, request.ExpiresAtUtc);

        _db.Certificates.Add(cert);
        await _db.SaveChangesAsync(ct);

        return Map(cert);
    }

    internal static CertificateSummary Map(Certificate c) => new(
        c.Id, c.MemberId, c.Type, c.Title, c.IssuingAuthority,
        c.VerificationCode, c.IssuedAtUtc, c.ExpiresAtUtc, c.IsRevoked);
}

// ─── List for member ───────────────────────────────────────────────────────

public sealed record ListMemberCertificatesQuery(Guid MemberId)
    : IRequest<IReadOnlyList<CertificateSummary>>;

public sealed class ListMemberCertificatesQueryHandler
    : IRequestHandler<ListMemberCertificatesQuery, IReadOnlyList<CertificateSummary>>
{
    private readonly IApplicationDbContext _db;
    public ListMemberCertificatesQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<CertificateSummary>> Handle(
        ListMemberCertificatesQuery request, CancellationToken ct)
    {
        var certs = await _db.Certificates.AsNoTracking()
            .Where(c => c.MemberId == request.MemberId)
            .OrderByDescending(c => c.IssuedAtUtc)
            .ToListAsync(ct);
        return certs.Select(IssueCertificateCommandHandler.Map).ToList();
    }
}

// ─── Download (render HTML/PDF) ────────────────────────────────────────────

public sealed record DownloadCertificateQuery(Guid CertificateId) : IRequest<CertificateDocument>;

public sealed class DownloadCertificateQueryHandler
    : IRequestHandler<DownloadCertificateQuery, CertificateDocument>
{
    private readonly IApplicationDbContext _db;
    private readonly ICertificateRenderer _renderer;

    public DownloadCertificateQueryHandler(IApplicationDbContext db, ICertificateRenderer renderer)
    {
        _db = db;
        _renderer = renderer;
    }

    public async Task<CertificateDocument> Handle(DownloadCertificateQuery request, CancellationToken ct)
    {
        var cert = await _db.Certificates.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.CertificateId, ct)
            ?? throw new NotFoundException("Certificate", request.CertificateId);

        var member = await _db.Members.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == cert.MemberId, ct)
            ?? throw new NotFoundException("Member", cert.MemberId);

        return await _renderer.RenderAsync(cert, member, ct);
    }
}

// ─── Verify by public code ────────────────────────────────────────────────

public sealed record VerifyCertificateQuery(string VerificationCode)
    : IRequest<CertificateSummary>;

public sealed class VerifyCertificateQueryHandler
    : IRequestHandler<VerifyCertificateQuery, CertificateSummary>
{
    private readonly IApplicationDbContext _db;
    public VerifyCertificateQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<CertificateSummary> Handle(VerifyCertificateQuery request, CancellationToken ct)
    {
        var cert = await _db.Certificates.AsNoTracking()
            .FirstOrDefaultAsync(c => c.VerificationCode == request.VerificationCode, ct)
            ?? throw new NotFoundException("Certificate", request.VerificationCode);
        return IssueCertificateCommandHandler.Map(cert);
    }
}
