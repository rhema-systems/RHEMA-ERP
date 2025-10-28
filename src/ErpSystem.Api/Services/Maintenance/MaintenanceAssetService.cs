using AutoMapper;
using Microsoft.Extensions.Logging;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Maintenance;
using ErpSystem.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Maintenance;

public class MaintenanceAssetService : IMaintenanceAssetService
{
    private readonly IMaintenanceAssetRepository _assetRepository;
    private readonly IMaintenanceAssetCategoryRepository _categoryRepository;
    private readonly IMapper _mapper;
    private readonly ILogger<MaintenanceAssetService> _logger;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;

    public MaintenanceAssetService(
        IMaintenanceAssetRepository assetRepository,
        IMaintenanceAssetCategoryRepository categoryRepository,
        IMapper mapper,
        ILogger<MaintenanceAssetService> logger,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork)
    {
        _assetRepository = assetRepository;
        _categoryRepository = categoryRepository;
        _mapper = mapper;
        _logger = logger;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
    }

    public async Task<MaintenanceAssetDto> CreateAssetAsync(CreateMaintenanceAssetDto createDto)
    {
        try
        {
            _logger.LogInformation("Creating new maintenance asset: {AssetName}", createDto.Name);

            // Validate category exists
            var category = await _categoryRepository.GetByIdAsync(createDto.AssetCategoryId);
            if (category == null)
            {
                throw new ArgumentException($"Asset category with ID {createDto.AssetCategoryId} not found");
            }

            // Generate asset number if not provided
            if (string.IsNullOrEmpty(createDto.AssetNumber))
            {
                createDto.AssetNumber = await GenerateAssetNumberAsync(createDto.AssetCategoryId);
            }
            else
            {
                // Validate uniqueness
                if (!await IsAssetNumberUniqueAsync(createDto.AssetNumber))
                {
                    throw new ArgumentException($"Asset number '{createDto.AssetNumber}' already exists");
                }
            }

            // Validate parent asset if specified
            if (createDto.ParentAssetId.HasValue)
            {
                var parentAsset = await _assetRepository.GetByIdAsync(createDto.ParentAssetId.Value);
                if (parentAsset == null)
                {
                    throw new ArgumentException($"Parent asset with ID {createDto.ParentAssetId} not found");
                }
            }

            var asset = _mapper.Map<MaintenanceAsset>(createDto);
            asset.TenantId = _currentUserService.TenantId ?? throw new InvalidOperationException("Tenant ID is required");
            
            // Debug logging
            _logger.LogInformation("Creating asset with AssetCategoryId: {CategoryId}", asset.AssetCategoryId);

            var createdAsset = await _assetRepository.AddAsync(asset);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Successfully created maintenance asset with ID: {AssetId}", createdAsset.Id);

            return _mapper.Map<MaintenanceAssetDto>(createdAsset);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating maintenance asset: {AssetName}", createDto.Name);
            throw;
        }
    }

    public async Task<MaintenanceAssetDto> UpdateAssetAsync(Guid id, UpdateMaintenanceAssetDto updateDto)
    {
        try
        {
            _logger.LogInformation("Updating maintenance asset: {AssetId}", id);

            var existingAsset = await _assetRepository.GetByIdAsync(id);
            if (existingAsset == null)
            {
                throw new ArgumentException($"Asset with ID {id} not found");
            }

            // Validate category exists
            var category = await _categoryRepository.GetByIdAsync(updateDto.AssetCategoryId);
            if (category == null)
            {
                throw new ArgumentException($"Asset category with ID {updateDto.AssetCategoryId} not found");
            }

            // Validate parent asset if specified
            if (updateDto.ParentAssetId.HasValue)
            {
                var parentAsset = await _assetRepository.GetByIdAsync(updateDto.ParentAssetId.Value);
                if (parentAsset == null)
                {
                    throw new ArgumentException($"Parent asset with ID {updateDto.ParentAssetId} not found");
                }

                // Ensure no circular reference
                if (await WouldCreateCircularReference(id, updateDto.ParentAssetId.Value))
                {
                    throw new ArgumentException("Cannot set parent asset as it would create a circular reference");
                }
            }

            _mapper.Map(updateDto, existingAsset);
            await _assetRepository.UpdateAsync(existingAsset);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Successfully updated maintenance asset: {AssetId}", id);

            return _mapper.Map<MaintenanceAssetDto>(existingAsset);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating maintenance asset: {AssetId}", id);
            throw;
        }
    }

