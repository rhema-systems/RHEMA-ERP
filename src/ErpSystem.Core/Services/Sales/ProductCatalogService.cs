using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Sales;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Sales;

public class ProductCatalogService : IProductCatalogService
{
    private readonly IGenericRepository<Product> _productRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<ProductCatalogService> _logger;

    public ProductCatalogService(
        IGenericRepository<Product> productRepo,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ILogger<ProductCatalogService> logger)
    {
        _productRepo = productRepo;
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    #region CRUD

    public async Task<ProductDetailDto> CreateAsync(CreateProductDto dto)
    {
        // Check for duplicate product code
        var existing = await _productRepo.GetQueryable()
            .FirstOrDefaultAsync(p => p.ProductCode == dto.ProductCode);
        if (existing != null)
            throw new InvalidOperationException($"Product with code '{dto.ProductCode}' already exists");

        var product = new Product
        {
            ProductCode = dto.ProductCode,
            ProductName = dto.ProductName,
            Description = dto.Description,
            ProductType = dto.ProductType,
            Category = dto.Category,
            Brand = dto.Brand,
            Unit = dto.Unit,
            ListPrice = dto.ListPrice,
            CostPrice = dto.CostPrice,
            TaxCode = dto.TaxCode,
            Vendor = dto.Vendor,
            Specifications = dto.Specifications,
            IsActive = true,
            TenantId = _currentUserProvider.TenantId
        };

        await _productRepo.AddAsync(product);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Created Product {ProductCode} — {ProductName}", product.ProductCode, product.ProductName);
        return await GetByIdAsync(product.Id) ?? throw new InvalidOperationException("Failed to retrieve created Product");
    }

    public async Task<ProductDetailDto> UpdateAsync(Guid id, UpdateProductDto dto)
    {
        var product = await _productRepo.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"Product {id} not found");

        if (dto.ProductName != null) product.ProductName = dto.ProductName;
        if (dto.Description != null) product.Description = dto.Description;
        if (dto.ProductType != null) product.ProductType = dto.ProductType;
        if (dto.Category != null) product.Category = dto.Category;
        if (dto.Brand != null) product.Brand = dto.Brand;
        if (dto.Unit != null) product.Unit = dto.Unit;
        if (dto.ListPrice.HasValue) product.ListPrice = dto.ListPrice.Value;
        if (dto.CostPrice.HasValue) product.CostPrice = dto.CostPrice.Value;
        if (dto.IsActive.HasValue) product.IsActive = dto.IsActive.Value;
        if (dto.TaxCode != null) product.TaxCode = dto.TaxCode;
        if (dto.Vendor != null) product.Vendor = dto.Vendor;
        if (dto.Specifications != null) product.Specifications = dto.Specifications;

        await _productRepo.UpdateAsync(product);
        await _unitOfWork.SaveChangesAsync();

        return await GetByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve updated Product");
    }

    public async Task<ProductDetailDto?> GetByIdAsync(Guid id)
    {
        var product = await _productRepo.GetByIdAsync(id);
        return product == null ? null : MapToDetailDto(product);
    }

    public async Task<PagedResult<ProductSummaryDto>> GetAllAsync(
        int page = 1, int pageSize = 20,
        string? search = null, string? category = null, string? productType = null, bool? isActive = null)
    {
        var query = _productRepo.GetQueryable();

        if (!string.IsNullOrEmpty(search))
            query = query.Where(p => p.ProductCode.Contains(search) || p.ProductName.Contains(search) ||
                                     (p.Brand != null && p.Brand.Contains(search)));
        if (!string.IsNullOrEmpty(category))
            query = query.Where(p => p.Category == category);
        if (!string.IsNullOrEmpty(productType))
            query = query.Where(p => p.ProductType == productType);
        if (isActive.HasValue)
            query = query.Where(p => p.IsActive == isActive.Value);

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderBy(p => p.ProductName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<ProductSummaryDto>
        {
            Items = items.Select(MapToSummaryDto).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    #endregion

    #region Queries

    public async Task<List<ProductSummaryDto>> GetByCategoryAsync(string category)
    {
        var items = await _productRepo.GetQueryable()
            .Where(p => p.Category == category && p.IsActive)
            .OrderBy(p => p.ProductName)
            .ToListAsync();
        return items.Select(MapToSummaryDto).ToList();
    }

    public async Task<List<ProductSummaryDto>> SearchAsync(string query, int maxResults = 20)
    {
        var items = await _productRepo.GetQueryable()
            .Where(p => p.IsActive &&
                        (p.ProductCode.Contains(query) || p.ProductName.Contains(query) ||
                         (p.Description != null && p.Description.Contains(query))))
            .OrderBy(p => p.ProductName)
            .Take(maxResults)
            .ToListAsync();
        return items.Select(MapToSummaryDto).ToList();
    }

    #endregion

    #region Lifecycle

    public async Task<ProductDetailDto> ActivateAsync(Guid id)
    {
        var product = await _productRepo.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"Product {id} not found");

        product.IsActive = true;
        await _productRepo.UpdateAsync(product);
        await _unitOfWork.SaveChangesAsync();

        return await GetByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve");
    }

    public async Task<ProductDetailDto> DeactivateAsync(Guid id)
    {
        var product = await _productRepo.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"Product {id} not found");

        product.IsActive = false;
        await _productRepo.UpdateAsync(product);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Product {ProductCode} deactivated", product.ProductCode);
        return await GetByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve");
    }

    #endregion

    #region Mapping

    private static ProductSummaryDto MapToSummaryDto(Product p) => new()
    {
        Id = p.Id,
        ProductCode = p.ProductCode,
        ProductName = p.ProductName,
        ProductType = p.ProductType,
        Category = p.Category,
        Brand = p.Brand,
        ListPrice = p.ListPrice,
        CostPrice = p.CostPrice,
        Margin = p.Margin,
        IsActive = p.IsActive,
        CreatedAt = p.CreatedAt
    };

    private static ProductDetailDto MapToDetailDto(Product p) => new()
    {
        Id = p.Id,
        ProductCode = p.ProductCode,
        ProductName = p.ProductName,
        Description = p.Description,
        ProductType = p.ProductType,
        Category = p.Category,
        Brand = p.Brand,
        Unit = p.Unit,
        ListPrice = p.ListPrice,
        CostPrice = p.CostPrice,
        Margin = p.Margin,
        IsActive = p.IsActive,
        TaxCode = p.TaxCode,
        Vendor = p.Vendor,
        Specifications = p.Specifications,
        CreatedAt = p.CreatedAt
    };

    #endregion
}
