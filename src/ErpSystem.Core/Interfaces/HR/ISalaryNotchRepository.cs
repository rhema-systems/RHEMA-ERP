using ErpSystem.Core.Entities;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// Salary notch repository.
/// Data-access only (EF Core). All methods are tenant-scoped; repositories never infer tenant context.
/// </summary>
public interface ISalaryNotchRepository
{
    /// <summary>
    /// Gets a salary notch by identifier (tenant-scoped).
    /// </summary>
    Task<SalaryNotch?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all notches for a level (tenant-scoped), ordered by notch number.
    /// </summary>
    Task<IReadOnlyList<SalaryNotch>> GetByLevelIdAsync(Guid tenantId, Guid salaryLevelId, bool includeInactive = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether a notch number already exists within a level (tenant-scoped).
    /// Optionally excludes an existing notch (used during updates).
    /// </summary>
    Task<bool> NotchNumberExistsAsync(Guid tenantId, Guid salaryLevelId, int notchNumber, Guid? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the highest notch number currently present for a level (tenant-scoped).
    /// Returns 0 when no notches exist.
    /// </summary>
    Task<int> GetMaxNotchNumberAsync(Guid tenantId, Guid salaryLevelId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new salary notch.
    /// </summary>
    Task<SalaryNotch> AddAsync(SalaryNotch entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing salary notch.
    /// </summary>
    Task UpdateAsync(SalaryNotch entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Hard deletes a salary notch (allowed for this module).
    /// </summary>
    Task HardDeleteAsync(SalaryNotch entity, CancellationToken cancellationToken = default);
}
