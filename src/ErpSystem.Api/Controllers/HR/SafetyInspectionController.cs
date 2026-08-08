using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums.Safety;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/safety/inspections")]
[Authorize]
public class SafetyInspectionController : SheApiControllerBase
{
    private readonly ISafetyInspectionService _service;

    public SafetyInspectionController(ISafetyInspectionService service, ICurrentUserService currentUser)
        : base(currentUser) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<SafetyInspectionSummaryDto>>> GetAll()
        => Ok(await _service.GetAllAsync());

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SafetyInspectionDto>> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id));

    [HttpGet("number/{inspectionNumber}")]
    public async Task<ActionResult<SafetyInspectionDto?>> GetByNumber(string inspectionNumber)
        => Ok(await _service.GetByNumberAsync(inspectionNumber));

    [HttpGet("status/{status}")]
    public async Task<ActionResult<IEnumerable<SafetyInspectionSummaryDto>>> GetByStatus(SheInspectionStatus status)
        => Ok(await _service.GetByStatusAsync(status));

    [HttpGet("type/{type}")]
    public async Task<ActionResult<IEnumerable<SafetyInspectionSummaryDto>>> GetByType(SheInspectionType type)
        => Ok(await _service.GetByTypeAsync(type));

    [HttpGet("category/{category}")]
    public async Task<ActionResult<IEnumerable<SafetyInspectionSummaryDto>>> GetByCategory(SheInspectionCategory category)
        => Ok(await _service.GetByCategoryAsync(category));

    [HttpGet("date-range")]
    public async Task<ActionResult<IEnumerable<SafetyInspectionSummaryDto>>> GetByDateRange([FromQuery] DateTime from, [FromQuery] DateTime to)
        => Ok(await _service.GetByDateRangeAsync(from, to));

    [HttpGet("inspector/{inspectorId:guid}")]
    public async Task<ActionResult<IEnumerable<SafetyInspectionSummaryDto>>> GetByInspector(Guid inspectorId)
        => Ok(await _service.GetByInspectorAsync(inspectorId));

    [HttpGet("location/{locationId:guid}")]
    public async Task<ActionResult<IEnumerable<SafetyInspectionSummaryDto>>> GetByLocation(Guid locationId)
        => Ok(await _service.GetByLocationAsync(locationId));

    [HttpGet("due")]
    public async Task<ActionResult<IEnumerable<SafetyInspectionSummaryDto>>> GetDue([FromQuery] int daysAhead = 30)
        => Ok(await _service.GetDueAsync(daysAhead));

    [HttpGet("open-with-findings")]
    public async Task<ActionResult<IEnumerable<SafetyInspectionSummaryDto>>> GetOpenWithFindings()
        => Ok(await _service.GetOpenWithFindingsAsync());

    [HttpPost]
    public async Task<ActionResult<SafetyInspectionDto>> Create([FromBody] CreateSafetyInspectionDto dto)
    {
        var created = await _service.CreateAsync(dto, TenantId, UserId);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SafetyInspectionDto>> Update(Guid id, [FromBody] UpdateSafetyInspectionDto dto)
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

    [HttpPost("{id:guid}/close")]
    public async Task<IActionResult> Close(Guid id, [FromBody] CloseSafetyInspectionDto dto)
    {
        dto.InspectionId = id;
        await _service.CloseAsync(dto, UserId);
        return Ok(new { message = "Inspection closed." });
    }

    // ── Items ──
    [HttpPost("{id:guid}/items")]
    public async Task<ActionResult<SafetyInspectionItemDto>> AddItem(Guid id, [FromBody] CreateSafetyInspectionItemDto dto)
    {
        dto.InspectionId = id;
        return Ok(await _service.AddItemAsync(dto, TenantId, UserId));
    }

    [HttpPut("items/{itemId:guid}")]
    public async Task<ActionResult<SafetyInspectionItemDto>> UpdateItem(Guid itemId, [FromBody] UpdateSafetyInspectionItemDto dto)
    {
        if (itemId != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateItemAsync(dto, UserId));
    }

    [HttpDelete("items/{itemId:guid}")]
    public async Task<IActionResult> DeleteItem(Guid itemId)
    {
        await _service.DeleteItemAsync(itemId);
        return NoContent();
    }

    // ── Discovered hazards ──
    [HttpPost("{id:guid}/hazards")]
    public async Task<ActionResult<SafetyInspectionHazardDto>> AddHazard(Guid id, [FromBody] CreateSafetyInspectionHazardDto dto)
    {
        dto.InspectionId = id;
        return Ok(await _service.AddHazardAsync(dto, TenantId, UserId));
    }

    [HttpPut("hazards/{hazardId:guid}")]
    public async Task<ActionResult<SafetyInspectionHazardDto>> UpdateHazard(Guid hazardId, [FromBody] UpdateSafetyInspectionHazardDto dto)
    {
        if (hazardId != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateHazardAsync(dto, UserId));
    }

    [HttpDelete("hazards/{hazardId:guid}")]
    public async Task<IActionResult> DeleteHazard(Guid hazardId)
    {
        await _service.DeleteHazardAsync(hazardId);
        return NoContent();
    }

    [HttpPost("hazards/{hazardId:guid}/actions")]
    public async Task<ActionResult<SafetyInspectionHazardActionDto>> AddHazardAction(Guid hazardId, [FromBody] CreateSafetyInspectionHazardActionDto dto)
    {
        dto.InspectionHazardId = hazardId;
        return Ok(await _service.AddHazardActionAsync(dto, TenantId, UserId));
    }

    [HttpPut("hazard-actions/{actionId:guid}")]
    public async Task<ActionResult<SafetyInspectionHazardActionDto>> UpdateHazardAction(Guid actionId, [FromBody] UpdateSafetyInspectionHazardActionDto dto)
    {
        if (actionId != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateHazardActionAsync(dto, UserId));
    }

    [HttpDelete("hazard-actions/{actionId:guid}")]
    public async Task<IActionResult> DeleteHazardAction(Guid actionId)
    {
        await _service.DeleteHazardActionAsync(actionId);
        return NoContent();
    }

    // ── Documents ──
    [HttpPost("{id:guid}/documents")]
    public async Task<ActionResult<SafetyInspectionDocumentDto>> AddDocument(Guid id, [FromBody] CreateSafetyInspectionDocumentDto dto)
    {
        dto.InspectionId = id;
        return Ok(await _service.AddDocumentAsync(dto, TenantId, UserId));
    }

    [HttpDelete("documents/{documentId:guid}")]
    public async Task<IActionResult> DeleteDocument(Guid documentId)
    {
        await _service.DeleteDocumentAsync(documentId);
        return NoContent();
    }
}
