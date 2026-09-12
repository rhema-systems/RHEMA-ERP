using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Procurement;

public class PurchaseOrderLandedCostPlanDto
{
    public Guid Id { get; set; }
    public Guid PurchaseOrderId { get; set; }
    public string Currency { get; set; } = "USD";
    public string Status { get; set; } = "Draft";
    public decimal TotalPlannedCost { get; set; }
    public string? Notes { get; set; }
    public List<PurchaseOrderLandedCostPlanItemDto> Items { get; set; } = new();
}

public class PurchaseOrderLandedCostPlanItemDto
{
    public Guid? PurchaseOrderItemId { get; set; }
    public Guid Id { get; set; }
    public LandedCostType CostType { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public decimal ExchangeRate { get; set; }
    public decimal AmountInPlanCurrency { get; set; }
    public string AllocationMethod { get; set; } = "ByValue";
    public Guid? SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }
}

public class UpsertPurchaseOrderLandedCostPlanDto
{
    [MaxLength(10)]
    public string Currency { get; set; } = "USD";

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [Required]
    public List<UpsertPurchaseOrderLandedCostPlanItemDto> Items { get; set; } = new();
}

public class UpsertPurchaseOrderLandedCostPlanItemDto
{
    public Guid? PurchaseOrderItemId { get; set; }
    // Only accepted as part of a PO save; resolved to its persisted line ID.
    [Range(0, int.MaxValue)]
    public int? PurchaseOrderLineIndex { get; set; }
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

    [MaxLength(1000)]
    public string? Notes { get; set; }
}
