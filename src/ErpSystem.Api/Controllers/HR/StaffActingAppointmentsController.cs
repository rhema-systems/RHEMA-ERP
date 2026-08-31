using ErpSystem.Core.DTOs.Common;
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
[Route("api/staff-acting-appointments")]
[Authorize(Policy = "InternalOnly")]
[MovementBusinessRules]
public class StaffActingAppointmentsController : ControllerBase
{
    private readonly IStaffActingAppointmentService _service;
    private readonly ICurrentUserService _currentUser;

    public StaffActingAppointmentsController(
        IStaffActingAppointmentService service, ICurrentUserService currentUser)
    {
        _service     = service;
        _currentUser = currentUser;
    }

    // =========================================================================
    // QUERIES
    // =========================================================================

    [HttpGet]
    [Authorize(Policy = HrPermissions.MovementsReadPolicy)]
    public async Task<ActionResult<PagedResult<StaffActingAppointmentSummaryDto>>> GetPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20)
        => Ok(await _service.GetPagedAsync(pageNumber, pageSize));

    [HttpGet("all")]
    [Authorize(Policy = HrPermissions.MovementsReadPolicy)]
    public async Task<ActionResult<IEnumerable<StaffActingAppointmentSummaryDto>>> GetAll()
        => Ok(await _service.GetAllAsync());

    [HttpGet("{id:guid}")]
    [Authorize(Policy = HrPermissions.MovementsReadPolicy)]
    public async Task<ActionResult<StaffActingAppointmentDto>> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id));

    [HttpGet("{id:guid}/details")]
    [Authorize(Policy = HrPermissions.MovementsReadPolicy)]
    public async Task<ActionResult<StaffActingAppointmentDto>> GetWithDetails(Guid id)
        => Ok(await _service.GetWithDetailsAsync(id));

    [HttpGet("number/{appointmentNumber}")]
    [Authorize(Policy = HrPermissions.MovementsReadPolicy)]
    public async Task<ActionResult<StaffActingAppointmentDto?>> GetByAppointmentNumber(string appointmentNumber)
        => Ok(await _service.GetByAppointmentNumberAsync(appointmentNumber));

    [HttpGet("employee/{employeeId:guid}")]
    [Authorize(Policy = HrPermissions.MovementsReadPolicy)]
    public async Task<ActionResult<IEnumerable<StaffActingAppointmentSummaryDto>>> GetByEmployee(Guid employeeId)
        => Ok(await _service.GetByEmployeeIdAsync(employeeId));

    [HttpGet("status/{status}")]
    [Authorize(Policy = HrPermissions.MovementsReadPolicy)]
    public async Task<ActionResult<IEnumerable<StaffActingAppointmentSummaryDto>>> GetByStatus(StaffActingStatus status)
        => Ok(await _service.GetByStatusAsync(status));

    [HttpGet("active")]
    [Authorize(Policy = HrPermissions.MovementsReadPolicy)]
    public async Task<ActionResult<IEnumerable<StaffActingAppointmentSummaryDto>>> GetActive()
        => Ok(await _service.GetActiveAppointmentsAsync());

    [HttpGet("position/{positionId:guid}")]
    [Authorize(Policy = HrPermissions.MovementsReadPolicy)]
    public async Task<ActionResult<IEnumerable<StaffActingAppointmentSummaryDto>>> GetByActingPosition(Guid positionId)
        => Ok(await _service.GetByActingPositionAsync(positionId));

    [HttpGet("expiring")]
    [Authorize(Policy = HrPermissions.MovementsReadPolicy)]
    public async Task<ActionResult<IEnumerable<StaffActingAppointmentSummaryDto>>> GetExpiring(
        [FromQuery] int daysAhead = 14)
        => Ok(await _service.GetExpiringAppointmentsAsync(daysAhead));

    [HttpGet("converted-to-permanent")]
    [Authorize(Policy = HrPermissions.MovementsReadPolicy)]
    public async Task<ActionResult<IEnumerable<StaffActingAppointmentSummaryDto>>> GetConvertedToPermanent()
        => Ok(await _service.GetConvertedToPermanentAsync());

    // =========================================================================
    // CRUD
    // =========================================================================

    [HttpPost]
    [Authorize(Policy = HrPermissions.MovementsWritePolicy)]
    public async Task<ActionResult<StaffActingAppointmentDto>> Create([FromBody] CreateStaffActingAppointmentDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId   = _currentUser.TenantId;
        var employeeId = _currentUser.EmployeeId;

        if (tenantId == null)   return BadRequest("Tenant context could not be resolved.");
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        var created = await _service.CreateAsync(dto, tenantId.Value, employeeId.Value);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = HrPermissions.MovementsWritePolicy)]
    public async Task<ActionResult<StaffActingAppointmentDto>> Update(
        Guid id, [FromBody] UpdateStaffActingAppointmentDto dto)
    {
        if (id != dto.Id)        return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.UpdateAsync(dto, employeeId.Value));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = HrPermissions.MovementsAdminPolicy)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }

    // =========================================================================
    // WORKFLOW
    // =========================================================================

    /// <summary>Marks the acting appointment as completed.</summary>
    [HttpPost("{id:guid}/complete")]
    [Authorize(Policy = HrPermissions.MovementsWritePolicy)]
    public async Task<IActionResult> Complete(Guid id, [FromBody] CompleteStaffActingAppointmentDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        dto.AppointmentId = id;
        await _service.CompleteAsync(dto, employeeId.Value);
        return Ok(new { message = "Acting appointment completed." });
    }

    /// <summary>Ends the acting appointment before its end date.</summary>
    /// <remarks>
    /// ⚠ The only route to <c>TerminatedEarly</c>. It used to be reachable only by assigning Status
    /// on the plain edit — which also reached Completed, left CompletionDate null, and locked the
    /// record out of both UpdateAsync and CompleteAsync. Lane 3 stopped the edit assigning Status, so
    /// this keeps the state reachable through a door that sets the completion date with it and
    /// records why.
    /// </remarks>
    [HttpPost("{id:guid}/terminate-early")]
    [Authorize(Policy = HrPermissions.MovementsWritePolicy)]
    public async Task<ActionResult<StaffActingAppointmentDto>> TerminateEarly(
        Guid id, [FromBody] TerminateStaffActingAppointmentEarlyDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        dto.AppointmentId = id;
        return Ok(await _service.TerminateEarlyAsync(dto, employeeId.Value));
    }

    /// <summary>Extends the acting appointment to a new end date.</summary>
    [HttpPost("{id:guid}/extend")]
    [Authorize(Policy = HrPermissions.MovementsWritePolicy)]
    public async Task<ActionResult<StaffActingAppointmentDto>> Extend(Guid id, [FromBody] ExtendStaffActingAppointmentDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        dto.AppointmentId = id;
        return Ok(await _service.ExtendAsync(dto, employeeId.Value));
    }

    /// <summary>Converts a completed acting appointment into a permanent promotion.</summary>
    [HttpPost("{id:guid}/convert")]
    [Authorize(Policy = HrPermissions.MovementsWritePolicy)]
    public async Task<IActionResult> ConvertToPermanent(Guid id, [FromBody] ConvertActingToPermanentDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        dto.AppointmentId = id;
        await _service.ConvertToPermanentAsync(dto, employeeId.Value);
        return Ok(new { message = "Acting appointment converted to permanent promotion." });
    }
}
