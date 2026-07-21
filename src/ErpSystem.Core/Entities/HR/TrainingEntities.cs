using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.HR.Training;

/// <summary>
/// Immutable audit-trail row capturing a single status transition on a training entity
/// (nomination / schedule / completion). Written automatically by an EF interceptor whenever a
/// watched entity's <c>Status</c> changes, so no code path can bypass it — for compliance evidencing.
/// </summary>
public class TrainingStatusHistory : TenantEntity
{
    /// <summary>The audited entity's type name, e.g. "TrainingNomination", "TrainingSchedule", "TrainingCompletion".</summary>
    [Required]
    [MaxLength(100)]
    public string EntityType { get; set; } = string.Empty;

    public Guid EntityId { get; set; }

    /// <summary>Human-friendly reference for display without a join (e.g. NominationNumber / ScheduleNumber).</summary>
    [MaxLength(100)]
    public string? EntityReference { get; set; }

    /// <summary>Numeric status before the change; null for the initial "created" entry.</summary>
    public int? FromStatus { get; set; }

    [MaxLength(100)]
    public string? FromStatusName { get; set; }

    public int ToStatus { get; set; }

    [MaxLength(100)]
    public string ToStatusName { get; set; } = string.Empty;

    /// <summary>Acting employee (null when the change was made by a background/system process).</summary>
    public Guid? ChangedByEmployeeId { get; set; }

    /// <summary>Actor display name (username, or "System" for background changes).</summary>
    [MaxLength(200)]
    public string? ChangedByName { get; set; }

    public DateTime ChangedAt { get; set; }

    /// <summary>Best-effort reason captured for known transitions (e.g. rejection / cancellation reason).</summary>
    [MaxLength(1000)]
    public string? Reason { get; set; }
}

/// <summary>
/// External training provider or vendor — the organisation you contract with
/// </summary>
public class TrainingVendor : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string VendorCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    
    public TrainingVendorType VendorType { get; set; }

    // Preferred / status
    public bool IsActive { get; set; } = true;
    public bool IsPreferred { get; set; }
    public DateTime? PreferredSince { get; set; }
    public bool IsBlacklisted { get; set; }
    public DateTime? BlacklistedDate { get; set; }
    
    [MaxLength(1000)]
    public string? BlacklistReason { get; set; }

    // Accreditation
    [MaxLength(200)]
    public string? AccreditationBody { get; set; }    // e.g. "ACCA", "HRCI", "ATD"
    
    [MaxLength(100)]
    public string? AccreditationNumber { get; set; }
    
    public VendorAccreditationStatus? AccreditationStatus { get; set; }
    
    public DateTime? AccreditationExpiryDate { get; set; }

    // Contact
    [MaxLength(200)]
    public string? PrimaryContactName { get; set; }
    
    [MaxLength(256)]
    [EmailAddress]
    public string? PrimaryContactEmail { get; set; }
    
    [MaxLength(20)]
    public string? PrimaryContactPhone { get; set; }
    
    [MaxLength(500)]
    public string? Website { get; set; }
    
    [MaxLength(500)]
    public string? Address { get; set; }

    // Commercial
    [MaxLength(3)]
    public string Currency { get; set; } = "GHS";
    
    public decimal? DefaultDailyRate { get; set; }
    
    [MaxLength(100)]
    public string? ContractReference { get; set; }

    public DateTime? ContractStartDate { get; set; }
    public DateTime? ContractEndDate { get; set; }

    /// <summary>Stored path/URL of the uploaded contract document (see FileUpload).</summary>
    [MaxLength(1000)]
    public string? ContractDocumentPath { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    // Relations
    public virtual ICollection<TrainerProfile> Trainers { get; set; } = new List<TrainerProfile>();
}

/// <summary>
/// Profile for internal trainers — extends the Employee record
/// </summary>
public class TrainerProfile : TenantEntity
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    // Internal trainer — link to employee record
    public Guid? EmployeeId { get; set; }
    
    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee? Employee { get; set; }

    // External trainer — link to vendor/provider organisation
    public Guid? VendorId { get; set; }
    
    [ForeignKey(nameof(VendorId))]
    public virtual TrainingVendor? Vendor { get; set; }

    [MaxLength(2000)]
    public string? Bio { get; set; }
    
    [MaxLength(200)]
    public string? Contact { get; set; }   // Individual direct contact (cell / personal email)
    
    [MaxLength(1000)]
    public string? ExpertiseAreas { get; set; }

    public int TotalTrainingHoursDelivered { get; set; }
    public int TotalSessionsDelivered { get; set; }

    // Aggregate rating from TrainingEvaluations
    [Range(0, 5)]
    public decimal? AverageRating { get; set; }
    
    public int TotalRatingsCount { get; set; }

    public bool IsActive { get; set; } = true;

    // Relations
    public virtual ICollection<TrainerSkill> TrainerSkills { get; set; } = new List<TrainerSkill>();
    public virtual ICollection<TrainerAvailability> Availability { get; set; } = new List<TrainerAvailability>();
}

/// <summary>
/// Skills a trainer can teach
/// </summary>
public class TrainerSkill : TenantEntity
{
    public Guid TrainerProfileId { get; set; }
    
    [ForeignKey(nameof(TrainerProfileId))]
    public virtual TrainerProfile TrainerProfile { get; set; } = null!;

    public Guid SkillId { get; set; }
    
    [ForeignKey(nameof(SkillId))]
    public virtual Skill Skill { get; set; } = null!;

    public ProficiencyLevel TrainerProficiency { get; set; }
    
    public bool IsCertifiedToTrain { get; set; }

    [MaxLength(100)]
    public string? CertificateNumber { get; set; }

    [MaxLength(200)]
    public string? CertificateName { get; set; }

    /// <summary>Stored path/URL of the uploaded certificate document (see FileUpload).</summary>
    [MaxLength(1000)]
    public string? CertificateFilePath { get; set; }
}

/// <summary>
/// Trainer availability windows for scheduling
/// </summary>
public class TrainerAvailability : TenantEntity
{
    public Guid TrainerProfileId { get; set; }
    