    public async Task DeleteAssetAsync(Guid id)
    {
        try
        {
            _logger.LogInformation("Deleting maintenance asset: {AssetId}", id);

            var asset = await _assetRepository.GetByIdAsync(id);
            if (asset == null)
            {
                throw new ArgumentException($"Asset with ID {id} not found");
            }

            // Check for child assets
            var childAssets = await _assetRepository.GetByParentAssetIdAsync(id);
            if (childAssets.Any())
            {
                throw new InvalidOperationException("Cannot delete asset that has child assets");
            }

            // Check for active work orders
            var activeWorkOrders = await _assetRepository.GetAssetsWithActiveWorkOrdersAsync();
            if (activeWorkOrders.Any(a => a.Id == id))
            {
                throw new InvalidOperationException("Cannot delete asset that has active work orders");
            }

            await _assetRepository.DeleteAsync(id);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Successfully deleted maintenance asset: {AssetId}", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting maintenance asset: {AssetId}", id);
            throw;
        }
    }

    public async Task<MaintenanceAssetDto?> GetAssetByIdAsync(Guid id)
    {
        try
        {
            var asset = await _assetRepository.GetByIdAsync(id, 
                a => a.AssetCategory, 
                a => a.ParentAsset, 
                a => a.ChildAssets);
                
            return asset != null ? _mapper.Map<MaintenanceAssetDto>(asset) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving maintenance asset: {AssetId}", id);
            throw;
        }
    }

    public async Task<IEnumerable<MaintenanceAssetDto>> GetAllAssetsAsync()
    {
        try
        {
            var assets = await _assetRepository.GetAllAsync(a => a.AssetCategory);
            return _mapper.Map<IEnumerable<MaintenanceAssetDto>>(assets);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all maintenance assets");
            throw;
        }
    }

    public async Task<PagedResult<MaintenanceAssetListDto>> GetAssetsPagedAsync(int page, int pageSize, string? searchTerm = null, Guid? categoryId = null)
    {
        try
        {
            var query = _assetRepository.GetQueryable()
                .Include(a => a.AssetCategory)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(a => 
                    a.Name.Contains(searchTerm) ||
                    a.AssetNumber.Contains(searchTerm) ||
                    (a.Description != null && a.Description.Contains(searchTerm)) ||
                    (a.SerialNumber != null && a.SerialNumber.Contains(searchTerm)));
            }

            if (categoryId.HasValue)
            {
                query = query.Where(a => a.AssetCategoryId == categoryId.Value);
            }

            var totalCount = await query.CountAsync();
            var assets = await query
                .OrderBy(a => a.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var assetListDtos = assets.Select(a => new MaintenanceAssetListDto
            {
                Id = a.Id,
                Name = a.Name,
                AssetNumber = a.AssetNumber,
                Description = a.Description,
                AssetCategoryId = a.AssetCategoryId,
                CategoryName = a.AssetCategory.Name,
                AssetType = a.AssetCategory.AssetType,
                Manufacturer = a.Manufacturer,
                Model = a.Model,
                Status = a.Status.ToString(),
                Criticality = a.Criticality.ToString(),
                Location = a.Location,
                CurrentValue = a.CurrentValue,
                SerialNumber = a.SerialNumber,
                PurchaseDate = a.PurchaseDate,
                WarrantyEndDate = a.WarrantyEndDate,
                WarrantyStartDate = a.WarrantyStartDate,
                // These would need additional queries or joins
                ActiveWorkOrdersCount = 0, // TODO: Implement
                LastMaintenanceDate = null, // TODO: Implement
                NextMaintenanceDate = null // TODO: Implement
            });

            return new PagedResult<MaintenanceAssetListDto>
            {
                Items = assetListDtos,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paged maintenance assets");
            throw;
        }
    }

    public async Task<bool> IsAssetNumberUniqueAsync(string assetNumber, Guid? excludeId = null)
    {
        try
        {
            return await _assetRepository.IsAssetNumberUniqueAsync(assetNumber, excludeId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking asset number uniqueness: {AssetNumber}", assetNumber);
            throw;
        }
    }

    public async Task<IEnumerable<MaintenanceAssetDto>> GetAssetsByStatusAsync(string status)
    {
        try
        {
            var assets = await _assetRepository.GetByStatusAsync(status);
            return _mapper.Map<IEnumerable<MaintenanceAssetDto>>(assets);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving assets by status: {Status}", status);
            throw;
        }
    }

    public async Task<IEnumerable<MaintenanceAssetDto>> GetAssetsByCategoryAsync(Guid categoryId)
    {
        try
        {
            var assets = await _assetRepository.GetByAssetCategoryIdAsync(categoryId);
            return _mapper.Map<IEnumerable<MaintenanceAssetDto>>(assets);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving assets by category: {CategoryId}", categoryId);
            throw;
        }
    }

    public async Task<IEnumerable<MaintenanceAssetDto>> GetAssetsWithActiveWorkOrdersAsync()
    {
        try
        {
            var assets = await _assetRepository.GetAssetsWithActiveWorkOrdersAsync();
            return _mapper.Map<IEnumerable<MaintenanceAssetDto>>(assets);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving assets with active work orders");
            throw;
        }
    }

    public async Task<IEnumerable<MaintenanceAssetDto>> GetAssetsRequiringMaintenanceAsync()
    {
        try
        {
            var assets = await _assetRepository.GetAssetsRequiringMaintenanceAsync();
            return _mapper.Map<IEnumerable<MaintenanceAssetDto>>(assets);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving assets requiring maintenance");
            throw;
        }
    }

    public async Task<IEnumerable<MaintenanceAssetDto>> GetAssetHierarchyAsync(Guid rootAssetId)
    {
        try
        {
            var assets = await _assetRepository.GetAssetHierarchyAsync(rootAssetId);
            return _mapper.Map<IEnumerable<MaintenanceAssetDto>>(assets);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving asset hierarchy: {RootAssetId}", rootAssetId);
            throw;
        }
    }

    public async Task<AssetMetricsDto> GetAssetMetricsAsync()
    {
        try
        {
            var allAssets = await _assetRepository.GetAllAsync(a => a.AssetCategory);
            var totalValue = await _assetRepository.GetTotalAssetValueAsync();

            var metrics = new AssetMetricsDto
            {
                TotalAssets = allAssets.Count(),
                ActiveAssets = allAssets.Count(a => a.Status == AssetStatus.Active),
                MaintenanceAssets = allAssets.Count(a => a.Status == AssetStatus.Maintenance),
                RetiredAssets = allAssets.Count(a => a.Status == AssetStatus.Retired),
                TotalValue = (decimal)totalValue,
                AverageValue = allAssets.Any() ? (decimal)totalValue / allAssets.Count() : 0,
                AssetsByCategory = allAssets.GroupBy(a => a.AssetCategory.Name)
                    .ToDictionary(g => g.Key, g => g.Count()),
                AssetsByStatus = allAssets.GroupBy(a => a.Status.ToString())
                    .ToDictionary(g => g.Key, g => g.Count()),
                AssetsByCriticality = allAssets.GroupBy(a => a.Criticality.ToString())
                    .ToDictionary(g => g.Key, g => g.Count())
            };

            return metrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving asset metrics");
            throw;
        }
    }

    public async Task UpdateAssetOperatingHoursAsync(Guid assetId, double operatingHours)
    {
        try
        {
            _logger.LogInformation("Updating operating hours for asset: {AssetId}", assetId);

            var asset = await _assetRepository.GetByIdAsync(assetId);
            if (asset == null)
            {
                throw new ArgumentException($"Asset with ID {assetId} not found");
            }

            asset.OperatingHours = operatingHours;
            asset.LastOperatingHoursUpdate = DateTime.UtcNow;

            await _assetRepository.UpdateAsync(asset);

            _logger.LogInformation("Successfully updated operating hours for asset: {AssetId}", assetId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating operating hours for asset: {AssetId}", assetId);
            throw;
        }
    }

    public async Task UpdateAssetMileageAsync(Guid assetId, double mileage)
    {
        try
        {
            _logger.LogInformation("Updating mileage for asset: {AssetId}", assetId);

            var asset = await _assetRepository.GetByIdAsync(assetId);
            if (asset == null)
            {
                throw new ArgumentException($"Asset with ID {assetId} not found");
            }

            asset.Mileage = mileage;
            asset.LastMileageUpdate = DateTime.UtcNow;

            await _assetRepository.UpdateAsync(asset);

            _logger.LogInformation("Successfully updated mileage for asset: {AssetId}", assetId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating mileage for asset: {AssetId}", assetId);
            throw;
        }
    }

    public async Task<string> GenerateAssetNumberAsync(Guid categoryId)
    {
        try
        {
            var category = await _categoryRepository.GetByIdAsync(categoryId);
            var categoryPrefix = category?.Code ?? "AST";

            var currentYear = DateTime.UtcNow.Year;
            var prefix = $"{categoryPrefix}{currentYear}-";

            var lastAsset = await _assetRepository.GetQueryable()
                .Where(a => a.AssetNumber.StartsWith(prefix))
                .OrderByDescending(a => a.AssetNumber)
                .FirstOrDefaultAsync();

            if (lastAsset != null)
            {
                var numberPart = lastAsset.AssetNumber.Substring(prefix.Length);
                if (int.TryParse(numberPart, out int lastNumber))
                {
                    return $"{prefix}{(lastNumber + 1):D4}";
                }
            }

            return $"{prefix}0001";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating asset number for category: {CategoryId}", categoryId);
            throw;
        }
    }

    private async Task<bool> WouldCreateCircularReference(Guid assetId, Guid parentAssetId)
    {
        var currentId = parentAssetId;
        while (currentId != Guid.Empty)
        {
            if (currentId == assetId)
                return true;

            var parent = await _assetRepository.GetByIdAsync(currentId);
            currentId = parent?.ParentAssetId ?? Guid.Empty;
        }

        return false;
    }
    
}