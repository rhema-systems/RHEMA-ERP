using ErpSystem.Core.Entities;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// Salary level repository.
/// Data-access only (EF Core). All methods are tenant-scoped; repositories never infer tenant context.
/// </summary>
public interface ISalaryLevelRepository
{
    /// <summary>
    /// Gets a salary level by identifier (tenant-scoped).
    /// Intended for update flows (tracking may be required by the caller).
    /// </summary>
    Task<SalaryLevel?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a salary level with its notches loaded (tenant-scoped).
    /// Intended for read/UI display.
    /// </summary>
    Task<SalaryLevel?> GetByIdWithNotchesAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all levels for a grade (tenant-scoped), ordered by sequence.
    /// </summary>
    Task<IReadOnlyList<SalaryLevel>> GetByGradeIdAsync(Guid tenantId, Guid salaryGradeId, bool includeInactive = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether a salary level code already exists within a grade (tenant-scoped).
    /// Optionally excludes an existing level (used during updates).
    /// </summary>
    Task<bool> ExistsByCodeAsync(Guid tenantId, Guid salaryGradeId, string code, Guid? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new salary level.
    /// </summary>
    Task<SalaryLevel> AddAsync(SalaryLevel entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing salary level.
    /// </summary>
    Task UpdateAsync(SalaryLevel entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft deletes a salary level.
    /// </summary>
    Task DeleteAsync(SalaryLevel entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists an updated sequence order for levels within a grade.
    /// The dictionary maps levelId -&gt; sequence.
    /// </summary>
    Task ResequenceAsync(Guid tenantId, Guid salaryGradeId, IReadOnlyDictionary<Guid, int> sequences, CancellationToken cancellationToken = default);
}
