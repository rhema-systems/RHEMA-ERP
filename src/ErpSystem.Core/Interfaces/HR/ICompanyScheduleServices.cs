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
    Task<IEnumerable<CompanyEventSummaryDto>> GetByDepartmentAsync(Guid departmentId, CancellationToken cancellationToken = default);
    Task<IEnumerable<CompanyEventSummaryDto>> GetByStatusAsync(EventStatus status, CancellationToken cancellationToken = default);
    Task<IEnumerable<CompanyEventSummaryDto>> GetByCategoryAsync(EventCategory category, CancellationToken cancellationToken = default);
    Task<IEnumerable<CompanyEventSummaryDto>> GetUpcomingEventsAsync(int daysAhead = 30, CancellationToken cancellationToken = default);
    Task<CompanyEventDto> CreateAsync(CreateCompanyEventDto createDto, Guid organizerId, CancellationToken cancellationToken = default);
    Task<CompanyEventDto> UpdateAsync(UpdateCompanyEventDto updateDto, CancellationToken cancellationToken = default);
    Task<bool> ApproveEventAsync(Guid eventId, Guid approvedById, CancellationToken cancellationToken = default);
    Task<bool> CancelEventAsync(CancelEventDto cancelDto, CancellationToken cancellationToken = default);
    Task<bool> RescheduleEventAsync(RescheduleEventDto rescheduleDto, CancellationToken cancellationToken = default);
    Task<bool> CompleteEventAsync(CompleteEventDto completeDto, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

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
    /// </remarks>
    Task<int> SendRsvpRemindersAsync(Guid eventId, CancellationToken cancellationToken = default);

    /// <summary>Emails every participant that the event is coming up (round 4, D6).</summary>
    /// <inheritdoc cref="SendRsvpRemindersAsync" path="/remarks/para[2]"/>
    Task<int> SendEventRemindersAsync(Guid eventId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The company-schedule reminder sweep for one tenant (round 4, lane N-b2): each live event's
    /// reminder <c>ReminderDaysBefore</c> days ahead where <c>SendReminders</c> is on, and the chase of
    /// unanswered invitations <c>CompanyEventRsvpChaseLeadDays</c> ahead of the RSVP deadline — each
    /// ONCE. Tenant-explicit: the hourly host has no signed-in user.
    /// </summary>
    Task<CompanyScheduleReminderRunDto> SendDueRemindersAsync(Guid tenantId, DateTime nowUtc, CancellationToken cancellationToken = default);

    /// <summary>The same sweep, now, for the signed-in HR officer's tenant.</summary>
    Task<CompanyScheduleReminderRunDto> RunDueRemindersNowAsync(CancellationToken cancellationToken = default);

    Task<EventParticipantDto> AddParticipantAsync(CreateEventParticipantDto createDto, CancellationToken cancellationToken = default);
    Task<IEnumerable<EventParticipantDto>> GetParticipantsAsync(Guid eventId, CancellationToken cancellationToken = default);
    Task<bool> RespondToInvitationAsync(RespondToEventInvitationDto responseDto, CancellationToken cancellationToken = default);
    Task<bool> RemoveParticipantAsync(Guid participantId, CancellationToken cancellationToken = default);

    // Attendance operations
    Task<EventAttendanceDto> MarkAttendanceAsync(MarkEventAttendanceDto markDto, Guid markedById, CancellationToken cancellationToken = default);
    Task<IEnumerable<EventAttendanceDto>> GetAttendanceAsync(Guid eventId, CancellationToken cancellationToken = default);
    Task<bool> CheckOutAsync(CheckOutEventDto checkOutDto, CancellationToken cancellationToken = default);

    // Attachment operations
    Task<EventAttachmentDto> AddAttachmentAsync(CreateEventAttachmentDto createDto, CancellationToken cancellationToken = default);
    Task<IEnumerable<EventAttachmentDto>> GetAttachmentsAsync(Guid eventId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAttachmentAsync(Guid attachmentId, CancellationToken cancellationToken = default);

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
    Task<MeetingRoomDto> CreateAsync(CreateMeetingRoomDto createDto, CancellationToken cancellationToken = default);
    Task<MeetingRoomDto> UpdateAsync(UpdateMeetingRoomDto updateDto, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
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
    Task<RoomBookingDto> CreateAsync(CreateRoomBookingDto createDto, Guid bookedById, CancellationToken cancellationToken = default);
    Task<RoomBookingDto> UpdateAsync(UpdateRoomBookingDto updateDto, CancellationToken cancellationToken = default);
    Task<bool> ApproveBookingAsync(Guid bookingId, Guid approvedById, CancellationToken cancellationToken = default);
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
    Task<bool> IsClosureDateAsync(DateTime date, Guid? locationId = null, Guid? departmentId = null, CancellationToken cancellationToken = default);
    Task<BusinessClosureDto> CreateAsync(CreateBusinessClosureDto createDto, Guid announcedById, CancellationToken cancellationToken = default);
    Task<BusinessClosureDto> UpdateAsync(UpdateBusinessClosureDto updateDto, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
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
