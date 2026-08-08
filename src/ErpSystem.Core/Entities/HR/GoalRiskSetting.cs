using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.Entities.HR.Performance;

/// <summary>
/// Configurable thresholds used by the Goal Risk evaluation engine.
///
/// Only one record with <see cref="IsActive"/> == <c>true</c> should exist at
/// any given time.  The risk evaluator (<c>IGoalRiskEvaluator</c>) is purely
/// in-memory; it never touches the database — all database access is performed
/// by <c>IGoalRiskSettingsProvider</c>, which loads this record once per
/// request and passes it to the evaluator.
///
/// This entity is NOT tenant-scoped: the risk thresholds are a system-wide
/// configuration concern, managed by HR administrators.  If per-tenant
/// configuration is required in the future, add a nullable TenantId and
/// change the provider query accordingly.
/// </summary>
public sealed class GoalRiskSetting : TenantEntity
{
    /// <summary>
    /// Goals with fewer than this many days remaining before their due date
    /// are considered "close to deadline".  Combined with
    /// <see cref="MinimumProgressPercent"/> to trigger Rule 1 of the evaluator.
    /// Default: 14 days.
    /// </summary>
    [Required]
    public int DaysRemainingThreshold { get; set; }

    /// <summary>
    /// Minimum progress percentage a goal must have reported when it is
    /// within <see cref="DaysRemainingThreshold"/> days of its due date.
    /// Goals below this threshold are flagged as at-risk via Rule 1.
    /// Default: 60 (%).
    /// </summary>
    [Required]
    public int MinimumProgressPercent { get; set; }

    /// <summary>
    /// Tolerance band applied when comparing actual progress against the
    /// linearly-expected progress for the elapsed duration.
    /// A goal is flagged only when:
    ///   actualProgress + tolerance &lt; expectedProgress
    /// Larger values make the evaluator more lenient.
    /// Default: 20 (%).
    /// </summary>
    [Required]
    public int ExpectedProgressTolerancePercent { get; set; }

    /// <summary>
    /// Marks this as the active configuration record.
    /// The system loads the first record where <c>IsActive == true</c>.
    /// Only one active record should exist; deactivate old records before
    /// inserting a new one.
    /// </summary>
    [Required]
    public bool IsActive { get; set; }
}
