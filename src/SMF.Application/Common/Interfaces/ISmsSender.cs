namespace SMF.Application.Common.Interfaces;

/// <summary>SMS gateway abstraction. Implementations: Unifonic (default,
/// KSA-native) and Twilio, gated on <c>Sms:Provider</c>.</summary>
public interface ISmsSender
{
    Task<SmsDeliveryResult> SendAsync(SmsMessage message, CancellationToken cancellationToken = default);
}

/// <summary>Outbound SMS payload. <see cref="ToPhoneE164"/> must be in E.164
/// format (e.g. <c>+9665XXXXXXXX</c>); senders reject anything else.</summary>
public sealed record SmsMessage(string ToPhoneE164, string Body);

public sealed record SmsDeliveryResult(bool Delivered, string? ProviderMessageId, string? Error);
