using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.Maintenance;

#region Asset Task Template Repository

public class AssetTaskTemplateRepository : GenericRepository<AssetTaskTemplate>, IAssetTaskTemplateRepository
{
    public AssetTaskTemplateRepository(ApplicationDbContext context) : base(context)
    {
    }

    public override async Task<IEnumerable<AssetTaskTemplate>> GetAllAsync()
    {
        return await _dbSet
            .Include(t => t.Asset)
            .Include(t => t.MaintenanceType)
            .Include(t => t.AssignedTechnician)
            .ToListAsync();
    }

    public async Task<IEnumerable<AssetTaskTemplate>> GetByAssetIdAsync(Guid assetId)
    {
        return await _dbSet
            .Where(t => t.AssetId == assetId)
            .Include(t => t.Asset)
            .Include(t => t.MaintenanceType)
            .OrderBy(t => t.Sequence)
            .ToListAsync();
    }

    public async Task<IEnumerable<AssetTaskTemplate>> GetByAssetIdAndMaintenanceTypeAsync(Guid assetId, Guid maintenanceTypeId)
    {
        return await _dbSet
            .Where(t => t.AssetId == assetId && t.MaintenanceTypeId == maintenanceTypeId && t.IsActive)
            .Include(t => t.Asset)
            .Include(t => t.MaintenanceType)
            .OrderBy(t => t.Sequence)
            .ToListAsync();
    }

    public async Task<IEnumerable<AssetTaskTemplate>> GetByMaintenanceTypeIdAsync(Guid maintenanceTypeId)
    {
        return await _dbSet
            .Where(t => t.MaintenanceTypeId == maintenanceTypeId)
            .Include(t => t.Asset)
            .Include(t => t.MaintenanceType)
            .OrderBy(t => t.Sequence)
            .ToListAsync();
    }

    public async Task<IEnumerable<AssetTaskTemplate>> GetActiveByAssetIdAsync(Guid assetId)
    {
        return await _dbSet
            .Where(t => t.AssetId == assetId && t.IsActive)
            .Include(t => t.Asset)
            .Include(t => t.MaintenanceType)
            .OrderBy(t => t.Sequence)
            .ToListAsync();
    }

    public async Task<IEnumerable<AssetTaskTemplate>> GetOrderedBySequenceAsync(Guid assetId, Guid maintenanceTypeId)
    {
        return await _dbSet
            .Where(t => t.AssetId == assetId && t.MaintenanceTypeId == maintenanceTypeId && t.IsActive)
            .Include(t => t.Asset)
            .Include(t => t.MaintenanceType)
            .OrderBy(t => t.Sequence)
            .ToListAsync();
    }
}

#endregion

#region Asset Type Task Template Repository

public class AssetTypeTaskTemplateRepository : GenericRepository<AssetTypeTaskTemplate>, IAssetTypeTaskTemplateRepository
{
    public AssetTypeTaskTemplateRepository(ApplicationDbContext context) : base(context)
    {
    }

    public override async Task<IEnumerable<AssetTypeTaskTemplate>> GetAllAsync()
    {
        return await _dbSet
            .Include(t => t.AssetType)
            .Include(t => t.MaintenanceType)
            .Include(t => t.AssignedTechnician)
            .ToListAsync();
    }

    public async Task<IEnumerable<AssetTypeTaskTemplate>> GetByAssetTypeIdAsync(Guid assetTypeId)
    {
        return await _dbSet
            .Where(t => t.AssetTypeId == assetTypeId)
            .Include(t => t.AssetType)
            .Include(t => t.MaintenanceType)
            .OrderBy(t => t.Sequence)
            .ToListAsync();
    }

    public async Task<IEnumerable<AssetTypeTaskTemplate>> GetByAssetTypeIdAndMaintenanceTypeAsync(Guid assetTypeId, Guid maintenanceTypeId)
    {
        return await _dbSet
            .Where(t => t.AssetTypeId == assetTypeId && t.MaintenanceTypeId == maintenanceTypeId && t.IsActive)
            .Include(t => t.AssetType)
            .Include(t => t.MaintenanceType)
            .OrderBy(t => t.Sequence)
            .ToListAsync();
    }

    public async Task<IEnumerable<AssetTypeTaskTemplate>> GetByMaintenanceTypeIdAsync(Guid maintenanceTypeId)
    {
        return await _dbSet
            .Where(t => t.MaintenanceTypeId == maintenanceTypeId)
            .Include(t => t.AssetType)
            .Include(t => t.MaintenanceType)
            .OrderBy(t => t.Sequence)
            .ToListAsync();
    }

    public async Task<IEnumerable<AssetTypeTaskTemplate>> GetActiveByAssetTypeIdAsync(Guid assetTypeId)
    {
        return await _dbSet
            .Where(t => t.AssetTypeId == assetTypeId && t.IsActive)
            .Include(t => t.AssetType)
            .Include(t => t.MaintenanceType)
            .OrderBy(t => t.Sequence)
            .ToListAsync();
    }

    public async Task<IEnumerable<AssetTypeTaskTemplate>> GetOrderedBySequenceAsync(Guid assetTypeId, Guid maintenanceTypeId)
    {
        return await _dbSet
            .Where(t => t.AssetTypeId == assetTypeId && t.MaintenanceTypeId == maintenanceTypeId && t.IsActive)
            .Include(t => t.AssetType)
            .Include(t => t.MaintenanceType)
            .OrderBy(t => t.Sequence)
            .ToListAsync();
    }
}

#endregion

#region Maintenance Task Template Repository

public class MaintenanceTaskTemplateRepository : GenericRepository<MaintenanceTaskTemplate>, IMaintenanceTaskTemplateRepository
{
    public MaintenanceTaskTemplateRepository(ApplicationDbContext context) : base(context)
    {
    }

    public override async Task<IEnumerable<MaintenanceTaskTemplate>> GetAllAsync()
    {
        return await _dbSet
            .Include(t => t.MaintenanceType)
            .Include(t => t.AssignedTechnician)
            .ToListAsync();
    }

    public async Task<IEnumerable<MaintenanceTaskTemplate>> GetByMaintenanceTypeIdAsync(Guid maintenanceTypeId)
    {
        return await _dbSet
            .Where(t => t.MaintenanceTypeId == maintenanceTypeId)
            .Include(t => t.MaintenanceType)
            .OrderBy(t => t.Sequence)
            .ToListAsync();
    }

    public async Task<IEnumerable<MaintenanceTaskTemplate>> GetActiveByMaintenanceTypeIdAsync(Guid maintenanceTypeId)
    {
        return await _dbSet
            .Where(t => t.MaintenanceTypeId == maintenanceTypeId && t.IsActive)
            .Include(t => t.MaintenanceType)
            .OrderBy(t => t.Sequence)
            .ToListAsync();
    }

    public async Task<IEnumerable<MaintenanceTaskTemplate>> GetOrderedBySequenceAsync(Guid maintenanceTypeId)
    {
        return await _dbSet
            .Where(t => t.MaintenanceTypeId == maintenanceTypeId && t.IsActive)
            .Include(t => t.MaintenanceType)
            .OrderBy(t => t.Sequence)
            .ToListAsync();
    }
}

#endregion
