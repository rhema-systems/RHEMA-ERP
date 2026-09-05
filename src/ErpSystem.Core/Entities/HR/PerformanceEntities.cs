using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.HR.Performance;

/// <summary>
/// Reusable grade descriptor (e.g., "Poor", "Good", "Exceptional").
/// Numeric score ranges are defined on TemplateItemGradeRange (template defaults)
/// and overridden per-item on PositionCriteriaMappingItem via MappingGradeRange.
/// </summary>
public class AppraisalGradeDefinition : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string GradeName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    // ── Overall-rating band (production-readiness Phase C) ──────────────────────
    // Optional configurable mapping of a 0–100 overall appraisal score to a 5-point PerformanceRating.
    // Only the grade definitions used as overall bands need these set; per-item TemplateItemGradeRange
    // is unaffected. When no bands are configured, scoring falls back to AppraisalScoring.MapScoreToRating.

    /// <summary>Inclusive lower bound (0–100) of the overall score band this grade represents, if any.</summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal? OverallMinScore { get; set; }

    /// <summary>Inclusive upper bound (0–100) of the overall score band this grade represents, if any.</summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal? OverallMaxScore { get; set; }

    /// <summary>The 5-point performance rating this overall band maps to (for talent sync / analytics).</summary>
    public PerformanceRating? MappedRating { get; set; }

    public virtual ICollection<TemplateItemGradeRange> TemplateItemGradeRanges { get; set; } = new List<TemplateItemGradeRange>();
}

