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

    /// <summary>
    /// The series this event is one occurrence of (final closure D-2, D-12): shared by every
    /// occurrence generated from one recurring create. Null on a one-off event.
    /// </summary>
    /// <remarks>
    /// ⚠ There is no series table, and that is the decision rather than an omission. Each occurrence
    /// is a full event with its own number, guests, answers and attendance — people miss one week and
    /// not the next — so a series action ("this and following", "the whole series") is a query on
    /// this id, and an ordinary event needs no special case anywhere.
    /// </remarks>
    public Guid? RecurrenceSeriesId { get; set; }

    /// <summary>1-based position within the series ("Occurrence 3 of 10"); null on a one-off event.</summary>
    public int? OccurrenceNumber { get; set; }

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

    /// <summary>
    /// The site this event is held at. Points at <see cref="Location"/> — the live location tree
    /// (structure → level → location) that the rest of HR uses — not the vestigial
    /// <c>WorkStation</c> these three FKs originally referenced, which has an empty table, no
    /// repository implementation and no endpoint. Repointed 2026-08-28 when the schedule screens
    /// were built, because a required FK with no lookup makes its form unfillable.
    /// </summary>
    public Guid? LocationId { get; set; }

    [ForeignKey(nameof(LocationId))]
    public virtual Location? SiteLocation { get; set; }

    // Organizer
    public Guid OrganizerId { get; set; }
    
    [ForeignKey(nameof(OrganizerId))]
    public virtual Employee Organizer { get; set; } = null!;

    /// <summary>
    /// ⚠ Retiring. Replaced outright by <see cref="OrganizationUnitId"/> (final closure D-5); no row on UAT
    /// or the dev databases carried one (2026-10-04). Kept while existing code reads it; a later migration drops it.
    /// </summary>
    public Guid? DepartmentId { get; set; }
    
    [ForeignKey(nameof(DepartmentId))]
    public virtual Department? Department { get; set; }

    /// <summary>
    /// The organisation unit a <c>Scope = Department</c> event is for (final closure D-5). The audience
    /// is the unit and everything beneath it, through the HR audience resolver.
    /// </summary>
    public Guid? OrganizationUnitId { get; set; }

    [ForeignKey(nameof(OrganizationUnitId))]
    public virtual OrganizationUnit? OrganizationUnit { get; set; }

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

    // Reminders — sent by the company-schedule reminder sweep (round 4, lane N-b2): the event reminder
    // ReminderDaysBefore days ahead when SendReminders is on, and the RSVP chase ahead of RsvpDeadline.
    // Each is sent ONCE, and the sent-date is what says so; HR's manual buttons stamp the same dates,
    // and a reschedule clears them, since a reminder for the old date reminds nobody of the new one.
    public bool SendReminders { get; set; }
    public int? ReminderDaysBefore { get; set; }
    public DateTime? ReminderSentDate { get; set; }
    public DateTime? RsvpReminderSentDate { get; set; }

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

    /// <summary>
    /// ⚠ <b>When the event was moved, not what it was moved from.</b> The name reads like the
    /// latter and the screens read it like the latter, which is half of company-schedule defect C-2.
    /// The original window is <see cref="OriginalStartDate"/> and its three siblings; this stays as
    /// it is because other readers already treat it as a timestamp.
    /// </summary>
    public DateTime? RescheduledDate { get; set; }

    /// <summary>
    /// What this event was originally scheduled for, kept when it is first moved (round 4, D7; C-2).
    /// </summary>
    /// <remarks>
    /// <para>⚠ The reschedule used to overwrite <c>StartDate</c>/<c>EndDate</c> and keep nothing,
    /// while the dialog told the user the original was retained. Nothing anywhere remembered it.
    /// The interview path solved this in lane C with <c>JobInterview.OriginalDate</c>; these four
    /// are the same repair for an event, which carries a date AND a time at each end.</para>
    ///
    /// <para>⚠ Set on the FIRST move only. "When was this originally going to be?" has one answer,
    /// and refreshing it on each move would make a twice-moved event claim it was always meant for
    /// whenever it last sat.</para>
    /// </remarks>
    public DateTime? OriginalStartDate { get; set; }
    public TimeSpan? OriginalStartTime { get; set; }
    public DateTime? OriginalEndDate { get; set; }
    public TimeSpan? OriginalEndTime { get; set; }

    [MaxLength(2000)]
    public string? RescheduleReason { get; set; }

    [MaxLength(2000)]
    public string? AdditionalNotes { get; set; }

    /// <summary>
    /// The record that created this event, when another module did (final closure D-9, C-51): today
    /// only the SHE emergency drill, which schedules its next drill as a company event. Null when
    /// somebody created the event by hand.
    /// </summary>
    [MaxLength(50)]
    public string? SourceEntityType { get; set; }

    /// <summary>The id of the <see cref="SourceEntityType"/> record. No FK: the source may live in any module.</summary>
    public Guid? SourceEntityId { get; set; }

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

    // ── the controlled upload gate — final closure D-3; the StaffRequisitionAttachment pattern. ──

    /// <summary>Who uploaded it, as an Employee id from the token. Null on rows from before the gate.</summary>
    public Guid? UploadedById { get; set; }

    [ForeignKey(nameof(UploadedById))]
    public virtual Employee? UploadedBy { get; set; }

    public long? FileSizeBytes { get; set; }

    /// <summary>Scanned controlled upload backing this attachment.</summary>
    /// <remarks>
    /// ⚠ Null on rows written before event attachments moved onto the gate, where
    /// <see cref="FilePath"/> arrived from the caller's payload and no file was ever stored (F-54).
    /// Those read "reference only — no file stored" and offer no download.
    /// </remarks>
    public Guid? FileUploadRecordId { get; set; }

    /// <summary>Central-DMS record, once registered.</summary>
    public Guid? DocumentRecordId { get; set; }

    /// <summary>Central-DMS version, once registered.</summary>
    public Guid? DocumentVersionId { get; set; }
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

    /// <summary>
    /// The site this room belongs to. See the note on <see cref="CompanyEvent.LocationId"/> for why
    /// this points at <see cref="Location"/> rather than the vestigial <c>WorkStation</c>. The
    /// navigation is <c>SiteLocation</c> and not <c>Location</c> because <see cref="Location"/>
    /// below is already taken by the free-text placement within that site.
    /// </summary>
    public Guid LocationId { get; set; }

    [ForeignKey(nameof(LocationId))]
    public virtual Location SiteLocation { get; set; } = null!;

    /// <summary>Where the room sits within the site, e.g. "East wing, past reception".</summary>
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

    /// <summary>
    /// Free-text references ("ISO certificate no. …, filed with Admin"). Not the documents themselves:
    /// those are <see cref="Documents"/>, real files on the upload gate (final closure D-3).
    /// </summary>
    [MaxLength(1000)]
    public string? RelatedDocuments { get; set; }

    public virtual ICollection<CompanyMilestoneDocument> Documents { get; set; } = new List<CompanyMilestoneDocument>();
}

