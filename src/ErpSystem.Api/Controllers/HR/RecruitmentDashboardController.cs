using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
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
    public string   CandidateName     { get; set; } = string.Empty;
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
/// </summary>
[ApiController]
[Route("api/recruitment-dashboard")]
[Authorize(Policy = HrPermissions.RecruitmentReadPolicy)]
public class RecruitmentDashboardController : ControllerBase
{
    private readonly IJobVacancyService     _vacancyService;
    private readonly IJobInterviewService   _interviewService;
    private readonly IJobOfferService       _offerService;
    private readonly IJobHireService        _hireService;
    private readonly IJobApplicationService _applicationService;
    private readonly IRecruitmentAnalyticsService _analyticsService;
    private readonly ILogger<RecruitmentDashboardController> _logger;

    public RecruitmentDashboardController(
        IJobVacancyService     vacancyService,
        IJobInterviewService   interviewService,
        IJobOfferService       offerService,
        IJobHireService        hireService,
        IJobApplicationService applicationService,
        IRecruitmentAnalyticsService analyticsService,
        ILogger<RecruitmentDashboardController> logger)
    {
        _vacancyService     = vacancyService;
        _interviewService   = interviewService;
        _offerService       = offerService;
        _hireService        = hireService;
        _applicationService = applicationService;
        _analyticsService   = analyticsService;
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
            var pendingOffers          = (await _offerService.GetByStatusAsync(JobOfferStatus.Sent, ct)).ToList();
            var expiringOffers         = (await _offerService.GetExpiringOffersAsync(7, ct)).ToList();
            var recentApps             = (await _applicationService.GetPagedAsync(1, 10, null, ct)).Items.ToList();
            var hires                  = (await _hireService.GetWithStartDateApproachingAsync(14, ct)).ToList();
            var deadlineVacancies      = (await _vacancyService.GetWithDeadlineApproachingAsync(7, ct)).ToList();

            var todayOnly = DateOnly.FromDateTime(today);

            // Pipeline stages derived from active vacancy aggregates
            var pipeline = new List<DashboardPipelineStageDto>
            {
                new() { StageName = "Applied",     Count = activeVacancies.Sum(v => v.ApplicationCount) },
                new() { StageName = "Shortlisted", Count = activeVacancies.Sum(v => v.ShortlistedCount) },
                new() { StageName = "Interviewing",Count = interviews.Count },
                new() { StageName = "Offer",       Count = activeVacancies.Sum(v => v.OfferCount) },
            };

            // SLA alerts: active vacancies whose ApplicationDeadline has passed
            var slaAlerts = activeVacancies
                .Where(v => v.ApplicationDeadline.HasValue && v.ApplicationDeadline.Value < today)
                .Select(v => new DashboardSlaAlertDto
                {
                    VacancyId           = v.Id,
                    VacancyNumber       = v.VacancyNumber,
                    JobTitle            = v.JobTitle,
                    ApplicationDeadline = v.ApplicationDeadline,
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
                    CandidateName     = $"Round {i.Round}",
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
}
