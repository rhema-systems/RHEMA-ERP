using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.HR.SuccessionPlanning;

/// <summary>
/// Succession plan for critical positions
/// </summary>
public class SuccessionPlan : TenantEntity
{
    public string PlanNumber { get; set; } = string.Empty;

    // Target Position (the position being planned for)
    public Guid PositionId { get; set; }
    public EmployeePosition Position { get; set; } = null!;

    public Guid? CurrentIncumbentId { get; set; }
    public Employee? CurrentIncumbent { get; set; }

    // Plan Details
    public int PlanYear { get; set; }
    public SuccessionPlanStatus Status { get; set; }

    // Risk Assessment
    public PositionCriticality Criticality { get; set; }
    public SuccessionRisk RiskLevel { get; set; }
    public string? RiskAssessmentNotes { get; set; }

    // Incumbent Status
    public DateTime? IncumbentRetirementDate { get; set; }
    public DateTime? AnticipatedVacancyDate { get; set; }
    public VacancyReason? AnticipatedVacancyReason { get; set; }

    // Successor Readiness
    public bool HasReadySuccessor { get; set; }
    public int NumberOfIdentifiedSuccessors { get; set; }

    // Development Needs
    public string? KeyCompetenciesRequired { get; set; }
    public string? DevelopmentNeeds { get; set; }
    public string? RecommendedActions { get; set; }

    // Timeline
    public DateTime? TargetSuccessionDate { get; set; }
    public int? EstimatedTimeToReadyMonths { get; set; }

    // Approval
    public Guid? ReviewedById { get; set; }
    public Employee? ReviewedBy { get; set; }
    public DateTime? ReviewDate { get; set; }

    public Guid? ApprovedById { get; set; }
    public Employee? ApprovedBy { get; set; }
    public DateTime? ApprovalDate { get; set; }

    // Next Review
    public DateTime? NextReviewDate { get; set; }

    // Relations
    public ICollection<SuccessionCandidate> Candidates { get; set; } = new List<SuccessionCandidate>();
    public ICollection<SuccessionAction> Actions { get; set; } = new List<SuccessionAction>();
}

/// <summary>
/// Potential successor for a position
/// </summary>
public class SuccessionCandidate : TenantEntity
{
    public Guid SuccessionPlanId { get; set; }
    public SuccessionPlan SuccessionPlan { get; set; } = null!;

    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    // Candidate Details
    public CandidateType Type { get; set; } // Internal, External, Emergency
    public int Rank { get; set; } // Priority ranking (1 = first choice)

    // Readiness Assessment
    public ReadinessLevel CurrentReadiness { get; set; }
    public DateTime? ReadyByDate { get; set; }
    public int? MonthsToReady { get; set; }

    // Performance & Potential
    public PerformanceRating? LatestPerformanceRating { get; set; }
    public PotentialRating? PotentialRating { get; set; }

    // Strengths & Gaps
    public string? Strengths { get; set; }
    public string? DevelopmentGaps { get; set; }
    public string? DevelopmentPlan { get; set; }

    // Experience
    public int YearsInCurrentRole { get; set; }
    public int YearsWithCompany { get; set; }
    public bool HasRelevantExperience { get; set; }

    // Mobility
    public bool WillingToRelocate { get; set; }
    public bool AvailableForPromotion { get; set; }
    public DateTime? AvailableFrom { get; set; }

    // Risk Factors
    public RetentionRisk RetentionRisk { get; set; }
    public string? RiskMitigationPlan { get; set; }

    // Assessment
    public Guid? AssessedById { get; set; }
    public Employee? AssessedBy { get; set; }
    public DateTime? AssessmentDate { get; set; }
    public string? AssessmentNotes { get; set; }

    public bool IsRecommended { get; set; }
    public string? RecommendationNotes { get; set; }

    // Tracking
    public bool IsSelected { get; set; }
    public DateTime? SelectionDate { get; set; }
    public bool SuccessionCompleted { get; set; }
    public DateTime? SuccessionDate { get; set; }

    public ICollection<SuccessionDevelopmentActivity> DevelopmentActivities { get; set; } = new List<SuccessionDevelopmentActivity>();
}

public class SuccessionDevelopmentActivity : TenantEntity
{
    public Guid CandidateId { get; set; }
    public SuccessionCandidate Candidate { get; set; } = null!;

    public string ActivityName { get; set; } = string.Empty;
    public DevelopmentActivityType Type { get; set; } // Training, Mentoring, Job Rotation, Project Assignment
    public string? Description { get; set; }

    public DateTime PlannedStartDate { get; set; }
    public DateTime? PlannedEndDate { get; set; }

    public DevelopmentActivityStatus Status { get; set; }

    public DateTime? ActualStartDate { get; set; }
    public DateTime? ActualEndDate { get; set; }

    public string? Outcome { get; set; }
    public bool CompetencyGained { get; set; }

    public Guid? SupervisorId { get; set; }
    public Employee? Supervisor { get; set; }
}

public class SuccessionAction : TenantEntity
{
    public Guid SuccessionPlanId { get; set; }
    public SuccessionPlan SuccessionPlan { get; set; } = null!;

    public string ActionDescription { get; set; } = string.Empty;
    public ActionType Type { get; set; } // Development, Recruitment, Retention
    public ActionPriority Priority { get; set; }

    public Guid? ResponsiblePersonId { get; set; }
    public Employee? ResponsiblePerson { get; set; }

    public DateTime? DueDate { get; set; }
    public ActionStatus Status { get; set; }

    public DateTime? CompletionDate { get; set; }
    public string? CompletionNotes { get; set; }
}

/// <summary>
/// Talent pool for high-potential employees
/// </summary>
public class TalentPool : TenantEntity
{
    public string PoolName { get; set; } = string.Empty;
    public string? Description { get; set; }

    public TalentPoolType Type { get; set; } // High Potential, Leadership, Technical Expert
    public bool IsActive { get; set; }

    public Guid OwnerId { get; set; }
    public Employee Owner { get; set; } = null!;

    public ICollection<TalentPoolMember> Members { get; set; } = new List<TalentPoolMember>();
}

public class TalentPoolMember : TenantEntity
{
    public Guid TalentPoolId { get; set; }
    public TalentPool TalentPool { get; set; } = null!;

    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public DateTime AddedDate { get; set; }
    public Guid AddedById { get; set; }
    public Employee AddedBy { get; set; } = null!;

    public string? Justification { get; set; }

    public PerformanceRating? PerformanceRating { get; set; }
    public PotentialRating? PotentialRating { get; set; }

    public bool IsActive { get; set; }
    public DateTime? RemovedDate { get; set; }
    public string? RemovalReason { get; set; }
}

/// <summary>
/// 9-Box Grid Assessment (Performance vs Potential)
/// </summary>
public class NineBoxAssessment : TenantEntity
{
    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    public int Year { get; set; }

    public PerformanceRating PerformanceRating { get; set; }
    public PotentialRating PotentialRating { get; set; }

    public NineBoxCategory Category { get; set; } // Top Talent, Solid Professional, etc.
    public string? CategoryDescription { get; set; }

    public string? DevelopmentRecommendations { get; set; }
    public string? CareerPathRecommendations { get; set; }
    public string? RetentionStrategies { get; set; }

    public Guid AssessedById { get; set; }
    public Employee AssessedBy { get; set; } = null!;
    public DateTime AssessmentDate { get; set; }

    public Guid? CalibratedById { get; set; }
    public Employee? CalibratedBy { get; set; }
    public DateTime? CalibrationDate { get; set; }
}