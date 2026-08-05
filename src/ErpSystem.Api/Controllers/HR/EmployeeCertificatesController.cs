using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/employee-certificates")]
[Authorize]
public class EmployeeCertificatesController : ControllerBase
{
    private readonly IEmployeeCertificateService _service;
    private readonly ICurrentUserService _currentUser;

    public EmployeeCertificatesController(IEmployeeCertificateService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EmployeeCertificateDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await _service.GetByIdAsync(id, ct));

    [HttpGet("employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<EmployeeCertificateDto>>> GetByEmployeeId(Guid employeeId, CancellationToken ct)
        => Ok(await _service.GetByEmployeeIdAsync(employeeId, ct));

    [HttpGet("unverified")]
    public async Task<ActionResult<IEnumerable<EmployeeCertificateSummaryDto>>> GetUnverified(CancellationToken ct)
        => Ok(await _service.GetUnverifiedAsync(ct));

    [HttpGet("expiring")]
    public async Task<ActionResult<IEnumerable<EmployeeCertificateSummaryDto>>> GetExpiring(
        [FromQuery] int daysAhead = 30,
        CancellationToken ct = default)
        => Ok(await _service.GetExpiringAsync(daysAhead, ct));

    [HttpPost]
    public async Task<ActionResult<EmployeeCertificateDto>> Create([FromBody] CreateEmployeeCertificateDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null) return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        var created = await _service.CreateAsync(dto, tenantId.Value, employeeId.Value, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<EmployeeCertificateDto>> Update(Guid id, [FromBody] UpdateEmployeeCertificateDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");
        dto.Id = id;
        return Ok(await _service.UpdateAsync(dto, employeeId.Value, ct));
    }

    [HttpPost("{id:guid}/verify")]
    public async Task<IActionResult> Verify(Guid id, [FromBody] VerifyEmployeeCertificateDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        dto.CertificateId = id;
        await _service.VerifyAsync(dto, employeeId.Value, ct);
        return Ok(new { message = "Certificate verified." });
    }
}
