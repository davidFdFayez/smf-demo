using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SMF.Application.Common.Exceptions;
using SMF.Application.Common.Interfaces;
using SMF.Domain.Entities;
using SMF.Domain.Enums;

namespace SMF.Application.Features.Events;

// ─── DTOs ───────────────────────────────────────────────────────────────────

public sealed record EventSummary(
    Guid Id,
    string Title,
    string? Description,
    string Location,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    DateTime RegistrationOpensAtUtc,
    DateTime RegistrationClosesAtUtc,
    long EntryFeeMinor,
    string Currency,
    int? Capacity,
    EventStatus Status,
    int RegistrationCount,
    LiveStreamProvider? LiveStreamProvider,
    string? LiveStreamUrl);

public sealed record EventRegistrationItem(
    Guid Id,
    Guid EventId,
    Guid MemberId,
    string MemberFullName,
    string MemberSmfId,
    EventRegistrationStatus Status,
    DateTime CreatedAtUtc,
    DateTime? CheckedInAtUtc);

// ─── Create ────────────────────────────────────────────────────────────────

public sealed record CreateEventCommand(
    string Title,
    string? Description,
    string Location,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    DateTime RegistrationOpensAtUtc,
    DateTime RegistrationClosesAtUtc,
    long EntryFeeMinor,
    string Currency,
    int? Capacity) : IRequest<EventSummary>;

public sealed class CreateEventCommandValidator : AbstractValidator<CreateEventCommand>
{
    public CreateEventCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Location).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(4000)
            .When(x => !string.IsNullOrWhiteSpace(x.Description));
        RuleFor(x => x.Currency).NotEmpty().Length(3);
        RuleFor(x => x.EntryFeeMinor).GreaterThanOrEqualTo(0);
        RuleFor(x => x.EndsAtUtc).GreaterThan(x => x.StartsAtUtc)
            .WithMessage("Event end must be after start.");
        RuleFor(x => x.RegistrationClosesAtUtc).GreaterThan(x => x.RegistrationOpensAtUtc)
            .WithMessage("Registration close must be after open.");
        RuleFor(x => x.Capacity).GreaterThan(0)
            .When(x => x.Capacity.HasValue)
            .WithMessage("Capacity must be positive.");
    }
}

public sealed class CreateEventCommandHandler : IRequestHandler<CreateEventCommand, EventSummary>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public CreateEventCommandHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<EventSummary> Handle(CreateEventCommand request, CancellationToken ct)
    {
        var ev = FederationEvent.Draft(
            request.Title, request.Description, request.Location,
            request.StartsAtUtc, request.EndsAtUtc,
            request.RegistrationOpensAtUtc, request.RegistrationClosesAtUtc,
            request.EntryFeeMinor, request.Currency, request.Capacity,
            _clock.UtcNow);

        _db.Events.Add(ev);
        await _db.SaveChangesAsync(ct);

        return EventMapper.ToSummary(ev);
    }
}

// ─── Update ─────────────────────────────────────────────────────────────────

public sealed record UpdateEventCommand(
    Guid EventId,
    string Title,
    string? Description,
    string Location,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    DateTime RegistrationOpensAtUtc,
    DateTime RegistrationClosesAtUtc,
    long EntryFeeMinor,
    string Currency,
    int? Capacity) : IRequest<EventSummary>;

public sealed class UpdateEventCommandValidator : AbstractValidator<UpdateEventCommand>
{
    public UpdateEventCommandValidator()
    {
        RuleFor(x => x.EventId).NotEqual(Guid.Empty);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Location).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(4000)
            .When(x => !string.IsNullOrWhiteSpace(x.Description));
        RuleFor(x => x.Currency).NotEmpty().Length(3);
        RuleFor(x => x.EntryFeeMinor).GreaterThanOrEqualTo(0);
        RuleFor(x => x.EndsAtUtc).GreaterThan(x => x.StartsAtUtc);
        RuleFor(x => x.RegistrationClosesAtUtc).GreaterThan(x => x.RegistrationOpensAtUtc);
        RuleFor(x => x.Capacity).GreaterThan(0).When(x => x.Capacity.HasValue);
    }
}

