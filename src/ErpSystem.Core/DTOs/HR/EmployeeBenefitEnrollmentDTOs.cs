using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.HR;

/// <summary>
/// DTO used to directly enroll an employee in a benefit policy. The assessed/taxable values and
/// contribution split are resolved by the service from the policy's valuation &amp; tax rules; any
/// values supplied here are treated as an explicit override.
/// </summary>
public class CreateEmployeeBenefitEnrollmentDto : IValidatableObject
{
    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    public Guid BenefitPolicyId { get; set; }

    [Required]
    public DateTime EffectiveFrom { get; set; }

    public DateTime? EffectiveTo { get; set; }

    /// <summary>Optional explicit override of the resolved assessed value.</summary>
    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal? AssessedValueOverride { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    /// <summary>Dependents to cover under this enrollment.</summary>
    public ICollection<CreateEnrollmentDependentDto> Dependents { get; set; } = new List<CreateEnrollmentDependentDto>();

    /// <summary>Named beneficiaries for life/provident-style benefits.</summary>
    public ICollection<CreateBenefitBeneficiaryDto> Beneficiaries { get; set; } = new List<CreateBenefitBeneficiaryDto>();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (EffectiveTo.HasValue && EffectiveTo.Value < EffectiveFrom)
        {
            yield return new ValidationResult(
                "EffectiveTo cannot be earlier than EffectiveFrom.",
                new[] { nameof(EffectiveFrom), nameof(EffectiveTo) });
        }

        var totalShare = Beneficiaries?.Sum(b => b.Percentage) ?? 0m;
        if (Beneficiaries is { Count: > 0 } && totalShare != 100m)
        {
            yield return new ValidationResult(
                "Beneficiary percentages must total 100.",
                new[] { nameof(Beneficiaries) });
        }
    }
}

/// <summary>DTO used to update an employee benefit enrollment's editable fields.</summary>
public class UpdateEmployeeBenefitEnrollmentDto : IValidatableObject
{
    public DateTime EffectiveFrom { get; set; }

    public DateTime? EffectiveTo { get; set; }

    /// <summary>Set to override the resolved assessed value (flags the enrollment as overridden).</summary>
    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal? AssessedValueOverride { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (EffectiveTo.HasValue && EffectiveTo.Value < EffectiveFrom)
        {
            yield return new ValidationResult(
                "EffectiveTo cannot be earlier than EffectiveFrom.",
                new[] { nameof(EffectiveFrom), nameof(EffectiveTo) });
        }
    }
}

/// <summary>DTO used to transition an enrollment's status (approve, suspend, terminate, etc.).</summary>
public class EnrollmentStatusChangeDto
{
    [Required]
    public EmployeeBenefitEnrollmentStatus Status { get; set; }

    [MaxLength(500)]
    public string? Reason { get; set; }
}

/// <summary>DTO to add a dependent to an enrollment.</summary>
public class CreateEnrollmentDependentDto : IValidatableObject
{
    [Required]
    public Guid EmployeeDependentId { get; set; }

    public DateOnly? CoverageStartDate { get; set; }

    public DateOnly? CoverageEndDate { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (CoverageStartDate.HasValue && CoverageEndDate.HasValue && CoverageEndDate.Value < CoverageStartDate.Value)
        {
            yield return new ValidationResult(
                "CoverageEndDate cannot be earlier than CoverageStartDate.",
                new[] { nameof(CoverageStartDate), nameof(CoverageEndDate) });
        }
    }
}

/// <summary>
/// DTO to amend a dependent's coverage on an enrollment. The dependent themselves cannot be
/// swapped — remove the row and add the other dependent instead, so the claim ledger stays
/// attributable to whoever the claims were actually made for.
/// </summary>
public class UpdateEnrollmentDependentDto : IValidatableObject
{
    public DateOnly? CoverageStartDate { get; set; }

    public DateOnly? CoverageEndDate { get; set; }

