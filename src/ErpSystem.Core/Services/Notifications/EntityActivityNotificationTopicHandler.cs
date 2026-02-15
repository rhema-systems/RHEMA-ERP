using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Events;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Notifications;

public class EntityActivityNotificationTopicHandler : IAppEventHandler<EntityActivityEvent>
{
    private readonly INotificationTopicPublisher _publisher;
    private readonly ILogger<EntityActivityNotificationTopicHandler> _logger;

    public EntityActivityNotificationTopicHandler(INotificationTopicPublisher publisher, ILogger<EntityActivityNotificationTopicHandler> logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    public async Task HandleAsync(EntityActivityEvent evt, CancellationToken cancellationToken = default)
    {
        if (evt.TenantId == Guid.Empty) return;

        var entityType = NormalizeSegment(evt.EntityType);
        var activity = NormalizeSegment(evt.Activity);
        var audience = NormalizeSegment(evt.Audience);
        if (string.IsNullOrWhiteSpace(entityType) || string.IsNullOrWhiteSpace(activity) || string.IsNullOrWhiteSpace(audience))
            return;

        var key = $"{entityType}.{activity}.{audience}";

        try
        {
            await _publisher.PublishAsync(new NotificationTopicEvent
            {
                TenantId = evt.TenantId,
                TopicKey = key,
                EntityType = entityType,
                EntityId = evt.EntityId,
                TriggeredByUserId = evt.TriggeredByUserId,
                Data = evt.Data ?? new Dictionary<string, object>()
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish notification topic for entity activity {TopicKey}", key);
        }
    }

    private static string NormalizeSegment(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var trimmed = value.Trim();
        var chars = trimmed.Where(char.IsLetterOrDigit).ToArray();
        return chars.Length == 0 ? string.Empty : new string(chars);
    }
}

