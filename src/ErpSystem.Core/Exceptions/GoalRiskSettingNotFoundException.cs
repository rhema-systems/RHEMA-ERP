namespace ErpSystem.Core.Exceptions;

/// <summary>
/// Thrown by <see cref="ErpSystem.Core.Services.HR.Appraisal.GoalRiskSettingsProvider"/>
/// when no active <c>GoalRiskSetting</c> record exists in the database.
///
/// This indicates a data-configuration problem, not a user error.
/// Callers should map this to HTTP 500 (Internal Server Error) or surface it
/// as an operations alert — the application cannot evaluate goal risk until
/// the database has a valid active record seeded.
/// </summary>
public sealed class GoalRiskSettingNotFoundException : Exception
{
    public GoalRiskSettingNotFoundException()
        : base("No active GoalRiskSetting record was found. " +
               "Ensure the GoalRiskSetting table contains exactly one row where IsActive = true. " +
               "Run the database migration (which includes seed data) to resolve this.")
    { }

    public GoalRiskSettingNotFoundException(string message)
        : base(message)
    { }

    public GoalRiskSettingNotFoundException(string message, Exception inner)
        : base(message, inner)
    { }
}
