using SMF.Domain.Entities;

namespace SMF.Application.Common.Interfaces;

/// <summary>
/// Renders a <see cref="Certificate"/> into a printable document. The
/// dev/default implementation emits HTML that browsers can print-to-PDF;
/// a production swap-in can wire QuestPDF or a headless Chrome pipeline.
/// </summary>
public interface ICertificateRenderer
{
    Task<CertificateDocument> RenderAsync(
        Certificate certificate,
        Member member,
        CancellationToken cancellationToken = default);
}

public sealed record CertificateDocument(
    string ContentType,
    byte[] Content,
    string FileName);
