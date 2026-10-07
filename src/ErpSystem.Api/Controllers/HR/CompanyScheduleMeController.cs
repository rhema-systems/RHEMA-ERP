using ErpSystem.Api.Filters;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Core.Services.HR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// An employee's own room bookings, from the portal (company-schedule final closure lane 3c, D-13): the rooms they may
/// book, when those rooms are held, and booking, changing and cancelling their own.
/// </summary>
/// <remarks>
/// <para><b>Why this exists.</b> Every room and booking route on <c>CompanyScheduleController</c> is on
/// <c>HR.Company.*</c>, which the Employee role does not hold, so booking a meeting room was the HR desk's alone. The
/// user's ruling (D-13): staff book any room in use under the same rules, see, change and cancel only their own, and see
/// others' bookings as busy times; a room needing approval goes to the approver; HR keeps the desk.</para>
///
/// <para><b>The rules, as the travel portal's (<see cref="StaffTravelMeController"/>):</b></para>
/// <list type="number">
/// <item>No route or query parameter carries an employee id. The booker is the token, always.</item>
/// <item>Every id-addressed operation resolves through <see cref="GetOwnBookingAsync"/>; someone else's booking is a
/// <b>404, not a 403</b>, so this surface cannot be used to find out which booking ids exist.</item>
/// <item>Approving, not approving, marking a no-show and deleting have <b>no route here at all</b>.</item>
/// <item>The booking service's rules are the desk's — the room's own limits, the seats, the lock, the approval on the
/// engine. Only two things are this door's: a booking here is never linked to an event (the user's ruling: a room for a
/// company event is the desk's), and it may not start in the past
/// (<see cref="RoomBookingRules.RefuseSelfServiceStart"/>).</item>
/// </list>
///
/// <para>Gated on <c>InternalOnly</c> only: holding no company-schedule permission is the normal case for the people this
/// controller serves.</para>
/// </remarks>
[ApiController]
[Route("api/CompanySchedule/me")]
[Authorize(Policy = "InternalOnly")]
[CompanyScheduleBusinessRules]
public class CompanyScheduleMeController : HrControllerBase
{
    private readonly IMeetingRoomService _rooms;
    private readonly IRoomBookingService _bookings;

    public CompanyScheduleMeController(
        IMeetingRoomService rooms,
        IRoomBookingService bookings,
        ICurrentUserService currentUser)
        : base(currentUser)
    {
        _rooms = rooms;
        _bookings = bookings;
    }

    /// <summary>
    /// The caller's own booking, or null when it is not theirs or does not exist. Callers turn null into 404 — never 403,
    /// and never a message that tells the two apart.
    /// </summary>
    private async Task<RoomBookingDto?> GetOwnBookingAsync(Guid id, Guid employeeId, CancellationToken ct)
    {
        try
        {
            var booking = await _bookings.GetByIdAsync(id, ct);
            return booking.BookedById == employeeId ? booking : null;
        }
        catch (ArgumentException)
        {
            // The service raises "not found" for an id outside the caller's tenant. Same answer.
            return null;
        }
    }

    // =========================================================================
    // THE ROOMS
    // =========================================================================

    /// <summary>The rooms you may book — in use and open for booking — with their seats, facilities and limits.</summary>
    [HttpGet("rooms")]
    public async Task<ActionResult<IEnumerable<MeetingRoomDto>>> GetRooms(CancellationToken ct)
    {
        if (TryGetEmployeeWriteContext(out _, out _, out _, "Seeing the rooms you may book") is { } error) return error;
        return Ok(await _rooms.GetBookableRoomsAsync(ct));
    }

    /// <summary>The rooms free for a window, inside each room's own limits — the desk's availability read.</summary>
    [HttpGet("rooms/available")]
    public async Task<ActionResult<IEnumerable<MeetingRoomSummaryDto>>> GetAvailableRooms(
        [FromQuery] DateTime startDateTime, [FromQuery] DateTime endDateTime, [FromQuery] int? minCapacity = null,
        CancellationToken ct = default)
    {
        if (TryGetEmployeeWriteContext(out _, out _, out _, "Finding a free room") is { } error) return error;
        return Ok(await _rooms.GetAvailableRoomsAsync(startDateTime, endDateTime, minCapacity, ct));
    }

