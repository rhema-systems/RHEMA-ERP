using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Procurement;

public class PartnerCategoryService : IPartnerCategoryService
{
    private readonly IPartnerCategoryRepository _repository;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<PartnerCategoryService> _logger;

    public PartnerCategoryService(
        IPartnerCategoryRepository repository,
        ICurrentUserProvider currentUserProvider,
        ILogger<PartnerCategoryService> logger)
    {
        _repository = repository;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    public async Task<PartnerCategoryDto?> GetByIdAsync(Guid id)
    {
        var category = await _repository.GetByIdAsync(id);
        return category == null ? null : MapToDto(category);
    }

    public async Task<IEnumerable<PartnerCategoryDto>> GetAllAsync()
    {
        var categories = await _repository.GetAllAsync();
        return categories.Select(MapToDto);
    }

    public async Task<IEnumerable<PartnerCategoryDto>> GetAllCategoriesAsync(string? categoryType = null)
    {
        var categories = await _repository.GetAllCategoriesAsync(categoryType);
        return categories.Select(MapToDto);
    }

    public async Task<PartnerCategoryDto?> GetWithSubCategoriesAsync(Guid id)
    {
        var category = await _repository.GetWithSubCategoriesAsync(id);
        return category == null ? null : MapToDtoWithSubCategories(category);
    }

    public async Task<PartnerCategoryDto> CreateAsync(CreatePartnerCategoryDto dto)
    {
        // Validate unique code
        var isUnique = await IsCategoryCodeUniqueAsync(dto.CategoryCode);
        if (!isUnique)
        {
            throw new InvalidOperationException($"Category code '{dto.CategoryCode}' already exists.");
        }

        var category = new PartnerCategory
        {
            CategoryCode = dto.CategoryCode,
            CategoryName = dto.CategoryName,
            Description = dto.Description,
            CategoryType = dto.CategoryType,
            ParentCategoryId = dto.ParentCategoryId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            TenantId = _currentUserProvider.TenantId
        };

        var created = await _repository.CreateAsync(category);
        await _repository.SaveChangesAsync();
        _logger.LogInformation("Created partner category {CategoryCode} - {CategoryName}", created.CategoryCode, created.CategoryName);

        return MapToDto(created);
    }

    public async Task<PartnerCategoryDto> UpdateAsync(Guid id, UpdatePartnerCategoryDto dto)
    {
        var category = await _repository.GetByIdAsync(id) ?? throw new InvalidOperationException($"Partner category with ID {id} not found.");
        category.CategoryName = dto.CategoryName;
        category.Description = dto.Description;
        category.IsActive = dto.IsActive;
        category.UpdatedAt = DateTime.UtcNow;

        var updated = await _repository.UpdateAsync(category);
        await _repository.SaveChangesAsync();
        _logger.LogInformation("Updated partner category {CategoryCode} - {CategoryName}", updated.CategoryCode, updated.CategoryName);

        return MapToDto(updated);
    }

    public async Task DeleteAsync(Guid id)
    {
        // Check if category has sub-categories
        var hasSubCategories = await HasSubCategoriesAsync(id);
        if (hasSubCategories)
        {
            throw new InvalidOperationException("Cannot delete category that has sub-categories.");
        }

        await _repository.DeleteAsync(id);
        await _repository.SaveChangesAsync();
        _logger.LogInformation("Deleted partner category with ID {CategoryId}", id);
    }

    public async Task<IEnumerable<PartnerCategoryDto>> GetActiveCategoriesAsync(string? categoryType = null)
    {
        var categories = await _repository.GetActiveCategoriesAsync(categoryType);
        return categories.Select(MapToDto);
    }

    public async Task<IEnumerable<PartnerCategoryDto>> GetRootCategoriesAsync(string? categoryType = null)
    {
        var categories = await _repository.GetRootCategoriesAsync(categoryType);
        return categories.Select(MapToDto);
    }

    public async Task<IEnumerable<PartnerCategoryDto>> GetSubCategoriesAsync(Guid parentCategoryId)
    {
        var categories = await _repository.GetSubCategoriesAsync(parentCategoryId);
        return categories.Select(MapToDto);
    }

    public async Task<PartnerCategoryDto?> GetByCodeAsync(string categoryCode)
    {
        var category = await _repository.GetByCodeAsync(categoryCode);
        return category == null ? null : MapToDto(category);
    }

    public async Task<bool> IsCategoryCodeUniqueAsync(string categoryCode, Guid? excludeId = null)
    {
        return await _repository.IsCategoryCodeUniqueAsync(categoryCode, excludeId);
    }

    public async Task<bool> HasSubCategoriesAsync(Guid categoryId)
    {
        return await _repository.HasSubCategoriesAsync(categoryId);
    }

    private static PartnerCategoryDto MapToDto(PartnerCategory category)
    {
        return new PartnerCategoryDto
        {
            Id = category.Id,
            CategoryCode = category.CategoryCode,
            CategoryName = category.CategoryName,
            Description = category.Description,
            CategoryType = category.CategoryType,
            ParentCategoryId = category.ParentCategoryId,
            ParentCategoryName = category.ParentCategory?.CategoryName,
            IsActive = category.IsActive,
            DisplayOrder = 0, // Default value since entity doesn't have this property
            SubCategories = new List<PartnerCategoryDto>()
        };
    }

    private static PartnerCategoryDto MapToDtoWithSubCategories(PartnerCategory category)
    {
        var dto = MapToDto(category);
        dto.SubCategories = category.SubCategories?.Select(MapToDto).ToList() ?? new List<PartnerCategoryDto>();
        return dto;
    }
}
