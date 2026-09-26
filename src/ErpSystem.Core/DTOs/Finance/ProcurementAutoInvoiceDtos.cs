using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Finance;

public sealed class ProcurementAutoInvoiceRequestDto
{
    public Guid RequestId { get; set; }
    public Guid BusinessPartnerId { get; set; }
    public Guid? BusinessPartnerRoleId { get; set; }
    [Required, StringLength(100)] public string SupplierInvoiceNumber { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public Guid? ExchangeRateId { get; set; }
    public decimal ExchangeRate { get; set; } = 1m;
    public List<ProcurementAutoInvoiceSelectionDto> Lines { get; set; } = [];
}

public sealed class ProcurementAutoInvoiceSelectionDto
{
    public Guid GoodsReceiptNoteItemId { get; set; }
    public decimal Quantity { get; set; }
}

public sealed class ProcurementInvoiceReceiptDto
{
    public Guid GoodsReceiptNoteId { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;
    public DateTime ReceiptDate { get; set; }
    public Guid PurchaseOrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public List<ProcurementInvoiceReceiptLineDto> Lines { get; set; } = [];
}

public sealed class ProcurementInvoiceReceiptLineDto
{
    public Guid GoodsReceiptNoteItemId { get; set; }
    public Guid PurchaseOrderItemId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal AcceptedQuantity { get; set; }
    public decimal ReturnedQuantity { get; set; }
    public decimal InvoicedQuantity { get; set; }
    public decimal AvailableQuantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public sealed class VendorInvoiceReceiptLinkDto
{
    public Guid InvoiceLineId { get; set; }
    public Guid GoodsReceiptNoteId { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;
    public Guid PurchaseOrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public Guid InspectionCaseId { get; set; }
    public decimal Quantity { get; set; }
}
