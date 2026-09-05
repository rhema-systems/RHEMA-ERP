using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.HR.Orientation;

/// <summary>
/// The one definition of "this enrollment occupies a seat". The capacity check, the session
/// mapper's EnrolledCount/AvailableSeats and the batched list counts all read it, so a withdrawn
/// participant cannot count against capacity on one screen and not on another.
/// </summary>
public static class OrientationEnrollmentStatuses
{
    public static readonly OrientationEnrollmentStatus[] Occupying =
    {
        OrientationEnrollmentStatus.PendingConfirmation,
        OrientationEnrollmentStatus.Confirmed,
        OrientationEnrollmentStatus.Active,
        OrientationEnrollmentStatus.Completed,
    };
}

// ===========================================================
//  SECTION 1 — CATALOG (Category, Program, Modules, Content)
// ===========================================================

/// <summary>
/// Top-level grouping for orientation programs (e.g. Onboarding, Compliance,
/// Product Launch). Supports nested categories via <see cref="ParentCategoryId"/>.
/// </summary>
public class OrientationCategory : TenantEntity
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>For sub-categories (e.g. Product Launch → Software Product Launch).</summary>
    public Guid? ParentCategoryId { get; set; }

    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation
    [ForeignKey(nameof(ParentCategoryId))]
    [InverseProperty(nameof(SubCategories))]
    public virtual OrientationCategory? ParentCategory { get; set; }

    [InverseProperty(nameof(ParentCategory))]
    public virtual ICollection<OrientationCategory> SubCategories { get; set; } = new List<OrientationCategory>();

    public virtual ICollection<OrientationProgram> Programs { get; set; } = new List<OrientationProgram>();
}

/// <summary>
/// The master definition of an orientation program — the reusable catalog entry,
/// not a single occurrence. Carries all configuration (audience, completion rules,
/// recurrence, certificate). One program can have many scheduled sessions.
/// </summary>
public class OrientationProgram : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string ProgramCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    /// <summary>High-level learning or communication objectives.</summary>
    [MaxLength(4000)]
    public string? Objectives { get; set; }

    public Guid? CategoryId { get; set; }

    // Classification
    public OrientationProgramType ProgramType { get; set; }
    public OrientationDeliveryMode DefaultDeliveryMode { get; set; }
    public OrientationProgramStatus Status { get; set; } = OrientationProgramStatus.Draft;
    public OrientationPriority Priority { get; set; }
    public OrientationAudienceScope AudienceScope { get; set; }

    /// <summary>Estimated minutes to complete.</summary>
    public int? EstimatedDurationMinutes { get; set; }

    // Completion rules
    public bool RequiresAssessment { get; set; }

    /// <summary>Minimum pass score (0–100) when an assessment is required.</summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal? PassingScorePercent { get; set; }

    public bool RequiresAcknowledgement { get; set; }

    /// <summary>Days after enrollment (or trigger) within which to complete.</summary>
    public int? CompletionDeadlineDays { get; set; }

    // Certificate
    public bool IsCertificateIssued { get; set; }
    public int? CertificateValidityMonths { get; set; }

    // Recurrence
    public bool IsRecurring { get; set; }
    public OrientationRecurrenceFrequency? RecurrenceFrequency { get; set; }

    public bool EnableReminders { get; set; } = true;

    /// <summary>Version label for auditing (e.g. "v3.1").</summary>
    [MaxLength(20)]
    public string? Version { get; set; }

    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }

    /// <summary>Tags for search/filtering, stored comma-separated or as JSON.</summary>
    [MaxLength(1000)]
    public string? Tags { get; set; }

    /// <summary>Primary HR owner / program manager.</summary>
    public Guid? OwnerEmployeeId { get; set; }

    /// <summary>Organization unit that owns this program.</summary>
    public Guid? OwnerOrganizationUnitId { get; set; }

    // Navigation
    [ForeignKey(nameof(CategoryId))]
    public virtual OrientationCategory? Category { get; set; }

    [ForeignKey(nameof(OwnerOrganizationUnitId))]
    public virtual OrganizationUnit? OwnerOrganizationUnit { get; set; }

    public virtual ICollection<OrientationModule> Modules { get; set; } = new List<OrientationModule>();
    public virtual ICollection<OrientationSession> Sessions { get; set; } = new List<OrientationSession>();
    public virtual ICollection<OrientationAudienceRule> AudienceRules { get; set; } = new List<OrientationAudienceRule>();

    [InverseProperty(nameof(OrientationPrerequisite.Program))]
    public virtual ICollection<OrientationPrerequisite> Prerequisites { get; set; } = new List<OrientationPrerequisite>();

    public virtual ICollection<OrientationAssessmentQuestion> AssessmentQuestions { get; set; } = new List<OrientationAssessmentQuestion>();
    public virtual ICollection<EmployeeOrientation> Enrollments { get; set; } = new List<EmployeeOrientation>();
}

