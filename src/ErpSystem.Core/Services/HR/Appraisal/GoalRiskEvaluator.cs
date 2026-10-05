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
//  Rule 3 (severity 50): Flagged by hand in a progress update
//    Fires when: Status == AtRisk and neither threshold rule already did
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
        // ── Watch agreed goals only ────────────────────────────────────────
        // Completed and Rejected goals are closed; a draft or a goal still waiting for the manager is
        // nobody's commitment yet (performance closure D-71), and every list that asks shares this.
        if (Array.IndexOf(GoalSetRules.RiskWatched, goal.Status) < 0)
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

        // ── Rule 3: Flagged by hand ───────────────────────────────────────
        // Someone recorded a progress update saying this goal is at risk. Neither threshold
        // rule may fire — a goal can be in trouble months before its deadline and still be
        // ahead of the straight line — but a human judgement is not something the evaluator
        // gets to overrule.
        //
        // This ran last, and was missing entirely, which made the two at-risk endpoints
        // disagree on identical data: /performance/team-goals/at-risk returns pre-filter
        // matches directly and so honoured the status, while /performance/goals-at-risk runs
        // every candidate through this evaluator and silently dropped them. Both controllers
        // document "Status == AtRisk" as qualifying; only one behaved that way.
        //
        // Scored below both threshold rules on purpose: those carry a specific, automated
        // reason, and results are sorted by severity, so a measured problem still outranks a
        // flagged one.
        if (goal.Status == GoalStatus.AtRisk)
        {
            return new RiskEvaluationResult
            {
                IsAtRisk      = true,
                SeverityScore = 50,
                Reason        = "Flagged at risk in a progress update"
            };
        }

        return RiskEvaluationResult.NotAtRisk;
    }
}
