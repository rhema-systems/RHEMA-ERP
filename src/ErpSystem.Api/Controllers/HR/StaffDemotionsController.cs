using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>Body payload for recording an employee's response to a demotion notice.</summary>
public record DemotionResponseRequest(string Response, DateTime ResponseDate);

[ApiController]
[Route("api/staff-demotions")]
[Authorize]
public class StaffDemotionsController : ControllerBase
{
    private readonly IStaffDemotionService _service;
    private readonly ICurrentUserService _currentUser;

    public StaffDemotionsController(IStaffDemotionService service, ICurrentUserService currentUser)
    {
        _service     = service;
        _currentUser = currentUser;
    }

    // =========================================================================
    // QUERIES
    // =========================================================================

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<StaffDemotionDto>> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id));

    [HttpGet("movement/{movementId:guid}")]
    public async Task<ActionResult<StaffDemotionDto?>> GetByMovement(Guid movementId)
        => Ok(await _service.GetByMovementIdAsync(movementId));

    [HttpGet("disciplinary")]
    public async Task<ActionResult<IEnumerable<StaffDemotionDto>>> GetDisciplinary()
        => Ok(await _service.GetDisciplinaryDemotionsAsync());

    [HttpGet("performance-related")]
    public async Task<ActionResult<IEnumerable<StaffDemotionDto>>> GetPerformanceRelated()
        => Ok(await _service.GetPerformanceRelatedDemotionsAsync());

    [HttpGet("pending-appeals")]
    public async Task<ActionResult<IEnumerable<StaffDemotionDto>>> GetPendingAppeals()
        => Ok(await _service.GetWithPendingAppealsAsync());

    // =========================================================================
    // CRUD
    // =========================================================================

    [HttpPost]
    public async Task<ActionResult<StaffDemotionDto>> Create([FromBody] CreateStaffDemotionDto dto)
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
    public async Task<ActionResult<StaffDemotionDto>> Update(Guid id, [FromBody] UpdateStaffDemotionDto dto)
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

    /// <summary>Records the employee's acceptance or appeal against a demotion notice.</summary>
    [HttpPost("{id:guid}/respond")]
    public async Task<IActionResult> RecordEmployeeResponse(Guid id, [FromBody] DemotionResponseRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _service.RecordEmployeeResponseAsync(id, request.Response, request.ResponseDate, employeeId.Value);
        return Ok(new { message = "Employee response to demotion recorded." });
    }
}
