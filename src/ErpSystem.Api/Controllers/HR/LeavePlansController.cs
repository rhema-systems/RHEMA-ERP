using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Leave plan management endpoints
/// </summary>
/// <remarks>
/// W3 slice 5: a plan belongs to its employee — drafting, submitting, responding to a manager's
/// suggestion and cancelling are self-or-write acts; the organisation-wide year view is the
/// leave read tier; approve/reject/suggest-changes stay with the workflow assignee, validated
/// per plan by the service.
/// </remarks>
[ApiController]
[Route("api/hr/leave-plans")]
[Authorize(Policy = "InternalOnly")]
public class LeavePlansController : ControllerBase
{
    private readonly ILeavePlanService _service;
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuthorizationService _authorization;
    private readonly ILogger<LeavePlansController> _logger;

    public LeavePlansController(
        ILeavePlanService service,
        ApplicationDbContext db,
        ICurrentUserService currentUserService,
        IAuthorizationService authorization,
        ILogger<LeavePlansController> logger)
    {
        _service = service;
        _db = db;
        _currentUserService = currentUserService;
        _authorization = authorization;
        _logger = logger;
    }

    /// <summary>Self-or-permission, as on LeavesController — see the remarks there.</summary>
    private async Task<bool> CanActForEmployeeAsync(Guid employeeId, string policy)
    {
        if (_currentUserService.EmployeeId is Guid me && me != Guid.Empty && me == employeeId)
            return true;
        return (await _authorization.AuthorizeAsync(User, policy)).Succeeded;
    }

    /// <summary>Self-or-permission resolved through the plan's owner.</summary>
    private async Task<bool> CanActOnPlanAsync(Guid planId, string policy)
    {
        if (_currentUserService.EmployeeId is Guid me && me != Guid.Empty &&
            _currentUserService.TenantId is Guid tenantId)
        {
            var mine = await _db.Set<Core.Entities.HR.StaffLeave.LeavePlan>()
                .AsNoTracking()
                .AnyAsync(p => p.Id == planId && p.TenantId == tenantId && p.EmployeeId == me);
            if (mine) return true;
        }
        return (await _authorization.AuthorizeAsync(User, policy)).Succeeded;
    }

    [HttpGet("employee/{employeeId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<LeavePlanDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<LeavePlanDto>>> GetByEmployee(
        Guid employeeId,
        [FromQuery] int year = 0)
    {
        if (!await CanActForEmployeeAsync(employeeId, HrPermissions.LeaveReadPolicy))
            return Forbid();

        if (year == 0) year = DateTime.Today.Year;
        return Ok(await _service.GetByEmployeeAndYearAsync(employeeId, year));
    }

    [HttpGet]
    [Authorize(Policy = HrPermissions.LeaveReadPolicy)]
    [ProducesResponseType(typeof(IEnumerable<LeavePlanDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<LeavePlanDto>>> GetByYear([FromQuery] int year = 0)
    {
        if (year == 0) year = DateTime.Today.Year;
        return Ok(await _service.GetByYearAsync(year));
    }

    /// <summary>
    /// Is this reliever actually free over these dates? The plan form asks before saving; the
    /// register carries the same answer on each row as <c>relieverClashes</c>.
    /// </summary>
    /// <remarks>
    /// Finish-plan lane 4 (2026-09-01). Deliberately on the class-level InternalOnly gate rather
    /// than a leave permission: an employee planning their own leave chooses their own reliever and
    /// needs this answer as much as the desk does. What it discloses — that a colleague is away over
    /// a window — is what naming them as reliever already presumes.
    /// </remarks>
    [HttpGet("reliever-clashes")]
    [ProducesResponseType(typeof(IReadOnlyList<LeaveRelieverClashDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<LeaveRelieverClashDto>>> GetRelieverClashes(
        [FromQuery] Guid relieverId,
        [FromQuery] DateOnly startDate,
        [FromQuery] DateOnly endDate,
        [FromQuery] Guid? excludePlanId = null)
    {
        if (relieverId == Guid.Empty)
            return BadRequest(new { message = "A reliever is required." });
        if (endDate < startDate)
            return BadRequest(new { message = "End date cannot be before the start date." });

        return Ok(await _service.GetRelieverClashesAsync(relieverId, startDate, endDate, excludePlanId));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(LeavePlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LeavePlanDto>> GetById(Guid id)
    {
        if (!await CanActOnPlanAsync(id, HrPermissions.LeaveReadPolicy))
            return Forbid();

        try { return Ok(await _service.GetByIdAsync(id)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpPost]
    [ProducesResponseType(typeof(LeavePlanDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LeavePlanDto>> Create([FromBody] CreateLeavePlanDto dto)
    {
        // W3: an employee plans their OWN leave; planning for someone else is the HR desk.
        if (!await CanActForEmployeeAsync(dto.EmployeeId, HrPermissions.LeaveWritePolicy))
            return Forbid();

        try
        {
            var result = await _service.CreateLeavePlanAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating leave plan");
            return StatusCode(500, "An error occurred while creating the leave plan");
        }
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(LeavePlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LeavePlanDto>> Update(Guid id, [FromBody] CreateLeavePlanDto dto)
    {
        if (!await CanActOnPlanAsync(id, HrPermissions.LeaveWritePolicy))
            return Forbid();

        try { return Ok(await _service.UpdateLeavePlanAsync(id, dto)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating leave plan {id}", id);
            return StatusCode(500, "An error occurred while updating the leave plan");
        }
    }

    [HttpPatch("{id:guid}/submit")]
    [ProducesResponseType(typeof(LeavePlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LeavePlanDto>> Submit(Guid id)
    {
        if (!await CanActOnPlanAsync(id, HrPermissions.LeaveWritePolicy))
            return Forbid();

        try { return Ok(await _service.SubmitLeavePlanAsync(id)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    // W3: approve, reject and suggest-changes are deliberately NOT permission-gated — they are
    // the workflow assignee's acts, validated per plan by the service (CanUserApproveAsync).
    [HttpPatch("{id:guid}/approve")]
    [ProducesResponseType(typeof(LeavePlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LeavePlanDto>> Approve(Guid id)
    {
        try { return Ok(await _service.ApproveLeavePlanAsync(id)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return Forbid(); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPatch("{id:guid}/reject")]
    [ProducesResponseType(typeof(LeavePlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LeavePlanDto>> Reject(Guid id, [FromBody] string reason)
    {
        try { return Ok(await _service.RejectLeavePlanAsync(id, reason)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return Forbid(); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPatch("{id:guid}/suggest-changes")]
    [ProducesResponseType(typeof(LeavePlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LeavePlanDto>> SuggestChanges(Guid id, [FromBody] SuggestLeavePlanChangesDto dto)
    {
        try { return Ok(await _service.SuggestChangesAsync(id, dto)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPatch("{id:guid}/respond-suggestion")]
    [ProducesResponseType(typeof(LeavePlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LeavePlanDto>> RespondToSuggestion(Guid id, [FromBody] RespondToLeaveSuggestionDto dto)
    {
        // W3: answering the manager's suggestion is the plan owner's act.
        if (!await CanActOnPlanAsync(id, HrPermissions.LeaveWritePolicy))
            return Forbid();

        try { return Ok(await _service.RespondToSuggestionAsync(id, dto)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPatch("{id:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(Guid id)
    {
        if (!await CanActOnPlanAsync(id, HrPermissions.LeaveWritePolicy))
            return Forbid();

        try
        {
            await _service.CancelLeavePlanAsync(id);
            return NoContent();
        }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }
}
