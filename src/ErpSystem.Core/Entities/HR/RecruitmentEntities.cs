using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.HR.Requisition;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.HR.Recruitment;

// =============================================================================
// SECTION 1 — VACANCY
// =============================================================================

/// <summary>
/// Job vacancy/opening raised against a staff requisition.
/// </summary>
public class JobVacancy : TenantEntity
{
    [MaxLength(50)]
    public string VacancyNumber { get; set; } = string.Empty;

    /// <summary>
    /// Optional override when the advert title differs from the job description title.
    /// </summary>
    [MaxLength(200)]
    public string? CustomAdvertTitle { get; set; }
	
    [NotMapped]
    public string JobTitle => CustomAdvertTitle ?? Requisition?.JobDescription?.JobTitle ?? string.Empty;

    public Guid StaffRequisitionId { get; set; }

    [ForeignKey(nameof(StaffRequisitionId))]
    public virtual StaffRequisition Requisition { get; set; } = null!;

    /// <summary>
    /// Direct FK to the position this vacancy is hiring for.
    /// Initialized from the requisition; enables indexed queries without joining through the requisition chain.
    /// </summary>
    public Guid PositionId { get; set; }

    [ForeignKey(nameof(PositionId))]
    public virtual EmployeePosition Position { get; set; } = null!;

    /// <summary>
    /// Number of openings for this specific vacancy.
    /// Initialized from the requisition but may differ (e.g. one requisition split into multiple vacancies).
    /// </summary>
    public int NumberOfPositions { get; set; } = 1;

    // Ownership
    public Guid? HiringManagerId { get; set; }

    [ForeignKey(nameof(HiringManagerId))]
    public virtual Employee? HiringManager { get; set; }

    public Guid? RecruiterId { get; set; }

    [ForeignKey(nameof(RecruiterId))]
    public virtual Employee? Recruiter { get; set; }

    // Publishing & timeline
    public JobVacancyStatus VacancyStatus { get; set; } = JobVacancyStatus.Draft;

    /// <summary>The date HR <b>intends</b> to publish (planning). Set on the form.</summary>
    public DateTime? PublishDate { get; set; }

    /// <summary>
    /// The date the vacancy was <b>actually</b> published — stamped when it really transitions into
    /// Published. Distinct from <see cref="PublishDate"/> (the intended date) so HR can compare planned
    /// vs actual go-live.
    /// </summary>
    public DateTime? ActualPublishDate { get; set; }

    public DateTime? ApplicationDeadline { get; set; }
	
	/// <summary>
    /// Internal deadline for completing shortlisting after applications close.
    /// </summary>
    public DateTime? ShortlistingDeadline { get; set; }

    // Shortlist approval workflow
    public ShortlistApprovalStatus ShortlistApprovalStatus { get; set; } = ShortlistApprovalStatus.NotSubmitted;

    public DateTime? ShortlistSubmittedAt { get; set; }

    public Guid? ShortlistSubmittedById { get; set; }

    [ForeignKey(nameof(ShortlistSubmittedById))]
    public virtual Employee? ShortlistSubmittedBy { get; set; }

    public DateTime? ShortlistApprovedAt { get; set; }

    public Guid? ShortlistApprovedById { get; set; }

    [ForeignKey(nameof(ShortlistApprovedById))]
    public virtual Employee? ShortlistApprovedBy { get; set; }

    [MaxLength(2000)]
    public string? ShortlistApprovalNotes { get; set; }
	
	public int? NumberOfInterviewRounds { get; set; }
    
	public DateTime? ClosedDate { get; set; }
	public DateTime? FilledDate { get; set; }
	
    // Closure
    public JobVacancyClosureReason? ClosureReason { get; set; }
    
	[MaxLength(2000)]
    public string? ClosureNotes { get; set; }
	
    /// <summary>
    /// Controls whether salary information is shown on public job postings.
    /// </summary>
    public bool IsSalaryVisible { get; set; } = false;

    // --- Vacancy-level posting details ---
    // These override or supplement position/JD defaults for this specific vacancy opening.

    /// <summary>
    /// Employment arrangement for this vacancy — may differ from a position's typical contract type.
    /// </summary>
    public EmploymentType EmploymentType { get; set; } = EmploymentType.Permanent;

    /// <summary>
    /// Work arrangement for this vacancy — may differ from the position's default work mode.
    /// </summary>
    public WorkMode WorkMode { get; set; } = WorkMode.OnSite;

    [Column(TypeName = "decimal(18,2)")]
    public decimal? SalaryRangeMin { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? SalaryRangeMax { get; set; }

    [MaxLength(10)]
    public string? SalaryCurrencyCode { get; set; }

    /// <summary>
    /// Minimum years of relevant experience required. Posting-facing summary;
    /// detailed scoring rules live in <see cref="ShortlistingCriteria"/>.
    /// </summary>
    public int? RequiredMinExperienceYears { get; set; }

    /// <summary>
    /// Key benefits to highlight in the job posting (e.g. health insurance, car, housing allowance).
    /// </summary>
    [MaxLength(2000)]
    public string? KeyBenefitsSummary { get; set; }

    /// <summary>
    /// Expected start date for the hired candidate.
    /// Typically echoes the requisition's DesiredStartDate but may be adjusted at vacancy level.
    /// </summary>
    public DateOnly? TargetStartDate { get; set; }

    // Recruitment configuration
    public bool RequiresWrittenTest { get; set; }
    public bool RequiresPracticalTest { get; set; }

    // Pipeline
    public Guid? RecruitmentPipelineId { get; set; }
    
	[ForeignKey(nameof(RecruitmentPipelineId))]
	public virtual RecruitmentPipeline? Pipeline { get; set; }

    // Denormalized counters for dashboard performance.
    // These are intentional read-side denormalization — maintained exclusively by the service
    // layer (increment/decrement on domain events). Never updated via raw DB queries.
    public int ApplicationCount { get; set; }
    public int ShortlistedCount { get; set; }
    public int InterviewCount { get; set; }
    public int OfferCount { get; set; }
    public int HireCount { get; set; }

    /// <summary>
    /// Optimistic concurrency token. EF Core includes this in every UPDATE WHERE clause,
    /// so concurrent counter mutations are detected and retried rather than silently lost.
    /// </summary>
    [Timestamp]
    public byte[] RowVersion { get; set; } = null!;

    // SLA tracking
    /// <summary>UTC timestamp when the shortlist was formally completed (approved or submitted).</summary>
    public DateTime? ShortlistCompletedAt { get; set; }

    /// <summary>Number of calendar days from vacancy publish to shortlist completion. Set on completion.</summary>
    public int? TimeToShortlistDays { get; set; }

    /// <summary>Set to true when <see cref="ShortlistingDeadline"/> is passed and the shortlist is still not completed.</summary>
    public bool ShortlistingSlaBreached { get; set; }

    // Test score integration
    /// <summary>
    /// Weight (0–100) given to test scores when computing the composite shortlist score.
    /// 0 = test scores are ignored; 100 = test scores fully replace criterion-based scoring.
    /// Blended as: FinalScore = (CriterionScore × (100 - TestScoreWeight) + TestScore × TestScoreWeight) / 100.
    /// </summary>
    public int TestScoreWeight { get; set; } = 0;

    // Audience scope — snapshotted from the originating staff requisition at vacancy-creation time.
    // A recruiter may override them on the vacancy without touching the requisition.
    // TransitionAsync uses these to auto-create the appropriate JobPosting records on publish.
    /// <summary>Whether internal employees may apply to this vacancy.</summary>
    public bool AllowInternalCandidates { get; set; } = true;

    /// <summary>Whether external candidates may apply to this vacancy.</summary>
    public bool AllowExternalCandidates { get; set; } = true;

    // Internal candidate preferencing
    /// <summary>
    /// Flat point bonus (0–20) added to the composite score for internal candidates.
    /// Applied after normalization so an internal candidate with score 65 and boost 10 effectively scores 75.
    /// </summary>
    public int InternalCandidateBoostPoints { get; set; } = 0;

    // Blind screening
    /// <summary>
    /// When enabled, GET endpoints return a blind view of applications (no candidate name, gender, age, or photo).
    /// Helps reduce unconscious bias during the initial shortlisting round.
    /// </summary>
    public bool IsBlindScreeningEnabled { get; set; }

    // Auto-shortlisting configuration
    /// <summary>
    /// When set, the Auto-Shortlist feature will automatically shortlist candidates
    /// whose AutoScore is at or above this threshold (0–100).
    /// Null means auto-shortlisting is not configured for this vacancy.
    /// </summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal? AutoShortlistMinScore { get; set; }

    /// <summary>
    /// When true, auto-shortlisting will also require that every mandatory criterion passed.
    /// Only meaningful when <see cref="AutoShortlistMinScore"/> is set.
    /// </summary>
    public bool AutoShortlistRequireAllMandatory { get; set; } = true;

	// Workflow Integration
    public Guid? WorkflowInstanceId { get; set; }

    // Relations
    public virtual ICollection<JobVacancyAttachment> Attachments { get; set; } = new List<JobVacancyAttachment>();
	public virtual ICollection<JobPosting> JobPostings { get; set; } = new List<JobPosting>();
	public virtual ICollection<JobShortlistingCriteria> ShortlistingCriteria { get; set; } = new List<JobShortlistingCriteria>();
	public virtual ICollection<JobApplication> JobApplications { get; set; } = new List<JobApplication>();
    public virtual ICollection<JobInterview> Interviews { get; set; } = new List<JobInterview>();
	public virtual ICollection<JobVacancyStatusHistory> StatusHistory { get; set; } = new List<JobVacancyStatusHistory>();
    public virtual ICollection<VacancyPipelineStageAssignment> PipelineStageAssignments { get; set; } = new List<VacancyPipelineStageAssignment>();
}

/// <summary>
/// A file attached to a vacancy
/// </summary>
public class JobVacancyAttachment : TenantEntity
{
    public Guid JobVacancyId { get; set; }

    [ForeignKey(nameof(JobVacancyId))]
    public virtual JobVacancy JobVacancy { get; set; } = null!;

    [MaxLength(200)]
    public string FileName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public DateTime UploadDate { get; set; } = DateTime.UtcNow;

    public Guid UploadedById { get; set; }

    [ForeignKey(nameof(UploadedById))]
    public virtual Employee UploadedBy { get; set; } = null!;
}

public class JobVacancyStatusHistory : TenantEntity
{
	public Guid JobVacancyId { get; set; }
	
	[ForeignKey(nameof(JobVacancyId))]
	public virtual JobVacancy JobVacancy { get; set; } = null!;
	
	public JobVacancyStatus FromStatus { get; set; }
	
	public JobVacancyStatus ToStatus { get; set; }
	
	public DateTime ChangedDate { get; set; } = DateTime.UtcNow;
	
	public Guid ChangedById { get; set; }
	
	[MaxLength(1000)]
	public string? Reason { get; set; }
	
	[MaxLength(2000)]
	public string? Comments { get; set; }
	
	[ForeignKey(nameof(ChangedById))]
	public virtual Employee ChangedBy { get; set; } = null!;
}

/// <summary>
/// Represents a vacancy advert published to a specific channel (website, LinkedIn, etc.).
/// One vacancy may have multiple postings across different channels.
/// </summary>
public class JobPosting : TenantEntity
{
    public Guid JobVacancyId { get; set; }

    [ForeignKey(nameof(JobVacancyId))]
    public virtual JobVacancy JobVacancy { get; set; } = null!;

    public JobPostingChannel Channel { get; set; }

    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string Description { get; set; } = string.Empty;
	
    /// <summary>
    /// The live URL of this posting on the external platform.
    /// </summary>
    [MaxLength(1000)]
    public string? PostingUrl { get; set; }
	
    /// <summary>
    /// The reference ID assigned by the external job board (e.g. LinkedIn job ID, Indeed job key).
    /// Essential for syncing status updates from external platforms.
    /// </summary>
    [MaxLength(200)]
    public string? ExternalPostingId { get; set; }

    /// <summary>The date HR intends to publish this posting on the channel.</summary>
    public DateTime PublishDate { get; set; }

    /// <summary>
    /// The date this posting was <b>actually</b> published — stamped when it is published, HR-editable
    /// (e.g. a newspaper may run the advert a day later than intended). Null until published.
    /// </summary>
    public DateTime? ActualPublishDate { get; set; }

    public DateTime? ExpiryDate { get; set; }

    public JobPostingStatus Status { get; set; } = JobPostingStatus.Draft;

    public bool IsActive { get; set; }

	public int ApplicationCount { get; set; }

    // --- Advert copy (for print channels e.g. Newspaper; composed by HR, seeded from the vacancy) ---

    /// <summary>Headline/title line as it should appear in the printed advert.</summary>
    [MaxLength(200)]
    public string? AdvertHeadline { get; set; }

    /// <summary>Body copy of the advert (role summary, requirements, etc.).</summary>
    [MaxLength(8000)]
    public string? AdvertBody { get; set; }

