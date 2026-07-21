using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.HR;

/// <summary>
/// EF Core repository for <see cref="SalaryGrade"/>.
/// Data access only; all methods are tenant-scoped.
/// </summary>
public class SalaryGradeRepository : GenericRepository<SalaryGrade>, ISalaryGradeRepository
{
    public SalaryGradeRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<SalaryGrade?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Set<SalaryGrade>()
            .FirstOrDefaultAsync(g => g.TenantId == tenantId && g.Id == id && !g.IsDeleted, cancellationToken);
    }

    public async Task<SalaryGrade?> GetByIdWithHierarchyAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Set<SalaryGrade>()
            .AsNoTracking()
            .Include(g => g.Levels.Where(l => !l.IsDeleted).OrderBy(l => l.Sequence))
                .ThenInclude(l => l.Notches.Where(n => !n.IsDeleted).OrderBy(n => n.NotchNumber))
            .FirstOrDefaultAsync(g => g.TenantId == tenantId && g.Id == id && !g.IsDeleted, cancellationToken);
    }

    public async Task<IReadOnlyList<SalaryGrade>> GetAllAsync(Guid tenantId, bool includeInactive = true, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<SalaryGrade>()
            .AsNoTracking()
            .Where(g => g.TenantId == tenantId && !g.IsDeleted);

        if (!includeInactive)
        {
            query = query.Where(g => g.IsActive);
        }

        return await query
            .OrderBy(g => g.Name)
            .ThenBy(g => g.Code)
            .ToListAsync(cancellationToken);
    }

    public async Task<Core.DTOs.Common.PagedResult<SalaryGrade>> GetPagedAsync(
        Guid tenantId,
        int pageNumber,
        int pageSize,
        string? searchTerm = null,
        bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        if (pageNumber <= 0) throw new ArgumentOutOfRangeException(nameof(pageNumber));
        if (pageSize <= 0) throw new ArgumentOutOfRangeException(nameof(pageSize));

        var query = _context.Set<SalaryGrade>()
            .AsNoTracking()
            .Where(g => g.TenantId == tenantId && !g.IsDeleted);

        if (isActive.HasValue)
        {
            query = query.Where(g => g.IsActive == isActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            query = query.Where(g => g.Code.Contains(term) || g.Name.Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(g => g.Name)
            .ThenBy(g => g.Code)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new ErpSystem.Core.DTOs.Common.PagedResult<SalaryGrade>
        {
            Items = items,
            TotalCount = totalCount,
            Page = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<bool> ExistsByCodeAsync(Guid tenantId, string code, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var normalized = (code ?? string.Empty).Trim();

        var query = _context.Set<SalaryGrade>()
            .AsNoTracking()
            .Where(g => g.TenantId == tenantId && !g.IsDeleted && g.Code == normalized);

        if (excludeId.HasValue)
        {
            query = query.Where(g => g.Id != excludeId.Value);
        }

        return await query.AnyAsync(cancellationToken);
    }

    public async Task<SalaryGrade> AddAsync(SalaryGrade entity, CancellationToken cancellationToken = default)
    {
        // Base GenericRepository already sets Id/CreatedAt.
        return await base.AddAsync(entity);
    }

    public async Task UpdateAsync(SalaryGrade entity, CancellationToken cancellationToken = default)
    {
        await base.UpdateAsync(entity);
    }

    public async Task DeleteAsync(SalaryGrade entity, CancellationToken cancellationToken = default)
    {
        await base.DeleteAsync(entity);
    }

    public async Task SetActiveAsync(Guid tenantId, Guid id, bool isActive, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Set<SalaryGrade>()
            .FirstOrDefaultAsync(g => g.TenantId == tenantId && g.Id == id && !g.IsDeleted, cancellationToken);

        if (entity == null)
        {
            return;
        }

        entity.IsActive = isActive;
        await base.UpdateAsync(entity);
    }
}
