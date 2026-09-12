using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Interfaces.HR.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Read-only governance query API for the Advanced Manager Workspace.
///
/// Route: /api/performance/team-goals
///
/// This controller is a pure pass-through to ITeamGoalsQueryService.
/// All business logic, security enforcement, and query composition
/// live in the service layer. This controller only handles HTTP concerns:
/// routing, response codes, and error formatting.
///
/// No mutation endpoints exist here. Approve / reject operations are
/// handled by the EmployeeGoalsController.
/// </summary>
[ApiController]
[Route("api/performance/team-goals")]
[Authorize(Policy = "InternalOnly")]
public class TeamGoalsController : ControllerBase
{
    private readonly ITeamGoalsQueryService _teamGoalsQueryService;
    private readonly IGoalDetailQueryService _goalDetailQueryService;
    private readonly ILogger<TeamGoalsController> _logger;

    public TeamGoalsController(
        ITeamGoalsQueryService teamGoalsQueryService,
        IGoalDetailQueryService goalDetailQueryService,
        ILogger<TeamGoalsController> logger)
    {
        _teamGoalsQueryService  = teamGoalsQueryService;
        _goalDetailQueryService = goalDetailQueryService;
        _logger                 = logger;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Overview Tab
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns one governance snapshot per direct report in the given appraisal cycle.
    /// Includes goal counts split by workflow/execution state, weight balance, and
    /// an overall GovernanceStatus per employee.
    /// Employees with zero goals appear with GovernanceStatus.NotStarted.
    /// </summary>
    /// <param name="cycleId">The appraisal cycle to query.</param>
    /// <param name="cancellationToken">Propagated cancellation token.</param>
    [HttpGet("overview/{cycleId:guid}")]
    [ProducesResponseType(typeof(List<TeamMemberOverviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetTeamOverview(
        Guid cycleId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _teamGoalsQueryService.GetTeamOverviewAsync(cycleId, cancellationToken);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access to team overview for cycleId={CycleId}", cycleId);
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving team overview for cycleId={CycleId}", cycleId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "An error occurred while retrieving the team overview." });
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Awaiting Approval Tab
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns all direct-report goals with Status == PendingApproval in the given cycle.
    /// Powers the "Awaiting Approval" tab.
    /// </summary>
    /// <param name="cycleId">The appraisal cycle to query.</param>
    /// <param name="cancellationToken">Propagated cancellation token.</param>
    [HttpGet("awaiting-approval/{cycleId:guid}")]
    [ProducesResponseType(typeof(List<TeamGoalFlatDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetGoalsAwaitingApproval(
        Guid cycleId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _teamGoalsQueryService.GetGoalsAwaitingApprovalAsync(cycleId, cancellationToken);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access to awaiting-approval tab for cycleId={CycleId}", cycleId);
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving awaiting-approval goals for cycleId={CycleId}", cycleId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "An error occurred while retrieving goals awaiting approval." });
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  At-Risk Tab
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns all at-risk direct-report goals in the given cycle.
    ///
    /// A goal is at risk when:
    ///   • Status == AtRisk, OR
    ///   • Status is InProgress/OnTrack AND DueDate &lt;= today+14d AND ProgressPercent &lt; 50
    ///
    /// Powers the "At Risk" tab.
    /// </summary>
    /// <param name="cycleId">The appraisal cycle to query.</param>
    /// <param name="cancellationToken">Propagated cancellation token.</param>
    [HttpGet("at-risk/{cycleId:guid}")]
    [ProducesResponseType(typeof(List<TeamGoalFlatDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetAtRiskGoals(
        Guid cycleId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _teamGoalsQueryService.GetAtRiskGoalsAsync(cycleId, cancellationToken);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access to at-risk tab for cycleId={CycleId}", cycleId);
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving at-risk goals for cycleId={CycleId}", cycleId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "An error occurred while retrieving at-risk goals." });
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Overdue Tab
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns all overdue direct-report goals in the given cycle.
    /// A goal is overdue when DueDate &lt; today AND Status is not Completed or Locked.
    /// Status is the single source of truth; the legacy IsLocked flag is not consulted.
    /// Powers the "Overdue" tab.
    /// </summary>
    /// <param name="cycleId">The appraisal cycle to query.</param>
    /// <param name="cancellationToken">Propagated cancellation token.</param>
    [HttpGet("overdue/{cycleId:guid}")]
    [ProducesResponseType(typeof(List<TeamGoalFlatDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetOverdueGoals(
        Guid cycleId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _teamGoalsQueryService.GetOverdueGoalsAsync(cycleId, cancellationToken);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access to overdue tab for cycleId={CycleId}", cycleId);
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving overdue goals for cycleId={CycleId}", cycleId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "An error occurred while retrieving overdue goals." });
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Locked Tab
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns all locked direct-report goals (Status == Locked) in the given cycle.
    /// Powers the "Locked" tab.
    /// </summary>
    /// <param name="cycleId">The appraisal cycle to query.</param>
    /// <param name="cancellationToken">Propagated cancellation token.</param>
    [HttpGet("locked/{cycleId:guid}")]
    [ProducesResponseType(typeof(List<TeamGoalFlatDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetLockedGoals(
        Guid cycleId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _teamGoalsQueryService.GetLockedGoalsAsync(cycleId, cancellationToken);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access to locked tab for cycleId={CycleId}", cycleId);
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving locked goals for cycleId={CycleId}", cycleId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "An error occurred while retrieving locked goals." });
        }
    }

    /// <summary>
    /// Returns ALL goals for a specific direct report in the given cycle.
    /// Intended for the Employee Drill-Down page.
    /// Returns 401 when <paramref name="employeeId"/> is not a direct report
    /// of the authenticated manager.
    /// </summary>
    /// <param name="employeeId">The direct report's employee ID.</param>
    /// <param name="cycleId">The appraisal cycle to query.</param>
    /// <param name="cancellationToken">Propagated cancellation token.</param>
    [HttpGet("employee/{employeeId:guid}/{cycleId:guid}")]
    [ProducesResponseType(typeof(List<TeamGoalFlatDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetEmployeeGoals(
        Guid employeeId,
        Guid cycleId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _teamGoalsQueryService.GetEmployeeGoalsAsync(
                employeeId, cycleId, cancellationToken);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex,
                "Unauthorized access to employee drill-down for employeeId={EmployeeId}, cycleId={CycleId}",
                employeeId, cycleId);
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error retrieving employee goals for employeeId={EmployeeId}, cycleId={CycleId}",
                employeeId, cycleId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "An error occurred while retrieving employee goals." });
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Goal Detail Drawer
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the full detail projection for a single <see cref="EmployeeGoal"/>,
    /// used by the Manager Goal Detail Drawer.
    ///
    /// Returns 404 when the goal does not exist or the authenticated manager
    /// is not the direct-report manager of the goal's employee (intentional —
    /// no information is leaked about whether the goal exists).
    /// </summary>
    /// <param name="goalId">The <see cref="EmployeeGoal"/> primary key.</param>
    /// <param name="cancellationToken">Propagated cancellation token.</param>
    [HttpGet("goal-detail/{goalId:guid}")]
    [ProducesResponseType(typeof(GoalDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetGoalDetail(
        Guid goalId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _goalDetailQueryService.GetGoalDetailAsync(goalId, cancellationToken);

            if (result is null)
                return NotFound(new { message = "Goal not found or access denied." });

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving goal detail for goalId={GoalId}", goalId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "An error occurred while retrieving goal detail." });
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Team Goal Progress Dashboard
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns one <see cref="TeamGoalProgressDto"/> per direct report, providing
    /// an execution-progress snapshot (goal counts by status, average progress %,
    /// and a per-goal summary list) for the given appraisal cycle.
    /// Powers the Manager Team Goal Progress page.
    /// </summary>
    /// <param name="cycleId">The appraisal cycle to query.</param>
    /// <param name="cancellationToken">Propagated cancellation token.</param>
    [HttpGet("progress/{cycleId:guid}")]
    [ProducesResponseType(typeof(List<TeamGoalProgressDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetTeamGoalProgress(
        Guid cycleId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _teamGoalsQueryService.GetTeamGoalProgressAsync(cycleId, cancellationToken);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access to team progress for cycleId={CycleId}", cycleId);
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving team goal progress for cycleId={CycleId}", cycleId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "An error occurred while retrieving team goal progress." });
        }
    }
}