/// <summary>
/// A logical curriculum unit within a program. A program is composed of one or
/// more ordered modules (e.g. "Introduction", "Compliance Policy", "Quiz").
/// </summary>
public class OrientationModule : TenantEntity
{
    public Guid ProgramId { get; set; }

    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    public int SequenceOrder { get; set; }

    public OrientationModuleType ModuleType { get; set; }

    public int? EstimatedDurationMinutes { get; set; }

    /// <summary>Whether this module must be completed before the next one unlocks.</summary>
    public bool IsSequentiallyRequired { get; set; } = true;

    public bool IsOptional { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation
    [ForeignKey(nameof(ProgramId))]
    public virtual OrientationProgram Program { get; set; } = null!;

    public virtual ICollection<OrientationContentItem> ContentItems { get; set; } = new List<OrientationContentItem>();
}

/// <summary>
/// A single piece of content attached to a module — the leaf-level deliverable
/// (video, document, slide deck, external link, etc.).
/// </summary>
public class OrientationContentItem : TenantEntity
{
    public Guid ModuleId { get; set; }

    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    public OrientationContentType ContentType { get; set; }

    /// <summary>
    /// Relative path, CDN URL, or external URL depending on <see cref="ContentType"/>.
    /// </summary>
    [MaxLength(1000)]
    public string? ResourceUrl { get; set; }

    /// <summary>Original file name for document/video uploads.</summary>
    [MaxLength(500)]
    public string? OriginalFileName { get; set; }

    public long? FileSizeBytes { get; set; }

    /// <summary>Duration in seconds for video/audio content.</summary>
    public int? MediaDurationSeconds { get; set; }

    public int SequenceOrder { get; set; }

    public bool IsRequired { get; set; } = true;

    public bool IsActive { get; set; } = true;

    // Navigation
    [ForeignKey(nameof(ModuleId))]
    public virtual OrientationModule Module { get; set; } = null!;

    public virtual ICollection<OrientationContentProgress> ContentProgress { get; set; } = new List<OrientationContentProgress>();
}

/// <summary>
/// A prerequisite program that must be completed before enrolling in another program.
/// </summary>
public class OrientationPrerequisite : TenantEntity
{
    /// <summary>The program that has the prerequisite.</summary>
    public Guid ProgramId { get; set; }

    /// <summary>The program that must be completed first.</summary>
    public Guid PrerequisiteProgramId { get; set; }

    /// <summary>Whether this prerequisite is mandatory or advisory.</summary>
    public bool IsMandatory { get; set; } = true;

    [MaxLength(500)]
    public string? Notes { get; set; }

    // Navigation
    [ForeignKey(nameof(ProgramId))]
    [InverseProperty(nameof(OrientationProgram.Prerequisites))]
    public virtual OrientationProgram Program { get; set; } = null!;

    [ForeignKey(nameof(PrerequisiteProgramId))]
    public virtual OrientationProgram PrerequisiteProgram { get; set; } = null!;
}

/// <summary>
/// A rule defining who should be enrolled in a program and what triggers it
/// (e.g. all new hires in a department, on hire date + N days).
/// </summary>
public class OrientationAudienceRule : TenantEntity
{
    public Guid ProgramId { get; set; }

