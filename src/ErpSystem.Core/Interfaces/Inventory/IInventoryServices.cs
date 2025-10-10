using ErpSystem.Core.DTOs.Inventory;

namespace ErpSystem.Core.Interfaces.Inventory;

/// <summary>
/// Main inventory management service interface
/// Used by Maintenance, Production, Sales, and other modules
/// </summary>
public interface IInventoryManagementService
{
    // Item Management
    Task<IEnumerable<InventoryItemDto>> GetMaintenancePartsAsync(string? searchTerm = null);
    Task<InventoryItemDetailDto?> GetInventoryItemDetailAsync(Guid itemId);
    
    // Stock Allocation
    Task<InventoryAllocationDto> AllocateForWorkOrderAsync(AllocateInventoryDto request);
    Task<bool> ConsumeAllocatedInventoryAsync(Guid allocationId, decimal quantity, Guid userId);
    Task<bool> ReleaseAllocationAsync(Guid allocationId, Guid userId);
    
    // Stock Availability
    Task<StockAvailabilityDto> CheckStockAvailabilityAsync(Guid inventoryItemId, decimal requiredQuantity);
    Task<IEnumerable<ReorderRequiredDto>> GetItemsRequiringReorderAsync();
}

/// <summary>
/// Warehouse management service interface
/// </summary>
public interface IWarehouseManagementService
{
    Task<IEnumerable<WarehouseDto>> GetWarehousesAsync();
    Task<WarehouseDto?> GetWarehouseByIdAsync(Guid warehouseId);
    Task<IEnumerable<WarehouseLocationDto>> GetWarehouseLocationsAsync(Guid warehouseId);
    Task<WarehouseLocationDto?> GetLocationByIdAsync(Guid locationId);
}

/// <summary>
/// Purchase order management service interface
/// </summary>
public interface IPurchaseOrderManagementService
{
    Task<IEnumerable<PurchaseOrderSummaryDto>> GetPurchaseOrdersAsync();
    Task<PurchaseOrderDetailDto?> GetPurchaseOrderByIdAsync(Guid purchaseOrderId);
    Task<PurchaseOrderDto> CreatePurchaseOrderAsync(CreatePurchaseOrderDto request);
    Task<bool> ApprovePurchaseOrderAsync(Guid purchaseOrderId, Guid approvedById);
    Task<PurchaseOrderReceiptDto> ReceivePurchaseOrderAsync(ReceivePurchaseOrderDto request);
}

/// <summary>
/// Stock movement tracking service interface
/// </summary>
public interface IStockMovementService
{
    Task<IEnumerable<StockMovementDto>> GetStockMovementsAsync(Guid? inventoryItemId = null, DateTime? fromDate = null, DateTime? toDate = null);
    Task<StockMovementDto> CreateStockMovementAsync(CreateStockMovementDto request);
    Task<IEnumerable<StockMovementDto>> GetMovementsByReferenceAsync(string referenceType, Guid referenceId);
}