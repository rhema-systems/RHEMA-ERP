using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.CompanySchedule;

namespace ErpSystem.Core.Services.HR.Extensions;

public static class CompanyScheduleMappingExtensions
{
    #region CompanyEvent

    public static CompanyEventDto ToDto(this CompanyEvent entity) => Fill(new CompanyEventDto(), entity);

    /// <summary>
    /// Every field of the event, for the list read and the detail read alike.
    /// </summary>
    /// <remarks>
    /// ⚠ The detail read used to be a hand-made copy of this list, and it drifted: it never carried the
    /// original window, so the event page could not show what a moved event was moved from (F-58). One
    /// filler for both means a field added here reaches both.
    /// </remarks>
    private static T Fill<T>(T dto, CompanyEvent entity) where T : CompanyEventDto
    {
        dto.Id = entity.Id;
        dto.TenantId = entity.TenantId;
        dto.EventNumber = entity.EventNumber;
        dto.EventName = entity.EventName;
        dto.Description = entity.Description;
        dto.Category = entity.Category;
        dto.Type = entity.Type;
        dto.Priority = entity.Priority;
        dto.StartDate = entity.StartDate;
        dto.StartTime = entity.StartTime;
        dto.EndDate = entity.EndDate;
        dto.EndTime = entity.EndTime;
        dto.IsAllDayEvent = entity.IsAllDayEvent;
        dto.IsRecurring = entity.IsRecurring;
        dto.RecurrencePattern = entity.RecurrencePattern;
        dto.RecurrenceDetails = entity.RecurrenceDetails;
        dto.RecurrenceEndDate = entity.RecurrenceEndDate;
        dto.RecurrenceCount = entity.RecurrenceCount;
        dto.RecurrenceSeriesId = entity.RecurrenceSeriesId;
        dto.OccurrenceNumber = entity.OccurrenceNumber;
        dto.SourceEntityType = entity.SourceEntityType;
        dto.SourceEntityId = entity.SourceEntityId;
        dto.LocationType = entity.LocationType;
        dto.VenueName = entity.VenueName;
        dto.VenueAddress = entity.VenueAddress;
        dto.OnlineMeetingLink = entity.OnlineMeetingLink;
        dto.MeetingPassword = entity.MeetingPassword;
        dto.LocationId = entity.LocationId;
        dto.LocationName = entity.SiteLocation?.Name;
        dto.OrganizerId = entity.OrganizerId;
        dto.OrganizerName = entity.Organizer?.FullName ?? string.Empty;
        dto.DepartmentId = entity.DepartmentId;
        dto.DepartmentName = entity.Department?.Name;
        dto.OrganizationUnitId = entity.OrganizationUnitId;
        dto.OrganizationUnitName = entity.OrganizationUnit?.Name;
        dto.Scope = entity.Scope;
        dto.EstimatedAttendees = entity.EstimatedAttendees;
        dto.RequiresRsvp = entity.RequiresRsvp;
        dto.RsvpDeadline = entity.RsvpDeadline;
        dto.AudienceDescription = CompanyEventRules.DescribeAudience(entity, entity.OrganizationUnit?.Name);
        dto.Visibility = entity.Visibility;
        dto.ShowOnCompanyCalendar = entity.ShowOnCompanyCalendar;
        dto.ShowOnIntranet = entity.ShowOnIntranet;
        dto.Status = entity.Status;
        dto.RequiresApproval = entity.RequiresApproval;
        dto.ApprovedById = entity.ApprovedById;
        dto.ApprovedByName = entity.ApprovedBy?.FullName;
        dto.ApprovalDate = entity.ApprovalDate;
        dto.HasBudget = entity.HasBudget;
        dto.BudgetAmount = entity.BudgetAmount;
        dto.ActualCost = entity.ActualCost;
        dto.BudgetCode = entity.BudgetCode;
        dto.RequiredResources = entity.RequiredResources;
        dto.CateringRequirements = entity.CateringRequirements;
        dto.TechnicalRequirements = entity.TechnicalRequirements;
        dto.SendReminders = entity.SendReminders;
        dto.ReminderDaysBefore = entity.ReminderDaysBefore;
        // ⚠ As UTC: a datetime2 reads back unspecified and JSON then drops its Z (round 4, lane E).
        dto.ReminderSentDate = entity.ReminderSentDate is { } reminded ? DateTime.SpecifyKind(reminded, DateTimeKind.Utc) : null;
        dto.RsvpReminderSentDate = entity.RsvpReminderSentDate is { } chased ? DateTime.SpecifyKind(chased, DateTimeKind.Utc) : null;
        dto.ActualStartTime = entity.ActualStartTime;
        dto.ActualEndTime = entity.ActualEndTime;
        dto.ActualAttendance = entity.ActualAttendance;
        dto.OutcomeSummary = entity.OutcomeSummary;
        dto.IsCancelled = entity.IsCancelled;
        dto.CancellationDate = entity.CancellationDate;
        dto.CancellationReason = entity.CancellationReason;
        dto.IsRescheduled = entity.IsRescheduled;
        dto.RescheduledDate = entity.RescheduledDate;
        // Round 4, D7 (C-2). Null on an event that never moved, and on any moved before that
        // lane — the original was overwritten then and cannot be recovered.
        dto.OriginalStartDate = entity.OriginalStartDate;
        dto.OriginalStartTime = entity.OriginalStartTime;
        dto.OriginalEndDate = entity.OriginalEndDate;
        dto.OriginalEndTime = entity.OriginalEndTime;
        dto.RescheduleReason = entity.RescheduleReason;
        dto.AdditionalNotes = entity.AdditionalNotes;
        dto.CreatedAt = entity.CreatedAt;
        dto.CreatedBy = entity.CreatedBy ?? string.Empty;
        dto.UpdatedAt = entity.UpdatedAt;
        dto.UpdatedBy = entity.UpdatedBy;
        return dto;
    }

