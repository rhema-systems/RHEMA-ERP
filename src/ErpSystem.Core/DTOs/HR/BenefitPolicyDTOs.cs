using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.HR;

/// <summary>
/// DTO used to create a new benefit policy.
/// </summary>
public class CreateBenefitPolicyDto : CreateDtoBase, IValidatableObject
{
    /// <summary>
    /// Type/category of the benefit policy (e.g., Medical, Housing, etc.).
    /// </summary>
    [Required]
    public BenefitPolicyType PolicyType { get; set; } = BenefitPolicyType.Medical;

    /// <summary>
    /// Human-friendly name of the benefit policy.
    /// </summary>
    [Required]
    [MaxLength(150)]
    public string PolicyName { get; set; } = string.Empty;

    /// <summary>
    /// Optional code used to uniquely identify the policy within a tenant.
    /// </summary>
    [MaxLength(50)]
    public string? PolicyCode { get; set; }

    /// <summary>
    /// Optional description or notes about the policy.
    /// </summary>
    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    /// The primary recipient group for this policy.
    /// </summary>
    [Required]
    public BenefitRecipient Recipient { get; set; } = BenefitRecipient.Staff;

    /// <summary>
    /// Maximum number of dependents covered under this policy, if applicable.
    /// </summary>
    [Range(0, int.MaxValue)]
    public int? MaxDependents { get; set; }

    /// <summary>
    /// Amount contributed by the employee.
    /// </summary>
    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal? EmployeeContribution { get; set; }

    /// <summary>
    /// Amount contributed by the employer.
    /// </summary>
    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal? EmployerContribution { get; set; }

    /// <summary>
    /// Maximum coverage limit for this policy.
    /// </summary>
    [Required]
    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal CoverageLimit { get; set; }

    /// <summary>
    /// Period over which the coverage limit is applied.
    /// </summary>
    [Required]
    public BenefitLimitPeriod LimitPeriod { get; set; } = BenefitLimitPeriod.Annual;

    /// <summary>
    /// Policy start date/time.
    /// </summary>
    [Required]
    public DateTime EffectiveFrom { get; set; }

    /// <summary>
    /// Optional policy end date/time.
    /// </summary>
    public DateTime? EffectiveTo { get; set; }

    /// <summary>
    /// Indicates whether enrollment is mandatory for eligible employees.
    /// </summary>
    public bool IsMandatory { get; set; }

    /// <summary>
    /// Indicates whether this policy is currently active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Enterprise classification, valuation, tax/statutory and payroll-link fields.</summary>
    public BenefitDefinitionFields Definition { get; set; } = new();

    /// <summary>
    /// Allowed relations/dependent rules for this policy.
    /// </summary>
    public ICollection<CreateBenefitPolicyRelationDto> Relations { get; set; } = new List<CreateBenefitPolicyRelationDto>();

    /// <summary>Per-grade / staff-level value &amp; eligibility rows.</summary>
    public ICollection<CreateBenefitGradeValueDto> GradeValues { get; set; } = new List<CreateBenefitGradeValueDto>();

    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (EffectiveTo.HasValue && EffectiveTo.Value < EffectiveFrom)
        {
            yield return new ValidationResult(
                "EffectiveTo cannot be earlier than EffectiveFrom.",
                new[] { nameof(EffectiveFrom), nameof(EffectiveTo) });
        }

        if (!string.IsNullOrWhiteSpace(PolicyCode) && PolicyCode.Trim().Length > 50)
        {
            yield return new ValidationResult(
                "PolicyCode cannot exceed 50 characters.",
                new[] { nameof(PolicyCode) });
        }

        foreach (var result in Definition.Validate())
        {
            yield return result;
        }
    }
}

/// <summary>
/// Reusable bundle of the enterprise benefit-definition fields (classification, valuation, tax,
/// statutory treatment, contribution model, eligibility and payroll link). Shared by create/update
/// and surfaced on the read DTO.
/// </summary>
public class BenefitDefinitionFields
{
    /// <summary>How the benefit is delivered (cash, in-kind, reimbursement, service, voucher).</summary>
    public BenefitDeliveryType DeliveryType { get; set; } = BenefitDeliveryType.Cash;

    /// <summary>ISO currency code for the benefit's monetary values.</summary>
    [MaxLength(3)]
    public string Currency { get; set; } = "GHS";

