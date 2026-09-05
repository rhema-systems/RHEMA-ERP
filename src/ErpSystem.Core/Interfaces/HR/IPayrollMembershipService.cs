using ErpSystem.Core.DTOs.HR;
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
}
