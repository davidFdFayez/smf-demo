using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SMF.Application.Common.Exceptions;
using SMF.Application.Common.Interfaces;
using SMF.Domain.Entities;
using SMF.Domain.Enums;

namespace SMF.Application.Features.Tournaments;

// ─── DTOs ───────────────────────────────────────────────────────────────────

public sealed record BracketMatchDto(
    Guid Id,
    int Round,
    int OrderInRound,
    Guid? ParticipantAId,
    string? ParticipantAName,
    Guid? ParticipantBId,
    string? ParticipantBName,
    BracketMatchStatus Status,
    Guid? WinnerId);

public sealed record TournamentDetails(
    Guid Id,
    Guid EventId,
    string Title,
    string Division,
    TournamentStatus Status,
    DateTime CreatedAtUtc,
    IReadOnlyList<BracketMatchDto> Matches);

// ─── Generate ───────────────────────────────────────────────────────────────

public sealed record GenerateTournamentCommand(
    Guid EventId,
    string Title,
    string Division,
    IReadOnlyList<Guid> ParticipantIds) : IRequest<TournamentDetails>;

public sealed class GenerateTournamentCommandValidator : AbstractValidator<GenerateTournamentCommand>
{
    public GenerateTournamentCommandValidator()
    {
        RuleFor(x => x.EventId).NotEqual(Guid.Empty);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Division).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ParticipantIds)
            .NotEmpty()
            .Must(ids => ids.Count >= 2).WithMessage("A tournament needs at least two participants.")
            .Must(ids => ids.Distinct().Count() == ids.Count).WithMessage("Participants must be unique.");
    }
}

public sealed class GenerateTournamentCommandHandler
    : IRequestHandler<GenerateTournamentCommand, TournamentDetails>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;
    private readonly ITournamentBroadcaster _broadcaster;

    public GenerateTournamentCommandHandler(
        IApplicationDbContext db,
        IDateTimeProvider clock,
        ITournamentBroadcaster broadcaster)
    {
        _db = db;
        _clock = clock;
        _broadcaster = broadcaster;
    }

    public async Task<TournamentDetails> Handle(GenerateTournamentCommand request, CancellationToken ct)
    {
        var eventExists = await _db.Events.AnyAsync(e => e.Id == request.EventId, ct);
        if (!eventExists) throw new NotFoundException("Event", request.EventId);

        // All participants must resolve to real members.
        var existing = await _db.Members.AsNoTracking()
            .Where(m => request.ParticipantIds.Contains(m.Id))
            .Select(m => m.Id).ToListAsync(ct);

        var missing = request.ParticipantIds.Except(existing).ToList();
        if (missing.Count > 0)
            throw new NotFoundException("Member", string.Join(",", missing));

        var tournament = Tournament.Generate(
            request.EventId, request.Title, request.Division,
            request.ParticipantIds, _clock.UtcNow);

        _db.Tournaments.Add(tournament);
        await _db.SaveChangesAsync(ct);

        var details = await TournamentMapper.BuildAsync(_db, tournament.Id, ct);
        await _broadcaster.BroadcastBracketUpdatedAsync(details, ct);
        return details;
    }
}

// ─── Record result ──────────────────────────────────────────────────────────

public sealed record RecordMatchResultCommand(Guid TournamentId, Guid MatchId, bool WinnerIsA) : IRequest;

public sealed class RecordMatchResultCommandHandler : IRequestHandler<RecordMatchResultCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly ITournamentBroadcaster _broadcaster;
    public RecordMatchResultCommandHandler(IApplicationDbContext db, ITournamentBroadcaster broadcaster)
    {
        _db = db;
        _broadcaster = broadcaster;
    }

    public async Task Handle(RecordMatchResultCommand request, CancellationToken ct)
    {
        var t = await _db.Tournaments
            .Include(x => x.Matches)
            .FirstOrDefaultAsync(x => x.Id == request.TournamentId, ct)
            ?? throw new NotFoundException("Tournament", request.TournamentId);

        if (t.Status == TournamentStatus.Draft) t.Start();
        t.RecordResult(request.MatchId, request.WinnerIsA);
        await _db.SaveChangesAsync(ct);

        var details = await TournamentMapper.BuildAsync(_db, t.Id, ct);
        await _broadcaster.BroadcastBracketUpdatedAsync(details, ct);
    }
}

