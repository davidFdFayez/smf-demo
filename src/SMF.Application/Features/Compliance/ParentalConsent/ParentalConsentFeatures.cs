using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMF.Application.Common.Interfaces;
using SMF.Application.Features.Compliance.Common;
using SMF.Application.Features.Compliance.Policies;
using SMF.Domain.Entities;
using SMF.Domain.Enums;

namespace SMF.Application.Features.Compliance.ParentalConsent;

/// <summary>
/// Parental-consent ceremony for minor athletes. Issues a one-shot signing
/// link to the guardian, records every touchpoint in <see cref="ConsentLog"/>,
/// and sets <c>Member.GuardianConsent</c> to true on Approve so the existing
/// admission checks treat the registration as valid.
/// </summary>
public static class ParentalConsentFeatures
{
    // ────────── issue (called from RegisterMember when DOB &lt; 18) ────
    public sealed record IssueParentalConsentCommand(
        Guid MemberId,
        string GuardianFullName,
        GuardianRelation Relation,
        string GuardianEmail,
        string GuardianPhone,
        string? GuardianNationalId,
        string? IpAddress,
        string? UserAgent
    ) : IRequest<IssueParentalConsentResult>;

    public sealed record IssueParentalConsentResult(
        Guid ConsentId,
        DateTime TokenExpiresAtUtc,
        string GuardianSignUrl);

    public sealed class IssueValidator : AbstractValidator<IssueParentalConsentCommand>
    {
        public IssueValidator()
        {
            RuleFor(x => x.GuardianFullName).NotEmpty().MaximumLength(200);
            RuleFor(x => x.GuardianEmail).NotEmpty().EmailAddress().MaximumLength(200);
            RuleFor(x => x.GuardianPhone).NotEmpty().MaximumLength(32);
            RuleFor(x => x.Relation).IsInEnum();
        }
    }

    public sealed class IssueHandler : IRequestHandler<IssueParentalConsentCommand, IssueParentalConsentResult>
    {
        private readonly IApplicationDbContext _db;
        private readonly IConsentSigner _signer;
        private readonly INotificationService _notifications;
        private readonly IComplianceUrlBuilder _urls;
        private readonly ILogger<IssueHandler> _logger;

        public IssueHandler(
            IApplicationDbContext db, IConsentSigner signer, INotificationService notifications,
            IComplianceUrlBuilder urls, ILogger<IssueHandler> logger)
        {
            _db = db; _signer = signer; _notifications = notifications;
            _urls = urls; _logger = logger;
        }

        public async Task<IssueParentalConsentResult> Handle(IssueParentalConsentCommand request, CancellationToken ct)
        {
            var member = await _db.Members.FirstOrDefaultAsync(m => m.Id == request.MemberId, ct)
                         ?? throw new KeyNotFoundException($"Member {request.MemberId} not found.");

            var rawToken = GenerateToken();
            var tokenHash = _signer.HashToken(rawToken);

            // Pin the active Code-of-Conduct version at issue time so the
            // guardian sees exactly the same wording at sign time even if
            // editors publish a new version mid-flight.
            var policy = await _db.PolicyDocuments
                .Where(p => p.Kind == PolicyDocumentKind.CodeOfConduct && p.IsActive)
                .OrderByDescending(p => p.EffectiveAtUtc)
                .FirstOrDefaultAsync(ct);

            var consent = SMF.Domain.Entities.ParentalConsent.Issue(
                member.Id, request.GuardianFullName, request.Relation,
                request.GuardianEmail, request.GuardianPhone, request.GuardianNationalId,
                tokenHash, DateTime.UtcNow.AddDays(7),
                policy?.Id, policy?.Version);

            _db.ParentalConsents.Add(consent);

            _db.ConsentLogs.Add(ConsentLog.Record(
                ConsentEventType.GuardianLinkIssued,
                member.Id, policy?.Id, consent.Id,
                policy?.Kind.ToString(), policy?.Version, policy?.ContentHash,
                request.IpAddress, request.UserAgent,
                detailsJson: $"{{\"guardianEmail\":\"{request.GuardianEmail}\"}}",
                signatureHmac: _signer.Sign($"issue|{consent.Id}|{member.Id}|{consent.CreatedAtUtc:O}")));

            await _db.SaveChangesAsync(ct);

            var url = _urls.BuildGuardianSigningUrl(consent.Id, rawToken);

            // Fire-and-forget email; failures are logged but don't roll the
            // ceremony back. Operators can re-send via the admin panel.
            try
            {
                await _notifications.SendAsync(new NotificationMessage(
                    NotificationChannel.Email, request.GuardianEmail,
                    "Action required: parental consent for federation registration",
                    BuildEmailBody(member.FullName, request.GuardianFullName, url, consent.TokenExpiresAtUtc)), ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send parental consent email for consent {ConsentId}", consent.Id);
            }

            return new IssueParentalConsentResult(consent.Id, consent.TokenExpiresAtUtc, url);
        }

        private static string BuildEmailBody(string member, string guardian, string url, DateTime expiresAtUtc) =>
            $"Dear {guardian},\n\nThe Saudi MuayThai Federation has received a registration for {member}, " +
            "who is under 18. As their parent or legal guardian, your consent is required for the registration " +
            "to proceed.\n\n" +
            $"Please review and sign the consent form using this secure link:\n{url}\n\n" +
            $"The link expires at {expiresAtUtc:u} UTC.\n\n" +
            "If you didn't expect this email, please ignore it.\n\nFederation Office";
    }

