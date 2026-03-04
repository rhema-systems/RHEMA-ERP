using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;

namespace ErpSystem.Core.Entities.Inventory;

#region Core Inventory Management

/// <summary>
/// Represents an inventory item (part, material, component, finished good, etc.)
/// This is the central entity for all inventory management across the ERP system
/// </summary>
public class InventoryItem : TenantEntity
{
    [Required]
    [MaxLength(100)]
    public string ItemCode { get; set; } = string.Empty; // SKU, Part Number, etc.

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    public Guid CategoryId { get; set; }

    [MaxLength(50)]
    public string? Brand { get; set; }

    [MaxLength(50)]
    public string? Manufacturer { get; set; }

    [MaxLength(100)]
    public string? Model { get; set; }

    [MaxLength(20)]
    public string UnitOfMeasure { get; set; } = "EA"; // Each, KG, LB, FT, M, L, GAL, etc.

    // Costing
    [Column(TypeName = "decimal(18,4)")]
    public decimal StandardCost { get; set; } = 0;

    [Column(TypeName = "decimal(18,4)")]
    public decimal AverageCost { get; set; } = 0;

    [Column(TypeName = "decimal(18,4)")]
    public decimal LastPurchaseCost { get; set; } = 0;

    [Column(TypeName = "decimal(18,4)")]
    public decimal SalePrice { get; set; } = 0;

    /// <summary>
    /// Daily rental rate for tools/fixed assets (ItemType = FixedAsset)
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal DailyRentalRate { get; set; } = 0;

    // Stock Management
    public decimal CurrentStock { get; set; } = 0;
    public decimal AvailableStock { get; set; } = 0; // Current - Allocated
    public decimal AllocatedStock { get; set; } = 0; // Reserved for orders/work orders
    public decimal OnOrderStock { get; set; } = 0; // In purchase orders

    // Stock Level Controls
    public decimal MinimumLevel { get; set; } = 0;
    public decimal MaximumLevel { get; set; } = 0;
    public decimal ReorderLevel { get; set; } = 0;
    public decimal ReorderQuantity { get; set; } = 0;
    public decimal SafetyStock { get; set; } = 0;

    // Lead Time Management
    public int LeadTimeDays { get; set; } = 7;
    public int SafetyLeadTimeDays { get; set; } = 0;

    // Item Classification
    public ItemType ItemType { get; set; } = ItemType.StockItem;

    [MaxLength(20)]
    public string ABCClass { get; set; } = "C"; // A (High Value), B (Medium), C (Low Value)

    public ItemStatus Status { get; set; } = ItemStatus.Active;

    // Physical Properties
    public decimal? Weight { get; set; }
    public decimal? Length { get; set; }
    public decimal? Width { get; set; }
    public decimal? Height { get; set; }
    public decimal? Volume { get; set; }

    // Tracking Options
    public bool IsSerialTracked { get; set; } = false;
    public bool IsLotTracked { get; set; } = false;
    public bool IsExpirationTracked { get; set; } = false;
    public bool IsLocationTracked { get; set; } = false;

    // Quality Control
    public bool RequiresInspection { get; set; } = false;
    public int? ShelfLifeDays { get; set; }

    // === ENHANCED FIELDS ===

    // Barcode / QR Code Support
    [MaxLength(100)]
    public string? Barcode { get; set; } // Primary barcode (EAN, UPC, etc.)

    [MaxLength(100)]
    public string? QRCode { get; set; } // QR code value

    [MaxLength(100)]
    public string? AlternateBarcode { get; set; } // Secondary barcode

    // Sub-Category Support
    public Guid? SubCategoryId { get; set; }