    /// <summary>How often the benefit's value is paid/assessed.</summary>
    public PayFrequency Frequency { get; set; } = PayFrequency.Monthly;

    /// <summary>How contribution amounts are calculated.</summary>
    public BenefitCalculationBasis CalculationBasis { get; set; } = BenefitCalculationBasis.FixedAmount;

    /// <summary>Whether the assessed value is subject to income tax.</summary>
    public bool IsTaxable { get; set; } = true;

    /// <summary>Income-tax treatment of the assessed value.</summary>
    public BenefitTaxTreatment TaxTreatment { get; set; } = BenefitTaxTreatment.FullyTaxable;

    /// <summary>Taxable portion (0-100) when partially taxable.</summary>
    [Range(0, 100)]
    public decimal? TaxablePercentage { get; set; }

    /// <summary>How the benefit's monetary / Benefit-in-Kind value is determined.</summary>
    public BenefitValuationMethod ValuationMethod { get; set; } = BenefitValuationMethod.FlatRate;

    /// <summary>Configured monetary value used by flat/actual-cost/market-value valuation methods.</summary>
    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal? FlatValue { get; set; }

    /// <summary>Rate used by percentage/statutory valuation methods.</summary>
    [Range(0, 1000)]
    public decimal? ValuationRate { get; set; }

    /// <summary>Optional periodic cap on the valued amount.</summary>
    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal? ValuationCap { get; set; }

    /// <summary>Amount of the assessed value that is exempt from tax.</summary>
    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal? TaxExemptThreshold { get; set; }

    /// <summary>Whether the benefit's value counts toward pension/SSNIT contributions.</summary>
    public bool IsPensionable { get; set; }

    /// <summary>Whether the benefit increases gross pay.</summary>
    public bool AffectsGrossPay { get; set; } = true;

    /// <summary>Whether the benefit affects net pay.</summary>
    public bool AffectsNetPay { get; set; } = true;

    /// <summary>Who bears the cost of the benefit.</summary>
    public BenefitContributionResponsibility ContributionResponsibility { get; set; } = BenefitContributionResponsibility.EmployerPaysAll;

    /// <summary>Employer contribution percentage (when percentage-based).</summary>
    [Range(0, 100)]
    public decimal? EmployerContributionRate { get; set; }

    /// <summary>Employee contribution percentage (when percentage-based).</summary>
    [Range(0, 100)]
    public decimal? EmployeeContributionRate { get; set; }

    /// <summary>Minimum completed months of service before eligibility.</summary>
    [Range(0, int.MaxValue)]
    public int? MinServiceMonths { get; set; }

    /// <summary>Whether available during probation.</summary>
    public bool AvailableDuringProbation { get; set; } = true;

    /// <summary>Optional link to the PayComponent payroll resolves for this benefit.</summary>
    public Guid? PayComponentId { get; set; }

    /// <summary>Validates internal consistency of the definition fields.</summary>
    public IEnumerable<ValidationResult> Validate()
    {
        if (TaxTreatment == BenefitTaxTreatment.PartiallyTaxable && !TaxablePercentage.HasValue)
        {
            yield return new ValidationResult(
                "TaxablePercentage is required when TaxTreatment is PartiallyTaxable.",
                new[] { nameof(TaxablePercentage) });
        }

        if ((ValuationMethod is BenefitValuationMethod.StatutoryFormula
                or BenefitValuationMethod.PercentageOfBasicSalary
                or BenefitValuationMethod.PercentageOfCashEmoluments)
            && !ValuationRate.HasValue)
        {
            yield return new ValidationResult(
                "ValuationRate is required for percentage/statutory valuation methods.",
                new[] { nameof(ValuationRate) });
        }
    }
}

/// <summary>
/// DTO used to update an existing benefit policy.
/// </summary>
public class UpdateBenefitPolicyDto : UpdateDtoBase, IValidatableObject
{
    /// <summary>
    /// Type/category of the benefit policy.
    /// </summary>
    [Required]
    public BenefitPolicyType PolicyType { get; set; }

    /// <summary>
    /// Human-friendly name of the benefit policy.
    /// </summary>
    [Required]
    [MaxLength(150)]
    public string PolicyName { get; set; } = string.Empty;

    /// <summary>
    /// Optional code used to uniquely identify the policy within a tenant.
    /// </summary>
    [MaxLength(50)]
    public string? PolicyCode { get; set; }

