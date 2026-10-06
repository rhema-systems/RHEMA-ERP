using ErpSystem.Api.Filters;
using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using ErpSystem.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Controllers.HR;

/// <summary>
/// The company schedule — events with participants, attendance, attachments and tasks; meeting
/// rooms and their bookings; company milestones; business closures; fiscal years and periods.
/// </summary>
/// <remarks>
/// <para><b>W3 slice 14.</b> All 90 actions carried a bare <c>[Authorize]</c> and no screen has
/// ever called any of them — the surface was dormant since the port. Gated verb-mechanically on
/// <c>HR.Company.*</c>: reads → Read, writes and decisions → Write, deletes → Admin.</para>
///
/// <para><b>Actor resolution, closed 2026-08-28 when the schedule screens were built.</b> The five
/// actor ids (<c>organizerId</c>, <c>approvedById</c>, <c>markedById</c>, <c>bookedById</c>,
/// <c>announcedById</c>) used to arrive as query parameters, which made every one of them
/// act-as-anyone the moment a screen existed. They now come from the caller's token via
/// <see cref="HrControllerBase.TryGetEmployeeWriteContext"/> and are no longer accepted from the
/// client. These are domain actor fields holding <c>Employee</c> ids, not audit fields, so the
/// employee-linked overload is the correct one — an unlinked administrative account genuinely
/// cannot organise an event or approve a booking.</para>
///
/// <para><b>Still not drawn here:</b> a self-service surface. <c>participants/respond</c> takes a
/// <c>ParticipantId</c> and stays on the HR-desk <c>Write</c> policy, so it is "HR records the
/// response", not "the invitee answers". A genuine self-service invitation reply needs a
/// self-or-permission check against the participant's own employee id first.</para>
/// </remarks>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "InternalOnly")]
[CompanyScheduleBusinessRules]
public class CompanyScheduleController : HrControllerBase
{
    private readonly ICompanyEventService _eventService;
    private readonly ErpSystem.Core.Services.HR.CompanySchedule.IPersonalScheduleService _personalSchedule;
    private readonly IMeetingRoomService _roomService;
    private readonly IRoomBookingService _bookingService;
    private readonly ICompanyMilestoneService _milestoneService;
    private readonly IBusinessClosureService _closureService;
    private readonly IFiscalYearService _fiscalYearService;
    // Lane 2h (C-18): event attachments through the upload gate, and their download.
    private readonly ErpSystem.Api.Services.HR.IHrControlledDocumentService _hrDocuments;
    private readonly ErpSystem.Core.Interfaces.DocumentManagement.ICentralDocumentRepositoryFileService _centralDocuments;
    private readonly IFileStorageService _fileStorage;
    private readonly ErpSystem.Data.ApplicationDbContext _db;
    private readonly ILogger<CompanyScheduleController> _logger;

