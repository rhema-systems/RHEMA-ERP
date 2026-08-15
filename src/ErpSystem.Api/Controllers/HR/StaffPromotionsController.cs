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
[Route("api/staff-promotions")]
[Authorize(Roles = StaffPromotionsController.HrRoles)]
[MovementBusinessRules]
public class StaffPromotionsController : ControllerBase
{
    internal const string HrRoles = Constants.Roles.SuperAdmin + "," + Constants.Roles.Hr;

    private readonly IStaffPromotionService _service;
    private readonly ICurrentUserService _currentUser;

    public StaffPromotionsController(IStaffPromotionService service, ICurrentUserService currentUser)
    {
        _service     = service;
        _currentUser = currentUser;
    }

    // =========================================================================
    // QUERIES
    // =========================================================================

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<StaffPromotionDto>> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id));

    [HttpGet("movement/{movementId:guid}")]
    public async Task<ActionResult<StaffPromotionDto?>> GetByMovement(Guid movementId)
        => Ok(await _service.GetByMovementIdAsync(movementId));

    [HttpGet("type/{type}")]
    public async Task<ActionResult<IEnumerable<StaffPromotionDto>>> GetByType(StaffPromotionType type)
        => Ok(await _service.GetByTypeAsync(type));

    [HttpGet("active-acting")]
    public async Task<ActionResult<IEnumerable<StaffPromotionDto>>> GetActiveActingPromotions()
        => Ok(await _service.GetActiveActingPromotionsAsync());

    // =========================================================================
    // CRUD
    // =========================================================================

    [HttpPost]
    public async Task<ActionResult<StaffPromotionDto>> Create([FromBody] CreateStaffPromotionDto dto)
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
    public async Task<ActionResult<StaffPromotionDto>> Update(Guid id, [FromBody] UpdateStaffPromotionDto dto)
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
