using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Projects;

public sealed class CivilEngineeringProfileListRequest
{
    public string? Search { get; set; }
    public CivilEngineeringConfigurationProfileStatus? Status { get; set; }
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 20;
}

public sealed class CivilEngineeringPagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
}

public class CivilEngineeringProfileSummaryDto
{
    public Guid Id { get; init; }
    public Guid ProfileKey { get; init; }
    public string ProfileCode { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int Version { get; init; }
    public CivilEngineeringConfigurationProfileStatus LifecycleStatus { get; init; }
    public DateTime EffectiveFrom { get; init; }
    public DateTime? EffectiveTo { get; init; }
    public bool IsDefault { get; init; }
    public int CompleteDecisionCount { get; init; }
    public int TotalDecisionCount { get; init; }
    public bool IsComplete => TotalDecisionCount == 13 && CompleteDecisionCount == 13;
    public DateTime UpdatedAt { get; init; }
    public string RowVersion { get; init; } = string.Empty;
}

public sealed class CivilEngineeringProfileDto : CivilEngineeringProfileSummaryDto
{
    public string? ChangeSummary { get; init; }
    public Guid? SupersedesProfileId { get; init; }
    public DateTime? PublishedAt { get; init; }
    public DateTime? RetiredAt { get; init; }
    public IReadOnlyList<CivilEngineeringDecisionDto> Decisions { get; init; } = Array.Empty<CivilEngineeringDecisionDto>();
    public CivilEngineeringValidationResultDto Validation { get; init; } = new();
}

public sealed class CivilEngineeringDecisionDto
{
    public Guid Id { get; init; }
    public string ConfigurationKey { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string OwnerGroup { get; init; } = string.Empty;
    public int SchemaVersion { get; init; }
    public CivilEngineeringConfigurationDecisionStatus Status { get; init; }
    public CivilEngineeringConfigurationApprovalStatus ApprovalStatus { get; init; }
    public CivilEngineeringConfigurationEvidenceStatus EvidenceStatus { get; init; }
    public JsonElement Value { get; init; }
    public DateTime? DecisionDate { get; init; }
    public DateTime? EffectiveFrom { get; init; }
    public DateTime? EffectiveTo { get; init; }
    public Guid? ApprovedById { get; init; }
    public DateTime? ApprovedAt { get; init; }
    public string? ApprovalReference { get; init; }
    public string? SourceLineage { get; init; }
    public string? Notes { get; init; }
    public bool IsComplete { get; init; }
    public string RowVersion { get; init; } = string.Empty;
    public IReadOnlyList<CivilEngineeringEvidenceDto> Evidence { get; init; } = Array.Empty<CivilEngineeringEvidenceDto>();
}

public sealed class CivilEngineeringEvidenceDto
{
    public Guid Id { get; init; }
    public Guid DecisionId { get; init; }
    public string EvidenceType { get; init; } = string.Empty;
    public Guid CentralDocumentRecordId { get; init; }
    public Guid CentralDocumentVersionId { get; init; }
    public string? DocumentReference { get; init; }
    public string? DocumentTitle { get; init; }
    public string? VersionNumber { get; init; }
    public string? Checksum { get; init; }
    public DateTime LinkedAt { get; init; }
    public Guid LinkedById { get; init; }
}

public sealed class CivilEngineeringRevisionDto
{
    public Guid Id { get; init; }
    public Guid ProfileId { get; init; }
    public Guid? DecisionId { get; init; }
    public string SourceType { get; init; } = string.Empty;
    public Guid SourceId { get; init; }
    public string Action { get; init; } = string.Empty;
    public AuditOperationKind Operation { get; init; }
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

public sealed class CivilEngineeringLookupOptionDto
{
    public string Value { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string? Group { get; init; }
}

public sealed class CivilEngineeringLookupsDto
{
    public IReadOnlyDictionary<string, IReadOnlyList<CivilEngineeringLookupOptionDto>> Sources { get; init; } =
        new Dictionary<string, IReadOnlyList<CivilEngineeringLookupOptionDto>>();
}

public sealed class CivilEngineeringValidationIssueDto
{
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string? ConfigurationKey { get; init; }
    public string Severity { get; init; } = "Error";
}

public sealed class CivilEngineeringValidationResultDto
{
    public bool IsValid => Errors.Count == 0;
    public IReadOnlyList<CivilEngineeringValidationIssueDto> Errors { get; init; } = Array.Empty<CivilEngineeringValidationIssueDto>();
    public IReadOnlyList<CivilEngineeringValidationIssueDto> Warnings { get; init; } = Array.Empty<CivilEngineeringValidationIssueDto>();
}

public class CreateCivilEngineeringProfileRequest
{
    [Required, StringLength(200)] public string Name { get; set; } = string.Empty;
    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow.Date;
    public DateTime? EffectiveTo { get; set; }
    [StringLength(1000)] public string? ChangeSummary { get; set; }
    public bool IsDefault { get; set; }
}

public sealed class UpdateCivilEngineeringProfileRequest : CreateCivilEngineeringProfileRequest
{
    [Required] public string RowVersion { get; set; } = string.Empty;
    [StringLength(1000)] public string? Reason { get; set; }
}

public sealed class SaveCivilEngineeringDecisionRequest
{
    public int SchemaVersion { get; set; } = 1;
    public JsonElement Value { get; set; }
    [StringLength(1000)] public string? SourceLineage { get; set; }
    [StringLength(2000)] public string? Notes { get; set; }
    [Required] public string RowVersion { get; set; } = string.Empty;
    [StringLength(1000)] public string? Reason { get; set; }
}

public class SubmitCivilEngineeringDecisionRequest
{
    [Required] public string RowVersion { get; set; } = string.Empty;
    [StringLength(1000)] public string? Reason { get; set; }
}

public sealed class DecideCivilEngineeringDecisionRequest : SubmitCivilEngineeringDecisionRequest
{
    [Required, StringLength(500)] public string ApprovalReference { get; set; } = string.Empty;
}

public sealed class CivilEngineeringLifecycleRequest
{
    [Required] public string RowVersion { get; set; } = string.Empty;
    [Required, StringLength(1000, MinimumLength = 3)] public string Reason { get; set; } = string.Empty;
}

public sealed class CloneCivilEngineeringProfileRequest
{
    public DateTime? EffectiveFrom { get; set; }
    [StringLength(1000)] public string? ChangeSummary { get; set; }
}

public sealed class LinkCivilEngineeringEvidenceRequest
{
    [Required, StringLength(100)] public string EvidenceType { get; set; } = string.Empty;
    public Guid CentralDocumentRecordId { get; set; }
    public Guid CentralDocumentVersionId { get; set; }
    [StringLength(128)] public string? Checksum { get; set; }
    [Required] public string DecisionRowVersion { get; set; } = string.Empty;
    [StringLength(1000)] public string? Reason { get; set; }
}

public sealed class CivilEngineeringFieldSchemaDto
{
    public string Name { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string Control { get; init; } = string.Empty;
    public bool Required { get; init; }
    public string? LookupSource { get; init; }
    public string? LookupGroup { get; init; }
    public IReadOnlyList<string> Options { get; init; } = Array.Empty<string>();
    public decimal? Minimum { get; init; }
    public decimal? Maximum { get; init; }
}

public sealed class CivilEngineeringDecisionSchemaDto
{
    public string ConfigurationKey { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string OwnerGroup { get; init; } = string.Empty;
    public int SchemaVersion { get; init; }
    public IReadOnlyList<CivilEngineeringFieldSchemaDto> Fields { get; init; } = Array.Empty<CivilEngineeringFieldSchemaDto>();
}

public abstract class CivilEngineeringEffectiveDecisionValue : IValidatableObject
{
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }

