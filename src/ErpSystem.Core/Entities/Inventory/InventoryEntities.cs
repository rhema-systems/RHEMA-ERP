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

    /// <summary>
    /// Tenant-unique primary machine-readable item identifier.
    /// Values are normalized by the shared inventory identifier service before persistence.
    /// </summary>
    [MaxLength(100)]
    public string? Barcode { get; set; }

    /// <summary>
    /// Optional tenant-unique secondary barcode for the same stocking item.
    /// </summary>
    [MaxLength(100)]
    public string? AlternateBarcode { get; set; }

    /// <summary>
    /// Optional tenant-unique QR payload used to resolve the item.
    /// </summary>
    [MaxLength(100)]
    public string? QRCode { get; set; }

    [MaxLength(20)]
    public string UnitOfMeasure { get; set; } = "EA"; // Each, KG, LB, FT, M, L, GAL, etc.

    public Guid? UnitOfMeasureScheduleId { get; set; }
    public virtual UnitOfMeasureSchedule? UnitOfMeasureSchedule { get; set; }

    public ValuationMethod ValuationMethod { get; set; } = ValuationMethod.WeightedAverage;
    public bool IsValuationLocked { get; set; }
    public decimal DailyRentalRate { get; set; }

    /// <summary>
    /// Declares whether the item may be selected against project-controlled demand.
    /// This is master-data applicability only; Projects remains the project owner.
    /// </summary>
    public bool IsProjectApplicable { get; set; }

    /// <summary>
    /// Declares whether the item may be selected against a Finance cost-centre segment.
    /// This does not create or own Finance segment values.
    /// </summary>
    public bool IsCostCentreApplicable { get; set; }

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

    // Optional GL defaults; posting services fall back to Finance settings when unset.
    public Guid? InventoryAccountId { get; set; }
    public Guid? InventoryDisposalAccountId { get; set; }
    public Guid? InventoryOffsetAccountId { get; set; }
    public Guid? CostOfGoodsSoldAccountId { get; set; }
    public Guid? SalesAccountId { get; set; }
    public Guid? MarkdownsAccountId { get; set; }
    public Guid? SalesReturnsAccountId { get; set; }
    public Guid? InUseAccountId { get; set; }
    public Guid? InServiceAccountId { get; set; }
    public Guid? DamagedAccountId { get; set; }
    public Guid? VarianceAccountId { get; set; }
    public Guid? DropShipItemsAccountId { get; set; }
    public Guid? PurchasePriceVarianceAccountId { get; set; }
    public Guid? UnrealisedPurchasePriceVarianceAccountId { get; set; }
    public Guid? InventoryReturnsAccountId { get; set; }
    public Guid? AssemblyVarianceAccountId { get; set; }
    public Guid? StandardCostRevaluationAccountId { get; set; }

    // Physical Properties
    public decimal ShippingWeight { get; set; }
    [Column(TypeName = "decimal(22,6)")]
    public decimal? Weight { get; set; }
    [MaxLength(2)]
    public string? WeightUnit { get; set; }
    public decimal? Length { get; set; }
    public decimal? Width { get; set; }
    public decimal? Height { get; set; }
    public decimal? Volume { get; set; }

    // Tracking Options
    public bool IsSerialTracked { get; set; } = false;
    public bool IsLotTracked { get; set; } = false;
    public bool IsBatchTracked { get; set; } = false;
    public bool IsManufactureDateTracked { get; set; } = false;
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
    public virtual ICollection<ItemUnitOfMeasure> ItemUnitsOfMeasure { get; set; } = new List<ItemUnitOfMeasure>();
    public virtual TaxGroup? DefaultTaxGroup { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
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
    public bool DefaultBatchTracking { get; set; } = false;
    public bool DefaultManufactureDateTracking { get; set; } = false;
    public bool DefaultExpirationTracking { get; set; } = false;
    public bool EnforceFifoIssue { get; set; } = false;
    public int MinimumShelfLifeDays { get; set; } = 0;
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
    public Guid? TransferDispatchAllocationId { get; set; }
    public Guid? TransferReceiptAllocationId { get; set; }
    [MaxLength(20)] public string? TransferLeg { get; set; }
    public InventoryTransferDispatchAllocation? TransferDispatchAllocation { get; set; }
    public InventoryTransferReceiptAllocation? TransferReceiptAllocation { get; set; }
    [Required]
    public Guid InventoryItemId { get; set; }

    [Required]
    public Guid WarehouseId { get; set; }

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

    [MaxLength(100)]
    public string? BatchNumber { get; set; }

    public DateTime? ManufactureDate { get; set; }

    public DateTime? ExpirationDate { get; set; }

    public Guid? InventoryTrackingExceptionId { get; set; }

    public Guid? InventoryIssueVoucherId { get; set; }
    public Guid? InventoryReturnVoucherId { get; set; }

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
    public virtual InventoryIssueVoucher? InventoryIssueVoucher { get; set; }
    public virtual InventoryReturnVoucher? InventoryReturnVoucher { get; set; }
}

