using System.ComponentModel.DataAnnotations;

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

// ─────────────────────────────────────────────────────────────────────────────
//  Goal Risk Settings DTOs
//
//  The administration side of the pipeline above: the thresholds themselves,
//  read and written through IGoalRiskSettingsService.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>The goal-risk thresholds in force for the tenant.</summary>
public sealed class GoalRiskSettingsDto
{
    /// <summary>Null until the tenant saves its own thresholds.</summary>
    public Guid? Id { get; init; }

    /// <summary>
    /// False when these are the built-in defaults rather than a stored row. The screen uses
    /// this to say the tenant is running on defaults, and to decide whether "reset" does anything.
    /// </summary>
    public bool IsConfigured { get; init; }

    /// <summary>Days before the due date at which a goal counts as close to deadline.</summary>
    public int DaysRemainingThreshold { get; init; }

    /// <summary>Progress a goal must have reached by then to escape being flagged.</summary>
    public int MinimumProgressPercent { get; init; }

    /// <summary>Slack allowed against linearly-expected progress before flagging.</summary>
    public int ExpectedProgressTolerancePercent { get; init; }

    public DateTime? UpdatedAt { get; init; }
    public string? UpdatedBy { get; init; }
}

/// <summary>Write model for the goal-risk thresholds.</summary>
public sealed class UpdateGoalRiskSettingsDto
{
    [Range(1, 365)]
    public int DaysRemainingThreshold { get; set; }

    [Range(0, 100)]
    public int MinimumProgressPercent { get; set; }

    [Range(0, 100)]
    public int ExpectedProgressTolerancePercent { get; set; }
}