    [ForeignKey(nameof(TrainerProfileId))]
    public virtual TrainerProfile TrainerProfile { get; set; } = null!;

    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }

    public bool IsAvailable { get; set; } // false = blocked/unavailable

    /// <summary>When blocked, indicates whether the trainer is engaged internally or externally.</summary>
    public TrainerEngagementType? EngagementType { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

/// <summary>
/// User-configurable training category (replaces the fixed TrainingCategory enum for training programs).
/// Set up per tenant with an optional colour used for calendar/badge differentiation.
/// </summary>
public class TrainingCategoryOption : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Hex colour (e.g. "#0EA5E9") for schedule-calendar / badge differentiation.</summary>
    [MaxLength(9)]
    public string? ColorHex { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    // Relations
    public virtual ICollection<TrainingProgram> Programs { get; set; } = new List<TrainingProgram>();
}

/// <summary>
/// Optional lightweight grouping / curriculum layer above <see cref="TrainingProgram"/>
/// (e.g. "Leadership Track", "Onboarding Curriculum", "Safety Essentials"). Programs stay
/// the course of record — a group just clusters related programs for browsing/reporting.
/// Set up per tenant with an optional colour for badge differentiation.
/// </summary>
public class TrainingProgramGroup : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Hex colour (e.g. "#0EA5E9") for badge / grouping differentiation.</summary>
    [MaxLength(9)]
    public string? ColorHex { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    // Relations
    public virtual ICollection<TrainingProgram> Programs { get; set; } = new List<TrainingProgram>();
}

/// <summary>
/// Training program/course offered by the organisation
/// </summary>
public class TrainingProgram : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string ProgramCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string ProgramName { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string Description { get; set; } = string.Empty;

    // Classification — user-configurable category (see TrainingCategoryOption)
    public Guid? CategoryOptionId { get; set; }

    [ForeignKey(nameof(CategoryOptionId))]
    public virtual TrainingCategoryOption? CategoryOption { get; set; }

    // Optional grouping / curriculum cluster (see TrainingProgramGroup)
    public Guid? ProgramGroupId { get; set; }

    [ForeignKey(nameof(ProgramGroupId))]
    public virtual TrainingProgramGroup? ProgramGroup { get; set; }

    public TrainingType Type { get; set; }

    /// <summary>Provenance of the training (Internal / External / Online), independent of its format Type.</summary>
    public TrainingSource Source { get; set; } = TrainingSource.Internal;

    public TrainingLevel Level { get; set; }

    // Details
    public int DurationDays { get; set; }
    public int DurationHours { get; set; }

    [MaxLength(2000)]
    public string? Prerequisites { get; set; }
    
    [MaxLength(4000)]
    public string? LearningObjectives { get; set; }

    // Cost
    public decimal CostPerParticipant { get; set; }
    
    [MaxLength(3)]
    public string Currency { get; set; } = "GHS";

    public bool IncludesAccommodation { get; set; }
    public bool IncludesMeals { get; set; }
    public bool IncludesTransport { get; set; }

    // Certification
    public bool ProvidesCertificate { get; set; }
    
    [MaxLength(200)]
    public string? CertificateName { get; set; }
    
    public int? CertificateValidityMonths { get; set; }

    // Capacity
    public int? MinParticipants { get; set; }
    public int? MaxParticipants { get; set; }

    // Status
    public bool IsActive { get; set; }
    public bool RequiresApproval { get; set; }

    // Service bond (binding service-obligation agreement) — optional template defined at the program level.
    // When set, a TrainingServiceBond is instantiated per nomination (auto on approval).
    public bool RequiresServiceBond { get; set; }

    /// <summary>Default service-obligation length in months applied to bonds for this program.</summary>
    public int? ServiceBondMonths { get; set; }

    /// <summary>Default terms text presented to the employee for acceptance.</summary>
    [MaxLength(4000)]
    public string? ServiceBondTerms { get; set; }

    // Relations
    public virtual ICollection<TrainingSchedule> Schedules { get; set; } = new List<TrainingSchedule>();
    public virtual ICollection<TrainingMaterial> Materials { get; set; } = new List<TrainingMaterial>();
    public virtual ICollection<TrainingProgramCompetency> Competencies { get; set; } = new List<TrainingProgramCompetency>();
    public virtual ICollection<TrainingProgramSkill> Skills { get; set; } = new List<TrainingProgramSkill>();
    public virtual ICollection<LearningPathProgram> LearningPathPrograms { get; set; } = new List<LearningPathProgram>();
}

/// <summary>
/// Training materials/resources attached to a program
/// </summary>
public class TrainingMaterial : TenantEntity
{
    public Guid ProgramId { get; set; }
    
    [ForeignKey(nameof(ProgramId))]
    public virtual TrainingProgram Program { get; set; } = null!;

    [Required]
    [MaxLength(200)]
    public string MaterialName { get; set; } = string.Empty;
    
    public MaterialType Type { get; set; } // Handbook, Slides, Video, Document

    [MaxLength(1000)]
    public string? FilePath { get; set; }
    
    [MaxLength(1000)]
    public string? ExternalUrl { get; set; }

    public bool IsPublic { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime UploadDate { get; set; }
}

/// <summary>
/// Scheduled instance of a training program
/// </summary>
public class TrainingSchedule : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string ScheduleNumber { get; set; } = string.Empty;

    /// <summary>Optimistic concurrency token — guards concurrent approve/complete/cancel edits.</summary>
    [Timestamp]
    public byte[]? RowVersion { get; set; }

    public Guid ProgramId { get; set; }
    
    [ForeignKey(nameof(ProgramId))]
    public virtual TrainingProgram Program { get; set; } = null!;

    // Schedule Details
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    [MaxLength(200)]
    public string? Venue { get; set; }
    
    [MaxLength(500)]
    public string? VenueAddress { get; set; }
    
    [MaxLength(1000)]
    public string? OnlineLink { get; set; }

    // Timing
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }

    // Trainer — at least one of these must be set
    public Guid? TrainerProfileId { get; set; }   // Specific individual (internal or external)
    
    [ForeignKey(nameof(TrainerProfileId))]
    public virtual TrainerProfile? TrainerProfile { get; set; }
    
    public Guid? VendorId { get; set; }           // Vendor/provider org (set when specific trainer not yet assigned)
    
    [ForeignKey(nameof(VendorId))]
    public virtual TrainingVendor? Vendor { get; set; }

    // Capacity
    public int MaxParticipants { get; set; }

    /// <summary>Scheduling priority — a higher-priority training can override a trainer's lower-priority one.</summary>
    public TrainingPriority Priority { get; set; } = TrainingPriority.Medium;

    // Registration
    public DateTime RegistrationOpenDate { get; set; }
    public DateTime RegistrationCloseDate { get; set; }

    // Status
    public ScheduleStatus Status { get; set; }

    // Budget
    public decimal ActualCost { get; set; }
    
    [MaxLength(1000)]
    public string? BudgetNotes { get; set; }
    public Guid? TrainingBudgetId { get; set; }
    
    [ForeignKey(nameof(TrainingBudgetId))]
    public virtual TrainingBudget? TrainingBudget { get; set; }

    // Approval
    public Guid? ApprovedById { get; set; }
    
    [ForeignKey(nameof(ApprovedById))]
    public virtual Employee? ApprovedBy { get; set; }
    
    public DateTime? ApprovalDate { get; set; }

    // Completion
    public DateTime? CompletionDate { get; set; }
    
    [MaxLength(2000)]
    public string? CompletionNotes { get; set; }

    // Cancellation
    [MaxLength(1000)]
    public string? CancellationReason { get; set; }
    
    public DateTime? CancelledDate { get; set; }
    public Guid? CancelledById { get; set; }

    // Relations
    public virtual ICollection<TrainingSession> Sessions { get; set; } = new List<TrainingSession>();
    public virtual ICollection<TrainingNomination> Nominations { get; set; } = new List<TrainingNomination>();
    public virtual ICollection<TrainingAttendance> Attendance { get; set; } = new List<TrainingAttendance>();
    public virtual ICollection<TrainingFeedback> Feedbacks { get; set; } = new List<TrainingFeedback>();
    public virtual ICollection<TrainingFollowUpAssessment> FollowUpAssessments { get; set; } = new List<TrainingFollowUpAssessment>();
    public virtual ICollection<TrainingWaitlist> Waitlist { get; set; } = new List<TrainingWaitlist>();
}

