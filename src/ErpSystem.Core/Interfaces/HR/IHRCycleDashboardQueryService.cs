using ErpSystem.Core.DTOs.HR;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// Aggregates all data needed to render the HR Cycle Dashboard, and the one action that screen
/// can take: nudging a single stalled appraisal.
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

    /// <summary>
    /// Raises an in-app notification for whoever owes the appraisal's current step.
    ///
    /// <para>Deliberately narrower than the cycle's deadline reminders, which go to everyone in
    /// scope for every live phase. This is one appraisal, and it reaches only the people who can
    /// actually clear it: the appraisee, the peers whose forms are outstanding, or the manager.</para>
    ///
    /// <para>Throws <see cref="ArgumentException"/> when the appraisal is not in the cycle, and
    /// <see cref="InvalidOperationException"/> when the step is not one an individual can be
    /// nudged about — a finished appraisal, or one waiting on calibration or HR itself.</para>
    /// </summary>
    Task<HRCycleNudgeResultDto> NudgeAsync(Guid cycleId, Guid appraisalId, CancellationToken ct = default);
}
