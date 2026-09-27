using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Inventory;

public partial class InventoryTransferService
{
    public async Task<InventoryTransitStockReportDto> GetTransitStockAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUserProvider.TenantId;
        var report = new InventoryTransitStockReportDto();
        var postedLineIds = _unitOfWork.Repository<InventoryMovement>()
            .GetQueryable(x => x.TenantId == tenantId && !x.IsDeleted && x.IsPosted &&
                x.ReferenceType == ReferenceType.Transfer && x.ReferenceId.HasValue)
            .Select(x => x.ReferenceId!.Value);
        var transfers = _transferRepository.GetQueryable(x => x.TenantId == tenantId && !x.IsDeleted &&
                x.SourceWarehouse.TenantId == tenantId && x.DestinationWarehouse.TenantId == tenantId &&
                x.Items.Any(line => !line.IsDeleted && line.TenantId == tenantId && postedLineIds.Contains(line.Id)))
            .AsNoTracking().Include(x => x.SourceWarehouse).Include(x => x.DestinationWarehouse)
            .Include(x => x.Items).OrderBy(x => x.Id);

        // Page by transfer before loading evidence; authorize both ends before exposing any ledger data.
        // Status is deliberately not a stock filter: closing a document cannot clear physical transit.
        for (var offset = 0; ; offset += 100)
        {
            var page = await transfers.Skip(offset).Take(100).ToListAsync(cancellationToken);
            var readable = new List<InventoryTransfer>();
            foreach (var transfer in page)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (await CanReadAsync(transfer)) readable.Add(transfer);
            }
            var lines = readable.SelectMany(x => x.Items)
                .Where(x => x.TenantId == tenantId && !x.IsDeleted).ToDictionary(x => x.Id);
            if (lines.Count > 0)
            {
                var lineIds = lines.Keys.ToList();
                var allocations = await _unitOfWork.Repository<InventoryTransferDispatchAllocation>()
                    .GetQueryable(x => x.TenantId == tenantId && !x.IsDeleted && lineIds.Contains(x.InventoryTransferItemId) &&
                        x.InventoryTransferAction.TenantId == tenantId && !x.InventoryTransferAction.IsDeleted &&
                        x.InventoryTransferAction.ActionType == InventoryTransferActionType.Dispatched &&
                        x.InventoryTransferAction.InventoryTransferId == x.InventoryTransferItem.InventoryTransferId)
                    .AsNoTracking().Include(x => x.InventoryTransferAction)
                    .Include(x => x.SourceLocation).Include(x => x.InTransitLocation).ThenInclude(x => x.Warehouse)
                    .ToListAsync(cancellationToken);
                var movements = await _unitOfWork.Repository<InventoryMovement>()
                    .GetQueryable(x => x.TenantId == tenantId && !x.IsDeleted && x.IsPosted &&
                        x.ReferenceType == ReferenceType.Transfer && x.ReferenceId.HasValue && lineIds.Contains(x.ReferenceId.Value))
                    .AsNoTracking().ToListAsync(cancellationToken);
                var ledgerByAllocation = movements.Where(x => x.TransferDispatchAllocationId.HasValue)
                    .ToLookup(x => x.TransferDispatchAllocationId!.Value);
                var transferById = readable.ToDictionary(x => x.Id);

                foreach (var allocation in allocations)
                {
                    var line = lines[allocation.InventoryTransferItemId];
                    var transfer = transferById[line.InventoryTransferId];
                    if (allocation.SourceLocation.TenantId != tenantId || allocation.InTransitLocation.TenantId != tenantId ||
                        allocation.InTransitLocation.Warehouse.TenantId != tenantId)
                        continue;
                    var ledger = ledgerByAllocation[allocation.Id].Where(x => x.ReferenceId == line.Id &&
                        x.InventoryItemId == line.InventoryItemId).ToList();
                    var transit = ledger.Where(x => x.LocationId == allocation.InTransitLocationId &&
                        x.WarehouseId == allocation.InTransitLocation.WarehouseId).ToList();
                    var incoming = transit.Where(x => x.TransferLeg == "TransitIn" && x.Direction == MovementDirection.In).ToList();
                    var outgoing = transit.Where(x => x.TransferLeg == "TransitOut" && x.Direction == MovementDirection.Out).ToList();
                    var quantity = incoming.Sum(x => x.Quantity) - outgoing.Sum(x => x.Quantity);
                    var value = incoming.Sum(x => x.TotalValue) - outgoing.Sum(x => x.TotalValue);
                    if (quantity == 0 && value == 0) continue;
                    // Retain abnormal negative balances in the report rather than silently hiding a reconciliation problem.
                    report.Items.Add(new InventoryTransitStockRowDto
                    {
                        TransferId = transfer.Id, TransferNumber = transfer.TransferNumber,
                        TransferItemId = line.Id, DispatchAllocationId = allocation.Id,
                        ItemId = line.InventoryItemId, ItemCode = line.ItemCode ?? string.Empty, ItemName = line.ItemName ?? string.Empty,
                        SourceWarehouseId = transfer.SourceWarehouseId, SourceWarehouseName = transfer.SourceWarehouse.Name,
                        DestinationWarehouseId = transfer.DestinationWarehouseId, DestinationWarehouseName = transfer.DestinationWarehouse.Name,
                        SourceLocationId = allocation.SourceLocationId,
                        SourceLocationName = allocation.SourceLocation.Name ?? allocation.SourceLocation.LocationCode,
                        InTransitLocationId = allocation.InTransitLocationId,
                        InTransitLocationName = allocation.InTransitLocation.Name ?? allocation.InTransitLocation.LocationCode,
                        InTransitWarehouseId = allocation.InTransitLocation.WarehouseId,
                        InTransitWarehouseName = allocation.InTransitLocation.Warehouse.Name,
                        CarrierBusinessPartnerId = allocation.CarrierBusinessPartnerId, CarrierName = allocation.CarrierName,
                        VehicleNumber = allocation.VehicleNumber, ShippedAtUtc = allocation.InventoryTransferAction.OccurredAtUtc,
                        RequestedQuantity = line.RequestedQuantity, DispatchedQuantity = incoming.Sum(x => x.Quantity),
                        ReceivedQuantity = ledger.Where(x => x.TransferLeg == "DestinationIn" && x.Direction == MovementDirection.In).Sum(x => x.Quantity),
                        ReturnedQuantity = ledger.Where(x => x.TransferLeg == "SourceReturn" && x.Direction == MovementDirection.In).Sum(x => x.Quantity),
                        InTransitQuantity = quantity, InTransitValue = value
                    });
                }

                // Pre-allocation transfers have no physical transit legs. Flag outstanding original evidence;
                // do not invent balances or blend a status-derived estimate into the company valuation.
                report.LegacyReconciliationRequiredCount += movements.Where(x => !x.TransferDispatchAllocationId.HasValue)
                    .GroupBy(x => x.ReferenceId!.Value)
                    .Count(group => group.Where(x => x.MovementType == InventoryMovementType.TransferOut).Sum(x => x.Quantity) >
                                    group.Where(x => x.MovementType == InventoryMovementType.TransferIn).Sum(x => x.Quantity));
            }
            if (page.Count < 100) break;
        }
        report.Items = report.Items.OrderBy(x => x.ShippedAtUtc).ThenBy(x => x.TransferNumber)
            .ThenBy(x => x.DispatchAllocationId).ToList();
        return report;
    }
}
