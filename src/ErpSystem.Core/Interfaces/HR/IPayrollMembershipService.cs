using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.DTOs.HR.Payroll;
using ErpSystem.Core.Entities.HR;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// HR's payroll-membership statement (<see cref="Employee.IsOnPayroll"/>) set beside payroll's own
/// answer (its employee profile). See <c>PayrollMembershipService</c> for why the two are kept as
/// separate facts and how far HR reaches into payroll.
/// </summary>
public interface IPayrollMembershipService
{
    /// <summary>Both sides for one employee, with the discrepancy (if any) named.</summary>
    Task<EmployeePayrollStatusDto> GetStatusAsync(Guid employeeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every active employee where HR's statement and payroll's profile disagree, or where an
    /// on-payroll statement has nothing to pay from. Consistent rows are not listed.
    /// </summary>
    Task<PayrollReconciliationDto> GetReconciliationAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Enrols an on-payroll employee in payroll when payroll has no profile for them yet.
    /// Create-only: an existing profile is never touched. Returns true when a profile was created.
    /// Failure is logged and reported through the reconciliation read, never thrown — the HR save
    /// that triggered it has already succeeded.
    /// </summary>
    Task<bool> EnsurePayrollProfileAsync(Employee employee, CancellationToken cancellationToken = default);

    /// <summary>Whether payroll's profile for this employee exists and is switched on.</summary>
    Task<(bool HasProfile, bool PayrollActive)> GetPayrollProfileStateAsync(Guid employeeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Payroll's employee profile — salary basis, switches, payment methods, loans — for one
    /// employee, read through payroll's own service. Null when payroll has no profile for them.
    /// </summary>
    /// <remarks>
    /// ⚠ Payroll exposes no by-employee read (round-2 plan § 7.1, item 1): its list takes a search
    /// term over number and name and stops at 250 rows. This filters that list to the exact
    /// employee, using the staff number as the term so the cap is never the reason a person is
    /// missing. It is HR's door onto payroll's window and adds nothing to payroll's controller.
    /// </remarks>
    Task<PayrollEmployeeProfileDto?> GetPayrollProfileAsync(Guid employeeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The monthly basic pay HR would quote for this person today, and where the figure came from.
    /// </summary>
    /// <remarks>
    /// The one resolution every HR reader of "what is this person paid" goes through — the
    /// separation settlement's daily rate, the emolument summary, the reconciliation. On the scale:
    /// the notch amount, else the level mid-point, else the flat figure on the record. Negotiated:
    /// payroll's active basis, else the flat figure. Off payroll: nothing. Returns
    /// <c>(null, reason)</c> when there is no figure, so the caller can print why rather than 0.
    /// </remarks>
    Task<(decimal? MonthlyBasicPay, string Source)> ResolveMonthlyBasicPayAsync(Guid employeeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Round 3, lane S — the one write of a payroll FIGURE from HR, for an APPROVED salary change.
    /// Reads payroll's full profile, changes only the salary basis, re-sends through payroll's own
    /// upsert with every payment method and component carried (the upsert is a replace-set), then
    /// re-reads and refuses to report success if the round trip changed anything else. Returns the
    /// failure in words rather than throwing: the caller records it on the request.
    /// </summary>
    Task<PayrollBasicWriteResult> UpdateMonthlyBasicAsync(Guid employeeId, decimal monthlyBasic, string? currencyCode, DateTime effectiveFrom, CancellationToken cancellationToken = default);
}

/// <summary>Outcome of <see cref="IPayrollMembershipService.UpdateMonthlyBasicAsync"/>.</summary>
public sealed record PayrollBasicWriteResult(bool Success, string? Failure)
{
    public static PayrollBasicWriteResult Ok() => new(true, null);
    public static PayrollBasicWriteResult Failed(string why) => new(false, why);
}
