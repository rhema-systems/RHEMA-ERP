using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Manages the one-to-one sub-entities of a disciplinary case:
/// Investigation, Hearing, Warning, Suspension, Fine, Appeal,
/// Corrective Action (with items), Termination, and Separation.
/// All routes are nested under <c>api/discipline</c>.
/// </summary>
[ApiController]
[Route("api/discipline")]
[Authorize]
public class StaffDisciplineSubEntityController : ControllerBase
{
    private readonly IStaffDisciplineInvestigationService _investigationService;
    private readonly IStaffDisciplineHearingService _hearingService;
    private readonly IStaffDisciplineWarningService _warningService;
    private readonly IStaffDisciplineSuspensionService _suspensionService;
    private readonly IStaffDisciplineFineService _fineService;
    private readonly IStaffDisciplineAppealService _appealService;
    private readonly IStaffDisciplineCorrectiveActionService _correctiveActionService;
    private readonly IStaffDisciplineTerminationService _terminationService;
    private readonly ICurrentUserService _currentUser;

    public StaffDisciplineSubEntityController(
        IStaffDisciplineInvestigationService investigationService,
        IStaffDisciplineHearingService hearingService,
        IStaffDisciplineWarningService warningService,
        IStaffDisciplineSuspensionService suspensionService,
        IStaffDisciplineFineService fineService,
        IStaffDisciplineAppealService appealService,
        IStaffDisciplineCorrectiveActionService correctiveActionService,
        IStaffDisciplineTerminationService terminationService,
        ICurrentUserService currentUser)
    {
        _investigationService = investigationService;
        _hearingService = hearingService;
        _warningService = warningService;
        _suspensionService = suspensionService;
        _fineService = fineService;
        _appealService = appealService;
        _correctiveActionService = correctiveActionService;
        _terminationService = terminationService;
        _currentUser = currentUser;
    }

    // =========================================================================
    // INVESTIGATION
    // =========================================================================

    [HttpGet("cases/{caseId:guid}/investigation")]
    public async Task<ActionResult<StaffDisciplineInvestigationDto?>> GetInvestigation(Guid caseId)
        => Ok(await _investigationService.GetByCaseIdAsync(caseId));

    [HttpGet("investigations/open")]
    public async Task<ActionResult<IEnumerable<StaffDisciplineInvestigationDto>>> GetOpenInvestigations()
        => Ok(await _investigationService.GetOpenInvestigationsAsync());

    [HttpGet("investigations/overdue")]
    public async Task<ActionResult<IEnumerable<StaffDisciplineInvestigationDto>>> GetOverdueInvestigations(
        [FromQuery] int maxDays = 30)
        => Ok(await _investigationService.GetOverdueInvestigationsAsync(maxDays));

    [HttpGet("investigations/by-investigator/{investigatorId:guid}")]
    public async Task<ActionResult<IEnumerable<StaffDisciplineInvestigationDto>>> GetByInvestigator(Guid investigatorId)
        => Ok(await _investigationService.GetByInvestigatorAsync(investigatorId));

    [HttpPost("cases/{caseId:guid}/investigation")]
    public async Task<ActionResult<StaffDisciplineInvestigationDto>> OpenInvestigation(
        Guid caseId, [FromBody] OpenInvestigationDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;
        if (tenantId == null)   return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        dto.CaseId = caseId;
        var created = await _investigationService.OpenAsync(dto, tenantId.Value, employeeId.Value);
        return CreatedAtAction(nameof(GetInvestigation), new { caseId }, created);
    }

    [HttpPut("cases/{caseId:guid}/investigation")]
    public async Task<ActionResult<StaffDisciplineInvestigationDto>> UpdateInvestigation(
        Guid caseId, [FromBody] UpdateInvestigationDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        dto.CaseId = caseId;
        return Ok(await _investigationService.UpdateAsync(dto, employeeId.Value));
    }

