using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.HR;

/// <summary>
/// EF Core repository implementation for <see cref="StaffLevel"/>.
/// Data access only; all methods are tenant-scoped.
/// </summary>
public class StaffLevelRepository : GenericRepository<StaffLevel>, IStaffLevelRepository
{
    public StaffLevelRepository(ApplicationDbContext context) : base(context)
    {
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StaffLevel>> GetAllOrderedByRankAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _context.Set<StaffLevel>()
            .AsNoTracking()
            .Where(sl => sl.TenantId == tenantId && !sl.IsDeleted)
            .OrderBy(sl => sl.Rank)
            .ThenBy(sl => sl.Name)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StaffLevel>> GetActiveOrderedByRankAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _context.Set<StaffLevel>()
            .AsNoTracking()
            .Where(sl => sl.TenantId == tenantId && !sl.IsDeleted && sl.IsActive)
            .OrderBy(sl => sl.Rank)
            .ThenBy(sl => sl.Name)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<StaffLevel?> GetByCodeAsync(Guid tenantId, string code, CancellationToken cancellationToken = default)
    {
        var normalized = (code ?? string.Empty).Trim();

        return await _context.Set<StaffLevel>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                sl => sl.TenantId == tenantId && !sl.IsDeleted && sl.Code == normalized,
                cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> RankExistsAsync(Guid tenantId, int rank, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<StaffLevel>()
            .AsNoTracking()
            .Where(sl => sl.TenantId == tenantId && !sl.IsDeleted && sl.Rank == rank);

        if (excludeId.HasValue)
        {
            query = query.Where(sl => sl.Id != excludeId.Value);
        }

        return await query.AnyAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> CodeExistsAsync(Guid tenantId, string code, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var normalizedUpper = (code ?? string.Empty).Trim().ToUpper();

        var query = _context.Set<StaffLevel>()
            .AsNoTracking()
            .Where(sl => sl.TenantId == tenantId && !sl.IsDeleted && (sl.Code ?? string.Empty).ToUpper() == normalizedUpper);

        if (excludeId.HasValue)
        {
            query = query.Where(sl => sl.Id != excludeId.Value);
        }

        return await query.AnyAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> IsInUseAsync(Guid tenantId, Guid staffLevelId, CancellationToken cancellationToken = default)
    {
        // Avoid loading navigation collections; query the dependent set directly.
        return await _context.Set<EmployeePosition>()
            .AsNoTracking()
            .AnyAsync(
                ep => ep.TenantId == tenantId && !ep.IsDeleted && ep.StaffLevelId == staffLevelId,
                cancellationToken);
    }
}
