using ErpSystem.Api.Filters;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums.Safety;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/safety/inspections")]
[SafetyBusinessRules]
[Authorize(Policy = "InternalOnly")]
public class SafetyInspectionController : SheApiControllerBase
{
    private readonly ISafetyInspectionService _service;

    public SafetyInspectionController(ISafetyInspectionService service, ICurrentUserService currentUser)
        : base(currentUser) => _service = service;

    [HttpGet]
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    public async Task<ActionResult<IEnumerable<SafetyInspectionSummaryDto>>> GetAll()
        => Ok(await _service.GetAllAsync());

    [HttpGet("{id:guid}")]
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    public async Task<ActionResult<SafetyInspectionDto>> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id));

    [HttpGet("number/{inspectionNumber}")]
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    public async Task<ActionResult<SafetyInspectionDto?>> GetByNumber(string inspectionNumber)
        => Ok(await _service.GetByNumberAsync(inspectionNumber));

    [HttpGet("status/{status}")]
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    public async Task<ActionResult<IEnumerable<SafetyInspectionSummaryDto>>> GetByStatus(SheInspectionStatus status)
        => Ok(await _service.GetByStatusAsync(status));

    [HttpGet("type/{type}")]
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    public async Task<ActionResult<IEnumerable<SafetyInspectionSummaryDto>>> GetByType(SheInspectionType type)
        => Ok(await _service.GetByTypeAsync(type));

    [HttpGet("category/{category}")]
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    public async Task<ActionResult<IEnumerable<SafetyInspectionSummaryDto>>> GetByCategory(SheInspectionCategory category)
        => Ok(await _service.GetByCategoryAsync(category));

    [HttpGet("date-range")]
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    public async Task<ActionResult<IEnumerable<SafetyInspectionSummaryDto>>> GetByDateRange([FromQuery] DateTime from, [FromQuery] DateTime to)
        => Ok(await _service.GetByDateRangeAsync(from, to));

    [HttpGet("inspector/{inspectorId:guid}")]
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    public async Task<ActionResult<IEnumerable<SafetyInspectionSummaryDto>>> GetByInspector(Guid inspectorId)
        => Ok(await _service.GetByInspectorAsync(inspectorId));

    [HttpGet("location/{locationId:guid}")]
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    public async Task<ActionResult<IEnumerable<SafetyInspectionSummaryDto>>> GetByLocation(Guid locationId)
        => Ok(await _service.GetByLocationAsync(locationId));

    [HttpGet("due")]
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    public async Task<ActionResult<IEnumerable<SafetyInspectionSummaryDto>>> GetDue([FromQuery] int daysAhead = 30)
        => Ok(await _service.GetDueAsync(daysAhead));

    [HttpGet("open-with-findings")]
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    public async Task<ActionResult<IEnumerable<SafetyInspectionSummaryDto>>> GetOpenWithFindings()
        => Ok(await _service.GetOpenWithFindingsAsync());

    [HttpPost]
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    public async Task<ActionResult<SafetyInspectionDto>> Create([FromBody] CreateSafetyInspectionDto dto)
    {
        var created = await _service.CreateAsync(dto, TenantId, UserId);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    public async Task<ActionResult<SafetyInspectionDto>> Update(Guid id, [FromBody] UpdateSafetyInspectionDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateAsync(dto, UserId));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }

    [HttpPost("{id:guid}/close")]
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    public async Task<IActionResult> Close(Guid id, [FromBody] CloseSafetyInspectionDto dto)
    {
        dto.InspectionId = id;
        await _service.CloseAsync(dto, UserId);
        return Ok(new { message = "Inspection closed." });
    }

    // ── Items ──
    [HttpPost("{id:guid}/items")]
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    public async Task<ActionResult<SafetyInspectionItemDto>> AddItem(Guid id, [FromBody] CreateSafetyInspectionItemDto dto)
    {
        dto.InspectionId = id;
        return Ok(await _service.AddItemAsync(dto, TenantId, UserId));
    }

    [HttpPut("items/{itemId:guid}")]
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    public async Task<ActionResult<SafetyInspectionItemDto>> UpdateItem(Guid itemId, [FromBody] UpdateSafetyInspectionItemDto dto)
    {
        if (itemId != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateItemAsync(dto, UserId));
    }

    [HttpDelete("items/{itemId:guid}")]
    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    public async Task<IActionResult> DeleteItem(Guid itemId)
    {
        await _service.DeleteItemAsync(itemId);
        return NoContent();
    }

    // ── Discovered hazards ──
    [HttpPost("{id:guid}/hazards")]
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    public async Task<ActionResult<SafetyInspectionHazardDto>> AddHazard(Guid id, [FromBody] CreateSafetyInspectionHazardDto dto)
    {
        dto.InspectionId = id;
        return Ok(await _service.AddHazardAsync(dto, TenantId, UserId));
    }

    [HttpPut("hazards/{hazardId:guid}")]
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    public async Task<ActionResult<SafetyInspectionHazardDto>> UpdateHazard(Guid hazardId, [FromBody] UpdateSafetyInspectionHazardDto dto)
    {
        if (hazardId != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateHazardAsync(dto, UserId));
    }

    [HttpDelete("hazards/{hazardId:guid}")]
    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    public async Task<IActionResult> DeleteHazard(Guid hazardId)
    {
        await _service.DeleteHazardAsync(hazardId);
        return NoContent();
    }

    [HttpPost("hazards/{hazardId:guid}/actions")]
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    public async Task<ActionResult<SafetyInspectionHazardActionDto>> AddHazardAction(Guid hazardId, [FromBody] CreateSafetyInspectionHazardActionDto dto)
    {
        dto.InspectionHazardId = hazardId;
        return Ok(await _service.AddHazardActionAsync(dto, TenantId, UserId));
    }

    [HttpPut("hazard-actions/{actionId:guid}")]
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    public async Task<ActionResult<SafetyInspectionHazardActionDto>> UpdateHazardAction(Guid actionId, [FromBody] UpdateSafetyInspectionHazardActionDto dto)
    {
        if (actionId != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateHazardActionAsync(dto, UserId));
    }

    [HttpDelete("hazard-actions/{actionId:guid}")]
    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    public async Task<IActionResult> DeleteHazardAction(Guid actionId)
    {
        await _service.DeleteHazardActionAsync(actionId);
        return NoContent();
    }

    // ── Documents ──
    [HttpPost("{id:guid}/documents")]
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    public async Task<ActionResult<SafetyInspectionDocumentDto>> AddDocument(Guid id, [FromBody] CreateSafetyInspectionDocumentDto dto)
    {
        dto.InspectionId = id;
        return Ok(await _service.AddDocumentAsync(dto, TenantId, UserId));
    }

    [HttpDelete("documents/{documentId:guid}")]
    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    public async Task<IActionResult> DeleteDocument(Guid documentId)
    {
        await _service.DeleteDocumentAsync(documentId);
        return NoContent();
    }

    // ── Checklist run (docs/HR/HR-SHE-INSPECTION-CHECKLIST-BUILDER-DESIGN.md §4) ──
    /// <summary>Loads a published template's items onto an inspection that has none yet.</summary>
    [HttpPost("{id:guid}/apply-checklist")]
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    public async Task<ActionResult<SafetyInspectionDto>> ApplyChecklist(Guid id, [FromBody] ApplySafetyInspectionChecklistDto dto)
        => Ok(await _service.ApplyChecklistAsync(id, dto, UserId));

    /// <summary>Bulk answer — the walk, saved in one call.</summary>
    [HttpPut("{id:guid}/responses")]
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    public async Task<ActionResult<SafetyInspectionDto>> SaveResponses(Guid id, [FromBody] List<SafetyInspectionResponseDto> responses)
        => Ok(await _service.SaveResponsesAsync(id, responses, UserId));

    /// <summary>Replace-set of the header field values: a field missing from the body is cleared.</summary>
    [HttpPut("{id:guid}/field-values")]
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    public async Task<ActionResult<SafetyInspectionDto>> SaveFieldValues(Guid id, [FromBody] List<SafetyInspectionFieldValueWriteDto> values)
        => Ok(await _service.SaveFieldValuesAsync(id, values, TenantId, UserId));

    [HttpGet("{id:guid}/score")]
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    public async Task<ActionResult<SafetyInspectionScoreDto>> GetScore(Guid id)
        => Ok(await _service.GetScoreAsync(id));

    /// <summary>Gate + persist the score and outcome; refused (422) while items are unassessed or required fields empty.</summary>
    [HttpPost("{id:guid}/complete")]
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    public async Task<ActionResult<SafetyInspectionDto>> Complete(Guid id, [FromBody] CompleteSafetyInspectionDto dto)
        => Ok(await _service.CompleteAsync(id, dto, UserId));

    /// <summary>System-user signatories sign as the token's employee; external ones need a typed name.</summary>
    [HttpPost("{id:guid}/signatures")]
    [Authorize(Policy = HrPermissions.SheWritePolicy)]
    public async Task<ActionResult<SafetyInspectionSignatureDto>> AddSignature(Guid id, [FromBody] CreateSafetyInspectionSignatureDto dto)
        => Ok(await _service.AddSignatureAsync(id, dto, TenantId, UserId));

    [HttpDelete("signatures/{signatureId:guid}")]
    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    public async Task<IActionResult> DeleteSignature(Guid signatureId)
    {
        await _service.DeleteSignatureAsync(signatureId);
        return NoContent();
    }
}
