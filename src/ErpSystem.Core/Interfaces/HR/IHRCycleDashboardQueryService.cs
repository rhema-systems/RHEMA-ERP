using ErpSystem.Core.DTOs.HR;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// Read-only query service that aggregates all data needed to render the HR Cycle Dashboard.
/// Handles the two endpoints exposed by <c>HRCycleDashboardController</c>.
/// </summary>
public interface IHRCycleDashboardQueryService
{
    /// <summary>
    /// Returns the ID of the most recent <c>Open</c> or <c>InProgress</c> appraisal cycle
    /// for the current tenant, or <see cref="Guid.Empty"/> when none exists.
    /// </summary>
    Task<Guid> GetActiveCycleIdAsync(CancellationToken ct = default);

    /// <summary>
    /// Builds the complete, pre-aggregated dashboard payload for the given cycle.
    /// Returns <c>null</c> when the cycle does not exist or belongs to a different tenant.
    /// </summary>
    Task<HRCycleDashboardDto?> BuildDashboardAsync(Guid cycleId, CancellationToken ct = default);
}
