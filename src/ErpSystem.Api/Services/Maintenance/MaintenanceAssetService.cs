using AutoMapper;
using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Maintenance;
using ErpSystem.Data;
using ErpSystem.Shared;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Api.Services.Maintenance;

public class MaintenanceAssetService : IMaintenanceAssetService
{
    private readonly IMaintenanceAssetRepository _assetRepository;
    private readonly IMaintenanceAssetCategoryRepository _categoryRepository;
    private readonly IMaintenanceScheduleRepository _scheduleRepository;
    private readonly IMaintenanceTypeRepository _maintenanceTypeRepository;
    private readonly IMapper _mapper;
    private readonly ILogger<MaintenanceAssetService> _logger;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ApplicationDbContext _context;
    private readonly IMaintenanceAssetMappingService _assetMappingService;

    public MaintenanceAssetService(
        IMaintenanceAssetRepository assetRepository,
        IMaintenanceAssetCategoryRepository categoryRepository,
        IMaintenanceScheduleRepository scheduleRepository,
        IMaintenanceTypeRepository maintenanceTypeRepository,
        IMapper mapper,
        ILogger<MaintenanceAssetService> logger,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork,
        ApplicationDbContext context,
        IMaintenanceAssetMappingService assetMappingService)
    {
        _assetRepository = assetRepository;
        _categoryRepository = categoryRepository;
        _scheduleRepository = scheduleRepository;
        _maintenanceTypeRepository = maintenanceTypeRepository;
        _mapper = mapper;
        _logger = logger;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
        _context = context;
        _assetMappingService = assetMappingService;
    }

    public Task<IReadOnlyList<JobCardAssetOptionDto>> GetJobCardAssetOptionsAsync(string? searchTerm = null)
        => _assetMappingService.GetSelectionOptionsAsync(searchTerm);

