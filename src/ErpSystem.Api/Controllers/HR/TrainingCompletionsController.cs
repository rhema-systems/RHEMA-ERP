using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/training-completions")]
[Authorize]
public class TrainingCompletionsController : ControllerBase
{
    private readonly ITrainingCompletionService _service;
    private readonly ICurrentUserService _currentUser;

    public TrainingCompletionsController(ITrainingCompletionService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    // =========================================================================
    // QUERIES
    // =========================================================================

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TrainingCompletionDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await _service.GetByIdAsync(id, ct));

    [HttpGet("nomination/{nominationId:guid}")]
    public async Task<ActionResult<TrainingCompletionDto?>> GetByNominationId(Guid nominationId, CancellationToken ct)
        => Ok(await _service.GetByNominationIdAsync(nominationId, ct));

    [HttpGet("employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<TrainingCompletionDto>>> GetByEmployeeId(Guid employeeId, CancellationToken ct)
        => Ok(await _service.GetByEmployeeIdAsync(employeeId, ct));

    [HttpGet("schedule/{scheduleId:guid}")]
    public async Task<ActionResult<IEnumerable<TrainingCompletionDto>>> GetByScheduleId(Guid scheduleId, CancellationToken ct)
        => Ok(await _service.GetByScheduleIdAsync(scheduleId, ct));

    [HttpGet("pending-verification")]
    public async Task<ActionResult<IEnumerable<TrainingCompletionDto>>> GetPendingVerification(CancellationToken ct)
        => Ok(await _service.GetPendingVerificationAsync(ct));

    // =========================================================================
    // COMPLETION OPERATIONS
    // =========================================================================

    [HttpPost]
    public async Task<ActionResult<TrainingCompletionDto>> RecordCompletion([FromBody] RecordTrainingCompletionDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null) return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        var created = await _service.RecordCompletionAsync(dto, tenantId.Value, employeeId.Value, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPost("bulk")]
    public async Task<ActionResult<BulkCompletionResultDto>> BulkRecordCompletion([FromBody] BulkRecordCompletionDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;
        if (tenantId == null) return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.BulkRecordCompletionAsync(dto, tenantId.Value, employeeId.Value, ct));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TrainingCompletionDto>> Update(Guid id, [FromBody] UpdateTrainingCompletionDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");
        dto.Id = id;
        return Ok(await _service.UpdateAsync(dto, employeeId.Value, ct));
    }

    [HttpPost("{id:guid}/verify")]
    public async Task<IActionResult> VerifyCompletion(Guid id, [FromBody] VerifyTrainingCompletionDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");
        dto.CompletionId = id;
        await _service.VerifyCompletionAsync(dto, employeeId.Value, ct);
        return Ok(new { message = "Training completion verified." });
    }

    // =========================================================================
    // CERTIFICATE OPERATIONS
    // =========================================================================

    [HttpPost("certificates")]
    public async Task<ActionResult<TrainingCertificateDto>> IssueCertificate([FromBody] IssueCertificateDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null) return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.IssueCertificateAsync(dto, tenantId.Value, employeeId.Value, ct));
    }

    [HttpPost("certificates/{id:guid}/revoke")]
    public async Task<IActionResult> RevokeCertificate(Guid id, [FromBody] RevokeCertificateDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");
        dto.CertificateId = id;
        await _service.RevokeCertificateAsync(dto, employeeId.Value, ct);
        return Ok(new { message = "Certificate revoked." });
    }

    [HttpGet("certificates/employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<TrainingCertificateSummaryDto>>> GetCertificatesForEmployee(Guid employeeId, CancellationToken ct)
        => Ok(await _service.GetCertificatesForEmployeeAsync(employeeId, ct));

    [HttpGet("certificates/expiring")]
    public async Task<ActionResult<IEnumerable<TrainingCertificateSummaryDto>>> GetExpiringCertificates(
        [FromQuery] int daysAhead = 30,
        CancellationToken ct = default)
        => Ok(await _service.GetExpiringCertificatesAsync(daysAhead, ct));
}
