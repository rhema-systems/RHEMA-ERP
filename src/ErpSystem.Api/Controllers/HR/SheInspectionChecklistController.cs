using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums.Safety;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/safety/inspection-checklists")]
[Authorize]
public class SheInspectionChecklistController : SheApiControllerBase
{
    private readonly ISheInspectionChecklistService _service;

    public SheInspectionChecklistController(ISheInspectionChecklistService service, ICurrentUserService currentUser)
        : base(currentUser) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<SheInspectionChecklistDto>>> GetAll([FromQuery] bool activeOnly = false)
        => Ok(await _service.GetAllAsync(activeOnly));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SheInspectionChecklistDto>> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id));

    [HttpGet("type/{type}")]
    public async Task<ActionResult<IEnumerable<SheInspectionChecklistDto>>> GetByType(SheInspectionType type)
        => Ok(await _service.GetByTypeAsync(type));

    [HttpPost]
    public async Task<ActionResult<SheInspectionChecklistDto>> Create([FromBody] CreateSheInspectionChecklistDto dto)
    {
        var created = await _service.CreateAsync(dto, TenantId, UserId);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SheInspectionChecklistDto>> Update(Guid id, [FromBody] UpdateSheInspectionChecklistDto dto)
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

    // ── Items ──
    [HttpPost("{id:guid}/items")]
    public async Task<ActionResult<SheInspectionChecklistItemDto>> AddItem(Guid id, [FromBody] CreateSheInspectionChecklistItemDto dto)
    {
        dto.ChecklistId = id;
        return Ok(await _service.AddItemAsync(dto, TenantId, UserId));
    }

    [HttpPut("items/{itemId:guid}")]
    public async Task<ActionResult<SheInspectionChecklistItemDto>> UpdateItem(Guid itemId, [FromBody] UpdateSheInspectionChecklistItemDto dto)
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
}
