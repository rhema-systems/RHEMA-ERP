using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.HR;

// ============================================================================
// ORIENTATION MODULE DTOs
// ============================================================================

// ============================================================================
// SECTION 1 — CATALOG (Category, Program, Module, Content, Prerequisite, Audience)
// ============================================================================

#region Orientation Category

/// <summary>Lightweight read model for category dropdown lookups.</summary>
public class OrientationCategoryLookupDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? ParentCategoryId { get; set; }
}

public class OrientationCategoryDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? ParentCategoryId { get; set; }
    public string? ParentCategoryName { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; }

    public int ProgramCount { get; set; }
    public List<OrientationCategoryLookupDto> SubCategories { get; set; } = new();
}

public class CreateOrientationCategoryDto : CreateDtoBase
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public Guid? ParentCategoryId { get; set; }

    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdateOrientationCategoryDto : UpdateDtoBase
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public Guid? ParentCategoryId { get; set; }

    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; }
}

#endregion

#region Orientation Program

public class OrientationProgramDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string ProgramCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Objectives { get; set; }

    // Category
    public Guid? CategoryId { get; set; }
    public string? CategoryName { get; set; }

    // Classification
    public OrientationProgramType ProgramType { get; set; }
    public string ProgramTypeName => ProgramType.ToString();
    public OrientationDeliveryMode DefaultDeliveryMode { get; set; }
    public string DefaultDeliveryModeName => DefaultDeliveryMode.ToString();
    public OrientationProgramStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public OrientationPriority Priority { get; set; }
    public string PriorityName => Priority.ToString();
    public OrientationAudienceScope AudienceScope { get; set; }
    public string AudienceScopeName => AudienceScope.ToString();

    public int? EstimatedDurationMinutes { get; set; }

    // Completion rules
    public bool RequiresAssessment { get; set; }
    public decimal? PassingScorePercent { get; set; }
    public bool RequiresAcknowledgement { get; set; }
    public int? CompletionDeadlineDays { get; set; }

    // Certificate
    public bool IsCertificateIssued { get; set; }
    public int? CertificateValidityMonths { get; set; }

    // Recurrence
    public bool IsRecurring { get; set; }
    public OrientationRecurrenceFrequency? RecurrenceFrequency { get; set; }
    public string? RecurrenceFrequencyName => RecurrenceFrequency?.ToString();

    public bool EnableReminders { get; set; }
    public string? Version { get; set; }
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public string? Tags { get; set; }

    // Ownership
    public Guid? OwnerEmployeeId { get; set; }
    public string? OwnerEmployeeName { get; set; }
    public Guid? OwnerOrganizationUnitId { get; set; }
    public string? OwnerOrganizationUnitName { get; set; }

    // Roll-up counts
    public int ModuleCount { get; set; }
    public int SessionCount { get; set; }
    public int EnrollmentCount { get; set; }
    public int CompletedCount { get; set; }

    // Child collections
    public List<OrientationModuleDto> Modules { get; set; } = new();
    public List<OrientationAudienceRuleDto> AudienceRules { get; set; } = new();
    public List<OrientationPrerequisiteDto> Prerequisites { get; set; } = new();
    public List<OrientationAssessmentQuestionDto> AssessmentQuestions { get; set; } = new();
}

public class OrientationProgramSummaryDto
{
    public Guid Id { get; set; }
    public string ProgramCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? CategoryName { get; set; }
    public OrientationProgramType ProgramType { get; set; }
    public string ProgramTypeName => ProgramType.ToString();
    public OrientationProgramStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public OrientationPriority Priority { get; set; }
    public string PriorityName => Priority.ToString();
    public OrientationDeliveryMode DefaultDeliveryMode { get; set; }
    public string DefaultDeliveryModeName => DefaultDeliveryMode.ToString();
    public int? EstimatedDurationMinutes { get; set; }
    public bool IsCertificateIssued { get; set; }
    public bool RequiresAssessment { get; set; }

    /// <summary>Round 4, lane I-b — lists need it to offer "open the next cycle" on a completion.</summary>
    public bool IsRecurring { get; set; }
    public OrientationRecurrenceFrequency? RecurrenceFrequency { get; set; }

    /// <summary>Round 4, lane L: Active and in its effective dates — what an enrolment picker may offer.</summary>
    public bool AcceptsEnrolment { get; set; }

    /// <summary>Why not ("it has been retired"), when <see cref="AcceptsEnrolment"/> is false.</summary>
    public string? ClosedBecause { get; set; }

    public int ModuleCount { get; set; }
    public int EnrollmentCount { get; set; }
    public int CompletedCount { get; set; }
}

