using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

#region Company Event Service

public interface ICompanyEventService
{
    Task<CompanyEventDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CompanyEventDetailDto> GetDetailByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<CompanyEventDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<CompanyEventDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<IEnumerable<CompanyEventSummaryDto>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    Task<IEnumerable<CompanyEventSummaryDto>> GetByOrganizerAsync(Guid organizerId, CancellationToken cancellationToken = default);
    /// <summary>Events for one organisation unit (lane 2a — replaces the retired department read, D-5).</summary>
    Task<IEnumerable<CompanyEventSummaryDto>> GetByOrganizationUnitAsync(Guid organizationUnitId, CancellationToken cancellationToken = default);
    Task<IEnumerable<CompanyEventSummaryDto>> GetByStatusAsync(EventStatus status, CancellationToken cancellationToken = default);
    Task<IEnumerable<CompanyEventSummaryDto>> GetByCategoryAsync(EventCategory category, CancellationToken cancellationToken = default);
    Task<IEnumerable<CompanyEventSummaryDto>> GetUpcomingEventsAsync(int daysAhead = 30, CancellationToken cancellationToken = default);

    /// <summary>The events register: filtered, sorted and paged on the server (lane 2g-1, C-10…C-13).</summary>
    Task<PagedResult<CompanyEventDto>> SearchAsync(CompanyEventSearchDto search, CancellationToken cancellationToken = default);

    /// <summary>Every event the search finds, as a CSV (lane 2g-1, C-12).</summary>
    Task<byte[]> ExportCsvAsync(CompanyEventSearchDto search, CancellationToken cancellationToken = default);

