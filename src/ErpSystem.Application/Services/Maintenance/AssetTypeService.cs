using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Application.DTOs.Maintenance;
using ErpSystem.Application.Interfaces;
using ErpSystem.Data;
using System.Text.Json;

namespace ErpSystem.Application.Services.Maintenance;

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
        try
        {
            // Check for duplicate asset type name within tenant
            var existingAssetType = await _context.AssetTypes
                .Where(at => at.TenantId == tenantId && at.Name == dto.Name)
                .FirstOrDefaultAsync();

            if (existingAssetType != null)
            {
                throw new InvalidOperationException($"Asset type '{dto.Name}' already exists.");
            }

            var assetType = new AssetType
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = dto.Name,
                Description = dto.Description,
                Category = dto.Category,
                Icon = dto.Icon,
                ColorCode = dto.ColorCode,
                RequiresLocation = dto.RequiresLocation,
                RequiresOperatingHours = dto.RequiresOperatingHours,
                RequiresMileage = dto.RequiresMileage,
                RequiresLicensing = dto.RequiresLicensing,
                RequiresInspection = dto.RequiresInspection,
                RequiresSafetyChecks = dto.RequiresSafetyChecks,
                RequiresLockoutTagout = dto.RequiresLockoutTagout,
                RequiresPermits = dto.RequiresPermits,
                DefaultMaintenanceIntervalDays = dto.DefaultMaintenanceIntervalDays,
                IsActive = dto.IsActive,
                CreatedBy = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedBy = userId,
                UpdatedAt = DateTime.UtcNow
            };

            _context.AssetTypes.Add(assetType);

            // Add custom fields if provided
            if (dto.Fields?.Any() == true)
            {
                var fields = dto.Fields.Select(f => new AssetTypeField
                {
                    Id = Guid.NewGuid(),
                    AssetTypeId = assetType.Id,
                    FieldName = f.FieldName,
                    DisplayName = f.DisplayName,
                    FieldType = f.FieldType,
                    DefaultValue = f.DefaultValue,
                    ValidationRules = f.ValidationRules != null ? JsonSerializer.Serialize(f.ValidationRules) : null,
                    Options = f.Options != null ? JsonSerializer.Serialize(f.Options) : null,
                    HelpText = f.HelpText,
                    IsRequired = f.IsRequired,
                    SortOrder = f.SortOrder,
                    IsActive = f.IsActive,
                    CreatedAt = DateTime.UtcNow
                }).ToList();

                _context.AssetTypeFields.AddRange(fields);
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Created asset type {AssetTypeName} for tenant {TenantId}", dto.Name, tenantId);

            return await GetAssetTypeByIdAsync(assetType.Id, tenantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating asset type {AssetTypeName} for tenant {TenantId}", dto.Name, tenantId);
            throw;
        }
    }

    public async Task<AssetTypeDto> UpdateAssetTypeAsync(Guid id, UpdateAssetTypeDto dto, Guid userId, Guid tenantId)
    {
        try
        {
            var assetType = await _context.AssetTypes
                .Include(at => at.Fields)
                .Where(at => at.Id == id && at.TenantId == tenantId)
                .FirstOrDefaultAsync();

            if (assetType == null)
            {
                throw new InvalidOperationException("Asset type not found.");
            }

            // Check for duplicate name (excluding current asset type)
            var existingAssetType = await _context.AssetTypes
                .Where(at => at.TenantId == tenantId && at.Name == dto.Name && at.Id != id)
                .FirstOrDefaultAsync();

            if (existingAssetType != null)
            {
                throw new InvalidOperationException($"Asset type '{dto.Name}' already exists.");
            }

            // Update asset type properties
            assetType.Name = dto.Name;
            assetType.Description = dto.Description;
            assetType.Category = dto.Category;
            assetType.Icon = dto.Icon;
            assetType.ColorCode = dto.ColorCode;
            assetType.RequiresLocation = dto.RequiresLocation;
            assetType.RequiresOperatingHours = dto.RequiresOperatingHours;
            assetType.RequiresMileage = dto.RequiresMileage;
            assetType.RequiresLicensing = dto.RequiresLicensing;
            assetType.RequiresInspection = dto.RequiresInspection;
            assetType.RequiresSafetyChecks = dto.RequiresSafetyChecks;
            assetType.RequiresLockoutTagout = dto.RequiresLockoutTagout;
            assetType.RequiresPermits = dto.RequiresPermits;
            assetType.DefaultMaintenanceIntervalDays = dto.DefaultMaintenanceIntervalDays;
            assetType.IsActive = dto.IsActive;
            assetType.UpdatedBy = userId;
            assetType.UpdatedAt = DateTime.UtcNow;

            // Update custom fields
            if (dto.Fields?.Any() == true)
            {
                // Remove existing fields that are not in the update
                var updatedFieldIds = dto.Fields.Where(f => f.Id.HasValue).Select(f => f.Id.Value).ToList();
                var fieldsToRemove = assetType.Fields.Where(f => !updatedFieldIds.Contains(f.Id)).ToList();
                _context.AssetTypeFields.RemoveRange(fieldsToRemove);

                foreach (var fieldDto in dto.Fields)
                {
                    if (fieldDto.Id.HasValue)
                    {
                        // Update existing field
                        var existingField = assetType.Fields.FirstOrDefault(f => f.Id == fieldDto.Id.Value);
                        if (existingField != null)
                        {
                            existingField.FieldName = fieldDto.FieldName;
                            existingField.DisplayName = fieldDto.DisplayName;
                            existingField.FieldType = fieldDto.FieldType;
                            existingField.DefaultValue = fieldDto.DefaultValue;
                            existingField.ValidationRules = fieldDto.ValidationRules != null ? JsonSerializer.Serialize(fieldDto.ValidationRules) : null;
                            existingField.Options = fieldDto.Options != null ? JsonSerializer.Serialize(fieldDto.Options) : null;
                            existingField.HelpText = fieldDto.HelpText;
                            existingField.IsRequired = fieldDto.IsRequired;
                            existingField.SortOrder = fieldDto.SortOrder;
                            existingField.IsActive = fieldDto.IsActive;
                        }
                    }
                    else
                    {
                        // Add new field
                        var newField = new AssetTypeField
                        {
                            Id = Guid.NewGuid(),
                            AssetTypeId = assetType.Id,
                            FieldName = fieldDto.FieldName,
                            DisplayName = fieldDto.DisplayName,
                            FieldType = fieldDto.FieldType,
                            DefaultValue = fieldDto.DefaultValue,
                            ValidationRules = fieldDto.ValidationRules != null ? JsonSerializer.Serialize(fieldDto.ValidationRules) : null,
                            Options = fieldDto.Options != null ? JsonSerializer.Serialize(fieldDto.Options) : null,
                            HelpText = fieldDto.HelpText,
                            IsRequired = fieldDto.IsRequired,
                            SortOrder = fieldDto.SortOrder,
                            IsActive = fieldDto.IsActive,
                            CreatedAt = DateTime.UtcNow
                        };
                        _context.AssetTypeFields.Add(newField);
                    }
                }
            }
            else
            {
                // Remove all fields if none provided
                _context.AssetTypeFields.RemoveRange(assetType.Fields);
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Updated asset type {AssetTypeId} for tenant {TenantId}", id, tenantId);

            return await GetAssetTypeByIdAsync(id, tenantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating asset type {AssetTypeId} for tenant {TenantId}", id, tenantId);
            throw;
        }
    }

    public async Task<bool> DeleteAssetTypeAsync(Guid id, Guid tenantId)
    {
        try
        {
            var assetType = await _context.AssetTypes
                .Where(at => at.Id == id && at.TenantId == tenantId)
                .FirstOrDefaultAsync();

            if (assetType == null)
            {
                return false;
            }

            // Check if asset type is being used by any assets
            var assetCount = await _context.MaintenanceAssets
                .Where(ma => ma.AssetTypeId == id)
                .CountAsync();

            if (assetCount > 0)
            {
                throw new InvalidOperationException($"Cannot delete asset type. It is being used by {assetCount} asset(s).");
            }

            _context.AssetTypes.Remove(assetType);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Deleted asset type {AssetTypeId} for tenant {TenantId}", id, tenantId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting asset type {AssetTypeId} for tenant {TenantId}", id, tenantId);
            throw;
        }
    }

    public async Task<AssetTypeDto> GetAssetTypeByIdAsync(Guid id, Guid tenantId)
    {
        var assetType = await _context.AssetTypes
            .Include(at => at.Fields.Where(f => f.IsActive).OrderBy(f => f.SortOrder))
            .Include(at => at.CreatedByUser)
            .Include(at => at.UpdatedByUser)
            .Where(at => at.Id == id && at.TenantId == tenantId)
            .Select(at => new AssetTypeDto
            {
                Id = at.Id,
                Name = at.Name,
                Description = at.Description,
                Category = at.Category,
                Icon = at.Icon,
                ColorCode = at.ColorCode,
                RequiresLocation = at.RequiresLocation,
                RequiresOperatingHours = at.RequiresOperatingHours,
                RequiresMileage = at.RequiresMileage,
                RequiresLicensing = at.RequiresLicensing,
                RequiresInspection = at.RequiresInspection,
                RequiresSafetyChecks = at.RequiresSafetyChecks,
                RequiresLockoutTagout = at.RequiresLockoutTagout,
                RequiresPermits = at.RequiresPermits,
                DefaultMaintenanceIntervalDays = at.DefaultMaintenanceIntervalDays,
                IsActive = at.IsActive,
                CreatedAt = at.CreatedAt,
                CreatedBy = at.CreatedBy,
                CreatedByName = at.CreatedByUser != null ? $"{at.CreatedByUser.FirstName} {at.CreatedByUser.LastName}" : null,
                UpdatedAt = at.UpdatedAt,
                UpdatedBy = at.UpdatedBy,
                UpdatedByName = at.UpdatedByUser != null ? $"{at.UpdatedByUser.FirstName} {at.UpdatedByUser.LastName}" : null,
                Fields = at.Fields.Select(f => new AssetTypeFieldDto
                {
                    Id = f.Id,
                    FieldName = f.FieldName,
                    DisplayName = f.DisplayName,
                    FieldType = f.FieldType,
                    DefaultValue = f.DefaultValue,
                    ValidationRules = f.ValidationRules != null ? JsonSerializer.Deserialize<Dictionary<string, object>>(f.ValidationRules) : null,
                    Options = f.Options != null ? JsonSerializer.Deserialize<List<string>>(f.Options) : null,
                    HelpText = f.HelpText,
                    IsRequired = f.IsRequired,
                    SortOrder = f.SortOrder,
                    IsActive = f.IsActive,
                    CreatedAt = f.CreatedAt
                }).ToList()
            })
            .FirstOrDefaultAsync();

        if (assetType == null)
        {
            throw new InvalidOperationException("Asset type not found.");
        }

        return assetType;
    }

    public async Task<List<AssetTypeDto>> GetAssetTypesAsync(Guid tenantId, bool includeInactive = false)
    {
        var query = _context.AssetTypes
            .Include(at => at.Fields.Where(f => f.IsActive).OrderBy(f => f.SortOrder))
            .Include(at => at.CreatedByUser)
            .Include(at => at.UpdatedByUser)
            .Where(at => at.TenantId == tenantId);

        if (!includeInactive)
        {
            query = query.Where(at => at.IsActive);
        }

        return await query
            .OrderBy(at => at.Category)
            .ThenBy(at => at.Name)
            .Select(at => new AssetTypeDto
            {
                Id = at.Id,
                Name = at.Name,
                Description = at.Description,
                Category = at.Category,
                Icon = at.Icon,
                ColorCode = at.ColorCode,
                RequiresLocation = at.RequiresLocation,
                RequiresOperatingHours = at.RequiresOperatingHours,
                RequiresMileage = at.RequiresMileage,
                RequiresLicensing = at.RequiresLicensing,
                RequiresInspection = at.RequiresInspection,
                RequiresSafetyChecks = at.RequiresSafetyChecks,
                RequiresLockoutTagout = at.RequiresLockoutTagout,
                RequiresPermits = at.RequiresPermits,
                DefaultMaintenanceIntervalDays = at.DefaultMaintenanceIntervalDays,
                IsActive = at.IsActive,
                CreatedAt = at.CreatedAt,
                CreatedBy = at.CreatedBy,
                CreatedByName = at.CreatedByUser != null ? $"{at.CreatedByUser.FirstName} {at.CreatedByUser.LastName}" : null,
                UpdatedAt = at.UpdatedAt,
                UpdatedBy = at.UpdatedBy,
                UpdatedByName = at.UpdatedByUser != null ? $"{at.UpdatedByUser.FirstName} {at.UpdatedByUser.LastName}" : null,
                Fields = at.Fields.Select(f => new AssetTypeFieldDto
                {
                    Id = f.Id,
                    FieldName = f.FieldName,
                    DisplayName = f.DisplayName,
                    FieldType = f.FieldType,
                    DefaultValue = f.DefaultValue,
                    ValidationRules = f.ValidationRules != null ? JsonSerializer.Deserialize<Dictionary<string, object>>(f.ValidationRules) : null,
                    Options = f.Options != null ? JsonSerializer.Deserialize<List<string>>(f.Options) : null,
                    HelpText = f.HelpText,
                    IsRequired = f.IsRequired,
                    SortOrder = f.SortOrder,
                    IsActive = f.IsActive,
                    CreatedAt = f.CreatedAt
                }).ToList()
            })
            .ToListAsync();
    }

    public async Task<List<AssetTypeDto>> GetAssetTypesByCategoryAsync(string category, Guid tenantId, bool includeInactive = false)
    {
        var query = _context.AssetTypes
            .Include(at => at.Fields.Where(f => f.IsActive).OrderBy(f => f.SortOrder))
            .Include(at => at.CreatedByUser)
            .Include(at => at.UpdatedByUser)
            .Where(at => at.TenantId == tenantId && at.Category == category);

        if (!includeInactive)
        {
            query = query.Where(at => at.IsActive);
        }

        return await query
            .OrderBy(at => at.Name)
            .Select(at => new AssetTypeDto
            {
                Id = at.Id,
                Name = at.Name,
                Description = at.Description,
                Category = at.Category,
                Icon = at.Icon,
                ColorCode = at.ColorCode,
                RequiresLocation = at.RequiresLocation,
                RequiresOperatingHours = at.RequiresOperatingHours,
                RequiresMileage = at.RequiresMileage,
                RequiresLicensing = at.RequiresLicensing,
                RequiresInspection = at.RequiresInspection,
                RequiresSafetyChecks = at.RequiresSafetyChecks,
                RequiresLockoutTagout = at.RequiresLockoutTagout,
                RequiresPermits = at.RequiresPermits,
                DefaultMaintenanceIntervalDays = at.DefaultMaintenanceIntervalDays,
                IsActive = at.IsActive,
                CreatedAt = at.CreatedAt,
                CreatedBy = at.CreatedBy,
                CreatedByName = at.CreatedByUser != null ? $"{at.CreatedByUser.FirstName} {at.CreatedByUser.LastName}" : null,
                UpdatedAt = at.UpdatedAt,
                UpdatedBy = at.UpdatedBy,
                UpdatedByName = at.UpdatedByUser != null ? $"{at.UpdatedByUser.FirstName} {at.UpdatedByUser.LastName}" : null,
                Fields = at.Fields.Select(f => new AssetTypeFieldDto
                {
                    Id = f.Id,
                    FieldName = f.FieldName,
                    DisplayName = f.DisplayName,
                    FieldType = f.FieldType,
                    DefaultValue = f.DefaultValue,
                    ValidationRules = f.ValidationRules != null ? JsonSerializer.Deserialize<Dictionary<string, object>>(f.ValidationRules) : null,
                    Options = f.Options != null ? JsonSerializer.Deserialize<List<string>>(f.Options) : null,
                    HelpText = f.HelpText,
                    IsRequired = f.IsRequired,
                    SortOrder = f.SortOrder,
                    IsActive = f.IsActive,
                    CreatedAt = f.CreatedAt
                }).ToList()
            })
            .ToListAsync();
    }

    public async Task<bool> AssetTypeExistsAsync(Guid id, Guid tenantId)
    {
        return await _context.AssetTypes
            .AnyAsync(at => at.Id == id && at.TenantId == tenantId && at.IsActive);
    }

    public async Task<List<string>> GetAssetTypeCategoriesAsync(Guid tenantId)
    {
        return await _context.AssetTypes
            .Where(at => at.TenantId == tenantId && at.IsActive)
            .Select(at => at.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();
    }
}