using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Orientation;
using ErpSystem.Core.Enums;

namespace ErpSystem.Application.HR.Extensions;

/// <summary>
/// Entity ⇄ DTO mapping extensions for the Orientation module.
/// Note: Orientation entities reference people by Guid only (no Employee navigation),
/// so employee display-name fields (EmployeeName, OwnerEmployeeName, EnrolledByName,
/// MarkedByName, IssuedByName, SubmittedByName, RecipientEmployeeName, TargetEntityName)
/// are left for the service layer to hydrate.
/// </summary>
public static class OrientationMappingExtensions
{
    // ========================================================================
    // SECTION 1 — CATALOG
    // ========================================================================

    #region OrientationCategory

    public static OrientationCategoryLookupDto ToLookupDto(this OrientationCategory entity)
    {
        return new OrientationCategoryLookupDto
        {
            Id = entity.Id,
            Name = entity.Name,
            ParentCategoryId = entity.ParentCategoryId,
        };
    }

    public static OrientationCategoryDto ToDto(this OrientationCategory entity)
    {
        return new OrientationCategoryDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            Name = entity.Name,
            Description = entity.Description,
            ParentCategoryId = entity.ParentCategoryId,
            ParentCategoryName = entity.ParentCategory?.Name,
            DisplayOrder = entity.DisplayOrder,
            IsActive = entity.IsActive,
            ProgramCount = entity.Programs.Count,
            SubCategories = entity.SubCategories.Select(c => c.ToLookupDto()).ToList(),
        };
    }

    public static OrientationCategory ToEntity(this CreateOrientationCategoryDto dto, Guid tenantId, Guid userId)
    {
        return new OrientationCategory
        {
            TenantId = tenantId,
            Name = dto.Name,
            Description = dto.Description,
            ParentCategoryId = dto.ParentCategoryId,
            DisplayOrder = dto.DisplayOrder,
            IsActive = dto.IsActive,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this OrientationCategory entity, UpdateOrientationCategoryDto dto, Guid userId)
    {
        entity.Name = dto.Name;
        entity.Description = dto.Description;
        entity.ParentCategoryId = dto.ParentCategoryId;
        entity.DisplayOrder = dto.DisplayOrder;
        entity.IsActive = dto.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    #endregion

    #region OrientationProgram

    public static OrientationProgramDto ToDto(this OrientationProgram entity)
    {
        return new OrientationProgramDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            ProgramCode = entity.ProgramCode,
            Title = entity.Title,
            Description = entity.Description,
            Objectives = entity.Objectives,
            CategoryId = entity.CategoryId,
            CategoryName = entity.Category?.Name,
            ProgramType = entity.ProgramType,
            DefaultDeliveryMode = entity.DefaultDeliveryMode,
            Status = entity.Status,
            Priority = entity.Priority,
            AudienceScope = entity.AudienceScope,
            EstimatedDurationMinutes = entity.EstimatedDurationMinutes,
            RequiresAssessment = entity.RequiresAssessment,
            PassingScorePercent = entity.PassingScorePercent,
            RequiresAcknowledgement = entity.RequiresAcknowledgement,
            CompletionDeadlineDays = entity.CompletionDeadlineDays,
            IsCertificateIssued = entity.IsCertificateIssued,
            CertificateValidityMonths = entity.CertificateValidityMonths,
            IsRecurring = entity.IsRecurring,
            RecurrenceFrequency = entity.RecurrenceFrequency,
            EnableReminders = entity.EnableReminders,
            Version = entity.Version,
            EffectiveFrom = entity.EffectiveFrom,
            EffectiveTo = entity.EffectiveTo,
            Tags = entity.Tags,
            OwnerEmployeeId = entity.OwnerEmployeeId,
            OwnerOrganizationUnitId = entity.OwnerOrganizationUnitId,
            OwnerOrganizationUnitName = entity.OwnerOrganizationUnit?.Name,
            ModuleCount = entity.Modules.Count,
            SessionCount = entity.Sessions.Count,
            EnrollmentCount = entity.Enrollments.Count,
            CompletedCount = entity.Enrollments.Count(e => e.CompletionStatus == OrientationCompletionStatus.Completed),
            Modules = entity.Modules.OrderBy(m => m.SequenceOrder).Select(m => m.ToDto()).ToList(),
            AudienceRules = entity.AudienceRules.Select(r => r.ToDto()).ToList(),
            Prerequisites = entity.Prerequisites.Select(p => p.ToDto()).ToList(),
            AssessmentQuestions = entity.AssessmentQuestions.OrderBy(q => q.SequenceOrder).Select(q => q.ToDto()).ToList(),
        };
    }

    public static OrientationProgramSummaryDto ToSummaryDto(this OrientationProgram entity)
    {
        var closedBecause = OrientationProgramEnrolment.WhyNotTaking(
            entity.Status, entity.EffectiveFrom, entity.EffectiveTo, DateOnly.FromDateTime(DateTime.UtcNow));
        return new OrientationProgramSummaryDto
        {
            Id = entity.Id,
            ProgramCode = entity.ProgramCode,
            Title = entity.Title,
            CategoryName = entity.Category?.Name,
            ProgramType = entity.ProgramType,
            Status = entity.Status,
            Priority = entity.Priority,
            DefaultDeliveryMode = entity.DefaultDeliveryMode,
            EstimatedDurationMinutes = entity.EstimatedDurationMinutes,
            IsCertificateIssued = entity.IsCertificateIssued,
            RequiresAssessment = entity.RequiresAssessment,
            IsRecurring = entity.IsRecurring,
            RecurrenceFrequency = entity.RecurrenceFrequency,
            AcceptsEnrolment = closedBecause is null,
            ClosedBecause = closedBecause,
            ModuleCount = entity.Modules.Count,
            EnrollmentCount = entity.Enrollments.Count,
            CompletedCount = entity.Enrollments.Count(e => e.CompletionStatus == OrientationCompletionStatus.Completed),
        };
    }

    public static OrientationProgram ToEntity(this CreateOrientationProgramDto dto, Guid tenantId, Guid userId)
    {
        return new OrientationProgram
        {
            TenantId = tenantId,
            ProgramCode = dto.ProgramCode ?? string.Empty,
            Title = dto.Title,
            Description = dto.Description,
            Objectives = dto.Objectives,
            CategoryId = dto.CategoryId,
            ProgramType = dto.ProgramType,
            DefaultDeliveryMode = dto.DefaultDeliveryMode,
            Status = OrientationProgramStatus.Draft,
            Priority = dto.Priority,
            AudienceScope = dto.AudienceScope,
            EstimatedDurationMinutes = dto.EstimatedDurationMinutes,
            RequiresAssessment = dto.RequiresAssessment,
            PassingScorePercent = dto.PassingScorePercent,
            RequiresAcknowledgement = dto.RequiresAcknowledgement,
            CompletionDeadlineDays = dto.CompletionDeadlineDays,
            IsCertificateIssued = dto.IsCertificateIssued,
            CertificateValidityMonths = dto.CertificateValidityMonths,
            IsRecurring = dto.IsRecurring,
            RecurrenceFrequency = dto.RecurrenceFrequency,
            EnableReminders = dto.EnableReminders,
            Version = dto.Version,
            EffectiveFrom = dto.EffectiveFrom,
            EffectiveTo = dto.EffectiveTo,
            Tags = dto.Tags,
            OwnerEmployeeId = dto.OwnerEmployeeId,
            OwnerOrganizationUnitId = dto.OwnerOrganizationUnitId,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this OrientationProgram entity, UpdateOrientationProgramDto dto, Guid userId)
    {
        entity.Title = dto.Title;
        entity.Description = dto.Description;
        entity.Objectives = dto.Objectives;
        entity.CategoryId = dto.CategoryId;
        entity.ProgramType = dto.ProgramType;
        entity.DefaultDeliveryMode = dto.DefaultDeliveryMode;
        entity.Priority = dto.Priority;
        entity.AudienceScope = dto.AudienceScope;
        entity.EstimatedDurationMinutes = dto.EstimatedDurationMinutes;
        entity.RequiresAssessment = dto.RequiresAssessment;
        entity.PassingScorePercent = dto.PassingScorePercent;
        entity.RequiresAcknowledgement = dto.RequiresAcknowledgement;
        entity.CompletionDeadlineDays = dto.CompletionDeadlineDays;
        entity.IsCertificateIssued = dto.IsCertificateIssued;
        entity.CertificateValidityMonths = dto.CertificateValidityMonths;
        entity.IsRecurring = dto.IsRecurring;
        entity.RecurrenceFrequency = dto.RecurrenceFrequency;
        entity.EnableReminders = dto.EnableReminders;
        entity.Version = dto.Version;
        entity.EffectiveFrom = dto.EffectiveFrom;
        entity.EffectiveTo = dto.EffectiveTo;
        entity.Tags = dto.Tags;
        entity.OwnerEmployeeId = dto.OwnerEmployeeId;
        entity.OwnerOrganizationUnitId = dto.OwnerOrganizationUnitId;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<OrientationProgramSummaryDto> ToSummaryDtoList(this IEnumerable<OrientationProgram> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    #region OrientationModule

    public static OrientationModuleDto ToDto(this OrientationModule entity)
    {
        return new OrientationModuleDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            ProgramId = entity.ProgramId,
            ProgramTitle = entity.Program?.Title,
            Title = entity.Title,
            Description = entity.Description,
            SequenceOrder = entity.SequenceOrder,
            ModuleType = entity.ModuleType,
            EstimatedDurationMinutes = entity.EstimatedDurationMinutes,
            IsSequentiallyRequired = entity.IsSequentiallyRequired,
            IsOptional = entity.IsOptional,
            IsActive = entity.IsActive,
            ContentItemCount = entity.ContentItems.Count,
            ContentItems = entity.ContentItems.OrderBy(c => c.SequenceOrder).Select(c => c.ToDto()).ToList(),
        };
    }

    public static OrientationModule ToEntity(this CreateOrientationModuleDto dto, Guid tenantId, Guid userId)
    {
        return new OrientationModule
        {
            TenantId = tenantId,
            ProgramId = dto.ProgramId,
            Title = dto.Title,
            Description = dto.Description,
            SequenceOrder = dto.SequenceOrder,
            ModuleType = dto.ModuleType,
            EstimatedDurationMinutes = dto.EstimatedDurationMinutes,
            IsSequentiallyRequired = dto.IsSequentiallyRequired,
            IsOptional = dto.IsOptional,
            IsActive = dto.IsActive,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this OrientationModule entity, UpdateOrientationModuleDto dto, Guid userId)
    {
        entity.Title = dto.Title;
        entity.Description = dto.Description;
        entity.SequenceOrder = dto.SequenceOrder;
        entity.ModuleType = dto.ModuleType;
        entity.EstimatedDurationMinutes = dto.EstimatedDurationMinutes;
        entity.IsSequentiallyRequired = dto.IsSequentiallyRequired;
        entity.IsOptional = dto.IsOptional;
        entity.IsActive = dto.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    #endregion

    #region OrientationContentItem

    public static OrientationContentItemDto ToDto(this OrientationContentItem entity)
    {
        return new OrientationContentItemDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            ModuleId = entity.ModuleId,
            ModuleTitle = entity.Module?.Title,
            Title = entity.Title,
            Description = entity.Description,
            ContentType = entity.ContentType,
            ResourceUrl = entity.ResourceUrl,
            OriginalFileName = entity.OriginalFileName,
            FileSizeBytes = entity.FileSizeBytes,
            MediaDurationSeconds = entity.MediaDurationSeconds,
            SequenceOrder = entity.SequenceOrder,
            IsRequired = entity.IsRequired,
            IsActive = entity.IsActive,
        };
    }

    public static OrientationContentItem ToEntity(this CreateOrientationContentItemDto dto, Guid tenantId, Guid userId)
    {
        return new OrientationContentItem
        {
            TenantId = tenantId,
            ModuleId = dto.ModuleId,
            Title = dto.Title,
            Description = dto.Description,
            ContentType = dto.ContentType,
            ResourceUrl = dto.ResourceUrl,
            OriginalFileName = dto.OriginalFileName,
            FileSizeBytes = dto.FileSizeBytes,
            MediaDurationSeconds = dto.MediaDurationSeconds,
            SequenceOrder = dto.SequenceOrder,
            IsRequired = dto.IsRequired,
            IsActive = dto.IsActive,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this OrientationContentItem entity, UpdateOrientationContentItemDto dto, Guid userId)
    {
        entity.Title = dto.Title;
        entity.Description = dto.Description;
        entity.ContentType = dto.ContentType;
        entity.ResourceUrl = dto.ResourceUrl;
        entity.OriginalFileName = dto.OriginalFileName;
        entity.FileSizeBytes = dto.FileSizeBytes;
        entity.MediaDurationSeconds = dto.MediaDurationSeconds;
        entity.SequenceOrder = dto.SequenceOrder;
        entity.IsRequired = dto.IsRequired;
        entity.IsActive = dto.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    #endregion

    #region OrientationPrerequisite

    public static OrientationPrerequisiteDto ToDto(this OrientationPrerequisite entity)
    {
        return new OrientationPrerequisiteDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            ProgramId = entity.ProgramId,
            ProgramTitle = entity.Program?.Title,
            PrerequisiteProgramId = entity.PrerequisiteProgramId,
            PrerequisiteProgramCode = entity.PrerequisiteProgram?.ProgramCode,
            PrerequisiteProgramTitle = entity.PrerequisiteProgram?.Title,
            IsMandatory = entity.IsMandatory,
            Notes = entity.Notes,
        };
    }

    public static OrientationPrerequisite ToEntity(this CreateOrientationPrerequisiteDto dto, Guid tenantId, Guid userId)
    {
        return new OrientationPrerequisite
        {
            TenantId = tenantId,
            ProgramId = dto.ProgramId,
            PrerequisiteProgramId = dto.PrerequisiteProgramId,
            IsMandatory = dto.IsMandatory,
            Notes = dto.Notes,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this OrientationPrerequisite entity, UpdateOrientationPrerequisiteDto dto, Guid userId)
    {
        entity.IsMandatory = dto.IsMandatory;
        entity.Notes = dto.Notes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    #endregion

    #region OrientationAudienceRule

    public static OrientationAudienceRuleDto ToDto(this OrientationAudienceRule entity)
    {
        return new OrientationAudienceRuleDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            ProgramId = entity.ProgramId,
            ProgramTitle = entity.Program?.Title,
            RuleName = entity.RuleName,
            Description = entity.Description,
            TargetType = entity.TargetType,
            TargetEntityId = entity.TargetEntityId,
            Population = entity.Population,
            Trigger = entity.Trigger,
            EnrollmentDelayDays = entity.EnrollmentDelayDays,
            IsInclusive = entity.IsInclusive,
            IsActive = entity.IsActive,
        };
    }

    public static OrientationAudienceRule ToEntity(this CreateOrientationAudienceRuleDto dto, Guid tenantId, Guid userId)
    {
        return new OrientationAudienceRule
        {
            TenantId = tenantId,
            ProgramId = dto.ProgramId,
            RuleName = dto.RuleName,
            Description = dto.Description,
            TargetType = dto.TargetType,
            TargetEntityId = dto.TargetEntityId,
            Population = dto.Population,
            Trigger = dto.Trigger,
            EnrollmentDelayDays = dto.EnrollmentDelayDays,
            IsInclusive = dto.IsInclusive,
            IsActive = dto.IsActive,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this OrientationAudienceRule entity, UpdateOrientationAudienceRuleDto dto, Guid userId)
    {
        entity.RuleName = dto.RuleName;
        entity.Description = dto.Description;
        entity.TargetType = dto.TargetType;
        entity.TargetEntityId = dto.TargetEntityId;
        entity.Population = dto.Population;
        entity.Trigger = dto.Trigger;
        entity.EnrollmentDelayDays = dto.EnrollmentDelayDays;
        entity.IsInclusive = dto.IsInclusive;
        entity.IsActive = dto.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    #endregion

    // ========================================================================
    // SECTION 2 — DELIVERY
    // ========================================================================

    #region OrientationSession

    public static OrientationSessionDto ToDto(this OrientationSession entity)
    {
        // Counted through the same "occupies a seat" rule the capacity check uses. Counting the raw
        // collection made a withdrawn participant consume a seat on the detail screen while the
        // enrolment guard let someone take it — two subsystems disagreeing about the same fact.
        var enrolled = entity.Enrollments.Count(e => OrientationEnrollmentStatuses.Occupying.Contains(e.EnrollmentStatus));
        return new OrientationSessionDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            ProgramId = entity.ProgramId,
            ProgramCode = entity.Program?.ProgramCode,
            ProgramTitle = entity.Program?.Title,
            SessionCode = entity.SessionCode,
            Title = entity.Title,
            Description = entity.Description,
            DeliveryMode = entity.DeliveryMode,
            Status = entity.Status,
            ScheduledStartAt = entity.ScheduledStartAt,
            ScheduledEndAt = entity.ScheduledEndAt,
            ActualStartAt = entity.ActualStartAt,
            ActualEndAt = entity.ActualEndAt,
            VenueDescription = entity.VenueDescription,
            VirtualMeetingUrl = entity.VirtualMeetingUrl,
            MaxParticipants = entity.MaxParticipants,
            EnrollmentDeadlineAt = entity.EnrollmentDeadlineAt,
            AllowWaitlist = entity.AllowWaitlist,
            RequiresApproval = entity.RequiresApproval,
            RecordingUrl = entity.RecordingUrl,
            ParticipantInstructions = entity.ParticipantInstructions,
            EnrolledCount = enrolled,
            AvailableSeats = entity.MaxParticipants.HasValue ? Math.Max(0, entity.MaxParticipants.Value - enrolled) : 0,
            Facilitators = entity.Facilitators.Select(f => f.ToDto()).ToList(),
        };
    }

    public static OrientationSessionSummaryDto ToSummaryDto(this OrientationSession entity)
    {
        var closedBecause = OrientationSessionEnrolment.WhyNotOpen(entity.Status, entity.EnrollmentDeadlineAt, DateTime.UtcNow);
        return new OrientationSessionSummaryDto
        {
            Id = entity.Id,
            SessionCode = entity.SessionCode,
            Title = entity.Title,
            ProgramId = entity.ProgramId,
            ProgramTitle = entity.Program?.Title,
            DeliveryMode = entity.DeliveryMode,
            Status = entity.Status,
            ScheduledStartAt = entity.ScheduledStartAt,
            MaxParticipants = entity.MaxParticipants,
            EnrollmentDeadlineAt = entity.EnrollmentDeadlineAt,
            AcceptsEnrolment = closedBecause is null,
            ClosedBecause = closedBecause,
            // Filled from a batched count in the service — list reads do not include Enrollments.
            EnrolledCount = entity.Enrollments.Count(e => OrientationEnrollmentStatuses.Occupying.Contains(e.EnrollmentStatus)),
        };
    }

    public static OrientationSession ToEntity(this CreateOrientationSessionDto dto, Guid tenantId, Guid userId)
    {
        return new OrientationSession
        {
            TenantId = tenantId,
            ProgramId = dto.ProgramId,
            SessionCode = dto.SessionCode ?? string.Empty,
            Title = dto.Title,
            Description = dto.Description,
            DeliveryMode = dto.DeliveryMode,
            Status = OrientationSessionStatus.Draft,
            ScheduledStartAt = dto.ScheduledStartAt,
            ScheduledEndAt = dto.ScheduledEndAt,
            VenueDescription = dto.VenueDescription,
            VirtualMeetingUrl = dto.VirtualMeetingUrl,
            MaxParticipants = dto.MaxParticipants,
            EnrollmentDeadlineAt = dto.EnrollmentDeadlineAt,
            AllowWaitlist = dto.AllowWaitlist,
            RequiresApproval = dto.RequiresApproval,
            RecordingUrl = dto.RecordingUrl,
            ParticipantInstructions = dto.ParticipantInstructions,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this OrientationSession entity, UpdateOrientationSessionDto dto, Guid userId)
    {
        entity.Title = dto.Title;
        entity.Description = dto.Description;
        entity.DeliveryMode = dto.DeliveryMode;
        entity.ScheduledStartAt = dto.ScheduledStartAt;
        entity.ScheduledEndAt = dto.ScheduledEndAt;
        entity.ActualStartAt = dto.ActualStartAt;
        entity.ActualEndAt = dto.ActualEndAt;
        entity.VenueDescription = dto.VenueDescription;
        entity.VirtualMeetingUrl = dto.VirtualMeetingUrl;
        entity.MaxParticipants = dto.MaxParticipants;
        entity.EnrollmentDeadlineAt = dto.EnrollmentDeadlineAt;
        entity.AllowWaitlist = dto.AllowWaitlist;
        entity.RequiresApproval = dto.RequiresApproval;
        entity.RecordingUrl = dto.RecordingUrl;
        entity.ParticipantInstructions = dto.ParticipantInstructions;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<OrientationSessionSummaryDto> ToSummaryDtoList(this IEnumerable<OrientationSession> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    #region OrientationSessionFacilitator

    public static OrientationSessionFacilitatorDto ToDto(this OrientationSessionFacilitator entity)
    {
        return new OrientationSessionFacilitatorDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            SessionId = entity.SessionId,
            EmployeeId = entity.EmployeeId,
            ExternalFacilitatorName = entity.ExternalFacilitatorName,
            ExternalFacilitatorEmail = entity.ExternalFacilitatorEmail,
            ExternalFacilitatorOrganization = entity.ExternalFacilitatorOrganization,
            ExternalFacilitatorVendorId = entity.ExternalFacilitatorVendorId,
            ExternalFacilitatorTrainerProfileId = entity.ExternalFacilitatorTrainerProfileId,
            Role = entity.Role,
            HasConfirmed = entity.HasConfirmed,
            Notes = entity.Notes,
        };
    }

    public static OrientationSessionFacilitator ToEntity(this CreateOrientationSessionFacilitatorDto dto, Guid tenantId, Guid userId)
    {
        return new OrientationSessionFacilitator
        {
            TenantId = tenantId,
            SessionId = dto.SessionId,
            EmployeeId = dto.EmployeeId,
            ExternalFacilitatorName = dto.ExternalFacilitatorName,
            ExternalFacilitatorEmail = dto.ExternalFacilitatorEmail,
            ExternalFacilitatorOrganization = dto.ExternalFacilitatorOrganization,
            Role = dto.Role,
            HasConfirmed = dto.HasConfirmed,
            Notes = dto.Notes,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this OrientationSessionFacilitator entity, UpdateOrientationSessionFacilitatorDto dto, Guid userId)
    {
        entity.EmployeeId = dto.EmployeeId;
        entity.ExternalFacilitatorName = dto.ExternalFacilitatorName;
        entity.ExternalFacilitatorEmail = dto.ExternalFacilitatorEmail;
        entity.ExternalFacilitatorOrganization = dto.ExternalFacilitatorOrganization;
        entity.Role = dto.Role;
        entity.HasConfirmed = dto.HasConfirmed;
        entity.Notes = dto.Notes;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    #endregion

    #region OrientationAttendanceRecord

    public static OrientationAttendanceRecordDto ToDto(this OrientationAttendanceRecord entity)
    {
        return new OrientationAttendanceRecordDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            EnrollmentId = entity.EnrollmentId,
            EmployeeId = entity.Enrollment?.EmployeeId,
            SessionDay = entity.SessionDay,
            AttendanceStatus = entity.AttendanceStatus,
            CheckInAt = entity.CheckInAt,
            CheckOutAt = entity.CheckOutAt,
            AttendedMinutes = entity.AttendedMinutes,
            MarkedLate = entity.MarkedLate,
            AbsenceReason = entity.AbsenceReason,
            MarkedByEmployeeId = entity.MarkedByEmployeeId,
        };
    }

    public static OrientationAttendanceRecord ToEntity(this CreateOrientationAttendanceRecordDto dto, Guid tenantId, Guid userId)
    {
        return new OrientationAttendanceRecord
        {
            TenantId = tenantId,
            EnrollmentId = dto.EnrollmentId,
            SessionDay = dto.SessionDay,
            AttendanceStatus = dto.AttendanceStatus,
            CheckInAt = dto.CheckInAt,
            CheckOutAt = dto.CheckOutAt,
            AttendedMinutes = dto.AttendedMinutes,
            MarkedLate = dto.MarkedLate,
            AbsenceReason = dto.AbsenceReason,
            MarkedByEmployeeId = dto.MarkedByEmployeeId,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this OrientationAttendanceRecord entity, UpdateOrientationAttendanceRecordDto dto, Guid userId)
    {
        entity.SessionDay = dto.SessionDay;
        entity.AttendanceStatus = dto.AttendanceStatus;
        entity.CheckInAt = dto.CheckInAt;
        entity.CheckOutAt = dto.CheckOutAt;
        entity.AttendedMinutes = dto.AttendedMinutes;
        entity.MarkedLate = dto.MarkedLate;
        entity.AbsenceReason = dto.AbsenceReason;
        entity.MarkedByEmployeeId = dto.MarkedByEmployeeId;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    #endregion

    // ========================================================================
    // SECTION 3 — ENROLLMENT & PROGRESS
    // ========================================================================

    #region EmployeeOrientation

    public static EmployeeOrientationDto ToDto(this EmployeeOrientation entity)
    {
        return new EmployeeOrientationDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            ProgramId = entity.ProgramId,
            ProgramCode = entity.Program?.ProgramCode,
            ProgramTitle = entity.Program?.Title,
            SessionId = entity.SessionId,
            SessionTitle = entity.Session?.Title,
            EmployeeId = entity.EmployeeId,
            EnrollmentStatus = entity.EnrollmentStatus,
            EnrollmentSource = entity.EnrollmentSource,
            AudienceRuleId = entity.AudienceRuleId,
            TriggerEvent = entity.TriggerEvent,
            TriggerDate = entity.TriggerDate,
            EnrolledAt = entity.EnrolledAt,
            EnrolledByEmployeeId = entity.EnrolledByEmployeeId,
            StartedAt = entity.StartedAt,
            CompletedAt = entity.CompletedAt,
            LastActivityAt = entity.LastActivityAt,
            ProgressPercentage = entity.ProgressPercentage,
            CompletionStatus = entity.CompletionStatus,
            FinalScore = entity.FinalScore,
            AttemptCount = entity.AttemptCount,
            IsPassed = entity.IsPassed,
            AcknowledgementSigned = entity.AcknowledgementSigned,
            CertificateIssued = entity.CertificateIssued,
            CertificateSerialNumber = entity.CertificateSerialNumber,
            CertificateExpiresAt = entity.CertificateExpiresAt,
            WaitlistPosition = entity.WaitlistPosition,
            NextDueDate = entity.NextDueDate,
            WithdrawalReason = entity.WithdrawalReason,
            ContentProgress = entity.ContentProgress.Select(c => c.ToDto()).ToList(),
            AssessmentResponses = entity.AssessmentResponses.Select(r => r.ToDto()).ToList(),
            Acknowledgements = entity.Acknowledgements.Select(a => a.ToDto()).ToList(),
            Feedbacks = entity.Feedbacks.Select(f => f.ToDto()).ToList(),
            AttendanceRecords = entity.AttendanceRecords.OrderBy(a => a.SessionDay).Select(a => a.ToDto()).ToList(),
            Certificates = entity.Certificates.Select(c => c.ToDto()).ToList(),
        };
    }

    public static EmployeeOrientationSummaryDto ToSummaryDto(this EmployeeOrientation entity)
    {
        return new EmployeeOrientationSummaryDto
        {
            Id = entity.Id,
            ProgramId = entity.ProgramId,
            ProgramCode = entity.Program?.ProgramCode,
            ProgramTitle = entity.Program?.Title,
            SessionId = entity.SessionId,
            SessionTitle = entity.Session?.Title,
            EmployeeId = entity.EmployeeId,
            EnrollmentStatus = entity.EnrollmentStatus,
            CompletionStatus = entity.CompletionStatus,
            ProgressPercentage = entity.ProgressPercentage,
            FinalScore = entity.FinalScore,
            IsPassed = entity.IsPassed,
            EnrolledAt = entity.EnrolledAt,
            CompletedAt = entity.CompletedAt,
            NextDueDate = entity.NextDueDate,
            EnrollmentSource = entity.EnrollmentSource,
            AudienceRuleId = entity.AudienceRuleId,
            TriggerEvent = entity.TriggerEvent,
            TriggerDate = entity.TriggerDate,
        };
    }

    public static EmployeeOrientation ToEntity(this CreateEmployeeOrientationDto dto, Guid tenantId, Guid userId)
    {
        return new EmployeeOrientation
        {
            TenantId = tenantId,
            ProgramId = dto.ProgramId,
            SessionId = dto.SessionId,
            EmployeeId = dto.EmployeeId,
            EnrollmentStatus = OrientationEnrollmentStatus.Confirmed,
            EnrollmentSource = dto.EnrollmentSource,
            EnrolledAt = DateTime.UtcNow,
            EnrolledByEmployeeId = dto.EnrolledByEmployeeId,
            CompletionStatus = OrientationCompletionStatus.NotStarted,
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this EmployeeOrientation entity, UpdateEmployeeOrientationDto dto, Guid userId)
    {
        entity.SessionId = dto.SessionId;
        entity.EnrollmentStatus = dto.EnrollmentStatus;
        entity.NextDueDate = dto.NextDueDate;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    public static IEnumerable<EmployeeOrientationSummaryDto> ToSummaryDtoList(this IEnumerable<EmployeeOrientation> entities)
        => entities.Select(e => e.ToSummaryDto());

    #endregion

    #region OrientationContentProgress

    public static OrientationContentProgressDto ToDto(this OrientationContentProgress entity)
    {
        return new OrientationContentProgressDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            EmployeeOrientationId = entity.EmployeeOrientationId,
            ContentItemId = entity.ContentItemId,
            ContentItemTitle = entity.ContentItem?.Title,
            ContentType = entity.ContentItem?.ContentType,
            Status = entity.Status,
            FirstAccessedAt = entity.FirstAccessedAt,
            LastAccessedAt = entity.LastAccessedAt,
            CompletedAt = entity.CompletedAt,
            TotalTimeSpentSeconds = entity.TotalTimeSpentSeconds,
            AccessCount = entity.AccessCount,
            IsAcknowledged = entity.IsAcknowledged,
        };
    }

    #endregion

    // ========================================================================
    // SECTION 4 — ASSESSMENT
    // ========================================================================

    #region OrientationAssessmentQuestion & Option

    public static OrientationAssessmentOptionDto ToDto(this OrientationAssessmentOption entity)
    {
        return new OrientationAssessmentOptionDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            QuestionId = entity.QuestionId,
            OptionText = entity.OptionText,
            IsCorrect = entity.IsCorrect,
            DisplayOrder = entity.DisplayOrder,
        };
    }

    public static OrientationAssessmentOption ToEntity(this CreateOrientationAssessmentOptionDto dto, Guid tenantId, Guid userId)
    {
        return new OrientationAssessmentOption
        {
            TenantId = tenantId,
            OptionText = dto.OptionText,
            IsCorrect = dto.IsCorrect,
            DisplayOrder = dto.DisplayOrder,
            CreatedBy = userId.ToString(),
        };
    }

    public static OrientationAssessmentQuestionDto ToDto(this OrientationAssessmentQuestion entity)
    {
        return new OrientationAssessmentQuestionDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            ProgramId = entity.ProgramId,
            ProgramTitle = entity.Program?.Title,
            QuestionText = entity.QuestionText,
            QuestionType = entity.QuestionType,
            Points = entity.Points,
            Explanation = entity.Explanation,
            SequenceOrder = entity.SequenceOrder,
            IsActive = entity.IsActive,
            Options = entity.Options.OrderBy(o => o.DisplayOrder).Select(o => o.ToDto()).ToList(),
        };
    }

    /// <summary>
    /// The participant-facing projection of a question. <paramref name="revealAnswers"/> is false while
    /// the attempt is still open, which blanks <c>IsCorrect</c> on every option and withholds the
    /// explanation — otherwise the client is told which option to pick before it asks.
    /// </summary>
    public static OrientationAssessmentQuestionDto ToParticipantDto(this OrientationAssessmentQuestion entity, bool revealAnswers)
    {
        var dto = entity.ToDto();
        if (revealAnswers) return dto;

        dto.Explanation = null;
        foreach (var option in dto.Options)
            option.IsCorrect = false;

        return dto;
    }

    public static OrientationAssessmentQuestion ToEntity(this CreateOrientationAssessmentQuestionDto dto, Guid tenantId, Guid userId)
    {
        return new OrientationAssessmentQuestion
        {
            TenantId = tenantId,
            ProgramId = dto.ProgramId,
            QuestionText = dto.QuestionText,
            QuestionType = dto.QuestionType,
            Points = dto.Points,
            Explanation = dto.Explanation,
            SequenceOrder = dto.SequenceOrder,
            IsActive = dto.IsActive,
            Options = dto.Options.Select(o => o.ToEntity(tenantId, userId)).ToList(),
            CreatedBy = userId.ToString(),
        };
    }

    public static void UpdateEntity(this OrientationAssessmentQuestion entity, UpdateOrientationAssessmentQuestionDto dto, Guid userId)
    {
        entity.QuestionText = dto.QuestionText;
        entity.QuestionType = dto.QuestionType;
        entity.Points = dto.Points;
        entity.Explanation = dto.Explanation;
        entity.SequenceOrder = dto.SequenceOrder;
        entity.IsActive = dto.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = userId.ToString();
    }

    #endregion

    #region OrientationAssessmentResponse

    public static OrientationAssessmentResponseDto ToDto(this OrientationAssessmentResponse entity)
    {
        return new OrientationAssessmentResponseDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            EmployeeOrientationId = entity.EmployeeOrientationId,
            QuestionId = entity.QuestionId,
            QuestionText = entity.Question?.QuestionText,
            SelectedOptionId = entity.SelectedOptionId,
            SelectedOptionText = entity.SelectedOption?.OptionText,
            FreeTextAnswer = entity.FreeTextAnswer,
            IsCorrect = entity.IsCorrect,
            PointsAwarded = entity.PointsAwarded,
            AnsweredAt = entity.AnsweredAt,
        };
    }

    #endregion

    // ========================================================================
    // SECTION 5 — COMPLETION ARTIFACTS
    // ========================================================================

    #region OrientationAcknowledgement

    public static OrientationAcknowledgementDto ToDto(this OrientationAcknowledgement entity)
    {
        return new OrientationAcknowledgementDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            EmployeeOrientationId = entity.EmployeeOrientationId,
            Title = entity.Title,
            AcknowledgementText = entity.AcknowledgementText,
            Status = entity.Status,
            PresentedAt = entity.PresentedAt,
            SignedAt = entity.SignedAt,
            DeclinedAt = entity.DeclinedAt,
            DeclineReason = entity.DeclineReason,
            SignatureIpAddress = entity.SignatureIpAddress,
        };
    }

    public static OrientationAcknowledgement ToEntity(this CreateOrientationAcknowledgementDto dto, Guid tenantId, Guid userId)
    {
        return new OrientationAcknowledgement
        {
            TenantId = tenantId,
            EmployeeOrientationId = dto.EmployeeOrientationId,
            Title = dto.Title,
            AcknowledgementText = dto.AcknowledgementText,
            Status = OrientationAcknowledgementStatus.Pending,
            CreatedBy = userId.ToString(),
        };
    }

    #endregion

    #region OrientationFeedback

    public static OrientationFeedbackDto ToDto(this OrientationFeedback entity)
    {
        return new OrientationFeedbackDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            EmployeeOrientationId = entity.EmployeeOrientationId,
            OverallRating = entity.OverallRating,
            ContentRating = entity.ContentRating,
            FacilitatorRating = entity.FacilitatorRating,
            RelevanceRating = entity.RelevanceRating,
            Comments = entity.Comments,
            IsAnonymous = entity.IsAnonymous,
            SubmittedByEmployeeId = entity.SubmittedByEmployeeId,
            SubmittedAt = entity.SubmittedAt,
        };
    }

    public static OrientationFeedback ToEntity(this CreateOrientationFeedbackDto dto, Guid tenantId, Guid userId)
    {
        return new OrientationFeedback
        {
            TenantId = tenantId,
            EmployeeOrientationId = dto.EmployeeOrientationId,
            OverallRating = dto.OverallRating,
            ContentRating = dto.ContentRating,
            FacilitatorRating = dto.FacilitatorRating,
            RelevanceRating = dto.RelevanceRating,
            Comments = dto.Comments,
            IsAnonymous = dto.IsAnonymous,
            // SubmittedByEmployeeId is stamped by the service from the token, not copied from here.
            SubmittedAt = DateTime.UtcNow,
            CreatedBy = userId.ToString(),
        };
    }

    #endregion

    #region OrientationCertificate

    public static OrientationCertificateDto ToDto(this OrientationCertificate entity)
    {
        return new OrientationCertificateDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            EmployeeOrientationId = entity.EmployeeOrientationId,
            EmployeeId = entity.EmployeeOrientation?.EmployeeId,
            ProgramTitle = entity.EmployeeOrientation?.Program?.Title,
            CertificateNumber = entity.CertificateNumber,
            IssuedAt = entity.IssuedAt,
            ExpiresAt = entity.ExpiresAt,
            IssuedByEmployeeId = entity.IssuedByEmployeeId,
            FilePath = entity.FilePath,
            VerificationUrl = entity.VerificationUrl,
            Status = entity.Status,
            RevokedAt = entity.RevokedAt,
            RevocationReason = entity.RevocationReason,
        };
    }

    public static OrientationCertificate ToEntity(this IssueOrientationCertificateDto dto, Guid tenantId, Guid userId)
    {
        return new OrientationCertificate
        {
            TenantId = tenantId,
            EmployeeOrientationId = dto.EmployeeOrientationId,
            CertificateNumber = dto.CertificateNumber ?? string.Empty,
            IssuedAt = DateTime.UtcNow,
            ExpiresAt = dto.ExpiresAt,
            IssuedByEmployeeId = dto.IssuedByEmployeeId,
            Status = OrientationCertificateStatus.Active,
            CreatedBy = userId.ToString(),
        };
    }

    #endregion

    #region OrientationNotification

    public static OrientationNotificationDto ToDto(this OrientationNotification entity)
    {
        return new OrientationNotificationDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            ProgramId = entity.ProgramId,
            ProgramTitle = entity.Program?.Title,
            EmployeeOrientationId = entity.EmployeeOrientationId,
            RecipientEmployeeId = entity.RecipientEmployeeId,
            Type = entity.Type,
            Subject = entity.Subject,
            Message = entity.Message,
            NavigationUrl = entity.NavigationUrl,
            IsRead = entity.IsRead,
            ReadAt = entity.ReadAt,
            SentAt = entity.SentAt,
        };
    }

    public static OrientationNotification ToEntity(this CreateOrientationNotificationDto dto, Guid tenantId, Guid userId)
    {
        return new OrientationNotification
        {
            TenantId = tenantId,
            ProgramId = dto.ProgramId,
            EmployeeOrientationId = dto.EmployeeOrientationId,
            RecipientEmployeeId = dto.RecipientEmployeeId,
            Type = dto.Type,
            Subject = dto.Subject,
            Message = dto.Message,
            NavigationUrl = dto.NavigationUrl,
            SentAt = DateTime.UtcNow,
            CreatedBy = userId.ToString(),
        };
    }

    public static IEnumerable<OrientationNotificationDto> ToDtoList(this IEnumerable<OrientationNotification> entities)
        => entities.Select(e => e.ToDto());

    #endregion
}
