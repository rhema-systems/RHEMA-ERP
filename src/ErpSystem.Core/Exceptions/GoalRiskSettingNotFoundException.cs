namespace ErpSystem.Core.Exceptions;

/// <summary>
/// No longer thrown by anything, and kept only so that catching it still compiles.
///
/// <c>GoalRiskSettingsProvider</c> used to raise this when a tenant had no active
/// <c>GoalRiskSetting</c> row. Nothing seeds that table — the migration creates it empty,
/// despite what the old message here claimed — so on any fresh tenant it took down every
/// at-risk read: five Team Goals tabs plus the org-wide report, all 500. The provider now
/// falls back to <c>GoalRiskSettingDefaults</c>, and HR sets its own thresholds through
/// <c>IGoalRiskSettingsService</c>.
///
/// Safe to delete.
/// </summary>
public sealed class GoalRiskSettingNotFoundException : Exception
{
    public GoalRiskSettingNotFoundException()
        : base("No active GoalRiskSetting record was found.")
    { }

    public GoalRiskSettingNotFoundException(string message)
        : base(message)
    { }

    public GoalRiskSettingNotFoundException(string message, Exception inner)
        : base(message, inner)
    { }
}
