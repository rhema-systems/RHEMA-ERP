using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.Entities.Inventory;

/// <summary>A voucher-specific declaration; the original receipt and master remain unchanged.</summary>
public sealed class LandedCostReceiptWeight : TenantEntity
{
    public Guid LandedCostId { get; set; }
    public Guid GoodsReceiptNoteItemId { get; set; }
    public decimal UnitWeightKg { get; set; }
    [MaxLength(20)] public string StockUom { get; set; } = string.Empty;
    [MaxLength(500)] public string Reason { get; set; } = string.Empty;
    public LandedCost LandedCost { get; set; } = null!;
    public GoodsReceiptNoteItem GoodsReceiptNoteItem { get; set; } = null!;
}