/// <summary>
/// Defines a KPI with measurement type, unit, and tolerances.
/// Reusable across positions and cycles.
/// </summary>
public class KpiDefinition : TenantEntity
{
    [Required]
    [MaxLength(200)]
    public string KpiName { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public MeasurementType MeasurementType { get; set; } = MeasurementType.NumericAbsolute;

    [MaxLength(50)]
    public string? Unit { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal? TolerancePercent { get; set; }

    public bool IsActive { get; set; } = true;

    public virtual ICollection<EmployeeGoal> GoalsUsingThisKpi { get; set; } = new List<EmployeeGoal>();
}

/// <summary>
/// Competency / soft-skill definition used in appraisal templates.
/// KPI-driven items are defined directly on AppraisalTemplateItem — not here.
/// Weights live in PositionCriteriaMapping or TemplateCriteriaMapping, NOT here.
/// </summary>
public class AppraisalCompetency : TenantEntity
{
    [MaxLength(50)]
    public string? Code { get; set; }

    [Required]
    [MaxLength(200)]
    public string CriteriaName { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public bool RequireEvidence { get; set; } = false;

    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Named, versioned appraisal template (e.g., "Developer Template 2026",
/// "Manager Template 2026"). Each template defines its own set of criteria
/// and weights. Templates are assigned to an appraisal cycle per dept/role.
/// </summary>
public class AppraisalTemplate : TenantEntity
{
    [Required]
    [MaxLength(100)]
    public string TemplateName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }
	
	public Guid? OrganizationLevelId { get; set; }

    public Guid? OrganizationUnitId { get; set; }

    public Guid? PositionId { get; set; }

    public bool IsActive { get; set; } = true;

    // ── Approval workflow (units draft → submit → HR approves/rejects) ──
    public TemplateApprovalStatus ApprovalStatus { get; set; } = TemplateApprovalStatus.Draft;
    public Guid? SubmittedById { get; set; }
    public DateTime? SubmittedDate { get; set; }
    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovalDate { get; set; }
    [MaxLength(1000)]
    public string? RejectionReason { get; set; }

	[ForeignKey(nameof(OrganizationLevelId))]
    public virtual OrganizationLevel? OrganizationLevel { get; set; }

    [ForeignKey(nameof(OrganizationUnitId))]
    public virtual OrganizationUnit? OrganizationUnit { get; set; }

    [ForeignKey(nameof(PositionId))]
    public virtual EmployeePosition? Position { get; set; }

	public virtual ICollection<AppraisalTemplateSection> Sections { get; set; } = new List<AppraisalTemplateSection>();
	public virtual ICollection<AppraisalCycleTemplate> CycleAssignments { get; set; } = new List<AppraisalCycleTemplate>();
}

public class AppraisalTemplateSection : TenantEntity
{
    [Required]
    public Guid AppraisalTemplateId { get; set; }
    
    [Required]
    [MaxLength(200)]
    public string SectionName { get; set; } = string.Empty;
    
    [MaxLength(500)]
    public string? Description { get; set; }
    
    public int DisplayOrder { get; set; }
	
	public int Weight { get; set; } // 0–100, should sum to 100 across sections
    
    [ForeignKey(nameof(AppraisalTemplateId))]
    public virtual AppraisalTemplate AppraisalTemplate { get; set; } = null!;
    
    public virtual ICollection<AppraisalTemplateItem> TemplateItems { get; set; } = new List<AppraisalTemplateItem>();
}

public class AppraisalTemplateItem : TenantEntity
{
    [Required]
    public Guid AppraisalTemplateSectionId { get; set; }
    
    public Guid? CompetencyId { get; set; } // Competency / soft skill

    public Guid? KpiDefinitionId { get; set; } // If KPI-driven

    /// <summary>Default KPI target value for this template item. Overridden by PCM or locked EmployeeGoal.</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? KpiTargetValue { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? KpiMinValue { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? KpiMaxValue { get; set; }

    [MaxLength(500)]
    public string? CustomQuestion { get; set; }

    public int DisplayOrder { get; set; }

    public int Weight { get; set; } // 0–100, should sum to 100 across items in the same section

    [ForeignKey(nameof(AppraisalTemplateSectionId))]
    public virtual AppraisalTemplateSection Section { get; set; } = null!;

    [ForeignKey(nameof(CompetencyId))]
    public virtual AppraisalCompetency? Competency { get; set; }

    [ForeignKey(nameof(KpiDefinitionId))]
    public virtual KpiDefinition? KpiDefinition { get; set; }

    /// <summary>Default grade score bands for this template item criterion. Inherited unless PCM item override exists.</summary>
    public virtual ICollection<TemplateItemGradeRange> GradeRanges { get; set; } = new List<TemplateItemGradeRange>();
}

/// <summary>
/// Default grade score bands defined on a template item criterion.
/// Inherited at appraisal population time unless a PositionCriteriaMappingItem
/// provides a per-scope override via MappingGradeRange.
/// </summary>
public class TemplateItemGradeRange : TenantEntity
{
    [Required]
    public Guid AppraisalTemplateItemId { get; set; }

    [Required]
    public Guid GradeDefinitionId { get; set; }

    public int LowScore { get; set; }

    public int HighScore { get; set; }

    [ForeignKey(nameof(AppraisalTemplateItemId))]
    public virtual AppraisalTemplateItem TemplateItem { get; set; } = null!;

    [ForeignKey(nameof(GradeDefinitionId))]
    public virtual AppraisalGradeDefinition GradeDefinition { get; set; } = null!;
}

/// <summary>
/// Global and period-specific settings for the appraisal process.
/// Linked to one or more AppraisalCycles.
/// </summary>
public class AppraisalSettings : TenantEntity
{
    [Required]
    [MaxLength(100)]
    public string SettingsName { get; set; } = string.Empty;

    // ═══════════════════════════════════════════
    //  SELF-EVALUATION SETTINGS
    // ═══════════════════════════════════════════
    
    public bool RequireSelfEvaluation { get; set; } = true;
    public bool AllowSelfSoftSkillRating { get; set; } = false;

    [Column(TypeName = "decimal(3,2)")]
    public decimal SelfEvaluationWeight { get; set; } = 0.1m;

    // ═══════════════════════════════════════════
    //  PEER EVALUATION SETTINGS
    // ═══════════════════════════════════════════
    
    public bool RequirePeerReviews { get; set; } = false;
    public PeerNominationMode PeerNominationMode { get; set; } = PeerNominationMode.Employee;
    public int MinPeerEvaluators { get; set; } = 1;
    public int MaxPeerEvaluators { get; set; } = 5;
    public bool PeerReviewsAnonymous { get; set; } = true;
    public bool AllowPeerKpiEvaluation { get; set; } = false;

    [Column(TypeName = "decimal(3,2)")]
    public decimal PeerEvaluationWeight { get; set; } = 0.2m;

    /// <summary>
    /// Controls when peer evaluations open relative to self-evaluation.
    /// WithSelfEval = peers and employee evaluate in parallel.
    /// AfterSelfEval = peer evaluation only opens after the employee submits their self-assessment (sequential).
    /// </summary>
    public PeerEvaluationOpenMode PeerEvaluationOpenMode { get; set; } = PeerEvaluationOpenMode.WithSelfEval;

    // ═══════════════════════════════════════════
    //  MANAGER EVALUATION SETTINGS
    // ═══════════════════════════════════════════
    
    public bool RequireManagerEvaluation { get; set; } = true;

    [Column(TypeName = "decimal(3,2)")]
    public decimal ManagerEvaluationWeight { get; set; } = 0.7m;

    public bool IsManagerAuthoritative { get; set; } = true;

    // ═══════════════════════════════════════════
    //  SCORING VISIBILITY CONTROLS
    // ═══════════════════════════════════════════

    /// <summary>
    /// If true, the manager can see the employee's self-rating scores
    /// while completing their own evaluation. If false, self-scores are
    /// hidden from the manager until after they submit (prevents anchoring bias).
    /// </summary>
    public bool ShowSelfScoreToManager { get; set; } = true;

    /// <summary>
    /// If true, aggregated peer evaluation scores are visible to the manager
    /// while completing their evaluation. If false, peer scores are only
    /// revealed after the manager submits their own assessment.
    /// </summary>
    public bool ShowPeerScoresToManager { get; set; } = true;

    /// <summary>
    /// If true, the employee sees the full criterion-by-criterion score
    /// breakdown in their final appraisal view. If false, only the overall
    /// score/grade and narrative comments are shown to the employee.
    /// </summary>
    public bool ShowScoreBreakdownToEmployee { get; set; } = true;
	
    // ═══════════════════════════════════════════
    //  CALIBRATION
    // ═══════════════════════════════════════════
	
    public bool RequireCalibration { get; set; } = true;

    // ═══════════════════════════════════════════
    //  HR REVIEW SETTINGS
    // ═══════════════════════════════════════════
    
    public bool RequireHRReview { get; set; } = true;
    public bool HRCanModifyScores { get; set; } = false;

    /// <summary>
    /// Optional default HR reviewer assigned when an appraisal progresses to HR review. When unset,
    /// the least-loaded active HR employee is chosen. Edited on the Appraisal Settings page.
    /// </summary>
    public Guid? DefaultHRReviewerId { get; set; }

    /// <summary>
    /// Controls whether HR review happens before or after the calibration step.
    /// BeforeCalibration = HR reviews raw manager appraisals before calibration meetings.
    /// AfterCalibration = HR does a final sign-off after calibration, before employee release.
    /// Only relevant when both RequireHRReview and RequireCalibration are true.
    /// </summary>
    public HRReviewTiming HRReviewTiming { get; set; } = HRReviewTiming.AfterCalibration;

    // ═══════════════════════════════════════════
    //  EMPLOYEE ACKNOWLEDGMENT SETTINGS
    // ═══════════════════════════════════════════
    
    public bool RequireEmployeeAcknowledgment { get; set; } = true;
    public bool AllowEmployeeResponse { get; set; } = true;

    /// <summary>
    /// If false, the employee cannot acknowledge the appraisal until the
    /// AppraisalConversation of type FinalReview is marked as completed.
    /// If true, acknowledgment is allowed even without a recorded conversation.
    /// </summary>
    public bool AllowAcknowledgmentWithoutConversation { get; set; } = false;

    // ═══════════════════════════════════════════
    //  APPEAL SETTINGS
    // ═══════════════════════════════════════════
    
    public bool EnableAppeals { get; set; } = true;

    /// <summary>Number of days after employee acknowledgment during which an appeal can be filed.</summary>
    public int AppealWindowDays { get; set; } = 7;
    
    /// <summary>Number of days allowed for HR/reviewer to complete the re-evaluation after appeal submission.</summary>
    public int AppealReevaluationWindowDays { get; set; } = 5;

	// ═══════════════════════════════════════════
	//  GOAL SETTING
	// ═══════════════════════════════════════════
	public bool RequireGoalSetting { get; set; } = true;
	
	/// <summary>
	/// If true, goals must be approved by manager before the performance
	/// period is considered started for that employee.
	/// </summary>
	public bool RequireManagerGoalApproval { get; set; } = true;
	
	/// <summary>Maximum number of goals per employee per cycle.</summary>
	public int? MaxGoalsPerEmployee { get; set; }

	/// <summary>Minimum number of goals per employee per cycle.</summary>
	public int? MinGoalsPerEmployee { get; set; }
	
	// ═══════════════════════════════════════════
	//  CHECK-INS / COACHING
	// ═══════════════════════════════════════════
	
    public bool EnableCheckIns { get; set; } = true;
	public bool EnablePrivateJournal { get; set; } = true;
	
	// ═══════════════════════════════════════════
	//  CONVERSATIONS
	// ═══════════════════════════════════════════
	
    public bool RequireKickOffConversation { get; set; } = false;
	public bool RequireMidYearConversation { get; set; } = false;
	public bool RequireFinalConversation { get; set; } = true;

    // ═══════════════════════════════════════════
    //  REVIEW EVENT SETTINGS
    // ═══════════════════════════════════════════

    /// <summary>
    /// Controls how many interim review events are created within a cycle.
    /// None = year-end only. MidYearOnly = one mid-year checkpoint.
    /// Quarterly = Q1 + Mid-Year + Q3. Custom = HR creates events manually.
    /// </summary>
    public ReviewFrequency ReviewFrequency { get; set; } = ReviewFrequency.MidYearOnly;

    /// <summary>
    /// Whether interim (quarterly/mid-year) review events are light-touch or a full appraisal
    /// against the period's goals/KPIs. See <see cref="InterimReviewDepth"/>.
    /// </summary>
    public InterimReviewDepth InterimReviewDepth { get; set; } = InterimReviewDepth.LightTouch;

    /// <summary>
    /// If true, employees must complete a self-assessment (achievements summary,
    /// challenges, and optionally preliminary self-ratings) during interim
    /// review events. If false, mid-year/quarterly events are manager-driven
    /// with just progress updates and conversation notes.
    /// </summary>
    public bool RequireMidYearSelfAssessment { get; set; } = false;

    /// <summary>
    /// If true, employees must update progress on ALL their active goals
    /// before an interim review event can be submitted/completed.
    /// If false, goal progress updates during review events are optional.
    /// </summary>
    public bool RequireGoalProgressUpdateAtReview { get; set; } = true;

    // ═══════════════════════════════════════════
    //  DEVELOPMENT PLAN SETTINGS
    // ═══════════════════════════════════════════

    /// <summary>
    /// If true, the employee's year-end self-appraisal form includes a mandatory
    /// development plan section and cannot be submitted until all objectives
    /// are updated. If false, the development plan section is optional or hidden
    /// in the year-end review.
    /// </summary>
    public bool RequireDevelopmentPlanUpdate { get; set; } = true;

    // ═══════════════════════════════════════════
    //  DEADLINE ENFORCEMENT
    // ═══════════════════════════════════════════

    /// <summary>
    /// If true, the system automatically locks draft submissions and advances
    /// the appraisal to the next pipeline step when a phase deadline is reached.
    /// For example, an unsubmitted self-evaluation is auto-locked at
    /// SelfEvaluationDeadline, and incomplete peer evaluations are closed at
    /// PeerEvaluationDeadline.
    /// If false, deadlines are advisory only (soft reminders) and HR must
    /// manually advance stalled appraisals.
    /// </summary>
    public bool AutoLockOnDeadline { get; set; } = false;

    // ═══════════════════════════════════════════
    //  OPERATIONAL POLICY DEFAULTS (production-readiness Phase D)
    //  Tenant-configurable constants that were previously hardcoded. Defaults preserve prior behavior.
    // ═══════════════════════════════════════════

    /// <summary>Months a probation period is extended when an Extend-Probation recommendation is actioned.</summary>
    public int ProbationExtensionMonths { get; set; } = 3;

    /// <summary>Pending-appraisal count at/above which a manager is flagged as "high workload" on the HR dashboard.</summary>
    public int ManagerWorkloadThreshold { get; set; } = 10;

    /// <summary>Deadline-risk band: ≤ this many days remaining is "High" risk.</summary>
    public int DeadlineRiskHighDays { get; set; } = 2;
    /// <summary>Deadline-risk band: ≤ this many days remaining is "Medium" risk.</summary>
    public int DeadlineRiskMediumDays { get; set; } = 5;
    /// <summary>Deadline-risk band: ≤ this many days remaining is "Low" risk.</summary>
    public int DeadlineRiskLowDays { get; set; } = 7;

    /// <summary>Name of the talent pool that succession nominations from appraisals are added to.</summary>
    [MaxLength(100)]
    public string SuccessionPoolName { get; set; } = "Appraisal Nominations";

    /// <summary>Default readiness assigned to an employee added to the succession pool from an appraisal.</summary>
    public ReadinessLevel SuccessionDefaultReadiness { get; set; } = ReadinessLevel.ReadyIn12Months;

    public virtual ICollection<AppraisalCycle> AppraisalCycles { get; set; } = new List<AppraisalCycle>();
}

/// <summary>
/// Defines an appraisal period/cycle. HR opens and manages these.
/// Contains phase deadlines for all workflow gates.
/// Nullable dates mean the phase is either not applicable for this cycle
/// or will be set later by HR.
/// </summary>
public class AppraisalCycle : TenantEntity
{
	[MaxLength(50)]
    public string CycleCode { get; set; } = string.Empty;

	[MaxLength(200)]
    public string CycleName { get; set; } = string.Empty;

    [Required]
    public int Year { get; set; }

    public AppraisalType AppraisalType { get; set; }

    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }

	[Required]
    public Guid AppraisalSettingsId { get; set; }

    public AppraisalCycleStatus Status { get; set; } = AppraisalCycleStatus.Draft;
	
    // ═══════════════════════════════════════════
    //  PHASE 1: GOAL SETTING
    // ═══════════════════════════════════════════
	
    public DateOnly? GoalSettingOpenDate { get; set; }
	public DateOnly? GoalSettingDeadline { get; set; }

    // ═══════════════════════════════════════════
    //  PHASE 2: INTERIM REVIEW EVENTS
    //  Which fields are used depends on ReviewFrequency in AppraisalSettings:
    //    None       → all interim dates ignored
    //    MidYearOnly → only MidYear dates used
    //    Quarterly   → Q1, MidYear (Q2), and Q3 dates used
    //    Custom      → dates live on individual AppraisalReviewEvent records
    // ═══════════════════════════════════════════
	
    // --- Q1 Review (used when ReviewFrequency = Quarterly) ---
    public DateOnly? Q1ReviewOpenDate { get; set; }
    public DateOnly? Q1ReviewDeadline { get; set; }

    // --- Mid-Year Review (Q2) ---
    public DateOnly? MidYearOpenDate { get; set; }
    public DateOnly? MidYearDeadline { get; set; }

    // --- Q3 Review (used when ReviewFrequency = Quarterly) ---
    public DateOnly? Q3ReviewOpenDate { get; set; }
    public DateOnly? Q3ReviewDeadline { get; set; }
	
    // ═══════════════════════════════════════════
    //  PHASE 3: FORMAL YEAR-END REVIEW
    //  Steps proceed in pipeline order. The exact sequence depends on
    //  settings (e.g., HRReviewTiming, PeerEvaluationOpenMode).
    //  All dates are nullable — omit dates for steps that are disabled.
    // ═══════════════════════════════════════════

    // --- Step 1: Peer Nomination (if RequirePeerReviews) ---
    /// <summary>
    /// Deadline for peer nominations to be submitted and approved.
    /// Should precede PeerEvaluationOpenDate so peers are confirmed before evaluation begins.
    /// Only relevant when AppraisalSettings.RequirePeerReviews = true.
    /// </summary>
    public DateOnly? PeerNominationDeadline { get; set; }

    // --- Step 2: Self-Evaluation ---
    public DateOnly? SelfEvaluationOpenDate { get; set; }
    public DateOnly? SelfEvaluationDeadline { get; set; }

    // --- Step 3: Peer Evaluation (if RequirePeerReviews) ---
    /// <summary>
    /// When peer evaluation opens. Interpretation depends on PeerEvaluationOpenMode:
    ///   WithSelfEval  → typically matches SelfEvaluationOpenDate
    ///   AfterSelfEval → set to a date after SelfEvaluationDeadline
    /// Only relevant when AppraisalSettings.RequirePeerReviews = true.
    /// </summary>
    public DateOnly? PeerEvaluationOpenDate { get; set; }
    public DateOnly? PeerEvaluationDeadline { get; set; }

    // --- Step 4: Manager Evaluation ---
    public DateOnly? ManagerEvaluationOpenDate { get; set; }
    public DateOnly? ManagerEvaluationDeadline { get; set; }

    // --- Step 5/6: Calibration & HR Review (order depends on HRReviewTiming) ---
    public DateOnly? CalibrationOpenDate { get; set; }
    public DateOnly? CalibrationDeadline { get; set; }

    /// <summary>
    /// When the HR review queue opens. Position relative to calibration depends on
    /// AppraisalSettings.HRReviewTiming (BeforeCalibration or AfterCalibration).
    /// Only relevant when AppraisalSettings.RequireHRReview = true.
    /// </summary>
    public DateOnly? HRReviewOpenDate { get; set; }
    public DateOnly? HRReviewDeadline { get; set; }

    // --- Step 7: Employee Acknowledgment & Final Conversation ---
    public DateOnly? EmployeeAcknowledgeDeadline { get; set; }

    /// <summary>
    /// Deadline by which the final appraisal conversation must be held.
    /// Only enforced when AppraisalSettings.RequireFinalConversation = true.
    /// When AllowAcknowledgmentWithoutConversation = false, the employee
    /// cannot acknowledge until the conversation is completed, so this
    /// deadline effectively gates acknowledgment as well.
    /// </summary>
    public DateOnly? FinalConversationDeadline { get; set; }

    // ═══════════════════════════════════════════
    //  CYCLE MANAGEMENT
    // ═══════════════════════════════════════════
    
    public Guid? OpenedById { get; set; }
    public DateTime? OpenedDate { get; set; }
    public Guid? ClosedById { get; set; }
    public DateTime? ClosedDate { get; set; }

    [ForeignKey(nameof(AppraisalSettingsId))]
    public virtual AppraisalSettings AppraisalSettings { get; set; } = null!;

    [ForeignKey(nameof(OpenedById))]
    public virtual Employee? OpenedBy { get; set; }

    [ForeignKey(nameof(ClosedById))]
    public virtual Employee? ClosedBy { get; set; }

    public virtual ICollection<AppraisalCycleTarget> AppraisalTargets { get; set; } = new List<AppraisalCycleTarget>();
	public virtual ICollection<AppraisalCycleTemplate> TemplateAssignments { get; set; } = new List<AppraisalCycleTemplate>();
	public virtual ICollection<PerformanceAppraisal> PerformanceAppraisals { get; set; } = new List<PerformanceAppraisal>();
    public virtual ICollection<CalibrationSession> CalibrationSessions { get; set; } = new List<CalibrationSession>();
	public virtual ICollection<CompanyGoal> CompanyGoals { get; set; } = new List<CompanyGoal>();
	public virtual ICollection<AppraisalReviewEvent> ReviewEvents { get; set; } = new List<AppraisalReviewEvent>();
}

/// <summary>
/// Defines which groups of employees are included in this appraisal cycle.
/// Population-only: identifies WHICH employees are in scope for the cycle.
/// The applicable appraisal configuration (template/criteria/weights) is resolved
/// separately at appraisal population time via IEffectiveAppraisalConfigurationService.
/// </summary>
public class AppraisalCycleTarget : TenantEntity
{
    [Required]
	public Guid AppraisalCycleId { get; set; }

	/// <summary>
	/// The scope type of this target.
	/// </summary>
	public AppraisalTargetType TargetType { get; set; }
	
	/// <summary>
	/// Shows which org level this target covers (if applicable).
	/// </summary>
	public Guid? OrganizationLevelId { get; set; }

	/// <summary>
	/// Shows which org unit this target covers (if applicable).
	/// </summary>
	public Guid? OrganizationUnitId { get; set; }

	/// <summary>
	/// Shows which position this target covers (if applicable).
	/// </summary>
	public Guid? PositionId { get; set; }
	
	/// <summary>
	/// Preview of affected employee count (computed at creation time).
	/// Updated automatically when exclusions are added/removed.
	/// </summary>
	public int EstimatedEmployeeCount { get; set; }

	/// <summary>
	/// Actual employee count after exclusions.
	/// </summary>
	[NotMapped]
    public int ActiveEmployeeCount { get => EstimatedEmployeeCount - Exclusions.Count(e => e.IsActive); }
	
	/// <summary>
	/// Optional notes about why this target was created or its purpose.
	/// </summary>
	[MaxLength(1000)]
	public string? Notes { get; set; }
	
	/// <summary>
	/// Whether this target is currently active.
	/// </summary>
	public bool IsActive { get; set; } = true;

	[ForeignKey(nameof(AppraisalCycleId))]
	public virtual AppraisalCycle AppraisalCycle { get; set; } = null!;
	
	[ForeignKey(nameof(OrganizationLevelId))]
	public virtual OrganizationLevel? OrganizationLevel { get; set; }

	[ForeignKey(nameof(OrganizationUnitId))]
	public virtual OrganizationUnit? OrganizationUnit { get; set; }

	[ForeignKey(nameof(PositionId))]
	public virtual EmployeePosition? Position { get; set; }

	public virtual ICollection<AppraisalCycleTargetExclusion> Exclusions { get; set; } = new List<AppraisalCycleTargetExclusion>();
}

/// <summary>
/// Exclusions on top of a selected mapping (e.g., exclude specific employees or units).
/// </summary>
public class AppraisalCycleTargetExclusion : TenantEntity
{
    [Required]
	public Guid AppraisalCycleTargetId { get; set; }
	
	public Guid? OrganizationLevelId { get; set; }
    
	public Guid? OrganizationUnitId { get; set; }
    
	public Guid? PositionId { get; set; }
    
	public Guid? EmployeeId { get; set; }
	
	/// <summary>
	/// Required explanation for the exclusion.
	/// </summary>
	[Required]
	[MaxLength(500)]
	public string Reason { get; set; } = string.Empty;
	
	/// <summary>
	/// Whether this exclusion is still active.
	/// </summary>
	public bool IsActive { get; set; } = true;

    [ForeignKey(nameof(AppraisalCycleTargetId))]
	public virtual AppraisalCycleTarget Target { get; set; } = null!;
	
	[ForeignKey(nameof(OrganizationLevelId))]
	public virtual OrganizationLevel? OrganizationLevel { get; set; }
    
	[ForeignKey(nameof(OrganizationUnitId))]
	public virtual OrganizationUnit? OrganizationUnit { get; set; }
    
	[ForeignKey(nameof(PositionId))]
	public virtual EmployeePosition? Position { get; set; }
    
	[ForeignKey(nameof(EmployeeId))]
	public virtual Employee? Employee { get; set; }
}

/// <summary>
/// Assigns a template to an appraisal cycle.
/// Scope (org level / unit / position) is defined once on <see cref="AppraisalTemplate"/>.
/// Resolution reads scope from the linked template, not from this record.
/// </summary>
public class AppraisalCycleTemplate : TenantEntity
{
    [Required]
    public Guid AppraisalCycleId { get; set; }

    [Required]
    public Guid AppraisalTemplateId { get; set; }

    /// <summary>Higher value wins when multiple templates could apply to the same employee.</summary>
    public int Priority { get; set; } = 0;

    public bool IsActive { get; set; } = true;

    [ForeignKey(nameof(AppraisalCycleId))]
    public virtual AppraisalCycle AppraisalCycle { get; set; } = null!;

    [ForeignKey(nameof(AppraisalTemplateId))]
    public virtual AppraisalTemplate AppraisalTemplate { get; set; } = null!;
}

/// <summary>
/// Pre-populated library of SMART goals / OKRs that HR can make available
/// for employees or managers to reference when creating individual goals.
/// Optionally scoped to a position or department.
/// </summary>
public class GoalLibrary : TenantEntity
{
    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }
	
	/// <summary>How success is measured for this goal.</summary>
	[MaxLength(1000)]
    public string? SuccessCriteria { get; set; }
	
	public Guid? OrganizationLevelId { get; set; }
	
	public Guid? OrganizationUnitId { get; set; }

    public Guid? PositionId { get; set; }

    public bool IsActive { get; set; } = true;
	
	[ForeignKey(nameof(OrganizationLevelId))]
    public virtual OrganizationLevel? OrganizationLevel { get; set; }
	
	[ForeignKey(nameof(OrganizationUnitId))]
    public virtual OrganizationUnit? OrganizationUnit { get; set; }

    [ForeignKey(nameof(PositionId))]
    public virtual EmployeePosition? Position { get; set; }

    public virtual ICollection<EmployeeGoal> EmployeeGoals { get; set; } = new List<EmployeeGoal>(); // Instances from library
}

/// <summary>
/// Multi-year company strategic goal / vision. Spans several years and is NOT tied to a single
/// appraisal cycle. Top of the cascading goal hierarchy. Cycle-bound <see cref="CompanyGoal"/>s
/// ("yearly objectives") are derived from these. HR / senior leadership create these.
/// </summary>
public class StrategicGoal : TenantEntity
{
	[Required, MaxLength(300)]
	public string Title { get; set; } = string.Empty;

	[MaxLength(2000)]
	public string? Description { get; set; }

	[MaxLength(1000)]
	public string? SuccessCriteria { get; set; }

	public GoalPriority Priority { get; set; } = GoalPriority.High;

	/// <summary>First year the strategic goal is in effect (e.g. 2026).</summary>
	public int StartYear { get; set; }

	/// <summary>Last year the strategic goal is in effect (e.g. 2030).</summary>
	public int EndYear { get; set; }

	public bool IsActive { get; set; } = true;

	/// <summary>Cycle-bound yearly objectives derived from this strategic goal.</summary>
	public virtual ICollection<CompanyGoal> CompanyGoals { get; set; } = new List<CompanyGoal>();
}

/// <summary>
/// Company-level yearly objective for a cycle, optionally derived from a multi-year
/// <see cref="StrategicGoal"/>. A node in the cascading goal hierarchy.
/// HR or senior leadership creates these.
/// </summary>
public class CompanyGoal : TenantEntity
{
	[Required]
	public Guid AppraisalCycleId { get; set; }

	/// <summary>The multi-year strategic goal this yearly objective is derived from (optional —
	/// null for organizations that define direct yearly objectives without a long-term layer).</summary>
	public Guid? StrategicGoalId { get; set; }

	[Required, MaxLength(300)]
	public string Title { get; set; } = string.Empty;

	[MaxLength(2000)]
	public string? Description { get; set; }

	[MaxLength(1000)]
	public string? SuccessCriteria { get; set; }

	public GoalPriority Priority { get; set; } = GoalPriority.High;

	[Column(TypeName = "decimal(18,4)")]
	public decimal? TargetValue { get; set; }
	
	[MaxLength(50)]
	public string? Unit { get; set; } // unit of measurement

	public DateOnly? DueDate { get; set; }

	public bool IsVisible { get; set; } = true;  // Visible to all employees?

	[ForeignKey(nameof(AppraisalCycleId))]
	public virtual AppraisalCycle AppraisalCycle { get; set; } = null!;

	[ForeignKey(nameof(StrategicGoalId))]
	public virtual StrategicGoal? StrategicGoal { get; set; }

	public virtual ICollection<UnitGoal> UnitGoals { get; set; } = new List<UnitGoal>();
	public virtual ICollection<EmployeeGoal> EmployeeGoals { get; set; } = new List<EmployeeGoal>();
}

/// <summary>
/// Unit-level goal cascaded from a CompanyGoal.
/// Managers create these for their units; employees can further cascade down.
/// </summary>
public class UnitGoal : TenantEntity
{
	[Required]
	public Guid AppraisalCycleId { get; set; }

	/// <summary>The company-level parent goal this is cascaded from.</summary>
	public Guid? ParentCompanyGoalId { get; set; }

	/// <summary>Optional parent unit goal — lets a department goal cascade down to the units under it.</summary>
	public Guid? ParentUnitGoalId { get; set; }

	[Required]
	public Guid OrganizationLevelId { get; set; }

	[Required]
	public Guid OrganizationUnitId { get; set; }

	[Required]
	public Guid CreatedByManagerId { get; set; }

	[Required, MaxLength(300)]
	public string Title { get; set; } = string.Empty;

	[MaxLength(2000)]
	public string? Description { get; set; }

	[MaxLength(1000)]
	public string? SuccessCriteria { get; set; }

	public GoalPriority Priority { get; set; } = GoalPriority.High;

	[Column(TypeName = "decimal(18,4)")]
	public decimal? TargetValue { get; set; }
	
	[MaxLength(50)]
	public string? Unit { get; set; } // unit of measurement

	public DateOnly? DueDate { get; set; }

	[ForeignKey(nameof(AppraisalCycleId))]
	public virtual AppraisalCycle AppraisalCycle { get; set; } = null!;

	[ForeignKey(nameof(ParentCompanyGoalId))]
	public virtual CompanyGoal? ParentCompanyGoal { get; set; }

	[ForeignKey(nameof(ParentUnitGoalId))]
	public virtual UnitGoal? ParentUnitGoal { get; set; }

	public virtual ICollection<UnitGoal> ChildUnitGoals { get; set; } = new List<UnitGoal>();

	[ForeignKey(nameof(OrganizationLevelId))]
	public virtual OrganizationLevel OrganizationLevel { get; set; } = null!;

	[ForeignKey(nameof(OrganizationUnitId))]
	public virtual OrganizationUnit OrganizationUnit { get; set; } = null!;

	[ForeignKey(nameof(CreatedByManagerId))]
	public virtual Employee CreatedByManager { get; set; } = null!;

	public virtual ICollection<EmployeeGoal> EmployeeGoals { get; set; } = new List<EmployeeGoal>();
	public virtual ICollection<AppraisalAttachment> Attachments { get; set; } = new List<AppraisalAttachment>();
}

/// <summary>
/// Individual SMART goal / OKR proposed by the employee or assigned by the manager.
/// Follows an approval workflow before becoming active.
/// Can be linked to a GoalLibraryItem, DepartmentGoal, or CompanyGoal.
/// </summary>
public class EmployeeGoal : TenantEntity
{
	[Required]
    public Guid EmployeeId { get; set; }
	
	[Required]
	public Guid AppraisalCycleId { get; set; }
	
	/// <summary>The performance appraisal this goal belongs to (once appraisal is created).</summary>
	public Guid? PerformanceAppraisalId { get; set; }
	
	// Cascading — separate FK per parent type to avoid composite FK constraint conflicts
    public Guid? CompanyGoalId { get; set; }   // parent is a CompanyGoal
    public Guid? UnitGoalId    { get; set; }   // parent is a UnitGoal
    public Guid? ParentGoalId  { get; set; }   // self-ref: parent is another EmployeeGoal

    /// <summary>Denormalized convenience field — computed by the service from whichever FK is populated.</summary>
    public GoalParentType? ParentType { get; set; }

	/// <summary>If sourced from the goal library.</summary>
	public Guid? GoalLibraryId { get; set; }
	
	public Guid? KpiDefinitionId { get; set; } // optional — if this goal is KPI-measured

    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }
	
	[MaxLength(1000)]
    public string? SuccessCriteria { get; set; }
	
	/// <summary>
	/// Weight of this goal in the overall appraisal (0–100).
	/// Must be managed alongside criteria weights.
	/// </summary>
    public int Weight { get; set; }
    
	public GoalPriority Priority { get; set; } = GoalPriority.Medium;

    public GoalStatus Status { get; set; } = GoalStatus.Draft; // Enum: Proposed, Approved, Locked, AtRisk, etc.
	
	// --- Measurement ---
	public MeasurementType MeasurementType { get; set; } = MeasurementType.NumericAbsolute;

	/// <summary>The period within the cycle this goal/KPI target applies to (Theme 7 — supports
	/// full quarterly/half-year appraisals against period-scoped targets).</summary>
	public GoalPeriod Period { get; set; } = GoalPeriod.FullCycle;

	[Column(TypeName = "decimal(18,2)")]
	public decimal? TargetValue { get; set; }

	[Column(TypeName = "decimal(18,2)")]
	public decimal? MinValue { get; set; }

	[Column(TypeName = "decimal(18,2)")]
	public decimal? MaxValue { get; set; }

	[MaxLength(50)]
	public string? Unit { get; set; } // unit of measurement

    public DateOnly StartDate { get; set; }
    public DateOnly DueDate { get; set; }
	
	[Column(TypeName = "decimal(5,2)")]
    public decimal ProgressPercent { get; set; } = 0; // For ongoing updates
	
	// Approval flow
    public Guid? SubmittedToManagerId { get; set; }
    public DateTime? SubmittedDate { get; set; }
	public DateTime? ApprovalDate { get; set; }
    
	[MaxLength(1000)]
	public string? ManagerFeedback { get; set; }  // Approval/rejection notes

    public bool IsLocked { get; set; } = false; // Finalize and lock
	
	public DateTime? LockedDate { get; set; } // When goals are locked after approval

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;
	
	[ForeignKey(nameof(AppraisalCycleId))]
    public virtual AppraisalCycle? AppraisalCycle { get; set; }
	
	[ForeignKey(nameof(PerformanceAppraisalId))]
	public virtual PerformanceAppraisal? Appraisal { get; set; }

    [ForeignKey(nameof(CompanyGoalId))]
    public virtual CompanyGoal? ParentCompanyGoal { get; set; }

    [ForeignKey(nameof(UnitGoalId))]
    public virtual UnitGoal? ParentUnitGoal { get; set; }

    [ForeignKey(nameof(ParentGoalId))]
    public virtual EmployeeGoal? ParentGoal { get; set; }

    [ForeignKey(nameof(GoalLibraryId))]
    public virtual GoalLibrary? LibraryItem { get; set; }
	
	[ForeignKey(nameof(KpiDefinitionId))]
	public virtual KpiDefinition? KpiDefinition { get; set; }
	
	[ForeignKey(nameof(SubmittedToManagerId))]
    public virtual Employee? Manager { get; set; }

    public virtual ICollection<GoalProgressEntry> ProgressEntries { get; set; } = new List<GoalProgressEntry>(); // Ongoing tracking
	public virtual ICollection<PerformanceJournalEntry> JournalEntries { get; set; } = new List<PerformanceJournalEntry>();
    public virtual ICollection<EmployeeGoalAppraisalAssessment> AppraisalAssessments { get; set; } = new List<EmployeeGoalAppraisalAssessment>();
    public virtual ICollection<GoalRequiredSkill> RequiredSkills { get; set; } = new List<GoalRequiredSkill>();
}

/// <summary>
/// A competency / soft skill an employee needs to develop in order to achieve a specific goal.
/// Drives development-plan suggestions and training nominations.
/// </summary>
public class GoalRequiredSkill : TenantEntity
{
    [Required]
    public Guid EmployeeGoalId { get; set; }

