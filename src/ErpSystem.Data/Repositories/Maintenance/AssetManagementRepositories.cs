using Microsoft.EntityFrameworkCore;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;
using ErpSystem.Data.Repositories;

namespace ErpSystem.Data.Repositories.Maintenance;

#region Core Asset Management Repository Implementations

public class MaintenanceAssetRepository : GenericRepository<MaintenanceAsset>, IMaintenanceAssetRepository
{
    public MaintenanceAssetRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<MaintenanceAsset>> GetByAssetCategoryIdAsync(Guid categoryId)
    {
        if (categoryId == Guid.Empty)
            throw new ArgumentException("Category ID cannot be empty", nameof(categoryId));
            
        return await _dbSet
            .Where(a => a.AssetCategoryId == categoryId && !a.IsDeleted)
            .Include(a => a.AssetCategory)
            .Include(a => a.ParentAsset)
            .OrderBy(a => a.AssetNumber)
            .ToListAsync();
    }

    public async Task<IEnumerable<MaintenanceAsset>> GetByParentAssetIdAsync(Guid parentAssetId)
    {
        if (parentAssetId == Guid.Empty)
            throw new ArgumentException("Parent Asset ID cannot be empty", nameof(parentAssetId));
            
        return await _dbSet
            .Where(a => a.ParentAssetId == parentAssetId && !a.IsDeleted)
            .Include(a => a.AssetCategory)
            .Include(a => a.ParentAsset)
            .OrderBy(a => a.AssetNumber)
            .ToListAsync();
    }

    public async Task<IEnumerable<MaintenanceAsset>> GetByStatusAsync(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
            throw new ArgumentException("Status cannot be null or empty", nameof(status));
            
        return await _dbSet
            .Where(a => a.Status.ToString() == status && !a.IsDeleted)
            .Include(a => a.AssetCategory)
            .OrderBy(a => a.AssetNumber)
            .ToListAsync();
    }

    public async Task<IEnumerable<MaintenanceAsset>> GetByCriticalityAsync(string criticality)
    {
        if (string.IsNullOrWhiteSpace(criticality))
            throw new ArgumentException("Criticality cannot be null or empty", nameof(criticality));
            
        return await _dbSet
            .Where(a => a.Criticality.ToString() == criticality && !a.IsDeleted)
            .Include(a => a.AssetCategory)
            .OrderBy(a => a.AssetNumber)
            .ToListAsync();
    }

    public async Task<IEnumerable<MaintenanceAsset>> GetAssetsWithActiveWorkOrdersAsync()
    {
        return await _dbSet
            .Where(a => !a.IsDeleted)
            .Where(a => _context.Set<WorkOrder>()
                .Any(wo => wo.AssetId == a.Id && 
                          !wo.IsDeleted && 
                          wo.Status != "Completed" && 
                          wo.Status != "Cancelled"))
            .Include(a => a.AssetCategory)
            .Include(a => a.ParentAsset)
            .OrderBy(a => a.AssetNumber)
            .ToListAsync();
    }

    public async Task<IEnumerable<MaintenanceAsset>> GetAssetsRequiringMaintenanceAsync()
    {
        var today = DateTime.UtcNow.Date;
        return await _dbSet
            .Where(a => !a.IsDeleted && 
                       (a.LastServiceDate == null || 
                        a.NextServiceDue <= today))
            .Include(a => a.AssetCategory)
            .Include(a => a.ParentAsset)
            .OrderBy(a => a.NextServiceDue)
            .ToListAsync();
    }

