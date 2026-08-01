using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Data.Repositories;
using Microsoft.EntityFrameworkCore;

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
            .Include(i => i.UnitOfMeasureSchedule)
            .OrderBy(i => i.ItemCode)
            .ToListAsync();
    }

    public async Task<InventoryItem?> GetByIdWithDetailsAsync(Guid id)
    {
        if (id == Guid.Empty)
        {
            return null;
        }

        return await _dbSet
            .Where(i => i.Id == id && !i.IsDeleted)
            .Include(i => i.Category)
            .Include(i => i.UnitOfMeasureSchedule)
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
        {
            return null;
        }

        return await _dbSet
            .Where(i => i.ItemCode == itemCode && !i.IsDeleted)
            .Include(i => i.Category)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<InventoryItem>> GetItemsByCategoryAsync(Guid categoryId)
    {
        if (categoryId == Guid.Empty)
        {
            return new List<InventoryItem>();
        }

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
        {
            return new List<InventoryItem>();
        }

        var normalizedSearchTerm = searchTerm.Trim();
        return await _dbSet
            .Where(i => !i.IsDeleted &&
                       (i.ItemCode.Contains(normalizedSearchTerm) ||
                        i.Name.Contains(normalizedSearchTerm) ||
                        (i.Description != null && i.Description.Contains(normalizedSearchTerm)) ||
                        (i.Brand != null && i.Brand.Contains(normalizedSearchTerm)) ||
                        (i.Manufacturer != null && i.Manufacturer.Contains(normalizedSearchTerm)) ||
                        (i.Barcode != null && i.Barcode.Contains(normalizedSearchTerm)) ||
                        (i.AlternateBarcode != null && i.AlternateBarcode.Contains(normalizedSearchTerm)) ||
                        (i.QRCode != null && i.QRCode.Contains(normalizedSearchTerm)) ||
                        i.ItemUnitsOfMeasure.Any(unit => !unit.IsDeleted && unit.Barcode != null && unit.Barcode.Contains(normalizedSearchTerm))))
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
        {
            return new List<InventoryCategory>();
        }

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
        {
            return new List<StockMovement>();
        }

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
        {
            return new List<StockMovement>();
        }

        return await _dbSet
            .Where(sm => sm.InventoryItemId == inventoryItemId && !sm.IsDeleted)
            .Include(sm => sm.InventoryItem)
            .Include(sm => sm.Location)
                .ThenInclude(l => l!.Warehouse)
            .OrderByDescending(sm => sm.MovementDate)
            .Take(count)
            .ToListAsync();
    }

    public async Task<IEnumerable<StockMovement>> GetMovementsByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        if (startDate >= endDate)
        {
            return new List<StockMovement>();
        }

        return await _dbSet
            .Where(sm => !sm.IsDeleted &&
                        sm.MovementDate >= startDate &&
                        sm.MovementDate <= endDate)
            .Include(sm => sm.InventoryItem)
            .Include(sm => sm.Location)
                .ThenInclude(l => l!.Warehouse)
            .OrderByDescending(sm => sm.MovementDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StockMovement>> GetMovementsByReferenceAsync(ReferenceType referenceType, string referenceNumber)
    {
        if (string.IsNullOrWhiteSpace(referenceNumber))
        {
            return new List<StockMovement>();
        }

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
        {
            return new List<StockMovement>();
        }

        return await _dbSet
            .Where(sm => sm.LocationId == locationId && !sm.IsDeleted)
            .Include(sm => sm.InventoryItem)
            .Include(sm => sm.Location)
            .OrderByDescending(sm => sm.MovementDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<StockMovement>> GetFilteredMovementsAsync(
        Guid? warehouseId = null,
        string? movementType = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? referenceNumber = null,
        int limit = 100)
    {
        var query = _dbSet
            .Where(sm => !sm.IsDeleted)
            .Include(sm => sm.InventoryItem)
            .Include(sm => sm.Warehouse)
            .Include(sm => sm.Location)
                .ThenInclude(l => l!.Warehouse)
            .AsQueryable();

        // If searching by reference number, skip date filter to search all history
        if (string.IsNullOrEmpty(referenceNumber))
        {
            // Apply date filter - default to last 30 days if no dates provided
            var effectiveStartDate = startDate ?? DateTime.UtcNow.AddDays(-30);
            var effectiveEndDate = endDate ?? DateTime.UtcNow;
            query = query.Where(sm => sm.MovementDate >= effectiveStartDate && sm.MovementDate <= effectiveEndDate);
        }
        else
        {
            // Apply date filters only if explicitly provided when searching by reference
            if (startDate.HasValue)
            {
                query = query.Where(sm => sm.MovementDate >= startDate.Value);
            }
            if (endDate.HasValue)
            {
                query = query.Where(sm => sm.MovementDate <= endDate.Value);
            }
        }

        // Apply warehouse filter using the direct WarehouseId on the movement
        if (warehouseId.HasValue && warehouseId.Value != Guid.Empty)
        {
            query = query.Where(sm => sm.WarehouseId == warehouseId.Value);
        }

        // Apply movement type filter (case-insensitive)
        if (!string.IsNullOrEmpty(movementType))
        {
            var lowerType = movementType.ToLower();
            query = query.Where(sm => sm.MovementType.ToLower() == lowerType);
        }

        // Apply reference number filter (partial match, case-insensitive)
        if (!string.IsNullOrEmpty(referenceNumber))
        {
            var lowerRef = referenceNumber.ToLower();
            query = query.Where(sm => sm.ReferenceNumber != null && sm.ReferenceNumber.ToLower().Contains(lowerRef));
        }

        return await query
            .OrderByDescending(sm => sm.MovementDate)
            .Take(limit)
            .ToListAsync();
    }
}

public class InventoryLocationRepository : GenericRepository<InventoryLocation>, IInventoryLocationRepository
{
    public InventoryLocationRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<InventoryLocation>> GetByInventoryItemAsync(Guid inventoryItemId)
    {
        if (inventoryItemId == Guid.Empty)
        {
            return new List<InventoryLocation>();
        }

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
        {
            return new List<InventoryLocation>();
        }

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
        {
            return null;
        }

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
        {
            return new List<InventoryAllocation>();
        }

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
        {
            return new List<InventoryAllocation>();
        }

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
        {
            return null;
        }

        return await _dbSet
            .Where(w => w.Code == code && !w.IsDeleted)
            .FirstOrDefaultAsync();
    }
}

public class WarehouseQuantityRepository : GenericRepository<WarehouseQuantity>, IWarehouseQuantityRepository
{
    public WarehouseQuantityRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<WarehouseQuantity>> GetAllWithDetailsAsync()
    {
        return await _dbSet
            .Where(wq => !wq.IsDeleted)
            .Include(wq => wq.InventoryItem)
                .ThenInclude(i => i.Category)
            .Include(wq => wq.Warehouse)
            .OrderBy(wq => wq.Warehouse.Name)
            .ThenBy(wq => wq.InventoryItem.ItemCode)
            .ToListAsync();
    }

    public async Task<WarehouseQuantity?> GetByIdWithDetailsAsync(Guid id)
    {
        if (id == Guid.Empty)
        {
            return null;
        }

        return await _dbSet
            .Where(wq => wq.Id == id && !wq.IsDeleted)
            .Include(wq => wq.InventoryItem)
                .ThenInclude(i => i.Category)
            .Include(wq => wq.Warehouse)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<WarehouseQuantity>> GetByWarehouseAsync(Guid warehouseId)
    {
        if (warehouseId == Guid.Empty)
        {
            return new List<WarehouseQuantity>();
        }

        return await _dbSet
            .Where(wq => wq.WarehouseId == warehouseId && !wq.IsDeleted)
            .Include(wq => wq.InventoryItem)
                .ThenInclude(i => i.Category)
            .Include(wq => wq.Warehouse)
            .OrderBy(wq => wq.InventoryItem.ItemCode)
            .ToListAsync();
    }

    public async Task<IEnumerable<WarehouseQuantity>> GetByWarehouseAndItemTypeAsync(Guid warehouseId, int itemType)
    {
        if (warehouseId == Guid.Empty)
        {
            return new List<WarehouseQuantity>();
        }

        var itemTypeEnum = (ItemType)itemType;
        return await _dbSet
            .Where(wq => wq.WarehouseId == warehouseId &&
                        wq.InventoryItem.ItemType == itemTypeEnum &&
                        !wq.IsDeleted)
            .Include(wq => wq.InventoryItem)
                .ThenInclude(i => i.Category)
            .Include(wq => wq.Warehouse)
            .OrderBy(wq => wq.InventoryItem.ItemCode)
            .ToListAsync();
    }

    public async Task<WarehouseQuantity?> GetByWarehouseAndItemAsync(Guid warehouseId, Guid inventoryItemId)
    {
        if (warehouseId == Guid.Empty || inventoryItemId == Guid.Empty)
        {
            return null;
        }

        return await _dbSet
            .Where(wq => wq.WarehouseId == warehouseId &&
                        wq.InventoryItemId == inventoryItemId &&
                        !wq.IsDeleted)
            .Include(wq => wq.InventoryItem)
                .ThenInclude(i => i.Category)
            .Include(wq => wq.Warehouse)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<WarehouseQuantity>> GetByInventoryItemIdAsync(Guid inventoryItemId)
    {
        if (inventoryItemId == Guid.Empty)
        {
            return new List<WarehouseQuantity>();
        }

        return await _dbSet
            .Where(wq => wq.InventoryItemId == inventoryItemId && !wq.IsDeleted)
            .Include(wq => wq.InventoryItem)
                .ThenInclude(i => i.Category)
            .Include(wq => wq.Warehouse)
            .OrderBy(wq => wq.Warehouse.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<WarehouseQuantity>> GetItemsWithStockAsync(Guid warehouseId, int? itemType = null)
    {
        var query = _dbSet
            .Where(wq => wq.WarehouseId == warehouseId &&
                        wq.AvailableStock > 0 &&
                        !wq.IsDeleted);

        if (itemType.HasValue)
        {
            var itemTypeEnum = (ItemType)itemType.Value;
            query = query.Where(wq => wq.InventoryItem.ItemType == itemTypeEnum);
        }

        return await query
            .Include(wq => wq.InventoryItem)
                .ThenInclude(i => i.Category)
            .Include(wq => wq.Warehouse)
            .OrderBy(wq => wq.InventoryItem.ItemCode)
            .ToListAsync();
    }

    public async Task<IEnumerable<WarehouseQuantity>> GetItemsBelowReorderLevelAsync(Guid warehouseId)
    {
        if (warehouseId == Guid.Empty)
        {
            return new List<WarehouseQuantity>();
        }

        return await _dbSet
            .Where(wq => wq.WarehouseId == warehouseId &&
                        wq.CurrentStock <= wq.ReorderLevel &&
                        !wq.IsDeleted)
            .Include(wq => wq.InventoryItem)
                .ThenInclude(i => i.Category)
            .Include(wq => wq.Warehouse)
            .OrderBy(wq => wq.CurrentStock)
            .ThenBy(wq => wq.InventoryItem.ItemCode)
            .ToListAsync();
    }
}

public class WarehouseLocationRepository : GenericRepository<WarehouseLocation>, IWarehouseLocationRepository
{
    public WarehouseLocationRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<WarehouseLocation>> GetLocationsByWarehouseAsync(Guid warehouseId)
    {
        if (warehouseId == Guid.Empty)
        {
            return new List<WarehouseLocation>();
        }

        return await _dbSet
            .Where(wl => wl.WarehouseId == warehouseId && !wl.IsDeleted)
            .Include(wl => wl.Warehouse)
            .OrderBy(wl => wl.LocationCode)
            .ToListAsync();
    }

    public async Task<IEnumerable<WarehouseLocation>> GetPickingLocationsAsync(Guid warehouseId)
    {
        if (warehouseId == Guid.Empty)
        {
            return new List<WarehouseLocation>();
        }

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
        {
            return new List<WarehouseLocation>();
        }

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
        {
            return null;
        }

        return await _dbSet
            .Where(wl => wl.LocationCode == locationCode && !wl.IsDeleted)
            .Include(wl => wl.Warehouse)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<WarehouseLocation>> GetAvailableLocationsAsync(Guid warehouseId)
    {
        if (warehouseId == Guid.Empty)
        {
            return new List<WarehouseLocation>();
        }

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
