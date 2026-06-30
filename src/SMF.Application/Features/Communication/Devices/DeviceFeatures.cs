using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SMF.Application.Common.Exceptions;
using SMF.Application.Common.Interfaces;
using SMF.Application.Features.Communication.Common;
using SMF.Domain.Entities;
using SMF.Domain.Enums;

namespace SMF.Application.Features.Communication.Devices;

// =============================================================
// Register / refresh a member's push device.
//
// Idempotent on (MemberId, Token): re-registering the same token just
// updates LastSeenAtUtc + flips IsActive back on. A new token from the
// same member implicitly deactivates older tokens for the same platform
// so we don't fan out push to dead installs.
// =============================================================

public sealed record RegisterDeviceCommand(
    Guid MemberId,
    DevicePlatform Platform,
    string Token) : IRequest<DeviceRegistrationDto>;

public sealed class RegisterDeviceCommandValidator : AbstractValidator<RegisterDeviceCommand>
{
    public RegisterDeviceCommandValidator()
    {
        RuleFor(x => x.MemberId).NotEqual(Guid.Empty);
        RuleFor(x => x.Platform).IsInEnum();
        RuleFor(x => x.Token).NotEmpty().MaximumLength(2048);
    }
}

public sealed class RegisterDeviceCommandHandler
    : IRequestHandler<RegisterDeviceCommand, DeviceRegistrationDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public RegisterDeviceCommandHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<DeviceRegistrationDto> Handle(RegisterDeviceCommand request, CancellationToken ct)
    {
        var memberExists = await _db.Members.AnyAsync(m => m.Id == request.MemberId, ct);
        if (!memberExists) throw new NotFoundException(nameof(Member), request.MemberId);

        var token = request.Token.Trim();
        var existing = await _db.DeviceRegistrations
            .FirstOrDefaultAsync(d => d.MemberId == request.MemberId && d.Token == token, ct);

        var now = _clock.UtcNow;
        if (existing is not null)
        {
            existing.TouchSeen(now);
        }
        else
        {
            // New token from this member — retire any other active tokens for
            // the same platform so we don't double-send if the install was
            // restored on another device under the same member.
            var stale = await _db.DeviceRegistrations
                .Where(d => d.MemberId == request.MemberId
                            && d.Platform == request.Platform
                            && d.IsActive)
                .ToListAsync(ct);
            foreach (var s in stale) s.Deactivate(now);

            existing = DeviceRegistration.Register(request.MemberId, request.Platform, token, now);
            _db.DeviceRegistrations.Add(existing);
        }

        await _db.SaveChangesAsync(ct);

        return new DeviceRegistrationDto(
            existing.Id, existing.MemberId, existing.Platform, existing.Token,
            existing.IsActive, existing.RegisteredAtUtc, existing.LastSeenAtUtc);
    }
}

// =============================================================
// Unregister: explicit user "log out from this device" or admin tooling.
// =============================================================

public sealed record UnregisterDeviceCommand(Guid MemberId, string Token) : IRequest<Unit>;

public sealed class UnregisterDeviceCommandValidator : AbstractValidator<UnregisterDeviceCommand>
{
    public UnregisterDeviceCommandValidator()
    {
        RuleFor(x => x.MemberId).NotEqual(Guid.Empty);
        RuleFor(x => x.Token).NotEmpty();
    }
}

public sealed class UnregisterDeviceCommandHandler
    : IRequestHandler<UnregisterDeviceCommand, Unit>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public UnregisterDeviceCommandHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<Unit> Handle(UnregisterDeviceCommand request, CancellationToken ct)
    {
        var token = request.Token.Trim();
        var device = await _db.DeviceRegistrations
            .FirstOrDefaultAsync(d => d.MemberId == request.MemberId && d.Token == token, ct);
        if (device is null) return Unit.Value;

        device.Deactivate(_clock.UtcNow);
        await _db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
