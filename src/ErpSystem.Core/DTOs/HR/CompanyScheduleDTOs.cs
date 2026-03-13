using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.HR;

// List DTO
public class CompanyEventListDto
{
    public Guid Id { get; set; }
    public string EventNumber { get; set; }
    public string EventName { get; set; }
    public EventCategory Category { get; set; }
    public string CategoryName { get; set; }
    public EventType EventType { get; set; }
    public string EventTypeName { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public EventStatus Status { get; set; }
    public string StatusName { get; set; }
    public EventLocation Location { get; set; }
    public string LocationName { get; set; }
    public string VenueName { get; set; }
    public string OrganizerName { get; set; }
    public int TotalParticipants { get; set; }
    public int ConfirmedParticipants { get; set; }
}

// Detail DTO
public class CompanyEventDetailDto
{
    public Guid Id { get; set; }
    public string EventNumber { get; set; }

    // Event Details
    public string EventName { get; set; }
    public string Description { get; set; }
    public EventCategory Category { get; set; }
    public string CategoryName { get; set; }
    public EventType EventType { get; set; }
    public string EventTypeName { get; set; }
    public EventPriority Priority { get; set; }
    public string PriorityName { get; set; }

    // Date & Time
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }
    public bool IsAllDayEvent { get; set; }

    // Recurrence
    public bool IsRecurring { get; set; }
    public RecurrencePattern? RecurrencePattern { get; set; }
    public string RecurrencePatternName { get; set; }
    public DateTime? RecurrenceEndDate { get; set; }
    public string RecurrenceDetails { get; set; }

    // Location
    public EventLocation Location { get; set; }
    public string LocationName { get; set; }
    public string VenueName { get; set; }
    public string VenueAddress { get; set; }
    public string OnlineMeetingLink { get; set; }
    public string MeetingPassword { get; set; }

    // Organizer
    public Guid OrganizerId { get; set; }
    public string OrganizerName { get; set; }
    public Guid? DepartmentId { get; set; }
    public string DepartmentName { get; set; }
    public Guid? StationId { get; set; }
    public string StationName { get; set; }

    // Participants
    public ParticipantScope ParticipantScope { get; set; }
    public string ParticipantScopeName { get; set; }
    public int ExpectedParticipants { get; set; }

    // Requirements
    public string RequiredResources { get; set; }
    public string CateringRequirements { get; set; }
    public string TechnicalRequirements { get; set; }

    // Budget
    public string BudgetCode { get; set; }
    public decimal? BudgetAmount { get; set; }
    public decimal? ActualCost { get; set; }

    // Status
    public EventStatus Status { get; set; }
    public string StatusName { get; set; }

    // Visibility
    public EventVisibility Visibility { get; set; }
    public string VisibilityName { get; set; }
    public bool RequiresRsvp { get; set; }
    public DateTime? RsvpDeadline { get; set; }

    // Approval
    public bool RequiresApproval { get; set; }
    public Guid? ApprovedById { get; set; }
    public string ApprovedByName { get; set; }
    public DateTime? ApprovalDate { get; set; }

    // Outcome
    public DateTime? CompletionDate { get; set; }
    public string OutcomeSummary { get; set; }

    // Cancellation
    public DateTime? CancellationDate { get; set; }
    public string CancellationReason { get; set; }

    public string AdditionalNotes { get; set; }

    // Collections
    public List<EventParticipantDto> Participants { get; set; }
    public List<EventAttachmentDto> Attachments { get; set; }
    public List<EventTaskDto> Tasks { get; set; }

    // Audit
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

// Create DTO
public class CreateCompanyEventDto
{
    public string EventName { get; set; }
    public string Description { get; set; }
    public EventCategory Category { get; set; }
    public EventType EventType { get; set; }
    public EventPriority Priority { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }
    public bool IsAllDayEvent { get; set; }
    public bool IsRecurring { get; set; }
    public RecurrencePattern? RecurrencePattern { get; set; }
    public DateTime? RecurrenceEndDate { get; set; }
    public string RecurrenceDetails { get; set; }
    public EventLocation Location { get; set; }
    public string VenueName { get; set; }
    public string VenueAddress { get; set; }
    public string OnlineMeetingLink { get; set; }
    public string MeetingPassword { get; set; }
    public Guid? DepartmentId { get; set; }
    public Guid? StationId { get; set; }
    public ParticipantScope ParticipantScope { get; set; }
    public int ExpectedParticipants { get; set; }
    public string RequiredResources { get; set; }
    public string CateringRequirements { get; set; }
    public string TechnicalRequirements { get; set; }
    public string BudgetCode { get; set; }
    public decimal? BudgetAmount { get; set; }
    public EventVisibility Visibility { get; set; }
    public bool RequiresRsvp { get; set; }
    public DateTime? RsvpDeadline { get; set; }
    public bool RequiresApproval { get; set; }
}

// Update DTO
public class UpdateCompanyEventDto
{
    public Guid Id { get; set; }
    public string EventName { get; set; }
    public string Description { get; set; }
    public EventPriority Priority { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }
    public string VenueName { get; set; }
    public string VenueAddress { get; set; }
    public string OnlineMeetingLink { get; set; }
    public int ExpectedParticipants { get; set; }
    public string RequiredResources { get; set; }
    public string CateringRequirements { get; set; }
    public string TechnicalRequirements { get; set; }
    public decimal? BudgetAmount { get; set; }
    public string AdditionalNotes { get; set; }
}

// Participant DTOs
public class EventParticipantDto
{
    public Guid Id { get; set; }
    public Guid? EmployeeId { get; set; }
    public string EmployeeName { get; set; }
    public string EmployeeNumber { get; set; }
    public ParticipantRole Role { get; set; }
    public string RoleName { get; set; }
    public string ExternalParticipantName { get; set; }
    public string ExternalParticipantEmail { get; set; }
    public string ExternalParticipantOrganization { get; set; }
    public InvitationStatus InvitationStatus { get; set; }
    public string InvitationStatusName { get; set; }
    public DateTime? InvitationSentDate { get; set; }
    public DateTime? ResponseDate { get; set; }
    public string ResponseComments { get; set; }
    public string SpecialRequirements { get; set; }
}

public class AddEventParticipantDto
{
    public Guid EventId { get; set; }
    public Guid? EmployeeId { get; set; }
    public ParticipantRole Role { get; set; }
    public string ExternalParticipantName { get; set; }
    public string ExternalParticipantEmail { get; set; }
    public string ExternalParticipantOrganization { get; set; }
}

public class RespondToEventInvitationDto
{
    public Guid ParticipantId { get; set; }
    public InvitationStatus Response { get; set; }
    public string Comments { get; set; }
}

// Attendance DTO
public class EventAttendanceDto
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; }
    public string EmployeeNumber { get; set; }
    public bool IsPresent { get; set; }
    public DateTime? CheckInTime { get; set; }
    public DateTime? CheckOutTime { get; set; }
    public string AbsenceReason { get; set; }
    public string Notes { get; set; }
}

