using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.QuantitySurvey;

public sealed class QuantitySurveyProfileListRequest
{
    public string? Search { get; set; }
    public QuantitySurveyConfigurationProfileStatus? Status { get; set; }
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 20;
}

public sealed class QuantitySurveyPagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
}

public class QuantitySurveyProfileSummaryDto
{
    public Guid Id { get; init; }
    public Guid ProfileKey { get; init; }
    public string ProfileCode { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int Version { get; init; }
    public QuantitySurveyConfigurationProfileStatus LifecycleStatus { get; init; }
    public DateTime EffectiveFrom { get; init; }
    public DateTime? EffectiveTo { get; init; }
    public bool IsDefault { get; init; }
    public int CompleteDecisionCount { get; init; }
    public int TotalDecisionCount { get; init; }
    public bool IsComplete => TotalDecisionCount == 17 && CompleteDecisionCount == 17;
    public DateTime UpdatedAt { get; init; }
    public string RowVersion { get; init; } = string.Empty;
}

public sealed class QuantitySurveyProfileDto : QuantitySurveyProfileSummaryDto
{
    public string? ChangeSummary { get; init; }
    public Guid? SupersedesProfileId { get; init; }
    public DateTime? PublishedAt { get; init; }
    public DateTime? RetiredAt { get; init; }
    public IReadOnlyList<QuantitySurveyDecisionDto> Decisions { get; init; } = Array.Empty<QuantitySurveyDecisionDto>();
    public QuantitySurveyValidationResultDto Validation { get; init; } = new();
    public IReadOnlyList<QuantitySurveyRevisionDto> RecentHistory { get; init; } = Array.Empty<QuantitySurveyRevisionDto>();
}

public sealed class QuantitySurveyDecisionDto
{
    public Guid Id { get; init; }
    public string DecisionKey { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string OwnerGroup { get; init; } = string.Empty;
    public int SchemaVersion { get; init; }
    public QuantitySurveyConfigurationDecisionStatus Status { get; init; }
    public QuantitySurveyConfigurationApprovalStatus ApprovalStatus { get; init; }
    public QuantitySurveyConfigurationEvidenceStatus EvidenceStatus { get; init; }
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
    public IReadOnlyList<QuantitySurveyEvidenceDto> Evidence { get; init; } = Array.Empty<QuantitySurveyEvidenceDto>();
}

public sealed class QuantitySurveyEvidenceDto
{
    public Guid Id { get; init; }
    public Guid DecisionId { get; init; }
    public string EvidenceType { get; init; } = string.Empty;
    public Guid? CentralDocumentRecordId { get; init; }
    public Guid? CentralDocumentVersionId { get; init; }
    public string? DocumentReference { get; init; }
    public string? DocumentTitle { get; init; }
    public string? VersionNumber { get; init; }
    public string? ExternalReference { get; init; }
    public string? Checksum { get; init; }
    public DateTime LinkedAt { get; init; }
    public Guid LinkedById { get; init; }
}

public sealed class QuantitySurveyRevisionDto
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

public sealed class QuantitySurveyFieldSchemaDto
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
    public string? HelpText { get; init; }
}

public sealed class QuantitySurveyDecisionSchemaDto
{
    public string DecisionKey { get; init; } = string.Empty;
    public string ConfigurationKey { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string OwnerGroup { get; init; } = string.Empty;
    public int SchemaVersion { get; init; }
    public IReadOnlyList<QuantitySurveyFieldSchemaDto> Fields { get; init; } = Array.Empty<QuantitySurveyFieldSchemaDto>();
}

public sealed class QuantitySurveyLookupOptionDto
{
    public string Value { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string? Group { get; init; }
}

public sealed class QuantitySurveyLookupsDto
{
    public IReadOnlyDictionary<string, IReadOnlyList<QuantitySurveyLookupOptionDto>> Sources { get; init; } =
        new Dictionary<string, IReadOnlyList<QuantitySurveyLookupOptionDto>>();
}

public sealed class QuantitySurveyValidationIssueDto
{
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string? DecisionKey { get; init; }
    public string Severity { get; init; } = "Error";
}

public sealed class QuantitySurveyValidationResultDto
{
    public bool IsValid => Errors.Count == 0;
    public IReadOnlyList<QuantitySurveyValidationIssueDto> Errors { get; init; } = Array.Empty<QuantitySurveyValidationIssueDto>();
    public IReadOnlyList<QuantitySurveyValidationIssueDto> Warnings { get; init; } = Array.Empty<QuantitySurveyValidationIssueDto>();
}

public class CreateQuantitySurveyProfileRequest
{
    [Required, StringLength(200)] public string Name { get; set; } = string.Empty;
    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow.Date;
    public DateTime? EffectiveTo { get; set; }
    [StringLength(1000)] public string? ChangeSummary { get; set; }
    public bool IsDefault { get; set; }
}

public sealed class UpdateQuantitySurveyProfileRequest : CreateQuantitySurveyProfileRequest
{
    [Required] public string RowVersion { get; set; } = string.Empty;
    [StringLength(1000)] public string? Reason { get; set; }
}

public sealed class SaveQuantitySurveyDecisionRequest
{
    public int SchemaVersion { get; set; } = 1;
    public JsonElement Value { get; set; }
    [StringLength(1000)] public string? SourceLineage { get; set; }
    [StringLength(2000)] public string? Notes { get; set; }
    [Required] public string RowVersion { get; set; } = string.Empty;
    [StringLength(1000)] public string? Reason { get; set; }
}

public class SubmitQuantitySurveyDecisionRequest
{
    [Required] public string RowVersion { get; set; } = string.Empty;
    [StringLength(1000)] public string? Reason { get; set; }
}

public sealed class DecideQuantitySurveyDecisionRequest : SubmitQuantitySurveyDecisionRequest
{
    [Required, StringLength(500)] public string ApprovalReference { get; set; } = string.Empty;
}

public sealed class QuantitySurveyLifecycleRequest
{
    [Required] public string RowVersion { get; set; } = string.Empty;
    [Required, StringLength(1000, MinimumLength = 3)] public string Reason { get; set; } = string.Empty;
}

public sealed class CloneQuantitySurveyProfileRequest
{
    public DateTime? EffectiveFrom { get; set; }
    [StringLength(1000)] public string? ChangeSummary { get; set; }
}

public sealed class LinkQuantitySurveyEvidenceRequest
{
    [Required, StringLength(100)] public string EvidenceType { get; set; } = string.Empty;
    public Guid? CentralDocumentRecordId { get; set; }
    public Guid? CentralDocumentVersionId { get; set; }
    [Url, StringLength(1000)] public string? ExternalReference { get; set; }
    [StringLength(128)] public string? Checksum { get; set; }
    [Required] public string DecisionRowVersion { get; set; } = string.Empty;
    [StringLength(1000)] public string? Reason { get; set; }
}

public abstract class QuantitySurveyEffectiveDecisionValue : IValidatableObject
{
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public virtual IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (EffectiveFrom == default) yield return new("Effective from is required.", new[] { nameof(EffectiveFrom) });
        if (EffectiveTo.HasValue && EffectiveTo < EffectiveFrom) yield return new("Effective to cannot be before effective from.", new[] { nameof(EffectiveTo) });
    }
}

public sealed class QsRolesAuthorityValue : QuantitySurveyEffectiveDecisionValue
{
    [MinLength(1)] public List<Guid> OfficerRoleIds { get; set; } = new();
    [MinLength(1)] public List<Guid> ApproverRoleIds { get; set; } = new();
    [MinLength(1)] public List<Guid> OversightRoleIds { get; set; } = new();
    [Required, RegularExpression("^[A-Z]{3}$")] public string CurrencyCode { get; set; } = "GHS";
    [Range(0, double.MaxValue)] public decimal OperationalAuthorityLimit { get; set; }
    [Range(0, double.MaxValue)] public decimal SeniorAuthorityLimit { get; set; }
    [Range(0, double.MaxValue)] public decimal ExecutiveAuthorityLimit { get; set; }
    public bool EnforceProjectScope { get; set; } = true;
    public bool EnforceContractScope { get; set; } = true;
    public bool EnforceSectionScope { get; set; } = true;
    public override IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        foreach (var item in base.Validate(context)) yield return item;
        if (OperationalAuthorityLimit > SeniorAuthorityLimit || SeniorAuthorityLimit > ExecutiveAuthorityLimit)
            yield return new("Authority limits must increase from operational to senior to executive.");
    }
}

public sealed class QsBoqStandardsValue : QuantitySurveyEffectiveDecisionValue
{
    [MinLength(1)] public List<QuantitySurveyBoqStandard> AllowedStandards { get; set; } = new();
    public QuantitySurveyBoqStandard DefaultStandard { get; set; }
    [MinLength(1)] public List<Guid> ProjectTypeIds { get; set; } = new();
    public bool RequireCostCode { get; set; } = true;
    public bool RequireTrade { get; set; } = true;
    public bool RequireWorkPackage { get; set; } = true;
}

public sealed class QsBoqVersionPolicyValue : QuantitySurveyEffectiveDecisionValue
{
    [MinLength(1)] public List<QuantitySurveyBoqVersionType> RequiredVersionTypes { get; set; } = new();
    public Guid BoqWorkflowDefinitionId { get; set; }
    public Guid EstimateWorkflowDefinitionId { get; set; }
    public bool ApprovedVersionsImmutable { get; set; } = true;
    public bool RequireWorkflowBeforeUse { get; set; } = true;
    public bool RequireLineLevelComparison { get; set; } = true;
}

public sealed class QsRateBuildUpValue : QuantitySurveyEffectiveDecisionValue
{
    [MinLength(1)] public List<QuantitySurveyRateComponent> Components { get; set; } = new();
    [Range(0, 100)] public decimal MaximumOverheadPercent { get; set; }
    [Range(0, 100)] public decimal MaximumProfitPercent { get; set; }
    [Range(0, 100)] public decimal MaximumContingencyPercent { get; set; }
    [Range(0, 100)] public decimal MaximumWastagePercent { get; set; }
    [Range(0, 6)] public int DecimalPlaces { get; set; } = 2;
}

public sealed class QsRateLibraryValue : QuantitySurveyEffectiveDecisionValue
{
    [MinLength(1)] public List<QuantitySurveyRateDimension> Dimensions { get; set; } = new();
    [Range(1, 36)] public int UpdateCadenceMonths { get; set; } = 3;
    [MinLength(1)] public List<Guid> ProjectTypeIds { get; set; } = new();
    [MinLength(1)] public List<Guid> LocationIds { get; set; } = new();
    public bool RequireMarketEvidence { get; set; } = true;
}

public sealed class QsEscalationValue : QuantitySurveyEffectiveDecisionValue
{
    [MinLength(1)] public List<QuantitySurveyIndexSource> IndexSources { get; set; } = new();
    public QuantitySurveyEscalationFormula Formula { get; set; }
    [Range(0, 100)] public decimal MaterialCoefficient { get; set; }
    [Range(0, 100)] public decimal LabourCoefficient { get; set; }
    [Range(0, 100)] public decimal PlantCoefficient { get; set; }
    [Range(0, 100)] public decimal OtherCoefficient { get; set; }
    public Guid ApprovalWorkflowDefinitionId { get; set; }
    [Required] public string ImportFormat { get; set; } = "Controlled Excel";
    public override IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        foreach (var item in base.Validate(context)) yield return item;
        if (MaterialCoefficient + LabourCoefficient + PlantCoefficient + OtherCoefficient != 100)
            yield return new("Escalation coefficients must total 100 percent.");
    }
}

