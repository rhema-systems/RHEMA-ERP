using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;

namespace ErpSystem.Core.Interfaces.Inventory;

/// <summary>
/// Repository interface for inventory items
/// </summary>
public interface IInventoryItemRepository : IGenericRepository<InventoryItem>
{
    Task<IEnumerable<InventoryItem>> GetActiveItemsAsync();
    Task<InventoryItem?> GetByIdWithDetailsAsync(Guid id);
    Task<InventoryItem?> GetByItemCodeAsync(string itemCode);
    Task<IEnumerable<InventoryItem>> GetItemsByCategoryAsync(Guid categoryId);
    Task<IEnumerable<InventoryItem>> GetItemsBelowReorderLevelAsync();
    Task<IEnumerable<InventoryItem>> SearchItemsAsync(string searchTerm);
}

/// <summary>
/// Repository interface for inventory categories
/// </summary>
public interface IInventoryCategoryRepository : IGenericRepository<InventoryCategory>
{
    Task<IEnumerable<InventoryCategory>> GetActiveCategoriesAsync();
    Task<IEnumerable<InventoryCategory>> GetRootCategoriesAsync();
    Task<IEnumerable<InventoryCategory>> GetSubCategoriesAsync(Guid parentCategoryId);
}

/// <summary>
/// Repository interface for stock movements
/// </summary>
public interface IStockMovementRepository : IGenericRepository<StockMovement>
{
    Task<IEnumerable<StockMovement>> GetMovementsByItemAsync(Guid inventoryItemId);
    Task<IEnumerable<StockMovement>> GetRecentMovementsAsync(Guid inventoryItemId, int count);
    Task<IEnumerable<StockMovement>> GetMovementsByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<IEnumerable<StockMovement>> GetMovementsByReferenceAsync(ReferenceType referenceType, string referenceNumber);
    Task<IEnumerable<StockMovement>> GetMovementsByLocationAsync(Guid locationId);
}

/// <summary>
/// Repository interface for inventory locations
/// </summary>
public interface IInventoryLocationRepository : IGenericRepository<InventoryLocation>
{
    Task<IEnumerable<InventoryLocation>> GetByInventoryItemAsync(Guid inventoryItemId);
    Task<IEnumerable<InventoryLocation>> GetByLocationAsync(Guid locationId);
    Task<InventoryLocation?> GetByLocationAndItemAsync(Guid locationId, Guid inventoryItemId);
    Task<IEnumerable<InventoryLocation>> GetLocationsWithStockAsync();
}

/// <summary>
/// Repository interface for inventory allocations
/// </summary>
public interface IInventoryAllocationRepository : IGenericRepository<InventoryAllocation>
{
    Task<IEnumerable<InventoryAllocation>> GetActiveAllocationsAsync();
    Task<IEnumerable<InventoryAllocation>> GetAllocationsByItemAsync(Guid inventoryItemId);
    Task<IEnumerable<InventoryAllocation>> GetAllocationsByReferenceAsync(string referenceType, Guid referenceId);
    Task<IEnumerable<InventoryAllocation>> GetExpiredAllocationsAsync();
}

/// <summary>
/// Repository interface for warehouses
/// </summary>
public interface IWarehouseRepository : IGenericRepository<Warehouse>
{
    Task<IEnumerable<Warehouse>> GetActiveWarehousesAsync();
    Task<Warehouse?> GetDefaultWarehouseAsync();
    Task<Warehouse?> GetByCodeAsync(string code);
}

/// <summary>
/// Repository interface for warehouse locations
/// </summary>
public interface IWarehouseLocationRepository : IGenericRepository<WarehouseLocation>
{
    Task<IEnumerable<WarehouseLocation>> GetLocationsByWarehouseAsync(Guid warehouseId);
    Task<IEnumerable<WarehouseLocation>> GetPickingLocationsAsync(Guid warehouseId);
    Task<IEnumerable<WarehouseLocation>> GetReceivingLocationsAsync(Guid warehouseId);
    Task<WarehouseLocation?> GetByLocationCodeAsync(string locationCode);
    Task<IEnumerable<WarehouseLocation>> GetAvailableLocationsAsync(Guid warehouseId);
}

/// <summary>
/// Repository interface for warehouse quantities
/// </summary>
public interface IWarehouseQuantityRepository : IGenericRepository<WarehouseQuantity>
{
    Task<IEnumerable<WarehouseQuantity>> GetByWarehouseAsync(Guid warehouseId);
    Task<IEnumerable<WarehouseQuantity>> GetByWarehouseAndItemTypeAsync(Guid warehouseId, int itemType);
    Task<WarehouseQuantity?> GetByWarehouseAndItemAsync(Guid warehouseId, Guid inventoryItemId);
    Task<IEnumerable<WarehouseQuantity>> GetByInventoryItemIdAsync(Guid inventoryItemId);
    Task<IEnumerable<WarehouseQuantity>> GetItemsWithStockAsync(Guid warehouseId, int? itemType = null);
    Task<IEnumerable<WarehouseQuantity>> GetItemsBelowReorderLevelAsync(Guid warehouseId);
}

/// <summary>
/// Repository interface for stock adjustments
/// </summary>
public interface IStockAdjustmentRepository : IGenericRepository<StockAdjustment>
{
    Task<IEnumerable<StockAdjustment>> GetAdjustmentsByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<IEnumerable<StockAdjustment>> GetPendingAdjustmentsAsync();
    Task<StockAdjustment?> GetByAdjustmentNumberAsync(string adjustmentNumber);
    Task<StockAdjustment?> GetWithItemsAsync(Guid adjustmentId);
}
