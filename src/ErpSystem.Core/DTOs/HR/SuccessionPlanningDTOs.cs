using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.HR;

// List DTO
public class SuccessionPlanListDto
{
    public Guid Id { get; set; }
    public string PlanNumber { get; set; }
    public string PositionName { get; set; }
    public string Department { get; set; }
    public int PlanYear { get; set; }
    public PositionCriticality Criticality { get; set; }
    public string CriticalityName { get; set; }
    public SuccessionRisk RiskLevel { get; set; }
    public string RiskLevelName { get; set; }
    public string CurrentIncumbentName { get; set; }
    public int ReadyNowCandidates { get; set; }
    public int TotalCandidates { get; set; }
    public SuccessionPlanStatus Status { get; set; }
    public string StatusName { get; set; }
}

// Detail DTO
public class SuccessionPlanDetailDto
{
    public Guid Id { get; set; }
    public string PlanNumber { get; set; }

    // Position Info
    public Guid PositionId { get; set; }
    public string PositionName { get; set; }
    public string Department { get; set; }

    // Current Incumbent
    public Guid? CurrentIncumbentId { get; set; }
    public string CurrentIncumbentName { get; set; }
    public int? IncumbentAge { get; set; }
    public int? IncumbentYearsInPosition { get; set; }
    public DateTime? IncumbentRetirementDate { get; set; }

    // Plan Details
    public int PlanYear { get; set; }
    public PositionCriticality Criticality { get; set; }
    public string CriticalityName { get; set; }
    public SuccessionRisk RiskLevel { get; set; }
    public string RiskLevelName { get; set; }
    public VacancyReason? AnticipatedVacancyReason { get; set; }
    public string AnticipatedVacancyReasonName { get; set; }
    public DateTime? AnticipatedVacancyDate { get; set; }

    // Analysis
    public string RiskAssessmentNotes { get; set; }
    public string KeyCompetenciesRequired { get; set; }
    public string DevelopmentNeeds { get; set; }
    public string RecommendedActions { get; set; }

    // Status
    public SuccessionPlanStatus Status { get; set; }
    public string StatusName { get; set; }

    // Workflow
    public Guid? ReviewedById { get; set; }
    public string ReviewedByName { get; set; }
    public DateTime? ReviewedDate { get; set; }
    public Guid? ApprovedById { get; set; }
    public string ApprovedByName { get; set; }
    public DateTime? ApprovedDate { get; set; }

    // Collections
    public List<SuccessionCandidateDto> Candidates { get; set; }
    public List<SuccessionActionDto> Actions { get; set; }

    // Audit
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

// Create DTO
public class CreateSuccessionPlanDto
{
    public Guid PositionId { get; set; }
    public Guid? CurrentIncumbentId { get; set; }
    public int PlanYear { get; set; }
    public PositionCriticality Criticality { get; set; }
    public SuccessionRisk RiskLevel { get; set; }
    public VacancyReason? AnticipatedVacancyReason { get; set; }
    public DateTime? AnticipatedVacancyDate { get; set; }
    public string RiskAssessmentNotes { get; set; }
    public string KeyCompetenciesRequired { get; set; }
    public string DevelopmentNeeds { get; set; }
    public string RecommendedActions { get; set; }
}

// Update DTO
public class UpdateSuccessionPlanDto
{
    public Guid Id { get; set; }
    public PositionCriticality Criticality { get; set; }
    public SuccessionRisk RiskLevel { get; set; }
    public VacancyReason? AnticipatedVacancyReason { get; set; }
    public DateTime? AnticipatedVacancyDate { get; set; }
    public string RiskAssessmentNotes { get; set; }
    public string KeyCompetenciesRequired { get; set; }
    public string DevelopmentNeeds { get; set; }
    public string RecommendedActions { get; set; }
}

// Succession Candidate DTOs
public class SuccessionCandidateDto
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; }
    public string EmployeeNumber { get; set; }
    public string CurrentPosition { get; set; }
    public string Department { get; set; }
    public CandidateType CandidateType { get; set; }
    public string CandidateTypeName { get; set; }
    public ReadinessLevel ReadinessLevel { get; set; }
    public string ReadinessLevelName { get; set; }
    public PotentialRating PotentialRating { get; set; }
    public string PotentialRatingName { get; set; }
    public PerformanceRating? CurrentPerformanceRating { get; set; }
    public string CurrentPerformanceRatingName { get; set; }
    public RetentionRisk RetentionRisk { get; set; }
    public string RetentionRiskName { get; set; }
    public string Strengths { get; set; }
    public string DevelopmentGaps { get; set; }
    public int DevelopmentActivitiesCount { get; set; }
    public bool IsRecommended { get; set; }
}

public class SuccessionCandidateDetailDto
{
    public Guid Id { get; set; }

