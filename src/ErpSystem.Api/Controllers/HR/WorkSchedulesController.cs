using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/work-schedules")]
[Authorize(Policy = "InternalOnly")]
public class WorkSchedulesController : AttendanceControllerBase
{
    private readonly IWorkScheduleService _service;

    public WorkSchedulesController(IWorkScheduleService service, ICurrentUserService currentUser)
        : base(currentUser)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<WorkScheduleSummaryDto>>> GetAll(CancellationToken ct = default)
        => Ok(await _service.GetAllAsync(ct));

    [HttpGet("paged")]
    public async Task<ActionResult<PagedResult<WorkScheduleSummaryDto>>> GetPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _service.GetPagedAsync(pageNumber, pageSize, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<WorkScheduleDto>> GetById(Guid id, CancellationToken ct = default)
        => Ok(await _service.GetByIdAsync(id, ct));

    [HttpGet("default")]
    public async Task<ActionResult<WorkScheduleDto?>> GetDefaultSchedule(CancellationToken ct = default)
        => Ok(await _service.GetDefaultScheduleAsync(ct));

    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<WorkScheduleSummaryDto>>> GetActiveSchedules(CancellationToken ct = default)
        => Ok(await _service.GetActiveSchedulesAsync(ct));

    [HttpGet("type/{type}")]
    public async Task<ActionResult<IEnumerable<WorkScheduleSummaryDto>>> GetByType(
        WorkScheduleType type, CancellationToken ct = default)
        => Ok(await _service.GetByTypeAsync(type, ct));

    [HttpPost]
    public async Task<ActionResult<WorkScheduleDto>> Create(
        [FromBody] CreateWorkScheduleDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetTenantAndEmployee(out var tenantId, out var employeeId) is { } error) return error;

        var created = await _service.CreateAsync(dto, tenantId, employeeId, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<WorkScheduleDto>> Update(
        Guid id, [FromBody] UpdateWorkScheduleDto dto, CancellationToken ct = default)
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

    [HttpGet("{id:guid}/shifts")]
    public async Task<ActionResult<IEnumerable<ShiftDefinitionSummaryDto>>> GetShifts(
        Guid id, CancellationToken ct = default)
        => Ok(await _service.GetShiftsAsync(id, ct));

    [HttpPost("{id:guid}/shifts")]
    public async Task<ActionResult<ShiftDefinitionDto>> AddShift(
        Guid id, [FromBody] CreateShiftDefinitionDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetTenantAndEmployee(out var tenantId, out var employeeId) is { } error) return error;

        dto.WorkScheduleId = id;
        var created = await _service.AddShiftAsync(dto, tenantId, employeeId, ct);
        return CreatedAtAction(nameof(GetShifts), new { id }, created);
    }

    [HttpPut("shifts/{shiftId:guid}")]
    public async Task<ActionResult<ShiftDefinitionDto>> UpdateShift(
        Guid shiftId, [FromBody] UpdateShiftDefinitionDto dto, CancellationToken ct = default)
    {
        if (shiftId != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetEmployee(out var employeeId) is { } error) return error;

        return Ok(await _service.UpdateShiftAsync(dto, employeeId, ct));
    }

    [HttpDelete("shifts/{shiftId:guid}")]
    public async Task<IActionResult> DeleteShift(Guid shiftId, CancellationToken ct = default)
    {
        await _service.DeleteShiftAsync(shiftId, ct);
        return NoContent();
    }
}