public sealed class QsMeasurementValue : QuantitySurveyEffectiveDecisionValue
{
    public Guid WorkflowDefinitionId { get; set; }
    public Guid MetadataTemplateId { get; set; }
    [MinLength(1)] public List<Guid> JointAttendanceRoleIds { get; set; } = new();
    [MinLength(1)] public List<Guid> ConsultantRoleIds { get; set; } = new();
    public bool RequireContractorSignature { get; set; } = true;
    public bool RequireConsultantSignature { get; set; } = true;
}

public sealed class QsValuationCertificateValue : QuantitySurveyEffectiveDecisionValue
{
    public Guid ValuationWorkflowDefinitionId { get; set; }
    public Guid CertificateWorkflowDefinitionId { get; set; }
    public Guid ValuationTemplateId { get; set; }
    public Guid CertificateTemplateId { get; set; }
    public Guid ValuationEvidenceMetadataTemplateId { get; set; }
    public Guid CertificateMetadataTemplateId { get; set; }
    public Guid CertificateExpenseAccountId { get; set; }
    public Guid CertificateAccountsPayableAccountId { get; set; }
    public Guid CertificatePaymentTermId { get; set; }
    public Guid CertificateTaxGroupId { get; set; }
    public Guid? CertificateWithholdingTaxId { get; set; }
    public QuantitySurveyTaxHandling TaxHandling { get; set; }
    public bool RequireContractorSubmission { get; set; } = true;
    public bool RequireConsultantEndorsement { get; set; } = true;
    public bool RequireSupportingEvidence { get; set; } = true;
    public bool RequirePreviousCertificate { get; set; } = true;
    public bool ApplyAdvanceRecovery { get; set; } = true;
    public bool ApplyRetention { get; set; } = true;
}

