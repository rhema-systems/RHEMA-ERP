using ErpSystem.Core.DTOs.HR;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// Authoritative source for an employee's effective emoluments (basic pay + allowances −
/// deductions), composed from position-level defaults and employee-level overrides, and for the
/// per-day leave-encashment rate derived from those emoluments. Reusable beyond leave (payroll).
/// </summary>
public interface IEmolumentService
{
    // ── Pay component master ──
    Task<IEnumerable<PayComponentDto>> GetPayComponentsAsync(bool activeOnly = true);
    Task<PayComponentDto> GetPayComponentByIdAsync(Guid id);
    Task<PayComponentDto> CreatePayComponentAsync(CreatePayComponentDto dto);
    Task<PayComponentDto> UpdatePayComponentAsync(Guid id, UpdatePayComponentDto dto);
    Task DeactivatePayComponentAsync(Guid id);

    // ── Position-level assignment ──
    Task<IEnumerable<PositionPayComponentDto>> GetPositionComponentsAsync(Guid positionId);
    Task<PositionPayComponentDto> AssignPositionComponentAsync(CreatePositionPayComponentDto dto);
    Task<PositionPayComponentDto> UpdatePositionComponentAsync(Guid id, decimal? amount, bool isActive);
    Task RemovePositionComponentAsync(Guid id);

    // ── Employee-level override / addition ──
    Task<IEnumerable<EmployeePayComponentDto>> GetEmployeeComponentsAsync(Guid employeeId);
    Task<EmployeePayComponentDto> AssignEmployeeComponentAsync(CreateEmployeePayComponentDto dto);
    Task<EmployeePayComponentDto> UpdateEmployeeComponentAsync(Guid id, UpdateEmployeePayComponentDto dto);
    Task RemoveEmployeeComponentAsync(Guid id);

    // ── Consolidated / derived ──
    Task<EmployeeEmolumentSummaryDto> GetEmployeeEmolumentSummaryAsync(Guid employeeId, DateOnly? asOf = null);

    /// <summary>Monthly basic pay for the employee as of a date (salary assignment notch, else Employee.Salary).</summary>
    Task<decimal> GetMonthlyBasicPayAsync(Guid employeeId, DateOnly asOf);

    /// <summary>Per-day encashment rate for an employee/leave-type pair, per the leave type's rate policy.</summary>
    Task<decimal> GetEncashmentDailyRateAsync(Guid employeeId, Guid leaveTypeId, DateOnly asOf);
}
