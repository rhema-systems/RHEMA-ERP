using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Entities.HR.Safety;
using ErpSystem.Core.Entities.HR.Training;

namespace ErpSystem.Core.Entities.HR.JobAnalysis;

/// <summary>
/// Formal job description document
/// </summary>
public class JobDescription : TenantEntity
{
    [MaxLength(50)]
    public string JobDescriptionNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Position is required")]
    public Guid PositionId { get; set; }

    [ForeignKey(nameof(PositionId))]
    public virtual EmployeePosition Position { get; set; } = null!;

    [Required(ErrorMessage = "Job title is required")]
    [MaxLength(200)]
    public string JobTitle { get; set; } = string.Empty;
	
	[MaxLength(2000)]
    public string JobSummary { get; set; } = string.Empty;

    // Version Control
    public int VersionNumber { get; set; }
    public DateTime EffectiveDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? RevisionReason { get; set; }

    public Guid? SupersededByVersionId { get; set; }

    [ForeignKey(nameof(SupersededByVersionId))]
    public virtual JobDescription? SupersededByVersion { get; set; }

    public JobDescriptionStatus Status { get; set; }

    public Guid? PreparedById { get; set; }

    [ForeignKey(nameof(PreparedById))]
    public virtual Employee? PreparedBy { get; set; }
    
    public DateTime? PreparedDate { get; set; }

    public Guid? ReviewedById { get; set; }
    
    [ForeignKey(nameof(ReviewedById))]
    public virtual Employee? ReviewedBy { get; set; }
    
    public DateTime? ReviewedDate { get; set; }

    public Guid? ApprovedById { get; set; }
    
    [ForeignKey(nameof(ApprovedById))]
    public virtual Employee? ApprovedBy { get; set; }
    
    public DateTime? ApprovalDate { get; set; }

    // Next Review
    public DateTime? NextReviewDate { get; set; }
    public int ReviewCycleMonths { get; set; } = 24; // Default 2 years

    // ───────────────────────── Job Evaluation / Valuation ─────────────────────────
    /// <summary>Intrinsic business value the organization attaches to the role itself
    /// (beyond the holder's qualifications/competencies).</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? RoleIntrinsicValue { get; set; }

    /// <summary>How critical the role is to keeping the organization running.</summary>
    public RoleCriticalityLevel? RoleCriticality { get; set; }

    /// <summary>Externally benchmarked salary for comparable roles in the industry.</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? IndustryBenchmarkSalary { get; set; }

    /// <summary>Computed/estimated lower bound of the salary range for the role.</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? EstimatedSalaryLow { get; set; }

    /// <summary>Computed/estimated upper bound of the salary range for the role.</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? EstimatedSalaryHigh { get; set; }

    /// <summary>Suggested salary grade matched from the estimated range.</summary>
    public Guid? SuggestedSalaryGradeId { get; set; }

    [ForeignKey(nameof(SuggestedSalaryGradeId))]
    public virtual SalaryGrade? SuggestedSalaryGrade { get; set; }

    [MaxLength(2000)]
    public string? ValuationNotes { get; set; }

    /// <summary>
    /// The grade the author PROPOSES for the post (round 3, lane J2; decision D-11). The suggestion
    /// above is the system's output and stays read-only; this is the human's answer to it, defaulted
    /// to the suggestion when the valuation is stored and otherwise the author's. The post's ACTUAL
    /// grade is on the position, where payroll reads it — neither of these assigns it.
    /// </summary>
    public Guid? ProposedSalaryGradeId { get; set; }

    [ForeignKey(nameof(ProposedSalaryGradeId))]
    public virtual SalaryGrade? ProposedSalaryGrade { get; set; }

    /// <summary>Why the proposal differs from the suggestion, when it does.</summary>
    [MaxLength(500)]
    public string? ProposedSalaryGradeNote { get; set; }

    // ───────────────────────── Authority & Financial Limits ─────────────────────────
    /// <summary>Level of decision-making authority/autonomy the role carries.</summary>
    public DecisionAuthorityLevel? AutonomyLevel { get; set; }

    [MaxLength(2000)]
    public string? DecisionMakingScope { get; set; }

    /// <summary>Maximum financial commitment the role can approve unaided.</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? FinancialAuthorityLimit { get; set; }

    [MaxLength(2000)]
    public string? ApprovalAuthorityNotes { get; set; }

