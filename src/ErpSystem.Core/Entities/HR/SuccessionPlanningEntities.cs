using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.HR.SuccessionPlanning;

/// <summary>
/// Succession plan for a critical position.
///
/// <para><b>DB CONSTRAINTS — enforce in OnModelCreating / migration:</b></para>
/// <list type="number">
///   <item>
///     Filtered unique index to prevent multiple active versions for the same
///     position:<br/>
///     <c>CREATE UNIQUE INDEX UX_SuccessionPlan_ActiveVersion
///        ON SuccessionPlans (PositionId, IsActiveVersion)
///        WHERE IsActiveVersion = 1;</c>
///   </item>
///   <item>
///     <c>DeleteBehavior.Restrict</c> on <see cref="SupersededByPlanId"/> to
///     prevent cascade cycles on the self-referencing FK.
///   </item>
/// </list>
/// </summary>
public class SuccessionPlan : TenantEntity
{
    public string PlanNumber { get; set; } = string.Empty;
	public string PlanName { get; set; } = string.Empty;
    public string? Description { get; set; }

    // Target Position (the position being planned for)
    public Guid PositionId { get; set; }

    [ForeignKey(nameof(PositionId))]
    public virtual EmployeePosition Position { get; set; } = null!;

    public Guid? CurrentIncumbentId { get; set; }

    [ForeignKey(nameof(CurrentIncumbentId))]
    public virtual Employee? CurrentIncumbent { get; set; }

    // Plan Details
    public int PlanYear { get; set; }
	public int VersionNumber { get; set; } = 1;
	
	/// <summary>
    /// Only one version per position may be active at a time. Enforced by a
    /// filtered unique index — see class-level XML doc.
    /// </summary>
	public bool IsActiveVersion { get; set; }
    
    public Guid? SupersededByPlanId { get; set; }

    [ForeignKey(nameof(SupersededByPlanId))]
    public virtual SuccessionPlan? SupersededByPlan { get; set; }

    public SuccessionPlanStatus Status { get; set; }

    // Risk Assessment
    public PositionCriticality Criticality { get; set; }
    public SuccessionRisk RiskLevel { get; set; }
    public string? RiskAssessmentNotes { get; set; }
	public string? BusinessImpactIfVacant { get; set; }

    // Incumbent Status
    public DateTime? IncumbentRetirementDate { get; set; }
    public DateTime? AnticipatedVacancyDate { get; set; }
    public VacancyReason? AnticipatedVacancyReason { get; set; }
	public string? IncumbentSuccessionNotes { get; set; }

    /// <summary>
    /// Derived: <c>true</c> when at least one non-emergency candidate has
    /// <see cref="ReadinessLevel.ReadyNow"/>. Call
    /// <see cref="RecalculateDerivedFields"/> after mutating
    /// <see cref="Candidates"/>.
    /// </summary>
    public bool HasReadyNowSuccessor { get; private set; }
    
    /// <summary>
    /// Derived: count of active, non-emergency candidates. Call
    /// <see cref="RecalculateDerivedFields"/> after mutating
    /// <see cref="Candidates"/>.
    /// </summary>
	public int NumberOfIdentifiedSuccessors { get; private set; }
	
    /// <summary>
    /// Derived: <c>true</c> when <see cref="EmergencySuccessorId"/> is set or
    /// at least one candidate has <see cref="SuccessionCandidate.IsEmergencyOnly"/>
    /// = <c>true</c>.
    /// </summary>
    public bool HasEmergencySuccessor { get; private set; }
    
	public Guid? EmergencySuccessorId { get; set; }
    
    [ForeignKey(nameof(EmergencySuccessorId))]
    public virtual Employee? EmergencySuccessor { get; set; }
    
    public string? EmergencyProtocol { get; set; }

    // Timeline
    public DateTime? TargetSuccessionDate { get; set; }
    public int? EstimatedTimeToReadyMonths { get; set; }

    // Approval
    public Guid? ReviewedById { get; set; }
    
    [ForeignKey(nameof(ReviewedById))]
    public virtual Employee? ReviewedBy { get; set; }
    
