namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// Outcome of a year-end processing run.
/// </summary>
public class LeaveYearEndResult
{
    public int BalancesProcessed { get; set; }
    public int BalancesAffected { get; set; }
    public decimal TotalDaysCarriedOver { get; set; }
    public decimal TotalDaysForfeited { get; set; }
    public List<string> Notes { get; set; } = new();
}

/// <summary>
/// Admin-triggered year-end / cut-off processing for leave: carry-over rollover, carry-over
/// expiry, and forfeiture of unused accrual ("force leave"). Implemented as on-demand operations
/// first; a schedule can drive them later.
/// </summary>
public interface ILeaveYearEndService
{
    /// <summary>
    /// Rolls each employee's remaining available days for <paramref name="fromYear"/> into the
    /// next year's <c>CarriedOverDays</c>, capped at the leave type's <c>MaxCarryOverDays</c>.
    /// Only applies to leave types with <c>AllowCarryOver</c>. Idempotent: re-running recomputes
    /// the carried-over figure rather than stacking it.
    /// </summary>
    Task<LeaveYearEndResult> ProcessCarryOverAsync(int fromYear, Guid? employeeId = null, CancellationToken ct = default);

    /// <summary>
    /// For leave types configured with <c>ForfeitUnusedAfterMonths</c>, once the cut-off date
    /// (year start + that many months) has passed, forfeits the unused accrued balance by posting
    /// a negative "Forfeiture" adjustment. Also zeroes carried-over days whose
    /// <c>CarryOverExpiryMonths</c> window has elapsed. Idempotent per balance.
    /// </summary>
    Task<LeaveYearEndResult> ProcessForfeitureAsync(int year, DateOnly? asOf = null, Guid? employeeId = null, CancellationToken ct = default);
}
