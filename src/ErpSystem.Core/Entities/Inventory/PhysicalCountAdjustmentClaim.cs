using System.ComponentModel.DataAnnotations.Schema;

namespace ErpSystem.Core.Entities.Inventory;

public sealed class PhysicalCountAdjustmentClaim : TenantEntity
{
    public Guid RootPhysicalCountItemId { get; set; }
    public Guid PhysicalCountId { get; set; }
    public Guid PhysicalCountItemId { get; set; }
    public Guid? StockAdjustmentId { get; set; }
    public Guid? StockAdjustmentItemId { get; set; }
    public Guid ClaimedById { get; set; }
    public DateTime ClaimedAtUtc { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal SystemQuantity { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal CountedQuantity { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal VarianceQuantity { get; set; }
}