// Task DTOs
public class EventTaskDto
{
    public Guid Id { get; set; }
    public TaskCategory TaskCategory { get; set; }
    public string TaskCategoryName { get; set; }
    public string TaskDescription { get; set; }
    public TaskPriority Priority { get; set; }
    public string PriorityName { get; set; }
    public Guid? AssignedToId { get; set; }
    public string AssignedToName { get; set; }
    public DateTime? DueDate { get; set; }
    public Enums.TaskStatus Status { get; set; }
    public string StatusName { get; set; }
    public DateTime? CompletionDate { get; set; }
    public string CompletionNotes { get; set; }
    public int DisplayOrder { get; set; }
}

// Attachment DTO
public class EventAttachmentDto
{
    public Guid Id { get; set; }
    public EventAttachmentType AttachmentType { get; set; }
    public string AttachmentTypeName { get; set; }
    public string FileName { get; set; }
    public string FilePath { get; set; }
    public long FileSize { get; set; }
    public string Description { get; set; }
    public DateTime UploadedAt { get; set; }
}

// Meeting Room DTOs
public class MeetingRoomListDto
{
    public Guid Id { get; set; }
    public string RoomCode { get; set; }
    public string RoomName { get; set; }
    public RoomType RoomType { get; set; }
    public string RoomTypeName { get; set; }
    public string Location { get; set; }
    public string StationName { get; set; }
    public int Capacity { get; set; }
    public bool HasProjector { get; set; }
    public bool HasVideoConference { get; set; }
    public bool IsActive { get; set; }
    public int UpcomingBookings { get; set; }
}

public class RoomBookingDto
{
    public Guid Id { get; set; }
    public string BookingNumber { get; set; }
    public Guid RoomId { get; set; }
    public string RoomName { get; set; }
    public Guid? EventId { get; set; }
    public string EventName { get; set; }
    public DateTime StartDateTime { get; set; }
    public DateTime EndDateTime { get; set; }
    public string Purpose { get; set; }
    public int ExpectedAttendees { get; set; }
    public BookingStatus Status { get; set; }
    public string StatusName { get; set; }
    public string BookedByName { get; set; }
    public string CateringRequirements { get; set; }
}

// Calendar View DTOs
public class CalendarEventDto
{
    public Guid Id { get; set; }
    public string EventNumber { get; set; }
    public string EventName { get; set; }
    public EventCategory Category { get; set; }
    public string CategoryName { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }
    public bool IsAllDayEvent { get; set; }
    public EventLocation Location { get; set; }
    public string VenueName { get; set; }
    public EventStatus Status { get; set; }
    public string StatusName { get; set; }
    public string OrganizerName { get; set; }
    public bool IsParticipant { get; set; }
    public InvitationStatus? MyInvitationStatus { get; set; }
}

public class MonthlyCalendarDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string MonthName { get; set; }
    public List<CalendarEventDto> Events { get; set; }
    public List<DateTime> PublicHolidays { get; set; }
    public List<DateTime> Weekends { get; set; }
}

// Dashboard DTO
public class CompanyScheduleDashboardDto
{
    public int TotalEventsThisMonth { get; set; }
    public int UpcomingEvents { get; set; }
    public int MyUpcomingEvents { get; set; }
    public int PendingRsvp { get; set; }
    public int RoomBookingsToday { get; set; }
    public Dictionary<EventCategory, int> EventsByCategory { get; set; }
    public List<CompanyEventListDto> ThisWeekEvents { get; set; }
    public List<CompanyEventListDto> MyEvents { get; set; }
    public List<RoomBookingDto> TodayRoomBookings { get; set; }
}