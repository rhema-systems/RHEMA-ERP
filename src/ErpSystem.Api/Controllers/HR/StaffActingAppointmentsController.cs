using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/staff-acting-appointments")]
[Authorize]
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
    public async Task<ActionResult<PagedResult<StaffActingAppointmentSummaryDto>>> GetPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20)
        => Ok(await _service.GetPagedAsync(pageNumber, pageSize));

    [HttpGet("all")]
    public async Task<ActionResult<IEnumerable<StaffActingAppointmentSummaryDto>>> GetAll()
        => Ok(await _service.GetAllAsync());

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<StaffActingAppointmentDto>> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id));

    [HttpGet("{id:guid}/details")]
    public async Task<ActionResult<StaffActingAppointmentDto>> GetWithDetails(Guid id)
        => Ok(await _service.GetWithDetailsAsync(id));

    [HttpGet("number/{appointmentNumber}")]
    public async Task<ActionResult<StaffActingAppointmentDto?>> GetByAppointmentNumber(string appointmentNumber)
        => Ok(await _service.GetByAppointmentNumberAsync(appointmentNumber));

    [HttpGet("employee/{employeeId:guid}")]
    public async Task<ActionResult<IEnumerable<StaffActingAppointmentSummaryDto>>> GetByEmployee(Guid employeeId)
        => Ok(await _service.GetByEmployeeIdAsync(employeeId));

    [HttpGet("status/{status}")]
    public async Task<ActionResult<IEnumerable<StaffActingAppointmentSummaryDto>>> GetByStatus(StaffActingStatus status)
        => Ok(await _service.GetByStatusAsync(status));

    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<StaffActingAppointmentSummaryDto>>> GetActive()
        => Ok(await _service.GetActiveAppointmentsAsync());

    [HttpGet("position/{positionId:guid}")]
    public async Task<ActionResult<IEnumerable<StaffActingAppointmentSummaryDto>>> GetByActingPosition(Guid positionId)
        => Ok(await _service.GetByActingPositionAsync(positionId));

    [HttpGet("expiring")]
    public async Task<ActionResult<IEnumerable<StaffActingAppointmentSummaryDto>>> GetExpiring(
        [FromQuery] int daysAhead = 14)
        => Ok(await _service.GetExpiringAppointmentsAsync(daysAhead));

    [HttpGet("converted-to-permanent")]
    public async Task<ActionResult<IEnumerable<StaffActingAppointmentSummaryDto>>> GetConvertedToPermanent()
        => Ok(await _service.GetConvertedToPermanentAsync());

    // =========================================================================
    // CRUD
    // =========================================================================

    [HttpPost]
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
    public async Task<IActionResult> Complete(Guid id, [FromBody] CompleteStaffActingAppointmentDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        dto.AppointmentId = id;
        await _service.CompleteAsync(dto, employeeId.Value);
        return Ok(new { message = "Acting appointment completed." });
    }

    /// <summary>Extends the acting appointment to a new end date.</summary>
    [HttpPost("{id:guid}/extend")]
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
