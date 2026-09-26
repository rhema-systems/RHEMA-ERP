using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Api.Controllers.HR;

// ============================================================================
// RECRUITMENT DASHBOARD — RESPONSE DTOS
// Property names intentionally mirror the Blazor view-model classes
// (RecruitmentDashboardData, RecruitmentSummaryModel, etc.) so the frontend
// service can ReadFromJsonAsync<RecruitmentDashboardData> without any mapping.
// ============================================================================

public sealed class RecruitmentDashboardDto
{
    public DashboardSummaryDto                   Summary                      { get; set; } = new();
    public List<DashboardPipelineStageDto>        PipelineStages               { get; set; } = new();
    public List<DashboardApplicationDto>          RecentApplications           { get; set; } = new();
    public List<DashboardInterviewDto>            UpcomingInterviews           { get; set; } = new();
    public List<DashboardSlaAlertDto>             SlaAlerts                    { get; set; } = new();
    public List<DashboardHireDto>                 HiresStartingSoon            { get; set; } = new();
    public List<DashboardExpiringOfferDto>        ExpiringOffers               { get; set; } = new();
    public List<DashboardDeadlineVacancyDto>      DeadlineApproachingVacancies { get; set; } = new();
}

/// <summary>Matches RecruitmentSummaryModel.</summary>
public sealed class DashboardSummaryDto
{
    public int TotalVacancies      { get; set; }
    public int ActiveApplications  { get; set; }
    public int InterviewsScheduled { get; set; }
    public int OffersMade          { get; set; }
}

/// <summary>Matches PipelineStageSummaryModel.</summary>
public sealed class DashboardPipelineStageDto
{
    public string StageName { get; set; } = string.Empty;
    public int    Count     { get; set; }
}

/// <summary>Matches RecentApplicationModel.</summary>
public sealed class DashboardApplicationDto
{
    public Guid     ApplicationId { get; set; }
    public string   CandidateName { get; set; } = string.Empty;
    public string   VacancyTitle  { get; set; } = string.Empty;
    public string   StageName     { get; set; } = string.Empty;
    public string   StageCssClass { get; set; } = string.Empty;
    public DateTime DateApplied   { get; set; }
}

/// <summary>Matches UpcomingInterviewModel.</summary>
public sealed class DashboardInterviewDto
{
    public Guid     InterviewId       { get; set; }

    /// <summary>
    /// Null — an interview is a session that can hold several candidates, so there is no single
    /// name to give. Use <see cref="CandidateCount"/>.
    /// </summary>
    /// <remarks>
    /// Made nullable 2026-09-15 (G-15.3), when it stopped carrying the literal text "Round N".
    /// Kept on the DTO rather than removed because this shape mirrors the view model the frontend
    /// deserializes into; dropping a property is a client change, and the defect was the nonsense
    /// value, not the field.
    /// </remarks>
    public string?  CandidateName     { get; set; }

    public string   VacancyTitle      { get; set; } = string.Empty;
    public DateTime InterviewDateTime { get; set; }
    public string   InterviewType     { get; set; } = string.Empty;
    public string   TypeCssClass      { get; set; } = string.Empty;
    public int      CandidateCount    { get; set; }
    public int      Round             { get; set; }
}

/// <summary>Matches SlaAlertModel.</summary>
public sealed class DashboardSlaAlertDto
{
    public Guid      VacancyId           { get; set; }
    public string    VacancyNumber       { get; set; } = string.Empty;
    public string    JobTitle            { get; set; } = string.Empty;
    public DateTime? ApplicationDeadline { get; set; }
}

/// <summary>Matches HireStartingSoonModel.</summary>
public sealed class DashboardHireDto
{
    public Guid     HireId            { get; set; }
    public string   HireNumber        { get; set; } = string.Empty;
    public string   CandidateName     { get; set; } = string.Empty;
    public string   PositionTitle     { get; set; } = string.Empty;
    public DateOnly ExpectedStartDate { get; set; }
    public string   StatusName        { get; set; } = string.Empty;
}

/// <summary>Matches ExpiringOfferModel.</summary>
public sealed class DashboardExpiringOfferDto
{
    public Guid      OfferId       { get; set; }
    public string    OfferNumber   { get; set; } = string.Empty;
    public string    CandidateName { get; set; } = string.Empty;
    public string    PositionTitle { get; set; } = string.Empty;
    public DateTime? ExpiryDate    { get; set; }
}

