namespace ErpSystem.Core.Interfaces;

public sealed class NotificationTopicEvent
{
    public Guid TenantId { get; init; }
    public string TopicKey { get; init; } = string.Empty;
    public string? EntityType { get; init; }
    public Guid? EntityId { get; init; }
    public Guid? TriggeredByUserId { get; init; }
    public Dictionary<string, object> Data { get; init; } = new();
}

/// <summary>
/// Publishes events into the admin-configurable Notification Topics system.
/// </summary>
public interface INotificationTopicPublisher
{
    Task PublishAsync(NotificationTopicEvent evt, CancellationToken cancellationToken = default);
}

