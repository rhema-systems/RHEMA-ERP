using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/employee-biometrics")]
[Authorize]
public class EmployeeBiometricsController : AttendanceControllerBase
{
    private readonly IEmployeeBiometricService _service;

    public EmployeeBiometricsController(IEmployeeBiometricService service, ICurrentUserService currentUser)
        : base(currentUser)
    {
        _service = service;
    }

    [HttpGet("paged")]
    public async Task<ActionResult<PagedResult<EmployeeBiometricSummaryDto>>> GetPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _service.GetPagedAsync(pageNumber, pageSize, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EmployeeBiometricDto>> GetById(Guid id, CancellationToken ct = default)
        => Ok(await _service.GetByIdAsync(id, ct));

    [HttpGet("employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<EmployeeBiometricSummaryDto>>> GetByEmployeeId(
        Guid employeeId, CancellationToken ct = default)
        => Ok(await _service.GetByEmployeeIdAsync(employeeId, ct));

    [HttpGet("employee/{employeeId:guid}/active")]
    public async Task<ActionResult<IEnumerable<EmployeeBiometricSummaryDto>>> GetActiveBiometricsForEmployee(
        Guid employeeId, CancellationToken ct = default)
        => Ok(await _service.GetActiveBiometricsForEmployeeAsync(employeeId, ct));

    [HttpGet("employee/{employeeId:guid}/type/{type}")]
    public async Task<ActionResult<EmployeeBiometricDto?>> GetByEmployeeAndType(
        Guid employeeId, BiometricType type, CancellationToken ct = default)
        => Ok(await _service.GetByEmployeeAndTypeAsync(employeeId, type, ct));

    [HttpGet("type/{type}")]
    public async Task<ActionResult<IEnumerable<EmployeeBiometricSummaryDto>>> GetByType(
        BiometricType type, CancellationToken ct = default)
        => Ok(await _service.GetByTypeAsync(type, ct));

    [HttpPost("enrol")]
    public async Task<ActionResult<EmployeeBiometricDto>> Enrol(
        [FromBody] EnrollBiometricDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetTenantAndEmployee(out var tenantId, out var employeeId) is { } error) return error;

        var created = await _service.EnrolAsync(dto, tenantId, employeeId, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<EmployeeBiometricDto>> Update(
        Guid id, [FromBody] UpdateEmployeeBiometricDto dto, CancellationToken ct = default)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetEmployee(out var employeeId) is { } error) return error;

        return Ok(await _service.UpdateAsync(dto, employeeId, ct));
    }

    [HttpPost("{id:guid}/revoke")]
    public async Task<IActionResult> Revoke(
        Guid id, [FromBody] RevokeBiometricRequest request, CancellationToken ct = default)
    {
        if (TryGetEmployee(out var employeeId) is { } error) return error;

        await _service.RevokeAsync(id, request.Reason, employeeId, ct);
        return Ok(new { message = "Biometric revoked successfully." });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }

    public sealed class RevokeBiometricRequest
    {
        public string Reason { get; set; } = string.Empty;
    }
}