    /// <summary>How-to-apply instructions printed at the foot of the advert.</summary>
    [MaxLength(1000)]
    public string? HowToApply { get; set; }

    /// <summary>Human-friendly closing-date text for the advert (e.g. "Applications close 31 August 2026").</summary>
    [MaxLength(200)]
    public string? ClosingDateText { get; set; }

    /// <summary>Whether salary is shown in the printed advert.</summary>
    public bool ShowSalaryInAdvert { get; set; }

    /// <summary>Contact details block printed in the advert.</summary>
    [MaxLength(500)]
    public string? ContactDetails { get; set; }

    /// <summary>
    /// The employee (recruiter/HR officer) who published this posting.
    /// </summary>
    public Guid? PostedById { get; set; }

	[ForeignKey(nameof(PostedById))]
    public virtual Employee? PostedBy { get; set; }

    /// <summary>Documents attached to this posting — newspaper artwork, agency brief, proof of publication.</summary>
    public virtual ICollection<JobPostingAttachment> Attachments { get; set; } = new List<JobPostingAttachment>();
}

/// <summary>
/// A file attached to a <see cref="JobPosting"/> — e.g. the newspaper artwork/scan, an agency brief, or
/// proof of publication. Mirrors <see cref="JobVacancyAttachment"/>.
/// </summary>
public class JobPostingAttachment : TenantEntity
{
    public Guid JobPostingId { get; set; }

    [ForeignKey(nameof(JobPostingId))]
    public virtual JobPosting JobPosting { get; set; } = null!;

    [MaxLength(200)]
    public string FileName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public DateTime UploadDate { get; set; } = DateTime.UtcNow;

    public Guid UploadedById { get; set; }

    [ForeignKey(nameof(UploadedById))]
    public virtual Employee UploadedBy { get; set; } = null!;
}

// =============================================================================
// SECTION 2 — RECRUITMENT PIPELINE
// =============================================================================

public class RecruitmentPipeline : TenantEntity
{
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool IsDefault { get; set; }

    public bool IsActive { get; set; } = true;
	
    /// <summary>
    /// Fallback total expected duration in days for a vacancy using this pipeline.
    /// Used for SLA reporting when individual stages do not specify their own DefaultTimeToCompleteDays.
    /// </summary>
    public int? DefaultTimeToCompleteDays { get; set; }

    public virtual ICollection<RecruitmentPipelineStage> Stages { get; set; } = new List<RecruitmentPipelineStage>();
}

public class RecruitmentPipelineStage : TenantEntity
{
    public Guid RecruitmentPipelineId { get; set; }

    [ForeignKey(nameof(RecruitmentPipelineId))]
    public virtual RecruitmentPipeline Pipeline { get; set; } = null!;

    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
	
	[MaxLength(500)]
	public string? Description { get; set; }

    public int Order { get; set; }

    /// <summary>
    /// The functional type of this stage. Drives which actions are available
    /// (e.g. StageType == Interview → allow creating a JobInterview).
    /// </summary>
    public RecruitmentPipelineStageType StageType { get; set; }

    /// <summary>
    /// Indicates this is the final stage in the recruitment pipeline.
    /// Only one stage per pipeline should be marked as final at any time.
    /// </summary>
    public bool IsFinalStage { get; set; }

    public bool IsActive { get; set; } = true;
	
	public bool IsRequired { get; set; } = true;
        
    public int? DefaultTimeToCompleteDays { get; set; }
        
    public bool CanSkip { get; set; }
        
    public bool CanRepeat { get; set; }
        
    public int? MaxAttempts { get; set; }
        
    [MaxLength(2000)]
    public string? Instructions { get; set; }
}

// =============================================================================
// SECTION 2b — VACANCY-SPECIFIC PIPELINE STAGE ASSIGNMENTS
// =============================================================================

/// <summary>
/// Vacancy-specific instantiation of a pipeline stage. Created when a hiring team
/// wants to assign a named owner and/or due date to a particular stage for THIS vacancy.
/// 
/// Relationship to the template:
///   RecruitmentPipeline → RecruitmentPipelineStage  (shared template)
///   JobVacancy + VacancyPipelineStageAssignment      (vacancy-level override)
/// 
/// Not all stages need an assignment record; unassigned stages simply have no
/// ownership/deadline tracking for that vacancy.
/// </summary>
public class VacancyPipelineStageAssignment : TenantEntity
{
    public Guid JobVacancyId { get; set; }

    [ForeignKey(nameof(JobVacancyId))]
    public virtual JobVacancy JobVacancy { get; set; } = null!;

    /// <summary>The pipeline stage this assignment is for.</summary>
    public Guid PipelineStageId { get; set; }

    [ForeignKey(nameof(PipelineStageId))]
    public virtual RecruitmentPipelineStage PipelineStage { get; set; } = null!;

    // ── Ownership ────────────────────────────────────────────────────────────

    /// <summary>The employee responsible for completing this stage.</summary>
    public Guid AssignedToId { get; set; }

    [ForeignKey(nameof(AssignedToId))]
    public virtual Employee AssignedTo { get; set; } = null!;

    /// <summary>The employee who created this assignment.</summary>
    public Guid AssignedById { get; set; }

    [ForeignKey(nameof(AssignedById))]
    public virtual Employee AssignedBy { get; set; } = null!;

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    // ── Deadline ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Date by which this stage activity must be completed for this vacancy.
    /// Null = no deadline enforced for this stage on this vacancy.
    /// </summary>
    public DateTime? DueDate { get; set; }

    // ── Status tracking ──────────────────────────────────────────────────────

    public VacancyStageAssignmentStatus Status { get; set; } = VacancyStageAssignmentStatus.NotStarted;

    public DateTime? CompletedAt { get; set; }

    public Guid? CompletedById { get; set; }

    [ForeignKey(nameof(CompletedById))]
    public virtual Employee? CompletedBy { get; set; }

    [MaxLength(2000)]
    public string? CompletionNotes { get; set; }

    // ── Escalation ───────────────────────────────────────────────────────────

    /// <summary>
    /// When true, an escalation notification is sent if <see cref="DueDate"/> passes
    /// without the stage being completed.
    /// </summary>
    public bool EscalationEnabled { get; set; }

    /// <summary>
    /// Number of days after <see cref="DueDate"/> before the escalation fires.
    /// 0 = escalate on the same day the deadline is missed.
    /// Null uses the system-wide default escalation delay.
    /// Only evaluated when <see cref="EscalationEnabled"/> is true.
    /// </summary>
    public int? EscalationDaysAfterDue { get; set; }

    /// <summary>The employee who should receive the escalation notification.</summary>
    public Guid? EscalateToId { get; set; }

    [ForeignKey(nameof(EscalateToId))]
    public virtual Employee? EscalateTo { get; set; }

    /// <summary>UTC timestamp when escalation was last triggered for this assignment.</summary>
    public DateTime? EscalatedAt { get; set; }

    [MaxLength(2000)]
    public string? EscalationNotes { get; set; }
}

// =============================================================================
// SECTION 3 — SHORTLISTING CRITERIA
// =============================================================================

/// <summary>
/// Criteria for dynamic applicant shortlisting
/// Weighted criteria used to automatically score and rank applicants.
/// </summary>
public class JobShortlistingCriteria : TenantEntity
{
    public Guid JobVacancyId { get; set; }

    [ForeignKey(nameof(JobVacancyId))]
    public virtual JobVacancy JobVacancy { get; set; } = null!;

    [MaxLength(100)]
    public string CriteriaName { get; set; } = string.Empty;
    
    [MaxLength(500)]
    public string? Description { get; set; }

    public JobShortlistingCriteriaType Type { get; set; }
    
    [MaxLength(500)]
    public string? RequiredValue { get; set; }
    
    /// <summary>
    /// Lower bound of the acceptable range (numeric score, age, GPA, etc.).
    /// For experience-based criteria, use <see cref="decimal"/> precision to express half-years.
    /// </summary>
    public decimal? MinValue { get; set; }

    /// <summary>
    /// Upper bound of the acceptable range. Null means no upper limit.
    /// </summary>
    public decimal? MaxValue { get; set; }

    public bool IsMandatory { get; set; }

    /// <summary>
    /// Determines how a multi-valued <see cref="RequiredValue"/> list is matched against
    /// a candidate's profile for criterion types that support multiple values
    /// (Skill, Qualification, Certification, Language).
    ///
    /// <list type="bullet">
    ///   <item><term><see cref="MandatoryMatchMode.AnyMatched"/></term>
    ///     <description>Passes if the candidate has at least one of the listed values. Default.</description></item>
    ///   <item><term><see cref="MandatoryMatchMode.AllRequired"/></term>
    ///     <description>Passes only when the candidate has every listed value.</description></item>
    /// </list>
    ///
    /// Has no effect on single-value or numeric criterion types.
    /// </summary>
    public MandatoryMatchMode MatchMode { get; set; } = MandatoryMatchMode.AnyMatched;

    /// <summary>
    /// Controls how individual required values are compared against candidate profile strings
    /// for list-based criterion types (Skill, Qualification, Certification, Language).
    /// Defaults to <see cref="ValueMatchStrategy.Exact"/> to preserve legacy behaviour.
    /// </summary>
    public ValueMatchStrategy MatchStrategy { get; set; } = ValueMatchStrategy.Exact;

    /// <summary>
    /// Optional link to the Skill master record for ID-first matching.
    /// When set, scoring tries an exact ID match before falling back to string matching.
    /// </summary>
    public Guid? RequiredSkillId { get; set; }

    [ForeignKey(nameof(RequiredSkillId))]
    public virtual Skill? RequiredSkill { get; set; }

    /// <summary>
    /// Optional link to the Qualification catalogue record for ID-first matching.
    /// </summary>
    public Guid? RequiredQualificationId { get; set; }

    [ForeignKey(nameof(RequiredQualificationId))]
    public virtual Qualification? RequiredQualification { get; set; }

	/// <summary>
    /// Weight used when computing the composite auto-score.
    /// </summary>
    public int Weight { get; set; } = 1;
	
