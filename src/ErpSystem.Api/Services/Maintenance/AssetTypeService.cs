using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;
using ErpSystem.Data;

namespace ErpSystem.Api.Services.Maintenance;

/// <summary>
/// Service for managing asset types
/// </summary>
public class AssetTypeService : IAssetTypeService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AssetTypeService> _logger;

    public AssetTypeService(
        ApplicationDbContext context,
        ILogger<AssetTypeService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<AssetTypeDto> CreateAssetTypeAsync(CreateAssetTypeDto dto, Guid userId, Guid tenantId)
    {
        // Check for duplicate name
        if (!await IsAssetTypeNameUniqueAsync(dto.Name, tenantId))
            throw new InvalidOperationException($"Asset type '{dto.Name}' already exists.");

        if (!await IsAssetTypeCodeUniqueAsync(dto.Code, tenantId))
            throw new InvalidOperationException($"Asset type with code '{dto.Code}' already exists.");

        var assetType = new AssetType
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = dto.Name,
            Code = dto.Code,
            Description = dto.Description,
            Color = dto.Color,
            Icon = dto.Icon,
            IsActive = dto.IsActive,
            RequiresLocation = dto.RequiresLocation,
            RequiresOperatingHours = dto.RequiresOperatingHours,
            RequiresMileageTracking = dto.RequiresMileageTracking,
            RequiresLicensing = dto.RequiresLicensing,
            RequiresInspections = dto.RequiresInspections,
            SupportsHierarchy = dto.SupportsHierarchy,
            RequiresSpecializedFields = dto.RequiresSpecializedFields,
            DefaultMaintenanceIntervalDays = dto.DefaultMaintenanceIntervalDays,
            RequiresPreventiveMaintenance = dto.RequiresPreventiveMaintenance,
            RequiresConditionMonitoring = dto.RequiresConditionMonitoring,
            RequiresSafetyChecks = dto.RequiresSafetyChecks,
            RequiresLockoutTagout = dto.RequiresLockoutTagout,
            RequiresPermits = dto.RequiresPermits,
            DefaultWorkOrderPriority = dto.DefaultWorkOrderPriority,
            DefaultEstimatedHours = dto.DefaultEstimatedHours,
            DefaultWorkInstructions = dto.DefaultWorkInstructions,
            CustomFieldsConfig = dto.CustomFieldsConfig,
            CreatedBy = userId.ToString(),
            CreatedById = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedBy = userId.ToString(),
            LastModifiedById = userId,
            UpdatedAt = DateTime.UtcNow
        };

        _context.AssetTypes.Add(assetType);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created asset type {Name} for tenant {TenantId}", dto.Name, tenantId);
        return (await GetAssetTypeByIdAsync(assetType.Id, tenantId))!;
    }

    public async Task<AssetTypeDto> UpdateAssetTypeAsync(Guid id, UpdateAssetTypeDto dto, Guid userId, Guid tenantId)
    {
        var assetType = await _context.AssetTypes
            .FirstOrDefaultAsync(at => at.Id == id && at.TenantId == tenantId)
            ?? throw new InvalidOperationException("Asset type not found.");

        // Check duplicate name (excluding current)
        if (!await IsAssetTypeNameUniqueAsync(dto.Name, tenantId, id))
            throw new InvalidOperationException($"Asset type '{dto.Name}' already exists.");

        if (!await IsAssetTypeCodeUniqueAsync(dto.Code, tenantId, id))
            throw new InvalidOperationException($"Asset type with code '{dto.Code}' already exists.");

        assetType.Name = dto.Name;
        assetType.Code = dto.Code;
        assetType.Description = dto.Description;
        assetType.Color = dto.Color;
        assetType.Icon = dto.Icon;
        assetType.IsActive = dto.IsActive;
        assetType.RequiresLocation = dto.RequiresLocation;
        assetType.RequiresOperatingHours = dto.RequiresOperatingHours;
        assetType.RequiresMileageTracking = dto.RequiresMileageTracking;
        assetType.RequiresLicensing = dto.RequiresLicensing;
        assetType.RequiresInspections = dto.RequiresInspections;
        assetType.SupportsHierarchy = dto.SupportsHierarchy;
        assetType.RequiresSpecializedFields = dto.RequiresSpecializedFields;
        assetType.DefaultMaintenanceIntervalDays = dto.DefaultMaintenanceIntervalDays;
        assetType.RequiresPreventiveMaintenance = dto.RequiresPreventiveMaintenance;
        assetType.RequiresConditionMonitoring = dto.RequiresConditionMonitoring;
        assetType.RequiresSafetyChecks = dto.RequiresSafetyChecks;
        assetType.RequiresLockoutTagout = dto.RequiresLockoutTagout;
        assetType.RequiresPermits = dto.RequiresPermits;
        assetType.DefaultWorkOrderPriority = dto.DefaultWorkOrderPriority;
        assetType.DefaultEstimatedHours = dto.DefaultEstimatedHours;
        assetType.DefaultWorkInstructions = dto.DefaultWorkInstructions;
        assetType.CustomFieldsConfig = dto.CustomFieldsConfig;
        assetType.UpdatedBy = userId.ToString();
        assetType.LastModifiedById = userId;
        assetType.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Updated asset type {Id} for tenant {TenantId}", id, tenantId);
        return (await GetAssetTypeByIdAsync(id, tenantId))!;
    }

    public async Task<bool> DeleteAssetTypeAsync(Guid id, Guid tenantId)
    {
        var assetType = await _context.AssetTypes
            .FirstOrDefaultAsync(at => at.Id == id && at.TenantId == tenantId);

        if (assetType == null)
            return false;

        _context.AssetTypes.Remove(assetType);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Deleted asset type {Id} for tenant {TenantId}", id, tenantId);
        return true;
    }

    public async Task<AssetTypeDto?> GetAssetTypeByIdAsync(Guid id, Guid tenantId)
    {
        return await _context.AssetTypes
            .Where(at => at.Id == id && at.TenantId == tenantId)
            .Select(at => MapToDto(at))
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<AssetTypeDto>> GetAssetTypesAsync(Guid tenantId, bool includeInactive = false)
    {
        var query = _context.AssetTypes.Where(at => at.TenantId == tenantId);
        if (!includeInactive) query = query.Where(at => at.IsActive);

        return await query.OrderBy(at => at.Name).Select(at => MapToDto(at)).ToListAsync();
    }

    public async Task<IEnumerable<AssetTypeDto>> GetAssetTypesByCategoryAsync(string category, Guid tenantId, bool includeInactive = false)
    {
        // Use Code as category for filtering
        var query = _context.AssetTypes.Where(at => at.TenantId == tenantId && at.Code == category);
        if (!includeInactive) query = query.Where(at => at.IsActive);

        return await query.OrderBy(at => at.Name).Select(at => MapToDto(at)).ToListAsync();
    }

    public async Task<bool> IsAssetTypeNameUniqueAsync(string name, Guid tenantId, Guid? excludeId = null)
    {
        var query = _context.AssetTypes.Where(at => at.TenantId == tenantId && at.Name == name);
        if (excludeId.HasValue)
            query = query.Where(at => at.Id != excludeId.Value);

        return !await query.AnyAsync();
    }

    public async Task<bool> IsAssetTypeCodeUniqueAsync(string code, Guid tenantId, Guid? excludeId = null)
    {
        if (string.IsNullOrWhiteSpace(code))
            return true; // Allow empty codes

        var query = _context.AssetTypes.Where(at => at.TenantId == tenantId && at.Code == code);
        if (excludeId.HasValue)
            query = query.Where(at => at.Id != excludeId.Value);

        return !await query.AnyAsync();
    }

    public async Task<bool> AssetTypeExistsAsync(Guid assetTypeId, Guid tenantId)
    {
        return await _context.AssetTypes.AnyAsync(at => at.Id == assetTypeId && at.TenantId == tenantId && at.IsActive);
    }

    public async Task<IEnumerable<string>> GetAssetTypeCategoriesAsync(Guid tenantId)
    {
        // Return distinct codes as categories
        return await _context.AssetTypes
            .Where(at => at.TenantId == tenantId && at.IsActive)
            .Select(at => at.Code)
            .Where(c => !string.IsNullOrEmpty(c))
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();
    }

    public async Task<int> GetAssetCountByTypeAsync(Guid assetTypeId, Guid tenantId)
    {
        // Get the asset type name first
        var assetType = await _context.AssetTypes
            .Where(at => at.Id == assetTypeId && at.TenantId == tenantId)
            .Select(at => at.Name)
            .FirstOrDefaultAsync();

        if (string.IsNullOrEmpty(assetType))
            return 0;

        // Count assets in categories that match this asset type name
        return await _context.MaintenanceAssets
            .Where(a => a.TenantId == tenantId && !a.IsDeleted)
            .Where(a => a.AssetCategory.AssetType == assetType)
            .CountAsync();
    }

    private static AssetTypeDto MapToDto(AssetType at) => new()
    {
        Id = at.Id,
        Name = at.Name,
        Code = at.Code,
        Description = at.Description,
        Color = at.Color,
        Icon = at.Icon,
        IsActive = at.IsActive,
        RequiresLocation = at.RequiresLocation,
        RequiresOperatingHours = at.RequiresOperatingHours,
        RequiresMileageTracking = at.RequiresMileageTracking,
        RequiresLicensing = at.RequiresLicensing,
        RequiresInspections = at.RequiresInspections,
        SupportsHierarchy = at.SupportsHierarchy,
        RequiresSpecializedFields = at.RequiresSpecializedFields,
        DefaultMaintenanceIntervalDays = at.DefaultMaintenanceIntervalDays,
        RequiresPreventiveMaintenance = at.RequiresPreventiveMaintenance,
        RequiresConditionMonitoring = at.RequiresConditionMonitoring,
        RequiresSafetyChecks = at.RequiresSafetyChecks,
        RequiresLockoutTagout = at.RequiresLockoutTagout,
        RequiresPermits = at.RequiresPermits,
        DefaultWorkOrderPriority = at.DefaultWorkOrderPriority,
        DefaultEstimatedHours = at.DefaultEstimatedHours,
        DefaultWorkInstructions = at.DefaultWorkInstructions,
        CustomFieldsConfig = at.CustomFieldsConfig,
        CreatedAt = at.CreatedAt,
        UpdatedAt = at.UpdatedAt
    };
}

