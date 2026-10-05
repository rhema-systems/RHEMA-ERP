using ErpSystem.Api.Models;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// The org-wide PIP dashboard. HR only: it lists every employee in the tenant who is on an
/// improvement plan, which is not a list a line manager or an ordinary user should hold.
/// </summary>
[ApiController]
[Route("api/PipDashboard")]
[Authorize(Policy = HrPermissions.PerformanceReadPolicy)]
public class PipDashboardController : ControllerBase
{
    private readonly IPerformanceImprovementPlanService _pipService;
    private readonly ErpSystem.Data.ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<PipDashboardController> _logger;

    public PipDashboardController(
        IPerformanceImprovementPlanService pipService,
        ErpSystem.Data.ApplicationDbContext db,
        ICurrentUserService currentUserService,
        ILogger<PipDashboardController> logger)
    {
        _pipService = pipService;
        _db         = db;
        _currentUserService = currentUserService;
        _logger     = logger;
    }

    /// <summary>
    /// Get the PIP dashboard with statistics and full list.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PipDashboardResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboard(CancellationToken ct = default)
    {
        if (_currentUserService.TenantId is not Guid tenantId)
            return Unauthorized("Tenant context could not be resolved");

        try
        {
            var allPips  = (await _pipService.GetAllAsync(ct)).ToList();
            var thisYear = DateTime.UtcNow.Year;
            var now      = DateTime.UtcNow;

            // Goal and meeting progress per plan. The list model has always carried these columns
            // and nothing ever filled them, so every row read "0 goals, 0 meetings" — indistinguishable
            // from a plan with nothing on it. Two grouped queries rather than a round trip per row.
            var goalStats = await _db.Set<ErpSystem.Core.Entities.HR.Performance.PipGoal>()
                .AsNoTracking()
                .Where(g => g.TenantId == tenantId && !g.IsDeleted)
                .GroupBy(g => g.PipId)
                .Select(g => new
                {
                    PipId = g.Key,
                    Total = g.Count(),
                    Completed = g.Count(x => x.Status == GoalProgressStatus.Completed),
                    AverageProgress = g.Average(x => (decimal?)x.ProgressPercent) ?? 0m,
                })
                .ToDictionaryAsync(x => x.PipId, ct);

            var meetingStats = await _db.Set<ErpSystem.Core.Entities.HR.Performance.PipReviewMeeting>()
                .AsNoTracking()
                .Where(m => m.TenantId == tenantId && !m.IsDeleted)
                .GroupBy(m => m.PipId)
                .Select(m => new
                {
                    PipId = m.Key,
                    // The stored status (decision D-73): held means recorded as held, and a cancelled
                    // meeting is neither held nor next. Both used to be read off the date.
                    Total = m.Count(x => x.Status != PipMeetingStatus.Cancelled),
                    Held = m.Count(x => x.Status == PipMeetingStatus.Held),
                    Next = m.Where(x => x.Status == PipMeetingStatus.Scheduled && x.MeetingDate > now)
                            .Min(x => (DateTime?)x.MeetingDate),
                })
                .ToDictionaryAsync(x => x.PipId, ct);

            var items = allPips.Select(pip =>
            {
                var totalDays    = Math.Max(1, (int)(pip.EndDate - pip.StartDate).TotalDays);
                var daysElapsed  = (int)Math.Min(totalDays, (now - pip.StartDate).TotalDays);
                var daysRemaining = Math.Max(0, (int)(pip.EndDate - now).TotalDays);
                var periodPct    = Math.Min(100m, (decimal)Math.Max(0, daysElapsed) / totalDays * 100m);
                // Only a plan that is actually running can be late; a draft with an end date in
                // the past has not started, and a closed one ended.
                var isOverdue    = (pip.Status == PipStatus.Active || pip.Status == PipStatus.InProgress)
                                   && pip.EndDate < now;

                goalStats.TryGetValue(pip.Id, out var goals);
                meetingStats.TryGetValue(pip.Id, out var meetings);

                return new PipListItemResponse
                {
                    PipId                = pip.Id,
                    PipNumber            = pip.PipNumber,
                    EmployeeId           = pip.EmployeeId,
                    EmployeeName         = pip.EmployeeName,
                    SupervisorName       = pip.SupervisorName,
                    HROwnerName          = pip.HROwnerName,
                    StartDate            = pip.StartDate,
                    EndDate              = pip.EndDate,
                    TotalDays            = totalDays,
                    DaysRemaining        = daysRemaining,
                    PeriodProgressPercent = periodPct,
                    Status               = pip.Status,
                    Outcome              = pip.Outcome,
                    CompletionDate       = pip.CompletionDate,
                    IsOverdue            = isOverdue,
                    AppraisalId          = pip.AppraisalId,
                    TotalGoals           = goals?.Total ?? 0,
                    CompletedGoals       = goals?.Completed ?? 0,
                    GoalProgressPercent  = goals?.AverageProgress ?? 0m,
                    TotalMeetings        = meetings?.Total ?? 0,
                    CompletedMeetings    = meetings?.Held ?? 0,
                    NextMeetingDate      = meetings?.Next,
                };
            }).ToList();

            var response = new PipDashboardResponse
            {
                PIPs                  = items,
                ActiveCount           = allPips.Count(p => p.Status == PipStatus.Active || p.Status == PipStatus.InProgress),
                DraftCount            = allPips.Count(p => p.Status == PipStatus.Draft),
                PendingApprovalCount  = allPips.Count(p => p.Status == PipStatus.PendingApproval),
                OverdueCount          = items.Count(p => p.IsOverdue),
                CompletedThisYearCount = allPips.Count(p =>
                    p.Status == PipStatus.Completed &&
                    p.CompletionDate.HasValue &&
                    p.CompletionDate.Value.Year == thisYear),
                PassedCount           = allPips.Count(p => p.Outcome == PipOutcome.PerformanceImproved),
                FailedCount           = allPips.Count(p => p.Status == PipStatus.Unsuccessful),
                ExtendedCount         = allPips.Count(p => p.Outcome == PipOutcome.Extended),
                TerminatedCount       = allPips.Count(p => p.Outcome == PipOutcome.Termination),
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading PIP dashboard");
            return StatusCode(500, "An error occurred while loading the PIP dashboard");
        }
    }
}
