using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Entities.Inventory;

namespace ErpSystem.Core.DTOs.Inventory;

public sealed class InventoryLabelProfileDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Symbology { get; set; } = "QR";
    public decimal WidthMm { get; set; }
    public decimal HeightMm { get; set; }
    public int Dpi { get; set; }
    public bool IncludeItemCode { get; set; }
    public bool IncludeItemName { get; set; }
    public bool IncludeUnit { get; set; }
    public bool IncludeLot { get; set; }
    public bool IncludeSerial { get; set; }
    public bool IncludeExpiry { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class SaveInventoryLabelProfileRequest
{
    [Required, StringLength(100)] public string Name { get; set; } = string.Empty;
    [StringLength(500)] public string? Description { get; set; }
    [Required, RegularExpression("^(QR|CODE128)$")] public string Symbology { get; set; } = "QR";
    [Range(20, 210)] public decimal WidthMm { get; set; } = 60;
    [Range(15, 297)] public decimal HeightMm { get; set; } = 40;
    [Range(96, 600)] public int Dpi { get; set; } = 203;
    public bool IncludeItemCode { get; set; } = true;
    public bool IncludeItemName { get; set; } = true;
    public bool IncludeUnit { get; set; } = true;
    public bool IncludeLot { get; set; }
    public bool IncludeSerial { get; set; }
    public bool IncludeExpiry { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
    public string? RowVersion { get; set; }
}

public sealed class InventoryLabelCandidateDto
{
    public Guid InventoryItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string Identifier { get; set; } = string.Empty;
    public string IdentifierKind { get; set; } = string.Empty;
    public Guid? UnitOfMeasureId { get; set; }
    public string? UnitCode { get; set; }
}

public sealed class RecordInventoryLabelPrintRequest
{
    [Required] public Guid LabelProfileId { get; set; }
    [Required] public Guid InventoryItemId { get; set; }
    public Guid? UnitOfMeasureId { get; set; }
    [Required, StringLength(200)] public string Identifier { get; set; } = string.Empty;
    [Required, StringLength(40)] public string IdentifierKind { get; set; } = string.Empty;
    [Range(1, 1000)] public int LabelCount { get; set; } = 1;
    [StringLength(100)] public string? LotNumber { get; set; }
    [StringLength(100)] public string? SerialNumber { get; set; }
    public DateTime? ExpiryDate { get; set; }
    [StringLength(200)] public string? PrinterName { get; set; }
}

public sealed class InventoryLabelPrintEventDto
{
    public Guid Id { get; set; }
    public Guid LabelProfileId { get; set; }
    public string LabelProfileName { get; set; } = string.Empty;
    public Guid InventoryItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string Identifier { get; set; } = string.Empty;
    public string IdentifierKind { get; set; } = string.Empty;
    public int LabelCount { get; set; }
    public string? LotNumber { get; set; }
    public string? SerialNumber { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? PrinterName { get; set; }
    public DateTime PrintedAtUtc { get; set; }
}

public class InventoryScanDocumentSummaryDto
{
    public Guid DocumentId { get; set; }
    public string DocumentReference { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public DateTime DocumentDate { get; set; }
}

public sealed class InventoryScanDocumentContextDto : InventoryScanDocumentSummaryDto
{
    public InventoryScanOperation Operation { get; set; }
    public List<InventoryScanDocumentLineDto> Lines { get; set; } = new();
}

public sealed class InventoryScanDocumentLineDto
{
    public Guid DocumentLineId { get; set; }
    public Guid InventoryItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public decimal ExpectedQuantity { get; set; }
    public decimal ProcessedQuantity { get; set; }
    public Guid? LocationId { get; set; }
    [StringLength(100)] public string? LocationIdentifier { get; set; }
    public string? LocationName { get; set; }
    public string? LotNumber { get; set; }
    public string? BatchNumber { get; set; }
    public string? SerialNumber { get; set; }
    public DateTime? ManufactureDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
}

public sealed class SynchronizeInventoryScanBatchRequest
{
    [Required, StringLength(100)] public string DeviceId { get; set; } = string.Empty;
    [Required, StringLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
    [Required] public InventoryScanOperation Operation { get; set; }
    [Required] public Guid DocumentId { get; set; }
    [Required] public Guid WarehouseId { get; set; }
    public bool ApplyTransaction { get; set; }
    [Required, MinLength(1), MaxLength(500)] public List<InventoryScanInputDto> Lines { get; set; } = new();
}

public sealed class InventoryScanInputDto
{
    [Required] public Guid ClientLineId { get; set; }
    [Required, StringLength(200)] public string RawIdentifier { get; set; } = string.Empty;
    public Guid? DocumentLineId { get; set; }
    [Range(0.00000001, 999999999)] public decimal Quantity { get; set; } = 1;
    public Guid? LocationId { get; set; }
    [StringLength(100)] public string? LocationIdentifier { get; set; }
    [StringLength(100)] public string? LotNumber { get; set; }
    [StringLength(100)] public string? BatchNumber { get; set; }
    [StringLength(100)] public string? SerialNumber { get; set; }
    public DateTime? ManufactureDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public Guid? InventoryTrackingExceptionId { get; set; }
    public DateTime ScannedAtUtc { get; set; }
}

public sealed class InventoryScanBatchDto
{
    public Guid Id { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public InventoryScanOperation Operation { get; set; }
    public Guid DocumentId { get; set; }
    public string DocumentReference { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public bool ApplyTransaction { get; set; }
    public InventoryScanBatchStatus Status { get; set; }
    public DateTime CapturedAtUtc { get; set; }
    public DateTime? ProcessedAtUtc { get; set; }
    public string? FailureCode { get; set; }
    public string? FailureMessage { get; set; }
    public InventoryScanReconciliationDto? Reconciliation { get; set; }
    public List<InventoryScanLineDto> Lines { get; set; } = new();
}

public sealed class InventoryScanLineDto
{
    public Guid ClientLineId { get; set; }
    public string RawIdentifier { get; set; } = string.Empty;
    public string IdentifierKind { get; set; } = string.Empty;
    public Guid InventoryItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public Guid DocumentLineId { get; set; }
    public decimal ScannedQuantity { get; set; }
    public decimal ConversionToBase { get; set; }
    public decimal BaseQuantity { get; set; }
    public Guid? LocationId { get; set; }
    public string? LocationName { get; set; }
    public string? LocationIdentifier { get; set; }
    public string? LotNumber { get; set; }
    public string? BatchNumber { get; set; }
    public string? SerialNumber { get; set; }
    public DateTime? ManufactureDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public Guid? InventoryTrackingExceptionId { get; set; }
    public DateTime ScannedAtUtc { get; set; }
}

public sealed class InventoryScanReconciliationDto
{
    public string DocumentStatus { get; set; } = string.Empty;
    public int ScannedLineCount { get; set; }
    public decimal ScannedBaseQuantity { get; set; }
    public int StockMovementCount { get; set; }
    public decimal NetStockMovementQuantity { get; set; }
    public int AuditedEventCount { get; set; }
    public DateTime ReconciledAtUtc { get; set; }
}

public sealed class InventoryTransactionScanLineDto
{
    public Guid DocumentLineId { get; set; }
    public Guid InventoryItemId { get; set; }
    public decimal BaseQuantity { get; set; }
    public Guid? LocationId { get; set; }
    public string? LotNumber { get; set; }
    public string? BatchNumber { get; set; }
    public string? SerialNumber { get; set; }
    public DateTime? ManufactureDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public Guid? InventoryTrackingExceptionId { get; set; }
}

public sealed class InventoryScanningException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}

public sealed class InventoryScanningAuthorizationException(string message) : UnauthorizedAccessException(message);
