namespace ErpSystem.Core.Entities.HR.Performance;

/// <summary>
/// The goal-risk thresholds a tenant gets before anyone configures its own.
///
/// Nothing seeds <see cref="GoalRiskSetting"/> — the table is created empty and only gains a row
/// when HR saves the thresholds screen. These values are the ones the entity's own documentation
/// calls the defaults, kept in one place so the provider's fallback and a first save agree.
/// </summary>
public static class GoalRiskSettingDefaults
{
    public const int DaysRemainingThreshold = 14;
    public const int MinimumProgressPercent = 60;
    public const int ExpectedProgressTolerancePercent = 20;

    /// <summary>
    /// A transient settings record carrying the defaults. Not tracked and never saved — the
    /// provider hands it to the evaluator exactly as it would a persisted row.
    /// </summary>
    public static GoalRiskSetting Create(Guid tenantId) => new()
    {
        TenantId = tenantId,
        DaysRemainingThreshold = DaysRemainingThreshold,
        MinimumProgressPercent = MinimumProgressPercent,
        ExpectedProgressTolerancePercent = ExpectedProgressTolerancePercent,
        IsActive = true,
    };
}
