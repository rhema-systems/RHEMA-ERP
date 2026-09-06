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
/// Invoice-entry identity: Id is a canonical Supplier id when one exists, otherwise an
/// approved BusinessPartner id accepted by the existing invoice command resolver.
/// BusinessPartnerId is the Procurement identity used to filter linked purchase orders.
/// This is not a report-filter identity and reading it never creates a Supplier.
/// </summary>
public sealed class ApInvoiceSupplierEntryDto
{
    public Guid Id { get; set; }
    public Guid? BusinessPartnerId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid? PaymentTermId { get; set; }
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