    [Required]
    public Guid CompetencyId { get; set; }

    /// <summary>True if the employee needs development/training in this skill to hit the goal.</summary>
    public bool DevelopmentNeeded { get; set; } = true;

    [MaxLength(500)]
    public string? Note { get; set; }

    [ForeignKey(nameof(EmployeeGoalId))]
    public virtual EmployeeGoal EmployeeGoal { get; set; } = null!;

    [ForeignKey(nameof(CompetencyId))]
    public virtual AppraisalCompetency Competency { get; set; } = null!;
}

/// <summary>
/// Stores the employee's year-end self-assessment and the manager's assessment for a goal
/// within a specific appraisal instance. Keyed by (EmployeeGoalId, PerformanceAppraisalId).
/// </summary>
public class EmployeeGoalAppraisalAssessment : TenantEntity
{
    [Required]
    public Guid EmployeeGoalId { get; set; }

    [Required]
    public Guid PerformanceAppraisalId { get; set; }

    // ── Employee self-assessment ──────────────────────────────────────────────
    [Column(TypeName = "decimal(5,2)")]
    public decimal? SelfFinalProgressPercent { get; set; }

    public GoalProgressStatus? SelfFinalStatus { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? SelfFinalActualValue { get; set; }

    [MaxLength(2000)]
    public string? SelfAssessmentNotes { get; set; }

    [MaxLength(1000)]
    public string? SelfEvidenceLinks { get; set; }

    // ── Manager assessment ────────────────────────────────────────────────────
    [Column(TypeName = "decimal(5,2)")]
    public decimal? ManagerFinalProgressPercent { get; set; }

    public GoalProgressStatus? ManagerFinalStatus { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? ManagerFinalActualValue { get; set; }

    [MaxLength(2000)]
    public string? ManagerAssessmentNotes { get; set; }

    [MaxLength(1000)]
    public string? ManagerEvidenceLinks { get; set; }

    [ForeignKey(nameof(EmployeeGoalId))]
    public virtual EmployeeGoal Goal { get; set; } = null!;

    [ForeignKey(nameof(PerformanceAppraisalId))]
    public virtual PerformanceAppraisal Appraisal { get; set; } = null!;
}

/// <summary>
/// A time-stamped progress update on an EmployeeGoal.
/// Can be made by the employee or the manager.
/// Provides the full history of goal progress throughout the cycle.
/// </summary>
public class GoalProgressEntry : TenantEntity
{
	[Required]
	public Guid EmployeeGoalId { get; set; }
	
	[Column(TypeName = "decimal(5,2)")]
	public decimal? ProgressPercent { get; set; }

	[Column(TypeName = "decimal(18,2)")]
	public decimal? ActualValue { get; set; }
	
	public GoalProgressStatus Status { get; set; }
	
	[MaxLength(500)]
    public string? Challenges { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
	
	[Required]
	public Guid RecordedById { get; set; }
	
	public DateTime EntryDate { get; set; } = DateTime.UtcNow;
	
	public Guid? ReviewEventId { get; set; }  // If progress was logged during a formal mid-year/quarterly event

    [ForeignKey(nameof(EmployeeGoalId))]
    public virtual EmployeeGoal? EmployeeGoal { get; set; }

    [ForeignKey(nameof(RecordedById))]
    public virtual Employee? RecordedBy { get; set; }
	
	[ForeignKey(nameof(ReviewEventId))] 
    public virtual AppraisalReviewEvent? ReviewEvent { get; set; }
}

/// <summary>
/// A coaching interaction between manager and employee (or peer to peer)
/// outside of the formal review cycle. Includes ad-hoc feedback, 1:1 meeting
/// notes, and goal progress discussions.
/// </summary>
public class CheckIn : TenantEntity
{
	[Required]
	public Guid AppraisalCycleId { get; set; }
	
	/// <summary>The employee being coached/receiving feedback.</summary>
    [Required]
    public Guid EmployeeId { get; set; }
    
	/// <summary>The person giving the feedback / running the meeting.</summary>
	[Required]
	public Guid ConductedById { get; set; }
    
    public CheckInType CheckInType { get; set; }
    
    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;
    
    public DateTime ScheduledDate { get; set; }
    public DateTime? ConductedDate { get; set; }
    
    [MaxLength(2000)]
    public string? Agenda { get; set; }
    
	/// <summary>Main notes for the check-in (shared with employee).</summary>
	[MaxLength(4000)]
	public string? SharedNotes { get; set; }
	
	/// <summary>
	/// Private notes — visible only to the person who created the check-in.
	/// Useful for manager's personal observations.
	/// </summary>
	[MaxLength(4000)]
	public string? PrivateNotes { get; set; }
    
	/// <summary>Action items agreed upon during this session.</summary>
	[MaxLength(2000)]
	public string? ActionItems { get; set; }
	
	/// <summary>Next steps or follow-up date.</summary>
	public DateTime? FollowUpDate { get; set; }
    
    [MaxLength(2000)]
    public string? EmployeeComments { get; set; }
	
	[ForeignKey(nameof(AppraisalCycleId))]
	public virtual AppraisalCycle Cycle { get; set; } = null!;
    
	[ForeignKey(nameof(EmployeeId))]
	public virtual Employee Employee { get; set; } = null!;
    
	[ForeignKey(nameof(ConductedById))]
	public virtual Employee ConductedBy { get; set; } = null!;
    
    public virtual ICollection<CheckInGoalUpdate> GoalUpdates { get; set; } = new List<CheckInGoalUpdate>();
	public virtual ICollection<AppraisalAttachment> Attachments { get; set; } = new List<AppraisalAttachment>();
    public virtual ICollection<CheckInObjectiveLink> ObjectiveLinks { get; set; } = new List<CheckInObjectiveLink>();
}

/// <summary>
/// Links a check-in to a company yearly objective (CompanyGoal) that anchors and directs
/// the coaching conversation.
/// </summary>
public class CheckInObjectiveLink : TenantEntity
{
    [Required]
    public Guid CheckInId { get; set; }

    [Required]
    public Guid CompanyGoalId { get; set; }

    [ForeignKey(nameof(CheckInId))]
    public virtual CheckIn CheckIn { get; set; } = null!;

    [ForeignKey(nameof(CompanyGoalId))]
    public virtual CompanyGoal CompanyGoal { get; set; } = null!;
}

/// <summary>
/// A tracked follow-up action recommended by an appraisal (Theme 8 backbone). Has an explicit
/// lifecycle; on approval it is dispatched to the owning module to create a real downstream
/// record, with a back-link captured in <see cref="TargetEntityType"/> + <see cref="TargetEntityId"/>.
/// Generalizes the existing appraisal→PIP handoff so no recommendation is a dead-end flag.
/// </summary>
public class AppraisalOutcomeRecommendation : TenantEntity
{
    [Required]
    public Guid PerformanceAppraisalId { get; set; }

    public RecommendationType RecommendationType { get; set; }

    public RecommendationStatus Status { get; set; } = RecommendationStatus.Proposed;

    public Guid? RecommendedById { get; set; }
    public DateTime? RecommendedDate { get; set; }

    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedDate { get; set; }

    public DateTime? ActionedDate { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [MaxLength(1000)]
    public string? ResolutionNotes { get; set; }

    /// <summary>The downstream entity type created when actioned (e.g. "TrainingNomination").</summary>
    [MaxLength(100)]
    public string? TargetEntityType { get; set; }

    /// <summary>The id of the created downstream record (set when actioned). Provides the back-link.</summary>
    public Guid? TargetEntityId { get; set; }

    [ForeignKey(nameof(PerformanceAppraisalId))]
    public virtual PerformanceAppraisal PerformanceAppraisal { get; set; } = null!;
}

/// <summary>
/// A pay-for-performance proposal (merit increase or bonus) raised from an appraisal outcome
/// recommendation (Theme 11). A lightweight intake record handed off to HR/payroll for approval
/// before it affects actual pay — keeps appraisal outcomes from dead-ending.
/// </summary>
public class SalaryReviewProposal : TenantEntity
{
    [Required]
    public Guid EmployeeId { get; set; }

    /// <summary>The appraisal that produced this proposal (provenance).</summary>
    public Guid? SourceAppraisalId { get; set; }

    public SalaryReviewProposalType ProposalType { get; set; }

    /// <summary>Suggested merit increase percentage (for MeritIncrease).</summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal? ProposedPercent { get; set; }

    /// <summary>Suggested bonus amount (for Bonus).</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? ProposedAmount { get; set; }

    public SalaryReviewProposalStatus Status { get; set; } = SalaryReviewProposalStatus.Proposed;

    [MaxLength(1000)]
    public string? Notes { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    [ForeignKey(nameof(SourceAppraisalId))]
    public virtual PerformanceAppraisal? SourceAppraisal { get; set; }
}

/// <summary>
/// Lightweight intake record raised when an appraisal outcome recommendation of an employment-action type
/// (Promotion / Demotion / ContractRenewal / Termination / Recognition) is approved. It captures the intent
/// so HR can action it in the destination module — avoiding the need to supply a heavyweight target
/// container (target position, award type, etc.) at approval time. Mirrors <see cref="SalaryReviewProposal"/>.
/// </summary>
public class EmploymentActionProposal : TenantEntity
{
    [Required]
    public Guid EmployeeId { get; set; }

    /// <summary>The appraisal that produced this proposal (provenance).</summary>
    public Guid? SourceAppraisalId { get; set; }

    public EmploymentActionType ActionType { get; set; }

    public EmploymentActionProposalStatus Status { get; set; } = EmploymentActionProposalStatus.Proposed;

    [MaxLength(1000)]
    public string? Notes { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    [ForeignKey(nameof(SourceAppraisalId))]
    public virtual PerformanceAppraisal? SourceAppraisal { get; set; }
}

/// <summary>
/// A goal progress update recorded within a check-in session.
/// Provides the linkage between a coaching event and specific goals.
/// </summary>
public class CheckInGoalUpdate : TenantEntity
{
    [Required]
    public Guid CheckInId { get; set; }
    
    [Required]
    public Guid EmployeeGoalId { get; set; }
	
	[Column(TypeName = "decimal(5,2)")]
	public decimal? UpdatedProgress { get; set; }
	
	public GoalProgressStatus UpdatedStatus { get; set; }
    
	public bool FlaggedAtRisk { get; set; } = false;
	
	[MaxLength(2000)]
	public string? Note { get; set; }
    
    [ForeignKey(nameof(CheckInId))]
    public virtual CheckIn CheckIn { get; set; } = null!;
    
    [ForeignKey(nameof(EmployeeGoalId))]
    public virtual EmployeeGoal EmployeeGoal { get; set; } = null!;
}

/// <summary>
/// A private journal entry for an employee OR manager to log achievements,
/// observations, or notes throughout the performance year.
/// </summary>
public class PerformanceJournalEntry : TenantEntity
{
	[Required]
	public Guid AppraisalCycleId { get; set; }
	
	/// <summary>The employee whose journal this is.</summary>
	[Required]
	public Guid OwnerId { get; set; }
	
	/// <summary>
	/// If journaling ABOUT a direct report (manager's view), link the subject employee.
	/// Null if journaling about oneself.
	/// </summary>
	public Guid? SubjectEmployeeId { get; set; }
	
	/// <summary>Link to a goal if this entry is about a specific goal.</summary>
	public Guid? RelatedGoalId { get; set; }
	
	[Required]
	[MaxLength(300)]
	public string Title { get; set; } = string.Empty;
	
	[MaxLength(4000)]
	public string Body { get; set; } = string.Empty;
	
	public DateTime EntryDate { get; set; }
    
    public bool IsPrivate { get; set; } = true; // Can be shared with manager
	
	[ForeignKey(nameof(AppraisalCycleId))]
	public virtual AppraisalCycle AppraisalCycle { get; set; } = null!;
	
	[ForeignKey(nameof(OwnerId))]
	public virtual Employee Owner { get; set; } = null!;
    
	[ForeignKey(nameof(SubjectEmployeeId))]
	public virtual Employee? SubjectEmployee { get; set; }
    
	[ForeignKey(nameof(RelatedGoalId))]
	public virtual EmployeeGoal? RelatedGoal { get; set; }
}

/// <summary>
/// The central appraisal record for a single employee in a single cycle.
/// Orchestrates all phases: self-eval, peer, manager, calibration, HR, acknowledgment.
/// </summary>
public class PerformanceAppraisal : TenantEntity
{
	[Required]
    public Guid AppraisalCycleId { get; set; }

    [MaxLength(50)]
    public string AppraisalNumber { get; set; } = string.Empty;

	[Required]
    public Guid EmployeeId { get; set; }
	
	/// <summary>The template resolved for this employee.</summary>
	public Guid? AppraisalTemplateId { get; set; }

    public int Year { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }

    public AppraisalStatus Status { get; set; } = AppraisalStatus.Draft; // 6-value lifecycle: Draft→Active→Governance→Appealed→Completed→Closed
	
	public Guid? DevelopmentPlanId { get; set; } // Main annual development plan
	
	// --- Calibration Gate ---
	
	/// <summary>
	/// Whether this appraisal's calibration review has been completed.
	/// Set by HR after the calibration session for this employee's dept.
	/// </summary>
	public bool IsCalibrated { get; set; } = false;

	public Guid? CalibrationSessionId { get; set; }

	// --- Peer ---
    public int PeerEvaluatorsCount { get; set; } = 0;

	// --- Scores ---
    [Column(TypeName = "decimal(5,2)")]
    public decimal? OverallScore { get; set; }
	
	/// <summary>
	/// Pre-calibration score (manager's proposed score before calibration adjustments).
	/// Preserved for audit / before-after comparison.
	/// </summary>
	[Column(TypeName = "decimal(5,2)")]
	public decimal? PreCalibrationScore { get; set; }

	/// <summary>
	/// Score after a successful appeal resolution.
	/// Set by HR when resolving an appeal that warrants a score change.
	/// Null when no appeal has been adjudicated with a score adjustment.
	/// </summary>
	[Column(TypeName = "decimal(5,2)")]
	public decimal? AdjustedScore { get; set; }
	
	/// <summary>Resolved overall rating band (populated after scoring).</summary>
	public Guid? OverallGradeDefinitionId { get; set; }

    // Rank among peers in same position
    public int? RankInPosition { get; set; }
    public int? RankInUnit { get; set; }

	// --- Recommendations ---
	public bool RecommendAward { get; set; }
    public bool RecommendPromotion { get; set; }
    public bool RecommendIncrement { get; set; }
    public bool RecommendTraining { get; set; }
    public bool RecommendPIP { get; set; }
    public bool RecommendTermination { get; set; }

    [MaxLength(2000)]
    public string? RecommendationNotes { get; set; }
	
	// --- Narrative ---
	[MaxLength(2000)]
    public string? OverallComments { get; set; }

    [MaxLength(2000)]
    public string? StrengthsIdentified { get; set; }

    [MaxLength(2000)]
    public string? AreasForImprovement { get; set; }

    [MaxLength(2000)]
    public string? TrainingNeeds { get; set; } // Ties to development plan

    [MaxLength(2000)]
    public string? CareerAspirations { get; set; } // Ties to development plan
	
	// --- Scheduling ---
	public DateOnly? NextAppraisalDate { get; set; }

	// --- Employee Acknowledgment ---
	public bool EmployeeAcknowledged { get; set; } = false;

    public DateTime? EmployeeAcknowledgedDate { get; set; }
    
	[MaxLength(2000)]
    public string? EmployeeAcknowledgmentComments { get; set; }

	// --- Appeal State ---
    public bool HasAppeal { get; set; } = false;
    public AppraisalAppealStatus? CurrentAppealStatus { get; set; }
    public DateTime? AppealRemandDeadline { get; set; }
    public DateTime? AppealRemandedDate { get; set; }

    [ForeignKey(nameof(AppraisalCycleId))]
    public virtual AppraisalCycle AppraisalCycle { get; set; } = null!;
	
	[ForeignKey(nameof(DevelopmentPlanId))] 
    public virtual EmployeeDevelopmentPlan? DevelopmentPlan { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;
	
    [ForeignKey(nameof(AppraisalTemplateId))]
    public virtual AppraisalTemplate? Template { get; set; }
	
	[ForeignKey(nameof(OverallGradeDefinitionId))]
	public virtual AppraisalGradeDefinition? OverallGrade { get; set; }

	[ForeignKey(nameof(CalibrationSessionId))]
	public virtual CalibrationSession? CalibrationSession { get; set; }

    public virtual ICollection<EvaluatorEvaluation> EvaluatorEvaluations { get; set; } = new List<EvaluatorEvaluation>();
    public virtual ICollection<AppraisalEmployeeResponse> EmployeeResponses { get; set; } = new List<AppraisalEmployeeResponse>();
    public virtual ICollection<AppraisalCustomQuestionResponse> CustomQuestionResponses { get; set; } = new List<AppraisalCustomQuestionResponse>();
    public virtual ICollection<AppraisalAppeal> Appeals { get; set; } = new List<AppraisalAppeal>();
    public virtual ICollection<AppraisalAttachment> Attachments { get; set; } = new List<AppraisalAttachment>();
    public virtual ICollection<PeerNomination> PeerNominations { get; set; } = new List<PeerNomination>();
	public virtual ICollection<EmployeeGoal> Goals { get; set; } = new List<EmployeeGoal>();
    public virtual ICollection<AppraisalConversation> Conversations { get; set; } = new List<AppraisalConversation>();
	public virtual ICollection<AppraisalHRReview> HRReviews { get; set; } = new List<AppraisalHRReview>();
    public virtual ICollection<AppraisalEvaluationSnapshot> EvaluationSnapshots { get; set; } = new List<AppraisalEvaluationSnapshot>();
	public virtual ICollection<AppraisalReviewEvent> ReviewEvents { get; set; } = new List<AppraisalReviewEvent>();  // ← All mid-year/quarterly events link here
    /// <summary>Snapshot of the resolved criteria config at the time this appraisal was populated.</summary>
    public virtual ICollection<PerformanceAppraisalCriterionConfig> CriterionConfigs { get; set; } = new List<PerformanceAppraisalCriterionConfig>();
    public virtual ICollection<AppraisalManualAdvanceLog> ManualAdvanceLogs { get; set; } = new List<AppraisalManualAdvanceLog>();
}

/// <summary>
/// Audit record created every time HR manually advances an appraisal past a stalled pipeline step.
/// Documents who advanced, from/to sub-status, from/to major status, reason, and the data actions performed.
/// </summary>
public class AppraisalManualAdvanceLog : TenantEntity
{
    [Required]
    public Guid PerformanceAppraisalId { get; set; }

    [Required]
    public Guid AdvancedByEmployeeId { get; set; }

    public DateTime AdvancedDate { get; set; }

    [Required, MaxLength(50)]
    public string FromSubStatus { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string ToSubStatus { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? FromMajorStatus { get; set; }

    [MaxLength(50)]
    public string? ToMajorStatus { get; set; }

    [Required, MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;

    /// <summary>JSON-serialized List&lt;string&gt; of data-mutation actions performed (e.g., "Auto-submitted self-eval").</summary>
    [MaxLength(4000)]
    public string? ActionsPerformedJson { get; set; }

    [ForeignKey(nameof(PerformanceAppraisalId))]
    public virtual PerformanceAppraisal Appraisal { get; set; } = null!;

    [ForeignKey(nameof(AdvancedByEmployeeId))]
    public virtual Employee AdvancedBy { get; set; } = null!;
}

/// <summary>
/// Snapshot of a single criterion's effective configuration for a specific appraisal.
/// Created when the appraisal is populated. Captures which weight and source type were used
/// so that future changes to templates/PCM do not affect historical appraisals.
/// </summary>
public class PerformanceAppraisalCriterionConfig : TenantEntity
{
    [Required]
    public Guid PerformanceAppraisalId { get; set; }

    [Required]
    public Guid TemplateItemId { get; set; }

    /// <summary>Effective weight used (copied from the template item).</summary>
    public int WeightUsed { get; set; }

    /// <summary>Snapshotted KPI target value at appraisal population time (after goal/template resolution).</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? KpiTargetValue { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? KpiMinValue { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? KpiMaxValue { get; set; }

    /// <summary>Indicates which tier of the resolution chain provided the KPI target.</summary>
    public KpiTargetSource? KpiTargetSource { get; set; }

    [ForeignKey(nameof(PerformanceAppraisalId))]
    public virtual PerformanceAppraisal PerformanceAppraisal { get; set; } = null!;

    [ForeignKey(nameof(TemplateItemId))]
    public virtual AppraisalTemplateItem TemplateItem { get; set; } = null!;

    /// <summary>Snapshot of the effective grade bands for this criterion in this appraisal.</summary>
    public virtual ICollection<PerformanceAppraisalCriterionConfigGradeRange> GradeRanges { get; set; } = new List<PerformanceAppraisalCriterionConfigGradeRange>();
}

/// <summary>
/// Snapshot of a single grade band for a PerformanceAppraisalCriterionConfig.
/// </summary>
public class PerformanceAppraisalCriterionConfigGradeRange : TenantEntity
{
    [Required]
    public Guid PerformanceAppraisalCriterionConfigId { get; set; }

    [Required]
    public Guid GradeDefinitionId { get; set; }

    public int LowScore { get; set; }

    public int HighScore { get; set; }

    [ForeignKey(nameof(PerformanceAppraisalCriterionConfigId))]
    public virtual PerformanceAppraisalCriterionConfig CriterionConfig { get; set; } = null!;

    [ForeignKey(nameof(GradeDefinitionId))]
    public virtual AppraisalGradeDefinition GradeDefinition { get; set; } = null!;
}

/// <summary>
/// Employee's personal development plan — created/updated during goal setting, mid-year, and year-end.
/// Contains structured development objectives with progress tracking.
/// </summary>
public class EmployeeDevelopmentPlan : TenantEntity
{
    [Required]
	public Guid EmployeeId { get; set; }
    
	public Guid? AppraisalCycleId { get; set; } // Usually the annual cycle this plan belongs to
    
	[MaxLength(200)]
	public string Title { get; set; } = string.Empty;
    
	public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    
	public DevelopmentPlanStatus PlanStatus { get; set; } = DevelopmentPlanStatus.Active;

    [MaxLength(2000)]
	public string? OverallNotes { get; set; }

    [ForeignKey(nameof(EmployeeId))]
	public virtual Employee Employee { get; set; } = null!;
    
	[ForeignKey(nameof(AppraisalCycleId))]
	public virtual AppraisalCycle? Cycle { get; set; }

    public virtual ICollection<EmployeeDevelopmentObjective> Objectives { get; set; } = new List<EmployeeDevelopmentObjective>();
    public virtual ICollection<AppraisalReviewEvent> ReviewEvents { get; set; } = new List<AppraisalReviewEvent>(); // optional: events where plan was updated
    public virtual ICollection<EmployeeDevelopmentPlanFeedback> Feedbacks { get; set; } = new List<EmployeeDevelopmentPlanFeedback>();
}

/// <summary>
/// A single measurable development objective within the plan (e.g., "Obtain AWS certification", "Improve leadership skills").
/// </summary>
public class EmployeeDevelopmentObjective : TenantEntity
{
    [Required]
	public Guid DevelopmentPlanId { get; set; }
    
	[MaxLength(300)]
	public string Title { get; set; } = string.Empty;
    
	[MaxLength(2000)]
	public string? Description { get; set; }
    
	[MaxLength(2000)]
	public string? Actions { get; set; }
    
	public DateOnly? TargetDate { get; set; }
    
	[Column(TypeName = "decimal(5,2)")]
	public decimal ProgressPercent { get; set; } = 0;
    
	[MaxLength(2000)]
	public string? ProgressNotes { get; set; }
    
	public DevelopmentObjectiveStatus ObjectiveStatus { get; set; } = DevelopmentObjectiveStatus.NotStarted;

    // Optional: link back to specific mid-year or year-end update event
    public Guid? UpdatedInReviewEventId { get; set; }

    [ForeignKey(nameof(DevelopmentPlanId))]
	public virtual EmployeeDevelopmentPlan Plan { get; set; } = null!;
    
	[ForeignKey(nameof(UpdatedInReviewEventId))]
	public virtual AppraisalReviewEvent? UpdatedInEvent { get; set; }
}

/// <summary>
/// Manager feedback entry recorded against a development plan.
/// Builds a timeline of manager observations, risk flags, progress notes, and reviews.
/// </summary>
public class EmployeeDevelopmentPlanFeedback : TenantEntity
{
    [Required]
    public Guid DevelopmentPlanId { get; set; }

    /// <summary>Employee ID of the manager who wrote the feedback.</summary>
    [Required]
    public Guid ManagerId { get; set; }

    public FeedbackType FeedbackType { get; set; } = FeedbackType.GeneralComment;

    [Required]
    [MaxLength(3000)]
    public string Comment { get; set; } = string.Empty;

    [ForeignKey(nameof(DevelopmentPlanId))]
    public virtual EmployeeDevelopmentPlan DevelopmentPlan { get; set; } = null!;
}

/// <summary>
/// A specific review/check-in event inside the annual cycle (mid-year, Q1/Q2/Q3, etc.).
/// Not a separate cycle — just a checkpoint, conversation, or light assessment.
/// Supports light-touch mid-year (progress summary, challenges, development plan update)
/// and more structured quarterly reviews without duplicating goals/templates.
/// </summary>
public class AppraisalReviewEvent : TenantEntity
{
    [Required]
	public Guid AppraisalCycleId { get; set; } // Always links to the annual cycle
    
	[Required]
	public Guid PerformanceAppraisalId { get; set; } // Links to the employee's main annual appraisal

    public ReviewEventType Type { get; set; }
    
    public DateOnly EventDate { get; set; }
	
    public AppraisalReviewStatus Status { get; set; } = AppraisalReviewStatus.Pending;
	
    public bool IsLightTouch { get; set; } = true;

    /// <summary>When true, this interim event is a full appraisal against the period's goals/KPIs (Theme 7).</summary>
    public bool IsFullAppraisal { get; set; } = false;

    /// <summary>Aggregated period score produced by a full interim appraisal (null for light-touch).</summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal? OverallPeriodScore { get; set; }

    // Mid-year / interim specific fields
    [MaxLength(2000)]
	public string? AchievementsSummary { get; set; }
    
	[MaxLength(2000)]
	public string? ChallengesSummary { get; set; }
    
	[MaxLength(2000)]
	public string? Notes { get; set; }

    /// <summary>Notes recorded by the manager (separate from the employee's Notes field).</summary>
    [MaxLength(2000)]
    public string? ManagerNotes { get; set; }

    // Link to related conversation (kick-off, mid-year meeting, etc.)
    public Guid? ConversationId { get; set; }
	
	// Optional: if this event triggers any development plan update
    public Guid? UpdatedDevelopmentPlanId { get; set; }

    [ForeignKey(nameof(AppraisalCycleId))] 
    public virtual AppraisalCycle Cycle { get; set; } = null!;

    [ForeignKey(nameof(PerformanceAppraisalId))] 
    public virtual PerformanceAppraisal Appraisal { get; set; } = null!;

    [ForeignKey(nameof(ConversationId))] 
    public virtual AppraisalConversation? Conversation { get; set; }
	
	[ForeignKey(nameof(UpdatedDevelopmentPlanId))] 
    public virtual EmployeeDevelopmentPlan? UpdatedDevelopmentPlan { get; set; }  // if tied to specific development plan update

    public virtual ICollection<AppraisalAttachment> Attachments { get; set; } = new List<AppraisalAttachment>();
    public virtual ICollection<GoalProgressEntry> ProgressEntries { get; set; } = new List<GoalProgressEntry>(); // optional: progress logged during this event
}

/// <summary>
/// Tracks a scheduled or completed appraisal conversation between manager and employee.
/// Covers: kick-off, mid-year check-in meeting, and final appraisal conversation.
/// </summary>
public class AppraisalConversation : TenantEntity
{
	[Required]
    public Guid AppraisalId { get; set; }
	
	public Guid? ScheduledById { get; set; }
    public Guid? ConductedById { get; set; }

    public ConversationType Type { get; set; } = ConversationType.KickOff; // Enum: KickOff, MidYear, FinalReview

    public DateTime? ScheduledDate { get; set; }
    public DateTime? HeldDate { get; set; }
	
	[MaxLength(2000)]
    public string? Agenda { get; set; }
	
	/// <summary>Notes logged by manager after the conversation is held.</summary>
	[MaxLength(4000)]
	public string? PostMeetingNotes { get; set; }

    [MaxLength(2000)]
    public string? KeyTakeaways { get; set; }
	
	public bool IsCompleted { get; set; } = false;
	
	public Guid? ReviewEventId { get; set; }

    [ForeignKey(nameof(AppraisalId))]
    public virtual PerformanceAppraisal Appraisal { get; set; } = null!;
	
	[ForeignKey(nameof(ScheduledById))]
    public virtual Employee? ScheduledBy { get; set; }
    
    [ForeignKey(nameof(ConductedById))]
    public virtual Employee? ConductedBy { get; set; }
	
	[ForeignKey(nameof(ReviewEventId))] 
    public virtual AppraisalReviewEvent? ReviewEvent { get; set; }
}

/// <summary>
/// Tracks nomination for peer evaluation.
/// </summary>
public class PeerNomination : TenantEntity
{
	[Required]
    public Guid AppraisalId { get; set; }

	/// <summary>The peer being asked to evaluate.</summary>
    public Guid PeerEmployeeId { get; set; }

	[Required]
    public Guid NominatedById { get; set; }

    public DateTime NominationDate { get; set; }
    public DateTime? InvitationSentDate { get; set; }
    public DateTime? DueDate { get; set; }

    [MaxLength(500)]
    public string? InstructionsToPeer { get; set; }

    public PeerNominationStatus NominationStatus { get; set; } = PeerNominationStatus.Pending;
	
	/// <summary>Required when mode = Manager (manager approves employee-nominated peers).</summary>
	public Guid? ApprovedByManagerId { get; set; }

    public DateTime? ApprovedDate { get; set; }

    [MaxLength(500)]
    public string? RejectionReason { get; set; }

    // Navigation
    [ForeignKey(nameof(AppraisalId))]
    public virtual PerformanceAppraisal Appraisal { get; set; } = null!;

    [ForeignKey(nameof(PeerEmployeeId))]
    public virtual Employee PeerEmployee { get; set; } = null!;

    [ForeignKey(nameof(NominatedById))]
    public virtual Employee NominatedBy { get; set; } = null!;
	
	[ForeignKey(nameof(ApprovedByManagerId))]
	public virtual Employee? ApprovedByManager { get; set; }
}

/// <summary>
/// Stores each evaluator's full evaluation with role, weight, and submission state.
/// One record per evaluator per appraisal.
/// </summary>
public class EvaluatorEvaluation : TenantEntity
{
	[Required]
    public Guid AppraisalId { get; set; }

	[Required]
    public Guid EvaluatorId { get; set; }

    public EvaluatorRole EvaluatorRole { get; set; }

    /// <summary>Role-based weight (e.g., Manager=0.7, Peer=0.2, Self=0.1).</summary>
    [Column(TypeName = "decimal(3,2)")]
    public decimal EvaluatorWeight { get; set; }

    /// <summary>Is this the authoritative/final evaluation (typically manager post-calibration)?</summary>
    public bool IsAuthoritative { get; set; }

    public DateTime? StartedDate { get; set; }
    public DateTime? SubmittedDate { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal? TotalScore { get; set; }

    [MaxLength(2000)]
    public string? OverallNotes { get; set; }

    [MaxLength(1000)]
    public string? Recommendation { get; set; }

    [ForeignKey(nameof(AppraisalId))]
    public virtual PerformanceAppraisal Appraisal { get; set; } = null!;

    [ForeignKey(nameof(EvaluatorId))]
    public virtual Employee Evaluator { get; set; } = null!;

    public virtual ICollection<CriterionScore> CriterionScores { get; set; } = new List<CriterionScore>();
}

/// <summary>
/// Stores per-criterion score for a specific evaluator evaluation.
/// </summary>
public class CriterionScore : TenantEntity
{
	[Required]
    public Guid EvaluatorEvaluationId { get; set; }

	[Required]
    public Guid TemplateItemId { get; set; }

	/// <summary>
	/// The resolved grade for this score (populated after scoring).
	/// </summary>
	public Guid? GradeDefinitionId { get; set; }

    /// <summary>For non-KPI criteria: numeric score chosen by evaluator (0-100).</summary>
    public int? NumericScore { get; set; }

    /// <summary>For KPI-driven criteria: the actual value achieved by the employee.</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? ActualValue { get; set; }

    /// <summary>Computed weighted score.</summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal WeightedScore { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [MaxLength(4000)]
    public string? EvidenceLinks { get; set; }

    [ForeignKey(nameof(EvaluatorEvaluationId))]
    public virtual EvaluatorEvaluation EvaluatorEvaluation { get; set; } = null!;

    [ForeignKey(nameof(TemplateItemId))]
    public virtual AppraisalTemplateItem TemplateItem { get; set; } = null!;

	[ForeignKey(nameof(GradeDefinitionId))]
	public virtual AppraisalGradeDefinition? GradeDefinition { get; set; }
}

/// <summary>
/// Represents a calibration session for a specific department or group.
/// HR triggers these; managers attend and review proposed ratings in a matrix.
/// </summary>
public class CalibrationSession : TenantEntity
{
	[Required]
    public Guid AppraisalCycleId { get; set; }
	
	[Required]
    [MaxLength(200)]
    public string SessionName { get; set; } = string.Empty;
	
	public Guid? OrganizationLevelId { get; set; }
    public Guid? OrganizationUnitId { get; set; }
	
	public CalibrationStatus Status { get; set; } = CalibrationStatus.Pending;

    public DateTime? ScheduledDate { get; set; }
    public DateTime? StartedDate { get; set; }
	public DateTime? CompletedDate { get; set; }
	
	/// <summary>HR officer who opened and manages this session.</summary>
	public Guid? FacilitatedById { get; set; }
	
	/// <summary>Who marked it as complete.</summary>
	public Guid? CompletedById { get; set; }
	
	[MaxLength(2000)]
    public string? Agenda { get; set; }
    
    [MaxLength(4000)]
    public string? MeetingNotes { get; set; }

    [ForeignKey(nameof(AppraisalCycleId))]
    public virtual AppraisalCycle AppraisalCycle { get; set; } = null!;
	
	[ForeignKey(nameof(OrganizationLevelId))]
    public virtual OrganizationLevel? OrganizationLevel { get; set; }

    [ForeignKey(nameof(OrganizationUnitId))]
    public virtual OrganizationUnit? OrganizationUnit { get; set; }

    [ForeignKey(nameof(FacilitatedById))]
    public virtual Employee? FacilitatedBy { get; set; }
	
	[ForeignKey(nameof(CompletedById))]
	public virtual Employee? CompletedBy { get; set; }

	public virtual ICollection<CalibrationParticipant> Participants { get; set; } = new List<CalibrationParticipant>();
	public virtual ICollection<CalibrationRatingAdjustment> RatingAdjustments { get; set; } = new List<CalibrationRatingAdjustment>();
	public virtual ICollection<PerformanceAppraisal> CalibratedAppraisals { get; set; } = new List<PerformanceAppraisal>();
	public virtual ICollection<AppraisalAttachment> Attachments { get; set; } = new List<AppraisalAttachment>();
}

/// <summary>
/// Tracks which managers and HR staff attended a calibration session.
/// </summary>
public class CalibrationParticipant : TenantEntity
{
    [Required]
    public Guid CalibrationSessionId { get; set; }
    
    [Required]
    public Guid EmployeeId { get; set; }
	
	[MaxLength(100)]
	public string Role { get; set; } = "Manager";
    
    public bool Attended { get; set; } = false;
    
    [ForeignKey(nameof(CalibrationSessionId))]
    public virtual CalibrationSession CalibrationSession { get; set; } = null!;
    
    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;
}

/// <summary>
/// Records a rating adjustment made during a calibration session.
/// Provides the audit trail of what changed, by whom, and why —
/// enabling before/after comparison of manager-proposed vs calibrated scores.
/// </summary>
public class CalibrationRatingAdjustment : TenantEntity
{
	[Required]
    public Guid CalibrationSessionId { get; set; }

	/// <summary>The appraisal being adjusted.</summary>
	[Required]
    public Guid PerformanceAppraisalId { get; set; }
	
	/// <summary>The specific template item being adjusted (null if adjusting overall score).</summary>
	public Guid? TemplateItemId { get; set; }
	
	[Column(TypeName = "decimal(5,2)")]
	public decimal? OriginalScore { get; set; }

	[Column(TypeName = "decimal(5,2)")]
	public decimal? AdjustedScore { get; set; }
	
	[Required]
	public Guid AdjustedById { get; set; }

    public DateTime AdjustmentDate { get; set; }

	[MaxLength(2000)]
	public string? Rationale { get; set; }

    [ForeignKey(nameof(CalibrationSessionId))]
    public virtual CalibrationSession CalibrationSession { get; set; } = null!;

    [ForeignKey(nameof(PerformanceAppraisalId))]
    public virtual PerformanceAppraisal PerformanceAppraisal { get; set; } = null!;
	
	[ForeignKey(nameof(TemplateItemId))]
	public virtual AppraisalTemplateItem? TemplateItem { get; set; }

	[ForeignKey(nameof(AdjustedById))]
	public virtual Employee AdjustedBy { get; set; } = null!;
}

/// <summary>
/// HR review record for a finalized appraisal before it is released to the employee.
/// Tracks HR's sign-off and any score modifications if settings allow.
/// </summary>
public class AppraisalHRReview : TenantEntity
{
	[Required]
	public Guid AppraisalId { get; set; }

	[Required]
	public Guid ReviewedByHRId { get; set; }

	public DateTime ReviewStartedDate { get; set; }
	public DateTime? ReviewCompletedDate { get; set; }

	public bool IsApproved { get; set; } = false;

	[MaxLength(2000)]
	public string? HRNotes { get; set; }

	/// <summary>
	/// If HRCanModifyScores = true, the HR-adjusted score is stored here.
	/// </summary>
	[Column(TypeName = "decimal(5,2)")]
	public decimal? AdjustedOverallScore { get; set; }

	[MaxLength(1000)]
	public string? AdjustmentReason { get; set; }

	[ForeignKey(nameof(AppraisalId))]
	public virtual PerformanceAppraisal Appraisal { get; set; } = null!;

	[ForeignKey(nameof(ReviewedByHRId))]
	public virtual Employee ReviewedByHR { get; set; } = null!;
}

/// <summary>
/// Employee reflections or responses — either overall or per criteria.
/// Separate from formal acknowledgment (which is a date stamp on PerformanceAppraisal).
/// </summary>
public class AppraisalEmployeeResponse : TenantEntity
{
    [Required]
    public Guid AppraisalId { get; set; }

    /// <summary>Null = overall appraisal response; set = per-template-item comment.</summary>
    public Guid? TemplateItemId { get; set; }

    [MaxLength(4000)]
    public string? ResponseText { get; set; }

    public DateTime ResponseDate { get; set; }
	
	/// <summary>Draft/Submitted — employee can save a draft before final submission.</summary>
	[MaxLength(20)]
	public AppraisalResponseStatus ResponseStatus { get; set; } = AppraisalResponseStatus.Draft;
	
	public DateTime? SubmittedDate { get; set; }

    [ForeignKey(nameof(AppraisalId))]
    public virtual PerformanceAppraisal Appraisal { get; set; } = null!;

    [ForeignKey(nameof(TemplateItemId))]
    public virtual AppraisalTemplateItem? TemplateItem { get; set; }
}

/// <summary>
/// Stores an employee's free-text response to a custom-question item in an appraisal template
/// during the self-evaluation phase. Custom question items have no CriteriaId — they are
/// purely open-ended prompts authored by HR in the template editor.
/// </summary>
public class AppraisalCustomQuestionResponse : TenantEntity
{
    [Required]
    public Guid PerformanceAppraisalId { get; set; }

    /// <summary>FK to the AppraisalTemplateItem whose CustomQuestion field is populated.</summary>
    [Required]
    public Guid TemplateItemId { get; set; }

    [MaxLength(4000)]
    public string? ResponseText { get; set; }

    public bool IsDraft { get; set; } = true;

    public DateTime? SubmittedDate { get; set; }

    [ForeignKey(nameof(PerformanceAppraisalId))]
    public virtual PerformanceAppraisal PerformanceAppraisal { get; set; } = null!;

    [ForeignKey(nameof(TemplateItemId))]
    public virtual AppraisalTemplateItem TemplateItem { get; set; } = null!;
}

/// <summary>
/// Formal appeal raised by an employee against a finalized appraisal.
/// </summary>
public class AppraisalAppeal : TenantEntity
{
	[Required]
    public Guid PerformanceAppraisalId { get; set; }

	[Required]
    public Guid EmployeeId { get; set; }

    public DateTime SubmittedDate { get; set; }

	[Required]
    [MaxLength(2000)]
    public string AppealReason { get; set; } = string.Empty;

    public AppraisalAppealStatus Status { get; set; } = AppraisalAppealStatus.Submitted;

    public Guid? ReviewedById { get; set; }

    [MaxLength(2000)]
    public string? ResolutionNotes { get; set; }

    public DateTime? ResolvedDate { get; set; }

    [ForeignKey(nameof(PerformanceAppraisalId))]
    public virtual PerformanceAppraisal PerformanceAppraisal { get; set; } = null!;

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    [ForeignKey(nameof(ReviewedById))]
    public virtual Employee? Reviewer { get; set; }

    public virtual ICollection<AppraisalAppealItem> Items { get; set; } = new List<AppraisalAppealItem>();
	public virtual ICollection<AppraisalAttachment> Attachments { get; set; } = new List<AppraisalAttachment>();
}

/// <summary>
/// An individual item being challenged within an appeal
/// (a specific KPI, a competency criterion, or the overall score).
/// </summary>
public class AppraisalAppealItem : TenantEntity
{
	[Required]
    public Guid AppraisalAppealId { get; set; }

    public Guid? TemplateItemId { get; set; }

	[Required]
    [MaxLength(2000)]
    public string Reason { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? ResolutionNotes { get; set; }

    public bool? ScoreAdjusted { get; set; }
	
	[Column(TypeName = "decimal(5,2)")]
	public decimal? OriginalScore { get; set; }

	[Column(TypeName = "decimal(5,2)")]
	public decimal? RevisedScore { get; set; }

    [ForeignKey(nameof(AppraisalAppealId))]
    public virtual AppraisalAppeal AppraisalAppeal { get; set; } = null!;

    [ForeignKey(nameof(TemplateItemId))]
    public virtual AppraisalTemplateItem? TemplateItem { get; set; }
}

/// <summary>
/// Immutable snapshot of an evaluator's evaluation at the moment of appeal remand.
/// Preserves original manager scores for before/after comparison.
/// </summary>
public class AppraisalEvaluationSnapshot : TenantEntity
{
    [Required]
    public Guid AppraisalId { get; set; }

    [Required]
    public Guid EvaluatorId { get; set; }

    [Required]
    public EvaluatorRole EvaluatorRole { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal? TotalScore { get; set; }

    [Required]
    public DateTime SnapshotDate { get; set; }

    [Required]
    [MaxLength(100)]
    public string SnapshotReason { get; set; } = "Appeal Remand";

    [ForeignKey(nameof(AppraisalId))]
    public virtual PerformanceAppraisal Appraisal { get; set; } = null!;

    [ForeignKey(nameof(EvaluatorId))]
    public virtual Employee Evaluator { get; set; } = null!;

    public virtual ICollection<AppraisalCriterionScoreSnapshot> CriterionScores { get; set; } = new List<AppraisalCriterionScoreSnapshot>();
}

/// <summary>
/// Immutable snapshot of a criterion score at the moment of appeal remand.
/// </summary>
public class AppraisalCriterionScoreSnapshot : TenantEntity
{
    [Required]
    public Guid AppraisalEvaluationSnapshotId { get; set; }

    [Required]
    public Guid TemplateItemId { get; set; }

    public int? NumericScore { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal WeightedScore { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    /// <summary>KPI target values snapshotted at the time of the appeal remand.</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? KpiTargetValue { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? KpiMinValue { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? KpiMaxValue { get; set; }

    public KpiTargetSource? KpiTargetSource { get; set; }

    [ForeignKey(nameof(AppraisalEvaluationSnapshotId))]
    public virtual AppraisalEvaluationSnapshot EvaluationSnapshot { get; set; } = null!;

    [ForeignKey(nameof(TemplateItemId))]
    public virtual AppraisalTemplateItem TemplateItem { get; set; } = null!;

    public virtual ICollection<AppraisalKpiEvaluationSnapshot> KpiSnapshots { get; set; } = new List<AppraisalKpiEvaluationSnapshot>();
}

/// <summary>
/// Immutable snapshot of a KPI evaluation record at the moment of appeal remand.
/// </summary>
public class AppraisalKpiEvaluationSnapshot : TenantEntity
{
    [Required]
    public Guid AppraisalCriterionScoreSnapshotId { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? ActualValue { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal AchievementPercent { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [MaxLength(1000)]
    public string? EvidenceLinks { get; set; }

    [Required]
    public DateTime SnapshotDate { get; set; }

    [ForeignKey(nameof(AppraisalCriterionScoreSnapshotId))]
    public virtual AppraisalCriterionScoreSnapshot CriterionScoreSnapshot { get; set; } = null!;
}

/// <summary>
/// Performance Improvement Plan — triggered when an employee is recommended for PIP.
/// Can be linked to the originating appraisal and tracks the full improvement journey.
/// </summary>
public class PerformanceImprovementPlan : TenantEntity
{
    [MaxLength(50)]
    public string PipNumber { get; set; } = string.Empty;

    [Required]
    public Guid EmployeeId { get; set; }

	/// <summary>The appraisal that triggered this PIP (if any).</summary>
    public Guid? AppraisalId { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    /// <summary>
    /// Defaults to <see cref="PipStatus.Draft"/>: a plan is in force only once it has been
    /// approved through the workflow engine, so a record created without an explicit status must
    /// not be live against the employee.
    /// </summary>
    public PipStatus Status { get; set; } = PipStatus.Draft;

    // Issues Identified
    [MaxLength(2000)]
    public string PerformanceIssues { get; set; } = string.Empty;
	
    [MaxLength(2000)]
    public string ExpectedStandards { get; set; } = string.Empty;

    // Action Plan
    [MaxLength(2000)]
    public string ImprovementActions { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? SupportProvided { get; set; }

    [MaxLength(2000)]
    public string? MeasurementCriteria { get; set; }

    [Required]
    public Guid SupervisorId { get; set; }
	
	/// <summary>HR co-owner for the PIP (optional but common).</summary>
	public Guid? HROwnerId { get; set; }

    [MaxLength(2000)]
    public string? ReviewSchedule { get; set; }

    // Outcome
    public DateTime? CompletionDate { get; set; }
    public PipOutcome? Outcome { get; set; }

    [MaxLength(2000)]
    public string? OutcomeNotes { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    [ForeignKey(nameof(AppraisalId))]
    public virtual PerformanceAppraisal? Appraisal { get; set; }

    [ForeignKey(nameof(SupervisorId))]
    public virtual Employee Supervisor { get; set; } = null!;
	
	[ForeignKey(nameof(HROwnerId))]
	public virtual Employee? HROwner { get; set; }

	public virtual ICollection<PipGoal> PipGoals { get; set; } = new List<PipGoal>();
	public virtual ICollection<PipReviewMeeting> ReviewMeetings { get; set; } = new List<PipReviewMeeting>();
	public virtual ICollection<AppraisalAttachment> Attachments { get; set; } = new List<AppraisalAttachment>();
}

/// <summary>
/// A specific measurable goal/milestone within a PIP.
/// Tracked independently from regular EmployeeGoals.
/// </summary>
public class PipGoal : TenantEntity
{
	[Required]
	public Guid PipId { get; set; }

	[Required, MaxLength(300)]
	public string Title { get; set; } = string.Empty;

	[MaxLength(2000)]
	public string? Description { get; set; }

	[MaxLength(1000)]
	public string? SuccessCriteria { get; set; }

	public DateOnly DueDate { get; set; }

	public GoalProgressStatus Status { get; set; } = GoalProgressStatus.NotStarted;

	[Column(TypeName = "decimal(5,2)")]
	public decimal? ProgressPercent { get; set; }

	[MaxLength(2000)]
	public string? ProgressNotes { get; set; }

	[ForeignKey(nameof(PipId))]
	public virtual PerformanceImprovementPlan Pip { get; set; } = null!;
}

/// <summary>
/// A scheduled review meeting within the PIP monitoring period.
/// </summary>
public class PipReviewMeeting : TenantEntity
{
    [Required]
    public Guid PipId { get; set; }

    [Required]
    public DateTime MeetingDate { get; set; }

    public bool EmployeeAttended { get; set; } = true;

    [MaxLength(2000)]
    public string ProgressNotes { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? IssuesDiscussed { get; set; }

    [MaxLength(2000)]
    public string? ActionsAgreed { get; set; }

    [MaxLength(2000)]
    public string? EmployeeComments { get; set; }

    [Required]
    public Guid ConductedById { get; set; }

    [ForeignKey(nameof(PipId))]
    public virtual PerformanceImprovementPlan Pip { get; set; } = null!;

    [ForeignKey(nameof(ConductedById))]
    public virtual Employee ConductedBy { get; set; } = null!;
}

/// <summary>
/// Flexible attachment record that can be linked to multiple entity types
/// (Appraisal, Goal, CheckIn, PIP, Appeal, KPI Evaluation, Calibration).
/// </summary>
public class AppraisalAttachment : TenantEntity
{
	[Required, MaxLength(255)]
	public string FileName { get; set; } = string.Empty;

	[Required, MaxLength(500)]
	public string FilePath { get; set; } = string.Empty;

	public long? FileSizeBytes { get; set; }

	[MaxLength(1000)]
	public string? Description { get; set; }

	public DateTime UploadDate { get; set; }

	[Required]
	public Guid UploadedById { get; set; }

	/// <summary>Scanned controlled upload backing this attachment.</summary>
	public Guid? FileUploadRecordId { get; set; }

	/// <summary>Central-DMS record, once registered.</summary>
	public Guid? DocumentRecordId { get; set; }

	/// <summary>Central-DMS version, once registered.</summary>
	public Guid? DocumentVersionId { get; set; }

	/// <summary>Discriminator for polymorphic attachment linking.</summary>
	public AppraisalAttachmentEntityType EntityType { get; set; }

	// --- Polymorphic FKs (exactly one populated) ---
	public Guid? PerformanceAppraisalId { get; set; }
	public Guid? EmployeeGoalId { get; set; }
	public Guid? UnitGoalId { get; set; }
	public Guid? CheckInId { get; set; }
	public Guid? PipId { get; set; }
	public Guid? AppealId { get; set; }
	public Guid? CalibrationSessionId { get; set; }
	public Guid? ReviewEventId { get; set; }

	[ForeignKey(nameof(UploadedById))]
	public virtual Employee UploadedBy { get; set; } = null!;

	[ForeignKey(nameof(PerformanceAppraisalId))]
	public virtual PerformanceAppraisal? PerformanceAppraisal { get; set; }

	[ForeignKey(nameof(EmployeeGoalId))]
	public virtual EmployeeGoal? EmployeeGoal { get; set; }

	[ForeignKey(nameof(UnitGoalId))]
	public virtual UnitGoal? UnitGoal { get; set; }

	[ForeignKey(nameof(CheckInId))]
	public virtual CheckIn? CheckIn { get; set; }

	[ForeignKey(nameof(PipId))]
	public virtual PerformanceImprovementPlan? Pip { get; set; }

	[ForeignKey(nameof(AppealId))]
	public virtual AppraisalAppeal? Appeal { get; set; }

	[ForeignKey(nameof(CalibrationSessionId))]
	public virtual CalibrationSession? CalibrationSession { get; set; }

	[ForeignKey(nameof(ReviewEventId))]
	public virtual AppraisalReviewEvent? ReviewEvent { get; set; }
}

// ────────────────────────────────────────────────────────────────────────────
// In-app notification record for appraisal workflow events
// ────────────────────────────────────────────────────────────────────────────
public class AppraisalNotification : TenantEntity
{
    [Required]
    public Guid RecipientEmployeeId { get; set; }

    public AppraisalNotificationType Type { get; set; }

    [Required, MaxLength(255)]
    public string Title { get; set; } = string.Empty;

    [Required, MaxLength(1000)]
    public string Message { get; set; } = string.Empty;

    [MaxLength(255)]
    public string? SubjectEmployeeName { get; set; }

    [MaxLength(255)]
    public string? CycleName { get; set; }

    [MaxLength(500)]
    public string? NavigationUrl { get; set; }

    public Guid? AppraisalId { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    public bool IsRead { get; set; } = false;

    public DateTime? ReadDate { get; set; }

    public NotificationUrgency Urgency { get; set; } = NotificationUrgency.Normal;

    [ForeignKey(nameof(RecipientEmployeeId))]
    public virtual Employee RecipientEmployee { get; set; } = null!;

    [ForeignKey(nameof(AppraisalId))]
    public virtual PerformanceAppraisal? Appraisal { get; set; }
}
