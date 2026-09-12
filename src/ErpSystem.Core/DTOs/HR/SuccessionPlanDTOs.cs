using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Entities.HR.PromotionTransfer;

namespace ErpSystem.Core.DTOs.HR;

// ============================================================================
// SUCCESSION PLAN DTOs
// ============================================================================

#region Succession Plan

public class SuccessionPlanDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string PlanNumber { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public string? Description { get; set; }

    // Position
    public Guid PositionId { get; set; }
    public string PositionTitle { get; set; } = string.Empty;

    // Incumbent
    public Guid? CurrentIncumbentId { get; set; }
    public string? CurrentIncumbentName { get; set; }
    public string? CurrentIncumbentNumber { get; set; }
    public int? IncumbentAge { get; set; }
    public int? IncumbentServiceYearsLeft { get; set; }
    public DateTime? IncumbentEffectiveRetirementDate { get; set; }

    // Plan details
    public int PlanYear { get; set; }
    public int VersionNumber { get; set; }
    public bool IsActiveVersion { get; set; }
    public Guid? SupersededByPlanId { get; set; }
    public string? SupersededByPlanNumber { get; set; }

    // Status & Risk
    public SuccessionPlanStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public PositionCriticality Criticality { get; set; }
    public string CriticalityName => Criticality.ToString();
    public SuccessionRisk RiskLevel { get; set; }
    public string RiskLevelName => RiskLevel.ToString();
    public string? RiskAssessmentNotes { get; set; }
    public string? BusinessImpactIfVacant { get; set; }

    // Incumbent status
    public DateTime? IncumbentRetirementDate { get; set; }
    public DateTime? AnticipatedVacancyDate { get; set; }
    public VacancyReason? AnticipatedVacancyReason { get; set; }
    public string? AnticipatedVacancyReasonName => AnticipatedVacancyReason?.ToString();
    public string? IncumbentSuccessionNotes { get; set; }

    // Derived readiness flags
    public bool HasReadyNowSuccessor { get; set; }
    public int NumberOfIdentifiedSuccessors { get; set; }
    public bool HasEmergencySuccessor { get; set; }

    // Emergency
    public Guid? EmergencySuccessorId { get; set; }
    public string? EmergencySuccessorName { get; set; }
    public string? EmergencyProtocol { get; set; }

    // Timeline
    public DateTime? TargetSuccessionDate { get; set; }
    public int? EstimatedTimeToReadyMonths { get; set; }

