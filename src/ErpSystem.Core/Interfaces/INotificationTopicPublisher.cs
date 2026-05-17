namespace ErpSystem.Core.Interfaces;

public sealed class NotificationTopicEvent
{
    public Guid TenantId { get; init; }
    public string TopicKey { get; init; } = string.Empty;
    public string? NotificationType { get; init; }
    public string? EntityType { get; init; }
    public Guid? EntityId { get; init; }
    public Guid? TriggeredByUserId { get; init; }
    public Dictionary<string, object> Data { get; init; } = new();
    public Dictionary<string, object> Metadata { get; init; } = new();
    public NotificationTopicEmailOptions? Email { get; init; }
}

public sealed class NotificationTopicEmailOptions
{
    public string? SubjectTemplateOverride { get; init; }
    public string? HtmlBodyTemplateOverride { get; init; }
    public string? TextBodyTemplateOverride { get; init; }
    public string? AttachmentSource { get; init; }
    public IReadOnlyList<NotificationTopicEmailAttachment> Attachments { get; init; } = [];
}

public sealed class NotificationTopicEmailAttachment
{
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = "application/octet-stream";
    public string? ContentBase64 { get; init; }
}

/// <summary>
/// Publishes events into the admin-configurable Notification Topics system.
/// </summary>
public interface INotificationTopicPublisher
{
    Task PublishAsync(NotificationTopicEvent evt, CancellationToken cancellationToken = default);
}

