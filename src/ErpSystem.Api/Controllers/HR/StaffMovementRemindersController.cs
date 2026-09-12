using ErpSystem.Api.Filters;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Admin surface for the staff-movement reminder engine (area 8 slice 5).
///
/// The daily background service and the run-now endpoint share one sweep implementation. Run-now
/// exists so HR can force a sweep after a bulk edit instead of waiting for tomorrow, and so the
/// engine is provable end-to-end — repeating it is safe, because dispatch is deduped per item.
/// </summary>
[ApiController]
[Route("api/staff-movements/reminders")]
[Authorize(Policy = "InternalOnly")]
[MovementBusinessRules]
public class StaffMovementRemindersController : ControllerBase
{
    private readonly IStaffMovementReminderService _service;
    private readonly ICurrentUserService _currentUser;

    public StaffMovementRemindersController(
        IStaffMovementReminderService service,
        ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    /// <summary>Runs a sweep for the authenticated tenant now. Safe to repeat.</summary>
    [HttpPost("run")]
    [Authorize(Policy = HrPermissions.MovementsWritePolicy)]
    public async Task<ActionResult<StaffMovementReminderRunResultDto>> Run()
    {
        if (_currentUser.TenantId is not Guid tenantId)
            return BadRequest("Tenant context could not be resolved.");

        Guid? userId = Guid.TryParse(_currentUser.UserId, out var parsed) ? parsed : null;

        return Ok(await _service.RunSweepForTenantAsync(tenantId, "Manual", userId));
    }

    [HttpGet("runs")]
    [Authorize(Policy = HrPermissions.MovementsReadPolicy)]
    public async Task<ActionResult<IEnumerable<StaffMovementReminderRunDto>>> GetRuns([FromQuery] int count = 20)
        => Ok(await _service.GetRecentRunsAsync(count));

    [HttpGet("log")]
    [Authorize(Policy = HrPermissions.MovementsReadPolicy)]
    public async Task<ActionResult<IEnumerable<StaffMovementReminderLogEntryDto>>> GetLog([FromQuery] int days = 14)
        => Ok(await _service.GetRecentLogAsync(days));
}