    public static CompanyEventSummaryDto ToSummaryDto(this CompanyEvent entity)
    {
        return new CompanyEventSummaryDto
        {
            Id = entity.Id,
            EventNumber = entity.EventNumber,
            EventName = entity.EventName,
            Category = entity.Category,
            StartDate = entity.StartDate,
            StartTime = entity.StartTime,
            EndDate = entity.EndDate,
            IsAllDayEvent = entity.IsAllDayEvent,
            LocationType = entity.LocationType,
            VenueName = entity.VenueName,
            Status = entity.Status,
            OrganizerName = entity.Organizer?.FullName ?? string.Empty,
            EstimatedAttendees = entity.EstimatedAttendees,
            RecurrenceSeriesId = entity.RecurrenceSeriesId,
            OccurrenceNumber = entity.OccurrenceNumber
        };
    }

    public static CompanyEventDetailDto ToDetailDto(this CompanyEvent entity)
    {
        var dto = Fill(new CompanyEventDetailDto(), entity);
        dto.Participants = entity.Participants?.Select(p => p.ToDto()).ToList() ?? new List<EventParticipantDto>();
        dto.AttendanceRecords = entity.AttendanceRecords?.Select(a => a.ToDto()).ToList() ?? new List<EventAttendanceDto>();
        dto.Attachments = entity.Attachments?.Select(a => a.ToDto()).ToList() ?? new List<EventAttachmentDto>();
        dto.Tasks = entity.Tasks?.Select(t => t.ToDto()).ToList() ?? new List<EventTaskDto>();
        return dto;
    }