    // Employee Info
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; }
    public string EmployeeNumber { get; set; }
    public string CurrentPosition { get; set; }
    public string Department { get; set; }
    public int? YearsWithCompany { get; set; }
    public int? YearsInCurrentRole { get; set; }

    // Assessment
    public CandidateType CandidateType { get; set; }
    public string CandidateTypeName { get; set; }
    public ReadinessLevel ReadinessLevel { get; set; }
    public string ReadinessLevelName { get; set; }
    public PotentialRating PotentialRating { get; set; }
    public string PotentialRatingName { get; set; }
    public PerformanceRating? CurrentPerformanceRating { get; set; }
    public string CurrentPerformanceRatingName { get; set; }
    public RetentionRisk RetentionRisk { get; set; }
    public string RetentionRiskName { get; set; }

    // Analysis
    public string Strengths { get; set; }
    public string DevelopmentGaps { get; set; }
    public string DevelopmentPlan { get; set; }
    public string RiskMitigationPlan { get; set; }

    // Assessment Details
    public Guid? AssessedById { get; set; }
    public string AssessedByName { get; set; }
    public DateTime? AssessmentDate { get; set; }
    public string AssessmentNotes { get; set; }

    public bool IsRecommended { get; set; }
    public string RecommendationNotes { get; set; }

    // Collections
    public List<SuccessionDevelopmentActivityDto> DevelopmentActivities { get; set; }

    // Audit
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class AddSuccessionCandidateDto
{
    public Guid SuccessionPlanId { get; set; }
    public Guid EmployeeId { get; set; }
    public CandidateType CandidateType { get; set; }
    public ReadinessLevel ReadinessLevel { get; set; }
    public PotentialRating PotentialRating { get; set; }
    public RetentionRisk RetentionRisk { get; set; }
    public string Strengths { get; set; }
    public string DevelopmentGaps { get; set; }
    public string DevelopmentPlan { get; set; }
    public string RiskMitigationPlan { get; set; }
    public bool IsRecommended { get; set; }
    public string RecommendationNotes { get; set; }
}

// Development Activity DTOs
public class SuccessionDevelopmentActivityDto
{
    public Guid Id { get; set; }
    public DevelopmentActivityType ActivityType { get; set; }
    public string ActivityTypeName { get; set; }
    public string ActivityName { get; set; }
    public string Description { get; set; }
    public DateTime PlannedStartDate { get; set; }
    public DateTime PlannedEndDate { get; set; }
    public DateTime? ActualStartDate { get; set; }
    public DateTime? ActualEndDate { get; set; }
    public DevelopmentActivityStatus Status { get; set; }
    public string StatusName { get; set; }
    public string Outcome { get; set; }
    public Guid? SupervisorId { get; set; }
    public string SupervisorName { get; set; }
}

public class CreateDevelopmentActivityDto
{
    public Guid CandidateId { get; set; }
    public DevelopmentActivityType ActivityType { get; set; }
    public string ActivityName { get; set; }
    public string Description { get; set; }
    public DateTime PlannedStartDate { get; set; }
    public DateTime PlannedEndDate { get; set; }
    public Guid? SupervisorId { get; set; }
}

// Succession Action DTOs
public class SuccessionActionDto
{
    public Guid Id { get; set; }
    public ActionType ActionType { get; set; }
    public string ActionTypeName { get; set; }
    public string ActionDescription { get; set; }
    public ActionPriority Priority { get; set; }
    public string PriorityName { get; set; }
    public Guid? ResponsiblePersonId { get; set; }
    public string ResponsiblePersonName { get; set; }
    public DateTime TargetDate { get; set; }
    public ActionStatus Status { get; set; }
    public string StatusName { get; set; }
    public DateTime? CompletionDate { get; set; }
    public string CompletionNotes { get; set; }
}

// Talent Pool DTOs
public class TalentPoolListDto
{
    public Guid Id { get; set; }
    public string PoolName { get; set; }
    public TalentPoolType PoolType { get; set; }
    public string PoolTypeName { get; set; }
    public string OwnerName { get; set; }
    public int TotalMembers { get; set; }
    public bool IsActive { get; set; }
}

public class TalentPoolDetailDto
{
    public Guid Id { get; set; }
    public string PoolName { get; set; }
    public string Description { get; set; }
    public TalentPoolType PoolType { get; set; }
    public string PoolTypeName { get; set; }
    public Guid OwnerId { get; set; }
    public string OwnerName { get; set; }
    public bool IsActive { get; set; }

    // Collections
    public List<TalentPoolMemberDto> Members { get; set; }

    // Audit
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class TalentPoolMemberDto
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; }
    public string EmployeeNumber { get; set; }
    public string Position { get; set; }
    public string Department { get; set; }
    public DateTime DateAdded { get; set; }
    public string AddedByName { get; set; }
    public string Justification { get; set; }
    public bool IsActive { get; set; }
}

// Nine Box Assessment DTOs
public class NineBoxAssessmentDto
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; }
    public string EmployeeNumber { get; set; }
    public string Position { get; set; }
    public int Year { get; set; }
    public PerformanceRating PerformanceRating { get; set; }
    public string PerformanceRatingName { get; set; }
    public PotentialRating PotentialRating { get; set; }
    public string PotentialRatingName { get; set; }
    public NineBoxCategory NineBoxCategory { get; set; }
    public string NineBoxCategoryName { get; set; }
    public string CategoryDescription { get; set; }
    public string AssessedByName { get; set; }
    public DateTime AssessmentDate { get; set; }
}

// Dashboard DTO
public class SuccessionPlanningDashboardDto
{
    public int TotalCriticalPositions { get; set; }
    public int PositionsWithSuccessors { get; set; }
    public int PositionsWithoutSuccessors { get; set; }
    public int ReadyNowCandidates { get; set; }
    public int HighRiskPositions { get; set; }
    public Dictionary<SuccessionRisk, int> PositionsByRisk { get; set; }
    public Dictionary<ReadinessLevel, int> CandidatesByReadiness { get; set; }
    public List<SuccessionPlanListDto> HighRiskPlans { get; set; }
    public List<SuccessionPlanListDto> VacanciesWithin12Months { get; set; }
}