    /// <summary>Clearing this ends cover: claims can no longer be recorded against the dependent.</summary>
    public bool IsActive { get; set; } = true;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (CoverageStartDate.HasValue && CoverageEndDate.HasValue && CoverageEndDate.Value < CoverageStartDate.Value)
        {
            yield return new ValidationResult(
                "CoverageEndDate cannot be earlier than CoverageStartDate.",
                new[] { nameof(CoverageStartDate), nameof(CoverageEndDate) });
        }
    }
}

/// <summary>
/// Replaces an enrollment's whole beneficiary set in one call.
/// <para>
/// Beneficiaries are edited as a set rather than row by row because the shares must total 100.
/// A per-row API cannot honour that: every path from one valid split to another passes through an
/// intermediate state that does not add up, so the server would have to either reject the first
/// step of any legitimate edit or stop enforcing the invariant altogether. Sending the whole set
/// lets it be checked exactly once, against the state the caller actually intends.
/// </para>
/// </summary>
public class ReplaceBenefitBeneficiariesDto : IValidatableObject
{
    public ICollection<CreateBenefitBeneficiaryDto> Beneficiaries { get; set; } = new List<CreateBenefitBeneficiaryDto>();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // An empty set is allowed — it clears the nomination, which is a legitimate thing to want.
        if (Beneficiaries is not { Count: > 0 })
        {
            yield break;
        }

        if (Beneficiaries.Sum(b => b.Percentage) != 100m)
        {
            yield return new ValidationResult(
                "Beneficiary percentages must total 100.",
                new[] { nameof(Beneficiaries) });
        }

        var duplicateDependent = Beneficiaries
            .Where(b => b.EmployeeDependentId.HasValue)
            .GroupBy(b => b.EmployeeDependentId!.Value)
            .Any(g => g.Count() > 1);

        if (duplicateDependent)
        {
            yield return new ValidationResult(
                "The same dependent cannot be named as a beneficiary more than once.",
                new[] { nameof(Beneficiaries) });
        }
    }
}

/// <summary>DTO to add a beneficiary to an enrollment.</summary>
public class CreateBenefitBeneficiaryDto
{
    [Required]
    [MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    public BenefitRelationType Relationship { get; set; } = BenefitRelationType.Any;

    public Guid? EmployeeDependentId { get; set; }

    [MaxLength(50)]
    public string? PhoneNumber { get; set; }

    [Range(0, 100)]
    public decimal Percentage { get; set; }
}

/// <summary>Lightweight enrollment DTO for listing screens.</summary>
public class EmployeeBenefitEnrollmentListDto
{
    public Guid Id { get; set; }

    public Guid EmployeeId { get; set; }

    public string EmployeeName { get; set; } = string.Empty;

    public Guid BenefitPolicyId { get; set; }

    public string BenefitPolicyName { get; set; } = string.Empty;

    public EmployeeBenefitEnrollmentStatus Status { get; set; }

    public BenefitEnrollmentSource Source { get; set; }

    public decimal AssessedValue { get; set; }

    public decimal TaxableValue { get; set; }

    public string Currency { get; set; } = "GHS";

    public DateTime EffectiveFrom { get; set; }

    public DateTime? EffectiveTo { get; set; }

    /// <summary>Resolved coverage ceiling for the current period.</summary>
    public decimal CoverageLimit { get; set; }

    /// <summary>Amount consumed within the current usage period.</summary>
    public decimal UtilizedAmount { get; set; }

    /// <summary>CoverageLimit − UtilizedAmount (never negative).</summary>
    public decimal RemainingAmount { get; set; }

    public DateTime? CurrentPeriodStart { get; set; }

    public DateTime? CurrentPeriodEnd { get; set; }
}

/// <summary>Full enrollment DTO with dependents and beneficiaries.</summary>
public class EmployeeBenefitEnrollmentDto : BaseDto
{
    public Guid EmployeeId { get; set; }

    public string EmployeeName { get; set; } = string.Empty;

    public Guid BenefitPolicyId { get; set; }

    public string BenefitPolicyName { get; set; } = string.Empty;

