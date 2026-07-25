using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Procurement;

public sealed class ProcurementMasterDataResourceDefinitionDto
{
    public ProcurementMasterDataResourceType ResourceType { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> AllowedFields { get; set; } = new();
    public bool AllowsTenantSingletonTarget { get; set; }
    public string SourceRequirements { get; set; } = string.Empty;
}

public sealed class ProcurementMasterDataPolicyDto
{
    public Guid Id { get; set; }
    public Guid PolicyKey { get; set; }
    public ProcurementMasterDataResourceType ResourceType { get; set; }
    public int Version { get; set; }
    public ProcurementMasterDataPolicyStatus Status { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<string> MakerRoles { get; set; } = new();
    public List<string> CheckerRoles { get; set; } = new();
    public bool RequireIndependentApproval { get; set; }
    public bool RequireRevalidation { get; set; }
    public bool RequireEvidence { get; set; }
    public Guid? WorkflowDefinitionId { get; set; }
    public string? WorkflowDefinitionName { get; set; }
    public int? WorkflowDefinitionVersion { get; set; }
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime? EffectiveToUtc { get; set; }
    public Guid? SupersedesPolicyId { get; set; }
    public DateTime? ActivatedAtUtc { get; set; }
    public DateTime? RetiredAtUtc { get; set; }
    public bool IsEffective { get; set; }
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class SaveProcurementMasterDataPolicyRequest
{
    public ProcurementMasterDataResourceType ResourceType { get; set; }
    [Required, StringLength(200)] public string Name { get; set; } = string.Empty;
    [StringLength(1000)] public string? Description { get; set; }
    public List<string> MakerRoles { get; set; } = new();
    public List<string> CheckerRoles { get; set; } = new();
    public bool RequireIndependentApproval { get; set; } = true;
    public bool RequireRevalidation { get; set; } = true;
    public bool RequireEvidence { get; set; }
    public Guid? WorkflowDefinitionId { get; set; }
    public DateTime EffectiveFromUtc { get; set; } = DateTime.UtcNow;
    public DateTime? EffectiveToUtc { get; set; }
    public string? RowVersion { get; set; }
}

public sealed class ProcurementMasterDataPolicyLifecycleRequest
{
    [Required, StringLength(1000)] public string Reason { get; set; } = string.Empty;
    [Required] public string RowVersion { get; set; } = string.Empty;
}

public sealed class ProcurementMasterDataChangeSearchRequest
{
    public ProcurementMasterDataResourceType? ResourceType { get; set; }
    public ProcurementMasterDataChangeStatus? Status { get; set; }
    public Guid? TargetId { get; set; }
    public Guid? MakerUserId { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public sealed class ProcurementMasterDataChangePageDto
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public List<ProcurementMasterDataChangeDto> Items { get; set; } = new();
}

public sealed class ProcurementMasterDataChangeSummaryDto
{
    public int PolicyCount { get; set; }
    public int EffectivePolicyCount { get; set; }
    public int DraftCount { get; set; }
    public int PendingApprovalCount { get; set; }
    public int ApprovedAwaitingEffectiveDateCount { get; set; }
    public int RevalidationFailedCount { get; set; }
    public int AppliedCount { get; set; }
}

public sealed class ProcurementMasterDataChangeDto
{
    public Guid Id { get; set; }
    public string RequestNumber { get; set; } = string.Empty;
    public Guid PolicyId { get; set; }
    public int PolicyVersion { get; set; }
    public ProcurementMasterDataResourceType ResourceType { get; set; }
    public ProcurementMasterDataTargetKind TargetKind { get; set; }
    public Guid TargetId { get; set; }
    public string TargetReference { get; set; } = string.Empty;
    public ProcurementMasterDataChangeStatus Status { get; set; }
    public string BeforeJson { get; set; } = "{}";
    public string BeforeHash { get; set; } = string.Empty;
    public string ProposedChangesJson { get; set; } = "{}";
    public string ProposedChangesHash { get; set; } = string.Empty;
    public string? AppliedAfterJson { get; set; }
    public string? AppliedAfterHash { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime EffectiveAtUtc { get; set; }
    public Guid MakerUserId { get; set; }
    public Guid? SubmittedById { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
    public Guid? CheckerUserId { get; set; }
    public DateTime? CheckedAtUtc { get; set; }
    public string? CheckerComment { get; set; }
    public bool? RevalidationPassed { get; set; }
    public DateTime? RevalidatedAtUtc { get; set; }
    public string? RevalidationMessage { get; set; }
    public string? RevalidatedSnapshotHash { get; set; }
    public Guid? AppliedById { get; set; }
    public DateTime? AppliedAtUtc { get; set; }
    public Guid? WorkflowDefinitionId { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string RowVersion { get; set; } = string.Empty;
    public List<ProcurementMasterDataChangeEvidenceDto> Evidence { get; set; } = new();
}

public sealed class SaveProcurementMasterDataChangeRequest
{
    public ProcurementMasterDataResourceType ResourceType { get; set; }
    public Guid TargetId { get; set; }
    [Required] public string ProposedChangesJson { get; set; } = "{}";
    [Required, StringLength(2000)] public string Reason { get; set; } = string.Empty;
    public DateTime EffectiveAtUtc { get; set; } = DateTime.UtcNow;
    public List<ProcurementControlEventEvidenceReference> Evidence { get; set; } = new();
    public string? RowVersion { get; set; }
}

public sealed class ProcurementMasterDataChangeDecisionRequest
{
    [Required, StringLength(2000)] public string Comment { get; set; } = string.Empty;
    [Required] public string RowVersion { get; set; } = string.Empty;
}

public sealed class ProcurementMasterDataChangeLifecycleRequest
{
    [StringLength(2000)] public string? Comment { get; set; }
    [Required] public string RowVersion { get; set; } = string.Empty;
}

public sealed class ProcurementMasterDataChangeEvidenceDto
{
    public Guid Id { get; set; }
    public ProcurementControlEvidenceReferenceKind ReferenceKind { get; set; }
    public Guid? ReferenceId { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string? Label { get; set; }
    public string? RequirementKey { get; set; }
    public string? FileName { get; set; }
    public string? Sha256 { get; set; }
    public string? VerificationStatus { get; set; }
    public bool ReferenceAvailable { get; set; }
}

public sealed class ProcurementMasterDataDirectMutationDecisionDto
{
    public bool Allowed { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Guid? PolicyId { get; set; }
    public ProcurementMasterDataResourceType? ResourceType { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
}
