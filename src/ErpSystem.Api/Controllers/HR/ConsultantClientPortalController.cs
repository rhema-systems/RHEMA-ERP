using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/client-portal")]
[Authorize(Policy = "ConsultantClientPortal", AuthenticationSchemes = PortalAuth.Scheme)]
public class ConsultantClientPortalController : ControllerBase
{
    private readonly IConsultantClientPortalService _portalService;
    private readonly IConsultantClientPortalAuthService _authService;

    public ConsultantClientPortalController(
        IConsultantClientPortalService portalService,
        IConsultantClientPortalAuthService authService)
    {
        _portalService = portalService;
        _authService = authService;
    }

    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(ConsultantClientPortalDashboardDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ConsultantClientPortalDashboardDto>> GetDashboard(CancellationToken ct)
    {
        var result = await _portalService.GetDashboardAsync(GetAccountId(), GetTenantId(), ct);
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
            var result = await _portalService.GetTimesheetAsync(GetAccountId(), id, GetTenantId(), ct);
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
            var result = await _portalService.ConfirmTimesheetAsync(
                GetAccountId(),
                id,
                dto,
                GetTenantId(),
                ct);
            return Ok(result);
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
            var result = await _portalService.RejectTimesheetAsync(
                GetAccountId(),
                id,
                dto,
                GetTenantId(),
                ct);
            return Ok(result);
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

    [HttpPost("profile/change-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ConsultantClientPortalChangePasswordDto dto,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            await _authService.ChangePasswordAsync(GetAccountId(), dto, GetTenantId(), ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private Guid GetAccountId()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");
        if (id == null || !Guid.TryParse(id, out var accountId))
            throw new UnauthorizedAccessException("Invalid portal token.");
        return accountId;
    }

    private Guid GetTenantId()
    {
        var tenant = User.FindFirstValue("tenant_id");
        if (tenant == null || !Guid.TryParse(tenant, out var tenantId))
            throw new UnauthorizedAccessException("Invalid portal token.");
        return tenantId;
    }
}
