using ErpSystem.Core.DTOs.Sales;
using ErpSystem.Core.Entities.Estate;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Sales;
using ErpSystem.Core.Services.Estate;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Core.Services.Sales;

public sealed class LandManagementSaleableSourceAdapter : ISalesSaleableSourceAdapter
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;

    public LandManagementSaleableSourceAdapter(
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider)
    {
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
    }

    public string AdapterKey => "land-management";

    public IReadOnlyCollection<SalesSaleableSourceFilterDefinitionDto> FilterDefinitions { get; } =
    [
        new()
        {
            Field = "isPublishedToExternalPortal",
            DisplayName = "Published to external portal",
            ValueType = "boolean",
            IsRequired = true,
            DefaultValue = "true",
            Options = ["true"]
        },
        new()
        {
            Field = "externalListingStatus",
            DisplayName = "External listing status",
            ValueType = "select",
            IsRequired = true,
            DefaultValue = "Published",
            Options = ["Published"]
        },
        new() { Field = "externalListingType", DisplayName = "Listing type", ValueType = "select", Options = ["Sale", "Rent", "Lease", "SaleAndRent", "SaleAndLease"] },
        new() { Field = "location", DisplayName = "Location" },
        new() { Field = "region", DisplayName = "Region" },
        new() { Field = "district", DisplayName = "District" },
        new() { Field = "town", DisplayName = "Town" },
        new() { Field = "boundaryVerified", DisplayName = "Boundary verified", ValueType = "boolean", Options = ["true", "false"] },
        new() { Field = "currency", DisplayName = "Currency" },
        new() { Field = "parentLandAssetReference", DisplayName = "Parent land reference" }
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
        var query = _unitOfWork.Repository<EstateLandDemarcation>()
            .GetQueryable()
            .AsNoTracking()
            .Include(item => item.EstateManagedAsset)
            .Where(item => item.TenantId == _currentUserProvider.TenantId
                && !item.IsDeleted
                && item.IsPublishedToExternalPortal
                && item.ExternalListingStatus == "Published"
                && item.BoundaryVerified
                && item.EstateManagedAsset.TenantId == _currentUserProvider.TenantId
                && !item.EstateManagedAsset.IsDeleted
                && item.EstateManagedAsset.AssetType == EstateManagedAssetType.Land
                && item.EstateManagedAsset.Status == EstateManagedAssetStatus.LandBank
                && !item.EstateManagedAsset.ProjectId.HasValue
                && !item.EstateManagedAsset.IsPublishedToExternalPortal);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(item => item.Description.Contains(term)
                || item.EstateManagedAsset.AssetCode.Contains(term)
                || item.EstateManagedAsset.Name.Contains(term)
                || (item.EstateManagedAsset.Location != null && item.EstateManagedAsset.Location.Contains(term))
                || (item.EstateManagedAsset.Town != null && item.EstateManagedAsset.Town.Contains(term))
                || (item.EstateManagedAsset.District != null && item.EstateManagedAsset.District.Contains(term)));
        }

        query = ApplyConfiguredFilters(query, SaleableSourceFilterSettings.Parse(source.SettingsJson));
        var records = await query
            .OrderBy(item => item.EstateManagedAsset.AssetCode)
            .ThenBy(item => item.DemarcationNumber)
            .Take(normalizedTake)
            .ToListAsync();

        return records
            .Select(item => Map(source, item))
            .ToArray();
    }

    private static IQueryable<EstateLandDemarcation> ApplyConfiguredFilters(
        IQueryable<EstateLandDemarcation> query,
        IReadOnlyCollection<SaleableSourceFilterValue> filters)
    {
        foreach (var filter in filters)
        {
            var value = filter.Value.Trim();
            switch (SaleableSourceFilterSettings.Normalize(filter.Field))
            {
                case "ispublishedtoexternalportal" when bool.TryParse(value, out var published):
                    query = query.Where(item => item.IsPublishedToExternalPortal == published);
                    break;
                case "externallistingstatus":
                case "status":
                    query = query.Where(item => item.ExternalListingStatus == value);
                    break;
                case "externallistingtype":
                case "listingtype":
                case "plottype":
                    query = query.Where(item => item.ExternalListingType == value);
                    break;
                case "location":
                    query = query.Where(item => item.EstateManagedAsset.Location != null
                        && item.EstateManagedAsset.Location.Contains(value));
                    break;
                case "region":
                    query = query.Where(item => item.EstateManagedAsset.Region != null
                        && item.EstateManagedAsset.Region.Contains(value));
                    break;
                case "district":
                    query = query.Where(item => item.EstateManagedAsset.District != null
                        && item.EstateManagedAsset.District.Contains(value));
                    break;
                case "town":
                    query = query.Where(item => item.EstateManagedAsset.Town != null
                        && item.EstateManagedAsset.Town.Contains(value));
                    break;
                case "boundaryverified" when bool.TryParse(value, out var boundaryVerified):
                    query = query.Where(item => item.BoundaryVerified == boundaryVerified);
                    break;
                case "currency":
                case "externallistingcurrency":
                    query = query.Where(item => item.ExternalListingCurrency == value);
                    break;
                case "parentlandassetreference":
                    query = query.Where(item => item.ParentLandAssetReference != null
                        && item.ParentLandAssetReference.Contains(value));
                    break;
            }
        }

        return query;
    }

    private static SalesSaleableItemDto Map(
        SalesSaleableSource source,
        EstateLandDemarcation item)
    {
        var asset = item.EstateManagedAsset;
        var reference = EstateLandDemarcationReference.Build(asset.AssetCode, item.DemarcationNumber);
        var listingType = item.ExternalListingType;
        var supportsSale = MatchesListingType(listingType, "Sale", "SaleAndRent", "SaleAndLease");
        var supportsAgreement = MatchesListingType(listingType, "Rent", "Lease", "SaleAndRent", "SaleAndLease");
        var estimatedValue = listingType.Equals("Rent", StringComparison.OrdinalIgnoreCase)
            ? item.ExternalMonthlyRent
            : listingType.Equals("Lease", StringComparison.OrdinalIgnoreCase)
                ? ResolveLeaseAmount(item)
                : item.ExternalSalePrice ?? item.ExternalListingPrice ?? item.TargetSalePrice;

        return new SalesSaleableItemDto
        {
            SourceId = source.Id,
            SourceCode = source.Code,
            SourceType = source.SourceType,
            AdapterKey = source.AdapterKey,
            SourceItemId = item.Id.ToString(),
            ItemCode = reference,
            ItemName = $"{asset.Name} - Parcel {item.DemarcationNumber:000}",
            ItemType = item.ExternalListingType,
            Status = item.ExternalListingStatus,
            CommercialStatus = "Available",
            EstimatedValue = estimatedValue,
            Currency = string.IsNullOrWhiteSpace(item.ExternalListingCurrency)
                ? source.DefaultCurrency
                : item.ExternalListingCurrency,
            AreaSquareMeters = item.AreaSquareFeet / 10.7639104167m,
            PropertyReference = reference,
            CanCreateSalesOrder = source.AllowSalesOrders && supportsSale,
            CanCreateSalesAgreement = source.AllowSalesAgreements && supportsSale,
            CanCreateLeaseAgreement = source.AllowSalesAgreements && supportsAgreement,
            SuggestedOrderType = "PropertySale",
            SuggestedAgreementType = "General",
            SuggestedLeaseAgreementType = "TenancyAgreement",
            ProjectId = asset.ProjectId,
            ProjectCode = asset.ProjectCode,
            ProjectTitle = asset.ProjectTitle,
            LocationName = asset.Location,
            UnitOfMeasure = "Plot",
            CurrentQuantity = 1,
            AvailableQuantity = 1,
            AllocatedQuantity = 0,
            ShouldCreateSalesAllocation = true
        };
    }

    private static bool MatchesListingType(string listingType, params string[] expectedTypes) =>
        expectedTypes.Any(expected => listingType.Equals(expected, StringComparison.OrdinalIgnoreCase));

    private static decimal? ResolveLeaseAmount(EstateLandDemarcation item)
        => item.ExternalMonthlyRent.HasValue
            && item.ExternalListingPrice == item.ExternalMonthlyRent
            && item.TargetSalePrice is > 0m
                ? item.TargetSalePrice
                : item.ExternalListingPrice ?? item.TargetSalePrice;
}
