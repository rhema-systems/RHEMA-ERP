using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Inventory;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.Inventory;

#region Unit of Measure Repositories

public class UnitOfMeasureRepository : GenericRepository<UnitOfMeasure>, IUnitOfMeasureRepository
{
    public UnitOfMeasureRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<UnitOfMeasure>> GetActiveUnitsAsync()
    {
        return await _dbSet
            .Where(u => u.IsActive && !u.IsDeleted)
            .OrderBy(u => u.SortOrder)
            .ThenBy(u => u.Code)
            .ToListAsync();
    }

    public async Task<UnitOfMeasure?> GetByCodeAsync(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        return await _dbSet
            .FirstOrDefaultAsync(u => u.Code == code && !u.IsDeleted);
    }

    public async Task<IEnumerable<UnitOfMeasure>> GetByCategoryAsync(string category)
    {
        if (string.IsNullOrWhiteSpace(category)) return new List<UnitOfMeasure>();
        return await _dbSet
            .Where(u => u.Category == category && u.IsActive && !u.IsDeleted)
            .OrderBy(u => u.SortOrder)
            .ToListAsync();
    }

    public async Task<IEnumerable<UnitOfMeasure>> GetBaseUnitsAsync()
    {
        return await _dbSet
            .Where(u => u.IsBaseUnit && u.IsActive && !u.IsDeleted)
            .OrderBy(u => u.Category)
            .ThenBy(u => u.Code)
            .ToListAsync();
    }
}

public class UnitOfMeasureConversionRepository : GenericRepository<UnitOfMeasureConversion>, IUnitOfMeasureConversionRepository
{
    public UnitOfMeasureConversionRepository(ApplicationDbContext context) : base(context) { }

    public async Task<UnitOfMeasureConversion?> GetConversionAsync(Guid fromUnitId, Guid toUnitId)
    {
        if (fromUnitId == Guid.Empty || toUnitId == Guid.Empty) return null;
        return await _dbSet
            .Include(c => c.FromUnit)
            .Include(c => c.ToUnit)
            .FirstOrDefaultAsync(c => c.FromUnitId == fromUnitId && c.ToUnitId == toUnitId && c.IsActive && !c.IsDeleted);
    }

    public async Task<IEnumerable<UnitOfMeasureConversion>> GetConversionsFromUnitAsync(Guid fromUnitId)
    {
        if (fromUnitId == Guid.Empty) return new List<UnitOfMeasureConversion>();
        return await _dbSet
            .Where(c => c.FromUnitId == fromUnitId && c.IsActive && !c.IsDeleted)
            .Include(c => c.ToUnit)
            .ToListAsync();
    }

    public async Task<IEnumerable<UnitOfMeasureConversion>> GetConversionsToUnitAsync(Guid toUnitId)
    {
        if (toUnitId == Guid.Empty) return new List<UnitOfMeasureConversion>();
        return await _dbSet
            .Where(c => c.ToUnitId == toUnitId && c.IsActive && !c.IsDeleted)
            .Include(c => c.FromUnit)
            .ToListAsync();
    }
}

public class ItemUnitOfMeasureRepository : GenericRepository<ItemUnitOfMeasure>, IItemUnitOfMeasureRepository
{
    public ItemUnitOfMeasureRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<ItemUnitOfMeasure>> GetByItemAsync(Guid inventoryItemId)
    {
        if (inventoryItemId == Guid.Empty) return new List<ItemUnitOfMeasure>();
        return await _dbSet
            .Where(iu => iu.InventoryItemId == inventoryItemId && iu.IsActive && !iu.IsDeleted)
            .Include(iu => iu.UnitOfMeasure)
            .ToListAsync();
    }

    public async Task<ItemUnitOfMeasure?> GetBaseUnitForItemAsync(Guid inventoryItemId)
    {
        if (inventoryItemId == Guid.Empty) return null;
        return await _dbSet
            .Include(iu => iu.UnitOfMeasure)
            .FirstOrDefaultAsync(iu => iu.InventoryItemId == inventoryItemId && iu.IsBaseUnit && !iu.IsDeleted);
    }

    public async Task<ItemUnitOfMeasure?> GetPurchaseUnitForItemAsync(Guid inventoryItemId)
    {
        if (inventoryItemId == Guid.Empty) return null;
        return await _dbSet
            .Include(iu => iu.UnitOfMeasure)
            .FirstOrDefaultAsync(iu => iu.InventoryItemId == inventoryItemId && iu.IsPurchaseUnit && !iu.IsDeleted);
    }

    public async Task<ItemUnitOfMeasure?> GetSalesUnitForItemAsync(Guid inventoryItemId)
    {
        if (inventoryItemId == Guid.Empty) return null;
        return await _dbSet
            .Include(iu => iu.UnitOfMeasure)
            .FirstOrDefaultAsync(iu => iu.InventoryItemId == inventoryItemId && iu.IsSalesUnit && !iu.IsDeleted);
    }

    public async Task<ItemUnitOfMeasure?> GetByBarcodeAsync(string barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode)) return null;
        return await _dbSet
            .Include(iu => iu.InventoryItem)
            .Include(iu => iu.UnitOfMeasure)
            .FirstOrDefaultAsync(iu => iu.Barcode == barcode && !iu.IsDeleted);
    }
}