    public DateTime? ReviewDate { get; set; }

    public Guid? ApprovedById { get; set; }
    
    [ForeignKey(nameof(ApprovedById))]
    public virtual Employee? ApprovedBy { get; set; }
    
    public DateTime? ApprovalDate { get; set; }

    // Next Review
	public int ReviewFrequencyMonths { get; set; } = 12;
    public DateTime? NextReviewDate { get; set; }

    // Relations
	public virtual ICollection<SuccessionCompetencyRequirement> CompetencyRequirements { get; set; } = new List<SuccessionCompetencyRequirement>();
    public virtual ICollection<SuccessionAction> Actions { get; set; } = new List<SuccessionAction>();
	public virtual ICollection<SuccessionPlanHistory> History { get; set; } = new List<SuccessionPlanHistory>();
	public virtual ICollection<SuccessionCandidate> Candidates { get; set; } = new List<SuccessionCandidate>();
	public virtual ICollection<SuccessionDocument> Documents { get; set; } = new List<SuccessionDocument>();

    /// <summary>
    /// Recalculates all derived fields from the current <see cref="Candidates"/>
    /// collection. Call this within the same unit of work after any add,
    /// remove, or readiness change on candidates, then let EF Core persist
    /// the updated scalars.
    /// </summary>
	public void RecalculateDerivedFields()
    {
        var active = Candidates.Where(c => !c.SuccessionCompleted).ToList();

        HasReadyNowSuccessor = active.Any(c => !c.IsEmergencyOnly && c.CurrentReadiness == ReadinessLevel.ReadyNow);

        NumberOfIdentifiedSuccessors = active.Count(c => !c.IsEmergencyOnly);

        HasEmergencySuccessor = EmergencySuccessorId.HasValue || active.Any(c => c.IsEmergencyOnly);
    }
}

public class SuccessionCompetencyRequirement : TenantEntity
{
    public Guid SuccessionPlanId { get; set; }
    
    [ForeignKey(nameof(SuccessionPlanId))]
    public virtual SuccessionPlan SuccessionPlan { get; set; } = null!;

    public Guid CompetencyId { get; set; }

    [ForeignKey(nameof(CompetencyId))]
    public virtual Competency Competency { get; set; } = null!;

    public int RequiredLevel { get; set; }
}

/// <summary>
/// A potential successor nominated for a specific position succession plan.
///
/// <para><b>DB CONSTRAINT REQUIRED:</b><br/>
/// <c>CREATE UNIQUE INDEX UX_SuccessionCandidate_Rank
///    ON SuccessionCandidates (SuccessionPlanId, Rank);</c><br/>
/// Prevents two candidates on the same plan sharing the same priority rank.
/// </para>
///
/// <para><b>Pool field authority:</b><br/>
/// When <see cref="TalentPoolMemberId"/> is set, <see cref="Strengths"/> and
/// <see cref="DevelopmentGaps"/> on this record are <em>plan-specific
/// overrides</em>. Leave them <c>null</c> when no override is needed and
/// read from <see cref="TalentPoolMember"/> instead, to avoid two diverging
/// copies of the same information.
/// </para>
/// </summary>
public class SuccessionCandidate : TenantEntity
{
    public Guid SuccessionPlanId { get; set; }

    [ForeignKey(nameof(SuccessionPlanId))]
    public virtual SuccessionPlan SuccessionPlan { get; set; } = null!;