    [HttpPost("cases/{caseId:guid}/investigation/complete")]
    public async Task<IActionResult> CompleteInvestigation(Guid caseId, [FromBody] string findings)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        await _investigationService.CompleteAsync(caseId, findings, employeeId.Value);
        return Ok(new { message = "Investigation completed." });
    }

    // =========================================================================
    // HEARING
    // =========================================================================

    [HttpGet("cases/{caseId:guid}/hearing")]
    public async Task<ActionResult<StaffDisciplineHearingDto?>> GetHearing(Guid caseId)
        => Ok(await _hearingService.GetByCaseIdAsync(caseId));

    [HttpGet("hearings/upcoming")]
    public async Task<ActionResult<IEnumerable<StaffDisciplineHearingDto>>> GetUpcomingHearings(
        [FromQuery] int daysAhead = 14)
        => Ok(await _hearingService.GetUpcomingHearingsAsync(daysAhead));

    [HttpGet("hearings/awaiting-outcome")]
    public async Task<ActionResult<IEnumerable<StaffDisciplineHearingDto>>> GetHearingsAwaitingOutcome()
        => Ok(await _hearingService.GetAwaitingOutcomeAsync());

    [HttpGet("hearings/by-officer/{officerId:guid}")]
    public async Task<ActionResult<IEnumerable<StaffDisciplineHearingDto>>> GetByHearingOfficer(Guid officerId)
        => Ok(await _hearingService.GetByHearingOfficerAsync(officerId));

    [HttpPost("cases/{caseId:guid}/hearing")]
    public async Task<ActionResult<StaffDisciplineHearingDto>> ScheduleHearing(
        Guid caseId, [FromBody] ScheduleHearingDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;
        if (tenantId == null)   return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        dto.CaseId = caseId;
        var created = await _hearingService.ScheduleAsync(dto, tenantId.Value, employeeId.Value);
        return CreatedAtAction(nameof(GetHearing), new { caseId }, created);
    }

    [HttpPut("cases/{caseId:guid}/hearing/outcome")]
    public async Task<ActionResult<StaffDisciplineHearingDto>> RecordHearingOutcome(
        Guid caseId, [FromBody] RecordHearingOutcomeDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        dto.CaseId = caseId;
        return Ok(await _hearingService.RecordOutcomeAsync(dto, employeeId.Value));
    }

    // =========================================================================
    // WARNING PENALTY
    // =========================================================================

    [HttpGet("cases/{caseId:guid}/warning")]
    public async Task<ActionResult<StaffDisciplineWarningDto?>> GetWarning(Guid caseId)
        => Ok(await _warningService.GetByCaseIdAsync(caseId));

    [HttpGet("warnings/employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<StaffDisciplineWarningDto>>> GetWarningsByEmployee(Guid employeeId)
        => Ok(await _warningService.GetByEmployeeAsync(employeeId));

    [HttpGet("warnings/employee/{employeeId:guid}/active")]
    public async Task<ActionResult<IEnumerable<StaffDisciplineWarningDto>>> GetActiveWarningsForEmployee(Guid employeeId)
        => Ok(await _warningService.GetActiveWarningsForEmployeeAsync(employeeId));

    [HttpGet("warnings/type/{warningType}")]
    public async Task<ActionResult<IEnumerable<StaffDisciplineWarningDto>>> GetWarningsByType(DisciplinaryWarningType warningType)
        => Ok(await _warningService.GetByTypeAsync(warningType));

    [HttpGet("warnings/expiring")]
    public async Task<ActionResult<IEnumerable<StaffDisciplineWarningDto>>> GetExpiringWarnings(
        [FromQuery] int daysAhead = 30)
        => Ok(await _warningService.GetExpiringAsync(daysAhead));

    [HttpPost("cases/{caseId:guid}/warning")]
    public async Task<ActionResult<StaffDisciplineWarningDto>> RecordWarning(
        Guid caseId, [FromBody] RecordWarningPenaltyDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;
        if (tenantId == null)   return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        dto.CaseId = caseId;
        var created = await _warningService.RecordAsync(dto, tenantId.Value, employeeId.Value);
        return CreatedAtAction(nameof(GetWarning), new { caseId }, created);
    }

    [HttpPut("cases/{caseId:guid}/warning")]
    public async Task<ActionResult<StaffDisciplineWarningDto>> UpdateWarning(
        Guid caseId, [FromBody] UpdateWarningPenaltyDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        dto.CaseId = caseId;
        return Ok(await _warningService.UpdateAsync(dto, employeeId.Value));
    }

    // =========================================================================
    // SUSPENSION PENALTY
    // =========================================================================

    [HttpGet("cases/{caseId:guid}/suspension")]
    public async Task<ActionResult<StaffDisciplineSuspensionDto?>> GetSuspension(Guid caseId)
        => Ok(await _suspensionService.GetByCaseIdAsync(caseId));

    [HttpGet("suspensions/employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<StaffDisciplineSuspensionDto>>> GetSuspensionsByEmployee(Guid employeeId)
        => Ok(await _suspensionService.GetByEmployeeAsync(employeeId));

    [HttpGet("suspensions/active")]
    public async Task<ActionResult<IEnumerable<StaffDisciplineSuspensionDto>>> GetCurrentlyActiveSuspensions()
        => Ok(await _suspensionService.GetCurrentlyActiveAsync());

    [HttpGet("suspensions/upcoming")]
    public async Task<ActionResult<IEnumerable<StaffDisciplineSuspensionDto>>> GetUpcomingSuspensions(
        [FromQuery] int daysAhead = 7)
        => Ok(await _suspensionService.GetUpcomingAsync(daysAhead));

    [HttpPost("cases/{caseId:guid}/suspension")]
    public async Task<ActionResult<StaffDisciplineSuspensionDto>> RecordSuspension(
        Guid caseId, [FromBody] RecordSuspensionPenaltyDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;
        if (tenantId == null)   return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        dto.CaseId = caseId;
        var created = await _suspensionService.RecordAsync(dto, tenantId.Value, employeeId.Value);
        return CreatedAtAction(nameof(GetSuspension), new { caseId }, created);
    }

    [HttpPut("cases/{caseId:guid}/suspension")]
    public async Task<ActionResult<StaffDisciplineSuspensionDto>> UpdateSuspension(
        Guid caseId, [FromBody] UpdateSuspensionPenaltyDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        dto.CaseId = caseId;
        return Ok(await _suspensionService.UpdateAsync(dto, employeeId.Value));
    }

    // =========================================================================
    // FINE PENALTY
    // =========================================================================

    [HttpGet("cases/{caseId:guid}/fine")]
    public async Task<ActionResult<StaffDisciplineFineDto?>> GetFine(Guid caseId)
        => Ok(await _fineService.GetByCaseIdAsync(caseId));

    [HttpGet("fines/employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<StaffDisciplineFineDto>>> GetFinesByEmployee(Guid employeeId)
        => Ok(await _fineService.GetByEmployeeAsync(employeeId));

    [HttpGet("fines/employee/{employeeId:guid}/outstanding-balance")]
    public async Task<ActionResult<decimal>> GetOutstandingBalanceForEmployee(Guid employeeId)
        => Ok(await _fineService.GetTotalOutstandingBalanceForEmployeeAsync(employeeId));

    [HttpGet("fines/outstanding")]
    public async Task<ActionResult<IEnumerable<StaffDisciplineFineDto>>> GetOutstandingFines()
        => Ok(await _fineService.GetOutstandingAsync());

    [HttpGet("fines/overdue")]
    public async Task<ActionResult<IEnumerable<StaffDisciplineFineDto>>> GetOverdueFines()
        => Ok(await _fineService.GetOverdueAsync());

    [HttpPost("cases/{caseId:guid}/fine")]
    public async Task<ActionResult<StaffDisciplineFineDto>> RecordFine(
        Guid caseId, [FromBody] RecordFinePenaltyDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;
        if (tenantId == null)   return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        dto.CaseId = caseId;
        var created = await _fineService.RecordAsync(dto, tenantId.Value, employeeId.Value);
        return CreatedAtAction(nameof(GetFine), new { caseId }, created);
    }

    [HttpPost("cases/{caseId:guid}/fine/payment")]
    public async Task<ActionResult<StaffDisciplineFineDto>> RecordFinePayment(
        Guid caseId, [FromBody] RecordFinePaymentDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        dto.CaseId = caseId;
        return Ok(await _fineService.RecordPaymentAsync(dto, employeeId.Value));
    }

    // =========================================================================
    // APPEAL
    // =========================================================================

    [HttpGet("cases/{caseId:guid}/appeal")]
    public async Task<ActionResult<StaffDisciplineAppealDto?>> GetAppeal(Guid caseId)
        => Ok(await _appealService.GetByCaseIdAsync(caseId));

    [HttpGet("appeals/{id:guid}")]
    public async Task<ActionResult<StaffDisciplineAppealDto?>> GetAppealById(Guid id)
        => Ok(await _appealService.GetByIdAsync(id));

    [HttpGet("appeals/employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<StaffDisciplineAppealDto>>> GetAppealsByEmployee(Guid employeeId)
        => Ok(await _appealService.GetByEmployeeAsync(employeeId));

    [HttpGet("appeals/status/{status}")]
    public async Task<ActionResult<IEnumerable<StaffDisciplineAppealDto>>> GetAppealsByStatus(DisciplineAppealStatus status)
        => Ok(await _appealService.GetByStatusAsync(status));

    [HttpGet("appeals/pending-hearing")]
    public async Task<ActionResult<IEnumerable<StaffDisciplineAppealDto>>> GetAppealsPendingHearing()
        => Ok(await _appealService.GetPendingHearingScheduleAsync());

    [HttpGet("appeals/awaiting-outcome")]
    public async Task<ActionResult<IEnumerable<StaffDisciplineAppealDto>>> GetAppealsAwaitingOutcome()
        => Ok(await _appealService.GetAwaitingOutcomeAsync());

    [HttpPost("cases/{caseId:guid}/appeal")]
    public async Task<ActionResult<StaffDisciplineAppealDto>> FileAppeal(
        Guid caseId, [FromBody] FileAppealDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;
        if (tenantId == null)   return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        dto.CaseId = caseId;
        var created = await _appealService.FileAsync(dto, tenantId.Value, employeeId.Value);
        return CreatedAtAction(nameof(GetAppeal), new { caseId }, created);
    }

    [HttpPost("cases/{caseId:guid}/appeal/schedule-hearing")]
    public async Task<IActionResult> ScheduleAppealHearing(Guid caseId, [FromBody] ScheduleAppealHearingDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        dto.CaseId = caseId;
        await _appealService.ScheduleHearingAsync(dto, employeeId.Value);
        return Ok(new { message = "Appeal hearing scheduled." });
    }

    [HttpPost("cases/{caseId:guid}/appeal/outcome")]
    public async Task<IActionResult> RecordAppealOutcome(Guid caseId, [FromBody] RecordAppealOutcomeDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        dto.CaseId = caseId;
        await _appealService.RecordOutcomeAsync(dto, employeeId.Value);
        return Ok(new { message = "Appeal outcome recorded." });
    }

    // =========================================================================
    // CORRECTIVE ACTION
    // =========================================================================

    [HttpGet("cases/{caseId:guid}/corrective-action")]
    public async Task<ActionResult<StaffDisciplineCorrectiveActionDto?>> GetCorrectiveAction(Guid caseId)
        => Ok(await _correctiveActionService.GetByCaseIdAsync(caseId));

    [HttpGet("corrective-actions/{id:guid}")]
    public async Task<ActionResult<StaffDisciplineCorrectiveActionDto?>> GetCorrectiveActionById(Guid id)
        => Ok(await _correctiveActionService.GetByIdAsync(id));

    [HttpGet("corrective-actions/employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<StaffDisciplineCorrectiveActionDto>>> GetCorrectiveActionsByEmployee(Guid employeeId)
        => Ok(await _correctiveActionService.GetByEmployeeAsync(employeeId));

    [HttpGet("corrective-actions/supervisor/{supervisorId:guid}")]
    public async Task<ActionResult<IEnumerable<StaffDisciplineCorrectiveActionDto>>> GetCorrectiveActionsBySupervisor(Guid supervisorId)
        => Ok(await _correctiveActionService.GetBySupervisorAsync(supervisorId));

    [HttpGet("corrective-actions/status/{status}")]
    public async Task<ActionResult<IEnumerable<StaffDisciplineCorrectiveActionDto>>> GetCorrectiveActionsByStatus(DisciplineCorrectiveActionStatus status)
        => Ok(await _correctiveActionService.GetByStatusAsync(status));

    [HttpGet("corrective-actions/overdue")]
    public async Task<ActionResult<IEnumerable<StaffDisciplineCorrectiveActionDto>>> GetOverdueCorrectiveActions()
        => Ok(await _correctiveActionService.GetOverdueAsync());

    [HttpGet("corrective-actions/due-for-review")]
    public async Task<ActionResult<IEnumerable<StaffDisciplineCorrectiveActionDto>>> GetCorrectiveActionsDueForReview(
        [FromQuery] int daysAhead = 14)
        => Ok(await _correctiveActionService.GetDueForReviewAsync(daysAhead));

    [HttpPost("cases/{caseId:guid}/corrective-action")]
    public async Task<ActionResult<StaffDisciplineCorrectiveActionDto>> CreateCorrectiveAction(
        Guid caseId, [FromBody] CreateStaffDisciplineCorrectiveActionDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;
        if (tenantId == null)   return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        dto.DisciplinaryActionId = caseId;
        var created = await _correctiveActionService.CreateAsync(dto, tenantId.Value, employeeId.Value);
        return CreatedAtAction(nameof(GetCorrectiveActionById), new { id = created.Id }, created);
    }

    [HttpPut("corrective-actions/{id:guid}")]
    public async Task<ActionResult<StaffDisciplineCorrectiveActionDto>> UpdateCorrectiveAction(
        Guid id, [FromBody] UpdateStaffDisciplineCorrectiveActionDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        return Ok(await _correctiveActionService.UpdateAsync(dto, employeeId.Value));
    }

    [HttpPost("corrective-actions/{id:guid}/complete")]
    public async Task<IActionResult> CompleteCorrectiveAction(Guid id)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        await _correctiveActionService.CompleteAsync(id, employeeId.Value);
        return Ok(new { message = "Corrective action plan completed." });
    }

    [HttpDelete("corrective-actions/{id:guid}")]
    public async Task<IActionResult> DeleteCorrectiveAction(Guid id)
    {
        await _correctiveActionService.DeleteAsync(id);
        return NoContent();
    }

    [HttpPost("corrective-actions/{correctiveActionId:guid}/items")]
    public async Task<ActionResult<StaffDisciplineCorrectiveActionItemDto>> AddCorrectiveActionItem(
        Guid correctiveActionId, [FromBody] CreateStaffDisciplineCorrectiveActionItemDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;
        if (tenantId == null)   return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        dto.CorrectiveActionId = correctiveActionId;
        return Ok(await _correctiveActionService.AddItemAsync(dto, tenantId.Value, employeeId.Value));
    }

    [HttpPut("corrective-action-items/{itemId:guid}")]
    public async Task<ActionResult<StaffDisciplineCorrectiveActionItemDto>> UpdateCorrectiveActionItem(
        Guid itemId, [FromBody] UpdateStaffDisciplineCorrectiveActionItemDto dto)
    {
        if (itemId != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        return Ok(await _correctiveActionService.UpdateItemAsync(dto, employeeId.Value));
    }

    [HttpPost("corrective-action-items/{itemId:guid}/complete")]
    public async Task<IActionResult> CompleteCorrectiveActionItem(Guid itemId, [FromBody] string completionNotes)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        await _correctiveActionService.CompleteItemAsync(itemId, completionNotes, employeeId.Value);
        return Ok(new { message = "Corrective action item completed." });
    }

    [HttpDelete("corrective-action-items/{itemId:guid}")]
    public async Task<IActionResult> DeleteCorrectiveActionItem(Guid itemId)
    {
        await _correctiveActionService.DeleteItemAsync(itemId);
        return NoContent();
    }

    // =========================================================================
    // TERMINATION
    // =========================================================================

    [HttpGet("cases/{caseId:guid}/termination")]
    public async Task<ActionResult<StaffDisciplineTerminationDto?>> GetTermination(Guid caseId)
        => Ok(await _terminationService.GetByCaseIdAsync(caseId));

    [HttpGet("terminations/type/{type}")]
    public async Task<ActionResult<IEnumerable<StaffDisciplineTerminationDto>>> GetTerminationsByType(EmployeeTerminationType type)
        => Ok(await _terminationService.GetByTypeAsync(type));

    [HttpGet("terminations/eligible-for-rehire")]
    public async Task<ActionResult<IEnumerable<StaffDisciplineTerminationDto>>> GetEligibleForRehire()
        => Ok(await _terminationService.GetEligibleForRehireAsync());

    [HttpGet("terminations/pending-paycheck")]
    public async Task<ActionResult<IEnumerable<StaffDisciplineTerminationDto>>> GetPendingPaycheckProcessing()
        => Ok(await _terminationService.GetPendingPaycheckProcessingAsync());

    [HttpPost("cases/{caseId:guid}/termination")]
    public async Task<ActionResult<StaffDisciplineTerminationDto>> RecordTermination(
        Guid caseId, [FromBody] RecordTerminationDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;
        if (tenantId == null)   return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        dto.CaseId = caseId;
        var created = await _terminationService.RecordAsync(dto, tenantId.Value, employeeId.Value);
        return CreatedAtAction(nameof(GetTermination), new { caseId }, created);
    }

    [HttpPut("cases/{caseId:guid}/termination")]
    public async Task<ActionResult<StaffDisciplineTerminationDto>> UpdateTermination(
        Guid caseId, [FromBody] UpdateTerminationDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        dto.CaseId = caseId;
        return Ok(await _terminationService.UpdateAsync(dto, employeeId.Value));
    }

    // =========================================================================
    // SEPARATION
    // =========================================================================

    [HttpGet("cases/{caseId:guid}/separation")]
    public async Task<ActionResult<StaffDisciplineSeparationDto?>> GetSeparation(Guid caseId)
        => Ok(await _terminationService.GetSeparationByCaseIdAsync(caseId));

    [HttpGet("separations/incomplete")]
    public async Task<ActionResult<IEnumerable<StaffDisciplineSeparationDto>>> GetIncompleteSeparations()
        => Ok(await _terminationService.GetIncompleteSeparationsAsync());

    [HttpPost("cases/{caseId:guid}/separation")]
    public async Task<ActionResult<StaffDisciplineSeparationDto>> InitiateSeparation(
        Guid caseId, [FromBody] InitiateSeparationDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;
        if (tenantId == null)   return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        dto.CaseId = caseId;
        var created = await _terminationService.InitiateSeparationAsync(dto, tenantId.Value, employeeId.Value);
        return CreatedAtAction(nameof(GetSeparation), new { caseId }, created);
    }

    [HttpPut("cases/{caseId:guid}/separation")]
    public async Task<ActionResult<StaffDisciplineSeparationDto>> UpdateSeparation(
        Guid caseId, [FromBody] UpdateSeparationDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        dto.CaseId = caseId;
        return Ok(await _terminationService.UpdateSeparationAsync(dto, employeeId.Value));
    }
}
