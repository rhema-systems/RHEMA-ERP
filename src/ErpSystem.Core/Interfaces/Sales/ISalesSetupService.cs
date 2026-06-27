using ErpSystem.Core.DTOs.Sales;
using ErpSystem.Core.Entities.Sales;

namespace ErpSystem.Core.Interfaces.Sales;

public interface ISalesSetupService
{
    Task<IReadOnlyCollection<SalesSaleableSourceDto>> GetSaleableSourcesAsync(bool includeInactive = false);
    Task<IReadOnlyCollection<SalesSaleableItemDto>> SearchSaleableItemsAsync(Guid sourceId, string? search = null, int take = 50);
    Task<SalesSaleableSourceDto> CreateSaleableSourceAsync(UpsertSalesSaleableSourceDto dto);
    Task<SalesSaleableSourceDto> UpdateSaleableSourceAsync(Guid id, UpsertSalesSaleableSourceDto dto);
    Task<SalesSaleableSourceDto> SetSaleableSourceActiveStateAsync(Guid id, bool isActive);
    Task DeleteSaleableSourceAsync(Guid id);
    Task SeedDefaultSaleableSourcesAsync();
}

public interface ISalesSaleableSourceAdapter
{
    string AdapterKey { get; }
    Task<IReadOnlyCollection<SalesSaleableItemDto>> SearchItemsAsync(SalesSaleableSource source, string? search = null, int take = 50);
}
