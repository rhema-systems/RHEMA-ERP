using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.HR;

/// <summary>
/// Per-grade / staff-level value &amp; eligibility row for a <see cref="BenefitPolicy"/>.
/// Acts as an entitlement template: when a policy's <c>ValuationMethod</c> is GradeBased (or its
/// <c>CalculationBasis</c> is GradeBandTable), the employee's grade/level selects the applicable
/// <see cref="Amount"/>/<see cref="Rate"/> and <see cref="CoverageLimit"/> at enrollment time.
/// </summary>
public class BenefitGradeValue : TenantEntity
{
    public Guid BenefitPolicyId { get; set; }

    /// <summary>Salary grade this value applies to (optional — may key off staff level instead).</summary>
    public Guid? SalaryGradeId { get; set; }

    /// <summary>Staff level this value applies to (mirrors the MedicalBenefitTier convention).</summary>
    public Guid? StaffLevelId { get; set; }

    /// <summary>Fixed monetary value for this grade (when amount-based).</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? Amount { get; set; }

    /// <summary>Percentage rate for this grade (when percentage-based).</summary>
    [Column(TypeName = "decimal(9,4)")]
    public decimal? Rate { get; set; }

    /// <summary>Coverage limit for this grade, if it overrides the policy default.</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? CoverageLimit { get; set; }

    public bool IsActive { get; set; } = true;

    [ForeignKey(nameof(BenefitPolicyId))]
    public virtual BenefitPolicy BenefitPolicy { get; set; } = null!;

    [ForeignKey(nameof(SalaryGradeId))]
    public virtual SalaryGrade? SalaryGrade { get; set; }

    [ForeignKey(nameof(StaffLevelId))]
    public virtual StaffLevel? StaffLevel { get; set; }
}

/// <summary>
/// The materialized, per-employee, in-force record of a benefit. This is the single ledger the
/// payroll bridge reads. Entitlement templates (<see cref="EmployeePositionBenefit"/>,
/// <see cref="BenefitGradeValue"/>) are reconciled into these rows; a row may then be overridden or
/// opted-out at the employee level without affecting the shared template. Mirrors the snapshot
/// pattern used by <c>EmployeeMedicalInsurancePolicy</c>.
/// </summary>
public class EmployeeBenefitEnrollment : TenantEntity
{
    public Guid EmployeeId { get; set; }

    public Guid BenefitPolicyId { get; set; }

    public DateTime EnrollmentDate { get; set; }

    public DateTime EffectiveFrom { get; set; }

    public DateTime? EffectiveTo { get; set; }

    public EmployeeBenefitEnrollmentStatus Status { get; set; } = EmployeeBenefitEnrollmentStatus.Draft;

    /// <summary>Where this enrollment came from (Position/Grade/Manual/Mandatory).</summary>
    public BenefitEnrollmentSource Source { get; set; } = BenefitEnrollmentSource.Manual;

    /// <summary>Provenance link to the position entitlement that generated this enrollment, if any.</summary>
    public Guid? SourcePositionBenefitId { get; set; }

    /// <summary>
    /// Provenance link to the BENEFIT GROUP that gave the position this entitlement, where it came
    /// through a group rather than an individual row (round 2, lane C3). Exactly one of this and
    /// <see cref="SourcePositionBenefitId"/> is set on a position-sourced enrollment.
    /// </summary>
    /// <remarks>
    /// ⚠ Deliberately a bare id with NO foreign key and NO navigation. It is a record of where the
    /// enrollment came from, not a live reference: the group may later be retired or its membership
    /// changed, and neither should be blocked by, or silently rewrite, an enrollment already made.
    /// </remarks>
    public Guid? SourceBenefitGroupId { get; set; }

    /// <summary>Resolved monetary or Benefit-in-Kind value of the benefit for this employee.</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal AssessedValue { get; set; }

    /// <summary>Portion of the assessed value subject to income tax (computed from policy tax rules).</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal TaxableValue { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal EmployerContribution { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal EmployeeContribution { get; set; }

    [MaxLength(3)]
    public string Currency { get; set; } = "GHS";

    /// <summary>Amount of the coverage limit consumed (for limit-based benefits).</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal UtilizedAmount { get; set; }