    // Description (Short & Long)
    [MaxLength(200)]
    public string? ShortDescription { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? LongDescription { get; set; }

    // Valuation Method
    public ValuationMethod ValuationMethod { get; set; } = ValuationMethod.WeightedAverage;

    /// <summary>
    /// Once true, the valuation method cannot be changed.
    /// Automatically set to true after the first inventory transaction.
    /// </summary>
    public bool IsValuationLocked { get; set; } = false;

    // Base Unit of Measure (links to UnitOfMeasure entity)
    public Guid? BaseUnitOfMeasureId { get; set; }

    // Primary Supplier (links to Supplier entity)
    public Guid? PrimarySupplierId { get; set; }

    // Ordering Settings
    public decimal MinimumOrderQuantity { get; set; } = 1; // MOQ
    public decimal OrderMultiple { get; set; } = 1; // Order in multiples of

    // Default Warehouse
    public Guid? DefaultWarehouseId { get; set; }

    // Image/Media
    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    [MaxLength(500)]
    public string? ThumbnailUrl { get; set; }

    // Tax Settings
    [MaxLength(50)]
    public string? TaxCode { get; set; }

    public bool IsTaxable { get; set; } = true;

    // Procurement Settings
    public bool AutoReorder { get; set; } = false;
    public bool AllowBackorder { get; set; } = false;
    public bool RequireApprovalForPurchase { get; set; } = false;

    // Stock Classification
    [MaxLength(10)]
    public string? XYZClass { get; set; } // X (Consistent), Y (Variable), Z (Erratic)

    [MaxLength(20)]
    public string? MovementClass { get; set; } // Fast, Slow, Non-Moving

    // Country of Origin
    [MaxLength(100)]
    public string? CountryOfOrigin { get; set; }

    [MaxLength(50)]
    public string? HSCode { get; set; } // Harmonized System code for customs

    // === END ENHANCED FIELDS ===

    // === GP-STYLE ADDITIONAL FIELDS ===

    // Additional Descriptions (GP: Generic Description)
    [MaxLength(500)]
    public string? GenericDescription { get; set; }

    // Decimal Precision Settings (GP: Quantity Decimals, Currency Decimals)
    public int QuantityDecimals { get; set; } = 2; // 0-5 decimal places
    public int CurrencyDecimals { get; set; } = 2; // 0-5 decimal places

    // Tax Settings (GP: Purchase Tax Option, Sales Tax Option)
    [MaxLength(50)]
    public string? PurchaseTaxOption { get; set; } // Taxable, Nontaxable, Base on vendor
    [MaxLength(50)]
    public string? SalesTaxOption { get; set; } // Taxable, Nontaxable, Base on customers
    [MaxLength(50)]
    public string? PurchaseTaxScheduleId { get; set; }
    [MaxLength(50)]
    public string? SalesTaxScheduleId { get; set; }

    // Classification (GP: Class ID, Price ID)
    public Guid? ItemClassId { get; set; } // Links to ItemClass entity
    public Guid? PriceGroupId { get; set; } // Links to PriceGroup entity

    // Additional Pricing (GP: Current Cost, List Price)
    [Column(TypeName = "decimal(18,4)")]
    public decimal CurrentCost { get; set; } = 0; // Current actual cost
    [Column(TypeName = "decimal(18,4)")]
    public decimal ListPrice { get; set; } = 0; // List/Retail price

    // Substitute Items (GP: Substitute Item 1, Substitute Item 2)
    public Guid? SubstituteItem1Id { get; set; }
    public Guid? SubstituteItem2Id { get; set; }
    public Guid? SubstituteItem3Id { get; set; }
    public Guid? SubstituteItem4Id { get; set; }

    // Lot Tracking Options (GP: Lot Category, Track)
    [MaxLength(50)]
    public string? LotCategory { get; set; } // Category for lot tracking
    public int MinimumShelfLifeDays { get; set; } = 0;
    public bool WarnBeforeLotExpires { get; set; } = false;
    public int DaysBeforeExpiryWarning { get; set; } = 30;

    // Warranty (GP: Warranty Days)
    public int WarrantyDays { get; set; } = 0;

    // Item Classification Flags (GP: Kit, Kit Component, Finished Good, etc.)
    public bool IsKit { get; set; } = false; // This item is a kit (BOM parent)
    public bool IsKitComponent { get; set; } = false; // This item is part of a kit
    public bool IsFinishedGood { get; set; } = false; // Manufactured finished product
    public bool IsFinishedGoodComponent { get; set; } = false; // Component of finished goods

    // Catalog Inclusion Options (GP: Include in Catalog - Quote, Order, Invoice, Fulfillment)
    public bool IncludeInQuotes { get; set; } = true;
    public bool IncludeInOrders { get; set; } = true;
    public bool IncludeInInvoices { get; set; } = true;
    public bool IncludeInFulfillment { get; set; } = true;
    public bool IsProcurementItem { get; set; } = false; // Procurement item flag

    // Category Attributes (GP: Manufacturer, Style, Feature)
    [MaxLength(100)]
    public string? Style { get; set; }
    [MaxLength(200)]
    public string? Feature { get; set; }

    // History Maintenance Options (GP: Maintain History)
    public bool MaintainCalendarYearHistory { get; set; } = true;
    public bool MaintainFiscalYearHistory { get; set; } = true;
    public bool MaintainTransactionHistory { get; set; } = true;

    // Shipping (GP: Shipping Weight)
    [Column(TypeName = "decimal(18,4)")]
    public decimal ShippingWeight { get; set; } = 0;

    // U of M Schedule (GP: U of M Schedule ID)
    public Guid? UnitOfMeasureScheduleId { get; set; }

    // === END GP-STYLE FIELDS ===

    // Supplier Information (legacy - kept for backward compatibility)
    [MaxLength(200)]
    public string? PrimarySupplier { get; set; }

    [MaxLength(100)]
    public string? SupplierItemCode { get; set; }

    // Dates
    public DateTime? LastStockDate { get; set; }
    public DateTime? LastPurchaseDate { get; set; }
    public DateTime? LastSaleDate { get; set; }
    public DateTime? LastCountDate { get; set; }

    // Custom Fields (JSON)
    [Column(TypeName = "nvarchar(max)")]
    public string? CustomFields { get; set; }

    // Navigation Properties
    public virtual InventoryCategory Category { get; set; } = null!;
    public virtual InventoryCategory? SubCategory { get; set; }
    public virtual UnitOfMeasure? BaseUnitOfMeasureEntity { get; set; }
    public virtual Warehouse? DefaultWarehouse { get; set; }
    public virtual ItemClass? ItemClass { get; set; }
    public virtual PriceGroup? PriceGroup { get; set; }
    public virtual InventoryItem? SubstituteItem1 { get; set; }
    public virtual InventoryItem? SubstituteItem2 { get; set; }
    public virtual InventoryItem? SubstituteItem3 { get; set; }
    public virtual InventoryItem? SubstituteItem4 { get; set; }
    public virtual UnitOfMeasureSchedule? UnitOfMeasureSchedule { get; set; }
    public virtual ICollection<StockMovement> StockMovements { get; set; } = new List<StockMovement>();
    public virtual ICollection<InventoryLocation> InventoryLocations { get; set; } = new List<InventoryLocation>();
    public virtual ICollection<InventoryAllocation> Allocations { get; set; } = new List<InventoryAllocation>();
    public virtual ICollection<StockAdjustment> StockAdjustments { get; set; } = new List<StockAdjustment>();
    public virtual ICollection<ItemUnitOfMeasure> ItemUnitsOfMeasure { get; set; } = new List<ItemUnitOfMeasure>();
    public virtual ICollection<ItemSupplier> ItemSuppliers { get; set; } = new List<ItemSupplier>();
    public virtual ICollection<InventoryCostLayer> CostLayers { get; set; } = new List<InventoryCostLayer>();
    public virtual ICollection<SuggestedSalesItem> SuggestedItems { get; set; } = new List<SuggestedSalesItem>();
}

/// <summary>
/// Hierarchical categorization of inventory items
/// </summary>
public class InventoryCategory : TenantEntity
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public Guid? ParentCategoryId { get; set; }