public class UnitOfMeasureScheduleRepository : GenericRepository<UnitOfMeasureSchedule>, IUnitOfMeasureScheduleRepository
{
    public UnitOfMeasureScheduleRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<UnitOfMeasureSchedule>> GetActiveSchedulesAsync()
    {
        return await _dbSet
            .Where(s => s.IsActive && !s.IsDeleted)
            .Include(s => s.BaseUnitOfMeasure)
            .Include(s => s.Details)
                .ThenInclude(d => d.UnitOfMeasure)
            .OrderBy(s => s.ScheduleId)
            .ToListAsync();
    }

    public async Task<UnitOfMeasureSchedule?> GetByScheduleIdAsync(string scheduleId)
    {
        if (string.IsNullOrWhiteSpace(scheduleId)) return null;
        return await _dbSet
            .Include(s => s.BaseUnitOfMeasure)
            .Include(s => s.Details)
                .ThenInclude(d => d.UnitOfMeasure)
            .FirstOrDefaultAsync(s => s.ScheduleId == scheduleId && !s.IsDeleted);
    }

    public async Task<UnitOfMeasureSchedule?> GetWithDetailsAsync(Guid id)
    {
        if (id == Guid.Empty) return null;
        return await _dbSet
            .Include(s => s.BaseUnitOfMeasure)
            .Include(s => s.Details)
                .ThenInclude(d => d.UnitOfMeasure)
            .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);
    }

    public async Task<IEnumerable<UnitOfMeasureSchedule>> GetAllWithDetailsAsync()
    {
        return await _dbSet
            .Where(s => !s.IsDeleted)
            .Include(s => s.BaseUnitOfMeasure)
            .Include(s => s.Details)
                .ThenInclude(d => d.UnitOfMeasure)
            .OrderBy(s => s.ScheduleId)
            .ToListAsync();
    }
}

public class UnitOfMeasureScheduleDetailRepository : GenericRepository<UnitOfMeasureScheduleDetail>, IUnitOfMeasureScheduleDetailRepository
{
    public UnitOfMeasureScheduleDetailRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<UnitOfMeasureScheduleDetail>> GetByScheduleAsync(Guid scheduleId)
    {
        if (scheduleId == Guid.Empty) return new List<UnitOfMeasureScheduleDetail>();
        return await _dbSet
            .Where(d => d.ScheduleId == scheduleId && !d.IsDeleted)
            .Include(d => d.UnitOfMeasure)
            .OrderBy(d => d.SortOrder)
            .ToListAsync();
    }

    public async Task DeleteByScheduleAsync(Guid scheduleId)
    {
        if (scheduleId == Guid.Empty) return;
        var details = await _dbSet.Where(d => d.ScheduleId == scheduleId).ToListAsync();
        _dbSet.RemoveRange(details);
    }
}

#endregion

#region Item Supplier Repository

public class ItemSupplierRepository : GenericRepository<ItemSupplier>, IItemSupplierRepository
{
    public ItemSupplierRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<ItemSupplier>> GetByItemAsync(Guid inventoryItemId)
    {
        if (inventoryItemId == Guid.Empty) return new List<ItemSupplier>();
        return await _dbSet
            .Where(s => s.InventoryItemId == inventoryItemId && s.IsActive && !s.IsDeleted)
            .OrderBy(s => s.Priority)
            .ToListAsync();
    }

    public async Task<IEnumerable<ItemSupplier>> GetBySupplierAsync(Guid supplierId)
    {
        if (supplierId == Guid.Empty) return new List<ItemSupplier>();
        return await _dbSet
            .Where(s => s.SupplierId == supplierId && s.IsActive && !s.IsDeleted)
            .Include(s => s.InventoryItem)
            .ToListAsync();
    }

    public async Task<ItemSupplier?> GetPreferredSupplierAsync(Guid inventoryItemId)
    {
        if (inventoryItemId == Guid.Empty) return null;
        return await _dbSet
            .FirstOrDefaultAsync(s => s.InventoryItemId == inventoryItemId && s.IsPreferred && s.IsActive && !s.IsDeleted);
    }

    public async Task<IEnumerable<ItemSupplier>> GetActiveSupplierItemsAsync(Guid supplierId)
    {
        if (supplierId == Guid.Empty) return new List<ItemSupplier>();
        return await _dbSet
            .Where(s => s.SupplierId == supplierId && s.IsActive && !s.IsDeleted)
            .Include(s => s.InventoryItem)
            .OrderBy(s => s.InventoryItem.ItemCode)
            .ToListAsync();
    }

    public async Task<ItemSupplier?> GetByItemAndSupplierAsync(Guid inventoryItemId, Guid supplierId)
    {
        if (inventoryItemId == Guid.Empty || supplierId == Guid.Empty) return null;
        return await _dbSet
            .FirstOrDefaultAsync(s => s.InventoryItemId == inventoryItemId && s.SupplierId == supplierId && !s.IsDeleted);
    }
}

#endregion

#region GRN Repositories

