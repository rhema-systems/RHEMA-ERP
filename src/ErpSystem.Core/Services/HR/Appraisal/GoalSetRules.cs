using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Services.HR.Appraisal;

/// <summary>
/// The goal-set rules in one place (performance closure lane L-a). The goal-setting gate
/// (<see cref="AppraisalGates"/>), the manager's "lock goal set", HR's advance past goal setting and
/// the team views all read these, so a set the gate calls complete is a set the lock accepts — the
/// weight total used to be defined twice, once with rejected goals and once without.
/// </summary>
public static class GoalSetRules
{
    /// <summary>A rejected goal is out of the set; every other goal counts toward it.</summary>
    public static bool IsLive(GoalStatus status) => status != GoalStatus.Rejected;

    /// <summary>
    /// Agreed with the manager: approved, or running since. A goal still in the legacy Locked status
    /// reads as approved and locked (decision D-29).
    /// </summary>
    public static bool IsAgreed(GoalStatus status) => status is GoalStatus.Approved
        or GoalStatus.InProgress or GoalStatus.OnTrack or GoalStatus.AtRisk
        or GoalStatus.Completed or GoalStatus.Locked;

    /// <summary>
    /// Decision D-71: the goals the at-risk rules watch — agreed and not finished. A draft, a goal
    /// waiting for the manager and a rejected one are not yet anyone's commitment, so they are never at
    /// risk; a completed one is done. An array so a query's <c>Contains</c> becomes SQL's IN.
    /// </summary>
    public static readonly GoalStatus[] RiskWatched =
    {
        GoalStatus.Approved, GoalStatus.InProgress, GoalStatus.OnTrack, GoalStatus.AtRisk, GoalStatus.Locked,
    };

    /// <summary>
    /// Decision D-72: the status an agreed goal runs in, from its progress — Completed at 100 % or more,
    /// InProgress once anything is recorded, else Approved. What unlock restores a goal the old lock
    /// left in the Locked status to, and what a goal falls back to when its progress entries go.
    /// </summary>
    public static GoalStatus RunningStatusFromProgress(decimal progressPercent, bool hasEntries) =>
        progressPercent >= 100 ? GoalStatus.Completed
        : progressPercent > 0 || hasEntries ? GoalStatus.InProgress
        : GoalStatus.Approved;

    /// <summary>
    /// Decision D-29: a lock freezes what the goal is — title, measure, target, weight, owner — and
    /// not its year, so the lock is the flag. Nothing sets the Locked status any more; a goal left
    /// in it by the old lock still counts as locked.
    /// </summary>
    public static bool IsLocked(bool isLocked, GoalStatus status) => isLocked || status == GoalStatus.Locked;

    /// <summary>The set's weight total: live goals only.</summary>
    public static int WeightTotal(IEnumerable<(GoalStatus Status, int Weight)> goals) =>
        goals.Where(g => IsLive(g.Status)).Sum(g => g.Weight);

    /// <summary>
    /// Why this employee's goal set for a cycle cannot be locked yet, or null when it can
    /// (closure plan L5): every live goal agreed, the count inside the cycle's minimum and maximum,
    /// and the weights adding to 100. The wording follows the goal-setting gate's.
    /// </summary>
    public static string? LockBlocker(
        IReadOnlyCollection<(GoalStatus Status, int Weight)> goals, int? minGoals, int? maxGoals)
    {
        var live = goals.Where(g => IsLive(g.Status)).ToList();
        if (live.Count == 0)
            return "no goals are set for this cycle yet";

        var waiting = live.Count(g => g.Status == GoalStatus.PendingApproval);
        if (waiting > 0)
            return waiting == 1
                ? "one goal still waits for the manager's approval"
                : $"{waiting} goals still wait for the manager's approval";

        var drafts = live.Count(g => g.Status == GoalStatus.Draft);
        if (drafts > 0)
            return drafts == 1
                ? "one goal is still a draft, not yet submitted for the manager's approval"
                : $"{drafts} goals are still drafts, not yet submitted for the manager's approval";

        var min = minGoals is int m && m > 0 ? m : 1;
        if (live.Count < min)
            return $"{live.Count} of the {min} goals this cycle requires are set";

        if (maxGoals is int max && max > 0 && live.Count > max)
            return $"{live.Count} goals are set and this cycle allows at most {max}";

        var total = live.Sum(g => g.Weight);
        if (total != 100)
            return $"the goal weights add up to {total}%, not 100%";

        return null;
    }
}