    [MaxLength(7)] // Hex color code
    public string? Color { get; set; }

    [MaxLength(50)]
    public string? Icon { get; set; }

    public bool IsActive { get; set; } = true;

    // Default settings for items in this category
    [MaxLength(20)]
    public string? DefaultUnitOfMeasure { get; set; }

    public bool DefaultSerialTracking { get; set; } = false;
    public bool DefaultLotTracking { get; set; } = false;
    public bool DefaultRequiresInspection { get; set; } = false;

    // Navigation Properties
    public virtual InventoryCategory? ParentCategory { get; set; }
    public virtual ICollection<InventoryCategory> SubCategories { get; set; } = new List<InventoryCategory>();
    public virtual ICollection<InventoryItem> Items { get; set; } = new List<InventoryItem>();
}

/// <summary>
/// Item Class for grouping similar items with shared settings (GP: Class ID)
/// </summary>
public class ItemClass : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string ClassId { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Description { get; set; } = string.Empty;

    // Default settings for items in this class
    public ItemType DefaultItemType { get; set; } = ItemType.StockItem;
    public ValuationMethod DefaultValuationMethod { get; set; } = ValuationMethod.WeightedAverage;

    [MaxLength(50)]
    public string? DefaultTaxCode { get; set; }

    public bool DefaultIsSerialTracked { get; set; } = false;
    public bool DefaultIsLotTracked { get; set; } = false;
    public bool DefaultRequiresInspection { get; set; } = false;

