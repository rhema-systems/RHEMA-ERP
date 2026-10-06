using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.HR;

#region Company Event DTOs

/// <summary>
/// DTO for company event read operations
/// </summary>
public class CompanyEventDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string EventNumber { get; set; } = string.Empty;
    public string EventName { get; set; } = string.Empty;
    public string? Description { get; set; }
    
    // Classification
    public EventCategory Category { get; set; }
    public string CategoryName => Category.ToString();
    public EventType Type { get; set; }
    public string TypeName => Type.ToString();
    public EventPriority Priority { get; set; }
    public string PriorityName => Priority.ToString();
    
    // Timing
    public DateTime StartDate { get; set; }
    public TimeSpan? StartTime { get; set; }
    public DateTime EndDate { get; set; }
    public TimeSpan? EndTime { get; set; }
    public bool IsAllDayEvent { get; set; }
    
    // Recurrence
    public bool IsRecurring { get; set; }
    public RecurrencePattern? RecurrencePattern { get; set; }
    public string? RecurrencePatternName => RecurrencePattern?.ToString();
    public string? RecurrenceDetails { get; set; }
    public DateTime? RecurrenceEndDate { get; set; }
    public int? RecurrenceCount { get; set; }

    /// <summary>The series this event is an occurrence of (lane 2f-1, D-12); null on a one-off event.</summary>
    public Guid? RecurrenceSeriesId { get; set; }

    /// <summary>Its place in the series, from 1.</summary>
    public int? OccurrenceNumber { get; set; }

    /// <summary>How many occurrences the series has, deleted ones apart — "occurrence 3 of 10".</summary>
    public int? OccurrenceCount { get; set; }

    /// <summary>The record that made the event (lane 2h, C-51) — "EmergencyDrill" — or null for one HR made.</summary>
    public string? SourceEntityType { get; set; }
    public Guid? SourceEntityId { get; set; }

    // Location
    public EventLocation LocationType { get; set; }
    public string LocationTypeName => LocationType.ToString();
    public string? VenueName { get; set; }
    public string? VenueAddress { get; set; }
    public string? OnlineMeetingLink { get; set; }
    public string? MeetingPassword { get; set; }
    public Guid? LocationId { get; set; }
    public string? LocationName { get; set; }
    
    // Organizer
    public Guid OrganizerId { get; set; }
    public string OrganizerName { get; set; } = string.Empty;

    /// <summary>⚠ Retired (D-5): no event carries one (L0-1). Read only, for any older row.</summary>
    public Guid? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }

    /// <summary>The organisation unit the event is for — required when <see cref="Scope"/> is Department (D-5).</summary>
    public Guid? OrganizationUnitId { get; set; }
    public string? OrganizationUnitName { get; set; }

    // Participants
    public ParticipantScope Scope { get; set; }
    public string ScopeName => Scope.ToString();
    public int? EstimatedAttendees { get; set; }
    public bool RequiresRsvp { get; set; }
    public DateTime? RsvpDeadline { get; set; }

    /// <summary>
    /// Who the event is for, from its scope and visibility (lane 2c, D-16): "Everyone", "Finance and the
    /// units beneath it", "Management — unit heads and line managers", or "Its guests and organiser".
    /// </summary>
    public string AudienceDescription { get; set; } = string.Empty;

    /// <summary>What the person saving should know that did not stop the save — an audience that reaches nobody. Create and update only.</summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Who an edit's notice reached — a move, a postponement, a new venue or link (lane 2e-2, R4-6.3). Update only;
    /// null when the edit told nobody.
    /// </summary>
    public CompanyEventNoticeResultDto? Told { get; set; }

    /// <summary>Lane 2f-2b: an edit with a series scope — the dates it changed, and who was told. Update only.</summary>
    public EventSeriesChangeResultDto? Series { get; set; }

    // Visibility
    public EventVisibility Visibility { get; set; }
    public string VisibilityName => Visibility.ToString();
    public bool ShowOnCompanyCalendar { get; set; }
    public bool ShowOnIntranet { get; set; }
    
    // Status
    public EventStatus Status { get; set; }
    public string StatusName => Status.ToString();
    
    // Approval
    public bool RequiresApproval { get; set; }
    public Guid? ApprovedById { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovalDate { get; set; }
    
    // Budget
    public bool HasBudget { get; set; }
    public decimal? BudgetAmount { get; set; }
    public decimal? ActualCost { get; set; }
    public string? BudgetCode { get; set; }
    
    // Resources
    public string? RequiredResources { get; set; }
    public string? CateringRequirements { get; set; }
    public string? TechnicalRequirements { get; set; }
    
    // Reminders
    public bool SendReminders { get; set; }
    public int? ReminderDaysBefore { get; set; }
    public DateTime? ReminderSentDate { get; set; }
    /// <summary>When the RSVP chase went — by the sweep or HR's button (round 4, lane N-b2).</summary>
    public DateTime? RsvpReminderSentDate { get; set; }

    // Completion
    public DateTime? ActualStartTime { get; set; }
    public DateTime? ActualEndTime { get; set; }
    public int? ActualAttendance { get; set; }
    public string? OutcomeSummary { get; set; }
    
    // Cancellation
    public bool IsCancelled { get; set; }
    public DateTime? CancellationDate { get; set; }
    public string? CancellationReason { get; set; }
    
    // Reschedule
    public bool IsRescheduled { get; set; }

    /// <summary>⚠ WHEN it was moved, not what it was moved from — see the entity. C-2.</summary>
    public DateTime? RescheduledDate { get; set; }

    /// <summary>
    /// What the event was originally scheduled for, kept from the first move (round 4, D7; C-2).
    /// </summary>
    /// <remarks>
    /// ⚠ Null on an event that has never moved, and on every event rescheduled BEFORE this lane —
    /// the original was overwritten and is not recoverable. A screen must render null as "not
    /// recorded", never as "same as now".
    /// </remarks>
    public DateTime? OriginalStartDate { get; set; }
    public TimeSpan? OriginalStartTime { get; set; }
    public DateTime? OriginalEndDate { get; set; }
    public TimeSpan? OriginalEndTime { get; set; }

    public string? RescheduleReason { get; set; }
    
    public string? AdditionalNotes { get; set; }
}

/// <summary>
/// Summary DTO for company event list views
/// </summary>
public class CompanyEventSummaryDto
{
    public Guid Id { get; set; }
    public string EventNumber { get; set; } = string.Empty;
    public string EventName { get; set; } = string.Empty;
    public EventCategory Category { get; set; }
    public string CategoryName => Category.ToString();
    public DateTime StartDate { get; set; }
    public TimeSpan? StartTime { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsAllDayEvent { get; set; }
    public EventLocation LocationType { get; set; }
    public string LocationTypeName => LocationType.ToString();
    public string? VenueName { get; set; }
    public EventStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public string OrganizerName { get; set; } = string.Empty;
    public int? EstimatedAttendees { get; set; }
}

/// <summary>
/// Detailed DTO for company event with related data
/// </summary>
public class CompanyEventDetailDto : CompanyEventDto
{
    public List<EventParticipantDto> Participants { get; set; } = new();
    public List<EventAttendanceDto> AttendanceRecords { get; set; } = new();
    public List<EventAttachmentDto> Attachments { get; set; } = new();
    public List<EventTaskDto> Tasks { get; set; } = new();

