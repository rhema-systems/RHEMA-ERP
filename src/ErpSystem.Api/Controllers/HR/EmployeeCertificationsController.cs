using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Services.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// What an employee holds: the credentials on their certification tab, and the compliance read that
/// sets them against what their position requires (demo feedback round 2, lane C2, plan § 6.3).
/// </summary>
/// <remarks>
/// The evidence file goes through the controlled upload gate on <c>EmployeeDocumentsController</c>
/// (<c>employee-certifications/{id}/evidence</c>), beside every other HR attachment. The verifier is
/// stamped from the token here, never read off a body — the actor lesson.
/// </remarks>
[ApiController]
[Route("api/hr/Employees/{employeeId:guid}/certifications")]
[Authorize(Policy = "InternalOnly")]
public class EmployeeCertificationsController : ControllerBase
{
    private readonly ICertificationService _service;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<EmployeeCertificationsController> _logger;

    public EmployeeCertificationsController(
        ICertificationService service,
        ICurrentUserService currentUser,
        ILogger<EmployeeCertificationsController> logger)
    {
        _service = service;
        _currentUser = currentUser;
        _logger = logger;
    }

    private ActionResult Rejected(InvalidOperationException ex, string action)
    {
        _logger.LogWarning("Employee certification rule rejected while {Action}: {Message}", action, ex.Message);
        return BadRequest(new { message = ex.Message });
    }

    [HttpGet]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [ProducesResponseType(typeof(IEnumerable<EmployeeCertificationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<EmployeeCertificationDto>>> GetAll(Guid employeeId, CancellationToken ct)
        => Ok(await _service.GetEmployeeCertificationsAsync(employeeId, ct));

    /// <summary>Required by the position, held by the person, expiring, expired, missing.</summary>
    [HttpGet("/api/hr/Employees/{employeeId:guid}/certification-compliance")]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [ProducesResponseType(typeof(EmployeeCertificationComplianceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeCertificationComplianceDto>> GetCompliance(Guid employeeId, CancellationToken ct)
    {
        try { return Ok(await _service.GetEmployeeComplianceAsync(employeeId, ct)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeReadPolicy)]
    [ProducesResponseType(typeof(EmployeeCertificationDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<EmployeeCertificationDto>> GetById(Guid employeeId, Guid id, CancellationToken ct)
    {
        try
        {
            var row = await _service.GetEmployeeCertificationAsync(id, ct);
            return row.EmployeeId == employeeId ? Ok(row) : NotFound();
        }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpPost]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(EmployeeCertificationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EmployeeCertificationDto>> Create(Guid employeeId, [FromBody] CreateEmployeeCertificationDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        dto.EmployeeId = employeeId;
        try
        {
            var created = await _service.AddEmployeeCertificationAsync(dto, ct);
            return CreatedAtAction(nameof(GetById), new { employeeId, id = created.Id }, created);
        }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Rejected(ex, "recording a credential"); }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(EmployeeCertificationDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<EmployeeCertificationDto>> Update(Guid employeeId, Guid id, [FromBody] UpdateEmployeeCertificationDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        dto.Id = id;
        try { return Ok(await _service.UpdateEmployeeCertificationAsync(dto, ct)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Rejected(ex, "updating a credential"); }
    }

    [HttpPost("{id:guid}/revoke")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(EmployeeCertificationDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<EmployeeCertificationDto>> Revoke(Guid employeeId, Guid id, [FromBody] RevokeEmployeeCertificationDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try { return Ok(await _service.RevokeEmployeeCertificationAsync(id, dto, ct)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Rejected(ex, "revoking a credential"); }
    }

    /// <summary>Marks the credential verified by the caller. The verifier comes from the token.</summary>
    [HttpPost("{id:guid}/verify")]
    [Authorize(Policy = HrPermissions.EmployeeWritePolicy)]
    [ProducesResponseType(typeof(EmployeeCertificationDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<EmployeeCertificationDto>> Verify(Guid employeeId, Guid id, CancellationToken ct)
    {
        try { return Ok(await _service.VerifyEmployeeCertificationAsync(id, _currentUser.EmployeeId, ct)); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Rejected(ex, "verifying a credential"); }
    }

    /// <summary>Admin tier, the same bar as removing an employee document.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = HrPermissions.EmployeeAdminPolicy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid employeeId, Guid id, CancellationToken ct)
    {
        try { await _service.RemoveEmployeeCertificationAsync(id, ct); return NoContent(); }
        catch (ArgumentException ex) { return NotFound(new { message = ex.Message }); }
    }
}