public class CreateOrientationProgramDto : CreateDtoBase
{
    [MaxLength(50)]
    public string? ProgramCode { get; set; }   // auto-generated if not supplied

    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(4000)]
    public string? Objectives { get; set; }

    public Guid? CategoryId { get; set; }

    [Required]
    public OrientationProgramType ProgramType { get; set; }

    [Required]
    public OrientationDeliveryMode DefaultDeliveryMode { get; set; }

    [Required]
    public OrientationPriority Priority { get; set; }

    [Required]
    public OrientationAudienceScope AudienceScope { get; set; }

    public int? EstimatedDurationMinutes { get; set; }

    public bool RequiresAssessment { get; set; }

    [Range(0, 100)]
    public decimal? PassingScorePercent { get; set; }

    public bool RequiresAcknowledgement { get; set; }
    public int? CompletionDeadlineDays { get; set; }

    public bool IsCertificateIssued { get; set; }
    public int? CertificateValidityMonths { get; set; }

    public bool IsRecurring { get; set; }
    public OrientationRecurrenceFrequency? RecurrenceFrequency { get; set; }

    public bool EnableReminders { get; set; } = true;

    [MaxLength(20)]
    public string? Version { get; set; }

    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }

    [MaxLength(1000)]
    public string? Tags { get; set; }

    public Guid? OwnerEmployeeId { get; set; }
    public Guid? OwnerOrganizationUnitId { get; set; }
}

public class UpdateOrientationProgramDto : UpdateDtoBase
{
    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(4000)]
    public string? Objectives { get; set; }

    public Guid? CategoryId { get; set; }

    [Required]
    public OrientationProgramType ProgramType { get; set; }

    [Required]
    public OrientationDeliveryMode DefaultDeliveryMode { get; set; }

    [Required]
    public OrientationPriority Priority { get; set; }

    [Required]
    public OrientationAudienceScope AudienceScope { get; set; }

    public int? EstimatedDurationMinutes { get; set; }

    public bool RequiresAssessment { get; set; }

    [Range(0, 100)]
    public decimal? PassingScorePercent { get; set; }

    public bool RequiresAcknowledgement { get; set; }
    public int? CompletionDeadlineDays { get; set; }

    public bool IsCertificateIssued { get; set; }
    public int? CertificateValidityMonths { get; set; }

    public bool IsRecurring { get; set; }
    public OrientationRecurrenceFrequency? RecurrenceFrequency { get; set; }

    public bool EnableReminders { get; set; }

    [MaxLength(20)]
    public string? Version { get; set; }

    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }

    [MaxLength(1000)]
    public string? Tags { get; set; }

    public Guid? OwnerEmployeeId { get; set; }
    public Guid? OwnerOrganizationUnitId { get; set; }
}

/// <summary>
/// Copy a programme (round 4, lane J2): its modules and their content, its assessment questions and
/// their options, its prerequisites and its audience rules — as a Draft. Sessions and enrolments are
/// deliveries of the original and stay with it.
/// </summary>
public class CloneOrientationProgramDto
{
    /// <summary>The copy's title.</summary>
    [Required]
    [MaxLength(300)]
    public string NewName { get; set; } = string.Empty;

    /// <summary>The copy's programme code; generated when omitted.</summary>
    [MaxLength(50)]
    public string? NewCode { get; set; }
}

/// <summary>Lifecycle transition for a program (publish, suspend, retire, archive …).</summary>
public class ChangeOrientationProgramStatusDto
{
    [Required]
    public Guid ProgramId { get; set; }

