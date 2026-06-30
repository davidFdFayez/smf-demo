using SMF.Domain.Enums;

namespace SMF.Application.Common.Interfaces;

/// <summary>
/// Issues and verifies signed Digital ID tokens. Consumed by the
/// Application layer so the command handlers stay framework-agnostic
/// (no direct HMAC / JSON plumbing leaks into the use-cases).
/// </summary>
public interface IDigitalIdTokenService
{
    /// <summary>
    /// Signs a new token for the given member. <paramref name="issuedAtUtc"/>
    /// and <paramref name="validUntilUtc"/> are returned so the caller can
    /// persist them for the mobile client's cache-expiry UI.
    /// </summary>
    DigitalIdToken Issue(DigitalIdClaims claims);

    /// <summary>
    /// Parse + verify a token. Returns a result that tells the caller
    /// exactly why a token was rejected (so we can emit the right gate
    /// message instead of a generic 401).
    /// </summary>
    DigitalIdVerification Verify(string token);
}

public sealed record DigitalIdClaims(
    Guid MemberId,
    string SmfId,
    string FullName,
    RegistrationStatus StatusAtIssue);

public sealed record DigitalIdToken(
    string Token,
    DateTime IssuedAtUtc,
    DateTime ValidUntilUtc);

public enum DigitalIdRejectionReason
{
    None = 0,
    Malformed = 1,
    SignatureMismatch = 2,
    NotYetValid = 3,
    Expired = 4,
}

public sealed record DigitalIdVerification(
    bool IsValid,
    DigitalIdRejectionReason Reason,
    DigitalIdClaims? Claims,
    DateTime? IssuedAtUtc,
    DateTime? ValidUntilUtc);
