using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Workflow;

namespace ErpSystem.Core.Entities.Inventory;

public enum InventoryTrackingDirection
{
    Receipt = 1,
    Issue = 2,
    Return = 3,
    TransferOut = 4,
    TransferIn = 5,
    AdjustmentIn = 6,
    AdjustmentOut = 7
}

[Table("InventoryTrackingExceptions")]
public sealed class InventoryTrackingException : TenantEntity
{
    public Guid InventoryItemId { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid? LocationId { get; set; }
    public Guid ReferenceId { get; set; }
    public Guid? ReferenceLineId { get; set; }

    [Required, MaxLength(100)] public string ReferenceType { get; set; } = string.Empty;
    [Required, MaxLength(100)] public string ReferenceNumber { get; set; } = string.Empty;
    [Required, MaxLength(2000)] public string Reason { get; set; } = string.Empty;
    [Required, Column(TypeName = "nvarchar(max)")] public string ExceptionCodesJson { get; set; } = "[]";
    [MaxLength(100)] public string? LotNumber { get; set; }
    [MaxLength(100)] public string? BatchNumber { get; set; }
    [MaxLength(100)] public string? SerialNumber { get; set; }

    public Guid WorkflowInstanceId { get; set; }
    public Guid WorkflowEvidenceDocumentId { get; set; }
    public Guid RequestedById { get; set; }
    public Guid ApprovedById { get; set; }
    public DateTime ApprovedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? ConsumedAtUtc { get; set; }
    public Guid? ConsumedByReferenceId { get; set; }
    public Guid? ConsumedByEventId { get; set; }
    [Required, MaxLength(64)] public string IntegrityHash { get; set; } = string.Empty;
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public InventoryItem InventoryItem { get; set; } = null!;
    public Warehouse Warehouse { get; set; } = null!;
    public WarehouseLocation? Location { get; set; }
    public WorkflowInstance WorkflowInstance { get; set; } = null!;
    public WorkflowEvidenceDocument WorkflowEvidenceDocument { get; set; } = null!;
}

[Table("InventoryTraceabilityEvents")]
public sealed class InventoryTraceabilityEvent : TenantEntity
{
    public Guid InventoryItemId { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid? LocationId { get; set; }
    public InventoryTrackingDirection Direction { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal Quantity { get; set; }
    [Required, MaxLength(100)] public string ReferenceType { get; set; } = string.Empty;
    [Required, MaxLength(100)] public string ReferenceNumber { get; set; } = string.Empty;
    public Guid ReferenceId { get; set; }
    public Guid? ReferenceLineId { get; set; }
    [Required, MaxLength(200)] public string EventKey { get; set; } = string.Empty;
    [MaxLength(100)] public string? LotNumber { get; set; }
    [MaxLength(100)] public string? BatchNumber { get; set; }
    [MaxLength(100)] public string? SerialNumber { get; set; }
    public DateTime? ManufactureDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public Guid? TrackingExceptionId { get; set; }
    public Guid ActorUserId { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    [Required, MaxLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [Required, MaxLength(64)] public string PayloadHash { get; set; } = string.Empty;

    public InventoryItem InventoryItem { get; set; } = null!;
    public Warehouse Warehouse { get; set; } = null!;
    public WarehouseLocation? Location { get; set; }
    public InventoryTrackingException? TrackingException { get; set; }
}
