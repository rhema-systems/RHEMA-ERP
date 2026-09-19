using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Services.Inventory;

namespace ErpSystem.Api.Services.Inventory;

/// <summary>
/// Closed owner bridge for disposal's shared-transaction participant. This is intentionally not a
/// Core/public DI contract: only the reviewed Inventory disposal service can reach Core's internal
/// stage/post methods.
/// </summary>
internal interface IInventoryDisposalStockAdjustmentParticipant
{
    Task<StockAdjustment> PreviewAsync(InventoryDisposalStockAdjustmentRequest request,
        CancellationToken cancellationToken = default);
    Task<StockAdjustment> StageApprovedAsync(InventoryDisposalStockAdjustmentRequest request,
        CancellationToken cancellationToken = default);
    Task StagePostedAsync(StockAdjustment adjustment, Guid postingUserId,
        Guid financePostingEventId, Guid financeJournalEntryId,
        IReadOnlyDictionary<Guid, Guid> negativeStockOverrideIds,
        CancellationToken cancellationToken = default);
}

internal sealed class InventoryDisposalStockAdjustmentParticipant(StockAdjustmentService adjustments)
    : IInventoryDisposalStockAdjustmentParticipant
{
    public Task<StockAdjustment> PreviewAsync(InventoryDisposalStockAdjustmentRequest request,
        CancellationToken cancellationToken = default) =>
        adjustments.PreviewDisposalAsync(request, cancellationToken);

    public Task<StockAdjustment> StageApprovedAsync(InventoryDisposalStockAdjustmentRequest request,
        CancellationToken cancellationToken = default) =>
        adjustments.StageApprovedDisposalAsync(request, cancellationToken);

    public Task StagePostedAsync(StockAdjustment adjustment, Guid postingUserId,
        Guid financePostingEventId, Guid financeJournalEntryId,
        IReadOnlyDictionary<Guid, Guid> negativeStockOverrideIds,
        CancellationToken cancellationToken = default) =>
        adjustments.StagePostedDisposalAsync(adjustment, postingUserId, financePostingEventId, financeJournalEntryId,
            negativeStockOverrideIds, cancellationToken);
}
