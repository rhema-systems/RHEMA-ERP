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

    /// <summary>Per-day encashment rate for an employee/leave-type pair, per the leave type's rate policy.</summary>
    /// <remarks>
    /// Returns the sentence that describes the rate as well as the rate — see
    /// <see cref="EncashmentDailyRate"/> for why the two travel together.
    /// </remarks>
    Task<EncashmentDailyRate> GetEncashmentDailyRateAsync(Guid employeeId, Guid leaveTypeId, DateOnly asOf);
}

/// <summary>
/// A per-day encashment rate, and the sentence describing how it was arrived at.
/// </summary>
/// <param name="Rate">The figure, per day.</param>
/// <param name="Basis">
/// How it was computed, in words — e.g. <c>"GHS 6,000.00 (basic + allowances) ÷ 22 working days =
/// GHS 272.7273 per day"</c>.
/// </param>
/// <remarks>
/// <para>⚠ <b>The divisor and the sentence are produced together, from the same number.</b> That is
/// the whole point of returning them as a pair: words assembled anywhere else could describe a basis
/// other than the one that produced the figure beside them, and nobody reading the payout would be
/// able to tell.</para>
///
/// <para>The same guarantee the final settlement already gives (<c>SeparationService.DailyRateAsync</c>),
/// now given by leave — which matters more here, because the two use <b>different bases by design</b>
/// and a payout that cannot say which one produced it is unauditable. See
/// <c>CompanyHrPolicySettings.EncashmentWorkingDaysPerMonth</c> for why they differ and why they are
/// not merged.</para>
/// </remarks>
public sealed record EncashmentDailyRate(decimal Rate, string Basis);
