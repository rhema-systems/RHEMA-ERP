using ErpSystem.Core.Entities.HR.StaffDiscipline;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// STAFF OFFENSE
// ============================================================================

#region Staff Offense

public interface IStaffOffenseRepository : IGenericRepository<StaffOffense>
{
    /// <summary>
    /// Returns every offense in the tenant's catalog, ordered by name.
    /// </summary>
    /// <remarks>
    /// Use this rather than the generic <c>GetAllAsync()</c>, which spans every tenant. The service
    /// used to call that and narrow the result in memory — a cross-tenant read however carefully the
    /// caller filters afterwards.
    /// </remarks>
    Task<IEnumerable<StaffOffense>> GetAllForTenantAsync(Guid tenantId);

    /// <summary>Returns the offense matching the given code, or null if not found.</summary>
    Task<StaffOffense?> GetByCodeAsync(Guid tenantId, string offenseCode);

    /// <summary>Returns all active offenses, ordered alphabetically by name.</summary>
    Task<IEnumerable<StaffOffense>> GetActiveOffensesAsync(Guid tenantId);

    /// <summary>Returns a fully-loaded offense including its ordered procedural steps.</summary>
    Task<StaffOffense?> GetWithProceduresAsync(Guid tenantId, Guid id);

    /// <summary>Returns whether the offense code is already in use for the given tenant.</summary>
    Task<bool> CodeExistsAsync(string offenseCode, Guid tenantId);
}

#endregion

// ============================================================================
// STAFF OFFENSE PROCEDURE
// ============================================================================

#region Staff Offense Procedure

public interface IStaffOffenseProcedureRepository : IGenericRepository<StaffOffenseProcedure>
{
    /// <summary>Returns all procedural steps for an offense, ordered by sequence number.</summary>
    Task<IEnumerable<StaffOffenseProcedure>> GetByOffenseIdAsync(Guid tenantId, Guid offenseId);

    /// <summary>
    /// Returns the highest sequence number currently used for an offense,
    /// to assist with auto-increment when adding a new step.
    /// </summary>
    Task<int> GetMaxSequenceForOffenseAsync(Guid tenantId, Guid offenseId);
}

#endregion

// ============================================================================
// STAFF DISCIPLINARY ACTION TYPE
// ============================================================================

#region Staff Disciplinary Action Type

public interface IStaffDisciplinaryActionTypeRepository : IGenericRepository<StaffDisciplinaryActionType>
{
    /// <summary>
    /// Returns every action type in the tenant's catalog, ordered by name.
    /// See <see cref="IStaffOffenseRepository.GetAllForTenantAsync"/> — same reason.
    /// </summary>
    Task<IEnumerable<StaffDisciplinaryActionType>> GetAllForTenantAsync(Guid tenantId);

    /// <summary>Returns the action type matching the given code, or null if not found.</summary>
    Task<StaffDisciplinaryActionType?> GetByCodeAsync(Guid tenantId, string code);

    /// <summary>Returns all active disciplinary action types, ordered alphabetically.</summary>
    Task<IEnumerable<StaffDisciplinaryActionType>> GetActiveTypesAsync(Guid tenantId);

    /// <summary>Returns whether the code is already in use for the given tenant.</summary>
    Task<bool> CodeExistsAsync(string code, Guid tenantId);
}

#endregion
