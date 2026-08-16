using ErpSystem.Api.Filters;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Manages disciplinary case headers: CRUD, status workflow, and the module dashboard.
/// All penalty, investigation, and hearing details are managed by
/// <see cref="StaffDisciplineSubEntityController"/>.
///
/// Gated per action rather than at class level: a disciplinary case carries an allegation against a
/// named employee, so the register, every query and every transition are HR-only — but the subject
/// of a case may read their own record, which is the record they are asked to answer. Stacked
/// <c>[Authorize]</c> attributes are ANDed, so a class-level role gate would lock the subject out of
/// their own case.
///
/// Reporting a case is HR-gated in this slice. Supervisors and HODs raise allegations in practice,
/// and FR-HR-080 defines what each may then issue — that authority model arrives with the workflow
/// slice, and opening reporting before it exists would leave sanctions ungoverned.
/// </summary>
[ApiController]
[Route("api/discipline/cases")]
[Authorize]
[DisciplineBusinessRules]
public class StaffDisciplineCasesController : ControllerBase
{
    private const string HrRoles = Constants.Roles.SuperAdmin + "," + Constants.Roles.Hr;

    private readonly IStaffDisciplinaryCaseService _caseService;
    private readonly ICurrentUserService _currentUser;

    public StaffDisciplineCasesController(
        IStaffDisciplinaryCaseService caseService,
        ICurrentUserService currentUser)
    {
        _caseService = caseService;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Whether the caller may see and act on any case, as opposed to only their own.
    /// "Admin" is included because the seeded administrator account is not in the HR role but is
    /// expected to be able to administer the module.
    /// </summary>
    private bool IsHr =>
        _currentUser.IsInRole(Constants.Roles.Hr) ||
        _currentUser.IsInRole(Constants.Roles.SuperAdmin) ||
        _currentUser.IsInRole("Admin");

    // =========================================================================
    // QUERIES
    // =========================================================================

    [Authorize(Roles = HrRoles)]
    [HttpGet]
    public async Task<ActionResult<PagedResult<StaffDisciplinaryActionSummaryDto>>> GetPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20)
        => Ok(await _caseService.GetPagedAsync(pageNumber, pageSize));

    [Authorize(Roles = HrRoles)]
    [HttpGet("open")]
    public async Task<ActionResult<IEnumerable<StaffDisciplinaryActionSummaryDto>>> GetAllOpen()
        => Ok(await _caseService.GetAllOpenAsync());

    /// <summary>
    /// The caller's own disciplinary cases. Token-derived and deliberately without an id segment —
    /// an id-bearing route would let anyone read anyone's cases by passing their employee id, which
    /// is exactly the hole this replaces.
    /// </summary>
    [HttpGet("mine")]
    public async Task<ActionResult<IEnumerable<StaffDisciplinaryActionSummaryDto>>> GetMine()
    {
        if (_currentUser.EmployeeId is not Guid employeeId)
            return Forbid();

        return Ok(await _caseService.GetByEmployeeAsync(employeeId));
    }

    /// <summary>
    /// Open to the case's subject as well as HR — an employee can read the allegation made against
    /// them, which is the record they are asked to answer.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<StaffDisciplinaryActionDto>> GetById(Guid id)
    {
        var disciplinaryCase = await _caseService.GetByIdAsync(id);

        if (!IsHr && disciplinaryCase.EmployeeId != _currentUser.EmployeeId)
            return Forbid();

        return Ok(disciplinaryCase);
    }

    [Authorize(Roles = HrRoles)]
    [HttpGet("number/{caseNumber}")]
    public async Task<ActionResult<StaffDisciplinaryActionDto?>> GetByCaseNumber(string caseNumber)
        => Ok(await _caseService.GetByCaseNumberAsync(caseNumber));

