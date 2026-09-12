using ErpSystem.Api.Filters;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ErpSystem.Shared;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/training-nominations")]
[Authorize(Policy = "InternalOnly")]
[TrainingBusinessRulesAttribute]
public class TrainingNominationsController : ControllerBase
{
    private readonly ITrainingNominationService _service;
    private readonly ITrainingScheduleService _scheduleService;
    private readonly INomineeAvailabilityService _availabilityService;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuthorizationService _authorization;

    public TrainingNominationsController(
        ITrainingNominationService service,
        ITrainingScheduleService scheduleService,
        INomineeAvailabilityService availabilityService,
        ICurrentUserService currentUser,
        IAuthorizationService authorization)
    {
        _service = service;
        _scheduleService = scheduleService;
        _availabilityService = availabilityService;
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

    [HttpPost("availability-check")]
    [Authorize(Policy = HrPermissions.TrainingReadPolicy)]
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
    [Authorize(Policy = HrPermissions.TrainingReadPolicy)]
    public async Task<ActionResult<Core.DTOs.Common.PagedResult<TrainingNominationSummaryDto>>> GetPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _service.GetPagedAsync(pageNumber, pageSize, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TrainingNominationDto>> GetById(Guid id, CancellationToken ct)
    {
        // W3: my-training links straight to the nomination detail, so the nominee reads their own.
        var nomination = await _service.GetByIdAsync(id, ct);
        if (!await SelfOrPolicyAsync(nomination.EmployeeId, HrPermissions.TrainingReadPolicy))
            return Forbid();
        return Ok(nomination);
    }

    [HttpGet("number/{nominationNumber}")]
    [Authorize(Policy = HrPermissions.TrainingReadPolicy)]
    public async Task<ActionResult<TrainingNominationDto?>> GetByNominationNumber(string nominationNumber, CancellationToken ct)
        => Ok(await _service.GetByNominationNumberAsync(nominationNumber, ct));

    [HttpGet("schedule/{scheduleId:guid}")]
    [Authorize(Policy = HrPermissions.TrainingReadPolicy)]
    public async Task<ActionResult<IEnumerable<TrainingNominationSummaryDto>>> GetByScheduleId(Guid scheduleId, CancellationToken ct)
        => Ok(await _service.GetByScheduleIdAsync(scheduleId, ct));

    /// <summary>The caller's own nominations, taken from the token.</summary>
    /// <remarks>
    /// The module's /mine convention: without it a self-service screen would have to fetch its own
    /// employee id and pass it back through the id-bearing route below, which is the shape that
    /// produced read-anyone's-record holes elsewhere in HR.
    /// </remarks>
    [HttpGet("mine")]
    public async Task<ActionResult<IEnumerable<TrainingNominationSummaryDto>>> GetMine(CancellationToken ct)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return Forbid();
        return Ok(await _service.GetByEmployeeIdAsync(employeeId.Value, ct));
    }

    [HttpGet("employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<TrainingNominationSummaryDto>>> GetByEmployeeId(Guid employeeId, CancellationToken ct)
    {
        if (!await SelfOrPolicyAsync(employeeId, HrPermissions.TrainingReadPolicy))
            return Forbid();
        return Ok(await _service.GetByEmployeeIdAsync(employeeId, ct));
    }

    [HttpGet("status/{status}")]
    [Authorize(Policy = HrPermissions.TrainingReadPolicy)]
    public async Task<ActionResult<IEnumerable<TrainingNominationSummaryDto>>> GetByStatus(NominationStatus status, CancellationToken ct)
        => Ok(await _service.GetByStatusAsync(status, ct));

    // W3: the two pending queues stay open — they are the approver's survey surface (the approvals
    // screen feeds on them), and line supervisors hold no HR permission. The approve/reject acts
    // themselves are validated per request (workflow assignee, or the service's legacy role check).
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

        // W3: nominating is an employee act when the nominee is the caller (self-nomination);
        // nominating someone else is the desk's act. The nominee comes from the body.
        if (!await SelfOrPolicyAsync(dto.EmployeeId, HrPermissions.TrainingWritePolicy))
            return Forbid();

        var created = await _service.CreateAsync(dto, tenantId.Value, employeeId.Value, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPost("bulk")]
    [Authorize(Policy = HrPermissions.TrainingWritePolicy)]
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
    [Authorize(Policy = HrPermissions.TrainingAdminPolicy)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = HrPermissions.TrainingWritePolicy)]
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
        // W3: submitting one's own nomination is a self act; submitting someone else's is the desk's.
        var nomination = await _service.GetByIdAsync(id, ct);
        if (!await SelfOrPolicyAsync(nomination.EmployeeId, HrPermissions.TrainingWritePolicy))
            return Forbid();

        var result = await _service.SubmitAsync(id, ct);
        return Ok(result);
    }

    // Approve/reject deliberately carry no permission attribute: on the workflow path the engine
    // validates the caller against the pending step's assignee (CanUserApproveAsync), and a
    // permission here would refuse line-supervisor approvers. The legacy no-workflow path is
    // role-checked inside TrainingNominationService (W3 slice 8).
    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveNominationDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        dto.NominationId = id;
        await _service.ApproveAsync(dto, employeeId.Value, ct);
        return Ok(new { message = "Nomination approved." });
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectNominationDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        dto.NominationId = id;
        await _service.RejectAsync(dto, employeeId.Value, ct);
        return Ok(new { message = "Nomination rejected." });
    }

