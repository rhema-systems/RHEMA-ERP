using ErpSystem.Core.Entities.Finance;

namespace ErpSystem.Core.Interfaces.Inventory;

public sealed record ReceiptCostAdjustmentTarget(Guid WarehouseId, Guid LocationId, Guid? LayerId,
    decimal AttributedQuantity, decimal ValueChange);

/// <summary>Internal Finance-to-Inventory owner; never a public price or quantity adjustment endpoint.</summary>
public interface IInventoryReceiptCostAdjustmentService
{
    Task ApplyReceiptCostAdjustmentAsync(VendorInvoiceReceiptCostAllocation cost,
        IReadOnlyList<ReceiptCostAdjustmentTarget> targets, CancellationToken ct = default);
    Task ReverseReceiptCostAdjustmentAsync(VendorInvoiceReceiptCostAllocation cost,
        IReadOnlyList<ReceiptCostAdjustmentTarget> targets, Guid reversalPostingEventId, Guid reversalJournalEntryId, CancellationToken ct = default);
}
