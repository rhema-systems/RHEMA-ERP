using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/[controller]")]
[Authorize]
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
    [ProducesResponseType(StatusCodes.Status200OK)]
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
}