    public DateTime EnrollmentDate { get; set; }

    public DateTime EffectiveFrom { get; set; }

    public DateTime? EffectiveTo { get; set; }

    public EmployeeBenefitEnrollmentStatus Status { get; set; }

    public BenefitEnrollmentSource Source { get; set; }

    public Guid? SourcePositionBenefitId { get; set; }

    public decimal AssessedValue { get; set; }

    public decimal TaxableValue { get; set; }

    public decimal EmployerContribution { get; set; }

    public decimal EmployeeContribution { get; set; }

    public string Currency { get; set; } = "GHS";

    public decimal UtilizedAmount { get; set; }

    /// <summary>Resolved coverage ceiling for the current period.</summary>
    public decimal CoverageLimit { get; set; }

    /// <summary>CoverageLimit − UtilizedAmount (never negative).</summary>
    public decimal RemainingAmount { get; set; }

    public DateTime? CurrentPeriodStart { get; set; }

    public DateTime? CurrentPeriodEnd { get; set; }

    public bool IsValueOverridden { get; set; }

    public Guid? ApprovedById { get; set; }

    public DateTime? ApprovedDate { get; set; }

    public string? TerminationReason { get; set; }

    public string? Notes { get; set; }

    public ICollection<EnrollmentDependentDto> Dependents { get; set; } = new List<EnrollmentDependentDto>();

    public ICollection<BenefitBeneficiaryDto> Beneficiaries { get; set; } = new List<BenefitBeneficiaryDto>();
}

/// <summary>Read DTO for a dependent covered under an enrollment.</summary>
public class EnrollmentDependentDto : BaseDto
{
    public Guid EmployeeDependentId { get; set; }

    /// <summary>Who the dependent is. Without it a coverage list is a column of bare identifiers.</summary>
    public string DependentName { get; set; } = string.Empty;

    /// <summary>The dependent's relationship to the employee, as registered on their profile.</summary>
    public DependentRelationship Relationship { get; set; }

    public Guid PolicyId { get; set; }

    public DateOnly EnrolledDate { get; set; }

    public DateOnly? CoverageStartDate { get; set; }

    public DateOnly? CoverageEndDate { get; set; }

    public bool IsActive { get; set; }

    public decimal BenefitAmountUsed { get; set; }
}

/// <summary>Read DTO for an enrollment beneficiary.</summary>
public class BenefitBeneficiaryDto : BaseDto
{
    public string FullName { get; set; } = string.Empty;

    public BenefitRelationType Relationship { get; set; }

    public Guid? EmployeeDependentId { get; set; }

    public string? PhoneNumber { get; set; }

    public decimal Percentage { get; set; }

    public bool IsActive { get; set; }
}

/// <summary>
/// Flattened, payroll-ready line produced by the Benefit→Payroll bridge for one in-force benefit.
/// This is the single contract the (separate) payroll module consumes — it never needs to merge
/// position/grade entitlements itself.
/// </summary>
public class EmployeeBenefitPayrollLineDto
{
    public Guid EnrollmentId { get; set; }

    public Guid EmployeeId { get; set; }

    public Guid BenefitPolicyId { get; set; }

    public string BenefitName { get; set; } = string.Empty;

    public string? PayComponentCode { get; set; }

    public BenefitDeliveryType DeliveryType { get; set; }

    /// <summary>Full assessed (gross) value of the benefit for the period.</summary>
    public decimal GrossValue { get; set; }

    /// <summary>Portion of the value subject to income tax.</summary>
    public decimal TaxableValue { get; set; }

    public decimal EmployerContribution { get; set; }

    public decimal EmployeeContribution { get; set; }

    public bool IsPensionable { get; set; }

    public bool AffectsGrossPay { get; set; }

    public bool AffectsNetPay { get; set; }

    public PayFrequency Frequency { get; set; }

    public string Currency { get; set; } = "GHS";

    public DateTime EffectiveFrom { get; set; }

    public DateTime? EffectiveTo { get; set; }
}
