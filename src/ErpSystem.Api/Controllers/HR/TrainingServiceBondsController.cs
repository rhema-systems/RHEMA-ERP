using ErpSystem.Api.Filters;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/training-service-bonds")]
[Authorize(Policy = "InternalOnly")]
[TrainingBusinessRulesAttribute]
public class TrainingServiceBondsController : ControllerBase
{
    private readonly ITrainingServiceBondService _service;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuthorizationService _authorization;

    public TrainingServiceBondsController(
        ITrainingServiceBondService service,
        ICurrentUserService currentUser,
        IAuthorizationService authorization)
    {
        _service = service;
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

    [HttpGet]
    [Authorize(Policy = HrPermissions.TrainingReadPolicy)]
    public async Task<ActionResult<IEnumerable<TrainingServiceBondDto>>> GetAll([FromQuery] TrainingBondStatus? status, CancellationToken ct)
        => Ok(await _service.GetAllAsync(status, ct));

    [HttpGet("active")]
    [Authorize(Policy = HrPermissions.TrainingReadPolicy)]
    public async Task<ActionResult<IEnumerable<TrainingServiceBondDto>>> GetActive(CancellationToken ct)
        => Ok(await _service.GetActiveAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TrainingServiceBondDto>> GetById(Guid id, CancellationToken ct)
    {
        // W3: the bond's owner reads their own (the accept flow needs it); others need the desk read.
        var bond = await _service.GetByIdAsync(id, ct);
        if (!await SelfOrPolicyAsync(bond.EmployeeId, HrPermissions.TrainingReadPolicy))
            return Forbid();
        return Ok(bond);
    }

    [HttpGet("by-nomination/{nominationId:guid}")]
    [Authorize(Policy = HrPermissions.TrainingReadPolicy)]
    public async Task<ActionResult<TrainingServiceBondDto>> GetByNomination(Guid nominationId, CancellationToken ct)
    {
        var bond = await _service.GetByNominationAsync(nominationId, ct);
        return bond == null ? NotFound() : Ok(bond);
    }

    [HttpGet("by-employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<TrainingServiceBondDto>>> GetByEmployee(Guid employeeId, CancellationToken ct)
    {
        if (!await SelfOrPolicyAsync(employeeId, HrPermissions.TrainingReadPolicy))
            return Forbid();
        return Ok(await _service.GetByEmployeeAsync(employeeId, ct));
    }

    /// <summary>The signed-in employee's own bonds (self-service).</summary>
    [HttpGet("mine")]
    public async Task<ActionResult<IEnumerable<TrainingServiceBondDto>>> GetMine(CancellationToken ct)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return Ok(Array.Empty<TrainingServiceBondDto>());
        return Ok(await _service.GetByEmployeeAsync(employeeId.Value, ct));
    }

    [HttpPost]
    [Authorize(Policy = HrPermissions.TrainingWritePolicy)]
    public async Task<ActionResult<TrainingServiceBondDto>> Create([FromBody] CreateTrainingServiceBondDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;
        if (tenantId == null) return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        var created = await _service.CreateAsync(dto, tenantId.Value, employeeId.Value, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = HrPermissions.TrainingWritePolicy)]
    public async Task<ActionResult<TrainingServiceBondDto>> Update(Guid id, [FromBody] UpdateTrainingServiceBondDto dto, CancellationToken ct)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");

        return Ok(await _service.UpdateAsync(id, dto, employeeId.Value, ct));
    }

    /// <summary>Employee accepts their own bond terms (self-service).</summary>
    [HttpPost("accept")]
    public async Task<ActionResult<TrainingServiceBondDto>> Accept([FromBody] AcceptTrainingServiceBondDto dto, CancellationToken ct)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");
        return Ok(await _service.AcceptAsync(dto, employeeId.Value, onBehalf: false, employeeId.Value, ct));
    }

    /// <summary>HR records acceptance on the employee's behalf (e.g. signed offline).</summary>
    [HttpPost("accept-on-behalf")]
    [Authorize(Policy = HrPermissions.TrainingWritePolicy)]
    public async Task<ActionResult<TrainingServiceBondDto>> AcceptOnBehalf([FromBody] AcceptTrainingServiceBondDto dto, CancellationToken ct)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");
        return Ok(await _service.AcceptAsync(dto, employeeId.Value, onBehalf: true, employeeId.Value, ct));
    }

    [HttpPost("record-exit")]
    [Authorize(Policy = HrPermissions.TrainingWritePolicy)]
    public async Task<ActionResult<TrainingServiceBondDto>> RecordExit([FromBody] RecordBondExitDto dto, CancellationToken ct)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");
        return Ok(await _service.RecordExitAsync(dto, employeeId.Value, ct));
    }

    // W3: waiving forgives money owed — an Admin act, deliberately ABOVE the HR desk's Write
    // (the compensation-slice precedent: switching off a financial obligation is administration).
    [HttpPost("waive")]
    [Authorize(Policy = HrPermissions.TrainingAdminPolicy)]
    public async Task<ActionResult<TrainingServiceBondDto>> Waive([FromBody] WaiveTrainingServiceBondDto dto, CancellationToken ct)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");
        return Ok(await _service.WaiveAsync(dto, employeeId.Value, ct));
    }

    [HttpPost("settle")]
    [Authorize(Policy = HrPermissions.TrainingWritePolicy)]
    public async Task<ActionResult<TrainingServiceBondDto>> Settle([FromBody] SettleTrainingServiceBondDto dto, CancellationToken ct)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record.");
        return Ok(await _service.SettleAsync(dto, employeeId.Value, ct));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = HrPermissions.TrainingAdminPolicy)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }
}
