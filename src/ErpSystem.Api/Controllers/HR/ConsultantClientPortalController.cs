using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// The consultant-client contact's own portal surface — dashboard and timesheet
/// confirm/reject — for invited accounts on the main JWT scheme (ConsultantClient role).
/// Replaced the PortalBearer-scheme controller 2026-08-31.
/// </summary>
/// <remarks>
/// Two fences sit in front of every action here: the <c>ConsultantClientOnly</c> policy (only
/// the ConsultantClient role gets in) and <c>ConsultantClientAccessMiddleware</c> (the same
/// tokens get NOTHING outside the client-portal allowlist). Ownership inside is by
/// <c>ConsultantClientContact.UserId</c> — every lookup scopes through the caller's own active
/// contact rows, so a guessed timesheet id is a refusal, not a disclosure. Password changes
/// live on the shared <c>api/auth</c> surface now; the bespoke change-password action retired
/// with the scheme.
/// </remarks>
[ApiController]
[Route("api/client-portal")]
[Authorize(Policy = "ConsultantClientOnly")]
public class ConsultantClientPortalController : ControllerBase
{
    private readonly IConsultantClientPortalService _portalService;
    private readonly ICurrentUserService _currentUser;

    public ConsultantClientPortalController(
        IConsultantClientPortalService portalService,
        ICurrentUserService currentUser)
    {
        _portalService = portalService;
        _currentUser = currentUser;
    }

    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(ConsultantClientPortalDashboardDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ConsultantClientPortalDashboardDto>> GetDashboard(CancellationToken ct)
    {
        var result = await _portalService.GetDashboardAsync(GetUserId(), GetTenantId(), ct);
        return Ok(result);
    }

    [HttpGet("timesheets/{id:guid}")]
    [ProducesResponseType(typeof(ClientTimesheetConfirmationPublicDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ClientTimesheetConfirmationPublicDto>> GetTimesheet(
        Guid id,
        CancellationToken ct)
    {
        try
        {
            var result = await _portalService.GetTimesheetAsync(GetUserId(), id, GetTenantId(), ct);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
    }

    [HttpPost("timesheets/{id:guid}/confirm")]
    [ProducesResponseType(typeof(ClientTimesheetConfirmationDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ClientTimesheetConfirmationDto>> ConfirmTimesheet(
        Guid id,
        [FromBody] ConsultantClientPortalConfirmTimesheetDto dto,
        CancellationToken ct)
    {
        try
        {
            var result = await _portalService.ConfirmTimesheetAsync(GetUserId(), id, dto, GetTenantId(), ct);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
    }

    [HttpPost("timesheets/{id:guid}/reject")]
    [ProducesResponseType(typeof(ClientTimesheetConfirmationDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ClientTimesheetConfirmationDto>> RejectTimesheet(
        Guid id,
        [FromBody] ConsultantClientPortalRejectTimesheetDto dto,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var result = await _portalService.RejectTimesheetAsync(GetUserId(), id, dto, GetTenantId(), ct);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
    }

    private Guid GetUserId()
    {
        var id = _currentUser.UserId;
        if (id == null || !Guid.TryParse(id, out var userId))
            throw new UnauthorizedAccessException("Invalid token.");
        return userId;
    }

    private Guid GetTenantId() =>
        _currentUser.TenantId ?? throw new UnauthorizedAccessException("Tenant context could not be resolved.");
}