public class GoodsReceiptNoteRepository : GenericRepository<GoodsReceiptNote>, IGoodsReceiptNoteRepository
{
    public GoodsReceiptNoteRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<GoodsReceiptNote>> GetByStatusAsync(GRNStatus status)
    {
        return await _dbSet
            .Where(g => g.Status == status && !g.IsDeleted)
            .Include(g => g.Warehouse)
            .OrderByDescending(g => g.ReceiptDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<GoodsReceiptNote>> GetByWarehouseAsync(Guid warehouseId)
    {
        if (warehouseId == Guid.Empty) return new List<GoodsReceiptNote>();
        return await _dbSet
            .Where(g => g.WarehouseId == warehouseId && !g.IsDeleted)
            .OrderByDescending(g => g.ReceiptDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<GoodsReceiptNote>> GetBySupplierAsync(Guid supplierId)
    {
        if (supplierId == Guid.Empty) return new List<GoodsReceiptNote>();
        return await _dbSet
            .Where(g => g.SupplierId == supplierId && !g.IsDeleted)
            .OrderByDescending(g => g.ReceiptDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<GoodsReceiptNote>> GetByPurchaseOrderAsync(Guid purchaseOrderId)
    {
        if (purchaseOrderId == Guid.Empty) return new List<GoodsReceiptNote>();
        return await _dbSet
            .Where(g => g.PurchaseOrderId == purchaseOrderId && !g.IsDeleted)
            .Include(g => g.Items)
            .OrderByDescending(g => g.ReceiptDate)
            .ToListAsync();
    }

    public async Task<GoodsReceiptNote?> GetByGRNNumberAsync(string grnNumber)
    {
        if (string.IsNullOrWhiteSpace(grnNumber)) return null;
        return await _dbSet
            .Include(g => g.Warehouse)
            .FirstOrDefaultAsync(g => g.GRNNumber == grnNumber && !g.IsDeleted);
    }

    public async Task<GoodsReceiptNote?> GetWithItemsAsync(Guid grnId)
    {
        if (grnId == Guid.Empty) return null;
        return await _dbSet
            .Include(g => g.Warehouse)
            .Include(g => g.ReceivingLocation)
            .Include(g => g.ReceivedBy)
            .Include(g => g.InspectedBy)
            .Include(g => g.Items)
                .ThenInclude(i => i.InventoryItem)
            .Include(g => g.Items)
                .ThenInclude(i => i.StorageLocation)
            .FirstOrDefaultAsync(g => g.Id == grnId && !g.IsDeleted);
    }

    public async Task<IEnumerable<GoodsReceiptNote>> GetPendingInspectionAsync()
    {
        return await _dbSet
            .Where(g => (g.Status == GRNStatus.PendingInspection || g.Status == GRNStatus.InspectionInProgress) && !g.IsDeleted)
            .Include(g => g.Warehouse)
            .Include(g => g.Items)
            .OrderBy(g => g.ReceiptDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<GoodsReceiptNote>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        return await _dbSet
            .Where(g => g.ReceiptDate >= startDate && g.ReceiptDate <= endDate && !g.IsDeleted)
            .Include(g => g.Warehouse)
            .OrderByDescending(g => g.ReceiptDate)
            .ToListAsync();
    }
}

public class GoodsReceiptNoteItemRepository : GenericRepository<GoodsReceiptNoteItem>, IGoodsReceiptNoteItemRepository
{
    public GoodsReceiptNoteItemRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<GoodsReceiptNoteItem>> GetByGRNAsync(Guid grnId)
    {
        if (grnId == Guid.Empty) return new List<GoodsReceiptNoteItem>();
        return await _dbSet
            .Where(i => i.GoodsReceiptNoteId == grnId && !i.IsDeleted)
            .Include(i => i.InventoryItem)
            .Include(i => i.StorageLocation)
            .ToListAsync();
    }

    public async Task<IEnumerable<GoodsReceiptNoteItem>> GetByInventoryItemAsync(Guid inventoryItemId)
    {
        if (inventoryItemId == Guid.Empty) return new List<GoodsReceiptNoteItem>();
        return await _dbSet
            .Where(i => i.InventoryItemId == inventoryItemId && !i.IsDeleted)
            .Include(i => i.GoodsReceiptNote)
            .OrderByDescending(i => i.GoodsReceiptNote.ReceiptDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<GoodsReceiptNoteItem>> GetPendingInspectionItemsAsync(Guid grnId)
    {
        if (grnId == Guid.Empty) return new List<GoodsReceiptNoteItem>();
        return await _dbSet
            .Where(i => i.GoodsReceiptNoteId == grnId && i.InspectionResult == InspectionResult.Pending && !i.IsDeleted)
            .Include(i => i.InventoryItem)
            .ToListAsync();
    }
}

#endregion

#region Inventory Transfer Repositories

public class InventoryTransferRepository : GenericRepository<InventoryTransfer>, IInventoryTransferRepository
{
    public InventoryTransferRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<InventoryTransfer>> GetByStatusAsync(TransferStatus status)
    {
        return await _dbSet
            .Where(t => t.Status == status && !t.IsDeleted)
            .Include(t => t.SourceWarehouse)
            .Include(t => t.DestinationWarehouse)
            .OrderByDescending(t => t.RequestDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<InventoryTransfer>> GetBySourceWarehouseAsync(Guid warehouseId)
    {
        if (warehouseId == Guid.Empty) return new List<InventoryTransfer>();
        return await _dbSet
            .Where(t => t.SourceWarehouseId == warehouseId && !t.IsDeleted)
            .Include(t => t.DestinationWarehouse)
            .OrderByDescending(t => t.RequestDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<InventoryTransfer>> GetByDestinationWarehouseAsync(Guid warehouseId)
    {
        if (warehouseId == Guid.Empty) return new List<InventoryTransfer>();
        return await _dbSet
            .Where(t => t.DestinationWarehouseId == warehouseId && !t.IsDeleted)
            .Include(t => t.SourceWarehouse)
            .OrderByDescending(t => t.RequestDate)
            .ToListAsync();
    }

    public async Task<InventoryTransfer?> GetByTransferNumberAsync(string transferNumber)
    {
        if (string.IsNullOrWhiteSpace(transferNumber)) return null;
        return await _dbSet
            .Include(t => t.SourceWarehouse)
            .Include(t => t.DestinationWarehouse)
            .FirstOrDefaultAsync(t => t.TransferNumber == transferNumber && !t.IsDeleted);
    }

    public async Task<InventoryTransfer?> GetWithItemsAsync(Guid transferId)
    {
        if (transferId == Guid.Empty) return null;
        return await _dbSet
            .Include(t => t.SourceWarehouse)
            .Include(t => t.DestinationWarehouse)
            .Include(t => t.RequestedBy)
            .Include(t => t.ApprovedBy)
            .Include(t => t.Items)
                .ThenInclude(i => i.InventoryItem)
            .Include(t => t.Items)
                .ThenInclude(i => i.SourceLocation)
            .Include(t => t.Items)
                .ThenInclude(i => i.DestinationLocation)
            .FirstOrDefaultAsync(t => t.Id == transferId && !t.IsDeleted);
    }

    public async Task<IEnumerable<InventoryTransfer>> GetInTransitAsync()
    {
        return await _dbSet
            .Where(t => t.Status == TransferStatus.InTransit && !t.IsDeleted)
            .Include(t => t.SourceWarehouse)
            .Include(t => t.DestinationWarehouse)
            .Include(t => t.Items)
            .OrderBy(t => t.ShippedDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<InventoryTransfer>> GetPendingApprovalAsync()
    {
        return await _dbSet
            .Where(t => t.Status == TransferStatus.Submitted && !t.IsDeleted)
            .Include(t => t.SourceWarehouse)
            .Include(t => t.DestinationWarehouse)
            .Include(t => t.RequestedBy)
            .OrderBy(t => t.RequestDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<InventoryTransfer>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        return await _dbSet
            .Where(t => t.RequestDate >= startDate && t.RequestDate <= endDate && !t.IsDeleted)
            .Include(t => t.SourceWarehouse)
            .Include(t => t.DestinationWarehouse)
            .OrderByDescending(t => t.RequestDate)
            .ToListAsync();
    }
}

public class InventoryTransferItemRepository : GenericRepository<InventoryTransferItem>, IInventoryTransferItemRepository
{
    public InventoryTransferItemRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<InventoryTransferItem>> GetByTransferAsync(Guid transferId)
    {
        if (transferId == Guid.Empty) return new List<InventoryTransferItem>();
        return await _dbSet
            .Where(i => i.InventoryTransferId == transferId && !i.IsDeleted)
            .Include(i => i.InventoryItem)
            .ToListAsync();
    }

    public async Task<IEnumerable<InventoryTransferItem>> GetByInventoryItemAsync(Guid inventoryItemId)
    {
        if (inventoryItemId == Guid.Empty) return new List<InventoryTransferItem>();
        return await _dbSet
            .Where(i => i.InventoryItemId == inventoryItemId && !i.IsDeleted)
            .Include(i => i.InventoryTransfer)
            .OrderByDescending(i => i.InventoryTransfer.RequestDate)
            .ToListAsync();
    }
}

#endregion

#region Physical Count Repositories

public class PhysicalCountRepository : GenericRepository<PhysicalCount>, IPhysicalCountRepository
{
    public PhysicalCountRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<PhysicalCount>> GetByStatusAsync(string status)
    {
        if (string.IsNullOrWhiteSpace(status)) return new List<PhysicalCount>();
        return await _dbSet
            .Where(c => c.Status == status && !c.IsDeleted)
            .Include(c => c.Warehouse)
            .OrderByDescending(c => c.CountDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<PhysicalCount>> GetByWarehouseAsync(Guid warehouseId)
    {
        if (warehouseId == Guid.Empty) return new List<PhysicalCount>();
        return await _dbSet
            .Where(c => c.WarehouseId == warehouseId && !c.IsDeleted)
            .OrderByDescending(c => c.CountDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<PhysicalCount>> GetByCountTypeAsync(CountType countType)
    {
        return await _dbSet
            .Where(c => c.CountType == countType && !c.IsDeleted)
            .Include(c => c.Warehouse)
            .OrderByDescending(c => c.CountDate)
            .ToListAsync();
    }

    public async Task<PhysicalCount?> GetByCountNumberAsync(string countNumber)
    {
        if (string.IsNullOrWhiteSpace(countNumber)) return null;
        return await _dbSet
            .Include(c => c.Warehouse)
            .FirstOrDefaultAsync(c => c.CountNumber == countNumber && !c.IsDeleted);
    }

    public async Task<PhysicalCount?> GetWithItemsAsync(Guid countId)
    {
        if (countId == Guid.Empty) return null;
        return await _dbSet
            .Include(c => c.Warehouse)
            .Include(c => c.Location)
            .Include(c => c.InitiatedBy)
            .Include(c => c.Items)
                .ThenInclude(i => i.InventoryItem)
            .FirstOrDefaultAsync(c => c.Id == countId && !c.IsDeleted);
    }

    public async Task<IEnumerable<PhysicalCount>> GetInProgressAsync()
    {
        return await _dbSet
            .Where(c => c.Status == "InProgress" && !c.IsDeleted)
            .Include(c => c.Warehouse)
            .Include(c => c.Items)
            .OrderBy(c => c.StartedDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<PhysicalCount>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        return await _dbSet
            .Where(c => c.CountDate >= startDate && c.CountDate <= endDate && !c.IsDeleted)
            .Include(c => c.Warehouse)
            .OrderByDescending(c => c.CountDate)
            .ToListAsync();
    }
}

public class PhysicalCountItemRepository : GenericRepository<PhysicalCountItem>, IPhysicalCountItemRepository
{
    public PhysicalCountItemRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<PhysicalCountItem>> GetByCountAsync(Guid countId)
    {
        if (countId == Guid.Empty) return new List<PhysicalCountItem>();
        return await _dbSet
            .Where(i => i.PhysicalCountId == countId && !i.IsDeleted)
            .Include(i => i.InventoryItem)
            .Include(i => i.Location)
            .ToListAsync();
    }

    public async Task<IEnumerable<PhysicalCountItem>> GetByInventoryItemAsync(Guid inventoryItemId)
    {
        if (inventoryItemId == Guid.Empty) return new List<PhysicalCountItem>();
        return await _dbSet
            .Where(i => i.InventoryItemId == inventoryItemId && !i.IsDeleted)
            .Include(i => i.PhysicalCount)
            .OrderByDescending(i => i.PhysicalCount.CountDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<PhysicalCountItem>> GetUncountedItemsAsync(Guid countId)
    {
        if (countId == Guid.Empty) return new List<PhysicalCountItem>();
        return await _dbSet
            .Where(i => i.PhysicalCountId == countId && !i.IsCounted && !i.IsDeleted)
            .Include(i => i.InventoryItem)
            .ToListAsync();
    }

    public async Task<IEnumerable<PhysicalCountItem>> GetItemsWithVarianceAsync(Guid countId)
    {
        if (countId == Guid.Empty) return new List<PhysicalCountItem>();
        return await _dbSet
            .Where(i => i.PhysicalCountId == countId && i.VarianceQuantity != 0 && !i.IsDeleted)
            .Include(i => i.InventoryItem)
            .OrderByDescending(i => Math.Abs(i.VarianceValue))
            .ToListAsync();
    }
}

#endregion

#region Inventory Valuation Repositories

public class InventoryCostLayerRepository : GenericRepository<InventoryCostLayer>, IInventoryCostLayerRepository
{
    public InventoryCostLayerRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<InventoryCostLayer>> GetByItemAsync(Guid inventoryItemId)
    {
        if (inventoryItemId == Guid.Empty) return new List<InventoryCostLayer>();
        return await _dbSet
            .Where(l => l.InventoryItemId == inventoryItemId && !l.IsDeleted)
            .OrderBy(l => l.LayerDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<InventoryCostLayer>> GetByItemAndWarehouseAsync(Guid inventoryItemId, Guid warehouseId)
    {
        if (inventoryItemId == Guid.Empty || warehouseId == Guid.Empty) return new List<InventoryCostLayer>();
        return await _dbSet
            .Where(l => l.InventoryItemId == inventoryItemId && l.WarehouseId == warehouseId && !l.IsDeleted)
            .OrderBy(l => l.LayerDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<InventoryCostLayer>> GetActiveLayers(Guid inventoryItemId)
    {
        if (inventoryItemId == Guid.Empty) return new List<InventoryCostLayer>();
        return await _dbSet
            .Where(l => l.InventoryItemId == inventoryItemId && l.RemainingQuantity > 0 && !l.IsDeleted)
            .OrderBy(l => l.LayerDate)
            .ToListAsync();
    }

    public async Task<InventoryCostLayer?> GetOldestActiveLayer(Guid inventoryItemId, Guid warehouseId)
    {
        if (inventoryItemId == Guid.Empty || warehouseId == Guid.Empty) return null;
        return await _dbSet
            .Where(l => l.InventoryItemId == inventoryItemId && l.WarehouseId == warehouseId && l.RemainingQuantity > 0 && !l.IsDeleted)
            .OrderBy(l => l.LayerDate)
            .FirstOrDefaultAsync();
    }

    public async Task<InventoryCostLayer?> GetNewestActiveLayer(Guid inventoryItemId, Guid warehouseId)
    {
        if (inventoryItemId == Guid.Empty || warehouseId == Guid.Empty) return null;
        return await _dbSet
            .Where(l => l.InventoryItemId == inventoryItemId && l.WarehouseId == warehouseId && l.RemainingQuantity > 0 && !l.IsDeleted)
            .OrderByDescending(l => l.LayerDate)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<InventoryCostLayer>> GetLayersByLotAsync(Guid inventoryItemId, string lotNumber)
    {
        if (inventoryItemId == Guid.Empty || string.IsNullOrWhiteSpace(lotNumber)) return new List<InventoryCostLayer>();
        return await _dbSet
            .Where(l => l.InventoryItemId == inventoryItemId && l.LotNumber == lotNumber && !l.IsDeleted)
            .OrderBy(l => l.LayerDate)
            .ToListAsync();
    }

    public async Task<decimal> GetTotalInventoryValueAsync(Guid inventoryItemId)
    {
        if (inventoryItemId == Guid.Empty) return 0;
        return await _dbSet
            .Where(l => l.InventoryItemId == inventoryItemId && l.RemainingQuantity > 0 && !l.IsDeleted)
            .SumAsync(l => l.RemainingQuantity * l.UnitCost);
    }

    public async Task<decimal> GetWarehouseInventoryValueAsync(Guid warehouseId)
    {
        if (warehouseId == Guid.Empty) return 0;
        return await _dbSet
            .Where(l => l.WarehouseId == warehouseId && l.RemainingQuantity > 0 && !l.IsDeleted)
            .SumAsync(l => l.RemainingQuantity * l.UnitCost);
    }
}

public class LandedCostRepository : GenericRepository<LandedCost>, ILandedCostRepository
{
    public LandedCostRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<LandedCost>> GetByStatusAsync(string status)
    {
        if (string.IsNullOrWhiteSpace(status)) return new List<LandedCost>();
        return await _dbSet
            .Where(l => l.Status == status && !l.IsDeleted)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<LandedCost>> GetByGRNAsync(Guid grnId)
    {
        if (grnId == Guid.Empty) return new List<LandedCost>();
        return await _dbSet
            .Where(l => l.GoodsReceiptNoteId == grnId && !l.IsDeleted)
            .Include(l => l.Items)
            .ToListAsync();
    }

    public async Task<LandedCost?> GetByLandedCostNumberAsync(string landedCostNumber)
    {
        if (string.IsNullOrWhiteSpace(landedCostNumber)) return null;
        return await _dbSet
            .FirstOrDefaultAsync(l => l.LandedCostNumber == landedCostNumber && !l.IsDeleted);
    }

    public async Task<LandedCost?> GetWithDetailsAsync(Guid landedCostId)
    {
        if (landedCostId == Guid.Empty) return null;
        return await _dbSet
            .Include(l => l.GoodsReceiptNote)
            .Include(l => l.Items)
            .Include(l => l.Allocations)
                .ThenInclude(a => a.GoodsReceiptNoteItem)
            .FirstOrDefaultAsync(l => l.Id == landedCostId && !l.IsDeleted);
    }

    public async Task<IEnumerable<LandedCost>> GetPendingApprovalAsync()
    {
        return await _dbSet
            .Where(l => l.Status == "Pending" && !l.IsDeleted)
            .Include(l => l.GoodsReceiptNote)
            .OrderBy(l => l.CreatedAt)
            .ToListAsync();
    }
}

public class LandedCostItemRepository : GenericRepository<LandedCostItem>, ILandedCostItemRepository
{
    public LandedCostItemRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<LandedCostItem>> GetByLandedCostAsync(Guid landedCostId)
    {
        if (landedCostId == Guid.Empty) return new List<LandedCostItem>();
        return await _dbSet
            .Where(i => i.LandedCostId == landedCostId && !i.IsDeleted)
            .ToListAsync();
    }

    public async Task<IEnumerable<LandedCostItem>> GetByCostTypeAsync(LandedCostType costType)
    {
        return await _dbSet
            .Where(i => i.CostType == costType && !i.IsDeleted)
            .Include(i => i.LandedCost)
            .ToListAsync();
    }
}

public class LandedCostAllocationRepository : GenericRepository<LandedCostAllocation>, ILandedCostAllocationRepository
{
    public LandedCostAllocationRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<LandedCostAllocation>> GetByLandedCostAsync(Guid landedCostId)
    {
        if (landedCostId == Guid.Empty) return new List<LandedCostAllocation>();
        return await _dbSet
            .Where(a => a.LandedCostId == landedCostId && !a.IsDeleted)
            .Include(a => a.GoodsReceiptNoteItem)
                .ThenInclude(i => i.InventoryItem)
            .ToListAsync();
    }

    public async Task<IEnumerable<LandedCostAllocation>> GetByGRNItemAsync(Guid grnItemId)
    {
        if (grnItemId == Guid.Empty) return new List<LandedCostAllocation>();
        return await _dbSet
            .Where(a => a.GoodsReceiptNoteItemId == grnItemId && !a.IsDeleted)
            .Include(a => a.LandedCost)
            .ToListAsync();
    }

    public async Task<IEnumerable<LandedCostAllocation>> GetByInventoryItemAsync(Guid inventoryItemId)
    {
        if (inventoryItemId == Guid.Empty) return new List<LandedCostAllocation>();
        return await _dbSet
            .Where(a => a.InventoryItemId == inventoryItemId && !a.IsDeleted)
            .Include(a => a.LandedCost)
            .ToListAsync();
    }

    public async Task<decimal> GetTotalAllocatedCostAsync(Guid grnItemId)
    {
        if (grnItemId == Guid.Empty) return 0;
        return await _dbSet
            .Where(a => a.GoodsReceiptNoteItemId == grnItemId && !a.IsDeleted)
            .SumAsync(a => a.AllocatedAmount);
    }
}

#endregion

#region Purchase Return Repositories

public class PurchaseReturnRepository : GenericRepository<PurchaseReturn>, IPurchaseReturnRepository
{
    public PurchaseReturnRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<PurchaseReturn>> GetByStatusAsync(string status)
    {
        if (string.IsNullOrWhiteSpace(status)) return new List<PurchaseReturn>();
        return await _dbSet
            .Where(r => r.Status == status && !r.IsDeleted)
            .Include(r => r.Warehouse)
            .OrderByDescending(r => r.ReturnDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<PurchaseReturn>> GetBySupplierAsync(Guid supplierId)
    {
        if (supplierId == Guid.Empty) return new List<PurchaseReturn>();
        return await _dbSet
            .Where(r => r.SupplierId == supplierId && !r.IsDeleted)
            .OrderByDescending(r => r.ReturnDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<PurchaseReturn>> GetByWarehouseAsync(Guid warehouseId)
    {
        if (warehouseId == Guid.Empty) return new List<PurchaseReturn>();
        return await _dbSet
            .Where(r => r.WarehouseId == warehouseId && !r.IsDeleted)
            .OrderByDescending(r => r.ReturnDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<PurchaseReturn>> GetByGRNAsync(Guid grnId)
    {
        if (grnId == Guid.Empty) return new List<PurchaseReturn>();
        return await _dbSet
            .Where(r => r.GoodsReceiptNoteId == grnId && !r.IsDeleted)
            .Include(r => r.Items)
            .ToListAsync();
    }

    public async Task<PurchaseReturn?> GetByReturnNumberAsync(string returnNumber)
    {
        if (string.IsNullOrWhiteSpace(returnNumber)) return null;
        return await _dbSet
            .Include(r => r.Warehouse)
            .FirstOrDefaultAsync(r => r.ReturnNumber == returnNumber && !r.IsDeleted);
    }

    public async Task<PurchaseReturn?> GetWithItemsAsync(Guid returnId)
    {
        if (returnId == Guid.Empty) return null;
        return await _dbSet
            .Include(r => r.Warehouse)
            .Include(r => r.GoodsReceiptNote)
            .Include(r => r.RequestedBy)
            .Include(r => r.ApprovedBy)
            .Include(r => r.Items)
                .ThenInclude(i => i.InventoryItem)
            .FirstOrDefaultAsync(r => r.Id == returnId && !r.IsDeleted);
    }

    public async Task<IEnumerable<PurchaseReturn>> GetPendingApprovalAsync()
    {
        return await _dbSet
            .Where(r => r.Status == "Submitted" && !r.IsDeleted)
            .Include(r => r.Warehouse)
            .Include(r => r.RequestedBy)
            .OrderBy(r => r.ReturnDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<PurchaseReturn>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        return await _dbSet
            .Where(r => r.ReturnDate >= startDate && r.ReturnDate <= endDate && !r.IsDeleted)
            .Include(r => r.Warehouse)
            .OrderByDescending(r => r.ReturnDate)
            .ToListAsync();
    }
}

public class PurchaseReturnItemRepository : GenericRepository<PurchaseReturnItem>, IPurchaseReturnItemRepository
{
    public PurchaseReturnItemRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<PurchaseReturnItem>> GetByReturnAsync(Guid returnId)
    {
        if (returnId == Guid.Empty) return new List<PurchaseReturnItem>();
        return await _dbSet
            .Where(i => i.PurchaseReturnId == returnId && !i.IsDeleted)
            .Include(i => i.InventoryItem)
            .ToListAsync();
    }

    public async Task<IEnumerable<PurchaseReturnItem>> GetByInventoryItemAsync(Guid inventoryItemId)
    {
        if (inventoryItemId == Guid.Empty) return new List<PurchaseReturnItem>();
        return await _dbSet
            .Where(i => i.InventoryItemId == inventoryItemId && !i.IsDeleted)
            .Include(i => i.PurchaseReturn)
            .OrderByDescending(i => i.PurchaseReturn.ReturnDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<PurchaseReturnItem>> GetByGRNItemAsync(Guid grnItemId)
    {
        if (grnItemId == Guid.Empty) return new List<PurchaseReturnItem>();
        return await _dbSet
            .Where(i => i.GoodsReceiptNoteItemId == grnItemId && !i.IsDeleted)
            .Include(i => i.PurchaseReturn)
            .ToListAsync();
    }
}

#endregion

#region Inventory Requisition Repositories

public class InventoryRequisitionRepository : GenericRepository<InventoryRequisition>, IInventoryRequisitionRepository
{
    public InventoryRequisitionRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<InventoryRequisition>> GetByStatusAsync(RequisitionStatus status)
    {
        return await _dbSet
            .Where(r => r.Status == status && !r.IsDeleted)
            .Include(r => r.Warehouse)
            .OrderByDescending(r => r.RequestDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<InventoryRequisition>> GetByWarehouseAsync(Guid warehouseId)
    {
        if (warehouseId == Guid.Empty) return new List<InventoryRequisition>();
        return await _dbSet
            .Where(r => r.WarehouseId == warehouseId && !r.IsDeleted)
            .Include(r => r.Warehouse)
            .OrderByDescending(r => r.RequestDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<InventoryRequisition>> GetByDepartmentAsync(Guid departmentId)
    {
        if (departmentId == Guid.Empty) return new List<InventoryRequisition>();
        return await _dbSet
            .Where(r => r.DepartmentId == departmentId && !r.IsDeleted)
            .Include(r => r.Warehouse)
            .OrderByDescending(r => r.RequestDate)
            .ToListAsync();
    }

    public async Task<InventoryRequisition?> GetByRequisitionNumberAsync(string requisitionNumber)
    {
        if (string.IsNullOrWhiteSpace(requisitionNumber)) return null;
        return await _dbSet
            .Include(r => r.Warehouse)
            .FirstOrDefaultAsync(r => r.RequisitionNumber == requisitionNumber && !r.IsDeleted);
    }

    public async Task<InventoryRequisition?> GetWithItemsAsync(Guid id)
    {
        if (id == Guid.Empty) return null;
        return await _dbSet
            .Include(r => r.Warehouse)
            .Include(r => r.Location)
            .Include(r => r.RequestedBy)
            .Include(r => r.ApprovedBy)
            .Include(r => r.IssuedBy)
            .Include(r => r.Items)
                .ThenInclude(i => i.InventoryItem)
            .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);
    }

    public async Task<IEnumerable<InventoryRequisition>> GetByDateRangeAsync(DateTime fromDate, DateTime toDate)
    {
        return await _dbSet
            .Where(r => r.RequestDate >= fromDate && r.RequestDate <= toDate && !r.IsDeleted)
            .Include(r => r.Warehouse)
            .OrderByDescending(r => r.RequestDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<InventoryRequisition>> GetPendingApprovalAsync()
    {
        return await _dbSet
            .Where(r => r.Status == RequisitionStatus.Submitted && !r.IsDeleted)
            .Include(r => r.Warehouse)
            .Include(r => r.RequestedBy)
            .OrderBy(r => r.RequestDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<InventoryRequisition>> GetPendingIssueAsync()
    {
        return await _dbSet
            .Where(r => (r.Status == RequisitionStatus.Approved || r.Status == RequisitionStatus.InProgress || r.Status == RequisitionStatus.PartiallyIssued) && !r.IsDeleted)
            .Include(r => r.Warehouse)
            .Include(r => r.RequestedBy)
            .OrderBy(r => r.RequiredDate ?? r.RequestDate)
            .ToListAsync();
    }

    public async Task<string> GenerateRequisitionNumberAsync()
    {
        var today = DateTime.UtcNow;
        var prefix = $"REQ-{today:yyyyMMdd}-";
        var lastRequisition = await _dbSet
            .Where(r => r.RequisitionNumber.StartsWith(prefix))
            .OrderByDescending(r => r.RequisitionNumber)
            .FirstOrDefaultAsync();

        int nextNumber = 1;
        if (lastRequisition != null)
        {
            var lastNumberStr = lastRequisition.RequisitionNumber.Replace(prefix, "");
            if (int.TryParse(lastNumberStr, out int lastNumber))
            {
                nextNumber = lastNumber + 1;
            }
        }

        return $"{prefix}{nextNumber:D4}";
    }
}

public class InventoryRequisitionItemRepository : GenericRepository<InventoryRequisitionItem>, IInventoryRequisitionItemRepository
{
    public InventoryRequisitionItemRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<InventoryRequisitionItem>> GetByRequisitionAsync(Guid requisitionId)
    {
        if (requisitionId == Guid.Empty) return new List<InventoryRequisitionItem>();
        return await _dbSet
            .Where(i => i.InventoryRequisitionId == requisitionId && !i.IsDeleted)
            .Include(i => i.InventoryItem)
            .ToListAsync();
    }

    public async Task<IEnumerable<InventoryRequisitionItem>> GetByInventoryItemAsync(Guid inventoryItemId)
    {
        if (inventoryItemId == Guid.Empty) return new List<InventoryRequisitionItem>();
        return await _dbSet
            .Where(i => i.InventoryItemId == inventoryItemId && !i.IsDeleted)
            .Include(i => i.InventoryRequisition)
            .OrderByDescending(i => i.InventoryRequisition.RequestDate)
            .ToListAsync();
    }
}

#endregion
