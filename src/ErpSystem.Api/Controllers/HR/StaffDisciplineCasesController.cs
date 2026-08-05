using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Manages disciplinary case headers: CRUD, status workflow, and the module dashboard.
/// All penalty, investigation, and hearing details are managed by
/// <see cref="StaffDisciplineSubEntityController"/>.
/// </summary>
[ApiController]
[Route("api/discipline/cases")]
[Authorize]
public class StaffDisciplineCasesController : ControllerBase
{
    private readonly IStaffDisciplinaryCaseService _caseService;
    private readonly ICurrentUserService _currentUser;

    public StaffDisciplineCasesController(
        IStaffDisciplinaryCaseService caseService,
        ICurrentUserService currentUser)
    {
        _caseService = caseService;
        _currentUser = currentUser;
    }

    // =========================================================================
    // QUERIES
    // =========================================================================

    [HttpGet]
    public async Task<ActionResult<PagedResult<StaffDisciplinaryActionSummaryDto>>> GetPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20)
        => Ok(await _caseService.GetPagedAsync(pageNumber, pageSize));

    [HttpGet("open")]
    public async Task<ActionResult<IEnumerable<StaffDisciplinaryActionSummaryDto>>> GetAllOpen()
        => Ok(await _caseService.GetAllOpenAsync());

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<StaffDisciplinaryActionDto>> GetById(Guid id)
        => Ok(await _caseService.GetByIdAsync(id));

    [HttpGet("number/{caseNumber}")]
    public async Task<ActionResult<StaffDisciplinaryActionDto?>> GetByCaseNumber(string caseNumber)
        => Ok(await _caseService.GetByCaseNumberAsync(caseNumber));

    [HttpGet("employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<StaffDisciplinaryActionSummaryDto>>> GetByEmployee(Guid employeeId)
        => Ok(await _caseService.GetByEmployeeAsync(employeeId));

    [HttpGet("employee/{employeeId:guid}/open-count")]
    public async Task<ActionResult<int>> GetOpenCountForEmployee(Guid employeeId)
        => Ok(await _caseService.GetOpenCaseCountForEmployeeAsync(employeeId));

    [HttpGet("status/{status}")]
    public async Task<ActionResult<IEnumerable<StaffDisciplinaryActionSummaryDto>>> GetByStatus(DisciplinaryStatus status)
        => Ok(await _caseService.GetByStatusAsync(status));

    [HttpGet("offense/{offenseId:guid}")]
    public async Task<ActionResult<IEnumerable<StaffDisciplinaryActionSummaryDto>>> GetByOffense(Guid offenseId)
        => Ok(await _caseService.GetByOffenseAsync(offenseId));

    [HttpGet("severity/{severity}")]
    public async Task<ActionResult<IEnumerable<StaffDisciplinaryActionSummaryDto>>> GetBySeverity(StaffOffenseSeverity severity)
        => Ok(await _caseService.GetBySeverityAsync(severity));

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

    [HttpGet("pending/investigation")]
    public async Task<ActionResult<IEnumerable<StaffDisciplinaryActionSummaryDto>>> GetPendingInvestigation()
        => Ok(await _caseService.GetPendingInvestigationAsync());

    [HttpGet("pending/hearing")]
    public async Task<ActionResult<IEnumerable<StaffDisciplinaryActionSummaryDto>>> GetPendingHearing()
        => Ok(await _caseService.GetPendingHearingAsync());

    [HttpGet("pending/closure")]
    public async Task<ActionResult<IEnumerable<StaffDisciplinaryActionSummaryDto>>> GetPendingClosure()
        => Ok(await _caseService.GetPendingClosureAsync());

    [HttpGet("with/active-warning")]
    public async Task<ActionResult<IEnumerable<StaffDisciplinaryActionSummaryDto>>> GetWithActiveWarning()
        => Ok(await _caseService.GetWithActiveWarningAsync());

    [HttpGet("with/active-suspension")]
    public async Task<ActionResult<IEnumerable<StaffDisciplinaryActionSummaryDto>>> GetWithActiveSuspension()
        => Ok(await _caseService.GetWithActiveSuspensionAsync());

    [HttpGet("with/outstanding-fine")]
    public async Task<ActionResult<IEnumerable<StaffDisciplinaryActionSummaryDto>>> GetWithOutstandingFine()
        => Ok(await _caseService.GetWithOutstandingFineAsync());

    [HttpGet("with/pending-termination")]
    public async Task<ActionResult<IEnumerable<StaffDisciplinaryActionSummaryDto>>> GetWithPendingTermination()
        => Ok(await _caseService.GetWithPendingTerminationAsync());

    [HttpGet("with/active-appeal")]
    public async Task<ActionResult<IEnumerable<StaffDisciplinaryActionSummaryDto>>> GetWithActiveAppeal()
        => Ok(await _caseService.GetWithActiveAppealAsync());

    [HttpGet("with/active-legal-review")]
    public async Task<ActionResult<IEnumerable<StaffDisciplinaryActionSummaryDto>>> GetWithActiveLegalReview()
        => Ok(await _caseService.GetWithActiveLegalReviewAsync());

    [HttpGet("number/{caseNumber}/exists")]
    public async Task<ActionResult<bool>> CaseNumberExists(string caseNumber)
    {
        var tenantId = _currentUser.TenantId;
        if (tenantId == null) return BadRequest("Tenant context could not be resolved.");
        return Ok(await _caseService.CaseNumberExistsAsync(caseNumber, tenantId.Value));
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<StaffDisciplineDashboardDto>> GetDashboard()
        => Ok(await _caseService.GetDashboardAsync());

    // =========================================================================
    // CRUD
    // =========================================================================

    [HttpPost]
    public async Task<ActionResult<StaffDisciplinaryActionDto>> Create([FromBody] CreateStaffDisciplinaryActionDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null)   return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        var created = await _caseService.CreateAsync(dto, tenantId.Value, employeeId.Value);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<StaffDisciplinaryActionDto>> Update(Guid id, [FromBody] UpdateStaffDisciplinaryActionDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _caseService.UpdateAsync(dto, employeeId.Value));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _caseService.DeleteAsync(id);
        return NoContent();
    }

    // =========================================================================
    // WORKFLOW TRANSITIONS
    // =========================================================================

    [HttpPost("{id:guid}/submit")]
    public async Task<IActionResult> Submit(Guid id)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _caseService.SubmitAsync(id, employeeId.Value);
        return Ok(new { message = "Case submitted." });
    }

    [HttpPost("{id:guid}/start-review")]
    public async Task<IActionResult> StartReview(Guid id)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _caseService.StartReviewAsync(id, employeeId.Value);
        return Ok(new { message = "Case moved to Under Review." });
    }

    [HttpPost("{id:guid}/decision")]
    public async Task<IActionResult> RecordDecision(Guid id, [FromBody] RecordDisciplinaryDecisionDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        dto.CaseId = id;
        await _caseService.RecordDecisionAsync(dto);
        return Ok(new { message = "Decision recorded." });
    }

    [HttpPost("{id:guid}/close")]
    public async Task<IActionResult> CloseCase(Guid id, [FromBody] CloseDisciplinaryCaseDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        dto.CaseId = id;
        await _caseService.CloseCaseAsync(dto);
        return Ok(new { message = "Case closed." });
    }

    [HttpPost("{id:guid}/hold")]
    public async Task<IActionResult> PutOnHold(Guid id)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _caseService.PutOnHoldAsync(id, employeeId.Value);
        return Ok(new { message = "Case put on hold." });
    }

    [HttpPost("{id:guid}/reactivate")]
    public async Task<IActionResult> Reactivate(Guid id)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _caseService.ReactivateCaseAsync(id, employeeId.Value);
        return Ok(new { message = "Case reactivated." });
    }

    [HttpPost("{id:guid}/dismiss")]
    public async Task<IActionResult> Dismiss(Guid id)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _caseService.DismissCaseAsync(id, employeeId.Value);
        return Ok(new { message = "Case dismissed." });
    }
}
