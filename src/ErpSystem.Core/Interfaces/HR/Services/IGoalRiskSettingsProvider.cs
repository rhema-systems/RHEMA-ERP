using ErpSystem.Core.Entities.HR.Performance;

namespace ErpSystem.Core.Interfaces.HR.Services;

/// <summary>
/// Provides the active <see cref="GoalRiskSetting"/> configuration record.
///
/// Implementations must:
///   • Query the database using AsNoTracking (read-only path).
///   • Return the single active record.
///   • Fall back to <see cref="GoalRiskSettingDefaults"/> when the tenant has none,
///     rather than throwing. Nothing seeds the table, and every at-risk read in the
///     manager workspace and the org-wide report goes through here — failing would take
///     all of them down for a tenant that has simply never opened the settings screen.
///
/// This interface is intentionally thin: callers load settings once per
/// request and pass the result directly to <see cref="IGoalRiskEvaluator"/>.
/// Writing the thresholds is a separate concern, on
/// <see cref="ErpSystem.Core.Interfaces.HR.IGoalRiskSettingsService"/>.
/// </summary>
public interface IGoalRiskSettingsProvider
{
    /// <summary>
    /// Returns the goal-risk thresholds in force: the tenant's active
    /// <see cref="GoalRiskSetting"/>, or a transient record carrying the documented
    /// defaults when it has not configured its own.
    /// </summary>
    Task<GoalRiskSetting> GetActiveAsync(CancellationToken cancellationToken = default);
}
