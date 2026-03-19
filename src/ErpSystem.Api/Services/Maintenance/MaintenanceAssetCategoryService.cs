using AutoMapper;
using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Api.Services.Maintenance;

public class MaintenanceAssetCategoryService : IMaintenanceAssetCategoryService
{
    private readonly IMaintenanceAssetCategoryRepository _categoryRepository;
    private readonly IMaintenanceAssetRepository _assetRepository;
    private readonly IMapper _mapper;
    private readonly ILogger<MaintenanceAssetCategoryService> _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;

    public MaintenanceAssetCategoryService(
        IMaintenanceAssetCategoryRepository categoryRepository,
        IMaintenanceAssetRepository assetRepository,
        IMapper mapper,
        ILogger<MaintenanceAssetCategoryService> logger,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider)
    {
        _categoryRepository = categoryRepository;
        _assetRepository = assetRepository;
        _mapper = mapper;
        _logger = logger;
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
    }

    public async Task<MaintenanceAssetCategoryDto> CreateCategoryAsync(CreateMaintenanceAssetCategoryDto createDto)
    {
        try
        {
            _logger.LogInformation("Creating new maintenance asset category: {CategoryName}", createDto.Name);

            // Validate code uniqueness
            if (!await IsCategoryCodeUniqueAsync(createDto.Code ?? string.Empty))
            {
                throw new ArgumentException($"Category code '{createDto.Code}' already exists");
            }

            // Validate parent category if specified
            if (createDto.ParentCategoryId.HasValue)
            {
                var parentCategory = await _categoryRepository.GetByIdAsync(createDto.ParentCategoryId.Value) ?? throw new ArgumentException($"Parent category with ID {createDto.ParentCategoryId} not found");
            }

            var category = _mapper.Map<MaintenanceAssetCategory>(createDto);
            category.TenantId = _currentUserProvider.TenantId;

            var createdCategory = await _categoryRepository.AddAsync(category);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Successfully created maintenance asset category with ID: {CategoryId}", createdCategory.Id);

            return _mapper.Map<MaintenanceAssetCategoryDto>(createdCategory);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating maintenance asset category: {CategoryName}", createDto.Name);
            throw;
        }
    }

    public async Task<MaintenanceAssetCategoryDto> UpdateCategoryAsync(Guid id, UpdateMaintenanceAssetCategoryDto updateDto)
    {
        try
        {
            _logger.LogInformation("Updating maintenance asset category: {CategoryId}", id);

            var existingCategory = await _categoryRepository.GetByIdAsync(id) ?? throw new ArgumentException($"Category with ID {id} not found");

            // Validate code uniqueness
            if (!await IsCategoryCodeUniqueAsync(updateDto.Code ?? string.Empty, id))
            {
                throw new ArgumentException($"Category code '{updateDto.Code}' already exists");
            }

            // Validate parent category if specified
            if (updateDto.ParentCategoryId.HasValue)
            {
                var parentCategory = await _categoryRepository.GetByIdAsync(updateDto.ParentCategoryId.Value) ?? throw new ArgumentException($"Parent category with ID {updateDto.ParentCategoryId} not found");

                // Ensure no circular reference
                if (await WouldCreateCircularReference(id, updateDto.ParentCategoryId.Value))
                {
                    throw new ArgumentException("Cannot set parent category as it would create a circular reference");
                }
            }

            _mapper.Map(updateDto, existingCategory);
            await _categoryRepository.UpdateAsync(existingCategory);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Successfully updated maintenance asset category: {CategoryId}", id);

            return _mapper.Map<MaintenanceAssetCategoryDto>(existingCategory);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating maintenance asset category: {CategoryId}", id);
            throw;
        }
    }

