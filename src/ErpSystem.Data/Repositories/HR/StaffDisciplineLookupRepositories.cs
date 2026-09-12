using ErpSystem.Core.Entities.HR.StaffDiscipline;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.HR;

// ============================================================================
// STAFF DISCIPLINE LOOKUP REPOSITORIES
//
// The by-code reads here were the cross-tenant FirstOrDefault shape: they matched a code across
// every tenant and returned whichever row came first, so a lookup could resolve — or a uniqueness
// guard could collide against — another tenant's catalog entry. Tenant-scoped now, like the rest.
// ============================================================================

// ============================================================================
// STAFF OFFENSE REPOSITORY
// ============================================================================

#region Staff Offense Repository

public class StaffOffenseRepository : GenericRepository<StaffOffense>, IStaffOffenseRepository
{
    public StaffOffenseRepository(ApplicationDbContext context) : base(context) { }

    private IQueryable<StaffOffense> Scoped(Guid tenantId) =>
        _dbSet.Where(o => o.TenantId == tenantId && !o.IsDeleted);

    /// <remarks>
    /// The catalog read. It exists because the service used to call the generic
    /// <c>GetAllAsync()</c> — every tenant's offences — and narrow the result in memory, which is a
    /// cross-tenant read however carefully the caller filters afterwards.
    /// </remarks>
    public async Task<IEnumerable<StaffOffense>> GetAllForTenantAsync(Guid tenantId)
    {
        return await Scoped(tenantId)
            .OrderBy(o => o.OffenseName)
            .ToListAsync();
    }

    public async Task<StaffOffense?> GetByCodeAsync(Guid tenantId, string offenseCode)
    {
        return await Scoped(tenantId)
            .FirstOrDefaultAsync(o => o.OffenseCode == offenseCode);
    }

    public async Task<IEnumerable<StaffOffense>> GetActiveOffensesAsync(Guid tenantId)
    {
        return await Scoped(tenantId)
            .Where(o => o.IsActive)
            .OrderBy(o => o.OffenseName)
            .ToListAsync();
    }

    public async Task<StaffOffense?> GetWithProceduresAsync(Guid tenantId, Guid id)
    {
        return await Scoped(tenantId)
            .Include(o => o.OffenseProcedures)
            .FirstOrDefaultAsync(o => o.Id == id);
    }

    public async Task<bool> CodeExistsAsync(string offenseCode, Guid tenantId)
    {
        return await _dbSet
            .AnyAsync(o => o.OffenseCode == offenseCode && o.TenantId == tenantId && !o.IsDeleted);
    }
}

#endregion

// ============================================================================
// STAFF OFFENSE PROCEDURE REPOSITORY
// ============================================================================

#region Staff Offense Procedure Repository

public class StaffOffenseProcedureRepository : GenericRepository<StaffOffenseProcedure>, IStaffOffenseProcedureRepository
{
    public StaffOffenseProcedureRepository(ApplicationDbContext context) : base(context) { }

    private IQueryable<StaffOffenseProcedure> Scoped(Guid tenantId) =>
        _dbSet.Where(p => p.TenantId == tenantId && !p.IsDeleted);

    public async Task<IEnumerable<StaffOffenseProcedure>> GetByOffenseIdAsync(Guid tenantId, Guid offenseId)
    {
        return await Scoped(tenantId)
            .Include(p => p.Offense)
            .Where(p => p.OffenseId == offenseId)
            .OrderBy(p => p.Sequence)
            .ToListAsync();
    }

    /// <remarks>
    /// Feeds the next sequence number for a procedure step. Unscoped, it took the maximum across
    /// every tenant's steps for that offense id — harmless only because offense ids do not collide
    /// across tenants, which is not a property worth relying on.
    /// </remarks>
    public async Task<int> GetMaxSequenceForOffenseAsync(Guid tenantId, Guid offenseId)
    {
        return await Scoped(tenantId)
            .Where(p => p.OffenseId == offenseId)
            .MaxAsync(p => (int?)p.Sequence) ?? 0;
    }
}

#endregion

// ============================================================================
// STAFF DISCIPLINARY ACTION TYPE REPOSITORY
// ============================================================================

#region Staff Disciplinary Action Type Repository

public class StaffDisciplinaryActionTypeRepository : GenericRepository<StaffDisciplinaryActionType>, IStaffDisciplinaryActionTypeRepository
{
    public StaffDisciplinaryActionTypeRepository(ApplicationDbContext context) : base(context) { }

    private IQueryable<StaffDisciplinaryActionType> Scoped(Guid tenantId) =>
        _dbSet.Where(t => t.TenantId == tenantId && !t.IsDeleted);

    /// <remarks>See <see cref="StaffOffenseRepository.GetAllForTenantAsync"/> — same reason.</remarks>
    public async Task<IEnumerable<StaffDisciplinaryActionType>> GetAllForTenantAsync(Guid tenantId)
    {
        return await Scoped(tenantId)
            .OrderBy(t => t.Name)
            .ToListAsync();
    }

    public async Task<StaffDisciplinaryActionType?> GetByCodeAsync(Guid tenantId, string code)
    {
        return await Scoped(tenantId)
            .FirstOrDefaultAsync(t => t.Code == code);
    }

    public async Task<IEnumerable<StaffDisciplinaryActionType>> GetActiveTypesAsync(Guid tenantId)
    {
        return await Scoped(tenantId)
            .Where(t => t.IsActive)
            .OrderBy(t => t.Name)
            .ToListAsync();
    }

    public async Task<bool> CodeExistsAsync(string code, Guid tenantId)
    {
        return await _dbSet
            .AnyAsync(t => t.Code == code && t.TenantId == tenantId && !t.IsDeleted);
    }
}

#endregion
