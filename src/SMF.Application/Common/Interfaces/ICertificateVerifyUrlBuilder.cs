namespace SMF.Application.Common.Interfaces;

/// <summary>
/// Builds public-facing certificate verification URLs from a verification
/// code. Lives in the Application layer so feature handlers (e.g. the
/// course-completion email) can construct links without referencing the
/// Infrastructure project. The default implementation reads the same
/// <c>Certificates:VerifyUrlTemplate</c> the certificate renderer uses,
/// guaranteeing the email link, the QR code, and the admin UI all agree.
/// </summary>
public interface ICertificateVerifyUrlBuilder
{
    string BuildVerifyUrl(string verificationCode);
}
