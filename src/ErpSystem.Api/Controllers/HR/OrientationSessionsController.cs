using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/orientation-sessions")]
[Authorize]
public class OrientationSessionsController : ControllerBase
{
    private readonly IOrientationSessionService _service;
    private readonly ICurrentUserService _currentUser;

    public OrientationSessionsController(IOrientationSessionService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    // =========================================================================
    // QUERIES
    // =========================================================================

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrientationSessionDto>> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id));

    [HttpGet("code/{sessionCode}")]
    public async Task<ActionResult<OrientationSessionDto?>> GetByCode(string sessionCode)
        => Ok(await _service.GetBySessionCodeAsync(sessionCode));

    [HttpGet("program/{programId:guid}")]
    public async Task<ActionResult<IEnumerable<OrientationSessionSummaryDto>>> GetByProgram(Guid programId)
        => Ok(await _service.GetByProgramIdAsync(programId));

    [HttpGet("status/{status}")]
    public async Task<ActionResult<IEnumerable<OrientationSessionSummaryDto>>> GetByStatus(OrientationSessionStatus status)
        => Ok(await _service.GetByStatusAsync(status));

    [HttpGet("upcoming")]
    public async Task<ActionResult<IEnumerable<OrientationSessionSummaryDto>>> GetUpcoming([FromQuery] int daysAhead = 30)
        => Ok(await _service.GetUpcomingAsync(daysAhead));

    [HttpGet("open-for-enrollment")]
    public async Task<ActionResult<IEnumerable<OrientationSessionSummaryDto>>> GetOpenForEnrollment()
        => Ok(await _service.GetOpenForEnrollmentAsync());

    // =========================================================================
    // CRUD + LIFECYCLE
    // =========================================================================

    [HttpPost]
    public async Task<ActionResult<OrientationSessionDto>> Create([FromBody] CreateOrientationSessionDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext(out var bad);
        if (bad != null) return bad;

        var created = await _service.CreateAsync(dto, ctx.TenantId, ctx.UserId);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<OrientationSessionDto>> Update(Guid id, [FromBody] UpdateOrientationSessionDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (_currentUser.EmployeeId is not { } userId) return BadRequest("Your user account is not linked to an employee record.");

        return Ok(await _service.UpdateAsync(dto, userId));
    }

    [HttpPost("{id:guid}/status")]
    public async Task<IActionResult> ChangeStatus(Guid id, [FromBody] ChangeOrientationSessionStatusDto dto)
    {
        if (id != dto.SessionId) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (_currentUser.EmployeeId is not { } userId) return BadRequest("Your user account is not linked to an employee record.");

        await _service.ChangeStatusAsync(dto, userId);
        return Ok(new { message = $"Session status changed to {dto.NewStatus}." });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }

    // =========================================================================
    // FACILITATORS
    // =========================================================================

    [HttpGet("{id:guid}/facilitators")]
    public async Task<ActionResult<IEnumerable<OrientationSessionFacilitatorDto>>> GetFacilitators(Guid id)
        => Ok(await _service.GetFacilitatorsAsync(id));

    [HttpPost("{id:guid}/facilitators")]
    public async Task<ActionResult<OrientationSessionFacilitatorDto>> AddFacilitator(Guid id, [FromBody] CreateOrientationSessionFacilitatorDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext(out var bad);
        if (bad != null) return bad;

        dto.SessionId = id;
        return Ok(await _service.AddFacilitatorAsync(dto, ctx.TenantId, ctx.UserId));
    }

    [HttpPut("facilitators/{facilitatorId:guid}")]
    public async Task<ActionResult<OrientationSessionFacilitatorDto>> UpdateFacilitator(Guid facilitatorId, [FromBody] UpdateOrientationSessionFacilitatorDto dto)
    {
        if (facilitatorId != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (_currentUser.EmployeeId is not { } userId) return BadRequest("Your user account is not linked to an employee record.");

        return Ok(await _service.UpdateFacilitatorAsync(dto, userId));
    }

    [HttpDelete("facilitators/{facilitatorId:guid}")]
    public async Task<IActionResult> RemoveFacilitator(Guid facilitatorId)
    {
        await _service.RemoveFacilitatorAsync(facilitatorId);
        return NoContent();
    }

    // =========================================================================
    // ATTENDANCE
    // =========================================================================

    [HttpGet("{id:guid}/attendance")]
    public async Task<ActionResult<IEnumerable<OrientationAttendanceRecordDto>>> GetAttendanceForSession(Guid id)
        => Ok(await _service.GetAttendanceForSessionAsync(id));

    [HttpGet("enrollments/{enrollmentId:guid}/attendance")]
    public async Task<ActionResult<IEnumerable<OrientationAttendanceRecordDto>>> GetAttendanceForEnrollment(Guid enrollmentId)
        => Ok(await _service.GetAttendanceForEnrollmentAsync(enrollmentId));

    [HttpPost("{id:guid}/attendance")]
    public async Task<ActionResult<IEnumerable<OrientationAttendanceRecordDto>>> MarkAttendance(Guid id, [FromBody] MarkOrientationAttendanceDto dto)
    {
        if (id != dto.SessionId) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (_currentUser.EmployeeId is not { } userId) return BadRequest("Your user account is not linked to an employee record.");

        return Ok(await _service.MarkAttendanceAsync(dto, userId));
    }

    // =========================================================================
    // HELPERS
    // =========================================================================

    private (Guid TenantId, Guid UserId) ResolveContext(out ActionResult? error)
    {
        error = null;
        if (_currentUser.TenantId is not { } tenantId)
        {
            error = BadRequest("Tenant context could not be resolved.");
            return default;
        }
        if (_currentUser.EmployeeId is not { } userId)
        {
            error = BadRequest("Your user account is not linked to an employee record.");
            return default;
        }
        return (tenantId, userId);
    }
}
