using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Public endpoints for client timesheet confirmation via tokenised links.
/// </summary>
[ApiController]
[Route("api/client-timesheet-confirmation")]
[AllowAnonymous]
[EnableRateLimiting("PublicPortalPolicy")]
public class ClientTimesheetConfirmationController : ControllerBase
{
    private readonly IConsultantTimesheetService _service;
    private readonly ILogger<ClientTimesheetConfirmationController> _logger;

    public ClientTimesheetConfirmationController(
        IConsultantTimesheetService service,
        ILogger<ClientTimesheetConfirmationController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet("validate")]
    public async Task<ActionResult<ClientTimesheetConfirmationPublicDto>> Validate(
        [FromQuery] Guid token,
        CancellationToken ct = default)
    {
        if (token == Guid.Empty)
            return BadRequest(new { message = "Invalid token." });

        try
        {
            return Ok(await _service.ValidateConfirmationTokenAsync(token, ct));
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Timesheet confirmation validate failed: {Reason}", ex.Message);
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("confirm")]
    public async Task<ActionResult<ClientTimesheetConfirmationDto>> Confirm(
        [FromBody] ClientConfirmTimesheetDto dto,
        CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            return Ok(await _service.ConfirmByClientAsync(dto, ct));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("reject")]
    public async Task<ActionResult<ClientTimesheetConfirmationDto>> Reject(
        [FromBody] ClientRejectTimesheetDto dto,
        CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            return Ok(await _service.RejectByClientAsync(dto, ct));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

}
