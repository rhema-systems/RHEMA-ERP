using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Inventory;

#region Unit of Measure

/// <summary>
/// Unit of Measure entity for inventory items
/// Supports base and alternate UOMs with conversion factors
/// </summary>
public class UnitOfMeasure : TenantEntity
{
    [Required]
    [MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(50)]
    public string Category { get; set; } = "Quantity"; // Quantity, Weight, Volume, Length, Area

    [MaxLength(10)]
    public string? Symbol { get; set; }

    public bool IsBaseUnit { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; } = 0;

    // Navigation Properties
    public virtual ICollection<UnitOfMeasureConversion> ConversionsFrom { get; set; } = new List<UnitOfMeasureConversion>();
    public virtual ICollection<UnitOfMeasureConversion> ConversionsTo { get; set; } = new List<UnitOfMeasureConversion>();
    public virtual ICollection<ItemUnitOfMeasure> ItemUnits { get; set; } = new List<ItemUnitOfMeasure>();
}

/// <summary>
/// Conversion factors between units of measure
/// </summary>
public class UnitOfMeasureConversion : TenantEntity
{
    [Required]
    public Guid FromUnitId { get; set; }

    [Required]
    public Guid ToUnitId { get; set; }

    [Column(TypeName = "decimal(18,8)")]
    public decimal ConversionFactor { get; set; } = 1;

    public bool IsActive { get; set; } = true;

    // Navigation Properties
    public virtual UnitOfMeasure FromUnit { get; set; } = null!;
    public virtual UnitOfMeasure ToUnit { get; set; } = null!;
}

/// <summary>
/// Item-specific unit of measure settings with conversion to base unit
/// </summary>
public class ItemUnitOfMeasure : TenantEntity
{
    [Required]
    public Guid InventoryItemId { get; set; }

    [Required]
    public Guid UnitOfMeasureId { get; set; }

    [Column(TypeName = "decimal(18,8)")]
    public decimal ConversionToBase { get; set; } = 1;

    public bool IsBaseUnit { get; set; } = false;
    public bool IsPurchaseUnit { get; set; } = false;
    public bool IsSalesUnit { get; set; } = false;
    public bool IsStockingUnit { get; set; } = false;

    [MaxLength(100)]
    public string? Barcode { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal? DefaultPrice { get; set; }

    public bool IsActive { get; set; } = true;

    // Navigation Properties
    public virtual InventoryItem InventoryItem { get; set; } = null!;
    public virtual UnitOfMeasure UnitOfMeasure { get; set; } = null!;
}

#endregion

#region Item Supplier Relationship

/// <summary>
/// Links inventory items to multiple suppliers with pricing, MOQ, and lead time
/// </summary>
public class ItemSupplier : TenantEntity
{
    [Required]
    public Guid InventoryItemId { get; set; }

    [Required]
    public Guid SupplierId { get; set; }

    [MaxLength(100)]
    public string? SupplierItemCode { get; set; }

    [MaxLength(200)]
    public string? SupplierItemName { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal UnitPrice { get; set; } = 0;

    [MaxLength(10)]
    public string Currency { get; set; } = "USD";

    public DateTime? PriceEffectiveDate { get; set; }
    public DateTime? PriceExpiryDate { get; set; }

    public decimal MinimumOrderQuantity { get; set; } = 1;
    public decimal OrderMultiple { get; set; } = 1;
    public int LeadTimeDays { get; set; } = 7;

    public bool IsPreferred { get; set; } = false;
    public bool IsActive { get; set; } = true;
    public int Priority { get; set; } = 0;

    [MaxLength(1000)]
    public string? Notes { get; set; }

    // Navigation Properties
    public virtual InventoryItem InventoryItem { get; set; } = null!;
}

#endregion

#region Goods Receipt Note (GRN)

/// <summary>
/// Goods Receipt Note - formal record of goods received against a PO
/// Supports inspection workflow and auto stock update
/// </summary>
public class GoodsReceiptNote : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string GRNNumber { get; set; } = string.Empty;

    /// <summary>
    /// Link back to the Procurement receipt (PurchaseOrderReceipt) that produced this GRN snapshot.
    /// This allows inventory workflows (like landed cost) to be accessed from the Procurement receipt UI
    /// even when Inventory GRNs use their own IDs.
    /// </summary>
    public Guid? PurchaseOrderReceiptId { get; set; }

