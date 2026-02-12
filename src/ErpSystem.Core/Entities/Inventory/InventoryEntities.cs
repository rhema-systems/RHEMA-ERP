using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Entities.Finance;

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

    // Tax Assignment
    /// <summary>
    /// Default tax group for this item (overrides category default)
    /// </summary>
    public Guid? DefaultTaxGroupId { get; set; }

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

    // Supplier Information
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
    public virtual ICollection<StockMovement> StockMovements { get; set; } = new List<StockMovement>();
    public virtual ICollection<InventoryLocation> InventoryLocations { get; set; } = new List<InventoryLocation>();
    public virtual ICollection<InventoryAllocation> Allocations { get; set; } = new List<InventoryAllocation>();
    public virtual ICollection<StockAdjustment> StockAdjustments { get; set; } = new List<StockAdjustment>();
    public virtual TaxGroup? DefaultTaxGroup { get; set; }
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

    // Tax Assignment
    /// <summary>
    /// Default tax group for items in this category
    /// </summary>
    public Guid? DefaultTaxGroupId { get; set; }

    // Navigation Properties
    public virtual InventoryCategory? ParentCategory { get; set; }
    public virtual ICollection<InventoryCategory> SubCategories { get; set; } = new List<InventoryCategory>();
    public virtual ICollection<InventoryItem> Items { get; set; } = new List<InventoryItem>();
    public virtual TaxGroup? DefaultTaxGroup { get; set; }
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

    [Required]
    [MaxLength(50)]
    public string ReasonCode { get; set; } = string.Empty; // Cycle Count, Physical Count, Damage, Loss, Found, etc.

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(20)]
    public string Status { get; set; } = "Draft"; // Draft, Approved, Posted

    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedAt { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal TotalAdjustmentValue { get; set; } = 0;

    // Navigation Properties
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

    // Contact Information
    [MaxLength(100)]
    public string? ContactPerson { get; set; }

    [MaxLength(50)]
    public string? Phone { get; set; }

    [MaxLength(100)]
    public string? Email { get; set; }

    // Navigation Properties
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
    public virtual WarehouseLocation? ParentLocation { get; set; }
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

