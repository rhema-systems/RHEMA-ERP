using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Org-wide at-risk goal query API for HR and admin roles.
///
/// Route: /api/performance/goals-at-risk
///
/// Unlike /api/performance/team-goals (which is manager-scoped),
/// this controller has no manager scope and returns results across
/// the entire organisation or an optional org-unit/level sub-set.
///
/// Access is restricted to HR Officer, HR Manager, and Admin roles.
/// </summary>
[ApiController]
[Route("api/performance/goals-at-risk")]
// This endpoint returns org-wide employee/goal/risk data with no manager scope, so it must be
// gated to HR/Admin (there is no global fallback policy — without this it is reachable anonymously).
[Authorize(Roles = "HR,Admin,SuperAdmin")]
public class AtRiskGoalsController : ControllerBase
{
    private readonly IAtRiskGoalsQueryService        _atRiskService;
    private readonly ILogger<AtRiskGoalsController> _logger;

    public AtRiskGoalsController(
        IAtRiskGoalsQueryService        atRiskService,
        ILogger<AtRiskGoalsController> logger)
    {
        _atRiskService = atRiskService;
        _logger        = logger;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Org-Wide At-Risk
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns all at-risk employee goals in the given appraisal cycle,
    /// optionally filtered to a specific organisation unit and/or level.
    ///
    /// A goal is at risk when:
    ///   • Status == AtRisk, OR
    ///   • Status is InProgress/OnTrack
    ///     AND DueDate &lt;= today + GoalRiskSetting.DaysRemainingThreshold
    ///     AND ProgressPercent &lt; GoalRiskSetting.MinimumProgressPercent
    ///
    /// Results are sorted by RiskSeverityScore descending, then DaysRemaining ascending.
    /// </summary>
    /// <param name="cycleId">The appraisal cycle to query.</param>
    /// <param name="organizationUnitId">
    /// Optional. When supplied, limits results to employees in that org unit
    /// (maps to <c>Employee.OrganizationUnitId</c>).
    /// </param>
    /// <param name="organizationLevelId">
    /// Optional. When supplied, limits results to employees at that org level
    /// (maps to <c>Employee.OrganizationLevelId</c>).
    /// Can be combined with <paramref name="organizationUnitId"/>.
    /// </param>
    /// <param name="cancellationToken">Propagated cancellation token.</param>
    [HttpGet("{cycleId:guid}")]
    [ProducesResponseType(typeof(List<TeamGoalFlatDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetOrgWideAtRiskGoals(
        Guid  cycleId,
        [FromQuery] Guid? organizationUnitId  = null,
        [FromQuery] Guid? organizationLevelId = null,
        CancellationToken cancellationToken   = default)
    {
        try
        {
            var request = new AtRiskGoalsRequest
            {
                AppraisalCycleId    = cycleId,
                OrganizationUnitId  = organizationUnitId,
                OrganizationLevelId = organizationLevelId,
            };

            var result = await _atRiskService.GetOrgWideAtRiskGoalsAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error retrieving org-wide at-risk goals for cycleId={CycleId}, " +
                "unitId={UnitId}, levelId={LevelId}",
                cycleId, organizationUnitId, organizationLevelId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "An error occurred while retrieving org-wide at-risk goals." });
        }
    }
}
