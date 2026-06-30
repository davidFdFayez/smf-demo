using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SMF.Application.Common.Interfaces;

namespace SMF.Infrastructure.Outbox;

/// <summary>
/// Long-running singleton that loops on the configured interval, creates a
/// per-iteration DI scope, and delegates work to <see cref="IOutboxProcessor"/>.
/// Opt out via <c>Outbox:RunHostedService = false</c> when another host is
/// driving the processor (e.g. integration tests).
/// </summary>
internal sealed class OutboxHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IOptionsMonitor<OutboxOptions> _options;
    private readonly ILogger<OutboxHostedService> _logger;

    public OutboxHostedService(
        IServiceScopeFactory scopes,
        IOptionsMonitor<OutboxOptions> options,
        ILogger<OutboxHostedService> logger)
    {
        _scopes = scopes;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.CurrentValue.RunHostedService)
        {
            _logger.LogInformation(
                "OutboxHostedService disabled via config (Outbox:RunHostedService = false).");
            return;
        }

        _logger.LogInformation(
            "OutboxHostedService started; polling every {Interval}.",
            _options.CurrentValue.PollInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<IOutboxProcessor>();

                var dispatched = await processor.ProcessPendingAsync(stoppingToken);
                if (dispatched > 0)
                {
                    _logger.LogDebug("Outbox dispatched {Count} messages.", dispatched);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Shutting down — normal path.
                break;
            }
            catch (Exception ex)
            {
                // Never let an unhandled exception take the hosted service
                // down permanently. Log and back off for the next tick.
                _logger.LogError(ex, "Outbox poll iteration failed; will retry after interval.");
            }

            try
            {
                await Task.Delay(_options.CurrentValue.PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
