using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/staff-transfers")]
[Authorize]
public class StaffTransfersController : ControllerBase
{
    private readonly IStaffTransferService _service;
    private readonly ICurrentUserService _currentUser;

    public StaffTransfersController(IStaffTransferService service, ICurrentUserService currentUser)
    {
        _service     = service;
        _currentUser = currentUser;
    }

    // =========================================================================
    // QUERIES
    // =========================================================================

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<StaffTransferDto>> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id));

    [HttpGet("movement/{movementId:guid}")]
    public async Task<ActionResult<StaffTransferDto?>> GetByMovement(Guid movementId)
        => Ok(await _service.GetByMovementIdAsync(movementId));

    [HttpGet("type/{type}")]
    public async Task<ActionResult<IEnumerable<StaffTransferDto>>> GetByType(StaffTransferType type)
        => Ok(await _service.GetByTypeAsync(type));

    [HttpGet("reason-category/{reasonCategory}")]
    public async Task<ActionResult<IEnumerable<StaffTransferDto>>> GetByReasonCategory(StaffTransferReasonCategory reasonCategory)
        => Ok(await _service.GetByReasonCategoryAsync(reasonCategory));

    [HttpGet("inter-company")]
    public async Task<ActionResult<IEnumerable<StaffTransferDto>>> GetInterCompany()
        => Ok(await _service.GetInterCompanyTransfersAsync());

    [HttpGet("relocation")]
    public async Task<ActionResult<IEnumerable<StaffTransferDto>>> GetRelocation()
        => Ok(await _service.GetRelocationTransfersAsync());

    [HttpGet("in-transition")]
    public async Task<ActionResult<IEnumerable<StaffTransferDto>>> GetInTransition()
        => Ok(await _service.GetInTransitionAsync());

    // =========================================================================
    // CRUD
    // =========================================================================

    [HttpPost]
    public async Task<ActionResult<StaffTransferDto>> Create([FromBody] CreateStaffTransferDto dto)
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
    public async Task<ActionResult<StaffTransferDto>> Update(Guid id, [FromBody] UpdateStaffTransferDto dto)
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
}
