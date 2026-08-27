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
/// Employee-relations cases: FR-HR-181's grievance and its escalation ladder, and — since area 9c —
/// the other case types an employee-relations function handles.
///
/// Gated per action, and the split is the opposite way round from the disciplinary case. A case is
/// raised ABOUT an employee, so it is HR's with a narrow window for the subject; a grievance is
/// raised BY one, so filing, escalating and withdrawing are the employee's and HR cannot do them at
/// all. What HR owns is answering, assigning, the parties, and seeing the whole register.
/// </summary>
/// <remarks>
/// <b>Two routes, one controller (area 9c decision D-6).</b> <c>api/hr/employee-relations</c> is the
/// correct name for a register that holds union consultations and mediations as well as grievances.
/// <c>api/grievances</c> is kept as an alias because the portal's four screens already call it, and
/// it is dropped in slice 12 — <i>after</i> the two greps prove nothing still does. Do not add new
/// clients against the old route.
/// </remarks>
[ApiController]
[Route("api/hr/employee-relations")]
[Route("api/grievances")]
[Authorize(Policy = "InternalOnly")]
[DisciplineBusinessRules]
public class StaffGrievancesController : ControllerBase
{
    private readonly IStaffGrievanceService _service;
    private readonly ICurrentUserService _currentUser;

    public StaffGrievancesController(IStaffGrievanceService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    // =========================================================================
    // HR's register
    // =========================================================================

    [Authorize(Policy = HrPermissions.DisciplineReadPolicy)]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<StaffGrievanceSummaryDto>>> GetAll()
        => Ok(await _service.GetAllAsync());

    [Authorize(Policy = HrPermissions.DisciplineReadPolicy)]
    [HttpGet("status/{status}")]
    public async Task<ActionResult<IEnumerable<StaffGrievanceSummaryDto>>> GetByStatus(GrievanceStatus status)
        => Ok(await _service.GetByStatusAsync(status));

    /// <summary>
    /// The employee-relations register — paged, filtered, and the read the desk screens use.
    /// </summary>
    /// <remarks>
    /// Prefer this to the unpaged <c>GET</c> above, which materialises every case in the tenant
    /// along with all of its steps and parties. <paramref name="pageSize"/> is capped at 200 by the
    /// service, so a caller cannot ask for the whole register by asking for a big enough page.
    /// </remarks>
    [Authorize(Policy = HrPermissions.DisciplineReadPolicy)]
    [HttpGet("register")]
    public async Task<ActionResult<PagedResult<StaffGrievanceSummaryDto>>> GetRegister(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] EmployeeRelationsCaseType? caseType = null,
        [FromQuery] GrievanceStatus? status = null,
        [FromQuery] GrievanceEscalationLevel? level = null,
        [FromQuery] Guid? organizationUnitId = null,
        [FromQuery] bool? awaitingResponseOnly = null,
        [FromQuery] string? search = null)
        => Ok(await _service.GetPagedAsync(
            page, pageSize, caseType, status, level, organizationUnitId, awaitingResponseOnly, search));

    /// <summary>Where the ladder is stuck — grievances whose current rung has not answered.</summary>
    [Authorize(Policy = HrPermissions.DisciplineReadPolicy)]
    [HttpGet("awaiting-response")]
    public async Task<ActionResult<IEnumerable<StaffGrievanceSummaryDto>>> GetAwaitingResponse(
        [FromQuery] GrievanceEscalationLevel? level = null)
        => Ok(await _service.GetAwaitingResponseAsync(level));

    // =========================================================================
    // The employee's own surface
    // =========================================================================

    /// <summary>The caller's own grievances. Token-derived, with no id-bearing equivalent.</summary>
    [HttpGet("mine")]
    public async Task<ActionResult<IEnumerable<StaffGrievanceSummaryDto>>> GetMine()
    {
        if (_currentUser.EmployeeId is not Guid employeeId) return Forbid();
        return Ok(await _service.GetMineAsync(employeeId));
    }

    /// <summary>
    /// Grievances this caller has been asked to answer. Open by design: the person answering at the
    /// supervisor or HOD rung is not in HR, and the register above refuses them — so without this
    /// they would have no way to see what they owe.
    /// </summary>
    [HttpGet("awaiting-my-response")]
    public async Task<ActionResult<IEnumerable<StaffGrievanceSummaryDto>>> GetAwaitingMyResponse()
    {
        if (_currentUser.EmployeeId is not Guid employeeId) return Forbid();
        return Ok(await _service.GetAwaitingMyResponseAsync(employeeId));
    }

    /// <summary>
    /// One grievance. The service refuses anyone but the griever, HR, or someone named on a step —
    /// a grievance is usually about somebody, so the read surface is narrow on purpose.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<StaffGrievanceDto>> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id, _currentUser.EmployeeId));

