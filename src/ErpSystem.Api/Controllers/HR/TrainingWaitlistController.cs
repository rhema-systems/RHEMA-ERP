using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/training-waitlist")]
[Authorize]
public class TrainingWaitlistController : ControllerBase
{
    private readonly ITrainingWaitlistService _service;
    private readonly ICurrentUserService _currentUser;

    public TrainingWaitlistController(ITrainingWaitlistService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TrainingWaitlistDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await _service.GetByIdAsync(id, ct));

    [HttpGet("schedule/{scheduleId:guid}")]
    public async Task<ActionResult<IEnumerable<TrainingWaitlistDto>>> GetByScheduleId(Guid scheduleId, CancellationToken ct)
        => Ok(await _service.GetByScheduleIdAsync(scheduleId, ct));

    [HttpGet("employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<TrainingWaitlistDto>>> GetByEmployeeId(Guid employeeId, CancellationToken ct)
        => Ok(await _service.GetByEmployeeIdAsync(employeeId, ct));

    [HttpGet("schedule/{scheduleId:guid}/active")]
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

        var created = await _service.AddToWaitlistAsync(dto, tenantId.Value, employeeId.Value, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> RemoveFromWaitlist(Guid id, CancellationToken ct)
    {
        await _service.RemoveFromWaitlistAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/offer-slot")]
    public async Task<IActionResult> OfferSlot(Guid id, [FromBody] OfferWaitlistPositionDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");
        dto.WaitlistId = id;
        await _service.OfferSlotAsync(dto, employeeId.Value, ct);
        return Ok(new { message = "Slot offered to waitlisted candidate." });
    }

    [HttpPost("{id:guid}/respond")]
    public async Task<IActionResult> RecordOfferResponse(Guid id, [FromBody] RespondToWaitlistOfferDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");
        dto.WaitlistId = id;
        await _service.RecordOfferResponseAsync(dto, employeeId.Value, ct);
        return Ok(new { message = "Waitlist offer response recorded." });
    }
}
