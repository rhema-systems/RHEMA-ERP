using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.HR.StaffLeave;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.HR.CompanySchedule;

/// <summary>
/// Company event/activity
/// </summary>
public class CompanyEvent : TenantEntity
{
    public string EventNumber { get; set; } = string.Empty;
    public string EventName { get; set; } = string.Empty;
    public string? Description { get; set; }

    // Classification
    public EventCategory Category { get; set; } // Meeting, Training, Company Event, Deadline, Holiday
    public EventType Type { get; set; } // Internal, External, Client, Statutory
    public EventPriority Priority { get; set; }

    // Timing
    public DateTime StartDate { get; set; }
    public TimeSpan? StartTime { get; set; }
    public DateTime EndDate { get; set; }
    public TimeSpan? EndTime { get; set; }
    public bool IsAllDayEvent { get; set; }

    // Recurrence
    public bool IsRecurring { get; set; }
    public RecurrencePattern? RecurrencePattern { get; set; }
    public string? RecurrenceDetails { get; set; }
    public DateTime? RecurrenceEndDate { get; set; }
    public int? RecurrenceCount { get; set; }

    // Location
    public EventLocation LocationType { get; set; } // OnSite, OffSite, Virtual, Hybrid
    public string? VenueName { get; set; }
    public string? VenueAddress { get; set; }
    public string? OnlineMeetingLink { get; set; }
    public string? MeetingPassword { get; set; }

    public Guid? StationId { get; set; }
    public WorkStation? Station { get; set; }

    // Organizer
    public Guid OrganizerId { get; set; }
    public Employee Organizer { get; set; } = null!;

    public Guid? DepartmentId { get; set; }
    public Department? Department { get; set; }

    // Participants
    public ParticipantScope Scope { get; set; } // AllStaff, Department, Selected, External
    public int? EstimatedAttendees { get; set; }
    public bool RequiresRsvp { get; set; }
    public DateTime? RsvpDeadline { get; set; }

    // Visibility
    public EventVisibility Visibility { get; set; } // Public, Private, Department, Management
    public bool ShowOnCompanyCalendar { get; set; }
    public bool ShowOnIntranet { get; set; }

    // Status
    public EventStatus Status { get; set; }

    // Approval (for certain event types)
    public bool RequiresApproval { get; set; }
    public Guid? ApprovedById { get; set; }
    public Employee? ApprovedBy { get; set; }
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

    // Completion
    public DateTime? ActualStartTime { get; set; }
    public DateTime? ActualEndTime { get; set; }
    public int? ActualAttendance { get; set; }
    public string? OutcomeSummary { get; set; }

    // Cancellation
    public bool IsCancelled { get; set; }
    public DateTime? CancellationDate { get; set; }
    public string? CancellationReason { get; set; }

    public string? AdditionalNotes { get; set; }

    // Relations
    public ICollection<EventParticipant> Participants { get; set; } = new List<EventParticipant>();
    public ICollection<EventAttendance> AttendanceRecords { get; set; } = new List<EventAttendance>();
    public ICollection<EventAttachment> Attachments { get; set; } = new List<EventAttachment>();
    public ICollection<EventTask> Tasks { get; set; } = new List<EventTask>();
}

public class EventParticipant : TenantEntity
{
    public Guid EventId { get; set; }
    public CompanyEvent Event { get; set; } = null!;

    public Guid? EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public string? ExternalParticipantName { get; set; }
    public string? ExternalParticipantEmail { get; set; }
    public string? ExternalParticipantOrganization { get; set; }

    public ParticipantRole Role { get; set; } // Organizer, Presenter, Attendee, Optional
    public bool IsRequired { get; set; }

    // RSVP
    public InvitationStatus InvitationStatus { get; set; }
    public DateTime? InvitationSentDate { get; set; }
    public DateTime? ResponseDate { get; set; }
    public string? ResponseComments { get; set; }

    public string? SpecialRequirements { get; set; }
}

public class EventAttendance : TenantEntity
{
    public Guid EventId { get; set; }
    public CompanyEvent Event { get; set; } = null!;

    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public bool Attended { get; set; }
    public DateTime? CheckInTime { get; set; }
    public DateTime? CheckOutTime { get; set; }

    public string? AbsenceReason { get; set; }
    public string? Notes { get; set; }

    public Guid? MarkedById { get; set; }
    public Employee? MarkedBy { get; set; }
}

public class EventAttachment : TenantEntity
{
    public Guid EventId { get; set; }
    public CompanyEvent Event { get; set; } = null!;

    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public EventAttachmentType Type { get; set; } // Agenda, Minutes, Presentation, Resource
    public string? Description { get; set; }
    public DateTime UploadDate { get; set; }
}