    public async Task<MaintenanceAsset?> GetByAssetNumberAsync(string assetNumber)
    {
        if (string.IsNullOrWhiteSpace(assetNumber))
            throw new ArgumentException("Asset number cannot be null or empty", nameof(assetNumber));
            
        return await _dbSet
            .Where(a => a.AssetNumber == assetNumber && !a.IsDeleted)
            .Include(a => a.AssetCategory)
            .Include(a => a.ParentAsset)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> IsAssetNumberUniqueAsync(string assetNumber, Guid? excludeId = null)
    {
        if (string.IsNullOrWhiteSpace(assetNumber))
            return false;
            
        var query = _dbSet.Where(a => a.AssetNumber == assetNumber && !a.IsDeleted);
        if (excludeId.HasValue)
            query = query.Where(a => a.Id != excludeId.Value);

        return !await query.AnyAsync();
    }

    public async Task<IEnumerable<MaintenanceAsset>> SearchAssetsAsync(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return new List<MaintenanceAsset>();
            
        var lowerSearchTerm = searchTerm.ToLower();
        return await _dbSet
            .Where(a => !a.IsDeleted &&
                       (a.AssetNumber.ToLower().Contains(lowerSearchTerm) ||
                        a.Name.ToLower().Contains(lowerSearchTerm) ||
                        (a.Description != null && a.Description.ToLower().Contains(lowerSearchTerm)) ||
                        (a.SerialNumber != null && a.SerialNumber.ToLower().Contains(lowerSearchTerm))))
            .Include(a => a.AssetCategory)
            .Include(a => a.ParentAsset)
            .OrderBy(a => a.AssetNumber)
            .ToListAsync();
    }

    public async Task<IEnumerable<MaintenanceAsset>> GetAssetHierarchyAsync(Guid rootAssetId)
    {
        if (rootAssetId == Guid.Empty)
            throw new ArgumentException("Root Asset ID cannot be empty", nameof(rootAssetId));
            
        var assets = new List<MaintenanceAsset>();
        
        // Get the root asset
        var rootAsset = await GetByIdAsync(rootAssetId);
        if (rootAsset != null)
        {
            assets.Add(rootAsset);
            
            // Recursively get child assets
            await GetChildAssetsRecursive(rootAssetId, assets);
        }
        
        return assets;
    }

    private async Task GetChildAssetsRecursive(Guid parentId, List<MaintenanceAsset> assets)
    {
        var childAssets = await _dbSet
            .Where(a => a.ParentAssetId == parentId && !a.IsDeleted)
            .Include(a => a.AssetCategory)
            .ToListAsync();
            
        assets.AddRange(childAssets);
        
        foreach (var child in childAssets)
        {
            await GetChildAssetsRecursive(child.Id, assets);
        }
    }

    public async Task<double> GetTotalAssetValueAsync()
    {
        return await _dbSet
            .Where(a => !a.IsDeleted && a.PurchasePrice.HasValue)
            .SumAsync(a => (double)a.PurchasePrice!.Value);
    }

    public async Task<double> GetAssetValueByCategoryAsync(Guid categoryId)
    {
        if (categoryId == Guid.Empty)
            return 0;
            
        return await _dbSet
            .Where(a => a.AssetCategoryId == categoryId && !a.IsDeleted && a.PurchasePrice.HasValue)
            .SumAsync(a => (double)a.PurchasePrice!.Value);
    }
}

public class MaintenanceAssetCategoryRepository : GenericRepository<MaintenanceAssetCategory>, IMaintenanceAssetCategoryRepository
{
    public MaintenanceAssetCategoryRepository(ApplicationDbContext context) : base(context) { }

    public async Task<MaintenanceAssetCategory?> GetByCodeAsync(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Code cannot be null or empty", nameof(code));
            
        return await _dbSet
            .Where(c => c.Code == code && !c.IsDeleted)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> IsCodeUniqueAsync(string code, Guid? excludeId = null)
    {
        if (string.IsNullOrWhiteSpace(code))
            return false;
            
        var query = _dbSet.Where(c => c.Code == code && !c.IsDeleted);
        if (excludeId.HasValue)
            query = query.Where(c => c.Id != excludeId.Value);

        return !await query.AnyAsync();
    }

    public async Task<IEnumerable<MaintenanceAssetCategory>> GetActiveAsync()
    {
        return await _dbSet
            .Where(c => c.IsActive && !c.IsDeleted)
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<int> GetAssetCountByCategoryAsync(Guid categoryId)
    {
        if (categoryId == Guid.Empty)
            return 0;
            
        return await _context.Set<MaintenanceAsset>()
            .Where(a => a.AssetCategoryId == categoryId && !a.IsDeleted)
            .CountAsync();
    }
}

#endregion