    public virtual IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (EffectiveFrom == default)
            yield return new ValidationResult("Effective from is required.", new[] { nameof(EffectiveFrom) });
        if (EffectiveTo.HasValue && EffectiveTo.Value.Date < EffectiveFrom.Date)
            yield return new ValidationResult("Effective to cannot be before effective from.", new[] { nameof(EffectiveTo) });
    }
}

public sealed class CivilEngineeringRolesAuthorityValue : CivilEngineeringEffectiveDecisionValue
{
    [MinLength(1)] public List<Guid> OperationalRoleIds { get; set; } = new();
    [MinLength(1)] public List<Guid> ApprovalRoleIds { get; set; } = new();
    [MinLength(1)] public List<Guid> OversightRoleIds { get; set; } = new();
    [MinLength(1)] public List<Guid> ExternalContributorRoleIds { get; set; } = new();
    [Required, RegularExpression("^[A-Z]{3}$")] public string CurrencyCode { get; set; } = "GHS";
    [Range(0, double.MaxValue)] public decimal OperationalAuthorityLimit { get; set; }
    [Range(0, double.MaxValue)] public decimal SeniorAuthorityLimit { get; set; }
    [Range(0, double.MaxValue)] public decimal ExecutiveAuthorityLimit { get; set; }
    public bool EnforceProjectScope { get; set; } = true;
    public bool EnforcePropertyScope { get; set; } = true;
    public bool EnforceDepartmentScope { get; set; } = true;