    public bool IsActive { get; set; } = true;

    // Navigation Properties
    public virtual ICollection<InventoryItem> Items { get; set; } = new List<InventoryItem>();
}

/// <summary>
/// Price Group for grouping items with similar pricing rules (GP: Price ID)
/// </summary>
public class PriceGroup : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string PriceGroupCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(10)]
    public string Currency { get; set; } = "USD";

    // Markup/Margin settings
    public decimal DefaultMarkupPercent { get; set; } = 0;
    public decimal DefaultMarginPercent { get; set; } = 0;

    public bool IsActive { get; set; } = true;

    // Navigation Properties
    public virtual ICollection<InventoryItem> Items { get; set; } = new List<InventoryItem>();
}

/// <summary>
/// Unit of Measure Schedule for defining multiple UOMs per item (GP: U of M Schedule)
/// </summary>
public class UnitOfMeasureSchedule : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string ScheduleId { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Description { get; set; } = string.Empty;

    public Guid BaseUnitOfMeasureId { get; set; }

    /// <summary>
    /// Number of decimal places for quantity display (0-5)
    /// </summary>
    public int QuantityDecimals { get; set; } = 2;

    public bool IsActive { get; set; } = true;

    // Navigation Properties
    public virtual UnitOfMeasure? BaseUnitOfMeasure { get; set; }
    public virtual ICollection<UnitOfMeasureScheduleDetail> Details { get; set; } = new List<UnitOfMeasureScheduleDetail>();
    public virtual ICollection<InventoryItem> Items { get; set; } = new List<InventoryItem>();
}

/// <summary>
/// Detail entries for Unit of Measure Schedule
/// </summary>
public class UnitOfMeasureScheduleDetail : TenantEntity
{
    [Required]
    public Guid ScheduleId { get; set; }

    [Required]
    public Guid UnitOfMeasureId { get; set; }

    // Conversion to base unit (e.g., 1 Case = 12 Each, so BaseQuantity = 12)
    [Column(TypeName = "decimal(18,6)")]
    public decimal BaseQuantity { get; set; } = 1;

    public int SortOrder { get; set; } = 0;

    // Navigation Properties
    public virtual UnitOfMeasureSchedule Schedule { get; set; } = null!;
    public virtual UnitOfMeasure UnitOfMeasure { get; set; } = null!;
}

/// <summary>
/// Suggested Sales Items - items that should be suggested when selling another item (GP: Suggest Sales Item Maintenance)
/// </summary>
public class SuggestedSalesItem : TenantEntity
{
    [Required]
    public Guid InventoryItemId { get; set; } // The main item

    [Required]
    public Guid SuggestedItemId { get; set; } // The suggested item

    [MaxLength(500)]
    public string? Description { get; set; } // Override description for suggestion

    [Column(TypeName = "decimal(18,4)")]
    public decimal SuggestedQuantity { get; set; } = 1;

