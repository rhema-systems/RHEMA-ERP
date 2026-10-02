using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using ErpSystem.Api.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

[ApiController]
[Route("api/staff-travel/bookings")]
[StaffTravelBusinessRules]
[Authorize(Policy = HrPermissions.TravelReadPolicy)]
public class StaffTravelBookingsController : HrControllerBase
{
    private readonly IStaffTravelBookingService _service;
    private readonly IStaffTravelFleetService _fleet;

    public StaffTravelBookingsController(
        IStaffTravelBookingService service,
        IStaffTravelFleetService fleet,
        ICurrentUserService currentUser)
        : base(currentUser)
    {
        _service = service;
        _fleet = fleet;
    }

    /// <summary>
    /// The vehicles, drivers and destinations a company-vehicle leg chooses from (lane 6, FX-4) — read through travel's
    /// door, since HR holds no Maintenance permission. Each says why it is not available over the window: the trip's
    /// days, or the leg's pick-up and drop-off when given. <paramref name="excludeFleetTripId"/> leaves out the leg's
    /// own fleet trip when it is being changed.
    /// </summary>
    [HttpGet("fleet/options")]
    public async Task<ActionResult<StaffTravelFleetOptionsDto>> GetFleetOptions(
        [FromQuery] Guid requestId, [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null,
        [FromQuery] Guid? excludeFleetTripId = null)
        => Ok(await _fleet.GetOptionsAsync(requestId, from, to, excludeFleetTripId));

    /// <summary>
    /// Tenant + platform user id for audit fields. Deliberately does not require an employee
    /// link — see <see cref="HrControllerBase"/>.
    /// </summary>
    private (Guid tenantId, Guid userId)? ResolveContext()
        => TryGetWriteContext(out var tenantId, out var userId) is null ? (tenantId, userId) : null;

    // ⚠ Lane 4, D-8: there was a `CallerMayApproveExceptionsAsync` here — a booker holding HR.Travel.Admin authorised
    // their own breach in the same request. A breaching booking is now saved awaiting authorisation (the caller's
    // employee record is passed as who asked), and a different administrator decides it on the routes below.

    // =========================================================================
    // POLICY EXCEPTIONS ON BOOKINGS (lane 4, D-8)
    // =========================================================================

    /// <summary>The policy-breach register: flight and hotel bookings with an exception, pending first.</summary>
    [HttpGet("exceptions")]
    public async Task<ActionResult<IEnumerable<StaffTravelBookingExceptionDto>>> GetBookingExceptions(
        [FromQuery] TravelBookingExceptionState? state = null)
        => Ok(await _service.GetBookingExceptionsAsync(state));

    /// <summary>A travel administrator who neither booked it nor asked for the exception, and is not the traveller.</summary>
    [Authorize(Policy = HrPermissions.TravelAdminPolicy)]
    [HttpPost("flights/{id:guid}/exception/authorise")]
    public async Task<ActionResult<StaffTravelFlightBookingDto>> AuthoriseFlightException(Guid id)
    {
        if (TryGetEmployeeWriteContext(out _, out _, out var employeeId,
                "Authorising a travel policy exception") is { } contextError) return contextError;
        return Ok(await _service.DecideFlightExceptionAsync(id, authorise: true, reason: null, employeeId));
    }

    [Authorize(Policy = HrPermissions.TravelAdminPolicy)]
    [HttpPost("flights/{id:guid}/exception/refuse")]
    public async Task<ActionResult<StaffTravelFlightBookingDto>> RefuseFlightException(
        Guid id, [FromBody] RefuseStaffTravelBookingExceptionDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetEmployeeWriteContext(out _, out _, out var employeeId,
                "Refusing a travel policy exception") is { } contextError) return contextError;
        return Ok(await _service.DecideFlightExceptionAsync(id, authorise: false, dto.Reason, employeeId));
    }

    [Authorize(Policy = HrPermissions.TravelAdminPolicy)]
    [HttpPost("hotels/{id:guid}/exception/authorise")]
    public async Task<ActionResult<StaffTravelHotelBookingDto>> AuthoriseHotelException(Guid id)
    {
        if (TryGetEmployeeWriteContext(out _, out _, out var employeeId,
                "Authorising a travel policy exception") is { } contextError) return contextError;
        return Ok(await _service.DecideHotelExceptionAsync(id, authorise: true, reason: null, employeeId));
    }

