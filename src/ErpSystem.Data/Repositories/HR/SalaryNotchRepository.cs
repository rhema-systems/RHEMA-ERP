using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.HR;

/// <summary>
/// EF Core repository for <see cref="SalaryNotch"/>.
/// Data access only; all methods are tenant-scoped.
/// </summary>
public class SalaryNotchRepository : GenericRepository<SalaryNotch>, ISalaryNotchRepository
{
    public SalaryNotchRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<SalaryNotch?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Set<SalaryNotch>()
            .FirstOrDefaultAsync(n => n.TenantId == tenantId && n.Id == id && !n.IsDeleted, cancellationToken);
    }

    public async Task<IReadOnlyList<SalaryNotch>> GetByLevelIdAsync(Guid tenantId, Guid salaryLevelId, bool includeInactive = true, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<SalaryNotch>()
            .AsNoTracking()
            .Where(n => n.TenantId == tenantId && n.SalaryLevelId == salaryLevelId && !n.IsDeleted);

        if (!includeInactive)
        {
            query = query.Where(n => n.IsActive);
        }

        return await query
            .OrderBy(n => n.NotchNumber)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> NotchNumberExistsAsync(Guid tenantId, Guid salaryLevelId, int notchNumber, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<SalaryNotch>()
            .AsNoTracking()
            .Where(n => n.TenantId == tenantId && n.SalaryLevelId == salaryLevelId && n.NotchNumber == notchNumber && !n.IsDeleted);

        if (excludeId.HasValue)
        {
            query = query.Where(n => n.Id != excludeId.Value);
        }

        return await query.AnyAsync(cancellationToken);
    }

    public async Task<int> GetMaxNotchNumberAsync(Guid tenantId, Guid salaryLevelId, CancellationToken cancellationToken = default)
    {
        return await _context.Set<SalaryNotch>()
            .AsNoTracking()
            .Where(n => n.TenantId == tenantId && n.SalaryLevelId == salaryLevelId && !n.IsDeleted)
            .Select(n => (int?)n.NotchNumber)
            .MaxAsync(cancellationToken) ?? 0;
    }

    public async Task<SalaryNotch> AddAsync(SalaryNotch entity, CancellationToken cancellationToken = default)
    {
        return await base.AddAsync(entity);
    }

    public async Task UpdateAsync(SalaryNotch entity, CancellationToken cancellationToken = default)
    {
        await base.UpdateAsync(entity);
    }

    public async Task HardDeleteAsync(SalaryNotch entity, CancellationToken cancellationToken = default)
    {
        _context.Set<SalaryNotch>().Remove(entity);
        await Task.CompletedTask;
    }
}