    /// <summary>
    /// The day the hourly sweep sends the reminder (lane 2e-2): <see cref="CompanyEventDto.ReminderDaysBefore"/>
    /// before the start. Null when reminders are off. Due and unsent means nobody has been reached yet.
    /// </summary>
    public DateTime? ReminderDueOn { get; set; }

    /// <summary>The day the hourly sweep chases unanswered invitations: the tenant's lead before the reply-by date.</summary>
    public DateTime? RsvpChaseDueOn { get; set; }

    /// <summary>
    /// Whether the tenant has a mail server set up (lane 2e-2) — without one no email goes, and only the people with
    /// a login are told, in the app.
    /// </summary>
    public bool MailServerSetUp { get; set; }

    /// <summary>Every occurrence of its series, in order (lane 2f-1); empty for a one-off event.</summary>
    public List<EventSeriesOccurrenceDto> SeriesOccurrences { get; set; } = new();

    /// <summary>
    /// "Falls on a public holiday: Republic Day" / "…a company-wide closure: …" — a day the company does not work
    /// (lane 2f-1, D-12: an occurrence there is generated and flagged, not skipped). Null on an ordinary day.
    /// </summary>
    public string? DayOffNote { get; set; }

    /// <summary>Where it came from, worded and linked (lane 2h, C-51) — the emergency drill; null for an event HR made.</summary>
    public EventSourceDto? Source { get; set; }
}

/// <summary>One occurrence of a series, as its list shows it (lane 2f-1).</summary>
public class EventSeriesOccurrenceDto
{
    public Guid Id { get; set; }
    public string EventNumber { get; set; } = string.Empty;
    public int OccurrenceNumber { get; set; }
    public DateTime StartDate { get; set; }
    public TimeSpan? StartTime { get; set; }
    public DateTime EndDate { get; set; }
    public EventStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public bool IsCancelled { get; set; }

    /// <summary>Set when the occurrence falls on a public holiday or a company-wide closure.</summary>
    public string? DayOffNote { get; set; }
}

/// <summary>
/// More occurrences after a series' last, on its rule (lane 2f-1, D-12) — either how many more, or the date they run
/// until; not both. The series holds at most 52.
/// </summary>
public class ExtendEventSeriesDto
{
    [Range(1, 51)]
    public int? Count { get; set; }
    public DateTime? Until { get; set; }
}

/// <summary>The occurrences a series action made, and what HR should know about their dates.</summary>
public class EventSeriesResultDto
{
    public List<EventSeriesOccurrenceDto> Occurrences { get; set; } = new();

    /// <summary>"EVT-… (Tuesday, 1 July 2026) falls on a public holiday: Republic Day" — flagged, not skipped.</summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// Lane 2f-2a (the user's ruling): how many of the latest occurrence's guests were put on the new dates — invited
    /// now, or with the approval when the new dates need one.
    /// </summary>
    public int Guests { get; set; }

    /// <summary>Who the series invitations reached; null when they wait for approval or there were no guests.</summary>
    public CompanyEventNoticeResultDto? Told { get; set; }
}

/// <summary>
/// What a guest action with a series scope did (lane 2f-2a, D-12): adding a guest, taking one off, recording an answer.
/// </summary>
public class EventSeriesGuestResultDto
{
    /// <summary>The dates acted on, by event number, in date order.</summary>
    public List<string> EventNumbers { get; set; } = new();

    /// <summary>Dates the scope covered with nothing to do: already invited (adding), or not invited (removing, answering).</summary>
    public int Skipped { get; set; }

    /// <summary>Dates the scope covered and left alone because they have started, been completed or been cancelled.</summary>
    public int Closed { get; set; }

    /// <summary>Dates added whose invitation waits for the series' approval (F-33).</summary>
    public int Waiting { get; set; }

    /// <summary>Who the one notice reached; null when nothing was sent.</summary>
    public CompanyEventNoticeResultDto? Told { get; set; }
}

/// <summary>
/// What an edit, a move or a cancellation with a series scope did (lane 2f-2b, D-12): the dates it changed and who was
/// told — each guest once.
/// </summary>
public class EventSeriesChangeResultDto
{
    /// <summary>The dates changed, by event number, in date order.</summary>
    public List<string> EventNumbers { get; set; } = new();

    /// <summary>Dates the scope covered and left alone because they have started, been completed or been cancelled.</summary>
    public int Closed { get; set; }