    /// <summary>
    /// Optional description or notes about the policy.
    /// </summary>
    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    /// The primary recipient group for this policy.
    /// </summary>
    [Required]
    public BenefitRecipient Recipient { get; set; }

    /// <summary>
    /// Maximum number of dependents covered under this policy, if applicable.
    /// </summary>
    [Range(0, int.MaxValue)]
    public int? MaxDependents { get; set; }

    /// <summary>
    /// Amount contributed by the employee.
    /// </summary>
    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal? EmployeeContribution { get; set; }

    /// <summary>
    /// Amount contributed by the employer.
    /// </summary>
    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal? EmployerContribution { get; set; }

    /// <summary>
    /// Maximum coverage limit for this policy.
    /// </summary>
    [Required]
    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal CoverageLimit { get; set; }

    /// <summary>
    /// Period over which the coverage limit is applied.
    /// </summary>
    [Required]
    public BenefitLimitPeriod LimitPeriod { get; set; }

    /// <summary>
    /// Policy start date/time.
    /// </summary>
    [Required]
    public DateTime EffectiveFrom { get; set; }

    /// <summary>
    /// Optional policy end date/time.
    /// </summary>
    public DateTime? EffectiveTo { get; set; }

    /// <summary>
    /// Indicates whether enrollment is mandatory for eligible employees.
    /// </summary>
    public bool IsMandatory { get; set; }

    /// <summary>
    /// Indicates whether this policy is currently active.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>Enterprise classification, valuation, tax/statutory and payroll-link fields.</summary>
    public BenefitDefinitionFields Definition { get; set; } = new();

    /// <summary>
    /// Relations to create/update for this policy.
    /// </summary>
    public ICollection<UpdateBenefitPolicyRelationDto> Relations { get; set; } = new List<UpdateBenefitPolicyRelationDto>();

    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Id == Guid.Empty)
        {
            yield return new ValidationResult("Id is required.", new[] { nameof(Id) });
        }

        if (EffectiveTo.HasValue && EffectiveTo.Value < EffectiveFrom)
        {
            yield return new ValidationResult(
                "EffectiveTo cannot be earlier than EffectiveFrom.",
                new[] { nameof(EffectiveFrom), nameof(EffectiveTo) });
        }

        foreach (var result in Definition.Validate())
        {
            yield return result;
        }
    }
}

/// <summary>
/// DTO returned to API/UI consumers for a benefit policy.
/// </summary>
public class BenefitPolicyDto : BaseDto
{
    /// <summary>
    /// Type/category of the benefit policy.
    /// </summary>
    public BenefitPolicyType PolicyType { get; set; }

    /// <summary>
    /// Human-friendly name of the benefit policy.
    /// </summary>
    public string PolicyName { get; set; } = string.Empty;

    /// <summary>
    /// Optional code used to uniquely identify the policy within a tenant.
    /// </summary>
    public string? PolicyCode { get; set; }

    /// <summary>
    /// Optional description or notes about the policy.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// The primary recipient group for this policy.
    /// </summary>
    public BenefitRecipient Recipient { get; set; }

    /// <summary>
    /// Maximum number of dependents covered under this policy, if applicable.
    /// </summary>
    public int? MaxDependents { get; set; }

    /// <summary>
    /// Amount contributed by the employee.
    /// </summary>
    public decimal? EmployeeContribution { get; set; }

    /// <summary>
    /// Amount contributed by the employer.
    /// </summary>
    public decimal? EmployerContribution { get; set; }

    /// <summary>
    /// Maximum coverage limit for this policy.
    /// </summary>
    public decimal CoverageLimit { get; set; }

    /// <summary>
    /// Period over which the coverage limit is applied.
    /// </summary>
    public BenefitLimitPeriod LimitPeriod { get; set; }

    /// <summary>
    /// Policy start date/time.
    /// </summary>
    public DateTime EffectiveFrom { get; set; }

    /// <summary>
    /// Optional policy end date/time.
    /// </summary>
    public DateTime? EffectiveTo { get; set; }

    /// <summary>
    /// Indicates whether enrollment is mandatory for eligible employees.
    /// </summary>
    public bool IsMandatory { get; set; }

    /// <summary>
    /// Indicates whether this policy is currently active.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>Enterprise classification, valuation, tax/statutory and payroll-link fields.</summary>
    public BenefitDefinitionFields Definition { get; set; } = new();

