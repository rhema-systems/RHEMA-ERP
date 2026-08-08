using ErpSystem.Api.Models;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/PipDashboard")]
[Authorize]
public class PipDashboardController : ControllerBase
{
    private readonly IPerformanceImprovementPlanService _pipService;
    private readonly ILogger<PipDashboardController> _logger;

    public PipDashboardController(
        IPerformanceImprovementPlanService pipService,
        ILogger<PipDashboardController> logger)
    {
        _pipService = pipService;
        _logger     = logger;
    }

    /// <summary>
    /// Get the PIP dashboard with statistics and full list.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PipDashboardResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboard()
    {
        try
        {
            var allPips  = (await _pipService.GetAllAsync()).ToList();
            var thisYear = DateTime.UtcNow.Year;
            var now      = DateTime.UtcNow;

            var items = allPips.Select(pip =>
            {
                var totalDays    = Math.Max(1, (int)(pip.EndDate - pip.StartDate).TotalDays);
                var daysElapsed  = (int)Math.Min(totalDays, (now - pip.StartDate).TotalDays);
                var daysRemaining = Math.Max(0, (int)(pip.EndDate - now).TotalDays);
                var periodPct    = Math.Min(100m, (decimal)daysElapsed / totalDays * 100m);
                var isOverdue    = pip.Status == PipStatus.Active && pip.EndDate < now;

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
                };
            }).ToList();

            var response = new PipDashboardResponse
            {
                PIPs                  = items,
                ActiveCount           = allPips.Count(p => p.Status == PipStatus.Active || p.Status == PipStatus.InProgress),
                DraftCount            = 0,
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
