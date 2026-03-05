using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;

namespace ErpSystem.Core.Interfaces.HR.Services;

/// <summary>
/// Service contract for Salary Grade operations (tenant-aware).
/// </summary>
public interface ISalaryGradeService
{
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
}
