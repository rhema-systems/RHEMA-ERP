namespace ErpSystem.Core.Interfaces.HR;

/// <summary>What one recruitment lifecycle sweep changed.</summary>
public sealed class RecruitmentSweepResultDto
{
    /// <summary>Offers taken from <c>Sent</c> to <c>Expired</c> because their expiry date passed.</summary>
    public int OffersExpired { get; set; }

    /// <summary>Adverts taken to <c>Expired</c> and deactivated because their closing date passed.</summary>
    public int PostingsExpired { get; set; }

    /// <summary>Anticipated vacancies whose expected date has arrived, promoted to <c>Open</c>.</summary>
    public int VacanciesOpened { get; set; }

    /// <summary>
    /// Test sittings left running past their deadline, marked on what was saved and closed
    /// (round 4, lane E).
    /// </summary>
    /// <remarks>
    /// ⚠ A candidate who closes the tab and never comes back leaves a sitting <c>InProgress</c> for
    /// ever. Without this it stays in HR's marking queue as work outstanding, and the attempt is
    /// never released either — so the candidate cannot legitimately be given another go.
    /// </remarks>
    public int SittingsExpired { get; set; }

    public int TotalChanged => OffersExpired + PostingsExpired + VacanciesOpened + SittingsExpired;
}

/// <summary>
/// The one thing in recruitment that happens because a <b>date passed</b> rather than because
/// somebody pressed a button.
/// </summary>
/// <remarks>
/// <para><b>Why this exists (G-2.4, G-6.2, 2026-09-15).</b> Appendix C of
/// <c>docs/HR/areas/recruitment/HR-RECRUITMENT-SYSTEM-GUIDE.md</c> named this as its third pattern: *date-driven
/// statuses that only a human can write*. There was <b>no scheduled job anywhere in the
/// recruitment module</b>, and two statuses depended on one:</para>
///
/// <list type="bullet">
///   <item><description><c>JobOfferStatus.Expired</c> was <b>never written anywhere in the
///   solution</b>. An offer that lapsed stayed <c>Sent</c> for ever. The landing page's "offers
///   expiring soon" tile has no lower bound, so every lapsed offer accumulated in it permanently
///   and the tile was amber for good once the first one passed (G-2.4); the analytics screen's
///   Expired bucket was structurally zero while the lapsed offers inflated <i>Pending</i>
///   (G-14.1); and the dashboard's "offers pending response" grew monotonically and never fell
///   (G-15.2).</description></item>
///   <item><description>Nothing expired an advert on its closing date — only a manual click or a
///   vacancy status change (G-6.2). The adverts screen has a whole view for the consequence,
///   *"Past expiry but still live"*, which made the overdue set not an edge case but the normal
///   resting state of any advert whose deadline had passed.</description></item>
/// </list>
///
/// <para><b>The sweep writes the statuses; the readers are bounded anyway.</b> Both halves were
/// done deliberately. A missed sweep — a host down overnight, a tenant added between runs — must
/// not be able to inflate a tile again, so the queries behind those counters now filter for
/// themselves as well. Belt and braces, because the failure mode being designed against is
/// silence: nobody noticed the absence of this job for the life of the module.</para>
///
/// <para><b>Shape follows the other HR engines.</b> The logic is here, in a scoped service, rather
/// than in the <c>BackgroundService</c>, so the HR-gated run-now endpoint exercises exactly the
/// path the host does — the same host/processor split probation, discipline, separation, assets
/// and travel use. Two HR sweeps in this repo were once registered nowhere and ran only when
/// somebody pressed the button, unnoticed for months; keeping run-now on the same code is what
/// makes that discoverable.</para>
/// </remarks>
public interface IRecruitmentLifecycleSweepService
{
    /// <summary>
    /// Advances every recruitment record whose date has passed, for one tenant.
    /// </summary>
    /// <param name="tenantId">The tenant to sweep.</param>
    /// <param name="trigger">"Scheduled" or "Manual" — recorded in the log line.</param>
    /// <param name="triggeredByUserId">Null for the scheduled run.</param>
    Task<RecruitmentSweepResultDto> RunSweepForTenantAsync(
        Guid tenantId,
        string trigger,
        Guid? triggeredByUserId,
        CancellationToken cancellationToken = default);
}
