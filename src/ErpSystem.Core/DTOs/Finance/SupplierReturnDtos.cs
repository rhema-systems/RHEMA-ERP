namespace ErpSystem.Core.DTOs.Finance;

public class SupplierReturnDto
{
    public Guid Id { get; set; }
    public string ReturnNumber { get; set; } = string.Empty;
    public Guid VendorId { get; set; }
    public string VendorName { get; set; } = string.Empty;
    public Guid? OriginalVendorInvoiceId { get; set; }
    public Guid? OriginalFinancePurchaseOrderReceiptId { get; set; }
    public LinkedVendorInvoiceDto? OriginalVendorInvoice { get; set; }
    public LinkedFinancePurchaseOrderReceiptDto? OriginalFinancePurchaseOrderReceipt { get; set; }
    public DateTime ReturnDate { get; set; }
    public string? Reason { get; set; }
    public string CurrencyCode { get; set; } = "GHS";
    public decimal ExchangeRate { get; set; } = 1m;
    public decimal SubTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal BaseCurrencyAmount { get; set; }
    public int Status { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public SupplierDebitNoteDto? DebitNote { get; set; }
    public List<SupplierReturnLineItemDto> LineItems { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}

public class SupplierReturnLineItemDto
{
    public Guid Id { get; set; }
    public Guid? OriginalVendorInvoiceLineItemId { get; set; }
    public Guid? OriginalFinancePurchaseOrderItemId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal QuantityReturned { get; set; }
    public decimal UnitPrice { get; set; }
    public Guid? TaxGroupId { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal DiscountPercentage { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal LineTotal { get; set; }
}

public class CreateSupplierReturnDto
{
    public string? ReturnNumber { get; set; }
    public Guid VendorId { get; set; }
    public string? VendorName { get; set; }
    public Guid? OriginalVendorInvoiceId { get; set; }
    public Guid? OriginalFinancePurchaseOrderReceiptId { get; set; }
    public DateTime ReturnDate { get; set; }
    public string? Reason { get; set; }
    public string CurrencyCode { get; set; } = "GHS";
    public decimal ExchangeRate { get; set; } = 1m;
    public List<CreateSupplierReturnLineItemDto> Lines { get; set; } = new();
}

public class CreateSupplierReturnLineItemDto
{
    public Guid? OriginalVendorInvoiceLineItemId { get; set; }
    public Guid? OriginalFinancePurchaseOrderItemId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal QuantityReturned { get; set; }
    public decimal UnitPrice { get; set; }
    public Guid? TaxGroupId { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal DiscountPercentage { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal LineTotal { get; set; }
}

public class SupplierDebitNoteDto
{
    public Guid Id { get; set; }
    public string DebitNoteNumber { get; set; } = string.Empty;
    public Guid? JournalEntryId { get; set; }
    public int Status { get; set; }
    public string StatusName { get; set; } = string.Empty;
}

public class LinkedVendorInvoiceDto
{
    public Guid Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
}

public class LinkedFinancePurchaseOrderReceiptDto
{
    public Guid Id { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;
}
