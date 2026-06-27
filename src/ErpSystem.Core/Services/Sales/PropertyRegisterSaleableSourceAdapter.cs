using ErpSystem.Core.DTOs.Sales;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Interfaces.Sales;

namespace ErpSystem.Core.Services.Sales;

public class PropertyRegisterSaleableSourceAdapter : ISalesSaleableSourceAdapter
{
    public string AdapterKey => "property-register";

    public Task<IReadOnlyCollection<SalesSaleableItemDto>> SearchItemsAsync(
        SalesSaleableSource source,
        string? search = null,
        int take = 50)
    {
        // The standalone property/land register is not present in this master repo yet.
        // Project Units and future Land Management plots are handled by their own adapters.
        return Task.FromResult<IReadOnlyCollection<SalesSaleableItemDto>>([]);
    }
}