    // ───────────────────────── Classification (Ghana context) ─────────────────────────
    /// <summary>Seniority classification (Management / Senior / Junior staff) from the StaffLevel master.</summary>
    public Guid? StaffLevelId { get; set; }

    [ForeignKey(nameof(StaffLevelId))]
    public virtual StaffLevel? StaffLevel { get; set; }

    /// <summary>Intended engagement type for the role (Permanent / Contract / Casual / …).</summary>
    public EmploymentType? IntendedEmploymentType { get; set; }

    /// <summary>Whether the role falls under a collective bargaining agreement.</summary>
    public bool IsBargainingUnitRole { get; set; }

    public Guid? UnionId { get; set; }

    [ForeignKey(nameof(UnionId))]
    public virtual Union? Union { get; set; }

    /// <summary>ISCO-08 / Ghana Standard Classification of Occupations code (optional).</summary>
    [MaxLength(50)]
    public string? OccupationCode { get; set; }

    [MaxLength(2000)]
    public string? EssentialFunctionsSummary { get; set; }

    // ───────────────────────── Job Architecture ─────────────────────────
    public Guid? JobFamilyId { get; set; }

    [ForeignKey(nameof(JobFamilyId))]
    public virtual JobFamily? JobFamily { get; set; }

    public Guid? JobSubFamilyId { get; set; }

    [ForeignKey(nameof(JobSubFamilyId))]
    public virtual JobSubFamily? JobSubFamily { get; set; }

    public Guid? JobLevelId { get; set; }

    [ForeignKey(nameof(JobLevelId))]
    public virtual CareerLevel? JobLevel { get; set; }

    // Relations
    public virtual ICollection<JobDutyItem> DutyItems { get; set; } = new List<JobDutyItem>();
    public virtual ICollection<JobResponsibility> Responsibilities { get; set; } = new List<JobResponsibility>();
	public virtual ICollection<JobQualification> Qualifications { get; set; } = new List<JobQualification>();
	public virtual ICollection<JobCompetency> Competencies { get; set; } = new List<JobCompetency>();
	public virtual ICollection<JobPhysicalDemand> PhysicalDemands { get; set; } = new List<JobPhysicalDemand>();
	public virtual ICollection<JobWorkingCondition> JobWorkingConditions { get; set; } = new List<JobWorkingCondition>();
	public virtual ICollection<JobPpeRequirement> PpeRequirements { get; set; } = new List<JobPpeRequirement>();
	public virtual ICollection<JobEquipmentTool> EquipmentTools { get; set; } = new List<JobEquipmentTool>();
	public virtual ICollection<JobReportingRelationship> ReportingRelationships { get; set; } = new List<JobReportingRelationship>();
	public virtual ICollection<JobMedicalRequirement> MedicalRequirements { get; set; } = new List<JobMedicalRequirement>();
}

/// <summary>
/// An itemized, numbered job-description statement (the "what the job is" duties),
/// distinct from <see cref="JobResponsibility"/> which captures the specific tasks performed.
/// Entered on the Basic Info tab, before the job summary.
/// </summary>
public class JobDutyItem : TenantEntity
{
    public Guid JobDescriptionId { get; set; }

    [ForeignKey(nameof(JobDescriptionId))]
    public virtual JobDescription JobDescription { get; set; } = null!;

    /// <summary>Display/ordering number for the itemized duty (1, 2, 3, ...)</summary>
    public int SequenceNumber { get; set; }

