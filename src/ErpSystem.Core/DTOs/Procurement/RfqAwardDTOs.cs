namespace ErpSystem.Core.DTOs.Procurement;

public class CreatePurchaseOrdersFromRfqDto
{
    /// <summary>
    /// WinnerTakesAll or SplitAward
    /// </summary>
    public string Mode { get; set; } = "WinnerTakesAll";

    /// <summary>
    /// Required when Mode=WinnerTakesAll
    /// </summary>
    public Guid? QuoteId { get; set; }

    /// <summary>
    /// Required when Mode=SplitAward (one entry per RFQ item).
    /// </summary>
    public List<RfqSplitAwardLineDto> Lines { get; set; } = new();
}

public class RfqSplitAwardLineDto
{
    public Guid RfqItemId { get; set; }
    public Guid QuoteId { get; set; }
}

public class CreatePurchaseOrdersFromRfqResponseDto
{
    public List<CreatedPurchaseOrderFromRfqDto> PurchaseOrders { get; set; } = new();
}

public class CreatedPurchaseOrderFromRfqDto
{
    public Guid PurchaseOrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public Guid BusinessPartnerId { get; set; }
    public string BusinessPartnerName { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
}