public sealed class UpdateEventCommandHandler : IRequestHandler<UpdateEventCommand, EventSummary>
{
    private readonly IApplicationDbContext _db;
    public UpdateEventCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<EventSummary> Handle(UpdateEventCommand request, CancellationToken ct)
    {
        var ev = await _db.Events
            .Include(e => e.Registrations)
            .FirstOrDefaultAsync(e => e.Id == request.EventId, ct)
            ?? throw new NotFoundException("Event", request.EventId);

        ev.UpdateDetails(
            request.Title, request.Description, request.Location,
            request.StartsAtUtc, request.EndsAtUtc,
            request.RegistrationOpensAtUtc, request.RegistrationClosesAtUtc,
            request.EntryFeeMinor, request.Currency, request.Capacity);

        await _db.SaveChangesAsync(ct);
        return EventMapper.ToSummary(ev);
    }
}

// ─── Publish / Cancel ───────────────────────────────────────────────────────

public sealed record PublishEventCommand(Guid EventId) : IRequest;

public sealed class PublishEventCommandHandler : IRequestHandler<PublishEventCommand>
{
    private readonly IApplicationDbContext _db;
    public PublishEventCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(PublishEventCommand request, CancellationToken ct)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.Id == request.EventId, ct)
            ?? throw new NotFoundException("Event", request.EventId);
        ev.Publish();
        ev.OpenRegistration();
        await _db.SaveChangesAsync(ct);
    }
}

public sealed record CancelEventCommand(Guid EventId) : IRequest;

public sealed class CancelEventCommandHandler : IRequestHandler<CancelEventCommand>
{
    private readonly IApplicationDbContext _db;
    public CancelEventCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(CancelEventCommand request, CancellationToken ct)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.Id == request.EventId, ct)
            ?? throw new NotFoundException("Event", request.EventId);
        ev.Cancel();
        await _db.SaveChangesAsync(ct);
    }
}

// ─── Register a member for an event ────────────────────────────────────────

public sealed record RegisterForEventCommand(Guid EventId, Guid MemberId) : IRequest<Guid>;

public sealed class RegisterForEventCommandValidator : AbstractValidator<RegisterForEventCommand>
{
    public RegisterForEventCommandValidator()
    {
        RuleFor(x => x.EventId).NotEqual(Guid.Empty);
        RuleFor(x => x.MemberId).NotEqual(Guid.Empty);
    }
}

public sealed class RegisterForEventCommandHandler : IRequestHandler<RegisterForEventCommand, Guid>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public RegisterForEventCommandHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<Guid> Handle(RegisterForEventCommand request, CancellationToken ct)
    {
        var ev = await _db.Events
            .Include(e => e.Registrations)
            .FirstOrDefaultAsync(e => e.Id == request.EventId, ct)
            ?? throw new NotFoundException("Event", request.EventId);

        var memberExists = await _db.Members.AnyAsync(m => m.Id == request.MemberId, ct);
        if (!memberExists) throw new NotFoundException("Member", request.MemberId);

        var reg = ev.RegisterMember(request.MemberId, _clock.UtcNow);

        // Free events confirm immediately; paid events leave the registration
        // in Pending status so the Payments flow can move it to Confirmed
        // on webhook success. This keeps us from accidentally checking people
        // into paid events they haven't actually paid for.
        if (ev.EntryFeeMinor == 0) reg.Confirm();

        await _db.SaveChangesAsync(ct);
        return reg.Id;
    }
}

// ─── Mark no-show / check-in ────────────────────────────────────────────────

public sealed record CheckInRegistrationCommand(Guid RegistrationId) : IRequest;

public sealed class CheckInRegistrationCommandHandler : IRequestHandler<CheckInRegistrationCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;
    public CheckInRegistrationCommandHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db; _clock = clock;
    }

    public async Task Handle(CheckInRegistrationCommand request, CancellationToken ct)
    {
        var reg = await _db.EventRegistrations.FirstOrDefaultAsync(r => r.Id == request.RegistrationId, ct)
            ?? throw new NotFoundException("EventRegistration", request.RegistrationId);
        reg.CheckIn(_clock.UtcNow);
        await _db.SaveChangesAsync(ct);
    }
}

public sealed record CancelRegistrationCommand(Guid RegistrationId) : IRequest;

