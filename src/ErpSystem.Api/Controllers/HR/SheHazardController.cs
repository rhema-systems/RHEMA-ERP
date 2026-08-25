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
/// Hazard register — HR only, with one exception: any authenticated employee can report a hazard
/// (FR-SHE-100 employee reporting). Everything else — the register reads, edits, controls and
/// corrective actions — is HR-gated. Employee self-service reads ("hazards I reported") are
/// deliberately deferred to the employee-portal work (area 25), not an oversight here.
/// </summary>
[ApiController]
[SafetyBusinessRules]
[Route("api/safety/hazards")]
[Authorize(Policy = "InternalOnly")]
public class SheHazardController : SheApiControllerBase
{
    // Gated per action rather than on the class: authorize attributes stack as AND, so a class-level
    // role requirement could not be relaxed for the one report action employees need.
    private readonly ISheHazardService _service;

    public SheHazardController(ISheHazardService service, ICurrentUserService currentUser)
        : base(currentUser) => _service = service;

    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<SheHazardSummaryDto>>> GetAll([FromQuery] bool activeOnly = false)
        => Ok(await _service.GetAllAsync(activeOnly));

    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SheHazardDto>> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id));

    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    [HttpGet("code/{code}")]
    public async Task<ActionResult<SheHazardDto?>> GetByCode(string code)
        => Ok(await _service.GetByCodeAsync(code));

    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    [HttpGet("status/{status}")]
    public async Task<ActionResult<IEnumerable<SheHazardSummaryDto>>> GetByStatus(SheHazardStatus status)
        => Ok(await _service.GetByStatusAsync(status));

    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    [HttpGet("category/{category}")]
    public async Task<ActionResult<IEnumerable<SheHazardSummaryDto>>> GetByCategory(SheHazardCategory category)
        => Ok(await _service.GetByCategoryAsync(category));

    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    [HttpGet("risk-level/{level}")]
    public async Task<ActionResult<IEnumerable<SheHazardSummaryDto>>> GetByResidualRiskLevel(SheHazardRiskLevel level)
        => Ok(await _service.GetByResidualRiskLevelAsync(level));

    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    [HttpGet("location/{locationId:guid}")]
    public async Task<ActionResult<IEnumerable<SheHazardSummaryDto>>> GetByLocation(Guid locationId)
        => Ok(await _service.GetByLocationAsync(locationId));

    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    [HttpGet("owner/{ownerId:guid}")]
    public async Task<ActionResult<IEnumerable<SheHazardSummaryDto>>> GetByOwner(Guid ownerId)
        => Ok(await _service.GetByOwnerAsync(ownerId));

    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    [HttpGet("high-risk")]
    public async Task<ActionResult<IEnumerable<SheHazardSummaryDto>>> GetHighResidualRisk([FromQuery] int minimumScore = 12)
        => Ok(await _service.GetHighResidualRiskAsync(minimumScore));

    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    [HttpGet("due-for-review")]
    public async Task<ActionResult<IEnumerable<SheHazardSummaryDto>>> GetDueForReview([FromQuery] int daysAhead = 30)
        => Ok(await _service.GetDueForReviewAsync(daysAhead));

    /// <summary>Open to any authenticated employee — hazard reporting must not be gatekept. The reporter is stamped from the token.</summary>
    [HttpPost]
    public async Task<ActionResult<SheHazardDto>> Create([FromBody] CreateSheHazardDto dto)
    {
        var created = await _service.CreateAsync(dto, TenantId, UserId);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SheHazardDto>> Update(Guid id, [FromBody] UpdateSheHazardDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateAsync(dto, UserId));
    }

    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }

    // ── Controls ──
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    [HttpPost("{id:guid}/controls")]
    public async Task<ActionResult<SheHazardControlDto>> AddControl(Guid id, [FromBody] CreateSheHazardControlDto dto)
    {
        dto.HazardId = id;
        return Ok(await _service.AddControlAsync(dto, TenantId, UserId));
    }

    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    [HttpPut("controls/{controlId:guid}")]
    public async Task<ActionResult<SheHazardControlDto>> UpdateControl(Guid controlId, [FromBody] UpdateSheHazardControlDto dto)
    {
        if (controlId != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateControlAsync(dto, UserId));
    }

    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    [HttpDelete("controls/{controlId:guid}")]
    public async Task<IActionResult> DeleteControl(Guid controlId)
    {
        await _service.DeleteControlAsync(controlId);
        return NoContent();
    }

    // ── Corrective actions ──
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    [HttpPost("{id:guid}/corrective-actions")]
    public async Task<ActionResult<SheHazardCorrectiveActionDto>> AddCorrectiveAction(Guid id, [FromBody] CreateSheHazardCorrectiveActionDto dto)
    {
        dto.HazardId = id;
        return Ok(await _service.AddCorrectiveActionAsync(dto, TenantId, UserId));
    }

    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    [HttpDelete("corrective-actions/{correctiveActionId:guid}")]
    public async Task<IActionResult> DeleteCorrectiveAction(Guid correctiveActionId)
    {
        await _service.DeleteCorrectiveActionAsync(correctiveActionId);
        return NoContent();
    }
}
