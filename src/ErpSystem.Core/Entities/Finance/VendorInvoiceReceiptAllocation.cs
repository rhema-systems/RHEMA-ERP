using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;

namespace ErpSystem.Core.Entities.Finance;

/// <summary>Immutable accepted receipt authority for one consolidated AP line, in PO units.</summary>
public sealed class VendorInvoiceReceiptAllocation : TenantEntity
{
    public Guid VendorInvoiceId { get; set; }
    public Guid VendorInvoiceLineItemId { get; set; }
    public Guid PurchaseOrderId { get; set; }
    public Guid PurchaseOrderItemId { get; set; }
    public Guid PurchaseOrderReceiptId { get; set; }
    public Guid PurchaseOrderReceiptItemId { get; set; }
    public Guid InspectionCaseId { get; set; }
    public Guid GoodsReceiptNoteId { get; set; }
    public Guid GoodsReceiptNoteItemId { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal Quantity { get; set; }
    public VendorInvoice VendorInvoice { get; set; } = null!;
    public VendorInvoiceLineItem VendorInvoiceLineItem { get; set; } = null!;
    public PurchaseOrder PurchaseOrder { get; set; } = null!;
    public PurchaseOrderItem PurchaseOrderItem { get; set; } = null!;
    public PurchaseOrderReceipt PurchaseOrderReceipt { get; set; } = null!;
    public PurchaseOrderReceiptItem PurchaseOrderReceiptItem { get; set; } = null!;
    public ProcurementReceiptInspectionCase InspectionCase { get; set; } = null!;
    public GoodsReceiptNote GoodsReceiptNote { get; set; } = null!;
    public GoodsReceiptNoteItem GoodsReceiptNoteItem { get; set; } = null!;
}
