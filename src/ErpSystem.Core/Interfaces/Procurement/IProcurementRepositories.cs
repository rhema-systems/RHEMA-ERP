using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.DTOs.Common;

namespace ErpSystem.Core.Interfaces.Procurement;

#region Supplier Management Repositories

/// <summary>
/// Repository interface for supplier management
/// </summary>
public interface ISupplierRepository : IGenericRepository<Supplier>
{
    Task<IEnumerable<Supplier>> GetActiveSuppliers();
    Task<Supplier?> GetBySupplierCodeAsync(string supplierCode);
    Task<bool> IsSupplierCodeUniqueAsync(string supplierCode, Guid? excludeId = null);
    Task<IEnumerable<Supplier>> GetPreferredSuppliersAsync();
    Task<IEnumerable<Supplier>> GetSuppliersByTypeAsync(string supplierType);
    Task<IEnumerable<Supplier>> SearchSuppliersAsync(string searchTerm);
    Task<IEnumerable<Supplier>> GetSuppliersByRatingAsync(int minRating);
    Task<Supplier?> GetSupplierWithContactsAsync(Guid supplierId);
    Task<IEnumerable<Supplier>> GetSuppliersForInventoryItemAsync(Guid inventoryItemId);
    Task<ErpSystem.Core.DTOs.Common.PagedResult<Supplier>> GetSuppliersAsync(int page, int pageSize, string? search = null, string? status = null, string? supplierType = null, bool? isActive = null, bool? isPreferred = null);
    Task<Supplier?> GetSupplierByIdAsync(Guid id);
    Task<Supplier?> GetSupplierByCodeAsync(string supplierCode);
    Task<Supplier> CreateSupplierAsync(Supplier supplier);
    Task<Supplier> UpdateSupplierAsync(Supplier supplier);
    Task DeleteSupplierAsync(Guid id);
    Task<bool> HasActivePurchaseOrdersAsync(Guid supplierId);
    Task UpdateSupplierStatusAsync(Guid supplierId, string status);
}

/// <summary>
/// Repository interface for supplier contacts
/// </summary>
public interface ISupplierContactRepository : IGenericRepository<SupplierContact>
{
    Task<IEnumerable<SupplierContact>> GetContactsBySupplierAsync(Guid supplierId);
    Task<SupplierContact?> GetPrimaryContactAsync(Guid supplierId);
    Task<IEnumerable<SupplierContact>> GetContactsByTypeAsync(string contactType);
    Task<IEnumerable<SupplierContact>> GetContactsBySupplierId(Guid supplierId);
    Task<SupplierContact> CreateContactAsync(SupplierContact contact);
}

/// <summary>
/// Repository interface for supplier item catalog
/// </summary>
public interface ISupplierItemCatalogRepository : IGenericRepository<SupplierItemCatalog>
{
    Task<IEnumerable<SupplierItemCatalog>> GetCatalogBySupplierAsync(Guid supplierId);
    Task<IEnumerable<SupplierItemCatalog>> GetCatalogByInventoryItemAsync(Guid inventoryItemId);
    Task<SupplierItemCatalog?> GetCatalogItemAsync(Guid supplierId, Guid inventoryItemId);
    Task<IEnumerable<SupplierItemCatalog>> GetActiveItemsAsync(Guid supplierId);
    Task<IEnumerable<SupplierItemCatalog>> GetPreferredSuppliersForItemAsync(Guid inventoryItemId);
    Task<SupplierItemCatalog?> GetBestPriceForItemAsync(Guid inventoryItemId);
    Task<IEnumerable<SupplierItemCatalog>> GetCatalogBySupplierId(Guid supplierId);
}

#endregion

#region Purchase Order Repositories