    // Approval
    public Guid? ReviewedById { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewDate { get; set; }
    public Guid? ApprovedById { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovalDate { get; set; }

    // Next review
    public int ReviewFrequencyMonths { get; set; }
    public DateTime? NextReviewDate { get; set; }

    // Child collections
    public List<SuccessionCompetencyRequirementDto> CompetencyRequirements { get; set; } = new();
    public List<SuccessionCandidateSummaryDto> Candidates { get; set; } = new();
    public List<SuccessionActionSummaryDto> Actions { get; set; } = new();
    public List<SuccessionDocumentDto> Documents { get; set; } = new();
}

public class SuccessionPlanSummaryDto
{
    public Guid Id { get; set; }
    public string PlanNumber { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public int PlanYear { get; set; }
    public int VersionNumber { get; set; }
    public bool IsActiveVersion { get; set; }

    /// <summary>The position the plan is for. Present so a register row can link to it.</summary>
    public Guid PositionId { get; set; }

    public string PositionTitle { get; set; } = string.Empty;
    public string? CurrentIncumbentName { get; set; }
    public SuccessionPlanStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public PositionCriticality Criticality { get; set; }
    public string CriticalityName => Criticality.ToString();
    public SuccessionRisk RiskLevel { get; set; }
    public string RiskLevelName => RiskLevel.ToString();
    public bool HasReadyNowSuccessor { get; set; }
    public int NumberOfIdentifiedSuccessors { get; set; }
    public bool HasEmergencySuccessor { get; set; }
    public DateTime? NextReviewDate { get; set; }
}

public class CreateSuccessionPlanDto : CreateDtoBase
{
    [Required]
    [MaxLength(100)]
    public string PlanName { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [Required]
    public Guid PositionId { get; set; }

    public Guid? CurrentIncumbentId { get; set; }

    [Required]
    [Range(2000, 2100)]
    public int PlanYear { get; set; }

    [Required]
    public PositionCriticality Criticality { get; set; }

    [Required]
    public SuccessionRisk RiskLevel { get; set; }

    [MaxLength(2000)]
    public string? RiskAssessmentNotes { get; set; }

    [MaxLength(2000)]
    public string? BusinessImpactIfVacant { get; set; }

    public DateTime? IncumbentRetirementDate { get; set; }
    public DateTime? AnticipatedVacancyDate { get; set; }
    public VacancyReason? AnticipatedVacancyReason { get; set; }

    [MaxLength(2000)]
    public string? IncumbentSuccessionNotes { get; set; }

    public Guid? EmergencySuccessorId { get; set; }

    [MaxLength(2000)]
    public string? EmergencyProtocol { get; set; }

    public DateTime? TargetSuccessionDate { get; set; }
    public int? EstimatedTimeToReadyMonths { get; set; }

    [Range(1, 60)]
    public int ReviewFrequencyMonths { get; set; } = 12;

    public DateTime? NextReviewDate { get; set; }
}

public class UpdateSuccessionPlanDto : UpdateDtoBase
{
    [Required]
    [MaxLength(100)]
    public string PlanName { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    public Guid? CurrentIncumbentId { get; set; }

    [Required]
    public PositionCriticality Criticality { get; set; }

    [Required]
    public SuccessionRisk RiskLevel { get; set; }

    [MaxLength(2000)]
    public string? RiskAssessmentNotes { get; set; }

    [MaxLength(2000)]
    public string? BusinessImpactIfVacant { get; set; }

    public DateTime? IncumbentRetirementDate { get; set; }
    public DateTime? AnticipatedVacancyDate { get; set; }
    public VacancyReason? AnticipatedVacancyReason { get; set; }

    [MaxLength(2000)]
    public string? IncumbentSuccessionNotes { get; set; }

    public Guid? EmergencySuccessorId { get; set; }

    [MaxLength(2000)]
    public string? EmergencyProtocol { get; set; }

    public DateTime? TargetSuccessionDate { get; set; }
    public int? EstimatedTimeToReadyMonths { get; set; }

    [Range(1, 60)]
    public int ReviewFrequencyMonths { get; set; } = 12;

    public DateTime? NextReviewDate { get; set; }
}

/// <remarks>
/// ⚠ <c>ReviewedById</c> and <c>ReviewDate</c> were removed deliberately. Both were caller-declared:
/// the reviewer's identity came from the request body, so any caller could record a review under a
/// colleague's name, and the date could be backdated at will. Who reviewed a succession plan and
/// when are facts the server knows and the client cannot — the actor comes from the token and the
/// date from the clock. See <c>plans/HR-Area-13-Succession-Build-Plan.md</c> §3.5.
/// </remarks>
/// <remarks>
/// ⚠ Replaces <c>ReviewSuccessionPlanDto</c>, which carried a <c>NewStatus</c> the caller chose.
/// That let a reviewer move a plan to any status they liked — including straight to Approved,
/// around whatever approval the organisation had configured. A status the caller picks is not an
/// approval decision, it is a way past one. Rejection is now the only non-approval outcome, and the
/// engine owns the transition.
/// </remarks>
public class RejectSuccessionPlanDto
{
    [Required]
    public Guid PlanId { get; set; }

    [Required]
    [MaxLength(2000)]
    public string RejectionReason { get; set; } = string.Empty;
}

/// <remarks>
/// ⚠ <c>ApprovedById</c> and <c>ApprovalDate</c> were removed: approval names a person as the
/// intended successor to a post, and an approver the caller chose for themselves is not an
/// approval. The approver is the signed-in user and the date is the clock.
///
/// ⚠ Since slice 8 this is an **approval step on the generic workflow engine**, not a status write.
/// A multi-step definition leaves the plan at <c>UnderReview</c> after an intermediate approval, so
/// the caller must re-read rather than assume the plan is now Approved.
/// </remarks>
public class ApproveSuccessionPlanDto
{
    [Required]
    public Guid PlanId { get; set; }

    [MaxLength(2000)]
    public string? ApprovalNotes { get; set; }
}

#endregion

// ============================================================================
// SUCCESSION COMPETENCY REQUIREMENT DTOs
// ============================================================================

#region Succession Competency Requirement

/// <summary>Lightweight read model for competency dropdown lookups.</summary>
public class CompetencyLookupDto
{
    public Guid   Id                  { get; set; }
    public string Name                { get; set; } = string.Empty;
    public string Code                { get; set; } = string.Empty;
    public CompetencyCategory CompetencyCategory { get; set; }
    public int    ProficiencyScaleMax  { get; set; }
}

public class SuccessionCompetencyRequirementDto : BaseDto
{
    public Guid SuccessionPlanId { get; set; }
    public Guid CompetencyId { get; set; }
    public string CompetencyCode { get; set; } = string.Empty;
    public string CompetencyName { get; set; } = string.Empty;
    public CompetencyCategory CompetencyCategory { get; set; }
    public string CompetencyCategoryName => CompetencyCategory.ToString();
    public int RequiredLevel { get; set; }
    public int ProficiencyScaleMax { get; set; }
}

public class CreateSuccessionCompetencyRequirementDto : CreateDtoBase
{
    [Required]
    public Guid SuccessionPlanId { get; set; }

    [Required]
    public Guid CompetencyId { get; set; }

    [Required]
    [Range(1, 10)]
    public int RequiredLevel { get; set; }
}

public class UpdateSuccessionCompetencyRequirementDto : UpdateDtoBase
{
    [Required]
    [Range(1, 10)]
    public int RequiredLevel { get; set; }
}

#endregion

// ============================================================================
// SUCCESSION CANDIDATE DTOs
// ============================================================================

#region Succession Candidate

public class SuccessionCandidateDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid SuccessionPlanId { get; set; }
    public string PlanNumber { get; set; } = string.Empty;

    // Employee
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;
    public string? EmployeePosition { get; set; }
    public string? EmployeeDepartment { get; set; }

    // Demographics — age & years-of-service from the employee; retirement date and active
    // service-years-left are driven by the company HR policy settings (retirement age).
    public DateTime? DateOfBirth { get; set; }
    public int? Age { get; set; }
    public int? YearsOfService { get; set; }
    public DateTime? RetirementDate { get; set; }
    public int? ServiceYearsLeft { get; set; }

    // Talent pool link
    public Guid? TalentPoolMemberId { get; set; }
    public string? TalentPoolName { get; set; }

    // Classification
    public CandidateType Type { get; set; }
    public string TypeName => Type.ToString();
    public int Rank { get; set; }

    // Readiness
    public ReadinessLevel CurrentReadiness { get; set; }
    public string CurrentReadinessName => CurrentReadiness.ToString();
    public DateTime? ReadyByDate { get; set; }
    public int? MonthsToReady { get; set; }
    public bool IsEmergencyOnly { get; set; }

    // Performance & Potential
    public PerformanceRating? LatestPerformanceRating { get; set; }
    public string? LatestPerformanceRatingName => LatestPerformanceRating?.ToString();
    public PotentialRating? PotentialRating { get; set; }
    public string? PotentialRatingName => PotentialRating?.ToString();
    public Guid? TalentReviewRatingId { get; set; }

    // Strengths & Gaps
    public string? Strengths { get; set; }
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

    // Risk
    public RetentionRisk RetentionRisk { get; set; }
    public string RetentionRiskName => RetentionRisk.ToString();
    public string? RiskMitigationPlan { get; set; }

    // Assessment
    public Guid? AssessedById { get; set; }
    public string? AssessedByName { get; set; }
    public DateTime? AssessmentDate { get; set; }
    public string? AssessmentNotes { get; set; }

    // Recommendation
    public bool IsRecommended { get; set; }
    public string? RecommendationNotes { get; set; }
    public DateTime? RecommendationDate { get; set; }
    public Guid? RecommendedById { get; set; }
    public string? RecommendedByName { get; set; }

    // Selection & Completion
    public bool IsSelected { get; set; }
    public DateTime? SelectionDate { get; set; }
    public bool SuccessionCompleted { get; set; }
    public DateTime? SuccessionDate { get; set; }

    // Child collections
    public List<SuccessionDevelopmentActivitySummaryDto> DevelopmentActivities { get; set; } = new();
    public List<SuccessionCandidateGapDto> CompetencyGaps { get; set; } = new();
    public List<SuccessionCandidateFeedbackDto> Feedback { get; set; } = new();
}

public class SuccessionCandidateSummaryDto
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;
    public int? Age { get; set; }
    public int? ServiceYearsLeft { get; set; }
    public CandidateType Type { get; set; }
    public string TypeName => Type.ToString();
    public int Rank { get; set; }
    public ReadinessLevel CurrentReadiness { get; set; }
    public string CurrentReadinessName => CurrentReadiness.ToString();
    public bool IsEmergencyOnly { get; set; }
    public PerformanceRating? LatestPerformanceRating { get; set; }
    public PotentialRating? PotentialRating { get; set; }
    public RetentionRisk RetentionRisk { get; set; }
    public bool IsRecommended { get; set; }
    public bool IsSelected { get; set; }
    public bool SuccessionCompleted { get; set; }

    // Feedback aggregate (for the candidates grid)
    public int FeedbackCount { get; set; }
    public int SupportCount { get; set; }
    public int OpposeCount { get; set; }
}

public class CreateSuccessionCandidateDto : CreateDtoBase
{
    [Required]
    public Guid SuccessionPlanId { get; set; }

    [Required]
    public Guid EmployeeId { get; set; }

    public Guid? TalentPoolMemberId { get; set; }

    [Required]
    public CandidateType Type { get; set; }

    [Required]
    [Range(1, 100)]
    public int Rank { get; set; }

    [Required]
    public ReadinessLevel CurrentReadiness { get; set; }

    public DateTime? ReadyByDate { get; set; }
    public int? MonthsToReady { get; set; }
    public bool IsEmergencyOnly { get; set; }

    public PerformanceRating? LatestPerformanceRating { get; set; }
    public PotentialRating? PotentialRating { get; set; }
    public Guid? TalentReviewRatingId { get; set; }

    [MaxLength(2000)]
    public string? Strengths { get; set; }

    [MaxLength(2000)]
    public string? DevelopmentGaps { get; set; }

    [MaxLength(2000)]
    public string? DevelopmentPlan { get; set; }

    [Range(0, 50)]
    public int YearsInCurrentRole { get; set; }

    [Range(0, 50)]
    public int YearsWithCompany { get; set; }

    public bool HasRelevantExperience { get; set; }

    [MaxLength(2000)]
    public string? RelevantExperienceDetails { get; set; }

    public bool WillingToRelocate { get; set; }
    public bool AvailableForPromotion { get; set; }
    public DateTime? AvailableFrom { get; set; }

    [Required]
    public RetentionRisk RetentionRisk { get; set; }

    [MaxLength(2000)]
    public string? RiskMitigationPlan { get; set; }
}

public class UpdateSuccessionCandidateDto : UpdateDtoBase
{
    [Required]
    public CandidateType Type { get; set; }

    [Required]
    [Range(1, 100)]
    public int Rank { get; set; }

    [Required]
    public ReadinessLevel CurrentReadiness { get; set; }

    public DateTime? ReadyByDate { get; set; }
    public int? MonthsToReady { get; set; }
    public bool IsEmergencyOnly { get; set; }

    public PerformanceRating? LatestPerformanceRating { get; set; }
    public PotentialRating? PotentialRating { get; set; }
    public Guid? TalentReviewRatingId { get; set; }

    [MaxLength(2000)]
    public string? Strengths { get; set; }

    [MaxLength(2000)]
    public string? DevelopmentGaps { get; set; }

    [MaxLength(2000)]
    public string? DevelopmentPlan { get; set; }

    [Range(0, 50)]
    public int YearsInCurrentRole { get; set; }

    [Range(0, 50)]
    public int YearsWithCompany { get; set; }

    public bool HasRelevantExperience { get; set; }

    [MaxLength(2000)]
    public string? RelevantExperienceDetails { get; set; }

    public bool WillingToRelocate { get; set; }
    public bool AvailableForPromotion { get; set; }
    public DateTime? AvailableFrom { get; set; }

    [Required]
    public RetentionRisk RetentionRisk { get; set; }

    [MaxLength(2000)]
    public string? RiskMitigationPlan { get; set; }
}

/// <remarks>
/// ⚠ <c>AssessedById</c> and <c>AssessmentDate</c> were removed deliberately, and this one mattered
/// more than the others. Measured 2026-08-18: a desk actor posted an assessment naming an unrelated
/// employee as the assessor, and it was stored — <c>assessedByName</c> came back as someone who had
/// never seen the candidate. Worse, the same call sets <c>IsRecommended</c>, and being recommended
/// is the gate on <c>SelectCandidateAsync</c>. So a caller could manufacture a recommendation under
/// a colleague's name and then select the candidate on the strength of it. The assessor is the
/// signed-in user and the date is the clock.
/// </remarks>
public class AssessCandidateDto
{
    [Required]
    public Guid CandidateId { get; set; }

    [MaxLength(4000)]
    public string? AssessmentNotes { get; set; }

    public bool IsRecommended { get; set; }

    [MaxLength(2000)]
    public string? RecommendationNotes { get; set; }
}

/// <summary>Wire format for a single candidate rank update in a bulk-reorder request.</summary>
public class CandidateRankUpdateDto
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    [Range(1, 100)]
    public int Rank { get; set; }
}

public class SuccessionCandidateFeedbackDto : BaseDto
{
    public Guid CandidateId { get; set; }
    public Guid ReviewerId { get; set; }
    public string ReviewerName { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
    public FeedbackDisposition Disposition { get; set; }
    public string DispositionName => Disposition.ToString();
}

public class CreateSuccessionCandidateFeedbackDto
{
    [Required]
    [MaxLength(2000)]
    public string Note { get; set; } = string.Empty;

    [Required]
    public FeedbackDisposition Disposition { get; set; }
}

/// <summary>Fit score for one existing plan candidate, computed against the plan's position.</summary>
public class CandidateFitScoreDto
{
    public Guid CandidateId { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public int CurrentRank { get; set; }
    public int FitScore { get; set; }
    public string FitBand { get; set; } = string.Empty;
    public int SuggestedRank { get; set; }
}

#endregion

// ============================================================================
// SUCCESSION CANDIDATE GAP DTOs
// ============================================================================

#region Succession Candidate Gap

public class SuccessionCandidateGapDto : BaseDto
{
    public Guid CandidateId { get; set; }
    public Guid CompetencyId { get; set; }
    public string CompetencyCode { get; set; } = string.Empty;
    public string CompetencyName { get; set; } = string.Empty;
    public CompetencyCategory CompetencyCategory { get; set; }
    public string CompetencyCategoryName => CompetencyCategory.ToString();
    public int RequiredLevel { get; set; }
    public int CurrentLevel { get; set; }
    public int GapSize { get; set; }
    public GapStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public string? GapNotes { get; set; }
    public bool Addressed { get; set; }
    public DateTime? AddressedDate { get; set; }
    public Guid? AddressedByActivityId { get; set; }
    public string? AddressedByActivityName { get; set; }
}

public class CreateSuccessionCandidateGapDto : CreateDtoBase
{
    [Required]
    public Guid CandidateId { get; set; }

    [Required]
    public Guid CompetencyId { get; set; }

    [Required]
    [Range(1, 10)]
    public int RequiredLevel { get; set; }

    [Required]
    [Range(0, 10)]
    public int CurrentLevel { get; set; }

    [MaxLength(2000)]
    public string? GapNotes { get; set; }
}

public class UpdateSuccessionCandidateGapDto : UpdateDtoBase
{
    [Required]
    [Range(0, 10)]
    public int CurrentLevel { get; set; }

    [MaxLength(2000)]
    public string? GapNotes { get; set; }

    public bool Addressed { get; set; }
    public DateTime? AddressedDate { get; set; }
    public Guid? AddressedByActivityId { get; set; }
}

#endregion

// ============================================================================
// SUCCESSION DEVELOPMENT ACTIVITY DTOs
// ============================================================================

#region Succession Development Activity

public class SuccessionDevelopmentActivityDto : BaseDto
{
    public Guid TenantId { get; set; }

    // Ownership
    public Guid? CandidateId { get; set; }
    public string? CandidateEmployeeName { get; set; }
    public Guid? TalentPoolMemberId { get; set; }
    public string? TalentPoolMemberName { get; set; }

    // Activity details
    public string ActivityName { get; set; } = string.Empty;
    public DevelopmentActivityType Type { get; set; }
    public string TypeName => Type.ToString();
    public string? Description { get; set; }

    // Dates
    public DateTime PlannedStartDate { get; set; }
    public DateTime? PlannedEndDate { get; set; }
    public DateTime? ActualStartDate { get; set; }
    public DateTime? ActualEndDate { get; set; }

    // Status & outcome
    public DevelopmentActivityStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public string? Outcome { get; set; }
    public bool CompetencyGained { get; set; }

    // Cost
    public decimal? EstimatedCost { get; set; }
    public decimal? ActualCost { get; set; }
    public string? CurrencyCode { get; set; }

    // Provider
    public Guid? SupervisorId { get; set; }
    public string? SupervisorName { get; set; }
    public Guid? ExternalProviderContactId { get; set; }
    public string? ExternalProviderName { get; set; }

    public string? Notes { get; set; }

    // Child collections
    public List<SuccessionDevelopmentMilestoneDto> Milestones { get; set; } = new();
    public List<SuccessionCandidateGapDto> AddressedGaps { get; set; } = new();
}

public class SuccessionDevelopmentActivitySummaryDto
{
    public Guid Id { get; set; }
    public string ActivityName { get; set; } = string.Empty;
    public DevelopmentActivityType Type { get; set; }
    public string TypeName => Type.ToString();
    public DevelopmentActivityStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public DateTime PlannedStartDate { get; set; }
    public DateTime? PlannedEndDate { get; set; }
    public bool CompetencyGained { get; set; }
}

public class CreateSuccessionDevelopmentActivityDto : CreateDtoBase
{
    // Exactly one of these must be set (validated in application layer)
    public Guid? CandidateId { get; set; }
    public Guid? TalentPoolMemberId { get; set; }

    [Required]
    [MaxLength(200)]
    public string ActivityName { get; set; } = string.Empty;

    [Required]
    public DevelopmentActivityType Type { get; set; }

    [MaxLength(2000)]
    public string? Description { get; set; }

    [Required]
    public DateTime PlannedStartDate { get; set; }

    public DateTime? PlannedEndDate { get; set; }

    public DevelopmentActivityStatus Status { get; set; } = DevelopmentActivityStatus.Planned;

    [Range(0, double.MaxValue)]
    public decimal? EstimatedCost { get; set; }

    [MaxLength(3)]
    public string? CurrencyCode { get; set; }

    public Guid? SupervisorId { get; set; }
    public Guid? ExternalProviderContactId { get; set; }

    [MaxLength(200)]
    public string? ExternalProviderName { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

public class UpdateSuccessionDevelopmentActivityDto : UpdateDtoBase
{
    [Required]
    [MaxLength(200)]
    public string ActivityName { get; set; } = string.Empty;

    [Required]
    public DevelopmentActivityType Type { get; set; }

    [MaxLength(2000)]
    public string? Description { get; set; }

    [Required]
    public DateTime PlannedStartDate { get; set; }

    public DateTime? PlannedEndDate { get; set; }
    public DateTime? ActualStartDate { get; set; }
    public DateTime? ActualEndDate { get; set; }

    [Required]
    public DevelopmentActivityStatus Status { get; set; }

    [MaxLength(2000)]
    public string? Outcome { get; set; }

    public bool CompetencyGained { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? EstimatedCost { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? ActualCost { get; set; }

    [MaxLength(3)]
    public string? CurrencyCode { get; set; }

    public Guid? SupervisorId { get; set; }
    public Guid? ExternalProviderContactId { get; set; }

    [MaxLength(200)]
    public string? ExternalProviderName { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

#endregion

// ============================================================================
// SUCCESSION DEVELOPMENT MILESTONE DTOs
// ============================================================================

#region Succession Development Milestone

public class SuccessionDevelopmentMilestoneDto : BaseDto
{
    public Guid ActivityId { get; set; }
    public string MilestoneName { get; set; } = string.Empty;
    public DateTime TargetDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public bool IsCompleted { get; set; }
    public string? Notes { get; set; }
}

public class CreateSuccessionDevelopmentMilestoneDto : CreateDtoBase
{
    [Required]
    public Guid ActivityId { get; set; }

    [Required]
    [MaxLength(200)]
    public string MilestoneName { get; set; } = string.Empty;

    [Required]
    public DateTime TargetDate { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

public class UpdateSuccessionDevelopmentMilestoneDto : UpdateDtoBase
{
    [Required]
    [MaxLength(200)]
    public string MilestoneName { get; set; } = string.Empty;

    [Required]
    public DateTime TargetDate { get; set; }

    public DateTime? CompletedDate { get; set; }
    public bool IsCompleted { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

#endregion

// ============================================================================
// SUCCESSION ACTION DTOs
// ============================================================================

#region Succession Action

public class SuccessionActionDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid SuccessionPlanId { get; set; }
    public string PlanNumber { get; set; } = string.Empty;

    public Guid? CandidateId { get; set; }
    public string? CandidateEmployeeName { get; set; }

    public string ActionDescription { get; set; } = string.Empty;
    public ActionType Type { get; set; }
    public string TypeName => Type.ToString();
    public ActionPriority Priority { get; set; }
    public string PriorityName => Priority.ToString();

    public Guid? ResponsiblePersonId { get; set; }
    public string? ResponsiblePersonName { get; set; }
    public Guid? AssignedById { get; set; }
    public string? AssignedByName { get; set; }

    public DateTime? DueDate { get; set; }
    public DateTime? StartedDate { get; set; }
    public ActionStatus Status { get; set; }
    public string StatusName => Status.ToString();

    public DateTime? CompletionDate { get; set; }
    public string? CompletionNotes { get; set; }

    public Guid? DependsOnActionId { get; set; }
    public string? DependsOnActionDescription { get; set; }

    public bool WasSuccessful { get; set; }
    public string? OutcomeNotes { get; set; }
}

public class SuccessionActionSummaryDto
{
    public Guid Id { get; set; }
    public string ActionDescription { get; set; } = string.Empty;
    public ActionType Type { get; set; }
    public string TypeName => Type.ToString();
    public ActionPriority Priority { get; set; }
    public string PriorityName => Priority.ToString();
    public ActionStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public DateTime? DueDate { get; set; }
    public string? ResponsiblePersonName { get; set; }
}

public class CreateSuccessionActionDto : CreateDtoBase
{
    [Required]
    public Guid SuccessionPlanId { get; set; }

    public Guid? CandidateId { get; set; }

    [Required]
    [MaxLength(2000)]
    public string ActionDescription { get; set; } = string.Empty;

    [Required]
    public ActionType Type { get; set; }

    [Required]
    public ActionPriority Priority { get; set; }

    public Guid? ResponsiblePersonId { get; set; }

    // AssignedById is deliberately absent — see UpdateSuccessionActionDto below. Adding an action
    // to a plan IS the act of assigning it, so the assigner comes from the token.

    public DateTime? DueDate { get; set; }

    public Guid? DependsOnActionId { get; set; }
}

public class UpdateSuccessionActionDto : UpdateDtoBase
{
    [Required]
    [MaxLength(2000)]
    public string ActionDescription { get; set; } = string.Empty;

    [Required]
    public ActionType Type { get; set; }

    [Required]
    public ActionPriority Priority { get; set; }

    public Guid? ResponsiblePersonId { get; set; }

    /// <remarks>
    /// <c>AssignedById</c> was here and was copied straight onto the entity's <c>Employee</c> FK,
    /// while the token's id went only to <c>CreatedBy</c> — so any HR user could record a
    /// colleague as the person who assigned an action. The sixth instance of the D-05 shape, and
    /// cleared the same way: the assigner is stamped from the token on create and this route no
    /// longer touches it, so the original assigner survives every later edit. Nothing had ever
    /// sent the field, so no caller broke.
    /// </remarks>
    public DateTime? DueDate { get; set; }
    public DateTime? StartedDate { get; set; }

    [Required]
    public ActionStatus Status { get; set; }

    public DateTime? CompletionDate { get; set; }

    [MaxLength(2000)]
    public string? CompletionNotes { get; set; }

    public Guid? DependsOnActionId { get; set; }

    public bool WasSuccessful { get; set; }

    [MaxLength(2000)]
    public string? OutcomeNotes { get; set; }
}

#endregion

// ============================================================================
// SUCCESSION PLAN HISTORY DTOs
// ============================================================================

#region Succession Plan History

public class SuccessionPlanHistoryDto : BaseDto
{
    public Guid SuccessionPlanId { get; set; }
    public string PlanNumber { get; set; } = string.Empty;
    public int VersionNumber { get; set; }
    public int PlanYear { get; set; }
    public SuccessionPlanStatus StatusAtSnapshot { get; set; }
    public string StatusAtSnapshotName => StatusAtSnapshot.ToString();
    public SuccessionRisk RiskLevelAtSnapshot { get; set; }
    public string RiskLevelAtSnapshotName => RiskLevelAtSnapshot.ToString();
    public bool HasReadyNowSuccessorAtSnapshot { get; set; }
    public int NumberOfSuccessorsAtSnapshot { get; set; }
    public string? NotesAtSnapshot { get; set; }
    public string? PlanSnapshot { get; set; }
    public DateTime SnapshotDate { get; set; }
    public Guid? SnapshotCreatedById { get; set; }
    public string? SnapshotCreatedByName { get; set; }
    public string? ChangeReason { get; set; }
}

public class SuccessionPlanHistorySummaryDto
{
    public Guid Id { get; set; }
    public int VersionNumber { get; set; }
    public int PlanYear { get; set; }
    public SuccessionPlanStatus StatusAtSnapshot { get; set; }
    public string StatusAtSnapshotName => StatusAtSnapshot.ToString();
    public SuccessionRisk RiskLevelAtSnapshot { get; set; }
    public DateTime SnapshotDate { get; set; }
    public string? SnapshotCreatedByName { get; set; }
    public string? ChangeReason { get; set; }
}

#endregion

// ============================================================================
// SUCCESSION DOCUMENT DTOs
// ============================================================================

#region Succession Document

public class SuccessionDocumentDto : BaseDto
{
    public Guid? SuccessionPlanId { get; set; }
    public string? PlanNumber { get; set; }
    public string DocumentName { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string DocumentUrl { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? CandidateId { get; set; }
    public string? CandidateEmployeeName { get; set; }
    public Guid? TalentPoolMemberId { get; set; }
    public string? TalentPoolMemberName { get; set; }
    public DateTime UploadDate { get; set; }
    public Guid UploadedById { get; set; }
    public string UploadedByName { get; set; } = string.Empty;

    /// <summary>
    /// Set on a row that came through the controlled boundary. A client uses this to tell a
    /// downloadable document from a legacy row whose <c>DocumentUrl</c> names a file the server
    /// never received: offer the download only when it is set.
    /// </summary>
    public Guid? FileUploadRecordId { get; set; }

    public Guid? DocumentRecordId { get; set; }
    public Guid? DocumentVersionId { get; set; }

    public long FileSizeBytes { get; set; }
    public string? FileHash { get; set; }
    public bool IsConfidential { get; set; }
    public DateTime? RetentionDate { get; set; }
}

public class CreateSuccessionDocumentDto : CreateDtoBase
{
    public Guid? SuccessionPlanId { get; set; }
    public Guid? CandidateId { get; set; }
    public Guid? TalentPoolMemberId { get; set; }

    [Required]
    [MaxLength(200)]
    public string DocumentName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string DocumentType { get; set; } = string.Empty;

    /// <summary>
    /// Legacy storage location, no longer required and refused when an API caller supplies it —
    /// see the controllers. Files arrive through <c>POST api/succession-documents/upload</c>,
    /// which puts them past the malware scanner into private storage and fills the three ids
    /// below instead.
    /// </summary>
    [MaxLength(1000)]
    public string DocumentUrl { get; set; } = string.Empty;

    /// <summary>Scanned controlled upload backing this document.</summary>
    public Guid? FileUploadRecordId { get; set; }

    /// <summary>Central-DMS record, once registered.</summary>
    public Guid? DocumentRecordId { get; set; }

    /// <summary>Central-DMS version, once registered.</summary>
    public Guid? DocumentVersionId { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }

    // D-15: UploadedById was [Required] here and copied straight onto the entity's Employee FK
    // while the token's id went only to CreatedBy — so the person recorded as having produced a
    // succession document was whoever the client said. It is now stamped by the service from the
    // authenticated employee and has no place in the request body.

    public long FileSizeBytes { get; set; }

    [MaxLength(64)]
    public string? FileHash { get; set; }

    public bool IsConfidential { get; set; }
    public DateTime? RetentionDate { get; set; }
}

#endregion

// ============================================================================
// TALENT POOL DTOs
// ============================================================================

#region Talent Pool

public class TalentPoolDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid PoolTypeId { get; set; }
    public string PoolTypeName { get; set; } = string.Empty;
    public string? PoolTypeColor { get; set; }
    public Guid? TargetPositionId { get; set; }
    public string? TargetPositionTitle { get; set; }
    public int TargetSize { get; set; }
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public bool IsActive { get; set; }
    public Guid OwnerId { get; set; }
    public string OwnerName { get; set; } = string.Empty;
    public int CurrentMemberCount { get; set; }
    public List<TalentPoolMemberSummaryDto> Members { get; set; } = new();
}

public class TalentPoolSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid PoolTypeId { get; set; }
    public string PoolTypeName { get; set; } = string.Empty;
    public string? PoolTypeColor { get; set; }
    public int TargetSize { get; set; }
    public bool IsActive { get; set; }
    public string OwnerName { get; set; } = string.Empty;
    public int CurrentMemberCount { get; set; }
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
}

[CallerSuppliesIdentifiers]
public class CreateTalentPoolDto : CreateDtoBase
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [Required]
    public Guid PoolTypeId { get; set; }

    public Guid? TargetPositionId { get; set; }

    [Required]
    [Range(1, 1000)]
    public int TargetSize { get; set; }

    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public bool IsActive { get; set; } = true;

    [Required]
    public Guid OwnerId { get; set; }
}

public class UpdateTalentPoolDto : UpdateDtoBase
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [Required]
    public Guid PoolTypeId { get; set; }

    public Guid? TargetPositionId { get; set; }

    [Required]
    [Range(1, 1000)]
    public int TargetSize { get; set; }

    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public bool IsActive { get; set; }

    [Required]
    public Guid OwnerId { get; set; }
}

#endregion

// ============================================================================
// TALENT POOL TYPE DEFINITION DTOs (tenant-configurable pool types)
// ============================================================================

#region Talent Pool Type Definition

public class TalentPoolTypeDefinitionDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ColorHex { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public bool IsSystemDefault { get; set; }
    public int PoolCount { get; set; }
}

public class CreateTalentPoolTypeDefinitionDto : CreateDtoBase
{
    [MaxLength(50)]
    public string? Code { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(9)]
    [RegularExpression(Shared.Constants.Colors.HexPattern, ErrorMessage = Shared.Constants.Colors.HexMessage)]
    public string? ColorHex { get; set; }

    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdateTalentPoolTypeDefinitionDto : UpdateDtoBase
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(9)]
    [RegularExpression(Shared.Constants.Colors.HexPattern, ErrorMessage = Shared.Constants.Colors.HexMessage)]
    public string? ColorHex { get; set; }

    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
}

#endregion

// ============================================================================
// TALENT POOL MEMBER DTOs
// ============================================================================

#region Talent Pool Member

public class TalentPoolMemberDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid TalentPoolId { get; set; }
    public string TalentPoolName { get; set; } = string.Empty;
    public string TalentPoolTypeName { get; set; } = string.Empty;

    // Employee
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;
    public string? EmployeePosition { get; set; }
    public string? EmployeeDepartment { get; set; }

    // Membership details
    public int Rank { get; set; }
    public ReadinessLevel Readiness { get; set; }
    public string ReadinessName => Readiness.ToString();
    public DateTime? ReadyByDate { get; set; }
    public string? Justification { get; set; }
    public string? Strengths { get; set; }
    public string? DevelopmentGaps { get; set; }

    // Enrolment
    public DateTime EnrolledDate { get; set; }
    public Guid? NominatedById { get; set; }
    public string? NominatedByName { get; set; }
    public string? NominationNotes { get; set; }

    // Review
    public DateTime? LastReviewDate { get; set; }
    public DateTime? NextReviewDate { get; set; }
    public string? ReviewNotes { get; set; }

    // Cached ratings
    public PerformanceRating? LatestPerformanceRating { get; set; }
    public string? LatestPerformanceRatingName => LatestPerformanceRating?.ToString();
    public PotentialRating? LatestPotentialRating { get; set; }
    public string? LatestPotentialRatingName => LatestPotentialRating?.ToString();
    public DateTime? RatingLastUpdated { get; set; }

    // Removal
    public DateTime? RemovedDate { get; set; }
    public string? RemovalReason { get; set; }
    public bool IsActive { get; set; }

    // Child collections
    public List<TalentReviewRatingSummaryDto> ReviewRatings { get; set; } = new();
    public List<SuccessionDevelopmentActivitySummaryDto> DevelopmentActivities { get; set; } = new();
    public List<SuccessionDocumentDto> Documents { get; set; } = new();
}

public class TalentPoolMemberSummaryDto
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;
    public string? EmployeePosition { get; set; }
    public int Rank { get; set; }
    public ReadinessLevel Readiness { get; set; }
    public string ReadinessName => Readiness.ToString();
    public PerformanceRating? LatestPerformanceRating { get; set; }
    public PotentialRating? LatestPotentialRating { get; set; }
    public bool IsActive { get; set; }
    public DateTime EnrolledDate { get; set; }
}

public class CreateTalentPoolMemberDto : CreateDtoBase
{
    [Required]
    public Guid TalentPoolId { get; set; }

    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    [Range(1, 1000)]
    public int Rank { get; set; }

    [Required]
    public ReadinessLevel Readiness { get; set; }

    public DateTime? ReadyByDate { get; set; }

    [MaxLength(2000)]
    public string? Justification { get; set; }

    [MaxLength(2000)]
    public string? Strengths { get; set; }

    [MaxLength(2000)]
    public string? DevelopmentGaps { get; set; }

    /// <summary>
    /// When the employee joined the pool. Caller-set on purpose — unlike the nominator, this is a
    /// business fact the desk may legitimately be back-recording.
    /// </summary>
    [Required]
    public DateTime EnrolledDate { get; set; }

    // ⚠ NominatedById was removed. It arrived on the body and was honoured, so a desk actor could
    // record a nomination under a colleague's name. The nominator is the signed-in user.

    [MaxLength(2000)]
    public string? NominationNotes { get; set; }
}

public class UpdateTalentPoolMemberDto : UpdateDtoBase
{
    [Required]
    [Range(1, 1000)]
    public int Rank { get; set; }

    [Required]
    public ReadinessLevel Readiness { get; set; }

    public DateTime? ReadyByDate { get; set; }

    [MaxLength(2000)]
    public string? Justification { get; set; }

    [MaxLength(2000)]
    public string? Strengths { get; set; }

    [MaxLength(2000)]
    public string? DevelopmentGaps { get; set; }

    public DateTime? LastReviewDate { get; set; }
    public DateTime? NextReviewDate { get; set; }

    [MaxLength(2000)]
    public string? ReviewNotes { get; set; }

    public bool IsActive { get; set; }
    public DateTime? RemovedDate { get; set; }

    [MaxLength(500)]
    public string? RemovalReason { get; set; }
}

#endregion

// ============================================================================
// TALENT REVIEW SESSION DTOs
// ============================================================================

#region Talent Review Session

public class TalentReviewSessionDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string SessionName { get; set; } = string.Empty;
    public int ReviewYear { get; set; }
    public DateTime SessionDate { get; set; }
    public string? Location { get; set; }

    // Facilitation
    public Guid? FacilitatedById { get; set; }
    public string? FacilitatedByName { get; set; }
    public string? Agenda { get; set; }
    public string? SessionNotes { get; set; }

    // Scope
    public Guid? OrganizationLevelId { get; set; }
    public string? OrganizationLevelName { get; set; }
    public Guid? OrganizationUnitId { get; set; }
    public string? OrganizationUnitName { get; set; }

    // Status
    public bool IsFinalized { get; set; }
    public DateTime? FinalizedDate { get; set; }
    public Guid? FinalizedById { get; set; }
    public string? FinalizedByName { get; set; }

    public List<TalentReviewRatingSummaryDto> Ratings { get; set; } = new();
}

public class TalentReviewSessionSummaryDto
{
    public Guid Id { get; set; }
    public string SessionName { get; set; } = string.Empty;
    public int ReviewYear { get; set; }
    public DateTime SessionDate { get; set; }
    public string? FacilitatedByName { get; set; }
    public string? OrganizationUnitName { get; set; }
    public bool IsFinalized { get; set; }
    public int RatingCount { get; set; }
}

public class CreateTalentReviewSessionDto : CreateDtoBase
{
    [Required]
    [MaxLength(200)]
    public string SessionName { get; set; } = string.Empty;

    [Required]
    [Range(2000, 2100)]
    public int ReviewYear { get; set; }

    [Required]
    public DateTime SessionDate { get; set; }

    [MaxLength(500)]
    public string? Location { get; set; }

    public Guid? FacilitatedById { get; set; }

    [MaxLength(4000)]
    public string? Agenda { get; set; }

    public Guid? OrganizationLevelId { get; set; }
    public Guid? OrganizationUnitId { get; set; }
}

public class UpdateTalentReviewSessionDto : UpdateDtoBase
{
    [Required]
    [MaxLength(200)]
    public string SessionName { get; set; } = string.Empty;

    [Required]
    public DateTime SessionDate { get; set; }

    [MaxLength(500)]
    public string? Location { get; set; }

    public Guid? FacilitatedById { get; set; }

    [MaxLength(4000)]
    public string? Agenda { get; set; }

    [MaxLength(4000)]
    public string? SessionNotes { get; set; }

    public Guid? OrganizationLevelId { get; set; }
    public Guid? OrganizationUnitId { get; set; }
}

/// <remarks>
/// ⚠ <c>FinalizedById</c> and <c>FinalizedDate</c> were removed, and this one was not merely
/// spoofable — it was **the reason the endpoint crashed**. <c>FinalizedById</c> was a *required*
/// Guid the client had no way to know, so a caller that omitted it sent <c>Guid.Empty</c> and the
/// save died on <c>FK_TalentReviewSessions_Employees_FinalizedById</c> with a 500. Measured
/// 2026-08-18.
///
/// The clearest statement of the rule this area keeps rediscovering: a value the client cannot know
/// is a value the client should not be sending. Here the client could not even guess it.
/// </remarks>
public class FinalizeTalentReviewSessionDto
{
    [Required]
    public Guid SessionId { get; set; }

    [MaxLength(4000)]
    public string? SessionNotes { get; set; }
}

#endregion

// ============================================================================
// TALENT REVIEW RATING DTOs
// ============================================================================

#region Talent Review Rating

public class TalentReviewRatingDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid SessionId { get; set; }
    public string SessionName { get; set; } = string.Empty;
    public int ReviewYear { get; set; }

    // Employee
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;
    public string? EmployeePosition { get; set; }

    // Pool link
    public Guid? TalentPoolMemberId { get; set; }
    public string? TalentPoolName { get; set; }

    // 9-Box
    public PerformanceRating Performance { get; set; }
    public string PerformanceName => Performance.ToString();
    public PotentialRating Potential { get; set; }
    public string PotentialName => Potential.ToString();

    // Previous ratings (trend)
    public Guid? PreviousRatingSessionId { get; set; }
    public string? PreviousSessionName { get; set; }
    public PerformanceRating? PreviousPerformance { get; set; }
    public string? PreviousPerformanceName => PreviousPerformance?.ToString();
    public PotentialRating? PreviousPotential { get; set; }
    public string? PreviousPotentialName => PreviousPotential?.ToString();

    // Narrative
    public string? Justification { get; set; }
    public string? KeyStrengths { get; set; }
    public string? DevelopmentPriorities { get; set; }

    // Calibration
    public Guid? RatedById { get; set; }
    public string? RatedByName { get; set; }
    public bool CalibrationConfirmed { get; set; }
    public Guid? CalibrationConfirmedById { get; set; }
    public string? CalibrationConfirmedByName { get; set; }
    public DateTime? CalibrationConfirmedDate { get; set; }
    public string? CalibrationNotes { get; set; }
}

public class TalentReviewRatingSummaryDto
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string? EmployeePosition { get; set; }
    public PerformanceRating Performance { get; set; }
    public string PerformanceName => Performance.ToString();
    public PotentialRating Potential { get; set; }
    public string PotentialName => Potential.ToString();
    public PerformanceRating? PreviousPerformance { get; set; }
    public PotentialRating? PreviousPotential { get; set; }
    public bool CalibrationConfirmed { get; set; }
}

public class CreateTalentReviewRatingDto : CreateDtoBase
{
    [Required]
    public Guid SessionId { get; set; }

    [Required]
    public Guid EmployeeId { get; set; }

    public Guid? TalentPoolMemberId { get; set; }

    [Required]
    public PerformanceRating Performance { get; set; }

    [Required]
    public PotentialRating Potential { get; set; }

    [MaxLength(2000)]
    public string? Justification { get; set; }

    [MaxLength(2000)]
    public string? KeyStrengths { get; set; }

    [MaxLength(2000)]
    public string? DevelopmentPriorities { get; set; }

    public Guid? RatedById { get; set; }
}

public class UpdateTalentReviewRatingDto : UpdateDtoBase
{
    [Required]
    public PerformanceRating Performance { get; set; }

    [Required]
    public PotentialRating Potential { get; set; }

    [MaxLength(2000)]
    public string? Justification { get; set; }

    [MaxLength(2000)]
    public string? KeyStrengths { get; set; }

    [MaxLength(2000)]
    public string? DevelopmentPriorities { get; set; }

    public Guid? RatedById { get; set; }
}

/// <remarks>
/// ⚠ <c>ConfirmedById</c> and <c>ConfirmedDate</c> removed. Measured 2026-08-18: a confirmation
/// naming an unrelated employee and dated <c>2020-01-01</c> was stored exactly as sent. Confirming
/// calibration is what publishes a nine-box placement to the talent pool member, so a forged
/// confirmer is a forged provenance on a rating that then feeds promotion decisions.
///
/// ⚠ Note what is deliberately KEPT caller-set on the neighbouring DTOs: <c>FacilitatedById</c> on a
/// session and <c>RatedById</c> on a rating. Those are business facts being recorded — who chaired
/// the meeting, which manager gave the score — and a desk may legitimately record them on someone
/// else's behalf. The line is not "every Guid ending in Id", it is: **an act performed by the
/// caller at the moment of the call comes from the token; a fact about someone else does not.**
/// </remarks>
/// <summary>
/// What the nine-box grid can suggest for an employee before a rater types anything.
/// </summary>
/// <remarks>
/// <para><b>Performance only, and only ever a suggestion.</b> Decision D-4: the review pre-fills
/// Performance from the employee's latest scored appraisal and lets the rater override it with a
/// justification. A calibration session exists precisely to disagree with what the paperwork says,
/// so locking the axis would defeat it.</para>
///
/// <para>⚠ <b>Potential is absent by necessity, not oversight.</b> <c>PotentialRating</c> exists
/// nowhere in area 5 — appraisals carry an <c>OverallScore</c> and nothing about potential — so one
/// axis of the nine box simply cannot be derived. That fact is what settled D-4: the grid could not
/// have been a projection of appraisal data even if we had wanted it to be.</para>
/// </remarks>
/// <summary>
/// A staff movement raised against a succession plan — the plan's outcome, seen from the plan.
/// </summary>
/// <remarks>
/// A deliberately thin projection of area 8's <c>StaffMovement</c>. Succession does not own these
/// records and must not grow a second copy of them: what a plan needs to show is that a named
/// successor is moving into the post, and enough to link through to the movement itself.
/// </remarks>
public class SuccessionPlanMovementDto
{
    public Guid Id { get; set; }
    public string MovementNumber { get; set; } = string.Empty;
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public StaffMovementType MovementType { get; set; }
    public string MovementTypeName => MovementType.ToString();
    public string? NewPositionTitle { get; set; }
    public DateTime RequestDate { get; set; }
    public DateTime? EffectiveDate { get; set; }

    /// <summary>The movement's own status — succession does not interpret it, only shows it.</summary>
    public string Status { get; set; } = string.Empty;
}

public class TalentRatingSuggestionDto
{
    public Guid EmployeeId { get; set; }

    /// <summary>Null when the employee has no scored appraisal — the grid then starts empty.</summary>
    public PerformanceRating? SuggestedPerformance { get; set; }

    /// <summary>The appraisal the suggestion came from, so the UI can say where it got it.</summary>
    public Guid? SourceAppraisalId { get; set; }
    public string? SourceAppraisalNumber { get; set; }
    public decimal? SourceOverallScore { get; set; }
    public DateTime? SourceAppraisalDate { get; set; }

    /// <summary>The employee's last confirmed nine-box placement, if any, for trend context.</summary>
    public PerformanceRating? PreviousPerformance { get; set; }
    public PotentialRating? PreviousPotential { get; set; }
    public string? PreviousSessionName { get; set; }
}

public class ConfirmCalibrationDto
{
    [Required]
    public Guid RatingId { get; set; }

    [MaxLength(2000)]
    public string? CalibrationNotes { get; set; }
}

#endregion

// ============================================================================
// SUCCESSION DASHBOARD
// ============================================================================

#region Succession Dashboard

/// <summary>
/// Aggregated executive dashboard payload for succession planning health.
/// All metrics are computed server-side and returned in a single request.
/// </summary>
public class SuccessionDashboardDto
{
    // ── Top KPI counters ──────────────────────────────────────────────────────
    public int TotalActivePlans                  { get; set; }
    public int TotalCriticalPositions            { get; set; }
    public int PositionsWithReadyNowSuccessor    { get; set; }
    public int HighRiskPositions                 { get; set; }
    public int PositionsWithoutSuccessors        { get; set; }
    public int PositionsWithoutEmergencyCover    { get; set; }

    // ── Coverage ──────────────────────────────────────────────────────────────
    public double CoveragePercentage             { get; set; }

    // ── Risk heatmap: [risk][criticality] = count ────────────────────────────
    public List<RiskHeatmapCell> RiskHeatmap     { get; set; } = new();

    // ── Coverage lists ────────────────────────────────────────────────────────
    public List<PositionCoverageRow> WithReadyNow    { get; set; } = new();
    public List<PositionCoverageRow> WithoutReadyNow { get; set; } = new();

    // ── Talent pool ───────────────────────────────────────────────────────────
    public int TotalTalentPoolMembers            { get; set; }
    public int TalentReadyNow                    { get; set; }
    public int TalentReady1To2Years              { get; set; }
    public int TalentLongTerm                    { get; set; }

    // ── Action execution ──────────────────────────────────────────────────────
    public int TotalActions                      { get; set; }
    public int CompletedActions                  { get; set; }
    public int InProgressActions                 { get; set; }
    public int OverdueActionsCount               { get; set; }

    // ── Overdue alerts (capped at 20 for dashboard) ───────────────────────────
    public List<OverdueActionAlert> OverdueAlerts { get; set; } = new();

    // ── Bench strength for critical positions ────────────────────────────────
    public List<BenchStrengthRow> BenchStrength  { get; set; } = new();

    // ── Upcoming vacancies & retirements (within configured lead times) ───────
    public List<UpcomingVacancyAlert> UpcomingVacancies { get; set; } = new();

    // ── Emergency-cover gaps (High/Critical positions with no emergency successor) ──
    public List<EmergencyCoverageRow> EmergencyCoverageGaps { get; set; } = new();

    // ── Coverage trend across plan-years (spans all years, ignores the year filter) ──
    public List<CoverageTrendPoint> CoverageTrend { get; set; } = new();

    // ── Plan status distribution ──────────────────────────────────────────────
    public int DraftPlans                        { get; set; }
    public int UnderReviewPlans                  { get; set; }
    public int ApprovedPlans                     { get; set; }

    // ── Meta ──────────────────────────────────────────────────────────────────
    public DateTime ComputedAt                   { get; set; } = DateTime.UtcNow;
    public int? FilterYear                        { get; set; }
}

public class RiskHeatmapCell
{
    public SuccessionRisk     RiskLevel   { get; set; }
    public PositionCriticality Criticality { get; set; }
    public int                 Count       { get; set; }
}

public class PositionCoverageRow
{
    public Guid   PlanId               { get; set; }
    public string PositionTitle        { get; set; } = string.Empty;
    public string? CurrentIncumbentName { get; set; }
    public SuccessionRisk RiskLevel    { get; set; }
    public int    SuccessorCount       { get; set; }
}

public class OverdueActionAlert
{
    public Guid   ActionId              { get; set; }
    public string ActionDescription     { get; set; } = string.Empty;
    public string? ResponsiblePersonName { get; set; }
    public DateTime DueDate             { get; set; }
    public string PositionTitle         { get; set; } = string.Empty;
}

public class BenchStrengthRow
{
    public Guid   PlanId          { get; set; }
    public string PositionTitle   { get; set; } = string.Empty;
    public int    SuccessorCount  { get; set; }
    /// <summary>Strong = ≥3, Moderate = 1–2, Weak = 0</summary>
    public string BenchCategory   => SuccessorCount >= 3 ? "Strong" : SuccessorCount >= 1 ? "Moderate" : "Weak";
}

/// <summary>Coverage / bench snapshot for one plan-year, for the dashboard trend.</summary>
public class CoverageTrendPoint
{
    public int PlanYear         { get; set; }
    public int TotalPlans       { get; set; }
    public int ReadyNowPlans    { get; set; }
    public int StrongBenchPlans { get; set; } // ≥3 identified successors
    public double CoveragePct   => TotalPlans == 0 ? 0 : Math.Round(ReadyNowPlans * 100.0 / TotalPlans, 1);
}

/// <summary>
/// A High/Critical position with no emergency successor — a single point of failure if the
/// incumbent leaves suddenly.
/// </summary>
public class EmergencyCoverageRow
{
    public Guid   PlanId               { get; set; }
    public string PositionTitle        { get; set; } = string.Empty;
    public string? CurrentIncumbentName { get; set; }
    public PositionCriticality Criticality { get; set; }
    public SuccessionRisk RiskLevel    { get; set; }
    public bool   HasReadyNowSuccessor { get; set; }
}

/// <summary>
/// An impending vacancy or retirement flagged for HR because it falls within the configured
/// alert lead time. Kind distinguishes an anticipated vacancy from an incumbent retirement.
/// </summary>
public class UpcomingVacancyAlert
{
    public Guid   PlanId               { get; set; }
    public string PositionTitle        { get; set; } = string.Empty;
    public string? CurrentIncumbentName { get; set; }
    public string Kind                 { get; set; } = string.Empty; // "Retirement" | "Vacancy"
    public DateTime EventDate          { get; set; }
    public int    DaysUntil            { get; set; }
    public string? Reason              { get; set; }
    public bool   HasReadyNowSuccessor { get; set; }
    public SuccessionRisk RiskLevel    { get; set; }
}

#endregion

// ============================================================================
// CANDIDATE SEARCH (criteria-based auto-pooling / succession candidate finder)
// ============================================================================

#region Candidate Search

/// <summary>
/// Criteria for objectively querying current employees as potential pool members or succession
/// candidates. When <see cref="TargetPositionId"/> is set, competency/skill gaps and the
/// competency-fit component of the fit score are computed against that position's requirements.
/// </summary>
public class SuccessionCandidateSearchDto
{
    /// <summary>Target position whose requirements drive gap analysis and competency fit.</summary>
    public Guid? TargetPositionId { get; set; }

    public string? SearchTerm { get; set; }
    public Guid? OrganizationUnitId { get; set; }
    public Guid? DepartmentId { get; set; }

    public int? MinYearsOfService { get; set; }
    public int? MinAge { get; set; }
    public int? MaxAge { get; set; }

    /// <summary>Exclude employees with fewer than this many active service years left (near retirement).</summary>
    public int? MinServiceYearsLeft { get; set; }

    /// <summary>Minimum latest appraisal overall score (raw scale, e.g. 0–5 or 0–100).</summary>
    public decimal? MinPerformanceScore { get; set; }

    public Guid? RequiredCompetencyId { get; set; }
    public int? RequiredCompetencyMinLevel { get; set; }

    /// <summary>Exclude employees who are already active members of this pool.</summary>
    public Guid? ExcludePoolId { get; set; }

    /// <summary>Exclude employees who are already candidates on this succession plan.</summary>
    public Guid? ExcludePlanId { get; set; }

    [Range(1, 500)]
    public int MaxResults { get; set; } = 100;
}

public class SuccessionCandidateSearchResultDto
{
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;
    public string? PositionTitle { get; set; }
    public string? OrganizationUnitName { get; set; }

    public int? Age { get; set; }
    public int? YearsOfService { get; set; }
    public int? ServiceYearsLeft { get; set; }
    public DateTime? RetirementDate { get; set; }

    public decimal? LatestPerformanceScore { get; set; }
    public int? LatestPerformanceYear { get; set; }

    public int FitScore { get; set; }
    public string FitBand { get; set; } = string.Empty;

    /// <summary>Number of target-position competency requirements evaluated.</summary>
    public int RequirementCount { get; set; }

    /// <summary>Number of competency requirements the employee falls short of.</summary>
    public int GapCount { get; set; }

    /// <summary>Combined competency + skill fit as a percentage (null when no target position).</summary>
    public double? CompetencyFitPercent { get; set; }
}

#endregion
