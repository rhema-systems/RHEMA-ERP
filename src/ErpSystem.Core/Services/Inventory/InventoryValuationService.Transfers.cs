using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Inventory;

public partial class InventoryValuationService
{
    private readonly Dictionary<Guid, List<InventoryMovement>> _transferMovementCache = new();

    public async Task<decimal> ProcessTransferDispatchAsync(Guid transferItemId, Guid actionId,
        Guid warehouseId, Guid locationId, decimal quantity)
    {
        var (line, action) = await RequireTransferValuationSourceAsync(transferItemId, actionId, warehouseId, locationId, quantity, true);
        if (action.ActionType != InventoryTransferActionType.Dispatched)
            throw new InvalidOperationException("Transfer valuation requires the recorded dispatch action.");
        var ledger = await TransferValuationLedgerAsync(line.Id);
        var actionKey = TransferActionKey(action.Id);
        if (ledger.Any(value => value.Notes == actionKey && value.MovementType == InventoryMovementType.TransferOut))
            throw new InvalidOperationException("This transfer dispatch already has a carrying-value entry.");
        var item = await _unitOfWork.Repository<InventoryItem>().GetQueryable(candidate =>
            candidate.TenantId == _currentUserProvider.TenantId && candidate.Id == line.InventoryItemId && !candidate.IsDeleted).SingleAsync();
        decimal value;
        if (item.ValuationMethod == ValuationMethod.StandardCost)
        {
            // Moving an existing asset is not a revaluation at today's standard.
            // Carry its retained source value; a standard-cost change has its own
            // controlled revaluation owner and must not leak into a transfer.
            value = await ProcessWACIssueAsync(item.Id, warehouseId, locationId, quantity);
            var balance = await GetOrCreateBalanceAsync(item.Id, warehouseId, locationId);
            balance.QuantityAvailable = balance.QuantityOnHand - balance.QuantityAllocated;
            var entry = await CreateMovementAsync(item.Id, warehouseId, locationId, InventoryMovementType.TransferOut,
                MovementDirection.Out, quantity, value / quantity, ReferenceType.Transfer,
                line.InventoryTransfer.TransferNumber, line.Id, line.LotNumber, line.SerialNumber);
            entry.RunningBalance = balance.QuantityOnHand;
            entry.RunningValue = balance.TotalValue;
        }
        else
        {
            value = await ProcessIssueAsync(line.InventoryItemId, warehouseId, locationId, quantity,
                InventoryMovementType.TransferOut, ReferenceType.Transfer, line.InventoryTransfer.TransferNumber,
                line.Id, line.LotNumber, line.SerialNumber);
        }
        var movement = _transferMovementCache[line.Id].Last();
        movement.Notes = actionKey;
        // Match the amount that the existing decimal(18,2) ledger persists.
        movement.TotalValue = decimal.Round(value, 2, MidpointRounding.AwayFromZero);
        return movement.TotalValue;
    }

