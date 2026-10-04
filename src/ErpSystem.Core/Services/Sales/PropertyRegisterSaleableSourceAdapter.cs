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
        new() { Field = "boundaryVerified", DisplayName = "Boundary verified", ValueType = "boolean", Options = ["true", "false"] },
        new() { Field = "isPublishedToExternalPortal", DisplayName = "Published to portal", ValueType = "boolean", Options = ["true", "false"] },
        new() { Field = "externalListingStatus", DisplayName = "Portal listing status", Options = ["Published"] }
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
        var filters = SaleableSourceFilterSettings.Parse(source.SettingsJson);
        var query = new EstateManagedAssetQuery
        {
            Search = search,
            Take = Math.Max(normalizedTake * 5, 100),
            AssetType = ResolveAssetType(filters),
            PublishedToExternalPortal = true
        };

        // Property and Facility sources are portal-register views. Transaction eligibility remains
        // explicit on each result through the CanCreate... flags below.
        var assets = await _managedAssetService.GetManagedAssetsAsync(query);

        return assets
            .Where(asset => MatchesFilters(asset, filters))
            .Take(normalizedTake)
            .Select(asset => new SalesSaleableItemDto
            {
                SourceId = source.Id,
                SourceCode = source.Code,
                SourceType = source.SourceType,
                AdapterKey = AdapterKey,
                SourceItemId = (asset.ProjectUnitId ?? asset.Id).ToString(),
                ItemCode = asset.AssetCode,
                ItemName = asset.Name,
                ItemType = asset.AssetType.ToString(),
                Status = asset.Status.ToString(),
                CommercialStatus = asset.Status.ToString(),
                EstimatedValue = asset.ValuationAmount,
                Currency = asset.Currency,
                AreaSquareMeters = asset.AreaSquareMeters,
                PropertyReference = asset.AssetCode,
                CanCreateSalesOrder = source.AllowSalesOrders,
                SalesOrderIneligibilityReason = !source.AllowSalesOrders
                    ? "Sales orders are disabled for this saleable source."
                    : null,
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
            "ispublishedtoexternalportal" => SaleableSourceFilterSettings.BoolMatches(asset.IsPublishedToExternalPortal, filter.Value),
            "externallistingstatus" => asset.ExternalListingStatus.Equals(filter.Value, StringComparison.OrdinalIgnoreCase),
            _ => true
        });

    private static EstateManagedAssetType? ResolveAssetType(
        IReadOnlyCollection<SaleableSourceFilterValue> filters)
    {
        var configured = filters.FirstOrDefault(filter =>
            SaleableSourceFilterSettings.Normalize(filter.Field) is "assettype" or "propertytype" or "type");

        return configured != null
            && Enum.TryParse<EstateManagedAssetType>(configured.Value, true, out var assetType)
                ? assetType
                : null;
    }
}