	public ShortlistingComparisonOperator? ComparisonOperator { get; set; }
}

// =============================================================================
// SECTION 4 — CANDIDATE PROFILE
// =============================================================================

/// <summary>
/// External candidate portal login account.
/// Separate from ASP.NET Identity — lightweight auth purely for the career portal.
/// Linked to a <see cref="JobCandidate"/> profile once the candidate completes their profile.
/// </summary>
public class CandidatePortalAccount : TenantEntity
{
    [Required, MaxLength(200), EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    public bool IsEmailVerified { get; set; }

    [MaxLength(512)]
    public string? EmailVerificationToken { get; set; }
    public DateTime? EmailVerificationExpiry { get; set; }

    [MaxLength(512)]
    public string? PasswordResetToken { get; set; }
    public DateTime? PasswordResetExpiry { get; set; }

    public DateTime? LastLoginAt { get; set; }
    public int FailedLoginAttempts { get; set; }
    public DateTime? LockedOutUntil { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Linked candidate profile — null until the candidate completes their profile.</summary>
    public Guid? JobCandidateId { get; set; }
    [ForeignKey(nameof(JobCandidateId))]
    public virtual JobCandidate? JobCandidate { get; set; }
}

/// <summary>
/// General candidate record (separate from specific applications for talent pool)
/// Represents a person who has applied or been added to the talent pool.
/// Personal profile data lives here so it is not duplicated across applications.
/// </summary>
public class JobCandidate : TenantEntity
{
	[MaxLength(50)]
    public string CandidateNumber { get; set; } = string.Empty;
	
	[MaxLength(100)]
	public string FirstName { get; set; } = string.Empty;
    
	[MaxLength(100)]
	public string? MiddleName { get; set; }
    
	[MaxLength(100)]
	public string LastName { get; set; } = string.Empty;

    [NotMapped]
    public string FullName => $"{FirstName} {MiddleName ?? string.Empty} {LastName}".Trim();

    public DateTime DateOfBirth { get; set; }
    public Gender Gender { get; set; }

    [MaxLength(100)]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [MaxLength(20)]
    public string Phone { get; set; } = string.Empty;
	
	[MaxLength(20)]
	public string? AlternatePhone { get; set; }

    [MaxLength(200)]
    public string? PostalAddress { get; set; }

    [MaxLength(30)]
    public string? DigitalAddress { get; set; }

    [MaxLength(100)]
    public string City { get; set; } = string.Empty;

    public Guid CountryId { get; set; }

    [ForeignKey(nameof(CountryId))]
    public virtual Country Country { get; set; } = null!;
	
    /// <summary>
    /// Whether this candidate is in the active talent pool for future vacancies.
    /// </summary>
    public bool IsInTalentPool { get; set; }
	
	public DateTime? TalentPoolAddedDate { get; set; }

    public DateTime? TalentPoolRemovedDate { get; set; }

    [MaxLength(1000)]
    public string? TalentPoolRemovalReason { get; set; }

    // ── Talent pool enrichment ────────────────────────────────────────────────

    /// <summary>How this candidate was originally added to the talent pool.</summary>
    public TalentPoolEntrySource TalentPoolSource { get; set; } = TalentPoolEntrySource.NotSpecified;

    /// <summary>Current activity/engagement status within the talent pool.</summary>
    public TalentPoolCandidateStatus TalentPoolStatus { get; set; } = TalentPoolCandidateStatus.Active;

    /// <summary>Free-text recruiter notes specific to this candidate's pool record.</summary>
    [MaxLength(2000)]
    public string? TalentPoolNotes { get; set; }

    /// <summary>Date when this pool record should next be reviewed by a recruiter.</summary>
    public DateTime? TalentPoolReviewDate { get; set; }

    /// <summary>Date of the most recent engagement event (denormalized for fast filtering).</summary>
    public DateTime? LastEngagedDate { get; set; }

    // ── Internal employee shadow candidate ───────────────────────────────────
    /// <summary>
    /// True when this candidate record was created automatically from an employee
    /// profile to support the Internal Job Board self-service flow.
    /// </summary>
    public bool IsInternalEmployee { get; set; }

    /// <summary>FK to the Employee record that generated this shadow candidate.</summary>
    public Guid? InternalEmployeeId { get; set; }

	[MaxLength(200)]
	public string? LinkedInProfile { get; set; }
    
	[MaxLength(200)]
    public string? PortfolioUrl { get; set; }
	
	[MaxLength(200)]
	public string? GitHubUrl { get; set; }

    // ── Professional profile ──────────────────────────────────────────────────

    /// <summary>Short professional tagline, e.g. "Senior .NET Engineer | 8yr Fintech".</summary>
    [MaxLength(300)]
    public string? Headline { get; set; }

    /// <summary>Freeform "About Me" / career objective narrative.</summary>
    [MaxLength(4000)]
    public string? ProfessionalSummary { get; set; }

    /// <summary>Snapshot of the candidate's current or most recent job title.</summary>
    [MaxLength(200)]
    public string? CurrentJobTitle { get; set; }

    /// <summary>Snapshot of the candidate's current or most recent employer.</summary>
    [MaxLength(200)]
    public string? CurrentEmployer { get; set; }

    /// <summary>Self-reported total years of professional experience.</summary>
    public int? TotalYearsExperience { get; set; }

    // ── Availability & preferences ────────────────────────────────────────────

    /// <summary>Notice period required by the candidate's current employer (in days).</summary>
    public int? NoticePeriodDays { get; set; }

    /// <summary>Earliest date the candidate can start a new role.</summary>
    public DateTime? AvailableFrom { get; set; }

    /// <summary>Candidate's preferred work arrangement (remote / hybrid / on-site / any).</summary>
    public PreferredWorkArrangement PreferredWorkArrangement { get; set; } = PreferredWorkArrangement.Any;

    // ── Compensation expectations ──────────────────────────────────────────────

    public decimal? ExpectedSalaryMin { get; set; }
    public decimal? ExpectedSalaryMax { get; set; }

    /// <summary>ISO 4217 currency code, e.g. "GHS", "USD", "GBP".</summary>
    [MaxLength(10)]
    public string? ExpectedSalaryCurrency { get; set; }

    // ── Compliance & eligibility ───────────────────────────────────────────────

    public WorkAuthorizationStatus WorkAuthorizationStatus { get; set; } = WorkAuthorizationStatus.NotSpecified;

    [MaxLength(100)]
    public string? Nationality { get; set; }

    // ── Documents ──────────────────────────────────────────────────────────────

    /// <summary>Path or URL to the candidate's most recently uploaded CV/résumé.</summary>
    [MaxLength(500)]
    public string? CvFilePath { get; set; }

    /// <summary>Path or URL to the candidate's profile photo/avatar.</summary>
    [MaxLength(500)]
    public string? ProfilePhotoUrl { get; set; }

    // Relations
    public virtual ICollection<JobCandidateQualification> Qualifications { get; set; } = new List<JobCandidateQualification>();
    public virtual ICollection<JobCandidateWorkHistory> WorkHistories { get; set; } = new List<JobCandidateWorkHistory>();
    public virtual ICollection<JobCandidateReferee> Referees { get; set; } = new List<JobCandidateReferee>();
    public virtual ICollection<JobCandidateSkill> Skills { get; set; } = new List<JobCandidateSkill>();
    public virtual ICollection<JobCandidateLanguage> Languages { get; set; } = new List<JobCandidateLanguage>();
    public virtual ICollection<JobCandidateInterest> Interests { get; set; } = new List<JobCandidateInterest>();
    public virtual ICollection<JobCandidateDocument> Documents { get; set; } = new List<JobCandidateDocument>();
	public virtual ICollection<JobCandidateNote> Notes { get; set; } = new List<JobCandidateNote>();
	public virtual ICollection<JobApplication> Applications { get; set; } = new List<JobApplication>();
    public virtual ICollection<CandidateSegmentMembership> SegmentMemberships { get; set; } = new List<CandidateSegmentMembership>();
    public virtual ICollection<CandidateEngagementEvent> EngagementEvents { get; set; } = new List<CandidateEngagementEvent>();
}

public class JobCandidateQualification : TenantEntity
{
    public Guid JobCandidateId { get; set; }
 
    [ForeignKey(nameof(JobCandidateId))]
    public virtual JobCandidate JobCandidate { get; set; } = null!;

    public QualificationType QualificationType { get; set; }

    /// <summary>
    /// Optional link to the qualification catalogue. May be null for externally-submitted
    /// qualifications that haven't yet been matched to the catalogue.
    /// </summary>
    public Guid? QualificationId { get; set; }

    [ForeignKey(nameof(QualificationId))]
    public virtual Qualification? Qualification { get; set; }

    /// <summary>
    /// Free-text qualification name entered by the candidate (populated when QualificationId is null).
    /// A recruiter can later reconcile this against the catalogue.
    /// </summary>
    [MaxLength(200)]
    public string? QualificationFreeText { get; set; }

	[MaxLength(200)]
    public string Institution { get; set; } = string.Empty;

    public DateOnly DateAwarded { get; set; }
	
	[MaxLength(100)]
    public string? Grade { get; set; }
}

public class JobCandidateWorkHistory : TenantEntity
{
    public Guid JobCandidateId { get; set; }
 
    [ForeignKey(nameof(JobCandidateId))]
    public virtual JobCandidate JobCandidate { get; set; } = null!;

    [MaxLength(200)]
    public string InstitutionName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string PositionHeld { get; set; } = string.Empty;

    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }

    [MaxLength(4000)]
    public string? Responsibilities { get; set; }
	
    [MaxLength(200)]
    public string? ReasonForLeaving { get; set; }
}

public class JobCandidateReferee : TenantEntity
{
    public Guid JobCandidateId { get; set; }
 
    [ForeignKey(nameof(JobCandidateId))]
    public virtual JobCandidate JobCandidate { get; set; } = null!;

