using SMF.Domain.Enums;

namespace SMF.Domain.Entities;

/// <summary>
/// FCM push device token registered by a member's mobile app or browser. We
/// upsert by <c>(MemberId, Token)</c> so re-installing the app doesn't create
/// duplicate rows; replacing the token from the same device deactivates the
/// previous one. Inactive rows stay in the table so admins can audit lapsed
/// devices, but the dispatcher only fetches <see cref="IsActive"/> rows.
/// </summary>
public class DeviceRegistration
{
    public Guid Id { get; private set; }
    public Guid MemberId { get; private set; }
    public DevicePlatform Platform { get; private set; }
    public string Token { get; private set; } = default!;
    public bool IsActive { get; private set; }
    public DateTime RegisteredAtUtc { get; private set; }
    public DateTime LastSeenAtUtc { get; private set; }

    private DeviceRegistration() { }

    public static DeviceRegistration Register(
        Guid memberId, DevicePlatform platform, string token, DateTime nowUtc)
    {
        if (memberId == Guid.Empty) throw new ArgumentException("MemberId required.", nameof(memberId));
        if (string.IsNullOrWhiteSpace(token)) throw new ArgumentException("Token required.", nameof(token));

        return new DeviceRegistration
        {
            Id              = Guid.NewGuid(),
            MemberId        = memberId,
            Platform        = platform,
            Token           = token.Trim(),
            IsActive        = true,
            RegisteredAtUtc = nowUtc,
            LastSeenAtUtc   = nowUtc
        };
    }

    public void TouchSeen(DateTime nowUtc)
    {
        IsActive      = true;
        LastSeenAtUtc = nowUtc;
    }

    /// <summary>Mark this device as no longer in use. Common reason: FCM returned
    /// <c>NOT_REGISTERED</c> after a successful send attempt.</summary>
    public void Deactivate(DateTime nowUtc)
    {
        IsActive      = false;
        LastSeenAtUtc = nowUtc;
    }
}
