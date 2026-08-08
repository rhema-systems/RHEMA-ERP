using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/staff-travel/itineraries")]
[Authorize]
public class StaffTravelItinerariesController : ControllerBase
{
    private readonly IStaffTravelItineraryService _service;
    private readonly ICurrentUserService _currentUser;

    public StaffTravelItinerariesController(IStaffTravelItineraryService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    // =========================================================================
    // ITINERARIES
    // =========================================================================

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<StaffTravelItineraryDto>> GetById(Guid id)
        => Ok(await _service.GetByIdAsync(id));

    [HttpGet("request/{requestId:guid}")]
    public async Task<ActionResult<IEnumerable<StaffTravelItinerarySummaryDto>>> GetByRequest(Guid requestId)
        => Ok(await _service.GetByRequestIdAsync(requestId));

    [HttpGet("request/{requestId:guid}/current")]
    public async Task<ActionResult<StaffTravelItineraryDto?>> GetCurrentVersion(Guid requestId)
        => Ok(await _service.GetCurrentVersionAsync(requestId));

    [HttpPost]
    public async Task<ActionResult<StaffTravelItineraryDto>> Create([FromBody] CreateStaffTravelItineraryDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var userId = _currentUser.EmployeeId;
        if (tenantId is null) return BadRequest("Tenant context could not be resolved.");
        if (userId is null) return BadRequest("Your user account is not linked to an employee record.");

        var created = await _service.CreateAsync(dto, tenantId.Value, userId.Value);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<StaffTravelItineraryDto>> Update(Guid id, [FromBody] UpdateStaffTravelItineraryDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var userId = _currentUser.EmployeeId;
        if (userId is null) return BadRequest("Your user account is not linked to an employee record.");

        return Ok(await _service.UpdateAsync(dto, userId.Value));
    }

    [HttpPost("{id:guid}/set-current")]
    public async Task<IActionResult> SetCurrentVersion(Guid id)
    {
        var userId = _currentUser.EmployeeId;
        if (userId is null) return BadRequest("Your user account is not linked to an employee record.");

        await _service.SetCurrentVersionAsync(id, userId.Value);
        return Ok(new { message = "Itinerary set as current version." });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }

    // =========================================================================
    // LEGS
    // =========================================================================

    [HttpGet("{itineraryId:guid}/legs")]
    public async Task<ActionResult<IEnumerable<StaffTravelItineraryLegDto>>> GetLegs(Guid itineraryId)
        => Ok(await _service.GetLegsAsync(itineraryId));

    [HttpGet("legs/{legId:guid}")]
    public async Task<ActionResult<StaffTravelItineraryLegDto>> GetLegById(Guid legId)
        => Ok(await _service.GetLegByIdAsync(legId));

    [HttpPost("{itineraryId:guid}/legs")]
    public async Task<ActionResult<StaffTravelItineraryLegDto>> AddLeg(Guid itineraryId, [FromBody] CreateStaffTravelItineraryLegDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var userId = _currentUser.EmployeeId;
        if (tenantId is null) return BadRequest("Tenant context could not be resolved.");
        if (userId is null) return BadRequest("Your user account is not linked to an employee record.");

        dto.StaffTravelItineraryId = itineraryId;
        return Ok(await _service.AddLegAsync(dto, tenantId.Value, userId.Value));
    }

    [HttpPut("legs/{legId:guid}")]
    public async Task<ActionResult<StaffTravelItineraryLegDto>> UpdateLeg(Guid legId, [FromBody] UpdateStaffTravelItineraryLegDto dto)
    {
        if (legId != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var userId = _currentUser.EmployeeId;
        if (userId is null) return BadRequest("Your user account is not linked to an employee record.");

        return Ok(await _service.UpdateLegAsync(dto, userId.Value));
    }

    [HttpDelete("legs/{legId:guid}")]
    public async Task<IActionResult> DeleteLeg(Guid legId)
    {
        await _service.DeleteLegAsync(legId);
        return NoContent();
    }

    // =========================================================================
    // ACTIVITIES
    // =========================================================================

    [HttpGet("legs/{legId:guid}/activities")]
    public async Task<ActionResult<IEnumerable<StaffTravelItineraryActivityDto>>> GetActivities(Guid legId)
        => Ok(await _service.GetActivitiesAsync(legId));

    [HttpPost("legs/{legId:guid}/activities")]
    public async Task<ActionResult<StaffTravelItineraryActivityDto>> AddActivity(Guid legId, [FromBody] CreateStaffTravelItineraryActivityDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var tenantId = _currentUser.TenantId;
        var userId = _currentUser.EmployeeId;
        if (tenantId is null) return BadRequest("Tenant context could not be resolved.");
        if (userId is null) return BadRequest("Your user account is not linked to an employee record.");

        dto.StaffTravelItineraryLegId = legId;
        return Ok(await _service.AddActivityAsync(dto, tenantId.Value, userId.Value));
    }

    [HttpPut("activities/{activityId:guid}")]
    public async Task<ActionResult<StaffTravelItineraryActivityDto>> UpdateActivity(Guid activityId, [FromBody] UpdateStaffTravelItineraryActivityDto dto)
    {
        if (activityId != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var userId = _currentUser.EmployeeId;
        if (userId is null) return BadRequest("Your user account is not linked to an employee record.");

        return Ok(await _service.UpdateActivityAsync(dto, userId.Value));
    }

    [HttpDelete("activities/{activityId:guid}")]
    public async Task<IActionResult> DeleteActivity(Guid activityId)
    {
        await _service.DeleteActivityAsync(activityId);
        return NoContent();
    }
}
