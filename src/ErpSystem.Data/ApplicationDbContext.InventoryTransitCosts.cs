using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data;

public partial class ApplicationDbContext
{
    private sealed record TransitCost(Guid ItemId, Guid WarehouseId, decimal Quantity, decimal Value);

    private async Task<List<TransitCost>> ReadInventoryTransitCostsAsync(Guid tenantId, Guid[] itemIds, bool asynchronous, CancellationToken token)
    {
        var lines = await ReadCostRowsAsync(Set<InventoryTransferItem>().IgnoreQueryFilters().AsNoTracking()
            .Include(value => value.InventoryTransfer).Where(value => value.TenantId == tenantId && !value.IsDeleted &&
                itemIds.Contains(value.InventoryItemId) && value.InventoryTransfer.TenantId == tenantId && !value.InventoryTransfer.IsDeleted), asynchronous, token);
        if (lines.Count == 0) return new();
        var lineIds = lines.Select(value => value.Id).ToArray();
        var movements = await ReadCostRowsAsync(Set<InventoryMovement>().IgnoreQueryFilters().AsNoTracking().Where(value =>
            value.TenantId == tenantId && value.ReferenceType == ReferenceType.Transfer && value.ReferenceId.HasValue &&
            lineIds.Contains(value.ReferenceId.Value) && value.IsPosted && !value.IsDeleted), asynchronous, token);
        var byId = movements.ToDictionary(value => value.Id);
        foreach (var entry in ChangeTracker.Entries<InventoryMovement>().Where(entry => entry.Entity.TenantId == tenantId &&
                     entry.Entity.ReferenceType == ReferenceType.Transfer && entry.Entity.ReferenceId.HasValue && lineIds.Contains(entry.Entity.ReferenceId.Value) &&
                     entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            byId.Remove(entry.Entity.Id);
            if (entry.State != EntityState.Deleted && !entry.Entity.IsDeleted && entry.Entity.IsPosted) byId[entry.Entity.Id] = entry.Entity;
        }
        var tagged = byId.Values.Where(value => value.Notes?.StartsWith("TransferAction:", StringComparison.Ordinal) == true).ToList();
        var actionIds = new Dictionary<Guid, Guid>();
        foreach (var movement in tagged)
        {
            if (!Guid.TryParseExact(movement.Notes!["TransferAction:".Length..], "N", out var actionId))
                throw new InvalidOperationException("Transfer carrying-value action identity is invalid.");
            actionIds[movement.Id] = actionId;
        }
        var ids = actionIds.Values.Distinct().ToArray();
        var actions = await ReadCostRowsAsync(Set<InventoryTransferAction>().IgnoreQueryFilters().AsNoTracking()
            .Include(value => value.Lines).Where(value => value.TenantId == tenantId && ids.Contains(value.Id) && !value.IsDeleted), asynchronous, token);
        var binIds = tagged.Where(value => value.LocationId.HasValue).Select(value => value.LocationId!.Value).Distinct().ToArray();
        var bins = await ReadCostRowsAsync(Set<WarehouseLocation>().IgnoreQueryFilters().AsNoTracking()
            .Where(value => value.TenantId == tenantId && binIds.Contains(value.Id) && !value.IsDeleted), asynchronous, token);
        var result = new List<TransitCost>();
        foreach (var line in lines)
        {
            var lineMovements = tagged.Where(value => value.ReferenceId == line.Id).ToList();
            if (byId.Values.Any(value => value.ReferenceId == line.Id && value.TransferDispatchAllocationId.HasValue))
            {
                // Allocated transfers retain transit in a real location balance. Adding the
                // old off-ledger supplement here would count the same asset twice. Never
                // reinterpret existing implicit-transit history as migrated physical stock.
                if (lineMovements.Count > 0)
                    throw new InvalidOperationException("Mixed legacy and allocated transfer carrying-value history requires reviewed reconciliation.");
                continue;
            }
            foreach (var movement in lineMovements)
            {
                var action = actions.SingleOrDefault(value => value.Id == actionIds[movement.Id] && value.InventoryTransferId == line.InventoryTransferId);
                var actionLine = action?.Lines.SingleOrDefault(value => value.TenantId == tenantId && !value.IsDeleted && value.InventoryTransferItemId == line.Id);
                var outgoing = movement.MovementType == InventoryMovementType.TransferOut && movement.Direction == MovementDirection.Out;
                var incoming = movement.MovementType == InventoryMovementType.TransferIn && movement.Direction == MovementDirection.In;
                if (action == null || actionLine == null || movement.InventoryItemId != line.InventoryItemId ||
                    movement.PostedById != action.ActorUserId || !movement.PostedAt.HasValue || movement.Quantity <= 0 || movement.TotalValue <= 0 ||
                    outgoing && action.ActionType != InventoryTransferActionType.Dispatched ||
                    incoming && action.ActionType is not (InventoryTransferActionType.Received or InventoryTransferActionType.DiscrepancyResolved or InventoryTransferActionType.ShipmentReversed) ||
                    !outgoing && !incoming)
                    throw new InvalidOperationException("Transfer carrying value is not anchored to its retained tenant/line/action.");
                var bin = bins.SingleOrDefault(value => value.Id == movement.LocationId && value.InventoryWarehouseId == movement.WarehouseId);
                // The movement retains its actual bin. A later partial receipt
                // may select another bin in that same governed warehouse; do not
                // reinterpret older movements using today's mutable line choice.
                var atSource = bin != null && bin.WarehouseId == line.InventoryTransfer.SourceWarehouseId;
                var atDestination = bin != null && bin.WarehouseId == line.InventoryTransfer.DestinationWarehouseId;
                if (outgoing && !atSource || incoming && action.ActionType == InventoryTransferActionType.Received && !atDestination ||
                    incoming && action.ActionType == InventoryTransferActionType.ShipmentReversed && !atSource ||
                    incoming && action.ActionType == InventoryTransferActionType.DiscrepancyResolved && !atSource && !atDestination)
                    throw new InvalidOperationException("Transfer carrying value does not match its retained source/destination bin.");
                var allowed = outgoing ? actionLine.DispatchedQuantity
                    : action.ActionType == InventoryTransferActionType.Received ? actionLine.ReceivedQuantity
                    : action.ActionType == InventoryTransferActionType.ShipmentReversed ? actionLine.DispatchedQuantity
                    : actionLine.DamagedQuantity + actionLine.ShortageQuantity;
                if (lineMovements.Where(value => actionIds[value.Id] == action.Id && value.Direction == movement.Direction).Sum(value => value.Quantity) > allowed)
                    throw new InvalidOperationException("Transfer carrying quantity exceeds its retained action.");
            }
            var outbound = lineMovements.Where(value => value.Direction == MovementDirection.Out).ToList();
            var inbound = lineMovements.Where(value => value.Direction == MovementDirection.In).ToList();
            if (outbound.Count == 0)
            {
                if (line.ShippedQuantity > line.ReceivedQuantity &&
                    (line.InventoryTransfer.Status == TransferStatus.InTransit || line.InventoryTransfer.HasOpenDiscrepancy))
                    throw new InvalidOperationException("An open legacy transfer lacks retained carrying value. Reconcile its valuation before updating current costs.");
                continue;
            }
            if (outbound.Select(value => value.WarehouseId).Distinct().Count() != 1)
                throw new InvalidOperationException("Transfer carrying value has conflicting source warehouse ownership.");
            var quantity = outbound.Sum(value => value.Quantity) - inbound.Sum(value => value.Quantity);
            var carryingValue = outbound.Sum(value => decimal.Round(value.TotalValue, 2, MidpointRounding.AwayFromZero)) -
                inbound.Sum(value => decimal.Round(value.TotalValue, 2, MidpointRounding.AwayFromZero));
            if (quantity < 0 || carryingValue < 0 || quantity == 0 && carryingValue != 0 || quantity > 0 && carryingValue == 0)
                throw new InvalidOperationException("Transfer carrying value does not reconcile with its remaining in-transit quantity.");
            if (quantity > 0) result.Add(new(line.InventoryItemId, outbound[0].WarehouseId, quantity, carryingValue));
        }
        return result;
    }
}
