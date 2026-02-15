using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.Inventory;

#region Unit of Measure Repositories

/// <summary>
/// Repository interface for units of measure
/// </summary>
public interface IUnitOfMeasureRepository : IGenericRepository<UnitOfMeasure>
{
    Task<IEnumerable<UnitOfMeasure>> GetActiveUnitsAsync();
    Task<UnitOfMeasure?> GetByCodeAsync(string code);
    Task<IEnumerable<UnitOfMeasure>> GetByCategoryAsync(string category);
    Task<IEnumerable<UnitOfMeasure>> GetBaseUnitsAsync();
}

/// <summary>
/// Repository interface for unit of measure conversions
/// </summary>
public interface IUnitOfMeasureConversionRepository : IGenericRepository<UnitOfMeasureConversion>
{
    Task<UnitOfMeasureConversion?> GetConversionAsync(Guid fromUnitId, Guid toUnitId);
    Task<IEnumerable<UnitOfMeasureConversion>> GetConversionsFromUnitAsync(Guid fromUnitId);
    Task<IEnumerable<UnitOfMeasureConversion>> GetConversionsToUnitAsync(Guid toUnitId);
}

/// <summary>
/// Repository interface for item-specific unit of measure
/// </summary>
public interface IItemUnitOfMeasureRepository : IGenericRepository<ItemUnitOfMeasure>
{
    Task<IEnumerable<ItemUnitOfMeasure>> GetByItemAsync(Guid inventoryItemId);
    Task<ItemUnitOfMeasure?> GetBaseUnitForItemAsync(Guid inventoryItemId);
    Task<ItemUnitOfMeasure?> GetPurchaseUnitForItemAsync(Guid inventoryItemId);
    Task<ItemUnitOfMeasure?> GetSalesUnitForItemAsync(Guid inventoryItemId);
    Task<ItemUnitOfMeasure?> GetByBarcodeAsync(string barcode);
}

/// <summary>
/// Repository interface for unit of measure schedules
/// </summary>
public interface IUnitOfMeasureScheduleRepository : IGenericRepository<UnitOfMeasureSchedule>
{
    Task<IEnumerable<UnitOfMeasureSchedule>> GetActiveSchedulesAsync();
    Task<UnitOfMeasureSchedule?> GetByScheduleIdAsync(string scheduleId);
    Task<UnitOfMeasureSchedule?> GetWithDetailsAsync(Guid id);
    Task<IEnumerable<UnitOfMeasureSchedule>> GetAllWithDetailsAsync();
}

/// <summary>
/// Repository interface for unit of measure schedule details
/// </summary>
public interface IUnitOfMeasureScheduleDetailRepository : IGenericRepository<UnitOfMeasureScheduleDetail>
{
    Task<IEnumerable<UnitOfMeasureScheduleDetail>> GetByScheduleAsync(Guid scheduleId);
    Task DeleteByScheduleAsync(Guid scheduleId);
}

#endregion

#region Item Supplier Repository

/// <summary>
/// Repository interface for item supplier relationships
/// </summary>
public interface IItemSupplierRepository : IGenericRepository<ItemSupplier>
{
    Task<IEnumerable<ItemSupplier>> GetByItemAsync(Guid inventoryItemId);
    Task<IEnumerable<ItemSupplier>> GetBySupplierAsync(Guid supplierId);
    Task<ItemSupplier?> GetPreferredSupplierAsync(Guid inventoryItemId);
    Task<IEnumerable<ItemSupplier>> GetActiveSupplierItemsAsync(Guid supplierId);
    Task<ItemSupplier?> GetByItemAndSupplierAsync(Guid inventoryItemId, Guid supplierId);
}

#endregion

#region GRN Repositories

/// <summary>
/// Repository interface for Goods Receipt Notes
/// </summary>
public interface IGoodsReceiptNoteRepository : IGenericRepository<GoodsReceiptNote>
{
    Task<IEnumerable<GoodsReceiptNote>> GetByStatusAsync(GRNStatus status);
    Task<IEnumerable<GoodsReceiptNote>> GetByWarehouseAsync(Guid warehouseId);
    Task<IEnumerable<GoodsReceiptNote>> GetBySupplierAsync(Guid supplierId);
    Task<IEnumerable<GoodsReceiptNote>> GetByPurchaseOrderAsync(Guid purchaseOrderId);
    Task<GoodsReceiptNote?> GetByGRNNumberAsync(string grnNumber);
    Task<GoodsReceiptNote?> GetWithItemsAsync(Guid grnId);
    Task<IEnumerable<GoodsReceiptNote>> GetPendingInspectionAsync();
    Task<IEnumerable<GoodsReceiptNote>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);
}

