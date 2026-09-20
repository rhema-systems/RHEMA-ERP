using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Inventory;

namespace ErpSystem.Core.Interfaces.Finance;

/// <summary>
/// Finance-owned, read-only translation of a preidentified Stock Adjustment graph into one neutral
/// producer intent. The caller, not this boundary, owns Inventory state and transaction sequencing.
/// </summary>
public interface IStockAdjustmentValuationIntentBuilder
{
    Task<ProducerAccountingIntentDto> BuildAsync(
        StockAdjustment adjustment,
        ProducerOwnerEffectIdentityDto? expectedOwnerEffect = null,
        CancellationToken cancellationToken = default);
}
