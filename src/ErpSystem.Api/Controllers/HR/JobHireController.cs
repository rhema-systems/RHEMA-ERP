using ErpSystem.Api.Filters;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>Request body for confirming a hire start date.</summary>
public sealed record ConfirmStartRequest(DateTime ActualStartDate, Guid? LinkedEmployeeId);

/// <summary>
/// Hire records — the handover from recruitment to employment.
///
/// <para><b>HR-only, reads included:</b> a hire record carries the agreed salary, the start date and
/// the link to the employee record it created. It previously carried a bare <c>[Authorize]</c>.</para>
///
/// <para>⚠ <c>confirm-start</c> is the consequential one: it creates the <c>Employee</c>, the
/// contract, the probation period, the salary assignment, the position history and the candidate's
/// qualifications, work history, referees and skills — and burns an employee number. It is
/// idempotent by design (it refuses once the hire is linked to an employee), which is also why
/// <c>Active</c> cannot be reached through the status endpoint.</para>
/// </summary>
[ApiController]
[Route("api/job-hires")]
[Authorize(Roles = Constants.Roles.SuperAdmin + "," + Constants.Roles.Hr)]
[RecruitmentBusinessRules]
public class JobHireController : ControllerBase
{
    private readonly IJobHireService _service;
    private readonly ICurrentUserService _currentUser;

    public JobHireController(IJobHireService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    // =========================================================================
    // QUERIES
    // =========================================================================

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<JobHireRecordDto>> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id));

    [HttpGet("number/{hireNumber}")]
    public async Task<ActionResult<JobHireRecordDto?>> GetByHireNumber(string hireNumber)
        => Ok(await _service.GetByHireNumberAsync(hireNumber));

    [HttpGet("application/{applicationId:guid}")]
    public async Task<ActionResult<JobHireRecordDto?>> GetByApplication(Guid applicationId)
        => Ok(await _service.GetByApplicationIdAsync(applicationId));

    [HttpGet("employee/{employeeId:guid}")]
    public async Task<ActionResult<JobHireRecordDto?>> GetByEmployee(Guid employeeId)
        => Ok(await _service.GetByEmployeeIdAsync(employeeId));

    [HttpGet("status/{status}")]
    public async Task<ActionResult<IEnumerable<JobHireRecordSummaryDto>>> GetByStatus(JobHireStatus status)
        => Ok(await _service.GetByStatusAsync(status));

    [HttpGet("start-approaching")]
    public async Task<ActionResult<IEnumerable<JobHireRecordSummaryDto>>> GetStartApproaching(
        [FromQuery] int daysAhead = 14)
        => Ok(await _service.GetWithStartDateApproachingAsync(daysAhead));

    // =========================================================================
    // CRUD
    // =========================================================================

    [HttpPost]
    public async Task<ActionResult<JobHireRecordDto>> Create([FromBody] CreateJobHireRecordDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null)
            return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        var created = await _service.CreateAsync(dto, tenantId.Value, employeeId.Value);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}/status")]
    public async Task<ActionResult<JobHireRecordDto>> UpdateStatus(
        Guid id, [FromBody] UpdateJobHireRecordStatusDto dto)
    {
        // The service keys off the body's id, so a mismatch used to move a different hire.
        dto.HireRecordId = id;
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.UpdateStatusAsync(dto, employeeId.Value));
    }

    // =========================================================================
    // WORKFLOW
    // =========================================================================

    [HttpPost("{id:guid}/confirm-start")]
    public async Task<IActionResult> ConfirmStart(Guid id, [FromBody] ConfirmStartRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _service.ConfirmStartAsync(id, request.ActualStartDate, request.LinkedEmployeeId, employeeId.Value);
        return Ok(new { message = "Start confirmed." });
    }
}