    public static CompanyEvent ToEntity(this CreateCompanyEventDto dto)
    {
        return new CompanyEvent
        {
            EventName = dto.EventName,
            Description = dto.Description,
            Category = dto.Category,
            Type = dto.Type,
            Priority = dto.Priority,
            StartDate = dto.StartDate,
            StartTime = dto.StartTime,
            EndDate = dto.EndDate,
            EndTime = dto.EndTime,
            IsAllDayEvent = dto.IsAllDayEvent,
            IsRecurring = dto.IsRecurring,
            RecurrencePattern = dto.RecurrencePattern,
            RecurrenceDetails = dto.RecurrenceDetails,
            RecurrenceEndDate = dto.RecurrenceEndDate,
            RecurrenceCount = dto.RecurrenceCount,
            LocationType = dto.LocationType,
            VenueName = dto.VenueName,
            VenueAddress = dto.VenueAddress,
            OnlineMeetingLink = dto.OnlineMeetingLink,
            MeetingPassword = dto.MeetingPassword,
            LocationId = dto.LocationId,
            DepartmentId = dto.DepartmentId,
            OrganizationUnitId = dto.OrganizationUnitId,
            Scope = dto.Scope,
            EstimatedAttendees = dto.EstimatedAttendees,
            RequiresRsvp = dto.RequiresRsvp,
            RsvpDeadline = dto.RsvpDeadline,
            Visibility = dto.Visibility,
            ShowOnCompanyCalendar = dto.ShowOnCompanyCalendar,
            ShowOnIntranet = dto.ShowOnIntranet,
            RequiresApproval = dto.RequiresApproval,
            HasBudget = dto.HasBudget,
            BudgetAmount = dto.BudgetAmount,
            BudgetCode = dto.BudgetCode,
            RequiredResources = dto.RequiredResources,
            CateringRequirements = dto.CateringRequirements,
            TechnicalRequirements = dto.TechnicalRequirements,
            SendReminders = dto.SendReminders,
            ReminderDaysBefore = dto.ReminderDaysBefore,
            AdditionalNotes = dto.AdditionalNotes
        };
    }

    /// <summary>
    /// The fields an edit sets directly.
    /// </summary>
    /// <remarks>
    /// ⚠ Not the dates, the times, the all-day switch, the status or the organiser (lane 2a): a change
    /// of window is a reschedule, the status has its rules and its own actions, and the organiser is
    /// checked first. The service applies those.
    /// </remarks>
    public static void UpdateEntity(this UpdateCompanyEventDto dto, CompanyEvent entity)
    {
        entity.EventName = dto.EventName;
        entity.Description = dto.Description;
        entity.Category = dto.Category;
        entity.Type = dto.Type;
        entity.Priority = dto.Priority;
        entity.LocationType = dto.LocationType;
        entity.VenueName = dto.VenueName;
        entity.VenueAddress = dto.VenueAddress;
        entity.OnlineMeetingLink = dto.OnlineMeetingLink;
        entity.MeetingPassword = dto.MeetingPassword;
        entity.LocationId = dto.LocationId;
        entity.DepartmentId = dto.DepartmentId;
        entity.OrganizationUnitId = dto.OrganizationUnitId;
        entity.Scope = dto.Scope;
        entity.EstimatedAttendees = dto.EstimatedAttendees;
        entity.RequiresRsvp = dto.RequiresRsvp;
        entity.RsvpDeadline = dto.RsvpDeadline;
        entity.Visibility = dto.Visibility;
        entity.ShowOnCompanyCalendar = dto.ShowOnCompanyCalendar;
        entity.ShowOnIntranet = dto.ShowOnIntranet;
        entity.HasBudget = dto.HasBudget;
        entity.BudgetAmount = dto.BudgetAmount;
        entity.ActualCost = dto.ActualCost;
        entity.BudgetCode = dto.BudgetCode;
        entity.RequiredResources = dto.RequiredResources;
        entity.CateringRequirements = dto.CateringRequirements;
        entity.TechnicalRequirements = dto.TechnicalRequirements;
        entity.SendReminders = dto.SendReminders;
        entity.ReminderDaysBefore = dto.ReminderDaysBefore;
        entity.AdditionalNotes = dto.AdditionalNotes;
    }

