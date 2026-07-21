using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums.Safety;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/safety/hazards")]
[Authorize]
public class SheHazardController : SheApiControllerBase
{
    private readonly ISheHazardService _service;

    public SheHazardController(ISheHazardService service, ICurrentUserService currentUser)
        : base(currentUser) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<SheHazardSummaryDto>>> GetAll([FromQuery] bool activeOnly = false)
        => Ok(await _service.GetAllAsync(activeOnly));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SheHazardDto>> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id));

    [HttpGet("code/{code}")]
    public async Task<ActionResult<SheHazardDto?>> GetByCode(string code)
        => Ok(await _service.GetByCodeAsync(code));

    [HttpGet("status/{status}")]
    public async Task<ActionResult<IEnumerable<SheHazardSummaryDto>>> GetByStatus(SheHazardStatus status)
        => Ok(await _service.GetByStatusAsync(status));

    [HttpGet("category/{category}")]
    public async Task<ActionResult<IEnumerable<SheHazardSummaryDto>>> GetByCategory(SheHazardCategory category)
        => Ok(await _service.GetByCategoryAsync(category));

    [HttpGet("risk-level/{level}")]
    public async Task<ActionResult<IEnumerable<SheHazardSummaryDto>>> GetByResidualRiskLevel(SheHazardRiskLevel level)
        => Ok(await _service.GetByResidualRiskLevelAsync(level));

    [HttpGet("location/{locationId:guid}")]
    public async Task<ActionResult<IEnumerable<SheHazardSummaryDto>>> GetByLocation(Guid locationId)
        => Ok(await _service.GetByLocationAsync(locationId));

    [HttpGet("owner/{ownerId:guid}")]
    public async Task<ActionResult<IEnumerable<SheHazardSummaryDto>>> GetByOwner(Guid ownerId)
        => Ok(await _service.GetByOwnerAsync(ownerId));

    [HttpGet("high-risk")]
    public async Task<ActionResult<IEnumerable<SheHazardSummaryDto>>> GetHighResidualRisk([FromQuery] int minimumScore = 12)
        => Ok(await _service.GetHighResidualRiskAsync(minimumScore));

    [HttpGet("due-for-review")]
    public async Task<ActionResult<IEnumerable<SheHazardSummaryDto>>> GetDueForReview([FromQuery] int daysAhead = 30)
        => Ok(await _service.GetDueForReviewAsync(daysAhead));

    [HttpPost]
    public async Task<ActionResult<SheHazardDto>> Create([FromBody] CreateSheHazardDto dto)
    {
        var created = await _service.CreateAsync(dto, TenantId, UserId);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SheHazardDto>> Update(Guid id, [FromBody] UpdateSheHazardDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateAsync(dto, UserId));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }

    // ── Controls ──
    [HttpPost("{id:guid}/controls")]
    public async Task<ActionResult<SheHazardControlDto>> AddControl(Guid id, [FromBody] CreateSheHazardControlDto dto)
    {
        dto.HazardId = id;
        return Ok(await _service.AddControlAsync(dto, TenantId, UserId));
    }

    [HttpPut("controls/{controlId:guid}")]
    public async Task<ActionResult<SheHazardControlDto>> UpdateControl(Guid controlId, [FromBody] UpdateSheHazardControlDto dto)
    {
        if (controlId != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateControlAsync(dto, UserId));
    }

    [HttpDelete("controls/{controlId:guid}")]
    public async Task<IActionResult> DeleteControl(Guid controlId)
    {
        await _service.DeleteControlAsync(controlId);
        return NoContent();
    }

    // ── Corrective actions ──
    [HttpPost("{id:guid}/corrective-actions")]
    public async Task<ActionResult<SheHazardCorrectiveActionDto>> AddCorrectiveAction(Guid id, [FromBody] CreateSheHazardCorrectiveActionDto dto)
    {
        dto.HazardId = id;
        return Ok(await _service.AddCorrectiveActionAsync(dto, TenantId, UserId));
    }

    [HttpDelete("corrective-actions/{correctiveActionId:guid}")]
    public async Task<IActionResult> DeleteCorrectiveAction(Guid correctiveActionId)
    {
        await _service.DeleteCorrectiveActionAsync(correctiveActionId);
        return NoContent();
    }
}
