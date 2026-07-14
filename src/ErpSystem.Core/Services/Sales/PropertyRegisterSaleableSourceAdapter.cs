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

    public async Task<IReadOnlyCollection<SalesSaleableItemDto>> SearchItemsAsync(
        SalesSaleableSource source,
        string? search = null,
        int take = 50)
    {
        var assets = await _managedAssetService.GetManagedAssetsAsync(new EstateManagedAssetQuery
        {
            Search = search,
            Take = take
        });

        return assets
            .Where(asset => asset.Status != EstateManagedAssetStatus.Sold
                && asset.Status != EstateManagedAssetStatus.Retired)
            .Where(asset => (source.AllowSalesOrders && asset.IsAvailableForSale)
                || (source.AllowSalesAgreements && asset.IsAvailableForLease))
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
                CanCreateSalesAgreement = source.AllowSalesAgreements,
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
}
