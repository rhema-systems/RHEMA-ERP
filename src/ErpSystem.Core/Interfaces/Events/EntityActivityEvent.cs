namespace ErpSystem.Core.Interfaces.Events;

/// <summary>
/// A generic, module-agnostic event describing an activity that occurred on an entity.
/// Used to drive admin-configurable Notification Topics across the application.
/// </summary>
public sealed class EntityActivityEvent
{
    public Guid TenantId { get; init; }
    public string EntityType { get; init; } = string.Empty;
    public string Activity { get; init; } = string.Empty;
    public string Audience { get; init; } = "Internal";
    public Guid? EntityId { get; init; }
    public Guid? TriggeredByUserId { get; init; }
    public Dictionary<string, object> Data { get; init; } = new();
}

