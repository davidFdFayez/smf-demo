using Microsoft.EntityFrameworkCore;
using SMF.Application.Common.Interfaces;
using SMF.Domain.Entities;
using SMF.Domain.Enums;

namespace SMF.Application.Features.Communication.Broadcasts;

/// <summary>One row per recipient resolved for a campaign — the dispatcher
/// uses this to fan a single <see cref="BroadcastCampaign"/> out to many
/// <see cref="Notification"/> rows without re-querying for each channel.</summary>
public sealed record BroadcastRecipient(
    Guid MemberId,
    string FullName,
    string? Email,
    string? PhoneE164,
    IReadOnlyList<string> ActiveDeviceTokens);

/// <summary>
/// Pure projection of the campaign's targeting filters into a recipient set.
/// Lives in the application layer so the admin UI can call it for a "preview
/// audience" count before the campaign is committed.
/// </summary>
public sealed class BroadcastAudienceResolver
{
    private readonly IApplicationDbContext _db;

    public BroadcastAudienceResolver(IApplicationDbContext db) => _db = db;

    public async Task<List<BroadcastRecipient>> ResolveAsync(
        BroadcastCampaign campaign, CancellationToken cancellationToken = default)
    {
        var roles = campaign.ResolveTargetRoles();
        var query = _db.Members.AsNoTracking().AsQueryable();

        if (roles.Count > 0)
            query = query.Where(m => roles.Contains(m.Role));

        if (campaign.ActiveMembersOnly)
            query = query.Where(m => m.RegistrationStatus == RegistrationStatus.Active);

        if (campaign.TargetClubId is { } clubId)
            query = query.Where(m => m.AffiliatedClubId == clubId);

        if (campaign.TargetEventId is { } eventId)
        {
            // Members who hold a registration row against the targeted event.
            query =
                from m in query
                join r in _db.EventRegistrations.AsNoTracking() on m.Id equals r.MemberId
                where r.EventId == eventId
                select m;
        }

        var memberRows = await query
            .Select(m => new
            {
                m.Id,
                m.FullName,
                m.Email,
                m.PhoneNumber
            })
            .ToListAsync(cancellationToken);

        var memberIds = memberRows.Select(m => m.Id).ToList();

        // Pull active push tokens in one extra query; in-memory join keeps the
        // SQL simple and avoids dragging the join column through the parent
        // query for filters that don't need it.
        var deviceLookup = await _db.DeviceRegistrations
            .AsNoTracking()
            .Where(d => d.IsActive && memberIds.Contains(d.MemberId))
            .Select(d => new { d.MemberId, d.Token })
            .ToListAsync(cancellationToken);

        var deviceMap = deviceLookup
            .GroupBy(d => d.MemberId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(x => x.Token).ToList());

        return memberRows.Select(m => new BroadcastRecipient(
            m.Id, m.FullName, m.Email, m.PhoneNumber,
            deviceMap.TryGetValue(m.Id, out var tokens) ? tokens : Array.Empty<string>()))
            .ToList();
    }
}
