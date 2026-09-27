using System.Text.Json;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Inventory;

public partial class InventoryTransferService
{
    private async Task DispatchPickedAllocationsAsync(InventoryTransfer transfer, InventoryTransferAction action,
        IReadOnlyDictionary<Guid, decimal> quantities, InventoryTransferMutationContext? control, string? trackingNumber, string correlationId)
    {
        if (_valuation is null) throw new InvalidOperationException("Authoritative transfer valuation is not configured.");
        var oldAllocationQuantity = await _unitOfWork.Repository<InventoryTransferDispatchAllocation>().GetQueryable()
            .Where(x => x.TenantId == transfer.TenantId && x.InventoryTransferAction.InventoryTransferId == transfer.Id && !x.IsDeleted)
            .SumAsync(x => (decimal?)x.Quantity) ?? 0;
        if (transfer.Items.Sum(x => x.ShippedQuantity) > oldAllocationQuantity)
            throw new InvalidOperationException("INV_TRANSFER_TRANSIT_RECONCILIATION_REQUIRED: historical shipments need reviewed transit reconciliation before another dispatch. Existing movements were not replayed.");
        var carrier = await SetCarrierAsync(transfer, control?.CarrierBusinessPartnerId, control?.VehicleNumber);
        foreach (var pair in quantities)
        {
            var item = transfer.Items.Single(x => x.Id == pair.Key && !x.IsDeleted);
            if (item.TotalAllocatedCost > 0 || transfer.CostAllocationMethod == SpreadToItemCost && transfer.TotalAdditionalCost > 0)
                throw new InvalidOperationException("Transfer shipping-cost capitalization requires a posted landed-cost voucher.");
            var picks = control?.Picks.GetValueOrDefault(item.Id) ?? new List<InventoryTransferPickRequest>();
            if (picks.Count == 0 && item.SourceLocationId.HasValue)
                picks = [new() { SourceLocationId = item.SourceLocationId.Value, Quantity = pair.Value }];
            if (picks.Count == 0 || picks.Any(x => x.SourceLocationId == Guid.Empty || x.Quantity <= 0 || decimal.Round(x.Quantity, 4) != x.Quantity) ||
                picks.Select(x => x.SourceLocationId).Distinct().Count() != picks.Count || picks.Sum(x => x.Quantity) != pair.Value)
                throw new ArgumentException("Select exact source bins with positive quantities adding up to the dispatched line quantity.");
            await EnsureWarehouseLocationsAsync("procurement.inventory.transfer", transfer.SourceWarehouseId,
                picks.Select(x => (Guid?)x.SourceLocationId), $"{transfer.TransferNumber}:dispatch-picks");
            foreach (var pick in picks)
            {
                var source = await EnsureLocationBelongsToWarehouseAsync(pick.SourceLocationId, transfer.SourceWarehouseId, "Source bin");
                if (!source.IsActive || !source.IsPickingLocation || source.IsInTransitLocation || source.IsQuarantineLocation ||
                    source.IsInspectionLocation || source.IsDamageLocation)
                    throw new ArgumentException("Select an active operational picking bin.");
                var ownedAsConsignment = await IsConsignmentWarehouseAsync(source.InventoryWarehouseId);
                // Ownership of the receiving bin is validated at receipt, when that bin is known.
                // A normal physical store may contain a consignment bin with a separate owner.
                var transit = await EnsureSystemTransitLocationAsync(transfer.TenantId, ownedAsConsignment);
                if (transfer.InTransitLocationId.HasValue && transfer.InTransitLocationId != transit.Id)
                    throw new InvalidOperationException("One transfer cannot mix transit ownership partitions.");
                transfer.InTransitLocationId = transit.Id;
                var tracking = DispatchPickTracking(item, pick, picks.Count);
                var allocation = new InventoryTransferDispatchAllocation
                {
                    TenantId = transfer.TenantId, InventoryTransferActionId = action.Id, InventoryTransferItemId = item.Id,
                    SourceLocationId = source.Id, SourceInventoryWarehouseId = source.InventoryWarehouseId,
                    InTransitLocationId = transit.Id, Quantity = pick.Quantity,
                    CarrierBusinessPartnerId = carrier?.Id, CarrierName = carrier?.PartnerName,
                    CarrierAccountNumber = carrier?.PartnerCode, VehicleNumber = transfer.VehicleNumber,
                    TrackingNumber = Normalize(trackingNumber, 100), TrackingSnapshotJson = JsonSerializer.Serialize(tracking)
                };
                await _unitOfWork.Repository<InventoryTransferDispatchAllocation>().AddAsync(allocation);
                await _unitOfWork.SaveChangesAsync();
                var decrease = await _negativeStockControls.PrepareDecreaseAsync(new InventoryStockDecreaseRequest
                {
                    InventoryItemId = item.InventoryItemId, WarehouseId = source.InventoryWarehouseId, LocationId = source.Id,
                    Quantity = pick.Quantity, ReferenceType = "InventoryTransfer", ReferenceNumber = transfer.TransferNumber,
                    ReferenceId = transfer.Id, ReferenceLineId = item.Id, CheckInventoryItemBalance = false,
                    NegativeStockOverrideId = control?.NegativeStockOverrideIds.GetValueOrDefault(item.Id), CorrelationId = correlationId
                });
                await StageAllocationTrackingAsync(transfer, item, allocation, null, source, tracking,
                    InventoryTrackingDirection.TransferOut, "source-out", correlationId);
                await StageAllocationTrackingAsync(transfer, item, allocation, null, transit, tracking,
                    InventoryTrackingDirection.TransferIn, "transit-in", correlationId);
                var value = await _valuation.ProcessTransferAllocationDispatchAsync(allocation.Id);
                await ApplyTransferProjectionAsync(transfer, item, source, -pick.Quantity, -value, "TransferOut", action, allocation.Id);
                await ApplyTransferProjectionAsync(transfer, item, transit, pick.Quantity, value, "TransferTransitIn", action, allocation.Id);
                if (decrease.EmergencyOverrideApplied)
                {
                    await _unitOfWork.SaveChangesAsync();
                    await _negativeStockControls.ClearMutationContextAsync();
                }
            }
            item.ShippedQuantity += pair.Value;
            item.ShipmentScanTrackingLinesJson = null;
            item.TrackingSequence++;
        }
    }