    public static List<CompanyEventDto> ToDtoList(this IEnumerable<CompanyEvent> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    public static List<CompanyEventSummaryDto> ToSummaryDtoList(this IEnumerable<CompanyEvent> entities)
    {
        return entities.Select(e => e.ToSummaryDto()).ToList();
    }

    #endregion

    #region EventParticipant

    public static EventParticipantDto ToDto(this EventParticipant entity)
    {
        return new EventParticipantDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            EventId = entity.EventId,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName,
            ExternalParticipantName = entity.ExternalParticipantName,
            ExternalParticipantEmail = entity.ExternalParticipantEmail,
            ExternalParticipantOrganization = entity.ExternalParticipantOrganization,
            Role = entity.Role,
            IsRequired = entity.IsRequired,
            InvitationStatus = entity.InvitationStatus,
            InvitationSentDate = entity.InvitationSentDate,
            ResponseDate = entity.ResponseDate,
            ResponseComments = entity.ResponseComments,
            SpecialRequirements = entity.SpecialRequirements,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static EventParticipant ToEntity(this CreateEventParticipantDto dto)
    {
        return new EventParticipant
        {
            EventId = dto.EventId,
            EmployeeId = dto.EmployeeId,
            ExternalParticipantName = dto.ExternalParticipantName,
            ExternalParticipantEmail = dto.ExternalParticipantEmail,
            ExternalParticipantOrganization = dto.ExternalParticipantOrganization,
            Role = dto.Role,
            IsRequired = dto.IsRequired,
            SpecialRequirements = dto.SpecialRequirements
        };
    }

    public static List<EventParticipantDto> ToDtoList(this IEnumerable<EventParticipant> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    #endregion

    #region EventAttendance

    public static EventAttendanceDto ToDto(this EventAttendance entity)
    {
        return new EventAttendanceDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            EventId = entity.EventId,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            Attended = entity.Attended,
            CheckInTime = entity.CheckInTime,
            CheckOutTime = entity.CheckOutTime,
            AbsenceReason = entity.AbsenceReason,
            Notes = entity.Notes,
            MarkedById = entity.MarkedById,
            MarkedByName = entity.MarkedBy?.FullName,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static EventAttendance ToEntity(this MarkEventAttendanceDto dto)
    {
        return new EventAttendance
        {
            EventId = dto.EventId,
            EmployeeId = dto.EmployeeId,
            Attended = dto.Attended,
            CheckInTime = dto.CheckInTime,
            AbsenceReason = dto.AbsenceReason,
            Notes = dto.Notes
        };
    }

    public static List<EventAttendanceDto> ToDtoList(this IEnumerable<EventAttendance> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    #endregion

    #region EventAttachment

    public static EventAttachmentDto ToDto(this EventAttachment entity)
    {
        return new EventAttachmentDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            EventId = entity.EventId,
            FileName = entity.FileName,
            FilePath = entity.FilePath,
            Type = entity.Type,
            Description = entity.Description,
            UploadDate = entity.UploadDate,
            // Lane 2h (C-18, F-54): a file the gate stored, or a reference from before it.
            HasFile = entity.FileUploadRecordId != null,
            FileSizeBytes = entity.FileSizeBytes,
            UploadedById = entity.UploadedById,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static List<EventAttachmentDto> ToDtoList(this IEnumerable<EventAttachment> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    #endregion

    #region EventTask

    public static EventTaskDto ToDto(this EventTask entity)
    {
        return new EventTaskDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            EventId = entity.EventId,
            TaskDescription = entity.TaskDescription,
            Category = entity.Category,
            AssignedToId = entity.AssignedToId,
            AssignedToName = entity.AssignedTo?.FullName,
            DueDate = entity.DueDate,
            Priority = entity.Priority,
            Status = entity.Status,
            IsOverdue = CompanyEventRules.IsOverdue(entity.Status, entity.DueDate, DateTime.UtcNow),
            OverdueChasedAt = entity.OverdueChasedAt,
            CompletionDate = entity.CompletionDate,
            CompletionNotes = entity.CompletionNotes,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static EventTask ToEntity(this CreateEventTaskDto dto)
    {
        return new EventTask
        {
            EventId = dto.EventId,
            TaskDescription = dto.TaskDescription,
            Category = dto.Category,
            AssignedToId = dto.AssignedToId,
            DueDate = dto.DueDate,
            Priority = dto.Priority
        };
    }

    public static void UpdateEntity(this UpdateEventTaskDto dto, EventTask entity)
    {
        entity.TaskDescription = dto.TaskDescription;
        entity.Category = dto.Category;
        entity.AssignedToId = dto.AssignedToId;
        entity.DueDate = dto.DueDate;
        entity.Priority = dto.Priority;
        entity.Status = dto.Status;
    }

    public static List<EventTaskDto> ToDtoList(this IEnumerable<EventTask> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    #endregion

    #region MeetingRoom

    public static MeetingRoomDto ToDto(this MeetingRoom entity)
    {
        return new MeetingRoomDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            RoomCode = entity.RoomCode,
            RoomName = entity.RoomName,
            Description = entity.Description,
            LocationId = entity.LocationId,
            LocationName = entity.SiteLocation?.Name ?? string.Empty,
            Location = entity.Location,
            Floor = entity.Floor,
            Building = entity.Building,
            Capacity = entity.Capacity,
            Type = entity.Type,
            HasProjector = entity.HasProjector,
            HasWhiteboard = entity.HasWhiteboard,
            HasVideoConference = entity.HasVideoConference,
            HasAudioSystem = entity.HasAudioSystem,
            HasAirConditioning = entity.HasAirConditioning,
            OtherFacilities = entity.OtherFacilities,
            IsActive = entity.IsActive,
            RequiresApproval = entity.RequiresApproval,
            IsBookable = entity.IsBookable,
            MaxBookingDurationHours = entity.MaxBookingDurationHours,
            AdvanceBookingDays = entity.AdvanceBookingDays,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static MeetingRoomSummaryDto ToSummaryDto(this MeetingRoom entity)
    {
        return new MeetingRoomSummaryDto
        {
            Id = entity.Id,
            RoomCode = entity.RoomCode,
            RoomName = entity.RoomName,
            LocationName = entity.SiteLocation?.Name ?? string.Empty,
            Location = entity.Location,
            Capacity = entity.Capacity,
            Type = entity.Type,
            IsActive = entity.IsActive,
            IsBookable = entity.IsBookable,
            RequiresApproval = entity.RequiresApproval
        };
    }

    public static MeetingRoom ToEntity(this CreateMeetingRoomDto dto)
    {
        return new MeetingRoom
        {
            RoomCode = dto.RoomCode,
            RoomName = dto.RoomName,
            Description = dto.Description,
            LocationId = dto.LocationId,
            Location = dto.Location,
            Floor = dto.Floor,
            Building = dto.Building,
            Capacity = dto.Capacity,
            Type = dto.Type,
            HasProjector = dto.HasProjector,
            HasWhiteboard = dto.HasWhiteboard,
            HasVideoConference = dto.HasVideoConference,
            HasAudioSystem = dto.HasAudioSystem,
            HasAirConditioning = dto.HasAirConditioning,
            OtherFacilities = dto.OtherFacilities,
            IsActive = dto.IsActive,
            RequiresApproval = dto.RequiresApproval,
            IsBookable = dto.IsBookable,
            MaxBookingDurationHours = dto.MaxBookingDurationHours,
            AdvanceBookingDays = dto.AdvanceBookingDays
        };
    }

    public static void UpdateEntity(this UpdateMeetingRoomDto dto, MeetingRoom entity)
    {
        entity.RoomCode = dto.RoomCode;
        entity.RoomName = dto.RoomName;
        entity.Description = dto.Description;
        entity.LocationId = dto.LocationId;
        entity.Location = dto.Location;
        entity.Floor = dto.Floor;
        entity.Building = dto.Building;
        entity.Capacity = dto.Capacity;
        entity.Type = dto.Type;
        entity.HasProjector = dto.HasProjector;
        entity.HasWhiteboard = dto.HasWhiteboard;
        entity.HasVideoConference = dto.HasVideoConference;
        entity.HasAudioSystem = dto.HasAudioSystem;
        entity.HasAirConditioning = dto.HasAirConditioning;
        entity.OtherFacilities = dto.OtherFacilities;
        entity.IsActive = dto.IsActive;
        entity.RequiresApproval = dto.RequiresApproval;
        entity.IsBookable = dto.IsBookable;
        entity.MaxBookingDurationHours = dto.MaxBookingDurationHours;
        entity.AdvanceBookingDays = dto.AdvanceBookingDays;
    }

    public static List<MeetingRoomDto> ToDtoList(this IEnumerable<MeetingRoom> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    public static List<MeetingRoomSummaryDto> ToSummaryDtoList(this IEnumerable<MeetingRoom> entities)
    {
        return entities.Select(e => e.ToSummaryDto()).ToList();
    }

    #endregion

    #region RoomBooking

    public static RoomBookingDto ToDto(this RoomBooking entity)
    {
        return new RoomBookingDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            BookingNumber = entity.BookingNumber,
            RoomId = entity.RoomId,
            RoomName = entity.Room?.RoomName ?? string.Empty,
            EventId = entity.EventId,
            EventName = entity.Event?.EventName,
            BookedById = entity.BookedById,
            BookedByName = entity.BookedBy?.FullName ?? string.Empty,
            // ⚠ Lane 3a (F-50): stored as UTC and read back unmarked, so a browser outside GMT shifted them.
            BookingDate = RoomBookingRules.AsUtc(entity.BookingDate),
            StartDateTime = RoomBookingRules.AsUtc(entity.StartDateTime),
            EndDateTime = RoomBookingRules.AsUtc(entity.EndDateTime),
            Purpose = entity.Purpose,
            ExpectedAttendees = entity.ExpectedAttendees,
            SpecialRequirements = entity.SpecialRequirements,
            CateringRequirements = entity.CateringRequirements,
            Status = entity.Status,
            ApprovedById = entity.ApprovedById,
            ApprovedByName = entity.ApprovedBy?.FullName,
            ApprovalDate = RoomBookingRules.AsUtc(entity.ApprovalDate),
            IsCancelled = entity.IsCancelled,
            CancellationDate = RoomBookingRules.AsUtc(entity.CancellationDate),
            CancellationReason = entity.CancellationReason,
            Notes = entity.Notes,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static RoomBookingSummaryDto ToSummaryDto(this RoomBooking entity)
    {
        return new RoomBookingSummaryDto
        {
            Id = entity.Id,
            BookingNumber = entity.BookingNumber,
            RoomId = entity.RoomId,
            RoomName = entity.Room?.RoomName ?? string.Empty,
            BookedById = entity.BookedById,
            BookedByName = entity.BookedBy?.FullName ?? string.Empty,
            StartDateTime = RoomBookingRules.AsUtc(entity.StartDateTime),
            EndDateTime = RoomBookingRules.AsUtc(entity.EndDateTime),
            Purpose = entity.Purpose,
            Status = entity.Status
        };
    }

    public static RoomBooking ToEntity(this CreateRoomBookingDto dto)
    {
        return new RoomBooking
        {
            RoomId = dto.RoomId,
            EventId = dto.EventId,
            StartDateTime = dto.StartDateTime,
            EndDateTime = dto.EndDateTime,
            Purpose = dto.Purpose,
            ExpectedAttendees = dto.ExpectedAttendees,
            SpecialRequirements = dto.SpecialRequirements,
            CateringRequirements = dto.CateringRequirements,
            Notes = dto.Notes
        };
    }

    public static void UpdateEntity(this UpdateRoomBookingDto dto, RoomBooking entity)
    {
        entity.StartDateTime = dto.StartDateTime;
        entity.EndDateTime = dto.EndDateTime;
        entity.Purpose = dto.Purpose;
        entity.ExpectedAttendees = dto.ExpectedAttendees;
        entity.SpecialRequirements = dto.SpecialRequirements;
        entity.CateringRequirements = dto.CateringRequirements;
        entity.Notes = dto.Notes;
    }

    public static List<RoomBookingDto> ToDtoList(this IEnumerable<RoomBooking> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    public static List<RoomBookingSummaryDto> ToSummaryDtoList(this IEnumerable<RoomBooking> entities)
    {
        return entities.Select(e => e.ToSummaryDto()).ToList();
    }

    #endregion

    #region CompanyMilestone

    public static CompanyMilestoneDto ToDto(this CompanyMilestone entity)
    {
        return new CompanyMilestoneDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            Title = entity.Title,
            Description = entity.Description,
            Category = entity.Category,
            MilestoneDate = entity.MilestoneDate,
            IsRecurringAnnually = entity.IsRecurringAnnually,
            ShowOnCalendar = entity.ShowOnCalendar,
            Significance = entity.Significance,
            RelatedDocuments = entity.RelatedDocuments,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static CompanyMilestone ToEntity(this CreateCompanyMilestoneDto dto)
    {
        return new CompanyMilestone
        {
            Title = dto.Title,
            Description = dto.Description,
            Category = dto.Category,
            MilestoneDate = dto.MilestoneDate,
            IsRecurringAnnually = dto.IsRecurringAnnually,
            ShowOnCalendar = dto.ShowOnCalendar,
            Significance = dto.Significance,
            RelatedDocuments = dto.RelatedDocuments
        };
    }

    public static void UpdateEntity(this UpdateCompanyMilestoneDto dto, CompanyMilestone entity)
    {
        entity.Title = dto.Title;
        entity.Description = dto.Description;
        entity.Category = dto.Category;
        entity.MilestoneDate = dto.MilestoneDate;
        entity.IsRecurringAnnually = dto.IsRecurringAnnually;
        entity.ShowOnCalendar = dto.ShowOnCalendar;
        entity.Significance = dto.Significance;
        entity.RelatedDocuments = dto.RelatedDocuments;
    }

    public static List<CompanyMilestoneDto> ToDtoList(this IEnumerable<CompanyMilestone> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    #endregion

    #region BusinessClosure

    public static BusinessClosureDto ToDto(this BusinessClosure entity)
    {
        return new BusinessClosureDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            Title = entity.Title,
            Reason = entity.Reason,
            StartDate = entity.StartDate,
            EndDate = entity.EndDate,
            Type = entity.Type,
            AffectsAllStations = entity.AffectsAllStations,
            LocationId = entity.LocationId,
            LocationName = entity.SiteLocation?.Name,
            DepartmentId = entity.DepartmentId,
            DepartmentName = entity.Department?.Name,
            OrganizationUnitId = entity.OrganizationUnitId,
            OrganizationUnitName = entity.OrganizationUnit?.Name,
            ScopeDescription = DescribeScope(entity),
            RecursAnnually = entity.RecursAnnually,
            IsPaidClosure = entity.IsPaidClosure,
            CountsAsWorkingDay = entity.CountsAsWorkingDay,
            AnnouncementDate = entity.AnnouncementDate,
            AnnouncedById = entity.AnnouncedById,
            AnnouncedByName = entity.AnnouncedBy?.FullName,
            CommunicationNotes = entity.CommunicationNotes,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static BusinessClosure ToEntity(this CreateBusinessClosureDto dto)
    {
        return new BusinessClosure
        {
            Title = dto.Title,
            Reason = dto.Reason,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            Type = dto.Type,
            AffectsAllStations = dto.AffectsAllStations,
            LocationId = dto.LocationId,
            DepartmentId = dto.DepartmentId,
            OrganizationUnitId = dto.OrganizationUnitId,
            RecursAnnually = dto.RecursAnnually,
            IsPaidClosure = dto.IsPaidClosure,
            CountsAsWorkingDay = dto.CountsAsWorkingDay,
            CommunicationNotes = dto.CommunicationNotes
        };
    }

    /// <summary>
    /// Who a closure covers, from its type (company-schedule final closure, D-1). The scope is the
    /// one <see cref="BusinessClosureRules.ScopeOf"/> answers, so the register says what the
    /// leave and diary code actually do.
    /// </summary>
    private static string DescribeScope(BusinessClosure entity)
    {
        var scope = BusinessClosureRules.ScopeOf(entity);
        return scope.Kind switch
        {
            ClosureScopeKind.Site => $"Site: {entity.SiteLocation?.Name ?? "(site not found)"}",
            ClosureScopeKind.Unit => $"Unit: {entity.OrganizationUnit?.Name ?? "(unit not found)"} and everything beneath it",
            _ => "Whole company",
        };
    }

    public static void UpdateEntity(this UpdateBusinessClosureDto dto, BusinessClosure entity)
    {
        entity.Title = dto.Title;
        entity.Reason = dto.Reason;
        entity.StartDate = dto.StartDate;
        entity.EndDate = dto.EndDate;
        entity.Type = dto.Type;
        entity.AffectsAllStations = dto.AffectsAllStations;
        entity.LocationId = dto.LocationId;
        entity.DepartmentId = dto.DepartmentId;
        entity.OrganizationUnitId = dto.OrganizationUnitId;
        entity.RecursAnnually = dto.RecursAnnually;
        entity.IsPaidClosure = dto.IsPaidClosure;
        entity.CountsAsWorkingDay = dto.CountsAsWorkingDay;
        entity.CommunicationNotes = dto.CommunicationNotes;
    }

    public static List<BusinessClosureDto> ToDtoList(this IEnumerable<BusinessClosure> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    #endregion

    #region FiscalYear

    public static FiscalYearDto ToDto(this FiscalYear entity)
    {
        return new FiscalYearDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            Year = entity.Year,
            FiscalYearName = entity.FiscalYearName,
            StartDate = entity.StartDate,
            EndDate = entity.EndDate,
            IsCurrent = entity.IsCurrent,
            Status = entity.Status,
            PeriodCount = entity.Periods?.Count ?? 0,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static FiscalYearDetailDto ToDetailDto(this FiscalYear entity)
    {
        return new FiscalYearDetailDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            Year = entity.Year,
            FiscalYearName = entity.FiscalYearName,
            StartDate = entity.StartDate,
            EndDate = entity.EndDate,
            IsCurrent = entity.IsCurrent,
            Status = entity.Status,
            PeriodCount = entity.Periods?.Count ?? 0,
            Periods = entity.Periods?.Select(p => p.ToDto()).ToList() ?? new List<FiscalPeriodDto>(),
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static FiscalYear ToEntity(this CreateFiscalYearDto dto)
    {
        return new FiscalYear
        {
            Year = dto.Year,
            FiscalYearName = dto.FiscalYearName,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            IsCurrent = dto.IsCurrent
        };
    }

    public static void UpdateEntity(this UpdateFiscalYearDto dto, FiscalYear entity)
    {
        entity.FiscalYearName = dto.FiscalYearName;
        entity.StartDate = dto.StartDate;
        entity.EndDate = dto.EndDate;
        entity.IsCurrent = dto.IsCurrent;
        entity.Status = dto.Status;
    }

    public static List<FiscalYearDto> ToDtoList(this IEnumerable<FiscalYear> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    #endregion

    #region FiscalPeriod

    public static FiscalPeriodDto ToDto(this FiscalPeriod entity)
    {
        return new FiscalPeriodDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            FiscalYearId = entity.FiscalYearId,
            FiscalYearName = entity.FiscalYear?.FiscalYearName ?? string.Empty,
            PeriodNumber = entity.PeriodNumber,
            PeriodName = entity.PeriodName,
            Type = entity.Type,
            StartDate = entity.StartDate,
            EndDate = entity.EndDate,
            IsClosed = entity.IsClosed,
            ClosedDate = entity.ClosedDate,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static FiscalPeriod ToEntity(this CreateFiscalPeriodDto dto)
    {
        return new FiscalPeriod
        {
            FiscalYearId = dto.FiscalYearId,
            PeriodNumber = dto.PeriodNumber,
            PeriodName = dto.PeriodName,
            Type = dto.Type,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate
        };
    }

    public static void UpdateEntity(this UpdateFiscalPeriodDto dto, FiscalPeriod entity)
    {
        entity.PeriodNumber = dto.PeriodNumber;
        entity.PeriodName = dto.PeriodName;
        entity.Type = dto.Type;
        entity.StartDate = dto.StartDate;
        entity.EndDate = dto.EndDate;
    }

    public static List<FiscalPeriodDto> ToDtoList(this IEnumerable<FiscalPeriod> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    #endregion
}
