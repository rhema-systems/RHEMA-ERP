namespace ErpSystem.Core.Interfaces.Inventory;

public interface IInventoryReceiptFinancePostingService
{
    Task<InventoryFinancePostingResult> PostAcceptedReceiptAsync(
        Guid purchaseOrderReceiptId,
        CancellationToken cancellationToken = default);
}

public interface IInventoryLandedCostFinancePostingService
{
    Task<InventoryFinancePostingResult> PostLandedCostAsync(
        Guid landedCostId,
        CancellationToken cancellationToken = default);
}

public sealed record InventoryFinancePostingResult(
    Guid PostingEventId,
    Guid JournalEntryId,
    bool WasDuplicate);
