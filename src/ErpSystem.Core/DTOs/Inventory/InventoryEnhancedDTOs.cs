using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Inventory;

#region Unit of Measure DTOs

public class UnitOfMeasureDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Symbol { get; set; }
    public string? Category { get; set; }
    public bool IsBaseUnit { get; set; }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
}

public class CreateUnitOfMeasureDto
{
    [Required]
    [MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(10)]
    public string? Symbol { get; set; }

    [MaxLength(50)]
    public string? Category { get; set; }

    public bool IsBaseUnit { get; set; }
    public int SortOrder { get; set; }
}

public class UpdateUnitOfMeasureDto
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(10)]
    public string? Symbol { get; set; }

    [MaxLength(50)]
    public string? Category { get; set; }

    public bool IsBaseUnit { get; set; }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
}

public class UnitOfMeasureConversionDto
{
    public Guid Id { get; set; }
    public Guid FromUnitId { get; set; }
    public string FromUnitCode { get; set; } = string.Empty;
    public Guid ToUnitId { get; set; }
    public string ToUnitCode { get; set; } = string.Empty;
    public decimal ConversionFactor { get; set; }
    public bool IsActive { get; set; }
}

public class CreateUnitOfMeasureConversionDto
{
    [Required]
    public Guid FromUnitId { get; set; }

    [Required]
    public Guid ToUnitId { get; set; }

    [Required]
    [Range(0.000001, double.MaxValue)]
    public decimal ConversionFactor { get; set; }
}

public class ItemUnitOfMeasureDto
{
    public Guid Id { get; set; }
    public Guid InventoryItemId { get; set; }
    public Guid UnitOfMeasureId { get; set; }
    public string UnitCode { get; set; } = string.Empty;
    public string UnitName { get; set; } = string.Empty;
    public decimal ConversionFactor { get; set; }
    public bool IsBaseUnit { get; set; }
    public bool IsPurchaseUnit { get; set; }
    public bool IsSalesUnit { get; set; }
    public string? Barcode { get; set; }
    public decimal? Length { get; set; }
    public decimal? Width { get; set; }
    public decimal? Height { get; set; }
    public decimal? Weight { get; set; }
}

public class CreateItemUnitOfMeasureDto
{
    [Required]
    public Guid InventoryItemId { get; set; }

    [Required]
    public Guid UnitOfMeasureId { get; set; }

    [Required]
    [Range(0.000001, double.MaxValue)]
    public decimal ConversionFactor { get; set; }

    public bool IsBaseUnit { get; set; }
    public bool IsPurchaseUnit { get; set; }
    public bool IsSalesUnit { get; set; }
    public string? Barcode { get; set; }
    public decimal? Length { get; set; }
    public decimal? Width { get; set; }
    public decimal? Height { get; set; }
    public decimal? Weight { get; set; }
}

#endregion

#region Item Supplier DTOs

public class ItemSupplierDto
{
    public Guid Id { get; set; }
    public Guid InventoryItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string? SupplierItemCode { get; set; }
    public string? SupplierItemName { get; set; }
    public bool IsPreferred { get; set; }
    public int Priority { get; set; }
    public decimal UnitPrice { get; set; }
    public string Currency { get; set; } = "USD";
    public decimal MinimumOrderQuantity { get; set; }
    public decimal OrderMultiple { get; set; }
    public int LeadTimeDays { get; set; }
    public DateTime? LastPurchaseDate { get; set; }
    public decimal? LastPurchasePrice { get; set; }
    public bool IsActive { get; set; }
}

public class CreateItemSupplierDto
{
    [Required]
    public Guid InventoryItemId { get; set; }

    [Required]
    public Guid SupplierId { get; set; }

    [MaxLength(100)]
    public string? SupplierItemCode { get; set; }

    [MaxLength(200)]
    public string? SupplierItemName { get; set; }

    public bool IsPreferred { get; set; }
    public int Priority { get; set; } = 1;