    public override IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        foreach (var item in base.Validate(context)) yield return item;
        if (OperationalAuthorityLimit > SeniorAuthorityLimit || SeniorAuthorityLimit > ExecutiveAuthorityLimit)
            yield return new ValidationResult("Authority limits must increase from operational to senior to executive.");
    }
}

public sealed class CivilEngineeringWorkClassificationValue : CivilEngineeringEffectiveDecisionValue
{
    [MinLength(1)] public List<Guid> ProjectTypeIds { get; set; } = new();
    [MinLength(1)] public List<CivilEngineeringWorkClassification> AllowedClassifications { get; set; } = new();
    public CivilEngineeringWorkClassification DefaultClassification { get; set; }
    public bool RequireProjectReference { get; set; } = true;
    public bool RequirePropertyReference { get; set; }
    public bool RequireLocationReference { get; set; } = true;
    public bool RequirePlanningGisValidation { get; set; } = true;

    public override IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        foreach (var item in base.Validate(context)) yield return item;
        if (AllowedClassifications.Count > 0 && !AllowedClassifications.Contains(DefaultClassification))
            yield return new ValidationResult("Default classification must be one of the allowed classifications.");
    }
}

public sealed class CivilEngineeringDesignReviewValue : CivilEngineeringEffectiveDecisionValue
{
    public Guid WorkflowDefinitionId { get; set; }
    public Guid SiteReconnaissanceTemplateId { get; set; }
    public Guid CrossSectionTemplateId { get; set; }
    public Guid DraftingReviewTemplateId { get; set; }
    public Guid SubmissionPackageTemplateId { get; set; }
    [MinLength(1)] public List<CivilEngineeringDesignDiscipline> RequiredDisciplines { get; set; } = new();
    [MinLength(1)] public List<Guid> ReviewerRoleIds { get; set; } = new();
    public bool RequireSiteReconnaissance { get; set; } = true;
    public bool RequireVersionedReview { get; set; } = true;
    public bool RequireHodApproval { get; set; } = true;
}

public sealed class CivilEngineeringDocumentPolicyValue : CivilEngineeringEffectiveDecisionValue
{
    [MinLength(1)] public List<string> AllowedFileExtensions { get; set; } = new();
    [Range(1, 500)] public int MaximumFileSizeMb { get; set; } = 100;
    public Guid MetadataTemplateId { get; set; }
    [MinLength(1)] public List<Guid> OwnerRoleIds { get; set; } = new();
    [MinLength(1)] public List<Guid> ReviewerRoleIds { get; set; } = new();
    public CivilEngineeringDocumentNamingPolicy NamingPolicy { get; set; }
    [Range(365, 36500)] public int MinimumRetentionDays { get; set; } = 2555;
    public bool RequireVersioning { get; set; } = true;
    public bool AllowAuthorizedPreview { get; set; } = true;
    public bool AllowAuthorizedDownload { get; set; } = true;
}

public sealed class CivilEngineeringSupervisionWorkflowValue : CivilEngineeringEffectiveDecisionValue
{
    public Guid SiteInstructionWorkflowDefinitionId { get; set; }
    public Guid RfiWorkflowDefinitionId { get; set; }
    public Guid TestReportWorkflowDefinitionId { get; set; }
    public Guid InterimCertificateWorkflowDefinitionId { get; set; }
    [MinLength(1)] public List<Guid> ProjectEngineerRoleIds { get; set; } = new();
    [MinLength(1)] public List<Guid> ProjectManagerRoleIds { get; set; } = new();
    [MinLength(1)] public List<Guid> CoordinatorRoleIds { get; set; } = new();
    public bool EnforceOrderedRouting { get; set; } = true;
    public bool RequireDmsEvidence { get; set; } = true;
    public bool RequireIndependentEndorsement { get; set; } = true;
}

public sealed class CivilEngineeringWeeklyReportValue : CivilEngineeringEffectiveDecisionValue
{
    public Guid MetadataTemplateId { get; set; }
    public Guid WorkflowDefinitionId { get; set; }
    [MinLength(1)] public List<Guid> ActivityCategoryIds { get; set; } = new();
    [MinLength(1)] public List<Guid> EscalationRoleIds { get; set; } = new();
    [Range(0, 100)] public int MinimumPhotoCount { get; set; }
    public CivilEngineeringReportDueDay DueDay { get; set; }
    public bool RequireProgressMeasurement { get; set; } = true;
    public bool RequireMaterialUsage { get; set; } = true;
    public bool RequireSafetyNotes { get; set; } = true;
    public bool RequireTestSummary { get; set; } = true;
}

public sealed class CivilEngineeringMaintenanceAssessmentValue : CivilEngineeringEffectiveDecisionValue
{
    public Guid WorkflowDefinitionId { get; set; }
    public Guid MetadataTemplateId { get; set; }
    [MinLength(1)] public List<CivilEngineeringRequestSource> AllowedRequestSources { get; set; } = new();
    [MinLength(1)] public List<Guid> DefectCategoryIds { get; set; } = new();
    [MinLength(1)] public List<CivilEngineeringUrgency> AllowedUrgencies { get; set; } = new();
    public bool RequireAssetOrProperty { get; set; } = true;
    public bool RequireSiteAssessment { get; set; } = true;
    public bool RequireCostEstimate { get; set; } = true;
    public bool RequireInspectionBeforeClosure { get; set; } = true;
    public bool RequireClosureEvidence { get; set; } = true;
}

public sealed class CivilEngineeringCostingApprovalValue : CivilEngineeringEffectiveDecisionValue
{
    [Required, RegularExpression("^[A-Z]{3}$")] public string CurrencyCode { get; set; } = "GHS";
    [Range(0, double.MaxValue)] public decimal QsReviewThreshold { get; set; }
    [Range(0, double.MaxValue)] public decimal ProcurementReviewThreshold { get; set; }
    [Range(0, double.MaxValue)] public decimal ManagementApprovalThreshold { get; set; }
    public Guid MaintenanceWorkflowDefinitionId { get; set; }
    public Guid ComplaintWorkflowDefinitionId { get; set; }
    public Guid ContractorWorkflowDefinitionId { get; set; }
    [MinLength(1)] public List<Guid> CostReviewerRoleIds { get; set; } = new();
    [MinLength(1)] public List<Guid> ApproverRoleIds { get; set; } = new();
    public bool RequireBudgetValidation { get; set; } = true;

