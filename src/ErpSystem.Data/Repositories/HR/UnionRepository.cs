using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.HR;

public class UnionRepository : GenericRepository<Union>, IUnionRepository
{
    public UnionRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<Union>> GetAllWithCountsAsync()
    {
        return await _dbSet
            .Include(u => u.Agreements)
            .OrderBy(u => u.Name)
            .ToListAsync();
    }

    public async Task<Union?> GetByIdWithAgreementsAsync(Guid id)
    {
        return await _dbSet
            .Include(u => u.Agreements)
            .FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<IEnumerable<Union>> GetActiveAsync()
    {
        return await _dbSet
            .Where(u => u.IsActive)
            .OrderBy(u => u.Name)
            .ToListAsync();
    }
}

public class CollectiveBargainingAgreementRepository : GenericRepository<CollectiveBargainingAgreement>, ICollectiveBargainingAgreementRepository
{
    public CollectiveBargainingAgreementRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<CollectiveBargainingAgreement>> GetByUnionIdAsync(Guid unionId)
    {
        return await _dbSet
            .Include(a => a.Union)
            .Where(a => a.UnionId == unionId)
            .OrderByDescending(a => a.EffectiveDate)
            .ToListAsync();
    }
}
