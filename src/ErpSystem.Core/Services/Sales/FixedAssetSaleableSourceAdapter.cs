using System.Text.Json;
using ErpSystem.Core.DTOs.Sales;
using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Sales;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Sales;

public class FixedAssetSaleableSourceAdapter : ISalesSaleableSourceAdapter
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;

    public FixedAssetSaleableSourceAdapter(IUnitOfWork unitOfWork, ICurrentUserProvider currentUserProvider)
    {
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
    }

    public string AdapterKey => "asset-register";

    public IReadOnlyCollection<SalesSaleableSourceFilterDefinitionDto> FilterDefinitions { get; } =
    [
        new() { Field = "status", DisplayName = "Asset status" },
        new() { Field = "categoryName", DisplayName = "Asset category" },
        new() { Field = "categoryId", DisplayName = "Category ID" },
        new() { Field = "assetCode", DisplayName = "Asset code" },
        new() { Field = "serialNumber", DisplayName = "Serial number" },
        new() { Field = "name", DisplayName = "Asset name" }
    ];

    public async Task<IReadOnlyCollection<SalesSaleableItemDto>> SearchItemsAsync(
        SalesSaleableSource source,
        string? search = null,
        int take = 50)
    {
        var settings = ParseSettings(source.SettingsJson);
        var filters = settings.GetFilters();
        var normalizedTake = Math.Clamp(take, 1, 100);

        var query = _unitOfWork.Repository<FixedAsset>()
            .GetQueryable()
            .Include(x => x.Category)
            .Where(x =>
                x.TenantId == _currentUserProvider.TenantId
                && !x.IsDeleted
                && x.Status != FixedAssetStatus.Disposed
                && x.Status != FixedAssetStatus.WrittenOff);

        if (!filters.Any(x => IsStatusField(x.Field)))
        {
            query = query.Where(x => x.Status == FixedAssetStatus.HeldForSale);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x =>
                x.AssetCode.Contains(term)
                || x.Name.Contains(term)
                || (x.Description != null && x.Description.Contains(term))
                || (x.SerialNumber != null && x.SerialNumber.Contains(term))
                || x.Category.Name.Contains(term)
                || x.Category.Code.Contains(term));
        }

        var assets = await query
            .OrderBy(x => x.Name)
            .Take(Math.Max(normalizedTake * 5, 100))
            .ToListAsync();

        return assets
            .Where(x => MatchesFilters(x, filters))
            .Take(normalizedTake)
            .Select(x => MapAsset(source, x))
            .ToList();
    }

    private static SalesSaleableItemDto MapAsset(SalesSaleableSource source, FixedAsset asset)
    {
        var estimatedValue = asset.NetBookValue > 0
            ? asset.NetBookValue
            : asset.AcquisitionCost > 0
                ? asset.AcquisitionCost
                : asset.PurchasePrice;
        var isAvailable = asset.Status != FixedAssetStatus.Disposed
            && asset.Status != FixedAssetStatus.WrittenOff;

        return new SalesSaleableItemDto
        {
            SourceId = source.Id,
            SourceCode = source.Code,
            SourceType = source.SourceType,
            AdapterKey = source.AdapterKey,
            SourceItemId = asset.Id.ToString(),
            ItemCode = asset.AssetCode,
            ItemName = asset.Name,
            ItemType = asset.Category?.Name,
            Status = asset.Status.ToString(),
            CommercialStatus = asset.Status == FixedAssetStatus.HeldForSale
                ? "Available for sale"
                : asset.Status.ToString(),
            EstimatedValue = estimatedValue,
            Currency = source.DefaultCurrency,
            CanCreateSalesOrder = isAvailable,
            CanCreateSalesAgreement = false,
            CanCreateLeaseAgreement = false,
            SuggestedOrderType = "Standard",
            UnitOfMeasure = "EA",
            CurrentQuantity = 1,
            AvailableQuantity = isAvailable ? 1 : 0,
            AllocatedQuantity = 0,
            ShouldCreateSalesAllocation = true
        };
    }

    private static bool MatchesFilters(FixedAsset asset, IReadOnlyCollection<SourceFilterSetting> filters)
    {
        if (filters.Count == 0)
        {
            return true;
        }

        return filters.All(filter => MatchesFilter(asset, filter));
    }

    private static bool MatchesFilter(FixedAsset asset, SourceFilterSetting filter)
    {
        if (string.IsNullOrWhiteSpace(filter.Field) || string.IsNullOrWhiteSpace(filter.Value))
        {
            return true;
        }

        var field = Normalize(filter.Field);
        var value = filter.Value.Trim();

        return field switch
        {
            "status" or "assetstatus" => MatchesEnum(asset.Status, value),
            "categoryid" or "fixedassetcategoryid" => asset.FixedAssetCategoryId.ToString().Equals(value, StringComparison.OrdinalIgnoreCase),
            "category" or "categoryname" => TextMatches(asset.Category?.Name, value) || TextMatches(asset.Category?.Code, value),
            "assetcode" or "code" => TextMatches(asset.AssetCode, value),
            "serialnumber" or "serial" => TextMatches(asset.SerialNumber, value),
            "name" or "assetname" => TextMatches(asset.Name, value),
            _ => true
        };
    }

    private static bool IsStatusField(string? field)
        => !string.IsNullOrWhiteSpace(field)
            && (Normalize(field) == "status" || Normalize(field) == "assetstatus");

    private static bool MatchesEnum<TEnum>(TEnum enumValue, string expected)
        where TEnum : struct, Enum
    {
        expected = expected.Trim();
        if (expected.Equals("heldforsale", StringComparison.OrdinalIgnoreCase)
            || expected.Equals("held for sale", StringComparison.OrdinalIgnoreCase)
            || expected.Equals("sale", StringComparison.OrdinalIgnoreCase))
        {
            expected = nameof(FixedAssetStatus.HeldForSale);
        }

        if (int.TryParse(expected, out var numeric))
        {
            return Convert.ToInt32(enumValue) == numeric;
        }

        return enumValue.ToString().Equals(expected, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TextMatches(string? actual, string expected)
        => !string.IsNullOrWhiteSpace(actual)
            && actual.Contains(expected.Trim(), StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string value)
        => value.Replace(" ", string.Empty).Replace("_", string.Empty).Replace("-", string.Empty).Trim().ToLowerInvariant();

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