/// <summary>
/// A file evidencing a company milestone — the certificate, the licence, the opening photograph
/// (final closure D-3). Stored through the controlled upload gate: scanned, registered in the
/// central DMS, served only through the download door.
/// </summary>
/// <remarks>
/// The milestone's evidence lives here rather than in a link to one employee's award or
/// certification (D-17): a company milestone is a company fact.
/// </remarks>
public class CompanyMilestoneDocument : TenantEntity
{
    public Guid MilestoneId { get; set; }

    [ForeignKey(nameof(MilestoneId))]
    public virtual CompanyMilestone Milestone { get; set; } = null!;

    [MaxLength(200)]
    public string FileName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public DateTime UploadDate { get; set; }

    /// <summary>Who uploaded it, as an Employee id from the token — the upload helper refuses a caller without one.</summary>
    public Guid UploadedById { get; set; }

    [ForeignKey(nameof(UploadedById))]
    public virtual Employee UploadedBy { get; set; } = null!;

    public long? FileSizeBytes { get; set; }

    /// <summary>Scanned controlled upload backing this document.</summary>
    public Guid? FileUploadRecordId { get; set; }

    /// <summary>Central-DMS record, once registered.</summary>
    public Guid? DocumentRecordId { get; set; }

    /// <summary>Central-DMS version, once registered.</summary>
    public Guid? DocumentVersionId { get; set; }
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

    /// <summary>True when the closure applies company-wide rather than to one site.</summary>
    public bool AffectsAllStations { get; set; }

    /// <summary>
    /// The single site affected when <see cref="AffectsAllStations"/> is false. See the note on
    /// <see cref="CompanyEvent.LocationId"/> for why this points at <see cref="Location"/>.
    /// </summary>
    public Guid? LocationId { get; set; }

    [ForeignKey(nameof(LocationId))]
    public virtual Location? SiteLocation { get; set; }

    /// <summary>
    /// ⚠ Retiring. Replaced outright by <see cref="OrganizationUnitId"/> (final closure D-5); no row on UAT
    /// or the dev databases carried one (2026-10-04). Kept while existing code reads it; a later migration drops it.
    /// </summary>
    public Guid? DepartmentId { get; set; }

    [ForeignKey(nameof(DepartmentId))]
    public virtual Department? Department { get; set; }

    /// <summary>
    /// The organisation unit an organisation-unit closure covers, with everything beneath it (final
    /// closure D-1, D-5). The closure's type decides which of site, unit or whole company applies.
    /// </summary>
    public Guid? OrganizationUnitId { get; set; }

    [ForeignKey(nameof(OrganizationUnitId))]
    public virtual OrganizationUnit? OrganizationUnit { get; set; }

    /// <summary>
    /// The closure falls on the same month and day every later year — the year-end stocktake typed
    /// once (final closure D-9, C-38).
    /// </summary>
    public bool RecursAnnually { get; set; }

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