    public override IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        foreach (var item in base.Validate(context)) yield return item;
        if (QsReviewThreshold > ProcurementReviewThreshold || ProcurementReviewThreshold > ManagementApprovalThreshold)
            yield return new ValidationResult("Cost approval thresholds must increase from QS review to Procurement review to Management approval.");
    }
}

public sealed class CivilEngineeringPermittingReviewValue : CivilEngineeringEffectiveDecisionValue
{
    public Guid WorkflowDefinitionId { get; set; }
    [MinLength(1)] public List<Guid> HandoffRoleIds { get; set; } = new();
    [MinLength(1)] public List<Guid> CommentCategoryIds { get; set; } = new();
    [MinLength(1)] public List<CivilEngineeringPermittingOutcome> AllowedOutcomes { get; set; } = new();
    public bool RequireProjectAndPropertyValidation { get; set; } = true;
    public bool RequireReasonForReturnOrRejection { get; set; } = true;
    public bool RequireHodDecision { get; set; } = true;
}

public sealed class CivilEngineeringTaskAssignmentValue : CivilEngineeringEffectiveDecisionValue
{
    public Guid WorkflowDefinitionId { get; set; }
    public Guid FeedbackMetadataTemplateId { get; set; }
    [MinLength(1)] public List<Guid> AssigneeRoleIds { get; set; } = new();
    public List<Guid> UrgentEscalationRoleIds { get; set; } = new();
    [MinLength(1)] public List<CivilEngineeringUrgency> AllowedUrgencies { get; set; } = new();
    [Range(1, 720)] public int UrgentResponseHours { get; set; } = 24;
    public bool AllowControlledReassignment { get; set; } = true;
    public bool RequireDueDate { get; set; } = true;
    public bool RequireFeedbackEvidence { get; set; } = true;
    public bool RequireClosureAcceptance { get; set; } = true;
}

public sealed class CivilEngineeringQualityTestValue : CivilEngineeringEffectiveDecisionValue
{
    public Guid WorkflowDefinitionId { get; set; }
    public Guid EvidenceMetadataTemplateId { get; set; }
    [MinLength(1)] public List<CivilEngineeringQualityTestCategory> TestCategories { get; set; } = new();
    [MinLength(1)] public List<Guid> ReviewerRoleIds { get; set; } = new();
    public bool RequireConfiguredPassThreshold { get; set; } = true;
    public bool RequireIndependentReview { get; set; } = true;
    public bool RequireEndorsementEvidence { get; set; } = true;
    public bool BlockAcceptanceOnFailure { get; set; } = true;
}

public sealed class CivilEngineeringReportPackValue : CivilEngineeringEffectiveDecisionValue
{
    [MinLength(1)] public List<Guid> ReportIds { get; set; } = new();
    [MinLength(1)] public List<Guid> ViewerRoleIds { get; set; } = new();
    [MinLength(1)] public List<string> ExportFormats { get; set; } = new();
    public bool EnforceProjectAndPropertyScope { get; set; } = true;
    public bool AllowScheduledDistribution { get; set; }
    public bool RequireAuditDrilldown { get; set; } = true;
}

public sealed class CivilEngineeringMigrationValue : CivilEngineeringEffectiveDecisionValue
{
    [MinLength(1)] public List<CivilEngineeringMigrationSource> SourceTypes { get; set; } = new();
    [MinLength(1)] public List<Guid> OwnerRoleIds { get; set; } = new();
    [MinLength(1)] public List<Guid> ReviewerRoleIds { get; set; } = new();
    [MinLength(1)] public List<Guid> SignOffRoleIds { get; set; } = new();
    public Guid ReconciliationEvidenceTemplateId { get; set; }
    public bool RequireStaging { get; set; } = true;
    public bool RequireReconciliation { get; set; } = true;
    public bool RequireSignedAcceptance { get; set; } = true;
    public bool PreservePhysicalFileReference { get; set; } = true;
}

/// <summary>
/// Controls only the Civil EOT envelope. Contract amendment, variation valuation,
/// budget revision and Finance posting remain with their authoritative owners.
/// </summary>
public sealed class CivilEngineeringExtensionOfTimeValue : CivilEngineeringEffectiveDecisionValue
{
    public Guid WorkflowDefinitionId { get; set; }
    public Guid EvidenceMetadataTemplateId { get; set; }
    [MinLength(1)] public List<Guid> ReviewerRoleIds { get; set; } = new();
    public bool RequireQuantitySurveyVariationForCostImpact { get; set; } = true;
    public bool RequireFinanceBudgetRevalidation { get; set; } = true;
    public bool RequireProcurementContractRevalidation { get; set; } = true;
    public bool RequireIndependentApproval { get; set; } = true;
}