    [Authorize(Roles = HrRoles)]
    [HttpGet("employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<StaffDisciplinaryActionSummaryDto>>> GetByEmployee(Guid employeeId)
        => Ok(await _caseService.GetByEmployeeAsync(employeeId));

    [Authorize(Roles = HrRoles)]
    [HttpGet("employee/{employeeId:guid}/open-count")]
    public async Task<ActionResult<int>> GetOpenCountForEmployee(Guid employeeId)
        => Ok(await _caseService.GetOpenCaseCountForEmployeeAsync(employeeId));

    [Authorize(Roles = HrRoles)]
    [HttpGet("status/{status}")]
    public async Task<ActionResult<IEnumerable<StaffDisciplinaryActionSummaryDto>>> GetByStatus(DisciplinaryStatus status)
        => Ok(await _caseService.GetByStatusAsync(status));

    [Authorize(Roles = HrRoles)]
    [HttpGet("offense/{offenseId:guid}")]
    public async Task<ActionResult<IEnumerable<StaffDisciplinaryActionSummaryDto>>> GetByOffense(Guid offenseId)
        => Ok(await _caseService.GetByOffenseAsync(offenseId));

    [Authorize(Roles = HrRoles)]
    [HttpGet("severity/{severity}")]
    public async Task<ActionResult<IEnumerable<StaffDisciplinaryActionSummaryDto>>> GetBySeverity(StaffOffenseSeverity severity)
        => Ok(await _caseService.GetBySeverityAsync(severity));

    [Authorize(Roles = HrRoles)]
    [HttpGet("date-range")]
    public async Task<ActionResult<IEnumerable<StaffDisciplinaryActionSummaryDto>>> GetByDateRange(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] string field = "incident")
    {
        if (string.Equals(field, "reported", StringComparison.OrdinalIgnoreCase))
            return Ok(await _caseService.GetByReportedDateRangeAsync(from, to));

        return Ok(await _caseService.GetByIncidentDateRangeAsync(from, to));
    }

    [Authorize(Roles = HrRoles)]
    [HttpGet("pending/investigation")]
    public async Task<ActionResult<IEnumerable<StaffDisciplinaryActionSummaryDto>>> GetPendingInvestigation()
        => Ok(await _caseService.GetPendingInvestigationAsync());

    [Authorize(Roles = HrRoles)]
    [HttpGet("pending/hearing")]
    public async Task<ActionResult<IEnumerable<StaffDisciplinaryActionSummaryDto>>> GetPendingHearing()
        => Ok(await _caseService.GetPendingHearingAsync());

    [Authorize(Roles = HrRoles)]
    [HttpGet("pending/closure")]
    public async Task<ActionResult<IEnumerable<StaffDisciplinaryActionSummaryDto>>> GetPendingClosure()
        => Ok(await _caseService.GetPendingClosureAsync());

    [Authorize(Roles = HrRoles)]
    [HttpGet("with/active-warning")]
    public async Task<ActionResult<IEnumerable<StaffDisciplinaryActionSummaryDto>>> GetWithActiveWarning()
        => Ok(await _caseService.GetWithActiveWarningAsync());

    [Authorize(Roles = HrRoles)]
    [HttpGet("with/active-suspension")]
    public async Task<ActionResult<IEnumerable<StaffDisciplinaryActionSummaryDto>>> GetWithActiveSuspension()
        => Ok(await _caseService.GetWithActiveSuspensionAsync());

    [Authorize(Roles = HrRoles)]
    [HttpGet("with/outstanding-fine")]
    public async Task<ActionResult<IEnumerable<StaffDisciplinaryActionSummaryDto>>> GetWithOutstandingFine()
        => Ok(await _caseService.GetWithOutstandingFineAsync());

    [Authorize(Roles = HrRoles)]
    [HttpGet("with/pending-termination")]
    public async Task<ActionResult<IEnumerable<StaffDisciplinaryActionSummaryDto>>> GetWithPendingTermination()
        => Ok(await _caseService.GetWithPendingTerminationAsync());

    [Authorize(Roles = HrRoles)]
    [HttpGet("with/active-appeal")]
    public async Task<ActionResult<IEnumerable<StaffDisciplinaryActionSummaryDto>>> GetWithActiveAppeal()
        => Ok(await _caseService.GetWithActiveAppealAsync());

    [Authorize(Roles = HrRoles)]
    [HttpGet("with/active-legal-review")]
    public async Task<ActionResult<IEnumerable<StaffDisciplinaryActionSummaryDto>>> GetWithActiveLegalReview()
        => Ok(await _caseService.GetWithActiveLegalReviewAsync());

    [Authorize(Roles = HrRoles)]
    [HttpGet("number/{caseNumber}/exists")]
    public async Task<ActionResult<bool>> CaseNumberExists(string caseNumber)
    {
        var tenantId = _currentUser.TenantId;
        if (tenantId == null) return BadRequest("Tenant context could not be resolved.");
        return Ok(await _caseService.CaseNumberExistsAsync(caseNumber, tenantId.Value));
    }

    [Authorize(Roles = HrRoles)]
    [HttpGet("dashboard")]
    public async Task<ActionResult<StaffDisciplineDashboardDto>> GetDashboard()
        => Ok(await _caseService.GetDashboardAsync());

    // =========================================================================
    // CRUD
    // =========================================================================

    /// <summary>
    /// Raises a case. The reporter is taken from the caller's token unless HR names someone else —
    /// HR routinely records an allegation on behalf of the person who made it, but nobody else may
    /// attribute a report to another employee.
    /// </summary>
    [Authorize(Roles = HrRoles)]
    [HttpPost]
    public async Task<ActionResult<StaffDisciplinaryActionDto>> Create([FromBody] CreateStaffDisciplinaryActionDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null)   return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        if (!IsHr || dto.ReportedById == Guid.Empty)
            dto.ReportedById = employeeId.Value;

        var created = await _caseService.CreateAsync(dto, tenantId.Value, employeeId.Value);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [Authorize(Roles = HrRoles)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<StaffDisciplinaryActionDto>> Update(Guid id, [FromBody] UpdateStaffDisciplinaryActionDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        if (!IsHr || dto.ReportedById == Guid.Empty)
            dto.ReportedById = employeeId.Value;

        return Ok(await _caseService.UpdateAsync(dto, employeeId.Value));
    }

    [Authorize(Roles = HrRoles)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _caseService.DeleteAsync(id);
        return NoContent();
    }

    // =========================================================================
    // WORKFLOW TRANSITIONS
    // =========================================================================

    [Authorize(Roles = HrRoles)]
    [HttpPost("{id:guid}/submit")]
    public async Task<IActionResult> Submit(Guid id)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _caseService.SubmitAsync(id, employeeId.Value);
        return Ok(new { message = "Case submitted." });
    }

    [Authorize(Roles = HrRoles)]
    [HttpPost("{id:guid}/start-review")]
    public async Task<IActionResult> StartReview(Guid id)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _caseService.StartReviewAsync(id, employeeId.Value);
        return Ok(new { message = "Case moved to Under Review." });
    }

