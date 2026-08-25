using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// The company schedule — events with participants, attendance, attachments and tasks; meeting
/// rooms and their bookings; company milestones; business closures; fiscal years and periods.
/// </summary>
/// <remarks>
/// <para><b>W3 slice 14.</b> All 90 actions carried a bare <c>[Authorize]</c> and no screen has
/// ever called any of them — the surface is dormant since the port. Gated verb-mechanically on
/// <c>HR.Company.*</c>: reads → Read, writes and decisions → Write, deletes → Admin. Nothing
/// self-service is drawn here on purpose: every actor id (<c>organizerId</c>, <c>bookedById</c>,
/// <c>approvedById</c>, <c>markedById</c>, <c>announcedById</c>) is client-supplied rather than
/// token-derived, so an open "book a room" or "respond to an invitation" surface would be
/// act-as-anyone. If a calendar or room-booking screen is ever built, those paths must first move
/// to token actors and only then open with self-or-permission checks — recorded as a slice-14
/// residual in the W3 plan.</para>
/// </remarks>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "InternalOnly")]
public class CompanyScheduleController : ControllerBase
{
    private readonly ICompanyEventService _eventService;
    private readonly IMeetingRoomService _roomService;
    private readonly IRoomBookingService _bookingService;
    private readonly ICompanyMilestoneService _milestoneService;
    private readonly IBusinessClosureService _closureService;
    private readonly IFiscalYearService _fiscalYearService;

    public CompanyScheduleController(
        ICompanyEventService eventService,
        IMeetingRoomService roomService,
        IRoomBookingService bookingService,
        ICompanyMilestoneService milestoneService,
        IBusinessClosureService closureService,
        IFiscalYearService fiscalYearService)
    {
        _eventService = eventService;
        _roomService = roomService;
        _bookingService = bookingService;
        _milestoneService = milestoneService;
        _closureService = closureService;
        _fiscalYearService = fiscalYearService;
    }

    #region Company Events