    public CompanyScheduleController(
        ICompanyEventService eventService,
        IMeetingRoomService roomService,
        IRoomBookingService bookingService,
        ICompanyMilestoneService milestoneService,
        IBusinessClosureService closureService,
        IFiscalYearService fiscalYearService,
        ErpSystem.Core.Services.HR.CompanySchedule.IPersonalScheduleService personalSchedule,
        ICurrentUserService currentUser,
        ErpSystem.Api.Services.HR.IHrControlledDocumentService hrDocuments,
        ErpSystem.Core.Interfaces.DocumentManagement.ICentralDocumentRepositoryFileService centralDocuments,
        IFileStorageService fileStorage,
        ErpSystem.Data.ApplicationDbContext db,
        ILogger<CompanyScheduleController> logger)
        : base(currentUser)
    {
        _eventService = eventService;
        _roomService = roomService;
        _bookingService = bookingService;
        _milestoneService = milestoneService;
        _closureService = closureService;
        _fiscalYearService = fiscalYearService;
        _personalSchedule = personalSchedule;
        _hrDocuments = hrDocuments;
        _centralDocuments = centralDocuments;
        _fileStorage = fileStorage;
        _db = db;
        _logger = logger;
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

    /// <summary>
    /// The events register (lane 2g-1, C-10…C-13): text, status, category, site, unit, organiser, series and a date range
    /// (by overlap), sorted and paged on the server.
    /// </summary>
    [HttpGet("events/search")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<ActionResult<PagedResult<CompanyEventDto>>> SearchEvents([FromQuery] CompanyEventSearchDto search, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Ok(await _eventService.SearchAsync(search, ct));
    }

    /// <summary>
    /// Every event the same search finds, as a CSV (lane 2g-1, C-12) — on the register's own read permission (the user's
    /// ruling), as the leave register's export is.
    /// </summary>
    [HttpGet("events/export")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<IActionResult> ExportEvents([FromQuery] CompanyEventSearchDto search, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return File(await _eventService.ExportCsvAsync(search, ct), "text/csv", $"company-events-{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    /// <summary>The landing page in one read (lane 2g-1, D-9): the next 30 days' events, the bookings awaiting approval,
    /// the next 60 days' closures and the next 90 days' milestones.</summary>
    [HttpGet("dashboard")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<ActionResult<CompanyScheduleDashboardDto>> GetDashboard(CancellationToken ct)
        => Ok(new CompanyScheduleDashboardDto
        {
            UpcomingEvents = (await _eventService.GetUpcomingEventsAsync(30, ct)).ToList(),
            PendingBookings = (await _bookingService.GetPendingApprovalsAsync(ct)).ToList(),
            UpcomingClosures = (await _closureService.GetUpcomingClosuresAsync(60, ct)).ToList(),
            UpcomingMilestones = (await _milestoneService.GetUpcomingMilestonesAsync(90, ct)).ToList(),
        });

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

    /// <summary>
    /// Who an event with this scope and visibility would be for, and how many people that is (lane 2c,
    /// D-16) — the line the event form shows before saving, with a warning when it reaches nobody.
    /// </summary>
    [HttpGet("events/audience-preview")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<ActionResult<EventAudiencePreviewDto>> PreviewEventAudience(
        [FromQuery] ParticipantScope scope,
        [FromQuery] EventVisibility visibility,
        [FromQuery] Guid? organizationUnitId)
        => Ok(await _eventService.PreviewAudienceAsync(scope, visibility, organizationUnitId));

    /// <summary>
    /// The live events these dates, this audience and this site would clash with (lane 2g-2, C-15), each refused or
    /// warned of — what the form shows before saving. Saves nothing.
    /// </summary>
    [HttpGet("events/clashes")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<ActionResult<IReadOnlyList<EventClashDto>>> FindEventClashes([FromQuery] EventClashQueryDto query, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Ok(await _eventService.FindClashesAsync(query, ct));
    }

    /// <summary>What announcing the event on the intranet would say, and to how many; saves nothing (lane 2c).</summary>
    [HttpGet("events/{id:guid}/announcement")]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    public async Task<ActionResult<EventAnnouncementPreviewDto>> PreviewEventAnnouncement(Guid id)
        => Ok(await _eventService.PreviewAnnouncementAsync(id));

    /// <summary>
    /// Announces the event on the intranet to its audience — HR's click, never a save's side effect
    /// (lane 2c; the closures' rule, L1-1). 422 with the reason when it cannot be announced.
    /// </summary>
    [HttpPost("events/{id:guid}/announce")]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    public async Task<ActionResult<HrAnnouncementDto>> AnnounceEvent(Guid id)
    {
        var ctx = TryGetEmployeeWriteContext(out _, out _, out var publisherId, "Announcing an event");
        if (ctx != null) return ctx;
        return Ok(await _eventService.AnnounceAsync(id, publisherId));
    }

    /// <summary>Events for one organisation unit (lane 2a; replaces the retired department read, D-5).</summary>
    [HttpGet("events/unit/{organizationUnitId:guid}")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<ActionResult<IEnumerable<CompanyEventSummaryDto>>> GetEventsByOrganizationUnit(Guid organizationUnitId)
        => Ok(await _eventService.GetByOrganizationUnitAsync(organizationUnitId));

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

    /// <summary>
    /// Creates an event. The organiser is the one chosen on the form, or the caller (D-11); the caller is
    /// recorded as the creator either way, so an account with no employee record still cannot create one.
    /// </summary>
    [HttpPost("events")]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    public async Task<ActionResult<CompanyEventDto>> CreateEvent([FromBody] CreateCompanyEventDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = TryGetEmployeeWriteContext(out _, out _, out var callerEmployeeId, "Organising an event");
        if (ctx != null) return ctx;

        var created = await _eventService.CreateAsync(dto, callerEmployeeId);
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

    /// <summary>
    /// Approves an event awaiting approval (lane 2b, D-10): the engine decides whether the caller may — the
    /// approver its definition names — and with no approval under way, <c>HR.Company.Approve</c>. The
    /// organiser may not approve their own. The body is optional: <c>{ comments }</c>.
    /// </summary>
    [HttpPost("events/{id:guid}/approve")]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    public async Task<IActionResult> ApproveEvent(
        Guid id,
        [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] EventDecisionDto? dto)
    {
        var ctx = TryGetEmployeeWriteContext(out _, out _, out var approvedById, "Approving an event");
        if (ctx != null) return ctx;

        await _eventService.ApproveEventAsync(id, approvedById, dto?.Comments);
        return Ok(new { message = "Event approved" });
    }

    /// <summary>
    /// Rejects an event awaiting approval (lane 2b): it is cancelled with the reason, which everybody
    /// invited is told, and its room bookings are cancelled with it. Body: <c>{ comments }</c>, required.
    /// </summary>
    [HttpPost("events/{id:guid}/reject")]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    public async Task<ActionResult<CompanyEventChangeDto>> RejectEvent(Guid id, [FromBody] EventDecisionDto dto)
    {
        var ctx = TryGetEmployeeWriteContext(out _, out _, out var rejectedById, "Rejecting an event");
        if (ctx != null) return ctx;

        return Ok(await _eventService.RejectEventAsync(id, rejectedById, dto.Comments ?? string.Empty));
    }

    /// <summary>Cancels the event and its live room bookings, and answers what was cancelled (F-39).</summary>
    [HttpPost("events/{id:guid}/cancel")]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    public async Task<ActionResult<CompanyEventChangeDto>> CancelEvent(Guid id, [FromBody] CancelEventDto dto)
    {
        dto.EventId = id;
        return Ok(await _eventService.CancelEventAsync(dto));
    }

    /// <summary>
    /// Moves the event — its room bookings with it, answers back to awaiting a reply, an approval cleared —
    /// and answers what changed (F-38, C-7).
    /// </summary>
    [HttpPost("events/{id:guid}/reschedule")]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    public async Task<ActionResult<CompanyEventChangeDto>> RescheduleEvent(Guid id, [FromBody] RescheduleEventDto dto)
    {
        dto.EventId = id;
        return Ok(await _eventService.RescheduleEventAsync(dto));
    }

    [HttpPost("events/{id:guid}/complete")]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    public async Task<IActionResult> CompleteEvent(Guid id, [FromBody] CompleteEventDto dto)
    {
        dto.EventId = id;
        await _eventService.CompleteEventAsync(dto);
        return Ok(new { message = "Event completed" });
    }

    /// <summary>Deletes the event after cancelling its live room bookings; answers 200 with the bookings cancelled (F-39).</summary>
    [HttpDelete("events/{id:guid}")]
    [Authorize(Policy = HrPermissions.CompanyAdminPolicy)]
    public async Task<ActionResult<CompanyEventChangeDto>> DeleteEvent(Guid id)
        => Ok(await _eventService.DeleteAsync(id));

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

    /// <summary>
    /// Records a guest's answer. Only accepted, declined or tentative, from a guest of this event (F-11). On a series,
    /// <c>scope</c> records it for this and following dates, or every date (lane 2f-2a); the answer lists them.
    /// </summary>
    [HttpPost("events/{eventId:guid}/participants/respond")]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    public async Task<ActionResult<EventSeriesGuestResultDto>> RespondToInvitation(Guid eventId, [FromBody] RespondToEventInvitationDto dto)
        => Ok(await _eventService.RespondToInvitationAsync(eventId, dto));

    /// <summary>
    /// Corrects a guest: their role, whether they are required, their needs, and an outside guest's name,
    /// address and organisation (lane 2d, C-22).
    /// </summary>
    [HttpPut("participants/{participantId:guid}")]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    public async Task<ActionResult<EventParticipantDto>> UpdateParticipant(Guid participantId, [FromBody] UpdateEventParticipantDto dto)
    {
        if (participantId != dto.Id) return BadRequest("ID mismatch");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Ok(await _eventService.UpdateParticipantAsync(dto));
    }

    // =========================================================================
    // THE DIARY  (round 4, D5)
    // =========================================================================

    /// <summary>
    /// Everything the signed-in employee is committed to between two dates — their events, room
    /// bookings, interview panels, training, leave and travel, in one place.
    /// </summary>
    /// <remarks>
    /// ⚠ <b>Self-service, and self only.</b> The employee id comes from the token, never from the
    /// caller: a query parameter here would let any signed-in user read a colleague's leave and
    /// travel, which is the exposure the panel-availability endpoint is HR-gated to prevent.
    /// Reading somebody else's diary is <c>team</c> below, which is gated on the unit.
    /// </remarks>
    [HttpGet("my-schedule")]
    public async Task<ActionResult<PersonalScheduleDto>> GetMySchedule(
        [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken ct)
    {
        // ⚠ `CurrentUser` from HrControllerBase — this controller has no `_currentUser` field of its
        // own, it hands the service to the base.
        var employeeId = CurrentUser.EmployeeId;
        if (employeeId is null)
            return BadRequest("Your user account is not linked to an employee record.");

        return Ok(await _personalSchedule.GetForEmployeeAsync(employeeId.Value, from, to, ct));
    }

    /// <summary>
    /// Everything an organisation unit and its subtree are committed to — what a head needs before
    /// scheduling something for their team.
    /// </summary>
    /// <remarks>
    /// ⚠ Gated on the company-schedule WRITE policy rather than Read: this exposes other people's
    /// leave and travel, which is desk information, not general reading.
    /// </remarks>
    [HttpGet("team-schedule/{organizationUnitId:guid}")]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    public async Task<ActionResult<TeamScheduleDto>> GetTeamSchedule(
        Guid organizationUnitId, [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken ct)
        => Ok(await _personalSchedule.GetForUnitAsync(organizationUnitId, from, to, ct));

    /// <summary>
    /// Chases everybody who has not answered their invitation (round 4, D6) — answering who it was for and who it
    /// reached (lane 2e-2). It counted attempts as <c>sent</c>.
    /// </summary>
    [HttpPost("events/{eventId:guid}/rsvp-reminders")]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    public async Task<ActionResult<CompanyEventNoticeResultDto>> SendRsvpReminders(Guid eventId, CancellationToken ct)
        => Ok(await _eventService.SendRsvpRemindersAsync(eventId, ct));

    /// <summary>Reminds every participant who has not declined that the event is coming (D6), with who it reached (lane 2e-2).</summary>
    [HttpPost("events/{eventId:guid}/reminders")]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    public async Task<ActionResult<CompanyEventNoticeResultDto>> SendEventReminders(Guid eventId, CancellationToken ct)
        => Ok(await _eventService.SendEventRemindersAsync(eventId, ct));

    /// <summary>Extends the event's series on its rule (lane 2f-1): either how many more occurrences, or until a date.</summary>
    [HttpPost("events/{eventId:guid}/series/extend")]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    public async Task<ActionResult<EventSeriesResultDto>> ExtendSeries(Guid eventId, [FromBody] ExtendEventSeriesDto dto, CancellationToken ct)
        => Ok(await _eventService.ExtendSeriesAsync(eventId, dto, ct));

    /// <summary>Sends again the invitations that reached nobody (lane 2e-2), with who they reached this time.</summary>
    [HttpPost("events/{eventId:guid}/invitations/send")]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    public async Task<ActionResult<CompanyEventNoticeResultDto>> SendUndeliveredInvitations(Guid eventId, CancellationToken ct)
        => Ok(await _eventService.SendUndeliveredInvitationsAsync(eventId, ct));

    /// <summary>
    /// Runs the reminder sweep now for the caller's tenant (round 4, lane N-b2) — exactly the code the
    /// hourly host runs, so HR can send what is due without waiting, and a test can drive it.
    /// </summary>
    [HttpPost("reminders/run")]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    public async Task<ActionResult<CompanyScheduleReminderRunDto>> RunDueReminders(CancellationToken ct)
    {
        var run = await _eventService.RunDueRemindersNowAsync(ct);
        // Lane 3b-2: the sweep's booking half — lapses and completions — as the hourly run does it.
        var swept = await _bookingService.SweepNowAsync(ct);
        run.BookingsLapsed = swept.Lapsed;
        run.BookingsCompleted = swept.Completed;
        return Ok(run);
    }

    /// <summary>
    /// Uninvites a guest — organiser work, on Write (lane 2d); it needed Admin. On a series, <c>?scope=</c>
    /// ThisAndFollowing or WholeSeries takes them off those dates still to come (lane 2f-2a); the answer lists them.
    /// </summary>
    [HttpDelete("participants/{participantId:guid}")]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    public async Task<ActionResult<EventSeriesGuestResultDto>> RemoveParticipant(
        Guid participantId, [FromQuery] SeriesScope scope = SeriesScope.ThisOccurrence)
        => Ok(await _eventService.RemoveParticipantAsync(participantId, scope));

    #endregion

    #region Attendance

    [HttpPost("events/{eventId:guid}/attendance")]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    public async Task<ActionResult<EventAttendanceDto>> MarkAttendance(Guid eventId, [FromBody] MarkEventAttendanceDto dto)
    {
        dto.EventId = eventId;
        var ctx = TryGetEmployeeWriteContext(out _, out _, out var markedById, "Marking event attendance");
        if (ctx != null) return ctx;

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

    /// <summary>Removes a row from the event's register — a correction, on Write, not an Admin destruction (C-21, D-9).</summary>
    [HttpDelete("events/{eventId:guid}/attendance/{attendanceId:guid}")]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    public async Task<IActionResult> RemoveAttendance(Guid eventId, Guid attendanceId)
    {
        await _eventService.RemoveAttendanceAsync(eventId, attendanceId);
        return NoContent();
    }

    #endregion

    #region Event Attachments

    /// <summary>
    /// Attaches a file to an event — its agenda, minutes, slides or a resource — through the upload gate: scanned, stored
    /// and registered (lane 2h, C-18). It took a file name and a path in JSON, and stored no file (F-54).
    /// </summary>
    /// <remarks>
    /// ⚠ The event is resolved — and a cancelled one refused — BEFORE a byte is stored: the gate cannot roll a stored
    /// file back once its registration has run (the asset photographs' lesson).
    /// </remarks>
    [HttpPost("events/{eventId:guid}/attachments")]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<IActionResult> AddEventAttachment(
        Guid eventId,
        IFormFile file,
        [FromForm] EventAttachmentType type,
        [FromForm] string? description,
        CancellationToken ct)
    {
        var ev = await _eventService.GetByIdAsync(eventId, ct);
        if (ErpSystem.Core.Services.HR.CompanyEventRules.RefuseAttaching(ev.EventName, ev.IsCancelled, ev.Status, type) is { } refusal)
            throw new InvalidOperationException(refusal);

        return await HrAttachmentUpload.ExecuteAsync(
            this, _hrDocuments, CurrentUser, _logger, file,
            sourceEntityType: "CompanyEvent",
            sourceRecordId: eventId,
            sourceLabel: $"Company event {ev.EventNumber}",
            documentType: "CompanyEventAttachment",
            description: description,
            persist: (uploadedById, document) => _eventService.AddUploadedAttachmentAsync(
                eventId, type, description, uploadedById,
                document.OriginalFileName, document.FilePath, document.FileSize, document.FileUploadRecordId,
                document.DocumentRecordId, document.DocumentVersionId, ct),
            ct,
            category: ControlledFileUploadCategories.HrCompanyScheduleAttachments);
    }

    [HttpGet("events/{eventId:guid}/attachments")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<ActionResult<IEnumerable<EventAttachmentDto>>> GetEventAttachments(Guid eventId)
        => Ok(await _eventService.GetAttachmentsAsync(eventId));

    /// <summary>
    /// Downloads an event's file (lane 2h, C-18), on the register's read permission. A row from before the gate is a
    /// reference with no file stored (F-54): it answers 404, saying so.
    /// </summary>
    [HttpGet("attachments/{attachmentId:guid}/download")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<IActionResult> DownloadEventAttachment(Guid attachmentId, CancellationToken ct)
    {
        if (CurrentUser.TenantId is not Guid tenantId)
            return Unauthorized("Tenant context could not be resolved");

        // ⚠ The entitlement check is this endpoint's: the download helper performs none. The service applies the tenant.
        var attachment = await _eventService.GetAttachmentAsync(attachmentId, ct);
        if (!attachment.HasFile)
            return NotFound(new { message = "Reference only — no file stored. It was recorded before files were uploaded here." });

        // ⚠ No legacy path: a path a caller once typed is not a file this server stored (F-54).
        var stored = await _db.EventAttachments.AsNoTracking()
            .Where(a => a.Id == attachmentId && a.TenantId == tenantId)
            .Select(a => new { a.DocumentRecordId, a.DocumentVersionId, a.FileUploadRecordId })
            .FirstAsync(ct);
        return await HrDocumentDownload.ServeAsync(
            this, _centralDocuments, _fileStorage, _db, tenantId,
            stored.DocumentRecordId, stored.DocumentVersionId, stored.FileUploadRecordId,
            legacyPath: null, attachment.FileName, fallbackContentType: null,
            inline: false, ct);
    }

    /// <summary>Removes an event's file — organiser work, on Write (lane 2h, the user's ruling); it was Admin.</summary>
    [HttpDelete("attachments/{attachmentId:guid}")]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
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

    [HttpGet("rooms/location/{locationId:guid}")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<ActionResult<IEnumerable<MeetingRoomSummaryDto>>> GetRoomsByLocation(Guid locationId)
        => Ok(await _roomService.GetByLocationAsync(locationId));

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

    /// <summary>
    /// Refused while the room has any booking on record — deactivate it instead (D-18, F-49: deleting one hid its
    /// history from the register).
    /// </summary>
    [HttpDelete("rooms/{id:guid}")]
    [Authorize(Policy = HrPermissions.CompanyAdminPolicy)]
    public async Task<IActionResult> DeleteMeetingRoom(Guid id)
    {
        await _roomService.DeleteAsync(id);
        return NoContent();
    }

    /// <summary>
    /// What retiring the room would touch (D-18, lane 3a): its bookings still to come, which deactivating it offers to
    /// cancel, and whether it can be deleted at all. The Rooms screens ask it before deactivating or deleting.
    /// </summary>
    [HttpGet("rooms/{id:guid}/retirement")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<ActionResult<RoomRetirementDto>> GetRoomRetirement(Guid id, CancellationToken ct)
        => Ok(await _roomService.GetRetirementAsync(id, ct));

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

    /// <summary>The bookings register (lane 2g-1, C-25): text, status, room and a date range, sorted and paged on the server.</summary>
    [HttpGet("bookings/search")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<ActionResult<PagedResult<RoomBookingDto>>> SearchBookings([FromQuery] RoomBookingSearchDto search, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return Ok(await _bookingService.SearchAsync(search, ct));
    }

    /// <summary>Every booking the same search finds, as a CSV (lane 2g-1, C-25), on the register's read permission.</summary>
    [HttpGet("bookings/export")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<IActionResult> ExportBookings([FromQuery] RoomBookingSearchDto search, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        return File(await _bookingService.ExportCsvAsync(search, ct), "text/csv", $"room-bookings-{DateTime.UtcNow:yyyyMMdd}.csv");
    }

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
    public async Task<ActionResult<RoomBookingDto>> CreateBooking([FromBody] CreateRoomBookingDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = TryGetEmployeeWriteContext(out _, out _, out var bookedById, "Booking a room");
        if (ctx != null) return ctx;

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
    public async Task<IActionResult> ApproveBooking(Guid id)
    {
        var ctx = TryGetEmployeeWriteContext(out _, out _, out var approvedById, "Approving a room booking");
        if (ctx != null) return ctx;

        await _bookingService.ApproveBookingAsync(id, approvedById);
        return Ok(new { message = "Booking approved" });
    }

    /// <summary>
    /// Not approved (lane 3b-1, D-10): the booking is cancelled, "Not approved: …", and its booker told why. Decided through
    /// the engine when an approval is under way, the approve tier otherwise; never by the booker.
    /// </summary>
    /// <summary>
    /// Marks a confirmed booking whose start has passed a no-show — held and not used — for good; its booker told (lane
    /// 3b-2, the user's ruling).
    /// </summary>
    [HttpPost("bookings/{id:guid}/no-show")]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    public async Task<ActionResult<RoomBookingDto>> MarkBookingNoShow(Guid id, CancellationToken ct)
        => Ok(await _bookingService.MarkNoShowAsync(id, ct));

    [HttpPost("bookings/{id:guid}/reject")]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    public async Task<ActionResult<RoomBookingDto>> RejectBooking(Guid id, [FromBody] EventDecisionDto dto)
    {
        var ctx = TryGetEmployeeWriteContext(out _, out _, out var rejectedById, "Rejecting a room booking");
        if (ctx != null) return ctx;

        return Ok(await _bookingService.RejectBookingAsync(id, rejectedById, dto.Comments ?? string.Empty));
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

    [HttpGet("closures/location/{locationId:guid}")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<ActionResult<IEnumerable<BusinessClosureDto>>> GetClosuresByLocation(Guid locationId)
        => Ok(await _closureService.GetByLocationAsync(locationId));

    [HttpGet("closures/upcoming")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<ActionResult<IEnumerable<BusinessClosureDto>>> GetUpcomingClosures([FromQuery] int daysAhead = 30)
        => Ok(await _closureService.GetUpcomingClosuresAsync(daysAhead));

    /// <summary>
    /// Whether a closure that is a day off covers the date for someone at this site and in this
    /// organisation unit (company-schedule final closure, lane 1). With neither, only a company-wide
    /// closure answers true; a partial closure never does — its day is still worked.
    /// </summary>
    [HttpGet("closures/is-closure-date")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<ActionResult<bool>> IsClosureDate(
        [FromQuery] DateTime date,
        [FromQuery] Guid? locationId = null,
        [FromQuery] Guid? organizationUnitId = null)
        => Ok(await _closureService.IsClosureDateAsync(date, locationId, organizationUnitId));

    [HttpPost("closures")]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    public async Task<ActionResult<BusinessClosureDto>> CreateClosure([FromBody] CreateBusinessClosureDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var ctx = TryGetEmployeeWriteContext(out _, out _, out var announcedById, "Announcing a business closure");
        if (ctx != null) return ctx;

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

    /// <summary>
    /// Deletes a closure, and answers what recounting the leave it covered did (lane 1c, D-15a) —
    /// 200 with the recount rather than 204, because the person deleting should see whose leave changed.
    /// </summary>
    [HttpDelete("closures/{id:guid}")]
    [Authorize(Policy = HrPermissions.CompanyAdminPolicy)]
    public async Task<ActionResult<LeaveRechargeResultDto>> DeleteClosure(Guid id)
        => Ok(await _closureService.DeleteAsync(id));

    /// <summary>
    /// The one-time recount (lane 1c): all granted leave in the current and later leave years, against
    /// the closures and holidays as they stand. For closures recorded before the recount existed. Safe
    /// to run again — leave whose count is right is left alone, and nobody is told anything about it.
    /// </summary>
    /// <remarks>
    /// Company ADMIN: it can change many people's balances at once. <c>?dryRun=true</c> answers the
    /// same list without saving anything or telling anyone — run it first.
    /// </remarks>
    [HttpPost("closures/recharge-leave")]
    [Authorize(Policy = HrPermissions.CompanyAdminPolicy)]
    public async Task<ActionResult<LeaveRechargeResultDto>> RechargeLeave([FromQuery] bool dryRun = false)
        => Ok(await _closureService.RechargeAllOpenLeaveAsync(dryRun));

    /// <summary>
    /// What announcing the closure would say and how many active staff it would reach — the line behind
    /// "Announce to the N staff it covers" (lane 1d, L1-1).
    /// </summary>
    [HttpGet("closures/{id:guid}/announcement")]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    public async Task<ActionResult<ClosureAnnouncementPreviewDto>> PreviewClosureAnnouncement(Guid id)
        => Ok(await _closureService.PreviewAnnouncementAsync(id));

    /// <summary>
    /// Announces the closure to the staff it covers, on HR's click (lane 1d, L1-1): an HR announcement
    /// addressed by the closure's scope, published as the caller. 422 when it covers nobody or is over.
    /// </summary>
    [HttpPost("closures/{id:guid}/announce")]
    [Authorize(Policy = HrPermissions.CompanyWritePolicy)]
    public async Task<ActionResult<HrAnnouncementDto>> AnnounceClosure(Guid id)
    {
        var ctx = TryGetEmployeeWriteContext(out _, out _, out var publisherId, "Announcing a business closure");
        if (ctx != null) return ctx;
        return Ok(await _closureService.AnnounceAsync(id, publisherId));
    }

    /// <summary>
    /// Each employee's closure days, each with its closure and whether staff are paid — the read payroll
    /// is pointed at (lane 1d, D-15c). HR records the pay flag; what an unpaid day is worth is payroll's.
    /// Up to 500 employees and a year at a time; a partial closure only with <c>includePartial</c>.
    /// </summary>
    [HttpGet("closures/employee-days")]
    [Authorize(Policy = HrPermissions.CompanyReadPolicy)]
    public async Task<ActionResult<List<EmployeeClosureDaysDto>>> GetEmployeeClosureDays(
        [FromQuery] List<Guid> employeeIds,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] bool includePartial = false)
        => Ok(await _closureService.GetEmployeeClosureDaysAsync(employeeIds ?? [], from, to, includePartial));

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

