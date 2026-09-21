using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Admin surface for the leave reminder engine (closure plan wave E, slice E2).
///
/// <para>The daily background service and the run-now endpoint share one sweep implementation.
/// Run-now exists so the leave desk can force a sweep after a bulk edit instead of waiting for
/// tomorrow, and so the engine is provable end-to-end — repeating it is safe, because dispatch is
/// deduped per item.</para>
///
/// <para>Gated on <c>HR.Leave.Admin</c> rather than the area's Read/Write pair: nothing here is one
/// employee's own record, the log lists references across the whole tenant, and forcing a sweep is
/// an administrative act. The same deliberate step up the travel engine makes.</para>
/// </summary>
[ApiController]
[Route("api/hr/leave/reminders")]
[Authorize(Policy = HrPermissions.LeaveAdminPolicy)]
public class LeaveRemindersController : ControllerBase
{
    private readonly ILeaveReminderService _service;
    private readonly ILeaveService _leaveService;
    private readonly ICurrentUserService _currentUser;

    public LeaveRemindersController(
        ILeaveReminderService service,
        ILeaveService leaveService,
        ICurrentUserService currentUser)
    {
        _service = service;
        _leaveService = leaveService;
        _currentUser = currentUser;
    }

    /// <summary>Runs a sweep for the authenticated tenant now. Safe to repeat.</summary>
    [HttpPost("run")]
    public async Task<ActionResult<LeaveReminderRunResultDto>> Run(CancellationToken ct)
    {
        if (_currentUser.TenantId is not Guid tenantId)
            return BadRequest("Tenant context could not be resolved.");

        Guid? userId = Guid.TryParse(_currentUser.UserId, out var parsed) ? parsed : null;
        return Ok(await _service.RunSweepForTenantAsync(tenantId, "Manual", userId, ct));
    }

    /// <summary>
    /// Reconciles the attendance register against the leave statuses that actually hold, now.
    /// </summary>
    /// <remarks>
    /// <para>The same reconciliation the nightly sweep performs, on demand. It exists for the same
    /// reason <c>run</c> does: a job that can only be triggered by a timer cannot be proved, and
    /// until this endpoint existed the reconciler was reachable from nothing but the 24-hour host —
    /// so no harness could exercise it and no operator could repair drift without waiting a day.
    /// The hr-leave suite is what surfaced that.</para>
    ///
    /// <para><b>The count it returns is a BUG SIGNAL, not a throughput figure.</b> Anything above
    /// zero means a leave status was changed without going through the leave service, and the
    /// service logs each repair as a warning for that reason.</para>
    /// </remarks>
    /// <param name="lookbackDays">How far back to look for requests whose status changed.</param>
    [HttpPost("reconcile-attendance")]
    public async Task<ActionResult> ReconcileAttendance(
        [FromQuery] int lookbackDays = 14, CancellationToken ct = default)
    {
        if (_currentUser.TenantId is not Guid tenantId)
            return BadRequest("Tenant context could not be resolved.");

        var repaired = await _leaveService.ReconcileRecentAttendanceAsync(tenantId, lookbackDays, ct);
        return Ok(new { repaired, lookbackDays });
    }

    /// <summary>
    /// Advance approved leave that has started into in-progress, on demand
    /// </summary>
    /// <remarks>
    /// <para>The same pass the nightly host runs, triggerable now — for the reason given on
    /// <c>reconcile-attendance</c> above: a job reachable only from a timer cannot be proved by a
    /// harness, and a recall cannot be demonstrated until something has advanced a request into the
    /// status recall is defined against.</para>
    ///
    /// <para>Unlike the reconciler, <b>a non-zero count here is entirely routine</b> — it is simply
    /// how many people started their leave today.</para>
    /// </remarks>
    [HttpPost("advance-in-progress")]
    public async Task<ActionResult> AdvanceInProgress(CancellationToken ct = default)
    {
        if (_currentUser.TenantId is not Guid tenantId)
            return BadRequest("Tenant context could not be resolved.");

        var advanced = await _leaveService.AdvanceLeaveInProgressAsync(tenantId, ct);
        return Ok(new { advanced });
    }

    /// <summary>
    /// What a sweep would fire, without firing it.
    /// </summary>
    /// <param name="asOf">
    /// Evaluate as if it were this instant. Every date in this engine is server-stamped, so without
    /// this a test can only assert what happens to be true today.
    /// </param>
    [HttpGet("preview")]
    public async Task<ActionResult<IEnumerable<LeaveReminderPreviewItemDto>>> Preview(
        [FromQuery] DateTime? asOf, CancellationToken ct)
        => Ok(await _service.PreviewSweepAsync(asOf, ct));

    /// <summary>Recent sweeps, newest first.</summary>
    [HttpGet("runs")]
    public async Task<ActionResult<IEnumerable<LeaveReminderRunDto>>> GetRuns(
        [FromQuery] int count = 20, CancellationToken ct = default)
        => Ok(await _service.GetRecentRunsAsync(count, ct));

    /// <summary>What has actually been dispatched recently.</summary>
    [HttpGet("log")]
    public async Task<ActionResult<IEnumerable<LeaveReminderLogEntryDto>>> GetLog(
        [FromQuery] int days = 14, CancellationToken ct = default)
        => Ok(await _service.GetRecentLogAsync(days, ct));
}
