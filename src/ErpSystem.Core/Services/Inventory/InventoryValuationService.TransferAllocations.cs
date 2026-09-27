using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Inventory;

public partial class InventoryValuationService
{
    public async Task<decimal> ProcessTransferAllocationDispatchAsync(Guid dispatchAllocationId)
    {
        var allocation = await RequireDispatchAllocationAsync(dispatchAllocationId);
        var line = allocation.InventoryTransferItem;
        var action = allocation.InventoryTransferAction;
        if (action.ActionType != InventoryTransferActionType.Dispatched ||
            line.InventoryTransfer.Status is not (TransferStatus.Approved or TransferStatus.InTransit))
            throw new InvalidOperationException("The dispatch allocation does not belong to an active dispatch.");
        var ledger = await TransferValuationLedgerAsync(line.Id);
        if (ledger.Any(x => x.TransferDispatchAllocationId == allocation.Id))
            throw new InvalidOperationException("This source allocation already owns carrying-value movements.");
        var value = await CreateTransferSourceOutAsync(line, allocation.SourceInventoryWarehouseId,
            allocation.SourceLocationId, allocation.Quantity, $"TransferPick:{allocation.Id:N}:SourceOut");
        var outbound = _transferMovementCache[line.Id].Last();
        outbound.TransferDispatchAllocationId = allocation.Id;
        outbound.TransferLeg = "SourceOut";
        // The SQL transit-in guard requires the retained source-out authority first.
        await _unitOfWork.SaveChangesAsync();
        var transitIn = await CreateRetainedTransferInAsync(line, allocation.InTransitLocation.WarehouseId,
            allocation.InTransitLocationId, allocation.Quantity, value, $"TransferPick:{allocation.Id:N}:TransitIn", allocation.Id);
        transitIn.TransferDispatchAllocationId = allocation.Id;
        transitIn.TransferLeg = "TransitIn";
        var transitBalance = await GetOrCreateBalanceAsync(line.InventoryItemId,
            allocation.InTransitLocation.WarehouseId, allocation.InTransitLocationId);
        transitBalance.QuantityAllocated = transitBalance.QuantityOnHand;
        transitBalance.QuantityAvailable = 0;
        await _unitOfWork.SaveChangesAsync();
        return value;
    }

