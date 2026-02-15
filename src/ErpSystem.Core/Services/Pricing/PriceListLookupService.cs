using ErpSystem.Core.Entities.Pricing;
using ErpSystem.Core.Interfaces.Pricing;
using ErpSystem.Core.Enums;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Pricing;

/// <summary>
/// Service for looking up prices from price lists
/// </summary>
public class PriceListLookupService
{
    private readonly IPriceListRepository _priceListRepository;
    private readonly IPriceListLineRepository _priceListLineRepository;
    private readonly ILogger<PriceListLookupService> _logger;

    public PriceListLookupService(
        IPriceListRepository priceListRepository,
        IPriceListLineRepository priceListLineRepository,
        ILogger<PriceListLookupService> logger)
    {
        _priceListRepository = priceListRepository;
        _priceListLineRepository = priceListLineRepository;
        _logger = logger;
    }

    /// <summary>
    /// Get the best price for an item from supplier price lists
    /// </summary>
    public async Task<PriceListLookupResult?> GetSupplierPriceAsync(
        Guid inventoryItemId,
        Guid supplierId,
        decimal quantity = 1,
        DateTime? asOfDate = null)
    {
        try
        {
            // Get supplier price lists
            var supplierPriceLists = await _priceListRepository.GetBySupplierAsync(supplierId);
            
            if (!supplierPriceLists.Any())
            {
                _logger.LogInformation("No price lists found for supplier {SupplierId}", supplierId);
                return null;
            }

            // Get active price lines for the item
            var priceLines = await _priceListLineRepository.GetActivePriceLinesForItemAsync(
                inventoryItemId,
                PriceListType.Purchase,
                quantity);

            // Filter to only supplier's price lists
            var supplierPriceLines = priceLines
                .Where(pl => supplierPriceLists.Any(spl => spl.Id == pl.PriceListId))
                .OrderByDescending(pl => pl.PriceList.Priority)
                .ThenByDescending(pl => pl.MinQuantity)
                .ToList();

            if (!supplierPriceLines.Any())
            {
                _logger.LogInformation("No price lines found for item {ItemId} from supplier {SupplierId}", 
                    inventoryItemId, supplierId);
                return null;
            }

            // Get the best price (highest priority, then highest min quantity)
            var bestPriceLine = supplierPriceLines.First();

            return new PriceListLookupResult
            {
                PriceListLineId = bestPriceLine.Id,
                PriceListId = bestPriceLine.PriceListId,
                PriceListCode = bestPriceLine.PriceList.PriceListCode,
                PriceListName = bestPriceLine.PriceList.Name,
                BasePrice = bestPriceLine.BasePrice,
                NetPrice = bestPriceLine.NetPrice,
                UnitOfMeasure = bestPriceLine.UnitOfMeasure,
                MinQuantity = bestPriceLine.MinQuantity,
                MaxQuantity = bestPriceLine.MaxQuantity,
                EffectiveFrom = bestPriceLine.PriceList.EffectiveFrom,
                EffectiveTo = bestPriceLine.PriceList.EffectiveTo,
                Currency = bestPriceLine.PriceList.Currency,
                DiscountPercent = bestPriceLine.DiscountPercent,
                LastPriceUpdate = bestPriceLine.LastPriceUpdate
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error looking up price for item {ItemId} from supplier {SupplierId}", 
                inventoryItemId, supplierId);
            return null;
        }
    }

