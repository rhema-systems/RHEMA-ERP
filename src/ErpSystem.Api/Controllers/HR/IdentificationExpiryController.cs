using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Services.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// The identification-expiry sweep: what is about to expire, and the pass that raises reminders.
/// </summary>
/// <remarks>
/// <para>Kept apart from <c>ReferenceDimensionsController</c> deliberately. That controller is
/// reference-data CRUD; this one is an engine. Putting the sweep's two endpoints there coupled the
/// two, so neither could be released or reverted without the other — a dependency created by where
/// the code was typed rather than by anything the features needed.</para>
///
/// <para>The sweep logic lives in <see cref="IIdentificationExpiryReminderService"/>, so the run-now
/// endpoint here and the nightly host exercise the same code path — the host/processor split every
/// other HR engine uses.</para>
/// </remarks>
[ApiController]
[Route("api/hr/identification-expiry")]
[Authorize(Policy = "InternalOnly")]
public class IdentificationExpiryController : ControllerBase
{
    private readonly IIdentificationExpiryReminderService _reminders;
    private readonly ILogger<IdentificationExpiryController> _logger;

    public IdentificationExpiryController(
        IIdentificationExpiryReminderService reminders,
        ILogger<IdentificationExpiryController> logger)
    {
        _reminders = reminders;
        _logger = logger;
    }

    /// <summary>What the sweep would raise now, without raising it.</summary>
    /// <remarks>
    /// ⚠ Gated on a POLICY, not filtered by recipient — so this is HR's view of what is expiring
    /// across the whole workforce, at both tiers. <c>RoutedToEmployeeId</c> decides whose job a card
    /// is, not who can see it.
    /// </remarks>
    [HttpGet("preview")]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [ProducesResponseType(typeof(IEnumerable<IdentificationExpiryReminderItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<IdentificationExpiryReminderItemDto>>> Preview(CancellationToken ct)
        => Ok(await _reminders.PreviewAsync(ct));

    /// <summary>The most recent passes, newest first.</summary>
    /// <remarks>
    /// A pass that queued nothing still appears. "The sweep ran and found nothing" and "the sweep
    /// never ran" are indistinguishable from the dispatch log alone, and only one is a defect —
    /// lane 1 found two nightly sweeps in this module that had never once executed.
    /// </remarks>
    [HttpGet("runs")]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [ProducesResponseType(typeof(IEnumerable<IdentificationExpiryRunDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<IdentificationExpiryRunDto>>> GetRuns(
        [FromQuery] int count = 20, CancellationToken ct = default)
        => Ok(await _reminders.GetRunsAsync(count, ct));

    /// <summary>Reminders actually raised, over a trailing window.</summary>
    /// <remarks>
    /// ⚠ The RAISED record, not a live view of the cards. Its dates are as they stood when the
    /// reminder went out — renewing a document raises a fresh reminder rather than rewriting this.
    /// </remarks>
    [HttpGet("log")]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [ProducesResponseType(typeof(IEnumerable<IdentificationExpiryLogEntryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<IdentificationExpiryLogEntryDto>>> GetLog(
        [FromQuery] int days = 14, CancellationToken ct = default)
        => Ok(await _reminders.GetLogAsync(days, ct));

    /// <summary>Runs the sweep now. The nightly host runs the same code path.</summary>
    [HttpPost("run")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(IdentificationExpiryRunResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<IdentificationExpiryRunResultDto>> Run(CancellationToken ct)
    {
        try
        {
            return Ok(await _reminders.RunSweepAsync("Manual", ct));
        }
        catch (InvalidOperationException ex)
        {
            // The middleware maps InvalidOperationException to a fixed string and discards the
            // message, so a refusal would otherwise reach the screen saying nothing.
            _logger.LogWarning("Identification expiry sweep rejected: {Message}", ex.Message);
            return UnprocessableEntity(new { message = ex.Message });
        }
    }
}
