using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/employee-competencies")]
[Authorize]
public class EmployeeCompetencyController : ControllerBase
{
    private readonly IEmployeeCompetencyService _service;
    private readonly IEmployeeCompetencyHistoryService _historyService;
    private readonly ICurrentUserService _currentUser;

    public EmployeeCompetencyController(
        IEmployeeCompetencyService service,
        IEmployeeCompetencyHistoryService historyService,
        ICurrentUserService currentUser)
    {
        _service        = service;
        _historyService = historyService;
        _currentUser    = currentUser;
    }

    // =========================================================================
    // QUERIES
    // =========================================================================

    [Authorize(Policy = HrPermissions.CompetencyReadPolicy)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EmployeeCompetencyDto>> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id));

    [Authorize(Policy = HrPermissions.CompetencyReadPolicy)]
    [HttpGet("{id:guid}/detail")]
    public async Task<ActionResult<EmployeeCompetencyDetailDto>> GetWithHistory(Guid id)
        => Ok(await _service.GetWithHistoryAsync(id));

    [Authorize(Policy = HrPermissions.CompetencyReadPolicy)]
    [HttpGet("employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<EmployeeCompetencyDto>>> GetByEmployee(Guid employeeId)
        => Ok(await _service.GetByEmployeeIdAsync(employeeId));

    [Authorize(Policy = HrPermissions.CompetencyReadPolicy)]
    [HttpGet("competency/{competencyId:guid}")]
    public async Task<ActionResult<IEnumerable<EmployeeCompetencyDto>>> GetByCompetency(Guid competencyId)
        => Ok(await _service.GetByCompetencyIdAsync(competencyId));

    [Authorize(Policy = HrPermissions.CompetencyReadPolicy)]
    [HttpGet("employee/{employeeId:guid}/competency/{competencyId:guid}")]
    public async Task<ActionResult<EmployeeCompetencyDto?>> GetByPair(Guid employeeId, Guid competencyId)
        => Ok(await _service.GetByEmployeeAndCompetencyAsync(employeeId, competencyId));

    [Authorize(Policy = HrPermissions.CompetencyReadPolicy)]
    [HttpGet("stale")]
    public async Task<ActionResult<IEnumerable<EmployeeCompetencyDto>>> GetStale([FromQuery] int monthsOld = 12)
        => Ok(await _service.GetStaleAssessmentsAsync(monthsOld));

    [Authorize(Policy = HrPermissions.CompetencyReadPolicy)]
    [HttpGet("qualified-for-position/{positionId:guid}")]
    public async Task<ActionResult<IEnumerable<EmployeeCompetencyDto>>> GetQualifiedForPosition(Guid positionId)
        => Ok(await _service.GetQualifiedEmployeesForPositionAsync(positionId));

    // =========================================================================
    // ANALYTICS
    // =========================================================================

    [Authorize(Policy = HrPermissions.CompetencyReadPolicy)]
    [HttpGet("employee/{employeeId:guid}/gaps")]
    public async Task<ActionResult<EmployeePositionCompetencyGapSummaryDto>> GetGapsForEmployee(Guid employeeId)
        => Ok(await _service.GetGapsForEmployeeAsync(employeeId));

    [Authorize(Policy = HrPermissions.CompetencyReadPolicy)]
    [HttpGet("employee/{employeeId:guid}/profile")]
    public async Task<ActionResult<EmployeeCompetencyProfileDto>> GetProfile(Guid employeeId)
        => Ok(await _service.GetEmployeeProfileAsync(employeeId));

    // =========================================================================
    // SELF SERVICE
    // =========================================================================
    //
    // ⚠ These exist because the browser cannot address the by-employee endpoints on the caller's
    // own behalf: the client `User` object carries roles, tenants and permissions but NO employee
    // link, so a screen has no id to put in `employee/{employeeId}`. That is the same gap that
    // produced probation's `reviews/to-conduct`, and the reason slice 0 asserts an employee is
    // refused on `employee/{theirOwnId}/profile` — the self tier could not arrive as a relaxed
    // permission, it had to arrive as an endpoint that takes the employee from the token.
    //
    // Plain [Authorize]: there is no permission to hold here. The record is the caller's own, and
    // the token is what says so.

    /// <summary>The signed-in employee's own competency profile.</summary>
    [Authorize]
    [HttpGet("me/profile")]
    public async Task<ActionResult<EmployeeCompetencyProfileDto>> GetMyProfile()
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");
        return Ok(await _service.GetEmployeeProfileAsync(employeeId.Value));
    }

    /// <summary>
    /// The signed-in employee's own gap analysis against the competencies their position requires.
    /// </summary>
    /// <remarks>
    /// Deliberately readable by the employee themselves. A competency gap is not a judgement filed
    /// about someone the way a succession readiness rating is — it is the list of what their own
    /// role asks for and where they currently stand against it, which is the thing they need in
    /// order to close it.
    /// </remarks>
    [Authorize]
    [HttpGet("me/gaps")]
    public async Task<ActionResult<EmployeePositionCompetencyGapSummaryDto>> GetMyGaps()
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");
        return Ok(await _service.GetGapsForEmployeeAsync(employeeId.Value));
    }

    // =========================================================================
    // CRUD
    // =========================================================================

    [Authorize(Policy = HrPermissions.CompetencyWritePolicy)]
    [HttpPost]
    public async Task<ActionResult<EmployeeCompetencyDto>> Create([FromBody] CreateEmployeeCompetencyDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId   = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null)   return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        var created = await _service.CreateAsync(dto, tenantId.Value, employeeId.Value);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>
    /// Re-assesses an employee's competency level. Automatically archives the previous
    /// assessment values to the history trail before applying the update.
    /// </summary>
    [Authorize(Policy = HrPermissions.CompetencyWritePolicy)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<EmployeeCompetencyDto>> Update(Guid id, [FromBody] UpdateEmployeeCompetencyDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.UpdateAsync(dto, employeeId.Value));
    }

    [Authorize(Policy = HrPermissions.CompetencyAdminPolicy)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }

    // =========================================================================
    // BATCH ASSESSMENT
    // =========================================================================

    /// <summary>
    /// Batch re-assessment: applies multiple competency level updates in a single call.
    /// For existing records the current state is snapshotted to history before the update.
    /// For first-time assessments (EmployeeCompetencyId = null, CompetencyId set) a new record is created.
    /// </summary>
    [Authorize(Policy = HrPermissions.CompetencyWritePolicy)]
    [HttpPost("batch-assess")]
    public async Task<ActionResult<BatchAssessmentResultDto>> BatchAssess(
        [FromBody] EmployeeCompetencyAssessmentUpdateDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId   = _currentUser.TenantId;
        var assessorId = _currentUser.EmployeeId;

        if (tenantId == null)   return BadRequest("Tenant context could not be resolved.");
        if (assessorId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        var result = await _service.BatchAssessAsync(dto, tenantId.Value, assessorId.Value);
        return Ok(result);
    }

    // =========================================================================
    // HISTORY (read-only — written internally by the service on every update)
    // =========================================================================

    [Authorize(Policy = HrPermissions.CompetencyReadPolicy)]
    [HttpGet("{id:guid}/history")]
    public async Task<ActionResult<IEnumerable<EmployeeCompetencyHistoryDto>>> GetHistory(Guid id)
        => Ok(await _historyService.GetByEmployeeCompetencyIdAsync(id));

    [Authorize(Policy = HrPermissions.CompetencyReadPolicy)]
    [HttpGet("{id:guid}/history/latest")]
    public async Task<ActionResult<EmployeeCompetencyHistoryDto?>> GetLatestHistory(Guid id)
        => Ok(await _historyService.GetLatestAsync(id));

    [Authorize(Policy = HrPermissions.CompetencyReadPolicy)]
    [HttpGet("employee/{employeeId:guid}/history")]
    public async Task<ActionResult<IEnumerable<EmployeeCompetencyHistoryDto>>> GetHistoryByEmployee(Guid employeeId)
        => Ok(await _historyService.GetByEmployeeIdAsync(employeeId));

    [Authorize(Policy = HrPermissions.CompetencyReadPolicy)]
    [HttpGet("competency/{competencyId:guid}/history")]
    public async Task<ActionResult<IEnumerable<EmployeeCompetencyHistoryDto>>> GetHistoryByCompetency(Guid competencyId)
        => Ok(await _historyService.GetByCompetencyIdAsync(competencyId));
}
