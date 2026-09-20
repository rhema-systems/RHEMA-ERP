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
[Route("api/safety/inspection-checklists")]
[SafetyBusinessRules]
[Authorize(Policy = "InternalOnly")]
public class SheInspectionChecklistController : SheApiControllerBase
{
    private readonly ISheInspectionChecklistService _service;

    public SheInspectionChecklistController(ISheInspectionChecklistService service, ICurrentUserService currentUser)
        : base(currentUser) => _service = service;

    [HttpGet]
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    public async Task<ActionResult<IEnumerable<SheInspectionChecklistDto>>> GetAll([FromQuery] bool activeOnly = false)
        => Ok(await _service.GetAllAsync(activeOnly));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    public async Task<ActionResult<SheInspectionChecklistDto>> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id));

    [HttpGet("type/{type}")]
    [Authorize(Policy = HrPermissions.SheReadPolicy)]
    public async Task<ActionResult<IEnumerable<SheInspectionChecklistDto>>> GetByType(SheInspectionType type)
        => Ok(await _service.GetByTypeAsync(type));

    [HttpPost]
    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    public async Task<ActionResult<SheInspectionChecklistDto>> Create([FromBody] CreateSheInspectionChecklistDto dto)
    {
        var created = await _service.CreateAsync(dto, TenantId, UserId);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    public async Task<ActionResult<SheInspectionChecklistDto>> Update(Guid id, [FromBody] UpdateSheInspectionChecklistDto dto)
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

    // ── Items ──
    [HttpPost("{id:guid}/items")]
    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    public async Task<ActionResult<SheInspectionChecklistItemDto>> AddItem(Guid id, [FromBody] CreateSheInspectionChecklistItemDto dto)
    {
        dto.ChecklistId = id;
        return Ok(await _service.AddItemAsync(dto, TenantId, UserId));
    }

    [HttpPut("items/{itemId:guid}")]
    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    public async Task<ActionResult<SheInspectionChecklistItemDto>> UpdateItem(Guid itemId, [FromBody] UpdateSheInspectionChecklistItemDto dto)
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

    // ── Builder: lifecycle (docs/HR/areas/she/HR-SHE-INSPECTION-CHECKLIST-BUILDER-DESIGN.md §4) ──
    [HttpPost("{id:guid}/publish")]
    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    public async Task<ActionResult<SheInspectionChecklistDto>> Publish(Guid id)
        => Ok(await _service.PublishAsync(id, UserId));

    [HttpPost("{id:guid}/retire")]
    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    public async Task<ActionResult<SheInspectionChecklistDto>> Retire(Guid id)
        => Ok(await _service.RetireAsync(id, UserId));

    [HttpPost("{id:guid}/new-version")]
    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    public async Task<ActionResult<SheInspectionChecklistDto>> NewVersion(Guid id)
    {
        var created = await _service.CreateNewVersionAsync(id, TenantId, UserId);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    // ── Builder: header fields ──
    [HttpPost("{id:guid}/fields")]
    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    public async Task<ActionResult<SheInspectionChecklistFieldDto>> AddField(Guid id, [FromBody] CreateSheInspectionChecklistFieldDto dto)
    {
        dto.ChecklistId = id;
        return Ok(await _service.AddFieldAsync(dto, TenantId, UserId));
    }

    [HttpPut("fields/{fieldId:guid}")]
    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    public async Task<ActionResult<SheInspectionChecklistFieldDto>> UpdateField(Guid fieldId, [FromBody] UpdateSheInspectionChecklistFieldDto dto)
    {
        if (fieldId != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateFieldAsync(dto, UserId));
    }

    [HttpDelete("fields/{fieldId:guid}")]
    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    public async Task<IActionResult> DeleteField(Guid fieldId)
    {
        await _service.DeleteFieldAsync(fieldId);
        return NoContent();
    }

    [HttpPut("{id:guid}/fields/order")]
    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    public async Task<ActionResult<SheInspectionChecklistDto>> ReorderFields(Guid id, [FromBody] SheChecklistReorderDto dto)
        => Ok(await _service.ReorderFieldsAsync(id, dto, UserId));

    // ── Builder: sections ──
    [HttpPost("{id:guid}/sections")]
    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    public async Task<ActionResult<SheInspectionChecklistSectionDto>> AddSection(Guid id, [FromBody] CreateSheInspectionChecklistSectionDto dto)
    {
        dto.ChecklistId = id;
        return Ok(await _service.AddSectionAsync(dto, TenantId, UserId));
    }

    [HttpPut("sections/{sectionId:guid}")]
    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    public async Task<ActionResult<SheInspectionChecklistSectionDto>> UpdateSection(Guid sectionId, [FromBody] UpdateSheInspectionChecklistSectionDto dto)
    {
        if (sectionId != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateSectionAsync(dto, UserId));
    }

    [HttpDelete("sections/{sectionId:guid}")]
    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    public async Task<IActionResult> DeleteSection(Guid sectionId)
    {
        await _service.DeleteSectionAsync(sectionId);
        return NoContent();
    }

    [HttpPut("{id:guid}/sections/order")]
    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    public async Task<ActionResult<SheInspectionChecklistDto>> ReorderSections(Guid id, [FromBody] SheChecklistReorderDto dto)
        => Ok(await _service.ReorderSectionsAsync(id, dto, UserId));

    [HttpPut("sections/{sectionId:guid}/items/order")]
    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    public async Task<ActionResult<SheInspectionChecklistDto>> ReorderSectionItems(Guid sectionId, [FromBody] SheChecklistReorderDto dto)
        => Ok(await _service.ReorderSectionItemsAsync(sectionId, dto, UserId));

    // ── Builder: outcomes ──
    [HttpPost("{id:guid}/outcomes")]
    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    public async Task<ActionResult<SheInspectionChecklistOutcomeDto>> AddOutcome(Guid id, [FromBody] CreateSheInspectionChecklistOutcomeDto dto)
    {
        dto.ChecklistId = id;
        return Ok(await _service.AddOutcomeAsync(dto, TenantId, UserId));
    }

    [HttpPut("outcomes/{outcomeId:guid}")]
    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    public async Task<ActionResult<SheInspectionChecklistOutcomeDto>> UpdateOutcome(Guid outcomeId, [FromBody] UpdateSheInspectionChecklistOutcomeDto dto)
    {
        if (outcomeId != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateOutcomeAsync(dto, UserId));
    }

    [HttpDelete("outcomes/{outcomeId:guid}")]
    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    public async Task<IActionResult> DeleteOutcome(Guid outcomeId)
    {
        await _service.DeleteOutcomeAsync(outcomeId);
        return NoContent();
    }

    [HttpPut("{id:guid}/outcomes/order")]
    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    public async Task<ActionResult<SheInspectionChecklistDto>> ReorderOutcomes(Guid id, [FromBody] SheChecklistReorderDto dto)
        => Ok(await _service.ReorderOutcomesAsync(id, dto, UserId));

    // ── Builder: signatories ──
    [HttpPost("{id:guid}/signatories")]
    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    public async Task<ActionResult<SheInspectionChecklistSignatoryDto>> AddSignatory(Guid id, [FromBody] CreateSheInspectionChecklistSignatoryDto dto)
    {
        dto.ChecklistId = id;
        return Ok(await _service.AddSignatoryAsync(dto, TenantId, UserId));
    }

    [HttpPut("signatories/{signatoryId:guid}")]
    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    public async Task<ActionResult<SheInspectionChecklistSignatoryDto>> UpdateSignatory(Guid signatoryId, [FromBody] UpdateSheInspectionChecklistSignatoryDto dto)
    {
        if (signatoryId != dto.Id) return BadRequest("ID mismatch.");
        return Ok(await _service.UpdateSignatoryAsync(dto, UserId));
    }

    [HttpDelete("signatories/{signatoryId:guid}")]
    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    public async Task<IActionResult> DeleteSignatory(Guid signatoryId)
    {
        await _service.DeleteSignatoryAsync(signatoryId);
        return NoContent();
    }

    [HttpPut("{id:guid}/signatories/order")]
    [Authorize(Policy = HrPermissions.SheAdminPolicy)]
    public async Task<ActionResult<SheInspectionChecklistDto>> ReorderSignatories(Guid id, [FromBody] SheChecklistReorderDto dto)
        => Ok(await _service.ReorderSignatoriesAsync(id, dto, UserId));
}
