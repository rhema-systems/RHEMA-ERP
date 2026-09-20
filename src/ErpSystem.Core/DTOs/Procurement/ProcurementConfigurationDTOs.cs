using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using ErpSystem.Core.Configuration;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Procurement;

public sealed class ProcurementConfigurationProfileListRequest
{
    public string? Search { get; set; }
    public ProcurementConfigurationProfileStatus? Status { get; set; }
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 20;
}

public sealed class ProcurementConfigurationPagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
}

public class ProcurementConfigurationProfileSummaryDto
{
    public Guid Id { get; init; }
    public Guid ProfileKey { get; init; }
    public string ProfileCode { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int Version { get; init; }
    public ProcurementConfigurationProfileStatus LifecycleStatus { get; init; }
    public DateTime EffectiveFrom { get; init; }
    public DateTime? EffectiveTo { get; init; }
    public bool IsDefault { get; init; }
    public int CompleteDecisionCount { get; init; }
    public int TotalDecisionCount { get; init; }
    public bool IsComplete => TotalDecisionCount > 0 && CompleteDecisionCount == TotalDecisionCount;
    public string? UpdatedBy { get; init; }
    public DateTime UpdatedAt { get; init; }
    public string RowVersion { get; init; } = string.Empty;
}

public sealed class ProcurementConfigurationProfileDto : ProcurementConfigurationProfileSummaryDto
{
    public string? ChangeSummary { get; init; }
    public Guid? SupersedesProfileId { get; init; }
    public DateTime? PublishedAt { get; init; }
    public Guid? PublishedById { get; init; }
    public DateTime? RetiredAt { get; init; }
    public Guid? RetiredById { get; init; }
    public IReadOnlyList<ProcurementConfigurationDecisionDto> Decisions { get; init; } = Array.Empty<ProcurementConfigurationDecisionDto>();
    public ProcurementConfigurationValidationResultDto Validation { get; init; } = new();
    public IReadOnlyList<ProcurementConfigurationRevisionDto> RecentHistory { get; init; } = Array.Empty<ProcurementConfigurationRevisionDto>();
}

public sealed class ProcurementConfigurationDecisionDto
{
    public Guid Id { get; init; }
    public string DecisionKey { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string OwnerGroup { get; init; } = string.Empty;
    public int SchemaVersion { get; init; }
    public ProcurementConfigurationDecisionStatus Status { get; init; }
    public ProcurementConfigurationApprovalStatus ApprovalStatus { get; init; }
    public ProcurementConfigurationEvidenceStatus EvidenceStatus { get; init; }
    public JsonElement Value { get; init; }
    public DateTime? DecisionDate { get; init; }
    public DateTime? EffectiveFrom { get; init; }
    public DateTime? EffectiveTo { get; init; }
    public Guid? ApprovedById { get; init; }
    public DateTime? ApprovedAt { get; init; }
    public Guid? ApprovalWorkflowInstanceId { get; init; }
    public string? ApprovalReference { get; init; }
    public string? SourceLineage { get; init; }
    public string? Notes { get; init; }
    public bool RequiresApproval { get; init; }
    public bool RequiresEvidence { get; init; }
    public bool IsComplete { get; init; }
    public string RowVersion { get; init; } = string.Empty;
    public IReadOnlyList<ProcurementConfigurationEvidenceLinkDto> Evidence { get; init; } = Array.Empty<ProcurementConfigurationEvidenceLinkDto>();
}

public sealed class ProcurementConfigurationEvidenceLinkDto
{
    public Guid Id { get; init; }
    public Guid DecisionId { get; init; }
    public string EvidenceType { get; init; } = string.Empty;
    public Guid? FileUploadRecordId { get; init; }
    public string? FilePath { get; init; }
    public string? OriginalFileName { get; init; }
    public string? ContentType { get; init; }
    public long? FileSize { get; init; }
    public FileVirusScanStatus? VirusScanStatus { get; init; }
    public string? ExternalReference { get; init; }
    public string? Checksum { get; init; }
    public DateTime UploadedAt { get; init; }
    public Guid UploadedById { get; init; }
}

public sealed class ProcurementConfigurationRevisionDto
{
    public Guid Id { get; init; }
    public Guid ProfileId { get; init; }
    public Guid? DecisionId { get; init; }
    public string Action { get; init; } = string.Empty;
    public string Result { get; init; } = string.Empty;
    public string CorrelationId { get; init; } = string.Empty;
    public Guid ActorUserId { get; init; }
    public string ActorName { get; init; } = string.Empty;
    public string? ActorRoles { get; init; }
    public string? Reason { get; init; }
    public JsonElement? Before { get; init; }
    public JsonElement? After { get; init; }
    public DateTime Timestamp { get; init; }
}

public sealed class ProcurementDecisionSchemaDto
{
    public string DecisionKey { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string OwnerGroup { get; init; } = string.Empty;
    public int SchemaVersion { get; init; }
    public string ValueType { get; init; } = string.Empty;
    public bool RequiresApproval { get; init; }
    public bool RequiresEvidence { get; init; }
    public bool RequiresRenewedApproval { get; init; }
}

public sealed class ProcurementConfigurationValidationIssueDto
{
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string? DecisionKey { get; init; }
    public string Severity { get; init; } = "Error";
}

public sealed class ProcurementConfigurationValidationResultDto
{
    public bool IsValid => Errors.Count == 0;
    public IReadOnlyList<ProcurementConfigurationValidationIssueDto> Errors { get; init; } = Array.Empty<ProcurementConfigurationValidationIssueDto>();
    public IReadOnlyList<ProcurementConfigurationValidationIssueDto> Warnings { get; init; } = Array.Empty<ProcurementConfigurationValidationIssueDto>();
}

public sealed class CreateProcurementConfigurationProfileRequest
{
    [Required, StringLength(50)] public string ProfileCode { get; set; } = "TDC-PROCUREMENT";
    [Required, StringLength(200)] public string Name { get; set; } = string.Empty;
    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow.Date;
    public DateTime? EffectiveTo { get; set; }
    [StringLength(1000)] public string? ChangeSummary { get; set; }
    public bool IsDefault { get; set; }
}

public sealed class UpdateProcurementConfigurationProfileRequest
{
    [Required, StringLength(200)] public string Name { get; set; } = string.Empty;
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    [StringLength(1000)] public string? ChangeSummary { get; set; }
    public bool IsDefault { get; set; }
    [Required] public string RowVersion { get; set; } = string.Empty;
    [StringLength(1000)] public string? Reason { get; set; }
}

public sealed class SaveProcurementConfigurationDecisionRequest
{
    public int SchemaVersion { get; set; } = 1;
    [Required, StringLength(200)] public string OwnerGroup { get; set; } = string.Empty;
    public ProcurementConfigurationDecisionStatus Status { get; set; } = ProcurementConfigurationDecisionStatus.Proposed;
    public ProcurementConfigurationApprovalStatus ApprovalStatus { get; set; } = ProcurementConfigurationApprovalStatus.Pending;
    public JsonElement Value { get; set; }
    public DateTime? DecisionDate { get; set; }
    public Guid? ApprovalWorkflowInstanceId { get; set; }
    [StringLength(500)] public string? ApprovalReference { get; set; }
    [StringLength(1000)] public string? SourceLineage { get; set; }
    [StringLength(2000)] public string? Notes { get; set; }
    [Required] public string RowVersion { get; set; } = string.Empty;
    [StringLength(1000)] public string? Reason { get; set; }
}

public sealed class ProcurementConfigurationLifecycleRequest
{
    [Required] public string RowVersion { get; set; } = string.Empty;
    [StringLength(1000)] public string? Reason { get; set; }
}

public sealed class WithdrawProcurementConfigurationDecisionRequest
{
    [Required] public string RowVersion { get; set; } = string.Empty;
    [Required, StringLength(1000)] public string Reason { get; set; } = string.Empty;
}

public sealed class CloneProcurementConfigurationProfileRequest
{
    [StringLength(1000)] public string? ChangeSummary { get; set; }
}

public sealed class LinkProcurementConfigurationEvidenceRequest
{
    [Required, StringLength(100)] public string EvidenceType { get; set; } = string.Empty;
    [StringLength(512)] public string? FilePath { get; set; }
    [StringLength(1000)] public string? ExternalReference { get; set; }
    [StringLength(128)] public string? Checksum { get; set; }
    [StringLength(2000)] public string? ReferenceMetadataJson { get; set; }
    [Required] public string DecisionRowVersion { get; set; } = string.Empty;
    [StringLength(1000)] public string? Reason { get; set; }
}

public abstract class EffectiveDatedDecisionValueDto : IValidatableObject
{
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }

