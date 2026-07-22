using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.HR.CompanySchedule;

/// <summary>
/// Company event/activity
/// </summary>
public class CompanyEvent : TenantEntity
{
    [MaxLength(50)]
    public string EventNumber { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(100)]
    public string EventName { get; set; } = string.Empty;
    
    [MaxLength(1000)]
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
    
    [MaxLength(500)]
    public string? RecurrenceDetails { get; set; }
    
    public DateTime? RecurrenceEndDate { get; set; }
    public int? RecurrenceCount { get; set; }

    // Location
    public EventLocation LocationType { get; set; } // OnSite, OffSite, Virtual, Hybrid

    [MaxLength(200)]
    public string? VenueName { get; set; }
    
    [MaxLength(500)]
    public string? VenueAddress { get; set; }
    
    [MaxLength(700)]
    public string? OnlineMeetingLink { get; set; }
    
    [MaxLength(100)]
    public string? MeetingPassword { get; set; }

    public Guid? StationId { get; set; }

    [ForeignKey(nameof(StationId))]
    public virtual WorkStation? Station { get; set; }

    // Organizer
    public Guid OrganizerId { get; set; }
    
    [ForeignKey(nameof(OrganizerId))]
    public virtual Employee Organizer { get; set; } = null!;

    public Guid? DepartmentId { get; set; }
    
    [ForeignKey(nameof(DepartmentId))]
    public virtual Department? Department { get; set; }

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
    
    [ForeignKey(nameof(ApprovedById))]
    public virtual Employee? ApprovedBy { get; set; }
    
    public DateTime? ApprovalDate { get; set; }

    // Budget
    public bool HasBudget { get; set; }
    public decimal? BudgetAmount { get; set; }
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
    public DateTime? ReminderSentDate { get; set; }

    // Completion
    public DateTime? ActualStartTime { get; set; }
    public DateTime? ActualEndTime { get; set; }
    public int? ActualAttendance { get; set; }
    
    [MaxLength(2000)]
    public string? OutcomeSummary { get; set; }

    // Cancellation
    public bool IsCancelled { get; set; }
    public DateTime? CancellationDate { get; set; }

    [MaxLength(1000)]
    public string? CancellationReason { get; set; }

    // Reschedule
    public bool IsRescheduled { get; set; }
    public DateTime? RescheduledDate { get; set; }
    
    [MaxLength(2000)]
    public string? RescheduleReason { get; set; }

    [MaxLength(2000)]
    public string? AdditionalNotes { get; set; }

    public virtual ICollection<EventParticipant> Participants { get; set; } = new List<EventParticipant>();
    public virtual ICollection<EventAttendance> AttendanceRecords { get; set; } = new List<EventAttendance>();
    public virtual ICollection<EventAttachment> Attachments { get; set; } = new List<EventAttachment>();
    public virtual ICollection<EventTask> Tasks { get; set; } = new List<EventTask>();
}

public class EventParticipant : TenantEntity
{
    public Guid EventId { get; set; }

    [ForeignKey(nameof(EventId))]
    public virtual CompanyEvent Event { get; set; } = null!;

    public Guid? EmployeeId { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee? Employee { get; set; }

    [MaxLength(100)]
    public string? ExternalParticipantName { get; set; }
    
    [MaxLength(100)]
    public string? ExternalParticipantEmail { get; set; }
    
    [MaxLength(100)]
    public string? ExternalParticipantOrganization { get; set; }

    public ParticipantRole Role { get; set; } // Organizer, Presenter, Attendee, Optional
    public bool IsRequired { get; set; }

    // RSVP
    public InvitationStatus InvitationStatus { get; set; }
    public DateTime? InvitationSentDate { get; set; }
    public DateTime? ResponseDate { get; set; }
    
    [MaxLength(1000)]
    public string? ResponseComments { get; set; }

    [MaxLength(1000)]
    public string? SpecialRequirements { get; set; }
}

public class EventAttendance : TenantEntity
{
    public Guid EventId { get; set; }

    [ForeignKey(nameof(EventId))]
    public virtual CompanyEvent Event { get; set; } = null!;

    public Guid EmployeeId { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    public bool Attended { get; set; }
    public DateTime? CheckInTime { get; set; }
    public DateTime? CheckOutTime { get; set; }

    [MaxLength(1000)]
    public string? AbsenceReason { get; set; }
    
    [MaxLength(1000)]
    public string? Notes { get; set; }

    public Guid? MarkedById { get; set; }

    [ForeignKey(nameof(MarkedById))]
    public virtual Employee? MarkedBy { get; set; }
}

public class EventAttachment : TenantEntity
{
    public Guid EventId { get; set; }

    [ForeignKey(nameof(EventId))]
    public virtual CompanyEvent Event { get; set; } = null!;

    [MaxLength(200)]
    public string FileName { get; set; } = string.Empty;
    
    [MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;
    
    public EventAttachmentType Type { get; set; } // Agenda, Minutes, Presentation, Resource

    [MaxLength(1000)]
    public string? Description { get; set; }
    
    public DateTime UploadDate { get; set; }
}

public class EventTask : TenantEntity
{
    public Guid EventId { get; set; }

    [ForeignKey(nameof(EventId))]
    public virtual CompanyEvent Event { get; set; } = null!;

