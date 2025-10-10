using Microsoft.EntityFrameworkCore;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Data.Repositories;

namespace ErpSystem.Data.Repositories.Inventory;

#region Inventory Management Repository Implementations

public class InventoryItemRepository : GenericRepository<InventoryItem>, IInventoryItemRepository
{
    public InventoryItemRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<InventoryItem>> GetActiveItemsAsync()
    {
        return await _dbSet
            .Where(i => i.Status == ItemStatus.Active && !i.IsDeleted)
            .Include(i => i.Category)
            .OrderBy(i => i.ItemCode)
            .ToListAsync();
    }

    public async Task<InventoryItem?> GetByIdWithDetailsAsync(Guid id)
    {
        if (id == Guid.Empty)
            return null;
            
        return await _dbSet
            .Where(i => i.Id == id && !i.IsDeleted)
            .Include(i => i.Category)
            .Include(i => i.InventoryLocations)
                .ThenInclude(il => il.Location)
                    .ThenInclude(l => l.Warehouse)
            .Include(i => i.StockMovements.OrderByDescending(sm => sm.MovementDate).Take(10))
            .Include(i => i.Allocations.Where(a => a.Status == "Active"))
            .FirstOrDefaultAsync();
    }

    public async Task<InventoryItem?> GetByItemCodeAsync(string itemCode)
    {
        if (string.IsNullOrWhiteSpace(itemCode))
            return null;
            
        return await _dbSet
            .Where(i => i.ItemCode == itemCode && !i.IsDeleted)
            .Include(i => i.Category)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<InventoryItem>> GetItemsByCategoryAsync(Guid categoryId)
    {
        if (categoryId == Guid.Empty)
            return new List<InventoryItem>();
            
        return await _dbSet
            .Where(i => i.CategoryId == categoryId && !i.IsDeleted)
            .Include(i => i.Category)
            .OrderBy(i => i.ItemCode)
            .ToListAsync();
    }

    public async Task<IEnumerable<InventoryItem>> GetItemsBelowReorderLevelAsync()
    {
        return await _dbSet
            .Where(i => !i.IsDeleted && 
                       i.Status == ItemStatus.Active &&
                       i.CurrentStock <= i.ReorderLevel)
            .Include(i => i.Category)
            .OrderBy(i => i.CurrentStock)
            .ThenBy(i => i.ItemCode)
            .ToListAsync();
    }

    public async Task<IEnumerable<InventoryItem>> SearchItemsAsync(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return new List<InventoryItem>();
            
        var lowerSearchTerm = searchTerm.ToLower();
        return await _dbSet
            .Where(i => !i.IsDeleted &&
                       (i.ItemCode.ToLower().Contains(lowerSearchTerm) ||
                        i.Name.ToLower().Contains(lowerSearchTerm) ||
                        (i.Description != null && i.Description.ToLower().Contains(lowerSearchTerm)) ||
                        (i.Brand != null && i.Brand.ToLower().Contains(lowerSearchTerm)) ||
                        (i.Manufacturer != null && i.Manufacturer.ToLower().Contains(lowerSearchTerm))))
            .Include(i => i.Category)
            .OrderBy(i => i.ItemCode)
            .ToListAsync();
    }
}

public class InventoryCategoryRepository : GenericRepository<InventoryCategory>, IInventoryCategoryRepository
{
    public InventoryCategoryRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<InventoryCategory>> GetActiveCategoriesAsync()
    {
        return await _dbSet
            .Where(c => c.IsActive && !c.IsDeleted)
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<InventoryCategory>> GetRootCategoriesAsync()
    {
        return await _dbSet
            .Where(c => c.ParentCategoryId == null && !c.IsDeleted)
            .Include(c => c.SubCategories.Where(sc => sc.IsActive))
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<InventoryCategory>> GetSubCategoriesAsync(Guid parentCategoryId)
    {
        if (parentCategoryId == Guid.Empty)
            return new List<InventoryCategory>();
            
        return await _dbSet
            .Where(c => c.ParentCategoryId == parentCategoryId && !c.IsDeleted)
            .OrderBy(c => c.Name)
            .ToListAsync();
    }
}

public class StockMovementRepository : GenericRepository<StockMovement>, IStockMovementRepository
{
    public StockMovementRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<StockMovement>> GetMovementsByItemAsync(Guid inventoryItemId)
    {
        if (inventoryItemId == Guid.Empty)
            return new List<StockMovement>();
            
        return await _dbSet
            .Where(sm => sm.InventoryItemId == inventoryItemId && !sm.IsDeleted)
            .Include(sm => sm.InventoryItem)
            .Include(sm => sm.Location)
                .ThenInclude(l => l!.Warehouse)
            .OrderByDescending(sm => sm.MovementDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StockMovement>> GetRecentMovementsAsync(Guid inventoryItemId, int count)
    {
        if (inventoryItemId == Guid.Empty)
            return new List<StockMovement>();
            
        return await _dbSet
            .Where(sm => sm.InventoryItemId == inventoryItemId && !sm.IsDeleted)
            .Include(sm => sm.InventoryItem)
            .Include(sm => sm.Location)
            .OrderByDescending(sm => sm.MovementDate)
            .Take(count)
            .ToListAsync();
    }

    public async Task<IEnumerable<StockMovement>> GetMovementsByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        if (startDate >= endDate)
            return new List<StockMovement>();
            
        return await _dbSet
            .Where(sm => !sm.IsDeleted &&
                        sm.MovementDate >= startDate &&
                        sm.MovementDate <= endDate)
            .Include(sm => sm.InventoryItem)
            .Include(sm => sm.Location)
            .OrderByDescending(sm => sm.MovementDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StockMovement>> GetMovementsByReferenceAsync(ReferenceType referenceType, string referenceNumber)
    {
        if (string.IsNullOrWhiteSpace(referenceNumber))
            return new List<StockMovement>();
            
        return await _dbSet
            .Where(sm => !sm.IsDeleted &&
                        sm.ReferenceType == referenceType &&
                        sm.ReferenceNumber == referenceNumber)
            .Include(sm => sm.InventoryItem)
            .Include(sm => sm.Location)
            .OrderByDescending(sm => sm.MovementDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StockMovement>> GetMovementsByLocationAsync(Guid locationId)
    {
        if (locationId == Guid.Empty)
            return new List<StockMovement>();
            
        return await _dbSet
            .Where(sm => sm.LocationId == locationId && !sm.IsDeleted)
            .Include(sm => sm.InventoryItem)
            .Include(sm => sm.Location)
            .OrderByDescending(sm => sm.MovementDate)
            .ToListAsync();
    }
}

public class InventoryLocationRepository : GenericRepository<InventoryLocation>, IInventoryLocationRepository
{
    public InventoryLocationRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<InventoryLocation>> GetByInventoryItemAsync(Guid inventoryItemId)
    {
        if (inventoryItemId == Guid.Empty)
            return new List<InventoryLocation>();
            
        return await _dbSet
            .Where(il => il.InventoryItemId == inventoryItemId && !il.IsDeleted)
            .Include(il => il.InventoryItem)
            .Include(il => il.Location)
                .ThenInclude(l => l.Warehouse)
            .Where(il => il.Quantity > 0) // Only locations with stock
            .OrderBy(il => il.Location.Warehouse.Name)
            .ThenBy(il => il.Location.LocationCode)
            .ToListAsync();
    }

    public async Task<IEnumerable<InventoryLocation>> GetByLocationAsync(Guid locationId)
    {
        if (locationId == Guid.Empty)
            return new List<InventoryLocation>();
            
        return await _dbSet
            .Where(il => il.LocationId == locationId && !il.IsDeleted)
            .Include(il => il.InventoryItem)
                .ThenInclude(i => i.Category)
            .Include(il => il.Location)
            .Where(il => il.Quantity > 0)
            .OrderBy(il => il.InventoryItem.ItemCode)
            .ToListAsync();
    }

    public async Task<InventoryLocation?> GetByLocationAndItemAsync(Guid locationId, Guid inventoryItemId)
    {
        if (locationId == Guid.Empty || inventoryItemId == Guid.Empty)
            return null;
            
        return await _dbSet
            .Where(il => il.LocationId == locationId && 
                        il.InventoryItemId == inventoryItemId && 
                        !il.IsDeleted)
            .Include(il => il.InventoryItem)
            .Include(il => il.Location)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<InventoryLocation>> GetLocationsWithStockAsync()
    {
        return await _dbSet
            .Where(il => !il.IsDeleted && il.Quantity > 0)
            .Include(il => il.InventoryItem)
                .ThenInclude(i => i.Category)
            .Include(il => il.Location)
                .ThenInclude(l => l.Warehouse)
            .OrderBy(il => il.Location.Warehouse.Name)
            .ThenBy(il => il.Location.LocationCode)
            .ThenBy(il => il.InventoryItem.ItemCode)
            .ToListAsync();
    }
}

public class InventoryAllocationRepository : GenericRepository<InventoryAllocation>, IInventoryAllocationRepository
{
    public InventoryAllocationRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<InventoryAllocation>> GetActiveAllocationsAsync()
    {
        return await _dbSet
            .Where(ia => ia.Status == "Active" && !ia.IsDeleted)
            .Include(ia => ia.InventoryItem)
            .Include(ia => ia.Location)
                .ThenInclude(l => l.Warehouse)
            .OrderBy(ia => ia.RequiredDate)
            .ThenBy(ia => ia.InventoryItem.ItemCode)
            .ToListAsync();
    }

    public async Task<IEnumerable<InventoryAllocation>> GetAllocationsByItemAsync(Guid inventoryItemId)
    {
        if (inventoryItemId == Guid.Empty)
            return new List<InventoryAllocation>();
            
        return await _dbSet
            .Where(ia => ia.InventoryItemId == inventoryItemId && !ia.IsDeleted)
            .Include(ia => ia.InventoryItem)
            .Include(ia => ia.Location)
            .OrderBy(ia => ia.RequiredDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<InventoryAllocation>> GetAllocationsByReferenceAsync(string referenceType, Guid referenceId)
    {
        if (string.IsNullOrWhiteSpace(referenceType) || referenceId == Guid.Empty)
            return new List<InventoryAllocation>();
            
        return await _dbSet
            .Where(ia => ia.AllocationType == referenceType && 
                        ia.ReferenceId == referenceId && 
                        !ia.IsDeleted)
            .Include(ia => ia.InventoryItem)
            .Include(ia => ia.Location)
            .OrderBy(ia => ia.InventoryItem.ItemCode)
            .ToListAsync();
    }

    public async Task<IEnumerable<InventoryAllocation>> GetExpiredAllocationsAsync()
    {
        var today = DateTime.UtcNow.Date;
        return await _dbSet
            .Where(ia => ia.Status == "Active" && 
                        !ia.IsDeleted &&
                        ia.ExpirationDate < today)
            .Include(ia => ia.InventoryItem)
            .Include(ia => ia.Location)
            .OrderBy(ia => ia.ExpirationDate)
            .ToListAsync();
    }
}

public class WarehouseRepository : GenericRepository<Warehouse>, IWarehouseRepository
{
    public WarehouseRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<Warehouse>> GetActiveWarehousesAsync()
    {
        return await _dbSet
            .Where(w => w.IsActive && !w.IsDeleted)
            .OrderBy(w => w.Name)
            .ToListAsync();
    }

    public async Task<Warehouse?> GetDefaultWarehouseAsync()
    {
        return await _dbSet
            .Where(w => w.IsDefault && w.IsActive && !w.IsDeleted)
            .FirstOrDefaultAsync();
    }

    public async Task<Warehouse?> GetByCodeAsync(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;
            
        return await _dbSet
            .Where(w => w.Code == code && !w.IsDeleted)
            .FirstOrDefaultAsync();
    }
}

public class WarehouseLocationRepository : GenericRepository<WarehouseLocation>, IWarehouseLocationRepository
{
    public WarehouseLocationRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<WarehouseLocation>> GetLocationsByWarehouseAsync(Guid warehouseId)
    {
        if (warehouseId == Guid.Empty)
            return new List<WarehouseLocation>();
            
        return await _dbSet
            .Where(wl => wl.WarehouseId == warehouseId && !wl.IsDeleted)
            .Include(wl => wl.Warehouse)
            .OrderBy(wl => wl.LocationCode)
            .ToListAsync();
    }

    public async Task<IEnumerable<WarehouseLocation>> GetPickingLocationsAsync(Guid warehouseId)
    {
        if (warehouseId == Guid.Empty)
            return new List<WarehouseLocation>();
            
        return await _dbSet
            .Where(wl => wl.WarehouseId == warehouseId && 
                        wl.IsPickingLocation && 
                        wl.IsActive && 
                        !wl.IsDeleted)
            .Include(wl => wl.Warehouse)
            .OrderBy(wl => wl.LocationCode)
            .ToListAsync();
    }

    public async Task<IEnumerable<WarehouseLocation>> GetReceivingLocationsAsync(Guid warehouseId)
    {
        if (warehouseId == Guid.Empty)
            return new List<WarehouseLocation>();
            
        return await _dbSet
            .Where(wl => wl.WarehouseId == warehouseId && 
                        wl.IsReceivingLocation && 
                        wl.IsActive && 
                        !wl.IsDeleted)
            .Include(wl => wl.Warehouse)
            .OrderBy(wl => wl.LocationCode)
            .ToListAsync();
    }

    public async Task<WarehouseLocation?> GetByLocationCodeAsync(string locationCode)
    {
        if (string.IsNullOrWhiteSpace(locationCode))
            return null;
            
        return await _dbSet
            .Where(wl => wl.LocationCode == locationCode && !wl.IsDeleted)
            .Include(wl => wl.Warehouse)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<WarehouseLocation>> GetAvailableLocationsAsync(Guid warehouseId)
    {
        if (warehouseId == Guid.Empty)
            return new List<WarehouseLocation>();
            
        return await _dbSet
            .Where(wl => wl.WarehouseId == warehouseId && 
                        wl.IsActive && 
                        !wl.IsDeleted &&
                        (wl.MaxItems == null || wl.CurrentItemCount < wl.MaxItems) &&
                        (wl.MaxWeight == null || wl.CurrentWeight < wl.MaxWeight) &&
                        (wl.MaxVolume == null || wl.CurrentVolume < wl.MaxVolume))
            .Include(wl => wl.Warehouse)
            .OrderBy(wl => wl.LocationCode)
            .ToListAsync();
    }
}

#endregion