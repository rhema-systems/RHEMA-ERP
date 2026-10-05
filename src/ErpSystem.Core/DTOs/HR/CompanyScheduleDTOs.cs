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
    public Guid? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    
    // Participants
    public ParticipantScope Scope { get; set; }
    public string ScopeName => Scope.ToString();
    public int? EstimatedAttendees { get; set; }
    public bool RequiresRsvp { get; set; }
    public DateTime? RsvpDeadline { get; set; }
    
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
    public Guid? DepartmentId { get; set; }

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
    public Guid? DepartmentId { get; set; }

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

    // Status
    public EventStatus Status { get; set; }

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
}

/// <summary>
/// DTO for creating an event attachment
/// </summary>
public class CreateEventAttachmentDto : CreateDtoBase
{
    [Required]
    public Guid EventId { get; set; }

    [Required]
    [MaxLength(200)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;

    [Required]
    public EventAttachmentType Type { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }
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
    public string RoomName { get; set; } = string.Empty;
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
public class CompanyScheduleReminderRunDto
{
    /// <summary>The event numbers whose reminder went this pass — each once, ever, per date.</summary>
    public List<string> Reminded { get; set; } = new();

    /// <summary>The event numbers whose unanswered invitations were chased this pass.</summary>
    public List<string> RsvpChased { get; set; } = new();

    public int EmailsSent { get; set; }

    /// <summary>The tenant's RSVP-chase lead, in days, as this pass read it.</summary>
    public int RsvpChaseLeadDays { get; set; }
}
