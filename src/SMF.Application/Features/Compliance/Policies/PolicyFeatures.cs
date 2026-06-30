using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SMF.Application.Common.Interfaces;
using SMF.Application.Features.Compliance.Common;
using SMF.Domain.Entities;
using SMF.Domain.Enums;

namespace SMF.Application.Features.Compliance.Policies;

/// <summary>
/// Policy version management + acceptance ceremony. Every accepted policy
/// row is bound to an immutable <see cref="PolicyDocument"/> so an audit
/// query can answer "exactly which words did this user agree to?".
/// </summary>
public static class PolicyFeatures
{
    // ────────── publish a new version ─────────────────────
    public sealed record PublishPolicyVersionCommand(
        PolicyDocumentKind Kind,
        string Version,
        string Title,
        string BodyMarkdown,
        DateTime EffectiveAtUtc,
        Guid? PublishedByMemberId
    ) : IRequest<PolicyDocumentDto>;

    public sealed class PublishValidator : AbstractValidator<PublishPolicyVersionCommand>
    {
        public PublishValidator()
        {
            RuleFor(x => x.Version).NotEmpty().MaximumLength(40);
            RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
            RuleFor(x => x.BodyMarkdown).NotEmpty();
        }
    }

    public sealed class PublishHandler : IRequestHandler<PublishPolicyVersionCommand, PolicyDocumentDto>
    {
        private readonly IApplicationDbContext _db;
        public PublishHandler(IApplicationDbContext db) => _db = db;

        public async Task<PolicyDocumentDto> Handle(PublishPolicyVersionCommand request, CancellationToken ct)
        {
            // Retire the previous active version of this kind. SOPC needs at
            // most one "current" version per kind so the registration form
            // can serve a deterministic copy.
            var previous = await _db.PolicyDocuments
                .Where(p => p.Kind == request.Kind && p.IsActive)
                .ToListAsync(ct);
            foreach (var p in previous) p.Retire();

            var doc = PolicyDocument.Publish(
                request.Kind, request.Version, request.Title,
                request.BodyMarkdown, request.EffectiveAtUtc, request.PublishedByMemberId);
            _db.PolicyDocuments.Add(doc);
            await _db.SaveChangesAsync(ct);
            return Map(doc);
        }
    }

    // ────────── queries ───────────────────────────────────
    public sealed record GetActivePoliciesQuery() : IRequest<IReadOnlyList<PolicyDocumentDto>>;

    public sealed class GetActiveHandler : IRequestHandler<GetActivePoliciesQuery, IReadOnlyList<PolicyDocumentDto>>
    {
        private readonly IApplicationDbContext _db;
        public GetActiveHandler(IApplicationDbContext db) => _db = db;

        public async Task<IReadOnlyList<PolicyDocumentDto>> Handle(GetActivePoliciesQuery request, CancellationToken ct)
        {
            var docs = await _db.PolicyDocuments
                .AsNoTracking()
                .Where(p => p.IsActive && p.EffectiveAtUtc <= DateTime.UtcNow)
                .OrderBy(p => p.Kind)
                .ThenByDescending(p => p.EffectiveAtUtc)
                .ToListAsync(ct);
            return docs.Select(Map).ToList();
        }
    }

    public sealed record ListPolicyVersionsQuery(PolicyDocumentKind? Kind) : IRequest<IReadOnlyList<PolicyDocumentDto>>;
    public sealed class ListVersionsHandler : IRequestHandler<ListPolicyVersionsQuery, IReadOnlyList<PolicyDocumentDto>>
    {
        private readonly IApplicationDbContext _db;
        public ListVersionsHandler(IApplicationDbContext db) => _db = db;

        public async Task<IReadOnlyList<PolicyDocumentDto>> Handle(ListPolicyVersionsQuery request, CancellationToken ct)
        {
            IQueryable<PolicyDocument> q = _db.PolicyDocuments.AsNoTracking();
            if (request.Kind.HasValue) q = q.Where(p => p.Kind == request.Kind.Value);

            var rows = await q
                .OrderBy(p => p.Kind)
                .ThenByDescending(p => p.EffectiveAtUtc)
                .ToListAsync(ct);
            return rows.Select(Map).ToList();
        }
    }