    // ────────── retrieve task by token ────────────────────
    public sealed record GetGuardianTaskQuery(Guid ConsentId, string Token) : IRequest<GuardianTaskDto?>;
    public sealed class GetTaskHandler : IRequestHandler<GetGuardianTaskQuery, GuardianTaskDto?>
    {
        private readonly IApplicationDbContext _db;
        private readonly IConsentSigner _signer;
        public GetTaskHandler(IApplicationDbContext db, IConsentSigner signer) { _db = db; _signer = signer; }

        public async Task<GuardianTaskDto?> Handle(GetGuardianTaskQuery request, CancellationToken ct)
        {
            var consent = await _db.ParentalConsents.FirstOrDefaultAsync(c => c.Id == request.ConsentId, ct);
            if (consent is null) return null;
            if (!ConstantTimeEquals(consent.TokenHash, _signer.HashToken(request.Token))) return null;

            var member = await _db.Members.AsNoTracking().FirstOrDefaultAsync(m => m.Id == consent.MemberId, ct);
            if (member is null) return null;

            PolicyDocumentDto? policyDto = null;
            if (consent.PolicyDocumentId.HasValue)
            {
                var policy = await _db.PolicyDocuments.AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == consent.PolicyDocumentId.Value, ct);
                if (policy != null) policyDto = PolicyFeatures.Map(policy);
            }

            var alreadyDecided = consent.Status != ParentalConsentStatus.Pending;
            var expired = DateTime.UtcNow > consent.TokenExpiresAtUtc;

            // Best-effort "opened" event the first time a guardian fetches
            // the form. Idempotent — the entity ignores subsequent calls.
            if (!alreadyDecided && !expired && consent.OpenedAtUtc is null)
            {
                consent.MarkOpened();
                _db.ConsentLogs.Add(ConsentLog.Record(
                    ConsentEventType.GuardianLinkOpened,
                    consent.MemberId, consent.PolicyDocumentId, consent.Id,
                    consent.PolicyVersion is null ? null : PolicyDocumentKind.CodeOfConduct.ToString(),
                    consent.PolicyVersion, contentHash: null,
                    ipAddress: null, userAgent: null,
                    detailsJson: null,
                    signatureHmac: _signer.Sign($"opened|{consent.Id}|{DateTime.UtcNow:O}")));
                await _db.SaveChangesAsync(ct);
            }

            return new GuardianTaskDto(
                consent.Id, member.FullName, member.DateOfBirth,
                consent.GuardianFullName, consent.Relation,
                consent.TokenExpiresAtUtc, policyDto, expired, alreadyDecided);
        }
    }

    // ────────── decide (approve / decline) ────────────────
    public sealed record DecideParentalConsentCommand(
        Guid ConsentId, string Token, bool Approve, string? DeclineReason,
        string? IpAddress, string? UserAgent
    ) : IRequest<ParentalConsentDto>;

    public sealed class DecideValidator : AbstractValidator<DecideParentalConsentCommand>
    {
        public DecideValidator()
        {
            RuleFor(x => x.Token).NotEmpty();
            RuleFor(x => x.DeclineReason).NotEmpty().MaximumLength(2000)
                .When(x => !x.Approve)
                .WithMessage("A decline reason is required.");
        }
    }

    public sealed class DecideHandler : IRequestHandler<DecideParentalConsentCommand, ParentalConsentDto>
    {
        private readonly IApplicationDbContext _db;
        private readonly IConsentSigner _signer;
        public DecideHandler(IApplicationDbContext db, IConsentSigner signer) { _db = db; _signer = signer; }

        public async Task<ParentalConsentDto> Handle(DecideParentalConsentCommand request, CancellationToken ct)
        {
            var consent = await _db.ParentalConsents.FirstOrDefaultAsync(c => c.Id == request.ConsentId, ct)
                          ?? throw new KeyNotFoundException("Consent record not found.");
            if (!ConstantTimeEquals(consent.TokenHash, _signer.HashToken(request.Token)))
                throw new UnauthorizedAccessException("Token does not match.");

            var member = await _db.Members.FirstOrDefaultAsync(m => m.Id == consent.MemberId, ct)
                         ?? throw new KeyNotFoundException("Member not found.");

            var payload = $"{(request.Approve ? "approve" : "decline")}|{consent.Id}|{member.Id}|{DateTime.UtcNow:O}";
            var hmac = _signer.Sign(payload);

            if (request.Approve)
            {
                consent.Approve(request.IpAddress, request.UserAgent, hmac);
                member.RecordGuardianConsentApproval();
                _db.ConsentLogs.Add(ConsentLog.Record(
                    ConsentEventType.GuardianApproved,
                    member.Id, consent.PolicyDocumentId, consent.Id,
                    PolicyDocumentKind.CodeOfConduct.ToString(),
                    consent.PolicyVersion, contentHash: null,
                    request.IpAddress, request.UserAgent, detailsJson: null,
                    signatureHmac: hmac));
            }
            else
            {
                consent.Decline(request.DeclineReason ?? "(none)", request.IpAddress, request.UserAgent, hmac);
                _db.ConsentLogs.Add(ConsentLog.Record(
                    ConsentEventType.GuardianDeclined,
                    member.Id, consent.PolicyDocumentId, consent.Id,
                    PolicyDocumentKind.CodeOfConduct.ToString(),
                    consent.PolicyVersion, contentHash: null,
                    request.IpAddress, request.UserAgent,
                    detailsJson: $"{{\"reason\":{System.Text.Json.JsonSerializer.Serialize(request.DeclineReason ?? "")}}}",
                    signatureHmac: hmac));
            }

            await _db.SaveChangesAsync(ct);

            return Map(consent, member.FullName);
        }
    }