    /// <summary>Who the notices reached, each guest counted once per kind of change.</summary>
    public CompanyEventNoticeResultDto? Told { get; set; }
}

/// <summary>
/// The events register's search (lane 2g-1, D-9; C-10…C-13): filtered, sorted and paged on the server — it loaded every
/// event and filtered in the browser. The export takes the same filters.
/// </summary>
public class CompanyEventSearchDto
{
    /// <summary>In the name, the number, the venue or the organiser's name.</summary>
    [MaxLength(100)]
    public string? Text { get; set; }
    public EventStatus? Status { get; set; }
    public EventCategory? Category { get; set; }
    /// <summary>The site.</summary>
    public Guid? LocationId { get; set; }
    public Guid? OrganizationUnitId { get; set; }
    public Guid? OrganizerId { get; set; }
    /// <summary>One series' dates (lane 2f-1's register filter), in their order.</summary>
    public Guid? SeriesId { get; set; }
    /// <summary>Events that touch this range — by overlap, as the range read does (lane 2a).</summary>
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    /// <summary><c>-start</c> (newest first, the default), <c>start</c>, <c>name</c>, <c>number</c> or <c>-number</c>.</summary>
    [MaxLength(20)]
    public string? Sort { get; set; }
    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;
    [Range(1, 200)]
    public int PageSize { get; set; } = 25;
}

/// <summary>
/// What the event form asks before saving (lane 2g-2, C-15): the events this one would clash with, refused or warned of.
/// The event being edited, and its own series, are left out.
/// </summary>
public class EventClashQueryDto
{
    [Required]
    public DateTime StartDate { get; set; }
    public TimeSpan? StartTime { get; set; }
    [Required]
    public DateTime EndDate { get; set; }
    public TimeSpan? EndTime { get; set; }
    public bool IsAllDayEvent { get; set; }
    public ParticipantScope Scope { get; set; }
    public EventVisibility Visibility { get; set; }
    public Guid? OrganizationUnitId { get; set; }
    /// <summary>The site; none means online or the whole company, which is every site.</summary>
    public Guid? LocationId { get; set; }
    /// <summary>The event being edited.</summary>
    public Guid? ExcludeId { get; set; }
    /// <summary>Its series: a series' dates do not clash with each other.</summary>
    public Guid? SeriesId { get; set; }
}

/// <summary>An event another would clash with (lane 2g-2, C-15), and what the server would do about it.</summary>
public class EventClashDto
{
    public Guid EventId { get; set; }
    public string EventNumber { get; set; } = string.Empty;
    public string EventName { get; set; } = string.Empty;
    /// <summary>"14 October 2026, 09:00–10:00".</summary>
    public string When { get; set; } = string.Empty;
    /// <summary>"Everyone", "Finance and the units beneath it", "Management — …".</summary>
    public string Audience { get; set; } = string.Empty;
    public string? SiteName { get; set; }
    /// <summary>The save would be refused (both for the whole company, or both for the same unit); otherwise a warning.</summary>
    public bool Refused { get; set; }
    /// <summary>The sentence the server answers with.</summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>The bookings register's search (lane 2g-1, D-9; C-25), as the events'.</summary>
public class RoomBookingSearchDto
{
    /// <summary>In the number, the room, the purpose or the booker's name.</summary>
    [MaxLength(100)]
    public string? Text { get; set; }
    public BookingStatus? Status { get; set; }
    public Guid? RoomId { get; set; }
    /// <summary>Bookings that touch this range.</summary>
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    /// <summary><c>-start</c> (newest first, the default), <c>start</c>, <c>number</c> or <c>-number</c>.</summary>
    [MaxLength(20)]
    public string? Sort { get; set; }
    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;
    [Range(1, 200)]
    public int PageSize { get; set; } = 25;
}

/// <summary>The company schedule's landing page in one read (lane 2g-1, D-9): it made four.</summary>
public class CompanyScheduleDashboardDto
{
    /// <summary>The next 30 days' events.</summary>
    public List<CompanyEventSummaryDto> UpcomingEvents { get; set; } = new();
    public List<RoomBookingSummaryDto> PendingBookings { get; set; } = new();
    /// <summary>The next 60 days' closures.</summary>
    public List<BusinessClosureDto> UpcomingClosures { get; set; } = new();
    /// <summary>The next 90 days' milestones.</summary>
    public List<CompanyMilestoneDto> UpcomingMilestones { get; set; } = new();
}

/// <summary>
/// DTO for creating a company event
/// </summary>
public class CreateCompanyEventDto : CreateDtoBase
{
    [Required]
    [MaxLength(100)]
    public string EventName { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    public EventCategory Category { get; set; }

    [Required]
    public EventType Type { get; set; }

    public EventPriority Priority { get; set; } = EventPriority.Medium;

    // Timing
    [Required]
    public DateTime StartDate { get; set; }

    public TimeSpan? StartTime { get; set; }

    [Required]
    public DateTime EndDate { get; set; }

    public TimeSpan? EndTime { get; set; }

    public bool IsAllDayEvent { get; set; }

    // Recurrence
    public bool IsRecurring { get; set; }
    public RecurrencePattern? RecurrencePattern { get; set; }

    [MaxLength(500)]
    public string? RecurrenceDetails { get; set; }

    public DateTime? RecurrenceEndDate { get; set; }
    public int? RecurrenceCount { get; set; }

    // Location
    [Required]
    public EventLocation LocationType { get; set; }

    [MaxLength(200)]
    public string? VenueName { get; set; }

    [MaxLength(500)]
    public string? VenueAddress { get; set; }

    [MaxLength(700)]
    public string? OnlineMeetingLink { get; set; }

    [MaxLength(100)]
    public string? MeetingPassword { get; set; }

    public Guid? LocationId { get; set; }

    /// <summary>⚠ Retired (D-5): refused when set. Choose <see cref="OrganizationUnitId"/> instead.</summary>
    public Guid? DepartmentId { get; set; }

    /// <summary>The organisation unit the event is for; required when <see cref="Scope"/> is Department.</summary>
    public Guid? OrganizationUnitId { get; set; }

    /// <summary>
    /// Who organises it (D-11) — an active employee. Empty means the person creating it; whoever creates
    /// it is recorded as the creator either way.
    /// </summary>
    public Guid? OrganizerId { get; set; }

    // Participants
    [Required]
    public ParticipantScope Scope { get; set; }

    public int? EstimatedAttendees { get; set; }
    public bool RequiresRsvp { get; set; }
    public DateTime? RsvpDeadline { get; set; }

    // Visibility
    public EventVisibility Visibility { get; set; } = EventVisibility.Public;
    public bool ShowOnCompanyCalendar { get; set; } = true;
    public bool ShowOnIntranet { get; set; }

    // Approval
    public bool RequiresApproval { get; set; }

    // Budget
    public bool HasBudget { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? BudgetAmount { get; set; }

    [MaxLength(50)]
    public string? BudgetCode { get; set; }

    // Resources
    [MaxLength(1000)]
    public string? RequiredResources { get; set; }

    [MaxLength(1000)]
    public string? CateringRequirements { get; set; }

    [MaxLength(1000)]
    public string? TechnicalRequirements { get; set; }

    // Reminders
    public bool SendReminders { get; set; }
    public int? ReminderDaysBefore { get; set; }

    [MaxLength(2000)]
    public string? AdditionalNotes { get; set; }
}

/// <summary>
/// DTO for updating a company event
/// </summary>
public class UpdateCompanyEventDto : UpdateDtoBase
{
    [Required]
    [MaxLength(100)]
    public string EventName { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    public EventCategory Category { get; set; }

    [Required]
    public EventType Type { get; set; }

    public EventPriority Priority { get; set; }

    // Timing
    [Required]
    public DateTime StartDate { get; set; }

    public TimeSpan? StartTime { get; set; }

    [Required]
    public DateTime EndDate { get; set; }

    public TimeSpan? EndTime { get; set; }

    public bool IsAllDayEvent { get; set; }

    // Location
    [Required]
    public EventLocation LocationType { get; set; }

    [MaxLength(200)]
    public string? VenueName { get; set; }

    [MaxLength(500)]
    public string? VenueAddress { get; set; }

    [MaxLength(700)]
    public string? OnlineMeetingLink { get; set; }

    [MaxLength(100)]
    public string? MeetingPassword { get; set; }

    public Guid? LocationId { get; set; }

    /// <summary>⚠ Retired (D-5): refused when set. Choose <see cref="OrganizationUnitId"/> instead.</summary>
    public Guid? DepartmentId { get; set; }

    /// <summary>The organisation unit the event is for; required when <see cref="Scope"/> is Department.</summary>
    public Guid? OrganizationUnitId { get; set; }

    /// <summary>Who organises it (D-11) — an active employee. Empty leaves the organiser as it is.</summary>
    public Guid? OrganizerId { get; set; }

    // Participants
    [Required]
    public ParticipantScope Scope { get; set; }

    public int? EstimatedAttendees { get; set; }
    public bool RequiresRsvp { get; set; }
    public DateTime? RsvpDeadline { get; set; }

    // Visibility
    public EventVisibility Visibility { get; set; }
    public bool ShowOnCompanyCalendar { get; set; }
    public bool ShowOnIntranet { get; set; }

    /// <summary>
    /// Scheduled, In progress or Postponed — or Confirmed where the event needs no approval. Empty
    /// leaves it as it is. Cancelled, Completed and Rescheduled come from their own actions (F-37).
    /// </summary>
    public EventStatus? Status { get; set; }

    /// <summary>
    /// Why the dates or times moved. An edit that changes the dates, the times or the all-day switch is
    /// a reschedule (F-37, R4-7.1): the original window is kept, the guests are told, and this is the
    /// reason they read. Refused without it when the window changes; ignored otherwise.
    /// </summary>
    [MaxLength(2000)]
    public string? RescheduleReason { get; set; }

    // Budget
    public bool HasBudget { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? BudgetAmount { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? ActualCost { get; set; }

    [MaxLength(50)]
    public string? BudgetCode { get; set; }

    // Resources
    [MaxLength(1000)]
    public string? RequiredResources { get; set; }

    [MaxLength(1000)]
    public string? CateringRequirements { get; set; }

    [MaxLength(1000)]
    public string? TechnicalRequirements { get; set; }

    // Reminders
    public bool SendReminders { get; set; }
    public int? ReminderDaysBefore { get; set; }

    [MaxLength(2000)]
    public string? AdditionalNotes { get; set; }

    /// <summary>
    /// Lane 2f-2b (D-12): on a recurring event, this date only (the default), or this and following dates, or every
    /// date — each taking only what this edit changed (the user's ruling); a new window moves them by the same amount.
    /// Named apart from <see cref="Scope"/>, which is who the event is for.
    /// </summary>
    public SeriesScope SeriesScope { get; set; }
}

/// <summary>
/// DTO for cancelling an event
/// </summary>
public class CancelEventDto
{
    [Required]
    public Guid EventId { get; set; }

    [Required]
    [MaxLength(1000)]
    public string CancellationReason { get; set; } = string.Empty;

    /// <summary>Lane 2f-2b (D-12): on a recurring event, this date only (the default), this and following, or every date.</summary>
    public SeriesScope SeriesScope { get; set; }
}

/// <summary>
/// DTO for rescheduling an event
/// </summary>
public class RescheduleEventDto
{
    [Required]
    public Guid EventId { get; set; }

    [Required]
    public DateTime NewStartDate { get; set; }

    public TimeSpan? NewStartTime { get; set; }

    [Required]
    public DateTime NewEndDate { get; set; }

    public TimeSpan? NewEndTime { get; set; }

    [Required]
    [MaxLength(2000)]
    public string RescheduleReason { get; set; } = string.Empty;

    /// <summary>
    /// A new reply-by date, when the event asks for replies and the current one would fall after the
    /// new start. Empty keeps the current deadline (F-38).
    /// </summary>
    public DateTime? NewRsvpDeadline { get; set; }

    /// <summary>
    /// Lane 2f-2b (D-12): on a recurring event, this date only (the default), or this and following dates, or every
    /// date — each moved by the same number of days, to the new times when given; a new reply-by date keeps its
    /// distance from each date's start.
    /// </summary>
    public SeriesScope SeriesScope { get; set; }
}

/// <summary>
/// DTO for completing an event
/// </summary>
public class CompleteEventDto
{
    [Required]
    public Guid EventId { get; set; }

    public DateTime? ActualStartTime { get; set; }
    public DateTime? ActualEndTime { get; set; }

    [Range(0, int.MaxValue)]
    public int? ActualAttendance { get; set; }

    [MaxLength(2000)]
    public string? OutcomeSummary { get; set; }
}

/// <summary>
/// Who an event with this scope and visibility would be for, and how many that is — the line the form
/// shows before the event is saved (lane 2c, D-16).
/// </summary>
public class EventAudiencePreviewDto
{
    public string Audience { get; set; } = string.Empty;

    /// <summary>Active staff the audience reaches; 0 when the event is for its guests and organiser only.</summary>
    public int Reach { get; set; }

    /// <summary>For its guests and organiser only: no wider audience, so nothing to count.</summary>
    public bool GuestListOnly { get; set; }

    /// <summary>Set when the audience reaches nobody — say so before the event is saved.</summary>
    public string? Warning { get; set; }
}

/// <summary>
/// What announcing an event on the intranet would say, and to how many (lane 2c, "Show on intranet") —
/// published only when HR confirms, as a closure's announcement is (L1-1).
/// </summary>
public class EventAnnouncementPreviewDto
{
    public Guid EventId { get; set; }
    public int StaffReached { get; set; }
    public bool CanAnnounce { get; set; }

    /// <summary>Why it cannot be announced, when it cannot.</summary>
    public string? Reason { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}

/// <summary>
/// An approver's decision on an event (lane 2b, D-10): optional comments on approval, the reason on a
/// rejection — which the organiser and everybody invited are told.
/// </summary>
public class EventDecisionDto
{
    [MaxLength(2000)]
    public string? Comments { get; set; }
}

/// <summary>
/// What cancelling, rescheduling or deleting an event did beyond the event itself (lane 2a: F-38, F-39),
/// so the screen can say it rather than leave HR to discover it.
/// </summary>
public class CompanyEventChangeDto
{
    /// <summary>The event as it now stands; null after a delete.</summary>
    public CompanyEventDto? Event { get; set; }

    /// <summary>Room bookings made for the event that moved with it, by number.</summary>
    public List<string> BookingsMoved { get; set; } = new();

    /// <summary>Room bookings made for the event that were cancelled with it, by number.</summary>
    public List<string> BookingsCancelled { get; set; } = new();

    /// <summary>Accepted or tentative answers set back to awaiting a reply, because the time changed.</summary>
    public int AnswersReset { get; set; }

    /// <summary>The event had been approved, moved, and now waits for approval again.</summary>
    public bool ApprovalCleared { get; set; }

    /// <summary>Who was told of it, and how (lane 2e-2, R4-6.3); null after a delete, which tells nobody.</summary>
    public CompanyEventNoticeResultDto? Told { get; set; }

    /// <summary>Lane 2f-2b: a move or a cancellation with a series scope — the dates it changed, and who was told.</summary>
    public EventSeriesChangeResultDto? Series { get; set; }

    /// <summary>Lane 2g-2 (C-15): what a move should know that did not stop it — another event at the same time and place.</summary>
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// What one notice did (company-schedule final closure, lane 2e-2; R4-6.3): how many people it was for, and how
/// many it reached — by an email the mail server took, or a notice in the app.
/// </summary>
/// <remarks>
/// ⚠ "Sent", "reminded" and "chased" used to count attempts: every guest with an address, whether or not any mail
/// server took the email, so a database with none (UAT) reported every send a success. A person counts as reached
/// once either way reached them; the counts are of people, not of emails or logins.
/// </remarks>
public class CompanyEventNoticeResultDto
{
    /// <summary>The people it was for.</summary>
    public int Issued { get; set; }

    /// <summary>The people it reached: an email taken, or a notice in the app.</summary>
    public int Reached { get; set; }

    public int NotReached => Issued - Reached;

    /// <summary>Emails the mail server took.</summary>
    public int Emailed { get; set; }

    /// <summary>Emails tried that no mail server took — none set up, refused, or no answer within ten seconds.</summary>
    public int EmailsNotTaken { get; set; }

    /// <summary>People told in the app, through at least one active login.</summary>
    public int ToldInApp { get; set; }

    /// <summary>Whether the tenant has a mail server set up — why no email went, when none did.</summary>
    public bool MailServerSetUp { get; set; }

    /// <summary>
    /// A reminder or a chase only: it reached somebody, so it counts as sent and is not sent again. One that reached
    /// nobody stays due, and the hourly sweep tries it again.
    /// </summary>
    public bool Stamped { get; set; }

    /// <summary>Adds another notice's counts to these — a reminder's guests and its organiser.</summary>
    public CompanyEventNoticeResultDto Add(CompanyEventNoticeResultDto other)
    {
        Issued += other.Issued;
        Reached += other.Reached;
        Emailed += other.Emailed;
        EmailsNotTaken += other.EmailsNotTaken;
        ToldInApp += other.ToldInApp;
        MailServerSetUp |= other.MailServerSetUp;
        return this;
    }
}

#endregion

#region Event Participant DTOs

/// <summary>
/// DTO for event participant read operations
/// </summary>
public class EventParticipantDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid EventId { get; set; }
    public Guid? EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public string? ExternalParticipantName { get; set; }
    public string? ExternalParticipantEmail { get; set; }
    public string? ExternalParticipantOrganization { get; set; }
    public string ParticipantName => EmployeeName ?? ExternalParticipantName ?? string.Empty;
    public ParticipantRole Role { get; set; }
    public string RoleName => Role.ToString();
    public bool IsRequired { get; set; }
    public InvitationStatus InvitationStatus { get; set; }
    public string InvitationStatusName => InvitationStatus.ToString();
    public DateTime? InvitationSentDate { get; set; }
    public DateTime? ResponseDate { get; set; }
    public string? ResponseComments { get; set; }
    public string? SpecialRequirements { get; set; }

    /// <summary>Lane 2f-2a: when the guest was added with a series scope, what that did across the dates.</summary>
    public EventSeriesGuestResultDto? Series { get; set; }
}

/// <summary>
/// DTO for creating an event participant
/// </summary>
public class CreateEventParticipantDto : CreateDtoBase
{
    [Required]
    public Guid EventId { get; set; }

    public Guid? EmployeeId { get; set; }

    [MaxLength(100)]
    public string? ExternalParticipantName { get; set; }

    [MaxLength(100)]
    [EmailAddress]
    public string? ExternalParticipantEmail { get; set; }

    [MaxLength(100)]
    public string? ExternalParticipantOrganization { get; set; }

    [Required]
    public ParticipantRole Role { get; set; }

    public bool IsRequired { get; set; }

    [MaxLength(1000)]
    public string? SpecialRequirements { get; set; }

    /// <summary>Lane 2f-2a (D-12): on a recurring event, this date only (the default), this and following, or every date.</summary>
    public SeriesScope Scope { get; set; }
}

/// <summary>
/// A guest's details as HR corrects them (lane 2d, C-22). The employee of an employee guest is not among
/// them — uninvite and invite the other person — and the outside details are for an outside guest only.
/// </summary>
public class UpdateEventParticipantDto
{
    [Required]
    public Guid Id { get; set; }

    [MaxLength(100)]
    public string? ExternalParticipantName { get; set; }

    [MaxLength(100)]
    [EmailAddress]
    public string? ExternalParticipantEmail { get; set; }

    [MaxLength(100)]
    public string? ExternalParticipantOrganization { get; set; }

    [Required]
    public ParticipantRole Role { get; set; }

    public bool IsRequired { get; set; }

    [MaxLength(1000)]
    public string? SpecialRequirements { get; set; }
}

/// <summary>
/// DTO for responding to event invitation
/// </summary>
public class RespondToEventInvitationDto
{
    [Required]
    public Guid ParticipantId { get; set; }

    [Required]
    public InvitationStatus Response { get; set; }

    [MaxLength(1000)]
    public string? ResponseComments { get; set; }

    /// <summary>
    /// Lane 2f-2a (D-12): on a recurring event, the answer for this date only (the default), or for this guest's
    /// invitations to this and following dates, or every date.
    /// </summary>
    public SeriesScope Scope { get; set; }
}

#endregion

#region Event Attendance DTOs

/// <summary>
/// DTO for event attendance read operations
/// </summary>
public class EventAttendanceDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid EventId { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public bool Attended { get; set; }
    public DateTime? CheckInTime { get; set; }
    public DateTime? CheckOutTime { get; set; }
    public string? AbsenceReason { get; set; }
    public string? Notes { get; set; }
    public Guid? MarkedById { get; set; }
    public string? MarkedByName { get; set; }
}

/// <summary>
/// DTO for marking event attendance
/// </summary>
public class MarkEventAttendanceDto
{
    [Required]
    public Guid EventId { get; set; }

    [Required]
    public Guid EmployeeId { get; set; }

    public bool Attended { get; set; }
    public DateTime? CheckInTime { get; set; }

    [MaxLength(1000)]
    public string? AbsenceReason { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

/// <summary>
/// DTO for checking out from event
/// </summary>
public class CheckOutEventDto
{
    [Required]
    public Guid AttendanceId { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

#endregion

#region Event Attachment DTOs

/// <summary>
/// DTO for event attachment read operations
/// </summary>
public class EventAttachmentDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid EventId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public EventAttachmentType Type { get; set; }
    public string TypeName => Type.ToString();
    public string? Description { get; set; }
    public DateTime UploadDate { get; set; }

    /// <summary>
    /// Lane 2h (C-18): a file stored through the upload gate, which downloads. False for a row from before the gate — a
    /// name and a path typed in, with no file ever stored (F-54): "reference only — no file stored".
    /// </summary>
    public bool HasFile { get; set; }
    public long? FileSizeBytes { get; set; }
    public Guid? UploadedById { get; set; }
}

// Lane 2h: CreateEventAttachmentDto is gone — an attachment is a file uploaded through the gate (C-18), never a name and
// a path the caller types (F-54).

/// <summary>
/// What a drill in Safety tells the company schedule (lane 2h, C-51): its next date becomes an all-day company event at
/// its site, organised by its coordinator, moved when the date moves and cancelled when it is cleared or the drill
/// deleted.
/// </summary>
public class DrillEventSyncDto
{
    public Guid DrillId { get; set; }
    public string DrillNumber { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public Guid CoordinatorId { get; set; }
    public Guid? LocationId { get; set; }
    public DateTime? NextDate { get; set; }
    /// <summary>The drill was deleted, or its plan was.</summary>
    public bool Removed { get; set; }
    /// <summary>Why, when <see cref="Removed"/>; the drill's own deletion when left out.</summary>
    public string? RemovedReason { get; set; }
}

/// <summary>Where an event came from (lane 2h, C-51): "Emergency drill DRILL-2026-001 — …", with a link to it.</summary>
public class EventSourceDto
{
    public string Kind { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    /// <summary>The page to open it on; null when the source has since been deleted.</summary>
    public string? Link { get; set; }
}

#endregion

#region Event Task DTOs

/// <summary>
/// DTO for event task read operations
/// </summary>
public class EventTaskDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid EventId { get; set; }
    public string TaskDescription { get; set; } = string.Empty;
    public EventTaskCategory Category { get; set; }
    public string CategoryName => Category.ToString();
    public Guid? AssignedToId { get; set; }
    public string? AssignedToName { get; set; }
    public DateTime? DueDate { get; set; }
    public TaskPriority Priority { get; set; }
    public string PriorityName => Priority.ToString();
    public EventTaskStatus Status { get; set; }
    public string StatusName => Status.ToString();

    /// <summary>Due before today and still open — worked out on every read, never stored (lane 2d, F-12).</summary>
    public bool IsOverdue { get; set; }

    /// <summary>When the hourly sweep chased the assignee about it being overdue (lane 2e-3, F-34); null until then.</summary>
    public DateTime? OverdueChasedAt { get; set; }

    public DateTime? CompletionDate { get; set; }
    public string? CompletionNotes { get; set; }
}

/// <summary>
/// DTO for creating an event task
/// </summary>
public class CreateEventTaskDto : CreateDtoBase
{
    [Required]
    public Guid EventId { get; set; }

    [Required]
    [MaxLength(1000)]
    public string TaskDescription { get; set; } = string.Empty;

    [Required]
    public EventTaskCategory Category { get; set; }

    public Guid? AssignedToId { get; set; }
    public DateTime? DueDate { get; set; }
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;
}

/// <summary>
/// DTO for updating an event task
/// </summary>
public class UpdateEventTaskDto : UpdateDtoBase
{
    [Required]
    [MaxLength(1000)]
    public string TaskDescription { get; set; } = string.Empty;

    [Required]
    public EventTaskCategory Category { get; set; }

    public Guid? AssignedToId { get; set; }
    public DateTime? DueDate { get; set; }
    public TaskPriority Priority { get; set; }
    public EventTaskStatus Status { get; set; }
}

/// <summary>
/// DTO for completing an event task
/// </summary>
public class CompleteEventTaskDto
{
    [Required]
    public Guid TaskId { get; set; }

    [MaxLength(1000)]
    public string? CompletionNotes { get; set; }
}

#endregion

#region Meeting Room DTOs

/// <summary>
/// DTO for meeting room read operations
/// </summary>
public class MeetingRoomDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string RoomCode { get; set; } = string.Empty;
    public string RoomName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid LocationId { get; set; }
    public string LocationName { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string? Floor { get; set; }
    public string? Building { get; set; }
    public int Capacity { get; set; }
    public RoomType Type { get; set; }
    public string TypeName => Type.ToString();
    
    // Facilities
    public bool HasProjector { get; set; }
    public bool HasWhiteboard { get; set; }
    public bool HasVideoConference { get; set; }
    public bool HasAudioSystem { get; set; }
    public bool HasAirConditioning { get; set; }
    public string? OtherFacilities { get; set; }
    
    public bool IsActive { get; set; }
    public bool RequiresApproval { get; set; }
    public bool IsBookable { get; set; }
    public int? MaxBookingDurationHours { get; set; }
    public int? AdvanceBookingDays { get; set; }
}

/// <summary>
/// Summary DTO for meeting room list views
/// </summary>
public class MeetingRoomSummaryDto
{
    public Guid Id { get; set; }
    public string RoomCode { get; set; } = string.Empty;
    public string RoomName { get; set; } = string.Empty;
    public string LocationName { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public RoomType Type { get; set; }
    public string TypeName => Type.ToString();
    public bool IsActive { get; set; }
    public bool IsBookable { get; set; }
}

/// <summary>
/// DTO for creating a meeting room
/// </summary>
public class CreateMeetingRoomDto : CreateDtoBase
{
    [MaxLength(50)]
    public string RoomCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string RoomName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    public Guid LocationId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Location { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? Floor { get; set; }

    [MaxLength(50)]
    public string? Building { get; set; }

    [Required]
    [Range(1, int.MaxValue)]
    public int Capacity { get; set; }

    [Required]
    public RoomType Type { get; set; }

    public bool HasProjector { get; set; }
    public bool HasWhiteboard { get; set; }
    public bool HasVideoConference { get; set; }
    public bool HasAudioSystem { get; set; }
    public bool HasAirConditioning { get; set; }

    [MaxLength(500)]
    public string? OtherFacilities { get; set; }

    public bool IsActive { get; set; } = true;
    public bool RequiresApproval { get; set; }
    public bool IsBookable { get; set; } = true;
    public int? MaxBookingDurationHours { get; set; }
    public int? AdvanceBookingDays { get; set; }
}

/// <summary>
/// DTO for updating a meeting room
/// </summary>
public class UpdateMeetingRoomDto : UpdateDtoBase
{
    [MaxLength(50)]
    public string RoomCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string RoomName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    public Guid LocationId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Location { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? Floor { get; set; }

    [MaxLength(50)]
    public string? Building { get; set; }

    [Required]
    [Range(1, int.MaxValue)]
    public int Capacity { get; set; }

    [Required]
    public RoomType Type { get; set; }

    public bool HasProjector { get; set; }
    public bool HasWhiteboard { get; set; }
    public bool HasVideoConference { get; set; }
    public bool HasAudioSystem { get; set; }
    public bool HasAirConditioning { get; set; }

    [MaxLength(500)]
    public string? OtherFacilities { get; set; }

    public bool IsActive { get; set; }
    public bool RequiresApproval { get; set; }
    public bool IsBookable { get; set; }
    public int? MaxBookingDurationHours { get; set; }
    public int? AdvanceBookingDays { get; set; }

    /// <summary>
    /// D-18 (lane 3a): deactivating a room that still has future bookings is refused unless this says to cancel them,
    /// each with the reason that the room was taken out of use. The form asks first, listing them
    /// (<c>GET rooms/{id}/retirement</c>).
    /// </summary>
    public bool CancelFutureBookings { get; set; }
}

/// <summary>
/// What retiring a room would touch (D-18, lane 3a): its future bookings, which deactivating it offers to cancel, and how
/// many bookings it has on record — any at all, and it cannot be deleted, only deactivated, so its history stays readable.
/// </summary>
public class RoomRetirementDto
{
    public Guid RoomId { get; set; }
    public string RoomName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public List<RoomBookingSummaryDto> FutureBookings { get; set; } = new();
    public int BookingsOnRecord { get; set; }
    public bool CanDelete => BookingsOnRecord == 0;
}

#endregion

#region Room Booking DTOs

/// <summary>
/// DTO for room booking read operations
/// </summary>
public class RoomBookingDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string BookingNumber { get; set; } = string.Empty;
    public Guid RoomId { get; set; }
    public string RoomName { get; set; } = string.Empty;
    public Guid? EventId { get; set; }
    public string? EventName { get; set; }
    public Guid BookedById { get; set; }
    public string BookedByName { get; set; } = string.Empty;
    public DateTime BookingDate { get; set; }
    public DateTime StartDateTime { get; set; }
    public DateTime EndDateTime { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public int ExpectedAttendees { get; set; }
    public string? SpecialRequirements { get; set; }
    public string? CateringRequirements { get; set; }
    public BookingStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public Guid? ApprovedById { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovalDate { get; set; }
    public bool IsCancelled { get; set; }
    public DateTime? CancellationDate { get; set; }
    public string? CancellationReason { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Summary DTO for room booking list views
/// </summary>
public class RoomBookingSummaryDto
{
    public Guid Id { get; set; }
    public string BookingNumber { get; set; } = string.Empty;
    public Guid RoomId { get; set; }
    public string RoomName { get; set; } = string.Empty;
    public Guid BookedById { get; set; }
    public string BookedByName { get; set; } = string.Empty;
    public DateTime StartDateTime { get; set; }
    public DateTime EndDateTime { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public BookingStatus Status { get; set; }
    public string StatusName => Status.ToString();
}

/// <summary>
/// DTO for creating a room booking
/// </summary>
public class CreateRoomBookingDto : CreateDtoBase
{
    [Required]
    public Guid RoomId { get; set; }

    public Guid? EventId { get; set; }

    [Required]
    public DateTime StartDateTime { get; set; }

    [Required]
    public DateTime EndDateTime { get; set; }

    [Required]
    [MaxLength(500)]
    public string Purpose { get; set; } = string.Empty;

    [Required]
    [Range(1, int.MaxValue)]
    public int ExpectedAttendees { get; set; }

    [MaxLength(1000)]
    public string? SpecialRequirements { get; set; }

    [MaxLength(1000)]
    public string? CateringRequirements { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

/// <summary>
/// DTO for updating a room booking
/// </summary>
public class UpdateRoomBookingDto : UpdateDtoBase
{
    [Required]
    public DateTime StartDateTime { get; set; }

    [Required]
    public DateTime EndDateTime { get; set; }

    [Required]
    [MaxLength(500)]
    public string Purpose { get; set; } = string.Empty;

    [Required]
    [Range(1, int.MaxValue)]
    public int ExpectedAttendees { get; set; }

    [MaxLength(1000)]
    public string? SpecialRequirements { get; set; }

    [MaxLength(1000)]
    public string? CateringRequirements { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

/// <summary>
/// DTO for cancelling a room booking
/// </summary>
public class CancelRoomBookingDto
{
    [Required]
    public Guid BookingId { get; set; }

    [Required]
    [MaxLength(1000)]
    public string CancellationReason { get; set; } = string.Empty;
}

#endregion

#region Company Milestone DTOs

/// <summary>
/// DTO for company milestone read operations
/// </summary>
public class CompanyMilestoneDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public MilestoneCategory Category { get; set; }
    public string CategoryName => Category.ToString();
    public DateTime MilestoneDate { get; set; }
    public bool IsRecurringAnnually { get; set; }
    public bool ShowOnCalendar { get; set; }
    public string? Significance { get; set; }
    public string? RelatedDocuments { get; set; }
}

/// <summary>
/// DTO for creating a company milestone
/// </summary>
public class CreateCompanyMilestoneDto : CreateDtoBase
{
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    public MilestoneCategory Category { get; set; }

    [Required]
    public DateTime MilestoneDate { get; set; }

    public bool IsRecurringAnnually { get; set; }
    public bool ShowOnCalendar { get; set; } = true;

    [MaxLength(1000)]
    public string? Significance { get; set; }

    [MaxLength(1000)]
    public string? RelatedDocuments { get; set; }
}

/// <summary>
/// DTO for updating a company milestone
/// </summary>
public class UpdateCompanyMilestoneDto : UpdateDtoBase
{
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    public MilestoneCategory Category { get; set; }

    [Required]
    public DateTime MilestoneDate { get; set; }

    public bool IsRecurringAnnually { get; set; }
    public bool ShowOnCalendar { get; set; }

    [MaxLength(1000)]
    public string? Significance { get; set; }

    [MaxLength(1000)]
    public string? RelatedDocuments { get; set; }
}

#endregion

#region Business Closure DTOs

/// <summary>
/// DTO for business closure read operations
/// </summary>
public class BusinessClosureDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public ClosureType Type { get; set; }
    public string TypeName => Type.ToString();
    public bool AffectsAllStations { get; set; }
    public Guid? LocationId { get; set; }
    public string? LocationName { get; set; }
    public Guid? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }

    /// <summary>The unit an organisation-unit closure covers, with everything beneath it (D-1, D-5).</summary>
    public Guid? OrganizationUnitId { get; set; }
    public string? OrganizationUnitName { get; set; }

    /// <summary>
    /// Who the closure covers, read from its type, as a sentence for the register: "Whole company",
    /// "Site: Tema", "Unit: Finance and everything beneath it".
    /// </summary>
    public string ScopeDescription { get; set; } = string.Empty;

    /// <summary>Falls on the same month and day every later year (C-38).</summary>
    public bool RecursAnnually { get; set; }

    public bool IsPaidClosure { get; set; }
    public bool CountsAsWorkingDay { get; set; }
    public DateTime AnnouncementDate { get; set; }
    public Guid? AnnouncedById { get; set; }
    public string? AnnouncedByName { get; set; }
    public string? CommunicationNotes { get; set; }

    /// <summary>
    /// Things the person saving should know that do not stop the save — the closure falls on a
    /// public holiday, or its site has no staff assigned to it. Set on create and update only.
    /// </summary>
    public List<string> Warnings { get; set; } = new();

    /// <summary>
    /// The granted leave this save recounted, and the leave in a finished year it left for HR to adjust
    /// (lane 1c, D-15a). Set on create and update only; null on a read.
    /// </summary>
    public LeaveRechargeResultDto? LeaveRecharge { get; set; }
}

/// <summary>
/// What announcing a closure would say, and to how many (company-schedule final closure, lane 1d:
/// L1-1) — the line behind "Announce to the N staff it covers".
/// </summary>
public class ClosureAnnouncementPreviewDto
{
    public Guid ClosureId { get; set; }

    /// <summary>Active staff the closure covers — the announcement's reach.</summary>
    public int StaffCovered { get; set; }

    /// <summary>False when nobody is covered: the screen says "no staff to tell" instead of offering the button.</summary>
    public bool CanAnnounce { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}

/// <summary>
/// One employee's closure days with each closure and its pay flag — the read payroll is pointed at
/// (lane 1d: D-15c). HR records whether staff are paid; payroll decides what an unpaid day is worth.
/// </summary>
public class EmployeeClosureDaysDto
{
    public Guid EmployeeId { get; set; }
    public List<EmployeeClosureDayDto> Days { get; set; } = new();
}

public class EmployeeClosureDayDto
{
    public DateOnly Date { get; set; }
    public Guid ClosureId { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool IsPaid { get; set; }

    /// <summary>True for a partial closure: the day is still worked.</summary>
    public bool IsWorkingDay { get; set; }
}

/// <summary>
/// DTO for creating a business closure
/// </summary>
public class CreateBusinessClosureDto : CreateDtoBase
{
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Reason { get; set; }

    [Required]
    public DateTime StartDate { get; set; }

    [Required]
    public DateTime EndDate { get; set; }

    [Required]
    public ClosureType Type { get; set; }

    public bool AffectsAllStations { get; set; } = true;
    public Guid? LocationId { get; set; }

    /// <summary>⚠ Retired (D-5): refused when set. Choose <see cref="OrganizationUnitId"/> instead.</summary>
    public Guid? DepartmentId { get; set; }

    public Guid? OrganizationUnitId { get; set; }
    public bool RecursAnnually { get; set; }
    public bool IsPaidClosure { get; set; }

    /// <summary>Follows the type: a partial closure counts as a working day, every other type does not.</summary>
    public bool CountsAsWorkingDay { get; set; }

    [MaxLength(1000)]
    public string? CommunicationNotes { get; set; }
}

/// <summary>
/// DTO for updating a business closure
/// </summary>
public class UpdateBusinessClosureDto : UpdateDtoBase
{
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Reason { get; set; }

    [Required]
    public DateTime StartDate { get; set; }

    [Required]
    public DateTime EndDate { get; set; }

    [Required]
    public ClosureType Type { get; set; }

    public bool AffectsAllStations { get; set; }
    public Guid? LocationId { get; set; }

    /// <summary>⚠ Retired (D-5): refused when set. Choose <see cref="OrganizationUnitId"/> instead.</summary>
    public Guid? DepartmentId { get; set; }

    public Guid? OrganizationUnitId { get; set; }
    public bool RecursAnnually { get; set; }
    public bool IsPaidClosure { get; set; }

    /// <summary>Follows the type: a partial closure counts as a working day, every other type does not.</summary>
    public bool CountsAsWorkingDay { get; set; }

    [MaxLength(1000)]
    public string? CommunicationNotes { get; set; }
}

#endregion

#region Fiscal Year DTOs

/// <summary>
/// DTO for fiscal year read operations
/// </summary>
public class FiscalYearDto : BaseDto
{
    public Guid TenantId { get; set; }
    public int Year { get; set; }
    public string FiscalYearName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsCurrent { get; set; }
    public FiscalYearStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public int PeriodCount { get; set; }
}

/// <summary>
/// Detailed DTO for fiscal year with periods
/// </summary>
public class FiscalYearDetailDto : FiscalYearDto
{
    public List<FiscalPeriodDto> Periods { get; set; } = new();
}

/// <summary>
/// DTO for creating a fiscal year
/// </summary>
public class CreateFiscalYearDto : CreateDtoBase
{
    [Required]
    [Range(2000, 2100)]
    public int Year { get; set; }

    [Required]
    [MaxLength(200)]
    public string FiscalYearName { get; set; } = string.Empty;

    [Required]
    public DateTime StartDate { get; set; }

    [Required]
    public DateTime EndDate { get; set; }

    public bool IsCurrent { get; set; }
}

/// <summary>
/// DTO for updating a fiscal year
/// </summary>
public class UpdateFiscalYearDto : UpdateDtoBase
{
    [Required]
    [MaxLength(200)]
    public string FiscalYearName { get; set; } = string.Empty;

    [Required]
    public DateTime StartDate { get; set; }

    [Required]
    public DateTime EndDate { get; set; }

    public bool IsCurrent { get; set; }
    public FiscalYearStatus Status { get; set; }
}

#endregion

#region Fiscal Period DTOs

/// <summary>
/// DTO for fiscal period read operations
/// </summary>
public class FiscalPeriodDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid FiscalYearId { get; set; }
    public string FiscalYearName { get; set; } = string.Empty;
    public int PeriodNumber { get; set; }
    public string PeriodName { get; set; } = string.Empty;
    public HRSchedulePeriodType Type { get; set; }
    public string TypeName => Type.ToString();
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsClosed { get; set; }
    public DateTime? ClosedDate { get; set; }
}

/// <summary>
/// DTO for creating a fiscal period
/// </summary>
public class CreateFiscalPeriodDto : CreateDtoBase
{
    [Required]
    public Guid FiscalYearId { get; set; }

    [Required]
    [Range(1, 12)]
    public int PeriodNumber { get; set; }

    [Required]
    [MaxLength(100)]
    public string PeriodName { get; set; } = string.Empty;

    [Required]
    public HRSchedulePeriodType Type { get; set; }

    [Required]
    public DateTime StartDate { get; set; }

    [Required]
    public DateTime EndDate { get; set; }
}

/// <summary>
/// DTO for updating a fiscal period
/// </summary>
public class UpdateFiscalPeriodDto : UpdateDtoBase
{
    [Required]
    [Range(1, 12)]
    public int PeriodNumber { get; set; }

    [Required]
    [MaxLength(100)]
    public string PeriodName { get; set; } = string.Empty;

    [Required]
    public HRSchedulePeriodType Type { get; set; }

    [Required]
    public DateTime StartDate { get; set; }

    [Required]
    public DateTime EndDate { get; set; }
}

/// <summary>
/// DTO for closing a fiscal period
/// </summary>
public class CloseFiscalPeriodDto
{
    [Required]
    public Guid PeriodId { get; set; }
}

#endregion


// ── The personal diary (round 4, D5) ───────────────────────────────────────────────────────────

/// <summary>One thing an employee is committed to, from any module that tracks commitments.</summary>
/// <remarks>
/// ⚠ Assembled by fanning out over the SAME <c>IPanelistCommitmentSource</c> implementations the
/// interview clash check uses. A diary written separately would have started identical and drifted,
/// and only one of them would learn about an eighth kind of commitment.
/// </remarks>
public class PersonalScheduleEntryDto
{
    /// <summary>Whose entry this is — the diary owner, or a team member on a unit read.</summary>
    public Guid SubjectId { get; set; }

    public CommitmentKind Kind { get; set; }
    public string KindName => Kind.ToString();

    /// <summary>
    /// How firm it is. ⚠ On the interview clash check <c>Hard</c> REFUSES a booking; in a diary
    /// nothing is refused and this is only a hint about how movable the entry is.
    /// </summary>
    public CommitmentHardness Hardness { get; set; }
    public string HardnessName => Hardness.ToString();

    public string Label { get; set; } = string.Empty;
    public DateTime Start { get; set; }
    public DateTime End { get; set; }

    /// <summary>
    /// ⚠ True when the source records whole DAYS — leave, travel, a closure, an all-day event. The
    /// times are the day's bounds, not a window, and a screen must not render them as hours.
    /// </summary>
    public bool IsDayGranular { get; set; }

    public string? Reference { get; set; }
}

/// <summary>Everything one employee is committed to between two dates.</summary>
public class PersonalScheduleDto
{
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public List<PersonalScheduleEntryDto> Entries { get; set; } = new();
}

/// <summary>The same for a whole organisation unit and everything beneath it.</summary>
/// <remarks>
/// ⚠ The SUBTREE. A head scheduling for their directorate means everybody under them; a unit-only
/// read would quietly leave out the sections reporting into it.
/// </remarks>
public class TeamScheduleDto
{
    public Guid OrganizationUnitId { get; set; }
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public List<PersonalScheduleDto> Members { get; set; } = new();
}

/// <summary>
/// One pass of the company-schedule reminder sweep (round 4, lane N-b2): which events it reminded and
/// chased, and how many emails that made. The scheduled run and HR's run-now return the same thing.
/// </summary>
/// <summary>One pass of the sweep's booking half (lane 3b-2): the booking numbers lapsed and completed.</summary>
public class RoomBookingSweepDto
{
    /// <summary>Still Tentative when their start came: cancelled, "Not approved before it started.", their bookers told.</summary>
    public List<string> Lapsed { get; set; } = new();

    /// <summary>Confirmed and ended: Completed, silently.</summary>
    public List<string> Completed { get; set; } = new();
}

public class CompanyScheduleReminderRunDto
{
    /// <summary>The bookings this pass lapsed (lane 3b-2, F-48).</summary>
    public List<string> BookingsLapsed { get; set; } = new();

    /// <summary>The bookings this pass completed (lane 3b-2).</summary>
    public List<string> BookingsCompleted { get; set; } = new();

    /// <summary>The event numbers whose reminder went this pass — each once, ever, per date.</summary>
    public List<string> Reminded { get; set; } = new();

    /// <summary>The event numbers whose unanswered invitations were chased this pass.</summary>
    public List<string> RsvpChased { get; set; } = new();

    /// <summary>
    /// The event numbers whose reminder was due and reached nobody (lane 2e-2): left due, so the next pass tries
    /// again until the event begins.
    /// </summary>
    public List<string> RemindersLeftDue { get; set; } = new();

    /// <summary>The event numbers whose chase was due and reached nobody, left due until the reply-by date.</summary>
    public List<string> ChasesLeftDue { get; set; } = new();

    /// <summary>
    /// The tasks whose assignee was chased this pass for being overdue (lane 2e-3, F-34) — once each, ever, unless
    /// the due date moves or the task passes to someone new. Counted here, not in the people counts below, which
    /// are the events' reminders and chases.
    /// </summary>
    public List<Guid> TasksChased { get; set; } = new();

    /// <summary>The overdue tasks whose chase reached nobody, left due for the next pass.</summary>
    public List<Guid> TasksLeftDue { get; set; } = new();

    /// <summary>The people this pass's reminders and chases were for, and how many it reached (lane 2e-2).</summary>
    public int PeopleIssued { get; set; }
    public int PeopleReached { get; set; }

    /// <summary>
    /// Emails the mail server took. ⚠ Until lane 2e-2 this counted every address tried. Under cross-module #40 the
    /// hourly host's emails find no mail server even where one is set up, so it counts them as not taken — truthfully.
    /// </summary>
    public int EmailsSent { get; set; }

    public int EmailsNotTaken { get; set; }

    public int ToldInApp { get; set; }

    /// <summary>The tenant's RSVP-chase lead, in days, as this pass read it.</summary>
    public int RsvpChaseLeadDays { get; set; }
}
