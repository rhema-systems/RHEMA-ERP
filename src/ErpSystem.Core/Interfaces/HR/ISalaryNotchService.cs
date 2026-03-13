using ErpSystem.Core.DTOs.HR;

namespace ErpSystem.Core.Interfaces.HR.Services;

/// <summary>
/// Service contract for Salary Notch operations (tenant-aware).
/// </summary>
public interface ISalaryNotchService
{
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
}