    [Range(0, double.MaxValue)]
    public decimal UnitPrice { get; set; }

    [MaxLength(10)]
    public string Currency { get; set; } = "USD";

    [Range(0, double.MaxValue)]
    public decimal MinimumOrderQuantity { get; set; } = 1;

    [Range(0, double.MaxValue)]
    public decimal OrderMultiple { get; set; } = 1;

    [Range(0, 365)]
    public int LeadTimeDays { get; set; } = 7;
}

public class UpdateItemSupplierDto
{
    [MaxLength(100)]
    public string? SupplierItemCode { get; set; }

    [MaxLength(200)]
    public string? SupplierItemName { get; set; }

    public bool IsPreferred { get; set; }
    public int Priority { get; set; }

    [Range(0, double.MaxValue)]
    public decimal UnitPrice { get; set; }

    [MaxLength(10)]
    public string Currency { get; set; } = "USD";

    [Range(0, double.MaxValue)]
    public decimal MinimumOrderQuantity { get; set; }

    [Range(0, double.MaxValue)]
    public decimal OrderMultiple { get; set; }

    [Range(0, 365)]
    public int LeadTimeDays { get; set; }

    public bool IsActive { get; set; }
}

#endregion

#region GRN DTOs

public class GoodsReceiptNoteDto
{
    public Guid Id { get; set; }
    public string GRNNumber { get; set; } = string.Empty;
    public DateTime ReceiptDate { get; set; }
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public Guid? SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public Guid? PurchaseOrderId { get; set; }
    public string? PurchaseOrderNumber { get; set; }
    public GRNStatus Status { get; set; }
    public string? DeliveryNoteNumber { get; set; }
    public string? VehicleNumber { get; set; }
    public int TotalItems { get; set; }
    public decimal TotalQuantity { get; set; }
    public decimal TotalValue { get; set; }
    public bool RequiresInspection { get; set; }
    public DateTime? InspectionDate { get; set; }
    public string? ReceivedByName { get; set; }
    public string? Notes { get; set; }
    public string CreatedAtFormatted { get; set; } = string.Empty;
}

public class GoodsReceiptNoteDetailDto : GoodsReceiptNoteDto
{
    public Guid? ReceivingLocationId { get; set; }
    public string? ReceivingLocationName { get; set; }
    public string? InspectedByName { get; set; }
    public string? InspectionNotes { get; set; }
    public List<GoodsReceiptNoteItemDto> Items { get; set; } = new();
}

public class GoodsReceiptNoteItemDto
{
    public Guid Id { get; set; }
    public Guid InventoryItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public decimal OrderedQuantity { get; set; }
    public decimal ReceivedQuantity { get; set; }
    public decimal AcceptedQuantity { get; set; }
    public decimal RejectedQuantity { get; set; }
    public string UnitOfMeasure { get; set; } = string.Empty;
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
    public string? LotNumber { get; set; }
    public string? SerialNumber { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public InspectionResult InspectionResult { get; set; }
    public string? InspectionNotes { get; set; }
    public Guid? StorageLocationId { get; set; }
    public string? StorageLocationName { get; set; }
}

public class CreateGoodsReceiptNoteDto
{
    [Required]
    public Guid WarehouseId { get; set; }

    public Guid? SupplierId { get; set; }
    public Guid? PurchaseOrderId { get; set; }
    public Guid? ReceivingLocationId { get; set; }

    [MaxLength(100)]
    public string? DeliveryNoteNumber { get; set; }

    [MaxLength(50)]
    public string? VehicleNumber { get; set; }

    [MaxLength(200)]
    public string? DriverName { get; set; }

    public bool RequiresInspection { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [Required]
    public List<CreateGoodsReceiptNoteItemDto> Items { get; set; } = new();
}

public class CreateGoodsReceiptNoteItemDto
{
    [Required]
    public Guid InventoryItemId { get; set; }

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal ReceivedQuantity { get; set; }