    public Guid EmployeeId { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;
	
    /// <summary>
    /// Optional link to the talent pool record for this employee.
    /// When set, <see cref="Strengths"/> and <see cref="DevelopmentGaps"/>
    /// represent plan-specific overrides; if those are null, defer to the
    /// pool member record.
    /// </summary>
	public Guid? TalentPoolMemberId { get; set; }
	
    [ForeignKey(nameof(TalentPoolMemberId))]
	public virtual TalentPoolMember? TalentPoolMember { get; set; }

    // Candidate Details
    public CandidateType Type { get; set; } // Internal, External, Emergency
	
    /// <summary>
    /// Priority ranking within the plan (1 = first choice).
    /// Must be unique per plan — see class-level DB constraint note.
    /// </summary>
    public int Rank { get; set; } // Priority ranking (1 = first choice)

    // Readiness Assessment
    public ReadinessLevel CurrentReadiness { get; set; }
    public DateTime? ReadyByDate { get; set; }
    public int? MonthsToReady { get; set; }
	public bool IsEmergencyOnly { get; set; }

    // Performance & Potential
    public PerformanceRating? LatestPerformanceRating { get; set; }
    public PotentialRating? PotentialRating { get; set; }
	
    public Guid? TalentReviewRatingId { get; set; }         // FK to source rating record
	
    [ForeignKey(nameof(TalentReviewRatingId))]
    public virtual TalentReviewRating? TalentReviewRating { get; set; }

    // Strengths & Gaps

    /// <summary>
    /// Plan-specific override. Leave <c>null</c> when
    /// <see cref="TalentPoolMemberId"/> is set and no override is needed.
    /// </summary>
    public string? Strengths { get; set; }

    /// <summary>
    /// Plan-specific override. Leave <c>null</c> when
    /// <see cref="TalentPoolMemberId"/> is set and no override is needed.
    /// </summary>
	public string? DevelopmentGaps { get; set; }
	public string? DevelopmentPlan { get; set; }

    // Experience
    public int YearsInCurrentRole { get; set; }
    public int YearsWithCompany { get; set; }
    public bool HasRelevantExperience { get; set; }
	public string? RelevantExperienceDetails { get; set; }

    // Mobility
    public bool WillingToRelocate { get; set; }
    public bool AvailableForPromotion { get; set; }
    public DateTime? AvailableFrom { get; set; }

    // Risk Factors
    public RetentionRisk RetentionRisk { get; set; }
    public string? RiskMitigationPlan { get; set; }

    // Assessment
    public Guid? AssessedById { get; set; }
    
    [ForeignKey(nameof(AssessedById))]
    public virtual Employee? AssessedBy { get; set; }
    
    public DateTime? AssessmentDate { get; set; }
    public string? AssessmentNotes { get; set; }

    public bool IsRecommended { get; set; }
    public string? RecommendationNotes { get; set; }
	public DateTime? RecommendationDate { get; set; }
    
    public Guid? RecommendedById { get; set; }
    
    [ForeignKey(nameof(RecommendedById))]
    public virtual Employee? RecommendedBy { get; set; }

    // Tracking
    public bool IsSelected { get; set; }
    public DateTime? SelectionDate { get; set; }
    public bool SuccessionCompleted { get; set; }
    public DateTime? SuccessionDate { get; set; }

    public virtual ICollection<SuccessionDevelopmentActivity> DevelopmentActivities { get; set; } = new List<SuccessionDevelopmentActivity>();
	public virtual ICollection<SuccessionCandidateGap> CompetencyGaps { get; set; } = new List<SuccessionCandidateGap>();
	public virtual ICollection<SuccessionCandidateFeedback> Feedback { get; set; } = new List<SuccessionCandidateFeedback>();
}

/// <summary>
/// A reviewer's note on a succession candidate during selection/calibration. Multiple reviewers
/// can "pass notes" with a Support/Neutral/Oppose disposition to inform the final choice.
/// <c>TenantEntity</c> already supplies Id/CreatedAt/audit — CreatedAt is the feedback timestamp.
/// </summary>
public class SuccessionCandidateFeedback : TenantEntity
{
	public Guid CandidateId { get; set; }

    [ForeignKey(nameof(CandidateId))]
	public virtual SuccessionCandidate Candidate { get; set; } = null!;

	public Guid ReviewerId { get; set; }

    [ForeignKey(nameof(ReviewerId))]
	public virtual Employee Reviewer { get; set; } = null!;

	public string Note { get; set; } = string.Empty;
	public FeedbackDisposition Disposition { get; set; }
}

public class SuccessionCandidateGap : TenantEntity
{
	public Guid CandidateId { get; set; }

