using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Finance;

public class FinancePurchaseOrderDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public Guid VendorId { get; set; }
    public Guid SupplierId { get; set; }
    public string? VendorName { get; set; }
    public string? SupplierName { get; set; }
    public DateTime OrderDate { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
    public Guid? PaymentTermId { get; set; }
    public int? PaymentTermsDays { get; set; }
    public decimal? EarlyPaymentDiscountPercentage { get; set; }
    public DateTime? EarlyPaymentDiscountDueDate { get; set; }
    public int Status { get; set; }
    public string CurrencyCode { get; set; } = "GHS";
    public decimal ExchangeRate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public Guid? TaxGroupId { get; set; }
    public string? Remarks { get; set; }
    public List<FinancePurchaseOrderItemDto> Items { get; set; } = new();
}

public class FinancePurchaseOrderItemDto
{
    public Guid Id { get; set; }
    public Guid FinancePurchaseOrderId { get; set; }
    public int LineType { get; set; }
    public Guid? InventoryItemId { get; set; }
    public Guid? WarehouseId { get; set; }
    public Guid? GlAccountId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal OrderedQuantity { get; set; }
    public decimal ReceivedQuantity { get; set; }
    public decimal InvoicedQuantity { get; set; }
    public decimal CancelledQuantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string? CurrencyCode { get; set; }
    public decimal ExchangeRate { get; set; }
    public string? TaxCode { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public Guid? TaxGroupId { get; set; }
    public decimal DiscountPercentage { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal LineTotal { get; set; }
    public string? InventoryItemName { get; set; }
    public string? InventoryItemCode { get; set; }
    public string? GlAccountName { get; set; }
    public string? GlAccountCode { get; set; }
}

public class CreateFinancePurchaseOrderDto
{
    [Required]
    public Guid VendorId { get; set; }

    public string? OrderNumber { get; set; }
    public DateTime? OrderDate { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
    public Guid? PaymentTermId { get; set; }
    public string? CurrencyCode { get; set; }
    public decimal? ExchangeRate { get; set; }
    public decimal? TotalAmount { get; set; }
    public decimal? DiscountAmount { get; set; }
    public Guid? TaxGroupId { get; set; }
    public string? Remarks { get; set; }

    [Required]
    public List<CreateFinancePurchaseOrderItemDto> Items { get; set; } = new();
}

public class CreateFinancePurchaseOrderItemDto
{
    /// <summary>
    /// 1 Inventory, 2 GL account.
    /// </summary>
    public int LineType { get; set; } = 1;
    public Guid? InventoryItemId { get; set; }
    public Guid? WarehouseId { get; set; }
    public Guid? GlAccountId { get; set; }

    [Required]
    public string Description { get; set; } = string.Empty;

    [Required]
    public decimal OrderedQuantity { get; set; }

    [Required]
    public decimal UnitPrice { get; set; }

    public string? CurrencyCode { get; set; }
    public decimal? ExchangeRate { get; set; }
    public string? TaxCode { get; set; }
    public decimal? TaxRate { get; set; }
    public decimal? TaxAmount { get; set; }
    public Guid? TaxGroupId { get; set; }
    public decimal? DiscountPercentage { get; set; }
    public decimal? DiscountAmount { get; set; }
    public decimal? LineTotal { get; set; }
}

public class FinancePurchaseOrderReceiptDto
{
    public Guid Id { get; set; }
    public Guid FinancePurchaseOrderId { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;
    public DateTime ReceiptDate { get; set; }
    public string? Remarks { get; set; }
    public Guid? VendorInvoiceId { get; set; }
    public string? OrderNumber { get; set; }
    public string? VendorName { get; set; }
    public List<FinancePurchaseOrderReceiptItemDto> Items { get; set; } = new();
}

public class FinancePurchaseOrderReceiptItemDto
{
    public Guid Id { get; set; }
    public Guid FinancePurchaseOrderReceiptId { get; set; }
    public Guid FinancePurchaseOrderItemId { get; set; }
    public decimal QuantityReceived { get; set; }
    public decimal InvoicedQuantity { get; set; }
    public decimal RemainingToInvoice { get; set; }
    public decimal DiscountPercentage { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? Description { get; set; }
    public decimal OrderedQuantity { get; set; }
    public decimal PreviouslyReceived { get; set; }
}

public class CreateFinancePurchaseOrderReceiptDto
{
    [Required]
    public Guid FinancePurchaseOrderId { get; set; }

    public string? ReceiptNumber { get; set; }
    public DateTime? ReceiptDate { get; set; }
    public string? Remarks { get; set; }
    public List<CreateFinancePurchaseOrderReceiptItemDto> Lines { get; set; } = new();
    public List<CreateFinancePurchaseOrderReceiptItemDto> Items { get; set; } = new();
}

public class CreateFinancePurchaseOrderReceiptItemDto
{
    [Required]
    public Guid FinancePurchaseOrderItemId { get; set; }

    [Required]
    public decimal QuantityReceived { get; set; }

    public decimal? DiscountPercentage { get; set; }
    public decimal? DiscountAmount { get; set; }
}