    [Authorize(Policy = HrPermissions.TravelAdminPolicy)]
    [HttpPost("hotels/{id:guid}/exception/refuse")]
    public async Task<ActionResult<StaffTravelHotelBookingDto>> RefuseHotelException(
        Guid id, [FromBody] RefuseStaffTravelBookingExceptionDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetEmployeeWriteContext(out _, out _, out var employeeId,
                "Refusing a travel policy exception") is { } contextError) return contextError;
        return Ok(await _service.DecideHotelExceptionAsync(id, authorise: false, dto.Reason, employeeId));
    }

    // =========================================================================
    // STATUS VERBS (lane 5, D1)
    // =========================================================================
    //
    // A booking's status moves only here: an edit no longer writes it, and a create is Pending. The table and the trip
    // states each verb needs are StaffTravelBookingRules.Next's.

    private const string BookingKinds = "regex(^(flights|hotels|ground-transport|car-rentals)$)";

    private async Task<object> MoveAsync(
        string kind, Guid id, TravelBookingVerb verb, string? ticketNumber, CancelStaffTravelBookingDto? cancel, Guid? actor)
        => kind switch
        {
            "flights" => (object)await _service.MoveFlightAsync(id, verb, ticketNumber, cancel, actor),
            "hotels" => (object)await _service.MoveHotelAsync(id, verb, cancel, actor),
            "ground-transport" => (object)await _service.MoveGroundTransportAsync(id, verb, cancel, actor),
            _ => (object)await _service.MoveCarRentalAsync(id, verb, cancel, actor),
        };

    /// <summary>Hold (Pending → OnHold), confirm (Pending or OnHold → Confirmed), no-show or complete (Confirmed or
    /// Ticketed → NoShow / Completed, once the trip has started).</summary>
    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPost("{kind:" + BookingKinds + "}/{id:guid}/{verb:regex(^(hold|confirm|no-show|complete)$)}")]
    public async Task<IActionResult> MoveBooking(string kind, Guid id, string verb)
    {
        var move = verb switch
        {
            "hold" => TravelBookingVerb.Hold,
            "confirm" => TravelBookingVerb.Confirm,
            "no-show" => TravelBookingVerb.NoShow,
            _ => TravelBookingVerb.Complete,
        };
        return Ok(await MoveAsync(kind, id, move, null, null, CurrentUser.EmployeeId));
    }

    /// <summary>Cancels a live booking with the reason (an internal note on the trip) and, on a flight or hotel, the
    /// supplier's fee. Needs a login linked to an employee — the note's author.</summary>
    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPost("{kind:" + BookingKinds + "}/{id:guid}/cancel")]
    public async Task<IActionResult> CancelBooking(string kind, Guid id, [FromBody] CancelStaffTravelBookingDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetEmployeeWriteContext(out _, out _, out var employeeId,
                "Cancelling a booking") is { } contextError) return contextError;
        return Ok(await MoveAsync(kind, id, TravelBookingVerb.Cancel, null, dto, employeeId));
    }

    /// <summary>Tickets a confirmed flight: the ticket number, and an approved visa application (or one recorded as not
    /// required) when the trip needs a visa (T-24).</summary>
    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPost("flights/{id:guid}/ticket")]
    public async Task<ActionResult<StaffTravelFlightBookingDto>> TicketFlight(Guid id, [FromBody] TicketStaffTravelFlightDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Ok(await _service.MoveFlightAsync(id, TravelBookingVerb.Ticket, dto.TicketNumber, null, CurrentUser.EmployeeId));
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

    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPost("flights")]
    public async Task<ActionResult<StaffTravelFlightBookingDto>> CreateFlight([FromBody] CreateStaffTravelFlightBookingDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext();
        if (ctx is null) return BadRequest("User/tenant context could not be resolved.");

        var created = await _service.CreateFlightAsync(
            dto, ctx.Value.tenantId, ctx.Value.userId, CurrentUser.EmployeeId);
        return CreatedAtAction(nameof(GetFlightById), new { id = created.Id }, created);
    }

    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPut("flights/{id:guid}")]
    public async Task<ActionResult<StaffTravelFlightBookingDto>> UpdateFlight(Guid id, [FromBody] UpdateStaffTravelFlightBookingDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext();
        if (ctx is null) return BadRequest("User/tenant context could not be resolved.");

        return Ok(await _service.UpdateFlightAsync(
            dto, ctx.Value.userId, CurrentUser.EmployeeId));
    }

    [Authorize(Policy = HrPermissions.TravelAdminPolicy)]
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

    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPost("flights/{flightBookingId:guid}/segments")]
    public async Task<ActionResult<StaffTravelFlightSegmentDto>> AddSegment(Guid flightBookingId, [FromBody] CreateStaffTravelFlightSegmentDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext();
        if (ctx is null) return BadRequest("User/tenant context could not be resolved.");

        dto.StaffTravelFlightBookingId = flightBookingId;
        return Ok(await _service.AddSegmentAsync(dto, ctx.Value.tenantId, ctx.Value.userId));
    }

    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPut("segments/{segmentId:guid}")]
    public async Task<ActionResult<StaffTravelFlightSegmentDto>> UpdateSegment(Guid segmentId, [FromBody] UpdateStaffTravelFlightSegmentDto dto)
    {
        if (segmentId != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext();
        if (ctx is null) return BadRequest("User/tenant context could not be resolved.");

        return Ok(await _service.UpdateSegmentAsync(dto, ctx.Value.userId));
    }

    [Authorize(Policy = HrPermissions.TravelAdminPolicy)]
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

    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPost("hotels")]
    public async Task<ActionResult<StaffTravelHotelBookingDto>> CreateHotel([FromBody] CreateStaffTravelHotelBookingDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext();
        if (ctx is null) return BadRequest("User/tenant context could not be resolved.");

        var created = await _service.CreateHotelAsync(
            dto, ctx.Value.tenantId, ctx.Value.userId, CurrentUser.EmployeeId);
        return CreatedAtAction(nameof(GetHotelById), new { id = created.Id }, created);
    }

    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPut("hotels/{id:guid}")]
    public async Task<ActionResult<StaffTravelHotelBookingDto>> UpdateHotel(Guid id, [FromBody] UpdateStaffTravelHotelBookingDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext();
        if (ctx is null) return BadRequest("User/tenant context could not be resolved.");

        return Ok(await _service.UpdateHotelAsync(
            dto, ctx.Value.userId, CurrentUser.EmployeeId));
    }

    [Authorize(Policy = HrPermissions.TravelAdminPolicy)]
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

    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPost("ground-transport")]
    public async Task<ActionResult<StaffTravelGroundTransportDto>> CreateGroundTransport([FromBody] CreateStaffTravelGroundTransportDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext();
        if (ctx is null) return BadRequest("User/tenant context could not be resolved.");

        var created = await _service.CreateGroundTransportAsync(dto, ctx.Value.tenantId, ctx.Value.userId);
        return CreatedAtAction(nameof(GetGroundTransportById), new { id = created.Id }, created);
    }

    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPut("ground-transport/{id:guid}")]
    public async Task<ActionResult<StaffTravelGroundTransportDto>> UpdateGroundTransport(Guid id, [FromBody] UpdateStaffTravelGroundTransportDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext();
        if (ctx is null) return BadRequest("User/tenant context could not be resolved.");

        return Ok(await _service.UpdateGroundTransportAsync(dto, ctx.Value.userId));
    }

    [Authorize(Policy = HrPermissions.TravelAdminPolicy)]
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

    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPost("car-rentals")]
    public async Task<ActionResult<StaffTravelCarRentalBookingDto>> CreateCarRental([FromBody] CreateStaffTravelCarRentalBookingDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext();
        if (ctx is null) return BadRequest("User/tenant context could not be resolved.");

        var created = await _service.CreateCarRentalAsync(dto, ctx.Value.tenantId, ctx.Value.userId);
        return CreatedAtAction(nameof(GetCarRentalById), new { id = created.Id }, created);
    }

    [Authorize(Policy = HrPermissions.TravelWritePolicy)]
    [HttpPut("car-rentals/{id:guid}")]
    public async Task<ActionResult<StaffTravelCarRentalBookingDto>> UpdateCarRental(Guid id, [FromBody] UpdateStaffTravelCarRentalBookingDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = ResolveContext();
        if (ctx is null) return BadRequest("User/tenant context could not be resolved.");

        return Ok(await _service.UpdateCarRentalAsync(dto, ctx.Value.userId));
    }

    [Authorize(Policy = HrPermissions.TravelAdminPolicy)]
    [HttpDelete("car-rentals/{id:guid}")]
    public async Task<IActionResult> DeleteCarRental(Guid id)
    {
        await _service.DeleteCarRentalAsync(id);
        return NoContent();
    }
}