    public virtual IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (EffectiveFrom == default)
            yield return new ValidationResult("Effective from is required.", new[] { nameof(EffectiveFrom) });
        if (EffectiveTo.HasValue && EffectiveTo.Value < EffectiveFrom)
            yield return new ValidationResult("Effective to cannot be before effective from.", new[] { nameof(EffectiveTo) });
    }
}

public sealed class ProcurementMethodThresholdDecisionValueDto : EffectiveDatedDecisionValueDto
{
    public ProcurementCategoryClass Category { get; set; }
    [Required, StringLength(150)] public string ServiceClass { get; set; } = string.Empty;
    public ProcurementMethodType Method { get; set; }
    [Required, StringLength(3), RegularExpression("^[A-Z]{3}$")] public string CurrencyCode { get; set; } = "GHS";
    [Range(typeof(decimal), "0", "9999999999999999")] public decimal LowerBound { get; set; }
    public decimal? UpperBound { get; set; }
    public bool LowerInclusive { get; set; } = true;
    public bool UpperInclusive { get; set; } = true;
    [Required, StringLength(500)] public string StatutoryReference { get; set; } = string.Empty;

    public override IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        foreach (var result in base.Validate(validationContext)) yield return result;
        if (UpperBound.HasValue && UpperBound.Value < LowerBound)
            yield return new ValidationResult("Upper bound cannot be below lower bound.", new[] { nameof(UpperBound) });
    }
}

