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

    /// <summary>
    /// Re-derives <c>EntitledDays</c> on stored balances from the rulebook, and reports every
    /// change. ⚠ <b>Explicitly invoked. Never a side effect of recalculation.</b>
    /// </summary>
    /// <remarks>
    /// <para><b>Why it exists (entitlement plan A3).</b> <c>EntitledDays</c> is written once, when
    /// the balance row is created, and no code path has ever brought it back into line with the
    /// rulebook afterwards. So an allocation corrected mid-year does not reach the balances that
    /// already exist, and — until A1 was fixed — a balance opened by posting an adjustment recorded
    /// the leave type's DEFAULT days rather than the employee's staff-level allocation, with no way
    /// to put it right. Fixing the creation site helps nobody who is already wrong.</para>
    ///
    /// <para>⚠ <b>Why it is separate from <see cref="RecalculateTenantAsync"/>, which is the same
    /// shape.</b> That one DERIVES counters from rows that exist: running it twice gives the same
    /// answer as running it once, and it cannot restate anything. This one OVERWRITES a stored
    /// figure from configuration that may have changed since. Re-deriving entitlement on every
    /// recalculation would silently rewrite history the moment somebody edited an allocation with a
    /// retrospective effective date — the balance would move and nothing would say why.</para>
    ///
    /// <para>⚠ <b>Hence the dry run</b>, which the tenant-wide recalculation deliberately does not
    /// have. The preview is the whole point: it names every row it would change and both figures,
    /// so the decision to overwrite is taken by a person looking at the list.</para>
    ///
    /// <para><b>It does not cascade.</b> <c>EntitledDays</c> is an input to no other stored counter
    /// — availability is computed — so nothing needs recalculating afterwards. ⚠ But a carry-over
    /// already run for this year used the OLD figure, and this pass does not revisit it. That is
    /// reported rather than corrected: re-running carry-over is a decision of its own.</para>
    /// </remarks>
    Task<LeaveEntitlementRepairResult> RepairEntitlementsAsync(
        int year, Guid? leaveTypeId = null, Guid? employeeId = null, bool dryRun = false,
        CancellationToken ct = default);
}

/// <summary>What an entitlement repair pass found, and what it changed.</summary>
public class LeaveEntitlementRepairResult
{
    public int Year { get; set; }

    /// <summary>
    /// ⚠ <b>True when NOTHING WAS WRITTEN.</b> The pass resolved every entitlement and reported
    /// what it would have done. Same contract as the year-end jobs' preview.
    /// </summary>
    public bool IsDryRun { get; set; }

    /// <summary>Balances looked at. ⚠ Not the number changed — see <see cref="BalancesChanged"/>.</summary>
    public int BalancesExamined { get; set; }

    /// <summary>Balances whose stored entitlement disagreed with the rulebook.</summary>
    public int BalancesChanged { get; set; }

    /// <summary>Examined and already correct. Always <c>Examined - Changed - Failed</c>.</summary>
    public int BalancesAlreadyCorrect { get; set; }

    /// <summary>
    /// ⚠ Balances whose entitlement could not be resolved — a missing leave type, an employee with
    /// no position and therefore no staff level. Counted and skipped; the pass continues.
    /// </summary>
    public int BalancesFailed { get; set; }

    /// <summary>
    /// ⚠ Of the changed rows, how many already carried days into the following year. Those
    /// carry-overs were computed from the OLD entitlement and this pass does not revisit them.
    /// </summary>
    public int ChangedWithCarryOverAlreadyRun { get; set; }

    /// <summary>One line per row it would change, naming both figures, plus a summary first.</summary>
    public List<string> Notes { get; set; } = new();
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