/// <summary>
/// Repository interface for GRN Items
/// </summary>
public interface IGoodsReceiptNoteItemRepository : IGenericRepository<GoodsReceiptNoteItem>
{
    Task<IEnumerable<GoodsReceiptNoteItem>> GetByGRNAsync(Guid grnId);
    Task<IEnumerable<GoodsReceiptNoteItem>> GetByInventoryItemAsync(Guid inventoryItemId);
    Task<IEnumerable<GoodsReceiptNoteItem>> GetPendingInspectionItemsAsync(Guid grnId);
}

#endregion

#region Inventory Transfer Repositories

/// <summary>
/// Repository interface for Inventory Transfers
/// </summary>
public interface IInventoryTransferRepository : IGenericRepository<InventoryTransfer>
{
    Task<IEnumerable<InventoryTransfer>> GetByStatusAsync(TransferStatus status);
    Task<IEnumerable<InventoryTransfer>> GetBySourceWarehouseAsync(Guid warehouseId);
    Task<IEnumerable<InventoryTransfer>> GetByDestinationWarehouseAsync(Guid warehouseId);
    Task<InventoryTransfer?> GetByTransferNumberAsync(string transferNumber);
    Task<InventoryTransfer?> GetWithItemsAsync(Guid transferId);
    Task<IEnumerable<InventoryTransfer>> GetInTransitAsync();
    Task<IEnumerable<InventoryTransfer>> GetPendingApprovalAsync();
    Task<IEnumerable<InventoryTransfer>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);
}

/// <summary>
/// Repository interface for Transfer Items
/// </summary>
public interface IInventoryTransferItemRepository : IGenericRepository<InventoryTransferItem>
{
    Task<IEnumerable<InventoryTransferItem>> GetByTransferAsync(Guid transferId);
    Task<IEnumerable<InventoryTransferItem>> GetByInventoryItemAsync(Guid inventoryItemId);
}

#endregion

#region Physical Count Repositories

/// <summary>
/// Repository interface for Physical Counts
/// </summary>
public interface IPhysicalCountRepository : IGenericRepository<PhysicalCount>
{
    Task<IEnumerable<PhysicalCount>> GetByStatusAsync(string status);
    Task<IEnumerable<PhysicalCount>> GetByWarehouseAsync(Guid warehouseId);
    Task<IEnumerable<PhysicalCount>> GetByCountTypeAsync(CountType countType);
    Task<PhysicalCount?> GetByCountNumberAsync(string countNumber);
    Task<PhysicalCount?> GetWithItemsAsync(Guid countId);
    Task<IEnumerable<PhysicalCount>> GetInProgressAsync();
    Task<IEnumerable<PhysicalCount>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);
}

/// <summary>
/// Repository interface for Physical Count Items
/// </summary>
public interface IPhysicalCountItemRepository : IGenericRepository<PhysicalCountItem>
{
    Task<IEnumerable<PhysicalCountItem>> GetByCountAsync(Guid countId);
    Task<IEnumerable<PhysicalCountItem>> GetByInventoryItemAsync(Guid inventoryItemId);
    Task<IEnumerable<PhysicalCountItem>> GetUncountedItemsAsync(Guid countId);
    Task<IEnumerable<PhysicalCountItem>> GetItemsWithVarianceAsync(Guid countId);
}

#endregion

#region Inventory Valuation Repositories

/// <summary>
/// Repository interface for Inventory Cost Layers (FIFO/LIFO)
/// </summary>
public interface IInventoryCostLayerRepository : IGenericRepository<InventoryCostLayer>
{
    Task<IEnumerable<InventoryCostLayer>> GetByItemAsync(Guid inventoryItemId);
    Task<IEnumerable<InventoryCostLayer>> GetByItemAndWarehouseAsync(Guid inventoryItemId, Guid warehouseId);
    Task<IEnumerable<InventoryCostLayer>> GetActiveLayers(Guid inventoryItemId);
    Task<InventoryCostLayer?> GetOldestActiveLayer(Guid inventoryItemId, Guid warehouseId); // For FIFO
    Task<InventoryCostLayer?> GetNewestActiveLayer(Guid inventoryItemId, Guid warehouseId); // For LIFO
    Task<IEnumerable<InventoryCostLayer>> GetLayersByLotAsync(Guid inventoryItemId, string lotNumber);
    Task<decimal> GetTotalInventoryValueAsync(Guid inventoryItemId);
    Task<decimal> GetWarehouseInventoryValueAsync(Guid warehouseId);
}