public sealed class ProcurementAuthorityDecisionValueDto : EffectiveDatedDecisionValueDto
{
    [Required, StringLength(200)] public string AuthorityLevel { get; set; } = string.Empty;
    [Required, StringLength(3), RegularExpression("^[A-Z]{3}$")] public string CurrencyCode { get; set; } = "GHS";
    [Range(typeof(decimal), "0", "9999999999999999")] public decimal LowerBound { get; set; }
    public decimal? UpperBound { get; set; }
    public bool LowerInclusive { get; set; } = true;
    public bool UpperInclusive { get; set; } = true;
    [Required, StringLength(200)] public string EscalationAuthority { get; set; } = string.Empty;
    [MinLength(1)] public List<ProcurementCategoryClass> ApplicableCategories { get; set; } = new();

    public override IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        foreach (var result in base.Validate(validationContext)) yield return result;
        if (UpperBound.HasValue && UpperBound.Value < LowerBound)
            yield return new ValidationResult("Upper bound cannot be below lower bound.", new[] { nameof(UpperBound) });
    }
}

public sealed class ProcurementWorkflowSelectionDecisionValueDto : EffectiveDatedDecisionValueDto
{
    [Required, StringLength(150)] public string TransactionEntityType { get; set; } = string.Empty;
    [Required, StringLength(150)] public string PolicySelector { get; set; } = string.Empty;
    public Guid WorkflowDefinitionId { get; set; }
    [Required, StringLength(2000)] public string ApplicabilityConditions { get; set; } = string.Empty;

    public override IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        foreach (var result in base.Validate(validationContext)) yield return result;
        if (WorkflowDefinitionId == Guid.Empty)
            yield return new ValidationResult("Workflow definition is required.", new[] { nameof(WorkflowDefinitionId) });
    }
}

public sealed class ProcurementAuthorityStageDecisionValueDto : EffectiveDatedDecisionValueDto
{
    [Required, StringLength(200)] public string AuthorityOrCommittee { get; set; } = string.Empty;
    [Required, StringLength(100)] public string RoleType { get; set; } = string.Empty;
    [Range(1, 100)] public int Quorum { get; set; } = 1;
    [MinLength(1)] public List<string> EvidenceRequirements { get; set; } = new();
    public decimal? MinimumAmount { get; set; }
    public decimal? MaximumAmount { get; set; }
    [MinLength(1)] public List<ProcurementCategoryClass> ApplicableCategories { get; set; } = new();
    [Range(1, 100)] public int Sequence { get; set; } = 1;
    [Required, StringLength(100)] public string StageGroup { get; set; } = string.Empty;
    [Required, StringLength(200)] public string EscalationAuthority { get; set; } = string.Empty;
}

public sealed class ProcurementPettyPurchaseDecisionValueDto : EffectiveDatedDecisionValueDto
{
    [Range(typeof(decimal), "0.01", "9999999999999999")] public decimal PettyThreshold { get; set; }
    [Required, StringLength(3), RegularExpression("^[A-Z]{3}$")] public string CurrencyCode { get; set; } = "GHS";
    public bool WaiverEligible { get; set; }
    public bool JustificationRequired { get; set; } = true;
    [MinLength(1)] public List<string> EvidenceRequirements { get; set; } = new();
    [Required, StringLength(200)] public string ApproverRole { get; set; } = string.Empty;
    public DateTime? ExpiryDate { get; set; }
}