    [MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string Position { get; set; } = string.Empty;

    [MaxLength(200)]
    public string Organization { get; set; } = string.Empty;

    [MaxLength(100)]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [MaxLength(20)]
    public string Phone { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Relationship { get; set; } = string.Empty;
	
	public int YearsKnown { get; set; }
}

/// <summary>
/// A skill claimed by a candidate, optionally linked to the master Skill catalogue.
/// </summary>
public class JobCandidateSkill : TenantEntity
{
    public Guid JobCandidateId { get; set; }

    [ForeignKey(nameof(JobCandidateId))]
    public virtual JobCandidate JobCandidate { get; set; } = null!;

    /// <summary>
    /// Optional link to the master Skill catalogue entry. Null for free-text skills.
    /// </summary>
    public Guid? SkillId { get; set; }

    [ForeignKey(nameof(SkillId))]
    public virtual Skill? Skill { get; set; }

    /// <summary>
    /// Skill name as entered/confirmed by the candidate.
    /// Required even when SkillId is set — acts as a snapshot in case the master record is renamed.
    /// </summary>
    [MaxLength(200)]
    public string SkillName { get; set; } = string.Empty;

    public ProficiencyLevel? Proficiency { get; set; }

    public int? YearsOfExperience { get; set; }

    public bool IsCertified { get; set; }

    [MaxLength(200)]
    public string? CertificationName { get; set; }
}

/// <summary>
/// A language spoken/written by a candidate, with self-assessed proficiency.
/// </summary>
public class JobCandidateLanguage : TenantEntity
{
    public Guid JobCandidateId { get; set; }

    [ForeignKey(nameof(JobCandidateId))]
    public virtual JobCandidate JobCandidate { get; set; } = null!;

    /// <summary>Language name, e.g. "English", "French", "Twi".</summary>
    [Required]
    [MaxLength(100)]
    public string LanguageName { get; set; } = string.Empty;

    public LanguageProficiency Proficiency { get; set; } = LanguageProficiency.ProfessionalWorking;
}

/// <summary>
/// A professional or personal interest declared by a candidate on their profile.
/// </summary>
public class JobCandidateInterest : TenantEntity
{
    public Guid JobCandidateId { get; set; }

    [ForeignKey(nameof(JobCandidateId))]
    public virtual JobCandidate JobCandidate { get; set; } = null!;

    [MaxLength(500)]
    public string Detail { get; set; } = string.Empty;
}

public class JobCandidateDocument : TenantEntity
{
    public Guid JobCandidateId { get; set; }
 
    [ForeignKey(nameof(JobCandidateId))]
    public virtual JobCandidate JobCandidate { get; set; } = null!;

    public JobCandidateDocumentType DocumentType { get; set; }

    [MaxLength(200)]
    public string FileName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;

    public DateTime UploadDate { get; set; }
}

public class JobCandidateNote : TenantEntity
{
    public Guid JobCandidateId { get; set; }
 
    [ForeignKey(nameof(JobCandidateId))]
    public virtual JobCandidate JobCandidate { get; set; } = null!;

    [MaxLength(4000)]
    public string NoteText { get; set; } = string.Empty;
    
	public bool IsPrivate { get; set; } // Visible only to recruiters/HR
}

// ── Talent Pool — Segments / Engagement ──────────────────────────────────────

/// <summary>
/// A named talent pool segment / category that candidates can be grouped into.
/// Examples: "Senior Engineers", "Graduate Cohort 2025", "Passive – London".
/// </summary>
public class CandidateTalentSegment : TenantEntity
{
    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>CSS color token or hex string for UI badge, e.g. "#3b82f6".</summary>
    [MaxLength(30)]
    public string? Color { get; set; }

    public bool IsActive { get; set; } = true;

    public virtual ICollection<CandidateSegmentMembership> Memberships { get; set; } = new List<CandidateSegmentMembership>();
}

/// <summary>
/// Associates a job candidate with a talent pool segment (many-to-many junction).
/// </summary>
public class CandidateSegmentMembership : TenantEntity
{
    public Guid JobCandidateId { get; set; }

    [ForeignKey(nameof(JobCandidateId))]
    public virtual JobCandidate JobCandidate { get; set; } = null!;

    public Guid SegmentId { get; set; }

    [ForeignKey(nameof(SegmentId))]
    public virtual CandidateTalentSegment Segment { get; set; } = null!;

    public Guid AddedByEmployeeId { get; set; }

    public DateTime AddedDate { get; set; } = DateTime.UtcNow;

    [MaxLength(500)]
    public string? Notes { get; set; }
}

/// <summary>
/// A logged engagement / contact event for a talent-pool candidate.
/// </summary>
public class CandidateEngagementEvent : TenantEntity
{
    public Guid JobCandidateId { get; set; }

    [ForeignKey(nameof(JobCandidateId))]
    public virtual JobCandidate JobCandidate { get; set; } = null!;

    public CandidateEngagementEventType EventType { get; set; }

    public DateTime EventDate { get; set; }

    [MaxLength(300)]
    public string? Subject { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    public Guid RecordedByEmployeeId { get; set; }

    /// <summary>True = internal recruiter note; False = outgoing/bi-directional communication.</summary>
    public bool IsInternal { get; set; }
}

// =============================================================================
// SECTION 5 — JOB APPLICATION (application-specific, links Candidate → Vacancy)
// =============================================================================

/// <summary>
/// A single application by a candidate for a specific vacancy.
/// Application-specific data lives here; profile data lives on Candidate.
/// </summary>
public class JobApplication : TenantEntity
{
    [MaxLength(50)]
    public string ApplicationNumber { get; set; } = string.Empty;
	
	public Guid JobVacancyId { get; set; }

    [ForeignKey(nameof(JobVacancyId))]
    public virtual JobVacancy JobVacancy { get; set; } = null!;
	
    public Guid JobCandidateId { get; set; }
 
    [ForeignKey(nameof(JobCandidateId))]
    public virtual JobCandidate JobCandidate { get; set; } = null!;
	
    // Application Details
    public DateTime ApplicationDate { get; set; } = DateTime.UtcNow;
    public ApplicationStatus Status { get; set; } = ApplicationStatus.New;
    public ApplicationSource Source { get; set; } = ApplicationSource.CompanyWebsite;
	
    /// <summary>
    /// The job posting through which this application was received (nullable for
    /// walk-in or internal referral applications).
    /// </summary>
    public Guid? JobPostingId { get; set; }
 
    [ForeignKey(nameof(JobPostingId))]
    public virtual JobPosting? JobPosting { get; set; }

    public int? YearsOfExperience { get; set; }
    public DateTime? AvailableFrom { get; set; }
	
    [MaxLength(5000)]
    public string? CoverLetter { get; set; }
	
    // Automatic Scoring (for dynamic shortlisting)
    public decimal? AutoScore { get; set; }
    public string? AutoScoreBreakdown { get; set; } // JSON — List<CriterionScoreResult>

    /// <summary>UTC timestamp of the last scoring run. Null means never scored.</summary>
    public DateTime? ScoredAt { get; set; }

    /// <summary>
    /// Set to true when criteria are added/changed after this application was scored.
    /// The UI should prompt the recruiter to re-run scoring.
    /// </summary>
    public bool ScoreIsStale { get; set; }

    /// <summary>
    /// Average score across all finalized <see cref="ShortlistReview"/> records for this application.
    /// Recomputed by the service whenever a review is finalized. Null until at least one review is finalized.
    /// </summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal? AggregatedReviewScore { get; set; }

    /// <summary>Whether the shortlist decision came from manual action, auto-threshold, or manual override of an auto result.</summary>
    public ShortlistDecisionSource? DecisionSource { get; set; }

    // Shortlisting
    public DateTime? ShortlistedDate { get; set; }

    public Guid? ShortlistedById { get; set; }

    [ForeignKey(nameof(ShortlistedById))]
    public virtual Employee? ShortlistedBy { get; set; }

	[MaxLength(2000)]
    public string? ShortlistingNotes { get; set; }

    /// <summary>UTC timestamp when shortlist notification emails were last sent via <c>SendShortlistNotificationsAsync</c>. Null means not yet sent.</summary>
    public DateTime? ShortlistNotificationSentAt { get; set; }

    // Waitlisting
    public DateTime? WaitlistedDate { get; set; }

    [MaxLength(1000)]
    public string? WaitlistReason { get; set; }

    // Withdrawal
    public DateTime? WithdrawnDate { get; set; }
 
    [MaxLength(1000)]
    public string? WithdrawalReason { get; set; }
	
    public DateTime? RejectedDate { get; set; }

    public Guid? RejectedById { get; set; }

    [ForeignKey(nameof(RejectedById))]
    public virtual Employee? RejectedBy { get; set; }

    [MaxLength(2000)]
    public string? RejectionReason { get; set; }

    /// <summary>UTC timestamp when rejection notification emails were last sent via <c>SendRejectionNotificationsAsync</c>. Null means not yet sent.</summary>
    public DateTime? RejectionNotificationSentAt { get; set; }

    // External portal tracking
    /// <summary>
    /// Unique opaque token generated when an external candidate submits via the public career portal.
    /// Allows the candidate to check their application status without having an account.
    /// Null for applications created by HR staff or via the internal employee portal.
    /// </summary>
    [MaxLength(100)]
    public string? ExternalTrackingToken { get; set; }

    // Internal candidate flags
    /// <summary>
    /// True when the applicant is a current employee of the organisation.
    /// Internal candidates receive a score boost per the vacancy's InternalCandidateBoostPoints setting.
    /// </summary>
    public bool IsInternalCandidate { get; set; }

    /// <summary>
    /// FK to the employee record when <see cref="IsInternalCandidate"/> is true.
    /// </summary>
    public Guid? InternalEmployeeId { get; set; }

    [ForeignKey(nameof(InternalEmployeeId))]
    public virtual Employee? InternalEmployee { get; set; }

	// Offer and hire links — inverse navigations; no FK columns here.
	// Driven by JobOffer.JobApplicationId and JobHireRecord.ApplicationId respectively.
    [InverseProperty(nameof(JobOffer.Application))]
    public virtual JobOffer? Offer { get; set; }

    [InverseProperty(nameof(JobHireRecord.Application))]
    public virtual JobHireRecord? HireRecord { get; set; }

	// Pipeline tracking
	public virtual ICollection<JobApplicationStageHistory> StageHistories { get; set; } = new List<JobApplicationStageHistory>();
 
    // Test results (if vacancy requires tests)
    public virtual ICollection<JobApplicantTestResult> TestResults { get; set; } = new List<JobApplicantTestResult>();
 
    // Interview links
    public virtual ICollection<JobInterviewee> InterviewSlots { get; set; } = new List<JobInterviewee>();
 
    // Communications sent to/from this applicant for this application
    public virtual ICollection<JobApplicantCommunication> Communications { get; set; } = new List<JobApplicantCommunication>();

    // Shortlist decision audit trail
    public virtual ICollection<ShortlistDecisionLog> DecisionLogs { get; set; } = new List<ShortlistDecisionLog>();

    // Multi-reviewer shortlist scores
    public virtual ICollection<ShortlistReview> ShortlistReviews { get; set; } = new List<ShortlistReview>();

    // ── Candidate profile snapshot ────────────────────────────────────────────
    /// <summary>
    /// JSON-serialized <see cref="ApplicationCandidateSnapshot"/> captured at the moment of
    /// submission.  The scoring engine reads this instead of the live candidate profile so
    /// that re-scoring always reflects the data that existed when the candidate applied,
    /// not any subsequent profile edits.  Null on legacy rows — the engine falls back to
    /// the live profile in that case.
    /// </summary>
    public string? ProfileSnapshotJson { get; set; }

    /// <summary>
    /// Concurrency token. Shortlist / reject / waitlist / withdraw / move-stage are all
    /// load-check-mutate-save, so two reviewers acting at once could otherwise clobber each other's
    /// decision (and double-count the vacancy's shortlist counter).
    /// </summary>
    [Timestamp]
    public byte[]? RowVersion { get; set; }
}

// =============================================================================
// SECTION 5b — CANDIDATE PROFILE SNAPSHOT (value objects, not EF-mapped tables)
// =============================================================================

/// <summary>
/// An immutable snapshot of a candidate's scoreable profile data captured at the
/// moment the application is submitted.  Persisted as JSON inside
/// <see cref="JobApplication.ProfileSnapshotJson"/> — it has no table of its own.
///
/// All fields mirror the live <see cref="JobCandidate"/> collections but are
/// stripped to the minimum needed by the scoring engine so the JSON stays compact.
/// </summary>
[NotMapped]
public sealed class ApplicationCandidateSnapshot
{
    /// <summary>UTC timestamp at which the snapshot was taken.</summary>
    public DateTime SnapshotTakenAt { get; init; }

    // ── Scalar profile fields used directly by scoring criteria ──────────────

    /// <summary>Candidate's years of experience as submitted on the application form.</summary>
    public int? YearsOfExperience { get; init; }

    public DateTime DateOfBirth { get; init; }
    public Gender   Gender      { get; init; }

    [MaxLength(100)]
    public string? City { get; init; }

    /// <summary>Self-reported total years of professional experience from candidate profile.</summary>
    public int? TotalYearsExperience { get; init; }

    // ── Collections ───────────────────────────────────────────────────────────

    public IReadOnlyList<SnapshotSkill>         Skills         { get; init; } = Array.Empty<SnapshotSkill>();
    public IReadOnlyList<SnapshotQualification> Qualifications { get; init; } = Array.Empty<SnapshotQualification>();
    public IReadOnlyList<SnapshotLanguage>      Languages      { get; init; } = Array.Empty<SnapshotLanguage>();
    public IReadOnlyList<SnapshotWorkHistory>   WorkHistories  { get; init; } = Array.Empty<SnapshotWorkHistory>();
}

/// <summary>Frozen skill entry within <see cref="ApplicationCandidateSnapshot"/>.</summary>
[NotMapped]
public sealed class SnapshotSkill
{
    public string  SkillName         { get; init; } = string.Empty;
    public bool    IsCertified       { get; init; }
    public string? CertificationName { get; init; }
    public int?    YearsOfExperience { get; init; }

    /// <summary>Numeric proficiency level (0 = null/unknown).</summary>
    public int? Proficiency { get; init; }

    /// <summary>Catalogue Skill ID, when the candidate linked their skill to the master record.</summary>
    public Guid? SkillId { get; init; }
}

/// <summary>Frozen qualification entry within <see cref="ApplicationCandidateSnapshot"/>.</summary>
[NotMapped]
public sealed class SnapshotQualification
{
    /// <summary>
    /// Normalised lower-case qualification name — either from the catalogue or
    /// the free-text entry.  Pre-normalised at snapshot time so comparisons are O(1).
    /// </summary>
    public string NormalisedName { get; init; } = string.Empty;

    /// <summary>Original display name preserved for audit / UI readback.</summary>
    public string DisplayName    { get; init; } = string.Empty;
    public string? Institution   { get; init; }

    /// <summary>Catalogue Qualification ID, when the candidate linked their qualification to the master record.</summary>
    public Guid? QualificationId { get; init; }
}

/// <summary>Frozen language entry within <see cref="ApplicationCandidateSnapshot"/>.</summary>
[NotMapped]
public sealed class SnapshotLanguage
{
    /// <summary>Lower-case normalised language name for O(1) set lookups.</summary>
    public string NormalisedName { get; init; } = string.Empty;
    public string DisplayName    { get; init; } = string.Empty;
    public int    Proficiency    { get; init; }  // LanguageProficiency enum value
}

/// <summary>Frozen work-history entry within <see cref="ApplicationCandidateSnapshot"/>.</summary>
[NotMapped]
public sealed class SnapshotWorkHistory
{
    public string   InstitutionName { get; init; } = string.Empty;
    public string   PositionHeld    { get; init; } = string.Empty;
    public DateOnly StartDate       { get; init; }
    public DateOnly? EndDate        { get; init; }
}

// =============================================================================
// SECTION 6 — APPLICANT PIPELINE STAGE TRACKING
// =============================================================================

/// <summary>
/// Tracks the movement of an application through the recruitment pipeline stages.
/// Each record represents a transition into a new stage.
/// </summary>
public class JobApplicationStageHistory : TenantEntity
{
    public Guid JobApplicationId { get; set; }
 
    [ForeignKey(nameof(JobApplicationId))]
    public virtual JobApplication JobApplication { get; set; } = null!;
	
    public Guid PipelineStageId { get; set; }
 
    [ForeignKey(nameof(PipelineStageId))]
    public virtual RecruitmentPipelineStage PipelineStage { get; set; } = null!;

    public DateTime EnteredAt { get; set; } = DateTime.UtcNow;
 
    public DateTime? ExitedAt { get; set; }
	
	public JobApplicationStageExitReason? ExitReason { get; set; }
	
    [MaxLength(1000)]
    public string? Notes { get; set; }

    public Guid? MovedById { get; set; }
 
    [ForeignKey(nameof(MovedById))]
    public virtual Employee? MovedBy { get; set; }
	
    /// <summary>
    /// Indicates this is the stage the application is currently in.
    /// Only one record per application should be current at any time.
    /// </summary>
    public bool IsCurrent { get; set; }
}

// =============================================================================
// SECTION 7 — WRITTEN & PRACTICAL TEST RESULTS
// =============================================================================

/// <summary>
/// Records the outcome of a written or practical test taken by an applicant
/// as part of the recruitment process.
/// </summary>
public class JobApplicantTestResult : TenantEntity
{
    public Guid JobApplicationId { get; set; }
 
    [ForeignKey(nameof(JobApplicationId))]
    public virtual JobApplication JobApplication { get; set; } = null!;
 
    public JobApplicantTestType TestType { get; set; } // Written, Practical
 
    [MaxLength(200)]
    public string TestName { get; set; } = string.Empty;
 
    public DateTime TestDate { get; set; }
 
    [MaxLength(500)]
    public string? Venue { get; set; }
 
    public decimal? Score { get; set; }
 
    public decimal? MaxScore { get; set; }
 
    [NotMapped]
    public decimal? ScorePercentage => MaxScore.HasValue && MaxScore > 0 ? Math.Round(Score!.Value / MaxScore.Value * 100, 2) : null;

    public bool? Passed { get; set; }
 
    [MaxLength(2000)]
    public string? Remarks { get; set; }
 
    public Guid? InvigilatedById { get; set; }
 
    [ForeignKey(nameof(InvigilatedById))]
    public virtual Employee? InvigilatedBy { get; set; }
 
    public Guid? MarkedById { get; set; }
 
    [ForeignKey(nameof(MarkedById))]
    public virtual Employee? MarkedBy { get; set; }
 
    public DateTime? MarkedDate { get; set; }
}

// =============================================================================
// SECTION 8 — INTERVIEW QUESTIONS (question bank)
// =============================================================================

public class JobInterviewQuestionType : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string TypeName { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? Code { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }
	
	public bool IsActive { get; set; } = true;

    public virtual ICollection<JobInterviewQuestionDetail> QuestionDetails { get; set; } = new List<JobInterviewQuestionDetail>();
}

public class JobInterviewQuestionDetail : TenantEntity
{
    [Required]
    [MaxLength(500)]
    public string QuestionText { get; set; } = string.Empty;

	/// <summary>
    /// Weight applied when computing weighted scores for this question.
    /// </summary>
    public int Weight { get; set; } = 1;
	
	public int MinScore { get; set; } = 1;
	
	public int MaxScore { get; set; } = 10;
	
    public Guid QuestionTypeId { get; set; }
 
    [ForeignKey(nameof(QuestionTypeId))]
    public virtual JobInterviewQuestionType QuestionType { get; set; } = null!;
 
    public bool IsActive { get; set; } = true;
}

// =============================================================================
// SECTION 8b — INTERVIEW QUESTION PRESETS
// =============================================================================

/// <summary>
/// A named template that groups question types with required question counts.
/// Used to quickly scaffold a structured question plan for an interview panel.
/// </summary>
public class InterviewQuestionPreset : TenantEntity
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public virtual ICollection<InterviewQuestionPresetItem> Items { get; set; } = new List<InterviewQuestionPresetItem>();
}

/// <summary>
/// One line-item inside a preset, tying a question type to a required count.
/// </summary>
public class InterviewQuestionPresetItem : TenantEntity
{
    public Guid PresetId { get; set; }

    [ForeignKey(nameof(PresetId))]
    public virtual InterviewQuestionPreset Preset { get; set; } = null!;

    public Guid QuestionTypeId { get; set; }

    [ForeignKey(nameof(QuestionTypeId))]
    public virtual JobInterviewQuestionType QuestionType { get; set; } = null!;

    public int RequiredQuestionCount { get; set; } = 1;

    public int AllowedPoolSize { get; set; } = 5;

    public int DisplayOrder { get; set; }
}

// =============================================================================
// SECTION 9 — JOB INTERVIEW
// =============================================================================

/// <summary>
/// A scheduled interview session for one round of a vacancy.
/// </summary>
public class JobInterview : TenantEntity
{
    [MaxLength(50)]
    public string InterviewNumber { get; set; } = string.Empty;

    public Guid JobVacancyId { get; set; }

    [ForeignKey(nameof(JobVacancyId))]
    public virtual JobVacancy JobVacancy { get; set; } = null!;

    public int Round { get; set; } = 1;
    public JobInterviewType Type { get; set; } = JobInterviewType.Panel;
    public InterviewMode Mode { get; set; } = InterviewMode.InPerson;
    public JobInterviewStatus Status { get; set; } = JobInterviewStatus.Scheduled;

    // Schedule
    public DateOnly ScheduledDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }

    [MaxLength(500)]
    public string? LocationOrLink { get; set; }
	
    [MaxLength(2000)]
    public string? Instructions { get; set; }
	
	[MaxLength(2000)]
	public string? RescheduleReason { get; set; }
	
	public DateOnly? OriginalDate { get; set; }
	
	[MaxLength(2000)]
	public string? CancellationReason { get; set; }

    /// <summary>The preset template applied when this interview was scheduled (nullable — manual if null).</summary>
    public Guid? QuestionPresetId { get; set; }

    [ForeignKey(nameof(QuestionPresetId))]
    public virtual InterviewQuestionPreset? QuestionPreset { get; set; }

	public virtual ICollection<JobInterviewee> Interviewees { get; set; } = new List<JobInterviewee>();
    public virtual ICollection<JobInterviewPanelist> Panelists { get; set; } = new List<JobInterviewPanelist>();
    public virtual ICollection<JobInterviewExternalPanelist> ExternalPanelists { get; set; } = new List<JobInterviewExternalPanelist>();
    public virtual ICollection<JobInterviewQuestion> Questions { get; set; } = new List<JobInterviewQuestion>();
}

public class JobInterviewPanelist : TenantEntity
{
    public Guid JobInterviewId { get; set; }

