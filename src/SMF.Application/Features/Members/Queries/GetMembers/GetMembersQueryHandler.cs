using MediatR;
using Microsoft.EntityFrameworkCore;
using SMF.Application.Common.Interfaces;

namespace SMF.Application.Features.Members.Queries.GetMembers;

public sealed class GetMembersQueryHandler
    : IRequestHandler<GetMembersQuery, PagedResult<MemberListItem>>
{
    private readonly IApplicationDbContext _db;

    public GetMembersQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<PagedResult<MemberListItem>> Handle(
        GetMembersQuery request,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var size = Math.Clamp(request.PageSize, 1, 200);
        var skip = (page - 1) * size;

        var q = _db.Members.AsNoTracking();

        // Role / status filters translate directly to SQL predicates.
        if (request.Role is { } role) q = q.Where(m => m.Role == role);
        if (request.Status is { } status) q = q.Where(m => m.RegistrationStatus == status);

        // Free-text search across name, SMF_ID, email, national-id and phone.
        // EF translates `Contains` to SQL LIKE `%term%`.
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            q = q.Where(m =>
                EF.Functions.Like(m.FullName, $"%{term}%")
                || EF.Functions.Like(m.SMF_ID, $"%{term}%")
                || EF.Functions.Like(m.Email, $"%{term}%")
                || EF.Functions.Like(m.NationalId, $"%{term}%")
                || EF.Functions.Like(m.PhoneNumber, $"%{term}%"));
        }

        var total = await q.CountAsync(cancellationToken);

        var items = await q
            .OrderByDescending(m => m.CreatedAtUtc)
            .Skip(skip)
            .Take(size)
            .Select(m => new MemberListItem(
                m.Id,
                m.FullName,
                m.DateOfBirth,
                m.Role,
                m.SMF_ID,
                m.RegistrationStatus,
                m.GuardianConsent,
                m.Email,
                m.PhoneNumber,
                m.NationalId,
                m.CreatedAtUtc,
                m.AffiliatedClubId,
                m.LicenseLevel,
                m.YearsOfExperience,
                m.WeightCategoryKg,
                m.MedicalCleared,
                m.MedicalClearedAtUtc))
            .ToListAsync(cancellationToken);

        return new PagedResult<MemberListItem>(items, total, page, size);
    }
}
