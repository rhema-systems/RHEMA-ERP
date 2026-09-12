using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.HR;

public class UnionRepository : GenericRepository<Union>, IUnionRepository
{
    public UnionRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<Union>> GetAllWithCountsAsync(Guid tenantId)
    {
        return await _dbSet
            .Where(u => u.TenantId == tenantId)
            .Include(u => u.Agreements)
            .OrderBy(u => u.Name)
            .ToListAsync();
    }

    public async Task<Union?> GetByIdWithAgreementsAsync(Guid id, Guid tenantId)
    {
        return await _dbSet
            .Include(u => u.Agreements)
            .FirstOrDefaultAsync(u => u.Id == id && u.TenantId == tenantId);
    }

    // ⚠ The Include is the fix, not decoration: without it this endpoint reported AgreementCount = 0
    // for a union the sibling endpoint reported 2 for. Same DTO, same union, same field.
    public async Task<IEnumerable<Union>> GetActiveAsync(Guid tenantId)
    {
        return await _dbSet
            .Where(u => u.TenantId == tenantId && u.IsActive)
            .Include(u => u.Agreements)
            .OrderBy(u => u.Name)
            .ToListAsync();
    }
}

public class CollectiveBargainingAgreementRepository : GenericRepository<CollectiveBargainingAgreement>, ICollectiveBargainingAgreementRepository
{
    public CollectiveBargainingAgreementRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<CollectiveBargainingAgreement>> GetByUnionIdAsync(Guid unionId, Guid tenantId)
    {
        return await _dbSet
            .Include(a => a.Union)
            .Where(a => a.UnionId == unionId && a.TenantId == tenantId)
            .OrderByDescending(a => a.EffectiveDate)
            .ToListAsync();
    }

    public async Task<CollectiveBargainingAgreement?> GetByIdWithUnionAsync(Guid id, Guid tenantId)
    {
        return await _dbSet
            .Include(a => a.Union)
            .FirstOrDefaultAsync(a => a.Id == id && a.TenantId == tenantId);
    }
}
