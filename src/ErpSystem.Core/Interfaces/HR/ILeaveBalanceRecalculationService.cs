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

    /// <summary>
    /// Recomputes every balance in the tenant for a year, optionally narrowed to one leave type.
    /// Returns how many employees were touched.
    /// </summary>
    /// <remarks>
    /// <para>⚠ <b>Why this exists (finding L-19).</b> Recalculation was one employee at a time,
    /// which is right for the ordinary case — it runs after every approval, cancellation and
    /// adjustment. But a policy correction reaches everybody: change an accrual rule, fix an
    /// entitlement, or land the <c>UsedDays</c> correction that G1 shipped, and nine hundred
    /// balances are stale with no route through the UI to put them right.</para>
    ///
    /// <para><b>It is safe to run and safe to re-run.</b> Recalculation DERIVES
    /// <c>UsedDays</c>, <c>PendingDays</c> and <c>AdjustmentDays</c> from the requests and
    /// adjustments that already exist; it never invents a figure and never touches
    /// <c>EntitledDays</c> or <c>CarriedOverDays</c>. That is what makes it different from the
    /// year-end jobs, which MOVE balances and therefore needed a dry run — there is nothing here to
    /// preview, because running it twice produces the same answer as running it once.</para>
    ///
    /// <para>⚠ It is still heavy: one pass per employee per leave type. Admin-tier, and it reports
    /// what it did rather than returning silently.</para>
    /// </remarks>
    Task<LeaveBulkRecalculationResult> RecalculateTenantAsync(
        int year, Guid? leaveTypeId = null, CancellationToken ct = default);
}

/// <summary>What a tenant-wide recalculation did.</summary>
public class LeaveBulkRecalculationResult
{
    public int Year { get; set; }
    public Guid? LeaveTypeId { get; set; }

    /// <summary>Employees whose balances were recomputed.</summary>
    public int EmployeesProcessed { get; set; }

    /// <summary>
    /// ⚠ Employees the run could not finish. Non-zero is a bug signal, not routine — the whole
    /// point of a derived figure is that deriving it cannot fail.
    /// </summary>
    public int EmployeesFailed { get; set; }

    public List<string> Notes { get; set; } = new();
}
