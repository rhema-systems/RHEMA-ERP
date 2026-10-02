using ErpSystem.Api.Filters;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Admin surface for the staff-travel reminder engine (area 12 slice 5a).
///
/// <para>The daily background service and the run-now endpoint share one sweep implementation.
/// Run-now exists so the travel desk can force a sweep after a bulk edit instead of waiting for
/// tomorrow, and so the engine is provable end-to-end — repeating it is safe, because dispatch is
/// deduped per item.</para>
///
/// <para>Gated on <c>HR.Travel.Admin</c> rather than the area's Read/Write pair: nothing here is
/// one traveller's own record, the log lists references across the whole tenant, and forcing a
/// sweep is an administrative act. That is a deliberate step up from the rest of the area. Since the travel final
/// closure's lane 4 (D-3) the HR desk holds Admin, so the desk that acts on an expiring passport sees the queue
/// (T-52).</para>
/// </summary>
[ApiController]
[Route("api/staff-travel/reminders")]
[StaffTravelBusinessRules]
[Authorize(Policy = HrPermissions.TravelAdminPolicy)]
public class StaffTravelRemindersController : ControllerBase
{
    private readonly IStaffTravelReminderService _service;
    private readonly ICurrentUserService _currentUser;

    public StaffTravelRemindersController(
        IStaffTravelReminderService service,
        ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    /// <summary>Runs a sweep for the authenticated tenant now. Safe to repeat.</summary>
    [HttpPost("run")]
    public async Task<ActionResult<StaffTravelReminderRunResultDto>> Run(CancellationToken ct)
    {
        if (_currentUser.TenantId is not Guid tenantId)
            return BadRequest("Tenant context could not be resolved.");

        Guid? userId = Guid.TryParse(_currentUser.UserId, out var parsed) ? parsed : null;
        return Ok(await _service.RunSweepForTenantAsync(tenantId, "Manual", userId, ct));
    }

    /// <summary>
    /// What a sweep would fire, without firing it.
    /// </summary>
    /// <param name="asOf">
    /// Evaluate as if it were this instant. Every date in this engine is server-stamped, so without
    /// this a test can only assert what happens to be true today — the seam area 9 had to add after
    /// the fact.
    /// </param>
    [HttpGet("preview")]
    public async Task<ActionResult<IEnumerable<StaffTravelReminderPreviewItemDto>>> Preview(
        [FromQuery] DateTime? asOf, CancellationToken ct)
        => Ok(await _service.PreviewSweepAsync(asOf, ct));

    /// <summary>Recent sweeps, newest first.</summary>
    [HttpGet("runs")]
    public async Task<ActionResult<IEnumerable<StaffTravelReminderRunDto>>> GetRuns(
        [FromQuery] int count = 20, CancellationToken ct = default)
        => Ok(await _service.GetRecentRunsAsync(count, ct));

    /// <summary>What has actually been dispatched recently.</summary>
    [HttpGet("log")]
    public async Task<ActionResult<IEnumerable<StaffTravelReminderLogEntryDto>>> GetLog(
        [FromQuery] int days = 14, CancellationToken ct = default)
        => Ok(await _service.GetRecentLogAsync(days, ct));
}
