using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ErpSystem.Shared;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/employee-work-schedules")]
[Authorize(Policy = "InternalOnly")]
public class EmployeeWorkSchedulesController : AttendanceControllerBase
{
    private readonly IEmployeeWorkScheduleService _service;

    public EmployeeWorkSchedulesController(IEmployeeWorkScheduleService service, ICurrentUserService currentUser)
        : base(currentUser)
    {
        _service = service;
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = HrPermissions.AttendanceReadPolicy)]
    public async Task<ActionResult<EmployeeWorkScheduleDto>> GetById(Guid id, CancellationToken ct = default)
        => Ok(await _service.GetByIdAsync(id, ct));

    // W3: self-or-permission — an employee reads their own current schedule.
    [HttpGet("employee/{employeeId:guid}/current")]
    public async Task<ActionResult<EmployeeWorkScheduleDto?>> GetCurrentForEmployee(
        Guid employeeId, CancellationToken ct = default)
    {
        if (!await SelfOrPolicyAsync(employeeId, HrPermissions.AttendanceReadPolicy))
            return Forbid();
        return Ok(await _service.GetCurrentForEmployeeAsync(employeeId, ct));
    }

    // W3: self-or-permission — an employee reads their own schedule history.
    [HttpGet("employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<EmployeeWorkScheduleDto>>> GetAllForEmployee(
        Guid employeeId, CancellationToken ct = default)
    {
        if (!await SelfOrPolicyAsync(employeeId, HrPermissions.AttendanceReadPolicy))
            return Forbid();
        return Ok(await _service.GetAllForEmployeeAsync(employeeId, ct));
    }

    [HttpGet("work-schedule/{workScheduleId:guid}")]
    [Authorize(Policy = HrPermissions.AttendanceReadPolicy)]
    public async Task<ActionResult<IEnumerable<EmployeeWorkScheduleDto>>> GetByWorkScheduleId(
        Guid workScheduleId, CancellationToken ct = default)
        => Ok(await _service.GetByWorkScheduleIdAsync(workScheduleId, ct));

    [HttpGet("paged")]
    [Authorize(Policy = HrPermissions.AttendanceReadPolicy)]
    public async Task<ActionResult<PagedResult<EmployeeWorkScheduleDto>>> GetPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _service.GetPagedAsync(pageNumber, pageSize, ct));

    [HttpPost("assign")]
    [Authorize(Policy = HrPermissions.AttendanceWritePolicy)]
    public async Task<ActionResult<EmployeeWorkScheduleDto>> Assign(
        [FromBody] AssignEmployeeWorkScheduleDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetTenantAndEmployee(out var tenantId, out var employeeId) is { } error) return error;

        var created = await _service.AssignAsync(dto, tenantId, employeeId, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = HrPermissions.AttendanceWritePolicy)]
    public async Task<ActionResult<EmployeeWorkScheduleDto>> Update(
        Guid id, [FromBody] UpdateEmployeeWorkScheduleDto dto, CancellationToken ct = default)
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
