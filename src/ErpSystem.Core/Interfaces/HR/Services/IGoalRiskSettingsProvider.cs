using ErpSystem.Core.Entities.HR.Performance;

namespace ErpSystem.Core.Interfaces.HR.Services;

/// <summary>
/// Provides the active <see cref="GoalRiskSetting"/> configuration record.
///
/// Implementations must:
///   • Query the database using AsNoTracking (read-only path).
///   • Return the single active record.
///   • Throw <see cref="ErpSystem.Core.Exceptions.GoalRiskSettingNotFoundException"/>
///     when no active record exists.
///
/// This interface is intentionally thin: callers load settings once per
/// request and pass the result directly to <see cref="IGoalRiskEvaluator"/>.
/// </summary>
public interface IGoalRiskSettingsProvider
{
    /// <summary>
    /// Returns the active <see cref="GoalRiskSetting"/> configuration.
    /// </summary>
    /// <exception cref="ErpSystem.Core.Exceptions.GoalRiskSettingNotFoundException">
    /// Thrown when no row with <c>IsActive == true</c> exists.
    /// </exception>
    Task<GoalRiskSetting> GetActiveAsync(CancellationToken cancellationToken = default);
}