public sealed class ProcurementExceptionPrerequisiteDecisionValueDto : EffectiveDatedDecisionValueDto
{
    public ProcurementMethodType Method { get; set; }
    [MinLength(1)] public List<string> Prerequisites { get; set; } = new();
    [Required, StringLength(200)] public string ApprovalAuthority { get; set; } = string.Empty;
    [MinLength(1)] public List<string> MandatoryEvidenceChecklist { get; set; } = new();
    [Required, StringLength(500)] public string FilingReference { get; set; } = string.Empty;
    public DateTime? ExpiryDate { get; set; }
}

public sealed class ProcurementSupplierFeeDecisionValueDto : EffectiveDatedDecisionValueDto
{
    public ProcurementSupplierOnboardingFeeMode Mode { get; set; } =
        ProcurementSupplierOnboardingFeeMode.Paid;
    [Required, StringLength(150)] public string FeeType { get; set; } = string.Empty;
    [Range(typeof(decimal), "0", "9999999999999999")] public decimal Amount { get; set; }
    [Required, StringLength(3), RegularExpression("^[A-Z]{3}$")] public string CurrencyCode { get; set; } = "GHS";
    [Range(typeof(decimal), "0", "100")] public decimal TaxPercent { get; set; }
    public List<string> PaymentChannels { get; set; } = new();
    public Guid? RevenueAccountId { get; set; }
    public Guid? TaxAccountId { get; set; }
    public Guid? ExemptionWorkflowDefinitionId { get; set; }
    [Required, StringLength(50)] public string ReceiptNumberFormat { get; set; } = string.Empty;
    [Required, StringLength(1000)] public string ExemptionRule { get; set; } = string.Empty;
    [Required, StringLength(1000)] public string RefundRule { get; set; } = string.Empty;
    [Required, StringLength(1000)] public string RenewalRule { get; set; } = string.Empty;

    public override IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        foreach (var result in base.Validate(validationContext)) yield return result;

        if (Mode == ProcurementSupplierOnboardingFeeMode.Free)
        {
            if (Amount != 0)
                yield return new ValidationResult(
                    "A free supplier-onboarding token must have a zero amount.",
                    new[] { nameof(Amount) });
            if (TaxPercent != 0)
                yield return new ValidationResult(
                    "A free supplier-onboarding token cannot carry tax.",
                    new[] { nameof(TaxPercent) });
        }
        else
        {
            if (Amount <= 0)
                yield return new ValidationResult(
                    "A paid supplier-onboarding token must have a positive amount.",
                    new[] { nameof(Amount) });
            if (PaymentChannels.Count == 0)
                yield return new ValidationResult(
                    "A paid supplier-onboarding token requires at least one payment-method code.",
                    new[] { nameof(PaymentChannels) });
            if (!RevenueAccountId.HasValue || RevenueAccountId == Guid.Empty)
                yield return new ValidationResult(
                    "A paid supplier-onboarding token requires a revenue GL account.",
                    new[] { nameof(RevenueAccountId) });
            if (TaxPercent > 0 && (!TaxAccountId.HasValue || TaxAccountId == Guid.Empty))
                yield return new ValidationResult(
                    "A taxed supplier-onboarding token requires a tax payable GL account.",
                    new[] { nameof(TaxAccountId) });
        }

        if (!ReceiptNumberFormat.Contains("{SEQ}", StringComparison.OrdinalIgnoreCase) &&
            !System.Text.RegularExpressions.Regex.IsMatch(ReceiptNumberFormat, @"\{#+\}"))
            yield return new ValidationResult(
                "Receipt number format must contain {SEQ} or a {####} sequence token.",
                new[] { nameof(ReceiptNumberFormat) });
    }
}

public sealed class ProcurementSignatureDecisionValueDto : EffectiveDatedDecisionValueDto
{
    [Required, StringLength(150)] public string DocumentType { get; set; } = string.Empty;
    public ProcurementSignatureMode SignatureMode { get; set; }
    [MinLength(1)] public List<string> SignatoryRoles { get; set; } = new();
    [Range(1, 100)] public int SigningOrder { get; set; } = 1;
    [Required, StringLength(1000)] public string VerificationRule { get; set; } = string.Empty;
    [MinLength(1)] public List<string> EvidenceRequirements { get; set; } = new();
}

