using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Entities.Inventory;

namespace ErpSystem.Core.DTOs.Inventory;

public sealed class InventoryDisposalDto
{
    public Guid Id { get; set; }
    public string DisposalNumber { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public string WarehouseCode { get; set; } = string.Empty;
    public string WarehouseName { get; set; } = string.Empty;
    public InventoryDisposalStatus Status { get; set; }
    public InventoryDisposalMethod Method { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string IdentificationDetails { get; set; } = string.Empty;
    public Guid RequestedById { get; set; }
    public string RequestedByName { get; set; } = string.Empty;
    public DateTime RequestedAtUtc { get; set; }
    public Guid? AuditVerifiedById { get; set; }
    public DateTime? AuditVerifiedAtUtc { get; set; }
    public string? AuditFindings { get; set; }
    public DateTime? CommitteeMeetingAtUtc { get; set; }
    public string? CommitteeReference { get; set; }
    public string AuthorityRoute { get; set; } = string.Empty;
    public Guid? WorkflowInstanceId { get; set; }
    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public Guid? StockAdjustmentId { get; set; }
    public decimal ProceedsAmount { get; set; }
    public string? BuyerOrRecipient { get; set; }
    public string? ExecutionReference { get; set; }
    public Guid? ProceedsPostingEventId { get; set; }
    public Guid? ProceedsJournalEntryId { get; set; }
    /// <summary>Durable C7/C8 checker handoff identity; no book or execution capability is exposed.</summary>
    public Guid? FinanceProducerApprovalId { get; set; }
    public bool FinanceProducerApprovalIsGroup { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public decimal TotalQuantity { get; set; }
    public decimal TotalValue { get; set; }
    public string RowVersion { get; set; } = string.Empty;
    public IReadOnlyList<InventoryDisposalLineDto> Lines { get; set; } = [];
    public IReadOnlyList<InventoryDisposalEvidenceDto> Evidence { get; set; } = [];
    public IReadOnlyList<InventoryDisposalCommitteeMemberDto> CommitteeMembers { get; set; } = [];
    public IReadOnlyList<InventoryDisposalActionDto> Actions { get; set; } = [];
}

public sealed class InventoryDisposalLineDto
{
    public Guid Id { get; set; }
    public Guid InventoryItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public Guid LocationId { get; set; }
    public string LocationCode { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalValue { get; set; }
    public string? LotNumber { get; set; }
    public string? BatchNumber { get; set; }
    public string? SerialNumber { get; set; }
    public string? ConditionNotes { get; set; }
}

public sealed class InventoryDisposalEvidenceDto
{
    public Guid Id { get; set; }
    public Guid CentralDocumentVersionId { get; set; }
    public Guid FileUploadRecordId { get; set; }
    public string Stage { get; set; } = string.Empty;
    public string EvidenceReference { get; set; } = string.Empty;
    public string DocumentReference { get; set; } = string.Empty;
    public string VersionNumber { get; set; } = string.Empty;
}

public sealed class InventoryDisposalCommitteeMemberDto
{
    public Guid MemberUserId { get; set; }
    public string MemberName { get; set; } = string.Empty;
    public bool? RecommendApproval { get; set; }
    public bool ConflictDeclared { get; set; }
    public DateTime? VotedAtUtc { get; set; }
    public string? Comment { get; set; }
}

public sealed class InventoryDisposalActionDto
{
    public int Sequence { get; set; }
    public InventoryDisposalActionType ActionType { get; set; }
    public Guid ActorUserId { get; set; }
    public string ActorName { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; }
    public string? Comment { get; set; }
}

public sealed class CreateInventoryDisposalRequest
{
    public Guid WarehouseId { get; set; }
    public InventoryDisposalMethod Method { get; set; }
    [Required, MaxLength(1000)] public string Reason { get; set; } = string.Empty;
    [Required, MaxLength(2000)] public string IdentificationDetails { get; set; } = string.Empty;
    [Required, MaxLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
    [MaxLength(100)] public string? CorrelationId { get; set; }
    [MinLength(1)] public List<CreateInventoryDisposalLineRequest> Lines { get; set; } = [];
    [MinLength(1)] public List<InventoryControlEvidenceRequest> Evidence { get; set; } = [];
}

public sealed class CreateInventoryDisposalLineRequest
{
    public Guid InventoryItemId { get; set; }
    public Guid LocationId { get; set; }
    [Range(typeof(decimal), "0.0001", "999999999999")] public decimal Quantity { get; set; }
    [MaxLength(100)] public string? LotNumber { get; set; }
    [MaxLength(100)] public string? BatchNumber { get; set; }
    [MaxLength(100)] public string? SerialNumber { get; set; }
    [MaxLength(1000)] public string? ConditionNotes { get; set; }
}

public abstract class InventoryDisposalMutationRequest
{
    [Required] public string RowVersion { get; set; } = string.Empty;
    [Required, MaxLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
    [MaxLength(100)] public string? CorrelationId { get; set; }
    [MaxLength(1000)] public string? Comment { get; set; }
}

public sealed class VerifyInventoryDisposalRequest : InventoryDisposalMutationRequest
{
    public bool Verified { get; set; }
    [Required, MaxLength(2000)] public string Findings { get; set; } = string.Empty;
    public List<InventoryControlEvidenceRequest> Evidence { get; set; } = [];
}

public sealed class ScheduleInventoryDisposalCommitteeRequest : InventoryDisposalMutationRequest
{
    public DateTime MeetingAtUtc { get; set; }
    [Required, MaxLength(100)] public string CommitteeReference { get; set; } = string.Empty;
    [MinLength(3)] public List<Guid> MemberUserIds { get; set; } = [];
}

public sealed class VoteInventoryDisposalRequest : InventoryDisposalMutationRequest
{
    public bool RecommendApproval { get; set; }
    public bool ConflictDeclared { get; set; }
}

public sealed class SubmitInventoryDisposalRequest : InventoryDisposalMutationRequest;

public sealed class DecideInventoryDisposalRequest : InventoryDisposalMutationRequest
{
    public bool Approved { get; set; }
}

public sealed class StageInventoryDisposalExecutionRequest : InventoryDisposalMutationRequest
{
    [Range(typeof(decimal), "0", "999999999999")] public decimal ProceedsAmount { get; set; }
    public Guid? ProceedsAccountId { get; set; }
    [MaxLength(200)] public string? BuyerOrRecipient { get; set; }
    [Required, MaxLength(200)] public string ExecutionReference { get; set; } = string.Empty;
    [MinLength(1)] public List<InventoryControlEvidenceRequest> Evidence { get; set; } = [];
}

public sealed class CompleteInventoryDisposalRequest : InventoryDisposalMutationRequest
{
    public IReadOnlyDictionary<Guid, Guid> NegativeStockOverrideIds { get; set; } = new Dictionary<Guid, Guid>();
}