    [Required]
    [MaxLength(200)]
    public string RuleName { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public OrientationAudienceScope TargetType { get; set; }

    /// <summary>
    /// Id of the target entity (OrganizationUnitId, JobGradeId, LocationId, EmployeeId, …)
    /// referenced by <see cref="TargetType"/>. Null = applies to all employees.
    /// </summary>
    public Guid? TargetEntityId { get; set; }

    public OrientationEnrollmentTrigger Trigger { get; set; }

    /// <summary>How many days after the trigger the enrollment should be created.</summary>
    public int EnrollmentDelayDays { get; set; }

    /// <summary>True = include this audience, false = exclude it.</summary>
    public bool IsInclusive { get; set; } = true;

    public bool IsActive { get; set; } = true;

    // Navigation
    [ForeignKey(nameof(ProgramId))]
    public virtual OrientationProgram Program { get; set; } = null!;
}

// ===========================================================
//  SECTION 2 — DELIVERY (Sessions, Facilitators, Attendance)
// ===========================================================

/// <summary>
/// A scheduled or on-demand delivery instance of a program. Instructor-led
/// sessions have a date/time/venue; self-paced sessions may be open-ended.
/// </summary>
public class OrientationSession : TenantEntity
{
    public Guid ProgramId { get; set; }

    [Required]
    [MaxLength(50)]
    public string SessionCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    public OrientationDeliveryMode DeliveryMode { get; set; }
    public OrientationSessionStatus Status { get; set; } = OrientationSessionStatus.Draft;

    public DateTime? ScheduledStartAt { get; set; }
    public DateTime? ScheduledEndAt { get; set; }
    public DateTime? ActualStartAt { get; set; }
    public DateTime? ActualEndAt { get; set; }

    /// <summary>For in-person sessions: physical venue (building/room, address).</summary>
    [MaxLength(500)]
    public string? VenueDescription { get; set; }

    /// <summary>For virtual sessions: meeting link (Zoom, Teams, Meet, etc.).</summary>
    [MaxLength(500)]
    public string? VirtualMeetingUrl { get; set; }

    /// <summary>Maximum participants allowed. Null means unlimited.</summary>
    public int? MaxParticipants { get; set; }

    /// <summary>Enrollment deadline. Null means open until the session starts.</summary>
    public DateTime? EnrollmentDeadlineAt { get; set; }

    /// <summary>Allow participants on a waiting list when the session is full.</summary>
    public bool AllowWaitlist { get; set; }

    /// <summary>Whether participants can self-enroll or require HR approval.</summary>
    public bool RequiresApproval { get; set; }

    [MaxLength(500)]
    public string? RecordingUrl { get; set; }

    /// <summary>Instructions sent to participants on enrollment.</summary>
    [MaxLength(4000)]
    public string? ParticipantInstructions { get; set; }

    // Navigation
    [ForeignKey(nameof(ProgramId))]
    public virtual OrientationProgram Program { get; set; } = null!;

    public virtual ICollection<OrientationSessionFacilitator> Facilitators { get; set; } = new List<OrientationSessionFacilitator>();
    public virtual ICollection<EmployeeOrientation> Enrollments { get; set; } = new List<EmployeeOrientation>();
}

/// <summary>
/// Associates a facilitator (trainer, SME, speaker) with a session. A session can
/// have multiple facilitators; internal facilitators reference an employee, external
/// ones are captured by name/email.
/// </summary>
public class OrientationSessionFacilitator : TenantEntity
{
    public Guid SessionId { get; set; }

    /// <summary>Employee acting as facilitator. Null if external.</summary>
    public Guid? EmployeeId { get; set; }

    [MaxLength(200)]
    public string? ExternalFacilitatorName { get; set; }

    [MaxLength(200)]
    public string? ExternalFacilitatorEmail { get; set; }

