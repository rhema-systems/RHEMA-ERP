using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/training-nominations")]
[Authorize]
public class TrainingNominationsController : ControllerBase
{
    private readonly ITrainingNominationService _service;
    private readonly ITrainingScheduleService _scheduleService;
    private readonly INomineeAvailabilityService _availabilityService;
    private readonly ICurrentUserService _currentUser;

    public TrainingNominationsController(
        ITrainingNominationService service,
        ITrainingScheduleService scheduleService,
        INomineeAvailabilityService availabilityService,
        ICurrentUserService currentUser)
    {
        _service = service;
        _scheduleService = scheduleService;
        _availabilityService = availabilityService;
        _currentUser = currentUser;
    }

    [HttpPost("availability-check")]
    public async Task<ActionResult<List<NomineeConflictDto>>> CheckNomineeAvailability([FromBody] NomineeAvailabilityCheckRequestDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var schedule = await _scheduleService.GetByIdAsync(dto.ScheduleId, ct);
        var conflicts = await _availabilityService.CheckAsync(dto.EmployeeIds, schedule.StartDate, schedule.EndDate, dto.ScheduleId, ct);
        return Ok(conflicts);
    }

    // =========================================================================
    // QUERIES
    // =========================================================================

    [HttpGet("paged")]
    public async Task<ActionResult<Core.DTOs.Common.PagedResult<TrainingNominationSummaryDto>>> GetPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _service.GetPagedAsync(pageNumber, pageSize, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TrainingNominationDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await _service.GetByIdAsync(id, ct));

    [HttpGet("number/{nominationNumber}")]
    public async Task<ActionResult<TrainingNominationDto?>> GetByNominationNumber(string nominationNumber, CancellationToken ct)
        => Ok(await _service.GetByNominationNumberAsync(nominationNumber, ct));

    [HttpGet("schedule/{scheduleId:guid}")]
    public async Task<ActionResult<IEnumerable<TrainingNominationSummaryDto>>> GetByScheduleId(Guid scheduleId, CancellationToken ct)
        => Ok(await _service.GetByScheduleIdAsync(scheduleId, ct));

