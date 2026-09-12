using ErpSystem.Api.Filters;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ErpSystem.Shared;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/training-waitlist")]
[Authorize(Policy = "InternalOnly")]
[TrainingBusinessRulesAttribute]
public class TrainingWaitlistController : ControllerBase
{
    private readonly ITrainingWaitlistService _service;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuthorizationService _authorization;

    public TrainingWaitlistController(
        ITrainingWaitlistService service,
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

    [HttpGet("{id:guid}")]
    [Authorize(Policy = HrPermissions.TrainingReadPolicy)]
    public async Task<ActionResult<TrainingWaitlistDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await _service.GetByIdAsync(id, ct));

    [HttpGet("schedule/{scheduleId:guid}")]
    [Authorize(Policy = HrPermissions.TrainingReadPolicy)]
    public async Task<ActionResult<IEnumerable<TrainingWaitlistDto>>> GetByScheduleId(Guid scheduleId, CancellationToken ct)
        => Ok(await _service.GetByScheduleIdAsync(scheduleId, ct));

    [HttpGet("employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<TrainingWaitlistDto>>> GetByEmployeeId(Guid employeeId, CancellationToken ct)
    {
        if (!await SelfOrPolicyAsync(employeeId, HrPermissions.TrainingReadPolicy))
            return Forbid();
        return Ok(await _service.GetByEmployeeIdAsync(employeeId, ct));
    }

    [HttpGet("schedule/{scheduleId:guid}/active")]
    [Authorize(Policy = HrPermissions.TrainingReadPolicy)]
    public async Task<ActionResult<IEnumerable<TrainingWaitlistDto>>> GetActiveWaitlist(Guid scheduleId, CancellationToken ct)
        => Ok(await _service.GetActiveWaitlistAsync(scheduleId, ct));

    [HttpPost]
    public async Task<ActionResult<TrainingWaitlistDto>> AddToWaitlist([FromBody] CreateTrainingWaitlistDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null) return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        // W3: joining the waitlist is the employee's own act; adding someone else is the desk's.
        if (!await SelfOrPolicyAsync(dto.EmployeeId, HrPermissions.TrainingWritePolicy))
            return Forbid();

        var created = await _service.AddToWaitlistAsync(dto, tenantId.Value, employeeId.Value, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> RemoveFromWaitlist(Guid id, CancellationToken ct)
    {
        // W3: leaving the waitlist is withdrawal (a status change, not a hard delete) — the
        // entry's owner may do it, anyone else needs the desk permission.
        var entry = await _service.GetByIdAsync(id, ct);
        if (!await SelfOrPolicyAsync(entry.EmployeeId, HrPermissions.TrainingWritePolicy))
            return Forbid();

        await _service.RemoveFromWaitlistAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/offer-slot")]
    [Authorize(Policy = HrPermissions.TrainingWritePolicy)]
    public async Task<IActionResult> OfferSlot(Guid id, [FromBody] OfferWaitlistPositionDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");
        dto.WaitlistId = id;
        // Returns the updated record rather than a bare message: the service already builds the
        // full DTO (re-read through its includes chain) and discarding it forced every caller to
        // refetch just to render the row it had just changed.
        return Ok(await _service.OfferSlotAsync(dto, employeeId.Value, ct));
    }

    [HttpPost("{id:guid}/respond")]
    public async Task<IActionResult> RecordOfferResponse(Guid id, [FromBody] RespondToWaitlistOfferDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        // W3: accepting or declining an offer belongs to the person it was offered to —
        // previously any internal user could answer anyone's offer.
        var entry = await _service.GetByIdAsync(id, ct);
        if (!await SelfOrPolicyAsync(entry.EmployeeId, HrPermissions.TrainingWritePolicy))
            return Forbid();

        dto.WaitlistId = id;
        // Returns the updated record rather than a bare message: the service already builds the
        // full DTO (re-read through its includes chain) and discarding it forced every caller to
        // refetch just to render the row it had just changed.
        return Ok(await _service.RecordOfferResponseAsync(dto, employeeId.Value, ct));
    }

    /// <summary>
    /// Enrols an employee who accepted their waitlist offer, creating the nomination.
    /// </summary>
    /// <remarks>
    /// Separate from <c>respond</c> on purpose: acceptance is the employee's action, enrolment is HR's.
    /// Returns the updated entry (carrying createdNominationNumber) rather than a bare message, since
    /// the caller needs the nomination it produced.
    /// </remarks>
    [HttpPost("{id:guid}/promote")]
    [Authorize(Policy = HrPermissions.TrainingWritePolicy)]
    public async Task<ActionResult<TrainingWaitlistDto>> PromoteToNomination(Guid id, CancellationToken ct)
    {
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.PromoteToNominationAsync(id, employeeId.Value, ct));
    }
}
