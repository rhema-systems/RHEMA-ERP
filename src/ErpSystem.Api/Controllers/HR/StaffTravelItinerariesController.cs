using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using ErpSystem.Api.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/staff-travel/itineraries")]
[StaffTravelBusinessRules]
[Authorize(Policy = HrPermissions.TravelReadPolicy)]
public class StaffTravelItinerariesController : HrControllerBase
{
    private readonly IStaffTravelItineraryService _service;

    public StaffTravelItinerariesController(IStaffTravelItineraryService service, ICurrentUserService currentUser)
        : base(currentUser)
    {
        _service = service;
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

    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPost]
    public async Task<ActionResult<StaffTravelItineraryDto>> Create([FromBody] CreateStaffTravelItineraryDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (TryGetWriteContext(out var tenantId, out var userId) is { } contextError) return contextError;

        var created = await _service.CreateAsync(dto, tenantId, userId);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<StaffTravelItineraryDto>> Update(Guid id, [FromBody] UpdateStaffTravelItineraryDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (TryGetWriteContext(out _, out var userId) is { } contextError) return contextError;

        return Ok(await _service.UpdateAsync(dto, userId));
    }

    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPost("{id:guid}/set-current")]
    public async Task<IActionResult> SetCurrentVersion(Guid id)
    {
        if (TryGetWriteContext(out _, out var userId) is { } contextError) return contextError;

        await _service.SetCurrentVersionAsync(id, userId);
        return Ok(new { message = "Itinerary set as current version." });
    }

    [Authorize(Policy = HrPermissions.TravelAdminPolicy)]
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

    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPost("{itineraryId:guid}/legs")]
    public async Task<ActionResult<StaffTravelItineraryLegDto>> AddLeg(Guid itineraryId, [FromBody] CreateStaffTravelItineraryLegDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (TryGetWriteContext(out var tenantId, out var userId) is { } contextError) return contextError;

        dto.StaffTravelItineraryId = itineraryId;
        return Ok(await _service.AddLegAsync(dto, tenantId, userId));
    }

    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPut("legs/{legId:guid}")]
    public async Task<ActionResult<StaffTravelItineraryLegDto>> UpdateLeg(Guid legId, [FromBody] UpdateStaffTravelItineraryLegDto dto)
    {
        if (legId != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (TryGetWriteContext(out _, out var userId) is { } contextError) return contextError;

        return Ok(await _service.UpdateLegAsync(dto, userId));
    }

    [Authorize(Policy = HrPermissions.TravelAdminPolicy)]
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

    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPost("legs/{legId:guid}/activities")]
    public async Task<ActionResult<StaffTravelItineraryActivityDto>> AddActivity(Guid legId, [FromBody] CreateStaffTravelItineraryActivityDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (TryGetWriteContext(out var tenantId, out var userId) is { } contextError) return contextError;

        dto.StaffTravelItineraryLegId = legId;
        return Ok(await _service.AddActivityAsync(dto, tenantId, userId));
    }

    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPut("activities/{activityId:guid}")]
    public async Task<ActionResult<StaffTravelItineraryActivityDto>> UpdateActivity(Guid activityId, [FromBody] UpdateStaffTravelItineraryActivityDto dto)
    {
        if (activityId != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (TryGetWriteContext(out _, out var userId) is { } contextError) return contextError;

        return Ok(await _service.UpdateActivityAsync(dto, userId));
    }

    [Authorize(Policy = HrPermissions.TravelAdminPolicy)]
    [HttpDelete("activities/{activityId:guid}")]
    public async Task<IActionResult> DeleteActivity(Guid activityId)
    {
        await _service.DeleteActivityAsync(activityId);
        return NoContent();
    }
}
