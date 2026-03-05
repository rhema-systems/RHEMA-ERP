using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.HR.JobAnalysis;

/// <summary>
/// Formal job description document
/// </summary>
public class JobDescription : TenantEntity
{
    public string JobDescriptionNumber { get; set; } = string.Empty;

    // Position
    public Guid PositionId { get; set; }
    public EmployeePosition Position { get; set; } = null!;

    public string JobTitle { get; set; } = string.Empty;
    public string? AlternateTitle { get; set; }

    // Organization
    public Guid DepartmentId { get; set; }
    public Department Department { get; set; } = null!;

    public Guid? SectionId { get; set; }
    public Section? Section { get; set; }

    public Guid? UnitId { get; set; }
    public Unit? Unit { get; set; }

    // Reporting Structure
    public Guid? ReportsToPositionId { get; set; }
    public EmployeePosition? ReportsToPosition { get; set; }

    public Guid? SupervisesPositionId { get; set; }
    public int NumberOfDirectReports { get; set; }
    public int NumberOfIndirectReports { get; set; }

    // Version Control
    public int VersionNumber { get; set; }
    public DateTime EffectiveDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? RevisionReason { get; set; }

    public Guid? SupersededByVersionId { get; set; }
    public JobDescription? SupersededByVersion { get; set; }

    // Job Summary
    public string JobPurpose { get; set; } = string.Empty;
    public string JobSummary { get; set; } = string.Empty;

    // Classification
    public JobLevel Level { get; set; }
    public JobGrade Grade { get; set; }
    // public EmploymentType EmploymentType { get; set; }
    public bool IsExempt { get; set; } // Exempt from overtime
    public FLSAClassification? FlsaClassification { get; set; }

    // Compensation
    public decimal? MinSalary { get; set; }
    public decimal? MidSalary { get; set; }
    public decimal? MaxSalary { get; set; }
    public string? SalaryGrade { get; set; }

    // Work Environment
    public string? WorkLocation { get; set; }
    public string? WorkSchedule { get; set; }
    public bool RequiresTravel { get; set; }
    public int? TravelPercentage { get; set; }
    public string? PhysicalDemands { get; set; }
    public string? WorkingConditions { get; set; }

    // Decision Making Authority
    public string? DecisionMakingAuthority { get; set; }
    public string? FinancialAuthority { get; set; }
    public decimal? BudgetResponsibility { get; set; }

    // Approval
    public JobDescriptionStatus Status { get; set; }

    public Guid? PreparedById { get; set; }
    public Employee? PreparedBy { get; set; }
    public DateTime? PreparedDate { get; set; }

    public Guid? ReviewedById { get; set; }
    public Employee? ReviewedBy { get; set; }
    public DateTime? ReviewedDate { get; set; }

    public Guid? ApprovedById { get; set; }
    public Employee? ApprovedBy { get; set; }
    public DateTime? ApprovalDate { get; set; }

    // Next Review
    public DateTime? NextReviewDate { get; set; }
    public int ReviewCycleMonths { get; set; } = 24; // Default 2 years

    // Relations
    public ICollection<JobResponsibility> Responsibilities { get; set; } = new List<JobResponsibility>();
    public ICollection<JobQualification> Qualifications { get; set; } = new List<JobQualification>();
    public ICollection<JobCompetency> Competencies { get; set; } = new List<JobCompetency>();
    public ICollection<JobRelationship> Relationships { get; set; } = new List<JobRelationship>();
    public ICollection<JobDescriptionAttachment> Attachments { get; set; } = new List<JobDescriptionAttachment>();
}

public class JobResponsibility : TenantEntity
{
    public Guid JobDescriptionId { get; set; }
    public JobDescription JobDescription { get; set; } = null!;

    public string ResponsibilityDescription { get; set; } = string.Empty;
    public ResponsibilityType Type { get; set; } // Core, Secondary, Occasional
    public int PercentageOfTime { get; set; }
    public bool IsEssentialFunction { get; set; }
    public int DisplayOrder { get; set; }
}

public class JobQualification : TenantEntity
{
    public Guid JobDescriptionId { get; set; }
    public JobDescription JobDescription { get; set; } = null!;

    public QualificationType Type { get; set; } // Education, Experience, Certification, License
    public string Description { get; set; } = string.Empty;
    public bool IsRequired { get; set; } // Required vs Preferred
    public int DisplayOrder { get; set; }
}

public class JobCompetency : TenantEntity
{
    public Guid JobDescriptionId { get; set; }
    public JobDescription JobDescription { get; set; } = null!;

    public string CompetencyName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public CompetencyType Type { get; set; } // Technical, Behavioral, Leadership
    public ProficiencyLevel RequiredLevel { get; set; }
    public bool IsCritical { get; set; }
    public int DisplayOrder { get; set; }
}

