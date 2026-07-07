using System.Text.Json;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Services.Workflow;

public static class WorkflowCrossStepSodEvaluator
{
    public static List<string> Validate(
        WorkflowApprovalConfigDto? config,
        IReadOnlyCollection<WorkflowStepInstance> previousSteps,
        IReadOnlyDictionary<Guid, IReadOnlyCollection<WorkflowApproval>> approvalsByStep,
        IReadOnlyDictionary<string, object> context,
        Guid userId)
    {
        var errors = new List<string>();
        var rules = config?.ConflictRules?.Where(rule => rule.IsEnabled).ToList()
            ?? new List<WorkflowApprovalConflictRuleDto>();
        if (rules.Count == 0 || userId == Guid.Empty)
        {
            return errors;
        }

        var orderedPreviousSteps = previousSteps
            .Where(step => step.Status == WorkflowStepInstanceStatus.Completed)
            .OrderBy(step => step.CompletedDate ?? step.CreatedDate)
            .ToList();

        foreach (var rule in rules)
        {
            var conflicts = rule.ActorSource switch
            {
                WorkflowApprovalActorSource.PreviousStepActor =>
                    orderedPreviousSteps.TakeLast(1).SelectMany(step => GetStepActors(step, approvalsByStep)),
                WorkflowApprovalActorSource.AnyPreviousApprover =>
                    orderedPreviousSteps.SelectMany(step => GetApprovalActors(step, approvalsByStep)),
                WorkflowApprovalActorSource.SpecificStepActor =>
                    orderedPreviousSteps
                        .Where(step => string.Equals(step.WorkflowStep?.Name, rule.SourceStepName, StringComparison.OrdinalIgnoreCase))
                        .SelectMany(step => GetStepActors(step, approvalsByStep)),
                WorkflowApprovalActorSource.ContextUser =>
                    TryResolveContextUser(context, rule.ContextField, out var contextUserId)
                        ? new[] { contextUserId }
                        : Array.Empty<Guid>(),
                _ => Array.Empty<Guid>()
            };

            if (!conflicts.Contains(userId))
            {
                continue;
            }

            errors.Add(string.IsNullOrWhiteSpace(rule.Message)
                ? BuildDefaultMessage(rule)
                : rule.Message.Trim());
        }

        return errors.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static IEnumerable<Guid> GetStepActors(
        WorkflowStepInstance step,
        IReadOnlyDictionary<Guid, IReadOnlyCollection<WorkflowApproval>> approvalsByStep)
    {
        if (step.AssignedToId.HasValue)
        {
            yield return step.AssignedToId.Value;
        }

        foreach (var actorId in GetApprovalActors(step, approvalsByStep))
        {
            yield return actorId;
        }
    }

    private static IEnumerable<Guid> GetApprovalActors(
        WorkflowStepInstance step,
        IReadOnlyDictionary<Guid, IReadOnlyCollection<WorkflowApproval>> approvalsByStep)
    {
        if (!approvalsByStep.TryGetValue(step.Id, out var approvals))
        {
            yield break;
        }

        foreach (var approval in approvals.Where(approval => approval.ProcessedById.HasValue))
        {
            yield return approval.ProcessedById!.Value;
        }
    }

    private static bool TryResolveContextUser(
        IReadOnlyDictionary<string, object> context,
        string? field,
        out Guid userId)
    {
        userId = Guid.Empty;
        if (string.IsNullOrWhiteSpace(field))
        {
            return false;
        }

        var pair = context.FirstOrDefault(item => string.Equals(item.Key, field.Trim(), StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value == null)
        {
            return false;
        }

        return pair.Value switch
        {
            Guid guid => SetGuid(guid, out userId),
            string text => Guid.TryParse(text, out userId),
            JsonElement element when element.ValueKind == JsonValueKind.String => Guid.TryParse(element.GetString(), out userId),
            _ => Guid.TryParse(pair.Value.ToString(), out userId)
        };
    }

    private static bool SetGuid(Guid value, out Guid output)
    {
        output = value;
        return value != Guid.Empty;
    }

    private static string BuildDefaultMessage(WorkflowApprovalConflictRuleDto rule)
    {
        return rule.ActorSource switch
        {
            WorkflowApprovalActorSource.PreviousStepActor => "The user who completed the previous workflow step cannot approve this step.",
            WorkflowApprovalActorSource.AnyPreviousApprover => "A user who approved an earlier workflow step cannot approve this step.",
            WorkflowApprovalActorSource.SpecificStepActor => $"A user who acted on step '{rule.SourceStepName}' cannot approve this step.",
            WorkflowApprovalActorSource.ContextUser => $"The user identified by workflow field '{rule.ContextField}' cannot approve this step.",
            _ => "The current user conflicts with this approval step's segregation-of-duties policy."
        };
    }
}