public sealed class ProcurementGhanepsDecisionValueDto : EffectiveDatedDecisionValueDto
{
    [Required, StringLength(100)] public string ProfileCode { get; set; } = string.Empty;
    [Required, MinLength(1)] public List<string> FileTemplateMappings { get; set; } = new();
    [Required, StringLength(100)] public string Frequency { get; set; } = string.Empty;
    [Required, StringLength(200)] public string Owner { get; set; } = string.Empty;
    [Required, StringLength(1000)] public string AcknowledgementRule { get; set; } = string.Empty;
    [Required, StringLength(1000)] public string ReconciliationRule { get; set; } = string.Empty;
}

public sealed class ProcurementNegativeStockDecisionValueDto : EffectiveDatedDecisionValueDto
{
    public ProcurementNegativeStockPolicy DefaultPolicy { get; set; } = ProcurementNegativeStockPolicy.Prohibited;
    public bool EmergencyOverrideEligible { get; set; }
    [Required, StringLength(200)] public string OverridePermission { get; set; } = string.Empty;
    public Guid? WorkflowDefinitionId { get; set; }
    [MinLength(1)] public List<string> EvidenceRequirements { get; set; } = new();
    [Range(1, 720)] public int OverrideDurationHours { get; set; } = 1;
    public bool AuditRequired { get; set; } = true;
}

public sealed class ProcurementSupplierRiskDecisionValueDto : EffectiveDatedDecisionValueDto
{
    [Range(1, 120)] public int ReviewFrequencyMonths { get; set; } = 12;
    [Range(1, 120)] public int ExposureWindowMonths { get; set; }
    [MinLength(1)] public List<string> RiskDimensions { get; set; } = new();
    [MinLength(1)] public List<string> RiskBands { get; set; } = new();
    [Range(typeof(decimal), "0", "100")] public decimal ConcentrationLimitPercent { get; set; }
    [Range(typeof(decimal), "0", "100")] public decimal MinimumScore { get; set; }
    public ProcurementSupplierRiskEligibilityAction EligibilityAction { get; set; } =
        ProcurementSupplierRiskEligibilityAction.AlertOnly;
    [Range(1, 120)] public int PerformanceWindowMonths { get; set; }
    [MinLength(7)] public List<string> PerformanceDimensions { get; set; } = new();
    [MinLength(1)] public List<string> PerformanceBands { get; set; } = new();
    [Range(typeof(decimal), "1", "100")]
    public decimal MinimumPerformanceDataCoveragePercent { get; set; }
    [Range(typeof(decimal), "1", "8760")] public decimal ResponseTargetHours { get; set; }
    public ProcurementSupplierRiskEligibilityAction PerformanceEligibilityAction { get; set; } =
        ProcurementSupplierRiskEligibilityAction.AlertOnly;

    public override IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        foreach (var result in base.Validate(validationContext)) yield return result;

