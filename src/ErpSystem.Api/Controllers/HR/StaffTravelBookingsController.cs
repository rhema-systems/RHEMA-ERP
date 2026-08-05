using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/staff-travel/bookings")]
[Authorize]
public class StaffTravelBookingsController : ControllerBase
{
    private readonly IStaffTravelBookingService _service;
    private readonly ICurrentUserService _currentUser;

    public StaffTravelBookingsController(IStaffTravelBookingService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    private (Guid tenantId, Guid userId)? ResolveContext()
    {
        var tenantId = _currentUser.TenantId;
        var userId = _currentUser.EmployeeId;
        if (tenantId is null || userId is null) return null;
        return (tenantId.Value, userId.Value);
    }

    // =========================================================================
    // FLIGHT BOOKINGS
    // =========================================================================

    [HttpGet("flights/{id:guid}")]
    public async Task<ActionResult<StaffTravelFlightBookingDto>> GetFlightById(Guid id)
        => Ok(await _service.GetFlightByIdAsync(id));

    [HttpGet("flights/request/{requestId:guid}")]
    public async Task<ActionResult<IEnumerable<StaffTravelFlightBookingSummaryDto>>> GetFlightsByRequest(Guid requestId)
        => Ok(await _service.GetFlightsByRequestAsync(requestId));

    [HttpGet("flights/status/{status}")]
    public async Task<ActionResult<IEnumerable<StaffTravelFlightBookingSummaryDto>>> GetFlightsByStatus(TravelBookingStatus status)
        => Ok(await _service.GetFlightsByStatusAsync(status));

    [HttpPost("flights")]
    public async Task<ActionResult<StaffTravelFlightBookingDto>> CreateFlight([FromBody] CreateStaffTravelFlightBookingDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext();
        if (ctx is null) return BadRequest("User/tenant context could not be resolved.");

        var created = await _service.CreateFlightAsync(dto, ctx.Value.tenantId, ctx.Value.userId);
        return CreatedAtAction(nameof(GetFlightById), new { id = created.Id }, created);
    }

    [HttpPut("flights/{id:guid}")]
    public async Task<ActionResult<StaffTravelFlightBookingDto>> UpdateFlight(Guid id, [FromBody] UpdateStaffTravelFlightBookingDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext();
        if (ctx is null) return BadRequest("User/tenant context could not be resolved.");

        return Ok(await _service.UpdateFlightAsync(dto, ctx.Value.userId));
    }

    [HttpDelete("flights/{id:guid}")]
    public async Task<IActionResult> DeleteFlight(Guid id)
    {
        await _service.DeleteFlightAsync(id);
        return NoContent();
    }

    // ---- Flight segments ---------------------------------------------------

    [HttpGet("flights/{flightBookingId:guid}/segments")]
    public async Task<ActionResult<IEnumerable<StaffTravelFlightSegmentDto>>> GetSegments(Guid flightBookingId)
        => Ok(await _service.GetSegmentsAsync(flightBookingId));

    [HttpPost("flights/{flightBookingId:guid}/segments")]
    public async Task<ActionResult<StaffTravelFlightSegmentDto>> AddSegment(Guid flightBookingId, [FromBody] CreateStaffTravelFlightSegmentDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext();
        if (ctx is null) return BadRequest("User/tenant context could not be resolved.");

        dto.StaffTravelFlightBookingId = flightBookingId;
        return Ok(await _service.AddSegmentAsync(dto, ctx.Value.tenantId, ctx.Value.userId));
    }

    [HttpPut("segments/{segmentId:guid}")]
    public async Task<ActionResult<StaffTravelFlightSegmentDto>> UpdateSegment(Guid segmentId, [FromBody] UpdateStaffTravelFlightSegmentDto dto)
    {
        if (segmentId != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext();
        if (ctx is null) return BadRequest("User/tenant context could not be resolved.");

        return Ok(await _service.UpdateSegmentAsync(dto, ctx.Value.userId));
    }

    [HttpDelete("segments/{segmentId:guid}")]
    public async Task<IActionResult> DeleteSegment(Guid segmentId)
    {
        await _service.DeleteSegmentAsync(segmentId);
        return NoContent();
    }

    // =========================================================================
    // HOTEL BOOKINGS
    // =========================================================================

    [HttpGet("hotels/{id:guid}")]
    public async Task<ActionResult<StaffTravelHotelBookingDto>> GetHotelById(Guid id)
        => Ok(await _service.GetHotelByIdAsync(id));

    [HttpGet("hotels/request/{requestId:guid}")]
    public async Task<ActionResult<IEnumerable<StaffTravelHotelBookingSummaryDto>>> GetHotelsByRequest(Guid requestId)
        => Ok(await _service.GetHotelsByRequestAsync(requestId));

    [HttpPost("hotels")]
    public async Task<ActionResult<StaffTravelHotelBookingDto>> CreateHotel([FromBody] CreateStaffTravelHotelBookingDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext();
        if (ctx is null) return BadRequest("User/tenant context could not be resolved.");

        var created = await _service.CreateHotelAsync(dto, ctx.Value.tenantId, ctx.Value.userId);
        return CreatedAtAction(nameof(GetHotelById), new { id = created.Id }, created);
    }

    [HttpPut("hotels/{id:guid}")]
    public async Task<ActionResult<StaffTravelHotelBookingDto>> UpdateHotel(Guid id, [FromBody] UpdateStaffTravelHotelBookingDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext();
        if (ctx is null) return BadRequest("User/tenant context could not be resolved.");

        return Ok(await _service.UpdateHotelAsync(dto, ctx.Value.userId));
    }

    [HttpDelete("hotels/{id:guid}")]
    public async Task<IActionResult> DeleteHotel(Guid id)
    {
        await _service.DeleteHotelAsync(id);
        return NoContent();
    }

    // =========================================================================
    // GROUND TRANSPORT
    // =========================================================================

    [HttpGet("ground-transport/{id:guid}")]
    public async Task<ActionResult<StaffTravelGroundTransportDto>> GetGroundTransportById(Guid id)
        => Ok(await _service.GetGroundTransportByIdAsync(id));

    [HttpGet("ground-transport/request/{requestId:guid}")]
    public async Task<ActionResult<IEnumerable<StaffTravelGroundTransportDto>>> GetGroundTransportsByRequest(Guid requestId)
        => Ok(await _service.GetGroundTransportsByRequestAsync(requestId));

    [HttpPost("ground-transport")]
    public async Task<ActionResult<StaffTravelGroundTransportDto>> CreateGroundTransport([FromBody] CreateStaffTravelGroundTransportDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext();
        if (ctx is null) return BadRequest("User/tenant context could not be resolved.");

        var created = await _service.CreateGroundTransportAsync(dto, ctx.Value.tenantId, ctx.Value.userId);
        return CreatedAtAction(nameof(GetGroundTransportById), new { id = created.Id }, created);
    }

    [HttpPut("ground-transport/{id:guid}")]
    public async Task<ActionResult<StaffTravelGroundTransportDto>> UpdateGroundTransport(Guid id, [FromBody] UpdateStaffTravelGroundTransportDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext();
        if (ctx is null) return BadRequest("User/tenant context could not be resolved.");

        return Ok(await _service.UpdateGroundTransportAsync(dto, ctx.Value.userId));
    }

    [HttpDelete("ground-transport/{id:guid}")]
    public async Task<IActionResult> DeleteGroundTransport(Guid id)
    {
        await _service.DeleteGroundTransportAsync(id);
        return NoContent();
    }

    // =========================================================================
    // CAR RENTAL BOOKINGS
    // =========================================================================

    [HttpGet("car-rentals/{id:guid}")]
    public async Task<ActionResult<StaffTravelCarRentalBookingDto>> GetCarRentalById(Guid id)
        => Ok(await _service.GetCarRentalByIdAsync(id));

    [HttpGet("car-rentals/request/{requestId:guid}")]
    public async Task<ActionResult<IEnumerable<StaffTravelCarRentalBookingDto>>> GetCarRentalsByRequest(Guid requestId)
        => Ok(await _service.GetCarRentalsByRequestAsync(requestId));

    [HttpPost("car-rentals")]
    public async Task<ActionResult<StaffTravelCarRentalBookingDto>> CreateCarRental([FromBody] CreateStaffTravelCarRentalBookingDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext();
        if (ctx is null) return BadRequest("User/tenant context could not be resolved.");

        var created = await _service.CreateCarRentalAsync(dto, ctx.Value.tenantId, ctx.Value.userId);
        return CreatedAtAction(nameof(GetCarRentalById), new { id = created.Id }, created);
    }

    [HttpPut("car-rentals/{id:guid}")]
    public async Task<ActionResult<StaffTravelCarRentalBookingDto>> UpdateCarRental(Guid id, [FromBody] UpdateStaffTravelCarRentalBookingDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext();
        if (ctx is null) return BadRequest("User/tenant context could not be resolved.");

        return Ok(await _service.UpdateCarRentalAsync(dto, ctx.Value.userId));
    }

    [HttpDelete("car-rentals/{id:guid}")]
    public async Task<IActionResult> DeleteCarRental(Guid id)
    {
        await _service.DeleteCarRentalAsync(id);
        return NoContent();
    }
}