    [Required]
    public OrientationProgramStatus NewStatus { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

#endregion

#region Orientation Module

public class OrientationModuleDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid ProgramId { get; set; }
    public string? ProgramTitle { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SequenceOrder { get; set; }

    public OrientationModuleType ModuleType { get; set; }
    public string ModuleTypeName => ModuleType.ToString();

    public int? EstimatedDurationMinutes { get; set; }
    public bool IsSequentiallyRequired { get; set; }
    public bool IsOptional { get; set; }
    public bool IsActive { get; set; }

    public int ContentItemCount { get; set; }
    public List<OrientationContentItemDto> ContentItems { get; set; } = new();
}

public class CreateOrientationModuleDto : CreateDtoBase
{
    [Required]
    public Guid ProgramId { get; set; }

    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    public int SequenceOrder { get; set; }

    [Required]
    public OrientationModuleType ModuleType { get; set; }

    public int? EstimatedDurationMinutes { get; set; }
    public bool IsSequentiallyRequired { get; set; } = true;
    public bool IsOptional { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdateOrientationModuleDto : UpdateDtoBase
{
    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    public int SequenceOrder { get; set; }

    [Required]
    public OrientationModuleType ModuleType { get; set; }

    public int? EstimatedDurationMinutes { get; set; }
    public bool IsSequentiallyRequired { get; set; }
    public bool IsOptional { get; set; }
    public bool IsActive { get; set; }
}

#endregion

#region Orientation Content Item

public class OrientationContentItemDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid ModuleId { get; set; }
    public string? ModuleTitle { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    public OrientationContentType ContentType { get; set; }
    public string ContentTypeName => ContentType.ToString();

    public string? ResourceUrl { get; set; }
    public string? OriginalFileName { get; set; }
    public long? FileSizeBytes { get; set; }
    public int? MediaDurationSeconds { get; set; }
    public int SequenceOrder { get; set; }
    public bool IsRequired { get; set; }
    public bool IsActive { get; set; }
}

public class CreateOrientationContentItemDto : CreateDtoBase
{
    [Required]
    public Guid ModuleId { get; set; }

    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [Required]
    public OrientationContentType ContentType { get; set; }

    [MaxLength(1000)]
    public string? ResourceUrl { get; set; }

    [MaxLength(500)]
    public string? OriginalFileName { get; set; }

    public long? FileSizeBytes { get; set; }
    public int? MediaDurationSeconds { get; set; }
    public int SequenceOrder { get; set; }
    public bool IsRequired { get; set; } = true;
    public bool IsActive { get; set; } = true;
}

public class UpdateOrientationContentItemDto : UpdateDtoBase
{
    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [Required]
    public OrientationContentType ContentType { get; set; }

    [MaxLength(1000)]
    public string? ResourceUrl { get; set; }

    [MaxLength(500)]
    public string? OriginalFileName { get; set; }

    public long? FileSizeBytes { get; set; }
    public int? MediaDurationSeconds { get; set; }
    public int SequenceOrder { get; set; }
    public bool IsRequired { get; set; }
    public bool IsActive { get; set; }
}

#endregion

#region Orientation Prerequisite

public class OrientationPrerequisiteDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid ProgramId { get; set; }
    public string? ProgramTitle { get; set; }

    public Guid PrerequisiteProgramId { get; set; }
    public string? PrerequisiteProgramCode { get; set; }
    public string? PrerequisiteProgramTitle { get; set; }

    public bool IsMandatory { get; set; }
    public string? Notes { get; set; }
}

public class CreateOrientationPrerequisiteDto : CreateDtoBase
{
    [Required]
    public Guid ProgramId { get; set; }

    [Required]
    public Guid PrerequisiteProgramId { get; set; }

    public bool IsMandatory { get; set; } = true;

    [MaxLength(500)]
    public string? Notes { get; set; }
}

public class UpdateOrientationPrerequisiteDto : UpdateDtoBase
{
    public bool IsMandatory { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}

#endregion

#region Orientation Audience Rule

public class OrientationAudienceRuleDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid ProgramId { get; set; }
    public string? ProgramTitle { get; set; }

    public string RuleName { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Where the people sit — the shared HR audience axis (round 4, lane I1).</summary>
    public HrAudienceTargetType TargetType { get; set; }
    public string TargetTypeName => TargetType.ToString();
    public Guid? TargetEntityId { get; set; }

    /// <summary>
    /// The unit, level, position, location or employee's name. Declared from the start and never
    /// filled until lane I2 — the rules list could only ever show a GUID.
    /// </summary>
    public string? TargetEntityName { get; set; }

    /// <summary>Which of the people at the target the rule means.</summary>
    public OrientationAudiencePopulation Population { get; set; }

    public OrientationEnrollmentTrigger Trigger { get; set; }
    public string TriggerName => Trigger.ToString();

    public int EnrollmentDelayDays { get; set; }
    public bool IsInclusive { get; set; }
    public bool IsActive { get; set; }

    /// <summary>How many active employees this rule reaches today, target and population together.
    /// Filled on the list read; an exclusion's reach is the number it keeps out.</summary>
    public int? ReachCount { get; set; }
}

public class CreateOrientationAudienceRuleDto : CreateDtoBase
{
    [Required]
    public Guid ProgramId { get; set; }

    [Required]
    [MaxLength(200)]
    public string RuleName { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    public HrAudienceTargetType TargetType { get; set; }

    public Guid? TargetEntityId { get; set; }

    public OrientationAudiencePopulation Population { get; set; } = OrientationAudiencePopulation.Anyone;

    [Required]
    public OrientationEnrollmentTrigger Trigger { get; set; }

    public int EnrollmentDelayDays { get; set; }
    public bool IsInclusive { get; set; } = true;
    public bool IsActive { get; set; } = true;
}

public class UpdateOrientationAudienceRuleDto : UpdateDtoBase
{
    [Required]
    [MaxLength(200)]
    public string RuleName { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    public HrAudienceTargetType TargetType { get; set; }

    public Guid? TargetEntityId { get; set; }

    public OrientationAudiencePopulation Population { get; set; } = OrientationAudiencePopulation.Anyone;

    [Required]
    public OrientationEnrollmentTrigger Trigger { get; set; }

    public int EnrollmentDelayDays { get; set; }
    public bool IsInclusive { get; set; }
    public bool IsActive { get; set; }
}

#endregion

// ============================================================================
// SECTION 2 — DELIVERY (Session, Facilitator, Attendance)
// ============================================================================

#region Orientation Session

public class OrientationSessionDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid ProgramId { get; set; }
    public string? ProgramCode { get; set; }
    public string? ProgramTitle { get; set; }

    public string SessionCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    public OrientationDeliveryMode DeliveryMode { get; set; }
    public string DeliveryModeName => DeliveryMode.ToString();
    public OrientationSessionStatus Status { get; set; }
    public string StatusName => Status.ToString();

    public DateTime? ScheduledStartAt { get; set; }
    public DateTime? ScheduledEndAt { get; set; }
    public DateTime? ActualStartAt { get; set; }
    public DateTime? ActualEndAt { get; set; }

    public string? VenueDescription { get; set; }
    public string? VirtualMeetingUrl { get; set; }
    public int? MaxParticipants { get; set; }
    public DateTime? EnrollmentDeadlineAt { get; set; }
    public bool AllowWaitlist { get; set; }
    public bool RequiresApproval { get; set; }
    public string? RecordingUrl { get; set; }
    public string? ParticipantInstructions { get; set; }

    public int EnrolledCount { get; set; }
    public int AvailableSeats { get; set; }
    public List<OrientationSessionFacilitatorDto> Facilitators { get; set; } = new();
}

public class OrientationSessionSummaryDto
{
    public Guid Id { get; set; }
    public string SessionCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public Guid ProgramId { get; set; }
    public string? ProgramTitle { get; set; }
    public OrientationDeliveryMode DeliveryMode { get; set; }
    public string DeliveryModeName => DeliveryMode.ToString();
    public OrientationSessionStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public DateTime? ScheduledStartAt { get; set; }
    public int? MaxParticipants { get; set; }
    public int EnrolledCount { get; set; }

    // Round 4, lane L — so a picker can mark what it cannot take rather than offer it.
    public DateTime? EnrollmentDeadlineAt { get; set; }

    /// <summary>Open for enrolment now — the same definition the enrol check applies.</summary>
    public bool AcceptsEnrolment { get; set; }

    /// <summary>Why not, in words ("it was cancelled"), when <see cref="AcceptsEnrolment"/> is false.</summary>
    public string? ClosedBecause { get; set; }
}

public class CreateOrientationSessionDto : CreateDtoBase
{
    [Required]
    public Guid ProgramId { get; set; }

    [MaxLength(50)]
    public string? SessionCode { get; set; }   // auto-generated if not supplied

    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [Required]
    public OrientationDeliveryMode DeliveryMode { get; set; }

    public DateTime? ScheduledStartAt { get; set; }
    public DateTime? ScheduledEndAt { get; set; }

    [MaxLength(500)]
    public string? VenueDescription { get; set; }

    [MaxLength(500)]
    public string? VirtualMeetingUrl { get; set; }

    public int? MaxParticipants { get; set; }
    public DateTime? EnrollmentDeadlineAt { get; set; }
    public bool AllowWaitlist { get; set; }
    public bool RequiresApproval { get; set; }

    [MaxLength(500)]
    public string? RecordingUrl { get; set; }

    [MaxLength(4000)]
    public string? ParticipantInstructions { get; set; }
}

public class UpdateOrientationSessionDto : UpdateDtoBase
{
    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [Required]
    public OrientationDeliveryMode DeliveryMode { get; set; }

    public DateTime? ScheduledStartAt { get; set; }
    public DateTime? ScheduledEndAt { get; set; }
    public DateTime? ActualStartAt { get; set; }
    public DateTime? ActualEndAt { get; set; }

    [MaxLength(500)]
    public string? VenueDescription { get; set; }

    [MaxLength(500)]
    public string? VirtualMeetingUrl { get; set; }

    public int? MaxParticipants { get; set; }
    public DateTime? EnrollmentDeadlineAt { get; set; }
    public bool AllowWaitlist { get; set; }
    public bool RequiresApproval { get; set; }

    [MaxLength(500)]
    public string? RecordingUrl { get; set; }

    [MaxLength(4000)]
    public string? ParticipantInstructions { get; set; }
}

/// <summary>
/// Run a session again on a new date (round 4, lane J3) — the common case for a recurring briefing.
/// </summary>
public class CloneOrientationSessionDto
{
    [Required]
    public DateTime? ScheduledStartAt { get; set; }

    /// <summary>When omitted, the copy keeps the original's length.</summary>
    public DateTime? ScheduledEndAt { get; set; }

    /// <summary>When omitted, the original's title — which often names the month, so the screen asks.</summary>
    [MaxLength(300)]
    public string? Title { get; set; }
}

public class ChangeOrientationSessionStatusDto
{
    [Required]
    public Guid SessionId { get; set; }

    [Required]
    public OrientationSessionStatus NewStatus { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

#endregion

#region Orientation Session Facilitator

public class OrientationSessionFacilitatorDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid SessionId { get; set; }

    public Guid? EmployeeId { get; set; }
    public string? EmployeeName { get; set; }

    public string? ExternalFacilitatorName { get; set; }
    public string? ExternalFacilitatorEmail { get; set; }
    public string? ExternalFacilitatorOrganization { get; set; }

    /// <summary>The training vendor, when picked from the register (round 4, lane M).</summary>
    public Guid? ExternalFacilitatorVendorId { get; set; }

    /// <summary>The vendor's trainer, when picked from the register and the person is known.</summary>
    public Guid? ExternalFacilitatorTrainerProfileId { get; set; }

    /// <summary>
    /// What the register says NOW about a vendor or trainer picked earlier, when it matters — "GIMPA
    /// has since been blacklisted in the training vendor register". Null when all is well or nothing
    /// was picked. The snapshot columns keep saying what was agreed; this says what changed since.
    /// </summary>
    public string? RegisterNote { get; set; }

    public OrientationFacilitatorRole Role { get; set; }
    public string RoleName => Role.ToString();

    public bool HasConfirmed { get; set; }
    public string? Notes { get; set; }

    /// <summary>
    /// Resolved display name — the employee, the external person, or (a vendor yet to name its
    /// trainer) the vendor.
    /// </summary>
    public string DisplayName => EmployeeName ?? ExternalFacilitatorName ?? ExternalFacilitatorOrganization ?? string.Empty;
}

public class CreateOrientationSessionFacilitatorDto : CreateDtoBase
{
    [Required]
    public Guid SessionId { get; set; }

    public Guid? EmployeeId { get; set; }

    [MaxLength(200)]
    public string? ExternalFacilitatorName { get; set; }

    [MaxLength(200)]
    [EmailAddress]
    public string? ExternalFacilitatorEmail { get; set; }

    [MaxLength(200)]
    public string? ExternalFacilitatorOrganization { get; set; }

    /// <summary>
    /// Pick from the training vendor register (round 4, lane M). The name, email and organisation are
    /// then taken from the register and any typed values are ignored. A trainer alone is enough — its
    /// vendor is implied.
    /// </summary>
    public Guid? ExternalFacilitatorVendorId { get; set; }

    public Guid? ExternalFacilitatorTrainerProfileId { get; set; }

    [Required]
    public OrientationFacilitatorRole Role { get; set; }

    public bool HasConfirmed { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}

public class UpdateOrientationSessionFacilitatorDto : UpdateDtoBase
{
    public Guid? EmployeeId { get; set; }

    [MaxLength(200)]
    public string? ExternalFacilitatorName { get; set; }

    [MaxLength(200)]
    [EmailAddress]
    public string? ExternalFacilitatorEmail { get; set; }

    [MaxLength(200)]
    public string? ExternalFacilitatorOrganization { get; set; }

    /// <summary>
    /// ⚠ The whole pick, every time — like every field here. Omitting these on an update means "not
    /// from the register", and the facilitator becomes a typed one.
    /// </summary>
    public Guid? ExternalFacilitatorVendorId { get; set; }

    public Guid? ExternalFacilitatorTrainerProfileId { get; set; }

    [Required]
    public OrientationFacilitatorRole Role { get; set; }

    public bool HasConfirmed { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}

#endregion

#region Orientation Attendance Record

public class OrientationAttendanceRecordDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid EnrollmentId { get; set; }

    public Guid? EmployeeId { get; set; }
    public string? EmployeeName { get; set; }

    public int SessionDay { get; set; }

    public OrientationAttendanceStatus AttendanceStatus { get; set; }
    public string AttendanceStatusName => AttendanceStatus.ToString();

    public DateTime? CheckInAt { get; set; }
    public DateTime? CheckOutAt { get; set; }
    public int? AttendedMinutes { get; set; }
    public bool MarkedLate { get; set; }
    public string? AbsenceReason { get; set; }
    public Guid? MarkedByEmployeeId { get; set; }
    public string? MarkedByName { get; set; }
}

public class CreateOrientationAttendanceRecordDto : CreateDtoBase
{
    [Required]
    public Guid EnrollmentId { get; set; }

    public int SessionDay { get; set; } = 1;

    [Required]
    public OrientationAttendanceStatus AttendanceStatus { get; set; }

    public DateTime? CheckInAt { get; set; }
    public DateTime? CheckOutAt { get; set; }
    public int? AttendedMinutes { get; set; }
    public bool MarkedLate { get; set; }

    [MaxLength(500)]
    public string? AbsenceReason { get; set; }

    public Guid? MarkedByEmployeeId { get; set; }
}

public class UpdateOrientationAttendanceRecordDto : UpdateDtoBase
{
    public int SessionDay { get; set; }

    [Required]
    public OrientationAttendanceStatus AttendanceStatus { get; set; }

    public DateTime? CheckInAt { get; set; }
    public DateTime? CheckOutAt { get; set; }
    public int? AttendedMinutes { get; set; }
    public bool MarkedLate { get; set; }

    [MaxLength(500)]
    public string? AbsenceReason { get; set; }

    public Guid? MarkedByEmployeeId { get; set; }
}

/// <summary>Bulk attendance marking for a session day.</summary>
public class MarkOrientationAttendanceDto
{
    [Required]
    public Guid SessionId { get; set; }

    public int SessionDay { get; set; } = 1;

    [Required]
    public List<OrientationAttendanceEntryDto> Entries { get; set; } = new();
}

public class OrientationAttendanceEntryDto
{
    [Required]
    public Guid EnrollmentId { get; set; }

    [Required]
    public OrientationAttendanceStatus AttendanceStatus { get; set; }

    public DateTime? CheckInAt { get; set; }
    public DateTime? CheckOutAt { get; set; }
    public bool MarkedLate { get; set; }

    [MaxLength(500)]
    public string? AbsenceReason { get; set; }
}

#endregion

// ============================================================================
// SECTION 3 — ENROLLMENT & PROGRESS
// ============================================================================

#region Employee Orientation (Enrollment)

public class EmployeeOrientationDto : BaseDto
{
    public Guid TenantId { get; set; }

    public Guid ProgramId { get; set; }
    public string? ProgramCode { get; set; }
    public string? ProgramTitle { get; set; }

    public Guid? SessionId { get; set; }
    public string? SessionTitle { get; set; }

    public Guid EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public string? EmployeeNumber { get; set; }

    public OrientationEnrollmentStatus EnrollmentStatus { get; set; }
    public string EnrollmentStatusName => EnrollmentStatus.ToString();
    public OrientationEnrollmentSource EnrollmentSource { get; set; }
    public string EnrollmentSourceName => EnrollmentSource.ToString();

    /// <summary>Round 4, lane I3: the audience rule, event and date behind an automatic enrollment.</summary>
    public Guid? AudienceRuleId { get; set; }
    public OrientationEnrollmentTrigger? TriggerEvent { get; set; }
    public DateOnly? TriggerDate { get; set; }

    public DateTime EnrolledAt { get; set; }
    public Guid? EnrolledByEmployeeId { get; set; }
    public string? EnrolledByName { get; set; }

    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? LastActivityAt { get; set; }

    public int ProgressPercentage { get; set; }

    public OrientationCompletionStatus CompletionStatus { get; set; }
    public string CompletionStatusName => CompletionStatus.ToString();

    public decimal? FinalScore { get; set; }
    public int AttemptCount { get; set; }
    public bool IsPassed { get; set; }

    public bool AcknowledgementSigned { get; set; }

    public bool CertificateIssued { get; set; }
    public string? CertificateSerialNumber { get; set; }
    public DateTime? CertificateExpiresAt { get; set; }

    public int? WaitlistPosition { get; set; }
    public DateTime? NextDueDate { get; set; }
    public string? WithdrawalReason { get; set; }

    // Child collections
    public List<OrientationContentProgressDto> ContentProgress { get; set; } = new();
    public List<OrientationAssessmentResponseDto> AssessmentResponses { get; set; } = new();
    public List<OrientationAcknowledgementDto> Acknowledgements { get; set; } = new();
    public List<OrientationFeedbackDto> Feedbacks { get; set; } = new();
    public List<OrientationAttendanceRecordDto> AttendanceRecords { get; set; } = new();
    public List<OrientationCertificateDto> Certificates { get; set; } = new();
}

public class EmployeeOrientationSummaryDto
{
    public Guid Id { get; set; }
    public Guid ProgramId { get; set; }
    public string? ProgramCode { get; set; }
    public string? ProgramTitle { get; set; }
    public Guid? SessionId { get; set; }
    public string? SessionTitle { get; set; }
    public Guid EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public string? EmployeeNumber { get; set; }
    public OrientationEnrollmentStatus EnrollmentStatus { get; set; }
    public string EnrollmentStatusName => EnrollmentStatus.ToString();
    public OrientationCompletionStatus CompletionStatus { get; set; }
    public string CompletionStatusName => CompletionStatus.ToString();
    public int ProgressPercentage { get; set; }
    public decimal? FinalScore { get; set; }
    public bool IsPassed { get; set; }
    public DateTime EnrolledAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? NextDueDate { get; set; }

    // Round 4, lane I3 — how the person came to be on it.
    public OrientationEnrollmentSource EnrollmentSource { get; set; }
    public Guid? AudienceRuleId { get; set; }
    public OrientationEnrollmentTrigger? TriggerEvent { get; set; }
    public DateOnly? TriggerDate { get; set; }

    // Round 4, lane K-b — so the list can offer "Issue certificate" or "Reissue".
    public bool CertificateIssued { get; set; }
    public string? CertificateSerialNumber { get; set; }
}

public class CreateEmployeeOrientationDto : CreateDtoBase
{
    [Required]
    public Guid ProgramId { get; set; }

    public Guid? SessionId { get; set; }

    [Required]
    public Guid EmployeeId { get; set; }

    public OrientationEnrollmentSource EnrollmentSource { get; set; } = OrientationEnrollmentSource.HrAssigned;

    public Guid? EnrolledByEmployeeId { get; set; }
}

/// <summary>Enroll many employees into a program (and optionally a session) at once.</summary>
public class BulkEnrollOrientationDto
{
    [Required]
    public Guid ProgramId { get; set; }

    public Guid? SessionId { get; set; }

    [Required]
    [MinLength(1)]
    public List<Guid> EmployeeIds { get; set; } = new();

    public OrientationEnrollmentSource EnrollmentSource { get; set; } = OrientationEnrollmentSource.HrAssigned;

    public Guid? EnrolledByEmployeeId { get; set; }
}

public class UpdateEmployeeOrientationDto : UpdateDtoBase
{
    public Guid? SessionId { get; set; }

    [Required]
    public OrientationEnrollmentStatus EnrollmentStatus { get; set; }

    public DateTime? NextDueDate { get; set; }
}

public class WithdrawOrientationDto
{
    [Required]
    public Guid EnrollmentId { get; set; }

    [Required]
    [MaxLength(1000)]
    public string WithdrawalReason { get; set; } = string.Empty;
}

#endregion

#region Orientation Content Progress

public class OrientationContentProgressDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid EmployeeOrientationId { get; set; }

    public Guid ContentItemId { get; set; }
    public string? ContentItemTitle { get; set; }
    public OrientationContentType? ContentType { get; set; }
    public string? ContentTypeName => ContentType?.ToString();

    public OrientationContentProgressStatus Status { get; set; }
    public string StatusName => Status.ToString();

    public DateTime? FirstAccessedAt { get; set; }
    public DateTime? LastAccessedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int TotalTimeSpentSeconds { get; set; }
    public int AccessCount { get; set; }
    public bool IsAcknowledged { get; set; }
}

/// <summary>
/// Records a participant's interaction with a content item. The service upserts the
/// progress row, increments time/access, and flips status/acknowledgement as needed.
/// </summary>
public class TrackOrientationContentProgressDto
{
    [Required]
    public Guid EmployeeOrientationId { get; set; }

    [Required]
    public Guid ContentItemId { get; set; }

    /// <summary>Seconds to add to the running total for this view.</summary>
    public int TimeSpentSecondsDelta { get; set; }

    /// <summary>When true, mark the item completed.</summary>
    public bool MarkCompleted { get; set; }

    /// <summary>When true, record acknowledgement of the item.</summary>
    public bool Acknowledge { get; set; }
}

#endregion

// ============================================================================
// SECTION 4 — ASSESSMENT (light Q&A)
// ============================================================================

#region Orientation Assessment Question & Option

public class OrientationAssessmentOptionDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid QuestionId { get; set; }
    public string OptionText { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
    public int DisplayOrder { get; set; }
}

public class OrientationAssessmentQuestionDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid ProgramId { get; set; }
    public string? ProgramTitle { get; set; }

    public string QuestionText { get; set; } = string.Empty;

    public OrientationQuestionType QuestionType { get; set; }
    public string QuestionTypeName => QuestionType.ToString();

    public decimal Points { get; set; }
    public string? Explanation { get; set; }
    public int SequenceOrder { get; set; }
    public bool IsActive { get; set; }

    public List<OrientationAssessmentOptionDto> Options { get; set; } = new();
}

public class CreateOrientationAssessmentOptionDto
{
    [Required]
    [MaxLength(1000)]
    public string OptionText { get; set; } = string.Empty;

    public bool IsCorrect { get; set; }
    public int DisplayOrder { get; set; }
}

public class CreateOrientationAssessmentQuestionDto : CreateDtoBase
{
    [Required]
    public Guid ProgramId { get; set; }

    [Required]
    [MaxLength(2000)]
    public string QuestionText { get; set; } = string.Empty;

    [Required]
    public OrientationQuestionType QuestionType { get; set; }

    [Range(0, 1000)]
    public decimal Points { get; set; } = 1;

    [MaxLength(2000)]
    public string? Explanation { get; set; }

    public int SequenceOrder { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Options for choice-based questions (ignored for free text).</summary>
    public List<CreateOrientationAssessmentOptionDto> Options { get; set; } = new();
}

public class UpdateOrientationAssessmentQuestionDto : UpdateDtoBase
{
    [Required]
    [MaxLength(2000)]
    public string QuestionText { get; set; } = string.Empty;

    [Required]
    public OrientationQuestionType QuestionType { get; set; }

    [Range(0, 1000)]
    public decimal Points { get; set; }

    [MaxLength(2000)]
    public string? Explanation { get; set; }

    public int SequenceOrder { get; set; }
    public bool IsActive { get; set; }

    /// <summary>Full replacement set of options for choice-based questions.</summary>
    public List<CreateOrientationAssessmentOptionDto> Options { get; set; } = new();
}

#endregion

#region Orientation Assessment Response & Submission

public class OrientationAssessmentResponseDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid EmployeeOrientationId { get; set; }

    public Guid QuestionId { get; set; }
    public string? QuestionText { get; set; }

    public Guid? SelectedOptionId { get; set; }
    public string? SelectedOptionText { get; set; }
    public string? FreeTextAnswer { get; set; }

    public bool IsCorrect { get; set; }
    public decimal? PointsAwarded { get; set; }
    public DateTime AnsweredAt { get; set; }
}

/// <summary>A single answer within an assessment submission.</summary>
public class OrientationAssessmentAnswerDto
{
    [Required]
    public Guid QuestionId { get; set; }

    /// <summary>Selected option ids (one for single-choice/true-false, many for multi-select).</summary>
    public List<Guid> SelectedOptionIds { get; set; } = new();

    [MaxLength(4000)]
    public string? FreeTextAnswer { get; set; }
}

/// <summary>Submit a full assessment attempt for an enrollment; the service grades and rolls up the score.</summary>
public class SubmitOrientationAssessmentDto
{
    [Required]
    public Guid EmployeeOrientationId { get; set; }

    [Required]
    [MinLength(1)]
    public List<OrientationAssessmentAnswerDto> Answers { get; set; } = new();
}

/// <summary>Graded outcome returned after an assessment submission.</summary>
public class OrientationAssessmentResultDto
{
    public Guid EmployeeOrientationId { get; set; }
    public decimal ScorePercent { get; set; }
    public decimal PointsAwarded { get; set; }
    public decimal TotalPoints { get; set; }
    public int CorrectCount { get; set; }
    public int TotalQuestions { get; set; }
    public bool Passed { get; set; }
    public int AttemptNumber { get; set; }
    public List<OrientationAssessmentResponseDto> Responses { get; set; } = new();
}

#endregion

// ============================================================================
// SECTION 5 — COMPLETION ARTIFACTS (Acknowledgement, Feedback, Certificate, Notification)
// ============================================================================

#region Orientation Acknowledgement

public class OrientationAcknowledgementDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid EmployeeOrientationId { get; set; }

    public string Title { get; set; } = string.Empty;
    public string AcknowledgementText { get; set; } = string.Empty;

    public OrientationAcknowledgementStatus Status { get; set; }
    public string StatusName => Status.ToString();

    public DateTime? PresentedAt { get; set; }
    public DateTime? SignedAt { get; set; }
    public DateTime? DeclinedAt { get; set; }
    public string? DeclineReason { get; set; }
    public string? SignatureIpAddress { get; set; }
}

public class CreateOrientationAcknowledgementDto : CreateDtoBase
{
    [Required]
    public Guid EmployeeOrientationId { get; set; }

    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(8000)]
    public string AcknowledgementText { get; set; } = string.Empty;
}

/// <summary>Participant signs or declines a presented acknowledgement.</summary>
public class SignOrientationAcknowledgementDto
{
    [Required]
    public Guid AcknowledgementId { get; set; }

    /// <summary>True = sign/accept, false = decline.</summary>
    public bool Accept { get; set; } = true;

    [MaxLength(500)]
    public string? DeclineReason { get; set; }
}

#endregion

#region Orientation Feedback

public class OrientationFeedbackDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid EmployeeOrientationId { get; set; }

    public int? OverallRating { get; set; }
    public int? ContentRating { get; set; }
    public int? FacilitatorRating { get; set; }
    public int? RelevanceRating { get; set; }
    public string? Comments { get; set; }
    public bool IsAnonymous { get; set; }

    public Guid? SubmittedByEmployeeId { get; set; }
    public string? SubmittedByName { get; set; }
    public DateTime SubmittedAt { get; set; }
}

public class CreateOrientationFeedbackDto : CreateDtoBase
{
    [Required]
    public Guid EmployeeOrientationId { get; set; }

    [Range(1, 5)]
    public int? OverallRating { get; set; }

    [Range(1, 5)]
    public int? ContentRating { get; set; }

    [Range(1, 5)]
    public int? FacilitatorRating { get; set; }

    [Range(1, 5)]
    public int? RelevanceRating { get; set; }

    [MaxLength(2000)]
    public string? Comments { get; set; }

    public bool IsAnonymous { get; set; }

    // No SubmittedByEmployeeId. The submitter is the caller, stamped from the token by the
    // service; a payload-supplied id let anyone file feedback in somebody else's name.
}

#endregion

#region Orientation Certificate

public class OrientationCertificateDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid EmployeeOrientationId { get; set; }

    public Guid? EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public string? ProgramTitle { get; set; }

    public string CertificateNumber { get; set; } = string.Empty;
    public DateTime IssuedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public Guid? IssuedByEmployeeId { get; set; }
    public string? IssuedByName { get; set; }
    public string? FilePath { get; set; }
    public string? VerificationUrl { get; set; }

    public OrientationCertificateStatus Status { get; set; }
    public string StatusName => Status.ToString();

    public DateTime? RevokedAt { get; set; }
    public string? RevocationReason { get; set; }
}

/// <summary>Issue a completion certificate for an enrollment.</summary>
public class IssueOrientationCertificateDto
{
    [Required]
    public Guid EmployeeOrientationId { get; set; }

