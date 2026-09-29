using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Exceptions;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Interfaces.HR.Services;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "InternalOnly")]
public class EmployeeGoalsController : ControllerBase
{
    private readonly IEmployeeGoalService _employeeGoalService;
    private readonly IGoalWorkflowCommandService _workflowService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<EmployeeGoalsController> _logger;

    public EmployeeGoalsController(
        IEmployeeGoalService employeeGoalService,
        IGoalWorkflowCommandService workflowService,
        ICurrentUserService currentUserService,
        ApplicationDbContext db,
        ILogger<EmployeeGoalsController> logger)
    {
        _employeeGoalService = employeeGoalService;
        _workflowService     = workflowService;
        _currentUserService  = currentUserService;
        _db                  = db;
        _logger              = logger;
    }

    // ── W3 entitlement ────────────────────────────────────────────────────
    //
    // The service is tenant-scoped only: it validates goal rules but never asks who is calling,
    // so before these guards any authenticated user could read, rewrite or delete anyone's
    // goals by id. A goal belongs to its employee; the employee's line manager shares it; the
    // performance permission stands in for the desk. Submit/approve/reject/lock stay ungated —
    // IGoalWorkflowCommandService resolves the caller from the token and enforces the
    // direct-manager rule itself (the bespoke goal workflow, kept off the engine by design).

    private async Task<bool> HoldsPolicyAsync(string policy)
    {
        var authorization = HttpContext.RequestServices.GetRequiredService<IAuthorizationService>();
        return (await authorization.AuthorizeAsync(User, policy)).Succeeded;
    }

    /// <summary>The goal's employee, that employee's line manager, or a policy holder.</summary>
    private async Task<bool> CanAccessGoalAsync(Guid goalId, string policy)
    {
        if (await HoldsPolicyAsync(policy)) return true;
        if (_currentUserService.EmployeeId is not Guid me || me == Guid.Empty) return false;
        if (_currentUserService.TenantId is not Guid tenantId) return false;

        return await _db.Set<EmployeeGoal>()
            .AsNoTracking()
            .Where(g => g.Id == goalId && g.TenantId == tenantId)
            .AnyAsync(g => g.EmployeeId == me || g.Employee.ManagerId == me, HttpContext.RequestAborted);
    }

    /// <summary>As <see cref="CanAccessGoalAsync"/> minus the employee — unlocking undoes the
    /// manager's lock, so it is the manager's (or the desk's) alone.</summary>
    private async Task<bool> CanManageGoalAsync(Guid goalId, string policy)
    {
        if (await HoldsPolicyAsync(policy)) return true;
        if (_currentUserService.EmployeeId is not Guid me || me == Guid.Empty) return false;
        if (_currentUserService.TenantId is not Guid tenantId) return false;

        return await _db.Set<EmployeeGoal>()
            .AsNoTracking()
            .Where(g => g.Id == goalId && g.TenantId == tenantId)
            .AnyAsync(g => g.Employee.ManagerId == me, HttpContext.RequestAborted);
    }

    /// <summary>The employee themselves, their line manager, or a policy holder.</summary>
    private async Task<bool> CanAccessEmployeeRecordsAsync(Guid employeeId, string policy)
    {
        if (_currentUserService.EmployeeId is Guid me && me != Guid.Empty)
        {
            if (me == employeeId) return true;
            if (_currentUserService.TenantId is Guid tenantId &&
                await _db.Set<Core.Entities.HR.Employee>()
                    .AsNoTracking()
                    .AnyAsync(e => e.Id == employeeId && e.TenantId == tenantId && e.ManagerId == me, HttpContext.RequestAborted))
                return true;
        }

        return await HoldsPolicyAsync(policy);
    }

