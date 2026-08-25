using ErpSystem.Api.Filters;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums.Safety;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// The unified corrective-action tracker (FR-SHE-245) — a read-only union view
/// over the four live corrective-action stores (incident, inspection, equipment,
/// committee). Writes stay on each silo's own controller; this surface only
/// reads, so it is HR-gated class-level with no open actions.
/// </summary>
[ApiController]
[Route("api/safety/corrective-actions")]
[SafetyBusinessRules]
[Authorize(Policy = "InternalOnly")]
public class SheCorrectiveActionsController : SheApiControllerBase
{
    private readonly ISheCorrectiveActionTrackerService _service;

    public SheCorrectiveActionsController(ISheCorrectiveActionTrackerService service, ICurrentUserService currentUser)
        : base(currentUser) => _service = service;

    [HttpGet]
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    public async Task<ActionResult<IEnumerable<SheUnifiedCorrectiveActionDto>>> GetAll(
        [FromQuery] SheCorrectiveActionSource? source,
        [FromQuery] SheUnifiedActionStatus? status,
        [FromQuery] Guid? assignedToId,
        [FromQuery] bool overdueOnly = false,
        [FromQuery] DateTime? dueFrom = null,
        [FromQuery] DateTime? dueTo = null)
        => Ok(await _service.GetAllAsync(source, status, assignedToId, overdueOnly, dueFrom, dueTo));

    [HttpGet("summary")]
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    public async Task<ActionResult<SheUnifiedCorrectiveActionSummaryDto>> GetSummary()
        => Ok(await _service.GetSummaryAsync());
}
