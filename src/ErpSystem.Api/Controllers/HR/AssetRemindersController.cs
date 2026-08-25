using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Admin surface for the asset reminder engine (area 16 slice 9, AST-1).
///
/// <para>The daily background service and the run-now endpoint share one sweep implementation.
/// Run-now exists so HR can force a sweep after a bulk edit instead of waiting for tomorrow, and so
/// the engine is provable end to end — repeating it is safe, because dispatch is deduped per
/// item.</para>
///
/// <para>HR-gated as a whole, and separate from <c>api/Assets</c> for that reason. Nothing here is
/// any one employee's, and the log names assets across the whole register — which is a different
/// thing to expose from "the laptop you are holding". The register controller carries eleven
/// self-service routes and this one carries none, so keeping them apart means no future route lands
/// on the wrong side of that line by inheriting a class attribute.</para>
/// </summary>
[ApiController]
[Route("api/assets/reminders")]
[Authorize(Policy = "InternalOnly")]
public class AssetRemindersController : ControllerBase
{
    private readonly IAssetReminderService _service;
    private readonly ICurrentUserService _currentUser;

    public AssetRemindersController(
        IAssetReminderService service,
        ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    /// <summary>Runs a sweep for the authenticated tenant now. Safe to repeat.</summary>
    [HttpPost("run")]
    [Authorize(Policy = HrPermissions.AssetsWritePolicy)]
    public async Task<ActionResult<AssetReminderRunResultDto>> Run()
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
    /// <paramref name="asOf"/> is what makes the engine provable: a maintenance schedule is written
    /// by the server from the asset's own interval, so the overdue rungs — the ones that matter —
    /// cannot otherwise be reached inside a single test run. It claims no dedupe key, so previewing
    /// a future date cannot rob the real sweep of a reminder.
    /// </remarks>
    [HttpGet("preview")]
    [Authorize(Policy = HrPermissions.AssetsReadPolicy)]
    public async Task<ActionResult<IEnumerable<AssetReminderPreviewItemDto>>> Preview(
        [FromQuery] DateTime? asOf = null)
        => Ok(await _service.PreviewSweepAsync(asOf));

    [HttpGet("runs")]
    [Authorize(Policy = HrPermissions.AssetsReadPolicy)]
    public async Task<ActionResult<IEnumerable<AssetReminderRunDto>>> GetRuns([FromQuery] int count = 20)
        => Ok(await _service.GetRecentRunsAsync(count));

    [HttpGet("log")]
    [Authorize(Policy = HrPermissions.AssetsReadPolicy)]
    public async Task<ActionResult<IEnumerable<AssetReminderLogEntryDto>>> GetLog([FromQuery] int days = 14)
        => Ok(await _service.GetRecentLogAsync(days));
}
