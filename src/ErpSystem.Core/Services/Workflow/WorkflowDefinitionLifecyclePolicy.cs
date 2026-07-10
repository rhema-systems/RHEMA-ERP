using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Services.Workflow;

public static class WorkflowDefinitionLifecyclePolicy
{
    public static bool IsEditable(WorkflowDefinition definition) =>
        definition.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Draft;

    public static bool IsRuntimeEligible(WorkflowDefinition definition) =>
        definition.IsActive &&
        definition.LifecycleStatus == WorkflowDefinitionLifecycleStatus.Published;

    public static int GetNextVersion(IEnumerable<WorkflowDefinition> versions) =>
        versions.Select(definition => definition.Version).DefaultIfEmpty(0).Max() + 1;

    public static void EnsureEditable(WorkflowDefinition definition)
    {
        if (!IsEditable(definition))
        {
            throw new InvalidOperationException(
                $"Workflow version {definition.Version} is {definition.LifecycleStatus.ToString().ToLowerInvariant()} and cannot be edited. Clone it as a new draft first.");
        }
    }

    public static void EnsureCanPublish(WorkflowDefinition definition)
    {
        if (definition.LifecycleStatus != WorkflowDefinitionLifecycleStatus.Draft)
        {
            throw new InvalidOperationException("Only a draft workflow version can be published.");
        }
    }
}