    /// <summary>
    /// The live events an event with these dates, audience and site would clash with (lane 2g-2, C-15) — each refused or
    /// warned of — for the form to show before saving.
    /// </summary>
    Task<IReadOnlyList<EventClashDto>> FindClashesAsync(EventClashQueryDto query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates an event. The organiser is <see cref="CreateCompanyEventDto.OrganizerId"/> when given,
    /// otherwise <paramref name="callerEmployeeId"/>; the caller is recorded as the creator either way (D-11).
    /// </summary>
    Task<CompanyEventDto> CreateAsync(CreateCompanyEventDto createDto, Guid callerEmployeeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Edits an event. A change of dates, times or the all-day switch is a reschedule (F-37): it needs
    /// <see cref="UpdateCompanyEventDto.RescheduleReason"/> and does what <see cref="RescheduleEventAsync"/> does.
    /// </summary>
    Task<CompanyEventDto> UpdateAsync(UpdateCompanyEventDto updateDto, CancellationToken cancellationToken = default);
    /// <summary>
    /// Approves an event awaiting approval — through the workflow engine when one is under way, the
    /// approve tier when none is (lane 2b, D-10). The organiser may not approve it.
    /// </summary>
    Task<bool> ApproveEventAsync(Guid eventId, Guid approvedById, string? comments = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rejects an event awaiting approval: it is cancelled with the reason, its room bookings with it, and
    /// everybody invited is told (lane 2b).
    /// </summary>
    Task<CompanyEventChangeDto> RejectEventAsync(Guid eventId, Guid rejectedById, string reason, CancellationToken cancellationToken = default);

    /// <summary>Who an event with this scope and visibility would be for, and how many (lane 2c, D-16) — for the form, before saving.</summary>
    Task<EventAudiencePreviewDto> PreviewAudienceAsync(ParticipantScope scope, EventVisibility visibility, Guid? organizationUnitId, CancellationToken cancellationToken = default);

    /// <summary>What announcing the event on the intranet would say, and to how many; saves nothing (lane 2c).</summary>
    Task<EventAnnouncementPreviewDto> PreviewAnnouncementAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Announces the event on the intranet to its audience, on HR's click (lane 2c; the closures' rule, L1-1).</summary>
    Task<HrAnnouncementDto> AnnounceAsync(Guid id, Guid publisherEmployeeId, CancellationToken cancellationToken = default);
    Task<CompanyEventChangeDto> CancelEventAsync(CancelEventDto cancelDto, CancellationToken cancellationToken = default);
    Task<CompanyEventChangeDto> RescheduleEventAsync(RescheduleEventDto rescheduleDto, CancellationToken cancellationToken = default);
    Task<bool> CompleteEventAsync(CompleteEventDto completeDto, CancellationToken cancellationToken = default);
    Task<CompanyEventChangeDto> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    // Participant operations
    /// <summary>
    /// Emails everybody who has not yet answered their invitation (round 4, D6).
    /// </summary>
    /// <remarks>
    /// <para>⚠ Only the UNANSWERED. Somebody who has already accepted or declined has done what was
    /// asked, and chasing them reads as the system not listening.</para>
    ///
    /// <para>Sent by hand from the event screen, and — since round 4, lane N-b2 — by the reminder
    /// sweep ahead of the RSVP deadline (<see cref="SendDueRemindersAsync"/>). Either way it is
    /// stamped on the event (<c>RsvpReminderSentDate</c>), so the other does not send it again.
    /// When D6 was written this had to be an endpoint only: HR's sweeps delivered nothing until lane
    /// K made one that does.</para>
    ///
    /// <para>Since lane 2e-2 (R4-6.3) it answers who it was for and who it reached — by an email the mail
    /// server took, or in the app — and is stamped only when it reached somebody.</para>
    /// </remarks>
    Task<CompanyEventNoticeResultDto> SendRsvpRemindersAsync(Guid eventId, CancellationToken cancellationToken = default);

    /// <summary>Emails every participant that the event is coming up (round 4, D6).</summary>
    /// <inheritdoc cref="SendRsvpRemindersAsync" path="/remarks"/>
    Task<CompanyEventNoticeResultDto> SendEventRemindersAsync(Guid eventId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends again the invitations that reached nobody (lane 2e-2) — once a mail server is set up, or a login made.
    /// Refused for a closed event, one awaiting approval (its approval sends them), one that has begun, and one
    /// whose invitations have all been delivered.
    /// </summary>
    Task<CompanyEventNoticeResultDto> SendUndeliveredInvitationsAsync(Guid eventId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds occurrences after the last of the event's series, on its rule (lane 2f-1, D-12): either how many more, or
    /// until a date; the series holds at most 52. Each new occurrence is a full event, copied from the last.
    /// </summary>
    Task<EventSeriesResultDto> ExtendSeriesAsync(Guid eventId, ExtendEventSeriesDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// The company-schedule reminder sweep for one tenant (round 4, lane N-b2): each live event's
    /// reminder <c>ReminderDaysBefore</c> days ahead where <c>SendReminders</c> is on, and the chase of
    /// unanswered invitations <c>CompanyEventRsvpChaseLeadDays</c> ahead of the RSVP deadline — each
    /// ONCE — and, since lane 2e-3 (F-34), each overdue task's assignee, once. Tenant-explicit: the hourly
    /// host has no signed-in user.
    /// </summary>
    Task<CompanyScheduleReminderRunDto> SendDueRemindersAsync(Guid tenantId, DateTime nowUtc, CancellationToken cancellationToken = default);

    /// <summary>The same sweep, now, for the signed-in HR officer's tenant.</summary>
    Task<CompanyScheduleReminderRunDto> RunDueRemindersNowAsync(CancellationToken cancellationToken = default);

    Task<EventParticipantDto> AddParticipantAsync(CreateEventParticipantDto createDto, CancellationToken cancellationToken = default);
    Task<IEnumerable<EventParticipantDto>> GetParticipantsAsync(Guid eventId, CancellationToken cancellationToken = default);
    /// <summary>A guest's role, whether they are required, their needs, and an outside guest's details (C-22).</summary>
    Task<EventParticipantDto> UpdateParticipantAsync(UpdateEventParticipantDto updateDto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a guest's answer — accepted, declined or tentative — on the event in the route (F-11); on a series, for
    /// this and following dates or every date when asked (lane 2f-2a).
    /// </summary>
    Task<EventSeriesGuestResultDto> RespondToInvitationAsync(Guid eventId, RespondToEventInvitationDto responseDto, CancellationToken cancellationToken = default);

    /// <summary>Uninvites a guest — from this date, or on a series from this and following dates or every date (lane 2f-2a).</summary>
    Task<EventSeriesGuestResultDto> RemoveParticipantAsync(Guid participantId, SeriesScope scope = SeriesScope.ThisOccurrence, CancellationToken cancellationToken = default);

    // Attendance operations
    Task<EventAttendanceDto> MarkAttendanceAsync(MarkEventAttendanceDto markDto, Guid markedById, CancellationToken cancellationToken = default);
    Task<IEnumerable<EventAttendanceDto>> GetAttendanceAsync(Guid eventId, CancellationToken cancellationToken = default);
    Task<bool> CheckOutAsync(CheckOutEventDto checkOutDto, CancellationToken cancellationToken = default);

    /// <summary>Removes a row from the event's register — a correction (C-21).</summary>
    Task RemoveAttendanceAsync(Guid eventId, Guid attendanceId, CancellationToken cancellationToken = default);

    // Attachment operations
    /// <summary>
    /// Records a file the upload gate stored and scanned as one of an event's papers (lane 2h, C-18) — the gate's persist
    /// step, after the controller resolved the event.
    /// </summary>
    Task<EventAttachmentDto> AddUploadedAttachmentAsync(
        Guid eventId, EventAttachmentType type, string? description, Guid uploadedById,
        string fileName, string filePath, long fileSize, Guid fileUploadRecordId, Guid? documentRecordId, Guid? documentVersionId,
        CancellationToken cancellationToken = default);
    Task<IEnumerable<EventAttachmentDto>> GetAttachmentsAsync(Guid eventId, CancellationToken cancellationToken = default);
    /// <summary>One attachment, this tenant's — for its download (lane 2h).</summary>
    Task<EventAttachmentDto> GetAttachmentAsync(Guid attachmentId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAttachmentAsync(Guid attachmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Keeps a drill's company event in step with the drill (lane 2h, C-51): makes it, moves it, or cancels it. Called by
    /// Safety, server-side, so its users need no HR permission.
    /// </summary>
    Task SyncDrillEventAsync(DrillEventSyncDto drill, CancellationToken cancellationToken = default);

    // Task operations
    Task<EventTaskDto> AddTaskAsync(CreateEventTaskDto createDto, CancellationToken cancellationToken = default);
    Task<IEnumerable<EventTaskDto>> GetTasksAsync(Guid eventId, CancellationToken cancellationToken = default);
    Task<EventTaskDto> UpdateTaskAsync(UpdateEventTaskDto updateDto, CancellationToken cancellationToken = default);
    Task<bool> CompleteTaskAsync(CompleteEventTaskDto completeDto, CancellationToken cancellationToken = default);
    Task<bool> DeleteTaskAsync(Guid taskId, CancellationToken cancellationToken = default);
}

#endregion Company Event Service

#region Meeting Room Service

public interface IMeetingRoomService
{
    Task<MeetingRoomDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<MeetingRoomDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<MeetingRoomDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<IEnumerable<MeetingRoomSummaryDto>> GetByLocationAsync(Guid locationId, CancellationToken cancellationToken = default);
    Task<IEnumerable<MeetingRoomSummaryDto>> GetAvailableRoomsAsync(DateTime startDateTime, DateTime endDateTime, int? minCapacity = null, CancellationToken cancellationToken = default);
    Task<IEnumerable<MeetingRoomSummaryDto>> GetActiveRoomsAsync(CancellationToken cancellationToken = default);

    /// <summary>The rooms anyone may book — in use and open for booking — with their rules (lane 3c, the portal's list).</summary>
    Task<IEnumerable<MeetingRoomDto>> GetBookableRoomsAsync(CancellationToken cancellationToken = default);
    Task<MeetingRoomDto> CreateAsync(CreateMeetingRoomDto createDto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a room. Deactivating one with future bookings is refused unless
    /// <see cref="UpdateMeetingRoomDto.CancelFutureBookings"/> says to cancel them (D-18, lane 3a).
    /// </summary>
    Task<MeetingRoomDto> UpdateAsync(UpdateMeetingRoomDto updateDto, CancellationToken cancellationToken = default);

    /// <summary>Deletes a room with no booking on record; one with any is refused — deactivate it instead (D-18, F-49).</summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>What retiring the room would touch: its future bookings and its bookings on record (D-18, lane 3a).</summary>
    Task<RoomRetirementDto> GetRetirementAsync(Guid id, CancellationToken cancellationToken = default);
}

#endregion Meeting Room Service

#region Room Booking Service

public interface IRoomBookingService
{
    Task<RoomBookingDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<RoomBookingDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<RoomBookingDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<IEnumerable<RoomBookingSummaryDto>> GetByRoomIdAsync(Guid roomId, CancellationToken cancellationToken = default);
    Task<IEnumerable<RoomBookingSummaryDto>> GetByBookerAsync(Guid bookedById, CancellationToken cancellationToken = default);
    Task<IEnumerable<RoomBookingSummaryDto>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    Task<IEnumerable<RoomBookingSummaryDto>> GetByStatusAsync(BookingStatus status, CancellationToken cancellationToken = default);
    Task<IEnumerable<RoomBookingSummaryDto>> GetPendingApprovalsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// When the bookable rooms are held over whole days, <paramref name="from"/> to <paramref name="to"/> (lane 3c, D-13):
    /// the room and the time of every live booking — the viewer's own marked, with their id; nobody else's purpose, booker
    /// or number. At most <c>31</c> days.
    /// </summary>
    Task<IReadOnlyList<RoomBusyTimeDto>> GetBusyTimesAsync(DateOnly from, DateOnly to, Guid viewerEmployeeId, CancellationToken cancellationToken = default);

    /// <summary>The bookings register: filtered, sorted and paged on the server (lane 2g-1, C-25).</summary>
    Task<PagedResult<RoomBookingDto>> SearchAsync(RoomBookingSearchDto search, CancellationToken cancellationToken = default);

    /// <summary>Every booking the search finds, as a CSV (lane 2g-1, C-25).</summary>
    Task<byte[]> ExportCsvAsync(RoomBookingSearchDto search, CancellationToken cancellationToken = default);

    Task<RoomBookingDto> CreateAsync(CreateRoomBookingDto createDto, Guid bookedById, CancellationToken cancellationToken = default);

    /// <summary>
    /// Books the room for every date of the linked event's series in the scope still to come (lane 3d-1, D-12): one booking
    /// per date under the room's rules and lock, each at the same distance from its date's start; the dates it cannot take
    /// are listed, saying why. On a room needing approval the first date's approval covers the rest (the user's ruling).
    /// </summary>
    Task<RoomBookingSeriesResultDto> CreateForSeriesAsync(CreateRoomBookingSeriesDto dto, Guid bookedById, CancellationToken cancellationToken = default);

    /// <summary>
    /// An extended series brings the latest date's rooms (lane 3d-2, the user's ruling): each room booked for
    /// <paramref name="templateEventId"/> still holding it is booked for the new dates, at the same distance from each
    /// date's start, under the same rules; the dates a room cannot take are listed, never refused. One result per room.
    /// </summary>
    Task<List<RoomBookingSeriesResultDto>> CarryRoomsAsync(Guid templateEventId, IReadOnlyList<Guid> newEventIds, Guid bookedById, CancellationToken cancellationToken = default);

    /// <summary>The room bookings made for an event, any status, in time order (lane 3d-2: the event page's Rooms card).</summary>
    Task<IEnumerable<RoomBookingSummaryDto>> GetByEventAsync(Guid eventId, CancellationToken cancellationToken = default);
    Task<RoomBookingDto> UpdateAsync(UpdateRoomBookingDto updateDto, CancellationToken cancellationToken = default);
    Task<bool> ApproveBookingAsync(Guid bookingId, Guid approvedById, CancellationToken cancellationToken = default);

    /// <summary>Not approved: the booking is cancelled, "Not approved: …", and its booker told why (lane 3b-1, D-10).</summary>
    Task<RoomBookingDto> RejectBookingAsync(Guid bookingId, Guid rejectedById, string reason, CancellationToken cancellationToken = default);

    /// <summary>Marks a confirmed booking whose start has passed a no-show, for good; its booker told (lane 3b-2).</summary>
    Task<RoomBookingDto> MarkNoShowAsync(Guid bookingId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The hourly sweep's booking half (lane 3b-2): lapses a Tentative booking whose start has come (F-48) and completes
    /// a confirmed one whose end has passed. Tenant-explicit; safe with nobody signed in.
    /// </summary>
    Task<RoomBookingSweepDto> SweepAsync(Guid tenantId, DateTime nowUtc, CancellationToken cancellationToken = default);

    /// <summary>The same, for the signed-in tenant now — HR's run-now.</summary>
    Task<RoomBookingSweepDto> SweepNowAsync(CancellationToken cancellationToken = default);
    Task<bool> CancelBookingAsync(CancelRoomBookingDto cancelDto, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

#endregion Room Booking Service

#region Company Milestone Service

public interface ICompanyMilestoneService
{
    Task<CompanyMilestoneDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<CompanyMilestoneDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<CompanyMilestoneDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<IEnumerable<CompanyMilestoneDto>> GetByCategoryAsync(MilestoneCategory category, CancellationToken cancellationToken = default);
    Task<IEnumerable<CompanyMilestoneDto>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    Task<IEnumerable<CompanyMilestoneDto>> GetUpcomingMilestonesAsync(int daysAhead = 90, CancellationToken cancellationToken = default);
    Task<CompanyMilestoneDto> CreateAsync(CreateCompanyMilestoneDto createDto, CancellationToken cancellationToken = default);
    Task<CompanyMilestoneDto> UpdateAsync(UpdateCompanyMilestoneDto updateDto, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

#endregion Company Milestone Service

#region Business Closure Service

public interface IBusinessClosureService
{
    Task<BusinessClosureDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<BusinessClosureDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<BusinessClosureDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<IEnumerable<BusinessClosureDto>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    Task<IEnumerable<BusinessClosureDto>> GetByTypeAsync(ClosureType type, CancellationToken cancellationToken = default);
    Task<IEnumerable<BusinessClosureDto>> GetByLocationAsync(Guid locationId, CancellationToken cancellationToken = default);
    Task<IEnumerable<BusinessClosureDto>> GetUpcomingClosuresAsync(int daysAhead = 30, CancellationToken cancellationToken = default);
    Task<bool> IsClosureDateAsync(DateTime date, Guid? locationId = null, Guid? organizationUnitId = null, CancellationToken cancellationToken = default);
    Task<BusinessClosureDto> CreateAsync(CreateBusinessClosureDto createDto, Guid announcedById, CancellationToken cancellationToken = default);
    Task<BusinessClosureDto> UpdateAsync(UpdateBusinessClosureDto updateDto, CancellationToken cancellationToken = default);
    /// <summary>Deletes the closure, and answers the recount of the leave it covered (lane 1c, D-15a).</summary>
    Task<LeaveRechargeResultDto> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// The one-time recount of granted leave against the closures and holidays as they stand (lane 1c).
    /// <paramref name="dryRun"/> answers what it would change, saving nothing and telling nobody.
    /// </summary>
    Task<LeaveRechargeResultDto> RechargeAllOpenLeaveAsync(bool dryRun = false, CancellationToken cancellationToken = default);

    /// <summary>What announcing the closure would say, and how many active staff it would reach (lane 1d, L1-1).</summary>
    Task<ClosureAnnouncementPreviewDto> PreviewAnnouncementAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Announces the closure to the staff it covers: an HR announcement addressed by the closure's scope,
    /// worded from it, published as <paramref name="publisherEmployeeId"/> (lane 1d, L1-1). Refused when
    /// it covers nobody.
    /// </summary>
    Task<HrAnnouncementDto> AnnounceAsync(Guid id, Guid publisherEmployeeId, CancellationToken cancellationToken = default);

    /// <summary>Each employee's closure days with the pay flag — payroll's read (lane 1d, D-15c).</summary>
    Task<List<EmployeeClosureDaysDto>> GetEmployeeClosureDaysAsync(
        IReadOnlyCollection<Guid> employeeIds, DateOnly from, DateOnly to, bool includePartial,
        CancellationToken cancellationToken = default);
}

#endregion Business Closure Service

#region Fiscal Year Service

public interface IFiscalYearService
{
    Task<FiscalYearDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<FiscalYearDetailDto> GetDetailByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<FiscalYearDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<FiscalYearDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<FiscalYearDto?> GetByYearAsync(int year, CancellationToken cancellationToken = default);
    Task<FiscalYearDto?> GetCurrentFiscalYearAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<FiscalYearDto>> GetByStatusAsync(FiscalYearStatus status, CancellationToken cancellationToken = default);
    Task<FiscalYearDto> CreateAsync(CreateFiscalYearDto createDto, CancellationToken cancellationToken = default);
    Task<FiscalYearDto> UpdateAsync(UpdateFiscalYearDto updateDto, CancellationToken cancellationToken = default);
    Task<bool> SetAsCurrentAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    // Period operations
    Task<FiscalPeriodDto> AddPeriodAsync(CreateFiscalPeriodDto createDto, CancellationToken cancellationToken = default);
    Task<IEnumerable<FiscalPeriodDto>> GetPeriodsAsync(Guid fiscalYearId, CancellationToken cancellationToken = default);
    Task<FiscalPeriodDto> UpdatePeriodAsync(UpdateFiscalPeriodDto updateDto, CancellationToken cancellationToken = default);
    Task<bool> ClosePeriodAsync(CloseFiscalPeriodDto closeDto, CancellationToken cancellationToken = default);
    Task<bool> DeletePeriodAsync(Guid periodId, CancellationToken cancellationToken = default);
}

#endregion Fiscal Year Service
