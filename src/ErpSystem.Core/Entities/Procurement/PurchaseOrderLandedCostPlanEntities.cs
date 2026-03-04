using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Procurement;

/// <summary>
/// Planned landed costs captured at the Purchase Order stage (estimates).
/// These are later copied to GRN-level LandedCost for actual allocation.
/// </summary>
public class PurchaseOrderLandedCostPlan : TenantEntity
{
    [Required]
    public Guid PurchaseOrderId { get; set; }

    [MaxLength(10)]
    public string Currency { get; set; } = "USD";

    [MaxLength(50)]
    public string Status { get; set; } = "Draft";

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalPlannedCost { get; set; } = 0;

    [MaxLength(2000)]
    public string? Notes { get; set; }

    public virtual PurchaseOrder PurchaseOrder { get; set; } = null!;
    public virtual ICollection<PurchaseOrderLandedCostPlanItem> Items { get; set; } = new List<PurchaseOrderLandedCostPlanItem>();
}

/// <summary>
/// Planned landed cost line items (freight, duty, insurance, etc.) with mixed allocation bases.
/// </summary>
public class PurchaseOrderLandedCostPlanItem : TenantEntity
{
    [Required]
    public Guid PurchaseOrderLandedCostPlanId { get; set; }

    public LandedCostType CostType { get; set; }

    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; } = 0;

    [MaxLength(10)]
    public string Currency { get; set; } = "USD";

    /// <summary>
    /// Exchange rate to the parent plan currency.
    /// </summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal ExchangeRate { get; set; } = 1;

    [Column(TypeName = "decimal(18,2)")]
    public decimal AmountInPlanCurrency { get; set; } = 0;

    /// <summary>
    /// Allocation method per cost line: ByValue, ByQuantity, ByWeight, ByVolume, Equal, Manual.
    /// (Manual allocations are applied at GRN stage.)
    /// </summary>
    [MaxLength(50)]
    public string AllocationMethod { get; set; } = "ByValue";

    public Guid? SupplierId { get; set; }

    [MaxLength(200)]
    public string? SupplierName { get; set; }

    [MaxLength(100)]
    public string? ReferenceNumber { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public virtual PurchaseOrderLandedCostPlan PurchaseOrderLandedCostPlan { get; set; } = null!;
}
