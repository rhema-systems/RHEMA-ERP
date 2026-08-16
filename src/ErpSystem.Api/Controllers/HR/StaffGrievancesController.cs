using ErpSystem.Api.Filters;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Employee grievances and FR-HR-181's escalation ladder.
///
/// Gated per action, and the split is the opposite way round from the disciplinary case. A case is
/// raised ABOUT an employee, so it is HR's with a narrow window for the subject; a grievance is
/// raised BY one, so filing, escalating and withdrawing are the employee's and HR cannot do them at
/// all. What HR owns is answering, assigning, and seeing the whole register.
/// </summary>
[ApiController]
[Route("api/grievances")]
[Authorize]
[DisciplineBusinessRules]
public class StaffGrievancesController : ControllerBase
{
    private const string HrRoles = Constants.Roles.SuperAdmin + "," + Constants.Roles.Hr;

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

    [Authorize(Roles = HrRoles)]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<StaffGrievanceSummaryDto>>> GetAll()
        => Ok(await _service.GetAllAsync());

    [Authorize(Roles = HrRoles)]
    [HttpGet("status/{status}")]
    public async Task<ActionResult<IEnumerable<StaffGrievanceSummaryDto>>> GetByStatus(GrievanceStatus status)
        => Ok(await _service.GetByStatusAsync(status));

    /// <summary>Where the ladder is stuck — grievances whose current rung has not answered.</summary>
    [Authorize(Roles = HrRoles)]
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
    [Authorize(Roles = HrRoles)]
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
}