/// <summary>Matches DeadlineApproachingVacancyModel.</summary>
public sealed class DashboardDeadlineVacancyDto
{
    public Guid      VacancyId           { get; set; }
    public string    VacancyNumber       { get; set; } = string.Empty;
    public string    JobTitle            { get; set; } = string.Empty;
    public string?   RecruiterName       { get; set; }
    public int       ApplicationCount    { get; set; }
    public DateTime? ApplicationDeadline { get; set; }
    public int       DaysRemaining       { get; set; }
}

// ============================================================================
// CONTROLLER
// ============================================================================

/// <summary>
/// Aggregated recruitment KPIs. HR-gated: the payload carries candidate names on recent
/// applications, upcoming interviews, offers and hires starting soon — the same PII shape that
/// <see cref="JobApplicationController"/> restricts to HR, so a bare <c>[Authorize]</c> here would
/// have let any authenticated employee read it.
///
/// <para>The <c>InternalOnly</c> pairing was added 2026-09-15 (G-15.5). Nothing was exposed
/// without it — <c>HR.Recruitment.Read</c> is held by no external role — but every other
/// recruitment controller pairs its permission policy with the blocklist, and this one stood
/// alone. Defence in depth is only a pattern if it is applied everywhere; a later change to the
/// permission map would have had one fewer thing standing in its way here.</para>
/// </summary>
[ApiController]
[Route("api/recruitment-dashboard")]
[Authorize(Policy = "InternalOnly")]
[Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
public class RecruitmentDashboardController : ControllerBase
{
    private readonly IJobVacancyService     _vacancyService;
    private readonly IJobInterviewService   _interviewService;
    private readonly IJobOfferService       _offerService;
    private readonly IJobHireService        _hireService;
    private readonly IJobApplicationService _applicationService;
    private readonly IRecruitmentAnalyticsService _analyticsService;
    private readonly IRecruitmentLifecycleSweepService _sweepService;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<RecruitmentDashboardController> _logger;

    public RecruitmentDashboardController(
        IJobVacancyService     vacancyService,
        IJobInterviewService   interviewService,
        IJobOfferService       offerService,
        IJobHireService        hireService,
        IJobApplicationService applicationService,
        IRecruitmentAnalyticsService analyticsService,
        IRecruitmentLifecycleSweepService sweepService,
        ICurrentUserService currentUser,
        ILogger<RecruitmentDashboardController> logger)
    {
        _vacancyService     = vacancyService;
        _interviewService   = interviewService;
        _offerService       = offerService;
        _hireService        = hireService;
        _applicationService = applicationService;
        _analyticsService   = analyticsService;
        _sweepService       = sweepService;
        _currentUser        = currentUser;
        _logger             = logger;
    }

    /// <summary>
    /// Returns a fully aggregated recruitment dashboard payload in a single request.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(RecruitmentDashboardDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboard(CancellationToken ct)
    {
        try
        {
            var today = DateTime.Today;

            // Run queries sequentially — EF Core DbContext is not thread-safe;
            // concurrent queries on the same scoped instance cause concurrency errors.
            var activeVacancies        = (await _vacancyService.GetActiveVacanciesAsync(ct)).ToList();
            var interviews              = (await _interviewService.GetByDateRangeAsync(today, today.AddDays(7), ct)).ToList();
            // ⚠ G-15.2 (2026-09-15): "Offers pending response" was a bare count of everything at
            // Sent, with no IsLatestVersion filter and no expiry bound — so it included offers
            // whose expiry passed long ago (because nothing ever wrote Expired, G-2.4) and v1
            // offers left in Sent by a revision (because ReviseOfferAsync never changed their
            // status, G-10.2). A tile labelled "pending response" grew monotonically and never
            // fell. Both writers are fixed, and the reader filters as well, because the analytics
            // screen — the one reader that got this right — is right precisely because it does not
            // trust the statuses.
            var pendingOffers          = (await _offerService.GetByStatusAsync(JobOfferStatus.Sent, ct))
                                            .Where(o => o.IsLatestVersion)
                                            .ToList();
            var expiringOffers         = (await _offerService.GetExpiringOffersAsync(7, ct)).ToList();
            var recentApps             = (await _applicationService.GetPagedAsync(1, 10, null, ct)).Items.ToList();
            var hires                  = (await _hireService.GetWithStartDateApproachingAsync(14, ct)).ToList();
            var deadlineVacancies      = (await _vacancyService.GetWithDeadlineApproachingAsync(7, ct)).ToList();

            var todayOnly = DateOnly.FromDateTime(today);

            // ── G-15.1 (2026-09-15): four bars, one unit, one window ──────────────────────────
            // The Interviewing bar used to be `interviews.Count` — a count of interview SESSIONS,
            // drawn on a shared scale beside three counts of PEOPLE. An interview is a session that
            // can hold many candidates, so the middle of the funnel counted something different in
            // kind from its neighbours. The window differed too: three bars were all-time totals on
            // currently-active vacancies while this one was sessions scheduled in the next seven
            // days, so a vacancy that interviewed forty people last month contributed nothing.
            //
            // It now uses the vacancy's own InterviewCount aggregate, which is maintained by the
            // same stage movement that maintains ApplicationCount, ShortlistedCount and OfferCount.
            // All four are now people, all-time, on the same set of vacancies — which is what makes
            // the shape of the funnel mean anything, and the shape is the card's entire purpose.
            var pipeline = new List<DashboardPipelineStageDto>
            {
                new() { StageName = "Applied",     Count = activeVacancies.Sum(v => v.ApplicationCount) },
                new() { StageName = "Shortlisted", Count = activeVacancies.Sum(v => v.ShortlistedCount) },
                new() { StageName = "Interviewing",Count = activeVacancies.Sum(v => v.InterviewCount) },
                new() { StageName = "Offer",       Count = activeVacancies.Sum(v => v.OfferCount) },
            };

            // ── G-15.4 (2026-09-15): the SLA card fires on the deadline it is named for ───────
            // The heading reads "Shortlisting SLA breached" and each row says "Deadline was {date}",
            // and the condition was `v.ApplicationDeadline < today` — the date applications CLOSE,
            // not the date shortlisting is due. A vacancy carries a separate ShortlistingDeadline,
            // which the screening screen already uses.
            //
            // So the card fired the day applications closed, on every open vacancy, whether or not
            // shortlisting was actually late — and stayed lit for the rest of the vacancy's life,
            // since the deadline only recedes further into the past. It is the loudest element on
            // the page and it was measuring the wrong date.
            //
            // A vacancy with no shortlisting deadline set now raises no alert at all, which is
            // right: an SLA nobody agreed cannot be breached.
            var slaAlerts = activeVacancies
                .Where(v => v.ShortlistingDeadline.HasValue && v.ShortlistingDeadline.Value < today)
                .Select(v => new DashboardSlaAlertDto
                {
                    VacancyId           = v.Id,
                    VacancyNumber       = v.VacancyNumber,
                    JobTitle            = v.JobTitle,
                    ApplicationDeadline = v.ShortlistingDeadline,
                })
                .ToList();

            var dto = new RecruitmentDashboardDto
            {
                Summary = new DashboardSummaryDto
                {
                    TotalVacancies      = activeVacancies.Count,
                    ActiveApplications  = activeVacancies.Sum(v => v.ApplicationCount),
                    InterviewsScheduled = interviews.Count(i => i.ScheduledDate == todayOnly),
                    OffersMade          = pendingOffers.Count,
                },

                PipelineStages = pipeline,

                RecentApplications = recentApps.Select(a => new DashboardApplicationDto
                {
                    ApplicationId = a.Id,
                    CandidateName = a.CandidateName,
                    VacancyTitle  = a.JobTitle,
                    StageName     = a.StatusName,
                    StageCssClass = a.Status.ToString().ToLowerInvariant(),
                    DateApplied   = a.ApplicationDate,
                }).ToList(),

                UpcomingInterviews = interviews.Take(5).Select(i => new DashboardInterviewDto
                {
                    InterviewId       = i.Id,
                    // ⚠ G-15.3 (2026-09-15): this read `CandidateName = $"Round {i.Round}"` — a
                    // round number in a name field, with the round ALSO carried correctly in
                    // `Round` just below. Nothing was visibly wrong because the page's table is
                    // Vacancy / Round / Type / When and has no Candidate column, so the value was
                    // never rendered. But the payload was wrong, any future consumer reading
                    // CandidateName got nonsense, and the controller's own docstring cites
                    // "candidate names on… upcoming interviews" as part of the justification for
                    // the HR gate — which, for this list, is not what it carried.
                    //
                    // An interview is a session that can hold several candidates, so there is no
                    // single candidate name to give. Null is the honest answer; CandidateCount
                    // already tells the reader how many people are in the session.
                    CandidateName     = null,
                    VacancyTitle      = i.JobTitle,
                    InterviewDateTime = i.ScheduledDate.ToDateTime(TimeOnly.MinValue) + i.StartTime,
                    InterviewType     = i.TypeName,
                    TypeCssClass      = i.Type.ToString().ToLowerInvariant(),
                    CandidateCount    = i.IntervieweeCount,
                    Round             = i.Round,
                }).ToList(),

                SlaAlerts = slaAlerts,

                HiresStartingSoon = hires.Take(10).Select(h => new DashboardHireDto
                {
                    HireId            = h.Id,
                    HireNumber        = h.HireNumber,
                    CandidateName     = h.CandidateName,
                    PositionTitle     = h.PositionTitle,
                    ExpectedStartDate = h.ExpectedStartDate,
                    StatusName        = h.StatusName,
                }).ToList(),

                ExpiringOffers = expiringOffers.Take(5).Select(o => new DashboardExpiringOfferDto
                {
                    OfferId       = o.Id,
                    OfferNumber   = o.OfferNumber,
                    CandidateName = o.CandidateName,
                    PositionTitle = o.PositionTitle,
                    ExpiryDate    = o.ExpiryDate,
                }).ToList(),

                DeadlineApproachingVacancies = deadlineVacancies.Take(10).Select(v => new DashboardDeadlineVacancyDto
                {
                    VacancyId           = v.Id,
                    VacancyNumber       = v.VacancyNumber,
                    JobTitle            = v.JobTitle,
                    RecruiterName       = v.RecruiterName,
                    ApplicationCount    = v.ApplicationCount,
                    ApplicationDeadline = v.ApplicationDeadline,
                    DaysRemaining       = v.ApplicationDeadline.HasValue
                        ? Math.Max(0, (v.ApplicationDeadline.Value.Date - today).Days)
                        : 0,
                }).ToList(),
            };

            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building recruitment dashboard");
            return StatusCode(500, "An error occurred while loading the recruitment dashboard.");
        }
    }

    /// <summary>
    /// Recruitment analytics for a year — speed, cost, funnel, source effectiveness,
    /// offer outcomes, vacancy ageing and recruiter load. Defaults to the current year.
    /// </summary>
    [HttpGet("analytics")]
    [ProducesResponseType(typeof(RecruitmentAnalyticsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAnalytics([FromQuery] int? year, CancellationToken ct)
    {
        try
        {
            return Ok(await _analyticsService.GetAnalyticsAsync(year, ct));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building recruitment analytics for year {Year}", year);
            return StatusCode(500, "An error occurred while loading recruitment analytics.");
        }
    }

    /// <summary>
    /// Runs the recruitment lifecycle sweep now, for the caller's tenant.
    /// </summary>
    /// <remarks>
    /// <para>The same code the nightly host runs — the host/processor split every other HR engine
    /// uses. It exists for three reasons: so the sweep can be proved to work without waiting a day,
    /// so a tenant that has been drifting can be brought current on demand, and so a harness has
    /// something deterministic to assert against.</para>
    ///
    /// <para>Gated on <c>Admin</c> rather than <c>Write</c>: it changes the status of records
    /// across the whole tenant at once, which is an administrative act even though each individual
    /// write is one the clock would have made anyway.</para>
    /// </remarks>
    [HttpPost("sweep")]
    [Authorize(Policy = HrPermissions.RecruitmentAdminPolicy)]
    [ProducesResponseType(typeof(RecruitmentSweepResultDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> RunSweep(CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId;
        if (tenantId == null) return BadRequest(new { message = "Tenant context could not be resolved." });

        try
        {
            // EmployeeId, not UserId: LastModifiedById on these records is an Employee FK
            // throughout this area (see hr-attendance-actor-conventions). The scheduled run passes
            // null, which is the honest answer for a write nobody made.
            var result = await _sweepService.RunSweepForTenantAsync(
                tenantId.Value, "Manual", _currentUser.EmployeeId, ct);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running the recruitment lifecycle sweep");
            return StatusCode(500, "An error occurred while running the recruitment sweep.");
        }
    }
}
