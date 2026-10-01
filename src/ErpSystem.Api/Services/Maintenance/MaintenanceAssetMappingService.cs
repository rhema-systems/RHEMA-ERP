using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities.Estate;
using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Maintenance;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Maintenance;

public sealed class MaintenanceAssetMappingService : IMaintenanceAssetMappingService
{
    private const string FinanceCategoryCode = "SOURCE-FA";
    private const string EstateCategoryCode = "SOURCE-EST";

    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public MaintenanceAssetMappingService(
        ApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<IReadOnlyList<JobCardAssetOptionDto>> GetSelectionOptionsAsync(string? searchTerm = null)
    {
        var tenantId = _currentUserService.TenantId
            ?? throw new UnauthorizedAccessException("Tenant not found.");
        var normalizedSearch = searchTerm?.Trim();

        var mappings = await _context.MaintenanceAssets
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted)
            .Select(item => new
            {
                item.Id,
                item.SourceType,
                item.FixedAssetId,
                item.EstateManagedAssetId,
                item.AssetNumber,
                item.Name,
                Category = item.AssetCategory.Name,
                item.Location,
                item.Status
            })
            .ToListAsync();

        var financeMappings = mappings
            .Where(item => item.FixedAssetId.HasValue)
            .GroupBy(item => item.FixedAssetId!.Value)
            .ToDictionary(group => group.Key, group => group.First().Id);
        var estateMappings = mappings
            .Where(item => item.EstateManagedAssetId.HasValue)
            .GroupBy(item => item.EstateManagedAssetId!.Value)
            .ToDictionary(group => group.Key, group => group.First().Id);

        var fixedAssetQuery = _context.FixedAssets
            .AsNoTracking()
            .Include(item => item.Category)
            .Where(item => item.TenantId == tenantId
                && !item.IsDeleted
                && !item.Category.IsDeleted
                && item.Category.RequiresMaintenance);

        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            fixedAssetQuery = fixedAssetQuery.Where(item =>
                item.AssetCode.Contains(normalizedSearch) ||
                item.Name.Contains(normalizedSearch) ||
                (item.Location != null && item.Location.Contains(normalizedSearch)) ||
                item.Category.Name.Contains(normalizedSearch));
        }

        var fixedAssets = await fixedAssetQuery
            .Select(item => new
            {
                item.Id,
                item.AssetCode,
                item.Name,
                item.Description,
                item.SerialNumber,
                item.PurchaseDate,
                item.NetBookValue,
                Category = item.Category.Name,
                item.Location,
                item.Status
            })
            .ToListAsync();

