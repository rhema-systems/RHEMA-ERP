namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// Recalculates leave balance figures (UsedDays, PendingDays, AdjustmentDays) from source data.
/// EntitledDays and CarriedOverDays are never overwritten.
/// If a balance record does not exist it will be created with defaults from the leave type.
/// </summary>
public interface ILeaveBalanceRecalculationService
{
    /// <summary>
    /// Recomputes UsedDays, PendingDays, and AdjustmentDays for a single
    /// (employee, leave-type, year) balance record.
    /// </summary>
    Task RecalculateAsync(Guid employeeId, Guid leaveTypeId, int year);

    /// <summary>
    /// Recomputes all leave-type balances for an employee in the given year.
    /// </summary>
    Task RecalculateAllAsync(Guid employeeId, int year);
}
