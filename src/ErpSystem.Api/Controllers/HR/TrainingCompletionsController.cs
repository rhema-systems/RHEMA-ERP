using ErpSystem.Api.Filters;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ErpSystem.Shared;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/training-completions")]
[Authorize(Policy = "InternalOnly")]
[TrainingBusinessRulesAttribute]
public class TrainingCompletionsController : ControllerBase
{
    private readonly ITrainingCompletionService _service;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuthorizationService _authorization;

    public TrainingCompletionsController(
        ITrainingCompletionService service,
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

    // =========================================================================
    // QUERIES
    // =========================================================================

    [HttpGet("{id:guid}")]
    [Authorize(Policy = HrPermissions.TrainingReadPolicy)]
    public async Task<ActionResult<TrainingCompletionDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await _service.GetByIdAsync(id, ct));

    [HttpGet("nomination/{nominationId:guid}")]
    [Authorize(Policy = HrPermissions.TrainingReadPolicy)]
    public async Task<ActionResult<TrainingCompletionDto?>> GetByNominationId(Guid nominationId, CancellationToken ct)
        => Ok(await _service.GetByNominationIdAsync(nominationId, ct));

    /// <summary>The caller's own completion records, taken from the token.</summary>
    [HttpGet("mine")]
    public async Task<ActionResult<IEnumerable<TrainingCompletionDto>>> GetMine(CancellationToken ct)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return Forbid();
        return Ok(await _service.GetByEmployeeIdAsync(employeeId.Value, ct));
    }

    [HttpGet("employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<TrainingCompletionDto>>> GetByEmployeeId(Guid employeeId, CancellationToken ct)
    {
        if (!await SelfOrPolicyAsync(employeeId, HrPermissions.TrainingReadPolicy))
            return Forbid();
        return Ok(await _service.GetByEmployeeIdAsync(employeeId, ct));
    }

    [HttpGet("schedule/{scheduleId:guid}")]
    [Authorize(Policy = HrPermissions.TrainingReadPolicy)]
    public async Task<ActionResult<IEnumerable<TrainingCompletionDto>>> GetByScheduleId(Guid scheduleId, CancellationToken ct)
        => Ok(await _service.GetByScheduleIdAsync(scheduleId, ct));

    [HttpGet("pending-verification")]
    [Authorize(Policy = HrPermissions.TrainingReadPolicy)]
    public async Task<ActionResult<IEnumerable<TrainingCompletionDto>>> GetPendingVerification(CancellationToken ct)
        => Ok(await _service.GetPendingVerificationAsync(ct));

    // =========================================================================
    // COMPLETION OPERATIONS
    // =========================================================================

    [HttpPost]
    [Authorize(Policy = HrPermissions.TrainingWritePolicy)]
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
    [Authorize(Policy = HrPermissions.TrainingWritePolicy)]
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
    [Authorize(Policy = HrPermissions.TrainingWritePolicy)]
    public async Task<ActionResult<TrainingCompletionDto>> Update(Guid id, [FromBody] UpdateTrainingCompletionDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");
        dto.Id = id;
        return Ok(await _service.UpdateAsync(dto, employeeId.Value, ct));
    }

    [HttpPost("{id:guid}/verify")]
    [Authorize(Policy = HrPermissions.TrainingWritePolicy)]
    public async Task<IActionResult> VerifyCompletion(Guid id, [FromBody] VerifyTrainingCompletionDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");
        dto.CompletionId = id;
        // Returns the updated record rather than a bare message: the service already builds the
        // full DTO (re-read through its includes chain) and discarding it forced every caller to
        // refetch just to render the row it had just changed.
        return Ok(await _service.VerifyCompletionAsync(dto, employeeId.Value, ct));
    }

    // =========================================================================
    // CERTIFICATE OPERATIONS
    // =========================================================================

    [HttpPost("certificates")]
    [Authorize(Policy = HrPermissions.TrainingWritePolicy)]
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
    [Authorize(Policy = HrPermissions.TrainingAdminPolicy)]
    public async Task<IActionResult> RevokeCertificate(Guid id, [FromBody] RevokeCertificateDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");
        dto.CertificateId = id;
        await _service.RevokeCertificateAsync(dto, employeeId.Value, ct);
        return Ok(new { message = "Certificate revoked." });
    }

    /// <summary>The caller's own training certificates, taken from the token.</summary>
    [HttpGet("certificates/mine")]
    public async Task<ActionResult<IEnumerable<TrainingCertificateSummaryDto>>> GetMyCertificates(CancellationToken ct)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return Forbid();
        return Ok(await _service.GetCertificatesForEmployeeAsync(employeeId.Value, ct));
    }

    [HttpGet("certificates/employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<TrainingCertificateSummaryDto>>> GetCertificatesForEmployee(Guid employeeId, CancellationToken ct)
    {
        if (!await SelfOrPolicyAsync(employeeId, HrPermissions.TrainingReadPolicy))
            return Forbid();
        return Ok(await _service.GetCertificatesForEmployeeAsync(employeeId, ct));
    }

    [HttpGet("certificates/expiring")]
    [Authorize(Policy = HrPermissions.TrainingReadPolicy)]
    public async Task<ActionResult<IEnumerable<TrainingCertificateSummaryDto>>> GetExpiringCertificates(
        [FromQuery] int daysAhead = 30,
        CancellationToken ct = default)
        => Ok(await _service.GetExpiringCertificatesAsync(daysAhead, ct));
}
