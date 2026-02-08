using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Interfaces;

namespace ErpSystem.Core.Entities.Pricing;

#region Enums

/// <summary>
/// Type of price list
/// </summary>
public enum PriceListType
{
    Purchase = 0,      // Supplier/purchase prices
    Sales = 1,         // Customer/sales prices
    StandardCost = 2,  // Standard cost for valuation
    Transfer = 3       // Internal transfer prices
}

/// <summary>
/// Status of a price list
/// </summary>
public enum PriceListStatus
{
    Draft = 0,
    Active = 1,
    Expired = 2,
    Superseded = 3,
    Cancelled = 4
}

/// <summary>
/// Approval status for price lists
/// </summary>
public enum PriceListApprovalStatus
{
    Draft = 0,
    PendingApproval = 1,
    Approved = 2,
    Rejected = 3
}

/// <summary>
/// Entity type that the price list applies to
/// </summary>
public enum PriceListApplicableEntityType
{
    All = 0,           // Default price list for all
    Supplier = 1,      // Specific supplier
    SupplierGroup = 2, // Supplier group
    Customer = 3,      // Specific customer
    CustomerGroup = 4, // Customer group
    ItemCategory = 5   // Item category
}

/// <summary>
/// Rounding rule for price calculations
/// </summary>
public enum PriceRoundingRule
{
    None = 0,
    RoundUp = 1,
    RoundDown = 2,
    RoundNearest = 3,
    RoundTo5Cents = 4,
    RoundTo10Cents = 5
}

#endregion

#region Price List Master

/// <summary>
/// Price List Master - defines a versioned, time-bound set of prices
/// </summary>
public class PriceList : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string PriceListCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    /// Type of price list (Purchase, Sales, StandardCost, Transfer)
    /// </summary>
    public PriceListType Type { get; set; } = PriceListType.Purchase;

    /// <summary>
    /// Currency for this price list
    /// </summary>
    [Required]
    [MaxLength(10)]
    public string Currency { get; set; } = "USD";

    /// <summary>
    /// Date from which this price list is effective
    /// </summary>
    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Date until which this price list is effective (null = no expiry)
    /// </summary>
    public DateTime? EffectiveTo { get; set; }

    /// <summary>
    /// Current status of the price list
    /// </summary>
    public PriceListStatus Status { get; set; } = PriceListStatus.Draft;

    /// <summary>
    /// Priority for price resolution (higher = preferred)
    /// </summary>
    public int Priority { get; set; } = 0;

    /// <summary>
    /// Is this the default price list for its type?
    /// </summary>
    public bool IsDefault { get; set; } = false;

    /// <summary>
    /// Type of entity this price list applies to
    /// </summary>
    public PriceListApplicableEntityType ApplicableEntityType { get; set; } = PriceListApplicableEntityType.All;

    /// <summary>
    /// ID of the applicable entity (Supplier, Customer, etc.)
    /// </summary>
    public Guid? ApplicableEntityId { get; set; }

    /// <summary>
    /// Approval status
    /// </summary>
    public PriceListApprovalStatus ApprovalStatus { get; set; } = PriceListApprovalStatus.Draft;

    /// <summary>
    /// User who approved the price list
    /// </summary>
    public Guid? ApprovedById { get; set; }

    /// <summary>
    /// Date when approved
    /// </summary>
    public DateTime? ApprovedDate { get; set; }

    /// <summary>
    /// Approval comments
    /// </summary>
    [MaxLength(1000)]
    public string? ApprovalComments { get; set; }

    /// <summary>
    /// Reference to the price list this one supersedes
    /// </summary>
    public Guid? SupersededPriceListId { get; set; }

    /// <summary>
    /// Version number for tracking changes
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// Notes/comments
    /// </summary>
    [MaxLength(2000)]
    public string? Notes { get; set; }

    // Navigation Properties
    public virtual ICollection<PriceListLine> Lines { get; set; } = new List<PriceListLine>();
    public virtual PriceList? SupersededPriceList { get; set; }

    /// <summary>
    /// Check if this price list is currently effective
    /// </summary>
    [NotMapped]
    public bool IsEffective =>
        Status == PriceListStatus.Active &&
        ApprovalStatus == PriceListApprovalStatus.Approved &&
        EffectiveFrom <= DateTime.UtcNow &&
        (EffectiveTo == null || EffectiveTo > DateTime.UtcNow);
}

#endregion

#region Price List Line

/// <summary>
/// Individual price line within a price list
/// </summary>
public class PriceListLine : TenantEntity
{
    [Required]
    public Guid PriceListId { get; set; }

    [Required]
    public Guid InventoryItemId { get; set; }

