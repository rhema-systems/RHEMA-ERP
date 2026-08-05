using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR.Services;

namespace ErpSystem.Core.Services.HR.Appraisal;

// ─────────────────────────────────────────────────────────────────────────────
//  GoalRiskEvaluator
//
//  Pure, stateless, synchronous.  Zero database access.  Zero side-effects.
//
//  ── Rule evaluation order ─────────────────────────────────────────────────
//  Rules are evaluated in severity order (highest first).  The first rule
//  that fires returns immediately — a single goal can only be flagged for
//  one reason.  This is intentional: surfacing the most actionable alert
//  prevents noise.
//
//  Rule 1 (severity 70): Close to deadline + low absolute progress
//    Fires when: daysRemaining <= threshold AND progressPercent < minProgress
//
//  Rule 2 (severity 60): Behind expected timeline
//    Fires when: actual + tolerance < linearlyExpectedProgress
//    Only evaluated when there is positive elapsed time (avoids division issues)
//
//  ── DateOnly arithmetic ───────────────────────────────────────────────────
//  DateOnly.DayNumber subtraction is used instead of (DateOnly - DateOnly).Days
//  because it avoids constructing a TimeSpan and is marginally faster in a
//  tight loop over large goal lists.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Production implementation of <see cref="IGoalRiskEvaluator"/>.
/// Evaluates goal risk using configurable thresholds from <see cref="GoalRiskSetting"/>.
/// </summary>
public sealed class GoalRiskEvaluator : IGoalRiskEvaluator
{
    /// <inheritdoc />
    public RiskEvaluationResult Evaluate(
        EmployeeGoal    goal,
        GoalRiskSetting settings,
        DateTime        utcNow)
    {
        // ── Skip terminal statuses ─────────────────────────────────────────
        // Completed and Rejected goals are closed; risk monitoring is irrelevant.
        if (goal.Status is GoalStatus.Completed or GoalStatus.Rejected)
            return RiskEvaluationResult.NotAtRisk;

        var today = DateOnly.FromDateTime(utcNow.Date);

        // ── Rule 1: Close to deadline with low absolute progress ──────────
        int daysRemaining = goal.DueDate.DayNumber - today.DayNumber;

        if (daysRemaining <= settings.DaysRemainingThreshold
            && goal.ProgressPercent < settings.MinimumProgressPercent)
        {
            return new RiskEvaluationResult
            {
                IsAtRisk      = true,
                SeverityScore = 70,
                Reason        = "Low progress near deadline"
            };
        }

        // ── Rule 2: Behind expected linear timeline ────────────────────────
        int totalDurationDays = goal.DueDate.DayNumber - goal.StartDate.DayNumber;

        if (totalDurationDays > 0)
        {
            int elapsedDays = today.DayNumber - goal.StartDate.DayNumber;

            // Only apply the rule once work has actually begun — avoids
            // penalising goals on the very first day where expected progress
            // is effectively 0% (or slightly above due to rounding).
            if (elapsedDays > 0)
            {
                double expectedProgress = (double)elapsedDays / totalDurationDays * 100.0;

                if ((double)goal.ProgressPercent + settings.ExpectedProgressTolerancePercent
                    < expectedProgress)
                {
                    return new RiskEvaluationResult
                    {
                        IsAtRisk      = true,
                        SeverityScore = 60,
                        Reason        = "Progress behind expected timeline"
                    };
                }
            }
        }

        return RiskEvaluationResult.NotAtRisk;
    }
}