    public Guid? PurchaseOrderId { get; set; }

    [MaxLength(50)]
    public string? PurchaseOrderNumber { get; set; }

    public Guid? SupplierId { get; set; }

    [MaxLength(200)]
    public string? SupplierName { get; set; }

    public DateTime ReceiptDate { get; set; } = DateTime.UtcNow;

    [Required]
    public Guid WarehouseId { get; set; }

    public Guid? ReceivingLocationId { get; set; }

    // Delivery Information
    [MaxLength(100)]
    public string? DeliveryNoteNumber { get; set; }

    [MaxLength(100)]
    public string? CarrierName { get; set; }

    [MaxLength(100)]
    public string? TrackingNumber { get; set; }

    [MaxLength(100)]
    public string? VehicleNumber { get; set; }

    [MaxLength(100)]
    public string? DriverName { get; set; }

    // Status
    public GRNStatus Status { get; set; } = GRNStatus.Draft;

    public bool RequiresInspection { get; set; } = false;

    public Guid? ReceivedById { get; set; }
    public Guid? InspectedById { get; set; }
    public DateTime? InspectionDate { get; set; }

    public InspectionResult InspectionResult { get; set; } = InspectionResult.Pending;

    [MaxLength(2000)]
    public string? InspectionNotes { get; set; }

    // Totals
    public int TotalItems { get; set; } = 0;

    [Column(TypeName = "decimal(18,4)")]
    public decimal TotalQuantityReceived { get; set; } = 0;

    [Column(TypeName = "decimal(18,4)")]
    public decimal TotalQuantityAccepted { get; set; } = 0;