    [MaxLength(200)]
    public string? ExternalFacilitatorOrganization { get; set; }

    public OrientationFacilitatorRole Role { get; set; }

    public bool HasConfirmed { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    // Navigation
    [ForeignKey(nameof(SessionId))]
    public virtual OrientationSession Session { get; set; } = null!;
}

/// <summary>
/// Attendance record for an instructor-led/virtual session. For multi-day sessions,
/// one record per day/segment.
/// </summary>
public class OrientationAttendanceRecord : TenantEntity
{
    public Guid EnrollmentId { get; set; }

    /// <summary>Which day/segment this record covers (1, 2, 3…).</summary>
    public int SessionDay { get; set; } = 1;

    public OrientationAttendanceStatus AttendanceStatus { get; set; }

    public DateTime? CheckInAt { get; set; }
    public DateTime? CheckOutAt { get; set; }

    /// <summary>Actual attended duration in minutes (calculated or overridden).</summary>
    public int? AttendedMinutes { get; set; }

    public bool MarkedLate { get; set; }

    [MaxLength(500)]
    public string? AbsenceReason { get; set; }

    /// <summary>Employee who recorded/verified this attendance.</summary>
    public Guid? MarkedByEmployeeId { get; set; }

    // Navigation
    [ForeignKey(nameof(EnrollmentId))]
    public virtual EmployeeOrientation Enrollment { get; set; } = null!;
}

// ===========================================================
//  SECTION 3 — ENROLLMENT & PROGRESS
// ===========================================================

/// <summary>
/// The primary tracking unit: one employee's enrollment in a program (and optionally
/// a specific session). Holds overall progress, completion, score, and certificate
/// summary for that participant.
/// </summary>
public class EmployeeOrientation : TenantEntity
{
    public Guid ProgramId { get; set; }

    /// <summary>Optional specific session for instructor-led delivery. Null for self-paced.</summary>
    public Guid? SessionId { get; set; }

    public Guid EmployeeId { get; set; }

    public OrientationEnrollmentStatus EnrollmentStatus { get; set; } = OrientationEnrollmentStatus.Confirmed;
    public OrientationEnrollmentSource EnrollmentSource { get; set; }

    public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;
    public Guid? EnrolledByEmployeeId { get; set; }

    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? LastActivityAt { get; set; }

    public int ProgressPercentage { get; set; }

    public OrientationCompletionStatus CompletionStatus { get; set; } = OrientationCompletionStatus.NotStarted;

    /// <summary>Final assessment score (0–100) once graded.</summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal? FinalScore { get; set; }

    public int AttemptCount { get; set; }
    public bool IsPassed { get; set; }

    // Acknowledgement summary
    public bool AcknowledgementSigned { get; set; }

    // Certificate summary
    public bool CertificateIssued { get; set; }

    [MaxLength(100)]
    public string? CertificateSerialNumber { get; set; }

    public DateTime? CertificateExpiresAt { get; set; }

    // Waitlist / recurrence
    public int? WaitlistPosition { get; set; }
    public DateTime? NextDueDate { get; set; }

    [MaxLength(1000)]
    public string? WithdrawalReason { get; set; }

    // Navigation
    [ForeignKey(nameof(ProgramId))]
    public virtual OrientationProgram Program { get; set; } = null!;

    [ForeignKey(nameof(SessionId))]
    public virtual OrientationSession? Session { get; set; }

    public virtual ICollection<OrientationContentProgress> ContentProgress { get; set; } = new List<OrientationContentProgress>();
    public virtual ICollection<OrientationAssessmentResponse> AssessmentResponses { get; set; } = new List<OrientationAssessmentResponse>();
    public virtual ICollection<OrientationAcknowledgement> Acknowledgements { get; set; } = new List<OrientationAcknowledgement>();
    public virtual ICollection<OrientationFeedback> Feedbacks { get; set; } = new List<OrientationFeedback>();
    public virtual ICollection<OrientationAttendanceRecord> AttendanceRecords { get; set; } = new List<OrientationAttendanceRecord>();
    public virtual ICollection<OrientationCertificate> Certificates { get; set; } = new List<OrientationCertificate>();
}

/// <summary>
/// Granular tracking of a participant's interaction with a single content item.
/// Module-level progress is derived from these rows.
/// </summary>
public class OrientationContentProgress : TenantEntity
{
    public Guid EmployeeOrientationId { get; set; }
    public Guid ContentItemId { get; set; }