    /// <summary>
    /// Unit of measure for this price (may differ from item's base UOM)
    /// </summary>
    [MaxLength(20)]
    public string UnitOfMeasure { get; set; } = "EA";

    /// <summary>
    /// Base price before any discounts
    /// </summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal BasePrice { get; set; } = 0;

    /// <summary>
    /// Discount percentage (0-100)
    /// </summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal DiscountPercent { get; set; } = 0;

    /// <summary>
    /// Net price after discount (BasePrice * (1 - DiscountPercent/100))
    /// </summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal NetPrice { get; set; } = 0;

    /// <summary>
    /// Minimum quantity for this price tier
    /// </summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal MinQuantity { get; set; } = 0;

    /// <summary>
    /// Maximum quantity for this price tier (null = unlimited)
    /// </summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal? MaxQuantity { get; set; }

    /// <summary>
    /// Lead time in days for this price (optional)
    /// </summary>
    public int? LeadTimeDays { get; set; }

    /// <summary>
    /// Is the price inclusive of tax?
    /// </summary>
    public bool IsTaxInclusive { get; set; } = false;

    /// <summary>
    /// Rounding rule for this price
    /// </summary>
    public PriceRoundingRule RoundingRule { get; set; } = PriceRoundingRule.None;

    /// <summary>
    /// Supplier's item code (for purchase price lists)
    /// </summary>
    [MaxLength(100)]
    public string? SupplierItemCode { get; set; }

    /// <summary>
    /// Minimum order quantity from supplier
    /// </summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal? MinimumOrderQuantity { get; set; }

    /// <summary>
    /// Order multiple (order in multiples of)
    /// </summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal? OrderMultiple { get; set; }

    /// <summary>
    /// Date when price was last updated
    /// </summary>
    public DateTime? LastPriceUpdate { get; set; }

    /// <summary>
    /// Previous price (for tracking changes)
    /// </summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal? PreviousPrice { get; set; }

    /// <summary>
    /// Price change percentage from previous
    /// </summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal? PriceChangePercent { get; set; }

    /// <summary>
    /// Notes for this price line
    /// </summary>
    [MaxLength(500)]
    public string? Notes { get; set; }

    /// <summary>
    /// Is this line active?
    /// </summary>
    public bool IsActive { get; set; } = true;

    // Navigation Properties
    public virtual PriceList PriceList { get; set; } = null!;
    public virtual ErpSystem.Core.Entities.Inventory.InventoryItem? InventoryItem { get; set; }
}

#endregion

#region Customer/Supplier Groups

/// <summary>
/// Customer Group for grouping customers with similar pricing
/// </summary>
public class CustomerGroup : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string GroupCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    /// Default discount percentage for this group
    /// </summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal DefaultDiscountPercent { get; set; } = 0;

    /// <summary>
    /// Default payment terms for this group
    /// </summary>
    [MaxLength(50)]
    public string? DefaultPaymentTerms { get; set; }

    /// <summary>
    /// Default credit limit for this group
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? DefaultCreditLimit { get; set; }

    /// <summary>
    /// Default price list for this group
    /// </summary>
    public Guid? DefaultPriceListId { get; set; }

    public bool IsActive { get; set; } = true;

    // Navigation Properties
    public virtual PriceList? DefaultPriceList { get; set; }
}

/// <summary>
/// Supplier Group for grouping suppliers with similar pricing
/// </summary>
public class SupplierGroup : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string GroupCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    /// Default payment terms for this group
    /// </summary>
    [MaxLength(50)]
    public string? DefaultPaymentTerms { get; set; }

    /// <summary>
    /// Default lead time for suppliers in this group
    /// </summary>
    public int DefaultLeadTimeDays { get; set; } = 7;

    /// <summary>
    /// Default price list for this group
    /// </summary>
    public Guid? DefaultPriceListId { get; set; }

    public bool IsActive { get; set; } = true;

    // Navigation Properties
    public virtual PriceList? DefaultPriceList { get; set; }
}

#endregion

#region Price List Change History

/// <summary>
/// Tracks price list line changes for audit trail
/// </summary>
public class PriceListChangeHistory : TenantEntity
{
    [Required]
    public Guid PriceListLineId { get; set; }

    [Required]
    public Guid InventoryItemId { get; set; }

    public Guid? PriceListId { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal OldPrice { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal NewPrice { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal ChangePercent { get; set; }

    [MaxLength(50)]
    public string ChangeType { get; set; } = "Update"; // Create, Update, Delete

    [MaxLength(500)]
    public string? ChangeReason { get; set; }

    public DateTime EffectiveDate { get; set; } = DateTime.UtcNow;

    // Set by audit trail
    public Guid? ChangedById { get; set; }
}

#endregion

