namespace ErpSystem.Core.DTOs.HR;

// ─────────────────────────────────────────────────────────────────────────────
//  Goal Risk DTOs
//
//  These records are produced by the risk evaluation pipeline:
//
//    IGoalRiskSettingsProvider  →  loads GoalRiskSetting from the database
//    IGoalRiskEvaluator         →  produces RiskEvaluationResult per goal
//    GoalRiskApplicationService →  assembles GoalWithRiskDto for callers
//
//  All types are immutable (init-only properties) to prevent accidental
//  mutation after the evaluation pipeline has completed.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Immutable result of evaluating a single goal against the active risk
/// thresholds.  Produced by <c>IGoalRiskEvaluator.Evaluate()</c>.
/// </summary>
public sealed class RiskEvaluationResult
{
    /// <summary>Whether the goal was flagged as at risk.</summary>
    public bool IsAtRisk { get; init; }

    /// <summary>
    /// Human-readable description of why the goal is at risk.
    /// <c>null</c> when <see cref="IsAtRisk"/> is <c>false</c>.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// Numeric indicator of risk urgency.
    ///   70 → Rule 1: low progress near deadline (higher urgency)
    ///   60 → Rule 2: behind expected timeline
    ///    0 → not at risk
    /// The score is intentionally a simple integer rather than an enum so
    /// that future rules can introduce additional severity bands without
    /// changing the contract.
    /// </summary>
    public int SeverityScore { get; init; }

    /// <summary>
    /// Canonical not-at-risk singleton.  Returned by the evaluator whenever
    /// no evaluation rule fires, avoiding unnecessary allocations.
    /// </summary>
    public static RiskEvaluationResult NotAtRisk { get; } =
        new() { IsAtRisk = false, Reason = null, SeverityScore = 0 };
}

/// <summary>
/// Projection combining goal data with the outcome of its risk evaluation.
/// Produced by <c>GoalRiskApplicationService.GetGoalsWithRiskAsync()</c>.
/// </summary>
public sealed class GoalWithRiskDto
{
    public Guid    GoalId            { get; init; }
    public string  Title             { get; init; } = string.Empty;
    public string  EmployeeName      { get; init; } = string.Empty;
    public decimal ProgressPercent   { get; init; }
    public DateOnly DueDate          { get; init; }

    /// <summary><c>true</c> if the risk evaluator flagged this goal.</summary>
    public bool    IsAtRisk          { get; init; }

    /// <summary>Populated only when <see cref="IsAtRisk"/> is <c>true</c>.</summary>
    public string? RiskReason        { get; init; }

    /// <summary>0 when not at risk; higher value = greater urgency.</summary>
    public int     RiskSeverityScore { get; init; }
}