public class TrainingSession : TenantEntity
{
    public Guid ScheduleId { get; set; }
    
    [ForeignKey(nameof(ScheduleId))]
    public virtual TrainingSchedule Schedule { get; set; } = null!;

    [Required]
    [MaxLength(200)]
    public string Topic { get; set; } = string.Empty;

    public DateTime Date { get; set; }

    /// <summary>Optional session start/end time — supports building a per-schedule timetable.</summary>
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }

    [MaxLength(2000)]
    public string? Description { get; set; }
}

public class TrainingProgramCompetency : TenantEntity
{
    public Guid ProgramId { get; set; }
    
    [ForeignKey(nameof(ProgramId))]
    public virtual TrainingProgram Program { get; set; } = null!;

    public Guid CompetencyId { get; set; }
    
    [ForeignKey(nameof(CompetencyId))]
    public virtual Competency Competency { get; set; } = null!;

    [Range(1, 5)]
    public int TargetLevel { get; set; }
}

/// <summary>
/// Link between Training Program and Skills gained
/// </summary>
public class TrainingProgramSkill : TenantEntity
{
    public Guid ProgramId { get; set; }
    
    [ForeignKey(nameof(ProgramId))]
    public virtual TrainingProgram Program { get; set; } = null!;

    public Guid SkillId { get; set; }
    
    [ForeignKey(nameof(SkillId))]
    public virtual Skill Skill { get; set; } = null!;

    public ProficiencyLevel TargetProficiency { get; set; }
}

/// <summary>
/// Employee nomination/application for training
/// </summary>
public class TrainingNomination : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string NominationNumber { get; set; } = string.Empty;

    public Guid ScheduleId { get; set; }
    
    [ForeignKey(nameof(ScheduleId))]
    public virtual TrainingSchedule Schedule { get; set; } = null!;

    public Guid EmployeeId { get; set; }
    
    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    // Nomination Details
    public NominationType Type { get; set; } // Self, Supervisor, HR, Management
    
    public Guid? NominatedById { get; set; }
    
    [ForeignKey(nameof(NominatedById))]
    public virtual Employee? NominatedBy { get; set; }

    public DateTime NominationDate { get; set; }
    
    [MaxLength(2000)]
    public string? Justification { get; set; }

    // Link to the needs assessment that triggered this
    public Guid? TrainingNeedsAssessmentId { get; set; }
    
    [ForeignKey(nameof(TrainingNeedsAssessmentId))]
    public virtual TrainingNeedsAssessment? TrainingNeedsAssessment { get; set; }

    public NominationStatus Status { get; set; }

    public Guid? SupervisorApprovedById { get; set; }
    
    [ForeignKey(nameof(SupervisorApprovedById))]
    public virtual Employee? SupervisorApprovedBy { get; set; }
    
    public DateTime? SupervisorApprovalDate { get; set; }
    
    [MaxLength(2000)]
    public string? SupervisorComments { get; set; }

    public Guid? HrApprovedById { get; set; }
    
    [ForeignKey(nameof(HrApprovedById))]
    public virtual Employee? HrApprovedBy { get; set; }
    
    public DateTime? HrApprovalDate { get; set; }
    
    [MaxLength(2000)]
    public string? HrComments { get; set; }

    // Rejection
    public DateTime? RejectedDate { get; set; }
    
    [MaxLength(1000)]
    public string? RejectionReason { get; set; }

    // Cost
    public decimal? ActualCost { get; set; }
    public bool EmployeeContributed { get; set; }
    public decimal? EmployeeContribution { get; set; }

    public virtual TrainingCompletion? CompletionRecord { get; set; }

    /// <summary>Optional binding service-obligation agreement attached to this nomination.</summary>
    public virtual TrainingServiceBond? ServiceBond { get; set; }

    /// <summary>Active configurable-workflow instance driving this nomination's approval (null = legacy Supervisor→HR chain).</summary>
    public Guid? WorkflowInstanceId { get; set; }

    /// <summary>Optimistic concurrency token — prevents concurrent approve/reject from clobbering each other.</summary>
    [Timestamp]
    public byte[]? RowVersion { get; set; }
}

/// <summary>
/// A binding service-obligation agreement tied to a training nomination: the employee agrees to
/// remain in service for a defined period after the (typically company-funded) training, and to
/// repay a pro-rated portion of the bond amount if they exit early. Terms are templated on the
/// <see cref="TrainingProgram"/> and instantiated per nomination.
/// </summary>
public class TrainingServiceBond : TenantEntity
{
    public Guid NominationId { get; set; }

    [ForeignKey(nameof(NominationId))]
    public virtual TrainingNomination Nomination { get; set; } = null!;

