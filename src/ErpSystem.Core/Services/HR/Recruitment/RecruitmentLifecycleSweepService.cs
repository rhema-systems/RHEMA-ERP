using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.HR.Recruitment;

/// <inheritdoc cref="IRecruitmentLifecycleSweepService"/>
public sealed class RecruitmentLifecycleSweepService : IRecruitmentLifecycleSweepService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RecruitmentLifecycleSweepService> _logger;

    public RecruitmentLifecycleSweepService(
        IUnitOfWork unitOfWork,
        ILogger<RecruitmentLifecycleSweepService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<RecruitmentSweepResultDto> RunSweepForTenantAsync(
        Guid tenantId,
        string trigger,
        Guid? triggeredByUserId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var result = new RecruitmentSweepResultDto
        {
            OffersExpired    = await ExpireOffersAsync(tenantId, now, triggeredByUserId, cancellationToken),
            PostingsExpired  = await ExpirePostingsAsync(tenantId, now, triggeredByUserId, cancellationToken),
            VacanciesOpened  = await OpenDueVacanciesAsync(tenantId, now, triggeredByUserId, cancellationToken),
        };

        if (result.TotalChanged > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Recruitment lifecycle sweep ({Trigger}) for tenant {TenantId}: " +
                "{Offers} offer(s) expired, {Postings} advert(s) expired, {Vacancies} anticipated vacancy(ies) opened.",
                trigger, tenantId, result.OffersExpired, result.PostingsExpired, result.VacanciesOpened);
        }
        else
        {
            _logger.LogDebug(
                "Recruitment lifecycle sweep ({Trigger}) for tenant {TenantId}: nothing was due.",
                trigger, tenantId);
        }

        return result;
    }

    /// <summary>
    /// An offer whose expiry date has passed with no answer is <c>Expired</c> (G-2.4).
    /// </summary>
    /// <remarks>
    /// <para><b>Only <c>Sent</c>.</b> <c>Negotiating</c> deliberately does not expire: somebody is
    /// actively talking to the candidate, and a clock running out underneath a live conversation
    /// would be worse than the gap. Nor does <c>ConditionallyAccepted</c> — the candidate has said
    /// yes and the wait is on pre-employment checks, which have their own queue. The lapsed
    /// <i>unanswered</i> offer is the one the tiles were counting for ever.</para>
    ///
    /// <para><b>Superseded versions are swept too</b>, and that is the point of not filtering on
    /// <c>IsLatestVersion</c> here. G-10.2 closes the writer so a revision no longer leaves its
    /// predecessor in <c>Sent</c>, but every offer revised before this shipped is already sitting
    /// in that state, and nothing else will ever move it.</para>
    /// </remarks>
    private async Task<int> ExpireOffersAsync(
        Guid tenantId, DateTime now, Guid? actingUserId, CancellationToken cancellationToken)
    {
        var repo = _unitOfWork.Repository<JobOffer>();

        var lapsed = await repo.GetQueryable()
            .Where(o => o.TenantId == tenantId
                     && !o.IsDeleted
                     && o.OfferStatus == JobOfferStatus.Sent
                     && o.ExpiryDate != null
                     && o.ExpiryDate < now)
            .ToListAsync(cancellationToken);

        foreach (var offer in lapsed)
        {
            offer.OfferStatus = JobOfferStatus.Expired;
            offer.UpdatedAt = now;
            offer.LastModifiedById = actingUserId;
            await repo.UpdateAsync(offer);
        }

        return lapsed.Count;
    }

    /// <summary>
    /// An advert past its closing date is <c>Expired</c> and no longer active (G-6.2).
    /// </summary>
    /// <remarks>
    /// This is the same write <c>JobVacancyService.ExpirePostingsIfUnpublishedAsync</c> makes when
    /// a vacancy leaves Published — deliberately, so a posting reaches the same resting state
    /// whichever way it gets there. The repository already had
    /// <c>GetExpiredActivePostingsAsync</c> finding exactly these rows, which is direct evidence
    /// the condition was known to occur; nothing acted on it.
    /// </remarks>
    private async Task<int> ExpirePostingsAsync(
        Guid tenantId, DateTime now, Guid? actingUserId, CancellationToken cancellationToken)
    {
        var repo = _unitOfWork.Repository<JobPosting>();

        var overdue = await repo.GetQueryable()
            .Where(p => p.TenantId == tenantId
                     && !p.IsDeleted
                     && p.ExpiryDate != null
                     && p.ExpiryDate < now
                     && (p.IsActive || p.Status == JobPostingStatus.Published))
            .ToListAsync(cancellationToken);

        foreach (var posting in overdue)
        {
            posting.Status = JobPostingStatus.Expired;
            posting.IsActive = false;
            posting.UpdatedAt = now;
            posting.LastModifiedById = actingUserId;
            await repo.UpdateAsync(posting);
        }

        return overdue.Count;
    }

    /// <summary>
    /// An anticipated vacancy whose expected date has arrived becomes <c>Open</c>.
    /// </summary>
    /// <remarks>
    /// <para>The companion to G-3.6. <c>PositionVacancyLog</c> opens an <c>Anticipated</c> row when
    /// notice is given, and promotes it to <c>Open</c> when the departure is actually applied. But
    /// the departure is applied by a person completing a separation, and that can happen days after
    /// the employee has gone — or not at all, if the paperwork lags. Without this, a seat that fell
    /// empty on Friday reads as "about to fall empty" until somebody files the last form.</para>
    ///
    /// <para>The clock is the honest arbiter of a date that has passed, which is the whole reason
    /// this service exists.</para>
    /// </remarks>
    private async Task<int> OpenDueVacanciesAsync(
        Guid tenantId, DateTime now, Guid? actingUserId, CancellationToken cancellationToken)
    {
        var repo = _unitOfWork.Repository<PositionVacancy>();

        var due = await repo.GetQueryable()
            .Where(v => v.TenantId == tenantId
                     && !v.IsDeleted
                     && v.Status == PositionVacancyStatus.Anticipated
                     && v.ExpectedVacancyDate != null
                     && v.ExpectedVacancyDate <= now)
            .ToListAsync(cancellationToken);

        foreach (var vacancy in due)
        {
            vacancy.Status = PositionVacancyStatus.Open;
            vacancy.IsAnticipated = false;
            vacancy.UpdatedAt = now;
            vacancy.LastModifiedById = actingUserId;
            await repo.UpdateAsync(vacancy);
        }

        return due.Count;
    }
}