public sealed class QsRetentionValue : QuantitySurveyEffectiveDecisionValue
{
    [Range(0, 100)] public decimal MaximumRetentionPercent { get; set; }
    [Range(0, 100)] public decimal PracticalCompletionReleasePercent { get; set; }
    [Range(0, 100)] public decimal SectionalTakeoverReleasePercent { get; set; }
    [Range(0, 100)] public decimal DefectsReleasePercent { get; set; }
    [Range(0, 3650)] public int DefectsLiabilityDays { get; set; }
    public Guid ApprovalWorkflowDefinitionId { get; set; }
    public bool AllowRetentionBond { get; set; }
}

public sealed class QsMaterialDeductionValue : QuantitySurveyEffectiveDecisionValue
{
    public bool AllowMaterialsOnSite { get; set; } = true;
    public bool AllowOffSiteMaterials { get; set; }
    public bool DeductTdcSuppliedMaterials { get; set; } = true;
    public QuantitySurveyMaterialValuationBasis ValuationBasis { get; set; }
    public bool RequireInventoryReconciliation { get; set; } = true;
    public Guid ApprovalWorkflowDefinitionId { get; set; }
}

public sealed class QsVariationClaimsValue : QuantitySurveyEffectiveDecisionValue
{
    public Guid VariationWorkflowDefinitionId { get; set; }
    public Guid ClaimWorkflowDefinitionId { get; set; }
    public Guid VariationEvidenceMetadataTemplateId { get; set; }
    [MinLength(1)] public List<string> AllowedTypes { get; set; } = new();
    public bool UpdateContractSum { get; set; } = true;
    public bool UpdateBudget { get; set; } = true;
    public bool UpdateForecast { get; set; } = true;
    public bool UpdateCertificate { get; set; } = true;
}

