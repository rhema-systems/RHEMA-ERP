using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Procurement;

public sealed class ProcurementWorksCloseoutEvidenceRequest
{
    [Required, StringLength(200)] public string RequirementKey { get; set; } = string.Empty;
    public ProcurementContractActivationEvidenceKind ReferenceKind { get; set; }
    public Guid? WorkflowEvidenceDocumentId { get; set; }
    public Guid? FileUploadRecordId { get; set; }
    [Required, StringLength(1000)] public string EvidenceReference { get; set; } = string.Empty;
}

public sealed class SubmitProcurementWorksCloseoutActionRequest
{
    public ProcurementWorksCloseoutActionType ActionType { get; set; }
    public Guid? ProjectHandoverItemId { get; set; }
    public Guid? ProjectDefectLiabilityCaseId { get; set; }
    public Guid? ProjectFinalAccountId { get; set; }
    public Guid? ProjectPaymentCertificateId { get; set; }
    public Guid? PerformanceBondRequestId { get; set; }
    public ProcurementRetentionReleaseStage? RetentionReleaseStage { get; set; }
    public bool UsesRetentionBond { get; set; }
    public DateTime? EffectiveAtUtc { get; set; }
    [Range(typeof(decimal), "0", "9999999999999999")] public decimal? Amount { get; set; }
    [StringLength(3), RegularExpression("^[A-Za-z]{3}$")] public string? Currency { get; set; }
    [Required, StringLength(1000)] public string Reason { get; set; } = string.Empty;
    [Required, StringLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
    [Required] public string ContractRowVersion { get; set; } = string.Empty;
    [MinLength(1)] public List<ProcurementWorksCloseoutEvidenceRequest> Evidence { get; set; } = new();
}

public sealed class DecideProcurementWorksCloseoutActionRequest
{
    public bool Approved { get; set; }
    [Required, StringLength(1000)] public string Comment { get; set; } = string.Empty;
    [Required] public string RowVersion { get; set; } = string.Empty;
}

public sealed class ProcurementWorksCloseoutCheckDto
{
    public string Key { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public ProcurementWorksCloseoutCheckStatus Status { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public bool IsRequired { get; init; } = true;
    public Guid? ReferenceId { get; init; }
    public string? Reference { get; init; }
}

public sealed class ProcurementWorksCloseoutEvidenceDto
{
    public Guid Id { get; init; }
    public string RequirementKey { get; init; } = string.Empty;
    public string RequirementLabel { get; init; } = string.Empty;
    public ProcurementContractActivationEvidenceKind ReferenceKind { get; init; }
    public Guid? WorkflowEvidenceDocumentId { get; init; }
    public Guid? FileUploadRecordId { get; init; }
    public string EvidenceReference { get; init; } = string.Empty;
    public string EvidenceHash { get; init; } = string.Empty;
}

public sealed class ProcurementWorksCloseoutActionDto
{
    public Guid Id { get; init; }
    public Guid ContractId { get; init; }
    public Guid ProjectId { get; init; }
    public int Sequence { get; init; }
    public ProcurementWorksCloseoutActionType ActionType { get; init; }
    public ProcurementWorksCloseoutActionStatus Status { get; init; }
    public Guid ConfigurationProfileId { get; init; }
    public int ConfigurationProfileVersion { get; init; }
    public Guid PolicySetId { get; init; }
    public int PolicyVersion { get; init; }
    public Guid AuthorityRuleId { get; init; }
    public string AuthorityName { get; init; } = string.Empty;
    public Guid WorkflowDefinitionId { get; init; }
    public Guid? WorkflowInstanceId { get; init; }
    public Guid? ProjectHandoverItemId { get; init; }
    public Guid? ProjectDefectLiabilityCaseId { get; init; }
    public Guid? ProjectFinalAccountId { get; init; }
    public Guid? ProjectPaymentCertificateId { get; init; }
    public Guid? PerformanceBondRequestId { get; init; }
    public ProcurementRetentionReleaseStage? RetentionReleaseStage { get; init; }
    public Guid? QuantitySurveyConfigurationProfileId { get; init; }
    public int? QuantitySurveyConfigurationProfileVersion { get; init; }
    public Guid? QuantitySurveyRetentionDecisionId { get; init; }
    public string? QuantitySurveyRetentionPolicyHash { get; init; }
    public decimal? RetentionHeldSnapshot { get; init; }
    public decimal? RetentionReleasedBefore { get; init; }
    public decimal? RetentionStageLimitAmount { get; init; }
    public decimal? RetentionReleasedAfter { get; init; }
    public bool UsesRetentionBond { get; init; }
    public DateTime? EffectiveAtUtc { get; init; }
    public DateTime? DefectsLiabilityEndsAtUtc { get; init; }
    public decimal? Amount { get; init; }
    public string? Currency { get; init; }
    public bool RequiresIndependentFinanceApproval { get; init; }
    public bool AmountAutoPosted { get; init; }
    public Guid SubmittedById { get; init; }
    public string SubmittedByName { get; init; } = string.Empty;
    public DateTime SubmittedAtUtc { get; init; }
    public Guid? DecidedById { get; init; }
    public string? DecidedByName { get; init; }
    public DateTime? DecidedAtUtc { get; init; }
    public string Reason { get; init; } = string.Empty;
    public string? DecisionComment { get; init; }
    public string IntegrityHash { get; init; } = string.Empty;
    public string RowVersion { get; init; } = string.Empty;
    public IReadOnlyList<ProcurementWorksCloseoutEvidenceDto> Evidence { get; init; } =
        Array.Empty<ProcurementWorksCloseoutEvidenceDto>();
}

public sealed class ProcurementWorksCloseoutProjectSummaryDto
{
    public Guid ProjectId { get; init; }
    public string ProjectCode { get; init; } = string.Empty;
    public string ProjectTitle { get; init; } = string.Empty;
    public string ProjectStatus { get; init; } = string.Empty;
    public int CompletedPracticalTakeovers { get; init; }
    public int CompletedFinalTakeovers { get; init; }
    public int OpenDefects { get; init; }
    public int ClosedDefects { get; init; }
    public decimal RetentionHeld { get; init; }
    public decimal RetentionReleased { get; init; }
    public Guid? FinalAccountId { get; init; }
    public string? FinalAccountStatus { get; init; }
    public decimal? FinalAccountValue { get; init; }
    public string? Currency { get; init; }
    public string? ProjectClosureStatus { get; init; }
}

public sealed class ProcurementWorksHandoverSourceDto
{
    public Guid Id { get; init; }
    public Guid? ProjectUnitId { get; init; }
    public string HandoverType { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string? ReferenceNumber { get; init; }
    public DateTime? CompletedDate { get; init; }
}

public sealed class ProcurementWorksDefectSourceDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public bool IsWarrantyRelated { get; init; }
    public DateTime? WarrantyExpiryDate { get; init; }
    public decimal? RectificationCost { get; init; }
    public string Currency { get; init; } = string.Empty;
}

public sealed class ProcurementWorksCertificateSourceDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? CertificateNumber { get; init; }
    public string Status { get; init; } = string.Empty;
    public decimal RetentionHeldAmount { get; init; }
    public decimal RetentionReleasedAmount { get; init; }
    public string Currency { get; init; } = string.Empty;
}

public sealed class ProcurementWorksSecuritySourceDto
{
    public Guid Id { get; init; }
    public string Status { get; init; } = string.Empty;
}

public sealed class ProcurementWorksCloseoutOverviewDto
{
    public Guid ContractId { get; init; }
    public string ContractNumber { get; init; } = string.Empty;
    public string ContractStatus { get; init; } = string.Empty;
    public string ContractRowVersion { get; init; } = string.Empty;
    public bool IsWorksContract { get; init; }
    public bool HasActiveDispute { get; init; }
    public bool IsClosed { get; init; }
    public IReadOnlyList<string> DecisionKeys { get; init; } = Array.Empty<string>();
    public IReadOnlyDictionary<ProcurementWorksCloseoutActionType, IReadOnlyList<string>>
        RequiredEvidence { get; init; } =
            new Dictionary<ProcurementWorksCloseoutActionType, IReadOnlyList<string>>();
    public ProcurementWorksCloseoutProjectSummaryDto? Project { get; init; }
    public IReadOnlyList<ProcurementWorksHandoverSourceDto> HandoverItems { get; init; } =
        Array.Empty<ProcurementWorksHandoverSourceDto>();
    public IReadOnlyList<ProcurementWorksDefectSourceDto> Defects { get; init; } =
        Array.Empty<ProcurementWorksDefectSourceDto>();
    public IReadOnlyList<ProcurementWorksCertificateSourceDto> PaymentCertificates { get; init; } =
        Array.Empty<ProcurementWorksCertificateSourceDto>();
    public ProcurementWorksSecuritySourceDto? PerformanceSecurity { get; init; }
    public ProcurementRetentionPolicyDto? RetentionPolicy { get; init; }
    public IReadOnlyList<ProcurementRetentionLedgerEntryDto> RetentionLedger { get; init; } =
        Array.Empty<ProcurementRetentionLedgerEntryDto>();
    public IReadOnlyList<ProcurementWorksCloseoutCheckDto> Checks { get; init; } =
        Array.Empty<ProcurementWorksCloseoutCheckDto>();
    public IReadOnlyList<ProcurementWorksCloseoutActionDto> History { get; init; } =
        Array.Empty<ProcurementWorksCloseoutActionDto>();
}

public sealed class ProcurementRetentionPolicyDto
{
    public Guid ProfileId { get; init; }
    public int ProfileVersion { get; init; }
    public Guid DecisionId { get; init; }
    public decimal MaximumRetentionPercent { get; init; }
    public decimal PracticalCompletionReleasePercent { get; init; }
    public decimal SectionalTakeoverReleasePercent { get; init; }
    public decimal DefectsReleasePercent { get; init; }
    public int DefectsLiabilityDays { get; init; }
    public Guid ApprovalWorkflowDefinitionId { get; init; }
    public bool AllowRetentionBond { get; init; }
    public string PolicyHash { get; init; } = string.Empty;
}

public sealed class ProcurementRetentionLedgerEntryDto
{
    public string SourceType { get; init; } = string.Empty;
    public Guid SourceId { get; init; }
    public string SourceReference { get; init; } = string.Empty;
    public DateTime EffectiveAtUtc { get; init; }
    public ProcurementRetentionReleaseStage? ReleaseStage { get; init; }
    public decimal HeldAmount { get; init; }
    public decimal ReleasedAmount { get; init; }
    public decimal RunningHeldAmount { get; init; }
    public decimal RunningReleasedAmount { get; init; }
    public decimal OutstandingAmount { get; init; }
    public string Currency { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
}
