using ErpSystem.Core.Entities;
using ErpSystem.Core.DTOs.Common;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// Salary grade repository.
/// Data-access only (EF Core). All methods are tenant-scoped; repositories never infer tenant context.
/// </summary>
public interface ISalaryGradeRepository
{
    /// <summary>
    /// Gets a salary grade by identifier (tenant-scoped).
    /// Intended for update flows (tracking may be required by the caller).
    /// </summary>
    Task<SalaryGrade?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a salary grade with its full hierarchy (levels + notches) loaded (tenant-scoped).
    /// Intended for read/UI display.
    /// </summary>
    Task<SalaryGrade?> GetByIdWithHierarchyAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all salary grades for a tenant.
    /// </summary>
    Task<IReadOnlyList<SalaryGrade>> GetAllAsync(Guid tenantId, bool includeInactive = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a paged list of salary grades for a tenant.
    /// Search filters by Code/Name.
    /// </summary>
    Task<PagedResult<SalaryGrade>> GetPagedAsync(
        Guid tenantId,
        int pageNumber,
        int pageSize,
        string? searchTerm = null,
        bool? isActive = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether a salary grade code already exists for a tenant.
    /// Optionally excludes an existing grade (used during updates).
    /// </summary>
    Task<bool> ExistsByCodeAsync(Guid tenantId, string code, Guid? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new salary grade.
    /// </summary>
    Task<SalaryGrade> AddAsync(SalaryGrade entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing salary grade.
    /// </summary>
    Task UpdateAsync(SalaryGrade entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft deletes a salary grade.
    /// </summary>
    Task DeleteAsync(SalaryGrade entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets active status (soft deactivate/activate) for a salary grade.
    /// </summary>
    Task SetActiveAsync(Guid tenantId, Guid id, bool isActive, CancellationToken cancellationToken = default);
}