    [Required(ErrorMessage = "Duty statement is required")]
    [MaxLength(1000)]
    public string DutyStatement { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

public class JobResponsibility : TenantEntity
{
    public Guid JobDescriptionId { get; set; }

    [ForeignKey(nameof(JobDescriptionId))]
    public virtual JobDescription JobDescription { get; set; } = null!;

    [Required(ErrorMessage = "Responsibility description is required")]
    [MaxLength(1000)]
    public string ResponsibilityDescription { get; set; } = string.Empty;
    
    [Required]
    public ResponsibilityType Type { get; set; }
	
	// Weighting (IMPORTANT)
    public decimal? PercentageOfTime { get; set; }
    public int? ImportanceWeight { get; set; }

    public virtual ICollection<JobQualification> Qualifications { get; set; } = new List<JobQualification>();
    public virtual ICollection<JobCompetency> Competencies { get; set; } = new List<JobCompetency>();
    public virtual ICollection<JobResponsibilityKpi> Kpis { get; set; } = new List<JobResponsibilityKpi>();
}

/// <summary>
/// A measurable KPI / performance standard for a responsibility — how success is judged.
/// Designed so the Performance/Appraisal module can later consume these as appraisal criteria.
/// </summary>
public class JobResponsibilityKpi : TenantEntity
{
    public Guid JobResponsibilityId { get; set; }

    [ForeignKey(nameof(JobResponsibilityId))]
    public virtual JobResponsibility JobResponsibility { get; set; } = null!;

    [Required(ErrorMessage = "KPI statement is required")]
    [MaxLength(500)]
    public string KpiStatement { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? TargetOrStandard { get; set; }   // e.g. "≥ 95%", "within 48 hours"

    [MaxLength(100)]
    public string? UnitOfMeasure { get; set; }       // e.g. "%", "days", "incidents"

    /// <summary>Relative weighting of this KPI (e.g. for appraisal scoring).</summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal? Weight { get; set; }

    public int SequenceNumber { get; set; }
}

public class JobQualification : TenantEntity
{
	public Guid JobDescriptionId { get; set; }

    [ForeignKey(nameof(JobDescriptionId))]
    public virtual JobDescription JobDescription { get; set; } = null!;
	
    public Guid? JobResponsibilityId { get; set; }

    [ForeignKey(nameof(JobResponsibilityId))]
    public virtual JobResponsibility? JobResponsibility { get; set; }

    public QualificationType Type { get; set; }

    public Guid? QualificationId { get; set; }

    [ForeignKey(nameof(QualificationId))]
    public virtual Qualification? Qualification { get; set; }

    /// <summary>
    /// A credential from the certification catalogue, where the requirement is one (round 2,
    /// lane C2, § 6.3): one column, so the job description and the position name the same thing.
    /// </summary>
    public Guid? CertificationId { get; set; }

    [ForeignKey(nameof(CertificationId))]
    public virtual Certification? Certification { get; set; }

    /// <summary>
    /// If QualificationId is set, this is auto-populated from qualification name
    /// If null, user entered custom title
    /// </summary>
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    public bool IsRequired { get; set; } // Required vs Preferred

    /// <summary>
    /// Additional context specific to this job
    /// E.g., "Must be licensed in California" or "Minimum 5 years post-qualification experience"
    /// </summary>
    [MaxLength(500)]
    public string? JobSpecificRequirements { get; set; }

    /// <summary>
    /// Job-evaluation: monetary value the organization attaches to this qualification for the role.
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? MonetaryValue { get; set; }
}

public class JobCompetency : TenantEntity
{
	public Guid JobDescriptionId { get; set; }

    [ForeignKey(nameof(JobDescriptionId))]
    public virtual JobDescription JobDescription { get; set; } = null!;
	
    public Guid? JobResponsibilityId { get; set; }

    [ForeignKey(nameof(JobResponsibilityId))]
    public virtual JobResponsibility? JobResponsibility { get; set; }

    /// <summary>
    /// Optional link to the master Skill catalogue. When set, the dropdown selection
    /// auto-populates <see cref="CompetencyName"/>.
    /// </summary>
    public Guid? SkillId { get; set; }

    [ForeignKey(nameof(SkillId))]
    public virtual Skill? Skill { get; set; }

    /// <summary>
    /// Optional link to the master Competency framework. When set, the dropdown selection
    /// auto-populates <see cref="CompetencyName"/>.
    /// </summary>
    public Guid? CompetencyId { get; set; }

    [ForeignKey(nameof(CompetencyId))]
    public virtual Competency? Competency { get; set; }

    [Required(ErrorMessage = "Competency name is required")]
    [MaxLength(200)]
    public string CompetencyName { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public CompetencyType Type { get; set; }

    public ProficiencyLevel RequiredLevel { get; set; }

    public bool IsCritical { get; set; }

    /// <summary>
    /// Job-evaluation: monetary value the organization attaches to this competency for the role.
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? MonetaryValue { get; set; }
}

/// <summary>
/// Physical demands required to perform the job
/// </summary>
public class JobPhysicalDemand : TenantEntity
{
    public Guid JobDescriptionId { get; set; }
    
	[ForeignKey(nameof(JobDescriptionId))]
    public virtual JobDescription JobDescription { get; set; } = null!;

    [Required]
    public PhysicalDemandType DemandType { get; set; }  // e.g. Lifting, Standing, Walking, FineManipulation, etc.

    public string DemandDescription { get; set; } = string.Empty;  // e.g. "Lift and carry up to 25 kg occasionally"

    public PhysicalDemandFrequency Frequency { get; set; }  // Never, Rarely, Occasionally, Frequently, Continuously

    [Range(0, double.MaxValue)]
    public double? WeightOrForceKg { get; set; }   // For lifting, pushing, gripping force, etc.

    public string? DistanceOrDuration { get; set; }  // e.g. "Up to 50 meters", "For 2 hours continuously"

    public bool IsEssential { get; set; } = true;

    [MaxLength(1000)]
    public string? NotesOrExamples { get; set; }   // e.g. "Requires bilateral grip strength; repetitive motion of hands"

    // ───── Physical attribute / structure requirement (per stakeholder decision, lives here) ─────
    /// <summary>
    /// When true, this row expresses a required physical attribute/structure (e.g. a certain build,
    /// height/reach) rather than an activity demand, with the rationale captured in <see cref="Justification"/>.
    /// </summary>
    public bool IsPhysicalAttribute { get; set; }

    [MaxLength(500)]
    public string? AttributeRequirement { get; set; }  // e.g. "Able to reach overhead shelving at 2.1 m"

    [MaxLength(1000)]
    public string? Justification { get; set; }         // why the attribute is needed for the job
}

/// <summary>
/// Environmental and working conditions the job exposes the employee to
/// </summary>
public class JobWorkingCondition : TenantEntity
{
    public Guid JobDescriptionId { get; set; }
    
	[ForeignKey(nameof(JobDescriptionId))]
    public virtual JobDescription JobDescription { get; set; } = null!;

    [Required]
    public WorkEnvironmentType EnvironmentType { get; set; }  // e.g. OutdoorWeather, Noise, Chemicals, etc.

	[MaxLength(1000)]
    public string Description { get; set; } = string.Empty;  // e.g. "Exposed to Ghana outdoor heat/humidity (avg 30-35°C)"

    public ExposureLevel ExposureLevel { get; set; }  // None, Occasional, Frequent, Constant

    public bool RequiresPPE { get; set; }  // Personal Protective Equipment required?

    [MaxLength(500)]
    public string? PPERequirements { get; set; }  // e.g. "Safety boots, gloves, hearing protection"

    public decimal? TravelPercentage { get; set; }
    
    public string? TravelRequirements { get; set; }
}

/// <summary>
/// Tools, equipment, machinery, software, vehicles, etc. used in the job
/// </summary>
public class JobEquipmentTool : TenantEntity
{
    public Guid JobDescriptionId { get; set; }
    
	[ForeignKey(nameof(JobDescriptionId))]
    public virtual JobDescription JobDescription { get; set; } = null!;

    [Required]
    [MaxLength(200)]
    public string ItemName { get; set; } = string.Empty;  // e.g. "Microsoft Excel", "Forklift", "Surgical Instruments"

    public EquipmentType Type { get; set; }  // Software, HandTool, Machinery, Vehicle, etc.

    [MaxLength(500)]
    public string DescriptionOrSpecification { get; set; } = string.Empty;

    public ProficiencyLevel RequiredProficiency { get; set; }  // Basic, Intermediate, Advanced, Expert

    public bool IsEssential { get; set; }

    [MaxLength(1000)]
    public string? TrainingRequired { get; set; }  // e.g. "Forklift certification mandatory"

    public Guid? LinkedQualificationId { get; set; }  // Optional link to JobQualification or Certification

	[ForeignKey(nameof(LinkedQualificationId))]
    public virtual JobQualification? LinkedQualification { get; set; }

    /// <summary>
    /// Itemized training requirements for using this equipment/tool (grid instead of the
    /// single legacy <see cref="TrainingRequired"/> text field).
    /// </summary>
    public virtual ICollection<JobEquipmentTraining> TrainingRequirements { get; set; } = new List<JobEquipmentTraining>();
}

/// <summary>
/// PPE (Personal Protective Equipment) required for the job. Replaces the single free-text
/// PPERequirements field on <see cref="JobWorkingCondition"/> with an itemized grid,
/// optionally linked to the master <see cref="PpeType"/> catalogue.
/// </summary>
public class JobPpeRequirement : TenantEntity
{
    public Guid JobDescriptionId { get; set; }

    [ForeignKey(nameof(JobDescriptionId))]
    public virtual JobDescription JobDescription { get; set; } = null!;

    /// <summary>Optional link to the master PPE catalogue. When null, <see cref="CustomPpeName"/> is used.</summary>
    public Guid? PpeTypeId { get; set; }

    [ForeignKey(nameof(PpeTypeId))]
    public virtual PpeType? PpeType { get; set; }

    /// <summary>Free-text PPE name when not selected from the master catalogue.</summary>
    [MaxLength(200)]
    public string? CustomPpeName { get; set; }

    public bool IsMandatory { get; set; } = true;

    [MaxLength(500)]
    public string? Notes { get; set; }
}

/// <summary>
/// An itemized training requirement for a specific equipment/tool, optionally linked to a
/// master <see cref="TrainingProgram"/>.
/// </summary>
public class JobEquipmentTraining : TenantEntity
{
    public Guid JobEquipmentToolId { get; set; }

    [ForeignKey(nameof(JobEquipmentToolId))]
    public virtual JobEquipmentTool JobEquipmentTool { get; set; } = null!;

    /// <summary>Optional link to the master training programme catalogue.</summary>
    public Guid? TrainingProgramId { get; set; }

    [ForeignKey(nameof(TrainingProgramId))]
    public virtual TrainingProgram? TrainingProgram { get; set; }

    [Required(ErrorMessage = "Training requirement is required")]
    [MaxLength(500)]
    public string RequirementText { get; set; } = string.Empty;

    public bool IsMandatory { get; set; } = true;
}

/// <summary>
/// Reporting lines, supervisory scope, and key contacts
/// </summary>
public class JobReportingRelationship : TenantEntity
{
    public Guid JobDescriptionId { get; set; }
    
	[ForeignKey(nameof(JobDescriptionId))]
    public virtual JobDescription JobDescription { get; set; } = null!;

    public ReportingRelationshipType RelationshipType { get; set; }  // ReportsTo, Supervises, InternalContacts, ExternalContacts

    [MaxLength(200)]
    public string TitleOrRole { get; set; } = string.Empty;  // e.g. "Finance Manager", "Direct Reports (5 staff)"

    public Guid? EmployeeOrPositionId { get; set; }  // Link to specific person/position if known
    
	[ForeignKey(nameof(EmployeeOrPositionId))]
    public virtual EmployeePosition? RelatedPosition { get; set; }

    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;  // e.g. "Reports directly to Head of HR; supervises junior accountants"

    public int? NumberOfDirectReports { get; set; }

    public bool IsPrimarySupervisor { get; set; }
}

/// <summary>
/// Manpower planning and budgeting
/// </summary>
public class ManpowerBudget : TenantEntity
{
    [MaxLength(50)]
    public string BudgetNumber { get; set; } = string.Empty;
    
    [Required(ErrorMessage = "Fiscal year is required")]
    public int FiscalYear { get; set; }

    public Guid? OrganizationLevelId { get; set; }
    
    [ForeignKey(nameof(OrganizationLevelId))]
    public virtual OrganizationLevel? OrganizationLevel { get; set; }

    public Guid? OrganizationUnitId { get; set; }

    [ForeignKey(nameof(OrganizationUnitId))]
    public virtual OrganizationUnit? OrganizationUnit { get; set; }

    public ManpowerBudgetStatus Status { get; set; }

    // Planning Period
    public DateTime PeriodStartDate { get; set; }
    public DateTime PeriodEndDate { get; set; }

    // Current State
    public int CurrentHeadcount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CurrentSalaryCost { get; set; }

    // Planned State
    public int PlannedHeadcount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal PlannedSalaryCost { get; set; }

    // Changes
    public int PlannedNewHires { get; set; }
    public int PlannedTerminations { get; set; }
    public int PlannedPromotions { get; set; }
    public int PlannedTransfers { get; set; }

    // Budget Allocation
    [Column(TypeName = "decimal(18,2)")]
    public decimal SalaryBudget { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal BenefitsBudget { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal RecruitmentBudget { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal TrainingBudget { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalBudget { get; set; }

    // Variance Tracking
    [Column(TypeName = "decimal(18,2)")]
    public decimal ActualSpent { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal Variance { get; set; }

    [MaxLength(2000)]
    public string? BusinessJustification { get; set; }

    public Guid? ApprovedById { get; set; }

    [ForeignKey(nameof(ApprovedById))]
    public virtual Employee? ApprovedBy { get; set; }

    public DateTime? ApprovalDate { get; set; }

    /// <summary>Why the budget was sent back, when it was.</summary>
    /// <remarks>
    /// ⚠ Added in area 17 slice 7. `RejectAsync(budgetId, reason)` took a reason, set the status to
    /// Rejected, and <b>discarded the reason entirely</b> — it reached the log line and nothing
    /// else. A budget holder opening a rejected budget could see that it had been refused and had
    /// no way to find out why, which makes the rejection unactionable.
    /// </remarks>
    [MaxLength(1000)]
    public string? RejectionReason { get; set; }

    public virtual ICollection<ManpowerBudgetLine> BudgetLines { get; set; } = new List<ManpowerBudgetLine>();
}

public class ManpowerBudgetLine : TenantEntity
{
    public Guid ManpowerBudgetId { get; set; }

    [ForeignKey(nameof(ManpowerBudgetId))]
    public virtual ManpowerBudget ManpowerBudget { get; set; } = null!;

    public Guid PositionId { get; set; }

    [ForeignKey(nameof(PositionId))]
    public virtual EmployeePosition Position { get; set; } = null!;
	
	public Guid? JobDescriptionId { get; set; }
    
    [ForeignKey(nameof(JobDescriptionId))]
    public virtual JobDescription? JobDescription { get; set; }

    /// <summary>
    /// The place on the salary scale the planned average salary was read from (round 2b, R3).
    /// </summary>
    /// <remarks>
    /// <para>The demo asked for "the salary that goes with a position" to show once the position
    /// is chosen, and for the line to take its figure from the scale rather than a typed number.
    /// Grade → (level) → notch, all optional, pre-selected on screen from
    /// <c>EmployeePosition.SalaryGradeId</c> (122 of 212 live positions carry one, measured
    /// 2026-09-10). The amount stays editable: <see cref="PlannedSalarySource"/> says whether it
    /// was read from the notch, the level's mid-point, the grade's minimum, or typed.</para>
    /// <para>Restrict on all three: a grade referenced by a budget line cannot be deleted from
    /// under it; retire it instead (the structure's own rule).</para>
    /// </remarks>
    public Guid? SalaryGradeId { get; set; }

    [ForeignKey(nameof(SalaryGradeId))]
    public virtual SalaryGrade? SalaryGrade { get; set; }

    public Guid? SalaryLevelId { get; set; }

    [ForeignKey(nameof(SalaryLevelId))]
    public virtual SalaryLevel? SalaryLevel { get; set; }

    public Guid? SalaryNotchId { get; set; }

    [ForeignKey(nameof(SalaryNotchId))]
    public virtual SalaryNotch? SalaryNotch { get; set; }

    public PlannedSalarySource PlannedSalarySource { get; set; } = PlannedSalarySource.Manual;

    // Current
    public int CurrentCount { get; set; }
    public int CurrentFilled { get; set; }
    public int CurrentVacant { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CurrentAverageSalary { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CurrentTotalCost { get; set; }

    // Planned
    public int PlannedCount { get; set; }
    public int PlannedNewPositions { get; set; }
    public int PlannedEliminations { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal PlannedAverageSalary { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal PlannedTotalCost { get; set; }

    // Timeline
    [Range(1, 4, ErrorMessage = "Quarter must be between 1 and 4")]
    public int? Quarter { get; set; } // Which quarter to fill
    
    public DateTime? TargetFillDate { get; set; }

    // Priority
    public BudgetPriority Priority { get; set; }
    public bool IsCritical { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

/// <summary>
/// Medical / mental / health / special requirement for a job (e.g. "not suitable for anyone
/// with a respiratory condition" for a dusty environment). Captured on its own tab; the rationale
/// explains why the requirement exists. Physical-structure requirements live on
/// <see cref="JobPhysicalDemand"/> (per stakeholder decision), not here.
/// </summary>
public class JobMedicalRequirement : TenantEntity
{
    public Guid JobDescriptionId { get; set; }

    [ForeignKey(nameof(JobDescriptionId))]
    public virtual JobDescription JobDescription { get; set; } = null!;

    [Required]
    public MedicalRequirementCategory Category { get; set; }

    [Required(ErrorMessage = "Requirement description is required")]
    [MaxLength(1000)]
    public string RequirementDescription { get; set; } = string.Empty;

    /// <summary>Why this requirement exists (the justification).</summary>
    [MaxLength(1000)]
    public string? Rationale { get; set; }

    /// <summary>Conditions that would make a candidate unsuitable, e.g. "asthma / chronic respiratory illness".</summary>
    [MaxLength(1000)]
    public string? Contraindications { get; set; }

    public bool IsMandatory { get; set; } = true;
}
