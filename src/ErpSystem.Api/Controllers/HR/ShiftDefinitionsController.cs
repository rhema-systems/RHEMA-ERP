using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/shift-definitions")]
[Authorize]
public class ShiftDefinitionsController : AttendanceControllerBase
{
    private readonly IShiftDefinitionService _service;

    public ShiftDefinitionsController(IShiftDefinitionService service, ICurrentUserService currentUser)
        : base(currentUser)
    {
        _service = service;
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ShiftDefinitionDto>> GetById(Guid id, CancellationToken ct = default)
        => Ok(await _service.GetByIdAsync(id, ct));

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ShiftDefinitionSummaryDto>>> GetAll(CancellationToken ct = default)
        => Ok(await _service.GetAllAsync(ct));

    [HttpGet("work-schedule/{workScheduleId:guid}")]
    public async Task<ActionResult<IEnumerable<ShiftDefinitionSummaryDto>>> GetByWorkScheduleId(
        Guid workScheduleId, CancellationToken ct = default)
        => Ok(await _service.GetByWorkScheduleIdAsync(workScheduleId, ct));

    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<ShiftDefinitionSummaryDto>>> GetActiveShifts(CancellationToken ct = default)
        => Ok(await _service.GetActiveShiftsAsync(ct));

    [HttpGet("night")]
    public async Task<ActionResult<IEnumerable<ShiftDefinitionSummaryDto>>> GetNightShifts(CancellationToken ct = default)
        => Ok(await _service.GetNightShiftsAsync(ct));

    [HttpGet("type/{type}")]
    public async Task<ActionResult<IEnumerable<ShiftDefinitionSummaryDto>>> GetByType(
        ShiftType type, CancellationToken ct = default)
        => Ok(await _service.GetByTypeAsync(type, ct));

    [HttpPost]
    public async Task<ActionResult<ShiftDefinitionDto>> Create(
        [FromBody] CreateShiftDefinitionDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetTenantAndEmployee(out var tenantId, out var employeeId) is { } error) return error;

        var created = await _service.CreateAsync(dto, tenantId, employeeId, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ShiftDefinitionDto>> Update(
        Guid id, [FromBody] UpdateShiftDefinitionDto dto, CancellationToken ct = default)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetEmployee(out var employeeId) is { } error) return error;

        return Ok(await _service.UpdateAsync(dto, employeeId, ct));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }
}