    [ForeignKey(nameof(JobInterviewId))]
    public virtual JobInterview JobInterview { get; set; } = null!;

    public Guid EmployeeId { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    public JobInterviewPanelistRole Role { get; set; }
    
    public bool IsRequired { get; set; }
    
    /// <summary>
    /// Null = not yet recorded; true = attended; false = no-show.
    /// </summary>
    public bool? Attended { get; set; }
	
	[MaxLength(1000)]
	public string? NoShowReason { get; set; }
	
	public DateTime? InvitationSentDate { get; set; }
	
	public bool IsConfirmed { get; set; }
        
    public DateTime? ConfirmationDate { get; set; }

    /// <summary>Single-use opaque token embedded in the panel assignment email's "Confirm" link.</summary>
    [MaxLength(64)]
    public string? ConfirmationToken { get; set; }

    /// <summary>
    /// When the <see cref="ConfirmationToken"/> stops being accepted. Null = legacy token with no expiry.
    /// Enforced by the anonymous confirm endpoint; the token is also cleared (single-use) once consumed.
    /// </summary>
    public DateTime? ConfirmationTokenExpiresAt { get; set; }

    public virtual ICollection<JobInterviewScoreSummary> ScoreSummaries { get; set; } = new List<JobInterviewScoreSummary>();
}

public class JobInterviewExternalPanelist : TenantEntity
{
    public Guid JobInterviewId { get; set; }

    [ForeignKey(nameof(JobInterviewId))]
    public virtual JobInterview JobInterview { get; set; } = null!;

    public Guid AssociateId { get; set; }

    [ForeignKey(nameof(AssociateId))]
    public virtual ExternalAssociate ExternalAssociate { get; set; } = null!;

    public JobInterviewPanelistRole Role { get; set; }

    public bool IsRequired { get; set; }

    /// <summary>
    /// Null = not yet recorded; true = attended; false = no-show.
    /// </summary>
    public bool? Attended { get; set; }
	
	[MaxLength(1000)]
	public string? NoShowReason { get; set; }
	
	public DateTime? InvitationSentDate { get; set; }
	
	public bool IsConfirmed { get; set; }
	
	public DateTime? ConfirmationDate { get; set; }

    /// <summary>Single-use opaque token embedded in the panel assignment email's "Confirm" link.</summary>
    [MaxLength(64)]
    public string? ConfirmationToken { get; set; }

    /// <summary>
    /// When the <see cref="ConfirmationToken"/> stops being accepted. Null = legacy token with no expiry.
    /// Enforced by the anonymous confirm endpoint; the token is also cleared (single-use) once consumed.
    /// </summary>
    public DateTime? ConfirmationTokenExpiresAt { get; set; }

    public virtual ICollection<JobInterviewScoreSummary> ScoreSummaries { get; set; } = new List<JobInterviewScoreSummary>();
}

/// <summary>
/// Links an application to a specific interview session.
/// </summary>
public class JobInterviewee : TenantEntity
{
    public Guid JobInterviewId { get; set; }

    [ForeignKey(nameof(JobInterviewId))]
    public virtual JobInterview JobInterview { get; set; } = null!;

    public Guid JobApplicationId { get; set; }

    [ForeignKey(nameof(JobApplicationId))]
	public virtual JobApplication JobApplication { get; set; } = null!;

    /// <summary>Individual start time for this candidate's slot within the interview event.</summary>
    public TimeSpan? SlotStartTime { get; set; }

    /// <summary>Individual end time for this candidate's slot within the interview event.</summary>
    public TimeSpan? SlotEndTime { get; set; }

    public DateTime? InvitationSentDate { get; set; }

    /// <summary>Single-use opaque token embedded in the invitation email's "Confirm attendance" link.</summary>
    [MaxLength(64)]
    public string? ConfirmationToken { get; set; }

    /// <summary>
    /// When the <see cref="ConfirmationToken"/> stops being accepted. Null = legacy token with no expiry.
    /// Enforced by the anonymous confirm endpoint; the token is also cleared (single-use) once consumed.
    /// </summary>
    public DateTime? ConfirmationTokenExpiresAt { get; set; }

    /// <summary>
    /// Whether the candidate confirmed attendance.
    /// </summary>
    public bool? ConfirmedAttendance { get; set; }
	
	public DateTime? ConfirmationDate { get; set; }

    /// <summary>
    /// Null = not yet recorded; true = attended; false = no-show.
    /// </summary>
    public bool? CandidateAttended { get; set; }

    [MaxLength(1000)]
    public string? NoShowReason { get; set; }

    public JobInterviewOutcome? Outcome { get; set; }

    public virtual ICollection<JobInterviewScoreSummary> Scores { get; set; } = new List<JobInterviewScoreSummary>();
}

/// <summary>
/// Specifies which question types (and how many) are used in this interview.
/// </summary>
public class JobInterviewQuestion : TenantEntity
{
	public Guid JobInterviewId { get; set; }

    [ForeignKey(nameof(JobInterviewId))]
    public virtual JobInterview JobInterview { get; set; } = null!;
	
    public Guid QuestionTypeId { get; set; }

    [ForeignKey(nameof(QuestionTypeId))]
    public virtual JobInterviewQuestionType QuestionType { get; set; } = null!;

	/// <summary>
    /// Minimum number of questions from this type that must be asked.
    /// </summary>
    public int RequiredQuestionCount { get; set; } = 2;
    
    /// <summary>
    /// Total pool size to randomly draw from.
    /// </summary>
    public int AllowedPoolSize { get; set; } = 8;
    
	public int DisplayOrder { get; set; }
	
	public virtual ICollection<JobInterviewSelectedQuestion> SelectedQuestions { get; set; } = new List<JobInterviewSelectedQuestion>();
}

public class JobInterviewSelectedQuestion : TenantEntity
{
	public Guid JobInterviewQuestionId { get; set; }
	
	[ForeignKey(nameof(JobInterviewQuestionId))]
	public virtual JobInterviewQuestion JobInterviewQuestion { get; set; } = null!;
	
	public Guid QuestionDetailId { get; set; }
	
	[ForeignKey(nameof(QuestionDetailId))]
	public virtual JobInterviewQuestionDetail Question { get; set; } = null!;
	
	public int DisplayOrder { get; set; }
}

/// <summary>
/// One panelist's overall evaluation of one interviewee.
/// </summary>
public class JobInterviewScoreSummary : TenantEntity
{
    public Guid JobIntervieweeId { get; set; }

    [ForeignKey(nameof(JobIntervieweeId))]
    public virtual JobInterviewee JobInterviewee { get; set; } = null!;

    public Guid? InternalPanelistId { get; set; }

    [ForeignKey(nameof(InternalPanelistId))]
    public virtual JobInterviewPanelist? InternalPanelist { get; set; }

    public Guid? ExternalPanelistId { get; set; }

    [ForeignKey(nameof(ExternalPanelistId))]
    public virtual JobInterviewExternalPanelist? ExternalPanelist { get; set; }

    public decimal TotalRawScore { get; set; }
    public decimal TotalWeightedScore { get; set; }

    public JobInterviewRecommendation Recommendation { get; set; }

    [MaxLength(2000)]
    public string? Comments { get; set; }

    public DateTime EvaluationDate { get; set; }

    public bool IsFinalized { get; set; }

    public DateTime? FinalizedDate { get; set; }

    public virtual ICollection<JobInterviewScoreEntry> ScoreEntries { get; set; } = new List<JobInterviewScoreEntry>();

    /// <summary>
    /// Recomputes TotalRawScore and TotalWeightedScore from the loaded ScoreEntries.
    /// Call this (with entries eagerly loaded) inside the same transaction whenever
    /// score entries are added, updated, or removed.
    /// </summary>
    public void RefreshTotals()
    {
        TotalRawScore = ScoreEntries.Sum(e => e.RawScore);
        TotalWeightedScore = ScoreEntries.Sum(e => e.WeightedScore);
    }
}

/// <summary>
/// Per-question score awarded by a panelist for an interviewee.
/// </summary>
public class JobInterviewScoreEntry : TenantEntity
{
    public Guid ScoreSummaryId { get; set; }

    [ForeignKey(nameof(ScoreSummaryId))]
    public virtual JobInterviewScoreSummary ScoreSummary { get; set; } = null!;
    
    public Guid QuestionDetailId { get; set; }

    [ForeignKey(nameof(QuestionDetailId))]
    public virtual JobInterviewQuestionDetail Question { get; set; } = null!;
    
    public decimal RawScore { get; set; }
    public decimal WeightedScore { get; set; }
    
    [MaxLength(1000)]
    public string? Remarks { get; set; }
}

// =============================================================================
// SECTION 9b — JOB INTERVIEW SCORE DRAFT
// =============================================================================

/// <summary>
/// Stores a panelist's in-progress (draft) scores for a candidate before finalization.
/// One draft per (interviewee, internalPanelist/externalPanelist) combination; upserted
/// on every "Save Draft" call.
/// </summary>
public class JobInterviewScoreDraft : TenantEntity
{
    public Guid JobInterviewId { get; set; }