    private static List<InventoryTransactionScanLineDto> DispatchPickTracking(InventoryTransferItem line,
        InventoryTransferPickRequest pick, int pickCount)
    {
        var tracking = ReadScanTrackingLines(line.ShipmentScanTrackingLinesJson, pick.Quantity, line);
        if (!string.IsNullOrWhiteSpace(line.ShipmentScanTrackingLinesJson))
        {
            if (pickCount > 1 && tracking.Any(x => !x.LocationId.HasValue))
                throw new InvalidOperationException("Split-bin tracked dispatch requires each scanned lot/serial to retain its exact source bin.");
            if (tracking.Any(x => x.LocationId.HasValue)) tracking = tracking.Where(x => x.LocationId == pick.SourceLocationId).ToList();
        }
        if (tracking.Count == 0 || tracking.Any(x => x.BaseQuantity <= 0) || tracking.Sum(x => x.BaseQuantity) != pick.Quantity)
            throw new InvalidOperationException("The source-bin tracking snapshot must match the quantity picked from that bin.");
        return tracking;
    }

    private async Task<WarehouseLocation> EnsureSystemTransitLocationAsync(Guid tenantId, bool consignment)
    {
        await _unitOfWork.AcquireTransactionLockAsync($"inventory-transfer-transit:{tenantId:N}:{consignment}");
        var code = consignment ? "SYS-TRANSIT-CONSIGN" : "SYS-TRANSIT-OWNED";
        var warehouse = await _unitOfWork.Repository<Warehouse>().GetQueryable()
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Code == code);
        if (warehouse is null)
        {
            warehouse = new Warehouse { TenantId = tenantId, Code = code, Name = consignment ? "In transit (consignment)" : "In transit",
                WarehouseType = "Transit", IsConsignmentWarehouse = consignment, IsActive = true };
            await _unitOfWork.Repository<Warehouse>().AddAsync(warehouse);
            await _unitOfWork.SaveChangesAsync();
        }
        if (warehouse.IsDeleted || !warehouse.IsActive || warehouse.WarehouseType != "Transit" || warehouse.IsConsignmentWarehouse != consignment)
            throw new InvalidOperationException("The reserved system transit warehouse requires administrator reconciliation.");
        var location = await _unitOfWork.Repository<WarehouseLocation>().GetQueryable()
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.WarehouseId == warehouse.Id && x.LocationCode == "IN-TRANSIT");
        if (location is null)
        {
            location = new WarehouseLocation { TenantId = tenantId, WarehouseId = warehouse.Id, LocationCode = "IN-TRANSIT",
                Name = "System in-transit custody", LocationType = "InTransit", LocationHierarchyType = WarehouseLocationType.InTransit,
                IsInTransitLocation = true, IsPickingLocation = false, IsReceivingLocation = false, IsActive = true };
            await _unitOfWork.Repository<WarehouseLocation>().AddAsync(location);
            await _unitOfWork.SaveChangesAsync();
        }
        if (location.IsDeleted || !location.IsActive || !location.IsInTransitLocation || location.IsPickingLocation || location.IsReceivingLocation)
            throw new InvalidOperationException("The reserved transit location requires administrator reconciliation.");
        return location;
    }

    private async Task<bool> ReceivePickedAllocationsAsync(InventoryTransfer transfer, InventoryTransferItem item,
        InventoryTransferItemDto input, InventoryTransferAction action, string correlationId)
    {
        var dispatches = await _unitOfWork.Repository<InventoryTransferDispatchAllocation>().GetQueryable()
            .Include(x => x.InTransitLocation).Include(x => x.SourceLocation)
            .Where(x => x.TenantId == transfer.TenantId && x.InventoryTransferItemId == item.Id && !x.IsDeleted).ToListAsync();
        if (dispatches.Count == 0) return false; // Historical transactions retain their governed legacy path.
        if (_valuation is null) throw new InvalidOperationException("Authoritative transfer valuation is not configured.");
        if (input.Allocations.Count == 0 || input.Allocations.Sum(x => x.ReceivedQuantity) != input.ReceivedQuantity ||
            input.Allocations.Any(x => x.ReceivedQuantity <= 0 || decimal.Round(x.ReceivedQuantity, 4) != x.ReceivedQuantity) ||
            input.Allocations.GroupBy(x => new { x.DispatchAllocationId, x.DestinationLocationId }).Any(x => x.Count() != 1))
            throw new ArgumentException("Allocate the actual received quantity to its original dispatch picks and destination bins.");
        await RevalidateLocationCapacityAsync(transfer.TenantId,
            input.Allocations.Select(x => new LocationCapacityAddition(transfer.DestinationWarehouseId, x.DestinationLocationId,
                item.InventoryItemId, x.ReceivedQuantity)).ToList(), "allocated transfer receipt");
        var proposedTracking = string.IsNullOrWhiteSpace(item.ReceiptScanTrackingLinesJson) ? null :
            ReadRetainedTrackingSnapshot(item.ReceiptScanTrackingLinesJson);
        if (proposedTracking is not null)
        {
            EnsureUniqueScanSerials(proposedTracking);
            var destinationIds = input.Allocations.Select(x => x.DestinationLocationId).Distinct().ToList();
            if (proposedTracking.Any(x => x.InventoryItemId != item.InventoryItemId || x.DocumentLineId != item.Id ||
                    x.BaseQuantity <= 0 || decimal.Round(x.BaseQuantity, 4) != x.BaseQuantity ||
                    (x.LocationId.HasValue && !destinationIds.Contains(x.LocationId.Value))) ||
                proposedTracking.Sum(x => x.BaseQuantity) != input.ReceivedQuantity ||
                (destinationIds.Count > 1 && proposedTracking.Any(x => !x.LocationId.HasValue)))
                throw new InvalidOperationException("Scan the actual receipt quantities and identify every destination bin when using multiple bins.");
        }
        foreach (var requested in input.Allocations.OrderBy(x => x.DispatchAllocationId).ThenBy(x => x.DestinationLocationId))
        {
            var dispatch = dispatches.SingleOrDefault(x => x.Id == requested.DispatchAllocationId)
                ?? throw new ArgumentException("The dispatch allocation does not belong to this transfer line.");
            var prior = await _unitOfWork.Repository<InventoryTransferReceiptAllocation>().GetQueryable()
                .Where(x => x.TenantId == transfer.TenantId && x.DispatchAllocationId == dispatch.Id && !x.IsDeleted).ToListAsync();
            if (requested.ReceivedQuantity > dispatch.Quantity - prior.Sum(x => x.Quantity))
                throw new InvalidOperationException("Actual receipt exceeds the outstanding quantity from the selected dispatch pick.");
            var destination = await EnsureLocationBelongsToWarehouseAsync(requested.DestinationLocationId, transfer.DestinationWarehouseId, "Destination bin");
            if (!destination.IsActive || !destination.IsReceivingLocation || destination.IsInTransitLocation ||
                destination.IsQuarantineLocation || destination.IsInspectionLocation || destination.IsDamageLocation ||
                destination.Id == dispatch.SourceLocationId)
                throw new ArgumentException("Select an active operational receiving bin distinct from the source bin.");
            if (await IsConsignmentWarehouseAsync(destination.InventoryWarehouseId) != await IsConsignmentWarehouseAsync(dispatch.SourceInventoryWarehouseId))
                throw new InvalidOperationException("The receiving bin cannot change inventory ownership.");
            await EnsureWarehouseLocationsAsync("procurement.inventory.transfer", transfer.DestinationWarehouseId,
                [destination.Id], $"{transfer.TransferNumber}:receipt-allocation");
            var tracking = ReceiptAllocationTracking(item, dispatch, prior, requested.ReceivedQuantity,
                destination.Id, proposedTracking);
            var receipt = new InventoryTransferReceiptAllocation
            {
                TenantId = transfer.TenantId, InventoryTransferActionId = action.Id, DispatchAllocationId = dispatch.Id,
                DestinationLocationId = destination.Id, DestinationInventoryWarehouseId = destination.InventoryWarehouseId,
                Quantity = requested.ReceivedQuantity, TrackingSnapshotJson = JsonSerializer.Serialize(tracking)
            };
            await _unitOfWork.Repository<InventoryTransferReceiptAllocation>().AddAsync(receipt);
            await _unitOfWork.SaveChangesAsync();
            await StageAllocationTrackingAsync(transfer, item, dispatch, receipt, dispatch.InTransitLocation, tracking,
                InventoryTrackingDirection.TransferOut, "transit-out", correlationId);
            await StageAllocationTrackingAsync(transfer, item, dispatch, receipt, destination, tracking,
                InventoryTrackingDirection.TransferIn, "destination-in", correlationId);
            var value = await _valuation.ProcessTransferAllocationReceiptAsync(receipt.Id);
            await ApplyTransferProjectionAsync(transfer, item, dispatch.InTransitLocation, -receipt.Quantity, -value, "TransferTransitOut", action, dispatch.Id, receipt.Id);
            await ApplyTransferProjectionAsync(transfer, item, destination, receipt.Quantity, value, "TransferIn", action, dispatch.Id, receipt.Id);
        }
        if (proposedTracking is not null && proposedTracking.Any(x => x.BaseQuantity != 0))
            throw new InvalidOperationException("Receipt scans must match the selected dispatch allocations exactly.");
        item.ReceiptScanTrackingLinesJson = null;
        item.ReceivedQuantity += input.ReceivedQuantity;
        item.TrackingSequence++;
        if (input.Allocations.Select(x => x.DestinationLocationId).Distinct().Count() == 1)
            item.DestinationLocationId = input.Allocations[0].DestinationLocationId;
        return true;
    }

    private static List<InventoryTransactionScanLineDto> ReceiptAllocationTracking(InventoryTransferItem item,
        InventoryTransferDispatchAllocation dispatch, IReadOnlyList<InventoryTransferReceiptAllocation> prior, decimal quantity,
        Guid destinationLocationId, List<InventoryTransactionScanLineDto>? proposed)
    {
        var issued = ReadRetainedTrackingSnapshot(dispatch.TrackingSnapshotJson);
        var received = prior.SelectMany(x => ReadRetainedTrackingSnapshot(x.TrackingSnapshotJson)).ToList();
        var remaining = AggregateTrackingQuantities(issued);
        SubtractTrackingQuantities(remaining, received, "Persisted receipt tracking exceeds the selected dispatch snapshot.");
        if (proposed is null)
        {
            if (issued.Count != 1 || !string.IsNullOrWhiteSpace(issued[0].SerialNumber))
                throw new InvalidOperationException("Scan the actual serial/lot quantities for this receipt allocation.");
            var row = CopyTransferTrackingLine(issued[0], quantity, destinationLocationId);
            EnsureReceiptTrackingSnapshotMatchesDispatch(issued, received, [row]);
            return [row];
        }
        var selected = new List<InventoryTransactionScanLineDto>();
        var needed = quantity;
        foreach (var row in proposed.Where(x => x.BaseQuantity > 0 &&
                     (!x.LocationId.HasValue || x.LocationId == destinationLocationId)))
        {
            var identity = ToTrackingIdentity(row);
            if (!remaining.TryGetValue(identity, out var available) || available <= 0) continue;
            var take = Math.Min(needed, Math.Min(row.BaseQuantity, available));
            if (take <= 0) break;
            selected.Add(CopyTransferTrackingLine(row, take, destinationLocationId));
            remaining[identity] -= take;
            row.BaseQuantity -= take;
            needed -= take;
            if (needed == 0) break;
        }
        if (needed != 0)
            throw new InvalidOperationException("Receipt scans must match outstanding tracking identities from the selected dispatch pick and destination bin.");
        return selected;
    }

    private static InventoryTransactionScanLineDto CopyTransferTrackingLine(
        InventoryTransactionScanLineDto source, decimal quantity, Guid locationId) => new()
    {
        DocumentLineId = source.DocumentLineId, InventoryItemId = source.InventoryItemId,
        BaseQuantity = quantity, LocationId = locationId, LotNumber = source.LotNumber,
        BatchNumber = source.BatchNumber, SerialNumber = source.SerialNumber,
        ManufactureDate = source.ManufactureDate, ExpiryDate = source.ExpiryDate,
        InventoryTrackingExceptionId = source.InventoryTrackingExceptionId,
        DocumentLineRowVersion = source.DocumentLineRowVersion
    };

    private async Task StageAllocationTrackingAsync(InventoryTransfer transfer, InventoryTransferItem item,
        InventoryTransferDispatchAllocation dispatch, InventoryTransferReceiptAllocation? receipt, WarehouseLocation location,
        IReadOnlyList<InventoryTransactionScanLineDto> tracking, InventoryTrackingDirection direction, string leg, string correlationId)
    {
        for (var index = 0; index < tracking.Count; index++)
        {
            var row = tracking[index];
            await _trackingControls.StageEventAsync(new InventoryTrackingMutationRequest
            {
                InventoryItemId = item.InventoryItemId, WarehouseId = location.InventoryWarehouseId, LocationId = location.Id,
                Direction = direction, Quantity = row.BaseQuantity, ReferenceType = "InventoryTransfer",
                ReferenceNumber = transfer.TransferNumber, ReferenceId = transfer.Id, ReferenceLineId = item.Id,
                EventKey = $"transfer-allocation:{(receipt?.Id ?? dispatch.Id):N}:{leg}:{index + 1}",
                LotNumber = row.LotNumber, BatchNumber = row.BatchNumber, SerialNumber = row.SerialNumber,
                ManufactureDate = row.ManufactureDate, ExpiryDate = row.ExpiryDate,
                TrackingExceptionId = row.InventoryTrackingExceptionId, CorrelationId = correlationId,
                TransferDispatchAllocationId = location.IsInTransitLocation ? dispatch.Id : null,
                TransferReceiptAllocationId = location.IsInTransitLocation ? receipt?.Id : null
            });
        }
    }

    private async Task ApplyTransferProjectionAsync(InventoryTransfer transfer, InventoryTransferItem item,
        WarehouseLocation location, decimal quantity, decimal value, string movementType, InventoryTransferAction action, Guid allocationId, Guid? receiptAllocationId = null)
    {
        var balance = await _warehouseQuantityRepository.GetByWarehouseAndItemAsync(location.InventoryWarehouseId, item.InventoryItemId);
        if (balance is null)
        {
            balance = new WarehouseQuantity { TenantId = transfer.TenantId, WarehouseId = location.InventoryWarehouseId, InventoryItemId = item.InventoryItemId };
            await _warehouseQuantityRepository.AddAsync(balance);
        }
        if (balance.CurrentStock + quantity < 0 || !location.IsInTransitLocation && quantity < 0 && balance.AvailableStock + quantity < 0)
            throw new InvalidOperationException("The exact source warehouse has insufficient available stock.");
        balance.CurrentStock += quantity;
        if (location.IsInTransitLocation) { balance.AllocatedStock = balance.CurrentStock; balance.AvailableStock = 0; }
        else balance.AvailableStock += quantity;
        balance.LastMovementDate = DateTime.UtcNow;
        await AdjustInventoryLocationQuantityAsync(location.Id, item.InventoryItemId, quantity);
        if (location.IsInTransitLocation && _transferLocationBalanceCache.TryGetValue((location.Id, item.InventoryItemId), out var bin))
        { bin.AllocatedQuantity = bin.Quantity; bin.AvailableQuantity = 0; }
        if (location.IsInTransitLocation)
        {
            var companyItem = await _unitOfWork.Repository<InventoryItem>().GetQueryable()
                .SingleAsync(x => x.Id == item.InventoryItemId && x.TenantId == transfer.TenantId && !x.IsDeleted);
            if (companyItem.AllocatedStock + quantity < 0)
                throw new InvalidOperationException("Company transit reservations require reconciliation before release.");
            companyItem.AllocatedStock += quantity;
            companyItem.AvailableStock = companyItem.CurrentStock - companyItem.AllocatedStock;
        }
        var movement = new StockMovement
        {
            TransferDispatchAllocationId = allocationId, TransferReceiptAllocationId = receiptAllocationId,
            TransferLeg = movementType switch { "TransferOut" => "SourceOut", "TransferTransitIn" => "TransitIn", "TransferTransitOut" => "TransitOut", "TransferReversal" => "SourceReturn", _ => "DestinationIn" },
            TenantId = transfer.TenantId, InventoryItemId = item.InventoryItemId, WarehouseId = location.InventoryWarehouseId,
            LocationId = location.Id, MovementType = movementType, Quantity = quantity, TotalValue = value,
            UnitCost = value / quantity, ReferenceType = ReferenceType.Transfer, ReferenceNumber = transfer.TransferNumber,
            ReferenceId = transfer.Id, LotNumber = item.LotNumber, BatchNumber = item.BatchNumber, SerialNumber = item.SerialNumber,
            ManufactureDate = item.ManufactureDate, ExpirationDate = item.ExpiryDate, InventoryTrackingExceptionId = item.InventoryTrackingExceptionId,
            ProcessedById = action.ActorUserId, RunningBalance = balance.CurrentStock, Notes = $"Transfer allocation {allocationId:N}; action {action.Sequence}"
        };
        await _stockMovementRepository.AddAsync(movement);
        if (!location.IsInTransitLocation) await _consignmentSettlementService.TryCreateFromStockMovementAsync(movement);
        // Retain Added balances/layers before another pick to avoid duplicate projection rows.
        await _unitOfWork.SaveChangesAsync();
    }

    private async Task<bool> ReversePickedAllocationsAsync(InventoryTransfer transfer, InventoryTransferItem item,
        InventoryTransferAction action, string correlationId)
    {
        var dispatches = await _unitOfWork.Repository<InventoryTransferDispatchAllocation>().GetQueryable()
            .Include(x => x.SourceLocation).Include(x => x.InTransitLocation)
            .Where(x => x.TenantId == transfer.TenantId && x.InventoryTransferItemId == item.Id && !x.IsDeleted).ToListAsync();
        if (dispatches.Count == 0) return false;
        if (_valuation is null) throw new InvalidOperationException("Authoritative transfer valuation is not configured.");
        var ids = dispatches.Select(x => x.Id).ToArray();
        if (await _unitOfWork.Repository<InventoryTransferReceiptAllocation>().GetQueryable()
            .AnyAsync(x => x.TenantId == transfer.TenantId && ids.Contains(x.DispatchAllocationId) && !x.IsDeleted))
            throw new InvalidOperationException("A shipment with any received allocation cannot be reversed.");
        await RevalidateLocationCapacityAsync(transfer.TenantId, dispatches.Select(x =>
            new LocationCapacityAddition(transfer.SourceWarehouseId, x.SourceLocationId, item.InventoryItemId, x.Quantity)).ToList(), "shipment reversal");
        foreach (var dispatch in dispatches)
        {
            await EnsureWarehouseLocationsAsync("procurement.inventory.transfer", transfer.SourceWarehouseId,
                [dispatch.SourceLocationId], $"{transfer.TransferNumber}:reverse-pick");
            var tracking = JsonSerializer.Deserialize<List<InventoryTransactionScanLineDto>>(dispatch.TrackingSnapshotJson)
                ?? throw new InvalidOperationException("The original dispatch tracking snapshot requires reconciliation.");
            var receipt = new InventoryTransferReceiptAllocation
            {
                TenantId = transfer.TenantId, InventoryTransferActionId = action.Id, DispatchAllocationId = dispatch.Id,
                DestinationLocationId = dispatch.SourceLocationId, DestinationInventoryWarehouseId = dispatch.SourceInventoryWarehouseId,
                Quantity = dispatch.Quantity, ReturnedToSource = true, TrackingSnapshotJson = dispatch.TrackingSnapshotJson
            };
            await _unitOfWork.Repository<InventoryTransferReceiptAllocation>().AddAsync(receipt);
            await _unitOfWork.SaveChangesAsync();
            await StageAllocationTrackingAsync(transfer, item, dispatch, receipt, dispatch.InTransitLocation, tracking,
                InventoryTrackingDirection.TransferOut, "transit-out", correlationId);
            await StageAllocationTrackingAsync(transfer, item, dispatch, receipt, dispatch.SourceLocation, tracking,
                InventoryTrackingDirection.TransferIn, "source-return", correlationId);
            var value = await _valuation.ProcessTransferAllocationReceiptAsync(receipt.Id);
            await ApplyTransferProjectionAsync(transfer, item, dispatch.InTransitLocation, -receipt.Quantity, -value,
                "TransferTransitOut", action, dispatch.Id, receipt.Id);
            await ApplyTransferProjectionAsync(transfer, item, dispatch.SourceLocation, receipt.Quantity, value,
                "TransferReversal", action, dispatch.Id, receipt.Id);
        }
        item.ShippedQuantity = 0;
        item.TrackingSequence++;
        return true;
    }
}
