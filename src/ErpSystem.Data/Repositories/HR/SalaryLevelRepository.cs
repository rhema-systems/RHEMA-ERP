using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.HR;

/// <summary>
/// EF Core repository for <see cref="SalaryLevel"/>.
/// Data access only; all methods are tenant-scoped.
/// </summary>
public class SalaryLevelRepository : GenericRepository<SalaryLevel>, ISalaryLevelRepository
{
    public SalaryLevelRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<SalaryLevel?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Set<SalaryLevel>()
            .FirstOrDefaultAsync(l => l.TenantId == tenantId && l.Id == id && !l.IsDeleted, cancellationToken);
    }

    public async Task<SalaryLevel?> GetByIdWithNotchesAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Set<SalaryLevel>()
            .AsNoTracking()
            .Include(l => l.Notches.Where(n => !n.IsDeleted).OrderBy(n => n.NotchNumber))
            .FirstOrDefaultAsync(l => l.TenantId == tenantId && l.Id == id && !l.IsDeleted, cancellationToken);
    }

    public async Task<IReadOnlyList<SalaryLevel>> GetByGradeIdAsync(Guid tenantId, Guid salaryGradeId, bool includeInactive = true, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<SalaryLevel>()
            .AsNoTracking()
            .Where(l => l.TenantId == tenantId && l.SalaryGradeId == salaryGradeId && !l.IsDeleted);

        if (!includeInactive)
        {
            query = query.Where(l => l.IsActive);
        }

        return await query
            .OrderBy(l => l.Sequence)
            .ThenBy(l => l.Code)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsByCodeAsync(Guid tenantId, Guid salaryGradeId, string code, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var normalized = (code ?? string.Empty).Trim();

        var query = _context.Set<SalaryLevel>()
            .AsNoTracking()
            .Where(l => l.TenantId == tenantId && l.SalaryGradeId == salaryGradeId && !l.IsDeleted && l.Code == normalized);

        if (excludeId.HasValue)
        {
            query = query.Where(l => l.Id != excludeId.Value);
        }

        return await query.AnyAsync(cancellationToken);
    }

    public async Task<SalaryLevel> AddAsync(SalaryLevel entity, CancellationToken cancellationToken = default)
    {
        return await base.AddAsync(entity);
    }

    public async Task UpdateAsync(SalaryLevel entity, CancellationToken cancellationToken = default)
    {
        await base.UpdateAsync(entity);
    }

    public async Task DeleteAsync(SalaryLevel entity, CancellationToken cancellationToken = default)
    {
        await base.DeleteAsync(entity);
    }

    public async Task ResequenceAsync(Guid tenantId, Guid salaryGradeId, IReadOnlyDictionary<Guid, int> sequences, CancellationToken cancellationToken = default)
    {
        var levelIds = sequences.Keys.ToList();

        var levels = await _context.Set<SalaryLevel>()
            .Where(l => l.TenantId == tenantId && l.SalaryGradeId == salaryGradeId && levelIds.Contains(l.Id) && !l.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (var level in levels)
        {
            if (sequences.TryGetValue(level.Id, out var sequence))
            {
                level.Sequence = sequence;
            }
        }

        await base.UpdateRangeAsync(levels);
    }
}