public sealed class QsContractControlsValue : QuantitySurveyEffectiveDecisionValue
{
    public bool ControlProvisionalSums { get; set; } = true;
    public bool ControlContingencies { get; set; } = true;
    public bool ControlDefectsLiability { get; set; } = true;
    public bool ControlSectionalTakeover { get; set; } = true;
    public bool ControlSubcontracts { get; set; } = true;
    public bool ControlClaimClauses { get; set; } = true;
    public bool ControlBackCharges { get; set; } = true;
    public bool ControlContraCharges { get; set; } = true;
    public bool RequireCommercialTermsDocument { get; set; } = true;
    public Guid SubcontractWorkflowDefinitionId { get; set; }
    public Guid FinalAccountWorkflowDefinitionId { get; set; }
}

public sealed class QsExternalSubmissionValue : QuantitySurveyEffectiveDecisionValue
{
    [MinLength(1)] public List<QuantitySurveyExternalSubmissionChannel> Channels { get; set; } = new();
    [MinLength(1)] public List<string> AllowedFileExtensions { get; set; } = new();
    [Range(1, 500)] public int MaximumFileSizeMb { get; set; } = 50;
    public bool RequirePortalIdentity { get; set; } = true;
    public bool RequireEvidence { get; set; } = true;
    public bool RequireSignature { get; set; } = true;
}

public sealed class QsThirdPartyToolsValue : QuantitySurveyEffectiveDecisionValue
{
    public List<QuantitySurveyThirdPartyTool> AllowedTools { get; set; } = new();
    [MinLength(1)] public List<QuantitySurveyExternalSubmissionChannel> ExchangeModes { get; set; } = new();
    public bool RequireStagingAndReconciliation { get; set; } = true;
    public List<Guid> MetadataTemplateIds { get; set; } = new();
}

public sealed class QsReportsValue : QuantitySurveyEffectiveDecisionValue
{
    [MinLength(1)] public List<Guid> ReportIds { get; set; } = new();
    [MinLength(1)] public List<Guid> ViewerRoleIds { get; set; } = new();
    [MinLength(1)] public List<string> ExportFormats { get; set; } = new();
    public bool EnforceScopedDrilldown { get; set; } = true;
    public bool AllowScheduledDistribution { get; set; }
}

public sealed class QsInterfacesValue : QuantitySurveyEffectiveDecisionValue
{
    [MinLength(1)] public List<QuantitySurveyIntegrationModule> Modules { get; set; } = new();
    public QuantitySurveyPostingMode PostingMode { get; set; }
    public bool RequireIdempotencyKey { get; set; } = true;
    public bool RequireReconciliation { get; set; } = true;
    public bool ProhibitDuplicatePosting { get; set; } = true;
}

public sealed class QsMigrationValue : QuantitySurveyEffectiveDecisionValue
{
    [MinLength(1)] public List<QuantitySurveyMigrationSource> SourceTypes { get; set; } = new();
    [MinLength(1)] public List<Guid> OwnerRoleIds { get; set; } = new();
    [MinLength(1)] public List<Guid> ReviewerRoleIds { get; set; } = new();
    [MinLength(1)] public List<Guid> SignOffRoleIds { get; set; } = new();
    public bool RequireStaging { get; set; } = true;
    public bool RequireReconciliation { get; set; } = true;
    public bool RequireSignedAcceptance { get; set; } = true;
}