    public async Task<decimal> ProcessTransferAllocationReceiptAsync(Guid receiptAllocationId)
    {
        if (!_unitOfWork.HasActiveTransaction)
            throw new InvalidOperationException("Transfer receipt valuation requires the owned transaction.");
        var receipt = await _unitOfWork.Repository<InventoryTransferReceiptAllocation>().GetQueryable()
            .Include(x => x.InventoryTransferAction).ThenInclude(x => x.Lines).Include(x => x.DestinationLocation)
            .SingleOrDefaultAsync(x => x.Id == receiptAllocationId && x.TenantId == _currentUserProvider.TenantId && !x.IsDeleted)
            ?? throw new InvalidOperationException("The receipt allocation is unavailable in this tenant.");
        var allocation = await RequireDispatchAllocationAsync(receipt.DispatchAllocationId, false);
        var line = allocation.InventoryTransferItem;
        var transfer = line.InventoryTransfer;
        var actionLine = receipt.InventoryTransferAction.Lines.SingleOrDefault(x => x.InventoryTransferItemId == line.Id && x.TenantId == transfer.TenantId && !x.IsDeleted);
        var allowedState = receipt.InventoryTransferAction.ActionType == InventoryTransferActionType.DiscrepancyResolved
            ? transfer.Status == TransferStatus.Received : transfer.Status == TransferStatus.InTransit;
        var allowed = receipt.ReturnedToSource
            ? receipt.InventoryTransferAction.ActionType is InventoryTransferActionType.ShipmentReversed or InventoryTransferActionType.DiscrepancyResolved
            : receipt.InventoryTransferAction.ActionType is InventoryTransferActionType.Received or InventoryTransferActionType.DiscrepancyResolved;
        if (!allowed || !allowedState || actionLine is null || receipt.InventoryTransferAction.IsDeleted || receipt.InventoryTransferAction.TenantId != transfer.TenantId || receipt.InventoryTransferAction.InventoryTransferId != transfer.Id ||
            receipt.InventoryTransferAction.ActorUserId != _currentUserProvider.UserId || receipt.Quantity <= 0 ||
            receipt.DestinationLocation.TenantId != transfer.TenantId || receipt.DestinationLocation.IsDeleted ||
            !receipt.DestinationLocation.IsActive || receipt.DestinationLocation.IsInTransitLocation ||
            receipt.DestinationLocation.InventoryWarehouseId != receipt.DestinationInventoryWarehouseId ||
            receipt.DestinationLocation.WarehouseId != (receipt.ReturnedToSource ? transfer.SourceWarehouseId : transfer.DestinationWarehouseId) ||
            (receipt.ReturnedToSource && receipt.DestinationLocationId != allocation.SourceLocationId))
            throw new InvalidOperationException("Receipt allocation actor, bin, source or direction is invalid.");
        var maximum = receipt.InventoryTransferAction.ActionType == InventoryTransferActionType.Received ? actionLine.ReceivedQuantity
            : receipt.InventoryTransferAction.ActionType == InventoryTransferActionType.ShipmentReversed ? actionLine.DispatchedQuantity
            : actionLine.DamagedQuantity + actionLine.ShortageQuantity;
        var allocated = await _unitOfWork.Repository<InventoryTransferReceiptAllocation>().GetQueryable()
            .Where(x => x.TenantId == transfer.TenantId && x.InventoryTransferActionId == receipt.InventoryTransferActionId && x.DispatchAllocation.InventoryTransferItemId == line.Id && !x.IsDeleted)
            .SumAsync(x => x.Quantity);
        if (allocated > maximum) throw new InvalidOperationException("Receipt allocations exceed the recorded action quantity.");
        if (receipt.InventoryTransferAction.ActionType == InventoryTransferActionType.ShipmentReversed &&
            (receipt.Quantity != allocation.Quantity || await _unitOfWork.Repository<InventoryTransferReceiptAllocation>().GetQueryable()
                .AnyAsync(x => x.TenantId == transfer.TenantId && !x.IsDeleted && x.Id != receipt.Id &&
                    x.InventoryTransferActionId != receipt.InventoryTransferActionId &&
                    x.DispatchAllocation.InventoryTransferItem.InventoryTransferId == transfer.Id)))
            throw new InvalidOperationException("A shipment can only be reversed in full before any receipt or discrepancy resolution.");
        var ledger = await TransferValuationLedgerAsync(line.Id);
        if (ledger.Any(x => x.TransferReceiptAllocationId == receipt.Id))
            throw new InvalidOperationException("This receipt allocation already owns carrying-value movements.");
        var inbound = ledger.Where(x => x.TransferDispatchAllocationId == allocation.Id && x.TransferLeg == "TransitIn").ToList();
        var outbound = ledger.Where(x => x.TransferDispatchAllocationId == allocation.Id && x.TransferLeg == "TransitOut").ToList();
        var outstanding = inbound.Sum(x => x.Quantity) - outbound.Sum(x => x.Quantity);
        var retainedValue = inbound.Sum(x => x.TotalValue) - outbound.Sum(x => x.TotalValue);
        if (inbound.Count != 1 || outstanding < receipt.Quantity || retainedValue < 0)
            throw new InvalidOperationException("The original shipment has insufficient retained transit quantity/value; reconcile its ledger before receiving.");
        var value = receipt.Quantity == outstanding ? retainedValue
            : decimal.Round(retainedValue * receipt.Quantity / outstanding, 2, MidpointRounding.AwayFromZero);
        if (value < 0) throw new InvalidOperationException("The retained shipment value cannot be negative.");
        var transitWarehouse = allocation.InTransitLocation.WarehouseId;
        var balance = await GetOrCreateBalanceAsync(line.InventoryItemId, transitWarehouse, allocation.InTransitLocationId);
        if (balance.QuantityOnHand < receipt.Quantity || balance.TotalValue < value)
            throw new InvalidOperationException("Transit balances do not reconcile to the retained transfer allocation.");
        var item = await _unitOfWork.Repository<InventoryItem>().GetQueryable()
            .SingleAsync(x => x.Id == line.InventoryItemId && x.TenantId == transfer.TenantId && !x.IsDeleted);
        Guid? layerId = null;
        if (item.ValuationMethod == ValuationMethod.FIFO)
        {
            var layer = await _unitOfWork.Repository<InventoryLayer>().GetQueryable()
                .SingleOrDefaultAsync(x => x.TenantId == transfer.TenantId && !x.IsDeleted && x.SourceType == "TransferTransit" &&
                    x.SourceId == allocation.Id && x.LocationId == allocation.InTransitLocationId && x.WarehouseId == transitWarehouse);
            if (layer is null || layer.RemainingQuantity < receipt.Quantity || layer.RemainingValue < value)
                throw new InvalidOperationException("The original transit FIFO layer requires reconciliation.");
            layer.RemainingQuantity -= receipt.Quantity;
            layer.RemainingValue -= value;
            layer.IsFullyConsumed = layer.RemainingQuantity == 0;
            layerId = layer.Id;
        }
        balance.QuantityOnHand -= receipt.Quantity;
        balance.TotalValue -= value;
        balance.QuantityAllocated = balance.QuantityOnHand;
        balance.QuantityAvailable = 0;
        balance.AverageUnitCost = balance.QuantityOnHand > 0 ? balance.TotalValue / balance.QuantityOnHand : 0;
        balance.LastMovementDate = balance.LastRecalculatedAt = DateTime.UtcNow;
        var transitOut = await CreateMovementAsync(line.InventoryItemId, transitWarehouse, allocation.InTransitLocationId,
            InventoryMovementType.TransferOut, MovementDirection.Out, receipt.Quantity, value / receipt.Quantity,
            ReferenceType.Transfer, transfer.TransferNumber, line.Id, line.LotNumber, line.SerialNumber,
            line.ExpiryDate, $"TransferReceipt:{receipt.Id:N}:TransitOut");
        transitOut.TotalValue = value; transitOut.RunningBalance = balance.QuantityOnHand; transitOut.RunningValue = balance.TotalValue;
        transitOut.CostLayerId = layerId; transitOut.TransferDispatchAllocationId = allocation.Id;
        transitOut.TransferReceiptAllocationId = receipt.Id; transitOut.TransferLeg = "TransitOut";
        await _unitOfWork.SaveChangesAsync();
        var destinationIn = await CreateRetainedTransferInAsync(line, receipt.DestinationInventoryWarehouseId,
            receipt.DestinationLocationId, receipt.Quantity, value, $"TransferReceipt:{receipt.Id:N}:DestinationIn");
        destinationIn.TransferDispatchAllocationId = allocation.Id; destinationIn.TransferReceiptAllocationId = receipt.Id;
        destinationIn.TransferLeg = receipt.ReturnedToSource ? "SourceReturn" : "DestinationIn";
        await _unitOfWork.SaveChangesAsync();
        return value;
    }

