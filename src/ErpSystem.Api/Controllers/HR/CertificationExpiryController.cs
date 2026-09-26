using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Services.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// The certification-expiry sweep: what it would raise, what it has raised, and a run-now.
/// The identification-expiry controller, one family over (demo feedback round 2, lane C2).
/// </summary>
[ApiController]
[Route("api/hr/certification-expiry")]
[Authorize(Policy = "InternalOnly")]
public class CertificationExpiryController : ControllerBase
{
    private readonly ICertificationExpiryReminderService _reminders;
    private readonly ILogger<CertificationExpiryController> _logger;

    public CertificationExpiryController(
        ICertificationExpiryReminderService reminders,
        ILogger<CertificationExpiryController> logger)
    {
        _reminders = reminders;
        _logger = logger;
    }

    [HttpGet("preview")]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [ProducesResponseType(typeof(IEnumerable<CertificationExpiryReminderItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<CertificationExpiryReminderItemDto>>> Preview(CancellationToken ct)
        => Ok(await _reminders.PreviewAsync(ct));

    [HttpGet("runs")]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [ProducesResponseType(typeof(IEnumerable<CertificationExpiryRunDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<CertificationExpiryRunDto>>> GetRuns(
        [FromQuery] int count = 20, CancellationToken ct = default)
        => Ok(await _reminders.GetRunsAsync(count, ct));

    [HttpGet("log")]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [ProducesResponseType(typeof(IEnumerable<CertificationExpiryLogEntryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<CertificationExpiryLogEntryDto>>> GetLog(
        [FromQuery] int days = 14, CancellationToken ct = default)
        => Ok(await _reminders.GetLogAsync(days, ct));

    [HttpPost("run")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(CertificationExpiryRunResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CertificationExpiryRunResultDto>> Run(CancellationToken ct)
    {
        try
        {
            return Ok(await _reminders.RunSweepAsync("Manual", ct));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Certification expiry sweep rejected: {Message}", ex.Message);
            return UnprocessableEntity(new { message = ex.Message });
        }
    }
}
