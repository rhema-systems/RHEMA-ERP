using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Inventory;

#region Inventory Item DTOs

/// <summary>
/// Basic inventory item information for lists and searches
/// </summary>
public class InventoryItemDto
{
    public Guid Id { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ShortDescription { get; set; }
    public string UnitOfMeasure { get; set; } = string.Empty;
    public Guid? UnitOfMeasureScheduleId { get; set; }
    public string? UnitOfMeasureScheduleName { get; set; }
    public decimal CurrentStock { get; set; }
    public decimal AvailableStock { get; set; }
    public decimal StandardCost { get; set; }
    public decimal AverageCost { get; set; }
    public decimal CurrentCost { get; set; }
    public decimal ListPrice { get; set; }
    public bool IsSerialTracked { get; set; }
    public bool IsLotTracked { get; set; }
    public ItemType ItemType { get; set; }
    public ItemStatus Status { get; set; }
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string? ItemClassName { get; set; }
    public string? PriceGroupName { get; set; }
    public bool IsActive { get; set; }
    
    // Valuation
    public ValuationMethod ValuationMethod { get; set; } = ValuationMethod.WeightedAverage;
    public bool IsValuationLocked { get; set; }

    // Media
    public string? ImageUrl { get; set; }
    public string? ThumbnailUrl { get; set; }
}

/// <summary>
/// Detailed inventory item information including locations and movements
/// </summary>
public class InventoryItemDetailDto : InventoryItemDto
{
    // Stock Information
    public decimal AllocatedStock { get; set; }
    public decimal OnOrderStock { get; set; }
    public decimal MinimumLevel { get; set; }
    public decimal MaximumLevel { get; set; }
    public decimal ReorderLevel { get; set; }
    public decimal ReorderQuantity { get; set; }
    public decimal LastPurchaseCost { get; set; }
    public string? PrimarySupplier { get; set; }
    public int LeadTimeDays { get; set; }
    public DateTime? LastStockDate { get; set; }
    public DateTime? LastPurchaseDate { get; set; }

    // GP-Style Additional Fields
    public string? GenericDescription { get; set; }
    public int QuantityDecimals { get; set; }
    public int CurrencyDecimals { get; set; }

    // Tax Settings
    public string? PurchaseTaxOption { get; set; }
    public string? SalesTaxOption { get; set; }
    public string? PurchaseTaxScheduleId { get; set; }
    public string? SalesTaxScheduleId { get; set; }

    // Classification
    public Guid? ItemClassId { get; set; }
    public Guid? PriceGroupId { get; set; }

    // Substitute Items
    public Guid? SubstituteItem1Id { get; set; }
    public string? SubstituteItem1Name { get; set; }
    public Guid? SubstituteItem2Id { get; set; }
    public string? SubstituteItem2Name { get; set; }
    public Guid? SubstituteItem3Id { get; set; }
    public string? SubstituteItem3Name { get; set; }
    public Guid? SubstituteItem4Id { get; set; }
    public string? SubstituteItem4Name { get; set; }

    // Lot Tracking Options
    public string? LotCategory { get; set; }
    public int MinimumShelfLifeDays { get; set; }
    public bool WarnBeforeLotExpires { get; set; }
    public int DaysBeforeExpiryWarning { get; set; }

    // Warranty
    public int WarrantyDays { get; set; }

    // Item Classification Flags
    public bool IsKit { get; set; }
    public bool IsKitComponent { get; set; }
    public bool IsFinishedGood { get; set; }
    public bool IsFinishedGoodComponent { get; set; }

    // Catalog Inclusion Options
    public bool IncludeInQuotes { get; set; }
    public bool IncludeInOrders { get; set; }
    public bool IncludeInInvoices { get; set; }
    public bool IncludeInFulfillment { get; set; }
    public bool IsProcurementItem { get; set; }

    // Category Attributes
    public string? Style { get; set; }
    public string? Feature { get; set; }

