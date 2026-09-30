using ErpSystem.Core.DTOs.Estate;
using ErpSystem.Core.DTOs.Sales;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Estate;
using ErpSystem.Core.Interfaces.Sales;

namespace ErpSystem.Core.Services.Sales;

public class PropertyRegisterSaleableSourceAdapter : ISalesSaleableSourceAdapter
{
    private readonly IEstateManagedAssetService _managedAssetService;

    public PropertyRegisterSaleableSourceAdapter(IEstateManagedAssetService managedAssetService)
    {
        _managedAssetService = managedAssetService;
    }

    public string AdapterKey => "property-register";

    public IReadOnlyCollection<SalesSaleableSourceFilterDefinitionDto> FilterDefinitions { get; } =
    [
        new() { Field = "status", DisplayName = "Property status" },
        new() { Field = "assetType", DisplayName = "Property type" },
        new() { Field = "location", DisplayName = "Location" },
        new() { Field = "region", DisplayName = "Region" },
        new() { Field = "district", DisplayName = "District" },
        new() { Field = "town", DisplayName = "Town" },
        new() { Field = "currency", DisplayName = "Currency" },
        new() { Field = "boundaryVerified", DisplayName = "Boundary verified", ValueType = "boolean", Options = ["true", "false"] }
    ];

    public async Task<IReadOnlyCollection<SalesSaleableItemDto>> SearchItemsAsync(
        SalesSaleableSource source,
        string? search = null,
        int take = 50)
    {
        if (!source.AllowSalesOrders && !source.AllowSalesAgreements)
        {
            return [];
        }

        var normalizedTake = Math.Clamp(take, 1, 100);
        var query = new EstateManagedAssetQuery
        {
            Search = search,
            Take = Math.Max(normalizedTake * 5, 100),
            ExcludedStatuses = [EstateManagedAssetStatus.Sold, EstateManagedAssetStatus.Retired]
        };

        if (source.AllowSalesAgreements)
        {
            query.AvailableForSaleOrLease = true;
        }
        else
        {
            query.AvailableForSale = true;
        }

        // Estate/Sales handoff: request only transaction-eligible estate assets before the source service applies its take limit.
        var assets = await _managedAssetService.GetManagedAssetsAsync(query);

        var filters = SaleableSourceFilterSettings.Parse(source.SettingsJson);
        return assets
            .Where(asset => MatchesFilters(asset, filters))
            .Take(normalizedTake)
            .Select(asset => new SalesSaleableItemDto
            {
                SourceId = source.Id,
                SourceCode = source.Code,
                SourceType = source.SourceType,
                AdapterKey = AdapterKey,
                SourceItemId = asset.Id.ToString(),
                ItemCode = asset.AssetCode,
                ItemName = asset.Name,
                ItemType = asset.AssetType.ToString(),
                Status = asset.Status.ToString(),
                CommercialStatus = asset.Status.ToString(),
                EstimatedValue = asset.ValuationAmount,
                Currency = asset.Currency,
                AreaSquareMeters = asset.AreaSquareMeters,
                PropertyReference = asset.AssetCode,
                CanCreateSalesOrder = source.AllowSalesOrders && asset.IsAvailableForSale,
                CanCreateSalesAgreement = source.AllowSalesAgreements && asset.IsAvailableForSale,
                CanCreateLeaseAgreement = source.AllowSalesAgreements && asset.IsAvailableForLease,
                SuggestedOrderType = "PropertySale",
                SuggestedAgreementType = "General",
                SuggestedLeaseAgreementType = "TenancyAgreement",
                ProjectId = asset.ProjectId,
                ProjectCode = asset.ProjectCode,
                ProjectTitle = asset.ProjectTitle,
                ProjectUnitId = asset.ProjectUnitId,
                ProjectUnitCode = asset.ProjectUnitCode,
                ProjectUnitName = asset.Name,
                LocationName = asset.Location
            })
            .ToList();
    }

    private static bool MatchesFilters(
        EstateManagedAssetDto asset,
        IReadOnlyCollection<SaleableSourceFilterValue> filters)
        => filters.All(filter => SaleableSourceFilterSettings.Normalize(filter.Field) switch
        {
            "status" => asset.Status.ToString().Equals(filter.Value, StringComparison.OrdinalIgnoreCase),
            "assettype" or "propertytype" or "type" => asset.AssetType.ToString().Equals(filter.Value, StringComparison.OrdinalIgnoreCase),
            "location" => SaleableSourceFilterSettings.TextMatches(asset.Location, filter.Value),
            "region" => SaleableSourceFilterSettings.TextMatches(asset.Region, filter.Value),
            "district" => SaleableSourceFilterSettings.TextMatches(asset.District, filter.Value),
            "town" => SaleableSourceFilterSettings.TextMatches(asset.Town, filter.Value),
            "currency" => asset.Currency.Equals(filter.Value, StringComparison.OrdinalIgnoreCase),
            "boundaryverified" => SaleableSourceFilterSettings.BoolMatches(asset.BoundaryVerified, filter.Value),
            _ => true
        });
}