public class JobRelationship : TenantEntity
{
    public Guid JobDescriptionId { get; set; }
    public JobDescription JobDescription { get; set; } = null!;

    public RelationshipType Type { get; set; } // Internal, External, Stakeholder
    public string RelationshipWith { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public InteractionFrequency Frequency { get; set; }
    public int DisplayOrder { get; set; }
}

public class JobDescriptionAttachment : TenantEntity
{
    public Guid JobDescriptionId { get; set; }
    public JobDescription JobDescription { get; set; } = null!;

    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime UploadDate { get; set; }
}

/// <summary>
/// Job evaluation for grading/classification
/// </summary>
public class JobEvaluation : TenantEntity
{
    public string EvaluationNumber { get; set; } = string.Empty;

    public Guid JobDescriptionId { get; set; }
    public JobDescription JobDescription { get; set; } = null!;

    public DateTime EvaluationDate { get; set; }
    public JobEvaluationMethod Method { get; set; } // Point Factor, Ranking, Classification

    // Evaluation Factors
    public int? EducationPoints { get; set; }
    public int? ExperiencePoints { get; set; }
    public int? ComplexityPoints { get; set; }
    public int? ResponsibilityPoints { get; set; }
    public int? SupervisionPoints { get; set; }
    public int? WorkingConditionsPoints { get; set; }

    public int TotalPoints { get; set; }
    public string? RecommendedGrade { get; set; }
    public decimal? RecommendedSalaryMin { get; set; }
    public decimal? RecommendedSalaryMax { get; set; }

    public Guid EvaluatorId { get; set; }
    public Employee Evaluator { get; set; } = null!;

    public JobEvaluationStatus Status { get; set; }
    public Guid? ApprovedById { get; set; }
    public Employee? ApprovedBy { get; set; }
    public DateTime? ApprovalDate { get; set; }

    public string? Notes { get; set; }
}

/// <summary>
/// Manpower planning and budgeting
/// </summary>
public class ManpowerBudget : TenantEntity
{
    public string BudgetNumber { get; set; } = string.Empty;
    public int FiscalYear { get; set; }

    public Guid? DepartmentId { get; set; }
    public Department? Department { get; set; }

    public Guid? DivisionId { get; set; }
    public Division? Division { get; set; }

    // If null, this is company-wide budget
    public bool IsCompanyWide => !DepartmentId.HasValue && !DivisionId.HasValue;

    public ManpowerBudgetStatus Status { get; set; }

    // Planning Period
    public DateTime PeriodStartDate { get; set; }
    public DateTime PeriodEndDate { get; set; }

    // Current State
    public int CurrentHeadcount { get; set; }
    public decimal CurrentSalaryCost { get; set; }

    // Planned State
    public int PlannedHeadcount { get; set; }
    public decimal PlannedSalaryCost { get; set; }

    // Changes
    public int PlannedNewHires { get; set; }
    public int PlannedTerminations { get; set; }
    public int PlannedPromotions { get; set; }
    public int PlannedTransfers { get; set; }

    // Budget Allocation
    public decimal SalaryBudget { get; set; }
    public decimal BenefitsBudget { get; set; }
    public decimal RecruitmentBudget { get; set; }
    public decimal TrainingBudget { get; set; }
    public decimal TotalBudget { get; set; }

    // Variance Tracking
    public decimal ActualSpent { get; set; }
    public decimal Variance { get; set; }

    // Justification
    public string? BusinessJustification { get; set; }
    public string? StrategicAlignment { get; set; }

    public Guid? ApprovedById { get; set; }
    public Employee? ApprovedBy { get; set; }
    public DateTime? ApprovalDate { get; set; }

    public ICollection<ManpowerBudgetLine> BudgetLines { get; set; } = new List<ManpowerBudgetLine>();
}

public class ManpowerBudgetLine : TenantEntity
{
    public Guid ManpowerBudgetId { get; set; }
    public ManpowerBudget ManpowerBudget { get; set; } = null!;

    public Guid PositionId { get; set; }
    public EmployeePosition Position { get; set; } = null!;

    // Current
    public int CurrentCount { get; set; }
    public int CurrentFilled { get; set; }
    public int CurrentVacant { get; set; }
    public decimal CurrentAverageSalary { get; set; }
    public decimal CurrentTotalCost { get; set; }

    // Planned
    public int PlannedCount { get; set; }
    public int PlannedNewPositions { get; set; }
    public int PlannedEliminations { get; set; }
    public decimal PlannedAverageSalary { get; set; }
    public decimal PlannedTotalCost { get; set; }

    // Timeline
    public int? Quarter { get; set; } // Which quarter to fill
    public DateTime? TargetFillDate { get; set; }

    // Priority
    public BudgetPriority Priority { get; set; }
    public bool IsCritical { get; set; }

    public string? Notes { get; set; }
}