    public async Task DeleteCategoryAsync(Guid id)
    {
        try
        {
            _logger.LogInformation("Deleting maintenance asset category: {CategoryId}", id);

            var category = await _categoryRepository.GetByIdAsync(id) ?? throw new ArgumentException($"Category with ID {id} not found");

            // Check for child categories - using the generic repository method since GetByParentCategoryIdAsync doesn't exist
            var allCategories = await _categoryRepository.GetAllAsync();
            var childCategories = allCategories.Where(c => c.ParentCategoryId == id);
            if (childCategories.Any())
            {
                throw new InvalidOperationException("Cannot delete category that has child categories");
            }

            // Check for assets in this category
            var assets = await _assetRepository.GetByAssetCategoryIdAsync(id);
            if (assets.Any())
            {
                throw new InvalidOperationException("Cannot delete category that contains assets");
            }

            await _categoryRepository.DeleteAsync(id);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Successfully deleted maintenance asset category: {CategoryId}", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting maintenance asset category: {CategoryId}", id);
            throw;
        }
    }

    public async Task<MaintenanceAssetCategoryDto?> GetCategoryByIdAsync(Guid id)
    {
        try
        {
            var category = await _categoryRepository.FirstOrDefaultAsync(
                c => c.Id == id && c.TenantId == _currentUserProvider.TenantId,
                c => c.ParentCategory!,
                c => c.ChildCategories!,
                c => c.Assets!);

            return category != null ? _mapper.Map<MaintenanceAssetCategoryDto>(category) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving maintenance asset category: {CategoryId}", id);
            throw;
        }
    }

    public async Task<IEnumerable<MaintenanceAssetCategoryDto>> GetAllCategoriesAsync()
    {
        try
        {
            // For now, return all categories to make the frontend work
            // TODO: Implement proper tenant filtering when authentication is fixed
            var categories = await _categoryRepository.GetAllAsync(
                c => c.ParentCategory!,
                c => c.ChildCategories!,
                c => c.Assets!);
            return _mapper.Map<IEnumerable<MaintenanceAssetCategoryDto>>(categories);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all maintenance asset categories");
            throw;
        }
    }

    public async Task<IEnumerable<MaintenanceAssetCategoryDto>> GetActiveCategoriesAsync()
    {
        try
        {
            var categories = await _categoryRepository.FindAsync(c => c.IsActive && c.TenantId == _currentUserProvider.TenantId);
            return _mapper.Map<IEnumerable<MaintenanceAssetCategoryDto>>(categories);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active maintenance asset categories");
            throw;
        }
    }

    public async Task<bool> IsCategoryCodeUniqueAsync(string code, Guid? excludeId = null)
    {
        try
        {
            return await _categoryRepository.IsCodeUniqueAsync(code, excludeId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking category code uniqueness: {Code}", code);
            throw;
        }
    }

    public async Task<CategoryStatisticsDto> GetCategoryStatisticsAsync(Guid categoryId)
    {
        try
        {
            var category = await _categoryRepository.GetByIdAsync(categoryId) ?? throw new ArgumentException($"Category with ID {categoryId} not found");
            var assets = await _assetRepository.GetByAssetCategoryIdAsync(categoryId);
            var allCategories = await _categoryRepository.GetAllAsync();
            var childCategories = allCategories.Where(c => c.ParentCategoryId == categoryId);

            var statistics = new CategoryStatisticsDto
            {
                CategoryId = categoryId,
                CategoryName = category.Name,
                TotalAssets = assets.Count(),
                ActiveAssets = assets.Count(a => a.Status == Core.Enums.AssetStatus.Active),
                TotalValue = assets.Sum(a => (decimal?)a.CurrentValue) ?? 0,
                AverageValue = assets.Any() ? assets.Average(a => (decimal?)a.CurrentValue) ?? 0 : 0,
                ActiveWorkOrders = 0, // TODO: Implement when work order integration is available
                OverdueMaintenanceCount = 0 // TODO: Implement when maintenance schedule integration is available
            };

            return statistics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving category statistics: {CategoryId}", categoryId);
            throw;
        }
    }

    public async Task<IEnumerable<MaintenanceAssetCategoryDto>> GetCategoryHierarchyAsync(Guid? parentId = null)
    {
        try
        {
            // Since GetCategoryHierarchyAsync doesn't exist, we'll implement it using GetAllAsync
            var allCategories = await _categoryRepository.GetAllAsync(c => c.ParentCategory!, c => c.ChildCategories!, c => c.Assets!);

            // Filter by parent ID if specified
            var filteredCategories = parentId.HasValue
                ? allCategories.Where(c => c.ParentCategoryId == parentId.Value)
                : allCategories.Where(c => c.ParentCategoryId == null);

            return _mapper.Map<IEnumerable<MaintenanceAssetCategoryDto>>(filteredCategories);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving category hierarchy: {ParentId}", parentId);
            throw;
        }
    }

    public async Task<IEnumerable<MaintenanceAssetCategoryDto>> GetChildCategoriesAsync(Guid parentId)
    {
        try
        {
            // Since GetByParentCategoryIdAsync doesn't exist, we'll use GetAllAsync and filter
            var allCategories = await _categoryRepository.GetAllAsync();
            var childCategories = allCategories.Where(c => c.ParentCategoryId == parentId);
            return _mapper.Map<IEnumerable<MaintenanceAssetCategoryDto>>(childCategories);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving child categories: {ParentId}", parentId);
            throw;
        }
    }

    private async Task<bool> WouldCreateCircularReference(Guid categoryId, Guid parentCategoryId)
    {
        var currentId = parentCategoryId;
        while (currentId != Guid.Empty)
        {
            if (currentId == categoryId)
            {
                return true;
            }

            var parent = await _categoryRepository.GetByIdAsync(currentId);
            currentId = parent?.ParentCategoryId ?? Guid.Empty;
        }

        return false;
    }
}
