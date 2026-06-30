using Microsoft.Extensions.Logging;
using SMF.Application.Common.Interfaces;

namespace SMF.Infrastructure.Invoicing;

/// <summary>
/// Picks the right concrete <see cref="IInvoiceRenderer"/> for a requested
/// <see cref="InvoiceFormat"/>. Falls back to HTML when the requested
/// format is not registered (e.g. PDF asked for, but the QuestPDF renderer
/// is disabled in configuration).
/// </summary>
internal sealed class InvoiceRendererResolver : IInvoiceRendererResolver
{
    private readonly IReadOnlyDictionary<InvoiceFormat, IInvoiceRenderer> _byFormat;
    private readonly ILogger<InvoiceRendererResolver> _logger;

    public InvoiceRendererResolver(
        IEnumerable<IInvoiceRenderer> renderers,
        ILogger<InvoiceRendererResolver> logger)
    {
        _byFormat = renderers
            .GroupBy(r => r.Format)
            .ToDictionary(g => g.Key, g => g.Last());
        _logger = logger;
    }

    public bool IsSupported(InvoiceFormat format) => _byFormat.ContainsKey(format);

    public IInvoiceRenderer Resolve(InvoiceFormat format)
    {
        if (_byFormat.TryGetValue(format, out var renderer))
            return renderer;

        if (_byFormat.TryGetValue(InvoiceFormat.Html, out var fallback))
        {
            _logger.LogInformation(
                "No renderer registered for {Format}; falling back to HTML.", format);
            return fallback;
        }

        throw new InvalidOperationException(
            $"No invoice renderer is registered (asked for {format}, no HTML fallback available).");
    }
}

/// <summary>
/// Bridges the strongly-typed <see cref="Payments.InvoicingOptions"/> from
/// Infrastructure across to the Application layer's
/// <see cref="IInvoicingSettings"/> interface.
/// </summary>
internal sealed class InvoicingSettingsAdapter : IInvoicingSettings
{
    private readonly Microsoft.Extensions.Options.IOptionsMonitor<Payments.InvoicingOptions> _options;

    public InvoicingSettingsAdapter(
        Microsoft.Extensions.Options.IOptionsMonitor<Payments.InvoicingOptions> options) => _options = options;

    public string IssuerName => _options.CurrentValue.IssuerName;
    public string? IssuerTaxNumber => _options.CurrentValue.IssuerTaxNumber;
    public string? IssuerAddress => _options.CurrentValue.IssuerAddress;
    public int DefaultVatRateBp => _options.CurrentValue.DefaultVatRateBp;
    public string DefaultCurrency => _options.CurrentValue.DefaultCurrency;
}