    // History Maintenance Options
    public bool MaintainCalendarYearHistory { get; set; }
    public bool MaintainFiscalYearHistory { get; set; }
    public bool MaintainTransactionHistory { get; set; }

    // Shipping
    public decimal ShippingWeight { get; set; }

    // U of M Schedule
    public Guid? UnitOfMeasureScheduleId { get; set; }
    public string? UnitOfMeasureScheduleName { get; set; }

    // Collections
    public List<InventoryLocationDto> Locations { get; set; } = new();
    public List<StockMovementDto> RecentMovements { get; set; } = new();
    public List<SuggestedSalesItemDto> SuggestedItems { get; set; } = new();
}

/// <summary>
/// DTO for creating or updating inventory items
/// </summary>
public class CreateInventoryItemDto
{
    // === BASIC INFO TAB ===
    [Required]
    public string ItemCode { get; set; } = string.Empty;

    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
    public string? ShortDescription { get; set; }
    public string? GenericDescription { get; set; }

    [Required]
    public Guid CategoryId { get; set; }

    public Guid? SubCategoryId { get; set; }
    public Guid? ItemClassId { get; set; }
    public Guid? PriceGroupId { get; set; }

    public string? Brand { get; set; }
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string? Style { get; set; }
    public string? Feature { get; set; }

    [Required]
    public string UnitOfMeasure { get; set; } = "EA";

    public Guid? UnitOfMeasureScheduleId { get; set; }
    public int QuantityDecimals { get; set; } = 2;
    public int CurrencyDecimals { get; set; } = 2;

    public ItemType ItemType { get; set; } = ItemType.StockItem;
    public ItemStatus Status { get; set; } = ItemStatus.Active;
    public ValuationMethod ValuationMethod { get; set; } = ValuationMethod.WeightedAverage;

    // === COSTS & PRICING TAB ===
    public decimal StandardCost { get; set; }
    public decimal CurrentCost { get; set; }
    public decimal ListPrice { get; set; }
    public decimal SalePrice { get; set; }

    // === STOCK SETTINGS TAB ===
    public decimal MinimumLevel { get; set; }
    public decimal MaximumLevel { get; set; }
    public decimal ReorderLevel { get; set; }
    public decimal ReorderQuantity { get; set; }
    public decimal SafetyStock { get; set; }
    public int LeadTimeDays { get; set; } = 7;
    public int SafetyLeadTimeDays { get; set; }
    public bool AllowBackorder { get; set; }
    public bool AutoReorder { get; set; }

    // === TRACKING TAB ===
    public bool IsSerialTracked { get; set; }
    public bool IsLotTracked { get; set; }
    public bool IsExpirationTracked { get; set; }
    public bool IsLocationTracked { get; set; }

    public string? LotCategory { get; set; }
    public int MinimumShelfLifeDays { get; set; }
    public bool WarnBeforeLotExpires { get; set; }
    public int DaysBeforeExpiryWarning { get; set; } = 30;

    // === ITEM OPTIONS TAB ===
    public Guid? SubstituteItem1Id { get; set; }
    public Guid? SubstituteItem2Id { get; set; }
    public Guid? SubstituteItem3Id { get; set; }
    public Guid? SubstituteItem4Id { get; set; }
    public int WarrantyDays { get; set; }

    // Item Classification Flags
    public bool IsKit { get; set; }
    public bool IsKitComponent { get; set; }
    public bool IsFinishedGood { get; set; }
    public bool IsFinishedGoodComponent { get; set; }

    // Catalog Inclusion Options
    public bool IncludeInQuotes { get; set; } = true;
    public bool IncludeInOrders { get; set; } = true;
    public bool IncludeInInvoices { get; set; } = true;
    public bool IncludeInFulfillment { get; set; } = true;
    public bool IsProcurementItem { get; set; }

