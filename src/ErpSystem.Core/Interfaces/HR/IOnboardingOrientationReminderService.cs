using ErpSystem.Core.DTOs.HR;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// The orientation & onboarding reminder engine (round 4, lane K) — the first HR sweep that
/// delivers: an in-app notification to each person, and an email through the templated email
/// service, rather than only a log of what it would have said.
/// </summary>
public interface IOnboardingOrientationReminderService
{
    /// <summary>
    /// Runs one sweep for a tenant: claims each fresh item, writes one notification per person, and
    /// emails it. Safe to repeat — an item is claimed once per due date and escalation tier.
    /// By tenant, not by current user: this is the nightly host's path as well as the run-now button's.
    /// </summary>
    Task<OnboardingOrientationReminderRunResultDto> RunSweepForTenantAsync(
        Guid tenantId, string trigger, Guid? triggeredByUserId, CancellationToken cancellationToken = default);

    /// <summary>What a sweep would remind, as at <paramref name="asOf"/>. Claims and sends nothing.</summary>
    Task<IEnumerable<OnboardingOrientationReminderPreviewItemDto>> PreviewSweepAsync(
        DateTime? asOf, CancellationToken cancellationToken = default);

    Task<IEnumerable<OnboardingOrientationReminderRunDto>> GetRecentRunsAsync(
        int count = 20, CancellationToken cancellationToken = default);

    Task<IEnumerable<OnboardingOrientationReminderLogEntryDto>> GetRecentLogAsync(
        int days = 14, CancellationToken cancellationToken = default);
}