    /// <summary>
    /// Allowed relations/dependent rules for this policy.
    /// </summary>
    public ICollection<BenefitPolicyRelationDto> Relations { get; set; } = new List<BenefitPolicyRelationDto>();

    /// <summary>Per-grade / staff-level value &amp; eligibility rows.</summary>
    public ICollection<BenefitGradeValueDto> GradeValues { get; set; } = new List<BenefitGradeValueDto>();
}

/// <summary>DTO for creating a per-grade benefit value row.</summary>
public class CreateBenefitGradeValueDto
{
    public Guid? SalaryGradeId { get; set; }

    public Guid? StaffLevelId { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal? Amount { get; set; }

    [Range(0, 1000)]
    public decimal? Rate { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal? CoverageLimit { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>DTO for updating a per-grade benefit value row (Id null = new).</summary>
public class UpdateBenefitGradeValueDto : CreateBenefitGradeValueDto
{
    public Guid? Id { get; set; }
}

/// <summary>Read DTO for a per-grade benefit value row.</summary>
public class BenefitGradeValueDto : BaseDto
{
    public Guid? SalaryGradeId { get; set; }

    public Guid? StaffLevelId { get; set; }

    public decimal? Amount { get; set; }

    public decimal? Rate { get; set; }

    public decimal? CoverageLimit { get; set; }

    public bool IsActive { get; set; }
}

/// <summary>
/// Lightweight DTO for benefit policy listing/search screens.
/// </summary>
public class BenefitPolicyListDto
{
    /// <summary>
    /// Benefit policy identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Human-friendly name of the benefit policy.
    /// </summary>
    public string PolicyName { get; set; } = string.Empty;

    /// <summary>
    /// Optional code used to uniquely identify the policy within a tenant.
    /// </summary>
    public string? PolicyCode { get; set; }

    /// <summary>
    /// Type/category of the benefit policy.
    /// </summary>
    public BenefitPolicyType PolicyType { get; set; }

    /// <summary>
    /// The primary recipient group for this policy.
    /// </summary>
    public BenefitRecipient Recipient { get; set; }

    /// <summary>
    /// Indicates whether this policy is currently active.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Policy start date/time.
    /// </summary>
    public DateTime EffectiveFrom { get; set; }

    /// <summary>
    /// Optional policy end date/time.
    /// </summary>
    public DateTime? EffectiveTo { get; set; }
}

/// <summary>
/// DTO used to define an allowed relationship/dependent rule when creating a benefit policy.
/// </summary>
public class CreateBenefitPolicyRelationDto : IValidatableObject
{
    /// <summary>
    /// Relationship type allowed under the policy (e.g., Spouse, Child).
    /// </summary>
    [Required]
    public BenefitRelationType RelationType { get; set; }

    /// <summary>
    /// Minimum eligible age (inclusive).
    /// </summary>
    [Range(0, int.MaxValue)]
    public int? MinAge { get; set; }

    /// <summary>
    /// Maximum eligible age (inclusive).
    /// </summary>
    [Range(0, int.MaxValue)]
    public int? MaxAge { get; set; }

    /// <summary>
    /// Indicates whether this relationship rule is active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MinAge.HasValue && MaxAge.HasValue && MinAge.Value > MaxAge.Value)
        {
            yield return new ValidationResult(
                "MinAge cannot be greater than MaxAge.",
                new[] { nameof(MinAge), nameof(MaxAge) });
        }
    }
}

/// <summary>
/// DTO used to create/update an allowed relationship/dependent rule for an existing benefit policy.
/// </summary>
public class UpdateBenefitPolicyRelationDto : IValidatableObject
{
    /// <summary>
    /// Benefit policy relation identifier.
    /// </summary>
    public Guid? Id { get; set; }

    /// <summary>
    /// Relationship type allowed under the policy (e.g., Spouse, Child).
    /// </summary>
    [Required]
    public BenefitRelationType RelationType { get; set; }

    /// <summary>
    /// Minimum eligible age (inclusive).
    /// </summary>
    [Range(0, int.MaxValue)]
    public int? MinAge { get; set; }

    /// <summary>
    /// Maximum eligible age (inclusive).
    /// </summary>
    [Range(0, int.MaxValue)]
    public int? MaxAge { get; set; }

    /// <summary>
    /// Indicates whether this relationship rule is active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Id.HasValue && Id.Value == Guid.Empty)
        {
            yield return new ValidationResult("Id, if supplied, cannot be empty.", new[] { nameof(Id) });
        }

        if (MinAge.HasValue && MaxAge.HasValue && MinAge.Value > MaxAge.Value)
        {
            yield return new ValidationResult(
                "MinAge cannot be greater than MaxAge.",
                new[] { nameof(MinAge), nameof(MaxAge) });
        }
    }
}

/// <summary>
/// DTO returned to API/UI consumers for a benefit policy relationship rule.
/// </summary>
public class BenefitPolicyRelationDto : BaseDto
{
    /// <summary>
    /// Relationship type allowed under the policy (e.g., Spouse, Child).
    /// </summary>
    public BenefitRelationType RelationType { get; set; }

    /// <summary>
    /// Minimum eligible age (inclusive).
    /// </summary>
    public int? MinAge { get; set; }

    /// <summary>
    /// Maximum eligible age (inclusive).
    /// </summary>
    public int? MaxAge { get; set; }

    /// <summary>
    /// Indicates whether this relationship rule is active.
    /// </summary>
    public bool IsActive { get; set; }
}

/// <summary>
/// Common lookup option DTO for presenting enum choices in UI dropdowns.
/// </summary>
public class EnumOptionDto
{
    /// <summary>
    /// Numeric value of the enum.
    /// </summary>
    public int Value { get; set; }

    /// <summary>
    /// Enum member name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// User-friendly label for display.
    /// </summary>
    public string Label { get; set; } = string.Empty;
}

/// <summary>
/// Lookup DTO that bundles benefit policy related enum options for UI consumption.
/// </summary>
public class BenefitPolicyLookupsDto
{
    /// <summary>
    /// Available benefit policy types.
    /// </summary>
    public IReadOnlyList<EnumOptionDto> PolicyTypes { get; set; } = Array.Empty<EnumOptionDto>();

    /// <summary>
    /// Available benefit recipients.
    /// </summary>
    public IReadOnlyList<EnumOptionDto> Recipients { get; set; } = Array.Empty<EnumOptionDto>();

    /// <summary>
    /// Available benefit relation types.
    /// </summary>
    public IReadOnlyList<EnumOptionDto> RelationTypes { get; set; } = Array.Empty<EnumOptionDto>();

    /// <summary>
    /// Available limit periods.
    /// </summary>
    public IReadOnlyList<EnumOptionDto> LimitPeriods { get; set; } = Array.Empty<EnumOptionDto>();

    /// <summary>Available delivery types (cash, in-kind, etc.).</summary>
    public IReadOnlyList<EnumOptionDto> DeliveryTypes { get; set; } = Array.Empty<EnumOptionDto>();

    /// <summary>Available tax treatments.</summary>
    public IReadOnlyList<EnumOptionDto> TaxTreatments { get; set; } = Array.Empty<EnumOptionDto>();

    /// <summary>Available valuation methods.</summary>
    public IReadOnlyList<EnumOptionDto> ValuationMethods { get; set; } = Array.Empty<EnumOptionDto>();

    /// <summary>Available calculation bases.</summary>
    public IReadOnlyList<EnumOptionDto> CalculationBases { get; set; } = Array.Empty<EnumOptionDto>();

    /// <summary>Available contribution responsibilities.</summary>
    public IReadOnlyList<EnumOptionDto> ContributionResponsibilities { get; set; } = Array.Empty<EnumOptionDto>();

    /// <summary>Available payment frequencies.</summary>
    public IReadOnlyList<EnumOptionDto> Frequencies { get; set; } = Array.Empty<EnumOptionDto>();

    /// <summary>Available enrollment statuses.</summary>
    public IReadOnlyList<EnumOptionDto> EnrollmentStatuses { get; set; } = Array.Empty<EnumOptionDto>();

    /// <summary>Active pay components a policy can link to for the payroll bridge.</summary>
    public IReadOnlyList<PayComponentLookupDto> PayComponents { get; set; } = Array.Empty<PayComponentLookupDto>();
}

/// <summary>Lightweight lookup option for a payroll <c>PayComponent</c> a benefit can link to.</summary>
public class PayComponentLookupDto
{
    /// <summary>Pay component identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Unique pay component code.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Human-friendly pay component name.</summary>
    public string Name { get; set; } = string.Empty;
}
