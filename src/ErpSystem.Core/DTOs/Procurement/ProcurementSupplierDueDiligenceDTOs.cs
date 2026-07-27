using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Procurement;

public sealed class ProcurementSupplierDueDiligenceSearchRequest
{
    public string? Search { get; set; }
    public Guid? BusinessPartnerId { get; set; }
    public ProcurementSupplierDueDiligenceStatus? Status { get; set; }
    public ProcurementSupplierDueDiligenceReviewType? ReviewType { get; set; }
    public bool? DueOnly { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

public sealed class ProcurementSupplierDueDiligenceSummaryDto
{
    public int TotalReviews { get; set; }
    public int DraftCount { get; set; }
    public int PendingApprovalCount { get; set; }
    public int CurrentApprovedCount { get; set; }
    public int DueOrExpiredCount { get; set; }
    public int AdverseCount { get; set; }
    public int SuppliersWithoutCurrentReview { get; set; }
    public bool PolicyAvailable { get; set; }
    public string? PolicyProfileCode { get; set; }
    public int? PolicyProfileVersion { get; set; }
    public int? ReviewFrequencyMonths { get; set; }
    public string? PolicyReleaseGate { get; set; }
}

public sealed class ProcurementSupplierDueDiligencePageDto
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public List<ProcurementSupplierDueDiligenceListItemDto> Items { get; set; } = new();
}

public class ProcurementSupplierDueDiligenceListItemDto
{
    public Guid Id { get; set; }
    public Guid BusinessPartnerId { get; set; }
    public string PartnerCode { get; set; } = string.Empty;
    public string PartnerName { get; set; } = string.Empty;
    public string ReviewReference { get; set; } = string.Empty;
    public int CycleNumber { get; set; }
    public ProcurementSupplierDueDiligenceReviewType ReviewType { get; set; }
    public ProcurementSupplierDueDiligenceStatus Status { get; set; }
    public ProcurementSupplierDueDiligenceOutcome Outcome { get; set; }
    public DateTime ReviewPeriodStartUtc { get; set; }
    public DateTime ReviewPeriodEndUtc { get; set; }
    public DateTime? NextReviewDueAtUtc { get; set; }
    public bool IsCurrent { get; set; }
    public bool IsDueOrExpired { get; set; }
    public string PolicyProfileCode { get; set; } = string.Empty;
    public int PolicyProfileVersion { get; set; }
    public int ReviewFrequencyMonths { get; set; }
    public int ClearCheckCount { get; set; }
    public int AdverseCheckCount { get; set; }
    public int EvidenceCount { get; set; }
    public IReadOnlyList<string> AllowedActions { get; set; } = Array.Empty<string>();
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class ProcurementSupplierDueDiligenceDto :
    ProcurementSupplierDueDiligenceListItemDto
{
    public Guid PolicyDecisionId { get; set; }
    public Guid PolicyProfileId { get; set; }
    public string PolicyValueHash { get; set; } = string.Empty;
    public string PolicySnapshotJson { get; set; } = "{}";
    public Guid WorkflowDefinitionId { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public Guid? SupersedesReviewId { get; set; }
    public Guid? SupersededByReviewId { get; set; }
    public string? Notes { get; set; }
    public string? ReviewComment { get; set; }
    public Guid? SubmittedById { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public Guid? RejectedById { get; set; }
    public DateTime? RejectedAtUtc { get; set; }
    public DateTime? ExpiredAtUtc { get; set; }
    public string IntegrityHash { get; set; } = string.Empty;
    public List<ProcurementSupplierDueDiligenceCheckDto> Checks { get; set; } = new();
}

public sealed class ProcurementSupplierDueDiligenceCheckDto
{
    public Guid Id { get; set; }
    public ProcurementSupplierDueDiligenceCheckType CheckType { get; set; }
    public ProcurementSupplierDueDiligenceCheckStatus Status { get; set; }
    public string SourceName { get; set; } = string.Empty;
    public string SourceReference { get; set; } = string.Empty;
    public DateTime? CheckedAtUtc { get; set; }
    public DateTime? ValidUntilUtc { get; set; }
    public Guid? ReviewedById { get; set; }
    public string? ReviewerName { get; set; }
    public string? Notes { get; set; }
    public string IntegrityHash { get; set; } = string.Empty;
    public List<ProcurementSupplierDueDiligenceEvidenceDto> Evidence { get; set; } = new();
}

public sealed class ProcurementSupplierDueDiligenceEvidenceDto
{
    public Guid Id { get; set; }
    public ProcurementControlEvidenceReferenceKind ReferenceKind { get; set; }
    public Guid? WorkflowEvidenceDocumentId { get; set; }
    public Guid? FileUploadRecordId { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string? Label { get; set; }
    public string RequirementKey { get; set; } = string.Empty;
    public string IntegrityHash { get; set; } = string.Empty;
}

public sealed class CreateProcurementSupplierDueDiligenceRequest
{
    public Guid BusinessPartnerId { get; set; }
    public ProcurementSupplierDueDiligenceReviewType ReviewType { get; set; }
    public Guid WorkflowDefinitionId { get; set; }
    [StringLength(1000)] public string? Notes { get; set; }
}

public sealed class UpdateProcurementSupplierDueDiligenceRequest
{
    [Required] public string RowVersion { get; set; } = string.Empty;
    [StringLength(1000)] public string? Notes { get; set; }
    [MinLength(6)] public List<SaveProcurementSupplierDueDiligenceCheckRequest> Checks { get; set; } = new();
}

public sealed class SaveProcurementSupplierDueDiligenceCheckRequest
{
    public ProcurementSupplierDueDiligenceCheckType CheckType { get; set; }
    public ProcurementSupplierDueDiligenceCheckStatus Status { get; set; }
    [Required, StringLength(200)] public string SourceName { get; set; } = string.Empty;
    [Required, StringLength(500)] public string SourceReference { get; set; } = string.Empty;
    public DateTime? CheckedAtUtc { get; set; }
    public DateTime? ValidUntilUtc { get; set; }
    [StringLength(1000)] public string? Notes { get; set; }
    public List<ProcurementControlEventEvidenceReference> Evidence { get; set; } = new();
}

public sealed class ProcurementSupplierDueDiligenceLifecycleRequest
{
    [Required] public string RowVersion { get; set; } = string.Empty;
    [Required, StringLength(1000)] public string Comment { get; set; } = string.Empty;
    [MinLength(1)] public List<ProcurementControlEventEvidenceReference> Evidence { get; set; } = new();
}

public sealed class ProcurementSupplierDueDiligenceWorkflowOptionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Version { get; set; }
}

public sealed class ProcurementSupplierDueDiligenceSupplierOptionDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool HasCurrentReview { get; set; }
    public DateTime? NextReviewDueAtUtc { get; set; }
}

public sealed class ProcurementSupplierDueDiligenceCurrentStateDto
{
    public Guid BusinessPartnerId { get; set; }
    public bool PolicyAvailable { get; set; }
    public bool HasApprovedReview { get; set; }
    public bool IsCurrent { get; set; }
    public bool IsClear { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Guid? ReviewId { get; set; }
    public string? ReviewReference { get; set; }
    public DateTime? ReviewPeriodEndUtc { get; set; }
    public DateTime? EarliestCheckExpiryUtc { get; set; }
    public string? IntegrityHash { get; set; }
    public Guid? PolicyDecisionId { get; set; }
    public string? PolicyValueHash { get; set; }
    public List<ProcurementSupplierDueDiligenceCheckDto> Checks { get; set; } = new();
}