    [ForeignKey(nameof(CandidateId))]
	public virtual SuccessionCandidate Candidate { get; set; } = null!;

	// FK into your existing competency/skills library
	public Guid CompetencyId { get; set; }
	
    [ForeignKey(nameof(CompetencyId))]
    public virtual Competency Competency  { get; set; } = null!;

	public int RequiredLevel { get; set; }    // 1–5 scale (or whatever your framework uses)
	public int CurrentLevel { get; set; }
	
    /// <summary>
    /// Signed gap: positive = still needs development, zero = met,
    /// negative = candidate exceeds the requirement.
    /// Use <see cref="Status"/> for categorised filtering.
    /// </summary>	
	public int GapSize => RequiredLevel - CurrentLevel;

    /// <summary>
    /// Categorised gap status derived from <see cref="GapSize"/>.
    /// Not stored — computed on read.
    /// </summary>
	public GapStatus Status => GapSize > 0 ? GapStatus.Gap : GapSize < 0 ? GapStatus.Exceeded : GapStatus.Met;

	public string? GapNotes { get; set; }
	public bool Addressed { get; set; } = false;
	public DateTime? AddressedDate { get; set; }

	// Link to the development activity that addressed this gap
	public Guid? AddressedByActivityId { get; set; }

    [ForeignKey(nameof(AddressedByActivityId))]
	public virtual SuccessionDevelopmentActivity? AddressedByActivity { get; set; }
}

/// <summary>
/// An individual development activity for a succession candidate or a talent
/// pool member.
///
/// <para><b>Ownership rule (enforce in application layer):</b><br/>
/// Exactly one of <see cref="CandidateId"/> or <see cref="TalentPoolMemberId"/>
/// must be set. A record where both are null is an orphan with no retrievable
/// owner. A record where both are set is ambiguous. Validate in the command
/// handler before persisting.
/// </para>
/// </summary>
public class SuccessionDevelopmentActivity : TenantEntity
{
    /// <summary>
    /// Set when this activity belongs to a specific succession candidate.
    /// Mutually exclusive with <see cref="TalentPoolMemberId"/>.
    /// </summary>
	public Guid? CandidateId { get; set; }

    [ForeignKey(nameof(CandidateId))]
    public virtual SuccessionCandidate? Candidate { get; set; }
	
    /// <summary>
    /// Set when this activity is pool-level (not yet tied to a specific plan
    /// or candidate). Mutually exclusive with <see cref="CandidateId"/>.
    /// </summary>
	public Guid? TalentPoolMemberId { get; set; }

    [ForeignKey(nameof(TalentPoolMemberId))]
    public virtual TalentPoolMember? TalentPoolMember { get; set; }

    public string ActivityName { get; set; } = string.Empty;
    public DevelopmentActivityType Type { get; set; } // Training, Mentoring, Job Rotation, Project Assignment
	public string? Description { get; set; }

    public DateTime PlannedStartDate { get; set; }
    public DateTime? PlannedEndDate { get; set; }
	public DateTime? ActualStartDate { get; set; }
    public DateTime? ActualEndDate { get; set; }

    public DevelopmentActivityStatus Status { get; set; }

    public string? Outcome { get; set; }
    public bool CompetencyGained { get; set; }
	
	public decimal? EstimatedCost { get; set; }
	public decimal? ActualCost { get; set; }
	public string? CurrencyCode { get; set; }   // ISO 4217, e.g. "USD", "GBP"

    public Guid? SupervisorId { get; set; }
    
    [ForeignKey(nameof(SupervisorId))]
    public virtual Employee? Supervisor { get; set; }
	
	public Guid? ExternalProviderContactId { get; set; }   // If externally facilitated
	public string? ExternalProviderName { get; set; }
	
	public string? Notes { get; set; }
	
    public virtual ICollection<SuccessionCandidateGap> AddressedGaps { get; set; } = new List<SuccessionCandidateGap>();
	public virtual ICollection<SuccessionDevelopmentMilestone> Milestones { get; set; } = new List<SuccessionDevelopmentMilestone>();
}

public class SuccessionDevelopmentMilestone : TenantEntity
{
    public Guid ActivityId { get; set; }

