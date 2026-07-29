using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Procurement;

public sealed class ProcurementSupplierAvlSearchRequest
{
    public string? Search { get; set; }
    public ProcurementSupplierAvlRegisterStatus? Status { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

public sealed class ProcurementSupplierAvlSummaryDto
{
    public int TotalRegisters { get; set; }
    public int DraftCount { get; set; }
    public int PendingApprovalCount { get; set; }
    public int PublishedCount { get; set; }
    public int CurrentEntryCount { get; set; }
    public int SuspendedEntryCount { get; set; }
    public bool PolicyAvailable { get; set; }
    public string? PolicyProfileCode { get; set; }
    public int? PolicyProfileVersion { get; set; }
    public int? ReviewFrequencyMonths { get; set; }
    public string? PolicyReleaseGate { get; set; }
}

public sealed class ProcurementSupplierAvlPageDto
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public List<ProcurementSupplierAvlListItemDto> Items { get; set; } = new();
}

public class ProcurementSupplierAvlListItemDto
{
    public Guid Id { get; set; }
    public string RegisterCode { get; set; } = string.Empty;
    public int Version { get; set; }
    public int ReviewYear { get; set; }
    public ProcurementSupplierAvlRegisterStatus Status { get; set; }
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? ScheduledRetirementAtUtc { get; set; }
    public int EntryCount { get; set; }
    public int ActiveEntryCount { get; set; }
    public int SuspendedEntryCount { get; set; }
    public string PolicyProfileCode { get; set; } = string.Empty;
    public int PolicyProfileVersion { get; set; }
    public IReadOnlyList<string> AllowedActions { get; set; } = Array.Empty<string>();
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class ProcurementSupplierAvlDto : ProcurementSupplierAvlListItemDto
{
    public Guid PolicyDecisionId { get; set; }
    public Guid PolicyProfileId { get; set; }
    public string PolicyValueHash { get; set; } = string.Empty;
    public string PolicySnapshotJson { get; set; } = "{}";
    public int ReviewFrequencyMonths { get; set; }
    public Guid WorkflowDefinitionId { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public string? Notes { get; set; }
    public string? ReviewComment { get; set; }
    public Guid? SubmittedById { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public Guid? PublishedById { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
    public string IntegrityHash { get; set; } = string.Empty;
    public List<ProcurementSupplierAvlEntryDto> Entries { get; set; } = new();
    public List<ProcurementSupplierAvlPublicationSnapshotDto> PublicationSnapshots { get; set; } = new();
}

public sealed class ProcurementSupplierAvlEntryDto
{
    public Guid Id { get; set; }
    public Guid BusinessPartnerId { get; set; }
    public string PartnerCode { get; set; } = string.Empty;
    public string PartnerName { get; set; } = string.Empty;
    public ProcurementSupplierAvlEntryStatus Status { get; set; }
    public Guid DueDiligenceReviewId { get; set; }
    public string? DueDiligenceReviewReference { get; set; }
    public Guid? RegistrationId { get; set; }
    public Guid? EvidencePackVersionId { get; set; }
    public Guid? QualifiedListEntryId { get; set; }
    public DateTime AddedAtUtc { get; set; }
    public DateTime? SuspendedAtUtc { get; set; }
    public string? SuspensionReason { get; set; }
    public DateTime? ReinstatedAtUtc { get; set; }
    public DateTime? ExpiredAtUtc { get; set; }
    public string EligibilityDecisionHash { get; set; } = string.Empty;
    public string IntegrityHash { get; set; } = string.Empty;
    public string RowVersion { get; set; } = string.Empty;
    public List<ProcurementSupplierAvlEntryStatusHistoryDto> StatusHistory { get; set; } = new();
}

public sealed class ProcurementSupplierAvlEntryStatusHistoryDto
{
    public Guid Id { get; set; }
    public ProcurementSupplierAvlEntryAction Action { get; set; }
    public ProcurementSupplierAvlEntryStatus BeforeStatus { get; set; }
    public ProcurementSupplierAvlEntryStatus AfterStatus { get; set; }
    public string Reason { get; set; } = string.Empty;
    public Guid ActorUserId { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string IntegrityHash { get; set; } = string.Empty;
}

public sealed class ProcurementSupplierAvlPublicationSnapshotDto
{
    public Guid Id { get; set; }
    public int Sequence { get; set; }
    public DateTime PublishedAtUtc { get; set; }
    public Guid PublishedById { get; set; }
    public string IntegrityHash { get; set; } = string.Empty;
}

public sealed class CreateProcurementSupplierAvlRequest
{
    [Range(2000, 9999)] public int ReviewYear { get; set; }
    public DateTime EffectiveFromUtc { get; set; }
    public Guid WorkflowDefinitionId { get; set; }
    [StringLength(1000)] public string? Notes { get; set; }
}

public sealed class UpdateProcurementSupplierAvlRequest
{
    [Required] public string RowVersion { get; set; } = string.Empty;
    public DateTime EffectiveFromUtc { get; set; }
    [StringLength(1000)] public string? Notes { get; set; }
}

public sealed class AddProcurementSupplierAvlEntryRequest
{
    public Guid BusinessPartnerId { get; set; }
    [Required] public string RegisterRowVersion { get; set; } = string.Empty;
}

public sealed class ProcurementSupplierAvlLifecycleRequest
{
    [Required] public string RowVersion { get; set; } = string.Empty;
    [Required, StringLength(1000)] public string Comment { get; set; } = string.Empty;
    [MinLength(1)] public List<ProcurementControlEventEvidenceReference> Evidence { get; set; } = new();
}

public sealed class ProcurementSupplierAvlEntryLifecycleRequest
{
    [Required] public string RowVersion { get; set; } = string.Empty;
    [Required, StringLength(1000)] public string Reason { get; set; } = string.Empty;
    [MinLength(1)] public List<ProcurementControlEventEvidenceReference> Evidence { get; set; } = new();
}

public sealed class ProcurementSupplierAvlWorkflowOptionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Version { get; set; }
}

public sealed class ProcurementSupplierAvlSupplierOptionDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public sealed class ProcurementSupplierAvlCurrentStateDto
{
    public Guid BusinessPartnerId { get; set; }
    public bool PolicyAvailable { get; set; }
    public bool RegisterAvailable { get; set; }
    public bool IsCurrent { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Guid? RegisterId { get; set; }
    public string? RegisterCode { get; set; }
    public int? RegisterVersion { get; set; }
    public Guid? EntryId { get; set; }
    public ProcurementSupplierAvlEntryStatus? EntryStatus { get; set; }
    public DateTime? EffectiveFromUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public string? IntegrityHash { get; set; }
}
