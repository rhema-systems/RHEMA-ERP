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
/// Session scheduling and the attendance register — HR only, with one exception: the by-id read stays
/// open so an enrolled participant can see when and where their session is and follow the joining
/// link. A session's time and venue are not sensitive; who attended it is, which is why the register
/// and the enrollment lists are gated.
/// </summary>
[ApiController]
[OrientationBusinessRules]
[Route("api/orientation-sessions")]
[Authorize(Policy = "InternalOnly")]
public class OrientationSessionsController : ControllerBase
{
    // Gated per action rather than on the class: authorize attributes stack as AND, so a class-level
    // role requirement cannot be relaxed for the one read participants need.
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

    /// <summary>Open to any authenticated user — participants need their session's time, venue and joining link.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = "InternalOnly")]
    public async Task<ActionResult<OrientationSessionDto>> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id));

    [Authorize(Policy = HrPermissions.OrientationReadPolicy)]
    [HttpGet("code/{sessionCode}")]
    public async Task<ActionResult<OrientationSessionDto?>> GetByCode(string sessionCode)
        => Ok(await _service.GetBySessionCodeAsync(sessionCode));

    [Authorize(Policy = HrPermissions.OrientationReadPolicy)]
    [HttpGet("program/{programId:guid}")]
    public async Task<ActionResult<IEnumerable<OrientationSessionSummaryDto>>> GetByProgram(Guid programId)
        => Ok(await _service.GetByProgramIdAsync(programId));

    [Authorize(Policy = HrPermissions.OrientationReadPolicy)]
    [HttpGet("status/{status}")]
    public async Task<ActionResult<IEnumerable<OrientationSessionSummaryDto>>> GetByStatus(OrientationSessionStatus status)
        => Ok(await _service.GetByStatusAsync(status));

    [Authorize(Policy = HrPermissions.OrientationReadPolicy)]
    [HttpGet("upcoming")]
    public async Task<ActionResult<IEnumerable<OrientationSessionSummaryDto>>> GetUpcoming([FromQuery] int daysAhead = 30)
        => Ok(await _service.GetUpcomingAsync(daysAhead));

    [Authorize(Policy = HrPermissions.OrientationReadPolicy)]
    [HttpGet("open-for-enrollment")]
    public async Task<ActionResult<IEnumerable<OrientationSessionSummaryDto>>> GetOpenForEnrollment()
        => Ok(await _service.GetOpenForEnrollmentAsync());

    // =========================================================================
    // CRUD + LIFECYCLE
    // =========================================================================

    [Authorize(Policy = HrPermissions.OrientationWritePolicy)]
    [HttpPost]
    public async Task<ActionResult<OrientationSessionDto>> Create([FromBody] CreateOrientationSessionDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext(out var bad);
        if (bad != null) return bad;

        var created = await _service.CreateAsync(dto, ctx.TenantId, ctx.UserId);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>
    /// Round 4, lane J3: run a session again on a new date — same programme, venue, capacity and
    /// facilitators (who must confirm afresh), as a new draft with nobody enrolled.
    /// </summary>
    [Authorize(Policy = HrPermissions.OrientationWritePolicy)]
    [HttpPost("{id:guid}/clone")]
    public async Task<ActionResult<OrientationSessionDto>> Clone(Guid id, [FromBody] CloneOrientationSessionDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext(out var bad);
        if (bad != null) return bad;

        var copy = await _service.CloneAsync(id, dto, ctx.TenantId, ctx.UserId);
        return CreatedAtAction(nameof(GetById), new { id = copy.Id }, copy);
    }

    [Authorize(Policy = HrPermissions.OrientationWritePolicy)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<OrientationSessionDto>> Update(Guid id, [FromBody] UpdateOrientationSessionDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (_currentUser.EmployeeId is not { } userId) return BadRequest("Your user account is not linked to an employee record.");

        return Ok(await _service.UpdateAsync(dto, userId));
    }

    [Authorize(Policy = HrPermissions.OrientationWritePolicy)]
    [HttpPost("{id:guid}/status")]
    public async Task<IActionResult> ChangeStatus(Guid id, [FromBody] ChangeOrientationSessionStatusDto dto)
    {
        if (id != dto.SessionId) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (_currentUser.EmployeeId is not { } userId) return BadRequest("Your user account is not linked to an employee record.");

        await _service.ChangeStatusAsync(dto, userId);
        return Ok(new { message = $"Session status changed to {dto.NewStatus}." });
    }

    [Authorize(Policy = HrPermissions.OrientationAdminPolicy)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }

    // =========================================================================
    // FACILITATORS
    // =========================================================================

    [Authorize(Policy = HrPermissions.OrientationReadPolicy)]
    [HttpGet("{id:guid}/facilitators")]
    public async Task<ActionResult<IEnumerable<OrientationSessionFacilitatorDto>>> GetFacilitators(Guid id)
        => Ok(await _service.GetFacilitatorsAsync(id));

    [Authorize(Policy = HrPermissions.OrientationWritePolicy)]
    [HttpPost("{id:guid}/facilitators")]
    public async Task<ActionResult<OrientationSessionFacilitatorDto>> AddFacilitator(Guid id, [FromBody] CreateOrientationSessionFacilitatorDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext(out var bad);
        if (bad != null) return bad;

        dto.SessionId = id;
        return Ok(await _service.AddFacilitatorAsync(dto, ctx.TenantId, ctx.UserId));
    }

    [Authorize(Policy = HrPermissions.OrientationWritePolicy)]
    [HttpPut("facilitators/{facilitatorId:guid}")]
    public async Task<ActionResult<OrientationSessionFacilitatorDto>> UpdateFacilitator(Guid facilitatorId, [FromBody] UpdateOrientationSessionFacilitatorDto dto)
    {
        if (facilitatorId != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (_currentUser.EmployeeId is not { } userId) return BadRequest("Your user account is not linked to an employee record.");

        return Ok(await _service.UpdateFacilitatorAsync(dto, userId));
    }

    [Authorize(Policy = HrPermissions.OrientationAdminPolicy)]
    [HttpDelete("facilitators/{facilitatorId:guid}")]
    public async Task<IActionResult> RemoveFacilitator(Guid facilitatorId)
    {
        await _service.RemoveFacilitatorAsync(facilitatorId);
        return NoContent();
    }

    // =========================================================================
    // ATTENDANCE
    // =========================================================================

    [Authorize(Policy = HrPermissions.OrientationReadPolicy)]
    [HttpGet("{id:guid}/attendance")]
    public async Task<ActionResult<IEnumerable<OrientationAttendanceRecordDto>>> GetAttendanceForSession(Guid id)
        => Ok(await _service.GetAttendanceForSessionAsync(id));

    [Authorize(Policy = HrPermissions.OrientationReadPolicy)]
    [HttpGet("enrollments/{enrollmentId:guid}/attendance")]
    public async Task<ActionResult<IEnumerable<OrientationAttendanceRecordDto>>> GetAttendanceForEnrollment(Guid enrollmentId)
        => Ok(await _service.GetAttendanceForEnrollmentAsync(enrollmentId));

    [Authorize(Policy = HrPermissions.OrientationWritePolicy)]
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
