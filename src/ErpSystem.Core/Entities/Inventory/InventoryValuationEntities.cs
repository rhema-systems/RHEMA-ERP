using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Inventory;

#region Inventory Movement (Source of Truth)

/// <summary>
/// Immutable inventory movement record - the single source of truth for all inventory transactions.
/// Once posted, movements cannot be modified - only reversed with new movements.
/// </summary>
public class InventoryMovement : TenantEntity
{
    /// <summary>
    /// Unique movement number for reference
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string MovementNumber { get; set; } = string.Empty;

    /// <summary>
    /// The inventory item being moved
    /// </summary>
    [Required]
    public Guid InventoryItemId { get; set; }

    /// <summary>
    /// The warehouse where this movement occurred
    /// </summary>
    [Required]
    public Guid WarehouseId { get; set; }

    /// <summary>
    /// Optional specific location within the warehouse
    /// </summary>
    public Guid? LocationId { get; set; }

    /// <summary>
    /// Type of movement (Receipt, Issue, Adjustment, Transfer, etc.)
    /// </summary>
    [Required]
    public InventoryMovementType MovementType { get; set; }

    /// <summary>
    /// Direction of movement: In (positive) or Out (negative)
    /// </summary>
    [Required]
    public MovementDirection Direction { get; set; }

    /// <summary>
    /// Quantity moved (always positive, direction determines sign)
    /// </summary>
    [Required]
    [Column(TypeName = "decimal(18,4)")]
    public decimal Quantity { get; set; }

    /// <summary>
    /// Unit cost at time of movement
    /// </summary>
    [Required]
    [Column(TypeName = "decimal(18,4)")]
    public decimal UnitCost { get; set; }

    /// <summary>
    /// Total value of this movement (Quantity * UnitCost)
    /// </summary>
    [Required]
    [Column(TypeName = "decimal(18,4)")]
    public decimal TotalValue { get; set; }

    /// <summary>
    /// Date and time of the movement
    /// </summary>
    [Required]
    public DateTime MovementDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Posting date for accounting purposes
    /// </summary>
    [Required]
    public DateTime PostingDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Reference type (PurchaseOrder, SalesOrder, Transfer, Adjustment, etc.)
    /// </summary>
    [Required]
    public ReferenceType ReferenceType { get; set; }

    /// <summary>
    /// Reference document number
    /// </summary>
    [MaxLength(100)]
    public string? ReferenceNumber { get; set; }

    /// <summary>
    /// Reference document ID
    /// </summary>
    public Guid? ReferenceId { get; set; }

    /// <summary>
    /// Running balance after this movement
    /// </summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal RunningBalance { get; set; }

    /// <summary>
    /// Running value after this movement
    /// </summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal RunningValue { get; set; }

    /// <summary>
    /// For FIFO: Reference to the cost layer consumed/created
    /// </summary>
    public Guid? CostLayerId { get; set; }

    /// <summary>
    /// For Standard Cost: Variance amount (actual - standard)
    /// </summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal? VarianceAmount { get; set; }

    /// <summary>
    /// Lot/Batch number for tracking
    /// </summary>
    [MaxLength(100)]
    public string? LotNumber { get; set; }

    /// <summary>
    /// Serial number for tracking
    /// </summary>
    [MaxLength(100)]
    public string? SerialNumber { get; set; }

    /// <summary>
    /// Expiration date for perishable items
    /// </summary>
    public DateTime? ExpirationDate { get; set; }

    /// <summary>
    /// User who created this movement
    /// </summary>
    public Guid? CreatedById { get; set; }

    /// <summary>
    /// User who approved/posted this movement
    /// </summary>
    public Guid? PostedById { get; set; }

    /// <summary>
    /// Date when movement was posted
    /// </summary>
    public DateTime? PostedAt { get; set; }

    /// <summary>
    /// Whether this movement has been posted (immutable after posting)
    /// </summary>
    public bool IsPosted { get; set; } = false;

