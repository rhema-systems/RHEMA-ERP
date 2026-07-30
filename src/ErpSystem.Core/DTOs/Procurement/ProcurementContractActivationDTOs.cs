using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Procurement;

public sealed class ProcurementContractActivationEvidenceRequest
{
    [Required, StringLength(200)] public string RequirementKey { get; set; } = string.Empty;
    public ProcurementContractActivationEvidenceKind ReferenceKind { get; set; }
    public Guid? WorkflowEvidenceDocumentId { get; set; }
    public Guid? FileUploadRecordId { get; set; }
    [Required, StringLength(1000)] public string EvidenceReference { get; set; } = string.Empty;
}

public sealed class SubmitProcurementContractActivationRequest
{
    [Required, StringLength(1000)] public string Reason { get; set; } = string.Empty;
    [Required, StringLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
    [Required] public string ContractRowVersion { get; set; } = string.Empty;
    [MinLength(1)] public List<ProcurementContractActivationEvidenceRequest> Evidence { get; set; } = new();
}

public sealed class DecideProcurementContractActivationRequest
{
    public bool Approved { get; set; }
    [Required, StringLength(1000)] public string Comment { get; set; } = string.Empty;
    [Required] public string RowVersion { get; set; } = string.Empty;
}

public sealed class ActivateProcurementContractRequest
{
    [Required, StringLength(200)] public string ContractorSignatoryName { get; set; } = string.Empty;
    public DateTime? ContractorSignedAtUtc { get; set; }
    [Required, StringLength(1000)] public string Comment { get; set; } = string.Empty;
    [Required] public string RowVersion { get; set; } = string.Empty;
}

public sealed class ProcurementContractActivationCheckDto
{
    public string Key { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public ProcurementContractActivationCheckStatus Status { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public bool IsRequired { get; init; } = true;
    public Guid? ReferenceId { get; init; }
    public string? Reference { get; init; }
}

public sealed class ProcurementContractActivationEvidenceDto
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

public sealed class ProcurementContractActivationDto
{
    public Guid Id { get; init; }
    public Guid ContractId { get; init; }
    public int Sequence { get; init; }
    public ProcurementContractActivationStatus Status { get; init; }
    public Guid ConfigurationProfileId { get; init; }
    public int ConfigurationProfileVersion { get; init; }
    public Guid PolicySetId { get; init; }
    public int PolicyVersion { get; init; }
    public Guid AuthorityRuleId { get; init; }
    public string AuthorityName { get; init; } = string.Empty;
    public Guid WorkflowDefinitionId { get; init; }
    public Guid? WorkflowInstanceId { get; init; }
    public Guid AwardReadinessDecisionId { get; init; }
    public int AwardReadinessSequence { get; init; }
    public bool GhanepsRequired { get; init; }
    public bool GhanepsCompliant { get; init; }
    public bool PerformanceSecurityRequired { get; init; }
    public Guid? PerformanceBondRequestId { get; init; }
    public Guid SubmittedById { get; init; }
    public string SubmittedByName { get; init; } = string.Empty;
    public DateTime SubmittedAtUtc { get; init; }
    public Guid? DecidedById { get; init; }
    public string? DecidedByName { get; init; }
    public DateTime? DecidedAtUtc { get; init; }
    public Guid? ActivatedById { get; init; }
    public string? ActivatedByName { get; init; }
    public DateTime? ActivatedAtUtc { get; init; }
    public string Reason { get; init; } = string.Empty;
    public string? DecisionComment { get; init; }
    public string IntegrityHash { get; init; } = string.Empty;
    public string RowVersion { get; init; } = string.Empty;
    public IReadOnlyList<ProcurementContractActivationEvidenceDto> Evidence { get; init; } =
        Array.Empty<ProcurementContractActivationEvidenceDto>();
}

public sealed class ProcurementContractActivationOverviewDto
{
    public Guid ContractId { get; init; }
    public string ContractNumber { get; init; } = string.Empty;
    public string ContractStatus { get; init; } = string.Empty;
    public bool IsReady { get; init; }
    public bool CanSubmit { get; init; }
    public bool CanDecide { get; init; }
    public bool CanActivate { get; init; }
    public IReadOnlyList<string> RequiredEvidenceKeys { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> DecisionKeys { get; init; } = Array.Empty<string>();
    public IReadOnlyList<ProcurementContractActivationCheckDto> Checks { get; init; } =
        Array.Empty<ProcurementContractActivationCheckDto>();
    public IReadOnlyList<ProcurementContractActivationDto> History { get; init; } =
        Array.Empty<ProcurementContractActivationDto>();
}