    // Document type options (when to suggest)
    public bool SuggestOnQuote { get; set; } = true;
    public bool SuggestOnOrder { get; set; } = true;
    public bool SuggestOnInvoice { get; set; } = true;
    public bool SuggestOnFulfillment { get; set; } = false;

    public int SortOrder { get; set; } = 0;
    public bool IsActive { get; set; } = true;

    // Navigation Properties
    public virtual InventoryItem InventoryItem { get; set; } = null!;
    public virtual InventoryItem SuggestedItem { get; set; } = null!;
}

#endregion

#region Stock Movement and Transactions

/// <summary>
/// Records all stock movements (in/out transactions)
/// </summary>
public class StockMovement : TenantEntity
{
    [Required]
    public Guid InventoryItemId { get; set; }

    [Required]
    [MaxLength(50)]
    public string MovementType { get; set; } = string.Empty;
    // Inbound: Receipt, Return, Adjustment+, Transfer-In, Production
    // Outbound: Issue, Sale, Adjustment-, Transfer-Out, Consumption, Waste

    [Required]
    public decimal Quantity { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal UnitCost { get; set; } = 0;

    [Column(TypeName = "decimal(18,4)")]
    public decimal TotalValue { get; set; } = 0;

    public DateTime MovementDate { get; set; } = DateTime.UtcNow;

    [Required]
    public ReferenceType ReferenceType { get; set; } = ReferenceType.Manual;

    [MaxLength(50)]
    public string? ReferenceNumber { get; set; }

    public Guid? ReferenceId { get; set; } // ID of the referenced entity

    /// <summary>
    /// The warehouse where this movement occurred
    /// </summary>
    public Guid? WarehouseId { get; set; }

    /// <summary>
    /// The specific location within the warehouse (optional, for bin-level tracking)
    /// </summary>
    public Guid? LocationId { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    // Tracking Information
    [MaxLength(100)]
    public string? SerialNumber { get; set; }

    [MaxLength(100)]
    public string? LotNumber { get; set; }

    public DateTime? ExpirationDate { get; set; }

    // Balances after this movement
    public decimal RunningBalance { get; set; } = 0;
    public decimal RunningValue { get; set; } = 0;

    // User and approval
    public Guid? ProcessedById { get; set; }
    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedAt { get; set; }

    // Navigation Properties
    public virtual InventoryItem InventoryItem { get; set; } = null!;
    public virtual Warehouse? Warehouse { get; set; }
    public virtual WarehouseLocation? Location { get; set; }
    public virtual ApplicationUser? ProcessedBy { get; set; }
    public virtual ApplicationUser? ApprovedBy { get; set; }
}

/// <summary>
/// Stock adjustments for inventory corrections
/// </summary>
public class StockAdjustment : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string AdjustmentNumber { get; set; } = string.Empty;

    public DateTime AdjustmentDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// The warehouse where the adjustment is being made
    /// </summary>
    public Guid? WarehouseId { get; set; }

    [Required]
    [MaxLength(50)]
    public string ReasonCode { get; set; } = string.Empty; // Cycle Count, Physical Count, Damage, Loss, Found, etc.

    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    /// External reference number (e.g., Physical Count #123)
    /// </summary>
    [MaxLength(100)]
    public string? Reference { get; set; }

    [MaxLength(20)]
    public string Status { get; set; } = "Draft"; // Draft, Approved, Posted, Cancelled

    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedAt { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal TotalAdjustmentValue { get; set; } = 0;

    // Navigation Properties
    public virtual Warehouse? Warehouse { get; set; }
    public virtual ApplicationUser? ApprovedBy { get; set; }
    public virtual ICollection<StockAdjustmentItem> Items { get; set; } = new List<StockAdjustmentItem>();
}

/// <summary>
/// Individual items in a stock adjustment
/// </summary>
public class StockAdjustmentItem : TenantEntity
{
    [Required]
    public Guid AdjustmentId { get; set; }

    [Required]
    public Guid InventoryItemId { get; set; }

    public Guid? LocationId { get; set; }

    [MaxLength(100)]
    public string? SerialNumber { get; set; }

    [MaxLength(100)]
    public string? LotNumber { get; set; }

    public decimal SystemQuantity { get; set; } = 0; // What the system shows
    public decimal PhysicalQuantity { get; set; } = 0; // What was actually counted
    public decimal AdjustmentQuantity { get; set; } = 0; // Difference (Physical - System)

    [Column(TypeName = "decimal(18,4)")]
    public decimal UnitCost { get; set; } = 0;

    [Column(TypeName = "decimal(18,4)")]
    public decimal AdjustmentValue { get; set; } = 0;

    /// <summary>
    /// Item-specific reason for adjustment
    /// </summary>
    [MaxLength(500)]
    public string? Reason { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    // Navigation Properties
    public virtual StockAdjustment Adjustment { get; set; } = null!;
    public virtual InventoryItem InventoryItem { get; set; } = null!;
    public virtual WarehouseLocation? Location { get; set; }
}

#endregion

#region Warehouse and Location Management

/// <summary>
/// Warehouse/Storage facilities
/// </summary>
public class Warehouse : TenantEntity
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

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

    public bool IsActive { get; set; } = true;
    public bool IsDefault { get; set; } = false;

    [MaxLength(20)]
    public string WarehouseType { get; set; } = "Standard"; // Standard, Distribution, Manufacturing, Quarantine

    // === ENHANCED FIELDS ===

    // Capacity Settings
    [Column(TypeName = "decimal(18,2)")]
    public decimal? TotalCapacitySquareFeet { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? UsedCapacitySquareFeet { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? TotalCapacityCubicFeet { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? MaxWeightCapacity { get; set; } // In pounds or kg

    public int? TotalLocations { get; set; }
    public int? UsedLocations { get; set; }

    // Operating Hours
    [MaxLength(500)]
    public string? OperatingHours { get; set; } // JSON or formatted string

    public TimeSpan? OpenTime { get; set; }
    public TimeSpan? CloseTime { get; set; }

    // Special Location Flags
    public bool HasQuarantineArea { get; set; } = false;
    public bool HasInspectionArea { get; set; } = false;
    public bool HasReceivingDock { get; set; } = true;
    public bool HasShippingDock { get; set; } = true;

    /// <summary>
    /// Indicates this warehouse stores consignment stock (vendor-owned stock).
    /// Any issue/transfer/sale/consumption from this warehouse should trigger consignment settlement.
    /// </summary>
    public bool IsConsignmentWarehouse { get; set; } = false;

    // Default Quarantine Location for this warehouse
    public Guid? DefaultQuarantineLocationId { get; set; }
    public Guid? DefaultReceivingLocationId { get; set; }
    public Guid? DefaultShippingLocationId { get; set; }
    public Guid? DefaultInTransitLocationId { get; set; }

    // Temperature Control (for perishables)
    public bool IsTemperatureControlled { get; set; } = false;

    [Column(TypeName = "decimal(5,2)")]
    public decimal? MinTemperature { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal? MaxTemperature { get; set; }

    [MaxLength(20)]
    public string? TemperatureUnit { get; set; } = "F"; // F or C

    // Cost Center / GL Account
    [MaxLength(50)]
    public string? CostCenter { get; set; }

    [MaxLength(50)]
    public string? GLAccountCode { get; set; }

    // Manager
    public Guid? ManagerId { get; set; }

    // === END ENHANCED FIELDS ===

    // Contact Information
    [MaxLength(100)]
    public string? ContactPerson { get; set; }

    [MaxLength(50)]
    public string? Phone { get; set; }

    [MaxLength(100)]
    public string? Email { get; set; }

    // Navigation Properties
    public virtual ApplicationUser? Manager { get; set; }
    public virtual WarehouseLocation? DefaultQuarantineLocation { get; set; }
    public virtual WarehouseLocation? DefaultReceivingLocation { get; set; }
    public virtual WarehouseLocation? DefaultShippingLocation { get; set; }
    public virtual WarehouseLocation? DefaultInTransitLocation { get; set; }
    public virtual ICollection<WarehouseLocation> Locations { get; set; } = new List<WarehouseLocation>();
}

/// <summary>
/// Specific locations within a warehouse (zones, aisles, bins, shelves)
/// </summary>
public class WarehouseLocation : TenantEntity
{
    [Required]
    public Guid WarehouseId { get; set; }

    [Required]
    [MaxLength(100)]
    public string LocationCode { get; set; } = string.Empty; // A-01-001, Zone-Aisle-Bin

    [MaxLength(200)]
    public string? Name { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(50)]
    public string LocationType { get; set; } = "Bin"; // Zone, Aisle, Shelf, Bin, Floor

    public Guid? ParentLocationId { get; set; }

    public bool IsActive { get; set; } = true;
    public bool IsPickingLocation { get; set; } = true;
    public bool IsReceivingLocation { get; set; } = true;

    /// <summary>
    /// When true, inventory stored in this bin/location is treated as consignment stock and is excluded from owned/main inventory totals.
    /// The stock is attributed to <see cref="ConsignmentWarehouseId"/> for warehouse-level reporting and settlement triggers.
    /// </summary>
    public bool IsConsignmentBin { get; set; } = false;

    /// <summary>
    /// The consignment warehouse that "owns" the inventory in this location (optional unless <see cref="IsConsignmentBin"/> is true).
    /// </summary>
    public Guid? ConsignmentWarehouseId { get; set; }

    [NotMapped]
    public Guid InventoryWarehouseId =>
        IsConsignmentBin && ConsignmentWarehouseId.HasValue && ConsignmentWarehouseId.Value != Guid.Empty
            ? ConsignmentWarehouseId.Value
            : WarehouseId;

    // === ENHANCED FIELDS ===

    // Special Location Types
    public bool IsQuarantineLocation { get; set; } = false;
    public bool IsInspectionLocation { get; set; } = false;
    public bool IsInTransitLocation { get; set; } = false;
    public bool IsShippingLocation { get; set; } = false;
    public bool IsStagingLocation { get; set; } = false;
    public bool IsReturnLocation { get; set; } = false;
    public bool IsDamageLocation { get; set; } = false;

    // Location Hierarchy Type (using enum)
    public WarehouseLocationType? LocationHierarchyType { get; set; }

    // Zone Information (for multi-zone warehouses)
    [MaxLength(50)]
    public string? Zone { get; set; }

    [MaxLength(50)]
    public string? Aisle { get; set; }

    [MaxLength(50)]
    public string? Rack { get; set; }

    [MaxLength(50)]
    public string? Shelf { get; set; }

    [MaxLength(50)]
    public string? Bin { get; set; }

    // Physical Position (for warehouse mapping)
    public int? RowNumber { get; set; }
    public int? ColumnNumber { get; set; }
    public int? LevelNumber { get; set; }

    // Picking Sequence (for optimized picking routes)
    public int PickSequence { get; set; } = 0;

    // ABC Classification for location
    [MaxLength(10)]
    public string? ABCClass { get; set; } // A (Fast-moving), B (Medium), C (Slow)

    // Temperature Zone (for controlled storage)
    [MaxLength(50)]
    public string? TemperatureZone { get; set; } // Ambient, Chilled, Frozen, etc.

    // Dedicated Item (if location is dedicated to specific item)
    public Guid? DedicatedItemId { get; set; }

    [MaxLength(100)]
    public string? DedicatedItemCode { get; set; }

    // Barcode/Label
    [MaxLength(100)]
    public string? LocationBarcode { get; set; }

    // === END ENHANCED FIELDS ===

    // Physical Constraints
    public decimal? MaxWeight { get; set; }
    public decimal? MaxVolume { get; set; }
    public int? MaxItems { get; set; }

    // Current Status
    public decimal CurrentWeight { get; set; } = 0;
    public decimal CurrentVolume { get; set; } = 0;
    public int CurrentItemCount { get; set; } = 0;

    // Navigation Properties
    public virtual Warehouse Warehouse { get; set; } = null!;
    public virtual Warehouse? ConsignmentWarehouse { get; set; }
    public virtual WarehouseLocation? ParentLocation { get; set; }
    public virtual InventoryItem? DedicatedItem { get; set; }
    public virtual ICollection<WarehouseLocation> ChildLocations { get; set; } = new List<WarehouseLocation>();
    public virtual ICollection<InventoryLocation> InventoryLocations { get; set; } = new List<InventoryLocation>();
}

/// <summary>
/// Junction table for inventory items and their locations with quantities
/// </summary>
public class InventoryLocation : TenantEntity
{
    [Required]
    public Guid InventoryItemId { get; set; }

    [Required]
    public Guid LocationId { get; set; }

    public decimal Quantity { get; set; } = 0;
    public decimal AllocatedQuantity { get; set; } = 0; // Reserved/allocated stock
    public decimal AvailableQuantity { get; set; } = 0; // Quantity - AllocatedQuantity

    [Column(TypeName = "decimal(18,4)")]
    public decimal AverageCost { get; set; } = 0;

    public DateTime? LastMovementDate { get; set; }
    public DateTime? LastCountDate { get; set; }

    // Cycle Counting
    public DateTime? NextCountDate { get; set; }
    public int CountFrequencyDays { get; set; } = 90; // How often to count this item at this location

    // Navigation Properties
    public virtual InventoryItem InventoryItem { get; set; } = null!;
    public virtual WarehouseLocation Location { get; set; } = null!;
}

#endregion

#region Warehouse Quantities

/// <summary>
/// Tracks inventory quantities at the warehouse level
/// Used for multi-warehouse inventory management
/// </summary>
public class WarehouseQuantity : TenantEntity
{
    [Required]
    public Guid InventoryItemId { get; set; }

    [Required]
    public Guid WarehouseId { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal CurrentStock { get; set; } = 0;

    [Column(TypeName = "decimal(18,4)")]
    public decimal AvailableStock { get; set; } = 0;

    [Column(TypeName = "decimal(18,4)")]
    public decimal AllocatedStock { get; set; } = 0;

    [Column(TypeName = "decimal(18,4)")]
    public decimal ReorderLevel { get; set; } = 0;

    [Column(TypeName = "decimal(18,4)")]
    public decimal MaxStock { get; set; } = 0;

    [Column(TypeName = "decimal(18,4)")]
    public decimal AverageCost { get; set; } = 0;

    public DateTime? LastMovementDate { get; set; }
    public DateTime? LastStockTakeDate { get; set; }
    public DateTime? NextStockTakeDate { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    // Navigation Properties
    public virtual InventoryItem InventoryItem { get; set; } = null!;
    public virtual Warehouse Warehouse { get; set; } = null!;
}

#endregion

#region Inventory Allocation and Reservations

/// <summary>
/// Represents allocated/reserved inventory for specific purposes
/// </summary>
public class InventoryAllocation : TenantEntity
{
    [Required]
    public Guid InventoryItemId { get; set; }

    [Required]
    public Guid WarehouseId { get; set; }

    public Guid? LocationId { get; set; }

    [Required]
    [MaxLength(50)]
    public string AllocationType { get; set; } = string.Empty; // WorkOrder, SalesOrder, TransferOrder, Production

    [MaxLength(50)]
    public string? ReferenceNumber { get; set; }

    public Guid? ReferenceId { get; set; }

    public decimal AllocatedQuantity { get; set; } = 0;
    public decimal ConsumedQuantity { get; set; } = 0;
    public decimal RemainingQuantity { get; set; } = 0;

    public DateTime AllocationDate { get; set; } = DateTime.UtcNow;
    public DateTime? RequiredDate { get; set; }
    public DateTime? ExpirationDate { get; set; }

    [MaxLength(20)]
    public string Status { get; set; } = "Active"; // Active, Consumed, Cancelled, Expired

    [MaxLength(100)]
    public string? SerialNumber { get; set; }

    [MaxLength(100)]
    public string? LotNumber { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public Guid? AllocatedById { get; set; }

    // Navigation Properties
    public virtual InventoryItem InventoryItem { get; set; } = null!;
    public virtual Warehouse Warehouse { get; set; } = null!;
    public virtual WarehouseLocation? Location { get; set; }
    public virtual ApplicationUser? AllocatedBy { get; set; }
}

#endregion