    [ForeignKey(nameof(JobInterviewId))]
    public virtual JobInterview JobInterview { get; set; } = null!;

    public Guid JobIntervieweeId { get; set; }

    [ForeignKey(nameof(JobIntervieweeId))]
    public virtual JobInterviewee Interviewee { get; set; } = null!;

    /// <summary>FK to JobInterviewPanelist.Id (NOT Employee.Id). Null for external panelists.</summary>
    public Guid? InternalPanelistId { get; set; }

    [ForeignKey(nameof(InternalPanelistId))]
    public virtual JobInterviewPanelist? InternalPanelist { get; set; }

    /// <summary>FK to JobInterviewExternalPanelist.Id. Null for internal panelists.</summary>
    public Guid? ExternalPanelistId { get; set; }

    [ForeignKey(nameof(ExternalPanelistId))]
    public virtual JobInterviewExternalPanelist? ExternalPanelist { get; set; }

    /// <summary>JSON-serialised List&lt;DraftScoreEntryDto&gt; stored as nvarchar(max).</summary>
    public string DraftJson { get; set; } = "[]";

    [MaxLength(2000)]
    public string? Comments { get; set; }

    /// <summary>Nullable JobInterviewRecommendation stored as int so draft can be saved before a recommendation is chosen.</summary>
    public int? Recommendation { get; set; }

    public DateTime LastModified { get; set; } = DateTime.UtcNow;
}

// =============================================================================
// SECTION 10 — JOB OFFER LIFECYCLE
// =============================================================================

/// <summary>
/// Formal offer of employment issued to a successful candidate.
/// </summary>
public class JobOffer : TenantEntity
{
	[MaxLength(50)]
    public string OfferNumber { get; set; } = string.Empty;
	
    public Guid JobApplicationId { get; set; }
 
    [ForeignKey(nameof(JobApplicationId))]
    public virtual JobApplication Application { get; set; } = null!;
	
	public JobOfferStatus OfferStatus { get; set; } = JobOfferStatus.Draft;
	
    // Terms
	public Guid PositionId {get; set;}
	
	[ForeignKey(nameof(PositionId))]
	public virtual EmployeePosition Position {get; set;} = null!;
	
    /// <summary>
    /// Snapshot of the position title at the time the offer was issued.
    /// Persisted so the offer document remains accurate even if the position is later renamed.
    /// </summary>
    [MaxLength(200)]
    public string PositionTitle { get; set; } = string.Empty;

    /// <summary>
    /// Snapshot of the reporting line title at offer issuance.
    /// </summary>
    [MaxLength(200)]
    public string ReportsToTitle { get; set; } = string.Empty;

    /// <summary>
    /// Snapshot of the salary grade title at offer issuance.
    /// </summary>
    [MaxLength(200)]
    public string GradeTitle { get; set; } = string.Empty;

    /// <summary>
    /// Snapshot of the department (organisation unit) name at offer issuance.
    /// </summary>
    [MaxLength(200)]
    public string DepartmentName { get; set; } = string.Empty;

    /// <summary>
    /// Work arrangement for this offer — defaulted from the vacancy, adjustable if needed.
    /// </summary>
    public WorkMode WorkMode { get; set; } = WorkMode.OnSite;

	public Guid? LocationLevelId {get; set;}
	
	[ForeignKey(nameof(LocationLevelId))]
	public virtual LocationLevel? LocationLevel {get; set;}
	
	public Guid? LocationId {get; set;}
	
	[ForeignKey(nameof(LocationId))]
	public virtual Location? Location {get; set;}
	
	public EmploymentType EmploymentType { get; set; }
	
    /// <summary>
    /// Duration in months — populated when EmploymentType is Contract.
    /// </summary>
    public int? ContractDurationMonths { get; set; }

    /// <summary>
    /// Probation period in months — defaulted from the position, HR can override per offer.
    /// </summary>
    public int? ProbationPeriodMonths { get; set; }

    /// <summary>
    /// Notice period in months the new hire must give the company if they resign.
    /// Defaulted from the position's standard notice period.
    /// </summary>
    public int? NoticePeriodMonths { get; set; }

    /// <summary>
    /// Annual leave entitlement in working days per year — captured as a snapshot at offer issuance.
    /// </summary>
    public int? AnnualLeaveDays { get; set; }

    /// <summary>
    /// Contracted weekly working hours — e.g. 40.0 for full-time, 20.0 for part-time.
    /// Auto-defaulted to 40 (or 20 for PartTime) if not supplied by HR.
    /// </summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal? WeeklyHours { get; set; }

    /// <summary>Whether this offer is subject to a Non-Disclosure Agreement.</summary>
    public bool NdaRequired { get; set; }

	public decimal? BaseSalary { get; set; }

    /// <summary>
    /// Snapshot of the salary grade minimum at offer issuance.
    /// Used to validate BaseSalary is within band and displayed as a reference on the offer form.
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? SalaryGradeMin { get; set; }

    /// <summary>
    /// Snapshot of the salary grade maximum at offer issuance.
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? SalaryGradeMax { get; set; }

    /// <summary>
    /// The specific salary level within the grade offered to this candidate.
    /// Null when the grade has no level structure configured.
    /// </summary>
    public Guid? SalaryLevelId { get; set; }

    [ForeignKey(nameof(SalaryLevelId))]
    public virtual SalaryLevel? SalaryLevel { get; set; }

    /// <summary>
    /// The specific notch (step) within the salary level offered.
    /// Null when the grade/level has no notch structure configured.
    /// </summary>
    public Guid? SalaryNotchId { get; set; }

    [ForeignKey(nameof(SalaryNotchId))]
    public virtual SalaryNotch? SalaryNotch { get; set; }

	[MaxLength(10)]
    public string? CurrencyCode { get; set; }
	
	public decimal? Bonus { get; set; }

	[MaxLength(2000)]
	public string? BonusTerms { get; set; }

	public decimal? Commission { get; set; }

	[MaxLength(2000)]
	public string? CommissionStructure { get; set; }

    /// <summary>
    /// Line items describing individual benefits included in this offer.
    /// Replaces the untyped JSON Benefits string — each benefit is a discrete record
    /// that can be queried, validated, and displayed individually.
    /// </summary>
    public virtual ICollection<JobOfferBenefit> Benefits { get; set; } = new List<JobOfferBenefit>();

	public DateOnly? ProposedStartDate { get; set; }
	
    [MaxLength(5000)]
    public string? AdditionalTerms { get; set; }

    public Guid? PreparedById { get; set; }

    [ForeignKey(nameof(PreparedById))]
    public virtual Employee? PreparedBy { get; set; }

    // Approval
    public Guid? ApprovedById { get; set; }
 
    [ForeignKey(nameof(ApprovedById))]
    public virtual Employee? ApprovedBy { get; set; }
	
    public DateTime? ApprovedDate { get; set; }

    /// <summary>
    /// Reason given by the approver when rejecting a submitted offer (status = Rejected).
    /// Distinct from <see cref="DeclineReason"/> which is the candidate's reason for declining.
    /// </summary>
    [MaxLength(2000)]
    public string? ApprovalRejectionReason { get; set; }
	
    public DateTime? OfferDate { get; set; }
    public DateTime? ExpiryDate { get; set; }

    [MaxLength(500)]
    public string? OfferLetterPath { get; set; }
	
	[MaxLength(500)]
    public string? SignedOfferLetterPath { get; set; }
	
    // Response
	public DateTime? AcceptedDate { get; set; }
	
	public DateTime? CounteredDate { get; set; }

    [MaxLength(2000)]
    public string? CandidateResponseNotes { get; set; }
	
    // Decline / revocation
    public DateTime? DeclinedDate { get; set; }
 
    [MaxLength(2000)]
    public string? DeclineReason { get; set; }
 
    public DateTime? RevokedDate { get; set; }
 
    [MaxLength(2000)]
    public string? RevocationReason { get; set; }
	
    /// <summary>
    /// Version counter — incremented each time new terms are issued after a counter-offer.
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// True when this is the most recent version of the offer for this application.
    /// Only one offer per application chain should have IsLatestVersion = true at any time.
    /// Set to false automatically when a revised offer (Version + 1) is issued.
    /// </summary>
    public bool IsLatestVersion { get; set; } = true;

    /// <summary>
    /// Links back to previous version if this offer was revised.
    /// </summary>
    public Guid? PreviousOfferId { get; set; }

    [ForeignKey(nameof(PreviousOfferId))]
    public virtual JobOffer? PreviousOffer { get; set; }

    /// <summary>
    /// When true the offer was issued subject to satisfactory pre-employment checks.
    /// A <see cref="PreEmploymentCheck"/> record will be created once the candidate
    /// accepts conditionally (status = ConditionallyAccepted).
    /// </summary>
    public bool IsConditional { get; set; }

    /// <summary>
    /// Navigation to the pre-employment check package for this offer.
    /// Populated only when <see cref="IsConditional"/> is true.
    /// </summary>
    public virtual PreEmploymentCheck? PreEmploymentCheck { get; set; }

    /// <summary>
    /// Concurrency token. Offer status transitions (approve / issue / candidate response / revoke)
    /// are load-check-mutate-save with no guard otherwise, so concurrent actions can silently
    /// overwrite each other.
    /// </summary>
    [Timestamp]
    public byte[]? RowVersion { get; set; }
}

// =============================================================================
// SECTION 10b — JOB OFFER BENEFIT
// =============================================================================

/// <summary>
/// A single benefit line item on a job offer.
/// Replaces the previous unstructured Benefits JSON string, enabling
/// per-benefit querying, display, and comparison across offer versions.
/// </summary>
public class JobOfferBenefit : TenantEntity
{
    public Guid JobOfferId { get; set; }

    [ForeignKey(nameof(JobOfferId))]
    public virtual JobOffer JobOffer { get; set; } = null!;

    [MaxLength(200)]
    public string BenefitName { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? MonetaryValue { get; set; }

    [MaxLength(10)]
    public string? CurrencyCode { get; set; }

    /// <summary>
    /// Whether this benefit is monetary (e.g. housing allowance) or non-monetary (e.g. company car).
    /// </summary>
    public bool IsMonetary { get; set; }

    public int DisplayOrder { get; set; }
}

// =============================================================================
// SECTION 10c — JOB OFFER NOTE
// =============================================================================

/// <summary>
/// A free-text note posted against a job offer by an HR officer or approver.
/// Provides an internal audit trail of comments and observations.
/// </summary>
public class JobOfferNote : TenantEntity
{
    public Guid JobOfferId { get; set; }

    [ForeignKey(nameof(JobOfferId))]
    public virtual JobOffer JobOffer { get; set; } = null!;

    [Required]
    [MaxLength(4000)]
    public string Body { get; set; } = string.Empty;

    /// <summary>Display name of the person who posted the note.</summary>
    [MaxLength(200)]
    public string? AuthorName { get; set; }
}

/// <summary>
/// Secure token issued to a candidate when an offer is sent.
/// The token is embedded in the offer response link so the candidate can
/// accept, negotiate, or decline without needing an account.
/// </summary>
public class OfferCandidateToken : TenantEntity
{
    [Required]
    public Guid JobOfferId { get; set; }

    [ForeignKey(nameof(JobOfferId))]
    public virtual JobOffer JobOffer { get; set; } = null!;

    /// <summary>Opaque token sent to the candidate via email.</summary>
    [Required]
    public Guid Token { get; set; } = Guid.NewGuid();

    /// <summary>When the token expires — defaults to the offer's ExpiryDate or 14 days.</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>True once the candidate has submitted a response via this token.</summary>
    public bool IsUsed { get; set; }

    public DateTime? UsedAt { get; set; }

    /// <summary>
    /// Concurrency token. Marking the token used is a check-then-act on an <b>anonymous</b> endpoint,
    /// so without this two simultaneous responses both pass the <see cref="IsUsed"/> check and the
    /// candidate can Accept and Decline the same offer (last write wins).
    /// </summary>
    [Timestamp]
    public byte[]? RowVersion { get; set; }
}

// =============================================================================
// SECTION 11 — APPLICANT COMMUNICATION LOG
// =============================================================================

/// <summary>
/// Audit trail of all communications sent to or received from a candidate
/// in relation to a specific application.
/// </summary>
public class JobApplicantCommunication : TenantEntity
{
    public Guid JobApplicationId { get; set; }
 
    [ForeignKey(nameof(JobApplicationId))]
    public virtual JobApplication JobApplication { get; set; } = null!;
 
    public JobApplicantCommunicationType Type { get; set; } // Email, SMS, Letter, Portal notification
 
    public JobApplicantCommunicationDirection Direction { get; set; } // Outbound, Inbound
 
    [MaxLength(200)]
    public string Subject { get; set; } = string.Empty;
 
    [MaxLength(8000)]
    public string Body { get; set; } = string.Empty;
 
    public DateTime SentAt { get; set; }
 
    public Guid? SentById { get; set; }
 