    [Range(0, double.MaxValue)]
    public decimal UnitCost { get; set; }

    [MaxLength(100)]
    public string? LotNumber { get; set; }

    [MaxLength(100)]
    public string? SerialNumber { get; set; }

    public DateTime? ExpiryDate { get; set; }
    public Guid? StorageLocationId { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

public class UpdateGRNInspectionDto
{
    [Required]
    public Guid GRNItemId { get; set; }

    [Required]
    public InspectionResult InspectionResult { get; set; }

    [Range(0, double.MaxValue)]
    public decimal AcceptedQuantity { get; set; }

    [Range(0, double.MaxValue)]
    public decimal RejectedQuantity { get; set; }

    [MaxLength(2000)]
    public string? InspectionNotes { get; set; }
}

#endregion

#region Inventory Transfer DTOs

public class InventoryTransferDto
{
    public Guid Id { get; set; }
    public string TransferNumber { get; set; } = string.Empty;
    public Guid SourceWarehouseId { get; set; }
    public string SourceWarehouseName { get; set; } = string.Empty;
    public Guid DestinationWarehouseId { get; set; }
    public string DestinationWarehouseName { get; set; } = string.Empty;
    public TransferStatus Status { get; set; }
    public TransferType TransferType { get; set; }
    public string Priority { get; set; } = "Normal";
    public DateTime RequestDate { get; set; }
    public DateTime? RequiredDate { get; set; }
    public DateTime? ShippedDate { get; set; }
    public DateTime? ReceivedDate { get; set; }
    public int TotalItems { get; set; }
    public decimal TotalQuantity { get; set; }
    public decimal TotalValue { get; set; }
    
    // Shipping Costs
    public decimal ShippingCost { get; set; }
    public decimal MiscellaneousCost { get; set; }
    public string? MiscellaneousCostDescription { get; set; }
    public decimal TotalAdditionalCost { get; set; }
    public string CostAllocationMethod { get; set; } = "SpreadToItemCost";
    public string CostApportionmentBasis { get; set; } = "Value";
    public string? ExpenseGLAccount { get; set; }
    public bool CostsAllocated { get; set; }
    
    public string? RequestedByName { get; set; }
    public string? ApprovedByName { get; set; }
    public string? Notes { get; set; }
    public string CreatedAtFormatted { get; set; } = string.Empty;

    /// <summary>
    /// Convenience field for UX: the current workflow step name when the transfer is pending approval.
    /// </summary>
    public string? CurrentWorkflowStepName { get; set; }
}

public class InventoryTransferDetailDto : InventoryTransferDto
{
    public DateTime? ApprovedDate { get; set; }
    public string? ShippedByName { get; set; }
    public string? ReceivedByName { get; set; }
    public string? TrackingNumber { get; set; }
    public string? CarrierName { get; set; }
    public List<InventoryTransferItemDto> Items { get; set; } = new();
}

public class InventoryTransferItemDto
{
    public Guid Id { get; set; }
    public Guid InventoryItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public decimal RequestedQuantity { get; set; }
    public decimal ShippedQuantity { get; set; }
    public decimal ReceivedQuantity { get; set; }
    public string UnitOfMeasure { get; set; } = string.Empty;
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
    
    // Allocated shipping/misc costs
    public decimal AllocatedCostPerUnit { get; set; }
    public decimal TotalAllocatedCost { get; set; }
    public decimal LandedUnitCost { get; set; }
    
    public string? LotNumber { get; set; }
    public string? SerialNumber { get; set; }
    public Guid? SourceLocationId { get; set; }
    public string? SourceLocationName { get; set; }
    public Guid? DestinationLocationId { get; set; }
    public string? DestinationLocationName { get; set; }
    public string? Notes { get; set; }
}

public class CreateInventoryTransferDto
{
    [Required]
    public Guid SourceWarehouseId { get; set; }

    [Required]
    public Guid DestinationWarehouseId { get; set; }

    public TransferType TransferType { get; set; } = TransferType.Standard;

    [MaxLength(20)]
    public string Priority { get; set; } = "Normal";

    public DateTime? RequiredDate { get; set; }

    [MaxLength(2000)]
    public string? Reason { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [Required]
    public List<CreateInventoryTransferItemDto> Items { get; set; } = new();
}

public class UpdateInventoryTransferDto
{
    public Guid? SourceWarehouseId { get; set; }

    public Guid? DestinationWarehouseId { get; set; }

    public DateTime? RequiredDate { get; set; }

    [MaxLength(2000)]
    public string? Reason { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

public class CreateInventoryTransferItemDto
{
    [Required]
    public Guid InventoryItemId { get; set; }

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal RequestedQuantity { get; set; }

    public Guid? SourceLocationId { get; set; }
    public Guid? DestinationLocationId { get; set; }

    [MaxLength(100)]
    public string? LotNumber { get; set; }

    [MaxLength(100)]
    public string? SerialNumber { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

public class AddTransferItemDto
{
    [Required]
    public Guid InventoryItemId { get; set; }

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal RequestedQuantity { get; set; }

    public Guid? SourceLocationId { get; set; }
    public Guid? DestinationLocationId { get; set; }

    [MaxLength(100)]
    public string? LotNumber { get; set; }

    [MaxLength(100)]
    public string? SerialNumber { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

public class UpdateTransferItemDto
{
    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal RequestedQuantity { get; set; }

    public Guid? SourceLocationId { get; set; }
    public Guid? DestinationLocationId { get; set; }

    [MaxLength(100)]
    public string? LotNumber { get; set; }

    [MaxLength(100)]
    public string? SerialNumber { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

/// <summary>
/// DTO for shipping a transfer with optional shipping costs
/// </summary>
public class ShipTransferWithCostsDto
{
    [MaxLength(100)]
    public string? TrackingNumber { get; set; }

    [MaxLength(100)]
    public string? CarrierName { get; set; }

    /// <summary>
    /// Shipping/freight cost
    /// </summary>
    [Range(0, double.MaxValue)]
    public decimal ShippingCost { get; set; }

    /// <summary>
    /// Other miscellaneous costs (handling, insurance, etc.)
    /// </summary>
    [Range(0, double.MaxValue)]
    public decimal MiscellaneousCost { get; set; }

    /// <summary>
    /// Description of miscellaneous costs
    /// </summary>
    [MaxLength(500)]
    public string? MiscellaneousCostDescription { get; set; }

    /// <summary>
    /// How to allocate the additional costs:
    /// - "SpreadToItemCost": Distribute costs proportionally to item values and add to item cost
    /// - "GLExpense": Post costs directly to a GL expense account
    /// </summary>
    [MaxLength(50)]
    public string CostAllocationMethod { get; set; } = "SpreadToItemCost";

    /// <summary>
    /// Basis used when spreading costs to item costs:
    /// - "Value": Proportional to shipped line value (default)
    /// - "Weight": Proportional to shipped quantity (placeholder until per-line shipment weight is captured)
    /// - "Quantity": Proportional to shipped quantity
    /// </summary>
    [MaxLength(50)]
    public string CostApportionmentBasis { get; set; } = "Value";

    /// <summary>
    /// GL Account number for expense posting (required when CostAllocationMethod is "GLExpense")
    /// </summary>
    [MaxLength(50)]
    public string? ExpenseGLAccount { get; set; }

    /// <summary>
    /// Optional: Specify shipped quantities per item (if different from requested)
    /// </summary>
    public List<ShipTransferItemDto>? Items { get; set; }
}

/// <summary>
/// DTO for specifying shipped quantity per item
/// </summary>
public class ShipTransferItemDto
{
    [Required]
    public Guid ItemId { get; set; }

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal ShippedQuantity { get; set; }
}

#endregion

#region Physical Count DTOs

public class PhysicalCountDto
{
    public Guid Id { get; set; }
    public string CountNumber { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public CountType CountType { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CountDate { get; set; }
    public DateTime? StartedDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public Guid? LocationId { get; set; }
    public string? LocationName { get; set; }
    public Guid? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public bool FreezeInventory { get; set; }
    public int TotalItems { get; set; }
    public int CountedItems { get; set; }
    public int ItemsWithVariance { get; set; }
    public decimal TotalVarianceValue { get; set; }
    public string? InitiatedByName { get; set; }
    public string? Notes { get; set; }
    public string CreatedAtFormatted { get; set; } = string.Empty;
}

public class PhysicalCountDetailDto : PhysicalCountDto
{
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public List<PhysicalCountItemDto> Items { get; set; } = new();
}

public class PhysicalCountItemDto
{
    public Guid Id { get; set; }
    public Guid InventoryItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public Guid? LocationId { get; set; }
    public string? LocationName { get; set; }
    public decimal SystemQuantity { get; set; }
    public decimal CountedQuantity { get; set; }
    public decimal VarianceQuantity { get; set; }
    public decimal VarianceValue { get; set; }
    public decimal VariancePercent { get; set; }
    public string UnitOfMeasure { get; set; } = string.Empty;
    public string? LotNumber { get; set; }
    public string? SerialNumber { get; set; }
    public bool IsCounted { get; set; }
    public DateTime? CountedAt { get; set; }
    public string? CountedByName { get; set; }
    public string? Notes { get; set; }
}

public class CreatePhysicalCountDto
{
    [Required]
    public Guid WarehouseId { get; set; }

    public CountType CountType { get; set; } = CountType.FullCount;
    public Guid? LocationId { get; set; }
    public Guid? CategoryId { get; set; }
    public bool FreezeInventory { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

public class RecordCountItemDto
{
    [Required]
    public Guid PhysicalCountItemId { get; set; }

    [Required]
    [Range(0, double.MaxValue)]
    public decimal CountedQuantity { get; set; }

    [MaxLength(100)]
    public string? LotNumber { get; set; }

    [MaxLength(100)]
    public string? SerialNumber { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

public class UpdatePhysicalCountDto
{
    public string? Notes { get; set; }
    public bool? FreezeInventory { get; set; }
    public bool? BlindCount { get; set; }
    public bool? IncludeZeroStock { get; set; }
}

public class AddCountItemDto
{
    [Required]
    public Guid InventoryItemId { get; set; }
    public Guid? LocationId { get; set; }
    public decimal? SystemQuantity { get; set; }
    public string? LotNumber { get; set; }
    public string? SerialNumber { get; set; }
}

public class PhysicalCountFilterDto
{
    public Guid? WarehouseId { get; set; }
    public string? Status { get; set; }
    public string? CountType { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? CountNumber { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public class PhysicalCountExportDto
{
    public Guid Id { get; set; }
    public string CountNumber { get; set; } = string.Empty;
    public string WarehouseName { get; set; } = string.Empty;
    public string CountType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string CountDateFormatted { get; set; } = string.Empty;
    public List<PhysicalCountItemExportDto> Items { get; set; } = new();
}

public class PhysicalCountItemExportDto
{
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string UnitOfMeasure { get; set; } = string.Empty;
    public string? LocationName { get; set; }
    public decimal SystemQuantity { get; set; }
    public decimal CountedQuantity { get; set; }
    public decimal VarianceQuantity { get; set; }
    public decimal VarianceValue { get; set; }
    public string? LotNumber { get; set; }
    public string? SerialNumber { get; set; }
    public bool IsCounted { get; set; }
    public string? Notes { get; set; }
}

public class ImportCountItemDto
{
    public string ItemCode { get; set; } = string.Empty;
    public decimal CountedQuantity { get; set; }
    public string? LotNumber { get; set; }
    public string? SerialNumber { get; set; }
    public string? Notes { get; set; }
}

public class ImportCountResultDto
{
    public int TotalRows { get; set; }
    public int SuccessCount { get; set; }
    public int ErrorCount { get; set; }
    public List<string> Errors { get; set; } = new();
}

public class VarianceReportDto
{
    public string CountNumber { get; set; } = string.Empty;
    public string WarehouseName { get; set; } = string.Empty;
    public string CountType { get; set; } = string.Empty;
    public string CountDateFormatted { get; set; } = string.Empty;
    public int TotalItems { get; set; }
    public int ItemsWithVariance { get; set; }
    public decimal TotalSystemValue { get; set; }
    public decimal TotalCountedValue { get; set; }
    public decimal TotalVarianceValue { get; set; }
    public decimal VariancePercentage { get; set; }
    public List<VarianceItemDto> Items { get; set; } = new();
}

public class VarianceItemDto
{
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? LocationName { get; set; }
    public decimal SystemQuantity { get; set; }
    public decimal CountedQuantity { get; set; }
    public decimal VarianceQuantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal VarianceValue { get; set; }
    public decimal VariancePercent { get; set; }
    public string? Notes { get; set; }
}

#endregion

#region Inventory Valuation DTOs

public class InventoryCostLayerDto
{
    public Guid Id { get; set; }
    public Guid InventoryItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public DateTime LayerDate { get; set; }
    public string ReferenceType { get; set; } = string.Empty;
    public string? ReferenceNumber { get; set; }
    public decimal OriginalQuantity { get; set; }
    public decimal RemainingQuantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalValue { get; set; }
    public string? LotNumber { get; set; }
    public string? SerialNumber { get; set; }
}

public class InventoryValuationSummaryDto
{
    public Guid InventoryItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public decimal TotalQuantity { get; set; }
    public decimal TotalValue { get; set; }
    public decimal AverageCost { get; set; }
    public ValuationMethod ValuationMethod { get; set; }
    public List<InventoryCostLayerDto> CostLayers { get; set; } = new();
}

public class LandedCostDto
{
    public Guid Id { get; set; }
    public string LandedCostNumber { get; set; } = string.Empty;
    public Guid GoodsReceiptNoteId { get; set; }
    public string GRNNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal TotalCostAmount { get; set; }
    public decimal AllocatedAmount { get; set; }
    public string Currency { get; set; } = "USD";
    public string? Notes { get; set; }
    public string CreatedAtFormatted { get; set; } = string.Empty;
}

public class LandedCostDetailDto : LandedCostDto
{
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public List<LandedCostItemDto> CostItems { get; set; } = new();
    public List<LandedCostAllocationDto> Allocations { get; set; } = new();
}

public class LandedCostItemDto
{
    public Guid Id { get; set; }
    public LandedCostType CostType { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public decimal ExchangeRate { get; set; }
    public decimal AmountInBaseCurrency { get; set; }
    public string AllocationMethod { get; set; } = "ByValue";
    public Guid? SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public string? ReferenceNumber { get; set; }
}

public class LandedCostAllocationDto
{
    public Guid Id { get; set; }
    public Guid GRNItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public decimal AllocatedAmount { get; set; }
    public decimal AllocationPercent { get; set; }
    public decimal NewUnitCost { get; set; }
}

public class CreateLandedCostDto
{
    [Required]
    public Guid GoodsReceiptNoteId { get; set; }

    [MaxLength(10)]
    public string Currency { get; set; } = "USD";

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [Required]
    public List<CreateLandedCostItemDto> CostItems { get; set; } = new();
}

public class CreateLandedCostItemDto
{
    [Required]
    public LandedCostType CostType { get; set; }

    [Required]
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }

    [MaxLength(10)]
    public string Currency { get; set; } = "USD";

    [Range(0.000001, double.MaxValue)]
    public decimal ExchangeRate { get; set; } = 1;

    [MaxLength(50)]
    public string AllocationMethod { get; set; } = "ByValue";

    public Guid? SupplierId { get; set; }

    [MaxLength(100)]
    public string? ReferenceNumber { get; set; }
}

public class SetManualLandedCostAllocationsDto
{
    [Required]
    public List<ManualLandedCostAllocationLineDto> Allocations { get; set; } = new();

    [MaxLength(500)]
    public string? Notes { get; set; }
}

public class ManualLandedCostAllocationLineDto
{
    [Required]
    public Guid GoodsReceiptNoteItemId { get; set; }

    [Required]
    [Range(0, double.MaxValue)]
    public decimal AllocatedAmount { get; set; }
}

#endregion

#region Purchase Return DTOs

public class PurchaseReturnDto
{
    public Guid Id { get; set; }
    public string ReturnNumber { get; set; } = string.Empty;
    public DateTime ReturnDate { get; set; }
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public Guid? GoodsReceiptNoteId { get; set; }
    public string? GRNNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public string ReturnReason { get; set; } = string.Empty;
    public int TotalItems { get; set; }
    public decimal TotalQuantity { get; set; }
    public decimal TotalValue { get; set; }
    public string? RequestedByName { get; set; }
    public string? ApprovedByName { get; set; }
    public string? Notes { get; set; }
    public string CreatedAtFormatted { get; set; } = string.Empty;
}

public class PurchaseReturnDetailDto : PurchaseReturnDto
{
    public DateTime? ApprovedDate { get; set; }
    public DateTime? ShippedDate { get; set; }
    public string? TrackingNumber { get; set; }
    public string? CreditNoteNumber { get; set; }
    public decimal? CreditNoteAmount { get; set; }
    public List<PurchaseReturnItemDto> Items { get; set; } = new();
}

public class PurchaseReturnItemDto
{
    public Guid Id { get; set; }
    public Guid InventoryItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public decimal ReturnQuantity { get; set; }
    public string UnitOfMeasure { get; set; } = string.Empty;
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
    public string ReturnReason { get; set; } = string.Empty;
    public string? LotNumber { get; set; }
    public string? SerialNumber { get; set; }
    public Guid? GRNItemId { get; set; }
    public string? Notes { get; set; }
}

public class CreatePurchaseReturnDto
{
    [Required]
    public Guid SupplierId { get; set; }

    [Required]
    public Guid WarehouseId { get; set; }

    public Guid? GoodsReceiptNoteId { get; set; }

    [Required]
    [MaxLength(100)]
    public string ReturnReason { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [Required]
    public List<CreatePurchaseReturnItemDto> Items { get; set; } = new();
}

public class CreatePurchaseReturnItemDto
{
    [Required]
    public Guid InventoryItemId { get; set; }

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal ReturnQuantity { get; set; }

    [Range(0, double.MaxValue)]
    public decimal UnitCost { get; set; }

    [Required]
    [MaxLength(100)]
    public string ReturnReason { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? LotNumber { get; set; }

    [MaxLength(100)]
    public string? SerialNumber { get; set; }

    public Guid? GRNItemId { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

#endregion

#region Inventory Requisition DTOs

public class InventoryRequisitionDto
{
    public Guid Id { get; set; }
    public string RequisitionNumber { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public string? CostCenter { get; set; }
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public Guid? ProjectId { get; set; }
    public string? ProjectCode { get; set; }
    public RequisitionStatus Status { get; set; }
    public RequisitionType RequisitionType { get; set; }
    public string Priority { get; set; } = "Normal";
    public DateTime RequestDate { get; set; }
    public string RequestDateFormatted { get; set; } = string.Empty;
    public DateTime? RequiredDate { get; set; }
    public string? RequiredDateFormatted { get; set; }
    public DateTime? IssuedDate { get; set; }
    public int TotalItems { get; set; }
    public decimal TotalQuantity { get; set; }
    public decimal TotalValue { get; set; }
    public string? RequestedByName { get; set; }
    public string? ApprovedByName { get; set; }
    public string? Notes { get; set; }
    public string? Purpose { get; set; }
    public string CreatedAtFormatted { get; set; } = string.Empty;

    /// <summary>
    /// Convenience field for UX: the current workflow step name when the requisition is pending approval.
    /// </summary>
    public string? CurrentWorkflowStepName { get; set; }
}

public class InventoryRequisitionDetailDto : InventoryRequisitionDto
{
    public DateTime? ApprovalDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public string? IssuedByName { get; set; }
    public string? RejectionReason { get; set; }
    public string? CancellationReason { get; set; }
    public Guid? LocationId { get; set; }
    public string? LocationName { get; set; }
    public List<InventoryRequisitionItemDto> Items { get; set; } = new();
}

public class InventoryRequisitionItemDto
{
    public Guid Id { get; set; }
    public Guid InventoryItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public decimal RequestedQuantity { get; set; }
    public decimal ApprovedQuantity { get; set; }
    public decimal IssuedQuantity { get; set; }
    public string UnitOfMeasure { get; set; } = string.Empty;
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
    public string? LotNumber { get; set; }
    public string? SerialNumber { get; set; }
    public Guid? LocationId { get; set; }
    public string? LocationName { get; set; }
    public string? Notes { get; set; }
}

public class CreateInventoryRequisitionDto
{
    [Required]
    public Guid DepartmentId { get; set; }

    [MaxLength(100)]
    public string? DepartmentName { get; set; }

    [MaxLength(100)]
    public string? CostCenter { get; set; }

    [Required]
    public Guid WarehouseId { get; set; }

    public Guid? LocationId { get; set; }

    public Guid? ProjectId { get; set; }

    [MaxLength(100)]
    public string? ProjectCode { get; set; }

    public RequisitionType RequisitionType { get; set; } = RequisitionType.DepartmentRequisition;

    [MaxLength(20)]
    public string Priority { get; set; } = "Normal";

    public DateTime? RequiredDate { get; set; }

    [MaxLength(2000)]
    public string? Purpose { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [Required]
    public List<CreateInventoryRequisitionItemDto> Items { get; set; } = new();
}

public class UpdateInventoryRequisitionDto
{
    public Guid? DepartmentId { get; set; }

    [MaxLength(100)]
    public string? DepartmentName { get; set; }

    [MaxLength(100)]
    public string? CostCenter { get; set; }

    public Guid? WarehouseId { get; set; }

    public Guid? LocationId { get; set; }

    public Guid? ProjectId { get; set; }

    [MaxLength(100)]
    public string? ProjectCode { get; set; }

    public DateTime? RequiredDate { get; set; }

    [MaxLength(2000)]
    public string? Purpose { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

public class CreateInventoryRequisitionItemDto
{
    [Required]
    public Guid InventoryItemId { get; set; }

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal RequestedQuantity { get; set; }

    public Guid? LocationId { get; set; }

    [MaxLength(100)]
    public string? LotNumber { get; set; }

    [MaxLength(100)]
    public string? SerialNumber { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

public class AddRequisitionItemDto
{
    [Required]
    public Guid InventoryItemId { get; set; }

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal RequestedQuantity { get; set; }

    public Guid? LocationId { get; set; }

    [MaxLength(100)]
    public string? LotNumber { get; set; }

    [MaxLength(100)]
    public string? SerialNumber { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

public class UpdateRequisitionItemDto
{
    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal RequestedQuantity { get; set; }

    public Guid? LocationId { get; set; }

    [MaxLength(100)]
    public string? LotNumber { get; set; }

    [MaxLength(100)]
    public string? SerialNumber { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

public class IssueRequisitionItemDto
{
    [Required]
    public Guid ItemId { get; set; }

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal IssuedQuantity { get; set; }

    public Guid? LocationId { get; set; }

    [MaxLength(100)]
    public string? LotNumber { get; set; }

    [MaxLength(100)]
    public string? SerialNumber { get; set; }
}

public class IssueRequisitionDto
{
    [Required]
    public List<IssueRequisitionItemDto> Items { get; set; } = new();

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

#endregion
