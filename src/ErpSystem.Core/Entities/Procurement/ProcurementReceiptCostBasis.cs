using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ErpSystem.Core.Entities.Procurement;

/// <summary>Original accepted purchase cost and conversion evidence; never recomputed from current PO or item defaults.</summary>
public sealed class ProcurementReceiptCostBasis : TenantEntity
{
    public int Version { get; set; } = 1;
    public Guid PurchaseOrderReceiptId { get; set; }
    public Guid PurchaseOrderReceiptItemId { get; set; }
    public Guid PurchaseOrderItemId { get; set; }
    public Guid InventoryItemId { get; set; }
    public Guid InventoryMovementId { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid LocationId { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal PurchaseQuantity { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal BaseQuantity { get; set; }
    [Column(TypeName = "decimal(18,8)")] public decimal ConversionToBase { get; set; }
    [Required, MaxLength(3)] public string PurchaseCurrency { get; set; } = string.Empty;
    [Required, MaxLength(3)] public string FunctionalCurrency { get; set; } = string.Empty;
    public Guid? ExchangeRateId { get; set; }
    [Column(TypeName = "decimal(18,6)")] public decimal ExchangeRateToFunctional { get; set; }
    public DateTime ExchangeRateDate { get; set; }
    [Column(TypeName = "decimal(18,6)")] public decimal PurchaseUnitCost { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal PurchaseAmount { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal FunctionalAccrualAmount { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal FunctionalInventoryAmount { get; set; }
    public DateTime CapturedAtUtc { get; set; } = DateTime.UtcNow;
}
