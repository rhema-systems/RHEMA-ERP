using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;

namespace ErpSystem.Core.Interfaces.HR.Services;

/// <summary>
/// Service contract for the Salary Structure module (Salary Grades, Levels, and Notches).
/// 
/// Clean Architecture boundary:
/// - Uses DTOs only (no entities exposed)
/// - Tenant is always provided explicitly
/// - Operations are async-only
/// </summary>
public interface ISalaryStructureService
{
    #region Salary Grades

    /// <summary>
    /// Creates a salary grade for the given tenant.
    /// </summary>
    Task<SalaryGradeDto> CreateGradeAsync(Guid tenantId, CreateSalaryGradeDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a salary grade.
    /// </summary>
    Task<SalaryGradeDto> UpdateGradeAsync(Guid tenantId, UpdateSalaryGradeDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the active status of a salary grade.
    /// </summary>
    Task<SalaryGradeDto> SetGradeActiveAsync(Guid tenantId, Guid gradeId, bool isActive, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft deletes a salary grade.
    /// Deletion is prevented when the grade has any non-deleted levels.
    /// </summary>
    Task<bool> DeleteGradeAsync(Guid tenantId, Guid gradeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a salary grade with its full hierarchy (levels and notches).
    /// </summary>
    Task<SalaryGradeDetailDto> GetGradeHierarchyAsync(Guid tenantId, Guid gradeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all salary grades for a tenant.
    /// </summary>
    Task<IReadOnlyList<SalaryGradeDto>> GetAllGradesAsync(Guid tenantId, bool includeInactive = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a paged list of salary grades for a tenant.
    /// Optional search filters by Code/Name.
    /// </summary>
    Task<PagedResult<SalaryGradeDto>> GetGradesPagedAsync(
        Guid tenantId,
        int pageNumber,
        int pageSize,
        string? searchTerm = null,
        bool? isActive = null,
        CancellationToken cancellationToken = default);

    #endregion

    #region Salary Levels

    /// <summary>
    /// Adds a salary level to a grade.
    /// Prevents duplicate codes within the grade and overlapping salary ranges.
    /// </summary>
    Task<SalaryLevelDto> CreateLevelAsync(Guid tenantId, CreateSalaryLevelDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a salary level.
    /// Prevents duplicate codes within the grade and overlapping salary ranges.
    /// </summary>
    Task<SalaryLevelDto> UpdateLevelAsync(Guid tenantId, UpdateSalaryLevelDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a salary level with its notches.
    /// </summary>
    Task<SalaryLevelDetailDto> GetLevelDetailAsync(Guid tenantId, Guid levelId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves salary levels for a grade.
    /// </summary>
    Task<IReadOnlyList<SalaryLevelDto>> GetLevelsByGradeAsync(Guid tenantId, Guid gradeId, bool includeInactive = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft deletes a salary level.
    /// Deletion is prevented when the level has any non-deleted notches.
    /// </summary>
    Task<bool> DeleteLevelAsync(Guid tenantId, Guid levelId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the active status of a salary level.
    /// </summary>
    Task<SalaryLevelDto> SetLevelActiveAsync(Guid tenantId, Guid levelId, bool isActive, CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-sequences levels within a grade using the provided ordered level identifiers.
    /// This operation is transactional.
    /// </summary>
    Task ResequenceLevelsAsync(Guid tenantId, Guid gradeId, IReadOnlyList<Guid> orderedLevelIds, CancellationToken cancellationToken = default);

    #endregion

    #region Salary Notches

    /// <summary>
    /// Adds a salary notch to a level.
    /// Enforces notch-number uniqueness, sequential numbering, and salary amount within level range.
    /// </summary>
    Task<SalaryNotchDto> CreateNotchAsync(Guid tenantId, CreateSalaryNotchDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a salary notch.
    /// Enforces notch-number uniqueness, sequential numbering, and salary amount within level range.
    /// </summary>
    Task<SalaryNotchDto> UpdateNotchAsync(Guid tenantId, UpdateSalaryNotchDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Hard deletes a salary notch.
    /// </summary>
    Task<bool> DeleteNotchAsync(Guid tenantId, Guid notchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves salary notches for a level.
    /// </summary>
    Task<IReadOnlyList<SalaryNotchDto>> GetNotchesByLevelAsync(Guid tenantId, Guid levelId, bool includeInactive = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a salary notch by ID.
    /// </summary>
    Task<SalaryNotchDto> GetNotchByIdAsync(Guid tenantId, Guid notchId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the active status of a salary notch.
    /// </summary>
    Task<SalaryNotchDto> SetNotchActiveAsync(Guid tenantId, Guid notchId, bool isActive, CancellationToken cancellationToken = default);

    #endregion
}