/// <summary>
/// Repository interface for Landed Costs
/// </summary>
public interface ILandedCostRepository : IGenericRepository<LandedCost>
{
    Task<IEnumerable<LandedCost>> GetByStatusAsync(string status);
    Task<IEnumerable<LandedCost>> GetByGRNAsync(Guid grnId);
    Task<LandedCost?> GetByLandedCostNumberAsync(string landedCostNumber);
    Task<LandedCost?> GetWithDetailsAsync(Guid landedCostId);
    Task<IEnumerable<LandedCost>> GetPendingApprovalAsync();
}

/// <summary>
/// Repository interface for Landed Cost Items
/// </summary>
public interface ILandedCostItemRepository : IGenericRepository<LandedCostItem>
{
    Task<IEnumerable<LandedCostItem>> GetByLandedCostAsync(Guid landedCostId);
    Task<IEnumerable<LandedCostItem>> GetByCostTypeAsync(LandedCostType costType);
}

/// <summary>
/// Repository interface for Landed Cost Allocations
/// </summary>
public interface ILandedCostAllocationRepository : IGenericRepository<LandedCostAllocation>
{
    Task<IEnumerable<LandedCostAllocation>> GetByLandedCostAsync(Guid landedCostId);
    Task<IEnumerable<LandedCostAllocation>> GetByGRNItemAsync(Guid grnItemId);
    Task<IEnumerable<LandedCostAllocation>> GetByInventoryItemAsync(Guid inventoryItemId);
    Task<decimal> GetTotalAllocatedCostAsync(Guid grnItemId);
}

#endregion

#region Purchase Return Repositories

/// <summary>
/// Repository interface for Purchase Returns
/// </summary>
public interface IPurchaseReturnRepository : IGenericRepository<PurchaseReturn>
{
    Task<IEnumerable<PurchaseReturn>> GetByStatusAsync(string status);
    Task<IEnumerable<PurchaseReturn>> GetBySupplierAsync(Guid supplierId);
    Task<IEnumerable<PurchaseReturn>> GetByWarehouseAsync(Guid warehouseId);
    Task<IEnumerable<PurchaseReturn>> GetByGRNAsync(Guid grnId);
    Task<PurchaseReturn?> GetByReturnNumberAsync(string returnNumber);
    Task<PurchaseReturn?> GetWithItemsAsync(Guid returnId);
    Task<IEnumerable<PurchaseReturn>> GetPendingApprovalAsync();
    Task<IEnumerable<PurchaseReturn>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);
}

/// <summary>
/// Repository interface for Purchase Return Items
/// </summary>
public interface IPurchaseReturnItemRepository : IGenericRepository<PurchaseReturnItem>
{
    Task<IEnumerable<PurchaseReturnItem>> GetByReturnAsync(Guid returnId);
    Task<IEnumerable<PurchaseReturnItem>> GetByInventoryItemAsync(Guid inventoryItemId);
    Task<IEnumerable<PurchaseReturnItem>> GetByGRNItemAsync(Guid grnItemId);
}

#endregion

#region Inventory Requisition Repositories

/// <summary>
/// Repository interface for Inventory Requisitions
/// </summary>
public interface IInventoryRequisitionRepository : IGenericRepository<InventoryRequisition>
{
    Task<IEnumerable<InventoryRequisition>> GetByStatusAsync(RequisitionStatus status);
    Task<IEnumerable<InventoryRequisition>> GetByWarehouseAsync(Guid warehouseId);
    Task<IEnumerable<InventoryRequisition>> GetByDepartmentAsync(Guid departmentId);
    Task<InventoryRequisition?> GetByRequisitionNumberAsync(string requisitionNumber);
    Task<InventoryRequisition?> GetWithItemsAsync(Guid id);
    Task<IEnumerable<InventoryRequisition>> GetByDateRangeAsync(DateTime fromDate, DateTime toDate);
    Task<IEnumerable<InventoryRequisition>> GetPendingApprovalAsync();
    Task<IEnumerable<InventoryRequisition>> GetPendingIssueAsync();
    Task<string> GenerateRequisitionNumberAsync();
}

/// <summary>
/// Repository interface for Requisition Items
/// </summary>
public interface IInventoryRequisitionItemRepository : IGenericRepository<InventoryRequisitionItem>
{
    Task<IEnumerable<InventoryRequisitionItem>> GetByRequisitionAsync(Guid requisitionId);
    Task<IEnumerable<InventoryRequisitionItem>> GetByInventoryItemAsync(Guid inventoryItemId);
}

#endregion

