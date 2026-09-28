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
    //
    // Ownership is split. Payroll owns code, name, type, calculation basis, default amount,
    // taxability and the active flag; those are mirrored by IPayComponentProjectionService and
    // the three write methods below throw PayrollOwnedException (surfaced as 409). HR owns
    // pension, tax treatment, gross-pay effect and effective dating, because payroll models none
    // of them and emoluments and the leave-encashment rate read them.
    Task<IEnumerable<PayComponentDto>> GetPayComponentsAsync(bool activeOnly = true);
    Task<PayComponentDto> GetPayComponentByIdAsync(Guid id);

    /// <summary>Forces a payroll → HR projection pass and reports what changed.</summary>
    Task<PayComponentProjectionResultDto> SyncPayComponentsAsync(CancellationToken ct = default);

    /// <summary>Updates the HR-owned attributes of a component, mirrored or not.</summary>
    Task<PayComponentDto> UpdatePayComponentHrAttributesAsync(Guid id, UpdatePayComponentHrAttributesDto dto);

    /// <summary>Not supported — payroll owns the component master. Throws PayrollOwnedException.</summary>
    Task<PayComponentDto> CreatePayComponentAsync(CreatePayComponentDto dto);

    /// <summary>Not supported — payroll owns these fields. Throws PayrollOwnedException.</summary>
    Task<PayComponentDto> UpdatePayComponentAsync(Guid id, UpdatePayComponentDto dto);

    /// <summary>Not supported — payroll owns the active flag. Throws PayrollOwnedException.</summary>
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

    // The per-day encashment rate (and its EncashmentDailyRate record) left with leave settings
    // audit 2 (L-73): Finance values leave; HR records the days.
}