    [ForeignKey(nameof(SentById))]
    public virtual Employee? SentBy { get; set; }
 
    /// <summary>
    /// Template used to generate this communication (nullable for ad-hoc messages).
    /// </summary>
    public Guid? TemplateId { get; set; }
 
    [MaxLength(500)]
    public string? ExternalMessageId { get; set; } // e.g. email provider message ID
}

// =============================================================================
// SECTION 12 — HIRE RECORD (recruitment → employee handoff)
// =============================================================================

/// <summary>
/// Created when a candidate formally accepts an offer and is confirmed as hired.
/// Serves as the bridge record that triggers onboarding and eventual Employee creation.
/// </summary>
public class JobHireRecord : TenantEntity
{
    [MaxLength(50)]
    public string HireNumber { get; set; } = string.Empty;
 
    public Guid ApplicationId { get; set; }
 
    [ForeignKey(nameof(ApplicationId))]
    public virtual JobApplication Application { get; set; } = null!;
 
    public Guid OfferId { get; set; }
 
    [ForeignKey(nameof(OfferId))]
    public virtual JobOffer Offer { get; set; } = null!;
 
    public JobHireStatus Status { get; set; } = JobHireStatus.PendingOnboarding;
 
    public DateOnly ExpectedStartDate { get; set; }
 
    public DateOnly? ActualStartDate { get; set; }
 
    // Once the employee record is created in the core HR module, link it here.
    public Guid? EmployeeId { get; set; }
 
    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee? Employee { get; set; }
 
    /// <summary>The HR user who confirmed the employee physically started. Null until start is confirmed.</summary>
    public Guid? ConfirmedById { get; set; }
 
    [ForeignKey(nameof(ConfirmedById))]
    public virtual Employee? ConfirmedBy { get; set; }
 
    /// <summary>The date/time start was confirmed. Null until start is confirmed.</summary>
    public DateTime? ConfirmedDate { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    /// <summary>
    /// Concurrency token. Confirming a start creates an Employee and their contract, probation,
    /// salary and profile records — a duplicate must never be possible.
    /// </summary>
    [Timestamp]
    public byte[]? RowVersion { get; set; }
}

// =============================================================================
// SECTION 13 — PRE-EMPLOYMENT CHECK (master record per hire)
// =============================================================================
 
/// <summary>
/// Master record grouping all pre-employment checks required for a specific hire.
/// Anchored to the <see cref="JobOffer"/> so checks run during the conditional-offer
/// window — before the hire record is created.
/// </summary>
public class PreEmploymentCheck : TenantEntity
{
    public Guid JobOfferId { get; set; }
 
    [ForeignKey(nameof(JobOfferId))]
    public virtual JobOffer JobOffer { get; set; } = null!;
 
    public PreEmploymentCheckStatus OverallStatus { get; set; } = PreEmploymentCheckStatus.Pending;
 
    public Guid? CoordinatedById { get; set; }
 
    [ForeignKey(nameof(CoordinatedById))]
    public virtual Employee? CoordinatedBy { get; set; }
 
    public DateTime? CompletedDate { get; set; }
 
    [MaxLength(2000)]
    public string? Notes { get; set; }
 
    public virtual ICollection<PreEmploymentCheckItem> Items { get; set; } = new List<PreEmploymentCheckItem>();
}
 
// =============================================================================
// SECTION 14 — PRE-EMPLOYMENT CHECK ITEM
// =============================================================================
 
/// <summary>
/// An individual check within the pre-employment check process
/// (medical, police clearance, background check, academic verification, etc.).
/// </summary>
public class PreEmploymentCheckItem : TenantEntity
{
    public Guid PreEmploymentCheckId { get; set; }
 
    [ForeignKey(nameof(PreEmploymentCheckId))]
    public virtual PreEmploymentCheck PreEmploymentCheck { get; set; } = null!;
 
    public PreEmploymentCheckType CheckType { get; set; }

    /// <summary>Optional display label (e.g. "Ref Check – John Smith"). Falls back to CheckType name when null.</summary>
    [MaxLength(200)]
    public string? Name { get; set; }

    [MaxLength(200)]
    public string? ServiceProviderName { get; set; }
 
    public CheckItemStatus Status { get; set; } = CheckItemStatus.Pending;
 
    public DateTime? RequestedDate { get; set; }
 
    public DateTime? ReceivedDate { get; set; }
 
    public DateTime? ExpiryDate { get; set; }
 
    public bool? Passed { get; set; }
 
    [MaxLength(2000)]
    public string? Instructions { get; set; }

    [MaxLength(2000)]
    public string? Remarks { get; set; }
 
    [MaxLength(500)]
    public string? DocumentPath { get; set; }

    /// <summary>Expected number of calendar days to complete this check.</summary>
    public int? ExpectedDays { get; set; }

    public bool IsMandatory { get; set; } = true;
 
    /// <summary>
    /// Whether a failed or inconclusive result on this item blocks onboarding.
    /// </summary>
    public bool IsBlockingOnFail { get; set; } = true;
 
    public Guid? ReviewedById { get; set; }
 
    [ForeignKey(nameof(ReviewedById))]
    public virtual Employee? ReviewedBy { get; set; }
 
    public DateTime? ReviewedDate { get; set; }
 
    /// <summary>
    /// Populated when CheckType is ReferenceCheck — links to the structured
    /// referee response.
    /// </summary>
    public virtual ReferenceCheckResponse? ReferenceResponse { get; set; }
}
 
// =============================================================================
// SECTION 15 — PRE-EMPLOYMENT CHECK TEMPLATE
// =============================================================================

/// <summary>
/// A reusable template that defines a standard set of pre-employment checks
/// for a role category or department. Can be applied to any conditional offer
/// to seed its <see cref="PreEmploymentCheck"/> items.
/// </summary>
public class PreEmploymentCheckTemplate : TenantEntity
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public virtual ICollection<PreEmploymentCheckTemplateItem> Items { get; set; } = new List<PreEmploymentCheckTemplateItem>();
}

/// <summary>
/// One check definition inside a <see cref="PreEmploymentCheckTemplate"/>.
/// </summary>
public class PreEmploymentCheckTemplateItem : TenantEntity
{
    public Guid TemplateId { get; set; }

    [ForeignKey(nameof(TemplateId))]
    public virtual PreEmploymentCheckTemplate Template { get; set; } = null!;

    public PreEmploymentCheckType CheckType { get; set; }

    [MaxLength(200)]
    public string? DefaultServiceProvider { get; set; }

    [MaxLength(2000)]
    public string? Instructions { get; set; }

    public bool IsMandatory { get; set; } = true;

    /// <summary>Whether failing this item blocks hire record creation.</summary>
    public bool IsBlockingOnFail { get; set; } = true;

    /// <summary>Expected number of calendar days to complete this check.</summary>
    public int? ExpectedDays { get; set; }
}

// =============================================================================
// SECTION 16 — REFERENCE CHECK RESPONSE
// =============================================================================
 
/// <summary>
/// Structured response from a referee as part of the pre-employment process.
/// Linked to a PreEmploymentCheckItem of type ReferenceCheck.
/// </summary>
public class ReferenceCheckResponse : TenantEntity
{
    public Guid CheckItemId { get; set; }
 
    [ForeignKey(nameof(CheckItemId))]
    public virtual PreEmploymentCheckItem CheckItem { get; set; } = null!;
 
    /// <summary>
    /// Referee from the candidate's profile — may differ if the candidate
    /// nominated a different contact for this specific role.
    /// </summary>
    public Guid? RefereeId { get; set; }
 
    [ForeignKey(nameof(RefereeId))]
    public virtual JobCandidateReferee? Referee { get; set; }
 
    // Referee details (captured at time of check in case profile changes)
    [MaxLength(200)]
    public string RefereeName { get; set; } = string.Empty;
 
    [MaxLength(200)]
    public string RefereeOrganisation { get; set; } = string.Empty;
 
    [MaxLength(200)]
    public string RefereePosition { get; set; } = string.Empty;
 
    [MaxLength(100)]
    [EmailAddress]
    public string RefereeEmail { get; set; } = string.Empty;
 
    [MaxLength(20)]
    public string? RefereePhone { get; set; }
 
    // Response
    public DateTime? ResponseDate { get; set; }
 
    public ReferenceResponseMethod ResponseMethod { get; set; }
 
    /// <summary>
    /// Summarised rating/recommendation from the referee.
    /// </summary>
    public ReferenceRating? OverallRating { get; set; }
 
    [MaxLength(5000)]
    public string? Comments { get; set; }
 
    public bool? WouldRehire { get; set; }
 
    public bool? ConfirmedDatesOfEmployment { get; set; }
 
    public bool? ConfirmedPositionHeld { get; set; }
 
    public bool? ConfirmedReasonForLeaving { get; set; }
 
    [MaxLength(500)]
    public string? DocumentPath { get; set; }
}
 
// =============================================================================
// SECTION 16 — ONBOARDING PLAN TEMPLATE
// =============================================================================

/// <summary>
/// A reusable onboarding plan template that can be assigned to new hires
/// based on job family, department, or grade. Each template contains a set
/// of task templates that will be instantiated as actual tasks for the hire.
/// </summary>
public class OnboardingPlanTemplate : TenantEntity
{
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;
 
    [MaxLength(1000)]
    public string? Description { get; set; }
 
    public bool IsDefault { get; set; }
 
    public bool IsActive { get; set; } = true;
 
    public virtual ICollection<OnboardingTaskTemplate> TaskTemplates { get; set; } = new List<OnboardingTaskTemplate>();
}
 
// =============================================================================
// SECTION 17 — ONBOARDING TASK TEMPLATE
// =============================================================================
 
/// <summary>
/// A single task definition within an onboarding plan template.
/// Instantiated as an OnboardingTask when a plan is assigned to a new hire.
/// </summary>
public class OnboardingTaskTemplate : TenantEntity
{
    public Guid PlanTemplateId { get; set; }
 
    [ForeignKey(nameof(PlanTemplateId))]
    public virtual OnboardingPlanTemplate PlanTemplate { get; set; } = null!;
 
    [MaxLength(200)]
    public string TaskName { get; set; } = string.Empty;
 
    [MaxLength(2000)]
    public string? Description { get; set; }
 
    public OnboardingTaskCategory Category { get; set; }
 
    /// <summary>
    /// Number of days after the start date by which this task must be completed.
    /// </summary>
    public int DueDaysFromStartDate { get; set; }
 
    public bool IsMandatory { get; set; } = true;
 
    public int DisplayOrder { get; set; }
 
    [MaxLength(500)]
    public string? InstructionsUrl { get; set; }

    /// <summary>
    /// The position responsible for completing this task (e.g. IT Support for system-access tasks).
    /// Prefer this over <see cref="OwnerId"/> for templates — templates are long-lived and named
    /// individuals change, whereas positions remain stable.
    /// </summary>
    public Guid? OwnerPositionId { get; set; }

    [ForeignKey(nameof(OwnerPositionId))]
    public virtual EmployeePosition? OwnerPosition { get; set; }
}
 
// =============================================================================
// SECTION 18 — ONBOARDING PLAN (instance per hire)
// =============================================================================
 
/// <summary>
/// The onboarding plan instance created for a specific new hire.
/// Generated from an OnboardingPlanTemplate when a HireRecord is confirmed.
/// </summary>
public class OnboardingPlan : TenantEntity
{
    public Guid EmployeeId { get; set; }
 
    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;
 
    public Guid? TemplatePlanId { get; set; }
 
    [ForeignKey(nameof(TemplatePlanId))]
    public virtual OnboardingPlanTemplate? TemplatePlan { get; set; }
 
    public OnboardingStatus Status { get; set; } = OnboardingStatus.NotStarted;
 
    public DateOnly StartDate { get; set; }
 
    public DateOnly? TargetCompletionDate { get; set; }
 
    public DateOnly? ActualCompletionDate { get; set; }
 
    public Guid? AssignedBuddyId { get; set; }
 
    [ForeignKey(nameof(AssignedBuddyId))]
    public virtual Employee? AssignedBuddy { get; set; }
 
    public Guid? OnboardingCoordinatorId { get; set; }
 
    [ForeignKey(nameof(OnboardingCoordinatorId))]
    public virtual Employee? OnboardingCoordinator { get; set; }
 
    [MaxLength(2000)]
    public string? Notes { get; set; }
 
    public virtual ICollection<OnboardingTask> Tasks { get; set; } = new List<OnboardingTask>();
    public virtual ICollection<OnboardingAsset> Assets { get; set; } = new List<OnboardingAsset>();
}
 
// =============================================================================
// SECTION 19 — ONBOARDING TASK (instance per task per hire)
// =============================================================================
 
/// <summary>
/// A single onboarding task assigned to a new hire, instantiated from a
/// task template. May also be created ad-hoc outside of a template.
/// </summary>
public class OnboardingTask : TenantEntity
{
    public Guid OnboardingPlanId { get; set; }
 
