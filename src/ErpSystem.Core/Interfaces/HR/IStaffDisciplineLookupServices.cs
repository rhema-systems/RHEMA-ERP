using ErpSystem.Core.DTOs.HR;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// STAFF OFFENSE SERVICE
// ============================================================================

#region Staff Offense Service

/// <summary>
/// Manages the StaffOffense master-data catalog and its child procedure steps.
/// </summary>
public interface IStaffOffenseService
{
    // Offense queries
    Task<StaffOffenseDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<StaffOffenseDto?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>Returns the offense with its ordered procedure steps fully loaded.</summary>
    Task<StaffOffenseDto?> GetWithProceduresAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IEnumerable<StaffOffenseSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffOffenseSummaryDto>> GetActiveAsync(CancellationToken cancellationToken = default);

    // Offense CRUD
    Task<StaffOffenseDto> CreateAsync(CreateStaffOffenseDto createDto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<StaffOffenseDto> UpdateAsync(UpdateStaffOffenseDto updateDto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    // Procedure operations
    Task<StaffOffenseProcedureDto?> GetProcedureByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffOffenseProcedureDto>> GetProceduresByOffenseAsync(Guid offenseId, CancellationToken cancellationToken = default);
    Task<StaffOffenseProcedureDto> AddProcedureAsync(CreateStaffOffenseProcedureDto createDto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<StaffOffenseProcedureDto> UpdateProcedureAsync(UpdateStaffOffenseProcedureDto updateDto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteProcedureAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Renumber procedure sequences 1..n in the given order.</summary>
    Task<IEnumerable<StaffOffenseProcedureDto>> ReorderProceduresAsync(
        Guid offenseId,
        IReadOnlyList<Guid> orderedProcedureIds,
        Guid userId,
        CancellationToken cancellationToken = default);
}

#endregion

// ============================================================================
// STAFF DISCIPLINARY ACTION TYPE SERVICE
// ============================================================================

#region Staff Disciplinary Action Type Service

/// <summary>
/// Manages the lookup table of disciplinary action types (Verbal Warning, Written Warning, Dismissal, etc.).
/// </summary>
public interface IStaffDisciplinaryActionTypeService
{
    Task<StaffDisciplinaryActionTypeDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<StaffDisciplinaryActionTypeDto?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffDisciplinaryActionTypeSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<StaffDisciplinaryActionTypeSummaryDto>> GetActiveAsync(CancellationToken cancellationToken = default);

    Task<StaffDisciplinaryActionTypeDto> CreateAsync(CreateStaffDisciplinaryActionTypeDto createDto, Guid tenantId, Guid userId, CancellationToken cancellationToken = default);
    Task<StaffDisciplinaryActionTypeDto> UpdateAsync(UpdateStaffDisciplinaryActionTypeDto updateDto, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

#endregion
