namespace ErpSystem.Core.DTOs.Inventory;

public sealed class InventoryIssueCostPreviewLineDto
{
    public Guid LineId { get; set; }
    public Guid InventoryItemId { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid? LocationId { get; set; }
    public decimal Quantity { get; set; }
    public string? LotNumber { get; set; }
    public string? SerialNumber { get; set; }
}
