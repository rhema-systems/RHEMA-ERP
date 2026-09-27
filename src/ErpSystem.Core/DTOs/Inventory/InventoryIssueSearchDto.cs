namespace ErpSystem.Core.DTOs.Inventory;

public sealed class InventoryIssueSearchDto
{
    public Guid Id { get; set; }
    public string VoucherNumber { get; set; } = string.Empty;
    public string RequisitionNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
