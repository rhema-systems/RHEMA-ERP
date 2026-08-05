using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/employee-orientations")]
[Authorize]
public class EmployeeOrientationsController : ControllerBase
{
    private readonly IEmployeeOrientationService _service;
    private readonly ICurrentUserService _currentUser;

    public EmployeeOrientationsController(IEmployeeOrientationService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    // =========================================================================
    // QUERIES
    // =========================================================================

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EmployeeOrientationDto>> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id));

    [HttpGet("employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<EmployeeOrientationSummaryDto>>> GetByEmployee(Guid employeeId)
        => Ok(await _service.GetByEmployeeIdAsync(employeeId));

    /// <summary>Enrollments for the signed-in employee (self-service "My Orientations").</summary>
    [HttpGet("mine")]
    public async Task<ActionResult<IEnumerable<EmployeeOrientationSummaryDto>>> GetMine()
    {
        if (_currentUser.EmployeeId is not { } employeeId)
            return BadRequest("Your user account is not linked to an employee record.");
        return Ok(await _service.GetByEmployeeIdAsync(employeeId));
    }

    [HttpGet("program/{programId:guid}")]
    public async Task<ActionResult<IEnumerable<EmployeeOrientationSummaryDto>>> GetByProgram(Guid programId)
        => Ok(await _service.GetByProgramIdAsync(programId));

    [HttpGet("program/{programId:guid}/paged")]
    public async Task<ActionResult<PagedResult<EmployeeOrientationSummaryDto>>> GetPagedByProgram(
        Guid programId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
        => Ok(await _service.GetPagedByProgramAsync(programId, pageNumber, pageSize));

    [HttpGet("session/{sessionId:guid}")]
    public async Task<ActionResult<IEnumerable<EmployeeOrientationSummaryDto>>> GetBySession(Guid sessionId)
        => Ok(await _service.GetBySessionIdAsync(sessionId));

    [HttpGet("completion-status/{status}")]
    public async Task<ActionResult<IEnumerable<EmployeeOrientationSummaryDto>>> GetByCompletionStatus(OrientationCompletionStatus status)
        => Ok(await _service.GetByCompletionStatusAsync(status));

    [HttpGet("overdue")]
    public async Task<ActionResult<IEnumerable<EmployeeOrientationSummaryDto>>> GetOverdue()
        => Ok(await _service.GetOverdueAsync());

    [HttpGet("due-soon")]
    public async Task<ActionResult<IEnumerable<EmployeeOrientationSummaryDto>>> GetDueSoon([FromQuery] int daysAhead = 7)
        => Ok(await _service.GetDueSoonAsync(daysAhead));

    // =========================================================================
    // ENROLLMENT
    // =========================================================================

    [HttpPost]
    public async Task<ActionResult<EmployeeOrientationDto>> Enroll([FromBody] CreateEmployeeOrientationDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext(out var bad);
        if (bad != null) return bad;

        var created = await _service.EnrollAsync(dto, ctx.TenantId, ctx.UserId);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPost("bulk-enroll")]
    public async Task<ActionResult<IEnumerable<EmployeeOrientationDto>>> BulkEnroll([FromBody] BulkEnrollOrientationDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext(out var bad);
        if (bad != null) return bad;

        return Ok(await _service.BulkEnrollAsync(dto, ctx.TenantId, ctx.UserId));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<EmployeeOrientationDto>> Update(Guid id, [FromBody] UpdateEmployeeOrientationDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (_currentUser.EmployeeId is not { } userId) return BadRequest("Your user account is not linked to an employee record.");

        return Ok(await _service.UpdateAsync(dto, userId));
    }

    [HttpPost("{id:guid}/withdraw")]
    public async Task<IActionResult> Withdraw(Guid id, [FromBody] WithdrawOrientationDto dto)
    {
        if (id != dto.EnrollmentId) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (_currentUser.EmployeeId is not { } userId) return BadRequest("Your user account is not linked to an employee record.");

        await _service.WithdrawAsync(dto, userId);
        return Ok(new { message = "Enrollment withdrawn." });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }

    // =========================================================================
    // CONTENT PROGRESS
    // =========================================================================

    [HttpGet("{id:guid}/content-progress")]
    public async Task<ActionResult<IEnumerable<OrientationContentProgressDto>>> GetContentProgress(Guid id)
        => Ok(await _service.GetContentProgressAsync(id));

    [HttpPost("{id:guid}/content-progress")]
    public async Task<ActionResult<OrientationContentProgressDto>> TrackContentProgress(Guid id, [FromBody] TrackOrientationContentProgressDto dto)
    {
        if (id != dto.EmployeeOrientationId) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext(out var bad);
        if (bad != null) return bad;

        return Ok(await _service.TrackContentProgressAsync(dto, ctx.TenantId, ctx.UserId));
    }

    // =========================================================================
    // ASSESSMENT
    // =========================================================================

    [HttpGet("{id:guid}/assessment")]
    public async Task<ActionResult<IEnumerable<OrientationAssessmentQuestionDto>>> GetAssessment(Guid id)
        => Ok(await _service.GetAssessmentForEnrollmentAsync(id));

    [HttpPost("{id:guid}/assessment/submit")]
    public async Task<ActionResult<OrientationAssessmentResultDto>> SubmitAssessment(Guid id, [FromBody] SubmitOrientationAssessmentDto dto)
    {
        if (id != dto.EmployeeOrientationId) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext(out var bad);
        if (bad != null) return bad;

        return Ok(await _service.SubmitAssessmentAsync(dto, ctx.TenantId, ctx.UserId));
    }

    [HttpGet("{id:guid}/assessment/responses")]
    public async Task<ActionResult<IEnumerable<OrientationAssessmentResponseDto>>> GetAssessmentResponses(Guid id)
        => Ok(await _service.GetAssessmentResponsesAsync(id));

    // =========================================================================
    // ACKNOWLEDGEMENTS
    // =========================================================================

    [HttpGet("{id:guid}/acknowledgements")]
    public async Task<ActionResult<IEnumerable<OrientationAcknowledgementDto>>> GetAcknowledgements(Guid id)
        => Ok(await _service.GetAcknowledgementsAsync(id));

    [HttpPost("{id:guid}/acknowledgements")]
    public async Task<ActionResult<OrientationAcknowledgementDto>> AddAcknowledgement(Guid id, [FromBody] CreateOrientationAcknowledgementDto dto)
    {
        if (id != dto.EmployeeOrientationId) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext(out var bad);
        if (bad != null) return bad;

        return Ok(await _service.AddAcknowledgementAsync(dto, ctx.TenantId, ctx.UserId));
    }

    [HttpPost("acknowledgements/sign")]
    public async Task<ActionResult<OrientationAcknowledgementDto>> SignAcknowledgement([FromBody] SignOrientationAcknowledgementDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (_currentUser.EmployeeId is not { } userId) return BadRequest("Your user account is not linked to an employee record.");

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        return Ok(await _service.SignAcknowledgementAsync(dto, ip, userId));
    }

    // =========================================================================
    // FEEDBACK
    // =========================================================================

    [HttpGet("{id:guid}/feedback")]
    public async Task<ActionResult<IEnumerable<OrientationFeedbackDto>>> GetFeedback(Guid id)
        => Ok(await _service.GetFeedbackAsync(id));

    [HttpPost("{id:guid}/feedback")]
    public async Task<ActionResult<OrientationFeedbackDto>> SubmitFeedback(Guid id, [FromBody] CreateOrientationFeedbackDto dto)
    {
        if (id != dto.EmployeeOrientationId) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext(out var bad);
        if (bad != null) return bad;

        return Ok(await _service.SubmitFeedbackAsync(dto, ctx.TenantId, ctx.UserId));
    }

    // =========================================================================
    // CERTIFICATES
    // =========================================================================

    [HttpGet("{id:guid}/certificates")]
    public async Task<ActionResult<IEnumerable<OrientationCertificateDto>>> GetCertificatesForEnrollment(Guid id)
        => Ok(await _service.GetCertificatesForEnrollmentAsync(id));

    [HttpGet("employee/{employeeId:guid}/certificates")]
    public async Task<ActionResult<IEnumerable<OrientationCertificateDto>>> GetCertificatesForEmployee(Guid employeeId)
        => Ok(await _service.GetCertificatesForEmployeeAsync(employeeId));

    [HttpGet("certificates/number/{certificateNumber}")]
    public async Task<ActionResult<OrientationCertificateDto?>> GetCertificateByNumber(string certificateNumber)
        => Ok(await _service.GetCertificateByNumberAsync(certificateNumber));

    [HttpGet("certificates/expiring")]
    public async Task<ActionResult<IEnumerable<OrientationCertificateDto>>> GetExpiringCertificates([FromQuery] int daysAhead = 30)
        => Ok(await _service.GetExpiringCertificatesAsync(daysAhead));

    [HttpPost("{id:guid}/certificates")]
    public async Task<ActionResult<OrientationCertificateDto>> IssueCertificate(Guid id, [FromBody] IssueOrientationCertificateDto dto)
    {
        if (id != dto.EmployeeOrientationId) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext(out var bad);
        if (bad != null) return bad;

        return Ok(await _service.IssueCertificateAsync(dto, ctx.TenantId, ctx.UserId));
    }

    [HttpPost("certificates/revoke")]
    public async Task<IActionResult> RevokeCertificate([FromBody] RevokeOrientationCertificateDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (_currentUser.EmployeeId is not { } userId) return BadRequest("Your user account is not linked to an employee record.");

        await _service.RevokeCertificateAsync(dto, userId);
        return Ok(new { message = "Certificate revoked." });
    }

    // =========================================================================
    // HELPERS
    // =========================================================================

    private (Guid TenantId, Guid UserId) ResolveContext(out ActionResult? error)
    {
        error = null;
        if (_currentUser.TenantId is not { } tenantId)
        {
            error = BadRequest("Tenant context could not be resolved.");
            return default;
        }
        if (_currentUser.EmployeeId is not { } userId)
        {
            error = BadRequest("Your user account is not linked to an employee record.");
            return default;
        }
        return (tenantId, userId);
    }
}
