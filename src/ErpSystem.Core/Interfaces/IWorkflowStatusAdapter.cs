using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces;

/// <summary>
/// Maps workflow outcomes into module-specific status fields
/// </summary>
public interface IWorkflowStatusAdapter
{
    /// <summary>
    /// Entity type names this adapter can handle (aliases allowed)
    /// </summary>
    IReadOnlyCollection<string> EntityTypes { get; }

    /// <summary>
    /// Applies workflow outcome after submission
    /// </summary>
    void ApplySubmitOutcome(object entity, WorkflowOutcome outcome, Guid? userId);

    /// <summary>
    /// Applies workflow outcome after approval processing
    /// </summary>
    void ApplyApprovalOutcome(object entity, WorkflowOutcome outcome, Guid? userId, string? rejectionReason = null);

    /// <summary>
    /// Applies workflow recall after the requester pulls the active workflow back to draft.
    /// </summary>
    void ApplyRecallOutcome(object entity, Guid? userId, string? reason = null)
    {
        WorkflowStatusAdapterDefaults.ApplyDraftOutcome(entity);
    }
}

public static class WorkflowStatusAdapterDefaults
{
    public static void ApplyDraftOutcome(object entity)
    {
        TrySetStatusProperty(entity, "Status", "Draft");
        TrySetStatusProperty(entity, "ApprovalStatus", "Draft");
        TrySetStatusProperty(entity, "ApprovedById", null);
        TrySetStatusProperty(entity, "ApprovedAt", null);
        TrySetStatusProperty(entity, "ApprovedDate", null);
    }

    private static void TrySetStatusProperty(object entity, string propertyName, object? value)
    {
        var property = entity.GetType().GetProperty(propertyName);
        if (property == null || !property.CanWrite)
        {
            return;
        }

        if (value == null)
        {
            if (Nullable.GetUnderlyingType(property.PropertyType) != null || !property.PropertyType.IsValueType)
            {
                property.SetValue(entity, null);
            }

            return;
        }

        if (property.PropertyType == typeof(string))
        {
            property.SetValue(entity, value.ToString());
            return;
        }

        if (property.PropertyType.IsEnum && Enum.TryParse(property.PropertyType, value.ToString(), ignoreCase: true, out var enumValue))
        {
            property.SetValue(entity, enumValue);
        }
    }
}

/// <summary>
/// Resolves workflow status adapters by entity type
/// </summary>
public interface IWorkflowStatusAdapterRegistry
{
    bool TryGetAdapter(string entityType, out IWorkflowStatusAdapter adapter);
    IWorkflowStatusAdapter GetAdapter(string entityType);
}