public class EventTask : TenantEntity
{
    public Guid EventId { get; set; }
    public CompanyEvent Event { get; set; } = null!;

    public string TaskDescription { get; set; } = string.Empty;
    public TaskCategory Category { get; set; } // Preparation, During Event, Follow-up

    public Guid? AssignedToId { get; set; }
    public Employee? AssignedTo { get; set; }

    public DateTime? DueDate { get; set; }
    public TaskPriority Priority { get; set; }

    public Enums.TaskStatus Status { get; set; }
    public DateTime? CompletionDate { get; set; }
    public string? CompletionNotes { get; set; }

    public int DisplayOrder { get; set; }
}

/// <summary>
/// Meeting room/resource
/// </summary>
public class MeetingRoom : TenantEntity
{
    public string RoomCode { get; set; } = string.Empty;
    public string RoomName { get; set; } = string.Empty;
    public string? Description { get; set; }

    public Guid StationId { get; set; }
    public WorkStation Station { get; set; } = null!;

    public string Location { get; set; } = string.Empty;
    public string? Floor { get; set; }
    public string? Building { get; set; }

    public int Capacity { get; set; }
    public RoomType Type { get; set; } // Conference, Boardroom, Training, Huddle

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

    public ICollection<RoomBooking> Bookings { get; set; } = new List<RoomBooking>();
}

public class RoomBooking : TenantEntity
{
    public string BookingNumber { get; set; } = string.Empty;

    public Guid RoomId { get; set; }
    public MeetingRoom Room { get; set; } = null!;

    public Guid? EventId { get; set; }
    public CompanyEvent? Event { get; set; }

    public Guid BookedById { get; set; }
    public Employee BookedBy { get; set; } = null!;

    public DateTime BookingDate { get; set; }
    public DateTime StartDateTime { get; set; }
    public DateTime EndDateTime { get; set; }

    public string Purpose { get; set; } = string.Empty;
    public int ExpectedAttendees { get; set; }

    public string? SpecialRequirements { get; set; }
    public string? CateringRequirements { get; set; }

    public BookingStatus Status { get; set; }

    public Guid? ApprovedById { get; set; }
    public Employee? ApprovedBy { get; set; }
    public DateTime? ApprovalDate { get; set; }

    public bool IsCancelled { get; set; }
    public DateTime? CancellationDate { get; set; }
    public string? CancellationReason { get; set; }

    public string? Notes { get; set; }
}

/// <summary>
/// Important company dates/milestones
/// </summary>
public class CompanyMilestone : TenantEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    public MilestoneCategory Category { get; set; } // Anniversary, Achievement, Launch, Target
    public DateTime MilestoneDate { get; set; }

    public bool IsRecurringAnnually { get; set; }
    public bool ShowOnCalendar { get; set; }

    public string? Significance { get; set; }
    public string? RelatedDocuments { get; set; }
}

/// <summary>
/// Business closure/non-working days
/// </summary>
public class BusinessClosure : TenantEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Reason { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    public ClosureType Type { get; set; } // Full Closure, Partial Closure, Specific Departments

    public bool AffectsAllStations { get; set; }
    public Guid? StationId { get; set; }
    public WorkStation? Station { get; set; }

    public Guid? DepartmentId { get; set; }
    public Department? Department { get; set; }

    public bool IsPaidClosure { get; set; }
    public bool CountsAsWorkingDay { get; set; }

    public DateTime AnnouncementDate { get; set; }
    public Guid? AnnouncedById { get; set; }
    public Employee? AnnouncedBy { get; set; }

    public string? CommunicationNotes { get; set; }
}

/// <summary>
/// Fiscal year calendar
/// </summary>
public class FiscalYear : TenantEntity
{
    public int Year { get; set; }
    public string FiscalYearName { get; set; } = string.Empty;

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    public bool IsCurrent { get; set; }
    public FiscalYearStatus Status { get; set; }

    public ICollection<FiscalPeriod> Periods { get; set; } = new List<FiscalPeriod>();
}

public class FiscalPeriod : TenantEntity
{
    public Guid FiscalYearId { get; set; }
    public FiscalYear FiscalYear { get; set; } = null!;

    public int PeriodNumber { get; set; }
    public string PeriodName { get; set; } = string.Empty;

    public HRSchedulePeriodType Type { get; set; } // Quarter, Month
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    public bool IsClosed { get; set; }
    public DateTime? ClosedDate { get; set; }
}