// ─── No-show / walkover ────────────────────────────────────────────────────

public sealed record MarkBracketNoShowCommand(Guid TournamentId, Guid MatchId, bool? WalkoverToA)
    : IRequest;

public sealed class MarkBracketNoShowCommandHandler : IRequestHandler<MarkBracketNoShowCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly ITournamentBroadcaster _broadcaster;
    public MarkBracketNoShowCommandHandler(IApplicationDbContext db, ITournamentBroadcaster broadcaster)
    {
        _db = db;
        _broadcaster = broadcaster;
    }

    public async Task Handle(MarkBracketNoShowCommand request, CancellationToken ct)
    {
        var t = await _db.Tournaments
            .Include(x => x.Matches)
            .FirstOrDefaultAsync(x => x.Id == request.TournamentId, ct)
            ?? throw new NotFoundException("Tournament", request.TournamentId);

        t.MarkNoShow(request.MatchId, request.WalkoverToA);
        await _db.SaveChangesAsync(ct);

        var details = await TournamentMapper.BuildAsync(_db, t.Id, ct);
        await _broadcaster.BroadcastBracketUpdatedAsync(details, ct);
    }
}

// ─── Get ────────────────────────────────────────────────────────────────────

public sealed record GetTournamentQuery(Guid TournamentId) : IRequest<TournamentDetails>;

public sealed class GetTournamentQueryHandler : IRequestHandler<GetTournamentQuery, TournamentDetails>
{
    private readonly IApplicationDbContext _db;
    public GetTournamentQueryHandler(IApplicationDbContext db) => _db = db;

    public Task<TournamentDetails> Handle(GetTournamentQuery request, CancellationToken ct)
        => TournamentMapper.BuildAsync(_db, request.TournamentId, ct);
}

public sealed record ListTournamentsForEventQuery(Guid EventId) : IRequest<IReadOnlyList<TournamentDetails>>;

public sealed class ListTournamentsForEventQueryHandler
    : IRequestHandler<ListTournamentsForEventQuery, IReadOnlyList<TournamentDetails>>
{
    private readonly IApplicationDbContext _db;
    public ListTournamentsForEventQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<TournamentDetails>> Handle(
        ListTournamentsForEventQuery request, CancellationToken ct)
    {
        var ids = await _db.Tournaments.AsNoTracking()
            .Where(t => t.EventId == request.EventId)
            .Select(t => t.Id).ToListAsync(ct);

        var list = new List<TournamentDetails>();
        foreach (var id in ids)
            list.Add(await TournamentMapper.BuildAsync(_db, id, ct));
        return list;
    }
}

internal static class TournamentMapper
{
    public static async Task<TournamentDetails> BuildAsync(
        IApplicationDbContext db, Guid tournamentId, CancellationToken ct)
    {
        var t = await db.Tournaments.AsNoTracking()
            .Include(x => x.Matches)
            .FirstOrDefaultAsync(x => x.Id == tournamentId, ct)
            ?? throw new NotFoundException("Tournament", tournamentId);

        // Single lookup of all participant names referenced in the bracket.
        var participantIds = t.Matches
            .SelectMany(m => new[] { m.ParticipantAId, m.ParticipantBId })
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var names = await db.Members.AsNoTracking()
            .Where(m => participantIds.Contains(m.Id))
            .Select(m => new { m.Id, m.FullName })
            .ToListAsync(ct);

        var nameMap = names.ToDictionary(x => x.Id, x => x.FullName);

        var dtos = t.Matches
            .OrderBy(m => m.Round).ThenBy(m => m.OrderInRound)
            .Select(m => new BracketMatchDto(
                m.Id, m.Round, m.OrderInRound,
                m.ParticipantAId,
                m.ParticipantAId is { } a && nameMap.TryGetValue(a, out var an) ? an : null,
                m.ParticipantBId,
                m.ParticipantBId is { } b && nameMap.TryGetValue(b, out var bn) ? bn : null,
                m.Status, m.Winner))
            .ToList();

        return new TournamentDetails(t.Id, t.EventId, t.Title, t.Division, t.Status, t.CreatedAtUtc, dtos);
    }
}
