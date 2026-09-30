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
    /// The moves the raw status routes may make — <c>PATCH {id}/status</c> and the workflow's
    /// <c>transition</c> (performance closure E-a): opening a Draft appraisal, and closing a Completed
    /// one that has a score. Every other move belongs to the action that makes it, with what that
    /// action checks and records. The routes allowed the whole table: an appraisal they moved to
    /// Completed was settled and published with no sign-off, one moved to Appealed had no appeal
    /// behind it, one moved back from governance skipped the return's resets, and an undecided appeal
    /// could be walked to Completed.
    /// </summary>
    public static void EnsureRawTransition(AppraisalStatus from, AppraisalStatus to, bool hasScore)
    {
        if (from == AppraisalStatus.Draft && to == AppraisalStatus.Active) return;

        if (from == AppraisalStatus.Completed && to == AppraisalStatus.Closed)
        {
            if (hasScore) return;
            throw new InvalidOperationException(
                "A Completed appraisal is closed once it has a score, and this one has none.");
        }

        throw new InvalidOperationException(
            $"An appraisal is not moved from {from} to {to} by a status change: {OwnerOf(from, to)}");
    }

    /// <summary>Which action makes a move the raw routes refuse, for the refusal's text.</summary>
    private static string OwnerOf(AppraisalStatus from, AppraisalStatus to) => to switch
    {
        AppraisalStatus.Governance =>
            "it moves into governance when its manager submits the evaluation.",
        AppraisalStatus.Active when from == AppraisalStatus.Governance =>
            "HR returns it to the manager with the return action, which reopens the evaluation.",
        AppraisalStatus.Completed when from == AppraisalStatus.Appealed =>
            "HR's decision on the appeal completes it.",
        AppraisalStatus.Completed =>
            "it completes when its last step is done — the manager's submission, the calibration " +
            "commit, HR's sign-off or the employee's acknowledgment — or through HR's audited advance.",
        AppraisalStatus.Appealed =>
            "the employee files an appeal.",
        AppraisalStatus.Closed when from == AppraisalStatus.Appealed =>
            "HR decides the appeal first; the Completed appraisal is then closed.",
        AppraisalStatus.Withdrawn =>
            "withdrawing takes a reason and records who withdrew it.",
        _ => "the status routes open a Draft appraisal and close a Completed one.",
    };

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
