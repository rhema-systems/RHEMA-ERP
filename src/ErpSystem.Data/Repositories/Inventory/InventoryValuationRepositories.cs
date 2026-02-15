using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Inventory;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.Inventory;

/// <summary>
/// Repository for inventory movements (valuation source of truth)
/// </summary>
public class InventoryMovementRepository : GenericRepository<InventoryMovement>, IInventoryMovementRepository
{
    public InventoryMovementRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<InventoryMovement>> GetMovementsByItemAsync(Guid inventoryItemId)
    {
        return await _context.InventoryMovements
            .Where(m => m.InventoryItemId == inventoryItemId)
            .Include(m => m.InventoryItem)
            .Include(m => m.Warehouse)
            .Include(m => m.Location)
            .Include(m => m.CreatedBy)
            .Include(m => m.PostedBy)
            .OrderByDescending(m => m.MovementDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<InventoryMovement>> GetMovementsByWarehouseAsync(Guid warehouseId)
    {
        return await _context.InventoryMovements
            .Where(m => m.WarehouseId == warehouseId)
            .Include(m => m.InventoryItem)
            .Include(m => m.Warehouse)
            .Include(m => m.Location)
            .OrderByDescending(m => m.MovementDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<InventoryMovement>> GetMovementsByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        return await _context.InventoryMovements
            .Where(m => m.MovementDate >= startDate && m.MovementDate <= endDate)
            .Include(m => m.InventoryItem)
            .Include(m => m.Warehouse)
            .Include(m => m.Location)
            .OrderBy(m => m.MovementDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<InventoryMovement>> GetMovementsByReferenceAsync(ReferenceType referenceType, Guid? referenceId)
    {
        var query = _context.InventoryMovements
            .Where(m => m.ReferenceType == referenceType);

        if (referenceId.HasValue)
            query = query.Where(m => m.ReferenceId == referenceId.Value);

        return await query
            .Include(m => m.InventoryItem)
            .Include(m => m.Warehouse)
            .Include(m => m.Location)
            .OrderBy(m => m.MovementDate)
            .ToListAsync();
    }

    public async Task<InventoryMovement?> GetByMovementNumberAsync(string movementNumber)
    {
        return await _context.InventoryMovements
            .Include(m => m.InventoryItem)
            .Include(m => m.Warehouse)
            .Include(m => m.Location)
            .Include(m => m.CostLayer)
            .Include(m => m.CreatedBy)
            .Include(m => m.PostedBy)
            .FirstOrDefaultAsync(m => m.MovementNumber == movementNumber);
    }

    public async Task<IEnumerable<InventoryMovement>> GetUnpostedMovementsAsync()
    {
        return await _context.InventoryMovements
            .Where(m => !m.IsPosted)
            .Include(m => m.InventoryItem)
            .Include(m => m.Warehouse)
            .OrderBy(m => m.MovementDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<InventoryMovement>> GetReversalsAsync(Guid originalMovementId)
    {
        return await _context.InventoryMovements
            .Where(m => m.ReversedMovementId == originalMovementId)
            .Include(m => m.InventoryItem)
            .Include(m => m.Warehouse)
            .OrderBy(m => m.MovementDate)
            .ToListAsync();
    }
}

/// <summary>
/// Repository for inventory layers (FIFO cost layers)
/// </summary>
public class InventoryLayerRepository : GenericRepository<InventoryLayer>, IInventoryLayerRepository
{
    public InventoryLayerRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<InventoryLayer>> GetLayersByItemAsync(Guid inventoryItemId)
    {
        return await _context.InventoryLayers
            .Where(l => l.InventoryItemId == inventoryItemId)
            .Include(l => l.InventoryItem)
            .Include(l => l.Warehouse)
            .Include(l => l.Location)
            .OrderBy(l => l.LayerDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<InventoryLayer>> GetActiveLayersAsync(Guid inventoryItemId, Guid warehouseId)
    {
        return await _context.InventoryLayers
            .Where(l => l.InventoryItemId == inventoryItemId &&
                       l.WarehouseId == warehouseId &&
                       l.IsActive &&
                       !l.IsFullyConsumed)
            .Include(l => l.InventoryItem)
            .Include(l => l.Warehouse)
            .OrderBy(l => l.LayerDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<InventoryLayer>> GetLayersForConsumptionAsync(
        Guid inventoryItemId,
        Guid warehouseId,
        Guid? locationId)
    {
        var query = _context.InventoryLayers
            .Where(l => l.InventoryItemId == inventoryItemId &&
                       l.WarehouseId == warehouseId &&
                       !l.IsFullyConsumed &&
                       l.RemainingQuantity > 0);

        if (locationId.HasValue)
            query = query.Where(l => l.LocationId == locationId.Value);

        return await query
            .OrderBy(l => l.LayerDate)
            .ThenBy(l => l.CreatedAt)
            .ToListAsync();
    }

    public async Task<InventoryLayer?> GetByLayerNumberAsync(string layerNumber)
    {
        return await _context.InventoryLayers
            .Include(l => l.InventoryItem)
            .Include(l => l.Warehouse)
            .Include(l => l.Location)
            .FirstOrDefaultAsync(l => l.LayerNumber == layerNumber);
    }

    public async Task<IEnumerable<InventoryLayer>> GetExpiringLayersAsync(DateTime beforeDate)
    {
        return await _context.InventoryLayers
            .Where(l => l.ExpirationDate.HasValue &&
                       l.ExpirationDate.Value <= beforeDate &&
                       !l.IsFullyConsumed &&
                       l.RemainingQuantity > 0)
            .Include(l => l.InventoryItem)
            .Include(l => l.Warehouse)
            .OrderBy(l => l.ExpirationDate)
            .ToListAsync();
    }
}

/// <summary>
/// Repository for inventory balances (performance cache)
/// </summary>
public class InventoryBalanceRepository : GenericRepository<InventoryBalance>, IInventoryBalanceRepository
{
    public InventoryBalanceRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<InventoryBalance>> GetBalancesByItemAsync(Guid inventoryItemId)
    {
        return await _context.InventoryBalances
            .Where(b => b.InventoryItemId == inventoryItemId)
            .Include(b => b.InventoryItem)
            .Include(b => b.Warehouse)
            .Include(b => b.Location)
            .ToListAsync();
    }

    public async Task<IEnumerable<InventoryBalance>> GetBalancesByWarehouseAsync(Guid warehouseId)
    {
        return await _context.InventoryBalances
            .Where(b => b.WarehouseId == warehouseId)
            .Include(b => b.InventoryItem)
            .Include(b => b.Warehouse)
            .OrderBy(b => b.InventoryItem.ItemCode)
            .ToListAsync();
    }

    public async Task<InventoryBalance?> GetBalanceAsync(Guid inventoryItemId, Guid warehouseId, Guid? locationId)
    {
        return await _context.InventoryBalances
            .Include(b => b.InventoryItem)
            .Include(b => b.Warehouse)
            .Include(b => b.Location)
            .FirstOrDefaultAsync(b => b.InventoryItemId == inventoryItemId &&
                                     b.WarehouseId == warehouseId &&
                                     b.LocationId == locationId);
    }

    public async Task<IEnumerable<InventoryBalance>> GetLowStockBalancesAsync(Guid? warehouseId = null)
    {
        var query = _context.InventoryBalances
            .Include(b => b.InventoryItem)
            .Include(b => b.Warehouse)
            .Where(b => b.QuantityOnHand <= b.InventoryItem.ReorderLevel &&
                       b.InventoryItem.Status == ItemStatus.Active);

        if (warehouseId.HasValue)
            query = query.Where(b => b.WarehouseId == warehouseId.Value);

        return await query
            .OrderBy(b => b.QuantityOnHand)
            .ToListAsync();
    }

    public async Task<IEnumerable<InventoryBalance>> GetBalancesRequiringRecalculationAsync(DateTime olderThan)
    {
        return await _context.InventoryBalances
            .Where(b => b.LastRecalculatedAt < olderThan)
            .Include(b => b.InventoryItem)
            .Include(b => b.Warehouse)
            .OrderBy(b => b.LastRecalculatedAt)
            .ToListAsync();
    }
}