    [MaxLength(1000)]
    public string TaskDescription { get; set; } = string.Empty;
    
    public EventTaskCategory Category { get; set; }

    public Guid? AssignedToId { get; set; }

    [ForeignKey(nameof(AssignedToId))]
    public virtual Employee? AssignedTo { get; set; }

    public DateTime? DueDate { get; set; }
    public TaskPriority Priority { get; set; }

    public EventTaskStatus Status { get; set; }
    public DateTime? CompletionDate { get; set; }

    [MaxLength(1000)]
    public string? CompletionNotes { get; set; }
}

/// <summary>
/// Meeting room/resource
/// </summary>
public class MeetingRoom : TenantEntity
{
    [MaxLength(50)]
    public string RoomCode { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(100)]
    public string RoomName { get; set; } = string.Empty;
    
    [MaxLength(500)]
    public string? Description { get; set; }

    public Guid StationId { get; set; }

    [ForeignKey(nameof(StationId))]
    public virtual WorkStation Station { get; set; } = null!;

    [MaxLength(200)]
    public string Location { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? Floor { get; set; }
    
    [MaxLength(50)]
    public string? Building { get; set; }

    public int Capacity { get; set; }
    
    public RoomType Type { get; set; }

    // Facilities
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

    public virtual ICollection<RoomBooking> Bookings { get; set; } = new List<RoomBooking>();
}

public class RoomBooking : TenantEntity
{
    [MaxLength(50)]
    public string BookingNumber { get; set; } = string.Empty;

    public Guid RoomId { get; set; }

    [ForeignKey(nameof(RoomId))]
    public virtual MeetingRoom Room { get; set; } = null!;

    public Guid? EventId { get; set; }

    [ForeignKey(nameof(EventId))]
    public virtual CompanyEvent? Event { get; set; }

    public Guid BookedById { get; set; }

    [ForeignKey(nameof(BookedById))]
    public virtual Employee BookedBy { get; set; } = null!;

    public DateTime BookingDate { get; set; }
    public DateTime StartDateTime { get; set; }
    public DateTime EndDateTime { get; set; }

    [MaxLength(500)]
    public string Purpose { get; set; } = string.Empty;
    
    public int ExpectedAttendees { get; set; }

    [MaxLength(1000)]
    public string? SpecialRequirements { get; set; }
    
    [MaxLength(1000)]
    public string? CateringRequirements { get; set; }

    public BookingStatus Status { get; set; }

    public Guid? ApprovedById { get; set; }

    [ForeignKey(nameof(ApprovedById))]
    public virtual Employee? ApprovedBy { get; set; }
    
    public DateTime? ApprovalDate { get; set; }

    public bool IsCancelled { get; set; }
    public DateTime? CancellationDate { get; set; }

    [MaxLength(1000)]
    public string? CancellationReason { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

/// <summary>
/// Important company dates/milestones
/// </summary>
public class CompanyMilestone : TenantEntity
{
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public MilestoneCategory Category { get; set; }
    public DateTime MilestoneDate { get; set; }

    public bool IsRecurringAnnually { get; set; }
    public bool ShowOnCalendar { get; set; }

    [MaxLength(1000)]
    public string? Significance { get; set; }

    [MaxLength(1000)]
    public string? RelatedDocuments { get; set; }
}

/// <summary>
/// Business closure/non-working days
/// </summary>
public class BusinessClosure : TenantEntity
{
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;
    
    [MaxLength(1000)]
    public string? Reason { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    public ClosureType Type { get; set; }

    public bool AffectsAllStations { get; set; }
    
    public Guid? StationId { get; set; }
    
    [ForeignKey(nameof(StationId))]
    public virtual WorkStation? Station { get; set; }

    public Guid? DepartmentId { get; set; }

    [ForeignKey(nameof(DepartmentId))]
    public virtual Department? Department { get; set; }

    public bool IsPaidClosure { get; set; }
    public bool CountsAsWorkingDay { get; set; }

    public DateTime AnnouncementDate { get; set; }
    
    public Guid? AnnouncedById { get; set; }
    
    [ForeignKey(nameof(AnnouncedById))]
    public virtual Employee? AnnouncedBy { get; set; }

    [MaxLength(1000)]
    public string? CommunicationNotes { get; set; }
}

/// <summary>
/// Fiscal year calendar
/// </summary>
public class FiscalYear : TenantEntity
{
    public int Year { get; set; }

    [MaxLength(200)]
    public string FiscalYearName { get; set; } = string.Empty;

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    public bool IsCurrent { get; set; }
    public FiscalYearStatus Status { get; set; }

    public virtual ICollection<FiscalPeriod> Periods { get; set; } = new List<FiscalPeriod>();
}

public class FiscalPeriod : TenantEntity
{
    public Guid FiscalYearId { get; set; }

    [ForeignKey(nameof(FiscalYearId))]
    public virtual FiscalYear FiscalYear { get; set; } = null!;

    [MaxLength(50)]
    public int PeriodNumber { get; set; }

    [MaxLength(100)]
    public string PeriodName { get; set; } = string.Empty;

    public HRSchedulePeriodType Type { get; set; } // Quarter, Month
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    public bool IsClosed { get; set; }
    public DateTime? ClosedDate { get; set; }
}
