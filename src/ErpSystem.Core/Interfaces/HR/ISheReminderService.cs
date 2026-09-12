using ErpSystem.Core.DTOs.HR;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// SHE reminder engine (S) — the slice-13 job engine behind FRD §17.
// One sweep evaluates every dated SHE obligation (permit windows, review and
// inspection due dates, certificate expiries, PPE stock), auto-expires lapsed
// permits and risk assessments, and queues due-soon/overdue notifications
// through the notification-topic pipeline with a send-once dedupe guarantee.
// ============================================================================

public interface ISheReminderService
{
    /// <summary>
    /// Runs one sweep for one tenant. Called by the background service (per tenant,
    /// trigger "Scheduled") and by the run-now endpoint (trigger "Manual"). Does not
    /// read the current user — the caller supplies tenant and actor explicitly.
    /// </summary>
    Task<SheReminderRunResultDto> RunSweepForTenantAsync(Guid tenantId, string trigger, Guid? triggeredByUserId, CancellationToken cancellationToken = default);

    /// <summary>Recent sweep runs for the authenticated tenant, newest first.</summary>
    Task<IEnumerable<SheReminderRunDto>> GetRecentRunsAsync(int count = 20, CancellationToken cancellationToken = default);

    /// <summary>Reminders dispatched in the last <paramref name="days"/> days for the authenticated tenant, newest first.</summary>
    Task<IEnumerable<SheReminderLogEntryDto>> GetRecentLogAsync(int days = 14, CancellationToken cancellationToken = default);
}
