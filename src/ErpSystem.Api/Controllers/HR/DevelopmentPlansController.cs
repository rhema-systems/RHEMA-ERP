using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Employee development plans — the growth half of the appraisal cycle, as opposed to the
/// corrective half in <see cref="PerformanceImprovementPlansController"/>.
///
/// <para><b>Who owns what.</b> The plan is the employee's: they write it, add objectives and log
/// progress. The manager reviews it and adds feedback (see
/// <see cref="DevelopmentPlanFeedbackController"/>). HR can see and touch any of them. Nobody
/// else — a development plan records what someone is not yet good at, and before this any
/// authenticated user could read any plan by id.</para>
///
/// <para><b>Deliberately not on the workflow engine.</b> Unlike an improvement plan, this has no
/// single-writer approval lifecycle: employee and manager both edit it, feedback arrives at any
/// time, and objectives move independently of the plan's own status. Activating a plan is the
/// manager agreeing to support it, not an approval gate that a definition should route — the same
/// reasoning that keeps goal approval bespoke.</para>
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "InternalOnly")]
public class DevelopmentPlansController : ControllerBase
{
    private readonly IDevelopmentPlanService _developmentPlanService;
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<DevelopmentPlansController> _logger;

    public DevelopmentPlansController(
        IDevelopmentPlanService developmentPlanService,
        ApplicationDbContext db,
        ICurrentUserService currentUserService,
        ILogger<DevelopmentPlansController> logger)
    {
        _developmentPlanService = developmentPlanService;
        _db = db;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>W3: whether the caller holds the given performance policy (seed and role fallback both count).</summary>
    private async Task<bool> HoldsPolicyAsync(string policy)
    {
        var authorization = HttpContext.RequestServices.GetRequiredService<IAuthorizationService>();
        return (await authorization.AuthorizeAsync(User, policy)).Succeeded;
    }

    /// <summary>
    /// Business rules — "a completed plan cannot be edited" and the like — come back as 422 with
    /// the rule's own message rather than falling to the generic handler, which returns a bare
    /// string body the client cannot read a message out of.
    /// </summary>
    private IActionResult BusinessRuleRejected(InvalidOperationException ex, string action)
    {
        _logger.LogWarning("Development plan rule rejected while {Action}: {Message}", action, ex.Message);
        return UnprocessableEntity(new { message = ex.Message });
    }

    /// <summary>The employee whose plan it is, their line manager, or a policy holder.</summary>
    private async Task<bool> CanAccessPlanAsync(Guid planId, string policy, CancellationToken ct = default)
    {
        if (await HoldsPolicyAsync(policy)) return true;
        if (_currentUserService.EmployeeId is not Guid me) return false;
        if (_currentUserService.TenantId is not Guid tenantId) return false;

        return await _db.Set<EmployeeDevelopmentPlan>()
            .AsNoTracking()
            .Where(p => p.Id == planId && p.TenantId == tenantId)
            .AnyAsync(p => p.EmployeeId == me || p.Employee.ManagerId == me, ct);
    }

    /// <summary>Same rule for a plan that does not exist yet, keyed on who it is for.</summary>
    private async Task<bool> CanAccessEmployeeAsync(Guid employeeId, string policy, CancellationToken ct = default)
    {
        if (await HoldsPolicyAsync(policy)) return true;
        if (_currentUserService.EmployeeId is not Guid me) return false;
        if (me == employeeId) return true;
        if (_currentUserService.TenantId is not Guid tenantId) return false;

        return await _db.Set<Employee>()
            .AsNoTracking()
            .AnyAsync(e => e.Id == employeeId && e.TenantId == tenantId && e.ManagerId == me, ct);
    }

    /// <summary>Get development plans with pagination. HR's org-wide view.</summary>
    [HttpGet("paged")]
    [Authorize(Policy = HrPermissions.PerformanceReadPolicy)]
    [ProducesResponseType(typeof(PagedResult<EmployeeDevelopmentPlanDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaged([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _developmentPlanService.GetPagedAsync(pageNumber, pageSize, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paged development plans");
            return StatusCode(500, "An error occurred while retrieving development plans");
        }
    }

    /// <summary>Get a development plan by ID</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(EmployeeDevelopmentPlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await CanAccessPlanAsync(id, HrPermissions.PerformanceReadPolicy, cancellationToken)) return Forbid();

        try
        {
            var result = await _developmentPlanService.GetByIdAsync(id, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving development plan {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the development plan");
        }
    }

    /// <summary>The signed-in employee's own development plans.</summary>
    [HttpGet("mine")]
    [ProducesResponseType(typeof(IEnumerable<EmployeeDevelopmentPlanDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken = default)
    {
        // An account with no employee link has no plans rather than an error — the shape every
        // other /me route in this module returns.
        if (_currentUserService.EmployeeId is not Guid me)
            return Ok(Array.Empty<EmployeeDevelopmentPlanDto>());

        try
        {
            return Ok(await _developmentPlanService.GetByEmployeeIdAsync(me, cancellationToken));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving the caller's development plans");
            return StatusCode(500, "An error occurred while retrieving your development plans");
        }
    }

    /// <summary>Development plans belonging to the signed-in manager's direct reports.</summary>
    [HttpGet("my-team")]
    [ProducesResponseType(typeof(IEnumerable<EmployeeDevelopmentPlanDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyTeam(CancellationToken cancellationToken = default)
    {
        if (_currentUserService.EmployeeId is not Guid me)
            return Ok(Array.Empty<EmployeeDevelopmentPlanDto>());

        try
        {
            return Ok(await _developmentPlanService.GetByManagerIdAsync(me, cancellationToken));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving team development plans");
            return StatusCode(500, "An error occurred while retrieving your team's development plans");
        }
    }

    /// <summary>Get all development plans for direct reports of a manager</summary>
    [HttpGet("by-manager/{managerId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<EmployeeDevelopmentPlanDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetByManager(Guid managerId, CancellationToken cancellationToken = default)
    {
        // Another manager's team is not this caller's to read; use /my-team for your own.
        if (_currentUserService.EmployeeId != managerId && !await HoldsPolicyAsync(HrPermissions.PerformanceReadPolicy)) return Forbid();

        try
        {
            var result = await _developmentPlanService.GetByManagerIdAsync(managerId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving development plans for manager {ManagerId}", managerId);
            return StatusCode(500, "An error occurred while retrieving team development plans");
        }
    }

    /// <summary>Get development plans for an employee</summary>
    [HttpGet("by-employee/{employeeId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<EmployeeDevelopmentPlanDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetByEmployee(Guid employeeId, CancellationToken cancellationToken = default)
    {
        if (!await CanAccessEmployeeAsync(employeeId, HrPermissions.PerformanceReadPolicy, cancellationToken)) return Forbid();

        try
        {
            var result = await _developmentPlanService.GetByEmployeeIdAsync(employeeId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving development plans for employee {EmployeeId}", employeeId);
            return StatusCode(500, "An error occurred while retrieving development plans");
        }
    }

    /// <summary>Get the active development plan for an employee, optionally filtered by cycle</summary>
    [HttpGet("active/{employeeId:guid}")]
    [ProducesResponseType(typeof(EmployeeDevelopmentPlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetActivePlan(Guid employeeId, [FromQuery] Guid? cycleId = null, CancellationToken cancellationToken = default)
    {
        if (!await CanAccessEmployeeAsync(employeeId, HrPermissions.PerformanceReadPolicy, cancellationToken)) return Forbid();

        try
        {
            var result = await _developmentPlanService.GetActivePlanAsync(employeeId, cycleId, cancellationToken);
            if (result == null) return NotFound(new { message = "No active development plan found" });
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active development plan for employee {EmployeeId}", employeeId);
            return StatusCode(500, "An error occurred while retrieving the active development plan");
        }
    }

    /// <summary>Create a new development plan</summary>
    [HttpPost]
    [ProducesResponseType(typeof(EmployeeDevelopmentPlanDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] CreateEmployeeDevelopmentPlanDto createDto, CancellationToken cancellationToken = default)
    {
        if (!await CanAccessEmployeeAsync(createDto.EmployeeId, HrPermissions.PerformanceWritePolicy, cancellationToken)) return Forbid();

        try
        {
            var result = await _developmentPlanService.CreateAsync(createDto, cancellationToken);
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
            _logger.LogError(ex, "Error creating development plan");
            return StatusCode(500, "An error occurred while creating the development plan");
        }
    }

    /// <summary>Update an existing development plan</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(EmployeeDevelopmentPlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateEmployeeDevelopmentPlanDto updateDto, CancellationToken cancellationToken = default)
    {
        // The service keys off the body's Id, so a mismatch would silently edit a different plan.
        if (id != updateDto.Id)
            return BadRequest(new { message = "The id in the route does not match the id in the body." });
        if (!await CanAccessPlanAsync(id, HrPermissions.PerformanceWritePolicy, cancellationToken)) return Forbid();

        try
        {
            var result = await _developmentPlanService.UpdateAsync(updateDto, cancellationToken);
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
            _logger.LogError(ex, "Error updating development plan {Id}", id);
            return StatusCode(500, "An error occurred while updating the development plan");
        }
    }

    /// <summary>Delete a development plan</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await CanAccessPlanAsync(id, HrPermissions.PerformanceWritePolicy, cancellationToken)) return Forbid();

        try
        {
            var result = await _developmentPlanService.DeleteAsync(id, cancellationToken);
            if (!result) return NotFound(new { message = "Development plan not found" });
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
            _logger.LogError(ex, "Error deleting development plan {Id}", id);
            return StatusCode(500, "An error occurred while deleting the development plan");
        }
    }

    /// <summary>Update the status of a development plan</summary>
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateDevelopmentPlanStatusRequest request, CancellationToken cancellationToken = default)
    {
        if (!await CanAccessPlanAsync(id, HrPermissions.PerformanceWritePolicy, cancellationToken)) return Forbid();

        try
        {
            var result = await _developmentPlanService.UpdateStatusAsync(id, request.Status, cancellationToken);
            if (!result) return NotFound(new { message = "Development plan not found" });
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BusinessRuleRejected(ex, "changing status");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating status of development plan {Id}", id);
            return StatusCode(500, "An error occurred while updating the development plan status");
        }
    }

    // ── Objectives ────────────────────────────────────────────────────────

    /// <summary>Add an objective to a development plan</summary>
    [HttpPost("{planId:guid}/objectives")]
    [ProducesResponseType(typeof(EmployeeDevelopmentObjectiveDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddObjective(Guid planId, [FromBody] CreateEmployeeDevelopmentObjectiveDto dto, CancellationToken cancellationToken = default)
    {
        if (!await CanAccessPlanAsync(planId, HrPermissions.PerformanceWritePolicy, cancellationToken)) return Forbid();

        try
        {
            var result = await _developmentPlanService.AddObjectiveAsync(planId, dto, cancellationToken);
            return StatusCode(201, result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BusinessRuleRejected(ex, "adding an objective");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding objective to development plan {PlanId}", planId);
            return StatusCode(500, "An error occurred while adding the objective");
        }
    }

    /// <summary>Get objectives for a development plan</summary>
    [HttpGet("{planId:guid}/objectives")]
    [ProducesResponseType(typeof(IEnumerable<EmployeeDevelopmentObjectiveDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetObjectives(Guid planId, CancellationToken cancellationToken = default)
    {
        if (!await CanAccessPlanAsync(planId, HrPermissions.PerformanceReadPolicy, cancellationToken)) return Forbid();

        try
        {
            var result = await _developmentPlanService.GetObjectivesAsync(planId, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving objectives for development plan {PlanId}", planId);
            return StatusCode(500, "An error occurred while retrieving objectives");
        }
    }

    /// <summary>Update an objective in a development plan</summary>
    [HttpPut("{planId:guid}/objectives/{objectiveId:guid}")]
    [ProducesResponseType(typeof(EmployeeDevelopmentObjectiveDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateObjective(Guid planId, Guid objectiveId, [FromBody] UpdateEmployeeDevelopmentObjectiveDto dto, CancellationToken cancellationToken = default)
    {
        // The service resolves the objective from the body's Id and ignores the route segment, so
        // without this guard a mismatched pair silently edited a different objective.
        if (objectiveId != dto.Id)
            return BadRequest(new { message = "The objective id in the route does not match the id in the body." });
        if (!await CanAccessPlanAsync(planId, HrPermissions.PerformanceWritePolicy, cancellationToken)) return Forbid();

        try
        {
            var result = await _developmentPlanService.UpdateObjectiveAsync(planId, dto, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BusinessRuleRejected(ex, "updating an objective");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating objective {ObjectiveId} for development plan {PlanId}", objectiveId, planId);
            return StatusCode(500, "An error occurred while updating the objective");
        }
    }

    /// <summary>Delete an objective from a development plan</summary>
    [HttpDelete("{planId:guid}/objectives/{objectiveId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteObjective(Guid planId, Guid objectiveId, CancellationToken cancellationToken = default)
    {
        if (!await CanAccessPlanAsync(planId, HrPermissions.PerformanceWritePolicy, cancellationToken)) return Forbid();

        try
        {
            var result = await _developmentPlanService.DeleteObjectiveAsync(planId, objectiveId, cancellationToken);
            if (!result) return NotFound(new { message = "Objective not found" });
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting objective {ObjectiveId} from development plan {PlanId}", objectiveId, planId);
            return StatusCode(500, "An error occurred while deleting the objective");
        }
    }

    /// <summary>Update progress on a specific objective</summary>
    [HttpPatch("{planId:guid}/objectives/{objectiveId:guid}/progress")]
    [ProducesResponseType(typeof(EmployeeDevelopmentObjectiveDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateObjectiveProgress(Guid planId, Guid objectiveId, [FromBody] UpdateObjectiveProgressRequest request, CancellationToken cancellationToken = default)
    {
        if (!await CanAccessPlanAsync(planId, HrPermissions.PerformanceWritePolicy, cancellationToken)) return Forbid();

        try
        {
            var result = await _developmentPlanService.UpdateObjectiveProgressAsync(planId, objectiveId, request.ProgressPercent, request.Notes, request.Status, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BusinessRuleRejected(ex, "recording progress");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating progress for objective {ObjectiveId} in development plan {PlanId}", objectiveId, planId);
            return StatusCode(500, "An error occurred while updating the objective progress");
        }
    }
}

/// <summary>Request body for updating objective progress</summary>
public record UpdateObjectiveProgressRequest(decimal ProgressPercent, string? Notes, DevelopmentObjectiveStatus Status);

/// <summary>
/// Request body for a plan status change. A wrapper rather than a bare enum in the body: the raw
/// form is awkward to send and gives the endpoint no room to grow a reason field.
/// </summary>
public record UpdateDevelopmentPlanStatusRequest(DevelopmentPlanStatus Status);
