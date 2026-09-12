using System.ComponentModel.DataAnnotations.Schema;

namespace ErpSystem.Core.Entities.Finance;

/// <summary>Finance-owned immutable handoff of an already physically dispatched Inventory return.</summary>
public sealed class InventorySupplierReturnPosting : TenantEntity
{
    public Guid InventoryPurchaseReturnId { get; set; }
    public Guid OriginalVendorInvoiceId { get; set; }
    public Guid PostingEventId { get; set; }
    public Guid JournalEntryId { get; set; }
    public Guid ClearingAccountId { get; set; }
    public Guid InventoryAccountId { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal CarryingAmount { get; set; }
    public DateTime PostingDate { get; set; }
}
