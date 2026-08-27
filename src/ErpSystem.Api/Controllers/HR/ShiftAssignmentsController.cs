using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Which shift an employee is on, and from when.
/// </summary>
/// <remarks>
/// <para><b>The reads were missed by W3 and gated here in area 25 slice 14.</b> The writes were
/// converted correctly — <c>AttendanceWritePolicy</c> on create and update,
/// <c>AttendanceAdminPolicy</c> on delete — but every GET was left on the bare class-level
/// <c>InternalOnly</c>, so any internal user could read any employee's shift pattern, and
/// <c>active</c> returned the whole roster. This controller is one of the family that fell
/// between the attendance sweep and the scheduling one.</para>
///
/// <para><b>The by-employee reads are self-or-permission</b>, the same split W3 applied across
/// the rest of the attendance family: your own shift is yours to see, anyone else's needs
/// <c>HR.Attendance.Read</c>. The refusal is a 403 rather than an empty list because the caller
/// named a specific employee — there is no ambiguity about what they asked for.</para>
///
/// <para>The roster-shaped reads — by id, by shift definition, and <c>active</c> — are plain
/// <c>AttendanceRead</c>: none of them is about one nameable person, so there is no "self" arm
/// to offer.</para>
/// </remarks>
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
    [Authorize(Policy = HrPermissions.AttendanceReadPolicy)]
    public async Task<ActionResult<ShiftAssignmentDto>> GetById(Guid id, CancellationToken ct = default)
        => Ok(await _service.GetByIdAsync(id, ct));

    /// <summary>The employee's shift right now. Theirs to read; anyone else's needs the permission.</summary>
    [HttpGet("employee/{employeeId:guid}/current")]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ShiftAssignmentDto?>> GetCurrentAssignmentForEmployee(
        Guid employeeId, CancellationToken ct = default)
    {
        if (!await SelfOrPolicyAsync(employeeId, HrPermissions.AttendanceReadPolicy))
            return Forbid();

        return Ok(await _service.GetCurrentAssignmentForEmployeeAsync(employeeId, ct));
    }

    /// <summary>The employee's shift history. Same self-or-permission rule as the current one.</summary>
    [HttpGet("employee/{employeeId:guid}")]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<ShiftAssignmentDto>>> GetByEmployeeId(
        Guid employeeId, CancellationToken ct = default)
    {
        if (!await SelfOrPolicyAsync(employeeId, HrPermissions.AttendanceReadPolicy))
            return Forbid();

        return Ok(await _service.GetByEmployeeIdAsync(employeeId, ct));
    }

    [HttpGet("shift/{shiftDefinitionId:guid}")]
    [Authorize(Policy = HrPermissions.AttendanceReadPolicy)]
    public async Task<ActionResult<IEnumerable<ShiftAssignmentDto>>> GetByShiftDefinitionId(
        Guid shiftDefinitionId, CancellationToken ct = default)
        => Ok(await _service.GetByShiftDefinitionIdAsync(shiftDefinitionId, ct));

    [HttpGet("active")]
    [Authorize(Policy = HrPermissions.AttendanceReadPolicy)]
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
