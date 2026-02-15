using ErpSystem.Core.Entities.Pricing;

namespace ErpSystem.Core.Interfaces.Pricing;

/// <summary>
/// Repository interface for Price Lists
/// </summary>
public interface IPriceListRepository : IGenericRepository<PriceList>
{
    /// <summary>
    /// Get price list by code
    /// </summary>
    Task<PriceList?> GetByCodeAsync(string priceListCode);

    /// <summary>
    /// Get price list with all lines
    /// </summary>
    Task<PriceList?> GetByIdWithLinesAsync(Guid id);

    /// <summary>
    /// Get active price lists of a specific type
    /// </summary>
    Task<IEnumerable<PriceList>> GetActiveByTypeAsync(PriceListType type);

    /// <summary>
    /// Get price lists for a specific supplier
    /// </summary>
    Task<IEnumerable<PriceList>> GetBySupplierAsync(Guid supplierId);

    /// <summary>
    /// Get price lists for a specific customer
    /// </summary>
    Task<IEnumerable<PriceList>> GetByCustomerAsync(Guid customerId);

    /// <summary>
    /// Get default price list for a type
    /// </summary>
    Task<PriceList?> GetDefaultByTypeAsync(PriceListType type);

    /// <summary>
    /// Get effective price lists (active, approved, within date range)
    /// </summary>
    Task<IEnumerable<PriceList>> GetEffectivePriceListsAsync(PriceListType type, DateTime? asOfDate = null);

    /// <summary>
    /// Get price lists pending approval
    /// </summary>
    Task<IEnumerable<PriceList>> GetPendingApprovalAsync();

    /// <summary>
    /// Get price lists by status
    /// </summary>
    Task<IEnumerable<PriceList>> GetByStatusAsync(PriceListStatus status);

    /// <summary>
    /// Search price lists
    /// </summary>
    Task<IEnumerable<PriceList>> SearchAsync(string searchTerm);

    /// <summary>
    /// Get price lists expiring soon
    /// </summary>
    Task<IEnumerable<PriceList>> GetExpiringSoonAsync(int daysAhead = 30);

    /// <summary>
    /// Check if price list code exists
    /// </summary>
    Task<bool> CodeExistsAsync(string priceListCode, Guid? excludeId = null);
}

/// <summary>
/// Repository interface for Price List Lines
/// </summary>
public interface IPriceListLineRepository : IGenericRepository<PriceListLine>
{
    /// <summary>
    /// Get all lines for a price list
    /// </summary>
    Task<IEnumerable<PriceListLine>> GetByPriceListIdAsync(Guid priceListId);

    /// <summary>
    /// Get lines for a specific inventory item across all price lists
    /// </summary>
    Task<IEnumerable<PriceListLine>> GetByInventoryItemAsync(Guid inventoryItemId);

    /// <summary>
    /// Get price line for specific item in specific price list
    /// </summary>
    Task<PriceListLine?> GetByPriceListAndItemAsync(Guid priceListId, Guid inventoryItemId);

    /// <summary>
    /// Get active price lines for item with quantity tier matching
    /// </summary>
    Task<IEnumerable<PriceListLine>> GetActivePriceLinesForItemAsync(
        Guid inventoryItemId, 
        PriceListType priceListType,
        decimal quantity = 1);

    /// <summary>
    /// Bulk get price lines for multiple items
    /// </summary>
    Task<IEnumerable<PriceListLine>> GetByInventoryItemsAsync(IEnumerable<Guid> inventoryItemIds);

    /// <summary>
    /// Get lines with price changes
    /// </summary>
    Task<IEnumerable<PriceListLine>> GetLinesWithRecentChangesAsync(DateTime since);

    /// <summary>
    /// Bulk update prices
    /// </summary>
    Task BulkUpdatePricesAsync(IEnumerable<PriceListLine> lines);

    /// <summary>
    /// Delete all lines for a price list
    /// </summary>
    Task DeleteByPriceListIdAsync(Guid priceListId);
}

/// <summary>
/// Repository interface for Customer Groups
/// </summary>
public interface ICustomerGroupRepository : IGenericRepository<CustomerGroup>
{
    Task<CustomerGroup?> GetByCodeAsync(string groupCode);
    Task<IEnumerable<CustomerGroup>> GetActiveGroupsAsync();
    Task<bool> CodeExistsAsync(string groupCode, Guid? excludeId = null);
}

/// <summary>
/// Repository interface for Supplier Groups
/// </summary>
public interface ISupplierGroupRepository : IGenericRepository<SupplierGroup>
{
    Task<SupplierGroup?> GetByCodeAsync(string groupCode);
    Task<IEnumerable<SupplierGroup>> GetActiveGroupsAsync();
    Task<bool> CodeExistsAsync(string groupCode, Guid? excludeId = null);
}

/// <summary>
/// Repository interface for Price List Change History
/// </summary>
public interface IPriceListChangeHistoryRepository : IGenericRepository<PriceListChangeHistory>
{
    Task<IEnumerable<PriceListChangeHistory>> GetByInventoryItemAsync(Guid inventoryItemId);
    Task<IEnumerable<PriceListChangeHistory>> GetByPriceListLineAsync(Guid priceListLineId);
    Task<IEnumerable<PriceListChangeHistory>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);
}