/// <summary>
/// Stock adjustments for inventory corrections
/// </summary>
public class StockAdjustment : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string AdjustmentNumber { get; set; } = string.Empty;

    [Required]
    public Guid WarehouseId { get; set; }

    [MaxLength(50)]
    public string Reference { get; set; } = string.Empty;

    public DateTime AdjustmentDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Finance owns accounting-book configuration. Inventory persists the explicit book selected
    /// for a governed opening-stock schedule so Finance can validate and post that same evidence.
    /// Ordinary stock adjustments retain IFRS as their established default.
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string BookClassification { get; set; } = "IFRS";

    [Required]
    [MaxLength(50)]
    public string ReasonCode { get; set; } = string.Empty; // Cycle Count, Physical Count, Damage, Loss, Found, etc.

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(20)]
    public string Status { get; set; } = "Draft"; // Draft, Approved, Posted

    // Server-owned submission snapshot. Existing records retain their approval obligations.
    public bool ApprovalRequired { get; set; } = true;

    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedAt { get; set; }

    public Guid RequestedById { get; set; }
    public Guid? SubmittedById { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public Guid? RejectedById { get; set; }
    public DateTime? RejectedAtUtc { get; set; }
    [MaxLength(1000)] public string? RejectionReason { get; set; }
    public Guid? PostedById { get; set; }
    public DateTime? PostedAtUtc { get; set; }
    public Guid? ReversedById { get; set; }
    public DateTime? ReversedAtUtc { get; set; }
    [MaxLength(1000)] public string? ReversalReason { get; set; }
    public Guid? RelatedIssueVoucherId { get; set; }
    public Guid? FinancePostingEventId { get; set; }
    public Guid? FinanceJournalEntryId { get; set; }
    public Guid? ReversalFinancePostingEventId { get; set; }
    public Guid? ReversalFinanceJournalEntryId { get; set; }
    [MaxLength(100)] public string? IdempotencyKey { get; set; }
    [MaxLength(64)] public string? PayloadHash { get; set; }
    [MaxLength(100)] public string? CorrelationId { get; set; }
    [MaxLength(64)] public string? IntegrityHash { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    [Column(TypeName = "decimal(18,4)")]
    public decimal TotalAdjustmentValue { get; set; } = 0;

    // Navigation Properties
    public virtual Warehouse? Warehouse { get; set; }
    public virtual ApplicationUser? ApprovedBy { get; set; }
    public virtual ICollection<StockAdjustmentItem> Items { get; set; } = new List<StockAdjustmentItem>();
    public virtual ICollection<StockAdjustmentEvidence> Evidence { get; set; } = new List<StockAdjustmentEvidence>();
    public virtual ICollection<StockAdjustmentAction> Actions { get; set; } = new List<StockAdjustmentAction>();
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

    [MaxLength(100)]
    public string? BatchNumber { get; set; }

    public DateTime? ManufactureDate { get; set; }

    public DateTime? ExpiryDate { get; set; }

    public decimal SystemQuantity { get; set; } = 0; // What the system shows
    public decimal PhysicalQuantity { get; set; } = 0; // What was actually counted
    public decimal AdjustmentQuantity { get; set; } = 0; // Difference (Physical - System)

    [Column(TypeName = "decimal(18,4)")]
    public decimal UnitCost { get; set; } = 0;

    [Column(TypeName = "decimal(18,4)")]
    public decimal AdjustmentValue { get; set; } = 0;

    [MaxLength(1000)]
    public string? Notes { get; set; }

    [MaxLength(500)]
    public string? Reason { get; set; }

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
    public bool IsConsignmentWarehouse { get; set; } = false;

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

    /// <summary>The warehouse's normal fallback bin for newly assigned items.</summary>
    public bool IsDefault { get; set; }
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

    // Governed project/requisition reservation lineage (TDC-0611).
    public Guid? InventoryRequisitionId { get; set; }
    public Guid? InventoryRequisitionItemId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? DepartmentId { get; set; }
    public Guid? OrganizationUnitId { get; set; }
    public Guid? SubstitutedFromAllocationId { get; set; }

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

    [MaxLength(100)]
    public string? IdempotencyKey { get; set; }

    [MaxLength(64)]
    public string? PayloadHash { get; set; }

    [MaxLength(100)]
    public string? CorrelationId { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    // Navigation Properties
    public virtual InventoryItem InventoryItem { get; set; } = null!;
    public virtual Warehouse Warehouse { get; set; } = null!;
    public virtual WarehouseLocation? Location { get; set; }
    public virtual ApplicationUser? AllocatedBy { get; set; }
    public virtual InventoryRequisition? InventoryRequisition { get; set; }
    public virtual InventoryRequisitionItem? InventoryRequisitionItem { get; set; }
    public virtual ErpSystem.Core.Entities.Projects.Project? Project { get; set; }
    public virtual InventoryAllocation? SubstitutedFromAllocation { get; set; }
    public virtual InventoryAllocation? SubstitutedByAllocation { get; set; }
    public virtual ICollection<InventoryProjectReservationAction> ProjectReservationActions { get; set; } = new List<InventoryProjectReservationAction>();
}

#endregion

#region Unit of Measure Schedules

/// <summary>
/// Unit of Measure Schedule - groups UOM conversions for inventory items
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

    public int QuantityDecimals { get; set; } = 2;

    public bool IsActive { get; set; } = true;

    // Navigation Properties
    public virtual UnitOfMeasure? BaseUnitOfMeasure { get; set; }
    public virtual ICollection<UnitOfMeasureScheduleDetail> Details { get; set; } = new List<UnitOfMeasureScheduleDetail>();
    public virtual ICollection<InventoryItem> Items { get; set; } = new List<InventoryItem>();
}

/// <summary>
/// Detail line for UOM Schedule - defines conversion to base unit
/// </summary>
public class UnitOfMeasureScheduleDetail : TenantEntity
{
    [Required]
    public Guid ScheduleId { get; set; }

    [Required]
    public Guid UnitOfMeasureId { get; set; }

    [Column(TypeName = "decimal(18,6)")]
    public decimal BaseQuantity { get; set; } = 1;

    public int SortOrder { get; set; } = 0;

    // Navigation Properties
    public virtual UnitOfMeasureSchedule Schedule { get; set; } = null!;
    public virtual UnitOfMeasure UnitOfMeasure { get; set; } = null!;
}

#endregion

