using ErpSystem.Core.Entities.HR;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// Staff level repository.
/// Extends the generic repository with StaffLevel-specific, tenant-scoped queries.
/// </summary>
public interface IStaffLevelRepository : IGenericRepository<StaffLevel>
{
    /// <summary>
    /// Gets all staff levels for a tenant ordered by <c>Rank</c>.
    /// </summary>
    Task<IReadOnlyList<StaffLevel>> GetAllOrderedByRankAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets only active staff levels for a tenant ordered by <c>Rank</c>.
    /// </summary>
    Task<IReadOnlyList<StaffLevel>> GetActiveOrderedByRankAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a staff level by business code (tenant-scoped).
    /// </summary>
    Task<StaffLevel?> GetByCodeAsync(Guid tenantId, string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether a given <paramref name="rank"/> already exists for the tenant.
    /// Optionally excludes an existing record (used during updates).
    /// </summary>
    Task<bool> RankExistsAsync(Guid tenantId, int rank, Guid? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether a given <paramref name="code"/> already exists for the tenant.
    /// Comparison is case-insensitive and trims whitespace.
    /// Optionally excludes an existing record (used during updates).
    /// </summary>
    Task<bool> CodeExistsAsync(Guid tenantId, string code, Guid? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether a staff level is in use.
    /// A staff level is considered in use if any non-deleted <see cref="EmployeePosition"/> references it.
    /// </summary>
    Task<bool> IsInUseAsync(Guid tenantId, Guid staffLevelId, CancellationToken cancellationToken = default);
}
