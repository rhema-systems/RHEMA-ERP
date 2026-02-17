using ErpSystem.Core.DTOs.HR;

namespace ErpSystem.Core.Interfaces.HR.Services;

/// <summary>
/// Service contract for Salary Level operations (tenant-aware).
/// </summary>
public interface ISalaryLevelService
{
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
}
