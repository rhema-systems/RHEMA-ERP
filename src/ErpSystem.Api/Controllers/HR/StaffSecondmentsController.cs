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
[Route("api/staff-secondments")]
[Authorize(Roles = StaffSecondmentsController.HrRoles)]
[MovementBusinessRules]
public class StaffSecondmentsController : ControllerBase
{
    internal const string HrRoles = Constants.Roles.SuperAdmin + "," + Constants.Roles.Hr;

    private readonly IStaffSecondmentService _service;
    private readonly ICurrentUserService _currentUser;

    public StaffSecondmentsController(IStaffSecondmentService service, ICurrentUserService currentUser)
    {
        _service     = service;
        _currentUser = currentUser;
    }

    // =========================================================================
    // QUERIES
    // =========================================================================

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<StaffSecondmentDto>> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id));

    [HttpGet("movement/{movementId:guid}")]
    public async Task<ActionResult<StaffSecondmentDto?>> GetByMovement(Guid movementId)
        => Ok(await _service.GetByMovementIdAsync(movementId));

    [HttpGet("type/{type}")]
    public async Task<ActionResult<IEnumerable<StaffSecondmentDto>>> GetByType(StaffSecondmentType type)
        => Ok(await _service.GetByTypeAsync(type));

    [HttpGet("external")]
    public async Task<ActionResult<IEnumerable<StaffSecondmentDto>>> GetExternal()
        => Ok(await _service.GetExternalSecondmentsAsync());

    [HttpGet("host-organization")]
    public async Task<ActionResult<IEnumerable<StaffSecondmentDto>>> GetByHostOrganization(
        [FromQuery] string hostOrganization)
        => Ok(await _service.GetByHostOrganizationAsync(hostOrganization));

    [HttpGet("ending-soon")]
    public async Task<ActionResult<IEnumerable<StaffSecondmentDto>>> GetEndingSoon(
        [FromQuery] int daysAhead = 30)
        => Ok(await _service.GetEndingSoonAsync(daysAhead));

    // =========================================================================
    // CRUD
    // =========================================================================

    [HttpPost]
    public async Task<ActionResult<StaffSecondmentDto>> Create([FromBody] CreateStaffSecondmentDto dto)
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
    public async Task<ActionResult<StaffSecondmentDto>> Update(Guid id, [FromBody] UpdateStaffSecondmentDto dto)
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

    /// <summary>Extends the secondment end date by a specified number of months.</summary>
    [HttpPost("{id:guid}/extend")]
    public async Task<ActionResult<StaffSecondmentDto>> Extend(Guid id, [FromBody] ExtendStaffSecondmentDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        dto.SecondmentId = id;
        return Ok(await _service.ExtendAsync(dto, employeeId.Value));
    }
}