    // ────────── admin queries ─────────────────────────────
    public sealed record ListConsentsQuery(ParentalConsentStatus? Status, int Page, int PageSize)
        : IRequest<PagedConsentResult>;
    public sealed record PagedConsentResult(
        IReadOnlyList<ParentalConsentDto> Items, int TotalCount, int Page, int PageSize);

    public sealed class ListHandler : IRequestHandler<ListConsentsQuery, PagedConsentResult>
    {
        private readonly IApplicationDbContext _db;
        public ListHandler(IApplicationDbContext db) => _db = db;
        public async Task<PagedConsentResult> Handle(ListConsentsQuery request, CancellationToken ct)
        {
            var q = from c in _db.ParentalConsents.AsNoTracking()
                    join m in _db.Members.AsNoTracking() on c.MemberId equals m.Id
                    where !request.Status.HasValue || c.Status == request.Status.Value
                    orderby c.CreatedAtUtc descending
                    select Map(c, m.FullName);

            var total = await q.CountAsync(ct);
            var page = Math.Max(1, request.Page);
            var size = Math.Clamp(request.PageSize, 1, 200);
            var items = await q.Skip((page - 1) * size).Take(size).ToListAsync(ct);
            return new PagedConsentResult(items, total, page, size);
        }
    }

    public sealed record ListConsentLogsQuery(Guid? MemberId, Guid? ConsentId, int Page, int PageSize)
        : IRequest<PagedConsentLogResult>;
    public sealed record PagedConsentLogResult(
        IReadOnlyList<ConsentLogDto> Items, int TotalCount, int Page, int PageSize);

    public sealed class ListLogsHandler : IRequestHandler<ListConsentLogsQuery, PagedConsentLogResult>
    {
        private readonly IApplicationDbContext _db;
        public ListLogsHandler(IApplicationDbContext db) => _db = db;
        public async Task<PagedConsentLogResult> Handle(ListConsentLogsQuery request, CancellationToken ct)
        {
            IQueryable<ConsentLog> q = _db.ConsentLogs.AsNoTracking();
            if (request.MemberId.HasValue) q = q.Where(l => l.MemberId == request.MemberId.Value);
            if (request.ConsentId.HasValue) q = q.Where(l => l.ParentalConsentId == request.ConsentId.Value);
            q = q.OrderByDescending(l => l.OccurredAtUtc);

            var total = await q.CountAsync(ct);
            var page = Math.Max(1, request.Page);
            var size = Math.Clamp(request.PageSize, 1, 500);
            var rows = await q.Skip((page - 1) * size).Take(size).ToListAsync(ct);

            var items = rows.Select(l => new ConsentLogDto(
                l.Id, l.OccurredAtUtc, l.EventType, l.MemberId, l.PolicyDocumentId,
                l.ParentalConsentId, l.PolicyKind, l.PolicyVersion, l.IpAddress, l.DetailsJson)).ToList();
            return new PagedConsentLogResult(items, total, page, size);
        }
    }

    internal static ParentalConsentDto Map(SMF.Domain.Entities.ParentalConsent c, string memberName) =>
        new(c.Id, c.MemberId, memberName, c.Status,
            c.GuardianFullName, c.Relation, c.GuardianEmail, c.GuardianPhone,
            c.CreatedAtUtc, c.TokenExpiresAtUtc, c.OpenedAtUtc, c.DecidedAtUtc, c.DeclineReason);

    private static string GenerateToken()
    {
        // 256 bits of entropy, URL-safe.
        Span<byte> buf = stackalloc byte[32];
        System.Security.Cryptography.RandomNumberGenerator.Fill(buf);
        return Convert.ToBase64String(buf).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    private static bool ConstantTimeEquals(string a, string b)
    {
        if (a.Length != b.Length) return false;
        var diff = 0;
        for (var i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
        return diff == 0;
    }
}

/// <summary>Resolves user-facing URLs for compliance flows. Implementation
/// lives in the API layer because base URLs depend on the current request.</summary>
public interface IComplianceUrlBuilder
{
    string BuildGuardianSigningUrl(Guid consentId, string token);
}
