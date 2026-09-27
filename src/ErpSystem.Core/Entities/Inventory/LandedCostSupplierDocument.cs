using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Inventory;

/// <summary>
/// Evidence of a supplier charge, not a physical goods movement. Its payable is
/// linked through VendorInvoiceLineItem.LandedCostItemId in the shared AP model.
/// </summary>
public sealed class LandedCostSupplierDocument : TenantEntity
{
    [MaxLength(37)] public string DocumentNumber { get; private set; } = string.Empty;
    public Guid LandedCostItemId { get; set; }
    public Guid LandedCostId { get; set; }
    public Guid GoodsReceiptNoteId { get; set; }
    public Guid? PurchaseOrderId { get; set; }
    public Guid BusinessPartnerId { get; set; }
    public LandedCostType CostType { get; set; }
    public decimal Amount { get; set; }
    [MaxLength(10)] public string Currency { get; set; } = string.Empty;
    public LandedCostItem LandedCostItem { get; set; } = null!;
    public LandedCost LandedCost { get; set; } = null!;
    public GoodsReceiptNote GoodsReceiptNote { get; set; } = null!;
    public PurchaseOrder? PurchaseOrder { get; set; }
    public BusinessPartner BusinessPartner { get; set; } = null!;
}
