using System.Text.Json;
using MediatR;
using SMF.Application.Common.Interfaces;
using SMF.Domain.Entities;
using SMF.Infrastructure.Persistence;

namespace SMF.Infrastructure.Outbox;

/// <summary>
/// EF Core implementation of <see cref="IOutbox"/>. Attaches a new
/// <see cref="OutboxMessage"/> to the current DbContext WITHOUT saving — the
/// caller commits the aggregate and the outbox row in a single transaction.
/// </summary>
internal sealed class EfOutbox : IOutbox
{
    internal static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web)
        {
            // Keep enums as strings so the payload stays readable in the DB
            // and survives renames that don't alter the wire name.
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never
        };

    private readonly ApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public EfOutbox(ApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public Task EnqueueAsync<TNotification>(
        TNotification notification,
        CancellationToken cancellationToken = default)
        where TNotification : INotification
    {
        if (notification is null)
            throw new ArgumentNullException(nameof(notification));

        // Use the concrete runtime type so polymorphic notifications serialise
        // and deserialise through the same assembly-qualified name.
        var runtimeType = notification.GetType();
        var typeName = runtimeType.AssemblyQualifiedName
            ?? throw new InvalidOperationException(
                $"Cannot resolve AssemblyQualifiedName for {runtimeType}.");

        var payload = JsonSerializer.Serialize(
            notification, runtimeType, SerializerOptions);

        var message = OutboxMessage.Create(typeName, payload, _clock.UtcNow);
        _db.OutboxMessages.Add(message);

        return Task.CompletedTask;
    }
}
