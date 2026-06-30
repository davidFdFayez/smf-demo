namespace SMF.Application.Common.Interfaces;

/// <summary>
/// Federation invoicing preferences (issuer block, VAT rate, currency)
/// surfaced to the Application layer so handlers don't need to reach into
/// the Infrastructure options class directly.
/// </summary>
public interface IInvoicingSettings
{
    string IssuerName { get; }
    string? IssuerTaxNumber { get; }
    string? IssuerAddress { get; }
    int DefaultVatRateBp { get; }
    string DefaultCurrency { get; }
}
