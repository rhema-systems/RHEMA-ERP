using ErpSystem.Core.DTOs.HR;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// The separation reminder sweep (FR-HR-111) — what is due, and what has gone unanswered.
/// </summary>
/// <remarks>
/// Five kinds: a retirement or a contract expiry approaching with no separation raised, a clearance
/// with mandatory lines unanswered, a settlement sitting with Internal Audit, and a settlement
/// approved but never completed. The last is the defect this area was opened on, turned into a
/// reminder rather than a discovery.
/// </remarks>
public interface ISeparationReminderService
{
    /// <summary>
    /// What the sweep would raise, without writing anything — including whether each reminder has
    /// already gone out at its current escalation tier.
    /// </summary>
    Task<IEnumerable<SeparationReminderItemDto>> PreviewAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs a pass: records the run, writes a dispatch row per reminder, and suppresses anything
    /// already raised at the same tier so a daily sweep does not become noise.
    /// </summary>
    Task<SeparationReminderRunResultDto> RunSweepAsync(
        string trigger = "Manual", CancellationToken cancellationToken = default);

    /// <summary>
    /// The same pass for a named tenant, for callers with no authenticated user — the nightly host,
    /// which loops over every tenant. <see cref="RunSweepAsync"/> is this method with the caller's
    /// own tenant and user filled in.
    /// </summary>
    Task<SeparationReminderRunResultDto> RunSweepForTenantAsync(
        Guid tenantId, string trigger, Guid? triggeredByUserId,
        CancellationToken cancellationToken = default);
}
