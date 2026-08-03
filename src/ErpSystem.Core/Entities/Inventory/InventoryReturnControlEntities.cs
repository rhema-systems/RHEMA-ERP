using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.DocumentManagement;

namespace ErpSystem.Core.Entities.Inventory;

public enum InventoryReturnVoucherStatus
{
    PendingApproval = 1,
    Approved = 2,
    Rejected = 3,
    Posted = 4,
    Reversed = 5
}

public enum InventoryReturnVoucherActionType
{
    Submitted = 1,
    Approved = 2,
    Rejected = 3,
    Posted = 4,
    Reversed = 5
}

public sealed class InventoryReturnVoucher : TenantEntity
{
    [Required, MaxLength(50)] public string VoucherNumber { get; set; } = string.Empty;
    public Guid InventoryRequisitionId { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid RequestedById { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public Guid? RejectedById { get; set; }
    public DateTime? RejectedAtUtc { get; set; }
    [MaxLength(1000)] public string? RejectionReason { get; set; }
    public Guid? PostedById { get; set; }
    public DateTime? PostedAtUtc { get; set; }
    public Guid? ReversedById { get; set; }
    public DateTime? ReversedAtUtc { get; set; }
    [MaxLength(1000)] public string? ReversalReason { get; set; }
    public InventoryReturnVoucherStatus Status { get; set; } = InventoryReturnVoucherStatus.PendingApproval;
    [Required, MaxLength(50)] public string ReasonCode { get; set; } = string.Empty;
    [Required, MaxLength(1000)] public string Reason { get; set; } = string.Empty;
    [MaxLength(2000)] public string? Notes { get; set; }
    [Required, MaxLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
    [Required, MaxLength(64)] public string PayloadHash { get; set; } = string.Empty;
    [Required, MaxLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [Required, MaxLength(64)] public string IntegrityHash { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,2)")] public decimal TotalValue { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public InventoryRequisition InventoryRequisition { get; set; } = null!;
    public Warehouse Warehouse { get; set; } = null!;
    public ApplicationUser RequestedBy { get; set; } = null!;
    public ICollection<InventoryReturnVoucherLine> Lines { get; set; } = new List<InventoryReturnVoucherLine>();
    public ICollection<InventoryReturnVoucherEvidence> Evidence { get; set; } = new List<InventoryReturnVoucherEvidence>();
    public ICollection<InventoryReturnVoucherAction> Actions { get; set; } = new List<InventoryReturnVoucherAction>();
}

public sealed class InventoryReturnVoucherLine : TenantEntity
{
    public Guid InventoryReturnVoucherId { get; set; }
    public Guid InventoryRequisitionItemId { get; set; }
    public Guid InventoryItemId { get; set; }
    public Guid? LocationId { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal Quantity { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal UnitCost { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal TotalValue { get; set; }
    [MaxLength(100)] public string? LotNumber { get; set; }
    [MaxLength(100)] public string? BatchNumber { get; set; }
    [MaxLength(100)] public string? SerialNumber { get; set; }
    public DateTime? ManufactureDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public Guid? InventoryTrackingExceptionId { get; set; }
    [Required, MaxLength(64)] public string IntegrityHash { get; set; } = string.Empty;

    public InventoryReturnVoucher InventoryReturnVoucher { get; set; } = null!;
    public InventoryRequisitionItem InventoryRequisitionItem { get; set; } = null!;
    public InventoryItem InventoryItem { get; set; } = null!;
    public WarehouseLocation? Location { get; set; }
}

public sealed class InventoryReturnVoucherEvidence : TenantEntity
{
    public Guid InventoryReturnVoucherId { get; set; }
    public Guid CentralDocumentVersionId { get; set; }
    public Guid FileUploadRecordId { get; set; }
    [Required, MaxLength(500)] public string EvidenceReference { get; set; } = string.Empty;
    [Required, MaxLength(64)] public string IntegrityHash { get; set; } = string.Empty;

    public InventoryReturnVoucher InventoryReturnVoucher { get; set; } = null!;
    public CentralDocumentVersion CentralDocumentVersion { get; set; } = null!;
    public FileUploadRecord FileUploadRecord { get; set; } = null!;
}

public sealed class InventoryReturnVoucherAction : TenantEntity
{
    public Guid InventoryReturnVoucherId { get; set; }
    public int Sequence { get; set; }
    public InventoryReturnVoucherActionType ActionType { get; set; }
    public Guid ActorUserId { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    [Required, MaxLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
    [Required, MaxLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [MaxLength(1000)] public string? Comment { get; set; }
    [Column(TypeName = "nvarchar(max)")] public string SnapshotJson { get; set; } = "{}";
    [Required, MaxLength(64)] public string IntegrityHash { get; set; } = string.Empty;

    public InventoryReturnVoucher InventoryReturnVoucher { get; set; } = null!;
    public ApplicationUser ActorUser { get; set; } = null!;
}

public sealed class StockAdjustmentEvidence : TenantEntity
{
    public Guid StockAdjustmentId { get; set; }
    public Guid CentralDocumentVersionId { get; set; }
    public Guid FileUploadRecordId { get; set; }
    [Required, MaxLength(500)] public string EvidenceReference { get; set; } = string.Empty;
    [Required, MaxLength(64)] public string IntegrityHash { get; set; } = string.Empty;

    public StockAdjustment StockAdjustment { get; set; } = null!;
    public CentralDocumentVersion CentralDocumentVersion { get; set; } = null!;
    public FileUploadRecord FileUploadRecord { get; set; } = null!;
}

public sealed class StockAdjustmentAction : TenantEntity
{
    public Guid StockAdjustmentId { get; set; }
    public int Sequence { get; set; }
    [Required, MaxLength(40)] public string ActionType { get; set; } = string.Empty;
    public Guid ActorUserId { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    [Required, MaxLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
    [Required, MaxLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [MaxLength(1000)] public string? Comment { get; set; }
    [Column(TypeName = "nvarchar(max)")] public string SnapshotJson { get; set; } = "{}";
    [Required, MaxLength(64)] public string IntegrityHash { get; set; } = string.Empty;

    public StockAdjustment StockAdjustment { get; set; } = null!;
    public ApplicationUser ActorUser { get; set; } = null!;
}
