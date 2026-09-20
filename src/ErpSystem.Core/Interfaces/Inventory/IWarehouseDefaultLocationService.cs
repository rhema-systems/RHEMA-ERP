using ErpSystem.Core.Entities.Inventory;

namespace ErpSystem.Core.Interfaces.Inventory;

public interface IWarehouseDefaultLocationService
{
    Task<WarehouseLocation> GetOrCreateAsync(Guid warehouseId, Guid actorId, CancellationToken cancellationToken = default);
    Task<WarehouseLocation> EnsureItemAssignmentAsync(Guid warehouseId, Guid inventoryItemId, Guid actorId, CancellationToken cancellationToken = default);
    Task SetDefaultAsync(WarehouseLocation location, Guid actorId, CancellationToken cancellationToken = default);
}