    [HttpGet("employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<TrainingNominationSummaryDto>>> GetByEmployeeId(Guid employeeId, CancellationToken ct)
        => Ok(await _service.GetByEmployeeIdAsync(employeeId, ct));

    [HttpGet("status/{status}")]
    public async Task<ActionResult<IEnumerable<TrainingNominationSummaryDto>>> GetByStatus(NominationStatus status, CancellationToken ct)
        => Ok(await _service.GetByStatusAsync(status, ct));

    [HttpGet("pending-supervisor")]
    public async Task<ActionResult<IEnumerable<TrainingNominationSummaryDto>>> GetPendingSupervisorApproval(CancellationToken ct)
        => Ok(await _service.GetPendingSupervisorApprovalAsync(ct));

    [HttpGet("pending-hr")]
    public async Task<ActionResult<IEnumerable<TrainingNominationSummaryDto>>> GetPendingHrApproval(CancellationToken ct)
        => Ok(await _service.GetPendingHrApprovalAsync(ct));

    // =========================================================================
    // CRUD
    // =========================================================================

    [HttpPost]
    public async Task<ActionResult<TrainingNominationDto>> Create([FromBody] CreateTrainingNominationDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null) return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        var created = await _service.CreateAsync(dto, tenantId.Value, employeeId.Value, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPost("bulk")]
    public async Task<ActionResult<BulkNominationResultDto>> BulkCreate([FromBody] BulkCreateTrainingNominationDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;
        if (tenantId == null) return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.BulkCreateAsync(dto, tenantId.Value, employeeId.Value, ct));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TrainingNominationDto>> Update(Guid id, [FromBody] UpdateTrainingNominationDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var updated = await _service.UpdateAsync(id, dto, ct);
        return Ok(updated);
    }

    // =========================================================================
    // WORKFLOW
    // =========================================================================

    [HttpPost("{id:guid}/submit")]
    public async Task<ActionResult<TrainingNominationDto>> Submit(Guid id, CancellationToken ct)
    {
        var result = await _service.SubmitAsync(id, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveNominationDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        dto.NominationId = id;
        await _service.ApproveAsync(dto, ct);
        return Ok(new { message = "Nomination approved." });
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectNominationDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        dto.NominationId = id;
        await _service.RejectAsync(dto, ct);
        return Ok(new { message = "Nomination rejected." });
    }

    [HttpPost("{id:guid}/withdraw")]
    public async Task<IActionResult> Withdraw(Guid id, [FromBody] WithdrawNominationDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        dto.NominationId = id;
        await _service.WithdrawAsync(dto, ct);
        return Ok(new { message = "Nomination withdrawn." });
    }

    // =========================================================================
    // ATTENDANCE
    // =========================================================================

    [HttpPost("attendance")]
    public async Task<ActionResult<TrainingAttendanceDto>> MarkAttendance([FromBody] MarkAttendanceDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null) return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.MarkAttendanceAsync(dto, tenantId.Value, employeeId.Value, ct));
    }

    [HttpPost("attendance/bulk")]
    public async Task<ActionResult<IEnumerable<TrainingAttendanceDto>>> BulkMarkAttendance([FromBody] BulkMarkAttendanceDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null) return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.BulkMarkAttendanceAsync(dto, tenantId.Value, employeeId.Value, ct));
    }

    [HttpGet("schedule/{scheduleId:guid}/attendance")]
    public async Task<ActionResult<IEnumerable<TrainingAttendanceDto>>> GetAttendanceForSchedule(Guid scheduleId, CancellationToken ct)
        => Ok(await _service.GetAttendanceForScheduleAsync(scheduleId, ct));

    [HttpGet("schedule/{scheduleId:guid}/attendance/by-date")]
    public async Task<ActionResult<IEnumerable<TrainingAttendanceDto>>> GetAttendanceByDate(Guid scheduleId, [FromQuery] DateTime date, CancellationToken ct)
        => Ok(await _service.GetAttendanceByDateAsync(scheduleId, date, ct));

    // =========================================================================
    // FEEDBACK
    // =========================================================================

    [HttpPost("feedback")]
    public async Task<ActionResult<TrainingFeedbackDto>> SubmitFeedback([FromBody] SubmitTrainingFeedbackDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null) return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.SubmitFeedbackAsync(dto, tenantId.Value, employeeId.Value, ct));
    }

    [HttpGet("schedule/{scheduleId:guid}/feedback")]
    public async Task<ActionResult<IEnumerable<TrainingFeedbackDto>>> GetFeedbackForSchedule(Guid scheduleId, CancellationToken ct)
        => Ok(await _service.GetFeedbackForScheduleAsync(scheduleId, ct));

    // =========================================================================
    // FOLLOW-UP ASSESSMENTS
    // =========================================================================

    [HttpPost("follow-up")]
    public async Task<ActionResult<TrainingFollowUpAssessmentDto>> SubmitFollowUpAssessment([FromBody] SubmitFollowUpAssessmentDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null) return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.SubmitFollowUpAssessmentAsync(dto, tenantId.Value, employeeId.Value, ct));
    }

    [HttpPost("follow-up/{id:guid}/manager-observation")]
    public async Task<IActionResult> SubmitManagerObservation(Guid id, [FromBody] SubmitManagerObservationDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        dto.AssessmentId = id;
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");
        await _service.SubmitManagerObservationAsync(dto, employeeId.Value, ct);
        return Ok(new { message = "Manager observation recorded." });
    }

    [HttpGet("schedule/{scheduleId:guid}/follow-up")]
    public async Task<ActionResult<IEnumerable<TrainingFollowUpAssessmentDto>>> GetFollowUpAssessments(Guid scheduleId, CancellationToken ct)
        => Ok(await _service.GetFollowUpAssessmentsAsync(scheduleId, ct));
}
