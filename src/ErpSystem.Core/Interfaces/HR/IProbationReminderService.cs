using ErpSystem.Core.DTOs.HR;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// The probation reminder engine: FR-HR-032's month-5 confirmation form and FR-HR-140's expiry
/// notice, plus the review queues that keep either from being reached blind.
/// </summary>
public interface IProbationReminderService
{
    /// <summary>Runs the sweep for one tenant and records what it queued.</summary>
    Task<ProbationReminderRunResultDto> RunSweepForTenantAsync(
        Guid tenantId, string trigger, Guid? triggeredByUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// What a sweep run at <paramref name="asOf"/> would fire. Reads only — claims nothing.
    /// </summary>
    /// <remarks>
    /// The date is a parameter rather than "now" so the ladder can be tested at a chosen point
    /// without waiting for the calendar, and so HR can answer "what lands next month?". Because a
    /// preview claims no dedupe keys, running one never suppresses the real sweep.
    /// </remarks>
    Task<IEnumerable<ProbationReminderPreviewItemDto>> PreviewSweepAsync(
        DateTime? asOf, CancellationToken cancellationToken = default);

    Task<IEnumerable<ProbationReminderRunDto>> GetRecentRunsAsync(int count = 20, CancellationToken cancellationToken = default);

    Task<IEnumerable<ProbationReminderLogEntryDto>> GetRecentLogAsync(int days = 14, CancellationToken cancellationToken = default);
}
