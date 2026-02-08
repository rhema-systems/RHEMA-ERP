namespace ErpSystem.Core.DTOs.Procurement;

public class RfqDto
{
    public Guid Id { get; set; }
    public string RfqNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? SubmissionDeadline { get; set; }
    public string Currency { get; set; } = "USD";
    public decimal EstimatedValue { get; set; }
    public Guid? SourcePurchaseRequisitionId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? SentAt { get; set; }
    public int SupplierCount { get; set; }
    public int QuoteCount { get; set; }
}

public class RfqDetailDto : RfqDto
{
    public string? Description { get; set; }
    public string? ExternalRecipientEmails { get; set; }
    public List<RfqItemDto> Items { get; set; } = new();
    public List<RfqInvitationDto> Suppliers { get; set; } = new();
    public List<RfqQuoteDto> Quotes { get; set; } = new();
}

public class RfqItemDto
{
    public Guid Id { get; set; }
    public int LineNumber { get; set; }
    public Guid? InventoryItemId { get; set; }
    public string? ItemCode { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string UnitOfMeasure { get; set; } = string.Empty;
    public string? Specifications { get; set; }
    public DateTime? RequiredDeliveryDate { get; set; }
}

public class RfqInvitationDto
{
    public Guid Id { get; set; }
    public Guid BusinessPartnerId { get; set; }
    public string PartnerCode { get; set; } = string.Empty;
    public string PartnerName { get; set; } = string.Empty;
    public string? PrimaryEmail { get; set; }
    public string Status { get; set; } = "Invited";
    public DateTime InvitedAt { get; set; }
}

public class RfqQuoteDto
{
    public Guid Id { get; set; }
    public Guid BusinessPartnerId { get; set; }
    public string PartnerCode { get; set; } = string.Empty;
    public string PartnerName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? SubmittedAt { get; set; }
    public string? Notes { get; set; }
    public decimal TotalAmount { get; set; }
    public List<RfqQuoteItemDto> Items { get; set; } = new();
}

public class RfqQuoteItemDto
{
    public Guid RfqItemId { get; set; }
    public int LineNumber { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string UnitOfMeasure { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }

    /// <summary>
    /// True when this quote line was selected in the RFQ award decision.
    /// </summary>
    public bool IsAwarded { get; set; }
}

public class CreateRfqFromPurchaseRequisitionDto
{
    /// <summary>
    /// Optional supplier IDs to invite immediately. If empty, RFQ is created with no suppliers.
    /// </summary>
    public List<Guid> SupplierIds { get; set; } = new();

    /// <summary>
    /// Optional external recipients (email-only). The API will parse common delimiters: comma, semicolon, newline.
    /// </summary>
    public string? ExternalRecipientEmails { get; set; }
}

public class UpdateRfqDto
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public DateTime? SubmissionDeadline { get; set; }
    public string? Currency { get; set; }
    public decimal? EstimatedValue { get; set; }
    public List<Guid>? SupplierIds { get; set; }
    public string? ExternalRecipientEmails { get; set; }
}

public class SendRfqDto
{
    public List<Guid> SupplierIds { get; set; } = new();
    public string? ExternalRecipientEmails { get; set; }
}

public class SubmitRfqQuoteDto
{
    public string? Notes { get; set; }
    public List<SubmitRfqQuoteItemDto> Items { get; set; } = new();
}

public class SubmitRfqQuoteItemDto
{
    public Guid RfqItemId { get; set; }
    public decimal UnitPrice { get; set; }
}