    public async Task<decimal> ProcessTransferReceiptAsync(Guid transferItemId, Guid actionId,
        Guid warehouseId, Guid locationId, decimal quantity, bool returnToSource = false)
    {
        var (line, action) = await RequireTransferValuationSourceAsync(transferItemId, actionId, warehouseId, locationId, quantity, returnToSource);
        if (action.ActionType is not (InventoryTransferActionType.Received or InventoryTransferActionType.DiscrepancyResolved or InventoryTransferActionType.ShipmentReversed))
            throw new InvalidOperationException("Transfer valuation requires the recorded receipt or return action.");
        if (action.ActionType == InventoryTransferActionType.Received && returnToSource ||
            action.ActionType == InventoryTransferActionType.ShipmentReversed && !returnToSource)
            throw new InvalidOperationException("The transfer action does not permit the selected receipt direction.");
        var ledger = await TransferValuationLedgerAsync(line.Id);
        var outbound = ledger.Where(value => value.MovementType == InventoryMovementType.TransferOut && value.Direction == MovementDirection.Out).ToList();
        var inbound = ledger.Where(value => value.MovementType == InventoryMovementType.TransferIn && value.Direction == MovementDirection.In).ToList();
        var remainingQuantity = outbound.Sum(value => value.Quantity) - inbound.Sum(value => value.Quantity);
        var remainingValue = outbound.Sum(value => value.TotalValue) - inbound.Sum(value => value.TotalValue);
        if (outbound.Count == 0 || quantity > remainingQuantity || remainingValue <= 0)
            throw new InvalidOperationException("The transfer has no sufficient retained outbound carrying value. Legacy shipments require a reviewed valuation reconciliation before receipt.");
        var actionLine = action.Lines.Single(value => value.InventoryTransferItemId == line.Id);
        var allowed = action.ActionType == InventoryTransferActionType.Received ? actionLine.ReceivedQuantity
            : action.ActionType == InventoryTransferActionType.ShipmentReversed ? actionLine.DispatchedQuantity
            : actionLine.DamagedQuantity + actionLine.ShortageQuantity;
        var receivedForAction = inbound.Where(value => value.Notes == TransferActionKey(action.Id)).Sum(value => value.Quantity);
        if (receivedForAction + quantity > allowed)
            throw new InvalidOperationException("The transfer receipt exceeds its recorded action quantity.");
        var value = quantity == remainingQuantity ? remainingValue
            : decimal.Round(remainingValue * quantity / remainingQuantity, 2, MidpointRounding.AwayFromZero);
        if (value <= 0) throw new InvalidOperationException("The transfer receipt quantity is too small for the retained currency value.");
        var item = await _unitOfWork.Repository<InventoryItem>().GetQueryable(candidate =>
            candidate.TenantId == _currentUserProvider.TenantId && candidate.Id == line.InventoryItemId && !candidate.IsDeleted).SingleAsync();
        if (item.ValuationMethod is not (ValuationMethod.FIFO or ValuationMethod.WeightedAverage or ValuationMethod.StandardCost))
            throw new InvalidOperationException("This transfer valuation method is not supported.");
        var unitCost = value / quantity;
        Guid? layerId = null;
        if (item.ValuationMethod == ValuationMethod.FIFO)
        {
            var layer = await CreateFIFOLayerAsync(item.Id, warehouseId, locationId, quantity, unitCost,
                "Transfer", line.InventoryTransfer.TransferNumber, line.Id, line.LotNumber, line.ExpiryDate);
            layer.RemainingValue = value;
            // Unlike a count-derived item suffix this remains unique for multiple
            // pending receipts of the same item before the transaction saves.
            layer.LayerNumber = $"TR-{layer.Id:N}";
            layerId = layer.Id;
        }
        var balance = await GetOrCreateBalanceAsync(item.Id, warehouseId, locationId);
        balance.QuantityOnHand += quantity;
        balance.TotalValue += value;
        balance.QuantityAvailable = balance.QuantityOnHand - balance.QuantityAllocated;
        balance.AverageUnitCost = balance.QuantityOnHand > 0 ? balance.TotalValue / balance.QuantityOnHand : 0;
        balance.LastReceiptDate = balance.LastMovementDate = balance.LastRecalculatedAt = DateTime.UtcNow;
        var movement = await CreateMovementAsync(item.Id, warehouseId, locationId, InventoryMovementType.TransferIn,
            MovementDirection.In, quantity, unitCost, ReferenceType.Transfer, line.InventoryTransfer.TransferNumber,
            line.Id, line.LotNumber, line.SerialNumber, line.ExpiryDate, TransferActionKey(action.Id));
        movement.TotalValue = value;
        movement.RunningBalance = balance.QuantityOnHand;
        movement.RunningValue = balance.TotalValue;
        movement.CostLayerId = layerId;
        return value;
    }