    [MaxLength(100)]
    public string? CertificateNumber { get; set; }   // auto-generated if not supplied

    public DateTime? ExpiresAt { get; set; }

    /// <summary>Ignored — the issuer is the signed-in HR officer (round 4, lane K-b). Kept so older callers still bind.</summary>
    public Guid? IssuedByEmployeeId { get; set; }

    /// <summary>Replace the enrolment's live certificate (marked Reissued). Without it, a second live one is refused.</summary>
    public bool Reissue { get; set; }
}

public class RevokeOrientationCertificateDto
{
    [Required]
    public Guid CertificateId { get; set; }

    [Required]
    [MaxLength(500)]
    public string RevocationReason { get; set; } = string.Empty;
}

#endregion

#region Orientation Notification

public class OrientationNotificationDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid? ProgramId { get; set; }
    public string? ProgramTitle { get; set; }
    public Guid? EmployeeOrientationId { get; set; }

    public Guid RecipientEmployeeId { get; set; }
    public string? RecipientEmployeeName { get; set; }

    public OrientationNotificationType Type { get; set; }
    public string TypeName => Type.ToString();

    public string Subject { get; set; } = string.Empty;
    public string? Message { get; set; }
    public string? NavigationUrl { get; set; }

    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime SentAt { get; set; }
}

public class CreateOrientationNotificationDto : CreateDtoBase
{
    public Guid? ProgramId { get; set; }
    public Guid? EmployeeOrientationId { get; set; }

    [Required]
    public Guid RecipientEmployeeId { get; set; }

    [Required]
    public OrientationNotificationType Type { get; set; }

    [Required]
    [MaxLength(300)]
    public string Subject { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string? Message { get; set; }

    [MaxLength(500)]
    public string? NavigationUrl { get; set; }
}

#endregion
