using ErpSystem.Core.Entities.Inventory;

namespace ErpSystem.Core.Interfaces.Inventory;

public interface IInventoryAdjustmentFinancePostingService
{
    Task<InventoryAdjustmentFinancePostingResult> PostAsync(StockAdjustment adjustment, CancellationToken cancellationToken = default);
    Task<InventoryAdjustmentFinancePostingResult> ReverseAsync(StockAdjustment adjustment, string reason, CancellationToken cancellationToken = default);
}

public sealed record InventoryAdjustmentFinancePostingResult(Guid PostingEventId, Guid JournalEntryId);
