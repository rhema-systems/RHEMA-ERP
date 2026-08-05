using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/training-schedules")]
[Authorize]
public class TrainingSchedulesController : ControllerBase
{
    private readonly ITrainingScheduleService _service;
    private readonly ICurrentUserService _currentUser;

    public TrainingSchedulesController(ITrainingScheduleService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    // =========================================================================
    // QUERIES
    // =========================================================================

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TrainingScheduleSummaryDto>>> GetAll(CancellationToken ct)
        => Ok(await _service.GetAllAsync(ct));

    [HttpGet("paged")]
    public async Task<ActionResult<Core.DTOs.Common.PagedResult<TrainingScheduleSummaryDto>>> GetPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => Ok(await _service.GetPagedAsync(pageNumber, pageSize, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TrainingScheduleDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await _service.GetByIdAsync(id, ct));

    [HttpGet("number/{scheduleNumber}")]
    public async Task<ActionResult<TrainingScheduleDto?>> GetByScheduleNumber(string scheduleNumber, CancellationToken ct)
        => Ok(await _service.GetByScheduleNumberAsync(scheduleNumber, ct));

    [HttpGet("program/{programId:guid}")]
    public async Task<ActionResult<IEnumerable<TrainingScheduleSummaryDto>>> GetByProgramId(Guid programId, CancellationToken ct)
        => Ok(await _service.GetByProgramIdAsync(programId, ct));

    [HttpGet("trainer/{trainerId:guid}")]
    public async Task<ActionResult<IEnumerable<TrainingScheduleSummaryDto>>> GetByTrainerId(Guid trainerId, CancellationToken ct)
        => Ok(await _service.GetByTrainerProfileIdAsync(trainerId, ct));

    [HttpGet("trainer/{trainerId:guid}/availability-check")]
    public async Task<ActionResult<TrainerAvailabilityCheckDto>> CheckTrainerAvailability(
        Guid trainerId,
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] Guid? excludeScheduleId,
        CancellationToken ct)
        => Ok(await _service.CheckTrainerAvailabilityAsync(trainerId, from, to, excludeScheduleId, ct));

    [HttpGet("status/{status}")]
    public async Task<ActionResult<IEnumerable<TrainingScheduleSummaryDto>>> GetByStatus(ScheduleStatus status, CancellationToken ct)
        => Ok(await _service.GetByStatusAsync(status, ct));

    [HttpGet("upcoming")]
    public async Task<ActionResult<IEnumerable<TrainingScheduleSummaryDto>>> GetUpcoming(
        [FromQuery] int daysAhead = 90,
        CancellationToken ct = default)
        => Ok(await _service.GetUpcomingAsync(daysAhead, ct));

    [HttpGet("open-for-registration")]
    public async Task<ActionResult<IEnumerable<TrainingScheduleSummaryDto>>> GetOpenForRegistration(CancellationToken ct)
        => Ok(await _service.GetOpenForRegistrationAsync(ct));

    // =========================================================================
    // CRUD
    // =========================================================================

    [HttpPost]
    public async Task<ActionResult<TrainingScheduleDto>> Create([FromBody] CreateTrainingScheduleDto dto, CancellationToken ct)
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
    public async Task<ActionResult<TrainingScheduleDto>> Update(Guid id, [FromBody] UpdateTrainingScheduleDto dto, CancellationToken ct)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.UpdateAsync(dto, employeeId.Value, ct));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }

    // =========================================================================
    // WORKFLOW
    // =========================================================================

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveTrainingScheduleDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        dto.ScheduleId = id;
        await _service.ApproveAsync(dto, employeeId.Value, ct);
        return Ok(new { message = "Training schedule approved." });
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelTrainingScheduleDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        dto.ScheduleId = id;
        await _service.CancelAsync(dto, ct);
        return Ok(new { message = "Training schedule cancelled." });
    }

    [HttpPost("{id:guid}/complete")]
    public async Task<IActionResult> Complete(Guid id, [FromBody] CompleteTrainingScheduleDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        dto.ScheduleId = id;
        await _service.CompleteAsync(dto, ct);
        return Ok(new { message = "Training schedule marked as completed." });
    }

    // =========================================================================
    // SESSION SUB-OPERATIONS
    // =========================================================================

    [HttpPost("{id:guid}/sessions")]
    public async Task<ActionResult<TrainingSessionDto>> AddSession(Guid id, [FromBody] CreateTrainingSessionDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null) return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        dto.ScheduleId = id;
        return Ok(await _service.AddSessionAsync(dto, tenantId.Value, employeeId.Value, ct));
    }

    [HttpGet("{id:guid}/sessions")]
    public async Task<ActionResult<IEnumerable<TrainingSessionDto>>> GetSessions(Guid id, CancellationToken ct)
        => Ok(await _service.GetSessionsAsync(id, ct));

    [HttpPut("sessions/{sessionId:guid}")]
    public async Task<ActionResult<TrainingSessionDto>> UpdateSession(Guid sessionId, [FromBody] UpdateTrainingSessionDto dto, CancellationToken ct)
    {
        if (sessionId != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.UpdateSessionAsync(dto, employeeId.Value, ct));
    }

    [HttpDelete("sessions/{sessionId:guid}")]
    public async Task<IActionResult> DeleteSession(Guid sessionId, CancellationToken ct)
    {
        await _service.DeleteSessionAsync(sessionId, ct);
        return NoContent();
    }
}
