using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Admin surface for the orientation & onboarding reminder engine (round 4, lane K4).
/// </summary>
/// <remarks>
/// <para>The daily background service and the run-now endpoint share one sweep implementation.
/// Run-now exists so HR can force a sweep after a bulk change instead of waiting for tomorrow, and so
/// the engine is provable end to end — repeating it is safe, because every item is claimed once.</para>
///
/// <para>⚠ Run-now is gated on <c>HR.Orientation.Write</c>, not Admin: the HR role holds Read and
/// Write but not Admin, and a button HR cannot use is the company-schedule finding over again (a red
/// Delete that answered 403 to the people it was shown to). The reads are <c>HR.Orientation.Read</c>.</para>
/// </remarks>
[ApiController]
[Route("api/orientation-reminders")]
[Authorize(Policy = "InternalOnly")]
public class OnboardingOrientationRemindersController : ControllerBase
{
    private readonly IOnboardingOrientationReminderService _service;
    private readonly ICurrentUserService _currentUser;

    public OnboardingOrientationRemindersController(
        IOnboardingOrientationReminderService service,
        ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    /// <summary>Runs a sweep for the authenticated tenant now — claims, notifies and emails. Safe to repeat.</summary>
    [HttpPost("run")]
    [Authorize(Policy = HrPermissions.OrientationWritePolicy)]
    public async Task<ActionResult<OnboardingOrientationReminderRunResultDto>> Run()
    {
        if (_currentUser.TenantId is not Guid tenantId)
            return BadRequest("Tenant context could not be resolved.");

        Guid? userId = Guid.TryParse(_currentUser.UserId, out var parsed) ? parsed : null;
        return Ok(await _service.RunSweepForTenantAsync(tenantId, "Manual", userId));
    }

    /// <summary>What a sweep would remind, optionally as at a supplied date. Claims and sends nothing.</summary>
    /// <remarks>
    /// <paramref name="asOf"/> is what makes the rules provable inside one test run — a due date days
    /// away cannot otherwise be reached without waiting for the calendar.
    /// </remarks>
    [HttpGet("preview")]
    [Authorize(Policy = HrPermissions.OrientationReadPolicy)]
    public async Task<ActionResult<IEnumerable<OnboardingOrientationReminderPreviewItemDto>>> Preview(
        [FromQuery] DateTime? asOf = null)
        => Ok(await _service.PreviewSweepAsync(asOf));

    [HttpGet("runs")]
    [Authorize(Policy = HrPermissions.OrientationReadPolicy)]
    public async Task<ActionResult<IEnumerable<OnboardingOrientationReminderRunDto>>> GetRuns([FromQuery] int count = 20)
        => Ok(await _service.GetRecentRunsAsync(count));

    [HttpGet("log")]
    [Authorize(Policy = HrPermissions.OrientationReadPolicy)]
    public async Task<ActionResult<IEnumerable<OnboardingOrientationReminderLogEntryDto>>> GetLog([FromQuery] int days = 14)
        => Ok(await _service.GetRecentLogAsync(days));
}
