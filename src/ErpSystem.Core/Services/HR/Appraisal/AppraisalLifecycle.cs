using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Services.HR.Appraisal;

/// <summary>
/// The appraisal's major-status transitions, in one table (performance closure E1). There were two
/// identical copies — <c>AppraisalWorkflowService</c> and <c>PerformanceAppraisalService.UpdateStatusAsync</c>
/// — and the gates' sync (B1) would have been a third.
/// </summary>
/// <remarks>
/// Withdrawn is not here yet: withdrawing takes a reason, an actor and a date (D-10), which the raw
/// status routes that read this table cannot carry. Lane E adds it with its own action.
/// </remarks>
public static class AppraisalLifecycle
{
    private static readonly IReadOnlyDictionary<AppraisalStatus, HashSet<AppraisalStatus>> Allowed =
        new Dictionary<AppraisalStatus, HashSet<AppraisalStatus>>
        {
            // Draft → Active: opened for the employee, or first worked on.
            [AppraisalStatus.Draft] = [AppraisalStatus.Active],

            // Active → Governance: the manager has submitted and a governance step follows.
            // Active → Completed: nothing follows the manager's evaluation.
            // Active → Appealed: rare edge case guard.
            [AppraisalStatus.Active] = [AppraisalStatus.Governance, AppraisalStatus.Completed, AppraisalStatus.Appealed],

            // Governance → Completed: the last governance step is behind it.
            // Governance → Appealed: an appeal lodged in governance.
            // Governance → Active: HR returns it to the manager.
            [AppraisalStatus.Governance] = [AppraisalStatus.Completed, AppraisalStatus.Appealed, AppraisalStatus.Active],

            // Completed → Appealed: the employee appeals. Completed → Closed: HR closes the record.
            [AppraisalStatus.Completed] = [AppraisalStatus.Appealed, AppraisalStatus.Closed],

            // Appealed → Completed: the appeal is decided. Appealed → Closed: decided and closed.
            [AppraisalStatus.Appealed] = [AppraisalStatus.Completed, AppraisalStatus.Closed],

            // Terminal.
            [AppraisalStatus.Closed] = [],
            [AppraisalStatus.Withdrawn] = [],
        };

    public static bool CanTransition(AppraisalStatus from, AppraisalStatus to)
        => Allowed.TryGetValue(from, out var next) && next.Contains(to);

    /// <summary>Throws <see cref="InvalidOperationException"/> naming the allowed moves when <paramref name="from"/> → <paramref name="to"/> is not one.</summary>
    public static void EnsureTransition(AppraisalStatus from, AppraisalStatus to)
    {
        if (CanTransition(from, to)) return;

        var allowed = Allowed.TryGetValue(from, out var next) && next.Count > 0
            ? string.Join(", ", next)
            : "none (terminal state)";

        throw new InvalidOperationException(
            $"Invalid appraisal lifecycle transition from '{from}' to '{to}'. " +
            $"Allowed transitions from '{from}': {allowed}.");
    }

    /// <summary>
    /// How far along the lifecycle a status is, for moving only forward: the gates' sync never
    /// moves an appraisal back (a Governance appraisal whose employee adds a draft goal is not sent
    /// back to Active). -1 for the states the sync leaves alone.
    /// </summary>
    public static int ForwardRank(AppraisalStatus status) => status switch
    {
        AppraisalStatus.Open or AppraisalStatus.Draft => 0,
        AppraisalStatus.Active => 1,
        AppraisalStatus.Governance => 2,
        AppraisalStatus.Completed => 3,
        AppraisalStatus.Closed => 4,
        _ => -1,
    };
}
