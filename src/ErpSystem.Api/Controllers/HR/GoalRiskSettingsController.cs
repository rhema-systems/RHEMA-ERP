using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// The thresholds that decide when a goal is flagged at risk.
///
/// Route: /api/performance/goal-risk-settings
///
/// These drive every at-risk read — the manager workspace tabs, the org-wide at-risk report and
/// the risk fields on each goal row. Nothing seeds them, so a tenant that has never saved here
/// runs on the documented defaults (14 days / 60% / 20%) and <c>isConfigured</c> comes back false.
///
/// Gated to HR and admin: these are org-wide governance thresholds, not a per-manager preference.
/// </summary>
[ApiController]
[Route("api/performance/goal-risk-settings")]
[Authorize(Roles = Constants.Roles.Hr + ",Admin," + Constants.Roles.SuperAdmin)]
public class GoalRiskSettingsController : ControllerBase
{
    private readonly IGoalRiskSettingsService _service;
    private readonly ILogger<GoalRiskSettingsController> _logger;

    public GoalRiskSettingsController(
        IGoalRiskSettingsService service,
        ILogger<GoalRiskSettingsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>Get the thresholds in force, falling back to the defaults when none are stored.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(GoalRiskSettingsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _service.GetAsync(cancellationToken));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving goal risk settings");
            return StatusCode(500, new { message = "An error occurred while retrieving the goal risk settings." });
        }
    }

    /// <summary>Save the thresholds. Creates the tenant's row on first save.</summary>
    [HttpPut]
    [ProducesResponseType(typeof(GoalRiskSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Save(
        [FromBody] UpdateGoalRiskSettingsDto dto,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            return Ok(await _service.SaveAsync(dto, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving goal risk settings");
            return StatusCode(500, new { message = "An error occurred while saving the goal risk settings." });
        }
    }

    /// <summary>Discard the tenant's thresholds and go back to the defaults.</summary>
    [HttpPost("reset")]
    [ProducesResponseType(typeof(GoalRiskSettingsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Reset(CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _service.ResetAsync(cancellationToken));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting goal risk settings");
            return StatusCode(500, new { message = "An error occurred while resetting the goal risk settings." });
        }
    }
}
