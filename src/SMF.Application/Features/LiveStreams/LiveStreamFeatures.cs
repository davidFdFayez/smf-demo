using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SMF.Application.Common.Exceptions;
using SMF.Application.Common.Interfaces;
using SMF.Domain.Entities;
using SMF.Domain.Enums;

namespace SMF.Application.Features.LiveStreams;

// PDF §9 "Advanced Features → Live streaming integration"
// Thin authoring API — events and matches can each carry an optional stream
// URL + provider. The front end picks the embed renderer from the provider.

public sealed record EventLiveStreamDto(
    Guid EventId, LiveStreamProvider? Provider, string? Url);

public sealed record MatchLiveStreamDto(
    Guid MatchId, string MatchCode, LiveStreamProvider? Provider, string? Url);

public sealed record SetEventLiveStreamCommand(
    Guid EventId, LiveStreamProvider? Provider, string? Url) : IRequest<EventLiveStreamDto>;

public sealed class SetEventLiveStreamCommandValidator : AbstractValidator<SetEventLiveStreamCommand>
{
    public SetEventLiveStreamCommandValidator()
    {
        RuleFor(x => x.EventId).NotEmpty();
        RuleFor(x => x.Url).MaximumLength(500);
        When(x => x.Provider is not null, () =>
        {
            RuleFor(x => x.Url).NotEmpty()
                .WithMessage("URL is required when provider is set.");
        });
    }
}

public sealed class SetEventLiveStreamCommandHandler
    : IRequestHandler<SetEventLiveStreamCommand, EventLiveStreamDto>
{
    private readonly IApplicationDbContext _db;
    public SetEventLiveStreamCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<EventLiveStreamDto> Handle(SetEventLiveStreamCommand request, CancellationToken ct)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.Id == request.EventId, ct)
                 ?? throw new NotFoundException(nameof(FederationEvent), request.EventId);
        ev.SetLiveStream(request.Provider, request.Url);
        await _db.SaveChangesAsync(ct);
        return new EventLiveStreamDto(ev.Id, ev.LiveStreamProvider, ev.LiveStreamUrl);
    }
}

public sealed record SetMatchLiveStreamCommand(
    Guid MatchId, LiveStreamProvider? Provider, string? Url) : IRequest<MatchLiveStreamDto>;

public sealed class SetMatchLiveStreamCommandHandler
    : IRequestHandler<SetMatchLiveStreamCommand, MatchLiveStreamDto>
{
    private readonly IApplicationDbContext _db;
    public SetMatchLiveStreamCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<MatchLiveStreamDto> Handle(SetMatchLiveStreamCommand request, CancellationToken ct)
    {
        var match = await _db.Matches.FirstOrDefaultAsync(m => m.Id == request.MatchId, ct)
                    ?? throw new NotFoundException(nameof(Match), request.MatchId);
        match.SetLiveStream(request.Provider, request.Url);
        await _db.SaveChangesAsync(ct);
        return new MatchLiveStreamDto(match.Id, match.Code, match.LiveStreamProvider, match.LiveStreamUrl);
    }
}

public sealed record GetMatchLiveStreamByCodeQuery(string MatchCode) : IRequest<MatchLiveStreamDto>;

public sealed class GetMatchLiveStreamByCodeQueryHandler
    : IRequestHandler<GetMatchLiveStreamByCodeQuery, MatchLiveStreamDto>
{
    private readonly IApplicationDbContext _db;
    public GetMatchLiveStreamByCodeQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<MatchLiveStreamDto> Handle(GetMatchLiveStreamByCodeQuery request, CancellationToken ct)
    {
        var match = await _db.Matches.AsNoTracking()
                        .FirstOrDefaultAsync(m => m.Code == request.MatchCode, ct)
                    ?? throw new NotFoundException(nameof(Match), request.MatchCode);
        return new MatchLiveStreamDto(match.Id, match.Code, match.LiveStreamProvider, match.LiveStreamUrl);
    }
}
