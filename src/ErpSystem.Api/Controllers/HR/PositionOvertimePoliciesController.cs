using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/position-overtime-policies")]
[Authorize]
public class PositionOvertimePoliciesController : AttendanceControllerBase
{
    private readonly IPositionOvertimePolicyService _service;

    public PositionOvertimePoliciesController(
        IPositionOvertimePolicyService service,
        ICurrentUserService currentUser)
        : base(currentUser)
    {
        _service = service;
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PositionOvertimePolicyDto>> GetById(Guid id, CancellationToken ct = default)
        => Ok(await _service.GetByIdAsync(id, ct));

    [HttpGet("position/{positionId:guid}")]
    public async Task<ActionResult<IEnumerable<PositionOvertimePolicyDto>>> GetByPositionId(
        Guid positionId, CancellationToken ct = default)
        => Ok(await _service.GetByPositionIdAsync(positionId, ct));

    [HttpGet("allowance-type/{allowanceType}")]
    public async Task<ActionResult<IEnumerable<PositionOvertimePolicyDto>>> GetByAllowanceType(
        OvertimeAllowanceType allowanceType, CancellationToken ct = default)
        => Ok(await _service.GetByAllowanceTypeAsync(allowanceType, ct));

    [HttpGet("position/{positionId:guid}/active")]
    public async Task<ActionResult<PositionOvertimePolicyDto?>> GetActiveForPosition(
        Guid positionId,
        [FromQuery] OvertimeAllowanceType allowanceType,
        CancellationToken ct = default)
        => Ok(await _service.GetActiveForPositionAsync(positionId, allowanceType, ct));

    [HttpPost]
    public async Task<ActionResult<PositionOvertimePolicyDto>> Create(
        [FromBody] CreatePositionOvertimePolicyDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetTenantAndEmployee(out var tenantId, out var employeeId) is { } error) return error;

        var created = await _service.CreateAsync(dto, tenantId, employeeId, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PositionOvertimePolicyDto>> Update(
        Guid id, [FromBody] UpdatePositionOvertimePolicyDto dto, CancellationToken ct = default)
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

    [HttpGet("{id:guid}/overrides")]
    public async Task<ActionResult<IEnumerable<EmployeeOvertimeOverrideDto>>> GetOverrides(
        Guid id, CancellationToken ct = default)
        => Ok(await _service.GetOverridesAsync(id, ct));

    [HttpPost("{id:guid}/overrides")]
    public async Task<ActionResult<EmployeeOvertimeOverrideDto>> AddOverride(
        Guid id, [FromBody] CreateEmployeeOvertimeOverrideDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetTenantAndEmployee(out var tenantId, out var employeeId) is { } error) return error;

        dto.PolicyId = id;
        return Ok(await _service.AddOverrideAsync(dto, tenantId, employeeId, ct));
    }

    [HttpPut("overrides/{overrideId:guid}")]
    public async Task<ActionResult<EmployeeOvertimeOverrideDto>> UpdateOverride(
        Guid overrideId, [FromBody] UpdateEmployeeOvertimeOverrideDto dto, CancellationToken ct = default)
    {
        if (overrideId != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetEmployee(out var employeeId) is { } error) return error;

        return Ok(await _service.UpdateOverrideAsync(dto, employeeId, ct));
    }

    [HttpDelete("overrides/{overrideId:guid}")]
    public async Task<IActionResult> DeleteOverride(Guid overrideId, CancellationToken ct = default)
    {
        await _service.DeleteOverrideAsync(overrideId, ct);
        return NoContent();
    }
}
