using ErpSystem.Core.DTOs.HR;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// Sends the emails queued on orientation &amp; onboarding notices (round 4, lane K-b) — the outbox
/// half of <see cref="IOnboardingOrientationNotices"/>. Run every minute by its host, and on demand by
/// HR's "Send queued emails now".
/// </summary>
public interface IOrientationNoticeEmailDispatcher
{
    /// <summary>The tenants with notice emails waiting.</summary>
    Task<IReadOnlyList<Guid>> TenantsWithQueuedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends up to <paramref name="max"/> of a tenant's queued notice emails, oldest first, and records
    /// each one's outcome. Holds the dispatch lock while it runs, so the host and the button can never
    /// send the same email twice; a pass that cannot take it reports <c>Busy</c> and sends nothing.
    /// </summary>
    Task<OrientationNoticeDispatchResultDto> DispatchAsync(
        Guid tenantId, int max = 200, CancellationToken cancellationToken = default);
}
