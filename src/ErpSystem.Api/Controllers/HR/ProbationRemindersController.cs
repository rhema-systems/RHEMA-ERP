using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Admin surface for the probation reminder engine — FR-HR-032's month-5 confirmation form and
/// FR-HR-140's expiry notice (area 15b slice 7).
/// </summary>
/// <remarks>
/// <para>The daily background service and the run-now endpoint share one sweep implementation.
/// Run-now exists so HR can force a sweep after a bulk edit instead of waiting for tomorrow, and so
/// the engine is provable end to end — repeating it is safe, because dispatch is deduped per item.</para>
///
/// <para>Gated on <c>HR.Probation.Admin</c> as a whole, including the reads: the log lists who
/// across the tenant is on probation and whose review is overdue, which is a register of people
/// under assessment. Nothing here belongs to the subject employee.</para>
/// </remarks>
[ApiController]
[Route("api/probations/reminders")]
[Authorize(Policy = HrPermissions.ProbationAdminPolicy)]
public class ProbationRemindersController : ControllerBase
{
    private readonly IProbationReminderService _service;
    private readonly ICurrentUserService _currentUser;

    public ProbationRemindersController(
        IProbationReminderService service,
        ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    /// <summary>Runs a sweep for the authenticated tenant now. Safe to repeat.</summary>
    [HttpPost("run")]
    public async Task<ActionResult<ProbationReminderRunResultDto>> Run()
    {
        if (_currentUser.TenantId is not Guid tenantId)
            return BadRequest("Tenant context could not be resolved.");

        Guid? userId = Guid.TryParse(_currentUser.UserId, out var parsed) ? parsed : null;

        return Ok(await _service.RunSweepForTenantAsync(tenantId, "Manual", userId));
    }

    /// <summary>What a sweep would fire, optionally as at a supplied date. Changes nothing.</summary>
    /// <remarks>
    /// <paramref name="asOf"/> is what makes the engine provable inside a single test run: a
    /// probation's end date is months away, so its ending-soon and overdue rungs cannot otherwise be
    /// reached without waiting for the calendar. A preview claims no dedupe key, so previewing a
    /// future date cannot rob the real sweep of a reminder.
    /// </remarks>
    [HttpGet("preview")]
    public async Task<ActionResult<IEnumerable<ProbationReminderPreviewItemDto>>> Preview(
        [FromQuery] DateTime? asOf = null)
        => Ok(await _service.PreviewSweepAsync(asOf));

    [HttpGet("runs")]
    public async Task<ActionResult<IEnumerable<ProbationReminderRunDto>>> GetRuns([FromQuery] int count = 20)
        => Ok(await _service.GetRecentRunsAsync(count));

    [HttpGet("log")]
    public async Task<ActionResult<IEnumerable<ProbationReminderLogEntryDto>>> GetLog([FromQuery] int days = 14)
        => Ok(await _service.GetRecentLogAsync(days));
}