public sealed class CancelRegistrationCommandHandler : IRequestHandler<CancelRegistrationCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;
    public CancelRegistrationCommandHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db; _clock = clock;
    }

    public async Task Handle(CancelRegistrationCommand request, CancellationToken ct)
    {
        var reg = await _db.EventRegistrations.FirstOrDefaultAsync(r => r.Id == request.RegistrationId, ct)
            ?? throw new NotFoundException("EventRegistration", request.RegistrationId);
        reg.Cancel(_clock.UtcNow);
        await _db.SaveChangesAsync(ct);
    }
}

public sealed record MarkNoShowCommand(Guid RegistrationId) : IRequest;

public sealed class MarkNoShowCommandHandler : IRequestHandler<MarkNoShowCommand>
{
    private readonly IApplicationDbContext _db;
    public MarkNoShowCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(MarkNoShowCommand request, CancellationToken ct)
    {
        var reg = await _db.EventRegistrations.FirstOrDefaultAsync(r => r.Id == request.RegistrationId, ct)
            ?? throw new NotFoundException("EventRegistration", request.RegistrationId);
        reg.MarkNoShow();
        await _db.SaveChangesAsync(ct);
    }
}

// ─── List & detail ──────────────────────────────────────────────────────────

public sealed record ListEventsQuery(EventStatus? Status = null) : IRequest<IReadOnlyList<EventSummary>>;

public sealed class ListEventsQueryHandler : IRequestHandler<ListEventsQuery, IReadOnlyList<EventSummary>>
{
    private readonly IApplicationDbContext _db;
    public ListEventsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<EventSummary>> Handle(ListEventsQuery request, CancellationToken ct)
    {
        var q = _db.Events.AsNoTracking().Include(e => e.Registrations);
        IQueryable<FederationEvent> filtered = q;
        if (request.Status is { } s) filtered = q.Where(e => e.Status == s);

        var events = await filtered.OrderBy(e => e.StartsAtUtc).ToListAsync(ct);
        return events.Select(EventMapper.ToSummary).ToList();
    }
}

public sealed record GetEventByIdQuery(Guid EventId) : IRequest<EventSummary>;

public sealed class GetEventByIdQueryHandler : IRequestHandler<GetEventByIdQuery, EventSummary>
{
    private readonly IApplicationDbContext _db;
    public GetEventByIdQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<EventSummary> Handle(GetEventByIdQuery request, CancellationToken ct)
    {
        var ev = await _db.Events.AsNoTracking()
            .Include(e => e.Registrations)
            .FirstOrDefaultAsync(e => e.Id == request.EventId, ct)
            ?? throw new NotFoundException("Event", request.EventId);
        return EventMapper.ToSummary(ev);
    }
}

public sealed record ListEventRegistrationsQuery(Guid EventId)
    : IRequest<IReadOnlyList<EventRegistrationItem>>;

public sealed class ListEventRegistrationsQueryHandler
    : IRequestHandler<ListEventRegistrationsQuery, IReadOnlyList<EventRegistrationItem>>
{
    private readonly IApplicationDbContext _db;
    public ListEventRegistrationsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<EventRegistrationItem>> Handle(
        ListEventRegistrationsQuery request, CancellationToken ct)
    {
        // Join to Members so the UI can show names/IDs without a second round-trip.
        var items = await (
            from r in _db.EventRegistrations.AsNoTracking()
            where r.EventId == request.EventId
            join m in _db.Members.AsNoTracking() on r.MemberId equals m.Id
            orderby r.CreatedAtUtc
            select new EventRegistrationItem(
                r.Id, r.EventId, r.MemberId,
                m.FullName, m.SMF_ID,
                r.Status, r.CreatedAtUtc, r.CheckedInAtUtc))
            .ToListAsync(ct);

        return items;
    }
}

// ─── Mapper ─────────────────────────────────────────────────────────────────

internal static class EventMapper
{
    public static EventSummary ToSummary(FederationEvent e) => new(
        e.Id, e.Title, e.Description, e.Location,
        e.StartsAtUtc, e.EndsAtUtc, e.RegistrationOpensAtUtc, e.RegistrationClosesAtUtc,
        e.EntryFeeMinor, e.Currency, e.Capacity, e.Status,
        e.Registrations.Count(r => r.Status != EventRegistrationStatus.Cancelled),
        e.LiveStreamProvider, e.LiveStreamUrl);
}
