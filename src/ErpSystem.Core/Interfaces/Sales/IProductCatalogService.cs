using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Sales;

namespace ErpSystem.Core.Interfaces.Sales;

/// <summary>
/// Service interface for the Sales Product/Service catalog.
/// Manages products available for quoting, ordering, and pricing.
/// </summary>
public interface IProductCatalogService
{
    // ── CRUD ─────────────────────────────────────────────────────────────

    Task<ProductDetailDto> CreateAsync(CreateProductDto dto);
    Task<ProductDetailDto> UpdateAsync(Guid id, UpdateProductDto dto);
    Task<ProductDetailDto?> GetByIdAsync(Guid id);
    Task<PagedResult<ProductSummaryDto>> GetAllAsync(
        int page = 1, int pageSize = 20,
        string? search = null,
        string? category = null,
        string? productType = null,
        bool? isActive = null);

    // ── Queries ──────────────────────────────────────────────────────────

    Task<List<ProductSummaryDto>> GetByCategoryAsync(string category);
    Task<List<ProductSummaryDto>> SearchAsync(string query, int maxResults = 20);

    // ── Lifecycle ────────────────────────────────────────────────────────

    Task<ProductDetailDto> ActivateAsync(Guid id);
    Task<ProductDetailDto> DeactivateAsync(Guid id);
}
