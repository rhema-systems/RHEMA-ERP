using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Manager feedback on a development plan — the timeline of observations that runs alongside the
/// objectives. Entitlement follows the plan: HR, the employee, and the employee's line manager.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DevelopmentPlanFeedbackController : ControllerBase
{
    private readonly IDevelopmentPlanFeedbackService _feedbackService;
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<DevelopmentPlanFeedbackController> _logger;

    public DevelopmentPlanFeedbackController(
        IDevelopmentPlanFeedbackService feedbackService,
        ApplicationDbContext db,
        ICurrentUserService currentUserService,
        ILogger<DevelopmentPlanFeedbackController> logger)
    {
        _feedbackService = feedbackService;
        _db              = db;
        _currentUserService = currentUserService;
        _logger          = logger;
    }

    private bool IsHr =>
        User.IsInRole(Constants.Roles.SuperAdmin) || User.IsInRole(Constants.Roles.Hr);

    /// <summary>HR, the employee whose plan it is, or that employee's line manager.</summary>
    private async Task<bool> CanAccessPlanAsync(Guid planId, CancellationToken ct = default)
    {
        if (IsHr) return true;
        if (_currentUserService.EmployeeId is not Guid me) return false;
        if (_currentUserService.TenantId is not Guid tenantId) return false;

        return await _db.Set<EmployeeDevelopmentPlan>()
            .AsNoTracking()
            .Where(p => p.Id == planId && p.TenantId == tenantId)
            .AnyAsync(p => p.EmployeeId == me || p.Employee.ManagerId == me, ct);
    }

    /// <summary>Get all feedback entries for a development plan</summary>
    [HttpGet("by-plan/{planId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<EmployeeDevelopmentPlanFeedbackDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetByPlan(Guid planId, CancellationToken cancellationToken = default)
    {
        if (!await CanAccessPlanAsync(planId, cancellationToken)) return Forbid();

        try
        {
            var result = await _feedbackService.GetByPlanIdAsync(planId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving feedback for plan {PlanId}", planId);
            return StatusCode(500, "An error occurred while retrieving feedback.");
        }
    }

    /// <summary>Add a new feedback entry to a development plan</summary>
    [HttpPost]
    [ProducesResponseType(typeof(EmployeeDevelopmentPlanFeedbackDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Add(
        [FromBody] CreateEmployeeDevelopmentPlanFeedbackDto dto,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (!await CanAccessPlanAsync(dto.DevelopmentPlanId, cancellationToken)) return Forbid();

        // The author is whoever is signed in. Taking it from the body let any caller post feedback
        // under someone else's name — and the client has no employee id of its own to send.
        if (_currentUserService.EmployeeId is not Guid me)
            return BadRequest(new { message = "Your account is not linked to an employee record, so feedback cannot be attributed." });
        dto.ManagerId = me;

        try
        {
            var result = await _feedbackService.AddAsync(dto, cancellationToken);
            return CreatedAtAction(nameof(GetByPlan), new { planId = result.DevelopmentPlanId }, result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding feedback to plan {PlanId}", dto.DevelopmentPlanId);
            return StatusCode(500, "An error occurred while adding feedback.");
        }
    }

    /// <summary>Delete a feedback entry</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        if (!IsHr)
        {
            // Feedback is a record of what was said. Its author can withdraw it; nobody else can
            // edit someone else's out of the timeline.
            if (_currentUserService.EmployeeId is not Guid me) return Forbid();
            if (_currentUserService.TenantId is not Guid tenantId) return Forbid();

            var isAuthor = await _db.Set<EmployeeDevelopmentPlanFeedback>()
                .AsNoTracking()
                .AnyAsync(f => f.Id == id && f.TenantId == tenantId && f.ManagerId == me, cancellationToken);
            if (!isAuthor) return Forbid();
        }

        try
        {
            await _feedbackService.DeleteAsync(id, cancellationToken);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting feedback {Id}", id);
            return StatusCode(500, "An error occurred while deleting feedback.");
        }
    }
}
