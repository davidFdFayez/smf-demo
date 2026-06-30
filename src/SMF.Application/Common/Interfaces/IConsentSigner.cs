namespace SMF.Application.Common.Interfaces;

/// <summary>
/// Tamper-evident HMAC signer for the compliance audit trail. Backed by a
/// secret in configuration so leaks of the database alone aren't enough to
/// forge a signature. Decoupled from the existing <c>IDigitalIdTokenService</c>
/// so consent secrets can be rotated independently.
/// </summary>
public interface IConsentSigner
{
    /// <summary>HMAC-SHA256 over <paramref name="payload"/>; returns lowercase hex.</summary>
    string Sign(string payload);

    /// <summary>Verifies a signature in constant time.</summary>
    bool Verify(string payload, string signatureHex);

    /// <summary>SHA-256 of an opaque secret token (e.g. parental-consent
    /// link). Used both at issue time (we store the hash, not the token)
    /// and at verification time (compare hashes, never raw values).</summary>
    string HashToken(string rawToken);
}
