using System.Security.Cryptography;
using System.Text;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Services.Workflow;

public sealed record WorkflowIntegrationRetryDecision(WorkflowExecutionQueueStatus Status, DateTime? NextAttemptAt);

public static class WorkflowPlatformPolicy
{
    public static IReadOnlyList<string> ValidateTemplateGraph(IEnumerable<Guid> stepIds,
        IEnumerable<(Guid FromStepId, Guid ToStepId)> transitions)
    {
        var ids = stepIds.ToList();
        var errors = new List<string>();
        if (ids.Count == 0) errors.Add("A workflow template must contain at least one step.");
        if (ids.Any(id => id == Guid.Empty)) errors.Add("Workflow template step IDs cannot be empty.");
        if (ids.Distinct().Count() != ids.Count) errors.Add("Workflow template step IDs must be unique.");
        var idSet = ids.ToHashSet();
        foreach (var transition in transitions)
        {
            if (!idSet.Contains(transition.FromStepId) || !idSet.Contains(transition.ToStepId))
                errors.Add("Every workflow template transition must reference an included step.");
            if (transition.FromStepId == transition.ToStepId)
                errors.Add("A workflow template transition cannot point to the same step.");
        }
        return errors.Distinct().ToList();
    }

    public static string NormalizeOfflineAction(string? actionType)
    {
        var normalized = actionType?.Trim().ToLowerInvariant();
        return normalized is "approve" or "reject"
            ? normalized
            : throw new ArgumentException("Offline workflow actions must be approve or reject.", nameof(actionType));
    }

    public static void EnsureIdempotencyKey(string? idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Trim().Length > 200)
            throw new ArgumentException("A valid idempotency key of no more than 200 characters is required.", nameof(idempotencyKey));
    }

    public static WorkflowIntegrationRetryDecision GetRetryDecision(int attemptCount, int maximumAttempts, DateTime nowUtc)
    {
        if (attemptCount >= Math.Max(maximumAttempts, 1))
            return new WorkflowIntegrationRetryDecision(WorkflowExecutionQueueStatus.DeadLetter, null);
        var delayMinutes = Math.Pow(2, Math.Min(Math.Max(attemptCount, 1), 8));
        return new WorkflowIntegrationRetryDecision(WorkflowExecutionQueueStatus.Failed, nowUtc.AddMinutes(delayMinutes));
    }

    public static int NormalizeAnalyticsDays(int days) => Math.Clamp(days, 1, 365);

    public static string ComputeArchiveHash(string canonicalJson) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalJson)));
}