    [HttpGet("events")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<ActionResult<IEnumerable<CompanyEventDto>>> GetEvents()
        => Ok(await _eventService.GetAllAsync());

    [HttpGet("events/paged")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<ActionResult<PagedResult<CompanyEventDto>>> GetEventsPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20)
        => Ok(await _eventService.GetPagedAsync(pageNumber, pageSize));

    [HttpGet("events/{id:guid}")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<ActionResult<CompanyEventDto>> GetEvent(Guid id)
        => Ok(await _eventService.GetByIdAsync(id));

    [HttpGet("events/{id:guid}/details")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<ActionResult<CompanyEventDetailDto>> GetEventDetails(Guid id)
        => Ok(await _eventService.GetDetailByIdAsync(id));

    [HttpGet("events/range")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<ActionResult<IEnumerable<CompanyEventSummaryDto>>> GetEventsByDateRange(
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate)
        => Ok(await _eventService.GetByDateRangeAsync(startDate, endDate));

    [HttpGet("events/organizer/{organizerId:guid}")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<ActionResult<IEnumerable<CompanyEventSummaryDto>>> GetEventsByOrganizer(Guid organizerId)
        => Ok(await _eventService.GetByOrganizerAsync(organizerId));

    [HttpGet("events/department/{departmentId:guid}")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<ActionResult<IEnumerable<CompanyEventSummaryDto>>> GetEventsByDepartment(Guid departmentId)
        => Ok(await _eventService.GetByDepartmentAsync(departmentId));

    [HttpGet("events/status/{status}")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<ActionResult<IEnumerable<CompanyEventSummaryDto>>> GetEventsByStatus(EventStatus status)
        => Ok(await _eventService.GetByStatusAsync(status));

    [HttpGet("events/category/{category}")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<ActionResult<IEnumerable<CompanyEventSummaryDto>>> GetEventsByCategory(EventCategory category)
        => Ok(await _eventService.GetByCategoryAsync(category));

    [HttpGet("events/upcoming")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<ActionResult<IEnumerable<CompanyEventSummaryDto>>> GetUpcomingEvents([FromQuery] int daysAhead = 30)
        => Ok(await _eventService.GetUpcomingEventsAsync(daysAhead));

    [HttpPost("events")]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    public async Task<ActionResult<CompanyEventDto>> CreateEvent([FromQuery] Guid organizerId, [FromBody] CreateCompanyEventDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var created = await _eventService.CreateAsync(dto, organizerId);
        return CreatedAtAction(nameof(GetEvent), new { id = created.Id }, created);
    }

    [HttpPut("events/{id:guid}")]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    public async Task<ActionResult<CompanyEventDto>> UpdateEvent(Guid id, [FromBody] UpdateCompanyEventDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var updated = await _eventService.UpdateAsync(dto);
        return Ok(updated);
    }

    [HttpPost("events/{id:guid}/approve")]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    public async Task<IActionResult> ApproveEvent(Guid id, [FromQuery] Guid approvedById)
    {
        await _eventService.ApproveEventAsync(id, approvedById);
        return Ok(new { message = "Event approved" });
    }

    [HttpPost("events/{id:guid}/cancel")]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    public async Task<IActionResult> CancelEvent(Guid id, [FromBody] CancelEventDto dto)
    {
        dto.EventId = id;
        await _eventService.CancelEventAsync(dto);
        return Ok(new { message = "Event cancelled" });
    }

    [HttpPost("events/{id:guid}/reschedule")]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    public async Task<IActionResult> RescheduleEvent(Guid id, [FromBody] RescheduleEventDto dto)
    {
        dto.EventId = id;
        await _eventService.RescheduleEventAsync(dto);
        return Ok(new { message = "Event rescheduled" });
    }

    [HttpPost("events/{id:guid}/complete")]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    public async Task<IActionResult> CompleteEvent(Guid id, [FromBody] CompleteEventDto dto)
    {
        dto.EventId = id;
        await _eventService.CompleteEventAsync(dto);
        return Ok(new { message = "Event completed" });
    }

    [HttpDelete("events/{id:guid}")]
    [Authorize(Policy = HrPermissions.CompanyAdminPolicy)]
    public async Task<IActionResult> DeleteEvent(Guid id)
    {
        await _eventService.DeleteAsync(id);
        return NoContent();
    }

    #region Participants

    [HttpPost("events/{eventId:guid}/participants")]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    public async Task<ActionResult<EventParticipantDto>> AddParticipant(Guid eventId, [FromBody] CreateEventParticipantDto dto)
    {
        dto.EventId = eventId;
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var created = await _eventService.AddParticipantAsync(dto);
        return CreatedAtAction(nameof(GetParticipants), new { eventId }, created);
    }

    [HttpGet("events/{eventId:guid}/participants")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<ActionResult<IEnumerable<EventParticipantDto>>> GetParticipants(Guid eventId)
        => Ok(await _eventService.GetParticipantsAsync(eventId));

    [HttpPost("events/{eventId:guid}/participants/respond")]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    public async Task<IActionResult> RespondToInvitation(Guid eventId, [FromBody] RespondToEventInvitationDto dto)
    {
        await _eventService.RespondToInvitationAsync(dto);
        return Ok(new { message = "Invitation response recorded" });
    }

    [HttpDelete("participants/{participantId:guid}")]
    [Authorize(Policy = HrPermissions.CompanyAdminPolicy)]
    public async Task<IActionResult> RemoveParticipant(Guid participantId)
    {
        await _eventService.RemoveParticipantAsync(participantId);
        return NoContent();
    }

    #endregion

    #region Attendance

    [HttpPost("events/{eventId:guid}/attendance")]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    public async Task<ActionResult<EventAttendanceDto>> MarkAttendance(Guid eventId, [FromBody] MarkEventAttendanceDto dto, [FromQuery] Guid markedById)
    {
        dto.EventId = eventId;
        var attendance = await _eventService.MarkAttendanceAsync(dto, markedById);
        return Ok(attendance);
    }

    [HttpGet("events/{eventId:guid}/attendance")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<ActionResult<IEnumerable<EventAttendanceDto>>> GetAttendance(Guid eventId)
        => Ok(await _eventService.GetAttendanceAsync(eventId));

    [HttpPost("attendance/{attendanceId:guid}/checkout")]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    public async Task<IActionResult> CheckoutAttendance(Guid attendanceId, [FromBody] CheckOutEventDto dto)
    {
        dto.AttendanceId = attendanceId;
        await _eventService.CheckOutAsync(dto);
        return Ok(new { message = "Checked out" });
    }

    #endregion

    #region Event Attachments

    [HttpPost("events/{eventId:guid}/attachments")]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    public async Task<ActionResult<EventAttachmentDto>> AddEventAttachment(Guid eventId, [FromBody] CreateEventAttachmentDto dto)
    {
        dto.EventId = eventId;
        var created = await _eventService.AddAttachmentAsync(dto);
        return CreatedAtAction(nameof(GetEventAttachments), new { eventId }, created);
    }

    [HttpGet("events/{eventId:guid}/attachments")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<ActionResult<IEnumerable<EventAttachmentDto>>> GetEventAttachments(Guid eventId)
        => Ok(await _eventService.GetAttachmentsAsync(eventId));

    [HttpDelete("attachments/{attachmentId:guid}")]
    [Authorize(Policy = HrPermissions.CompanyAdminPolicy)]
    public async Task<IActionResult> DeleteEventAttachment(Guid attachmentId)
    {
        await _eventService.DeleteAttachmentAsync(attachmentId);
        return NoContent();
    }

    #endregion

    #region Event Tasks

    [HttpPost("events/{eventId:guid}/tasks")]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    public async Task<ActionResult<EventTaskDto>> AddEventTask(Guid eventId, [FromBody] CreateEventTaskDto dto)
    {
        dto.EventId = eventId;
        var created = await _eventService.AddTaskAsync(dto);
        return CreatedAtAction(nameof(GetEventTasks), new { eventId }, created);
    }

    [HttpGet("events/{eventId:guid}/tasks")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<ActionResult<IEnumerable<EventTaskDto>>> GetEventTasks(Guid eventId)
        => Ok(await _eventService.GetTasksAsync(eventId));

    [HttpPut("tasks/{taskId:guid}")]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    public async Task<ActionResult<EventTaskDto>> UpdateEventTask(Guid taskId, [FromBody] UpdateEventTaskDto dto)
    {
        if (taskId != dto.Id) return BadRequest("ID mismatch");
        var updated = await _eventService.UpdateTaskAsync(dto);
        return Ok(updated);
    }

    [HttpPost("tasks/{taskId:guid}/complete")]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    public async Task<IActionResult> CompleteEventTask(Guid taskId, [FromBody] CompleteEventTaskDto dto)
    {
        dto.TaskId = taskId;
        await _eventService.CompleteTaskAsync(dto);
        return Ok(new { message = "Task completed" });
    }

    [HttpDelete("tasks/{taskId:guid}")]
    [Authorize(Policy = HrPermissions.CompanyAdminPolicy)]
    public async Task<IActionResult> DeleteEventTask(Guid taskId)
    {
        await _eventService.DeleteTaskAsync(taskId);
        return NoContent();
    }

    #endregion

    #endregion

    #region Meeting Rooms

    [HttpGet("rooms")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<ActionResult<IEnumerable<MeetingRoomDto>>> GetMeetingRooms()
        => Ok(await _roomService.GetAllAsync());

    [HttpGet("rooms/paged")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<ActionResult<PagedResult<MeetingRoomDto>>> GetMeetingRoomsPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20)
        => Ok(await _roomService.GetPagedAsync(pageNumber, pageSize));

    [HttpGet("rooms/{id:guid}")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<ActionResult<MeetingRoomDto>> GetMeetingRoom(Guid id)
        => Ok(await _roomService.GetByIdAsync(id));

    [HttpGet("rooms/station/{stationId:guid}")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<ActionResult<IEnumerable<MeetingRoomSummaryDto>>> GetRoomsByStation(Guid stationId)
        => Ok(await _roomService.GetByStationAsync(stationId));

    [HttpGet("rooms/available")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<ActionResult<IEnumerable<MeetingRoomSummaryDto>>> GetAvailableRooms(
        [FromQuery] DateTime startDateTime,
        [FromQuery] DateTime endDateTime,
        [FromQuery] int? minCapacity = null)
        => Ok(await _roomService.GetAvailableRoomsAsync(startDateTime, endDateTime, minCapacity));

    [HttpGet("rooms/active")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<ActionResult<IEnumerable<MeetingRoomSummaryDto>>> GetActiveRooms()
        => Ok(await _roomService.GetActiveRoomsAsync());

    [HttpPost("rooms")]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    public async Task<ActionResult<MeetingRoomDto>> CreateMeetingRoom([FromBody] CreateMeetingRoomDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var created = await _roomService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetMeetingRoom), new { id = created.Id }, created);
    }

    [HttpPut("rooms/{id:guid}")]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    public async Task<ActionResult<MeetingRoomDto>> UpdateMeetingRoom(Guid id, [FromBody] UpdateMeetingRoomDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var updated = await _roomService.UpdateAsync(dto);
        return Ok(updated);
    }

    [HttpDelete("rooms/{id:guid}")]
    [Authorize(Policy = HrPermissions.CompanyAdminPolicy)]
    public async Task<IActionResult> DeleteMeetingRoom(Guid id)
    {
        await _roomService.DeleteAsync(id);
        return NoContent();
    }

    #endregion

    #region Room Bookings

    [HttpGet("bookings")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<ActionResult<IEnumerable<RoomBookingDto>>> GetBookings()
        => Ok(await _bookingService.GetAllAsync());

    [HttpGet("bookings/paged")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<ActionResult<PagedResult<RoomBookingDto>>> GetBookingsPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20)
        => Ok(await _bookingService.GetPagedAsync(pageNumber, pageSize));

    [HttpGet("bookings/{id:guid}")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<ActionResult<RoomBookingDto>> GetBooking(Guid id)
        => Ok(await _bookingService.GetByIdAsync(id));

    [HttpGet("bookings/room/{roomId:guid}")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<ActionResult<IEnumerable<RoomBookingSummaryDto>>> GetBookingsByRoom(Guid roomId)
        => Ok(await _bookingService.GetByRoomIdAsync(roomId));

    [HttpGet("bookings/booker/{bookedById:guid}")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<ActionResult<IEnumerable<RoomBookingSummaryDto>>> GetBookingsByBooker(Guid bookedById)
        => Ok(await _bookingService.GetByBookerAsync(bookedById));

    [HttpGet("bookings/range")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<ActionResult<IEnumerable<RoomBookingSummaryDto>>> GetBookingsByDateRange(
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate)
        => Ok(await _bookingService.GetByDateRangeAsync(startDate, endDate));

    [HttpGet("bookings/status/{status}")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<ActionResult<IEnumerable<RoomBookingSummaryDto>>> GetBookingsByStatus(BookingStatus status)
        => Ok(await _bookingService.GetByStatusAsync(status));

    [HttpGet("bookings/pending-approvals")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<ActionResult<IEnumerable<RoomBookingSummaryDto>>> GetPendingBookingApprovals()
        => Ok(await _bookingService.GetPendingApprovalsAsync());

    [HttpPost("bookings")]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    public async Task<ActionResult<RoomBookingDto>> CreateBooking(
        [FromQuery] Guid bookedById,
        [FromBody] CreateRoomBookingDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var created = await _bookingService.CreateAsync(dto, bookedById);
        return CreatedAtAction(nameof(GetBooking), new { id = created.Id }, created);
    }

    [HttpPut("bookings/{id:guid}")]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    public async Task<ActionResult<RoomBookingDto>> UpdateBooking(Guid id, [FromBody] UpdateRoomBookingDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var updated = await _bookingService.UpdateAsync(dto);
        return Ok(updated);
    }

    [HttpPost("bookings/{id:guid}/approve")]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    public async Task<IActionResult> ApproveBooking(Guid id, [FromQuery] Guid approvedById)
    {
        await _bookingService.ApproveBookingAsync(id, approvedById);
        return Ok(new { message = "Booking approved" });
    }

    [HttpPost("bookings/{id:guid}/cancel")]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    public async Task<IActionResult> CancelBooking(Guid id, [FromBody] CancelRoomBookingDto dto)
    {
        dto.BookingId = id;
        await _bookingService.CancelBookingAsync(dto);
        return Ok(new { message = "Booking cancelled" });
    }

    [HttpDelete("bookings/{id:guid}")]
    [Authorize(Policy = HrPermissions.CompanyAdminPolicy)]
    public async Task<IActionResult> DeleteBooking(Guid id)
    {
        await _bookingService.DeleteAsync(id);
        return NoContent();
    }

    #endregion

    #region Company Milestones

    [HttpGet("milestones")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<ActionResult<IEnumerable<CompanyMilestoneDto>>> GetMilestones()
        => Ok(await _milestoneService.GetAllAsync());

    [HttpGet("milestones/paged")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<ActionResult<PagedResult<CompanyMilestoneDto>>> GetMilestonesPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20)
        => Ok(await _milestoneService.GetPagedAsync(pageNumber, pageSize));

    [HttpGet("milestones/{id:guid}")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<ActionResult<CompanyMilestoneDto>> GetMilestone(Guid id)
        => Ok(await _milestoneService.GetByIdAsync(id));

    [HttpGet("milestones/category/{category}")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<ActionResult<IEnumerable<CompanyMilestoneDto>>> GetMilestonesByCategory(MilestoneCategory category)
        => Ok(await _milestoneService.GetByCategoryAsync(category));

    [HttpGet("milestones/range")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<ActionResult<IEnumerable<CompanyMilestoneDto>>> GetMilestonesByRange(
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate)
        => Ok(await _milestoneService.GetByDateRangeAsync(startDate, endDate));

    [HttpGet("milestones/upcoming")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<ActionResult<IEnumerable<CompanyMilestoneDto>>> GetUpcomingMilestones([FromQuery] int daysAhead = 90)
        => Ok(await _milestoneService.GetUpcomingMilestonesAsync(daysAhead));

    [HttpPost("milestones")]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    public async Task<ActionResult<CompanyMilestoneDto>> CreateMilestone([FromBody] CreateCompanyMilestoneDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var created = await _milestoneService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetMilestone), new { id = created.Id }, created);
    }

    [HttpPut("milestones/{id:guid}")]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    public async Task<ActionResult<CompanyMilestoneDto>> UpdateMilestone(Guid id, [FromBody] UpdateCompanyMilestoneDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var updated = await _milestoneService.UpdateAsync(dto);
        return Ok(updated);
    }

    [HttpDelete("milestones/{id:guid}")]
    [Authorize(Policy = HrPermissions.CompanyAdminPolicy)]
    public async Task<IActionResult> DeleteMilestone(Guid id)
    {
        await _milestoneService.DeleteAsync(id);
        return NoContent();
    }

    #endregion

    #region Business Closures

    [HttpGet("closures")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<ActionResult<IEnumerable<BusinessClosureDto>>> GetClosures()
        => Ok(await _closureService.GetAllAsync());

    [HttpGet("closures/paged")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<ActionResult<PagedResult<BusinessClosureDto>>> GetClosuresPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20)
        => Ok(await _closureService.GetPagedAsync(pageNumber, pageSize));

    [HttpGet("closures/{id:guid}")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<ActionResult<BusinessClosureDto>> GetClosure(Guid id)
        => Ok(await _closureService.GetByIdAsync(id));

    [HttpGet("closures/range")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<ActionResult<IEnumerable<BusinessClosureDto>>> GetClosuresByRange(
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate)
        => Ok(await _closureService.GetByDateRangeAsync(startDate, endDate));

    [HttpGet("closures/type/{type}")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<ActionResult<IEnumerable<BusinessClosureDto>>> GetClosuresByType(ClosureType type)
        => Ok(await _closureService.GetByTypeAsync(type));

    [HttpGet("closures/station/{stationId:guid}")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<ActionResult<IEnumerable<BusinessClosureDto>>> GetClosuresByStation(Guid stationId)
        => Ok(await _closureService.GetByStationAsync(stationId));

    [HttpGet("closures/upcoming")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<ActionResult<IEnumerable<BusinessClosureDto>>> GetUpcomingClosures([FromQuery] int daysAhead = 30)
        => Ok(await _closureService.GetUpcomingClosuresAsync(daysAhead));

    [HttpGet("closures/is-closure-date")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<ActionResult<bool>> IsClosureDate(
        [FromQuery] DateTime date,
        [FromQuery] Guid? stationId = null,
        [FromQuery] Guid? departmentId = null)
        => Ok(await _closureService.IsClosureDateAsync(date, stationId, departmentId));

    [HttpPost("closures")]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    public async Task<ActionResult<BusinessClosureDto>> CreateClosure(
        [FromQuery] Guid announcedById,
        [FromBody] CreateBusinessClosureDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var created = await _closureService.CreateAsync(dto, announcedById);
        return CreatedAtAction(nameof(GetClosure), new { id = created.Id }, created);
    }

    [HttpPut("closures/{id:guid}")]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    public async Task<ActionResult<BusinessClosureDto>> UpdateClosure(Guid id, [FromBody] UpdateBusinessClosureDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var updated = await _closureService.UpdateAsync(dto);
        return Ok(updated);
    }

    [HttpDelete("closures/{id:guid}")]
    [Authorize(Policy = HrPermissions.CompanyAdminPolicy)]
    public async Task<IActionResult> DeleteClosure(Guid id)
    {
        await _closureService.DeleteAsync(id);
        return NoContent();
    }

    #endregion

    #region Fiscal Years

    [HttpGet("fiscal-years")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<ActionResult<IEnumerable<FiscalYearDto>>> GetFiscalYears()
        => Ok(await _fiscalYearService.GetAllAsync());

    [HttpGet("fiscal-years/paged")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<ActionResult<PagedResult<FiscalYearDto>>> GetFiscalYearsPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20)
        => Ok(await _fiscalYearService.GetPagedAsync(pageNumber, pageSize));

    [HttpGet("fiscal-years/{id:guid}")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<ActionResult<FiscalYearDto>> GetFiscalYear(Guid id)
        => Ok(await _fiscalYearService.GetByIdAsync(id));

    [HttpGet("fiscal-years/{id:guid}/details")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<ActionResult<FiscalYearDetailDto>> GetFiscalYearDetail(Guid id)
        => Ok(await _fiscalYearService.GetDetailByIdAsync(id));

    [HttpGet("fiscal-years/by-year/{year:int}")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<ActionResult<FiscalYearDto>> GetFiscalYearByYear(int year)
        => Ok(await _fiscalYearService.GetByYearAsync(year));

    [HttpGet("fiscal-years/current")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<ActionResult<FiscalYearDto>> GetCurrentFiscalYear()
        => Ok(await _fiscalYearService.GetCurrentFiscalYearAsync());

    [HttpGet("fiscal-years/status/{status}")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<ActionResult<IEnumerable<FiscalYearDto>>> GetFiscalYearsByStatus(FiscalYearStatus status)
        => Ok(await _fiscalYearService.GetByStatusAsync(status));

    [HttpPost("fiscal-years")]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    public async Task<ActionResult<FiscalYearDto>> CreateFiscalYear([FromBody] CreateFiscalYearDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var created = await _fiscalYearService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetFiscalYear), new { id = created.Id }, created);
    }

    [HttpPut("fiscal-years/{id:guid}")]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    public async Task<ActionResult<FiscalYearDto>> UpdateFiscalYear(Guid id, [FromBody] UpdateFiscalYearDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var updated = await _fiscalYearService.UpdateAsync(dto);
        return Ok(updated);
    }

    [HttpPost("fiscal-years/{id:guid}/set-current")]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    public async Task<IActionResult> SetCurrentFiscalYear(Guid id)
    {
        await _fiscalYearService.SetAsCurrentAsync(id);
        return Ok(new { message = "Fiscal year set as current" });
    }

    [HttpDelete("fiscal-years/{id:guid}")]
    [Authorize(Policy = HrPermissions.CompanyAdminPolicy)]
    public async Task<IActionResult> DeleteFiscalYear(Guid id)
    {
        await _fiscalYearService.DeleteAsync(id);
        return NoContent();
    }

    #region Fiscal Periods

    [HttpPost("fiscal-years/{fiscalYearId:guid}/periods")]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    public async Task<ActionResult<FiscalPeriodDto>> AddFiscalPeriod(Guid fiscalYearId, [FromBody] CreateFiscalPeriodDto dto)
    {
        dto.FiscalYearId = fiscalYearId;
        var created = await _fiscalYearService.AddPeriodAsync(dto);
        return CreatedAtAction(nameof(GetFiscalPeriods), new { fiscalYearId }, created);
    }

    [HttpGet("fiscal-years/{fiscalYearId:guid}/periods")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<ActionResult<IEnumerable<FiscalPeriodDto>>> GetFiscalPeriods(Guid fiscalYearId)
        => Ok(await _fiscalYearService.GetPeriodsAsync(fiscalYearId));

    [HttpPut("periods/{id:guid}")]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    public async Task<ActionResult<FiscalPeriodDto>> UpdateFiscalPeriod(Guid id, [FromBody] UpdateFiscalPeriodDto dto)
    {
        if (id != dto.Id) return BadRequest("ID mismatch");
        var updated = await _fiscalYearService.UpdatePeriodAsync(dto);
        return Ok(updated);
    }

    [HttpPost("periods/{id:guid}/close")]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    public async Task<IActionResult> CloseFiscalPeriod(Guid id, [FromBody] CloseFiscalPeriodDto dto)
    {
        dto.PeriodId = id;
        await _fiscalYearService.ClosePeriodAsync(dto);
        return Ok(new { message = "Fiscal period closed" });
    }

    [HttpDelete("periods/{id:guid}")]
    [Authorize(Policy = HrPermissions.CompanyAdminPolicy)]
    public async Task<IActionResult> DeleteFiscalPeriod(Guid id)
    {
        await _fiscalYearService.DeletePeriodAsync(id);
        return NoContent();
    }

    #endregion

    #endregion
}