        var dimensions = new List<(string Name, decimal Weight)>();
        foreach (var value in RiskDimensions)
        {
            var parts = value.Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) ||
                !decimal.TryParse(parts[1],
                    System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var weight) ||
                weight <= 0 || weight > 100)
            {
                yield return new ValidationResult(
                    "Risk dimensions must use Metric=WeightPercent with a weight above 0 and not above 100.",
                    new[] { nameof(RiskDimensions) });
                continue;
            }
            if (!ProcurementSupplierRiskDimensionCatalog.TryResolve(parts[0], out _))
            {
                yield return new ValidationResult(
                    $"Risk dimension '{parts[0]}' is not supported. Supported metrics: {string.Join(", ", ProcurementSupplierRiskDimensionCatalog.SupportedNames)}.",
                    new[] { nameof(RiskDimensions) });
                continue;
            }
            dimensions.Add((parts[0], weight));
        }
        if (dimensions.GroupBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Any(group => group.Count() > 1))
            yield return new ValidationResult(
                "Risk dimension names must be unique.",
                new[] { nameof(RiskDimensions) });
        if (dimensions.Count == RiskDimensions.Count &&
            dimensions.Sum(item => item.Weight) != 100)
            yield return new ValidationResult(
                "Risk dimension weights must total exactly 100 percent.",
                new[] { nameof(RiskDimensions) });

        var bands = new List<(string Name, decimal Minimum, decimal Maximum)>();
        foreach (var value in RiskBands)
        {
            var nameAndRange = value.Split('=', 2, StringSplitOptions.TrimEntries);
            var bounds = nameAndRange.Length == 2
                ? nameAndRange[1].Split('-', 2, StringSplitOptions.TrimEntries)
                : Array.Empty<string>();
            if (nameAndRange.Length != 2 || string.IsNullOrWhiteSpace(nameAndRange[0]) ||
                bounds.Length != 2 ||
                !decimal.TryParse(bounds[0],
                    System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var minimum) ||
                !decimal.TryParse(bounds[1],
                    System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var maximum) ||
                minimum < 0 || maximum > 100 || maximum <= minimum)
            {
                yield return new ValidationResult(
                    "Risk bands must use BandName=Minimum-Maximum within 0 to 100.",
                    new[] { nameof(RiskBands) });
                continue;
            }
            bands.Add((nameAndRange[0], minimum, maximum));
        }
        if (bands.GroupBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Any(group => group.Count() > 1))
            yield return new ValidationResult(
                "Risk band names must be unique.",
                new[] { nameof(RiskBands) });
        if (bands.Count == RiskBands.Count && bands.Count > 0)
        {
            var ordered = bands.OrderBy(item => item.Minimum).ToList();
            if (ordered[0].Minimum != 0 || ordered[^1].Maximum != 100 ||
                ordered.Zip(ordered.Skip(1), (left, right) =>
                        left.Maximum == right.Minimum)
                    .Any(contiguous => !contiguous))
                yield return new ValidationResult(
                    "Risk bands must provide contiguous coverage from 0 through 100.",
                    new[] { nameof(RiskBands) });
        }

        var performanceDimensions = new List<(string Name, decimal Weight)>();
        foreach (var value in PerformanceDimensions)
        {
            var parts = value.Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) ||
                !Enum.TryParse<ProcurementSupplierPerformanceMetricKey>(
                    parts[0], true, out _) ||
                !decimal.TryParse(parts[1],
                    System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var weight) ||
                weight <= 0 || weight > 100)
            {
                yield return new ValidationResult(
                    "Performance dimensions must use a supported Metric=WeightPercent value with a weight above 0 and not above 100.",
                    new[] { nameof(PerformanceDimensions) });
                continue;
            }
            performanceDimensions.Add((parts[0], weight));
        }
        if (performanceDimensions.GroupBy(item => item.Name,
                StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
            yield return new ValidationResult(
                "Performance dimension names must be unique.",
                new[] { nameof(PerformanceDimensions) });
        var requiredPerformanceDimensions =
            Enum.GetNames<ProcurementSupplierPerformanceMetricKey>();
        if (performanceDimensions.Count == PerformanceDimensions.Count &&
            (performanceDimensions.Count != requiredPerformanceDimensions.Length ||
             requiredPerformanceDimensions.Except(
                 performanceDimensions.Select(item => item.Name),
                 StringComparer.OrdinalIgnoreCase).Any()))
            yield return new ValidationResult(
                "Performance dimensions must configure DeliveryTimeliness, GrnQuality, RejectionRate, PriceCompetitiveness, Responsiveness, ComplaintResolution, and ContractCompletion exactly once.",
                new[] { nameof(PerformanceDimensions) });
        if (performanceDimensions.Count == PerformanceDimensions.Count &&
            performanceDimensions.Sum(item => item.Weight) != 100)
            yield return new ValidationResult(
                "Performance dimension weights must total exactly 100 percent.",
                new[] { nameof(PerformanceDimensions) });

        var performanceBands =
            new List<(string Name, decimal Minimum, decimal Maximum)>();
        foreach (var value in PerformanceBands)
        {
            var nameAndRange = value.Split('=', 2, StringSplitOptions.TrimEntries);
            var bounds = nameAndRange.Length == 2
                ? nameAndRange[1].Split('-', 2, StringSplitOptions.TrimEntries)
                : Array.Empty<string>();
            if (nameAndRange.Length != 2 ||
                string.IsNullOrWhiteSpace(nameAndRange[0]) ||
                bounds.Length != 2 ||
                !decimal.TryParse(bounds[0],
                    System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var minimum) ||
                !decimal.TryParse(bounds[1],
                    System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var maximum) ||
                minimum < 0 || maximum > 100 || maximum <= minimum)
            {
                yield return new ValidationResult(
                    "Performance bands must use BandName=Minimum-Maximum within 0 to 100.",
                    new[] { nameof(PerformanceBands) });
                continue;
            }
            performanceBands.Add((nameAndRange[0], minimum, maximum));
        }
        if (performanceBands.GroupBy(item => item.Name,
                StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
            yield return new ValidationResult(
                "Performance band names must be unique.",
                new[] { nameof(PerformanceBands) });
        if (performanceBands.Count == PerformanceBands.Count &&
            performanceBands.Count > 0)
        {
            var ordered = performanceBands.OrderBy(item => item.Minimum).ToList();
            if (ordered[0].Minimum != 0 || ordered[^1].Maximum != 100 ||
                ordered.Zip(ordered.Skip(1), (left, right) =>
                        left.Maximum == right.Minimum)
                    .Any(contiguous => !contiguous))
                yield return new ValidationResult(
                    "Performance bands must provide contiguous coverage from 0 through 100.",
                    new[] { nameof(PerformanceBands) });
        }
    }
}

public sealed class ProcurementCutoverDecisionValueDto : EffectiveDatedDecisionValueDto
{
    public DateTime CutoverDate { get; set; }
    [Range(0, 365)] public int DualRunPeriodDays { get; set; }
    [Required, StringLength(200)] public string DataOwner { get; set; } = string.Empty;
    [MinLength(1)] public List<string> AcceptanceSignatories { get; set; } = new();
    [Required, StringLength(100)] public string ReleaseStatus { get; set; } = string.Empty;
    [MinLength(1)] public List<string> EvidenceRequirements { get; set; } = new();

    public override IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        foreach (var result in base.Validate(validationContext)) yield return result;
        if (CutoverDate == default)
            yield return new ValidationResult("Cutover date is required.", new[] { nameof(CutoverDate) });
    }
}

public sealed class ProcurementReceiptDocumentDecisionValueDto : EffectiveDatedDecisionValueDto
{
    public ProcurementReceiptDocumentType DocumentType { get; set; }
    [Required, StringLength(1000)] public string ApplicabilityRule { get; set; } = string.Empty;
    public ProcurementReceiptCoexistenceRule CoexistenceRule { get; set; }
    [Required, StringLength(200)] public string NumberFormat { get; set; } = string.Empty;
    [Required, StringLength(500)] public string TemplateReference { get; set; } = string.Empty;
    [StringLength(200)] public string? GrnNumberFormat { get; set; }
    [StringLength(200)] public string? MrnNumberFormat { get; set; }
    [StringLength(80)] public string? GrnTemplateReference { get; set; }
    [StringLength(80)] public string? MrnTemplateReference { get; set; }
    [MinLength(1)] public List<string> SignatureRequirements { get; set; } = new();
    [MinLength(1)] public List<string> EvidenceRequirements { get; set; } = new();

    public override IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        foreach (var result in base.Validate(validationContext)) yield return result;

        if (DocumentType is ProcurementReceiptDocumentType.Grn or ProcurementReceiptDocumentType.GrnAndMrn)
            foreach (var result in ValidateFormat(ResolveNumberFormat(ProcurementReceiptDocumentType.Grn), nameof(NumberFormat)))
                yield return result;
        if (DocumentType is ProcurementReceiptDocumentType.Mrn or ProcurementReceiptDocumentType.GrnAndMrn)
            foreach (var result in ValidateFormat(ResolveNumberFormat(ProcurementReceiptDocumentType.Mrn), nameof(NumberFormat)))
                yield return result;

        if (DocumentType == ProcurementReceiptDocumentType.GrnAndMrn &&
            string.Equals(ResolveNumberFormat(ProcurementReceiptDocumentType.Grn), ResolveNumberFormat(ProcurementReceiptDocumentType.Mrn), StringComparison.OrdinalIgnoreCase))
            yield return new ValidationResult("GRN and MRN number formats must resolve to distinct values.",
                new[] { nameof(GrnNumberFormat), nameof(MrnNumberFormat), nameof(NumberFormat) });

        if (DocumentType == ProcurementReceiptDocumentType.GrnAndMrn &&
            CoexistenceRule == ProcurementReceiptCoexistenceRule.MutuallyExclusive)
            yield return new ValidationResult("GRN and MRN cannot be mutually exclusive when both document types are required.",
                new[] { nameof(DocumentType), nameof(CoexistenceRule) });
        if (DocumentType != ProcurementReceiptDocumentType.GrnAndMrn &&
            CoexistenceRule != ProcurementReceiptCoexistenceRule.MutuallyExclusive)
            yield return new ValidationResult("A single configured receipt-document type must use the mutually-exclusive coexistence rule.",
                new[] { nameof(DocumentType), nameof(CoexistenceRule) });

        if (DocumentType is ProcurementReceiptDocumentType.Grn or ProcurementReceiptDocumentType.GrnAndMrn &&
            string.IsNullOrWhiteSpace(ResolveTemplateReference(ProcurementReceiptDocumentType.Grn)))
            yield return new ValidationResult("A GRN template reference is required.", new[] { nameof(TemplateReference) });
        if (DocumentType is ProcurementReceiptDocumentType.Mrn or ProcurementReceiptDocumentType.GrnAndMrn &&
            string.IsNullOrWhiteSpace(ResolveTemplateReference(ProcurementReceiptDocumentType.Mrn)))
            yield return new ValidationResult("An MRN template reference is required.", new[] { nameof(TemplateReference) });
        if (DocumentType == ProcurementReceiptDocumentType.GrnAndMrn &&
            string.Equals(ResolveTemplateReference(ProcurementReceiptDocumentType.Grn), ResolveTemplateReference(ProcurementReceiptDocumentType.Mrn), StringComparison.OrdinalIgnoreCase))
            yield return new ValidationResult("GRN and MRN central DMS template references must resolve to distinct values.",
                new[] { nameof(GrnTemplateReference), nameof(MrnTemplateReference), nameof(TemplateReference) });

        var roles = SignatureRequirements.Select(item => item.Trim()).Where(item => item.Length > 0).ToList();
        if (roles.Count != roles.Distinct(StringComparer.OrdinalIgnoreCase).Count())
            yield return new ValidationResult("Signature requirements must be unique.", new[] { nameof(SignatureRequirements) });
    }

    public string ResolveNumberFormat(ProcurementReceiptDocumentType type)
    {
        var specific = type == ProcurementReceiptDocumentType.Mrn ? MrnNumberFormat : GrnNumberFormat;
        return (!string.IsNullOrWhiteSpace(specific) ? specific : NumberFormat)
            .Replace("{TYPE}", type == ProcurementReceiptDocumentType.Mrn ? "MRN" : "GRN", StringComparison.OrdinalIgnoreCase)
            .Trim();
    }

    public string ResolveTemplateReference(ProcurementReceiptDocumentType type)
    {
        var specific = type == ProcurementReceiptDocumentType.Mrn ? MrnTemplateReference : GrnTemplateReference;
        return (!string.IsNullOrWhiteSpace(specific) ? specific : TemplateReference)
            .Replace("{TYPE}", type == ProcurementReceiptDocumentType.Mrn ? "MRN" : "GRN", StringComparison.OrdinalIgnoreCase)
            .Trim();
    }

    private static IEnumerable<ValidationResult> ValidateFormat(string format, string member)
    {
        if (string.IsNullOrWhiteSpace(format) ||
            (!format.Contains("{SEQ}", StringComparison.OrdinalIgnoreCase) &&
             !System.Text.RegularExpressions.Regex.IsMatch(format, @"\{#+\}")))
            yield return new ValidationResult("Each receipt-document number format must contain {SEQ} or a {####} sequence token.", new[] { member });
    }
}

public sealed class ProcurementNonFunctionalDecisionValueDto : EffectiveDatedDecisionValueDto
{
    [Required, StringLength(500)] public string WorkloadScenario { get; set; } = string.Empty;
    [Range(typeof(decimal), "0", "100")] public decimal AvailabilityTargetPercent { get; set; }
    [Range(1, 600000)] public int ResponseTargetMilliseconds { get; set; }
    [Range(1, 168)] public int BackupFrequencyHours { get; set; }
    [Range(0, 10080)] public int RpoMinutes { get; set; }
    [Range(0, 10080)] public int RtoMinutes { get; set; }
    [Required, StringLength(1000)] public string AuthenticationTarget { get; set; } = string.Empty;
    [Required, StringLength(1000)] public string MonitoringTarget { get; set; } = string.Empty;
    [Required, StringLength(1000)] public string UsabilityTarget { get; set; } = string.Empty;
    [Required, StringLength(1000)] public string AccessibilityTarget { get; set; } = string.Empty;
    [Required, StringLength(1000)] public string AcceptanceMethod { get; set; } = string.Empty;
}
