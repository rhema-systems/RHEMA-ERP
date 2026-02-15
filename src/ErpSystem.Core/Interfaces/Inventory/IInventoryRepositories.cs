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
    Task<IEnumerable<StockMovement>> GetFilteredMovementsAsync(
        Guid? warehouseId = null,
        string? movementType = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? referenceNumber = null,
        int limit = 100);
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
    Task<IEnumerable<WarehouseQuantity>> GetAllWithDetailsAsync();
    Task<WarehouseQuantity?> GetByIdWithDetailsAsync(Guid id);
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
    Task<IEnumerable<StockAdjustment>> GetAllWithItemsAsync();
    Task<IEnumerable<StockAdjustment>> GetAdjustmentsByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<IEnumerable<StockAdjustment>> GetPendingAdjustmentsAsync();
    Task<StockAdjustment?> GetByAdjustmentNumberAsync(string adjustmentNumber);
    Task<StockAdjustment?> GetWithItemsAsync(Guid adjustmentId);
    Task<IEnumerable<StockAdjustment>> GetByReasonCodeAsync(string reasonCode);
    Task<string> GenerateAdjustmentNumberAsync();
    Task<IEnumerable<StockAdjustment>> GetAdjustmentsRequiringApprovalAsync();
}

/// <summary>
/// Repository interface for inventory movements (valuation source of truth)
/// </summary>
public interface IInventoryMovementRepository : IGenericRepository<InventoryMovement>
{
    Task<IEnumerable<InventoryMovement>> GetMovementsByItemAsync(Guid inventoryItemId);
    Task<IEnumerable<InventoryMovement>> GetMovementsByWarehouseAsync(Guid warehouseId);
    Task<IEnumerable<InventoryMovement>> GetMovementsByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<IEnumerable<InventoryMovement>> GetMovementsByReferenceAsync(ReferenceType referenceType, Guid? referenceId);
    Task<InventoryMovement?> GetByMovementNumberAsync(string movementNumber);
    Task<IEnumerable<InventoryMovement>> GetUnpostedMovementsAsync();
    Task<IEnumerable<InventoryMovement>> GetReversalsAsync(Guid originalMovementId);
}

/// <summary>
/// Repository interface for inventory layers (FIFO cost layers)
/// </summary>
public interface IInventoryLayerRepository : IGenericRepository<InventoryLayer>
{
    Task<IEnumerable<InventoryLayer>> GetLayersByItemAsync(Guid inventoryItemId);
    Task<IEnumerable<InventoryLayer>> GetActiveLayersAsync(Guid inventoryItemId, Guid warehouseId);
    Task<IEnumerable<InventoryLayer>> GetLayersForConsumptionAsync(Guid inventoryItemId, Guid warehouseId, Guid? locationId);
    Task<InventoryLayer?> GetByLayerNumberAsync(string layerNumber);
    Task<IEnumerable<InventoryLayer>> GetExpiringLayersAsync(DateTime beforeDate);
}

/// <summary>
/// Repository interface for inventory balances (performance cache)
/// </summary>
public interface IInventoryBalanceRepository : IGenericRepository<InventoryBalance>
{
    Task<IEnumerable<InventoryBalance>> GetBalancesByItemAsync(Guid inventoryItemId);
    Task<IEnumerable<InventoryBalance>> GetBalancesByWarehouseAsync(Guid warehouseId);
    Task<InventoryBalance?> GetBalanceAsync(Guid inventoryItemId, Guid warehouseId, Guid? locationId);
    Task<IEnumerable<InventoryBalance>> GetLowStockBalancesAsync(Guid? warehouseId = null);
    Task<IEnumerable<InventoryBalance>> GetBalancesRequiringRecalculationAsync(DateTime olderThan);
}
