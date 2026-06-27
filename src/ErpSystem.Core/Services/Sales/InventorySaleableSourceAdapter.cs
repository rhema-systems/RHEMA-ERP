using System.Text.Json;
using ErpSystem.Core.DTOs.Sales;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Sales;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Sales;

public class InventorySaleableSourceAdapter : ISalesSaleableSourceAdapter
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;

    public InventorySaleableSourceAdapter(IUnitOfWork unitOfWork, ICurrentUserProvider currentUserProvider)
    {
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
    }

    public string AdapterKey => "inventory";

    public async Task<IReadOnlyCollection<SalesSaleableItemDto>> SearchItemsAsync(
        SalesSaleableSource source,
        string? search = null,
        int take = 50)
    {
        var settings = ParseSettings(source.SettingsJson);
        var filters = settings.GetFilters();
        var normalizedTake = Math.Clamp(take, 1, 100);

        if (Guid.TryParse(settings.LocationId, out var locationId))
        {
            return await SearchLocationItemsAsync(source, locationId, search, filters, normalizedTake);
        }

        if (Guid.TryParse(settings.WarehouseId, out var warehouseId))
        {
            return await SearchWarehouseItemsAsync(source, warehouseId, search, filters, normalizedTake);
        }

        return await SearchInventoryItemsAsync(source, search, filters, normalizedTake);
    }

    private async Task<IReadOnlyCollection<SalesSaleableItemDto>> SearchLocationItemsAsync(
        SalesSaleableSource source,
        Guid locationId,
        string? search,
        IReadOnlyCollection<SourceFilterSetting> filters,
        int take)
    {
        var query = _unitOfWork.Repository<InventoryLocation>()
            .GetQueryable()
            .Include(x => x.InventoryItem)
                .ThenInclude(x => x.Category)
            .Include(x => x.Location)
                .ThenInclude(x => x.Warehouse)
            .Where(x =>
                x.TenantId == _currentUserProvider.TenantId
                && !x.IsDeleted
                && x.LocationId == locationId
                && x.Location.IsActive
                && !x.Location.IsDeleted
                && x.InventoryItem.Status == ItemStatus.Active
                && !x.InventoryItem.IsDeleted
                && (x.AvailableQuantity > 0 || x.InventoryItem.ItemType != ItemType.StockItem));

        query = ApplySearch(query, search, x => x.InventoryItem);

        var records = await query
            .OrderBy(x => x.InventoryItem.Name)
            .Take(Math.Max(take * 5, 100))
            .ToListAsync();

        return records
            .Where(x => MatchesFilters(x.InventoryItem, filters))
            .Take(take)
            .Select(x => MapLocationItem(source, x))
            .ToList();
    }

    private async Task<IReadOnlyCollection<SalesSaleableItemDto>> SearchWarehouseItemsAsync(
        SalesSaleableSource source,
        Guid warehouseId,
        string? search,
        IReadOnlyCollection<SourceFilterSetting> filters,
        int take)
    {
        var query = _unitOfWork.Repository<WarehouseQuantity>()
            .GetQueryable()
            .Include(x => x.InventoryItem)
                .ThenInclude(x => x.Category)
            .Include(x => x.Warehouse)
            .Where(x =>
                x.TenantId == _currentUserProvider.TenantId
                && !x.IsDeleted
                && x.WarehouseId == warehouseId
                && x.Warehouse.IsActive
                && !x.Warehouse.IsDeleted
                && x.InventoryItem.Status == ItemStatus.Active
                && !x.InventoryItem.IsDeleted
                && (x.AvailableStock > 0 || x.InventoryItem.ItemType != ItemType.StockItem));

        query = ApplySearch(query, search, x => x.InventoryItem);

        var records = await query
            .OrderBy(x => x.InventoryItem.Name)
            .Take(Math.Max(take * 5, 100))
            .ToListAsync();

        return records
            .Where(x => MatchesFilters(x.InventoryItem, filters))
            .Take(take)
            .Select(x => MapWarehouseItem(source, x))
            .ToList();
    }

    private async Task<IReadOnlyCollection<SalesSaleableItemDto>> SearchInventoryItemsAsync(
        SalesSaleableSource source,
        string? search,
        IReadOnlyCollection<SourceFilterSetting> filters,
        int take)
    {
        var query = _unitOfWork.Repository<InventoryItem>()
            .GetQueryable()
            .Include(x => x.Category)
            .Where(x =>
                x.TenantId == _currentUserProvider.TenantId
                && !x.IsDeleted
                && x.Status == ItemStatus.Active
                && (x.AvailableStock > 0 || x.ItemType != ItemType.StockItem));

        query = ApplySearch(query, search, x => x);

        var records = await query
            .OrderBy(x => x.Name)
            .Take(Math.Max(take * 5, 100))
            .ToListAsync();

        return records
            .Where(x => MatchesFilters(x, filters))
            .Take(take)
            .Select(x => MapInventoryItem(source, x))
            .ToList();
    }

    private static IQueryable<T> ApplySearch<T>(
        IQueryable<T> query,
        string? search,
        System.Linq.Expressions.Expression<Func<T, InventoryItem>> itemSelector)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return query;
        }

        var term = search.Trim();
        return query.Where(BuildSearchExpression(itemSelector, term));
    }

    private static System.Linq.Expressions.Expression<Func<T, bool>> BuildSearchExpression<T>(
        System.Linq.Expressions.Expression<Func<T, InventoryItem>> itemSelector,
        string term)
    {
        var item = itemSelector.Body;
        var parameter = itemSelector.Parameters[0];
        var containsMethod = typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;
        var termConstant = System.Linq.Expressions.Expression.Constant(term);

        System.Linq.Expressions.Expression BuildContains(string propertyName)
        {
            var property = System.Linq.Expressions.Expression.Property(item, propertyName);
            return System.Linq.Expressions.Expression.AndAlso(
                System.Linq.Expressions.Expression.NotEqual(property, System.Linq.Expressions.Expression.Constant(null, typeof(string))),
                System.Linq.Expressions.Expression.Call(property, containsMethod, termConstant));
        }

        var itemCode = BuildContains(nameof(InventoryItem.ItemCode));
        var name = BuildContains(nameof(InventoryItem.Name));
        var description = BuildContains(nameof(InventoryItem.Description));
        var brand = BuildContains(nameof(InventoryItem.Brand));
        var model = BuildContains(nameof(InventoryItem.Model));

        var body = System.Linq.Expressions.Expression.OrElse(
            System.Linq.Expressions.Expression.OrElse(itemCode, name),
            System.Linq.Expressions.Expression.OrElse(
                description,
                System.Linq.Expressions.Expression.OrElse(brand, model)));

        return System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(body, parameter);
    }

    private static SalesSaleableItemDto MapLocationItem(SalesSaleableSource source, InventoryLocation record)
    {
        var item = record.InventoryItem;
        var location = record.Location;
        var warehouse = location.Warehouse;

        return MapBaseItem(
            source,
            item,
            sourceItemId: $"{item.Id}:{location.Id}",
            currentQuantity: record.Quantity,
            availableQuantity: record.AvailableQuantity,
            allocatedQuantity: record.AllocatedQuantity,
            warehouseId: location.InventoryWarehouseId,
            warehouseName: warehouse?.Name,
            locationId: location.Id,
            locationName: BuildLocationName(location));
    }

    private static SalesSaleableItemDto MapWarehouseItem(SalesSaleableSource source, WarehouseQuantity record)
    {
        var item = record.InventoryItem;

        return MapBaseItem(
            source,
            item,
            sourceItemId: $"{item.Id}:{record.WarehouseId}",
            currentQuantity: record.CurrentStock,
            availableQuantity: record.AvailableStock,
            allocatedQuantity: record.AllocatedStock,
            warehouseId: record.WarehouseId,
            warehouseName: record.Warehouse.Name);
    }

    private static SalesSaleableItemDto MapInventoryItem(SalesSaleableSource source, InventoryItem item)
        => MapBaseItem(
            source,
            item,
            sourceItemId: item.Id.ToString(),
            currentQuantity: item.CurrentStock,
            availableQuantity: item.AvailableStock,
            allocatedQuantity: item.AllocatedStock);

    private static SalesSaleableItemDto MapBaseItem(
        SalesSaleableSource source,
        InventoryItem item,
        string sourceItemId,
        decimal currentQuantity,
        decimal availableQuantity,
        decimal allocatedQuantity,
        Guid? warehouseId = null,
        string? warehouseName = null,
        Guid? locationId = null,
        string? locationName = null)
    {
        var canCreateOrder = item.ItemType != ItemType.StockItem || availableQuantity > 0;
        return new SalesSaleableItemDto
        {
            SourceId = source.Id,
            SourceCode = source.Code,
            SourceType = source.SourceType,
            AdapterKey = source.AdapterKey,
            SourceItemId = sourceItemId,
            ItemCode = item.ItemCode,
            ItemName = item.Name,
            ItemType = item.ItemType.ToString(),
            Status = item.Status.ToString(),
            CommercialStatus = canCreateOrder ? "Available" : "Out of stock",
            EstimatedValue = item.SalePrice > 0 ? item.SalePrice : item.AverageCost,
            Currency = source.DefaultCurrency,
            CanCreateSalesOrder = canCreateOrder,
            CanCreateSalesAgreement = false,
            CanCreateLeaseAgreement = false,
            SuggestedOrderType = "Standard",
            InventoryItemId = item.Id,
            WarehouseId = warehouseId,
            WarehouseName = warehouseName,
            LocationId = locationId,
            LocationName = locationName,
            UnitOfMeasure = item.UnitOfMeasure,
            CurrentQuantity = currentQuantity,
            AvailableQuantity = availableQuantity,
            AllocatedQuantity = allocatedQuantity,
            ShouldCreateSalesAllocation = false
        };
    }

    private static bool MatchesFilters(InventoryItem item, IReadOnlyCollection<SourceFilterSetting> filters)
    {
        if (filters.Count == 0)
        {
            return true;
        }

        return filters.All(filter => MatchesFilter(item, filter));
    }

    private static bool MatchesFilter(InventoryItem item, SourceFilterSetting filter)
    {
        if (string.IsNullOrWhiteSpace(filter.Field) || string.IsNullOrWhiteSpace(filter.Value))
        {
            return true;
        }

        var field = Normalize(filter.Field);
        var value = filter.Value.Trim();

        return field switch
        {
            "itemtype" or "inventorytype" or "type" => MatchesEnum(item.ItemType, value),
            "status" => MatchesEnum(item.Status, value),
            "categoryid" => item.CategoryId.ToString().Equals(value, StringComparison.OrdinalIgnoreCase),
            "category" or "categoryname" => TextMatches(item.Category?.Name, value) || TextMatches(item.Category?.Code, value),
            "brand" => TextMatches(item.Brand, value),
            "manufacturer" => TextMatches(item.Manufacturer, value),
            "model" => TextMatches(item.Model, value),
            "unitofmeasure" or "uom" => TextMatches(item.UnitOfMeasure, value),
            "isserialtracked" => MatchesBool(item.IsSerialTracked, value),
            "islottracked" => MatchesBool(item.IsLotTracked, value),
            "isexpirationtracked" => MatchesBool(item.IsExpirationTracked, value),
            "islocationtracked" => MatchesBool(item.IsLocationTracked, value),
            _ => true
        };
    }

    private static bool MatchesEnum<TEnum>(TEnum enumValue, string expected)
        where TEnum : struct, Enum
    {
        expected = expected.Trim();
        if (typeof(TEnum) == typeof(ItemType)
            && (expected.Equals("stock", StringComparison.OrdinalIgnoreCase)
                || expected.Equals("stocks", StringComparison.OrdinalIgnoreCase)
                || expected.Equals("stockitem", StringComparison.OrdinalIgnoreCase)))
        {
            expected = nameof(ItemType.StockItem);
        }

        if (int.TryParse(expected, out var numeric))
        {
            return Convert.ToInt32(enumValue) == numeric;
        }

        return enumValue.ToString().Equals(expected, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesBool(bool actual, string expected)
        => bool.TryParse(expected, out var parsed)
            ? actual == parsed
            : (actual ? "yes" : "no").Equals(expected.Trim(), StringComparison.OrdinalIgnoreCase);

    private static bool TextMatches(string? actual, string expected)
        => !string.IsNullOrWhiteSpace(actual)
            && actual.Contains(expected.Trim(), StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string value)
        => value.Replace(" ", string.Empty).Replace("_", string.Empty).Replace("-", string.Empty).Trim().ToLowerInvariant();

    private static string BuildLocationName(WarehouseLocation location)
        => string.IsNullOrWhiteSpace(location.Name)
            ? location.LocationCode
            : $"{location.LocationCode} - {location.Name}";

    private static SourceSettings ParseSettings(string? settingsJson)
    {
        if (string.IsNullOrWhiteSpace(settingsJson))
        {
            return new SourceSettings();
        }

        try
        {
            return JsonSerializer.Deserialize<SourceSettings>(
                settingsJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new SourceSettings();
        }
        catch (JsonException)
        {
            return new SourceSettings();
        }
    }

    private sealed class SourceSettings
    {
        public string? WarehouseId { get; set; }
        public string? WarehouseName { get; set; }
        public string? LocationId { get; set; }
        public string? LocationName { get; set; }
        public string? FilterField { get; set; }
        public string? FilterValue { get; set; }
        public List<SourceFilterSetting>? Filters { get; set; }

        public IReadOnlyCollection<SourceFilterSetting> GetFilters()
        {
            var filters = (Filters ?? [])
                .Where(x => !string.IsNullOrWhiteSpace(x.Field) && !string.IsNullOrWhiteSpace(x.Value))
                .ToList();

            if (!string.IsNullOrWhiteSpace(FilterField) && !string.IsNullOrWhiteSpace(FilterValue))
            {
                filters.Add(new SourceFilterSetting { Field = FilterField, Value = FilterValue });
            }

            return filters;
        }
    }

    private sealed class SourceFilterSetting
    {
        public string? Field { get; set; }
        public string? Value { get; set; }
    }
}
