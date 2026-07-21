using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.HR;

/// <summary>
/// Master definition of a pay component — an earning (Allowance) added on top of basic pay, or a
/// Deduction subtracted from gross. Reusable system-wide (payroll, leave encashment, contracts).
/// Assigned at the position level (<see cref="PositionPayComponent"/>) as a default and/or at the
/// employee level (<see cref="EmployeePayComponent"/>) as an override or one-off addition.
/// Mirrors the existing <c>BenefitPolicy</c> → <c>EmployeePositionBenefit</c> convention.
/// </summary>
public class PayComponent : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public PayComponentType ComponentType { get; set; } = PayComponentType.Allowance;

    public PayComponentCalculationBasis CalculationBasis { get; set; } = PayComponentCalculationBasis.FixedAmount;

    /// <summary>
    /// Default value used when neither a position nor an employee assignment overrides it.
    /// Interpreted as a fixed monetary amount or a percentage of basic pay per
    /// <see cref="CalculationBasis"/>.
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? DefaultAmount { get; set; }

    /// <summary>Whether this earning is taxable (informational for payroll roll-up).</summary>
    public bool IsTaxable { get; set; } = true;

    /// <summary>Whether this component's value counts toward pension/SSNIT contributions.</summary>
    public bool IsPensionable { get; set; }

    /// <summary>Whether this component contributes to gross pay (false for notional BIK lines).</summary>
    public bool AffectsGrossPay { get; set; } = true;

    /// <summary>Income-tax treatment applied to this component by payroll.</summary>
    public TaxTreatmentType StatutoryTreatment { get; set; } = TaxTreatmentType.PAYE;

    public DateTime EffectiveFrom { get; set; }

    public DateTime? EffectiveTo { get; set; }

    public bool IsActive { get; set; } = true;

    public virtual List<PositionPayComponent> PositionAssignments { get; set; } = new();
    public virtual List<EmployeePayComponent> EmployeeAssignments { get; set; } = new();
}

/// <summary>
/// Position-level default assignment of a pay component: everyone holding the position inherits
/// the component with the (optional) <see cref="Amount"/> override; otherwise the component's
/// <see cref="PayComponent.DefaultAmount"/> applies.
/// </summary>
public class PositionPayComponent : TenantEntity
{
    public Guid PositionId { get; set; }

    public Guid PayComponentId { get; set; }

    /// <summary>Position-level amount override; falls back to the component default when null.</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? Amount { get; set; }

    public bool IsActive { get; set; } = true;

    [ForeignKey(nameof(PositionId))]
    public virtual EmployeePosition Position { get; set; } = null!;

    [ForeignKey(nameof(PayComponentId))]
    public virtual PayComponent PayComponent { get; set; } = null!;
}

/// <summary>
/// Employee-level assignment of a pay component: a per-employee amount override of a position
/// default, or a one-off component not tied to the position. Effective-dated so changes over
/// time are preserved.
/// </summary>
public class EmployeePayComponent : TenantEntity
{
    public Guid EmployeeId { get; set; }

    public Guid PayComponentId { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    public DateTime EffectiveFrom { get; set; }

    public DateTime? EffectiveTo { get; set; }

    public bool IsActive { get; set; } = true;

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    [ForeignKey(nameof(PayComponentId))]
    public virtual PayComponent PayComponent { get; set; } = null!;
}
