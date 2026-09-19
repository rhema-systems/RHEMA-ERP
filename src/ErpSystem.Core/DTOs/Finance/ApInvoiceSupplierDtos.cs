namespace ErpSystem.Core.DTOs.Finance;

/// <summary>
/// Finance-owned, read-only projection of the canonical Supplier identity required by AP reports
/// and invoice commands. Procurement retains ownership of the Supplier master; Finance consumes
/// only this projection and uses <see cref="Id"/> as VendorInvoice.SupplierId.
/// </summary>
public sealed class ApInvoiceSupplierDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid? PaymentTermId { get; set; }
}

/// <summary>
/// Finance-owned entry option spanning Procurement's approved Business Partner identities and
/// canonical Supplier identities. <see cref="Id"/> is safe to submit to the vendor-invoice
/// command, which resolves either identity to the persisted Supplier foreign key.
/// </summary>
public sealed class ApInvoiceSupplierEntryOptionDto
{
    public Guid Id { get; set; }
    public Guid? SupplierId { get; set; }
    public Guid? BusinessPartnerId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid? PaymentTermId { get; set; }
    public string? Currency { get; set; }
}

public sealed class ApGoodsInvoiceEntryDto
{
    public Guid PurchaseOrderId { get; init; }
    public IReadOnlyList<ApGoodsInvoiceEntryLineDto> Lines { get; init; } = [];
}

public sealed class ApGoodsInvoiceEntryLineDto
{
    public Guid PurchaseOrderItemId { get; init; }
    public decimal AcceptedQuantity { get; init; }
    public decimal InvoicedQuantity { get; init; }
    public decimal AvailableQuantity => Math.Max(0m, AcceptedQuantity - InvoicedQuantity);
}
