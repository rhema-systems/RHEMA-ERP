using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.HR;

// ─── Pay Component (master) ────────────────────────────────────────────────────

public class PayComponentDto
{
    public Guid Id { get; set; }

    // ── Payroll-owned ────────────────────────────────────────────────────────
    // Mirrored from PayrollComponent and overwritten on every projection pass.
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public PayComponentType ComponentType { get; set; }
    public PayComponentCalculationBasis CalculationBasis { get; set; }
    public decimal? DefaultAmount { get; set; }
    public bool IsTaxable { get; set; }
    public bool IsActive { get; set; }

    // ── HR-owned ─────────────────────────────────────────────────────────────
    // Payroll has no equivalent for these, so the projection never touches them
    // and they stay editable. See UpdatePayComponentHrAttributesDto.
    /// <summary>Whether this component counts toward pension/SSNIT contributions.</summary>
    public bool IsPensionable { get; set; }

    /// <summary>False for notional benefit-in-kind lines that do not add to gross pay.</summary>
    public bool AffectsGrossPay { get; set; }

    /// <summary>Income-tax treatment HR asserts for this component.</summary>
    public TaxTreatmentType StatutoryTreatment { get; set; }

    /// <summary>
    /// Emoluments and the leave-encashment rate filter on this window, so it is HR-owned:
    /// payroll models no effective dating at all.
    /// </summary>
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }

    // ── Provenance ───────────────────────────────────────────────────────────
    /// <summary>
    /// True when this row mirrors a payroll component. The payroll-owned fields above are then
    /// read-only and every write to them returns 409; only the HR-owned fields can be edited.
    /// </summary>
    public bool IsPayrollDefined { get; set; }
}

/// <summary>
/// The subset of a pay component HR owns. Payroll models none of these, so they remain editable
/// on mirrored components — without this they would be stuck at their defaults forever, which
/// would misstate SSNIT and tax treatment on every emolument that reads them.
/// </summary>
public class UpdatePayComponentHrAttributesDto
{
    public bool IsPensionable { get; set; }
    public bool AffectsGrossPay { get; set; } = true;
    public TaxTreatmentType StatutoryTreatment { get; set; } = TaxTreatmentType.PAYE;
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
}

/// <summary>Outcome of a payroll → HR pay-component projection pass.</summary>
public class PayComponentProjectionResultDto
{
    public int Created { get; set; }
    public int Updated { get; set; }
    public int Deactivated { get; set; }
    public bool SkippedAsUnchanged { get; set; }

    /// <summary>
    /// Non-fatal issues: duplicate payroll codes, and employer-contribution components that have
    /// no HR equivalent and were skipped.
    /// </summary>
    public List<string> Warnings { get; set; } = new();
}

public class CreatePayComponentDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public PayComponentType ComponentType { get; set; } = PayComponentType.Allowance;
    public PayComponentCalculationBasis CalculationBasis { get; set; } = PayComponentCalculationBasis.FixedAmount;
    public decimal? DefaultAmount { get; set; }
    public bool IsTaxable { get; set; } = true;
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
}

public class UpdatePayComponentDto : CreatePayComponentDto
{
    public bool IsActive { get; set; } = true;
}

// ─── Position-level assignment ─────────────────────────────────────────────────

public class PositionPayComponentDto
{
    public Guid Id { get; set; }
    public Guid PositionId { get; set; }
    public string PositionTitle { get; set; } = string.Empty;
    public Guid PayComponentId { get; set; }
    public string PayComponentName { get; set; } = string.Empty;
    public PayComponentType ComponentType { get; set; }
    public decimal? Amount { get; set; }
    public decimal? DefaultAmount { get; set; }
    public bool IsActive { get; set; }
}

public class CreatePositionPayComponentDto
{
    public Guid PositionId { get; set; }
    public Guid PayComponentId { get; set; }
    public decimal? Amount { get; set; }
}

// ─── Employee-level assignment / override ──────────────────────────────────────

public class EmployeePayComponentDto
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public Guid PayComponentId { get; set; }
    public string PayComponentName { get; set; } = string.Empty;
    public PayComponentType ComponentType { get; set; }
    public decimal Amount { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public bool IsActive { get; set; }
}

public class CreateEmployeePayComponentDto
{
    public Guid EmployeeId { get; set; }
    public Guid PayComponentId { get; set; }
    public decimal Amount { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
}

public class UpdateEmployeePayComponentDto : CreateEmployeePayComponentDto
{
    public bool IsActive { get; set; } = true;
}

// ─── Consolidated emolument view ───────────────────────────────────────────────

/// <summary>One line in an employee's effective emolument package, with its source.</summary>
public class EffectivePayComponentDto
{
    public Guid PayComponentId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public PayComponentType ComponentType { get; set; }
    public PayComponentCalculationBasis CalculationBasis { get; set; }
    /// <summary>The resolved monetary amount for this component (percentages already applied to basic).</summary>
    public decimal Amount { get; set; }
    public bool IsTaxable { get; set; }
    /// <summary>"Position" (inherited), "Employee" (override/addition), or "Default".</summary>
    public string Source { get; set; } = string.Empty;
}

/// <summary>An employee's effective emolument roll-up as of a date: basic + allowances − deductions.</summary>
public class EmployeeEmolumentSummaryDto
{
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string? PositionTitle { get; set; }
    public DateOnly AsOfDate { get; set; }
    public decimal MonthlyBasicPay { get; set; }
    public decimal TotalAllowances { get; set; }
    public decimal TotalDeductions { get; set; }
    /// <summary>Gross monthly = basic + allowances.</summary>
    public decimal GrossMonthly { get; set; }
    /// <summary>Net monthly = basic + allowances − deductions.</summary>
    public decimal NetMonthly { get; set; }
    public List<EffectivePayComponentDto> Components { get; set; } = new();
}