    [Column(TypeName = "decimal(18,4)")]
    public decimal TotalQuantityRejected { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalValue { get; set; } = 0;

    [MaxLength(2000)]
    public string? Notes { get; set; }

    // Governed source/capacity snapshot (TDC-0501)
    [MaxLength(100)]
    public string? IdempotencyKey { get; set; }

    [MaxLength(64)]
    public string? IdempotencyRequestHash { get; set; }

    [MaxLength(100)]
    public string? CorrelationId { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal ReceiptTolerancePercent { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? ReceiptSourceSnapshotJson { get; set; }

    [MaxLength(64)]
    public string? ReceiptSourceIntegrityHash { get; set; }

    public DateTime? ReceiptSourceValidatedAtUtc { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public bool StockUpdated { get; set; } = false;
    public DateTime? StockUpdatedAt { get; set; }

    // Navigation Properties
    public virtual Warehouse Warehouse { get; set; } = null!;
    public virtual WarehouseLocation? ReceivingLocation { get; set; }
    public virtual ApplicationUser? ReceivedBy { get; set; }
    public virtual ApplicationUser? InspectedBy { get; set; }
    public virtual ICollection<GoodsReceiptNoteItem> Items { get; set; } = new List<GoodsReceiptNoteItem>();
}

/// <summary>
/// Individual line items on a Goods Receipt Note
/// </summary>
public class GoodsReceiptNoteItem : TenantEntity
{
    [Required]
    public Guid GoodsReceiptNoteId { get; set; }

    [Required]
    public Guid InventoryItemId { get; set; }

    public Guid? PurchaseOrderItemId { get; set; }

    [MaxLength(100)]
    public string? ItemCode { get; set; }

    [MaxLength(200)]
    public string? ItemName { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal OrderedQuantity { get; set; } = 0;

    [Column(TypeName = "decimal(18,4)")]
    public decimal ReceivedQuantity { get; set; } = 0;

    [Column(TypeName = "decimal(18,4)")]
    public decimal PreviouslyReceiptedQuantitySnapshot { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal ToleranceQuantitySnapshot { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal MaximumReceivableQuantitySnapshot { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal RemainingQuantityBeforeReceiptSnapshot { get; set; }

    [MaxLength(64)]
    public string? ReceiptLineIntegrityHash { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal AcceptedQuantity { get; set; } = 0;

    [Column(TypeName = "decimal(18,4)")]
    public decimal RejectedQuantity { get; set; } = 0;

    [Column(TypeName = "decimal(18,4)")]
    public decimal UnitCost { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal LineValue { get; set; } = 0;

    [MaxLength(20)]
    public string? UnitOfMeasure { get; set; }

    // Storage Location
    public Guid? StorageLocationId { get; set; }

    // Tracking
    [MaxLength(100)]
    public string? SerialNumber { get; set; }

    [MaxLength(100)]
    public string? LotNumber { get; set; }

    [MaxLength(100)]
    public string? BatchNumber { get; set; }

    public DateTime? ManufactureDate { get; set; }
    public DateTime? ExpiryDate { get; set; }

    // Inspection
    public InspectionResult InspectionResult { get; set; } = InspectionResult.Pending;

    [MaxLength(1000)]
    public string? InspectionNotes { get; set; }

    [MaxLength(1000)]
    public string? RejectionReason { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    // Navigation Properties
    public virtual GoodsReceiptNote GoodsReceiptNote { get; set; } = null!;
    public virtual InventoryItem InventoryItem { get; set; } = null!;
    public virtual WarehouseLocation? StorageLocation { get; set; }
}

#endregion

#region Inventory Transfer

/// <summary>
/// Inter-warehouse or inter-location transfer of inventory
/// </summary>
public class InventoryTransfer : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string TransferNumber { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Description { get; set; }

    // Source
    [Required]
    public Guid SourceWarehouseId { get; set; }

    public Guid? SourceLocationId { get; set; }

    // Destination
    [Required]
    public Guid DestinationWarehouseId { get; set; }

    public Guid? DestinationLocationId { get; set; }

    // In-Transit Location (virtual)
    public Guid? InTransitLocationId { get; set; }

    // Dates
    public DateTime RequestDate { get; set; } = DateTime.UtcNow;
    public DateTime? RequiredDate { get; set; }
    public DateTime? ShippedDate { get; set; }
    public DateTime? ReceivedDate { get; set; }
    public DateTime? CompletedDate { get; set; }

    // Status
    public TransferStatus Status { get; set; } = TransferStatus.Draft;

    // Personnel
    public Guid? RequestedById { get; set; }
    public Guid? ApprovedById { get; set; }
    public Guid? ShippedById { get; set; }
    public Guid? ReceivedById { get; set; }

    public DateTime? ApprovalDate { get; set; }

    // Shipping Info
    [MaxLength(100)]
    public string? CarrierName { get; set; }

    [MaxLength(100)]
    public string? TrackingNumber { get; set; }

    [MaxLength(100)]
    public string? VehicleNumber { get; set; }

    // Totals
    public int TotalItems { get; set; } = 0;

    [Column(TypeName = "decimal(18,4)")]
    public decimal TotalQuantity { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalValue { get; set; } = 0;

    // Shipping Costs
    [Column(TypeName = "decimal(18,2)")]
    public decimal ShippingCost { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal MiscellaneousCost { get; set; } = 0;

    [MaxLength(500)]
    public string? MiscellaneousCostDescription { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAdditionalCost { get; set; } = 0; // ShippingCost + MiscellaneousCost

    /// <summary>
    /// How to allocate additional costs: SpreadToItemCost or GLExpense
    /// </summary>
    [MaxLength(50)]
    public string CostAllocationMethod { get; set; } = "SpreadToItemCost";

    /// <summary>
    /// Basis used when spreading costs to item landed cost.
    /// Supported values: Value, Weight, Quantity.
    /// </summary>
    [MaxLength(50)]
    public string CostApportionmentBasis { get; set; } = "Value";

    /// <summary>
    /// GL Account for expense posting when CostAllocationMethod is GLExpense
    /// </summary>
    [MaxLength(50)]
    public string? ExpenseGLAccount { get; set; }

    /// <summary>
    /// Whether the additional costs have been allocated/posted
    /// </summary>
    public bool CostsAllocated { get; set; } = false;

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [MaxLength(500)]
    public string? CancellationReason { get; set; }

    // Navigation Properties
    public virtual Warehouse SourceWarehouse { get; set; } = null!;
    public virtual WarehouseLocation? SourceLocation { get; set; }
    public virtual Warehouse DestinationWarehouse { get; set; } = null!;
    public virtual WarehouseLocation? DestinationLocation { get; set; }
    public virtual WarehouseLocation? InTransitLocation { get; set; }
    public virtual ApplicationUser? RequestedBy { get; set; }
    public virtual ApplicationUser? ApprovedBy { get; set; }
    public virtual ApplicationUser? ShippedBy { get; set; }
    public virtual ApplicationUser? ReceivedBy { get; set; }
    public virtual ICollection<InventoryTransferItem> Items { get; set; } = new List<InventoryTransferItem>();
}

/// <summary>
/// Line items for inventory transfer
/// </summary>
public class InventoryTransferItem : TenantEntity
{
    [Required]
    public Guid InventoryTransferId { get; set; }

    [Required]
    public Guid InventoryItemId { get; set; }

    [MaxLength(100)]
    public string? ItemCode { get; set; }

    [MaxLength(200)]
    public string? ItemName { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal RequestedQuantity { get; set; } = 0;

    [Column(TypeName = "decimal(18,4)")]
    public decimal ShippedQuantity { get; set; } = 0;

    [Column(TypeName = "decimal(18,4)")]
    public decimal ReceivedQuantity { get; set; } = 0;

    [Column(TypeName = "decimal(18,4)")]
    public decimal DamagedQuantity { get; set; } = 0;

    [Column(TypeName = "decimal(18,4)")]
    public decimal UnitCost { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal LineValue { get; set; } = 0;

    /// <summary>
    /// Allocated additional cost (shipping + misc) per unit
    /// </summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal AllocatedCostPerUnit { get; set; } = 0;

    /// <summary>
    /// Total allocated additional cost for this line item
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAllocatedCost { get; set; } = 0;

    /// <summary>
    /// Final unit cost including allocated costs (UnitCost + AllocatedCostPerUnit)
    /// </summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal LandedUnitCost { get; set; } = 0;

    [MaxLength(20)]
    public string? UnitOfMeasure { get; set; }

    // Source Location
    public Guid? SourceLocationId { get; set; }

    // Destination Location
    public Guid? DestinationLocationId { get; set; }

    // Tracking
    [MaxLength(100)]
    public string? SerialNumber { get; set; }

    [MaxLength(100)]
    public string? LotNumber { get; set; }

    [MaxLength(100)]
    public string? BatchNumber { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    [MaxLength(500)]
    public string? DamageNotes { get; set; }

    // Navigation Properties
    public virtual InventoryTransfer InventoryTransfer { get; set; } = null!;
    public virtual InventoryItem InventoryItem { get; set; } = null!;
    public virtual WarehouseLocation? SourceLocation { get; set; }
    public virtual WarehouseLocation? DestinationLocation { get; set; }
}

#endregion

#region Physical Count / Cycle Count

/// <summary>
/// Physical inventory count header - supports full counts and cycle counts
/// </summary>
public class PhysicalCount : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string CountNumber { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Description { get; set; }

    public CountType CountType { get; set; } = CountType.FullCount;

    [Required]
    public Guid WarehouseId { get; set; }

    public Guid? LocationId { get; set; } // Optional: specific location for cycle count

    public Guid? CategoryId { get; set; } // Optional: specific category for cycle count

    // Dates
    public DateTime CountDate { get; set; } = DateTime.UtcNow;
    public DateTime? StartedDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public DateTime? PostedDate { get; set; }

    // Status: Draft, InProgress, PendingApproval, Approved, Posted, Cancelled
    [MaxLength(50)]
    public string Status { get; set; } = "Draft";

    // Options
    public bool FreezeInventory { get; set; } = false;
    public bool IncludeZeroStock { get; set; } = false;
    public bool BlindCount { get; set; } = false; // Hide system qty from counters

    // Personnel
    public Guid? InitiatedById { get; set; }
    public Guid? CountedById { get; set; }
    public Guid? ApprovedById { get; set; }
    public Guid? PostedById { get; set; }

    // Summary
    public int TotalItems { get; set; } = 0;
    public int CountedItems { get; set; } = 0;
    public int VarianceItems { get; set; } = 0;

    [Column(TypeName = "decimal(18,4)")]
    public decimal TotalSystemQuantity { get; set; } = 0;

    [Column(TypeName = "decimal(18,4)")]
    public decimal TotalCountedQuantity { get; set; } = 0;

    [Column(TypeName = "decimal(18,4)")]
    public decimal TotalVarianceQuantity { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalVarianceValue { get; set; } = 0;

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [MaxLength(500)]
    public string? CancellationReason { get; set; }

    // Navigation Properties
    public virtual Warehouse Warehouse { get; set; } = null!;
    public virtual WarehouseLocation? Location { get; set; }
    public virtual InventoryCategory? Category { get; set; }
    public virtual ApplicationUser? InitiatedBy { get; set; }
    public virtual ApplicationUser? CountedBy { get; set; }
    public virtual ApplicationUser? ApprovedBy { get; set; }
    public virtual ApplicationUser? PostedBy { get; set; }
    public virtual ICollection<PhysicalCountItem> Items { get; set; } = new List<PhysicalCountItem>();
}

/// <summary>
/// Individual line items for physical count
/// </summary>
public class PhysicalCountItem : TenantEntity
{
    [Required]
    public Guid PhysicalCountId { get; set; }

    [Required]
    public Guid InventoryItemId { get; set; }

    public Guid? LocationId { get; set; }

    [MaxLength(100)]
    public string? ItemCode { get; set; }

    [MaxLength(200)]
    public string? ItemName { get; set; }

    [MaxLength(20)]
    public string? UnitOfMeasure { get; set; }

    // Quantities
    [Column(TypeName = "decimal(18,4)")]
    public decimal SystemQuantity { get; set; } = 0;

    [Column(TypeName = "decimal(18,4)")]
    public decimal CountedQuantity { get; set; } = 0;

    [Column(TypeName = "decimal(18,4)")]
    public decimal VarianceQuantity { get; set; } = 0; // Counted - System

    // Costing
    [Column(TypeName = "decimal(18,4)")]
    public decimal UnitCost { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal VarianceValue { get; set; } = 0;

    // Status
    public bool IsCounted { get; set; } = false;
    public DateTime? CountedAt { get; set; }
    public Guid? CountedById { get; set; }

    // Recount tracking
    public int CountAttempts { get; set; } = 0;
    public bool RequiresRecount { get; set; } = false;

    // Tracking (if lot/serial tracked)
    [MaxLength(100)]
    public string? LotNumber { get; set; }

    [MaxLength(100)]
    public string? SerialNumber { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    [MaxLength(500)]
    public string? VarianceReason { get; set; }

    // Navigation Properties
    public virtual PhysicalCount PhysicalCount { get; set; } = null!;
    public virtual InventoryItem InventoryItem { get; set; } = null!;
    public virtual WarehouseLocation? Location { get; set; }
    public virtual ApplicationUser? CountedBy { get; set; }
}

#endregion

#region Inventory Valuation (FIFO/LIFO Cost Layers)

/// <summary>
/// Inventory cost layer for FIFO/LIFO valuation tracking
/// Each layer represents a batch of inventory at a specific cost
/// </summary>
public class InventoryCostLayer : TenantEntity
{
    [Required]
    public Guid InventoryItemId { get; set; }

    [Required]
    public Guid WarehouseId { get; set; }

    public Guid? LocationId { get; set; }

    // Layer identification
    [Required]
    [MaxLength(50)]
    public string LayerNumber { get; set; } = string.Empty;

    public DateTime LayerDate { get; set; } = DateTime.UtcNow;

    // Source reference
    [MaxLength(50)]
    public string? SourceType { get; set; } // GRN, Transfer, Opening, Adjustment

    [MaxLength(50)]
    public string? SourceReference { get; set; }

    public Guid? SourceId { get; set; }

    // Quantities
    [Column(TypeName = "decimal(18,4)")]
    public decimal OriginalQuantity { get; set; } = 0;

    [Column(TypeName = "decimal(18,4)")]
    public decimal RemainingQuantity { get; set; } = 0;

    [Column(TypeName = "decimal(18,4)")]
    public decimal ConsumedQuantity { get; set; } = 0;

    // Costs
    [Column(TypeName = "decimal(18,4)")]
    public decimal UnitCost { get; set; } = 0;

    [Column(TypeName = "decimal(18,4)")]
    public decimal LandedCostPerUnit { get; set; } = 0;

    [Column(TypeName = "decimal(18,4)")]
    public decimal TotalUnitCost { get; set; } = 0; // UnitCost + LandedCostPerUnit

    [Column(TypeName = "decimal(18,2)")]
    public decimal RemainingValue { get; set; } = 0;

    // Tracking
    [MaxLength(100)]
    public string? LotNumber { get; set; }

    [MaxLength(100)]
    public string? BatchNumber { get; set; }

    public DateTime? ExpiryDate { get; set; }

    public bool IsFullyConsumed { get; set; } = false;
    public bool IsActive { get; set; } = true;

    // Navigation Properties
    public virtual InventoryItem InventoryItem { get; set; } = null!;
    public virtual Warehouse Warehouse { get; set; } = null!;
    public virtual WarehouseLocation? Location { get; set; }
}

/// <summary>
/// Landed cost header - additional costs allocated to inventory receipt
/// </summary>
public class LandedCost : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string LandedCostNumber { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Description { get; set; }

    /// <summary>
    /// Document currency for totals and allocations (typically GRN/PO currency).
    /// </summary>
    [MaxLength(10)]
    public string Currency { get; set; } = "USD";

    // Source Reference (GRN, PO, etc.)
    [Required]
    public Guid GoodsReceiptNoteId { get; set; }

    [MaxLength(50)]
    public string? GRNNumber { get; set; }

    public DateTime CostDate { get; set; } = DateTime.UtcNow;

    // Status: Draft, PendingApproval, Approved, Posted, Cancelled
    [MaxLength(50)]
    public string Status { get; set; } = "Draft";

    // Totals
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalCost { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal AllocatedAmount { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal UnallocatedAmount { get; set; } = 0;

    // Vendor info (for cost invoice)
    public Guid? VendorId { get; set; }

    [MaxLength(200)]
    public string? VendorName { get; set; }

    [MaxLength(100)]
    public string? InvoiceNumber { get; set; }

    public DateTime? InvoiceDate { get; set; }

    // Approval
    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public Guid? PostedById { get; set; }
    public DateTime? PostedDate { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    // Navigation Properties
    public virtual GoodsReceiptNote GoodsReceiptNote { get; set; } = null!;
    public virtual ApplicationUser? ApprovedBy { get; set; }
    public virtual ApplicationUser? PostedBy { get; set; }
    public virtual ICollection<LandedCostItem> Items { get; set; } = new List<LandedCostItem>();
    public virtual ICollection<LandedCostAllocation> Allocations { get; set; } = new List<LandedCostAllocation>();
}

/// <summary>
/// Individual cost line items (freight, customs, insurance, etc.)
/// </summary>
public class LandedCostItem : TenantEntity
{
    [Required]
    public Guid LandedCostId { get; set; }

    public LandedCostType CostType { get; set; }

    [MaxLength(200)]
    public string? Description { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; } = 0;

    [MaxLength(10)]
    public string Currency { get; set; } = "USD";

    /// <summary>
    /// Exchange rate to the parent LandedCost.Currency.
    /// </summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal ExchangeRate { get; set; } = 1;

    /// <summary>
    /// Amount converted into the parent LandedCost.Currency.
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal AmountInBaseCurrency { get; set; } = 0;

    // Allocation method per cost line: ByValue, ByQuantity, ByWeight, ByVolume, Equal, Manual
    [MaxLength(50)]
    public string AllocationMethod { get; set; } = "ByValue";

    // Service supplier (can differ per cost line)
    public Guid? SupplierId { get; set; }

    [MaxLength(200)]
    public string? SupplierName { get; set; }

    [MaxLength(100)]
    public string? ReferenceNumber { get; set; }

    [MaxLength(100)]
    public string? InvoiceNumber { get; set; }

    public DateTime? InvoiceDate { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    // Navigation Properties
    public virtual LandedCost LandedCost { get; set; } = null!;
}

/// <summary>
/// Allocation of landed costs to GRN line items
/// </summary>
public class LandedCostAllocation : TenantEntity
{
    [Required]
    public Guid LandedCostId { get; set; }

    [Required]
    public Guid LandedCostItemId { get; set; }

    [Required]
    public Guid GoodsReceiptNoteItemId { get; set; }

    [Required]
    public Guid InventoryItemId { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal Quantity { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal AllocatedAmount { get; set; } = 0;

    [Column(TypeName = "decimal(18,4)")]
    public decimal CostPerUnit { get; set; } = 0;

    // Cost layer reference (for updating valuation)
    public Guid? CostLayerId { get; set; }

    /// <summary>
    /// Link to the valuation FIFO layer (InventoryLayers) that this allocation was applied to (if applicable).
    /// This is used for audit/reversal in the new valuation engine.
    /// </summary>
    public Guid? InventoryLayerId { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    // Navigation Properties
    public virtual LandedCost LandedCost { get; set; } = null!;
    public virtual LandedCostItem LandedCostItem { get; set; } = null!;
    public virtual GoodsReceiptNoteItem GoodsReceiptNoteItem { get; set; } = null!;
    public virtual InventoryItem InventoryItem { get; set; } = null!;
    public virtual InventoryCostLayer? CostLayer { get; set; }
    public virtual InventoryLayer? InventoryLayer { get; set; }
}

#endregion

#region Purchase Return

/// <summary>
/// Return of goods to supplier
/// </summary>
public class PurchaseReturn : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string ReturnNumber { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Description { get; set; }

    // Source
    public Guid? GoodsReceiptNoteId { get; set; }

    [MaxLength(50)]
    public string? GRNNumber { get; set; }

    public Guid? PurchaseOrderId { get; set; }

    [MaxLength(50)]
    public string? PurchaseOrderNumber { get; set; }

    [Required]
    public Guid SupplierId { get; set; }

    [MaxLength(200)]
    public string? SupplierName { get; set; }

    [Required]
    public Guid WarehouseId { get; set; }

    // Dates
    public DateTime ReturnDate { get; set; } = DateTime.UtcNow;
    public DateTime? ShippedDate { get; set; }
    public DateTime? AcknowledgedDate { get; set; }

    // Return Reason
    [MaxLength(50)]
    public string ReturnReason { get; set; } = "Quality"; // Quality, Damage, Excess, Wrong, Other

    [MaxLength(2000)]
    public string? ReturnReasonDetails { get; set; }

    // Status: Draft, Approved, Shipped, Acknowledged, Completed, Cancelled
    [MaxLength(50)]
    public string Status { get; set; } = "Draft";

    // Personnel
    public Guid? RequestedById { get; set; }
    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedDate { get; set; }

    // Shipping
    [MaxLength(100)]
    public string? CarrierName { get; set; }

    [MaxLength(100)]
    public string? TrackingNumber { get; set; }

    // Debit Note
    public bool DebitNoteRequired { get; set; } = true;

    [MaxLength(50)]
    public string? DebitNoteNumber { get; set; }

    public DateTime? DebitNoteDate { get; set; }

    // Totals
    public int TotalItems { get; set; } = 0;

    [Column(TypeName = "decimal(18,4)")]
    public decimal TotalQuantity { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalValue { get; set; } = 0;

    // Credit/Refund
    [MaxLength(50)]
    public string? CreditNoteNumber { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CreditAmount { get; set; } = 0;

    public bool RefundReceived { get; set; } = false;
    public DateTime? RefundDate { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    // Navigation Properties
    public virtual GoodsReceiptNote? GoodsReceiptNote { get; set; }
    public virtual Warehouse Warehouse { get; set; } = null!;
    public virtual ApplicationUser? RequestedBy { get; set; }
    public virtual ApplicationUser? ApprovedBy { get; set; }
    public virtual ICollection<PurchaseReturnItem> Items { get; set; } = new List<PurchaseReturnItem>();
}

/// <summary>
/// Line items for purchase return
/// </summary>
public class PurchaseReturnItem : TenantEntity
{
    [Required]
    public Guid PurchaseReturnId { get; set; }

    [Required]
    public Guid InventoryItemId { get; set; }

    public Guid? GoodsReceiptNoteItemId { get; set; }

    [MaxLength(100)]
    public string? ItemCode { get; set; }

    [MaxLength(200)]
    public string? ItemName { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal ReceivedQuantity { get; set; } = 0; // Original received qty

    [Column(TypeName = "decimal(18,4)")]
    public decimal ReturnQuantity { get; set; } = 0;

    [Column(TypeName = "decimal(18,4)")]
    public decimal UnitCost { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal LineValue { get; set; } = 0;

    [MaxLength(20)]
    public string? UnitOfMeasure { get; set; }

    // Storage Location
    public Guid? LocationId { get; set; }

    // Tracking
    [MaxLength(100)]
    public string? SerialNumber { get; set; }

    [MaxLength(100)]
    public string? LotNumber { get; set; }

    [MaxLength(100)]
    public string? BatchNumber { get; set; }

    // Return reason
    [MaxLength(50)]
    public string? ReturnReason { get; set; }

    [MaxLength(1000)]
    public string? ReturnReasonDetails { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    // Stock reversal
    public bool StockReversed { get; set; } = false;
    public DateTime? StockReversedAt { get; set; }

    // Navigation Properties
    public virtual PurchaseReturn PurchaseReturn { get; set; } = null!;
    public virtual InventoryItem InventoryItem { get; set; } = null!;
    public virtual GoodsReceiptNoteItem? GoodsReceiptNoteItem { get; set; }
    public virtual WarehouseLocation? Location { get; set; }
}

#endregion

#region Inventory Requisition

/// <summary>
/// Department/Project requisition for issuing inventory items
/// </summary>
public class InventoryRequisition : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string RequisitionNumber { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Description { get; set; }

    // Requesting Department/Cost Center
    [Required]
    public Guid DepartmentId { get; set; }

    [MaxLength(100)]
    public string? DepartmentName { get; set; }

    [MaxLength(100)]
    public string? CostCenter { get; set; }

    // Source Warehouse
    [Required]
    public Guid WarehouseId { get; set; }

    public Guid? LocationId { get; set; }

    // Project Reference (optional)
    public Guid? ProjectId { get; set; }

    [MaxLength(100)]
    public string? ProjectCode { get; set; }

    // Dates
    public DateTime RequestDate { get; set; } = DateTime.UtcNow;
    public DateTime? RequiredDate { get; set; }
    public DateTime? ApprovalDate { get; set; }
    public DateTime? IssuedDate { get; set; }
    public DateTime? CompletedDate { get; set; }

    // Status
    public RequisitionStatus Status { get; set; } = RequisitionStatus.Draft;
    public RequisitionType RequisitionType { get; set; } = RequisitionType.DepartmentRequisition;

    [MaxLength(20)]
    public string Priority { get; set; } = "Normal";

    // Personnel
    public Guid? RequestedById { get; set; }
    public Guid? ApprovedById { get; set; }
    public Guid? IssuedById { get; set; }

    // Totals
    public int TotalItems { get; set; } = 0;

    [Column(TypeName = "decimal(18,4)")]
    public decimal TotalQuantity { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalValue { get; set; } = 0;

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [MaxLength(2000)]
    public string? Purpose { get; set; }

    [MaxLength(500)]
    public string? RejectionReason { get; set; }

    [MaxLength(500)]
    public string? CancellationReason { get; set; }

    // Navigation Properties
    public virtual Warehouse Warehouse { get; set; } = null!;
    public virtual WarehouseLocation? Location { get; set; }
    public virtual ApplicationUser? RequestedBy { get; set; }
    public virtual ApplicationUser? ApprovedBy { get; set; }
    public virtual ApplicationUser? IssuedBy { get; set; }
    public virtual ICollection<InventoryRequisitionItem> Items { get; set; } = new List<InventoryRequisitionItem>();
}

/// <summary>
/// Line items for inventory requisition
/// </summary>
public class InventoryRequisitionItem : TenantEntity
{
    [Required]
    public Guid InventoryRequisitionId { get; set; }

    [Required]
    public Guid InventoryItemId { get; set; }

    [MaxLength(100)]
    public string? ItemCode { get; set; }

    [MaxLength(200)]
    public string? ItemName { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal RequestedQuantity { get; set; } = 0;

    [Column(TypeName = "decimal(18,4)")]
    public decimal ApprovedQuantity { get; set; } = 0;

    [Column(TypeName = "decimal(18,4)")]
    public decimal IssuedQuantity { get; set; } = 0;

    [Column(TypeName = "decimal(18,4)")]
    public decimal UnitCost { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal LineValue { get; set; } = 0;

    [MaxLength(20)]
    public string? UnitOfMeasure { get; set; }

    // Location
    public Guid? LocationId { get; set; }

    // Tracking
    [MaxLength(100)]
    public string? SerialNumber { get; set; }

    [MaxLength(100)]
    public string? LotNumber { get; set; }

    [MaxLength(100)]
    public string? BatchNumber { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    // Navigation Properties
    public virtual InventoryRequisition InventoryRequisition { get; set; } = null!;
    public virtual InventoryItem InventoryItem { get; set; } = null!;
    public virtual WarehouseLocation? Location { get; set; }
}

#endregion
