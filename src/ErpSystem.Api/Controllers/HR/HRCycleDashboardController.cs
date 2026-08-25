using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// The HR view of one appraisal cycle: how far the pipeline has got, which deadlines are biting,
/// how the scores fell out, and what still needs a person to do something about it.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "InternalOnly")]
public class HRCycleDashboardController : ControllerBase
{
    private readonly IHRCycleDashboardQueryService _dashboardService;
    private readonly ILogger<HRCycleDashboardController> _logger;

    public HRCycleDashboardController(
        IHRCycleDashboardQueryService    dashboardService,
        ILogger<HRCycleDashboardController> logger)
    {
        _dashboardService = dashboardService;
        _logger           = logger;
    }

    /// <summary>
    /// Rules the caller has hit are answered 422 with the rule's own wording, so the client can
    /// show it. Matches <c>EmployeeGoalsController</c> and the rest of the HR appraisal surface.
    /// </summary>
    private IActionResult BusinessRuleRejected(InvalidOperationException ex, string action)
    {
        _logger.LogWarning(ex, "Dashboard rule rejected while {Action}", action);
        return UnprocessableEntity(new { message = ex.Message });
    }

    /// <summary>
    /// Returns the ID of the most recent Open/InProgress appraisal cycle.
    /// Returns <c>Guid.Empty</c> (all-zero GUID) when no active cycle exists.
    /// </summary>
    [HttpGet("active-cycle")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActiveCycleId(CancellationToken ct)
    {
        try
        {
            var id = await _dashboardService.GetActiveCycleIdAsync(ct);
            return Ok(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active cycle ID");
            return StatusCode(500, "An error occurred while retrieving the active cycle.");
        }
    }

    /// <summary>
    /// Returns the fully-aggregated HR Cycle Dashboard for the specified cycle.
    /// </summary>
    [HttpGet("{cycleId:guid}")]
    [Authorize(Policy = HrPermissions.PerformanceReadPolicy)]
    [ProducesResponseType(typeof(HRCycleDashboardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDashboard(Guid cycleId, CancellationToken ct)
    {
        try
        {
            var dto = await _dashboardService.BuildDashboardAsync(cycleId, ct);
            if (dto is null)
                return NotFound($"Appraisal cycle '{cycleId}' was not found.");

            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building HR cycle dashboard for cycle {CycleId}", cycleId);
            return StatusCode(500, "An error occurred while building the dashboard.");
        }
    }

    /// <summary>
    /// Nudges one stalled appraisal: raises an in-app notification for whoever owes its current
    /// step — the appraisee, the peers with forms still open, or the manager.
    ///
    /// <para>The cycle-wide <c>deadline-reminders</c> endpoint is the blunt instrument: it writes
    /// to everyone in scope for every live phase. This is the targeted one, driven from a single
    /// row on the dashboard's attention list.</para>
    /// </summary>
    [HttpPost("{cycleId:guid}/appraisals/{appraisalId:guid}/nudge")]
    [Authorize(Policy = HrPermissions.PerformanceWritePolicy)]
    [ProducesResponseType(typeof(HRCycleNudgeResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Nudge(Guid cycleId, Guid appraisalId, CancellationToken ct)
    {
        try
        {
            return Ok(await _dashboardService.NudgeAsync(cycleId, appraisalId, ct));
        }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BusinessRuleRejected(ex, "nudging the appraisal"); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error nudging appraisal {AppraisalId} in cycle {CycleId}", appraisalId, cycleId);
            return StatusCode(500, "An error occurred while sending the reminder.");
        }
    }
}