    [ForeignKey(nameof(ActivityId))]
    public virtual SuccessionDevelopmentActivity Activity { get; set; } = null!;
    
    public string MilestoneName { get; set; } = string.Empty;
    public DateTime TargetDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public bool IsCompleted { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// A plan-level or candidate-specific action item.
///
/// <para><b>Cycle prevention:</b><br/>
/// <see cref="DependsOnActionId"/> creates a self-referencing chain. The
/// database cannot prevent a circular dependency (A → B → A). The application
/// layer must perform a graph traversal check before persisting any new
/// dependency link.
/// </para>
/// </summary>
public class SuccessionAction : TenantEntity
{
    public Guid SuccessionPlanId { get; set; }

    [ForeignKey(nameof(SuccessionPlanId))]
    public virtual SuccessionPlan SuccessionPlan { get; set; } = null!;

    /// <summary>
    /// Optional. When set, this action is specific to the candidate's
    /// development rather than the plan as a whole.
    /// </summary>
	public Guid? CandidateId { get; set; }

    [ForeignKey(nameof(CandidateId))]
    public virtual SuccessionCandidate? Candidate { get; set; }

    public string ActionDescription { get; set; } = string.Empty;
    public ActionType Type { get; set; } // Development, Recruitment, Retention
	public ActionPriority Priority { get; set; }

    public Guid? ResponsiblePersonId { get; set; }

    [ForeignKey(nameof(ResponsiblePersonId))]
    public virtual Employee? ResponsiblePerson { get; set; }
	
    public Guid? AssignedById { get; set; }
    
    [ForeignKey(nameof(AssignedById))]
    public virtual Employee? AssignedBy { get; set; }

    public DateTime? DueDate { get; set; }
	public DateTime? StartedDate { get; set; }
    public ActionStatus Status { get; set; }

    public DateTime? CompletionDate { get; set; }
    public string? CompletionNotes { get; set; }
	
    /// <summary>
    /// Self-referencing dependency. The application layer MUST check for
    /// circular chains before saving — no DB constraint can prevent this.
    /// </summary>
    public Guid? DependsOnActionId { get; set; }

    [ForeignKey(nameof(DependsOnActionId))]
    public virtual SuccessionAction? DependsOnAction { get; set; }
	
	// Outcome
    public bool WasSuccessful { get; set; }
    public string? OutcomeNotes { get; set; }
}

/// <summary>
/// Immutable audit snapshot of a <see cref="SuccessionPlan"/> at a specific
/// point in time. Written on every approval, version change, or material edit.
/// Never modified after creation.
///
/// <para>
/// <see cref="PlanSnapshot"/> holds a full JSON serialization of the plan —
/// including candidates, competency requirements, and actions — at snapshot
/// time. The scalar fields below are retained for fast dashboard queries
/// without deserializing JSON.
/// </para>
/// </summary>
public class SuccessionPlanHistory : TenantEntity
{
	public Guid SuccessionPlanId { get; set; }

    [ForeignKey(nameof(SuccessionPlanId))]
	public virtual SuccessionPlan SuccessionPlan { get; set; } = null!;

	public int VersionNumber { get; set; }
	public int PlanYear { get; set; }

	// Snapshot fields — mirrors key fields from SuccessionPlan at save time
	public SuccessionPlanStatus StatusAtSnapshot { get; set; }
	public SuccessionRisk RiskLevelAtSnapshot { get; set; }
	public bool HasReadyNowSuccessorAtSnapshot { get; set; }
	public int NumberOfSuccessorsAtSnapshot { get; set; }
	public string? NotesAtSnapshot { get; set; }
	
    /// <summary>
    /// JSON serialization of the complete plan state at snapshot time,
    /// including all candidates, competency requirements, and actions.
    /// Serialized by the application layer before saving.
    /// Map to NVARCHAR(MAX) (SQL Server) or JSONB (PostgreSQL) in EF config.
    /// </summary>
    public string? PlanSnapshot { get; set; }

	public DateTime SnapshotDate { get; set; }

	public Guid? SnapshotCreatedById { get; set; }
	
    [ForeignKey(nameof(SnapshotCreatedById))]
    public virtual Employee? SnapshotCreatedBy { get; set; }
	
    public string? ChangeReason { get; set; }
}

/// <summary>
/// A document attached to a succession plan, a specific candidate, or a
/// talent pool member.
///
/// <para><b>Ownership rules:</b></para>
/// <list type="bullet">
///   <item><see cref="SuccessionPlanId"/> is optional. It must be set when the
///   document belongs to a plan or plan candidate. It may be null when the
///   document is attached to a pool member before any plan exists.</item>
///   <item>
///     At least one of <see cref="SuccessionPlanId"/> or
///     <see cref="TalentPoolMemberId"/> must be non-null.
///     Enforce in the application layer.
///   </item>
///   <item>
///     <see cref="CandidateId"/> further narrows a plan-level document to a
///     specific candidate. Requires <see cref="SuccessionPlanId"/> to also be set.
///   </item>
/// </list>
/// </summary>
public class SuccessionDocument : TenantEntity
{
    public Guid? SuccessionPlanId { get; set; }

    [ForeignKey(nameof(SuccessionPlanId))]
    public virtual SuccessionPlan? SuccessionPlan { get; set; }
    
    public string DocumentName { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string DocumentUrl { get; set; } = string.Empty;
    public string? Description { get; set; }

	/// <summary>Narrows the document to a specific plan candidate.</summary>
    public Guid? CandidateId { get; set; }

    [ForeignKey(nameof(CandidateId))]
    public virtual SuccessionCandidate? Candidate { get; set; }
	
    /// <summary>
    /// Set when the document belongs to a pool member and no plan yet exists.
    /// </summary>
	public Guid? TalentPoolMemberId { get; set; }

    [ForeignKey(nameof(TalentPoolMemberId))]
    public virtual TalentPoolMember? TalentPoolMember { get; set; }
    
    public DateTime UploadDate { get; set; }
    
    public Guid UploadedById { get; set; }

    [ForeignKey(nameof(UploadedById))]
    public virtual Employee UploadedBy { get; set; } = null!;
    
    public long FileSizeBytes { get; set; }
    public string? FileHash { get; set; }
    public bool IsConfidential { get; set; }
    public DateTime? RetentionDate { get; set; }
}

/// <summary>
/// A talent pool for high-potential employees, independent of any specific
/// succession plan. Allows candidate identification and development before a
/// vacancy is anticipated.
/// </summary>
public class TalentPool : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>
    /// Tenant-defined pool type. Replaces the former <c>TalentPoolType</c> enum so HR can
    /// configure their own pool types — see <see cref="TalentPoolTypeDefinition"/>.
    /// </summary>
    public Guid PoolTypeId { get; set; }

    [ForeignKey(nameof(PoolTypeId))]
    public virtual TalentPoolTypeDefinition PoolType { get; set; } = null!;

    /// <summary>
    /// Optional target position this pool is grooming talent for. When set, its
    /// competency/skill requirements can be shown on the pool and used for gap mapping.
    /// </summary>
    public Guid? TargetPositionId { get; set; }

    [ForeignKey(nameof(TargetPositionId))]
    public virtual EmployeePosition? TargetPosition { get; set; }

	public int TargetSize { get; set; }
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    
	public bool IsActive { get; set; }

    public Guid OwnerId { get; set; }

    [ForeignKey(nameof(OwnerId))]
    public virtual Employee Owner { get; set; } = null!;

    public virtual ICollection<TalentPoolMember> Members { get; set; } = new List<TalentPoolMember>();
}

/// <summary>
/// Tenant-defined talent pool type (e.g. "High Potential", "Leadership Pipeline"). Replaces
/// the former hardcoded <c>TalentPoolType</c> enum so HR can configure their own types via a
/// setup UI. Seeded from the original enum values so existing pools keep working.
/// </summary>
public class TalentPoolTypeDefinition : TenantEntity
{
    /// <summary>Stable machine code (e.g. "HighPotential"). Mirrors the former enum names for
    /// programmatic lookup (e.g. by the appraisal succession-nomination handler).</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Optional badge colour (hex, e.g. "#2563EB") for UI rendering.</summary>
    public string? ColorHex { get; set; }

    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>True for the built-in types seeded from the original enum; a hint that the row
    /// is system-provided (deletion may be restricted in the UI).</summary>
    public bool IsSystemDefault { get; set; }

    public virtual ICollection<TalentPool> Pools { get; set; } = new List<TalentPool>();
}

/// <summary>
/// Membership of an employee in a talent pool.
///
/// <para><b>Rating field authority:</b><br/>
/// <see cref="LatestPerformanceRating"/> and <see cref="LatestPotentialRating"/>
/// are performance-optimised cache fields. The canonical source of truth is the
/// most recent confirmed <see cref="TalentReviewRating"/> in
/// <see cref="ReviewRatings"/>. Update the cached fields in the application
/// layer whenever a new rating is calibration-confirmed, and set
/// <see cref="RatingLastUpdated"/> to the confirmation timestamp. Never write
/// to these cache fields in isolation — always derive from a ReviewRating.
/// </para>
/// </summary>
public class TalentPoolMember : TenantEntity
{
    public Guid TalentPoolId { get; set; }

    [ForeignKey(nameof(TalentPoolId))]
    public virtual TalentPool TalentPool { get; set; } = null!;

    public Guid EmployeeId { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;
	
	public int Rank { get; set; }                               // Within the pool
    public ReadinessLevel Readiness { get; set; }
    public DateTime? ReadyByDate { get; set; }
	public string? Justification { get; set; }
    public string? Strengths { get; set; }
    public string? DevelopmentGaps { get; set; }
	
	public DateTime EnrolledDate { get; set; }
	public Guid? NominatedById { get; set; }
	public Employee? NominatedBy { get; set; }
	public string? NominationNotes { get; set; }

	public DateTime? LastReviewDate { get; set; }
	public DateTime? NextReviewDate { get; set; }
	public string? ReviewNotes { get; set; }

    /// <summary>
    /// Dashboard cache — populated from the most recent confirmed
    /// <see cref="TalentReviewRating"/>. Do not use for analytical queries;
    /// join to <see cref="ReviewRatings"/> instead.
    /// </summary>
	public PerformanceRating? LatestPerformanceRating { get; set; }
	
    /// <summary>
    /// Dashboard cache — see note on <see cref="LatestPerformanceRating"/>.
    /// </summary>
	public PotentialRating? LatestPotentialRating { get; set; }

    /// <summary>Timestamp of the last cache update from a confirmed rating.</summary>
	public DateTime? RatingLastUpdated { get; set; }

    public DateTime? RemovedDate { get; set; }
    public string? RemovalReason { get; set; }
	
	public bool IsActive { get; set; } = true;
	
	public virtual ICollection<SuccessionCandidate> SuccessionCandidacies { get; set; } = new List<SuccessionCandidate>();
	public virtual ICollection<TalentReviewRating> ReviewRatings { get; set; } = new List<TalentReviewRating>();
	public virtual ICollection<SuccessionDevelopmentActivity> DevelopmentActivities { get; set; } = new List<SuccessionDevelopmentActivity>();
    public virtual ICollection<SuccessionDocument> Documents { get; set; } = new List<SuccessionDocument>();
}

/// <summary>
/// A structured talent review / 9-box calibration session.
/// </summary>
public class TalentReviewSession : TenantEntity
{
	public string SessionName { get; set; } = string.Empty;
	public int ReviewYear { get; set; }

	public DateTime SessionDate { get; set; }
	public string? Location { get; set; }           // Physical location or video link

	// ── Facilitation ─────────────────────────────────────────────────────
	public Guid? FacilitatedById { get; set; }

    [ForeignKey(nameof(FacilitatedById))]
	public virtual Employee? FacilitatedBy { get; set; }
	
    public string? Agenda { get; set; }
	public string? SessionNotes { get; set; }

	/// <summary>Null = company-wide session.</summary>
	public Guid? OrganizationLevelId { get; set; }          // Null = company-wide session

    [ForeignKey(nameof(OrganizationLevelId))]
	public virtual OrganizationLevel? OrganizationLevel { get; set; }

	public Guid? OrganizationUnitId { get; set; }

    [ForeignKey(nameof(OrganizationUnitId))]
    public virtual OrganizationUnit? OrganizationUnit { get; set; }

	// ── Status ───────────────────────────────────────────────────────────
	public bool IsFinalized { get; set; } = false;
	public DateTime? FinalizedDate { get; set; }
	
    public Guid? FinalizedById { get; set; }
	
    [ForeignKey(nameof(FinalizedById))]
    public virtual Employee? FinalizedBy { get; set; }

	// ── Relations ────────────────────────────────────────────────────────
	public virtual ICollection<TalentReviewRating> Ratings { get; set; } = new List<TalentReviewRating>();
}

/// <summary>
/// An individual 9-box rating for one employee within a talent review session.
///
/// <para><b>Previous-rating population:</b><br/>
/// <see cref="PreviousPerformance"/> and <see cref="PreviousPotential"/> are
/// populated by the application layer at save time by looking up the most
/// recent confirmed <see cref="TalentReviewRating"/> for this employee across
/// all prior sessions. <see cref="PreviousRatingSessionId"/> records exactly
/// which session those values came from, making the trend traceable and
/// auditable without relying on implicit ordering. If no prior rating exists,
/// all three fields remain null.
/// </para>
/// </summary>
public class TalentReviewRating : TenantEntity
{
	public Guid SessionId { get; set; }

    [ForeignKey(nameof(SessionId))]
	public virtual TalentReviewSession Session { get; set; } = null!;

	public Guid EmployeeId { get; set; }

    [ForeignKey(nameof(EmployeeId))]
	public virtual Employee Employee { get; set; } = null!;

	// Optional direct link to a talent pool membership
	public Guid? TalentPoolMemberId { get; set; }
    
    [ForeignKey(nameof(TalentPoolMemberId))]
	public virtual TalentPoolMember? TalentPoolMember { get; set; }

	// ── 9-Box Axes ───────────────────────────────────────────────────────
	public PerformanceRating Performance { get; set; }
	public PotentialRating Potential { get; set; }

    /// <summary>
    /// The session from which <see cref="PreviousPerformance"/> and
    /// <see cref="PreviousPotential"/> were sourced. Null for first-time ratings.
    /// </summary>
	public Guid? PreviousRatingSessionId { get; set; }
    
    [ForeignKey(nameof(PreviousRatingSessionId))]
    public virtual TalentReviewSession? PreviousRatingSession  { get; set; }

	// ── Previous Ratings (populated at save time for trend tracking) ──────
	public PerformanceRating? PreviousPerformance { get; set; }
	public PotentialRating? PreviousPotential { get; set; }

	// ── Narrative ────────────────────────────────────────────────────────
	public string? Justification { get; set; }
	public string? KeyStrengths { get; set; }
	public string? DevelopmentPriorities { get; set; }

	// ── Calibration ──────────────────────────────────────────────────────
	public Guid? RatedById { get; set; }

    [ForeignKey(nameof(RatedById))]
	public virtual Employee? RatedBy { get; set; }

	public bool CalibrationConfirmed { get; set; } = false;
	
    public Guid? CalibrationConfirmedById { get; set; }
    
    [ForeignKey(nameof(CalibrationConfirmedById))]
	public virtual Employee? CalibrationConfirmedBy { get; set; }
	
    public DateTime? CalibrationConfirmedDate { get; set; }

	public string? CalibrationNotes { get; set; }

	// ── Relations ────────────────────────────────────────────────────────
	// SuccessionCandidates that reference this rating as their source
	public virtual ICollection<SuccessionCandidate> LinkedCandidates { get; set; } = new List<SuccessionCandidate>();
}
