using MediatR;
using Microsoft.EntityFrameworkCore;
using SMF.Application.Common.Interfaces;
using SMF.Domain.Enums;

namespace SMF.Application.Features.Admin;

public sealed record AdminStats(
    int TotalMembers,
    IReadOnlyDictionary<string, int> MembersByRole,
    IReadOnlyDictionary<string, int> MembersByStatus,
    int PendingMemberApprovals,
    int TotalClubs,
    int PendingClubApprovals,
    int TotalEvents,
    int UpcomingEvents,
    long MembershipRevenueThisMonthMinor,
    long EventRevenueThisMonthMinor,
    IReadOnlyList<RecentMember> RecentRegistrations);

public sealed record RecentMember(
    Guid Id,
    string FullName,
    string SMF_ID,
    MemberRole Role,
    RegistrationStatus Status,
    DateTime CreatedAtUtc);

public sealed record GetAdminStatsQuery : IRequest<AdminStats>;

public sealed class GetAdminStatsQueryHandler : IRequestHandler<GetAdminStatsQuery, AdminStats>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public GetAdminStatsQueryHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<AdminStats> Handle(GetAdminStatsQuery request, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var members = _db.Members.AsNoTracking();

        var totalMembers = await members.CountAsync(ct);
        var pendingApprovals = await members.CountAsync(
            m => m.RegistrationStatus == RegistrationStatus.Pending, ct);

        var byRole = await members
            .GroupBy(m => m.Role)
            .Select(g => new { Role = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var byStatus = await members
            .GroupBy(m => m.RegistrationStatus)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var totalClubs = await _db.Clubs.AsNoTracking().CountAsync(ct);
        var pendingClubs = await _db.Clubs.AsNoTracking()
            .CountAsync(c => c.Status == ClubStatus.Pending, ct);

        var totalEvents = await _db.Events.AsNoTracking().CountAsync(ct);
        var upcomingEvents = await _db.Events.AsNoTracking()
            .CountAsync(e => e.StartsAtUtc > now && e.Status != EventStatus.Cancelled, ct);

        // Revenue split: "membership" = successful payments for MembershipFee,
        // "event" = successful payments for EventEntry. Keep minor-units.
        var membershipRevenueCents = await _db.Payments.AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Succeeded
                        && p.Purpose == PaymentPurpose.MembershipFee
                        && p.CompletedAtUtc >= monthStart)
            .SumAsync(p => (long?)p.AmountMinor, ct) ?? 0;

        var eventRevenueCents = await _db.Payments.AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Succeeded
                        && p.Purpose == PaymentPurpose.EventFee
                        && p.CompletedAtUtc >= monthStart)
            .SumAsync(p => (long?)p.AmountMinor, ct) ?? 0;

        var recent = await members
            .OrderByDescending(m => m.CreatedAtUtc)
            .Take(10)
            .Select(m => new RecentMember(
                m.Id, m.FullName, m.SMF_ID, m.Role, m.RegistrationStatus, m.CreatedAtUtc))
            .ToListAsync(ct);

        return new AdminStats(
            TotalMembers: totalMembers,
            MembersByRole: byRole.ToDictionary(x => x.Role.ToString(), x => x.Count),
            MembersByStatus: byStatus.ToDictionary(x => x.Status.ToString(), x => x.Count),
            PendingMemberApprovals: pendingApprovals,
            TotalClubs: totalClubs,
            PendingClubApprovals: pendingClubs,
            TotalEvents: totalEvents,
            UpcomingEvents: upcomingEvents,
            MembershipRevenueThisMonthMinor: membershipRevenueCents,
            EventRevenueThisMonthMinor: eventRevenueCents,
            RecentRegistrations: recent);
    }
}