    [ForeignKey(nameof(OnboardingPlanId))]
    public virtual OnboardingPlan OnboardingPlan { get; set; } = null!;
 
    /// <summary>
    /// The template this was instantiated from. Null for ad-hoc tasks.
    /// </summary>
    public Guid? TaskTemplateId { get; set; }
 
    [ForeignKey(nameof(TaskTemplateId))]
    public virtual OnboardingTaskTemplate? TaskTemplate { get; set; }
 
    [MaxLength(200)]
    public string TaskName { get; set; } = string.Empty;
 
    [MaxLength(2000)]
    public string? Description { get; set; }
 
    public OnboardingTaskCategory Category { get; set; }
 
    public OnboardingTaskStatus Status { get; set; } = OnboardingTaskStatus.Pending;
 
    public DateOnly DueDate { get; set; }
 
    public DateOnly? CompletedDate { get; set; }
 
    public bool IsMandatory { get; set; } = true;
 
    /// <summary>
    /// The specific employee currently assigned to complete this task.
    /// </summary>
    public Guid? AssignedToId { get; set; }
 
    [ForeignKey(nameof(AssignedToId))]
    public virtual Employee? AssignedTo { get; set; }
 
    public Guid? AssignedOrganizationUnitId { get; set; }

    [ForeignKey(nameof(AssignedOrganizationUnitId))]
    public virtual OrganizationUnit? AssignedOrganizationUnit { get; set; }

    /// <summary>
    /// The position responsible for completing this task, copied from the task template
    /// at instantiation time. Acts as a fallback when AssignedToId cannot be resolved
    /// (e.g. the position is currently vacant at hire time), preserving the intended
    /// owner role even for unassigned tasks.
    /// </summary>
    public Guid? OwnerPositionId { get; set; }

    [ForeignKey(nameof(OwnerPositionId))]
    public virtual EmployeePosition? OwnerPosition { get; set; }

    // Completion
    public Guid? CompletedById { get; set; }
 
    [ForeignKey(nameof(CompletedById))]
    public virtual Employee? CompletedBy { get; set; }
 
    [MaxLength(2000)]
    public string? CompletionNotes { get; set; }
 
    [MaxLength(500)]
    public string? EvidenceFilePath { get; set; }
 
    // Verification (for tasks that require a second-party sign-off)
    public bool RequiresVerification { get; set; }
 
    public Guid? VerifiedById { get; set; }
 
    [ForeignKey(nameof(VerifiedById))]
    public virtual Employee? VerifiedBy { get; set; }
 
    public DateTime? VerifiedDate { get; set; }
 
    public int DisplayOrder { get; set; }
 
    public virtual ICollection<OnboardingTaskComment> Comments { get; set; } = new List<OnboardingTaskComment>();
}
 
// =============================================================================
// SECTION 20 — ONBOARDING TASK COMMENT
// =============================================================================
 
/// <summary>
/// A note or update posted against an onboarding task.
/// </summary>
public class OnboardingTaskComment : TenantEntity
{
    public Guid TaskId { get; set; }
 
    [ForeignKey(nameof(TaskId))]
    public virtual OnboardingTask Task { get; set; } = null!;
 
    [MaxLength(4000)]
    public string Comment { get; set; } = string.Empty;
 
    public Guid AuthorId { get; set; }
 
    [ForeignKey(nameof(AuthorId))]
    public virtual Employee Author { get; set; } = null!;
 
    public DateTime CommentDate { get; set; } = DateTime.UtcNow;
}
 
// =============================================================================
// SECTION 21 — ONBOARDING ASSET
// =============================================================================
 
/// <summary>
/// An item of equipment, access, or resource provisioned for a new hire
/// as part of the onboarding process (laptop, access card, system account, etc.).
/// </summary>
public class OnboardingAsset : TenantEntity
{
    public Guid OnboardingPlanId { get; set; }
 
    [ForeignKey(nameof(OnboardingPlanId))]
    public virtual OnboardingPlan OnboardingPlan { get; set; } = null!;
 
    public OnboardingAssetType AssetType { get; set; }
 
    [MaxLength(200)]
    public string AssetName { get; set; } = string.Empty;
 
    [MaxLength(500)]
    public string? Description { get; set; }
 
    [MaxLength(100)]
    public string? AssetTag { get; set; }
 
    [MaxLength(100)]
    public string? SerialNumber { get; set; }
 
    public OnboardingAssetProvisionStatus Status { get; set; } = OnboardingAssetProvisionStatus.Pending;
 
    public DateOnly? RequiredByDate { get; set; }
 
    public DateTime? ProvisionedDate { get; set; }
 
    public Guid? ProvisionedById { get; set; }
 
    [ForeignKey(nameof(ProvisionedById))]
    public virtual Employee? ProvisionedBy { get; set; }
 
    public DateTime? IssuedToEmployeeDate { get; set; }
 
    /// <summary>
    /// Acknowledgement sign-off by the new hire confirming receipt.
    /// </summary>
    public bool AcknowledgedByEmployee { get; set; }
 
    public DateTime? AcknowledgementDate { get; set; }
 
    [MaxLength(500)]
    public string? AcknowledgementDocumentPath { get; set; }
 
    [MaxLength(2000)]
    public string? Notes { get; set; }
}
 
// =============================================================================
// SECTION 22 — PROBATION PERIOD
// =============================================================================
 
/// <summary>
/// Probation period record for an employee. One record per employment term;
/// a new probation record is created if employment terms are restarted
/// (e.g. after rehire or contract renewal with a probation clause).
/// </summary>
public class ProbationPeriod : TenantEntity
{
    public Guid EmployeeId { get; set; }
 
    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;
 
    public Guid ContractDetailId { get; set; }
 
    [ForeignKey(nameof(ContractDetailId))]
    public virtual EmployeeContractDetail EmployeeContractDetail { get; set; } = null!;
 
    public DateOnly StartDate { get; set; }
 
    public DateOnly OriginalEndDate { get; set; }
 
    /// <summary>
    /// The actual end date, which may differ from OriginalEndDate if extended.
    /// </summary>
    public DateOnly CurrentEndDate { get; set; }
 
    public int DurationMonths { get; set; }
 
    public ProbationStatus Status { get; set; } = ProbationStatus.Active;
 
    [MaxLength(2000)]
    public string? OutcomeNotes { get; set; }
 
    /// <summary>
    /// Running count of extensions applied to this probation period.
    /// Incremented automatically whenever a ProbationExtension record is created.
    /// </summary>
    public int ExtensionCount { get; set; } = 0;

    public virtual ICollection<ProbationReview> Reviews { get; set; } = new List<ProbationReview>();
    public virtual ICollection<ProbationExtension> Extensions { get; set; } = new List<ProbationExtension>();
}

// =============================================================================
// SECTION 23 — PROBATION EXTENSION (audit trail per extension)
// =============================================================================

/// <summary>
/// Audit record for each individual probation extension.
/// Created whenever a probation period's end date is pushed out.
/// ProbationPeriod.ExtensionCount is incremented in the same transaction.
/// </summary>
public class ProbationExtension : TenantEntity
{
    public Guid ProbationPeriodId { get; set; }

    [ForeignKey(nameof(ProbationPeriodId))]
    public virtual ProbationPeriod ProbationPeriod { get; set; } = null!;

    /// <summary>The end date before this extension was applied.</summary>
    public DateOnly PreviousEndDate { get; set; }

    /// <summary>The new end date after extension.</summary>
    public DateOnly NewEndDate { get; set; }

    /// <summary>Duration of this specific extension in months.</summary>
    public int ExtensionMonths { get; set; }

    [MaxLength(2000)]
    public string Reason { get; set; } = string.Empty;

    public Guid ExtendedById { get; set; }

    [ForeignKey(nameof(ExtendedById))]
    public virtual Employee ExtendedBy { get; set; } = null!;

    public DateTime ExtendedDate { get; set; } = DateTime.UtcNow;

    [MaxLength(2000)]
    public string? Comments { get; set; }
}

// =============================================================================
// SECTION 24 — PROBATION REVIEW
// =============================================================================
 
/// <summary>
/// A scheduled or ad-hoc review conducted during the probation period.
/// Multiple reviews may be held (e.g. at 1 month, 3 months, end of probation).
/// </summary>
public class ProbationReview : TenantEntity
{
    public Guid ProbationPeriodId { get; set; }
 
    [ForeignKey(nameof(ProbationPeriodId))]
    public virtual ProbationPeriod ProbationPeriod { get; set; } = null!;
 
    public int ReviewNumber { get; set; } // 1, 2, 3...
 
    public DateOnly ScheduledDate { get; set; }
 
    public DateOnly? ActualDate { get; set; }
 
    public ProbationReviewStatus Status { get; set; } = ProbationReviewStatus.Scheduled;
 
    // Ratings
    public ProbationPerformanceRating? PerformanceRating { get; set; }
 
    public ProbationPerformanceRating? ConductRating { get; set; }
 
    public ProbationPerformanceRating? AttitudeRating { get; set; }
 
    [MaxLength(5000)]
    public string? StrengthsObserved { get; set; }
 
    [MaxLength(5000)]
    public string? AreasForImprovement { get; set; }
 
    [MaxLength(5000)]
    public string? ReviewerComments { get; set; }
 
    [MaxLength(5000)]
    public string? EmployeeResponse { get; set; }
 
    // Recommendation coming out of this review
    public ProbationReviewRecommendation? Recommendation { get; set; }
 
    /// <summary>
    /// If Recommendation is Extend, how many additional months are proposed.
    /// </summary>
    public int? ProposedExtensionMonths { get; set; }
 
    // Reviewer
    public Guid ReviewedById { get; set; }
 
    [ForeignKey(nameof(ReviewedById))]
    public virtual Employee ReviewedBy { get; set; } = null!;
 
    public Guid? SecondReviewerId { get; set; }
 
    [ForeignKey(nameof(SecondReviewerId))]
    public virtual Employee? SecondReviewer { get; set; }
 
    // Approval / acknowledgement
    public bool EmployeeAcknowledged { get; set; }
 
    public DateTime? EmployeeAcknowledgementDate { get; set; }
 
    public bool HrApproved { get; set; }
 
    public Guid? HrApprovedById { get; set; }
 
    [ForeignKey(nameof(HrApprovedById))]
    public virtual Employee? HrApprovedBy { get; set; }
 
    public DateTime? HrApprovalDate { get; set; }
 
    [MaxLength(500)]
    public string? SignedDocumentPath { get; set; }
}

// =============================================================================
// SECTION 23 — SHORTLIST DECISION LOG (immutable audit trail)
// =============================================================================

/// <summary>
/// An immutable record written once for every shortlisting decision.
/// Never updated or deleted — append-only. Answers: who, when, why, score at decision time.
/// </summary>
public class ShortlistDecisionLog : TenantEntity
{
    public Guid JobApplicationId { get; set; }

    [ForeignKey(nameof(JobApplicationId))]
    public virtual JobApplication JobApplication { get; set; } = null!;

    /// <summary>Snapshot of the application number at decision time.</summary>
    [MaxLength(50)]
    public string ApplicationNumber { get; set; } = string.Empty;

    public ShortlistDecisionType DecisionType { get; set; }

    public Guid? DecisionById { get; set; }

    [ForeignKey(nameof(DecisionById))]
    public virtual Employee? DecisionBy { get; set; }

    public DateTime DecisionAt { get; set; } = DateTime.UtcNow;

    /// <summary>AutoScore at the exact moment this decision was made.</summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal? AutoScoreAtDecision { get; set; }

    /// <summary>Whether this decision was triggered automatically (score threshold) or by a human.</summary>
    public bool IsAutoDecision { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    /// <summary>
    /// Set when a human overrides a prior auto-decision (e.g. manually unshortlisting
    /// an auto-shortlisted candidate). Links to the preceding log entry.
    /// </summary>
    public Guid? OverridesDecisionLogId { get; set; }
}

// =============================================================================
// SECTION 24 — SHORTLIST REVIEW (multi-reviewer scoring)
// =============================================================================

/// <summary>
/// One reviewer's manual shortlist score for a specific application.
/// Multiple reviewers (HR, hiring manager) score independently;
/// the aggregated score is computed and stored back on JobApplication.AggregatedReviewScore.
/// </summary>
public class ShortlistReview : TenantEntity
{
    public Guid JobApplicationId { get; set; }

    [ForeignKey(nameof(JobApplicationId))]
    public virtual JobApplication JobApplication { get; set; } = null!;

    public Guid ReviewerId { get; set; }

    [ForeignKey(nameof(ReviewerId))]
    public virtual Employee Reviewer { get; set; } = null!;

    /// <summary>Raw score given by this reviewer. Convention: 0–100.</summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal Score { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    /// <summary>When the reviewer submitted their score.</summary>
    public DateTime ReviewedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Once finalized a review cannot be changed.</summary>
    public bool IsFinalized { get; set; }

    public DateTime? FinalizedAt { get; set; }
}