    private async Task<InventoryTransferDispatchAllocation> RequireDispatchAllocationAsync(Guid id, bool dispatch = true)
    {
        if (!_unitOfWork.HasActiveTransaction)
            throw new InvalidOperationException("Transfer allocation valuation requires the owned transaction.");
        var allocation = await _unitOfWork.Repository<InventoryTransferDispatchAllocation>().GetQueryable()
            .Include(x => x.InventoryTransferAction).ThenInclude(x => x.Lines).Include(x => x.InventoryTransferItem).ThenInclude(x => x.InventoryTransfer)
            .Include(x => x.SourceLocation).Include(x => x.InTransitLocation)
            .SingleOrDefaultAsync(x => x.Id == id && x.TenantId == _currentUserProvider.TenantId && !x.IsDeleted)
            ?? throw new InvalidOperationException("The transfer allocation is unavailable in this tenant.");
        var transfer = allocation.InventoryTransferItem.InventoryTransfer;
        var actionLine = allocation.InventoryTransferAction.Lines.SingleOrDefault(x => x.InventoryTransferItemId == allocation.InventoryTransferItemId && x.TenantId == allocation.TenantId && !x.IsDeleted);
        if (allocation.Quantity <= 0 || actionLine is null || allocation.InventoryTransferAction.ActionType != InventoryTransferActionType.Dispatched || transfer.TenantId != allocation.TenantId || transfer.IsDeleted ||
            allocation.InventoryTransferAction.TenantId != allocation.TenantId || allocation.InventoryTransferAction.IsDeleted ||
            allocation.InventoryTransferItem.TenantId != allocation.TenantId || allocation.InventoryTransferItem.IsDeleted ||
            allocation.InventoryTransferAction.InventoryTransferId != transfer.Id ||
            (dispatch && allocation.InventoryTransferAction.ActorUserId != _currentUserProvider.UserId) ||
            allocation.SourceLocation.TenantId != allocation.TenantId || allocation.SourceLocation.IsDeleted || !allocation.SourceLocation.IsActive ||
            allocation.SourceLocation.WarehouseId != transfer.SourceWarehouseId || allocation.SourceLocation.IsInTransitLocation ||
            allocation.SourceLocation.InventoryWarehouseId != allocation.SourceInventoryWarehouseId ||
            allocation.InTransitLocation.TenantId != allocation.TenantId || allocation.InTransitLocation.IsDeleted ||
            !allocation.InTransitLocation.IsInTransitLocation || !allocation.InTransitLocation.IsActive)
            throw new InvalidOperationException("Transfer allocation ownership or exact location evidence is invalid.");
        var allocated = await _unitOfWork.Repository<InventoryTransferDispatchAllocation>().GetQueryable()
            .Where(x => x.TenantId == allocation.TenantId && x.InventoryTransferActionId == allocation.InventoryTransferActionId && x.InventoryTransferItemId == allocation.InventoryTransferItemId && !x.IsDeleted).SumAsync(x => x.Quantity);
        if (allocated > actionLine.DispatchedQuantity)
            throw new InvalidOperationException("Source picks exceed the recorded dispatch quantity.");
        return allocation;
    }
}