    /// <summary>
    /// Get all available prices for an item from a supplier
    /// </summary>
    public async Task<List<PriceListLookupResult>> GetAllSupplierPricesAsync(
        Guid inventoryItemId,
        Guid supplierId)
    {
        try
        {
            var results = new List<PriceListLookupResult>();

            // Get supplier price lists
            var supplierPriceLists = await _priceListRepository.GetBySupplierAsync(supplierId);
            
            if (!supplierPriceLists.Any())
            {
                return results;
            }

            // Get all price lines for the item
            var priceLines = await _priceListLineRepository.GetByInventoryItemAsync(inventoryItemId);

            // Filter to only supplier's price lists and active ones
            var supplierPriceLines = priceLines
                .Where(pl => supplierPriceLists.Any(spl => spl.Id == pl.PriceListId) &&
                            pl.PriceList.Status == PriceListStatus.Active &&
                            pl.PriceList.ApprovalStatus == PriceListApprovalStatus.Approved)
                .OrderByDescending(pl => pl.PriceList.Priority)
                .ThenBy(pl => pl.MinQuantity)
                .ToList();

            foreach (var priceLine in supplierPriceLines)
            {
                results.Add(new PriceListLookupResult
                {
                    PriceListLineId = priceLine.Id,
                    PriceListId = priceLine.PriceListId,
                    PriceListCode = priceLine.PriceList.PriceListCode,
                    PriceListName = priceLine.PriceList.Name,
                    BasePrice = priceLine.BasePrice,
                    NetPrice = priceLine.NetPrice,
                    UnitOfMeasure = priceLine.UnitOfMeasure,
                    MinQuantity = priceLine.MinQuantity,
                    MaxQuantity = priceLine.MaxQuantity,
                    EffectiveFrom = priceLine.PriceList.EffectiveFrom,
                    EffectiveTo = priceLine.PriceList.EffectiveTo,
                    Currency = priceLine.PriceList.Currency,
                    DiscountPercent = priceLine.DiscountPercent,
                    LastPriceUpdate = priceLine.LastPriceUpdate
                });
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all prices for item {ItemId} from supplier {SupplierId}", 
                inventoryItemId, supplierId);
            return new List<PriceListLookupResult>();
        }
    }

    /// <summary>
    /// Get price history for an item from a supplier
    /// </summary>
    public async Task<List<PriceHistoryResult>> GetPriceHistoryAsync(
        Guid inventoryItemId,
        Guid supplierId,
        int months = 12)
    {
        try
        {
            var results = new List<PriceHistoryResult>();
            var sinceDate = DateTime.UtcNow.AddMonths(-months);

            // Get supplier price lists
            var supplierPriceLists = await _priceListRepository.GetBySupplierAsync(supplierId);
            
            if (!supplierPriceLists.Any())
            {
                return results;
            }

            // Get price lines with recent changes
            var priceLines = await _priceListLineRepository.GetLinesWithRecentChangesAsync(sinceDate);

            // Filter to only supplier's price lists and this item
            var relevantPriceLines = priceLines
                .Where(pl => pl.InventoryItemId == inventoryItemId &&
                            supplierPriceLists.Any(spl => spl.Id == pl.PriceListId))
                .OrderByDescending(pl => pl.LastPriceUpdate)
                .ToList();

            foreach (var priceLine in relevantPriceLines)
            {
                results.Add(new PriceHistoryResult
                {
                    Date = priceLine.LastPriceUpdate ?? priceLine.CreatedAt,
                    UnitPrice = priceLine.NetPrice,
                    UnitOfMeasure = priceLine.UnitOfMeasure,
                    PriceListName = priceLine.PriceList.Name,
                    Currency = priceLine.PriceList.Currency
                });
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting price history for item {ItemId} from supplier {SupplierId}", 
                inventoryItemId, supplierId);
            return new List<PriceHistoryResult>();
        }
    }
}

/// <summary>
/// Result of a price list lookup
/// </summary>
public class PriceListLookupResult
{
    public Guid PriceListLineId { get; set; }
    public Guid PriceListId { get; set; }
    public string PriceListCode { get; set; } = string.Empty;
    public string PriceListName { get; set; } = string.Empty;
    public decimal BasePrice { get; set; }
    public decimal NetPrice { get; set; }
    public string UnitOfMeasure { get; set; } = string.Empty;
    public decimal MinQuantity { get; set; }
    public decimal? MaxQuantity { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public string Currency { get; set; } = "USD";
    public decimal? DiscountPercent { get; set; }
    public DateTime? LastPriceUpdate { get; set; }
    
    public decimal FinalPrice => NetPrice;
}

/// <summary>
/// Price history result
/// </summary>
public class PriceHistoryResult
{
    public DateTime Date { get; set; }
    public decimal UnitPrice { get; set; }
    public string UnitOfMeasure { get; set; } = string.Empty;
    public string PriceListName { get; set; } = string.Empty;
    public string Currency { get; set; } = "USD";
}
