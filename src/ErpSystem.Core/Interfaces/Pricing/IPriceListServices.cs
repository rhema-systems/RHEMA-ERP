using ErpSystem.Core.DTOs.Pricing;
using ErpSystem.Core.Entities.Pricing;

namespace ErpSystem.Core.Interfaces.Pricing;

/// <summary>
/// Service interface for Price List management
/// </summary>
public interface IPriceListService
{
    // Price List CRUD
    Task<IEnumerable<PriceListDto>> GetAllAsync();
    Task<PriceListDto?> GetByIdAsync(Guid id);
    Task<PriceListDto?> GetByCodeAsync(string code);
    Task<PriceListDto> CreateAsync(CreatePriceListDto dto);
    Task<PriceListDto> UpdateAsync(Guid id, UpdatePriceListDto dto);
    Task<bool> DeleteAsync(Guid id);
    
    // Filtering
    Task<IEnumerable<PriceListDto>> GetByTypeAsync(PriceListType type);
    Task<IEnumerable<PriceListDto>> GetActiveAsync();
    Task<IEnumerable<PriceListDto>> GetByStatusAsync(PriceListStatus status);
    Task<IEnumerable<PriceListDto>> SearchAsync(string searchTerm);
    
    // Approval Workflow
    Task<PriceListDto> SubmitForApprovalAsync(Guid id);
    Task<PriceListDto> ApproveAsync(Guid id, Guid approvedById, string? comments = null);
    Task<PriceListDto> RejectAsync(Guid id, Guid rejectedById, string reason);
    Task<IEnumerable<PriceListDto>> GetPendingApprovalAsync();
    
    // Price List Operations
    Task<PriceListDto> ActivateAsync(Guid id);
    Task<PriceListDto> DeactivateAsync(Guid id);
    Task<PriceListDto> CopyPriceListAsync(Guid sourcePriceListId, string newCode, string newName);
    Task<PriceListDto> SupersedePriceListAsync(Guid oldPriceListId, Guid newPriceListId);
    
    // Pricing Lookup
    Task<decimal?> GetPriceForItemAsync(Guid priceListId, Guid inventoryItemId, decimal quantity = 1);
    Task<PriceListLineDto?> GetPriceLineForItemAsync(Guid priceListId, Guid inventoryItemId, decimal quantity = 1);
}

/// <summary>
/// Service interface for Price List Line management
/// </summary>
public interface IPriceListLineService
{
    Task<IEnumerable<PriceListLineDto>> GetByPriceListAsync(Guid priceListId);
    Task<PriceListLineDto?> GetByIdAsync(Guid id);
    Task<PriceListLineDto> CreateAsync(CreatePriceListLineDto dto);
    Task<PriceListLineDto> UpdateAsync(Guid id, UpdatePriceListLineDto dto);
    Task<bool> DeleteAsync(Guid id);

    // Get lines by inventory item
    Task<IEnumerable<ItemPriceListLineDto>> GetByInventoryItemAsync(Guid inventoryItemId);

    // Bulk Operations
    Task<IEnumerable<PriceListLineDto>> BulkCreateAsync(IEnumerable<CreatePriceListLineDto> dtos);
    Task<int> BulkUpdatePricesAsync(Guid priceListId, decimal percentageChange);
    Task<int> BulkDeleteByPriceListAsync(Guid priceListId);

    // Import/Export
    Task<IEnumerable<PriceListLineDto>> ImportFromCsvAsync(Guid priceListId, Stream csvStream);
    Task<byte[]> ExportToCsvAsync(Guid priceListId);
}

/// <summary>
/// Service interface for Customer Group management
/// </summary>
public interface ICustomerGroupService
{
    Task<IEnumerable<CustomerGroupDto>> GetAllAsync();
    Task<CustomerGroupDto?> GetByIdAsync(Guid id);
    Task<CustomerGroupDto?> GetByCodeAsync(string code);
    Task<CustomerGroupDto> CreateAsync(CreateCustomerGroupDto dto);
    Task<CustomerGroupDto> UpdateAsync(Guid id, UpdateCustomerGroupDto dto);
    Task<bool> DeleteAsync(Guid id);
    Task<IEnumerable<CustomerGroupDto>> GetActiveAsync();
}

/// <summary>
/// Service interface for Supplier Group management
/// </summary>
public interface ISupplierGroupService
{
    Task<IEnumerable<SupplierGroupDto>> GetAllAsync();
    Task<SupplierGroupDto?> GetByIdAsync(Guid id);
    Task<SupplierGroupDto?> GetByCodeAsync(string code);
    Task<SupplierGroupDto> CreateAsync(CreateSupplierGroupDto dto);
    Task<SupplierGroupDto> UpdateAsync(Guid id, UpdateSupplierGroupDto dto);
    Task<bool> DeleteAsync(Guid id);
    Task<IEnumerable<SupplierGroupDto>> GetActiveAsync();
}

/// <summary>
/// Service interface for Price List Change History
/// </summary>
public interface IPriceListChangeHistoryService
{
    Task<IEnumerable<PriceListChangeHistoryDto>> GetByInventoryItemAsync(Guid inventoryItemId);
    Task<IEnumerable<PriceListChangeHistoryDto>> GetByPriceListLineAsync(Guid priceListLineId);
    Task<IEnumerable<PriceListChangeHistoryDto>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task RecordPriceChangeAsync(Guid priceListLineId, Guid inventoryItemId, decimal oldPrice, decimal newPrice, string changeType, string? reason = null);
}

