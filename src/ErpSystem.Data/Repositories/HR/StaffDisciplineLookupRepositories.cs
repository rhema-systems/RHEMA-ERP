using ErpSystem.Core.Entities.HR.StaffDiscipline;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.HR;

// ============================================================================
// STAFF OFFENSE REPOSITORY
// ============================================================================

#region Staff Offense Repository

public class StaffOffenseRepository : GenericRepository<StaffOffense>, IStaffOffenseRepository
{
    public StaffOffenseRepository(ApplicationDbContext context) : base(context) { }

    public async Task<StaffOffense?> GetByCodeAsync(string offenseCode)
    {
        return await _dbSet
            .FirstOrDefaultAsync(o => o.OffenseCode == offenseCode && !o.IsDeleted);
    }

    public async Task<IEnumerable<StaffOffense>> GetActiveOffensesAsync()
    {
        return await _dbSet
            .Where(o => o.IsActive && !o.IsDeleted)
            .OrderBy(o => o.OffenseName)
            .ToListAsync();
    }

    public async Task<StaffOffense?> GetWithProceduresAsync(Guid id)
    {
        return await _dbSet
            .Include(o => o.OffenseProcedures)
            .FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted);
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

    public async Task<IEnumerable<StaffOffenseProcedure>> GetByOffenseIdAsync(Guid offenseId)
    {
        return await _dbSet
            .Include(p => p.Offense)
            .Where(p => p.OffenseId == offenseId && !p.IsDeleted)
            .OrderBy(p => p.Sequence)
            .ToListAsync();
    }

    public async Task<int> GetMaxSequenceForOffenseAsync(Guid offenseId)
    {
        return await _dbSet
            .Where(p => p.OffenseId == offenseId && !p.IsDeleted)
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

    public async Task<StaffDisciplinaryActionType?> GetByCodeAsync(string code)
    {
        return await _dbSet
            .FirstOrDefaultAsync(t => t.Code == code && !t.IsDeleted);
    }

    public async Task<IEnumerable<StaffDisciplinaryActionType>> GetActiveTypesAsync()
    {
        return await _dbSet
            .Where(t => t.IsActive && !t.IsDeleted)
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
