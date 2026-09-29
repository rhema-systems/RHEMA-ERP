namespace ErpSystem.Core.DTOs.Inventory;

/// <summary>Read-only receipt lineage and current return capacity; dispatch revalidates under source locks.</summary>
public sealed class SupplierReturnSourceContextDto
{
    public Guid? PurchaseOrderReceiptId { get; set; }
    public string? ReceiptNumber { get; set; }
    public List<SupplierReturnSourceLineContextDto> Lines { get; set; } = [];
}

public sealed class SupplierReturnSourceLineContextDto
{
    public Guid GoodsReceiptNoteItemId { get; set; }
    public decimal PreviouslyReturnedQuantity { get; set; }
    public decimal ReservedReturnQuantity { get; set; }
    public decimal ReservedInvoiceQuantity { get; set; }
    public decimal RemainingReturnableQuantity { get; set; }
    public decimal InvoicedQuantity { get; set; }
    public List<SupplierReturnSourceInvoiceDto> Invoices { get; set; } = [];
}

public sealed class SupplierReturnSourceInvoiceDto
{
    public Guid InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public decimal BaseQuantity { get; set; }
    public bool Posted { get; set; }
}