    public OrientationContentProgressStatus Status { get; set; } = OrientationContentProgressStatus.NotStarted;

    public DateTime? FirstAccessedAt { get; set; }
    public DateTime? LastAccessedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public int TotalTimeSpentSeconds { get; set; }
    public int AccessCount { get; set; }

    public bool IsAcknowledged { get; set; }

    // Navigation
    [ForeignKey(nameof(EmployeeOrientationId))]
    public virtual EmployeeOrientation EmployeeOrientation { get; set; } = null!;

    [ForeignKey(nameof(ContentItemId))]
    public virtual OrientationContentItem ContentItem { get; set; } = null!;
}

// ===========================================================
//  SECTION 4 — ASSESSMENT (light Q&A)
// ===========================================================

/// <summary>
/// A single question in a program's knowledge-check assessment.
/// </summary>
public class OrientationAssessmentQuestion : TenantEntity
{
    public Guid ProgramId { get; set; }

    [Required]
    [MaxLength(2000)]
    public string QuestionText { get; set; } = string.Empty;

    public OrientationQuestionType QuestionType { get; set; }

    /// <summary>Point value of this question.</summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal Points { get; set; } = 1;

    /// <summary>Explanation shown after the question is answered.</summary>
    [MaxLength(2000)]
    public string? Explanation { get; set; }

    public int SequenceOrder { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation
    [ForeignKey(nameof(ProgramId))]
    public virtual OrientationProgram Program { get; set; } = null!;

    public virtual ICollection<OrientationAssessmentOption> Options { get; set; } = new List<OrientationAssessmentOption>();
    public virtual ICollection<OrientationAssessmentResponse> Responses { get; set; } = new List<OrientationAssessmentResponse>();
}

/// <summary>
/// An answer option for single-choice, multi-select, or true/false questions.
/// </summary>
public class OrientationAssessmentOption : TenantEntity
{
    public Guid QuestionId { get; set; }

    [Required]
    [MaxLength(1000)]
    public string OptionText { get; set; } = string.Empty;

    public bool IsCorrect { get; set; }

    public int DisplayOrder { get; set; }

    // Navigation
    [ForeignKey(nameof(QuestionId))]
    public virtual OrientationAssessmentQuestion Question { get; set; } = null!;
}

/// <summary>
/// A participant's answer to a single question for an enrollment. One row per
/// question answered; attempt count and final score are rolled up onto
/// <see cref="EmployeeOrientation"/>.
/// </summary>
public class OrientationAssessmentResponse : TenantEntity
{
    public Guid EmployeeOrientationId { get; set; }
    public Guid QuestionId { get; set; }

    /// <summary>Selected option for option-based questions. Null for free-text.</summary>
    public Guid? SelectedOptionId { get; set; }

    /// <summary>Free-text answer. Null for option-based questions.</summary>
    [MaxLength(4000)]
    public string? FreeTextAnswer { get; set; }

    public bool IsCorrect { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal? PointsAwarded { get; set; }

    public DateTime AnsweredAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey(nameof(EmployeeOrientationId))]
    public virtual EmployeeOrientation EmployeeOrientation { get; set; } = null!;

    [ForeignKey(nameof(QuestionId))]
    public virtual OrientationAssessmentQuestion Question { get; set; } = null!;

    [ForeignKey(nameof(SelectedOptionId))]
    public virtual OrientationAssessmentOption? SelectedOption { get; set; }
}

// ===========================================================
//  SECTION 5 — COMPLETION ARTIFACTS
// ===========================================================

/// <summary>
/// A formal acknowledgement a participant signs to confirm they read and understood
/// the orientation content. The exact text is stored immutably at signing time.
/// </summary>
public class OrientationAcknowledgement : TenantEntity
{
    public Guid EmployeeOrientationId { get; set; }

    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(8000)]
    public string AcknowledgementText { get; set; } = string.Empty;

