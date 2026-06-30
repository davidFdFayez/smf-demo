namespace SMF.Infrastructure.Outbox;

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    /// <summary>
    /// How often the hosted service polls for pending messages. Keep this low
    /// (sub-second) in dev; in production tune against DB round-trip cost.
    /// </summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>Max rows drained per poll iteration.</summary>
    public int BatchSize { get; set; } = 50;

    /// <summary>
    /// Set to false in environments where an external host (tests, one-off
    /// CLI) drives <see cref="SMF.Application.Common.Interfaces.IOutboxProcessor"/>
    /// manually. The background service becomes a no-op.
    /// </summary>
    public bool RunHostedService { get; set; } = true;
}
