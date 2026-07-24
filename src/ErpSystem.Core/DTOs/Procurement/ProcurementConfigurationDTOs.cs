using System.ComponentModel.DataAnnotations;
using System.Text.Json;
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
    [Required, StringLength(150)] public string FeeType { get; set; } = string.Empty;
    [Range(typeof(decimal), "0", "9999999999999999")] public decimal Amount { get; set; }
    [Required, StringLength(3), RegularExpression("^[A-Z]{3}$")] public string CurrencyCode { get; set; } = "GHS";
    [Range(typeof(decimal), "0", "100")] public decimal TaxPercent { get; set; }
    [MinLength(1)] public List<string> PaymentChannels { get; set; } = new();
    [Required, StringLength(200)] public string ReceiptNumberFormat { get; set; } = string.Empty;
    [Required, StringLength(1000)] public string ExemptionRule { get; set; } = string.Empty;
    [Required, StringLength(1000)] public string RefundRule { get; set; } = string.Empty;
    [Required, StringLength(1000)] public string RenewalRule { get; set; } = string.Empty;
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
    [MinLength(1)] public List<string> RiskDimensions { get; set; } = new();
    [MinLength(1)] public List<string> RiskBands { get; set; } = new();
    [Range(typeof(decimal), "0", "100")] public decimal ConcentrationLimitPercent { get; set; }
    [Range(typeof(decimal), "0", "100")] public decimal MinimumScore { get; set; }
    [Required, StringLength(500)] public string EligibilityAction { get; set; } = string.Empty;
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
    [MinLength(1)] public List<string> SignatureRequirements { get; set; } = new();
    [MinLength(1)] public List<string> EvidenceRequirements { get; set; } = new();
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