    /// <summary>
    /// Raises a grievance. Deliberately ungated: this is the employee's own act, and HR cannot do it
    /// for them — there is no employee id on the payload to allow it.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<StaffGrievanceDto>> File([FromBody] FileGrievanceDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (_currentUser.EmployeeId is not Guid employeeId)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        var created = await _service.FileAsync(dto, employeeId);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Escalates to the next rung. The griever's act — the service refuses everyone else.</summary>
    [HttpPost("{id:guid}/escalate")]
    public async Task<ActionResult<StaffGrievanceDto>> Escalate(Guid id, [FromBody] EscalateGrievanceDto? dto = null)
    {
        if (_currentUser.EmployeeId is not Guid employeeId)
            return BadRequest("Your user account is not linked to an employee record.");

        return Ok(await _service.EscalateAsync(id, dto ?? new EscalateGrievanceDto(), employeeId));
    }

    [HttpPost("{id:guid}/withdraw")]
    public async Task<ActionResult<StaffGrievanceDto>> Withdraw(Guid id, [FromBody] WithdrawGrievanceDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (_currentUser.EmployeeId is not Guid employeeId)
            return BadRequest("Your user account is not linked to an employee record.");

        return Ok(await _service.WithdrawAsync(id, dto, employeeId));
    }

    // =========================================================================
    // Answering
    // =========================================================================

    /// <summary>Names who should answer at the current rung — how a supervisor or HOD is brought in.</summary>
    [Authorize(Policy = HrPermissions.DisciplineWritePolicy)]
    [HttpPost("{id:guid}/assign")]
    public async Task<ActionResult<StaffGrievanceDto>> Assign(Guid id, [FromBody] AssignGrievanceStepDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Ok(await _service.AssignCurrentStepAsync(id, dto));
    }

    /// <summary>
    /// Answers at the current rung. Ungated here because the responder is often not in HR — the
    /// service allows HR or whoever the step names, and refuses everyone else including the griever.
    /// </summary>
    [HttpPost("{id:guid}/respond")]
    public async Task<ActionResult<StaffGrievanceDto>> Respond(Guid id, [FromBody] RespondToGrievanceDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (_currentUser.EmployeeId is not Guid employeeId)
            return BadRequest("Your user account is not linked to an employee record.");

        return Ok(await _service.RespondAsync(id, dto, employeeId));
    }

    // =========================================================================
    // The wider register — area 9c slice 1
    // =========================================================================

    /// <summary>
    /// Opens an employee-relations case that is not a grievance — a mediation, a welfare matter,
    /// a union consultation.
    /// </summary>
    /// <remarks>
    /// HR-gated, and the mirror image of <see cref="File"/> above: this one takes an explicit
    /// employee id because the desk opens the case ABOUT somebody. The service refuses
    /// <c>CaseType.Grievance</c> here, so this cannot become a raise-on-behalf-of.
    /// </remarks>
    [Authorize(Policy = HrPermissions.DisciplineWritePolicy)]
    [HttpPost("cases")]
    public async Task<ActionResult<StaffGrievanceDto>> OpenCase([FromBody] OpenEmployeeRelationsCaseDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (_currentUser.EmployeeId is not Guid employeeId)
            return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        var created = await _service.OpenCaseAsync(dto, employeeId);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>
    /// Adds somebody to a case — respondent, representative, union official, witness, mediator.
    /// </summary>
    /// <remarks>
    /// ⚠ Adding a party does NOT give them the right to read the case. The read rule stays what
    /// area 9 slice 7 set: the primary party, HR, or somebody named on a STEP. A respondent must
    /// not receive the complainant's statement by being recorded as a respondent.
    /// </remarks>
    [Authorize(Policy = HrPermissions.DisciplineWritePolicy)]
    [HttpPost("{id:guid}/parties")]
    public async Task<ActionResult<StaffGrievanceDto>> AddParty(Guid id, [FromBody] AddGrievancePartyDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (_currentUser.EmployeeId is not Guid employeeId)
            return BadRequest("Your user account is not linked to an employee record.");

        return Ok(await _service.AddPartyAsync(id, dto, employeeId));
    }

    /// <summary>Stands a party down. Not a delete — the case file must still read correctly.</summary>
    [Authorize(Policy = HrPermissions.DisciplineWritePolicy)]
    [HttpPost("{id:guid}/parties/{partyId:guid}/remove")]
    public async Task<ActionResult<StaffGrievanceDto>> RemoveParty(
        Guid id, Guid partyId, [FromBody] RemoveGrievancePartyDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Ok(await _service.RemovePartyAsync(id, partyId, dto));
    }
}
