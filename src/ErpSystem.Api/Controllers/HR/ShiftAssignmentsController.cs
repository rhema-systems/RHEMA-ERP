using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/shift-assignments")]
[Authorize(Policy = "InternalOnly")]
public class ShiftAssignmentsController : AttendanceControllerBase
{
    private readonly IShiftAssignmentService _service;

    public ShiftAssignmentsController(IShiftAssignmentService service, ICurrentUserService currentUser)
        : base(currentUser)
    {
        _service = service;
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ShiftAssignmentDto>> GetById(Guid id, CancellationToken ct = default)
        => Ok(await _service.GetByIdAsync(id, ct));

    [HttpGet("employee/{employeeId:guid}/current")]
    public async Task<ActionResult<ShiftAssignmentDto?>> GetCurrentAssignmentForEmployee(
        Guid employeeId, CancellationToken ct = default)
        => Ok(await _service.GetCurrentAssignmentForEmployeeAsync(employeeId, ct));

    [HttpGet("employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<ShiftAssignmentDto>>> GetByEmployeeId(
        Guid employeeId, CancellationToken ct = default)
        => Ok(await _service.GetByEmployeeIdAsync(employeeId, ct));

    [HttpGet("shift/{shiftDefinitionId:guid}")]
    public async Task<ActionResult<IEnumerable<ShiftAssignmentDto>>> GetByShiftDefinitionId(
        Guid shiftDefinitionId, CancellationToken ct = default)
        => Ok(await _service.GetByShiftDefinitionIdAsync(shiftDefinitionId, ct));

    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<ShiftAssignmentDto>>> GetActiveAssignments(
        [FromQuery] DateTime? asOf = null, CancellationToken ct = default)
        => Ok(await _service.GetActiveAssignmentsAsync(asOf, ct));

    [HttpPost]
    [Authorize(Policy = HrPermissions.AttendanceWritePolicy)]
    public async Task<ActionResult<ShiftAssignmentDto>> Assign(
        [FromBody] CreateShiftAssignmentDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetTenantAndEmployee(out var tenantId, out var employeeId) is { } error) return error;

        var created = await _service.AssignAsync(dto, tenantId, employeeId, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = HrPermissions.AttendanceWritePolicy)]
    public async Task<ActionResult<ShiftAssignmentDto>> Update(
        Guid id, [FromBody] UpdateShiftAssignmentDto dto, CancellationToken ct = default)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetEmployee(out var employeeId) is { } error) return error;

        return Ok(await _service.UpdateAsync(dto, employeeId, ct));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = HrPermissions.AttendanceAdminPolicy)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }
}
