using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Interfaces.Inventory;

namespace ErpSystem.Core.Services.Inventory;

public partial class InventoryTransferService
{
    private readonly IInventoryValuationService? _valuation;

    private Task<decimal> DispatchCarryingValueAsync(InventoryTransfer transfer, InventoryTransferItem line,
        InventoryTransferAction action, Guid warehouseId, decimal quantity)
    {
        if (_valuation is null) throw new InvalidOperationException("Authoritative transfer valuation is not configured; no stock was dispatched.");
        if (!line.SourceLocationId.HasValue || line.SourceLocationId == Guid.Empty)
            throw new InvalidOperationException("Select an exact source bin before dispatching this transfer.");
        if (line.TotalAllocatedCost > 0 || transfer.CostAllocationMethod == "SpreadToItemCost" && transfer.TotalAdditionalCost > 0)
            throw new InvalidOperationException("Transfer shipping-cost capitalization requires a posted landed-cost voucher. No stock was dispatched; use a transfer without unposted allocated charges.");
        return _valuation.ProcessTransferDispatchAsync(line.Id, action.Id, warehouseId, line.SourceLocationId.Value, quantity);
    }

    private Task<decimal> ReceiveCarryingValueAsync(InventoryTransfer transfer, InventoryTransferItem line,
        InventoryTransferAction action, Guid warehouseId, decimal quantity, bool toSource = false)
    {
        if (_valuation is null) throw new InvalidOperationException("Authoritative transfer valuation is not configured; no stock was received.");
        var locationId = toSource ? line.SourceLocationId : line.DestinationLocationId;
        if (!locationId.HasValue || locationId == Guid.Empty)
            throw new InvalidOperationException("Select an exact receipt bin before receiving this transfer.");
        if (!toSource && line.TotalAllocatedCost > 0)
            throw new InvalidOperationException("Transfer shipping-cost capitalization needs a posted landed-cost voucher. Receive the original carrying value through a transfer without unposted allocated charges.");
        return _valuation.ProcessTransferReceiptAsync(line.Id, action.Id, warehouseId, locationId.Value, quantity, toSource);
    }
}