    public async Task<MaintenanceAssetDto> CreateAssetAsync(CreateMaintenanceAssetDto createDto)
    {
        try
        {
            _logger.LogInformation("Creating new maintenance asset: {AssetName}", createDto.Name);

            // Validate category exists
            var category = await _categoryRepository.GetByIdAsync(createDto.AssetCategoryId) ?? throw new ArgumentException($"Asset category with ID {createDto.AssetCategoryId} not found");

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
                var parentAsset = await _assetRepository.GetByIdAsync(createDto.ParentAssetId.Value) ?? throw new ArgumentException($"Parent asset with ID {createDto.ParentAssetId} not found");
            }

            var asset = _mapper.Map<MaintenanceAsset>(createDto);
            asset.TenantId = _currentUserService.TenantId ?? throw new InvalidOperationException("Tenant ID is required");

            // Debug logging
            _logger.LogInformation("Creating asset with AssetCategoryId: {CategoryId}", asset.AssetCategoryId);

            var createdAsset = await _assetRepository.AddAsync(asset);
            await _unitOfWork.SaveChangesAsync();

            // After the asset is created, optionally create maintenance schedules
            // based on the asset category's maintenance schedule configuration.
            if (category.AutoGenerateSchedules)
            {
                try
                {
                    await CreateSchedulesForAssetFromCategoryAsync(createdAsset, category);
                    await _unitOfWork.SaveChangesAsync();
                }
                catch (Exception scheduleEx)
                {
                    // Do not fail asset creation if schedule generation fails; just log it.
                    _logger.LogError(scheduleEx,
                        "Error auto-generating maintenance schedules for asset {AssetId} from category {CategoryId}",
                        createdAsset.Id, category.Id);
                }
            }
            else
            {
                _logger.LogInformation(
                    "Auto-generation of maintenance schedules is disabled for category {CategoryId}; skipping schedule creation for asset {AssetId}",
                    category.Id, createdAsset.Id);
            }

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

            var existingAsset = await _assetRepository.GetByIdAsync(id) ?? throw new ArgumentException($"Asset with ID {id} not found");

            // Validate category exists
            var category = await _categoryRepository.GetByIdAsync(updateDto.AssetCategoryId) ?? throw new ArgumentException($"Asset category with ID {updateDto.AssetCategoryId} not found");

            // Validate parent asset if specified
            if (updateDto.ParentAssetId.HasValue)
            {
                var parentAsset = await _assetRepository.GetByIdAsync(updateDto.ParentAssetId.Value) ?? throw new ArgumentException($"Parent asset with ID {updateDto.ParentAssetId} not found");

                // Ensure no circular reference
                if (await WouldCreateCircularReference(id, updateDto.ParentAssetId.Value))
                {
                    throw new ArgumentException("Cannot set parent asset as it would create a circular reference");
                }
            }

            var sourceControlled = existingAsset.FixedAssetId.HasValue || existingAsset.EstateManagedAssetId.HasValue;
            var canonicalSnapshot = sourceControlled
                ? new
                {
                    existingAsset.Name,
                    existingAsset.AssetNumber,
                    existingAsset.Description,
                    existingAsset.Manufacturer,
                    existingAsset.Model,
                    existingAsset.Year,
                    existingAsset.OwnershipType,
                    existingAsset.SerialNumber,
                    existingAsset.PurchaseDate,
                    existingAsset.PurchasePrice,
                    existingAsset.CurrentValue,
                    existingAsset.Location,
                    existingAsset.Status
                }
                : null;

            _mapper.Map(updateDto, existingAsset);
            if (canonicalSnapshot != null)
            {
                existingAsset.Name = canonicalSnapshot.Name;
                existingAsset.AssetNumber = canonicalSnapshot.AssetNumber;
                existingAsset.Description = canonicalSnapshot.Description;
                existingAsset.Manufacturer = canonicalSnapshot.Manufacturer;
                existingAsset.Model = canonicalSnapshot.Model;
                existingAsset.Year = canonicalSnapshot.Year;
                existingAsset.OwnershipType = canonicalSnapshot.OwnershipType;
                existingAsset.SerialNumber = canonicalSnapshot.SerialNumber;
                existingAsset.PurchaseDate = canonicalSnapshot.PurchaseDate;
                existingAsset.PurchasePrice = canonicalSnapshot.PurchasePrice;
                existingAsset.CurrentValue = canonicalSnapshot.CurrentValue;
                existingAsset.Location = canonicalSnapshot.Location;
                existingAsset.Status = canonicalSnapshot.Status;
            }
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

            var asset = await _assetRepository.GetByIdAsync(id) ?? throw new ArgumentException($"Asset with ID {id} not found");

            if (asset.FixedAssetId.HasValue || asset.EstateManagedAssetId.HasValue)
            {
                throw new InvalidOperationException("Source-controlled maintenance profiles cannot be deleted. Disable the Finance category flag or retire the Estate asset in its authoritative module instead.");
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
                a => a.AssetCategory!,
                a => a.CurrentProject!,
                a => a.CurrentSiteLocation!,
                a => a.ParentAsset!,
                a => a.ChildAssets!);

            var scope = await GetMaintenanceLocationScopeAsync();
            if (asset != null && scope.IsScoped && (!scope.LocationId.HasValue || asset.CurrentSiteLocationId != scope.LocationId.Value))
            {
                return null;
            }

            return asset != null ? _mapper.Map<MaintenanceAssetDto>(asset) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving maintenance asset: {AssetId}", id);
            throw;
        }
    }

    public async Task<IEnumerable<MaintenanceAssetDto>> GetAssetsByIdsAsync(List<Guid> ids)
    {
        try
        {
            if (ids == null || !ids.Any())
            {
                return Enumerable.Empty<MaintenanceAssetDto>();
            }

            var assets = await _assetRepository.GetQueryable()
                .Include(a => a.AssetCategory)
                .Where(a => ids.Contains(a.Id))
                .ToListAsync();

            return _mapper.Map<IEnumerable<MaintenanceAssetDto>>(assets);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving maintenance assets by IDs");
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

    public async Task<PagedResult<MaintenanceAssetListDto>> GetAssetsPagedAsync(int page, int pageSize, string? searchTerm = null, Guid? categoryId = null, Guid? siteLocationId = null)
    {
        try
        {
            var query = _assetRepository.GetQueryable()
                .Include(a => a.AssetCategory)
                .Include(a => a.CurrentProject)
                .Include(a => a.CurrentSiteLocation)
                .AsQueryable();

            var scope = await GetMaintenanceLocationScopeAsync();
            if (scope.IsScoped)
            {
                query = scope.LocationId.HasValue
                    ? query.Where(a => a.CurrentSiteLocationId == scope.LocationId.Value)
                    : query.Where(a => false);
            }

            if (siteLocationId.HasValue && siteLocationId.Value != Guid.Empty)
            {
                query = query.Where(a => a.CurrentSiteLocationId == siteLocationId.Value);
            }

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

            // Pre-load next maintenance dates from schedules for these assets.
            var assetIds = assets.Select(a => a.Id).ToList();
            var schedules = await _scheduleRepository.GetQueryable()
                .Where(s => assetIds.Contains(s.AssetId) && s.IsActive)
                .ToListAsync();

            var nextMaintenanceByAsset = schedules
                .GroupBy(s => s.AssetId)
                .ToDictionary(
                    g => g.Key,
                    g =>
                    {
                        // Prefer time-based or combined schedules for next maintenance date
                        var timeBased = g.Where(s =>
                            string.Equals(s.PrimaryTriggerType, "Time", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(s.PrimaryTriggerType, "Combined", StringComparison.OrdinalIgnoreCase) ||
                            (string.IsNullOrEmpty(s.PrimaryTriggerType) &&
                             !string.Equals(s.Frequency, "Custom", StringComparison.OrdinalIgnoreCase) &&
                             !string.Equals(s.Frequency, "Usage", StringComparison.OrdinalIgnoreCase) &&
                             !string.Equals(s.Frequency, "Condition", StringComparison.OrdinalIgnoreCase)));

                        return timeBased.Any()
                            ? timeBased.Min(s => s.NextDueDate)
                            : g.Min(s => s.NextDueDate);
                    });

            var assetListDtos = assets.Select(a => new MaintenanceAssetListDto
            {
                Id = a.Id,
                AssetSource = a.SourceType,
                SourceAssetId = a.FixedAssetId ?? a.EstateManagedAssetId ?? a.Id,
                IsSourceControlled = a.FixedAssetId.HasValue || a.EstateManagedAssetId.HasValue,
                Name = a.Name,
                AssetNumber = a.AssetNumber,
                Description = a.Description,
                AssetCategoryId = a.AssetCategoryId,
                CategoryName = a.AssetCategory.Name,
                AssetType = a.AssetCategory.AssetType,
                Manufacturer = a.Manufacturer,
                Model = a.Model,
                Year = a.Year,
                OwnershipType = a.OwnershipType.ToString(),
                LicensePlate = a.LicensePlate,
                VIN = a.VIN,
                FuelType = a.FuelType,
                Status = a.Status.ToString(),
                Criticality = a.Criticality.ToString(),
                Location = a.Location,
                CurrentProjectId = a.CurrentProjectId,
                CurrentProjectName = a.CurrentProject != null ? a.CurrentProject.Title : null,
                CurrentSiteLocationId = a.CurrentSiteLocationId,
                CurrentSiteLocationName = a.CurrentSiteLocation != null ? a.CurrentSiteLocation.Name : null,
                CurrentValue = a.CurrentValue,
                SerialNumber = a.SerialNumber,
                PurchaseDate = a.PurchaseDate,
                WarrantyEndDate = a.WarrantyEndDate,
                WarrantyStartDate = a.WarrantyStartDate,
                IsFleetAsset = a.IsFleetAsset,
                // These would need additional queries or joins
                ActiveWorkOrdersCount = 0, // TODO: Implement
                LastMaintenanceDate = null, // TODO: Implement
                NextMaintenanceDate = nextMaintenanceByAsset.TryGetValue(a.Id, out var next)
                    ? next
                    : (DateTime?)null
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
            var today = DateTime.UtcNow.Date;

            var metrics = new AssetMetricsDto
            {
                TotalAssets = allAssets.Count(),
                ActiveAssets = allAssets.Count(a => a.Status == AssetStatus.Active),
                MaintenanceAssets = allAssets.Count(a => a.Status == AssetStatus.Maintenance),
                RetiredAssets = allAssets.Count(a => a.Status == AssetStatus.Retired),
                CriticalAssets = allAssets.Count(a =>
                    a.Criticality == AssetCriticality.Critical ||
                    a.Status == AssetStatus.OutOfService),
                AssetsRequiringMaintenance = allAssets.Count(a =>
                    a.LastServiceDate == null ||
                    (a.NextServiceDue.HasValue && a.NextServiceDue.Value.Date <= today)),
                TotalValue = (decimal)totalValue,
                AverageValue = allAssets.Any() ? (decimal)totalValue / allAssets.Count() : 0,
                AssetsByCategory = allAssets.GroupBy(a => a.AssetCategory?.Name ?? "Uncategorized")
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

            var asset = await _assetRepository.GetByIdAsync(assetId) ?? throw new ArgumentException($"Asset with ID {assetId} not found");
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

            var asset = await _assetRepository.GetByIdAsync(assetId) ?? throw new ArgumentException($"Asset with ID {assetId} not found");
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

            // Get all assets for the current prefix to find max sequence
            var assetsWithPrefix = await _assetRepository.GetQueryable()
                .Where(a => a.AssetNumber.StartsWith(prefix))
                .Select(a => a.AssetNumber)
                .ToListAsync();

            int nextSequence = 1;
            if (assetsWithPrefix.Any())
            {
                // Parse all sequence numbers and find the maximum
                var maxSequence = assetsWithPrefix
                    .Select(assetNum =>
                    {
                        var numberPart = assetNum.Substring(prefix.Length);
                        if (int.TryParse(numberPart, out int seq))
                        {
                            return seq;
                        }

                        return 0;
                    })
                    .Max();

                nextSequence = maxSequence + 1;
            }

            return $"{prefix}{nextSequence:D4}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating asset number for category: {CategoryId}", categoryId);
            throw;
        }
    }

    public async Task<IEnumerable<MaintenanceAssetDto>> GetAvailableVehiclesAsync()
    {
        try
        {
            _logger.LogInformation("Retrieving available vehicles");

            var query = _assetRepository.GetQueryable()
                .Include(a => a.AssetCategory)
                .Where(a => a.AssetCategory.AssetType == "Vehicle" && a.Status == AssetStatus.Active);

            var vehicles = await query.ToListAsync();

            _logger.LogInformation("Found {Count} available vehicles", vehicles.Count);

            return _mapper.Map<IEnumerable<MaintenanceAssetDto>>(vehicles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving available vehicles");
            throw;
        }
    }

    public async Task<MaintenanceAssetDto> MoveAssetAsync(Guid assetId, MoveMaintenanceAssetDto moveDto)
    {
        var tenantId = _currentUserService.TenantId ?? throw new InvalidOperationException("Tenant ID is required");
        var scope = await GetMaintenanceLocationScopeAsync();
        var asset = await _context.MaintenanceAssets
            .Include(a => a.AssetCategory)
            .Include(a => a.CurrentProject)
            .Include(a => a.CurrentSiteLocation)
            .FirstOrDefaultAsync(a => a.Id == assetId && a.TenantId == tenantId)
            ?? throw new ArgumentException($"Asset with ID {assetId} not found");

        if (scope.IsScoped && (!scope.LocationId.HasValue || asset.CurrentSiteLocationId != scope.LocationId.Value))
        {
            throw new UnauthorizedAccessException("You can only move assets assigned to your HR location/site.");
        }

        var targetProject = moveDto.ProjectId.HasValue
            ? await _context.Projects.FirstOrDefaultAsync(p => p.Id == moveDto.ProjectId.Value && p.TenantId == tenantId)
            : null;
        if (moveDto.ProjectId.HasValue && targetProject == null)
        {
            throw new ArgumentException("The selected project was not found");
        }

        var targetSite = moveDto.SiteLocationId.HasValue
            ? await _context.Locations.FirstOrDefaultAsync(l => l.Id == moveDto.SiteLocationId.Value && l.TenantId == tenantId && l.IsActive)
            : null;
        if (moveDto.SiteLocationId.HasValue && targetSite == null)
        {
            throw new ArgumentException("The selected site/location was not found or is inactive");
        }

        if (scope.IsScoped && (!scope.LocationId.HasValue || !moveDto.SiteLocationId.HasValue || moveDto.SiteLocationId.Value != scope.LocationId.Value))
        {
            throw new UnauthorizedAccessException("You can only assign assets to your HR location/site.");
        }

        var targetLocation = string.IsNullOrWhiteSpace(moveDto.Location)
            ? targetSite?.Name
            : moveDto.Location.Trim();

        if (asset.CurrentProjectId == moveDto.ProjectId &&
            asset.CurrentSiteLocationId == moveDto.SiteLocationId &&
            string.Equals(asset.Location?.Trim(), targetLocation?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The asset is already assigned to the selected project and site");
        }

        var movement = new MaintenanceAssetMovement
        {
            TenantId = tenantId,
            AssetId = asset.Id,
            FromProjectId = asset.CurrentProjectId,
            FromProjectName = asset.CurrentProject?.Title,
            ToProjectId = targetProject?.Id,
            ToProjectName = targetProject?.Title,
            FromSiteLocationId = asset.CurrentSiteLocationId,
            FromSiteLocationName = asset.CurrentSiteLocation?.Name,
            ToSiteLocationId = targetSite?.Id,
            ToSiteLocationName = targetSite?.Name,
            FromLocation = asset.Location,
            ToLocation = targetLocation,
            MovementType = "Transfer",
            EffectiveDate = moveDto.EffectiveDate == default ? DateTime.UtcNow : moveDto.EffectiveDate,
            Reason = moveDto.Reason.Trim(),
            Notes = moveDto.Notes?.Trim(),
            MovedByUserId = Guid.TryParse(_currentUserService.UserId, out var movedByUserId) ? movedByUserId : null,
            CreatedBy = _currentUserService.UserName
        };

        asset.CurrentProjectId = targetProject?.Id;
        asset.CurrentSiteLocationId = targetSite?.Id;
        asset.Location = targetLocation;
        asset.UpdatedAt = DateTime.UtcNow;
        asset.UpdatedBy = _currentUserService.UserName;

        _context.MaintenanceAssetMovements.Add(movement);
        await _context.SaveChangesAsync();

        asset.CurrentProject = targetProject;
        asset.CurrentSiteLocation = targetSite;
        return _mapper.Map<MaintenanceAssetDto>(asset);
    }

    public async Task<IReadOnlyList<MaintenanceAssetMovementDto>> GetMovementHistoryAsync(Guid assetId)
    {
        var tenantId = _currentUserService.TenantId ?? throw new InvalidOperationException("Tenant ID is required");
        var assetExists = await _context.MaintenanceAssets.AnyAsync(a => a.Id == assetId && a.TenantId == tenantId);
        if (!assetExists)
        {
            throw new ArgumentException($"Asset with ID {assetId} not found");
        }

        var movements = await _context.MaintenanceAssetMovements
            .Where(m => m.AssetId == assetId && m.TenantId == tenantId)
            .OrderByDescending(m => m.EffectiveDate)
            .ThenByDescending(m => m.CreatedAt)
            .ToListAsync();

        return _mapper.Map<List<MaintenanceAssetMovementDto>>(movements);
    }

    public async Task<MaintenanceAssetLifecycleHistoryDto> GetLifecycleHistoryAsync(Guid assetId)
    {
        var tenantId = _currentUserService.TenantId ?? throw new InvalidOperationException("Tenant ID is required");
        var assetExists = await _context.MaintenanceAssets.AnyAsync(a => a.Id == assetId && a.TenantId == tenantId);
        if (!assetExists)
        {
            throw new ArgumentException($"Asset with ID {assetId} not found");
        }

        var movements = await GetMovementHistoryAsync(assetId);
        var workOrders = await _context.WorkOrders
            .Where(w => w.AssetId == assetId && w.TenantId == tenantId)
            .OrderByDescending(w => w.CreatedAt)
            .Select(w => new MaintenanceAssetWorkOrderHistoryDto
            {
                Id = w.Id,
                WorkOrderNumber = w.WorkOrderNumber,
                Title = w.Title,
                Status = w.Status,
                WorkOrderType = w.WorkOrderType.Name,
                MaintenanceType = w.MaintenanceType.Name,
                CreatedAt = w.CreatedAt,
                ActualCompletionDate = w.ActualCompletionDate,
                ActualCost = w.ActualCost
            })
            .ToListAsync();

        var inspections = await _context.AssetInspections
            .Where(i => i.AssetId == assetId && i.TenantId == tenantId)
            .OrderByDescending(i => i.InspectionDate)
            .Select(i => new MaintenanceAssetInspectionHistoryDto
            {
                Id = i.Id,
                TemplateName = i.InspectionTemplate.Name,
                InspectionDate = i.InspectionDate,
                Status = i.Status,
                OverallResult = i.OverallResult,
                Notes = i.Notes,
                FailedItemCount = i.OverallResult == "Fail" ? 1 : 0,
                FlaggedItemCount = i.OverallResult == "ConditionalPass" ? 1 : 0,
                GeneratedWorkOrderId = null,
                WorkflowEntityType = null
            })
            .ToListAsync();

        var fleetInspections = await _context.FleetTripInspections
            .Where(i => i.VehicleAssetId == assetId && i.TenantId == tenantId && !i.IsDeleted)
            .OrderByDescending(i => i.StartedAtUtc)
            .Select(i => new MaintenanceAssetInspectionHistoryDto
            {
                Id = i.Id,
                TemplateName = i.InspectionTemplate.Name,
                InspectionDate = i.CompletedAtUtc ?? i.StartedAtUtc,
                Status = i.Status,
                OverallResult = i.OverallResult,
                Notes = i.Notes,
                FailedItemCount = i.OverallResult == "Fail" ? 1 : 0,
                FlaggedItemCount = i.OverallResult == "ConditionalPass" ? 1 : 0,
                GeneratedWorkOrderId = null,
                WorkflowEntityType = "FleetTripInspection"
            })
            .ToListAsync();

        var fleetInspectionIds = fleetInspections.Select(i => i.Id).ToList();
        if (fleetInspectionIds.Count > 0)
        {
            var workOrdersByInspection = await _context.FleetDefects
                .Where(d => d.TenantId == tenantId && d.FleetTripInspectionId.HasValue &&
                    fleetInspectionIds.Contains(d.FleetTripInspectionId.Value) && !d.IsDeleted)
                .Select(d => new { InspectionId = d.FleetTripInspectionId!.Value, d.WorkOrderId })
                .ToListAsync();
            var workOrderLookup = workOrdersByInspection
                .GroupBy(x => x.InspectionId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.WorkOrderId).FirstOrDefault(id => id.HasValue));
            foreach (var inspection in fleetInspections)
            {
                if (workOrderLookup.TryGetValue(inspection.Id, out var workOrderId))
                    inspection.GeneratedWorkOrderId = workOrderId;
            }
        }

        inspections.AddRange(fleetInspections);
        inspections = inspections.OrderByDescending(i => i.InspectionDate).ToList();

        return new MaintenanceAssetLifecycleHistoryDto
        {
            AssetId = assetId,
            Movements = movements.ToList(),
            WorkOrderHistory = workOrders,
            ServiceHistory = workOrders
                .Where(w => w.ActualCompletionDate.HasValue ||
                    string.Equals(w.Status, "Completed", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(w.Status, "Closed", StringComparison.OrdinalIgnoreCase))
                .ToList(),
            InspectionHistory = inspections
        };
    }

    public async Task<MaintenanceAssetImportResultDto> ImportAssetsFromExcelAsync(Stream fileStream, string fileName)
    {
        var tenantId = _currentUserService.TenantId ?? throw new InvalidOperationException("Tenant ID is required");
        var result = new MaintenanceAssetImportResultDto();

        using var workbook = new XLWorkbook(fileStream);
        var worksheet = workbook.Worksheets.FirstOrDefault();
        var rowCount = worksheet?.LastRowUsed(XLCellsUsedOptions.AllContents)?.RowNumber() ?? 0;
        if (worksheet == null || rowCount < 2)
        {
            result.Errors.Add(new MaintenanceAssetImportErrorDto { RowNumber = 0, Field = "File", Error = "The spreadsheet has no asset rows" });
            result.ErrorCount = 1;
            return result;
        }

        var categories = (await _context.MaintenanceAssetCategories
                .Where(c => c.TenantId == tenantId && c.IsActive)
                .ToListAsync())
            .GroupBy(c => (c.Code ?? c.Name).Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        foreach (var category in categories.Values.DistinctBy(c => c.Id).ToList())
        {
            categories.TryAdd(category.Name.Trim(), category);
        }

        var projects = (await _context.Projects.Where(p => p.TenantId == tenantId).ToListAsync())
            .GroupBy(p => p.ProjectCode.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var sites = (await _context.Locations.Where(l => l.TenantId == tenantId && l.IsActive).ToListAsync())
            .GroupBy(l => string.IsNullOrWhiteSpace(l.Code) ? l.Name.Trim() : l.Code.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        foreach (var site in sites.Values.DistinctBy(s => s.Id).ToList())
        {
            sites.TryAdd(site.Name.Trim(), site);
        }

        var existingNumbers = new HashSet<string>(
            await _context.MaintenanceAssets.Where(a => a.TenantId == tenantId).Select(a => a.AssetNumber).ToListAsync(),
            StringComparer.OrdinalIgnoreCase);
        var assetsToCreate = new List<MaintenanceAsset>();
        result.TotalRows = rowCount - 1;

        for (var row = 2; row <= rowCount; row++)
        {
            var assetNumber = CellText(worksheet, row, 1);
            var name = CellText(worksheet, row, 2);
            var categoryKey = CellText(worksheet, row, 3);
            var errors = new List<MaintenanceAssetImportErrorDto>();

            void AddError(string field, string error) => errors.Add(new MaintenanceAssetImportErrorDto
            {
                RowNumber = row,
                AssetNumber = assetNumber,
                Field = field,
                Error = error
            });

            if (string.IsNullOrWhiteSpace(assetNumber)) AddError("Asset Number", "Required");
            else if (existingNumbers.Contains(assetNumber)) AddError("Asset Number", "Duplicate asset number");
            if (string.IsNullOrWhiteSpace(name)) AddError("Name", "Required");
            if (string.IsNullOrWhiteSpace(categoryKey)) AddError("Category Code", "Required");
            else if (!categories.ContainsKey(categoryKey)) AddError("Category Code", "Category was not found");

            var projectCode = CellText(worksheet, row, 5);
            var siteCode = CellText(worksheet, row, 6);
            if (!string.IsNullOrWhiteSpace(projectCode) && !projects.ContainsKey(projectCode)) AddError("Project Code", "Project was not found");
            if (!string.IsNullOrWhiteSpace(siteCode) && !sites.ContainsKey(siteCode)) AddError("Site Code", "Site/location was not found");

            var purchaseDateText = CellText(worksheet, row, 11);
            var purchaseDate = ParseImportDate(purchaseDateText);
            if (!string.IsNullOrWhiteSpace(purchaseDateText) && !purchaseDate.HasValue) AddError("Purchase Date", "Invalid date");

            if (errors.Count > 0)
            {
                result.Errors.AddRange(errors);
                result.ErrorCount++;
                continue;
            }

            var category = categories[categoryKey];
            projects.TryGetValue(projectCode, out var project);
            sites.TryGetValue(siteCode, out var site);
            var statusText = CellText(worksheet, row, 14);
            var criticalityText = CellText(worksheet, row, 15);
            var ownershipText = CellText(worksheet, row, 19);

            var asset = new MaintenanceAsset
            {
                TenantId = tenantId,
                AssetNumber = assetNumber,
                Name = name,
                AssetCategoryId = category.Id,
                Description = NullIfEmpty(CellText(worksheet, row, 4)),
                CurrentProjectId = project?.Id,
                CurrentSiteLocationId = site?.Id,
                Location = NullIfEmpty(CellText(worksheet, row, 7)) ?? site?.Name,
                Manufacturer = NullIfEmpty(CellText(worksheet, row, 8)),
                Model = NullIfEmpty(CellText(worksheet, row, 9)),
                SerialNumber = NullIfEmpty(CellText(worksheet, row, 10)),
                PurchaseDate = purchaseDate,
                PurchasePrice = ParseImportDecimal(CellText(worksheet, row, 12)),
                CurrentValue = ParseImportDecimal(CellText(worksheet, row, 13)),
                Status = Enum.TryParse<AssetStatus>(statusText, true, out var status) ? status : AssetStatus.Active,
                Criticality = Enum.TryParse<AssetCriticality>(criticalityText, true, out var criticality) ? criticality : AssetCriticality.Medium,
                IsFleetAsset = ParseImportBool(CellText(worksheet, row, 16)),
                LicensePlate = NullIfEmpty(CellText(worksheet, row, 17)),
                VIN = NullIfEmpty(CellText(worksheet, row, 18)),
                OwnershipType = Enum.TryParse<AssetOwnershipType>(ownershipText, true, out var ownership) ? ownership : AssetOwnershipType.Owned,
                FuelType = NullIfEmpty(CellText(worksheet, row, 20)),
                Year = int.TryParse(CellText(worksheet, row, 21), out var year) ? year : null,
                CreatedBy = _currentUserService.UserName
            };

            assetsToCreate.Add(asset);
            existingNumbers.Add(assetNumber);
            result.SuccessfulAssetNumbers.Add(assetNumber);
        }

        if (assetsToCreate.Count > 0)
        {
            await _context.MaintenanceAssets.AddRangeAsync(assetsToCreate);
            await _context.SaveChangesAsync();

            foreach (var asset in assetsToCreate)
            {
                var category = categories.Values.First(c => c.Id == asset.AssetCategoryId);
                if (category.AutoGenerateSchedules)
                {
                    await CreateSchedulesForAssetFromCategoryAsync(asset, category);
                }
            }

            await _unitOfWork.SaveChangesAsync();
            result.SuccessCount = assetsToCreate.Count;
        }

        return result;
    }

    public Task<byte[]> GenerateImportTemplateAsync()
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Maintenance Assets");
        var headers = new[]
        {
            "Asset Number*", "Name*", "Category Code*", "Description", "Project Code", "Site Code",
            "Location", "Manufacturer", "Model", "Serial Number", "Purchase Date", "Purchase Price",
            "Current Value", "Status", "Criticality", "Fleet Asset", "License Plate", "VIN",
            "Ownership Type", "Fuel Type", "Year"
        };

        for (var column = 0; column < headers.Length; column++)
        {
            worksheet.Cell(1, column + 1).Value = headers[column];
            worksheet.Cell(1, column + 1).Style.Font.Bold = true;
            worksheet.Cell(1, column + 1).Style.Fill.BackgroundColor = XLColor.LightBlue;
        }

        var sample = new object?[]
        {
            "AST-2026-001", "Service Vehicle", "VEH", "Field support vehicle", "PRJ-001", "SITE-01",
            "North Site", "Toyota", "Hilux", "SN-001", "2026-01-15", 250000, 240000,
            "Active", "High", "Yes", "GT-1234-26", "VIN123456", "Owned", "Diesel", 2025
        };
        for (var column = 0; column < sample.Length; column++)
        {
            SetCellValue(worksheet.Cell(2, column + 1), sample[column]);
        }

        worksheet.SheetView.FreezeRows(1);
        worksheet.ColumnsUsed().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return Task.FromResult(stream.ToArray());
    }

    private static string CellText(IXLWorksheet worksheet, int row, int column)
        => worksheet.Cell(row, column).GetFormattedString().Trim();

    private static void SetCellValue(IXLCell cell, object? value)
    {
        if (value == null)
        {
            cell.Clear(XLClearOptions.Contents);
            return;
        }

        cell.Value = value switch
        {
            string text => text,
            bool boolean => boolean,
            int number => number,
            long number => number,
            double number => number,
            decimal number => number,
            DateTime dateTime => dateTime,
            _ => value.ToString() ?? string.Empty
        };
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DateTime? ParseImportDate(string? value) =>
        DateTime.TryParse(value, out var parsed) ? parsed : null;

    private static decimal? ParseImportDecimal(string? value) =>
        decimal.TryParse(value, out var parsed) ? parsed : null;

    private static bool ParseImportBool(string? value) =>
        string.Equals(value?.Trim(), "yes", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value?.Trim(), "true", StringComparison.OrdinalIgnoreCase) ||
        value?.Trim() == "1";

    /// <summary>
    /// Creates one or more MaintenanceSchedule records for a newly created asset
    /// based on its category's maintenance schedule configuration.
    ///
    /// This treats the category-level configuration as a template and materializes it
    /// into concrete schedules that the scheduler can process.
    /// </summary>
    private async Task CreateSchedulesForAssetFromCategoryAsync(MaintenanceAsset asset, MaintenanceAssetCategory category)
    {
        // Basic validation – if no maintenance type is configured, do nothing.
        if (string.IsNullOrWhiteSpace(category.MaintenanceType))
        {
            _logger.LogDebug("Skipping schedule generation for asset {AssetId}: category {CategoryId} has no maintenance type configured",
                asset.Id, category.Id);
            return;
        }

        // Load active maintenance types to pick a reasonable default for the schedule.
        var maintenanceTypes = (await _maintenanceTypeRepository.GetActiveAsync()).ToList();
        if (!maintenanceTypes.Any())
        {
            _logger.LogWarning("No active maintenance types available; cannot auto-generate schedules for asset {AssetId}", asset.Id);
            return;
        }

        // Helper to get a time-based or usage-based maintenance type.
        var defaultTimeType = maintenanceTypes.FirstOrDefault(mt => mt.IsTimeBased)
                               ?? maintenanceTypes.FirstOrDefault();
        var defaultUsageType = maintenanceTypes.FirstOrDefault(mt => mt.IsUsageBased)
                                ?? defaultTimeType;

        if (defaultTimeType == null)
        {
            _logger.LogWarning("Unable to resolve any maintenance type for schedules; skipping auto-generation for asset {AssetId}", asset.Id);
            return;
        }

        var schedulesToCreate = new List<MaintenanceSchedule>();
        var today = DateTime.UtcNow.Date;

        // Local helpers for mapping category config into concrete schedules.
        void AddTimeBasedSchedule(string? frequencyLabel)
        {
            if (string.IsNullOrWhiteSpace(frequencyLabel))
            {
                frequencyLabel = "Monthly";
            }

            string frequency;
            int frequencyValue = 1;
            string frequencyUnit = "Days";
            DateTime nextDue;

            switch (frequencyLabel)
            {
                case "Weekly":
                    frequency = "Weekly";
                    nextDue = today.AddDays(7);
                    break;
                case "Monthly":
                    frequency = "Monthly";
                    nextDue = today.AddMonths(1);
                    break;
                case "Quarterly":
                    frequency = "Quarterly";
                    nextDue = today.AddMonths(3);
                    break;
                case "Semi-Annual":
                    frequency = "Bi-Annual"; // UI uses Bi-Annual, backend supports Custom via FrequencyUnit/Value
                    frequencyUnit = "Months";
                    frequencyValue = 6;
                    nextDue = today.AddMonths(6);
                    break;
                case "Annual":
                    frequency = "Annual";
                    nextDue = today.AddYears(1);
                    break;
                default:
                    frequency = "Monthly";
                    nextDue = today.AddMonths(1);
                    break;
            }

            var schedule = new MaintenanceSchedule
            {
                Id = Guid.NewGuid(),
                AssetId = asset.Id,
                MaintenanceTypeId = defaultTimeType.Id,
                Name = $"{asset.Name} - Time-based maintenance",
                Code = $"SCH-{Guid.NewGuid():N}"[..13],
                Description = $"Auto-generated from category {category.Name} time-based configuration",
                Frequency = frequency,
                FrequencyValue = frequencyValue,
                FrequencyUnit = frequencyUnit,
                StartDate = today,
                NextDueDate = nextDue,
                Priority = "Medium",
                EstimatedHours = 4,
                EstimatedCost = 0,
                Instructions = string.Empty,
                SafetyNotes = string.Empty,
                RequiredSkills = "[]",
                RequiredTools = "[]",
                RequiredParts = "[]",
                AutoGenerateWorkOrders = true,
                IsActive = true,
                ScheduleType = "Preventive",
                PrimaryTriggerType = "Time",
                TenantId = asset.TenantId,
                CreatedAt = DateTime.UtcNow,
                CreatedById = Guid.TryParse(_currentUserService.UserId, out var creatorId) ? creatorId : Guid.Empty
            };

            schedulesToCreate.Add(schedule);
        }

        void AddUsageBasedSchedule(string criteriaLabel, double? value, string? unit)
        {
            if (!value.HasValue || value.Value <= 0)
            {
                return;
            }

            var schedule = new MaintenanceSchedule
            {
                Id = Guid.NewGuid(),
                AssetId = asset.Id,
                MaintenanceTypeId = (defaultUsageType ?? defaultTimeType).Id,
                Name = $"{asset.Name} - Usage-based maintenance",
                Code = $"SCH-{Guid.NewGuid():N}"[..13],
                Description = $"Auto-generated from category {category.Name} {criteriaLabel.ToLowerInvariant()} configuration",
                Frequency = "Custom",
                FrequencyValue = 1,
                FrequencyUnit = unit ?? "Usage",
                StartDate = today,
                // For pure usage-based schedules, NextDueDate is a placeholder; triggers are usage-driven.
                NextDueDate = today,
                Priority = "Medium",
                EstimatedHours = 4,
                EstimatedCost = 0,
                Instructions = string.Empty,
                SafetyNotes = string.Empty,
                RequiredSkills = "[]",
                RequiredTools = "[]",
                RequiredParts = "[]",
                AutoGenerateWorkOrders = true,
                IsActive = true,
                ScheduleType = "Preventive",
                PrimaryTriggerType = "Usage",
                TenantId = asset.TenantId,
                CreatedAt = DateTime.UtcNow,
                CreatedById = Guid.TryParse(_currentUserService.UserId, out var creatorId) ? creatorId : Guid.Empty
            };

            switch (criteriaLabel)
            {
                case "Distance":
                    schedule.MileageTrigger = (decimal?)value;
                    schedule.UsageUnit = unit;
                    break;
                case "Usage":
                    schedule.OperatingHoursTrigger = (decimal?)value;
                    schedule.UsageUnit = unit;
                    break;
                case "Cycles":
                    schedule.CycleTrigger = (decimal?)value;
                    schedule.UsageUnit = unit;
                    break;
            }

            schedulesToCreate.Add(schedule);
        }

        // Map primary criteria
        if (string.Equals(category.MaintenanceScheduleType, "single", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(category.MaintenanceScheduleType))
        {
            switch (category.MaintenanceType)
            {
                case "Time":
                    AddTimeBasedSchedule(category.MaintenanceFrequency);
                    break;
                case "Distance":
                case "Usage":
                case "Cycles":
                    AddUsageBasedSchedule(category.MaintenanceType, category.MaintenanceValue, category.MaintenanceUnit);
                    break;
            }
        }
        else if (string.Equals(category.MaintenanceScheduleType, "multi", StringComparison.OrdinalIgnoreCase))
        {
            // For multi-criteria, create separate schedules for primary and secondary criteria.
            switch (category.MaintenanceType)
            {
                case "Time":
                    AddTimeBasedSchedule(category.MaintenanceFrequency);
                    break;
                case "Distance":
                case "Usage":
                case "Cycles":
                    AddUsageBasedSchedule(category.MaintenanceType, category.MaintenanceValue, category.MaintenanceUnit);
                    break;
            }

            if (!string.IsNullOrWhiteSpace(category.SecondaryMaintenanceType))
            {
                switch (category.SecondaryMaintenanceType)
                {
                    case "Time":
                        AddTimeBasedSchedule(category.SecondaryMaintenanceFrequency);
                        break;
                    case "Distance":
                    case "Usage":
                    case "Cycles":
                        AddUsageBasedSchedule(category.SecondaryMaintenanceType, category.SecondaryMaintenanceValue, category.SecondaryMaintenanceUnit);
                        break;
                }
            }
        }

        if (!schedulesToCreate.Any())
        {
            _logger.LogDebug("No schedules generated from category {CategoryId} for asset {AssetId}", category.Id, asset.Id);
            return;
        }

        foreach (var schedule in schedulesToCreate)
        {
            await _scheduleRepository.AddAsync(schedule);
        }

        _logger.LogInformation("Auto-generated {Count} maintenance schedules for asset {AssetId} from category {CategoryId}",
            schedulesToCreate.Count, asset.Id, category.Id);
    }

    private async Task<bool> WouldCreateCircularReference(Guid assetId, Guid parentAssetId)
    {
        var currentId = parentAssetId;
        while (currentId != Guid.Empty)
        {
            if (currentId == assetId)
            {
                return true;
            }

            var parent = await _assetRepository.GetByIdAsync(currentId);
            currentId = parent?.ParentAssetId ?? Guid.Empty;
        }

        return false;
    }

    private async Task<(bool IsScoped, Guid? LocationId)> GetMaintenanceLocationScopeAsync()
    {
        if (!_currentUserService.IsAuthenticated || !_currentUserService.EmployeeId.HasValue)
        {
            return (false, null);
        }

        var unrestrictedRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Constants.Roles.SuperAdmin,
            Constants.Roles.TenantAdmin,
            Constants.Roles.Manager,
            "MaintenanceManager",
            "MaintenanceSupervisor",
            "MaintenanceDirector",
            "FleetManager",
            "FleetSupervisor"
        };

        if ((_currentUserService.Roles ?? Enumerable.Empty<string>()).Any(unrestrictedRoles.Contains))
        {
            return (false, null);
        }

        var tenantId = _currentUserService.TenantId;
        if (!tenantId.HasValue)
        {
            return (false, null);
        }

        var employee = await _context.Employees
            .Include(e => e.Department)
            .FirstOrDefaultAsync(e =>
                e.Id == _currentUserService.EmployeeId.Value &&
                e.TenantId == tenantId.Value &&
                !e.IsDeleted &&
                e.IsActive);

        if (employee?.Department == null ||
            !employee.Department.Name.Contains("Maintenance", StringComparison.OrdinalIgnoreCase))
        {
            return (false, null);
        }

        return (true, employee.LocationId);
    }

}
