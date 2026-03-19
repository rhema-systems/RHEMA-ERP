using AutoMapper;
using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Maintenance;
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

    public MaintenanceAssetService(
        IMaintenanceAssetRepository assetRepository,
        IMaintenanceAssetCategoryRepository categoryRepository,
        IMaintenanceScheduleRepository scheduleRepository,
        IMaintenanceTypeRepository maintenanceTypeRepository,
        IMapper mapper,
        ILogger<MaintenanceAssetService> logger,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork)
    {
        _assetRepository = assetRepository;
        _categoryRepository = categoryRepository;
        _scheduleRepository = scheduleRepository;
        _maintenanceTypeRepository = maintenanceTypeRepository;
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

            var asset = await _assetRepository.GetByIdAsync(id) ?? throw new ArgumentException($"Asset with ID {id} not found");

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
                a => a.ParentAsset!,
                a => a.ChildAssets!);

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

}
