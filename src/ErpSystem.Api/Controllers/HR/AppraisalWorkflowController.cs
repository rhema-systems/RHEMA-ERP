using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ErpSystem.Shared;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "InternalOnly")]
public class AppraisalWorkflowController : ControllerBase
{
    private readonly IAppraisalWorkflowService _workflowService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<AppraisalWorkflowController> _logger;

    public AppraisalWorkflowController(
        IAppraisalWorkflowService workflowService,
        ICurrentUserService currentUserService,
        ApplicationDbContext db,
        ILogger<AppraisalWorkflowController> logger)
    {
        _workflowService = workflowService;
        _currentUserService = currentUserService;
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// A party to the appraisal — the appraisee, their line manager, or a peer whose nomination
    /// was approved — or the performance desk (performance closure P17). The two reads were open
    /// to any authenticated user, and answered 404 or 200 for any id in the tenant. An unknown id
    /// falls to the desk, so the desk is told it is missing and anyone else is refused.
    /// </summary>
    private async Task<bool> CanReadAppraisalAsync(Guid appraisalId, CancellationToken ct)
    {
        if (_currentUserService.TenantId is Guid tenantId
            && _currentUserService.EmployeeId is Guid me && me != Guid.Empty
            && await _db.Set<PerformanceAppraisal>()
                .AsNoTracking()
                .Where(a => a.Id == appraisalId && a.TenantId == tenantId)
                .AnyAsync(a => a.EmployeeId == me
                            || a.Employee.ManagerId == me
                            || a.PeerNominations.Any(n => n.PeerEmployeeId == me
                                                       && n.NominationStatus == PeerNominationStatus.Approved
                                                       && !n.IsDeleted), ct))
            return true;

        var authorization = HttpContext.RequestServices.GetRequiredService<IAuthorizationService>();
        return (await authorization.AuthorizeAsync(User, HrPermissions.PerformanceReadPolicy)).Succeeded;
    }

    /// <summary>
    /// Get the current fine-grained lifecycle phase for an appraisal.
    /// The phase is computed from live entity state and is NOT persisted.
    /// </summary>
    [HttpGet("{appraisalId:guid}/phase")]
    [ProducesResponseType(typeof(AppraisalPhaseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCurrentPhase(Guid appraisalId, CancellationToken cancellationToken = default)
    {
        if (!await CanReadAppraisalAsync(appraisalId, cancellationToken)) return Forbid();

        try
        {
            var phase = await _workflowService.GetCurrentPhaseAsync(appraisalId, cancellationToken);
            return Ok(new AppraisalPhaseResponse(appraisalId, phase));
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving phase for appraisal {AppraisalId}", appraisalId);
            return StatusCode(500, "An error occurred while retrieving the appraisal phase");
        }
    }

    /// <summary>
    /// Enforce lifecycle transition rules and change the appraisal status.
    /// Throws if the requested transition is not permitted from the current status.
    /// </summary>
    [HttpPost("{appraisalId:guid}/transition")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Authorize(Policy = HrPermissions.PerformanceWritePolicy)]
    public async Task<IActionResult> Transition(Guid appraisalId, [FromBody] AppraisalStatus newStatus, CancellationToken cancellationToken = default)
    {
        try
        {
            await _workflowService.TransitionAsync(appraisalId, newStatus, cancellationToken);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error transitioning appraisal {AppraisalId} to status {NewStatus}", appraisalId, newStatus);
            return StatusCode(500, "An error occurred while transitioning the appraisal status");
        }
    }

    /// <summary>
    /// Check whether a role-holder is permitted to submit edits to the appraisal
    /// given its current lifecycle state and phase.
    /// <para>Role values: <c>Employee</c>, <c>Manager</c>, <c>Peer</c>, <c>HR</c>.</para>
    /// </summary>
    [HttpGet("{appraisalId:guid}/editable/{role}")]
    [ProducesResponseType(typeof(AppraisalEditableResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> IsEditableByRole(Guid appraisalId, string role, CancellationToken cancellationToken = default)
    {
        if (!await CanReadAppraisalAsync(appraisalId, cancellationToken)) return Forbid();

        try
        {
            var editable = await _workflowService.IsEditableByRoleAsync(appraisalId, role, cancellationToken);
            return Ok(new AppraisalEditableResponse(appraisalId, role, editable));
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking editability for appraisal {AppraisalId}, role {Role}", appraisalId, role);
            return StatusCode(500, "An error occurred while checking appraisal editability");
        }
    }
}

/// <summary>Response for the current appraisal phase query</summary>
public record AppraisalPhaseResponse(Guid AppraisalId, AppraisalPhase Phase);

/// <summary>Response for the role-based editability query</summary>
public record AppraisalEditableResponse(Guid AppraisalId, string Role, bool IsEditable);