        var estateQuery = _context.EstateManagedAssets
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId
                && !item.IsDeleted
                && item.Status != EstateManagedAssetStatus.Retired);

        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            estateQuery = estateQuery.Where(item =>
                item.AssetCode.Contains(normalizedSearch) ||
                item.Name.Contains(normalizedSearch) ||
                (item.Location != null && item.Location.Contains(normalizedSearch)) ||
                (item.ProjectUnitCode != null && item.ProjectUnitCode.Contains(normalizedSearch)) ||
                (item.UnitType != null && item.UnitType.Contains(normalizedSearch)));
        }

        // External listing state is deliberately not used as an availability filter. Sold,
        // Leased and Occupied property units continue to require operational maintenance.
        var estateAssets = await estateQuery
            .Select(item => new
            {
                item.Id,
                item.AssetCode,
                item.Name,
                item.Description,
                item.ValuationAmount,
                item.TotalCapitalizedCost,
                item.UnitType,
                item.AssetType,
                item.Location,
                item.Status
            })
            .ToListAsync();

        var options = new List<JobCardAssetOptionDto>(fixedAssets.Count + estateAssets.Count + mappings.Count);
        options.AddRange(fixedAssets.Select(item => new JobCardAssetOptionDto
        {
            AssetSource = JobCardAssetSource.FixedAsset,
            SourceAssetId = item.Id,
            MaintenanceAssetId = financeMappings.GetValueOrDefault(item.Id),
            AssetCode = item.AssetCode,
            AssetName = item.Name,
            CategoryOrPropertyType = item.Category,
            Location = item.Location,
            Status = item.Status.ToString(),
            Description = item.Description,
            SerialNumber = item.SerialNumber,
            AcquisitionDate = item.PurchaseDate,
            CurrentValue = item.NetBookValue
        }));
        options.AddRange(estateAssets.Select(item => new JobCardAssetOptionDto
        {
            AssetSource = JobCardAssetSource.EstateManagedAsset,
            SourceAssetId = item.Id,
            MaintenanceAssetId = estateMappings.GetValueOrDefault(item.Id),
            AssetCode = item.AssetCode,
            AssetName = item.Name,
            CategoryOrPropertyType = string.IsNullOrWhiteSpace(item.UnitType)
                ? item.AssetType.ToString()
                : item.UnitType,
            Location = item.Location,
            Status = item.Status.ToString(),
            Description = item.Description,
            CurrentValue = item.ValuationAmount ?? item.TotalCapitalizedCost
        }));

        // Keep pre-integration records selectable so existing operations and history remain usable.
        options.AddRange(mappings
            .Where(item => !item.FixedAssetId.HasValue && !item.EstateManagedAssetId.HasValue)
            .Where(item => string.IsNullOrWhiteSpace(normalizedSearch)
                || item.AssetNumber.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
                || item.Name.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
                || (item.Location?.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ?? false))
            .Select(item => new JobCardAssetOptionDto
            {
                AssetSource = JobCardAssetSource.LegacyMaintenanceAsset,
                SourceAssetId = item.Id,
                MaintenanceAssetId = item.Id,
                AssetCode = item.AssetNumber,
                AssetName = item.Name,
                CategoryOrPropertyType = item.Category,
                Location = item.Location,
                Status = item.Status.ToString()
            }));

        return options
            .OrderBy(item => item.AssetSource)
            .ThenBy(item => item.AssetCode)
            .ThenBy(item => item.AssetName)
            .ToList();
    }

    public async Task<MaintenanceAsset> ResolveOrCreateProfileAsync(
        JobCardAssetSource assetSource,
        Guid sourceAssetId,
        Guid tenantId,
        Guid? createdById = null)
    {
        if (sourceAssetId == Guid.Empty)
        {
            throw new ArgumentException("An asset must be selected.", nameof(sourceAssetId));
        }

        if (assetSource == JobCardAssetSource.LegacyMaintenanceAsset)
        {
            return await _context.MaintenanceAssets.FirstOrDefaultAsync(item =>
                       item.Id == sourceAssetId && item.TenantId == tenantId && !item.IsDeleted)
                   ?? throw new ArgumentException($"Maintenance asset with ID {sourceAssetId} was not found.");
        }

        var existing = assetSource switch
        {
            JobCardAssetSource.FixedAsset => await _context.MaintenanceAssets.FirstOrDefaultAsync(item =>
                item.TenantId == tenantId && item.FixedAssetId == sourceAssetId && !item.IsDeleted),
            JobCardAssetSource.EstateManagedAsset => await _context.MaintenanceAssets.FirstOrDefaultAsync(item =>
                item.TenantId == tenantId && item.EstateManagedAssetId == sourceAssetId && !item.IsDeleted),
            _ => throw new ArgumentOutOfRangeException(nameof(assetSource), assetSource, "Unsupported asset source.")
        };
        if (existing != null)
        {
            return existing;
        }

        var category = await EnsureSourceCategoryAsync(assetSource, tenantId, createdById);
        MaintenanceAsset profile;

        if (assetSource == JobCardAssetSource.FixedAsset)
        {
            var source = await _context.FixedAssets
                .Include(item => item.Category)
                .FirstOrDefaultAsync(item => item.Id == sourceAssetId
                    && item.TenantId == tenantId
                    && !item.IsDeleted
                    && item.Category.RequiresMaintenance)
                ?? throw new ArgumentException("The selected Finance fixed asset is not eligible for Maintenance.");

            profile = new MaintenanceAsset
            {
                TenantId = tenantId,
                SourceType = JobCardAssetSource.FixedAsset,
                FixedAssetId = source.Id,
                AssetCategoryId = category.Id,
                Name = source.Name,
                AssetNumber = source.AssetCode,
                Description = source.Description,
                SerialNumber = source.SerialNumber,
                PurchaseDate = source.PurchaseDate,
                PurchasePrice = source.AcquisitionCost,
                CurrentValue = source.NetBookValue,
                Location = source.Location,
                Status = MapFixedAssetStatus(source.Status),
                CreatedById = createdById
            };
            _context.MaintenanceAssets.Add(profile);
            source.MaintenanceAssetId = profile.Id;
        }
        else
        {
            var source = await _context.EstateManagedAssets.FirstOrDefaultAsync(item =>
                item.Id == sourceAssetId
                && item.TenantId == tenantId
                && !item.IsDeleted
                && item.Status != EstateManagedAssetStatus.Retired)
                ?? throw new ArgumentException("The selected Estate managed asset is not available for Maintenance.");

            profile = new MaintenanceAsset
            {
                TenantId = tenantId,
                SourceType = JobCardAssetSource.EstateManagedAsset,
                EstateManagedAssetId = source.Id,
                AssetCategoryId = category.Id,
                Name = source.Name,
                AssetNumber = source.AssetCode,
                Description = source.Description,
                PurchasePrice = source.TotalCapitalizedCost,
                CurrentValue = source.ValuationAmount,
                Location = source.Location,
                Status = MapEstateAssetStatus(source.Status),
                CreatedById = createdById
            };
            _context.MaintenanceAssets.Add(profile);
        }

        try
        {
            await _context.SaveChangesAsync();
            return profile;
        }
        catch (DbUpdateException)
        {
            _context.ChangeTracker.Clear();
            var winner = assetSource == JobCardAssetSource.FixedAsset
                ? await _context.MaintenanceAssets.FirstOrDefaultAsync(item =>
                    item.TenantId == tenantId && item.FixedAssetId == sourceAssetId && !item.IsDeleted)
                : await _context.MaintenanceAssets.FirstOrDefaultAsync(item =>
                    item.TenantId == tenantId && item.EstateManagedAssetId == sourceAssetId && !item.IsDeleted);
            if (winner != null)
            {
                return winner;
            }

            throw;
        }
    }

    private async Task<MaintenanceAssetCategory> EnsureSourceCategoryAsync(
        JobCardAssetSource source,
        Guid tenantId,
        Guid? createdById)
    {
        var code = source == JobCardAssetSource.FixedAsset ? FinanceCategoryCode : EstateCategoryCode;
        var existing = await _context.MaintenanceAssetCategories.FirstOrDefaultAsync(item =>
            item.TenantId == tenantId && item.Code == code && !item.IsDeleted);
        if (existing != null)
        {
            return existing;
        }

        var category = new MaintenanceAssetCategory
        {
            TenantId = tenantId,
            Code = code,
            Name = source == JobCardAssetSource.FixedAsset
                ? "Finance Fixed Asset"
                : "Estate Managed Asset",
            Description = "System maintenance profile category for assets owned by another authoritative register.",
            AssetType = source == JobCardAssetSource.FixedAsset ? "FixedAsset" : "Property",
            AutoGenerateSchedules = false,
            IsActive = true,
            CreatedById = createdById
        };
        _context.MaintenanceAssetCategories.Add(category);
        await _context.SaveChangesAsync();
        return category;
    }

    private static AssetStatus MapFixedAssetStatus(FixedAssetStatus status) => status switch
    {
        FixedAssetStatus.Active or FixedAssetStatus.FullyDepreciated => AssetStatus.Active,
        FixedAssetStatus.Disposed or FixedAssetStatus.WrittenOff => AssetStatus.Retired,
        _ => AssetStatus.Inactive
    };

    private static AssetStatus MapEstateAssetStatus(EstateManagedAssetStatus status) => status switch
    {
        EstateManagedAssetStatus.UnderMaintenance => AssetStatus.Maintenance,
        EstateManagedAssetStatus.Retired => AssetStatus.Retired,
        EstateManagedAssetStatus.Blocked => AssetStatus.Inactive,
        _ => AssetStatus.Active
    };
}