    // ────────── acceptance (post-login or first-touch) ────
    public sealed record AcceptPoliciesCommand(
        Guid MemberId,
        IReadOnlyList<Guid> PolicyDocumentIds,
        string? IpAddress,
        string? UserAgent
    ) : IRequest<IReadOnlyList<PolicyAcceptanceDto>>;

    public sealed class AcceptHandler : IRequestHandler<AcceptPoliciesCommand, IReadOnlyList<PolicyAcceptanceDto>>
    {
        private readonly IApplicationDbContext _db;
        private readonly IConsentSigner _signer;
        public AcceptHandler(IApplicationDbContext db, IConsentSigner signer) { _db = db; _signer = signer; }

        public async Task<IReadOnlyList<PolicyAcceptanceDto>> Handle(AcceptPoliciesCommand request, CancellationToken ct)
        {
            if (!request.PolicyDocumentIds.Any())
                throw new ArgumentException("At least one policy id must be supplied.", nameof(request));

            var member = await _db.Members.FirstOrDefaultAsync(m => m.Id == request.MemberId, ct)
                         ?? throw new KeyNotFoundException($"Member {request.MemberId} not found.");

            var policies = await _db.PolicyDocuments
                .Where(p => request.PolicyDocumentIds.Contains(p.Id))
                .ToListAsync(ct);

            var results = new List<PolicyAcceptanceDto>();
            foreach (var policy in policies)
            {
                var payload = string.Join('|',
                    request.MemberId, policy.Kind, policy.Version, policy.ContentHash,
                    DateTime.UtcNow.ToString("O"));
                var hmac = _signer.Sign(payload);

                var row = PolicyAcceptance.Record(
                    request.MemberId, policy, request.IpAddress, request.UserAgent, hmac);
                _db.PolicyAcceptances.Add(row);

                _db.ConsentLogs.Add(ConsentLog.Record(
                    ConsentEventType.PolicyAccepted,
                    request.MemberId, policy.Id, parentalConsentId: null,
                    policy.Kind.ToString(), policy.Version, policy.ContentHash,
                    request.IpAddress, request.UserAgent,
                    detailsJson: null, signatureHmac: hmac));

                results.Add(new PolicyAcceptanceDto(
                    row.Id, request.MemberId, member.FullName,
                    row.PolicyKind, row.PolicyVersion, row.ContentHash,
                    row.AcceptedAtUtc, row.IpAddress));
            }

            await _db.SaveChangesAsync(ct);
            return results;
        }
    }

    // ────────── admin audit query ─────────────────────────
    public sealed record ListAcceptancesQuery(
        Guid? MemberId, PolicyDocumentKind? Kind, int Page, int PageSize)
        : IRequest<PagedAcceptanceResult>;
    public sealed record PagedAcceptanceResult(
        IReadOnlyList<PolicyAcceptanceDto> Items, int TotalCount, int Page, int PageSize);

    public sealed class ListAcceptanceHandler : IRequestHandler<ListAcceptancesQuery, PagedAcceptanceResult>
    {
        private readonly IApplicationDbContext _db;
        public ListAcceptanceHandler(IApplicationDbContext db) => _db = db;

        public async Task<PagedAcceptanceResult> Handle(ListAcceptancesQuery request, CancellationToken ct)
        {
            var q = from a in _db.PolicyAcceptances.AsNoTracking()
                    join m in _db.Members.AsNoTracking() on a.MemberId equals m.Id
                    where !request.MemberId.HasValue || a.MemberId == request.MemberId.Value
                    where !request.Kind.HasValue || a.PolicyKind == request.Kind.Value
                    orderby a.AcceptedAtUtc descending
                    select new PolicyAcceptanceDto(
                        a.Id, a.MemberId, m.FullName,
                        a.PolicyKind, a.PolicyVersion, a.ContentHash,
                        a.AcceptedAtUtc, a.IpAddress);

            var total = await q.CountAsync(ct);
            var page = Math.Max(1, request.Page);
            var size = Math.Clamp(request.PageSize, 1, 200);
            var items = await q.Skip((page - 1) * size).Take(size).ToListAsync(ct);
            return new PagedAcceptanceResult(items, total, page, size);
        }
    }

    internal static PolicyDocumentDto Map(PolicyDocument p) =>
        new(p.Id, p.Kind, p.Version, p.Title, p.BodyMarkdown, p.ContentHash,
            p.IsActive, p.EffectiveAtUtc, p.CreatedAtUtc);
}
