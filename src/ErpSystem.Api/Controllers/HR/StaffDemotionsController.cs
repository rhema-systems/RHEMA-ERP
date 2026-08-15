using ErpSystem.Api.Filters;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// Body payload for recording an employee's response to a demotion notice.
/// The response date is not taken from the client — the server stamps it, because the date an appeal
/// was filed is the thing the appeal deadline is measured against.
/// </summary>
public record DemotionResponseRequest(string Response);

/// <summary>
/// Demotion detail records. Gated per action rather than at class level: everything here is HR's
/// except the demoted employee's own response, which is theirs alone (stacked [Authorize] attributes
/// are ANDed, so a class-level role gate would lock the subject out of their own appeal).
/// </summary>
[ApiController]
[Route("api/staff-demotions")]
[Authorize]
[MovementBusinessRules]
public class StaffDemotionsController : ControllerBase
{
    private const string HrRoles = Constants.Roles.SuperAdmin + "," + Constants.Roles.Hr;

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

    [Authorize(Roles = HrRoles)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<StaffDemotionDto>> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id));

    [Authorize(Roles = HrRoles)]
    [HttpGet("movement/{movementId:guid}")]
    public async Task<ActionResult<StaffDemotionDto?>> GetByMovement(Guid movementId)
        => Ok(await _service.GetByMovementIdAsync(movementId));

    [Authorize(Roles = HrRoles)]
    [HttpGet("disciplinary")]
    public async Task<ActionResult<IEnumerable<StaffDemotionDto>>> GetDisciplinary()
        => Ok(await _service.GetDisciplinaryDemotionsAsync());

    [Authorize(Roles = HrRoles)]
    [HttpGet("performance-related")]
    public async Task<ActionResult<IEnumerable<StaffDemotionDto>>> GetPerformanceRelated()
        => Ok(await _service.GetPerformanceRelatedDemotionsAsync());

    [Authorize(Roles = HrRoles)]
    [HttpGet("pending-appeals")]
    public async Task<ActionResult<IEnumerable<StaffDemotionDto>>> GetPendingAppeals()
        => Ok(await _service.GetWithPendingAppealsAsync());

    // =========================================================================
    // CRUD
    // =========================================================================

    [Authorize(Roles = HrRoles)]
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

    [Authorize(Roles = HrRoles)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<StaffDemotionDto>> Update(Guid id, [FromBody] UpdateStaffDemotionDto dto)
    {
        if (id != dto.Id)        return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        return Ok(await _service.UpdateAsync(dto, employeeId.Value));
    }

    [Authorize(Roles = HrRoles)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }

    // =========================================================================
    // WORKFLOW
    // =========================================================================

    /// <summary>
    /// Records the employee's acceptance or appeal against a demotion notice. Open by design, and the
    /// service refuses anyone but the demoted employee — an appeal filed in someone else's name is
    /// worse than no appeal at all.
    /// </summary>
    [HttpPost("{id:guid}/respond")]
    public async Task<IActionResult> RecordEmployeeResponse(Guid id, [FromBody] DemotionResponseRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var employeeId = _currentUser.EmployeeId;
        if (employeeId == null) return BadRequest("Your user account is not linked to an employee record. Please contact your administrator.");

        await _service.RecordEmployeeResponseAsync(id, request.Response, employeeId.Value);
        return Ok(new { message = "Employee response to demotion recorded." });
    }
}