    /// <summary>
    /// Whether this is a reversal of another movement
    /// </summary>
    public bool IsReversal { get; set; } = false;

    /// <summary>
    /// Reference to the original movement if this is a reversal
    /// </summary>
    public Guid? ReversedMovementId { get; set; }

    /// <summary>
    /// Notes/comments for this movement
    /// </summary>
    [MaxLength(2000)]
    public string? Notes { get; set; }

    // Navigation Properties
    public virtual InventoryItem InventoryItem { get; set; } = null!;
    public virtual Warehouse Warehouse { get; set; } = null!;
    public virtual WarehouseLocation? Location { get; set; }
    public virtual InventoryLayer? CostLayer { get; set; }
    public virtual ApplicationUser? CreatedBy { get; set; }
    public virtual ApplicationUser? PostedBy { get; set; }
    public virtual InventoryMovement? ReversedMovement { get; set; }
}

#endregion

#region Inventory Layer (FIFO Cost Layers)

/// <summary>
/// Inventory cost layer for FIFO valuation.
/// Each layer represents a receipt batch with its original cost.
/// </summary>
public class InventoryLayer : TenantEntity
{
    /// <summary>
    /// Unique layer number for reference
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string LayerNumber { get; set; } = string.Empty;

    /// <summary>
    /// The inventory item this layer belongs to
    /// </summary>
    [Required]
    public Guid InventoryItemId { get; set; }

    /// <summary>
    /// The warehouse where this layer exists
    /// </summary>
    [Required]
    public Guid WarehouseId { get; set; }

    /// <summary>
    /// Optional specific location
    /// </summary>
    public Guid? LocationId { get; set; }

    /// <summary>
    /// Date when this layer was created (receipt date)
    /// </summary>
    [Required]
    public DateTime LayerDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Original quantity received in this layer
    /// </summary>
    [Required]
    [Column(TypeName = "decimal(18,4)")]
    public decimal OriginalQuantity { get; set; }

    /// <summary>
    /// Remaining quantity in this layer
    /// </summary>
    [Required]
    [Column(TypeName = "decimal(18,4)")]
    public decimal RemainingQuantity { get; set; }

    /// <summary>
    /// Unit cost for this layer
    /// </summary>
    [Required]
    [Column(TypeName = "decimal(18,4)")]
    public decimal UnitCost { get; set; }

    /// <summary>
    /// Total remaining value (RemainingQuantity * UnitCost)
    /// </summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal RemainingValue { get; set; }

    /// <summary>
    /// Source reference type (GRN, Transfer, Opening, etc.)
    /// </summary>
    [MaxLength(50)]
    public string? SourceType { get; set; }

    /// <summary>
    /// Source reference number
    /// </summary>
    [MaxLength(100)]
    public string? SourceReference { get; set; }

    /// <summary>
    /// Source document ID
    /// </summary>
    public Guid? SourceId { get; set; }

    /// <summary>
    /// Lot/Batch number
    /// </summary>
    [MaxLength(100)]
    public string? LotNumber { get; set; }

    /// <summary>
    /// Expiration date
    /// </summary>
    public DateTime? ExpirationDate { get; set; }

    /// <summary>
    /// Whether this layer is fully consumed
    /// </summary>
    public bool IsFullyConsumed { get; set; } = false;

    /// <summary>
    /// Whether this layer is active
    /// </summary>
    public bool IsActive { get; set; } = true;

    // Navigation Properties
    public virtual InventoryItem InventoryItem { get; set; } = null!;
    public virtual Warehouse Warehouse { get; set; } = null!;
    public virtual WarehouseLocation? Location { get; set; }
    public virtual ICollection<InventoryMovement> Movements { get; set; } = new List<InventoryMovement>();
}

#endregion

#region Inventory Balance (Performance Cache)

/// <summary>
/// Inventory balance cache - derived from movements for performance.
/// This is a denormalized view that can be rebuilt from movements.
/// </summary>
public class InventoryBalance : TenantEntity
{
    /// <summary>
    /// The inventory item
    /// </summary>
    [Required]
    public Guid InventoryItemId { get; set; }

