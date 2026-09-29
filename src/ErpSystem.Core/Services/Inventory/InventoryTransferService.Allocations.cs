using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Procurement;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Inventory;

public partial class InventoryTransferService
{
    private static string AllocationPayloadSalt(string? prior, Guid? carrier, string? vehicle,
        IReadOnlyDictionary<Guid, List<InventoryTransferPickRequest>>? picks) => Hash(System.Text.Json.JsonSerializer.Serialize(new
        {
            prior, carrier, vehicle = Normalize(vehicle, 100),
            picks = (picks ?? new Dictionary<Guid, List<InventoryTransferPickRequest>>()).OrderBy(x => x.Key)
                .Select(x => new { itemId = x.Key, rows = x.Value.OrderBy(p => p.SourceLocationId).Select(p => new { p.SourceLocationId, p.Quantity }) })
        }));
    public async Task<IReadOnlyList<InventoryTransferPickingOptionDto>> GetPickingOptionsAsync(
        Guid transferId, CancellationToken cancellationToken = default)
    {
        var transfer = await _transferRepository.GetWithItemsAsync(transferId)
            ?? throw new ArgumentException("The transfer is not available in the current tenant.");
        await EnsureTransferAccessAsync(transfer, TransferAccessDirection.Source, "picking-options");
        var itemIds = transfer.Items.Where(x => !x.IsDeleted).Select(x => x.InventoryItemId).Distinct().ToArray();
        var candidates = await (from balance in _unitOfWork.Repository<InventoryBalance>().GetQueryable().AsNoTracking()
                                join location in _unitOfWork.Repository<WarehouseLocation>().GetQueryable().AsNoTracking()
                                    on balance.LocationId equals (Guid?)location.Id
                                where balance.TenantId == transfer.TenantId && !balance.IsDeleted &&
                                    itemIds.Contains(balance.InventoryItemId) && balance.QuantityAvailable > 0 &&
                                    location.TenantId == transfer.TenantId && !location.IsDeleted && location.IsActive &&
                                    location.WarehouseId == transfer.SourceWarehouseId && location.IsPickingLocation &&
                                    !location.IsInTransitLocation && !location.IsQuarantineLocation && !location.IsDamageLocation &&
                                    !location.IsInspectionLocation && balance.WarehouseId ==
                                        (location.IsConsignmentBin && location.ConsignmentWarehouseId.HasValue
                                            ? location.ConsignmentWarehouseId.Value : location.WarehouseId)
                                select new { balance.InventoryItemId, LocationId = location.Id, location.LocationCode,
                                    balance.QuantityOnHand, balance.QuantityAllocated, balance.QuantityAvailable })
            .ToListAsync(cancellationToken);
        var result = new List<InventoryTransferPickingOptionDto>();
        foreach (var candidate in candidates)
        {
            var decision = await _accessControl.CheckCapabilityAsync(
                BuildAccessRequest("procurement.inventory.transfer", transfer.SourceWarehouseId,
                    candidate.LocationId, $"{transfer.TransferNumber}:picking-options"), Guid.NewGuid().ToString("N"));
            if (!decision.Allowed) continue;
            foreach (var line in transfer.Items.Where(x => !x.IsDeleted && x.InventoryItemId == candidate.InventoryItemId))
                result.Add(new InventoryTransferPickingOptionDto
                {
                    ItemId = line.Id, SourceLocationId = candidate.LocationId, SourceLocationName = candidate.LocationCode,
                    QuantityOnHand = candidate.QuantityOnHand, QuantityAllocated = candidate.QuantityAllocated,
                    QuantityAvailable = candidate.QuantityAvailable
                });
        }
        return result.OrderBy(x => x.SourceLocationName).ThenBy(x => x.ItemId).ToList();
    }

    private static void ValidateOptionalInterBinLocations(Guid? source, Guid? destination)
    {
        if (source.HasValue && destination.HasValue && source == destination)
            throw new ArgumentException("Source and destination bins must be different for an inter-bin transfer.");
    }

    private async Task<BusinessPartner?> SetCarrierAsync(InventoryTransfer transfer, Guid? carrierId, string? vehicleNumber)
    {
        BusinessPartner? carrier = null;
        if (carrierId.HasValue)
        {
            carrier = await _unitOfWork.Repository<BusinessPartner>().GetQueryable()
                .SingleOrDefaultAsync(x => x.Id == carrierId && x.TenantId == transfer.TenantId && !x.IsDeleted &&
                    x.IsActive && BusinessPartnerRoles.SupplierTypes.Contains(x.PartnerType));
            if (carrier is null) throw new ArgumentException("Select an active supplier business partner in the current tenant as carrier.");
        }
        transfer.CarrierBusinessPartnerId = carrier?.Id;
        // Only new shipping preparation/dispatch calls enter here; historical displays retain their stored free text.
        transfer.CarrierName = carrier is null ? null : Normalize(carrier.PartnerName, 100);
        transfer.VehicleNumber = Normalize(vehicleNumber, 100);
        return carrier;
    }

    private async Task PopulateDispatchAllocationsAsync(InventoryTransfer transfer, InventoryTransferDetailDto dto)
    {
        dto.CarrierBusinessPartnerId = transfer.CarrierBusinessPartnerId;
        dto.VehicleNumber = transfer.VehicleNumber;
        dto.InTransitLocationId = transfer.InTransitLocationId;
        var allocations = await _unitOfWork.Repository<InventoryTransferDispatchAllocation>().GetQueryable().AsNoTracking()
            .Where(x => x.TenantId == transfer.TenantId && !x.IsDeleted && x.InventoryTransferAction.InventoryTransferId == transfer.Id)
            .Include(x => x.SourceLocation).Include(x => x.InventoryTransferAction).ToListAsync();
        var ids = allocations.Select(x => x.Id).ToArray();
        var receipts = await _unitOfWork.Repository<InventoryTransferReceiptAllocation>().GetQueryable().AsNoTracking()
            .Where(x => x.TenantId == transfer.TenantId && !x.IsDeleted && ids.Contains(x.DispatchAllocationId)).ToListAsync();
        foreach (var item in dto.Items)
        {
            item.DispatchAllocations = allocations.Where(x => x.InventoryTransferItemId == item.Id)
                .Select(x => new InventoryTransferDispatchAllocationDto
                {
                    Id = x.Id, SourceLocationId = x.SourceLocationId, SourceLocationName = x.SourceLocation.LocationCode,
                    Quantity = x.Quantity,
                    ReceivedQuantity = receipts.Where(r => r.DispatchAllocationId == x.Id && !r.ReturnedToSource).Sum(r => r.Quantity),
                    ReturnedQuantity = receipts.Where(r => r.DispatchAllocationId == x.Id && r.ReturnedToSource).Sum(r => r.Quantity),
                    OutstandingQuantity = x.Quantity - receipts.Where(r => r.DispatchAllocationId == x.Id).Sum(r => r.Quantity),
                    CarrierBusinessPartnerId = x.CarrierBusinessPartnerId, CarrierName = x.CarrierName,
                    VehicleNumber = x.VehicleNumber, ShippedAtUtc = x.InventoryTransferAction.OccurredAtUtc
                }).ToList();
        }
        dto.RequiresTransitReconciliation = dto.Items.Any(x => x.ShippedQuantity > 0 && x.DispatchAllocations.Count == 0);
    }
}
