using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.Entities.Inventory;

public enum InventoryScanOperation
{
    GoodsReceipt = 1,
    RequisitionIssue = 2,
    RequisitionReturn = 3,
    TransferShipment = 4,
    TransferReceipt = 5,
    PhysicalCount = 6
}

public enum InventoryScanBatchStatus
{
    Captured = 1,
    Applied = 2,
    Failed = 3
}

public sealed class InventoryLabelProfile : TenantEntity
{
    [MaxLength(100)] public string Name { get; set; } = string.Empty;
    [MaxLength(500)] public string? Description { get; set; }
    [MaxLength(20)] public string Symbology { get; set; } = "QR";
    public decimal WidthMm { get; set; } = 60;
    public decimal HeightMm { get; set; } = 40;
    public int Dpi { get; set; } = 203;
    public bool IncludeItemCode { get; set; } = true;
    public bool IncludeItemName { get; set; } = true;
    public bool IncludeUnit { get; set; } = true;
    public bool IncludeLot { get; set; }
    public bool IncludeSerial { get; set; }
    public bool IncludeExpiry { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
    [Timestamp] public byte[] RowVersion { get; set; } = [];

    public ICollection<InventoryLabelPrintEvent> PrintEvents { get; set; } = new List<InventoryLabelPrintEvent>();
}

public sealed class InventoryLabelPrintEvent : TenantEntity
{
    public Guid LabelProfileId { get; set; }
    public InventoryLabelProfile LabelProfile { get; set; } = null!;
    public Guid InventoryItemId { get; set; }
    public InventoryItem InventoryItem { get; set; } = null!;
    public Guid? UnitOfMeasureId { get; set; }
    public UnitOfMeasure? UnitOfMeasure { get; set; }
    [MaxLength(200)] public string Identifier { get; set; } = string.Empty;
    [MaxLength(40)] public string IdentifierKind { get; set; } = string.Empty;
    public int LabelCount { get; set; }
    [MaxLength(100)] public string? LotNumber { get; set; }
    [MaxLength(100)] public string? SerialNumber { get; set; }
    public DateTime? ExpiryDate { get; set; }
    [MaxLength(200)] public string? PrinterName { get; set; }
    public Guid PrintedById { get; set; }
    public DateTime PrintedAtUtc { get; set; }
    [MaxLength(100)] public string CorrelationId { get; set; } = string.Empty;
}

public sealed class InventoryScanBatch : TenantEntity
{
    [MaxLength(100)] public string DeviceId { get; set; } = string.Empty;
    [MaxLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
    [MaxLength(64)] public string PayloadHash { get; set; } = string.Empty;
    public InventoryScanOperation Operation { get; set; }
    public Guid DocumentId { get; set; }
    [MaxLength(100)] public string DocumentReference { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;
    public bool ApplyTransaction { get; set; }
    public InventoryScanBatchStatus Status { get; set; }
    public Guid ActorUserId { get; set; }
    public DateTime CapturedAtUtc { get; set; }
    public DateTime? ProcessedAtUtc { get; set; }
    [MaxLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [MaxLength(1000)] public string? FailureCode { get; set; }
    [MaxLength(2000)] public string? FailureMessage { get; set; }
    public string? ReconciliationJson { get; set; }

    public ICollection<InventoryScanLine> Lines { get; set; } = new List<InventoryScanLine>();
}

public sealed class InventoryScanLine : TenantEntity
{
    public Guid ScanBatchId { get; set; }
    public InventoryScanBatch ScanBatch { get; set; } = null!;
    public Guid ClientLineId { get; set; }
    public int Sequence { get; set; }
    [MaxLength(200)] public string RawIdentifier { get; set; } = string.Empty;
    [MaxLength(40)] public string IdentifierKind { get; set; } = string.Empty;
    public Guid InventoryItemId { get; set; }
    public InventoryItem InventoryItem { get; set; } = null!;
    public Guid? UnitOfMeasureId { get; set; }
    public UnitOfMeasure? UnitOfMeasure { get; set; }
    public Guid DocumentLineId { get; set; }
    public decimal ScannedQuantity { get; set; }
    public decimal ConversionToBase { get; set; } = 1;
    public decimal BaseQuantity { get; set; }
    public Guid? LocationId { get; set; }
    public WarehouseLocation? Location { get; set; }
    [MaxLength(100)] public string? LocationIdentifier { get; set; }
    [MaxLength(100)] public string? LotNumber { get; set; }
    [MaxLength(100)] public string? BatchNumber { get; set; }
    [MaxLength(100)] public string? SerialNumber { get; set; }
    public DateTime? ManufactureDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public Guid? InventoryTrackingExceptionId { get; set; }
    public DateTime ScannedAtUtc { get; set; }
}