    [HttpPost("{id:guid}/withdraw")]
    public async Task<IActionResult> Withdraw(Guid id, [FromBody] WithdrawNominationDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        // W3: withdrawal is the nominee's act or the desk's — previously any internal user could
        // withdraw anyone's nomination (no actor reached the service at all).
        var nomination = await _service.GetByIdAsync(id, ct);
        if (!await SelfOrPolicyAsync(nomination.EmployeeId, HrPermissions.TrainingWritePolicy))
            return Forbid();

        dto.NominationId = id;
        await _service.WithdrawAsync(dto, ct);
        return Ok(new { message = "Nomination withdrawn." });
    }

    // =========================================================================
    // ATTENDANCE
    // =========================================================================

    [HttpPost("attendance")]
    [Authorize(Policy = HrPermissions.TrainingWritePolicy)]
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
    [Authorize(Policy = HrPermissions.TrainingWritePolicy)]
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
    [Authorize(Policy = HrPermissions.TrainingReadPolicy)]
    public async Task<ActionResult<IEnumerable<TrainingAttendanceDto>>> GetAttendanceForSchedule(Guid scheduleId, CancellationToken ct)
        => Ok(await _service.GetAttendanceForScheduleAsync(scheduleId, ct));

    [HttpGet("schedule/{scheduleId:guid}/attendance/by-date")]
    [Authorize(Policy = HrPermissions.TrainingReadPolicy)]
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

        // W3: feedback is the trainee's own voice — previously it could be filed as anyone.
        if (!await SelfOrPolicyAsync(dto.EmployeeId, HrPermissions.TrainingWritePolicy))
            return Forbid();

        return Ok(await _service.SubmitFeedbackAsync(dto, tenantId.Value, employeeId.Value, ct));
    }

    /// <summary>The feedback the caller has given, newest first.</summary>
    /// <remarks>
    /// Token-actor, with no id-bearing twin: the sibling below is HR's aggregate over a whole
    /// course. Without this an employee could file feedback and never see it again, and the form
    /// could not tell them they had already answered.
    /// </remarks>
    [HttpGet("feedback/mine")]
    [ProducesResponseType(typeof(IEnumerable<TrainingFeedbackDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyFeedback(CancellationToken ct)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return Forbid();
        return Ok(await _service.GetMyFeedbackAsync(employeeId.Value, ct));
    }

    [HttpGet("schedule/{scheduleId:guid}/feedback")]
    [Authorize(Policy = HrPermissions.TrainingReadPolicy)]
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

        // W3: the follow-up self-assessment belongs to the trainee named in the body.
        if (!await SelfOrPolicyAsync(dto.EmployeeId, HrPermissions.TrainingWritePolicy))
            return Forbid();

        return Ok(await _service.SubmitFollowUpAssessmentAsync(dto, tenantId.Value, employeeId.Value, ct));
    }

    // W3: deliberately open to any internal user. The observation is a manager act, but the org
    // holds no usable reporting lines to validate against (7% of employees carry a ManagerId —
    // the deferred org-authority model), so the caller is recorded as the observing manager
    // rather than checked. Revisit when FR-HR-080/181 land.
    [HttpPost("follow-up/{id:guid}/manager-observation")]
    public async Task<IActionResult> SubmitManagerObservation(Guid id, [FromBody] SubmitManagerObservationDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        dto.AssessmentId = id;
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");
        // Returns the updated record rather than a bare message: the service already builds the
        // full DTO (re-read through its includes chain) and discarding it forced every caller to
        // refetch just to render the row it had just changed.
        return Ok(await _service.SubmitManagerObservationAsync(dto, employeeId.Value, ct));
    }

    [HttpGet("schedule/{scheduleId:guid}/follow-up")]
    [Authorize(Policy = HrPermissions.TrainingReadPolicy)]
    public async Task<ActionResult<IEnumerable<TrainingFollowUpAssessmentDto>>> GetFollowUpAssessments(Guid scheduleId, CancellationToken ct)
        => Ok(await _service.GetFollowUpAssessmentsAsync(scheduleId, ct));
}
