namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// Outcome of a year-end processing run.
/// </summary>
public class LeaveYearEndResult
{
    /// <summary>
    /// How many balances the run LOOKED AT. ⚠ Not how many it changed.
    /// </summary>
    /// <remarks>
    /// ⚠ <b>The name reads as "did something to", and finding L-26 is about that.</b> A run
    /// that examined 900 balances and changed 12 reported "900 processed", which is true of the
    /// loop and false of the work. It is kept under this name because callers use it, and
    /// <see cref="BalancesSkipped"/> now sits beside it so the two numbers cannot be confused.
    /// </remarks>
    public int BalancesProcessed { get; set; }

    /// <summary>How many balances the run actually changed.</summary>
    public int BalancesAffected { get; set; }

    /// <summary>
    /// Examined but left alone — the leave type does not allow carry-over, there was nothing
    /// remaining, or no rule applied. Always <c>BalancesProcessed - BalancesAffected</c>.
    /// </summary>
    public int BalancesSkipped { get; set; }

    /// <summary>
    /// ⚠ <b>True when NOTHING WAS WRITTEN.</b> The run computed exactly what it would have done
    /// and rolled nothing into the database (finding L-24).
    /// </summary>
    /// <remarks>
    /// Carry-over and forfeiture both move people's balances in bulk and there is no undo for
    /// either. A preview is the difference between finding a misconfigured leave type before the
    /// run and finding it in nine hundred balances afterwards.
    /// </remarks>
    public bool IsDryRun { get; set; }
    public decimal TotalDaysCarriedOver { get; set; }
    public decimal TotalDaysForfeited { get; set; }

    /// <summary>
    /// Carried-over days that lapsed because they were not taken before the leave type's carry-over
    /// expiry (round 5, lane G). Counted apart from forfeiture: a different rule, and a different
    /// answer to "where did my days go".
    /// </summary>
    public decimal TotalDaysExpired { get; set; }

    /// <summary>The first note is the run's summary in words; the rest are per balance.</summary>
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
    /// <remarks>
    /// Since round 5, lane G: refused for a year that has not ended, unless <paramref name="dryRun"/>;
    /// one pot per leave type, carried into the next year's type-level balance; and the closing
    /// year's own carried days count only as far as they were still usable, so days that had lapsed
    /// never travel again.
    /// </remarks>
    /// <param name="dryRun">⚠ Compute and report, write nothing.</param>
    Task<LeaveYearEndResult> ProcessCarryOverAsync(int fromYear, Guid? employeeId = null, bool dryRun = false, CancellationToken ct = default);

    /// <summary>
    /// For leave types configured with <c>ForfeitUnusedAfterMonths</c>, once the cut-off date
    /// (year start + that many months) has passed, forfeits the unused accrued balance by posting
    /// a negative "Forfeiture" adjustment. Also expires the carried-over days not taken before their
    /// <c>CarryOverExpiryMonths</c> window closed — only those; the ones taken in time stay (round 5,
    /// lane G). Idempotent per balance.
    /// </summary>
    /// <param name="dryRun">⚠ Compute and report, write nothing.</param>
    Task<LeaveYearEndResult> ProcessForfeitureAsync(int year, DateOnly? asOf = null, Guid? employeeId = null, bool dryRun = false, CancellationToken ct = default);
}
