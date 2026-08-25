using ErpSystem.Api.Filters;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ErpSystem.Shared;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/employee-certificates")]
[Authorize(Policy = "InternalOnly")]
[TrainingBusinessRulesAttribute]
public class EmployeeCertificatesController : ControllerBase
{
    private readonly IEmployeeCertificateService _service;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuthorizationService _authorization;

    public EmployeeCertificatesController(
        IEmployeeCertificateService service,
        ICurrentUserService currentUser,
        IAuthorizationService authorization)
    {
        _service = service;
        _currentUser = currentUser;
        _authorization = authorization;
    }

    /// <summary>Self-or-permission (W3), as on LeavesController — see the remarks there.</summary>
    private async Task<bool> SelfOrPolicyAsync(Guid employeeId, string policy)
    {
        if (_currentUser.EmployeeId is Guid me && me != Guid.Empty && me == employeeId)
            return true;
        return (await _authorization.AuthorizeAsync(User, policy)).Succeeded;
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EmployeeCertificateDto>> GetById(Guid id, CancellationToken ct)
    {
        var certificate = await _service.GetByIdAsync(id, ct);
        if (!await SelfOrPolicyAsync(certificate.EmployeeId, HrPermissions.TrainingReadPolicy))
            return Forbid();
        return Ok(certificate);
    }

    /// <summary>The caller's own externally-held certificates, taken from the token.</summary>
    /// <remarks>
    /// The module's /mine convention: without it a self-service screen has to fetch its own employee
    /// id and pass it back through the id-bearing route below, which is the shape behind the
    /// read-anyone's-record holes closed elsewhere in HR.
    /// </remarks>
    [HttpGet("mine")]
    public async Task<ActionResult<IEnumerable<EmployeeCertificateDto>>> GetMine(CancellationToken ct)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return Forbid();
        return Ok(await _service.GetByEmployeeIdAsync(employeeId.Value, ct));
    }

    [HttpGet("employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<EmployeeCertificateDto>>> GetByEmployeeId(Guid employeeId, CancellationToken ct)
    {
        if (!await SelfOrPolicyAsync(employeeId, HrPermissions.TrainingReadPolicy))
            return Forbid();
        return Ok(await _service.GetByEmployeeIdAsync(employeeId, ct));
    }

    [HttpGet("unverified")]
    [Authorize(Policy = HrPermissions.TrainingReadPolicy)]
    public async Task<ActionResult<IEnumerable<EmployeeCertificateSummaryDto>>> GetUnverified(CancellationToken ct)
        => Ok(await _service.GetUnverifiedAsync(ct));

    [HttpGet("expiring")]
    [Authorize(Policy = HrPermissions.TrainingReadPolicy)]
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

        // W3: an employee registers their OWN externally-held certificate; registering one for
        // someone else is the desk's act. The subject comes from the body.
        if (!await SelfOrPolicyAsync(dto.EmployeeId, HrPermissions.TrainingWritePolicy))
            return Forbid();

        var created = await _service.CreateAsync(dto, tenantId.Value, employeeId.Value, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        // W3: removing one's own certificate record is a self act; anyone else's is administration.
        var certificate = await _service.GetByIdAsync(id, ct);
        if (!await SelfOrPolicyAsync(certificate.EmployeeId, HrPermissions.TrainingAdminPolicy))
            return Forbid();

        await _service.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<EmployeeCertificateDto>> Update(Guid id, [FromBody] UpdateEmployeeCertificateDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        // W3: ownership is read off the stored record, not the payload.
        var existing = await _service.GetByIdAsync(id, ct);
        if (!await SelfOrPolicyAsync(existing.EmployeeId, HrPermissions.TrainingWritePolicy))
            return Forbid();

        dto.Id = id;
        return Ok(await _service.UpdateAsync(dto, employeeId.Value, ct));
    }

    [HttpPost("{id:guid}/verify")]
    [Authorize(Policy = HrPermissions.TrainingWritePolicy)]
    public async Task<IActionResult> Verify(Guid id, [FromBody] VerifyEmployeeCertificateDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        dto.CertificateId = id;
        // Returns the updated record rather than a bare message: the service already builds the full
        // DTO (untracked re-read, so VerifiedByName resolves) and discarding it forced the caller to
        // refetch just to render the row it had just verified.
        return Ok(await _service.VerifyAsync(dto, employeeId.Value, ct));
    }
}