    [Authorize(Roles = HrRoles)]
    [HttpPost("{id:guid}/decision")]
    public async Task<IActionResult> RecordDecision(Guid id, [FromBody] RecordDisciplinaryDecisionDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        dto.CaseId = id;
        await _caseService.RecordDecisionAsync(dto, employeeId.Value);
        return Ok(new { message = "Decision recorded." });
    }

    [Authorize(Roles = HrRoles)]
    [HttpPost("{id:guid}/close")]
    public async Task<IActionResult> CloseCase(Guid id, [FromBody] CloseDisciplinaryCaseDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        dto.CaseId = id;
        await _caseService.CloseCaseAsync(dto, employeeId.Value);
        return Ok(new { message = "Case closed." });
    }

    [Authorize(Roles = HrRoles)]
    [HttpPost("{id:guid}/hold")]
    public async Task<IActionResult> PutOnHold(Guid id)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _caseService.PutOnHoldAsync(id, employeeId.Value);
        return Ok(new { message = "Case put on hold." });
    }

    [Authorize(Roles = HrRoles)]
    [HttpPost("{id:guid}/reactivate")]
    public async Task<IActionResult> Reactivate(Guid id)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _caseService.ReactivateCaseAsync(id, employeeId.Value);
        return Ok(new { message = "Case reactivated." });
    }

    [Authorize(Roles = HrRoles)]
    [HttpPost("{id:guid}/dismiss")]
    public async Task<IActionResult> Dismiss(Guid id)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _caseService.DismissCaseAsync(id, employeeId.Value);
        return Ok(new { message = "Case dismissed." });
    }
}
