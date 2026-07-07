using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Interfaces;

namespace ErpSystem.Core.Services.Workflow;

public sealed class WorkflowModuleConformanceReport
{
    public bool IsConformant => MissingStatusAdapters.Count == 0;
    public int ActiveEntityTypeCount { get; init; }
    public IReadOnlyList<string> SupportedEntityTypes { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> MissingStatusAdapters { get; init; } = Array.Empty<string>();
}

public static class WorkflowModuleConformanceValidator
{
    public static WorkflowModuleConformanceReport Evaluate(
        IEnumerable<WorkflowEntityType> activeEntityTypes,
        IWorkflowStatusAdapterRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(activeEntityTypes);
        ArgumentNullException.ThrowIfNull(registry);

        var supported = new List<string>();
        var missing = new List<string>();
        var types = activeEntityTypes
            .Where(entityType => entityType.IsActive && !entityType.IsDeleted)
            .OrderBy(entityType => entityType.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var entityType in types)
        {
            var candidates = new[] { entityType.Name, entityType.Code, entityType.DisplayName }
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase);

            if (candidates.Any(candidate => registry.TryGetAdapter(candidate, out _)))
            {
                supported.Add(entityType.Name);
            }
            else
            {
                missing.Add($"{entityType.Name} ({entityType.Code})");
            }
        }

        return new WorkflowModuleConformanceReport
        {
            ActiveEntityTypeCount = types.Count,
            SupportedEntityTypes = supported,
            MissingStatusAdapters = missing
        };
    }
}
