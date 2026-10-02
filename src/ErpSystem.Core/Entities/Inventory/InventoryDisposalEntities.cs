using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.DocumentManagement;

namespace ErpSystem.Core.Entities.Inventory;

public enum InventoryDisposalStatus
{
    Identified = 1,
    AuditVerified = 2,
    CommitteeScheduled = 3,
    CommitteeRecommended = 4,
    PendingApproval = 5,
    Approved = 6,
    AdjustmentPending = 7,
    Completed = 8,
    Rejected = 9,
    Cancelled = 10,
    ReadyForExecution = 11
}

public enum InventoryDisposalMethod
{
    Auction = 1,
    Sale = 2,
    WriteOff = 3,
    Donation = 4,
    Destruction = 5
}

public enum InventoryDisposalActionType
{
    Identified = 1,
    AuditVerified = 2,
    AuditRejected = 3,
    CommitteeScheduled = 4,
    CommitteeVoteRecorded = 5,
    CommitteeRecommended = 6,
    CommitteeRejected = 7,
    Submitted = 8,
    Approved = 9,
    Rejected = 10,
    AdjustmentStaged = 11,
    Completed = 12,
    Cancelled = 13,
    Edited = 14,
    ApprovalNotRequired = 15
}

public sealed class InventoryDisposalCase : TenantEntity
{
    [Required, MaxLength(50)] public string DisposalNumber { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public InventoryDisposalStatus Status { get; set; } = InventoryDisposalStatus.Identified;
    public bool ApprovalRequired { get; set; } = true;
    public InventoryDisposalMethod Method { get; set; }
    // Zero preserves the original grouped proceeds authority for historical cases.
    public int AccountingVersion { get; set; }
    [Required, MaxLength(1000)] public string Reason { get; set; } = string.Empty;
    [Required, MaxLength(2000)] public string IdentificationDetails { get; set; } = string.Empty;
    public Guid RequestedById { get; set; }
    public DateTime RequestedAtUtc { get; set; }
    public Guid? AuditVerifiedById { get; set; }
    public DateTime? AuditVerifiedAtUtc { get; set; }
    [MaxLength(2000)] public string? AuditFindings { get; set; }
    public DateTime? CommitteeMeetingAtUtc { get; set; }
    [MaxLength(100)] public string? CommitteeReference { get; set; }
    public Guid? CommitteeScheduledById { get; set; }
    public DateTime? CommitteeRecommendedAtUtc { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    [MaxLength(200)] public string AuthorityRoute { get; set; } = "Configured disposal workflow";
    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public Guid? RejectedById { get; set; }
    public DateTime? RejectedAtUtc { get; set; }
    [MaxLength(1000)] public string? RejectionReason { get; set; }
    public Guid? StockAdjustmentId { get; set; }
    public Guid? PreparedStockAdjustmentId { get; set; }
    public Guid? ProceedsAccountId { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal ProceedsAmount { get; set; }
    [MaxLength(200)] public string? BuyerOrRecipient { get; set; }
    [MaxLength(200)] public string? ExecutionReference { get; set; }
    public Guid? ProceedsPostingEventId { get; set; }
    public Guid? ProceedsJournalEntryId { get; set; }
    public Guid? CompletedById { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal TotalQuantity { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal TotalValue { get; set; }
    [Required, MaxLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
    [Required, MaxLength(64)] public string PayloadHash { get; set; } = string.Empty;
    [Required, MaxLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [Required, MaxLength(64)] public string IntegrityHash { get; set; } = string.Empty;
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public Warehouse Warehouse { get; set; } = null!;
    public ApplicationUser RequestedBy { get; set; } = null!;
    public StockAdjustment? StockAdjustment { get; set; }
    public ICollection<InventoryDisposalLine> Lines { get; set; } = new List<InventoryDisposalLine>();
    public ICollection<InventoryDisposalEvidence> Evidence { get; set; } = new List<InventoryDisposalEvidence>();
    public ICollection<InventoryDisposalCommitteeMember> CommitteeMembers { get; set; } = new List<InventoryDisposalCommitteeMember>();
    public ICollection<InventoryDisposalAction> Actions { get; set; } = new List<InventoryDisposalAction>();
}

public sealed class InventoryDisposalLine : TenantEntity, ErpSystem.Core.Interfaces.Inventory.ICommercialQuantityEvidenceLine
{
    public Guid InventoryDisposalCaseId { get; set; }
    public Guid InventoryItemId { get; set; }
    public Guid LocationId { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal Quantity { get; set; }
    public Guid? UnitOfMeasureId { get; set; }
    [MaxLength(20)] public string? UnitOfMeasureCodeSnapshot { get; set; }
    public int? UnitOfMeasureDecimalPlacesSnapshot { get; set; }
    [Column(TypeName = "decimal(18,6)")] public decimal? UnitOfMeasureRoundingIncrementSnapshot { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal UnitCost { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal TotalValue { get; set; }
    [MaxLength(100)] public string? LotNumber { get; set; }
    [MaxLength(100)] public string? BatchNumber { get; set; }
    [MaxLength(100)] public string? SerialNumber { get; set; }
    [MaxLength(1000)] public string? ConditionNotes { get; set; }
    [Required, MaxLength(64)] public string IntegrityHash { get; set; } = string.Empty;

    public InventoryDisposalCase InventoryDisposalCase { get; set; } = null!;
    public InventoryItem InventoryItem { get; set; } = null!;
    public WarehouseLocation Location { get; set; } = null!;
}

public sealed class InventoryDisposalEvidence : TenantEntity
{
    public Guid InventoryDisposalCaseId { get; set; }
    public Guid CentralDocumentVersionId { get; set; }
    public Guid FileUploadRecordId { get; set; }
    [Required, MaxLength(40)] public string Stage { get; set; } = string.Empty;
    [Required, MaxLength(500)] public string EvidenceReference { get; set; } = string.Empty;
    [Required, MaxLength(64)] public string IntegrityHash { get; set; } = string.Empty;

    public InventoryDisposalCase InventoryDisposalCase { get; set; } = null!;
    public CentralDocumentVersion CentralDocumentVersion { get; set; } = null!;
    public FileUploadRecord FileUploadRecord { get; set; } = null!;
}

public sealed class InventoryDisposalCommitteeMember : TenantEntity
{
    public Guid InventoryDisposalCaseId { get; set; }
    public Guid MemberUserId { get; set; }
    public bool? RecommendApproval { get; set; }
    public bool ConflictDeclared { get; set; }
    public DateTime? VotedAtUtc { get; set; }
    [MaxLength(1000)] public string? Comment { get; set; }
    [Required, MaxLength(64)] public string IntegrityHash { get; set; } = string.Empty;

    public InventoryDisposalCase InventoryDisposalCase { get; set; } = null!;
    public ApplicationUser MemberUser { get; set; } = null!;
}

public sealed class InventoryDisposalAction : TenantEntity
{
    public Guid InventoryDisposalCaseId { get; set; }
    public int Sequence { get; set; }
    public InventoryDisposalActionType ActionType { get; set; }
    public Guid ActorUserId { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    [Required, MaxLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
    [Required, MaxLength(64)] public string PayloadHash { get; set; } = string.Empty;
    [Required, MaxLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [MaxLength(1000)] public string? Comment { get; set; }
    [Column(TypeName = "nvarchar(max)")] public string SnapshotJson { get; set; } = "{}";
    [Required, MaxLength(64)] public string IntegrityHash { get; set; } = string.Empty;

    public InventoryDisposalCase InventoryDisposalCase { get; set; } = null!;
    public ApplicationUser ActorUser { get; set; } = null!;
}