    private async Task<(InventoryTransferItem Line, InventoryTransferAction Action)> RequireTransferValuationSourceAsync(
        Guid lineId, Guid actionId, Guid warehouseId, Guid locationId, decimal quantity, bool source)
    {
        if (!_unitOfWork.HasActiveTransaction || quantity <= 0 || locationId == Guid.Empty)
            throw new InvalidOperationException("A governed transfer valuation needs an owned transaction, positive quantity and exact bin.");
        var tenant = _currentUserProvider.TenantId;
        var line = await _unitOfWork.Repository<InventoryTransferItem>().GetQueryable(value =>
            value.Id == lineId && value.TenantId == tenant && !value.IsDeleted)
            .Include(value => value.InventoryTransfer).SingleOrDefaultAsync()
            ?? throw new InvalidOperationException("The transfer valuation line is not available in this tenant.");
        var action = await _unitOfWork.Repository<InventoryTransferAction>().GetQueryable(value =>
            value.Id == actionId && value.TenantId == tenant && value.InventoryTransferId == line.InventoryTransferId && !value.IsDeleted)
            .Include(value => value.Lines).SingleOrDefaultAsync()
            ?? throw new InvalidOperationException("The transfer valuation action is not retained.");
        if (line.InventoryTransfer.TenantId != tenant || line.InventoryTransfer.IsDeleted ||
            action.ActorUserId != _currentUserProvider.UserId || action.Lines.Count(value =>
                value.InventoryTransferItemId == line.Id && value.TenantId == tenant && !value.IsDeleted) != 1)
            throw new InvalidOperationException("The transfer valuation action does not own this line and actor.");
        var allowedState = action.ActionType switch
        {
            InventoryTransferActionType.Dispatched => line.InventoryTransfer.Status is TransferStatus.Approved or TransferStatus.InTransit,
            InventoryTransferActionType.Received or InventoryTransferActionType.ShipmentReversed => line.InventoryTransfer.Status == TransferStatus.InTransit,
            InventoryTransferActionType.DiscrepancyResolved => line.InventoryTransfer.Status == TransferStatus.Received,
            _ => false
        };
        if (!allowedState)
            throw new InvalidOperationException("The transfer lifecycle does not permit this carrying-value action.");
        var ownership = await _unitOfWork.Repository<Warehouse>().GetQueryable(value => value.TenantId == tenant && !value.IsDeleted &&
            (value.Id == line.InventoryTransfer.SourceWarehouseId || value.Id == line.InventoryTransfer.DestinationWarehouseId)).ToListAsync();
        var ownerIds = new[] { line.InventoryTransfer.SourceWarehouseId, line.InventoryTransfer.DestinationWarehouseId }.Distinct().ToArray();
        if (ownership.Count != ownerIds.Length)
            throw new InvalidOperationException("The transfer warehouse ownership is missing or unavailable in this tenant.");
        if (ownership.Select(value => value.IsConsignmentWarehouse).Distinct().Count() > 1)
            throw new InvalidOperationException("A transfer cannot change consignment ownership. Use the controlled ownership/settlement process instead.");
        var location = await _unitOfWork.Repository<WarehouseLocation>().GetQueryable(value =>
            value.Id == locationId && value.TenantId == tenant && !value.IsDeleted && value.IsActive).SingleOrDefaultAsync();
        if (location == null || location.InventoryWarehouseId != warehouseId || location.WarehouseId !=
            (source ? line.InventoryTransfer.SourceWarehouseId : line.InventoryTransfer.DestinationWarehouseId) ||
            (source ? line.SourceLocationId : line.DestinationLocationId) != locationId)
            throw new InvalidOperationException("Transfer valuation must use the recorded exact warehouse/bin.");
        var actionLine = action.Lines.Single(value => value.InventoryTransferItemId == line.Id);
        if (action.ActionType == InventoryTransferActionType.Dispatched && actionLine.DispatchedQuantity != quantity)
            throw new InvalidOperationException("The transfer carrying quantity differs from its recorded dispatch.");
        return (line, action);
    }

    private async Task<List<InventoryMovement>> TransferValuationLedgerAsync(Guid lineId)
    {
        var saved = await _unitOfWork.Repository<InventoryMovement>().GetQueryable(value =>
            value.TenantId == _currentUserProvider.TenantId && value.ReferenceType == ReferenceType.Transfer &&
            value.ReferenceId == lineId && value.IsPosted && !value.IsDeleted).ToListAsync();
        var result = saved.ToDictionary(value => value.Id);
        if (_transferMovementCache.TryGetValue(lineId, out var pending))
            foreach (var movement in pending) result[movement.Id] = movement;
        return result.Values.ToList();
    }

    private static string TransferActionKey(Guid actionId) => $"TransferAction:{actionId:N}";
}