    // History Maintenance Options
    public bool MaintainCalendarYearHistory { get; set; } = true;
    public bool MaintainFiscalYearHistory { get; set; } = true;
    public bool MaintainTransactionHistory { get; set; } = true;

    // === TAX TAB ===
    public string? TaxCode { get; set; }
    public bool IsTaxable { get; set; } = true;
    public string? PurchaseTaxOption { get; set; }
    public string? SalesTaxOption { get; set; }
    public string? PurchaseTaxScheduleId { get; set; }
    public string? SalesTaxScheduleId { get; set; }

    // === PHYSICAL PROPERTIES TAB ===
    public decimal? Weight { get; set; }
    public decimal ShippingWeight { get; set; }
    public decimal? Length { get; set; }
    public decimal? Width { get; set; }
    public decimal? Height { get; set; }
    public decimal? Volume { get; set; }

    // === SUPPLIER TAB ===
    public Guid? PrimarySupplierId { get; set; }
    public string? PrimarySupplier { get; set; }
    public string? SupplierItemCode { get; set; }
    public Guid? DefaultWarehouseId { get; set; }

    // === MEDIA TAB ===
    public string? ImageUrl { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string? Barcode { get; set; }
    public string? QRCode { get; set; }
    public string? AlternateBarcode { get; set; }

    // === CUSTOMS TAB ===
    public string? CountryOfOrigin { get; set; }
    public string? HSCode { get; set; }
}

/// <summary>
/// DTO for updating inventory items
/// </summary>
public class UpdateInventoryItemDto : CreateInventoryItemDto
{
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// DTO for Suggested Sales Items
/// </summary>
public class SuggestedSalesItemDto
{
    public Guid Id { get; set; }
    public Guid InventoryItemId { get; set; }
    public Guid SuggestedItemId { get; set; }
    public string? SuggestedItemCode { get; set; }
    public string? SuggestedItemName { get; set; }
    public string? Description { get; set; }
    public decimal SuggestedQuantity { get; set; }
    public bool SuggestOnQuote { get; set; }
    public bool SuggestOnOrder { get; set; }
    public bool SuggestOnInvoice { get; set; }
    public bool SuggestOnFulfillment { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// DTO for creating Suggested Sales Items
/// </summary>
public class CreateSuggestedSalesItemDto
{
    [Required]
    public Guid InventoryItemId { get; set; }

    [Required]
    public Guid SuggestedItemId { get; set; }

    public string? Description { get; set; }
    public decimal SuggestedQuantity { get; set; } = 1;
    public bool SuggestOnQuote { get; set; } = true;
    public bool SuggestOnOrder { get; set; } = true;
    public bool SuggestOnInvoice { get; set; } = true;
    public bool SuggestOnFulfillment { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>
/// DTO for Item Class
/// </summary>
public class ItemClassDto
{
    public Guid Id { get; set; }
    public string ClassId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ItemType DefaultItemType { get; set; }
    public string DefaultValuationMethod { get; set; } = string.Empty;
    public string? DefaultTaxCode { get; set; }
    public bool DefaultIsSerialTracked { get; set; }
    public bool DefaultIsLotTracked { get; set; }
    public bool DefaultRequiresInspection { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// DTO for Price Group
/// </summary>
public class PriceGroupDto
{
    public Guid Id { get; set; }
    public string PriceGroupCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Currency { get; set; } = "USD";
    public decimal DefaultMarkupPercent { get; set; }
    public decimal DefaultMarginPercent { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// DTO for Unit of Measure Schedule
/// </summary>
public class UnitOfMeasureScheduleDto
{
    public Guid Id { get; set; }
    public string ScheduleId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid BaseUnitOfMeasureId { get; set; }
    public string? BaseUnitOfMeasureName { get; set; }
    public string? BaseUnitOfMeasureCode { get; set; }
    public int QuantityDecimals { get; set; } = 2;
    public bool IsActive { get; set; }
    public List<UnitOfMeasureScheduleDetailDto> Details { get; set; } = new();
}

/// <summary>
/// DTO for Unit of Measure Schedule Detail
/// </summary>
public class UnitOfMeasureScheduleDetailDto
{
    public Guid Id { get; set; }
    public Guid ScheduleId { get; set; }
    public Guid UnitOfMeasureId { get; set; }
    public string? UnitOfMeasureName { get; set; }
    public string? UnitOfMeasureCode { get; set; }
    public decimal BaseQuantity { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>
/// DTO for creating a Unit of Measure Schedule
/// </summary>
public class CreateUnitOfMeasureScheduleDto
{
    [Required]
    [MaxLength(50)]
    public string ScheduleId { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public Guid BaseUnitOfMeasureId { get; set; }

    [Range(0, 5)]
    public int QuantityDecimals { get; set; } = 2;

    public List<CreateUnitOfMeasureScheduleDetailDto> Details { get; set; } = new();
}

/// <summary>
/// DTO for creating a Unit of Measure Schedule Detail
/// </summary>
public class CreateUnitOfMeasureScheduleDetailDto
{
    [Required]
    public Guid UnitOfMeasureId { get; set; }

    [Required]
    [Range(0.000001, double.MaxValue)]
    public decimal BaseQuantity { get; set; } = 1;

    public int SortOrder { get; set; } = 0;
}

/// <summary>
/// DTO for updating a Unit of Measure Schedule
/// </summary>
public class UpdateUnitOfMeasureScheduleDto
{
    [Required]
    [MaxLength(200)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public Guid BaseUnitOfMeasureId { get; set; }

    [Range(0, 5)]
    public int QuantityDecimals { get; set; } = 2;

    public bool IsActive { get; set; } = true;

    public List<CreateUnitOfMeasureScheduleDetailDto> Details { get; set; } = new();
}

#endregion

#region Stock Management DTOs

/// <summary>
/// Stock movement information
/// </summary>
public class StockMovementDto
{
    public Guid Id { get; set; }
    public string MovementType { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public DateTime MovementDate { get; set; }
    public ReferenceType ReferenceType { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }
    public string? ProcessedBy { get; set; }
    public string? SerialNumber { get; set; }
    public string? LotNumber { get; set; }
    public decimal RunningBalance { get; set; }
}

/// <summary>
/// DTO for creating stock movements
/// </summary>
public class CreateStockMovementDto
{
    [Required]
    public Guid InventoryItemId { get; set; }

    [Required]
    public string MovementType { get; set; } = string.Empty;

    [Required]
    public decimal Quantity { get; set; }

    public decimal UnitCost { get; set; }

    [Required]
    public ReferenceType ReferenceType { get; set; } = ReferenceType.Manual;

    public string? ReferenceNumber { get; set; }
    public Guid? ReferenceId { get; set; }
    public Guid? LocationId { get; set; }
    public string? Notes { get; set; }
    public string? SerialNumber { get; set; }
    public string? LotNumber { get; set; }
    public DateTime? ExpirationDate { get; set; }

    [Required]
    public Guid ProcessedById { get; set; }
}

/// <summary>
/// Stock availability check result
/// </summary>
public class StockAvailabilityDto
{
    public Guid InventoryItemId { get; set; }
    public string? ItemCode { get; set; }
    public string? ItemName { get; set; }
    public decimal RequiredQuantity { get; set; }
    public decimal CurrentStock { get; set; }
    public decimal AvailableQuantity { get; set; }
    public decimal AllocatedQuantity { get; set; }
    public decimal OnOrderQuantity { get; set; }
    public bool IsAvailable { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool ReorderRequired { get; set; }
    public decimal ReorderLevel { get; set; }
    public decimal ReorderQuantity { get; set; }
}

#endregion

#region Allocation DTOs

/// <summary>
/// Inventory allocation information
/// </summary>
public class InventoryAllocationDto
{
    public Guid Id { get; set; }
    public Guid InventoryItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public Guid LocationId { get; set; }
    public string LocationCode { get; set; } = string.Empty;
    public string AllocationType { get; set; } = string.Empty;
    public string? ReferenceNumber { get; set; }
    public decimal AllocatedQuantity { get; set; }
    public decimal ConsumedQuantity { get; set; }
    public decimal RemainingQuantity { get; set; }
    public DateTime AllocationDate { get; set; }
    public DateTime? RequiredDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

/// <summary>
/// DTO for allocating inventory
/// </summary>
public class AllocateInventoryDto
{
    [Required]
    public Guid InventoryItemId { get; set; }

    [Required]
    public decimal Quantity { get; set; }

    [Required]
    public string ReferenceNumber { get; set; } = string.Empty;

    public string? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }
    public DateTime? RequiredDate { get; set; }
    public string? Notes { get; set; }

    [Required]
    public Guid UserId { get; set; }
}

#endregion

#region Location DTOs

/// <summary>
/// Inventory location information (item quantities at specific warehouse locations)
/// </summary>
public class InventoryLocationDto
{
    public Guid LocationId { get; set; }
    public string LocationCode { get; set; } = string.Empty;
    public string? LocationName { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal AvailableQuantity { get; set; }
    public decimal AllocatedQuantity { get; set; }
    public DateTime? LastMovementDate { get; set; }
    public DateTime? LastCountDate { get; set; }
}

/// <summary>
/// Bin-level stock snapshot (InventoryItem x WarehouseLocation).
/// Backed by InventoryLocations (operational bin quantities).
/// </summary>
public class BinStockDto
{
    public Guid InventoryItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? UnitOfMeasure { get; set; }

    public Guid WarehouseId { get; set; }
    public string WarehouseCode { get; set; } = string.Empty;
    public string WarehouseName { get; set; } = string.Empty;

    public Guid LocationId { get; set; }
    public string LocationCode { get; set; } = string.Empty;

    public decimal Quantity { get; set; }
    public decimal AvailableQuantity { get; set; }
    public decimal AllocatedQuantity { get; set; }
    public decimal AverageCost { get; set; }
    public DateTime? LastMovementDate { get; set; }
}

/// <summary>
/// Warehouse information
/// </summary>
public class WarehouseDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }
    public string? Country { get; set; }
    public bool IsActive { get; set; }
    public bool IsDefault { get; set; }
    public string WarehouseType { get; set; } = string.Empty;
    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
}

/// <summary>
/// Warehouse location information
/// </summary>
public class WarehouseLocationDto
{
    public Guid Id { get; set; }
    public Guid WarehouseId { get; set; }
    public string LocationCode { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string LocationType { get; set; } = string.Empty;
    public Guid? ParentLocationId { get; set; }
    public bool IsActive { get; set; }
    public bool IsPickingLocation { get; set; }
    public bool IsReceivingLocation { get; set; }
    public decimal? MaxWeight { get; set; }
    public decimal? MaxVolume { get; set; }
    public int? MaxItems { get; set; }
    public decimal CurrentWeight { get; set; }
    public decimal CurrentVolume { get; set; }
    public int CurrentItemCount { get; set; }
}

/// <summary>
/// DTO for creating a warehouse
/// </summary>
public class CreateWarehouseDto
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(500)]
    public string? Address { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    [MaxLength(50)]
    public string? State { get; set; }

    [MaxLength(20)]
    public string? ZipCode { get; set; }

    [MaxLength(50)]
    public string? Country { get; set; }

    [MaxLength(50)]
    public string WarehouseType { get; set; } = "Standard";

    [MaxLength(100)]
    public string? ContactPerson { get; set; }

    [MaxLength(50)]
    public string? Phone { get; set; }

    [MaxLength(100)]
    public string? Email { get; set; }

    public bool IsDefault { get; set; } = false;
}

/// <summary>
/// DTO for updating a warehouse
/// </summary>
public class UpdateWarehouseDto : CreateWarehouseDto
{
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// DTO for creating a warehouse location
/// </summary>
public class CreateWarehouseLocationDto
{
    [Required]
    public Guid WarehouseId { get; set; }

    [Required]
    [MaxLength(50)]
    public string LocationCode { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Name { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(50)]
    public string LocationType { get; set; } = "Bin";

    public Guid? ParentLocationId { get; set; }

    public bool IsPickingLocation { get; set; } = true;
    public bool IsReceivingLocation { get; set; } = true;

    public decimal? MaxWeight { get; set; }
    public decimal? MaxVolume { get; set; }
    public int? MaxItems { get; set; }
}

/// <summary>
/// DTO for updating a warehouse location
/// </summary>
public class UpdateWarehouseLocationDto : CreateWarehouseLocationDto
{
    public bool IsActive { get; set; } = true;
}

#endregion

#region Purchase Order DTOs

/// <summary>
/// Purchase order summary for lists
/// </summary>
public class PurchaseOrderSummaryDto
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public DateTime? RequiredDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public int ItemCount { get; set; }
}

/// <summary>
/// Detailed purchase order information
/// </summary>
public class PurchaseOrderDetailDto : PurchaseOrderSummaryDto
{
    public string? SupplierAddress { get; set; }
    public string? ContactPerson { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactEmail { get; set; }
    public DateTime? PromisedDate { get; set; }
    public DateTime? ReceivedDate { get; set; }
    public string? RequestedByName { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public decimal SubTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal ShippingCost { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? PaymentTerms { get; set; }
    public string? ShippingTerms { get; set; }
    public string? Notes { get; set; }
    public string? DeliveryAddress { get; set; }
    public List<PurchaseOrderItemDto> Items { get; set; } = new();
}

/// <summary>
/// Purchase order item information
/// </summary>
public class PurchaseOrderItemDto
{
    public Guid Id { get; set; }
    public Guid InventoryItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? SupplierItemCode { get; set; }
    public decimal OrderedQuantity { get; set; }
    public decimal ReceivedQuantity { get; set; }
    public decimal RemainingQuantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Basic purchase order DTO
/// </summary>
public class PurchaseOrderDto
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
}

/// <summary>
/// DTO for creating purchase orders
/// </summary>
public class CreatePurchaseOrderDto
{
    [Required]
    public string SupplierName { get; set; } = string.Empty;

    public string? SupplierAddress { get; set; }
    public string? ContactPerson { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactEmail { get; set; }
    public DateTime? RequiredDate { get; set; }
    public string? PaymentTerms { get; set; }
    public string? ShippingTerms { get; set; }
    public string? Notes { get; set; }
    public Guid? DeliveryWarehouseId { get; set; }
    public string? DeliveryAddress { get; set; }

    [Required]
    public Guid RequestedById { get; set; }

    [Required]
    public List<CreatePurchaseOrderItemDto> Items { get; set; } = new();
}

/// <summary>
/// DTO for creating purchase order items
/// </summary>
public class CreatePurchaseOrderItemDto
{
    [Required]
    public Guid InventoryItemId { get; set; }

    public string? SupplierItemCode { get; set; }

    [Required]
    public decimal OrderedQuantity { get; set; }

    [Required]
    public decimal UnitPrice { get; set; }

    public DateTime? ExpectedDeliveryDate { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Purchase order receipt information
/// </summary>
public class PurchaseOrderReceiptDto
{
    public Guid Id { get; set; }
    public Guid PurchaseOrderId { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;
    public DateTime ReceiptDate { get; set; }
    public string? DeliveryNote { get; set; }
    public string? CarrierName { get; set; }
    public string? TrackingNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ReceivedByName { get; set; }
    public string? InspectedByName { get; set; }
    public string? Notes { get; set; }
    public List<PurchaseOrderReceiptItemDto> Items { get; set; } = new();
}

/// <summary>
/// Purchase order receipt item information
/// </summary>
public class PurchaseOrderReceiptItemDto
{
    public Guid Id { get; set; }
    public Guid PurchaseOrderItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public decimal ReceivedQuantity { get; set; }
    public decimal AcceptedQuantity { get; set; }
    public decimal RejectedQuantity { get; set; }
    public string? LocationCode { get; set; }
    public string? SerialNumber { get; set; }
    public string? LotNumber { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public string? Notes { get; set; }
    public string? RejectionReason { get; set; }
}

/// <summary>
/// DTO for receiving purchase orders
/// </summary>
public class ReceivePurchaseOrderDto
{
    [Required]
    public Guid PurchaseOrderId { get; set; }

    public string? DeliveryNote { get; set; }
    public string? CarrierName { get; set; }
    public string? TrackingNumber { get; set; }

    [Required]
    public Guid ReceivedById { get; set; }

    public Guid? InspectedById { get; set; }
    public string? Notes { get; set; }

    [Required]
    public List<ReceivePurchaseOrderItemDto> Items { get; set; } = new();
}

/// <summary>
/// DTO for receiving individual purchase order items
/// </summary>
public class ReceivePurchaseOrderItemDto
{
    [Required]
    public Guid PurchaseOrderItemId { get; set; }

    [Required]
    public decimal ReceivedQuantity { get; set; }

    public decimal AcceptedQuantity { get; set; }
    public decimal RejectedQuantity { get; set; }
    public Guid? LocationId { get; set; }
    public string? SerialNumber { get; set; }
    public string? LotNumber { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public string? Notes { get; set; }
    public string? RejectionReason { get; set; }
}

#endregion

#region Reorder DTOs

/// <summary>
/// Items requiring reorder information
/// </summary>
public class ReorderRequiredDto
{
    public Guid InventoryItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public decimal CurrentStock { get; set; }
    public decimal AvailableStock { get; set; }
    public decimal AllocatedStock { get; set; }
    public decimal ReorderLevel { get; set; }
    public decimal ReorderQuantity { get; set; }
    public decimal RecommendedOrderQuantity { get; set; }
    public string? PrimarySupplier { get; set; }
    public int LeadTimeDays { get; set; }
    public DateTime? LastPurchaseDate { get; set; }
}

#endregion

#region Stock Adjustment DTOs

/// <summary>
/// Stock adjustment summary for lists
/// </summary>
public class StockAdjustmentDto
{
    public Guid Id { get; set; }
    public string AdjustmentNumber { get; set; } = string.Empty;
    public DateTime AdjustmentDate { get; set; }
    public Guid? WarehouseId { get; set; }
    public string? WarehouseName { get; set; }
    public string ReasonCode { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Reference { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal TotalAdjustmentValue { get; set; }
    public int ItemCount { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedByName { get; set; }
}

/// <summary>
/// Detailed stock adjustment information
/// </summary>
public class StockAdjustmentDetailDto : StockAdjustmentDto
{
    public List<StockAdjustmentItemDto> Items { get; set; } = new();
}

/// <summary>
/// Stock adjustment item information
/// </summary>
public class StockAdjustmentItemDto
{
    public Guid Id { get; set; }
    public Guid AdjustmentId { get; set; }
    public Guid InventoryItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? CategoryName { get; set; }
    public string UnitOfMeasure { get; set; } = string.Empty;
    public Guid? LocationId { get; set; }
    public string? LocationCode { get; set; }
    public string? WarehouseName { get; set; }
    public string? SerialNumber { get; set; }
    public string? LotNumber { get; set; }
    public decimal SystemQuantity { get; set; }
    public decimal PhysicalQuantity { get; set; }
    public decimal AdjustmentQuantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal AdjustmentValue { get; set; }
    /// <summary>
    /// Total value of the adjustment (UnitCost * |AdjustmentQuantity|)
    /// </summary>
    public decimal TotalValue { get; set; }
    /// <summary>
    /// Quantity before adjustment
    /// </summary>
    public decimal PreviousQuantity { get; set; }
    /// <summary>
    /// Quantity after adjustment (PreviousQuantity + AdjustmentQuantity)
    /// </summary>
    public decimal NewQuantity { get; set; }
    /// <summary>
    /// Item-specific reason for adjustment
    /// </summary>
    public string? Reason { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// DTO for creating stock adjustments
/// </summary>
public class CreateStockAdjustmentDto
{
    /// <summary>
    /// The warehouse where the adjustment is being made
    /// </summary>
    public Guid? WarehouseId { get; set; }

    [Required]
    [MaxLength(50)]
    public string ReasonCode { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    /// External reference number (e.g., Physical Count #123)
    /// </summary>
    [MaxLength(100)]
    public string? Reference { get; set; }

    public DateTime? AdjustmentDate { get; set; }

    [Required]
    public List<CreateStockAdjustmentItemDto> Items { get; set; } = new();
}

/// <summary>
/// DTO for creating stock adjustment items
/// </summary>
public class CreateStockAdjustmentItemDto
{
    [Required]
    public Guid InventoryItemId { get; set; }

    public Guid? LocationId { get; set; }

    [MaxLength(100)]
    public string? SerialNumber { get; set; }

    [MaxLength(100)]
    public string? LotNumber { get; set; }

    /// <summary>
    /// The adjustment quantity (positive for increase, negative for decrease)
    /// </summary>
    [Required]
    public decimal AdjustmentQuantity { get; set; }

    public decimal? UnitCost { get; set; }

    /// <summary>
    /// Item-specific reason for adjustment
    /// </summary>
    [MaxLength(500)]
    public string? Reason { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

/// <summary>
/// DTO for updating stock adjustments
/// </summary>
public class UpdateStockAdjustmentDto
{
    [Required]
    [MaxLength(50)]
    public string ReasonCode { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    /// External reference number (e.g., Physical Count #123)
    /// </summary>
    [MaxLength(100)]
    public string? Reference { get; set; }

    public DateTime? AdjustmentDate { get; set; }

    public List<CreateStockAdjustmentItemDto>? Items { get; set; }
}

/// <summary>
/// Common reason codes for stock adjustments
/// </summary>
public static class StockAdjustmentReasonCodes
{
    public const string CycleCount = "CYCLE_COUNT";
    public const string PhysicalCount = "PHYSICAL_COUNT";
    public const string Damage = "DAMAGE";
    public const string Loss = "LOSS";
    public const string Found = "FOUND";
    public const string Theft = "THEFT";
    public const string Expired = "EXPIRED";
    public const string QualityIssue = "QUALITY_ISSUE";
    public const string Donation = "DONATION";
    public const string WriteOff = "WRITE_OFF";
    public const string PositiveAdjustment = "POSITIVE_ADJUSTMENT";
    public const string NegativeAdjustment = "NEGATIVE_ADJUSTMENT";
    public const string InitialStock = "INITIAL_STOCK";
    public const string Other = "OTHER";

    public static readonly Dictionary<string, string> ReasonCodeDescriptions = new()
    {
        { CycleCount, "Cycle Count Adjustment" },
        { PhysicalCount, "Physical Count Adjustment" },
        { Damage, "Damaged Goods" },
        { Loss, "Lost Inventory" },
        { Found, "Found Inventory" },
        { Theft, "Theft/Shrinkage" },
        { Expired, "Expired Items" },
        { QualityIssue, "Quality Issue" },
        { Donation, "Donation" },
        { WriteOff, "Write Off" },
        { PositiveAdjustment, "Positive Adjustment (Increase)" },
        { NegativeAdjustment, "Negative Adjustment (Decrease)" },
        { InitialStock, "Initial Stock Entry" },
        { Other, "Other" }
    };
}

#endregion
