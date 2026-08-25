using ErpSystem.Api.Filters;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Admin surface for the discipline reminder engine (area 9 slice 8).
///
/// The daily background service and the run-now endpoint share one sweep implementation. Run-now
/// exists so HR can force a sweep after a bulk edit instead of waiting for tomorrow, and so the
/// engine is provable end-to-end — repeating it is safe, because dispatch is deduped per item.
///
/// HR-gated as a whole, unlike the case and appeal controllers: nothing here is the subject
/// employee's, and the log lists case and grievance numbers across the tenant.
/// </summary>
[ApiController]
[Route("api/discipline/reminders")]
[Authorize(Policy = "InternalOnly")]
[DisciplineBusinessRules]
public class DisciplineRemindersController : ControllerBase
{
    private readonly IDisciplineReminderService _service;
    private readonly ICurrentUserService _currentUser;

    public DisciplineRemindersController(
        IDisciplineReminderService service,
        ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    /// <summary>Runs a sweep for the authenticated tenant now. Safe to repeat.</summary>
    [HttpPost("run")]
    [Authorize(Policy = HrPermissions.DisciplineWritePolicy)]
    public async Task<ActionResult<DisciplineReminderRunResultDto>> Run()
    {
        if (_currentUser.TenantId is not Guid tenantId)
            return BadRequest("Tenant context could not be resolved.");

        Guid? userId = Guid.TryParse(_currentUser.UserId, out var parsed) ? parsed : null;

        return Ok(await _service.RunSweepForTenantAsync(tenantId, "Manual", userId));
    }

    /// <summary>
    /// What a sweep would fire, optionally as at a supplied date. Changes nothing.
    /// </summary>
    /// <remarks>
    /// <paramref name="asOf"/> is what makes the engine provable: an appeal's filed date and a
    /// grievance step's reached date are stamped by the server, so their overdue rungs cannot
    /// otherwise be reached inside a single test run. It claims no dedupe key, so previewing a future
    /// date cannot rob the real sweep of a reminder.
    /// </remarks>
    [HttpGet("preview")]
    [Authorize(Policy = HrPermissions.DisciplineReadPolicy)]
    public async Task<ActionResult<IEnumerable<DisciplineReminderPreviewItemDto>>> Preview(
        [FromQuery] DateTime? asOf = null)
        => Ok(await _service.PreviewSweepAsync(asOf));

    [HttpGet("runs")]
    [Authorize(Policy = HrPermissions.DisciplineReadPolicy)]
    public async Task<ActionResult<IEnumerable<DisciplineReminderRunDto>>> GetRuns([FromQuery] int count = 20)
        => Ok(await _service.GetRecentRunsAsync(count));

    [HttpGet("log")]
    [Authorize(Policy = HrPermissions.DisciplineReadPolicy)]
    public async Task<ActionResult<IEnumerable<DisciplineReminderLogEntryDto>>> GetLog([FromQuery] int days = 14)
        => Ok(await _service.GetRecentLogAsync(days));
}
