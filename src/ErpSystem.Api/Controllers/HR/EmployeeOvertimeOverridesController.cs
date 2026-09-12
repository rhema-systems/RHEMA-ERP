using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ErpSystem.Shared;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/employee-overtime-overrides")]
[Authorize(Policy = "InternalOnly")]
public class EmployeeOvertimeOverridesController : AttendanceControllerBase
{
    private readonly IEmployeeOvertimeOverrideService _service;

    public EmployeeOvertimeOverridesController(
        IEmployeeOvertimeOverrideService service,
        ICurrentUserService currentUser)
        : base(currentUser)
    {
        _service = service;
    }

    // W3: self-or-permission — an override carries a person's rates; ownership is only knowable
    // after the fetch.
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EmployeeOvertimeOverrideDto>> GetById(Guid id, CancellationToken ct = default)
    {
        var overrideDto = await _service.GetByIdAsync(id, ct);
        if (!await SelfOrPolicyAsync(overrideDto.EmployeeId, HrPermissions.AttendanceReadPolicy))
            return Forbid();
        return Ok(overrideDto);
    }

    // W3: self-or-permission — an employee reads their own overtime terms.
    [HttpGet("employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<EmployeeOvertimeOverrideDto>>> GetByEmployeeId(
        Guid employeeId, CancellationToken ct = default)
    {
        if (!await SelfOrPolicyAsync(employeeId, HrPermissions.AttendanceReadPolicy))
            return Forbid();
        return Ok(await _service.GetByEmployeeIdAsync(employeeId, ct));
    }

    // W3: self-or-permission — an employee reads their own active overtime terms.
    [HttpGet("employee/{employeeId:guid}/active")]
    public async Task<ActionResult<IEnumerable<EmployeeOvertimeOverrideDto>>> GetActiveOverridesForEmployee(
        Guid employeeId, CancellationToken ct = default)
    {
        if (!await SelfOrPolicyAsync(employeeId, HrPermissions.AttendanceReadPolicy))
            return Forbid();
        return Ok(await _service.GetActiveOverridesForEmployeeAsync(employeeId, ct));
    }

    [HttpPost]
    [Authorize(Policy = HrPermissions.AttendanceWritePolicy)]
    public async Task<ActionResult<EmployeeOvertimeOverrideDto>> Create(
        [FromBody] CreateEmployeeOvertimeOverrideDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetTenantAndEmployee(out var tenantId, out var employeeId) is { } error) return error;

        var created = await _service.CreateAsync(dto, tenantId, employeeId, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = HrPermissions.AttendanceWritePolicy)]
    public async Task<ActionResult<EmployeeOvertimeOverrideDto>> Update(
        Guid id, [FromBody] UpdateEmployeeOvertimeOverrideDto dto, CancellationToken ct = default)
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
