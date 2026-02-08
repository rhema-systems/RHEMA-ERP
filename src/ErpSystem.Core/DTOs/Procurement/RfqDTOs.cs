namespace ErpSystem.Core.DTOs.Procurement;

/// <summary>
/// Suggested suppliers for an RFQ based on a purchase requisition's items.
/// </summary>
public class SuggestedSupplierDto
{
    public Guid SupplierId { get; set; }

    /// <summary>
    /// Number of requisition items that map to this supplier via item-supplier relationships.
    /// </summary>
    public int ItemMatchCount { get; set; }

    /// <summary>
    /// Number of requisition items explicitly preferring this supplier.
    /// </summary>
    public int PreferredItemCount { get; set; }
}

public class CreateRfqFromPurchaseRequisitionResponseDto
{
    public Guid RfqId { get; set; }
    public string RfqNumber { get; set; } = string.Empty;
}
