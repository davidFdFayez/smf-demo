namespace SMF.Application.Common.Interfaces;

/// <summary>Push notification gateway. The default implementation targets
/// Firebase Cloud Messaging HTTP v1; in dev (no service-account configured)
/// a logging stub keeps the pipeline green without external calls.</summary>
public interface IPushSender
{
    Task<PushDeliveryResult> SendAsync(PushMessage message, CancellationToken cancellationToken = default);
}

/// <summary>FCM payload. <see cref="DeviceToken"/> is the registration token
/// issued by the Firebase SDK on the client; <see cref="Title"/> +
/// <see cref="Body"/> populate the visible notification; <see cref="Data"/>
/// is the optional silent-data envelope (deep-link target, etc.).</summary>
public sealed record PushMessage(
    string DeviceToken,
    string Title,
    string Body,
    IReadOnlyDictionary<string, string>? Data = null);

public sealed record PushDeliveryResult(
    bool Delivered,
    string? ProviderMessageId,
    string? Error,
    bool TokenInvalidated = false);
