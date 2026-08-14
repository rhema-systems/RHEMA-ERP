using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ErpSystem.Core.Entities.Inventory;

public enum InventoryIssueVoucherStatus
{
    Issued = 1,
    Acknowledged = 2
}

public enum InventoryIssueVoucherActionType
{
    Issued = 1,
    Acknowledged = 2
}

[Table("InventoryIssueVouchers")]
public sealed class InventoryIssueVoucher : TenantEntity
{
    [Required, MaxLength(50)] public string VoucherNumber { get; set; } = string.Empty;
    public Guid InventoryRequisitionId { get; set; }
    public InventoryIssueVoucherStatus Status { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid? LocationId { get; set; }
    public Guid DepartmentId { get; set; }
    [MaxLength(100)] public string? DepartmentName { get; set; }
    [MaxLength(100)] public string? CostCenter { get; set; }
    public Guid? ProjectId { get; set; }
    [MaxLength(100)] public string? ProjectCode { get; set; }
    public Guid RequestedById { get; set; }
    public Guid ApprovedById { get; set; }
    public Guid IssuedById { get; set; }
    public Guid ReceiverUserId { get; set; }
    public Guid? AcknowledgedById { get; set; }
    public DateTime IssuedAtUtc { get; set; }
    public DateTime? AcknowledgedAtUtc { get; set; }
    [MaxLength(2000)] public string? Notes { get; set; }
    [MaxLength(1000)] public string? ReceiverComment { get; set; }
    [Required, MaxLength(50)] public string MovementReasonCode { get; set; } = string.Empty;
    public Guid? FinancePostingEventId { get; set; }
    public Guid? FinanceJournalEntryId { get; set; }
    [Required, MaxLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
    [Required, MaxLength(64)] public string PayloadHash { get; set; } = string.Empty;
    [Required, MaxLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [Required, Column(TypeName = "nvarchar(max)")] public string SourceSnapshotJson { get; set; } = "{}";
    [Required, MaxLength(64)] public string IntegrityHash { get; set; } = string.Empty;
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public InventoryRequisition InventoryRequisition { get; set; } = null!;
    public Warehouse Warehouse { get; set; } = null!;
    public WarehouseLocation? Location { get; set; }
    public ApplicationUser RequestedBy { get; set; } = null!;
    public ApplicationUser ApprovedBy { get; set; } = null!;
    public ApplicationUser IssuedBy { get; set; } = null!;
    public ApplicationUser ReceiverUser { get; set; } = null!;
    public ApplicationUser? AcknowledgedBy { get; set; }
    public ICollection<InventoryIssueVoucherLine> Lines { get; set; } = new List<InventoryIssueVoucherLine>();
    public ICollection<InventoryIssueVoucherAction> Actions { get; set; } = new List<InventoryIssueVoucherAction>();
}

[Table("InventoryIssueVoucherLines")]
public sealed class InventoryIssueVoucherLine : TenantEntity
{
    public Guid InventoryIssueVoucherId { get; set; }
    public Guid InventoryRequisitionItemId { get; set; }
    public Guid InventoryItemId { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid? LocationId { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal Quantity { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal UnitCost { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal TotalValue { get; set; }
    [MaxLength(20)] public string? UnitOfMeasure { get; set; }
    [MaxLength(100)] public string? LotNumber { get; set; }
    [MaxLength(100)] public string? BatchNumber { get; set; }
    [MaxLength(100)] public string? SerialNumber { get; set; }
    public DateTime? ManufactureDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public Guid? InventoryTrackingExceptionId { get; set; }
    [Required, MaxLength(64)] public string IntegrityHash { get; set; } = string.Empty;

    public InventoryIssueVoucher InventoryIssueVoucher { get; set; } = null!;
    public InventoryRequisitionItem InventoryRequisitionItem { get; set; } = null!;
    public InventoryItem InventoryItem { get; set; } = null!;
    public Warehouse Warehouse { get; set; } = null!;
    public WarehouseLocation? Location { get; set; }
}

[Table("InventoryIssueVoucherActions")]
public sealed class InventoryIssueVoucherAction : TenantEntity
{
    public Guid InventoryIssueVoucherId { get; set; }
    [Range(1, int.MaxValue)] public int Sequence { get; set; }
    public InventoryIssueVoucherActionType ActionType { get; set; }
    public InventoryIssueVoucherStatus StatusAfter { get; set; }
    public Guid ActorUserId { get; set; }
    [Required, MaxLength(300)] public string ActorName { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; }
    [Required, MaxLength(1000)] public string Comment { get; set; } = string.Empty;
    [Required, Column(TypeName = "nvarchar(max)")] public string PayloadJson { get; set; } = "{}";
    [Required, MaxLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [Required, MaxLength(64)] public string IntegrityHash { get; set; } = string.Empty;

    public InventoryIssueVoucher InventoryIssueVoucher { get; set; } = null!;
}
