namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// Point-in-time view of an employee's entitlement for one leave type / year.
/// </summary>
public class LeaveEntitlementSnapshot
{
    /// <summary>Full annual entitlement (the yearly cap), resolved from subtype/allocation/default.</summary>
    public decimal AnnualEntitledDays { get; set; }

    /// <summary>Days accrued by the as-of date, per the accrual policy. Capped at the annual entitlement.</summary>
    public decimal AccruedToDateDays { get; set; }

    /// <summary>True when an active accrual policy governs this leave type (otherwise the full entitlement is available immediately).</summary>
    public bool HasAccrualPolicy { get; set; }

    /// <summary>True when the employee has met the service gate and may apply for this leave type.</summary>
    public bool IsAccessible { get; set; }

    /// <summary>The date from which the employee may first apply for this leave type (service gate), if known.</summary>
    public DateOnly? AccessibleFrom { get; set; }
}

/// <summary>
/// Single authority for "how many days is this employee entitled to / accrued-to-date" for a
/// leave type and year. Accrual is computed on read (no scheduled job): the engine derives the
/// accrued figure from the employee's service and the accrual policy whenever asked.
/// </summary>
public interface ILeaveEntitlementService
{
    /// <summary>
    /// Resolves the full annual entitlement. Precedence: leave-subtype <c>MaxDaysAllowed</c> →
    /// effective-dated <c>LeaveCategoryAllocation</c> for the employee's staff level →
    /// <c>LeaveType.DefaultDaysPerYear</c>.
    /// </summary>
    Task<decimal> ResolveAnnualEntitlementAsync(Guid employeeId, Guid leaveTypeId, Guid? leaveSubTypeId, int year, CancellationToken ct = default);

    /// <summary>
    /// Days accrued as of <paramref name="asOf"/> (defaults to today), capped at the annual
    /// entitlement. Returns the full entitlement when no active accrual policy applies.
    /// </summary>
    Task<decimal> GetAccruedAsOfAsync(Guid employeeId, Guid leaveTypeId, Guid? leaveSubTypeId, int year, DateOnly? asOf = null, CancellationToken ct = default);

    /// <summary>
    /// Whether the employee has met the leave type's service gate (<c>MinServiceMonthsToAccess</c>)
    /// and may apply for it as of <paramref name="asOf"/> (defaults to today).
    /// </summary>
    Task<bool> IsAccessibleAsync(Guid employeeId, Guid leaveTypeId, DateOnly? asOf = null, CancellationToken ct = default);

    /// <summary>Convenience roll-up of entitlement, accrued-to-date and accessibility.</summary>
    Task<LeaveEntitlementSnapshot> GetSnapshotAsync(Guid employeeId, Guid leaveTypeId, Guid? leaveSubTypeId, int year, DateOnly? asOf = null, CancellationToken ct = default);
}