/// <summary>
/// Repository interface for purchase orders
/// </summary>
public interface IPurchaseOrderRepository : IGenericRepository<PurchaseOrder>
{
    Task<IEnumerable<PurchaseOrder>> GetActiveOrdersAsync();
    Task<IEnumerable<PurchaseOrder>> GetOrdersByStatusAsync(string status);
    Task<IEnumerable<PurchaseOrder>> GetOrdersBySupplierAsync(Guid supplierId);
    Task<PurchaseOrder?> GetByOrderNumberAsync(string orderNumber);
    Task<PurchaseOrder?> GetWithItemsAsync(Guid purchaseOrderId);
    Task<IEnumerable<PurchaseOrder>> GetOrdersDueAsync(int daysAhead = 7);
    Task<IEnumerable<PurchaseOrder>> GetOverdueOrdersAsync();
    Task<IEnumerable<PurchaseOrder>> GetOrdersByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<string> GenerateOrderNumberAsync();
    Task<decimal> GetTotalOrderValueBySupplierAsync(Guid supplierId, DateTime startDate, DateTime endDate);
    Task<IEnumerable<PurchaseOrder>> GetOrdersRequiringApprovalAsync();
    Task<ErpSystem.Core.DTOs.Common.PagedResult<PurchaseOrder>> GetPurchaseOrdersAsync(int page, int pageSize, string? search = null, string? status = null, Guid? supplierId = null, DateTime? startDate = null, DateTime? endDate = null);
    Task<PurchaseOrder?> GetPurchaseOrderByIdAsync(Guid id);
    Task<PurchaseOrder> CreatePurchaseOrderAsync(PurchaseOrder purchaseOrder);
    Task<PurchaseOrder> UpdatePurchaseOrderAsync(PurchaseOrder purchaseOrder);
    Task UpdateStatusAsync(Guid purchaseOrderId, string status);
    Task<IEnumerable<PurchaseOrder>> GetPurchaseOrdersBySupplierId(Guid supplierId);
    Task<IEnumerable<PurchaseOrder>> GetPurchaseOrdersByStatus(string status);
}

/// <summary>
/// Repository interface for purchase order items
/// </summary>
public interface IPurchaseOrderItemRepository : IGenericRepository<PurchaseOrderItem>
{
    Task<IEnumerable<PurchaseOrderItem>> GetItemsByOrderAsync(Guid purchaseOrderId);
    Task<IEnumerable<PurchaseOrderItem>> GetPendingItemsAsync();
    Task<IEnumerable<PurchaseOrderItem>> GetItemsByInventoryItemAsync(Guid inventoryItemId);
    Task<IEnumerable<PurchaseOrderItem>> GetItemsBySupplierAsync(Guid supplierId);
    Task<IEnumerable<PurchaseOrderItem>> GetOverdueItemsAsync();
    Task<IEnumerable<PurchaseOrderItem>> GetItemsByPurchaseOrderIdAsync(Guid purchaseOrderId);
    Task<PurchaseOrderItem> CreateItemAsync(PurchaseOrderItem item);
    Task<PurchaseOrderItem> UpdateItemAsync(PurchaseOrderItem item);
    Task<PurchaseOrderItem?> GetItemByIdAsync(Guid itemId);
}

#endregion

#region Purchase Order Receipt Repositories

/// <summary>
/// Repository interface for purchase order receipts
/// </summary>
public interface IPurchaseOrderReceiptRepository : IGenericRepository<PurchaseOrderReceipt>
{
    Task<IEnumerable<PurchaseOrderReceipt>> GetReceiptsByOrderAsync(Guid purchaseOrderId);
    Task<IEnumerable<PurchaseOrderReceipt>> GetReceiptsByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<PurchaseOrderReceipt?> GetByReceiptNumberAsync(string receiptNumber);
    Task<PurchaseOrderReceipt?> GetReceiptWithItemsAsync(Guid receiptId);
    Task<IEnumerable<PurchaseOrderReceipt>> GetReceiptsByStatusAsync(string status);
    Task<IEnumerable<PurchaseOrderReceipt>> GetReceiptsRequiringInspectionAsync();
    Task<string> GenerateReceiptNumberAsync();
    Task<IEnumerable<PurchaseOrderReceipt>> GetReceiptsByPurchaseOrderIdAsync(Guid purchaseOrderId);
    Task<PurchaseOrderReceipt> CreateReceiptAsync(PurchaseOrderReceipt receipt);
    Task<PurchaseOrderReceiptItem> CreateReceiptItemAsync(PurchaseOrderReceiptItem receiptItem);
}

