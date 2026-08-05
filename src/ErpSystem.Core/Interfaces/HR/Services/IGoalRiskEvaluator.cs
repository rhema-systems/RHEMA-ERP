using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Performance;

namespace ErpSystem.Core.Interfaces.HR.Services;

/// <summary>
/// Pure, stateless evaluator that determines whether a single
/// <see cref="EmployeeGoal"/> is at risk given the active risk settings.
///
/// CONTRACT (strictly enforced):
///   • No database access — the implementation must never inject DbContext.
///   • No side-effects — the result depends only on the three parameters.
///   • Always synchronous — risk evaluation is an in-memory computation.
///   • Deterministic — identical inputs must always produce identical output.
///
/// The separation between this interface and <see cref="IGoalRiskSettingsProvider"/>
/// is intentional: settings are loaded once by the application service and
/// reused for every goal, avoiding N+1 database queries.
/// </summary>
public interface IGoalRiskEvaluator
{
    /// <summary>
    /// Evaluates the risk status for <paramref name="goal"/> at the point
    /// in time represented by <paramref name="utcNow"/>.
    /// </summary>
    /// <param name="goal">The goal to evaluate. Must not be null.</param>
    /// <param name="settings">Active risk thresholds. Must not be null.</param>
    /// <param name="utcNow">
    /// Current UTC instant.  Injected rather than read from the clock so that
    /// all goals in a batch are evaluated against the same reference point.
    /// </param>
    /// <returns>
    /// A <see cref="RiskEvaluationResult"/> describing whether the goal is
    /// at risk and why.  Never returns null.
    /// </returns>
    RiskEvaluationResult Evaluate(
        EmployeeGoal   goal,
        GoalRiskSetting settings,
        DateTime       utcNow);
}