    /// <summary>
    /// The warehouse
    /// </summary>
    [Required]
    public Guid WarehouseId { get; set; }

    /// <summary>
    /// Optional specific location
    /// </summary>
    public Guid? LocationId { get; set; }

    /// <summary>
    /// Current quantity on hand
    /// </summary>
    [Required]
    [Column(TypeName = "decimal(18,4)")]
    public decimal QuantityOnHand { get; set; } = 0;

    /// <summary>
    /// Quantity allocated/reserved
    /// </summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal QuantityAllocated { get; set; } = 0;

    /// <summary>
    /// Available quantity (OnHand - Allocated)
    /// </summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal QuantityAvailable { get; set; } = 0;

    /// <summary>
    /// Quantity on order (incoming)
    /// </summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal QuantityOnOrder { get; set; } = 0;

    /// <summary>
    /// Total inventory value based on valuation method
    /// </summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal TotalValue { get; set; } = 0;

    /// <summary>
    /// Average unit cost (TotalValue / QuantityOnHand)
    /// </summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal AverageUnitCost { get; set; } = 0;

    /// <summary>
    /// Last movement date
    /// </summary>
    public DateTime? LastMovementDate { get; set; }

    /// <summary>
    /// Last receipt date
    /// </summary>
    public DateTime? LastReceiptDate { get; set; }

    /// <summary>
    /// Last issue date
    /// </summary>
    public DateTime? LastIssueDate { get; set; }

    /// <summary>
    /// Last count date
    /// </summary>
    public DateTime? LastCountDate { get; set; }

    /// <summary>
    /// Last time this balance was recalculated
    /// </summary>
    public DateTime LastRecalculatedAt { get; set; } = DateTime.UtcNow;

    // Navigation Properties
    public virtual InventoryItem InventoryItem { get; set; } = null!;
    public virtual Warehouse Warehouse { get; set; } = null!;
    public virtual WarehouseLocation? Location { get; set; }
}

#endregion

#region Enums

/// <summary>
/// Type of inventory movement
/// </summary>
public enum InventoryMovementType
{
    /// <summary>
    /// Receipt from purchase order
    /// </summary>
    PurchaseReceipt = 1,

    /// <summary>
    /// Issue for sales order
    /// </summary>
    SalesIssue = 2,

    /// <summary>
    /// Transfer out to another warehouse
    /// </summary>
    TransferOut = 3,

    /// <summary>
    /// Transfer in from another warehouse
    /// </summary>
    TransferIn = 4,

    /// <summary>
    /// Positive adjustment
    /// </summary>
    AdjustmentIn = 5,

    /// <summary>
    /// Negative adjustment
    /// </summary>
    AdjustmentOut = 6,

    /// <summary>
    /// Production receipt (finished goods)
    /// </summary>
    ProductionReceipt = 7,

    /// <summary>
    /// Production issue (raw materials)
    /// </summary>
    ProductionIssue = 8,

    /// <summary>
    /// Return from customer
    /// </summary>
    CustomerReturn = 9,

    /// <summary>
    /// Return to supplier
    /// </summary>
    SupplierReturn = 10,

    /// <summary>
    /// Scrap/Write-off
    /// </summary>
    Scrap = 11,

    /// <summary>
    /// Opening balance
    /// </summary>
    OpeningBalance = 12,

    /// <summary>
    /// Physical count adjustment
    /// </summary>
    CountAdjustment = 13,

    /// <summary>
    /// Requisition issue
    /// </summary>
    RequisitionIssue = 14,

    /// <summary>
    /// Requisition return
    /// </summary>
    RequisitionReturn = 15
}

/// <summary>
/// Direction of inventory movement
/// </summary>
public enum MovementDirection
{
    /// <summary>
    /// Inbound movement (increases stock)
    /// </summary>
    In = 1,

    /// <summary>
    /// Outbound movement (decreases stock)
    /// </summary>
    Out = 2
}

#endregion
