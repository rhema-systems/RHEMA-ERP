using System.Text.Json;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Inventory;

public partial class InventoryTransferService
{
    private async Task<bool> ResolvePickedDiscrepancyAsync(InventoryTransfer transfer, InventoryTransferItem item,
        InventoryTransferDiscrepancy discrepancy, InventoryTransferAction action,
        List<InventoryTransferReceiptAllocationRequest>? requests, List<InventoryTransactionScanLineDto>? scans, bool toSource, string correlationId)
    {
        var picks = await _unitOfWork.Repository<InventoryTransferDispatchAllocation>().GetQueryable()
            .Include(x => x.InTransitLocation).Include(x => x.SourceLocation)
            .Where(x => x.TenantId == transfer.TenantId && x.InventoryTransferItemId == item.Id && !x.IsDeleted).ToListAsync();
        if (picks.Count == 0) return false;
        if (_valuation is null) throw new InvalidOperationException("Authoritative transfer valuation is not configured.");
        var quantity = discrepancy.DamagedQuantity + discrepancy.ShortageQuantity;
        if (requests is null || requests.Count == 0 || requests.Sum(x => x.ReceivedQuantity) != quantity ||
            requests.Any(x => x.ReceivedQuantity <= 0 || decimal.Round(x.ReceivedQuantity, 4) != x.ReceivedQuantity) ||
            requests.GroupBy(x => new { x.DispatchAllocationId, x.DestinationLocationId }).Any(x => x.Count() != 1))
            throw new ArgumentException("Allocate each resolved discrepancy to its original dispatch picks and actual return/receiving bins.");
        var warehouseId = toSource ? transfer.SourceWarehouseId : transfer.DestinationWarehouseId;
        await RevalidateLocationCapacityAsync(transfer.TenantId, requests.Select(x =>
            new LocationCapacityAddition(warehouseId, x.DestinationLocationId, item.InventoryItemId, x.ReceivedQuantity)).ToList(), "discrepancy allocation");
        var proposed = scans is { Count: > 0 } ? scans.Select(x => CopyTransferTrackingLine(x, x.BaseQuantity, x.LocationId ?? Guid.Empty)).ToList() : null;
        if (proposed is not null)
        {
            EnsureUniqueScanSerials(proposed);
            if (proposed.Sum(x => x.BaseQuantity) != quantity || proposed.Any(x => x.DocumentLineId != item.Id ||
                x.InventoryItemId != item.InventoryItemId || x.BaseQuantity <= 0 || decimal.Round(x.BaseQuantity, 4) != x.BaseQuantity ||
                !requests.Any(r => r.DestinationLocationId == x.LocationId)))
                throw new ArgumentException("Resolution scans must identify the discrepant item, actual positive quantities and exact receiving/return bins.");
        }
        foreach (var input in requests.OrderBy(x => x.DispatchAllocationId).ThenBy(x => x.DestinationLocationId))
        {
            var pick = picks.SingleOrDefault(x => x.Id == input.DispatchAllocationId)
                ?? throw new ArgumentException("The resolved allocation does not belong to the discrepant line.");
            var prior = await _unitOfWork.Repository<InventoryTransferReceiptAllocation>().GetQueryable()
                .Where(x => x.TenantId == transfer.TenantId && x.DispatchAllocationId == pick.Id && !x.IsDeleted).ToListAsync();
            if (input.ReceivedQuantity > pick.Quantity - prior.Sum(x => x.Quantity))
                throw new InvalidOperationException("Resolved quantity exceeds original retained transit stock.");
            var destination = await EnsureLocationBelongsToWarehouseAsync(input.DestinationLocationId, warehouseId, "Resolution bin");
            if (!destination.IsActive || destination.IsInTransitLocation || destination.IsQuarantineLocation || destination.IsDamageLocation ||
                destination.IsInspectionLocation || (toSource ? destination.Id != pick.SourceLocationId : !destination.IsReceivingLocation || destination.Id == pick.SourceLocationId))
                throw new ArgumentException("Use the original source bin for returns or a distinct operational receiving bin for replacements.");
            if (await IsConsignmentWarehouseAsync(destination.InventoryWarehouseId) != await IsConsignmentWarehouseAsync(pick.SourceInventoryWarehouseId))
                throw new InvalidOperationException("Discrepancy resolution cannot change inventory ownership.");
            await EnsureWarehouseLocationsAsync("procurement.inventory.transfer", warehouseId, [destination.Id], $"{transfer.TransferNumber}:resolve-allocation");
            var tracking = ReceiptAllocationTracking(item, pick, prior, input.ReceivedQuantity, destination.Id, proposed);
            var receipt = new InventoryTransferReceiptAllocation
            {
                TenantId = transfer.TenantId, InventoryTransferActionId = action.Id, DispatchAllocationId = pick.Id,
                DestinationLocationId = destination.Id, DestinationInventoryWarehouseId = destination.InventoryWarehouseId,
                Quantity = input.ReceivedQuantity, ReturnedToSource = toSource, TrackingSnapshotJson = JsonSerializer.Serialize(tracking)
            };
            await _unitOfWork.Repository<InventoryTransferReceiptAllocation>().AddAsync(receipt);
            await _unitOfWork.SaveChangesAsync();
            await StageAllocationTrackingAsync(transfer, item, pick, receipt, pick.InTransitLocation, tracking,
                InventoryTrackingDirection.TransferOut, "transit-out", correlationId);
            await StageAllocationTrackingAsync(transfer, item, pick, receipt, destination, tracking,
                InventoryTrackingDirection.TransferIn, toSource ? "source-return" : "destination-in", correlationId);
            var value = await _valuation.ProcessTransferAllocationReceiptAsync(receipt.Id);
            await ApplyTransferProjectionAsync(transfer, item, pick.InTransitLocation, -receipt.Quantity, -value, "TransferTransitOut", action, pick.Id, receipt.Id);
            await ApplyTransferProjectionAsync(transfer, item, destination, receipt.Quantity, value,
                toSource ? "TransferReversal" : "TransferIn", action, pick.Id, receipt.Id);
        }
        if (proposed is not null && proposed.Any(x => x.BaseQuantity != 0))
            throw new InvalidOperationException("Resolution scans must match the selected discrepancy allocations exactly.");
        return true;
    }
}