    /// <summary>
    /// When the rooms are held, over whole days (at most 31): the room and the time of each booking, your own marked with
    /// its id. Nobody else's purpose, booker or number (D-13).
    /// </summary>
    [HttpGet("rooms/busy")]
    public async Task<ActionResult<IReadOnlyList<RoomBusyTimeDto>>> GetBusyTimes(
        [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken ct)
    {
        if (TryGetEmployeeWriteContext(out _, out _, out var employeeId, "Seeing when the rooms are free") is { } error) return error;
        return Ok(await _bookings.GetBusyTimesAsync(from, to, employeeId, ct));
    }

    // =========================================================================
    // MY BOOKINGS
    // =========================================================================

    /// <summary>Every booking you have made, any status, the latest first.</summary>
    [HttpGet("room-bookings")]
    public async Task<ActionResult<IEnumerable<RoomBookingSummaryDto>>> GetMyBookings(CancellationToken ct)
    {
        if (TryGetEmployeeWriteContext(out _, out _, out var employeeId, "Seeing your room bookings") is { } error) return error;
        return Ok(await _bookings.GetByBookerAsync(employeeId, ct));
    }

    /// <summary>One of your bookings.</summary>
    [HttpGet("room-bookings/{id:guid}")]
    public async Task<ActionResult<RoomBookingDto>> GetMyBooking(Guid id, CancellationToken ct)
    {
        if (TryGetEmployeeWriteContext(out _, out _, out var employeeId, "Seeing a room booking") is { } error) return error;

        var booking = await GetOwnBookingAsync(id, employeeId, ct);
        return booking is null ? NotFound() : Ok(booking);
    }

    /// <summary>
    /// Book a room for yourself. A room needing approval waits for it — the approver is asked through the engine, and you
    /// are told the outcome.
    /// </summary>
    /// <remarks>
    /// <c>EventId</c> is dropped rather than read: linking a booking to a company event is the desk's (the user's ruling).
    /// </remarks>
    [HttpPost("room-bookings")]
    public async Task<ActionResult<RoomBookingDto>> CreateMyBooking([FromBody] CreateRoomBookingDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetEmployeeWriteContext(out _, out _, out var employeeId, "Booking a room") is { } error) return error;

        dto.EventId = null;
        if (RoomBookingRules.RefuseSelfServiceStart(dto.StartDateTime, null, DateTime.UtcNow) is { } refusal)
            throw new InvalidOperationException(refusal);

        var created = await _bookings.CreateAsync(dto, employeeId, ct);
        return CreatedAtAction(nameof(GetMyBooking), new { id = created.Id }, created);
    }

    /// <summary>
    /// Change your booking while it holds its room — the room's rules again; a new time on a room needing approval waits
    /// for approval again. A changed start may not be in the past; an unchanged one may, so a booking under way can run on.
    /// </summary>
    [HttpPut("room-bookings/{id:guid}")]
    public async Task<ActionResult<RoomBookingDto>> UpdateMyBooking(Guid id, [FromBody] UpdateRoomBookingDto dto, CancellationToken ct)
    {
        if (id != dto.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetEmployeeWriteContext(out _, out _, out var employeeId, "Changing a room booking") is { } error) return error;

        var own = await GetOwnBookingAsync(id, employeeId, ct);
        if (own is null) return NotFound();
        if (RoomBookingRules.RefuseSelfServiceStart(dto.StartDateTime, own.StartDateTime, DateTime.UtcNow) is { } refusal)
            throw new InvalidOperationException(refusal);

        return Ok(await _bookings.UpdateAsync(dto, ct));
    }

    /// <summary>
    /// Cancel your booking, saying why — as at the desk. An approval still under way is withdrawn; nobody is told of your
    /// own act.
    /// </summary>
    [HttpPost("room-bookings/{id:guid}/cancel")]
    public async Task<ActionResult<RoomBookingDto>> CancelMyBooking(Guid id, [FromBody] CancelMyRoomBookingDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (TryGetEmployeeWriteContext(out _, out _, out var employeeId, "Cancelling a room booking") is { } error) return error;

        if (await GetOwnBookingAsync(id, employeeId, ct) is null) return NotFound();

        await _bookings.CancelBookingAsync(new CancelRoomBookingDto
        {
            BookingId = id,
            CancellationReason = dto.CancellationReason ?? string.Empty,
        }, ct);
        return Ok(await _bookings.GetByIdAsync(id, ct));
    }
}