    public OrientationAcknowledgementStatus Status { get; set; } = OrientationAcknowledgementStatus.Pending;

    public DateTime? PresentedAt { get; set; }
    public DateTime? SignedAt { get; set; }
    public DateTime? DeclinedAt { get; set; }

    [MaxLength(500)]
    public string? DeclineReason { get; set; }

    /// <summary>IP address at the time of signing.</summary>
    [MaxLength(50)]
    public string? SignatureIpAddress { get; set; }

    /// <summary>Hash of (EmployeeId + text + SignedAt) for tamper detection.</summary>
    [MaxLength(256)]
    public string? SignatureHash { get; set; }

    // Navigation
    [ForeignKey(nameof(EmployeeOrientationId))]
    public virtual EmployeeOrientation EmployeeOrientation { get; set; } = null!;
}

/// <summary>
/// A participant's feedback on an orientation (structured ratings + comments).
/// </summary>
public class OrientationFeedback : TenantEntity
{
    public Guid EmployeeOrientationId { get; set; }

    /// <summary>Overall satisfaction rating (1–5).</summary>
    public int? OverallRating { get; set; }

    /// <summary>Quality of the content (1–5).</summary>
    public int? ContentRating { get; set; }

    /// <summary>Facilitator effectiveness (1–5), for instructor-led delivery.</summary>
    public int? FacilitatorRating { get; set; }

    /// <summary>Relevance to the participant's role (1–5).</summary>
    public int? RelevanceRating { get; set; }

    [MaxLength(2000)]
    public string? Comments { get; set; }

    public bool IsAnonymous { get; set; }

    public Guid? SubmittedByEmployeeId { get; set; }

    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey(nameof(EmployeeOrientationId))]
    public virtual EmployeeOrientation EmployeeOrientation { get; set; } = null!;
}

/// <summary>
/// A completion certificate issued to a participant. Immutable audit record.
/// </summary>
public class OrientationCertificate : TenantEntity
{
    public Guid EmployeeOrientationId { get; set; }

    [Required]
    [MaxLength(100)]
    public string CertificateNumber { get; set; } = string.Empty;

    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }

    public Guid? IssuedByEmployeeId { get; set; }

    /// <summary>Rendered certificate stored as a relative path.</summary>
    [MaxLength(500)]
    public string? FilePath { get; set; }

    /// <summary>Externally accessible / verification URL.</summary>
    [MaxLength(500)]
    public string? VerificationUrl { get; set; }

    public OrientationCertificateStatus Status { get; set; } = OrientationCertificateStatus.Active;

    public DateTime? RevokedAt { get; set; }

    [MaxLength(500)]
    public string? RevocationReason { get; set; }

    // Navigation
    [ForeignKey(nameof(EmployeeOrientationId))]
    public virtual EmployeeOrientation EmployeeOrientation { get; set; } = null!;
}

/// <summary>
/// A lifecycle notification sent for an orientation (enrollment, reminder, overdue,
/// completion, certificate). Instance record — not a template.
/// </summary>
public class OrientationNotification : TenantEntity
{
    public Guid? ProgramId { get; set; }
    public Guid? EmployeeOrientationId { get; set; }

    public Guid RecipientEmployeeId { get; set; }

    public OrientationNotificationType Type { get; set; }

    [Required]
    [MaxLength(300)]
    public string Subject { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string? Message { get; set; }

    [MaxLength(500)]
    public string? NavigationUrl { get; set; }

    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }

    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey(nameof(ProgramId))]
    public virtual OrientationProgram? Program { get; set; }

    [ForeignKey(nameof(EmployeeOrientationId))]
    public virtual EmployeeOrientation? EmployeeOrientation { get; set; }
}
