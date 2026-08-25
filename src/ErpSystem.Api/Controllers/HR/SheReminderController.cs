using ErpSystem.Api.Filters;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Admin surface for the SHE reminder engine (slice 13). The hourly background
/// service and the run-now endpoint share one sweep implementation; run-now exists
/// so the SHE team can force a sweep after bulk edits instead of waiting an hour,
/// and so the engine is provable end-to-end. HR-gated like the rest of the area —
/// reminders land with recipients through the notification pipeline; this surface
/// only operates the engine and reads its dispatch history.
/// </summary>
[ApiController]
[Route("api/safety/reminders")]
[SafetyBusinessRules]
[Authorize(Policy = "InternalOnly")]
public class SheReminderController : SheApiControllerBase
{
    private readonly ISheReminderService _service;

    public SheReminderController(ISheReminderService service, ICurrentUserService currentUser)
        : base(currentUser) => _service = service;

    /// <summary>Runs a sweep for the authenticated tenant now. Safe to repeat — dispatch is deduped.</summary>
    [HttpPost("run")]
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    public async Task<ActionResult<SheReminderRunResultDto>> Run()
        => Ok(await _service.RunSweepForTenantAsync(TenantId, "Manual", UserId));

    [HttpGet("runs")]
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    public async Task<ActionResult<IEnumerable<SheReminderRunDto>>> GetRuns([FromQuery] int count = 20)
        => Ok(await _service.GetRecentRunsAsync(count));

    [HttpGet("log")]
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    public async Task<ActionResult<IEnumerable<SheReminderLogEntryDto>>> GetLog([FromQuery] int days = 14)
        => Ok(await _service.GetRecentLogAsync(days));
}
