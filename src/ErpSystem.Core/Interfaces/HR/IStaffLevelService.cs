using ErpSystem.Core.DTOs.HR;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// Application service for Staff Level operations.
///
/// This service represents the application boundary for the Staff Level module and is intended
/// to be consumed by API controllers (and UI clients).
///
/// All operations are tenant-scoped; the caller must provide the current tenant identifier.
/// </summary>
public interface IStaffLevelService
{
    /// <summary>
    /// Gets all staff levels for a tenant ordered by rank.
    /// </summary>
    Task<IReadOnlyList<StaffLevelListDto>> GetAllAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets active staff levels only for a tenant ordered by rank.
    /// </summary>
    Task<IReadOnlyList<StaffLevelListDto>> GetActiveAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a staff level by identifier.
    /// </summary>
    Task<StaffLevelDto> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a staff level by business code.
    /// </summary>
    Task<StaffLevelDto> GetByCodeAsync(Guid tenantId, string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a staff level detail view including usage counts.
    /// </summary>
    Task<StaffLevelDetailDto> GetDetailAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new staff level.
    /// Enforces unique Rank and (when provided) unique Code per tenant.
    /// </summary>
    Task<StaffLevelDto> CreateAsync(Guid tenantId, CreateStaffLevelDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing staff level.
    /// Enforces unique Rank and (when provided) unique Code per tenant.
    /// </summary>
    Task<StaffLevelDto> UpdateAsync(Guid tenantId, UpdateStaffLevelDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Activates a staff level.
    /// Operation is idempotent.
    /// </summary>
    Task<StaffLevelDto> ActivateAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deactivates a staff level.
    /// Operation is idempotent.
    /// </summary>
    Task<StaffLevelDto> DeactivateAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft deletes a staff level.
    /// Deletion is blocked when the staff level is referenced by any employee position.
    /// </summary>
    Task<bool> DeleteAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);
}