    // Denormalised for straightforward per-employee / per-program querying (e.g. offboarding checks).
    public Guid EmployeeId { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    public Guid ProgramId { get; set; }

    [ForeignKey(nameof(ProgramId))]
    public virtual TrainingProgram Program { get; set; } = null!;

    // ── Terms ────────────────────────────────────────────────────────────────
    /// <summary>Service-obligation length in months.</summary>
    public int BondDurationMonths { get; set; }

    /// <summary>Total bond value; pre-filled from the training cost, HR-editable. Repayment pro-rates this.</summary>
    public decimal BondAmount { get; set; }

    [MaxLength(3)]
    public string Currency { get; set; } = "GHS";

    [MaxLength(4000)]
    public string? TermsText { get; set; }

    public TrainingBondStatus Status { get; set; } = TrainingBondStatus.PendingAcceptance;

    // ── Acceptance ───────────────────────────────────────────────────────────
    public bool AcceptedByEmployee { get; set; }
    public DateTime? AcceptedDate { get; set; }

    /// <summary>Set when HR records acceptance on the employee's behalf (null = employee self-accepted).</summary>
    public Guid? AcceptanceRecordedById { get; set; }

    [ForeignKey(nameof(AcceptanceRecordedById))]
    public virtual Employee? AcceptanceRecordedBy { get; set; }

    [MaxLength(1000)]
    public string? AcceptanceNotes { get; set; }

    // ── Obligation window ────────────────────────────────────────────────────
    /// <summary>When the service obligation starts running (defaults to training completion, else acceptance date).</summary>
    public DateTime? BondStartDate { get; set; }

    /// <summary>Computed = BondStartDate + BondDurationMonths.</summary>
    public DateTime? BondEndDate { get; set; }

    // ── Early exit / repayment ───────────────────────────────────────────────
    public DateTime? ExitDate { get; set; }

    /// <summary>Pro-rated amount owed on early exit = BondAmount × (remaining months ÷ total months).</summary>
    public decimal? RepaymentAmount { get; set; }

    public DateTime? SettledDate { get; set; }

    public DateTime? WaivedDate { get; set; }
    public Guid? WaivedById { get; set; }

    [ForeignKey(nameof(WaivedById))]
    public virtual Employee? WaivedBy { get; set; }

    [MaxLength(1000)]
    public string? WaiverReason { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    /// <summary>Optimistic concurrency token — guards concurrent accept/exit/waive/settle transitions.</summary>
    [Timestamp]
    public byte[]? RowVersion { get; set; }
}

/// <summary>
/// Training completion with scores and expiry
/// </summary>
public class TrainingCompletion : TenantEntity
{
    public Guid NominationId { get; set; }
    
    [ForeignKey(nameof(NominationId))]
    public virtual TrainingNomination Nomination { get; set; } = null!;

    public Guid EmployeeId { get; set; }
    
    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    public DateTime CompletionDate { get; set; }
    
    public TrainingCompletionStatus Status { get; set; }

    // Scores & Grades
    [Range(0, 100)]
    public decimal? FinalScore { get; set; }
    
    [Range(0, 100)]
    public decimal? PreAssessmentScore { get; set; }
    
    [Range(0, 100)]
    public decimal? PostAssessmentScore { get; set; }

    public bool IsPassed { get; set; }

    // Manager Verification
    public bool IsVerifiedByManager { get; set; }
    public Guid? VerifiedById { get; set; }
    public DateTime? VerificationDate { get; set; }
    
    [MaxLength(2000)]
    public string? VerificationNotes { get; set; }
}

/// <summary>
/// Daily attendance for training sessions
/// </summary>
public class TrainingAttendance : TenantEntity
{
    public Guid ScheduleId { get; set; }
    
    [ForeignKey(nameof(ScheduleId))]
    public virtual TrainingSchedule Schedule { get; set; } = null!;

    public Guid EmployeeId { get; set; }
    
    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    public Guid? NominationId { get; set; }
    
    [ForeignKey(nameof(NominationId))]
    public virtual TrainingNomination? Nomination { get; set; }

    public DateTime AttendanceDate { get; set; }
    public bool IsPresent { get; set; }

    public TimeSpan? CheckInTime { get; set; }
    public TimeSpan? CheckOutTime { get; set; }
    
    [MaxLength(500)]
    public string? AbsenceReason { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public Guid? MarkedById { get; set; }
    
    [ForeignKey(nameof(MarkedById))]
    public virtual Employee? MarkedBy { get; set; }
    
    public DateTime? MarkedAt { get; set; }
}

/// <summary>
/// Training evaluation/feedback
/// </summary>
public class TrainingFeedback : TenantEntity
{
    public Guid ScheduleId { get; set; }
    
    [ForeignKey(nameof(ScheduleId))]
    public virtual TrainingSchedule Schedule { get; set; } = null!;

    public Guid EmployeeId { get; set; }
    
    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    public Guid? NominationId { get; set; }
    
    [ForeignKey(nameof(NominationId))]
    public virtual TrainingNomination? Nomination { get; set; }

    // Ratings (1-5 scale)
    [Range(1, 5)]
    public int? ContentRelevanceRating { get; set; }
    
    [Range(1, 5)]
    public int? TrainerKnowledgeRating { get; set; }
    
    [Range(1, 5)]
    public int? DeliveryMethodRating { get; set; }
    
    [Range(1, 5)]
    public int? MaterialQualityRating { get; set; }
    
    [Range(1, 5)]
    public int? VenueFacilitiesRating { get; set; }
    
    [Range(1, 5)]
    public int? OverallSatisfactionRating { get; set; }

    // Feedback
    [MaxLength(2000)]
    public string? StrengthsOfTraining { get; set; }
    
    [MaxLength(2000)]
    public string? AreasForImprovement { get; set; }
    
    [MaxLength(2000)]
    public string? SuggestionsForFuture { get; set; }
    
    [MaxLength(2000)]
    public string? AdditionalComments { get; set; }

    // Impact Assessment
    public bool WouldRecommend { get; set; }
    
    [Range(1, 5)]
    public int? LikelihoodToApply { get; set; } // 1-5 scale
    
    [MaxLength(2000)]
    public string? ExpectedApplicationOnJob { get; set; }
    
    [MaxLength(2000)]
    public string? BarriersToApplication { get; set; }

    public DateTime FeedbackDate { get; set; }
}

/// <summary>
/// Level 2/3 post-training evaluation — learning retention and on-the-job application
/// Completed 30, 60, or 90 days after training
/// </summary>
public class TrainingFollowUpAssessment : TenantEntity
{
    public Guid ScheduleId { get; set; }
    
    [ForeignKey(nameof(ScheduleId))]
    public virtual TrainingSchedule Schedule { get; set; } = null!;

    public Guid EmployeeId { get; set; }
    
    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    public Guid? NominationId { get; set; }
    
    [ForeignKey(nameof(NominationId))]
    public virtual TrainingNomination? Nomination { get; set; }

    public TrainingAssessmentType AssessmentType { get; set; } // 30/60/90 day
    
    public DateTime AssessmentDate { get; set; }

    // Level 2 — Knowledge retention
    [MaxLength(4000)]
    public string? KeyLearningsTaken { get; set; }
    
    [MaxLength(2000)]
    public string? ConceptsStillUnclear { get; set; }

    // Level 3 — Behavioral / on-the-job application
    [Range(1, 5)]
    public int? FrequencyOfUse { get; set; } // 1-5 scale
    
    [MaxLength(2000)]
    public string? HowSkillsApplied { get; set; }
    
    [MaxLength(2000)]
    public string? BarriersToApplication { get; set; }
    
    [MaxLength(2000)]
    public string? SupportNeeded { get; set; }

    // Manager observation (filled by direct supervisor)
    public Guid? ManagerId { get; set; }
    
    [ForeignKey(nameof(ManagerId))]
    public virtual Employee? Manager { get; set; }
    
    [MaxLength(2000)]
    public string? ManagerObservationNotes { get; set; }
    
    public DateTime? ManagerSubmittedDate { get; set; }

    public bool RecommendFurtherTraining { get; set; }
    
    [MaxLength(2000)]
    public string? RecommendedFollowUp { get; set; }
}

/// <summary>
/// Certificate issued after completing a company-sponsored training
/// </summary>
public class TrainingCertificate : TenantEntity
{
    [Required]
    [MaxLength(100)]
    public string CertificateNumber { get; set; } = string.Empty;

    /// <summary>Globally-unique short code for public verification (printed on the certificate). Unlike
    /// CertificateNumber (unique only per tenant), this is unique across tenants so an anonymous
    /// verifier can validate a certificate without a tenant context.</summary>
    [MaxLength(32)]
    public string? VerificationCode { get; set; }

    public Guid NominationId { get; set; }
    
    [ForeignKey(nameof(NominationId))]
    public virtual TrainingNomination Nomination { get; set; } = null!;

    public Guid EmployeeId { get; set; }
    
    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    public Guid ProgramId { get; set; }
    
    [ForeignKey(nameof(ProgramId))]
    public virtual TrainingProgram Program { get; set; } = null!;

    [Required]
    [MaxLength(200)]
    public string CertificateName { get; set; } = string.Empty;
    
    public DateTime IssuedDate { get; set; }
    
    public DateTime? ExpiryDate { get; set; }
    
    public int? ValidityMonths { get; set; }

    // Status
    public CertificateStatus Status { get; set; } = CertificateStatus.Active;
    
    public DateTime? RevokedDate { get; set; }
    
    [MaxLength(1000)]
    public string? RevokedReason { get; set; }
    
    public Guid? RevokedById { get; set; }

    // Document
    [MaxLength(1000)]
    public string? FilePath { get; set; }
    
    [MaxLength(1000)]
    public string? ExternalUrl { get; set; }

    // Renewal
    public bool IsRenewal { get; set; }
    
    public Guid? PreviousCertificateId { get; set; }
    
    [ForeignKey(nameof(PreviousCertificateId))]
    public virtual TrainingCertificate? PreviousCertificate { get; set; }

    public Guid? IssuedById { get; set; }
    
    [ForeignKey(nameof(IssuedById))]
    public virtual Employee? IssuedBy { get; set; }
}

/// <summary>
/// Employee-uploaded external / third-party certificates (PMP, ACCA, CIMA, etc.)
/// </summary>
public class EmployeeCertificate : TenantEntity
{
    public Guid EmployeeId { get; set; }
    
    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    [Required]
    [MaxLength(200)]
    public string CertificateName { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(200)]
    public string IssuingBody { get; set; } = string.Empty;
    
    [MaxLength(100)]
    public string? CertificateNumber { get; set; }

    public DateTime IssuedDate { get; set; }
    public DateTime? ExpiryDate { get; set; }

    public CertificateStatus Status { get; set; }

    public SkillCategory? Category { get; set; }
    
    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(1000)]
    public string? FilePath { get; set; }
    
    public bool IsVerified { get; set; }
    
    public Guid? VerifiedById { get; set; }
    
    [ForeignKey(nameof(VerifiedById))]
    public virtual Employee? VerifiedBy { get; set; }
    
    public DateTime? VerifiedDate { get; set; }

    public virtual ICollection<EmployeeSkill> LinkedSkills { get; set; } = new List<EmployeeSkill>();
}

/// <summary>
/// Mandatory training requirements defined at the role/department/organisation level
/// </summary>
public class ComplianceTrainingRequirement : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string RequirementCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string RequirementName { get; set; } = string.Empty;
    
    [MaxLength(2000)]
    public string? Description { get; set; }
    
    [MaxLength(500)]
    public string? RegulatoryReference { get; set; } // e.g. "GDPR Art. 39", "Labour Act s.118"

    public Guid ProgramId { get; set; }
    
    [ForeignKey(nameof(ProgramId))]
    public virtual TrainingProgram Program { get; set; } = null!;

    // Applicability — null = applies to all
    public Guid? OrganizationLevelId { get; set; }
    
    [ForeignKey(nameof(OrganizationLevelId))]
    public virtual OrganizationLevel? OrganizationLevel { get; set; }

    public Guid? OrganizationUnitId { get; set; }
    
    [ForeignKey(nameof(OrganizationUnitId))]
    public virtual OrganizationUnit? OrganizationUnit { get; set; }

    public Guid? PositionId { get; set; }
    
    [ForeignKey(nameof(PositionId))]
    public virtual EmployeePosition? Position { get; set; }

    public ComplianceFrequency Frequency { get; set; }
    public int? CustomFrequencyDays { get; set; } // Used when Frequency = Custom

    // Deadlines
    public int? GracePeriodDays { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime EffectiveDate { get; set; }
    public DateTime? ExpiryDate { get; set; }

    // Consequences
    [MaxLength(2000)]
    public string? NonComplianceConsequences { get; set; }

    // Relations
    public virtual ICollection<EmployeeComplianceRecord> EmployeeRecords { get; set; } = new List<EmployeeComplianceRecord>();
}

/// <summary>
/// Per-employee compliance record for each mandatory training requirement
/// </summary>
public class EmployeeComplianceRecord : TenantEntity
{
    public Guid EmployeeId { get; set; }
    
    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    public Guid RequirementId { get; set; }
    
    [ForeignKey(nameof(RequirementId))]
    public virtual ComplianceTrainingRequirement Requirement { get; set; } = null!;

    public ComplianceStatus Status { get; set; }

    public DateTime AssignedDate { get; set; }
    public DateTime? LastCompletedDate { get; set; }
    public DateTime? NextDueDate { get; set; }
    public DateTime? GracePeriodExpiry { get; set; }

    // Linked to the nomination that fulfilled this
    public Guid? FulfillingNominationId { get; set; }
    
    [ForeignKey(nameof(FulfillingNominationId))]
    public virtual TrainingNomination? FulfillingNomination { get; set; }

    // Exemptions
    public bool IsExempt { get; set; }
    
    [MaxLength(1000)]
    public string? ExemptionReason { get; set; }
    
    public Guid? ExemptedById { get; set; }
    
    [ForeignKey(nameof(ExemptedById))]
    public virtual Employee? ExemptedBy { get; set; }
    
    public DateTime? ExemptionDate { get; set; }
    
    public DateTime? ExemptionExpiryDate { get; set; }

    // Notifications
    public DateTime? ReminderSentDate { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

/// <summary>
/// Training budget per department per period
/// </summary>
public class TrainingBudget : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string BudgetCode { get; set; } = string.Empty;
    public int Year { get; set; }

    /// <summary>Optimistic concurrency token — guards concurrent spend/commit updates on the budget.</summary>
    [Timestamp]
    public byte[]? RowVersion { get; set; }
    public int? Quarter { get; set; } // null = full year

    public Guid? OrganizationLevelId { get; set; }
    
    [ForeignKey(nameof(OrganizationLevelId))]
    public virtual OrganizationLevel? OrganizationLevel { get; set; }

    public Guid? OrganizationUnitId { get; set; }
    
    [ForeignKey(nameof(OrganizationUnitId))]
    public virtual OrganizationUnit? OrganizationUnit { get; set; }

    [MaxLength(3)]
    public string Currency { get; set; } = "GHS";
    public decimal AllocatedAmount { get; set; }
    public decimal CommittedAmount { get; set; } // Approved but not yet spent
    public decimal SpentAmount { get; set; }
    public decimal RemainingAmount => AllocatedAmount - SpentAmount - CommittedAmount;

    // Finance reference (no live GL posting yet — lets HR cite the org GL / cost centre).
    [MaxLength(50)]
    public string? GLAccountCode { get; set; }

    [MaxLength(50)]
    public string? CostCenterCode { get; set; }

    public TrainingBudgetStatus Status { get; set; }

    public Guid? ApprovedById { get; set; }
    
    [ForeignKey(nameof(ApprovedById))]
    public virtual Employee? ApprovedBy { get; set; }
    
    public DateTime? ApprovalDate { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    // Relations
    public virtual ICollection<TrainingSchedule> Schedules { get; set; } = new List<TrainingSchedule>();
    public virtual ICollection<TrainingBudgetTransaction> Transactions { get; set; } = new List<TrainingBudgetTransaction>();
}

/// <summary>
/// Individual debit/credit transactions against a training budget
/// </summary>
public class TrainingBudgetTransaction : TenantEntity
{
    public Guid BudgetId { get; set; }
    
    [ForeignKey(nameof(BudgetId))]
    public virtual TrainingBudget Budget { get; set; } = null!;

    public Guid? ScheduleId { get; set; }
    
    [ForeignKey(nameof(ScheduleId))]
    public virtual TrainingSchedule? Schedule { get; set; }

    [Required]
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;
    
    public decimal Amount { get; set; } // Positive = debit, Negative = credit/refund
    
    public DateTime TransactionDate { get; set; }

    public Guid? RecordedById { get; set; }
    
    [ForeignKey(nameof(RecordedById))]
    public virtual Employee? RecordedBy { get; set; }

    [MaxLength(100)]
    public string? Reference { get; set; } // Invoice or PO number

    [MaxLength(50)]
    public string? GLAccountCode { get; set; }

    [MaxLength(50)]
    public string? VoucherNumber { get; set; } // Finance voucher reference (raised externally)

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

/// <summary>
/// Annual training plan
/// </summary>
public class TrainingPlan : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string PlanNumber { get; set; } = string.Empty;
    public int Year { get; set; }

    public Guid? OrganizationLevelId { get; set; }
    
    [ForeignKey(nameof(OrganizationLevelId))]
    public virtual OrganizationLevel? OrganizationLevel { get; set; }

    public Guid? OrganizationUnitId { get; set; }
    
    [ForeignKey(nameof(OrganizationUnitId))]
    public virtual OrganizationUnit? OrganizationUnit { get; set; }

    public TrainingPlanStatus Status { get; set; }

    public Guid? ApprovedById { get; set; }
    
    [ForeignKey(nameof(ApprovedById))]
    public virtual Employee? ApprovedBy { get; set; }
    
    public DateTime? ApprovalDate { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    public virtual ICollection<TrainingPlanItem> Items { get; set; } = new List<TrainingPlanItem>();
    public virtual ICollection<TrainingPlanBudgetLine> BudgetLines { get; set; } = new List<TrainingPlanBudgetLine>();
}

/// <summary>
/// Line item in a training plan
/// </summary>
public class TrainingPlanItem : TenantEntity
{
    public Guid PlanId { get; set; }
    
    [ForeignKey(nameof(PlanId))]
    public virtual TrainingPlan Plan { get; set; } = null!;

    public Guid? ProgramId { get; set; }
    
    [ForeignKey(nameof(ProgramId))]
    public virtual TrainingProgram? Program { get; set; }

    [Required]
    [MaxLength(200)]
    public string TrainingTitle { get; set; } = string.Empty;
    
    [MaxLength(2000)]
    public string? Description { get; set; }

    [Range(1, 4)]
    public int Quarter { get; set; } // 1-4

    public DateTime? PlannedStartDate { get; set; }
    public DateTime? PlannedEndDate { get; set; }

    public int EstimatedParticipants { get; set; }
    public decimal EstimatedCost { get; set; }

    public bool IsCompleted { get; set; }
    public DateTime? CompletionDate { get; set; }

    public int? ActualParticipants { get; set; }
    public decimal? ActualCost { get; set; }

    // Link to the schedule that fulfilled this plan item
    public Guid? FulfilledByScheduleId { get; set; }
    
    [ForeignKey(nameof(FulfilledByScheduleId))]
    public virtual TrainingSchedule? FulfilledBySchedule { get; set; }
}

/// <summary>
/// Budget line items for training plan
/// </summary>
public class TrainingPlanBudgetLine : TenantEntity
{
    public Guid PlanId { get; set; }
    
    [ForeignKey(nameof(PlanId))]
    public virtual TrainingPlan Plan { get; set; } = null!;

    [Required]
    [MaxLength(100)]
    public string Category { get; set; } = string.Empty; // Venue, Catering, Trainer, Materials

    public decimal BudgetedAmount { get; set; }
    public decimal ActualAmount { get; set; }
    public decimal CommittedAmount { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

/// <summary>
/// Training needs assessment
/// </summary>
public class TrainingNeedsAssessment : TenantEntity
{
    public Guid EmployeeId { get; set; }
    
    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    public int Year { get; set; }
    public AssessmentSource Source { get; set; } // Performance Review, Self Assessment, Manager Request

    [MaxLength(4000)]
    public string IdentifiedGaps { get; set; } = string.Empty;

    public TrainingPriority Priority { get; set; }

    public Guid? IdentifiedById { get; set; }
    
    [ForeignKey(nameof(IdentifiedById))]
    public virtual Employee? IdentifiedBy { get; set; }
    public DateTime IdentifiedDate { get; set; }

    [MaxLength(2000)]
    public string? AdditionalNotes { get; set; }

    // Fulfillment
    public bool TrainingProvided { get; set; }
    public DateTime? TrainingProvidedDate { get; set; }

    // Recommended programs (proper FK instead of free text)
    public virtual ICollection<TrainingNeedsAssessmentProgram> RecommendedPrograms { get; set; } = new List<TrainingNeedsAssessmentProgram>();

    // Skill gaps (linked to skill definitions)
    public virtual ICollection<TrainingNeedsAssessmentSkill> SkillGaps { get; set; } = new List<TrainingNeedsAssessmentSkill>();
}

/// <summary>
/// Programs recommended against a training need
/// </summary>
public class TrainingNeedsAssessmentProgram : TenantEntity
{
    public Guid AssessmentId { get; set; }
    
    [ForeignKey(nameof(AssessmentId))]
    public virtual TrainingNeedsAssessment Assessment { get; set; } = null!;

    public Guid ProgramId { get; set; }
    
    [ForeignKey(nameof(ProgramId))]
    public virtual TrainingProgram Program { get; set; } = null!;

    public TrainingPriority Priority { get; set; }
    
    [MaxLength(2000)]
    public string? Rationale { get; set; }
}

/// <summary>
/// Skill gaps identified in a training needs assessment
/// </summary>
public class TrainingNeedsAssessmentSkill : TenantEntity
{
    public Guid AssessmentId { get; set; }
    
    [ForeignKey(nameof(AssessmentId))]
    public virtual TrainingNeedsAssessment Assessment { get; set; } = null!;

    public Guid SkillId { get; set; }
    
    [ForeignKey(nameof(SkillId))]
    public virtual Skill Skill { get; set; } = null!;

    public ProficiencyLevel CurrentProficiency { get; set; }
    public ProficiencyLevel RequiredProficiency { get; set; }
    public TrainingPriority GapPriority { get; set; }
}

/// <summary>
/// Waitlist for a full training schedule — replaces the IsWaitlisted flag on nominations
/// Supports priority ordering and auto-promotion
/// </summary>
public class TrainingWaitlist : TenantEntity
{
    public Guid ScheduleId { get; set; }
    
    [ForeignKey(nameof(ScheduleId))]
    public virtual TrainingSchedule Schedule { get; set; } = null!;

    public Guid EmployeeId { get; set; }
    
    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    public int Position { get; set; } // Ordering within the waitlist
    public DateTime AddedDate { get; set; }

    /// <summary>Optimistic concurrency token — guards concurrent offer/promote/expire transitions.</summary>
    [Timestamp]
    public byte[]? RowVersion { get; set; }
    public TrainingWaitlistStatus Status { get; set; }

    public DateTime? OfferDate { get; set; }
    public DateTime? OfferExpiryDate { get; set; }
    public DateTime? ResponseDate { get; set; }
    public bool? OfferAccepted { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    // If offered and accepted, link to nomination
    public Guid? CreatedNominationId { get; set; }
    
    [ForeignKey(nameof(CreatedNominationId))]
    public virtual TrainingNomination? Nomination { get; set; }
}

/// <summary>
/// Allows employees to request training programs that are not yet scheduled.
/// </summary>
public class TrainingRequest : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string RequestNumber { get; set; } = string.Empty;
    public Guid EmployeeId { get; set; }
    
    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    [Required]
    [MaxLength(200)]
    public string RequestedTrainingTitle { get; set; } = string.Empty;
    
    [MaxLength(2000)]
    public string? Description { get; set; }
    
    [MaxLength(2000)]
    public string? Justification { get; set; }
    public DateTime RequestDate { get; set; }

    public TrainingRequestStatus Status { get; set; }

    public Guid? ApprovedById { get; set; }
    
    [ForeignKey(nameof(ApprovedById))]
    public virtual Employee? ApprovedBy { get; set; }
    public DateTime? ApprovalDate { get; set; }
    
    [MaxLength(1000)]
    public string? RejectionReason { get; set; }

    public Guid? LinkedProgramId { get; set; }        // If it matches an existing program
    
    [ForeignKey(nameof(LinkedProgramId))]
    public virtual TrainingProgram? LinkedProgram { get; set; }
}

public class LearningPath : TenantEntity
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(4000)]
    public string Description { get; set; } = string.Empty;

    public Guid? OrganizationLevelId { get; set; }

    [ForeignKey(nameof(OrganizationLevelId))]
    public virtual OrganizationLevel? OrganizationLevel { get; set; }

    public Guid? OrganizationUnitId { get; set; }

    [ForeignKey(nameof(OrganizationUnitId))]
    public virtual OrganizationUnit? OrganizationUnit { get; set; }

    public Guid? PositionId { get; set; }

    [ForeignKey(nameof(PositionId))]
    public virtual EmployeePosition? Position { get; set; }

    public LearningPathStatus Status { get; set; }

    public int? EstimatedDurationDays { get; set; }
    public int? EstimatedDurationHours { get; set; }

    public bool ProvidesCertificate { get; set; }
    
    [MaxLength(200)]
    public string? CompletionCertificateName { get; set; }

    // Relations
    public virtual ICollection<LearningPathProgram> Programs { get; set; } = new List<LearningPathProgram>();
    public virtual ICollection<LearningPathSkill> TargetSkills { get; set; } = new List<LearningPathSkill>();
    public virtual ICollection<EmployeeLearningPath> Enrollments { get; set; } = new List<EmployeeLearningPath>();
}

/// <summary>
/// Ordered program steps within a learning path
/// </summary>
public class LearningPathProgram : TenantEntity
{
    public Guid LearningPathId { get; set; }
    
    [ForeignKey(nameof(LearningPathId))]
    public virtual LearningPath LearningPath { get; set; } = null!;

    public Guid ProgramId { get; set; }
    
    [ForeignKey(nameof(ProgramId))]
    public virtual TrainingProgram Program { get; set; } = null!;

    public int SequenceOrder { get; set; }
    public bool IsMandatory { get; set; } = true;
    
    [MaxLength(1000)]
    public string? Notes { get; set; }

    // Prerequisites within the path
    public Guid? PrerequisitePathProgramId { get; set; }
    
    [ForeignKey(nameof(PrerequisitePathProgramId))]
    public virtual LearningPathProgram? PrerequisitePathProgram { get; set; }
}

/// <summary>
/// Skills a learning path is designed to build
/// </summary>
public class LearningPathSkill : TenantEntity
{
    public Guid LearningPathId { get; set; }
    
    [ForeignKey(nameof(LearningPathId))]
    public virtual LearningPath LearningPath { get; set; } = null!;

    public Guid SkillId { get; set; }
    
    [ForeignKey(nameof(SkillId))]
    public virtual Skill Skill { get; set; } = null!;

    public ProficiencyLevel TargetProficiency { get; set; }
}

/// <summary>
/// Employee enrollment in a learning path
/// </summary>
public class EmployeeLearningPath : TenantEntity
{
    public Guid EmployeeId { get; set; }
    
    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    public Guid LearningPathId { get; set; }
    
    [ForeignKey(nameof(LearningPathId))]
    public virtual LearningPath LearningPath { get; set; } = null!;

    public DateTime EnrolledDate { get; set; }
    public DateTime? TargetCompletionDate { get; set; }
    public DateTime? ActualCompletionDate { get; set; }

    [Range(0, 100)]
    public int ProgressPercentage { get; set; } // 0-100
    public bool IsCompleted { get; set; }

    public Guid? AssignedById { get; set; }
    
    [ForeignKey(nameof(AssignedById))]
    public virtual Employee? AssignedBy { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    // Step-level progress
    public virtual ICollection<EmployeeLearningPathStep> Steps { get; set; } = new List<EmployeeLearningPathStep>();
}

/// <summary>
/// Progress on each program step in an employee's learning path
/// </summary>
public class EmployeeLearningPathStep : TenantEntity
{
    public Guid EmployeeLearningPathId { get; set; }
    
    [ForeignKey(nameof(EmployeeLearningPathId))]
    public virtual EmployeeLearningPath EmployeeLearningPath { get; set; } = null!;

    public Guid LearningPathProgramId { get; set; }
    
    [ForeignKey(nameof(LearningPathProgramId))]
    public virtual LearningPathProgram LearningPathProgram { get; set; } = null!;

    public bool IsCompleted { get; set; }
    public DateTime? CompletedDate { get; set; }

    // Linked to the nomination that fulfilled this step
    public Guid? NominationId { get; set; }
    
    [ForeignKey(nameof(NominationId))]
    public virtual TrainingNomination? Nomination { get; set; }
}

/// <summary>
/// Structured mentoring program definition
/// </summary>
public class MentoringProgram : TenantEntity
{
    [Required]
    [MaxLength(200)]
    public string ProgramName { get; set; } = string.Empty;
    
    [MaxLength(2000)]
    public string? Description { get; set; }
    
    [MaxLength(2000)]
    public string? Objectives { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    public int? SessionsPerMonth { get; set; }
    public int? MinutesPerSession { get; set; }

    public bool IsActive { get; set; } = true;

    public Guid? CoordinatedById { get; set; }
    
    [ForeignKey(nameof(CoordinatedById))]
    public virtual Employee? CoordinatedBy { get; set; }

    // Relations
    public virtual ICollection<MentoringPair> Pairs { get; set; } = new List<MentoringPair>();
}

/// <summary>
/// Mentor-mentee pairing
/// </summary>
public class MentoringPair : TenantEntity
{
    public Guid ProgramId { get; set; }
    
    [ForeignKey(nameof(ProgramId))]
    public virtual MentoringProgram Program { get; set; } = null!;

    public Guid MentorId { get; set; }
    
    [ForeignKey(nameof(MentorId))]
    public virtual Employee Mentor { get; set; } = null!;

    public Guid MenteeId { get; set; }
    
    [ForeignKey(nameof(MenteeId))]
    public virtual Employee Mentee { get; set; } = null!;

    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    public MentoringStatus Status { get; set; }
    
    [MaxLength(2000)]
    public string? Goals { get; set; }
    
    [MaxLength(1000)]
    public string? FocusAreas { get; set; }

    [MaxLength(2000)]
    public string? ClosureNotes { get; set; }
    
    [Range(1, 5)]
    public int? MentorRating { get; set; } // 1-5, given by mentee
    
    [Range(1, 5)]
    public int? MenteeRating { get; set; } // 1-5, given by mentor

    // Relations
    public virtual ICollection<MentoringSession> Sessions { get; set; } = new List<MentoringSession>();
}

/// <summary>
/// Individual mentoring session log
/// </summary>
public class MentoringSession : TenantEntity
{
    public Guid PairId { get; set; }
    
    [ForeignKey(nameof(PairId))]
    public virtual MentoringPair Pair { get; set; } = null!;

    public DateTime SessionDate { get; set; }
    
    [Range(1, 1440)]
    public int DurationMinutes { get; set; }
    
    public MentoringSessionFormat? Format { get; set; }

    [MaxLength(4000)]
    public string? TopicsDiscussed { get; set; }
    
    [MaxLength(2000)]
    public string? ActionItems { get; set; }
    
    [MaxLength(2000)]
    public string? MentorNotes { get; set; }
    
    [MaxLength(2000)]
    public string? MenteeNotes { get; set; }

    public bool AttendedByMentor { get; set; }
    public bool AttendedByMentee { get; set; }
}