    /// <summary>The appraisal's subject, their line manager, or a policy holder.</summary>
    private async Task<bool> CanAccessAppraisalAsync(Guid appraisalId, string policy)
    {
        if (await HoldsPolicyAsync(policy)) return true;
        if (_currentUserService.EmployeeId is not Guid me || me == Guid.Empty) return false;
        if (_currentUserService.TenantId is not Guid tenantId) return false;

        return await _db.Set<PerformanceAppraisal>()
            .AsNoTracking()
            .Where(a => a.Id == appraisalId && a.TenantId == tenantId)
            .AnyAsync(a => a.EmployeeId == me || a.Employee.ManagerId == me, HttpContext.RequestAborted);
    }

    /// <summary>The named employee is the caller, or the caller holds the policy.</summary>
    private async Task<bool> SelfOrPolicyAsync(Guid employeeId, string policy)
    {
        if (_currentUserService.EmployeeId is Guid me && me != Guid.Empty && me == employeeId) return true;
        return await HoldsPolicyAsync(policy);
    }

    /// <summary>Get employee goals with pagination</summary>
    [HttpGet("paged")]
    [ProducesResponseType(typeof(PagedResult<EmployeeGoalDto>), StatusCodes.Status200OK)]
    [Authorize(Policy = HrPermissions.PerformanceReadPolicy)]
    public async Task<IActionResult> GetPaged([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, [FromQuery] Guid? employeeId = null, [FromQuery] Guid? cycleId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _employeeGoalService.GetPagedAsync(pageNumber, pageSize, employeeId, cycleId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paged employee goals");
            return StatusCode(500, "An error occurred while retrieving employee goals");
        }
    }

    /// <summary>Get an employee goal by ID</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(EmployeeGoalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await CanAccessGoalAsync(id, HrPermissions.PerformanceReadPolicy)) return Forbid();

        try
        {
            var result = await _employeeGoalService.GetByIdAsync(id, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving employee goal {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the employee goal");
        }
    }

    /// <summary>Get goals for a specific employee, optionally filtered by cycle</summary>
    [HttpGet("by-employee/{employeeId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<EmployeeGoalDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByEmployee(Guid employeeId, [FromQuery] Guid? cycleId = null, CancellationToken cancellationToken = default)
    {
        if (!await CanAccessEmployeeRecordsAsync(employeeId, HrPermissions.PerformanceReadPolicy)) return Forbid();

        try
        {
            var result = await _employeeGoalService.GetByEmployeeIdAsync(employeeId, cycleId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving goals for employee {EmployeeId}", employeeId);
            return StatusCode(500, "An error occurred while retrieving employee goals");
        }
    }

    /// <summary>Get goals linked to a specific appraisal</summary>
    [HttpGet("by-appraisal/{appraisalId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<EmployeeGoalDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByAppraisal(Guid appraisalId, CancellationToken cancellationToken = default)
    {
        if (!await CanAccessAppraisalAsync(appraisalId, HrPermissions.PerformanceReadPolicy)) return Forbid();

        try
        {
            var result = await _employeeGoalService.GetByAppraisalIdAsync(appraisalId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving goals for appraisal {AppraisalId}", appraisalId);
            return StatusCode(500, "An error occurred while retrieving employee goals");
        }
    }

    /// <summary>Get goals pending approval for a manager, optionally filtered by cycle</summary>
    [HttpGet("pending-approval/{managerId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<EmployeeGoalDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingApproval(Guid managerId, [FromQuery] Guid? cycleId = null, CancellationToken cancellationToken = default)
    {
        if (!await SelfOrPolicyAsync(managerId, HrPermissions.PerformanceReadPolicy)) return Forbid();

        try
        {
            var result = await _employeeGoalService.GetPendingApprovalAsync(managerId, cycleId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pending approval goals for manager {ManagerId}", managerId);
            return StatusCode(500, "An error occurred while retrieving pending approval goals");
        }
    }

    /// <summary>Get the goal summary for an employee in a cycle</summary>
    [HttpGet("summary/{employeeId:guid}/{cycleId:guid}")]
    [ProducesResponseType(typeof(EmployeeGoalSummaryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGoalSummary(Guid employeeId, Guid cycleId, CancellationToken cancellationToken = default)
    {
        if (!await CanAccessEmployeeRecordsAsync(employeeId, HrPermissions.PerformanceReadPolicy)) return Forbid();

        try
        {
            var result = await _employeeGoalService.GetGoalSummaryAsync(employeeId, cycleId, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving goal summary for employee {EmployeeId} in cycle {CycleId}", employeeId, cycleId);
            return StatusCode(500, "An error occurred while retrieving the goal summary");
        }
    }

    /// <summary>Get the goal summary for a manager's team in a cycle</summary>
    [HttpGet("team-summary/{managerId:guid}/{cycleId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<TeamGoalSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTeamGoalSummary(Guid managerId, Guid cycleId, CancellationToken cancellationToken = default)
    {
        if (!await SelfOrPolicyAsync(managerId, HrPermissions.PerformanceReadPolicy)) return Forbid();

        try
        {
            var result = await _employeeGoalService.GetTeamGoalSummaryAsync(managerId, cycleId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving team goal summary for manager {ManagerId} in cycle {CycleId}", managerId, cycleId);
            return StatusCode(500, "An error occurred while retrieving the team goal summary");
        }
    }

    /// <summary>Create a new employee goal</summary>
    [HttpPost]
    [ProducesResponseType(typeof(EmployeeGoalDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateEmployeeGoalDto createDto, CancellationToken cancellationToken = default)
    {
        // A goal is raised for its employee by that employee, their manager, or the desk — the
        // payload names the employee, so without this anyone could plant goals on a colleague.
        if (!await CanAccessEmployeeRecordsAsync(createDto.EmployeeId, HrPermissions.PerformanceWritePolicy)) return Forbid();

        try
        {
            var result = await _employeeGoalService.CreateAsync(createDto, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BusinessRuleRejected(ex, "creating");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating employee goal");
            return StatusCode(500, "An error occurred while creating the employee goal");
        }
    }

    /// <summary>Update an existing employee goal</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(EmployeeGoalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateEmployeeGoalDto updateDto, CancellationToken cancellationToken = default)
    {
        // The service updates the body's id, so without this a PUT to one goal's URL could edit another.
        if (id != updateDto.Id)
            return BadRequest(new { message = "Route id does not match body id." });

        if (!await CanAccessGoalAsync(id, HrPermissions.PerformanceWritePolicy)) return Forbid();

        try
        {
            var result = await _employeeGoalService.UpdateAsync(updateDto, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BusinessRuleRejected(ex, "updating");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating employee goal {Id}", id);
            return StatusCode(500, "An error occurred while updating the employee goal");
        }
    }

    /// <summary>Delete an employee goal</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        // The employee prunes their own drafts and the manager their team's — the service's
        // status rules decide what may go; the Admin arm covers the desk.
        if (!await CanAccessGoalAsync(id, HrPermissions.PerformanceAdminPolicy)) return Forbid();

        try
        {
            var result = await _employeeGoalService.DeleteAsync(id, cancellationToken);
            if (!result) return NotFound(new { message = "Employee goal not found" });
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BusinessRuleRejected(ex, "deleting");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting employee goal {Id}", id);
            return StatusCode(500, "An error occurred while deleting the employee goal");
        }
    }

    // ── Approval workflow ─────────────────────────────────────────────────
    //
    // All four commands below are served by IGoalWorkflowCommandService.
    // The service resolves the calling user from ICurrentUserService internally,
    // so no managerId / employeeId is accepted from the request — that would
    // allow callers to impersonate other users.
    //
    // HTTP status mapping for GoalWorkflowException.Reason:
    //   UnauthorizedAccess → 403
    //   GoalNotFound       → 404
    //   InvalidTransition
    //   GoalLocked
    //   MissingFeedback    → 422

    /// <summary>
    /// Submit a goal for manager approval.
    /// Transitions: Draft → PendingApproval, Rejected → PendingApproval.
    /// The submission target manager is derived from the employee's HR record.
    /// </summary>
    [HttpPost("{goalId:guid}/submit")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Submit(Guid goalId, CancellationToken cancellationToken = default)
    {
        try
        {
            await _workflowService.SubmitGoalAsync(goalId, cancellationToken);
            return NoContent();
        }
        catch (GoalWorkflowException ex)
        {
            return WorkflowError(ex, goalId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting goal {GoalId}", goalId);
            return StatusCode(500, new { message = "An error occurred while submitting the goal." });
        }
    }

    /// <summary>
    /// Approve a pending goal (manager action).
    /// Transition: PendingApproval → Approved.
    /// The calling user must be the direct manager of the goal's employee.
    /// </summary>
    [HttpPost("{goalId:guid}/approve")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Approve(Guid goalId, [FromBody] ApproveGoalRequest? request = null, CancellationToken cancellationToken = default)
    {
        try
        {
            await _workflowService.ApproveGoalAsync(goalId, request?.Feedback, cancellationToken);
            return NoContent();
        }
        catch (GoalWorkflowException ex)
        {
            return WorkflowError(ex, goalId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving goal {GoalId}", goalId);
            return StatusCode(500, new { message = "An error occurred while approving the goal." });
        }
    }

    /// <summary>
    /// Reject a pending goal, or send an approved one back for changes (manager action).
    /// Transitions: PendingApproval → Rejected; Approved / InProgress / OnTrack / AtRisk →
    /// Rejected when the goal is not locked — the only way what an approved goal measures can
    /// change (performance closure decision D-30).
    /// Non-empty feedback is required.
    /// The calling user must be the direct manager of the goal's employee.
    /// </summary>
    [HttpPost("{goalId:guid}/reject")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Reject(
        Guid goalId,
        [FromBody] RejectGoalRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request?.Feedback))
            return UnprocessableEntity(new { message = "Rejection feedback is required." });

        try
        {
            await _workflowService.RejectGoalAsync(goalId, request.Feedback, cancellationToken);
            return NoContent();
        }
        catch (GoalWorkflowException ex)
        {
            return WorkflowError(ex, goalId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting goal {GoalId}", goalId);
            return StatusCode(500, new { message = "An error occurred while rejecting the goal." });
        }
    }

    /// <summary>
    /// Lock a goal (manager action).
    /// Permitted on goals in Approved / InProgress / AtRisk / OnTrack / Completed status.
    /// Once locked, no further workflow transitions or edits are permitted. The goal's status is
    /// left alone: a lock freezes what the goal is, not its year (decision D-29).
    /// </summary>
    [HttpPost("{goalId:guid}/lock")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Lock(Guid goalId, CancellationToken cancellationToken = default)
    {
        try
        {
            await _workflowService.LockGoalAsync(goalId, cancellationToken);
            return NoContent();
        }
        catch (GoalWorkflowException ex)
        {
            return WorkflowError(ex, goalId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error locking goal {GoalId}", goalId);
            return StatusCode(500, new { message = "An error occurred while locking the goal." });
        }
    }

    /// <summary>
    /// Lock an employee's whole goal set for a cycle (manager action; performance closure L2/L5).
    /// Refused until every live goal is agreed, the count is inside the cycle's minimum and maximum
    /// and the weights add to 100 — the message names what is missing. Rejected goals are not part
    /// of the set, and goals already locked stay as they are.
    /// </summary>
    [HttpPost("lock-set")]
    [ProducesResponseType(typeof(GoalSetLockResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> LockSet([FromBody] LockGoalSetRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _workflowService.LockGoalSetAsync(request.EmployeeId, request.AppraisalCycleId, cancellationToken);
            return Ok(result);
        }
        catch (GoalWorkflowException ex)
        {
            return WorkflowError(ex, request.EmployeeId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error locking the goal set of employee {EmployeeId} for cycle {CycleId}",
                request.EmployeeId, request.AppraisalCycleId);
            return StatusCode(500, new { message = "An error occurred while locking the goal set." });
        }
    }

    /// <summary>Unlock a goal</summary>
    [HttpPost("{goalId:guid}/unlock")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unlock(Guid goalId, CancellationToken cancellationToken = default)
    {
        // Locking runs through the goal workflow, which holds it to the direct manager; unlock
        // bypassed that entirely, so anyone could undo a manager's lock — including its subject.
        if (!await CanManageGoalAsync(goalId, HrPermissions.PerformanceWritePolicy)) return Forbid();

        try
        {
            var result = await _employeeGoalService.UnlockGoalAsync(goalId, cancellationToken);
            if (!result) return NotFound(new { message = "Employee goal not found" });
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            // A scored goal cannot be unlocked (performance closure L2).
            return BusinessRuleRejected(ex, "unlocking");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unlocking goal {GoalId}", goalId);
            return StatusCode(500, "An error occurred while unlocking the goal");
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────

    /// <summary>
    /// Maps a <see cref="GoalWorkflowException"/> to the correct HTTP status code
    /// using the typed <see cref="GoalWorkflowFailureReason"/> on the exception.
    /// </summary>
    private IActionResult WorkflowError(GoalWorkflowException ex, Guid subjectId)
    {
        // The subject is the goal, or the employee for a goal-set lock.
        _logger.LogWarning(
            "Goal workflow rejected: subject={SubjectId}, reason={Reason}, message={Message}",
            subjectId, ex.Reason, ex.Message);

        return ex.Reason switch
        {
            GoalWorkflowFailureReason.GoalNotFound       => NotFound(new { message = ex.Message }),
            GoalWorkflowFailureReason.UnauthorizedAccess => StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message }),
            _                                            => UnprocessableEntity(new { message = ex.Message }),
        };
    }

    /// <summary>
    /// Maps a business-rule rejection from the service onto 422 with the rule's own message.
    ///
    /// The service raises <see cref="InvalidOperationException"/> for rules the caller can act
    /// on — the goal is locked, the cycle's goal cap is reached, the goal is not in a status
    /// that accepts progress. Nothing caught these, so they fell into the generic handler and
    /// came back as a 500 carrying a bare string; because the body was a string rather than
    /// <c>{ message }</c>, the client could not read a message out of it either, and the user
    /// got an unexplained failure on rules they hit routinely.
    ///
    /// 422 rather than 409 to match <see cref="WorkflowError"/> above, which already returns 422
    /// for a locked goal — the same goal in the same state should not answer differently
    /// depending on which endpoint was asked.
    ///
    /// Logged at warning, not error: the request was refused correctly and nobody needs paging.
    ///
    /// Caveat: the service's tenant guard raises the same exception type, so a token with no
    /// tenant claim would also land here as a 422 rather than a fault. Left as-is because that
    /// guard is unreachable for any authenticated caller (every token carries tenant_id) and
    /// giving it a distinct type means touching the ~10 HR services that copy the same guard.
    /// Worth doing if that refactor happens for another reason.
    /// </summary>
    private IActionResult BusinessRuleRejected(InvalidOperationException ex, string action)
    {
        _logger.LogWarning("Employee goal rule rejected while {Action}: {Message}", action, ex.Message);
        return UnprocessableEntity(new { message = ex.Message });
    }

    // ── Progress entries ──────────────────────────────────────────────────

    /// <summary>Add a progress entry to a goal</summary>
    [HttpPost("{goalId:guid}/progress")]
    [ProducesResponseType(typeof(GoalProgressEntryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddProgressEntry(Guid goalId, [FromBody] CreateGoalProgressEntryDto dto, CancellationToken cancellationToken = default)
    {
        // The recorder is the caller. The payload used to name them, so a progress entry could be
        // attributed to a colleague who never made it.
        if (_currentUserService.EmployeeId is not Guid recordedById || recordedById == Guid.Empty)
            return Unauthorized("User employee context not found");

        // Progress on a goal is recorded by its parties, not by any colleague with the id.
        if (!await CanAccessGoalAsync(goalId, HrPermissions.PerformanceWritePolicy)) return Forbid();

        try
        {
            var result = await _employeeGoalService.AddProgressEntryAsync(goalId, dto, recordedById, cancellationToken);
            return StatusCode(201, result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BusinessRuleRejected(ex, "adding a progress entry to");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding progress entry to goal {GoalId}", goalId);
            return StatusCode(500, "An error occurred while adding the progress entry");
        }
    }

    /// <summary>Get progress entries for a goal</summary>
    [HttpGet("{goalId:guid}/progress")]
    [ProducesResponseType(typeof(IEnumerable<GoalProgressEntryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProgressEntries(Guid goalId, CancellationToken cancellationToken = default)
    {
        if (!await CanAccessGoalAsync(goalId, HrPermissions.PerformanceReadPolicy)) return Forbid();

        try
        {
            var result = await _employeeGoalService.GetProgressEntriesAsync(goalId, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving progress entries for goal {GoalId}", goalId);
            return StatusCode(500, "An error occurred while retrieving progress entries");
        }
    }

    /// <summary>Update a progress entry</summary>
    [HttpPut("{goalId:guid}/progress/{entryId:guid}")]
    [ProducesResponseType(typeof(GoalProgressEntryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateProgressEntry(Guid goalId, Guid entryId, [FromBody] UpdateGoalProgressEntryDto dto, CancellationToken cancellationToken = default)
    {
        // The service keys off the body's Id, so without this a mismatch silently edits another entry.
        if (entryId != dto.Id)
            return BadRequest(new { message = "ID mismatch" });

        if (!await CanAccessGoalAsync(goalId, HrPermissions.PerformanceWritePolicy)) return Forbid();

        try
        {
            // P8: the recorder or the desk — the service decides, with the goal and the entry in hand.
            var isDesk = await HoldsPolicyAsync(HrPermissions.PerformanceWritePolicy);
            var result = await _employeeGoalService.UpdateProgressEntryAsync(
                goalId, dto, _currentUserService.EmployeeId, isDesk, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating progress entry {EntryId} for goal {GoalId}", entryId, goalId);
            return StatusCode(500, "An error occurred while updating the progress entry");
        }
    }

    /// <summary>Delete a progress entry</summary>
    [HttpDelete("{goalId:guid}/progress/{entryId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteProgressEntry(Guid goalId, Guid entryId, CancellationToken cancellationToken = default)
    {
        if (!await CanAccessGoalAsync(goalId, HrPermissions.PerformanceWritePolicy)) return Forbid();

        try
        {
            // P8: the recorder or the desk — the service decides, with the goal and the entry in hand.
            var isDesk = await HoldsPolicyAsync(HrPermissions.PerformanceWritePolicy);
            var result = await _employeeGoalService.DeleteProgressEntryAsync(
                goalId, entryId, _currentUserService.EmployeeId, isDesk, cancellationToken);
            if (!result) return NotFound(new { message = "Progress entry not found" });
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting progress entry {EntryId} for goal {GoalId}", entryId, goalId);
            return StatusCode(500, "An error occurred while deleting the progress entry");
        }
    }
}

/// <summary>
/// Request body for <c>POST {goalId}/reject</c>.
/// Feedback is mandatory — rejection without explanation is a domain rule violation.
/// </summary>
public sealed record RejectGoalRequest(string? Feedback);

/// <summary>
/// Optional request body for <c>POST {goalId}/approve</c>.
/// Allows the manager to attach a brief approval comment.
/// </summary>
public sealed record ApproveGoalRequest(string? Feedback);

/// <summary>Request body for <c>POST lock-set</c>: whose goal set, for which cycle.</summary>
public sealed record LockGoalSetRequest(Guid EmployeeId, Guid AppraisalCycleId);