    /// <summary>
    /// Start of the current usage period (anchors the periodic reset driven by the policy's
    /// <c>LimitPeriod</c>). Defaults to <see cref="EffectiveFrom"/>; advanced lazily when the period
    /// elapses so <see cref="UtilizedAmount"/> recomputes against the new window. Null = not yet set.
    /// </summary>
    public DateTime? CurrentPeriodStart { get; set; }

    /// <summary>True once a user edits a position/grade-derived value, so reconcile leaves it alone.</summary>
    public bool IsValueOverridden { get; set; }

    public Guid? ApprovedById { get; set; }

    public DateTime? ApprovedDate { get; set; }

    [MaxLength(500)]
    public string? TerminationReason { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    [ForeignKey(nameof(BenefitPolicyId))]
    public virtual BenefitPolicy BenefitPolicy { get; set; } = null!;

    [ForeignKey(nameof(SourcePositionBenefitId))]
    public virtual EmployeePositionBenefit? SourcePositionBenefit { get; set; }

    /// <summary>Dependents covered under this enrollment (reuses the existing dependent-benefit entity).</summary>
    public virtual List<EmployeeDependentBenefit> Dependents { get; set; } = new();

    /// <summary>Named beneficiaries for life/provident-style benefits.</summary>
    public virtual List<BenefitBeneficiary> Beneficiaries { get; set; } = new();

    /// <summary>Recorded expense/claim drawdowns against this enrollment's coverage limit.</summary>
    public virtual List<BenefitUtilization> Utilizations { get; set; } = new();
}

/// <summary>
/// A single drawdown (expense / reimbursement / adjustment) against an
/// <see cref="EmployeeBenefitEnrollment"/>'s coverage limit. Append-only ledger row with a claim
/// lifecycle; only <see cref="BenefitClaimStatus.Approved"/> and <see cref="BenefitClaimStatus.Paid"/>
/// rows count toward the enrollment's used amount for the current period. May be attributed to a
/// specific covered dependent via <see cref="EmployeeDependentId"/>.
/// </summary>
public class BenefitUtilization : TenantEntity
{
    public Guid EnrollmentId { get; set; }

    /// <summary>Optional dependent this drawdown is attributed to; null = the employee directly.</summary>
    public Guid? EmployeeDependentId { get; set; }

    /// <summary>Date the expense/claim was incurred (drives which usage period it falls in).</summary>
    public DateOnly ClaimDate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    public BenefitUtilizationType Type { get; set; } = BenefitUtilizationType.Expense;

    public BenefitClaimStatus Status { get; set; } = BenefitClaimStatus.Pending;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(100)]
    public string? ReferenceNumber { get; set; }

    public Guid? ApprovedById { get; set; }

    public DateTime? ApprovedDate { get; set; }

    [MaxLength(500)]
    public string? RejectionReason { get; set; }

    [ForeignKey(nameof(EnrollmentId))]
    public virtual EmployeeBenefitEnrollment Enrollment { get; set; } = null!;

    [ForeignKey(nameof(EmployeeDependentId))]
    public virtual EmployeeDependent? EmployeeDependent { get; set; }
}

/// <summary>
/// A named beneficiary of an <see cref="EmployeeBenefitEnrollment"/> (e.g. for life assurance or a
/// provident fund), with the share of proceeds allocated to them.
/// </summary>
public class BenefitBeneficiary : TenantEntity
{
    public Guid EnrollmentId { get; set; }

    [Required]
    [MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    public BenefitRelationType Relationship { get; set; } = BenefitRelationType.Any;

    /// <summary>Optional link to a registered dependent, when the beneficiary is one.</summary>
    public Guid? EmployeeDependentId { get; set; }

    [MaxLength(50)]
    public string? PhoneNumber { get; set; }

    /// <summary>Share of proceeds allocated to this beneficiary (0-100).</summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal Percentage { get; set; }

    public bool IsActive { get; set; } = true;

    [ForeignKey(nameof(EnrollmentId))]
    public virtual EmployeeBenefitEnrollment Enrollment { get; set; } = null!;

    [ForeignKey(nameof(EmployeeDependentId))]
    public virtual EmployeeDependent? EmployeeDependent { get; set; }
}