/// <summary>
/// Repository interface for purchase order receipt items
/// </summary>
public interface IPurchaseOrderReceiptItemRepository : IGenericRepository<PurchaseOrderReceiptItem>
{
    Task<IEnumerable<PurchaseOrderReceiptItem>> GetItemsByReceiptAsync(Guid receiptId);
    Task<IEnumerable<PurchaseOrderReceiptItem>> GetItemsByOrderItemAsync(Guid purchaseOrderItemId);
    Task<IEnumerable<PurchaseOrderReceiptItem>> GetRejectedItemsAsync();
    Task<IEnumerable<PurchaseOrderReceiptItem>> GetItemsByQualityStatusAsync(string qualityStatus);
}

#endregion

#region Purchase Requisition Repositories

/// <summary>
/// Repository interface for purchase requisitions
/// </summary>
public interface IPurchaseRequisitionRepository : IGenericRepository<PurchaseRequisition>
{
    Task<IEnumerable<PurchaseRequisition>> GetRequisitionsByStatusAsync(string status);
    Task<IEnumerable<PurchaseRequisition>> GetRequisitionsByRequesterAsync(Guid requesterId);
    Task<PurchaseRequisition?> GetByRequisitionNumberAsync(string requisitionNumber);
    Task<PurchaseRequisition?> GetRequisitionWithItemsAsync(Guid requisitionId);
    Task<IEnumerable<PurchaseRequisition>> GetRequisitionsRequiringApprovalAsync();
    Task<IEnumerable<PurchaseRequisition>> GetRequisitionsByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<IEnumerable<PurchaseRequisition>> GetRequisitionsByDepartmentAsync(string department);
    Task<IEnumerable<PurchaseRequisition>> GetUrgentRequisitionsAsync();
    Task<string> GenerateRequisitionNumberAsync();
    Task<ErpSystem.Core.DTOs.Common.PagedResult<PurchaseRequisition>> GetRequisitionsAsync(int page, int pageSize, string? search = null, string? status = null, string? priority = null, DateTime? startDate = null, DateTime? endDate = null, string? department = null);
    Task<PurchaseRequisition?> GetRequisitionByIdAsync(Guid id);
    Task<PurchaseRequisition> CreateRequisitionAsync(PurchaseRequisition requisition);
    Task UpdateStatusAsync(Guid requisitionId, string status);
    Task<PurchaseRequisition> UpdateRequisitionAsync(PurchaseRequisition requisition);
    Task<IEnumerable<PurchaseRequisition>> GetRequisitionsByStatus(string status);
    Task<IEnumerable<PurchaseRequisition>> GetRequisitionsByPriority(string priority);
    Task<IEnumerable<PurchaseRequisition>> GetRequisitionsByDepartment(string department);
    Task<IEnumerable<PurchaseRequisition>> GetPendingApprovalRequisitions();
}

/// <summary>
/// Repository interface for purchase requisition items
/// </summary>
public interface IPurchaseRequisitionItemRepository : IGenericRepository<PurchaseRequisitionItem>
{
    Task<IEnumerable<PurchaseRequisitionItem>> GetItemsByRequisitionAsync(Guid requisitionId);
    Task<IEnumerable<PurchaseRequisitionItem>> GetPendingItemsAsync();
    Task<IEnumerable<PurchaseRequisitionItem>> GetItemsByInventoryItemAsync(Guid inventoryItemId);
    Task<IEnumerable<PurchaseRequisitionItem>> GetItemsBySupplierAsync(Guid supplierId);
    Task<IEnumerable<PurchaseRequisitionItem>> GetApprovedItemsNotOrderedAsync();
    Task<IEnumerable<PurchaseRequisitionItem>> GetItemsByRequisitionIdAsync(Guid requisitionId);
    Task<PurchaseRequisitionItem> CreateItemAsync(PurchaseRequisitionItem item);
}

#endregion