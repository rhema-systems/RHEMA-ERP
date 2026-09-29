using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Inventory;

/// <summary>Ordinary stock and master-data owners cannot mutate transfer custody storage.</summary>
public static class InventoryTransitProtection
{
    public const string Message = "System-managed in-transit stock and locations can only be changed by the controlled transfer process.";

    public static bool IsTransitType(string? value) =>
        string.Equals(value?.Trim(), "Transit", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value?.Trim(), "InTransit", StringComparison.OrdinalIgnoreCase);

    public static bool IsProtected(Warehouse warehouse) => IsTransitType(warehouse.WarehouseType);

    public static bool IsProtected(WarehouseLocation location) => location.IsInTransitLocation ||
        location.LocationHierarchyType == WarehouseLocationType.InTransit || IsTransitType(location.LocationType);

    public static async Task<bool> IsProtectedScopeAsync(IUnitOfWork unitOfWork, Guid tenantId,
        Guid warehouseId, Guid? locationId, CancellationToken cancellationToken = default)
    {
        var warehouseIds = new HashSet<Guid> { warehouseId };
        if (locationId.HasValue)
        {
            var location = await unitOfWork.Repository<WarehouseLocation>()
                .GetQueryable(x => x.TenantId == tenantId && x.Id == locationId.Value)
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
            if (location is not null)
            {
                if (IsProtected(location)) return true;
                warehouseIds.Add(location.WarehouseId);
                if (location.ConsignmentWarehouseId.HasValue) warehouseIds.Add(location.ConsignmentWarehouseId.Value);
            }
        }
        var types = await unitOfWork.Repository<Warehouse>()
            .GetQueryable(x => x.TenantId == tenantId && warehouseIds.Contains(x.Id))
            .Select(x => x.WarehouseType).ToListAsync(cancellationToken);
        return types.Any(IsTransitType);
    }

    public static async Task EnsureOrdinaryStockScopeAsync(IUnitOfWork unitOfWork, Guid tenantId,
        Guid warehouseId, Guid? locationId, CancellationToken cancellationToken = default)
    {
        if (await IsProtectedScopeAsync(unitOfWork, tenantId, warehouseId, locationId, cancellationToken))
            throw new InvalidOperationException(Message);
    }
}
