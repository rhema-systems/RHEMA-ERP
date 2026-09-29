using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.HR;

public class AppraisalGradeDefinitionDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string GradeName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    // Optional overall score → rating band (production-readiness Phase C).
    public decimal? OverallMinScore { get; set; }
    public decimal? OverallMaxScore { get; set; }
    public PerformanceRating? MappedRating { get; set; }
}

public class CreateAppraisalGradeDefinitionDto : CreateDtoBase
{
    [Required]
    [MaxLength(50)]
    public string GradeName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    [Range(0, 100)]
    public decimal? OverallMinScore { get; set; }
    [Range(0, 100)]
    public decimal? OverallMaxScore { get; set; }
    public PerformanceRating? MappedRating { get; set; }

    public Guid TenantId { get; set; }
}

public class UpdateAppraisalGradeDefinitionDto : UpdateDtoBase
{
    [Required]
    [MaxLength(50)]
    public string GradeName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    [Range(0, 100)]
    public decimal? OverallMinScore { get; set; }
    [Range(0, 100)]
    public decimal? OverallMaxScore { get; set; }
    public PerformanceRating? MappedRating { get; set; }

    public Guid TenantId { get; set; }
}

// ── Template Item Grade Range DTOs ───────────────────────────────────────────

public class TemplateItemGradeRangeDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid AppraisalTemplateItemId { get; set; }
    public Guid GradeDefinitionId { get; set; }
    public string GradeName { get; set; } = string.Empty;
    public int LowScore { get; set; }
    public int HighScore { get; set; }
}

public class CreateTemplateItemGradeRangeDto : CreateDtoBase
{
    [Required]
    public Guid AppraisalTemplateItemId { get; set; }

    [Required]
    public Guid GradeDefinitionId { get; set; }

    [Required]
    [Range(0, int.MaxValue)]
    public int LowScore { get; set; }

    [Required]
    [Range(0, int.MaxValue)]
    public int HighScore { get; set; }
}

public class UpdateTemplateItemGradeRangeDto : UpdateDtoBase
{
    [Required]
    public Guid AppraisalTemplateItemId { get; set; }

    [Required]
    public Guid GradeDefinitionId { get; set; }

    [Required]
    [Range(0, int.MaxValue)]
    public int LowScore { get; set; }

    [Required]
    [Range(0, int.MaxValue)]
    public int HighScore { get; set; }
}

/// <summary>Represents a single grade-range entry used in the bulk replace operation.</summary>
public class GradeRangeInputDto
{
    [Required]
    public Guid GradeDefinitionId { get; set; }

    [Required]
    [Range(0, 100)]
    public int LowScore { get; set; }

    [Required]
    [Range(0, 100)]
    public int HighScore { get; set; }
}

/// <summary>Replaces all grade ranges for a template item in one call.</summary>
public class UpsertTemplateItemGradeRangesDto
{
    [Required]
    public List<GradeRangeInputDto> Ranges { get; set; } = new();
}

// ── PerformanceAppraisalCriterionConfig DTOs ─────────────────────────────────

public class PerformanceAppraisalCriterionConfigDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid PerformanceAppraisalId { get; set; }
    public Guid TemplateItemId { get; set; }
    public string? TemplateItemName { get; set; }
    public int WeightUsed { get; set; }
    public decimal? KpiTargetValue { get; set; }
    public decimal? KpiMinValue { get; set; }
    public decimal? KpiMaxValue { get; set; }
    public KpiTargetSource? KpiTargetSource { get; set; }
    public List<PerformanceAppraisalCriterionConfigGradeRangeDto> GradeRanges { get; set; } = new();
}

public class PerformanceAppraisalCriterionConfigGradeRangeDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid PerformanceAppraisalCriterionConfigId { get; set; }
    public Guid GradeDefinitionId { get; set; }
    public string GradeName { get; set; } = string.Empty;
    public int LowScore { get; set; }
    public int HighScore { get; set; }
}

// ── EffectiveAppraisalConfigDto — result of scope resolution ─────────────────

/// <summary>
/// The resolved effective configuration for an employee in a given cycle.
/// Produced by IEffectiveAppraisalConfigurationService.
/// </summary>
public class EffectiveAppraisalConfigDto
{
    public Guid EmployeeId { get; set; }
    public Guid AppraisalCycleId { get; set; }
    public Guid ResolvedTemplateId { get; set; }
    public string ResolvedTemplateName { get; set; } = string.Empty;
    public List<EffectiveAppraisalCriterionDto> Criteria { get; set; } = new();
}

public class EffectiveAppraisalCriterionDto
{
    public Guid TemplateItemId { get; set; }
    /// <summary>Populated for competency items; null for KPI items.</summary>
    public Guid? CompetencyId { get; set; }
    /// <summary>Populated for KPI items; null for competency items.</summary>
    public Guid? KpiDefinitionId { get; set; }
    /// <summary>Display name — competency name or KPI name, depending on item type.</summary>
    public string ItemName { get; set; } = string.Empty;
    public int Weight { get; set; }
    /// <summary>The item's section, and that section's weight in the template (0–100).</summary>
    public Guid AppraisalTemplateSectionId { get; set; }
    public int SectionWeight { get; set; }
    public decimal? KpiTargetValue { get; set; }
    public decimal? KpiMinValue { get; set; }
    public decimal? KpiMaxValue { get; set; }
    public KpiTargetSource? KpiTargetSource { get; set; }
    public List<EffectiveGradeRangeDto> GradeRanges { get; set; } = new();
}

public class EffectiveGradeRangeDto
{
    public Guid GradeDefinitionId { get; set; }
    public string GradeName { get; set; } = string.Empty;
    public int LowScore { get; set; }
    public int HighScore { get; set; }
}

public class KpiDefinitionDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string KpiName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public MeasurementType MeasurementType { get; set; }
    public string? Unit { get; set; }
    public decimal? TolerancePercent { get; set; }
    public bool IsActive { get; set; } = true;
}

public class CreateKpiDefinitionDto : CreateDtoBase
{
    [Required]
    [MaxLength(200)]
    public string KpiName { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    public MeasurementType MeasurementType { get; set; } = MeasurementType.NumericAbsolute;

    [MaxLength(50)]
    public string? Unit { get; set; }

    [Range(0, 100)]
    public decimal? TolerancePercent { get; set; }

    public bool IsActive { get; set; } = true;
    
    public Guid TenantId { get; set; }
}

public class UpdateKpiDefinitionDto : UpdateDtoBase
{
    [Required]
    [MaxLength(200)]
    public string KpiName { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    public MeasurementType MeasurementType { get; set; }

    [MaxLength(50)]
    public string? Unit { get; set; }

    [Range(0, 100)]
    public decimal? TolerancePercent { get; set; }

    public bool IsActive { get; set; } = true;
    
    public Guid TenantId { get; internal set; }
}

public class AppraisalCompetencyDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string? Code { get; set; }
    public string CriteriaName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

public class CreateAppraisalCompetencyDto : CreateDtoBase
{
    [MaxLength(50)]
    public string? Code { get; set; }

    [Required]
    [MaxLength(200)]
    public string CriteriaName { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}

public class UpdateAppraisalCompetencyDto : UpdateDtoBase
{
    [MaxLength(50)]
    public string? Code { get; set; }

    [Required]
    [MaxLength(200)]
    public string CriteriaName { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}

public class PerformanceAppraisalDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid AppraisalCycleId { get; set; }
    public string? AppraisalCycleCode { get; set; }
    public string AppraisalNumber { get; set; } = string.Empty;
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? DepartmentName { get; set; }
    public string? PositionTitle { get; set; }
    public int Year { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public AppraisalStatus Status { get; set; }
    public int PeerEvaluatorsCount { get; set; }
    /// <summary>
    /// The outcome is released to the appraisee (performance closure P2): completed, or in
    /// governance with only the acknowledgment outstanding. On the appraisee's own unreleased
    /// appraisal the scores, grade, ranks, recommendations and the manager's narrative are withheld.
    /// </summary>
    public bool OutcomeReleased { get; set; }
    public decimal? OverallScore { get; set; }
    /// <summary>Score after a successful appeal resolution. Null until an appeal is adjudicated with a score change.</summary>
    public decimal? AdjustedScore { get; set; }
    public int? RankInPosition { get; set; }
    public int? RankInUnit { get; set; }
    public string? OverallComments { get; set; }
    public string? StrengthsIdentified { get; set; }
    public string? AreasForImprovement { get; set; }
    public string? TrainingNeeds { get; set; }
    public string? CareerAspirations { get; set; }
    public bool RecommendPromotion { get; set; }
    public bool RecommendIncrement { get; set; }
    public bool RecommendTraining { get; set; }
    public bool RecommendPIP { get; set; }
    public bool RecommendTermination { get; set; }
    public bool RecommendAward { get; set; }
    public string? RecommendationNotes { get; set; }
    public DateOnly? NextAppraisalDate { get; set; }
    public bool IsCalibrated { get; set; }
    public Guid? CalibrationSessionId { get; set; }
    public decimal? PreCalibrationScore { get; set; }
    /// <summary>The overall a committed calibration restated; the settled score reads it ahead of the computed one.</summary>
    public decimal? CalibratedOverallScore { get; set; }
    public Guid? OverallGradeDefinitionId { get; set; }
    public Guid? AppraisalTemplateId { get; set; }
    public Guid? DevelopmentPlanId { get; set; }
    public bool EmployeeAcknowledged { get; set; }
    public DateTime? EmployeeAcknowledgedDate { get; set; }
    public string? EmployeeAcknowledgmentComments { get; set; }
    public bool HasAppeal { get; set; }
    public AppraisalAppealStatus? CurrentAppealStatus { get; set; }
    
    // Appeal Remand Tracking
    public bool IsRemandedAppeal { get; set; }
    public DateTime? AppealRemandedDate { get; set; }
    public DateTime? AppealRemandDeadline { get; set; }
    public bool IsRemandDeadlineExceeded { get; set; }
}

public class CreatePerformanceAppraisalDto : CreateDtoBase
{
    [Required]
    public Guid AppraisalCycleId { get; set; }

    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    [Range(2000, 2100)]
    public int Year { get; set; }
    
    [Required]
    public DateOnly StartDate { get; set; }
    
    [Required]
    public DateOnly EndDate { get; set; }
}

public class UpdatePerformanceAppraisalDto : UpdateDtoBase
{
    [Required]
    public Guid AppraisalCycleId { get; set; }

    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    [Range(2000, 2100)]
    public int Year { get; set; }
    
    [Required]
    public DateOnly StartDate { get; set; }
    
    [Required]
    public DateOnly EndDate { get; set; }

    public AppraisalStatus Status { get; set; }

    public int PeerEvaluatorsCount { get; set; }

    [Range(0, 100)]
    public decimal? OverallScore { get; set; }

    public int? RankInPosition { get; set; }
    
    public int? RankInUnit { get; set; }

    [MaxLength(2000)]
    public string? OverallComments { get; set; }

    [MaxLength(2000)]
    public string? StrengthsIdentified { get; set; }
    
    [MaxLength(2000)]
    public string? AreasForImprovement { get; set; }
    
    [MaxLength(2000)]
    public string? TrainingNeeds { get; set; }
    
    [MaxLength(2000)]
    public string? CareerAspirations { get; set; }
    
    public bool RecommendPromotion { get; set; }
    
    public bool RecommendIncrement { get; set; }
    
    public bool RecommendTraining { get; set; }
    
    public bool RecommendPIP { get; set; }
    
    public bool RecommendTermination { get; set; }
    
    [MaxLength(2000)]
    public string? RecommendationNotes { get; set; }

    public DateOnly? NextAppraisalDate { get; set; }
}

public class UpdateAppraisalStatusDto
{
    [Required]
    public Guid AppraisalId { get; set; }

    [Required]
    public AppraisalStatus Status { get; set; }
}

public class AppraisalAppealDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid PerformanceAppraisalId { get; set; }
    public string AppraisalNumber { get; set; } = string.Empty;
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public DateTime SubmittedDate { get; set; }
    public string AppealReason { get; set; } = string.Empty;
    public AppraisalAppealStatus Status { get; set; }
    public Guid? ReviewedById { get; set; }
    public string? ReviewerName { get; set; }
    public string? ResolutionNotes { get; set; }
    public DateTime? ResolvedDate { get; set; }
    public List<AppraisalAppealItemDto> Items { get; set; } = new();
}

public class CreateAppraisalAppealDto : CreateDtoBase
{
    [Required]
    public Guid PerformanceAppraisalId { get; set; }

    [Required]
    [MaxLength(2000)]
    public string AppealReason { get; set; } = string.Empty;

    public List<CreateAppraisalAppealItemDto> Items { get; set; } = new();
}

public class UpdateAppraisalAppealDto : UpdateDtoBase
{
    [Required]
    public Guid PerformanceAppraisalId { get; set; }

    [Required]
    [MaxLength(2000)]
    public string AppealReason { get; set; } = string.Empty;

    [Required]
    public AppraisalAppealStatus Status { get; set; }

    [MaxLength(2000)]
    public string? ResolutionNotes { get; set; }
}

public class AppraisalAppealItemDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid AppraisalAppealId { get; set; }
    public Guid? TemplateItemId { get; set; }
    public string? TemplateItemName { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? ResolutionNotes { get; set; }
    public bool? ScoreAdjusted { get; set; }
    public decimal? OriginalScore { get; set; }
}

public class CreateAppraisalAppealItemDto : CreateDtoBase
{
    [Required]
    public Guid AppraisalAppealId { get; set; }

    public Guid? TemplateItemId { get; set; }

    [Required]
    [MaxLength(2000)]
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// DTO for viewing appeal status (read-only)
/// </summary>
public class AppealStatusViewDto
{
    public Guid AppealId { get; set; }
    public Guid AppraisalId { get; set; }
    public string AppraisalNumber { get; set; } = string.Empty;
    public string CycleName { get; set; } = string.Empty;
    public AppraisalAppealStatus Status { get; set; }
    public DateTime SubmittedDate { get; set; }
    public string AppealReason { get; set; } = string.Empty;
    
    // Resolution details
    public string? ReviewedByName { get; set; }
    public DateTime? ResolvedDate { get; set; }
    public string? ResolutionNotes { get; set; }
    
    // Scores
    public decimal? OriginalScore { get; set; }
    public decimal? AdjustedScore { get; set; }
    public bool HasScoreAdjustment => OriginalScore.HasValue && AdjustedScore.HasValue && OriginalScore != AdjustedScore;
    
    // Appealed items
    public List<AppealedItemViewDto> AppealedItems { get; set; } = new();
}

/// <summary>
/// Individual appealed item for viewing
/// </summary>
public class AppealedItemViewDto
{
    public Guid ItemId { get; set; }
    public string ItemType { get; set; } = string.Empty; // "KPI" or "Competency"
    public string ItemName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    
    // Original evaluation details
    public decimal? OriginalScore { get; set; }
    public decimal? TargetValue { get; set; }
    public decimal? ActualValue { get; set; }
}

/// <summary>
/// DTO for appeal list item (for HR/Manager to review)
/// </summary>
public class AppealListItemDto
{
    public Guid AppealId { get; set; }
    public Guid AppraisalId { get; set; }
    public string AppraisalNumber { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;
    public string CycleName { get; set; } = string.Empty;
    public Guid CycleId { get; set; }
    public DateTime SubmittedDate { get; set; }
    public int AppealedItemsCount { get; set; }
    public AppraisalAppealStatus Status { get; set; }
}

public class UpdateAppraisalAppealItemDto : UpdateDtoBase
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
}

public class ResolveAppraisalAppealDto
{
    [Required]
    public Guid AppealId { get; set; }

    [Required]
    [MaxLength(2000)]
    public string ResolutionNotes { get; set; } = string.Empty;

    [Required]
    public AppraisalAppealStatus Status { get; set; }

    /// <summary>
    /// Optional override score to apply to the appraisal when the appeal is upheld.
    /// When provided and the appeal status is <see cref="AppraisalAppealStatus.Upheld"/>,
    /// this value is persisted as <see cref="PerformanceAppraisal.AdjustedScore"/>.
    /// Leave null if no score change is warranted.
    /// </summary>
    [Range(0, 100)]
    public decimal? AdjustedScore { get; set; }

    public List<ResolveAppealItemDto> ItemResolutions { get; set; } = new();
}

public class ResolveAppealItemDto
{
    [Required]
    public Guid AppealItemId { get; set; }

    [MaxLength(2000)]
    public string? ResolutionNotes { get; set; }

    public bool? ScoreAdjusted { get; set; }
}

public class EvaluatorEvaluationDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid AppraisalId { get; set; }
    public Guid EvaluatorId { get; set; }
    public string EvaluatorName { get; set; } = string.Empty;
    public EvaluatorRole EvaluatorRole { get; set; }
    public decimal EvaluatorWeight { get; set; }
    public DateTime? StartedDate { get; set; }
    public DateTime? SubmittedDate { get; set; }
    public decimal? TotalScore { get; set; }
    public string? OverallNotes { get; set; }
    public string? Recommendation { get; set; }
}

public class CreateEvaluatorEvaluationDto : CreateDtoBase
{
    [Required]
    public Guid AppraisalId { get; set; }
    
    [Required]
    public Guid EvaluatorId { get; set; }

    [Required]
    public EvaluatorRole EvaluatorRole { get; set; }

    [Required]
    [Range(0, 1)]
    public decimal EvaluatorWeight { get; set; }

    [MaxLength(2000)]
    public string? OverallNotes { get; set; }

    [MaxLength(1000)]
    public string? Recommendation { get; set; }
}

public class UpdateEvaluatorEvaluationDto : UpdateDtoBase
{
    [Required]
    public Guid AppraisalId { get; set; }

    [Required]
    public Guid EvaluatorId { get; set; }

    [Required]
    public EvaluatorRole EvaluatorRole { get; set; }

    [Required]
    [Range(0, 1)]
    public decimal EvaluatorWeight { get; set; }

    [MaxLength(2000)]
    public string? OverallNotes { get; set; }

    [MaxLength(1000)]
    public string? Recommendation { get; set; }
}

public class CriterionScoreDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid EvaluatorEvaluationId { get; set; }
    public Guid? TemplateItemId { get; set; }
    public string TemplateItemName { get; set; } = string.Empty;
    public Guid? GradeDefinitionId { get; set; }
    public string? GradeName { get; set; }
    /// <summary>For competency / custom-question items: 0-100 score.</summary>
    public int? NumericScore { get; set; }
    /// <summary>For KPI-driven items: actual value achieved by the evaluatee.</summary>
    public decimal? ActualValue { get; set; }
    public decimal WeightedScore { get; set; }
    public string? Notes { get; set; }
    public string? EvidenceLinks { get; set; }
}

public class CreateCriterionScoreDto : CreateDtoBase
{
    [Required]
    public Guid EvaluatorEvaluationId { get; set; }
    
    [Required]
    public Guid TemplateItemId { get; set; }

    /// <summary>For competency / custom-question items: 0-100 score.</summary>
    [Range(0, int.MaxValue)]
    public int? NumericScore { get; set; }

    /// <summary>For KPI-driven items: actual value achieved.</summary>
    [Range(0, double.MaxValue, ErrorMessage = "Actual value must be non-negative")]
    public decimal? ActualValue { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

public class UpdateCriterionScoreDto : UpdateDtoBase
{
    [Required]
    public Guid EvaluatorEvaluationId { get; set; }

    [Required]
    public Guid TemplateItemId { get; set; }

    /// <summary>For competency / custom-question items: 0-100 score.</summary>
    [Range(0, int.MaxValue)]
    public int? NumericScore { get; set; }

    /// <summary>For KPI-driven items: actual value achieved.</summary>
    [Range(0, double.MaxValue, ErrorMessage = "Actual value must be non-negative")]
    public decimal? ActualValue { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

public class AppraisalEmployeeResponseDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid AppraisalId { get; set; }
    public Guid? TemplateItemId { get; set; }
    public string? TemplateItemName { get; set; }
    public string? ResponseText { get; set; }
    public DateTime ResponseDate { get; set; }
    public AppraisalResponseStatus ResponseStatus { get; set; }
    public DateTime? SubmittedDate { get; set; }
}

public class CreateAppraisalEmployeeResponseDto : CreateDtoBase
{
    [Required]
    public Guid AppraisalId { get; set; }

    public Guid? TemplateItemId { get; set; }

    [Required]
    [MaxLength(2000)]
    public string ResponseText { get; set; } = string.Empty;
}

public class UpdateAppraisalEmployeeResponseDto : UpdateDtoBase
{
    [Required]
    public Guid AppraisalId { get; set; }

    public Guid? TemplateItemId { get; set; }

    [Required]
    [MaxLength(2000)]
    public string ResponseText { get; set; } = string.Empty;
}

public class PerformanceImprovementPlanDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string PipNumber { get; set; } = string.Empty;
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public Guid? AppraisalId { get; set; }
    public string? AppraisalNumber { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public PipStatus Status { get; set; }
    public string PerformanceIssues { get; set; } = string.Empty;
    public string ExpectedStandards { get; set; } = string.Empty;
    public string ImprovementActions { get; set; } = string.Empty;
    public string SupportProvided { get; set; } = string.Empty;
    public string MeasurementCriteria { get; set; } = string.Empty;
    public Guid SupervisorId { get; set; }
    public string SupervisorName { get; set; } = string.Empty;
    public Guid? HROwnerId { get; set; }
    public string? HROwnerName { get; set; }
    public string? ReviewSchedule { get; set; }
    public DateTime? CompletionDate { get; set; }
    public PipOutcome? Outcome { get; set; }
    public string? OutcomeNotes { get; set; }
}

public class CreatePerformanceImprovementPlanDto : CreateDtoBase
{
    [Required]
    public Guid EmployeeId { get; set; }
    
    public Guid? AppraisalId { get; set; }
    
    [Required]
    public DateTime StartDate { get; set; }
    
    [Required]
    public DateTime EndDate { get; set; }

    [Required]
    [MaxLength(2000)]
    public string PerformanceIssues { get; set; } = string.Empty;

    [Required]
    [MaxLength(2000)]
    public string ExpectedStandards { get; set; } = string.Empty;

    [Required]
    [MaxLength(2000)]
    public string ImprovementActions { get; set; } = string.Empty;

    [Required]
    [MaxLength(2000)]
    public string SupportProvided { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(2000)]
    public string MeasurementCriteria { get; set; } = string.Empty;

    [Required]
    public Guid SupervisorId { get; set; }

    public Guid? HROwnerId { get; set; }

    [MaxLength(2000)]
    public string? ReviewSchedule { get; set; }
}

public class UpdatePerformanceImprovementPlanDto : UpdateDtoBase
{
    [Required]
    public Guid EmployeeId { get; set; }
    
    public Guid? AppraisalId { get; set; }
    
    [Required]
    public DateTime StartDate { get; set; }
    
    [Required]
    public DateTime EndDate { get; set; }
    
    [Required]
    public PipStatus Status { get; set; }

    [Required]
    [MaxLength(2000)]
    public string PerformanceIssues { get; set; } = string.Empty;

    [Required]
    [MaxLength(2000)]
    public string ExpectedStandards { get; set; } = string.Empty;

    [Required]
    [MaxLength(2000)]
    public string ImprovementActions { get; set; } = string.Empty;

    [Required]
    [MaxLength(2000)]
    public string SupportProvided { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(2000)]
    public string MeasurementCriteria { get; set; } = string.Empty;

    [Required]
    public Guid SupervisorId { get; set; }

    public Guid? HROwnerId { get; set; }

    [MaxLength(2000)]
    public string? ReviewSchedule { get; set; }

    public DateTime? CompletionDate { get; set; }
    
    public PipOutcome? Outcome { get; set; }
    
    [MaxLength(2000)]
    public string? OutcomeNotes { get; set; }
}

public class UpdatePipStatusDto
{
    [Required]
    public Guid PipId { get; set; }

    [Required]
    public PipStatus Status { get; set; }
}

public class CompletePipDto
{
    [Required]
    public Guid PipId { get; set; }

    [Required]
    public PipOutcome Outcome { get; set; }

    [Required]
    [MaxLength(2000)]
    public string OutcomeNotes { get; set; } = string.Empty;

    /// <summary>
    /// Required when the outcome is <see cref="PipOutcome.Extended"/>, ignored otherwise.
    /// Extending pushes the end date out and leaves the plan running rather than closing it.
    /// </summary>
    public DateTime? NewEndDate { get; set; }
}

public class PipReviewMeetingDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid PipId { get; set; }
    public DateTime MeetingDate { get; set; }
    public bool EmployeeAttended { get; set; }
    public string ProgressNotes { get; set; } = string.Empty;
    public string? IssuesDiscussed { get; set; }
    public string? ActionsAgreed { get; set; }
    public string? EmployeeComments { get; set; }
    public Guid ConductedById { get; set; }
    public string ConductedByName { get; set; } = string.Empty;
}

public class CreatePipReviewMeetingDto : CreateDtoBase
{
    [Required]
    public Guid PipId { get; set; }

    [Required]
    public DateTime MeetingDate { get; set; }

    public bool EmployeeAttended { get; set; } = true;

    [Required]
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
}

public class UpdatePipReviewMeetingDto : UpdateDtoBase
{
    [Required]
    public Guid PipId { get; set; }

    [Required]
    public DateTime MeetingDate { get; set; }

    public bool EmployeeAttended { get; set; }

    [Required]
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
}

public class AppraisalAttachmentDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid AppraisalId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime UploadDate { get; set; }
    public long? FileSizeBytes { get; set; }
    public Guid UploadedById { get; set; }
    public string UploadedByName { get; set; } = string.Empty;
    /// <summary>Set when the attachment hangs off an interim review event rather than the appraisal.</summary>
    public Guid? ReviewEventId { get; set; }
}

public class CreateAppraisalAttachmentDto : CreateDtoBase
{
    [Required]
    public Guid AppraisalId { get; set; }

    [Required]
    [MaxLength(255)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;
    
    [MaxLength(1000)]
    public string? Description { get; set; }
}

public class UpdateAppraisalAttachmentDto : UpdateDtoBase
{
    [Required]
    public Guid AppraisalId { get; set; }

    [Required]
    [MaxLength(255)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }
}

public class PipAttachmentDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid PipId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string? PublicUrl { get; set; }
    public long? FileSizeBytes { get; set; }
    public string? Description { get; set; }
    public DateTime UploadDate { get; set; }
    public Guid UploadedById { get; set; }
    public string UploadedByName { get; set; } = string.Empty;
}

public class AppraisalSettingsDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string SettingsName { get; set; } = string.Empty;

    // Self-evaluation
    public bool RequireSelfEvaluation { get; set; }
    public bool AllowSelfSoftSkillRating { get; set; }
    public decimal SelfEvaluationWeight { get; set; }

    // Peer evaluation
    public bool RequirePeerReviews { get; set; }
    public PeerNominationMode PeerNominationMode { get; set; }
    public int MinPeerEvaluators { get; set; }
    public int MaxPeerEvaluators { get; set; }
    public bool PeerReviewsAnonymous { get; set; }
    public bool AllowPeerKpiEvaluation { get; set; }
    public decimal PeerEvaluationWeight { get; set; }
    public PeerEvaluationOpenMode PeerEvaluationOpenMode { get; set; }

    // Manager evaluation
    public bool RequireManagerEvaluation { get; set; }
    public decimal ManagerEvaluationWeight { get; set; }

    // Scoring visibility
    public bool ShowSelfScoreToManager { get; set; }
    public bool ShowPeerScoresToManager { get; set; }
    public bool ShowScoreBreakdownToEmployee { get; set; }

    // Calibration
    public bool RequireCalibration { get; set; }

    // HR review
    public bool RequireHRReview { get; set; }
    public bool HRCanModifyScores { get; set; }
    public HRReviewTiming HRReviewTiming { get; set; }

    // Employee acknowledgment
    public bool RequireEmployeeAcknowledgment { get; set; }
    public bool AllowEmployeeResponse { get; set; }
    public bool AllowAcknowledgmentWithoutConversation { get; set; }

    // Appeals
    public bool EnableAppeals { get; set; }
    public int AppealWindowDays { get; set; }
    public int AppealReevaluationWindowDays { get; set; }

    // Goal setting
    public bool RequireGoalSetting { get; set; }
    public bool RequireManagerGoalApproval { get; set; }
    public int? MaxGoalsPerEmployee { get; set; }
    public int? MinGoalsPerEmployee { get; set; }

    // Check-ins / coaching
    public bool EnableCheckIns { get; set; }
    public bool EnablePrivateJournal { get; set; }

    // Conversations
    public bool RequireKickOffConversation { get; set; }
    public bool RequireMidYearConversation { get; set; }
    public bool RequireFinalConversation { get; set; }

    // Review event settings
    public ReviewFrequency ReviewFrequency { get; set; }
    public InterimReviewDepth InterimReviewDepth { get; set; }
    public bool RequireMidYearSelfAssessment { get; set; }
    public bool RequireGoalProgressUpdateAtReview { get; set; }

    // Deadline enforcement
    public bool AutoLockOnDeadline { get; set; }

    // Operational policy defaults (production-readiness Phase D)
    public Guid? DefaultHRReviewerId { get; set; }
    public int ProbationExtensionMonths { get; set; } = 3;
    public int ManagerWorkloadThreshold { get; set; } = 10;
    public int DeadlineRiskHighDays { get; set; } = 2;
    public int DeadlineRiskMediumDays { get; set; } = 5;
    public int DeadlineRiskLowDays { get; set; } = 7;
    public string SuccessionPoolName { get; set; } = "Appraisal Nominations";
    public ReadinessLevel SuccessionDefaultReadiness { get; set; } = ReadinessLevel.ReadyIn12Months;
}

public class CreateAppraisalSettingsDto : CreateDtoBase
{
    [Required]
    [MaxLength(100)]
    public string SettingsName { get; set; } = string.Empty;
    
    public bool RequireSelfEvaluation { get; set; } = true;
    
    public bool AllowSelfSoftSkillRating { get; set; } = false;
    
    [Range(0, 1)]
    public decimal SelfEvaluationWeight { get; set; } = 0.1m;
    
    public bool RequirePeerReviews { get; set; } = false;
    
    public PeerNominationMode PeerNominationMode { get; set; } = PeerNominationMode.Employee;
    
    [Range(0, 10)]
    public int MinPeerEvaluators { get; set; } = 0;
    
    [Range(0, 10)]
    public int MaxPeerEvaluators { get; set; } = 5;
    
    public bool PeerReviewsAnonymous { get; set; } = true;
    
    public bool AllowPeerKpiEvaluation { get; set; } = false;
    
    [Range(0, 1)]
    public decimal PeerEvaluationWeight { get; set; } = 0.2m;
    
    public bool RequireManagerEvaluation { get; set; } = true;
    
    [Range(0, 1)]
    public decimal ManagerEvaluationWeight { get; set; } = 0.7m;
    
    public bool RequireHRReview { get; set; } = true;
    
    public bool HRCanModifyScores { get; set; } = false;
    
    public bool RequireEmployeeAcknowledgment { get; set; } = true;
    
    public bool AllowEmployeeResponse { get; set; } = true;
    
    public bool EnableAppeals { get; set; } = true;
    
    [Range(1, 30)]
    public int AppealWindowDays { get; set; } = 7;
    
    [Range(1, 30)]
    public int AppealReevaluationWindowDays { get; set; } = 5;

    public bool RequireCalibration { get; set; } = true;

    public HRReviewTiming HRReviewTiming { get; set; } = HRReviewTiming.AfterCalibration;

    public bool AllowAcknowledgmentWithoutConversation { get; set; } = false;

    public bool RequireGoalSetting { get; set; } = true;
    public bool RequireManagerGoalApproval { get; set; } = true;
    public int? MaxGoalsPerEmployee { get; set; }
    public int? MinGoalsPerEmployee { get; set; }
    public bool EnableCheckIns { get; set; } = true;
    public bool EnablePrivateJournal { get; set; } = true;
    public bool RequireKickOffConversation { get; set; } = false;
    public bool RequireMidYearConversation { get; set; } = false;
    public bool RequireFinalConversation { get; set; } = true;

    public PeerEvaluationOpenMode PeerEvaluationOpenMode { get; set; } = PeerEvaluationOpenMode.WithSelfEval;
    public bool ShowSelfScoreToManager { get; set; } = true;
    public bool ShowPeerScoresToManager { get; set; } = true;
    public bool ShowScoreBreakdownToEmployee { get; set; } = true;
    public ReviewFrequency ReviewFrequency { get; set; } = ReviewFrequency.MidYearOnly;
    public InterimReviewDepth InterimReviewDepth { get; set; } = InterimReviewDepth.LightTouch;
    public bool RequireMidYearSelfAssessment { get; set; } = false;
    public bool RequireGoalProgressUpdateAtReview { get; set; } = true;
    public bool AutoLockOnDeadline { get; set; } = false;

    // Operational policy defaults (production-readiness Phase D)
    public Guid? DefaultHRReviewerId { get; set; }
    public int ProbationExtensionMonths { get; set; } = 3;
    public int ManagerWorkloadThreshold { get; set; } = 10;
    public int DeadlineRiskHighDays { get; set; } = 2;
    public int DeadlineRiskMediumDays { get; set; } = 5;
    public int DeadlineRiskLowDays { get; set; } = 7;
    public string SuccessionPoolName { get; set; } = "Appraisal Nominations";
    public ReadinessLevel SuccessionDefaultReadiness { get; set; } = ReadinessLevel.ReadyIn12Months;
}

public class UpdateAppraisalSettingsDto : UpdateDtoBase
{
    [Required]
    [MaxLength(100)]
    public string SettingsName { get; set; } = string.Empty;
    
    public bool RequireSelfEvaluation { get; set; }
    
    public bool AllowSelfSoftSkillRating { get; set; }
    
    [Range(0, 1)]
    public decimal SelfEvaluationWeight { get; set; }
    
    public bool RequirePeerReviews { get; set; }
    
    public PeerNominationMode PeerNominationMode { get; set; }
    
    [Range(0, 10)]
    public int MinPeerEvaluators { get; set; }
    
    [Range(0, 10)]
    public int MaxPeerEvaluators { get; set; }
    
    public bool PeerReviewsAnonymous { get; set; }
    
    public bool AllowPeerKpiEvaluation { get; set; }
    
    [Range(0, 1)]
    public decimal PeerEvaluationWeight { get; set; }
    
    public bool RequireManagerEvaluation { get; set; }
    
    [Range(0, 1)]
    public decimal ManagerEvaluationWeight { get; set; }
    
    public bool RequireHRReview { get; set; }
    
    public bool HRCanModifyScores { get; set; }
    
    public bool RequireEmployeeAcknowledgment { get; set; }
    
    public bool AllowEmployeeResponse { get; set; }
    
    public bool EnableAppeals { get; set; }
    
    [Range(1, 30)]
    public int AppealWindowDays { get; set; }
    
    [Range(1, 30)]
    public int AppealReevaluationWindowDays { get; set; }

    public bool RequireCalibration { get; set; }

    public HRReviewTiming HRReviewTiming { get; set; }

    public bool AllowAcknowledgmentWithoutConversation { get; set; }

    public bool RequireGoalSetting { get; set; }
    public bool RequireManagerGoalApproval { get; set; }
    public int? MaxGoalsPerEmployee { get; set; }
    public int? MinGoalsPerEmployee { get; set; }
    public bool EnableCheckIns { get; set; }
    public bool EnablePrivateJournal { get; set; }
    public bool RequireKickOffConversation { get; set; }
    public bool RequireMidYearConversation { get; set; }
    public bool RequireFinalConversation { get; set; }

    public PeerEvaluationOpenMode PeerEvaluationOpenMode { get; set; }
    public bool ShowSelfScoreToManager { get; set; }
    public bool ShowPeerScoresToManager { get; set; }
    public bool ShowScoreBreakdownToEmployee { get; set; }
    public ReviewFrequency ReviewFrequency { get; set; }
    public InterimReviewDepth InterimReviewDepth { get; set; }
    public bool RequireMidYearSelfAssessment { get; set; }
    public bool RequireGoalProgressUpdateAtReview { get; set; }
    public bool AutoLockOnDeadline { get; set; }

    // Operational policy defaults (production-readiness Phase D)
    public Guid? DefaultHRReviewerId { get; set; }
    public int ProbationExtensionMonths { get; set; } = 3;
    public int ManagerWorkloadThreshold { get; set; } = 10;
    public int DeadlineRiskHighDays { get; set; } = 2;
    public int DeadlineRiskMediumDays { get; set; } = 5;
    public int DeadlineRiskLowDays { get; set; } = 7;
    public string SuccessionPoolName { get; set; } = "Appraisal Nominations";
    public ReadinessLevel SuccessionDefaultReadiness { get; set; } = ReadinessLevel.ReadyIn12Months;
}

public class AppraisalCycleDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string CycleCode { get; set; } = string.Empty;
    public string CycleName { get; set; } = string.Empty;
    public int Year { get; set; }
    public AppraisalType AppraisalType { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public Guid AppraisalSettingsId { get; set; }
    public string? AppraisalSettingsName { get; set; }
    public AppraisalCycleStatus Status { get; set; }

    // Phase 1: Goal setting
    public DateOnly? GoalSettingOpenDate { get; set; }
    public DateOnly? GoalSettingDeadline { get; set; }

    // Phase 2: Interim reviews
    public DateOnly? Q1ReviewOpenDate { get; set; }
    public DateOnly? Q1ReviewDeadline { get; set; }
    public DateOnly? MidYearOpenDate { get; set; }
    public DateOnly? MidYearDeadline { get; set; }
    public DateOnly? Q3ReviewOpenDate { get; set; }
    public DateOnly? Q3ReviewDeadline { get; set; }

    // Phase 3: Year-end pipeline
    public DateOnly? PeerNominationDeadline { get; set; }
    public DateOnly? SelfEvaluationOpenDate { get; set; }
    public DateOnly? SelfEvaluationDeadline { get; set; }
    public DateOnly? PeerEvaluationOpenDate { get; set; }
    public DateOnly? PeerEvaluationDeadline { get; set; }
    public DateOnly? ManagerEvaluationOpenDate { get; set; }
    public DateOnly? ManagerEvaluationDeadline { get; set; }
    public DateOnly? CalibrationOpenDate { get; set; }
    public DateOnly? CalibrationDeadline { get; set; }
    public DateOnly? HRReviewOpenDate { get; set; }
    public DateOnly? HRReviewDeadline { get; set; }
    public DateOnly? EmployeeAcknowledgeDeadline { get; set; }
    public DateOnly? FinalConversationDeadline { get; set; }

    // Cycle management
    public Guid? OpenedById { get; set; }
    public string? OpenedByName { get; set; }
    public DateTime? OpenedDate { get; set; }
    public Guid? ClosedById { get; set; }
    public string? ClosedByName { get; set; }
    public DateTime? ClosedDate { get; set; }
}

public class CreateAppraisalCycleDto : CreateDtoBase
{
    public string CycleCode { get; set; } = string.Empty;
    
    public string CycleName { get; set; } = string.Empty;
    
    [Required]
    [Range(2000, 2100)]
    public int Year { get; set; }
    
    [Required]
    public AppraisalType AppraisalType { get; set; }
    
    [Required]
    public DateOnly StartDate { get; set; }
    
    [Required]
    public DateOnly EndDate { get; set; }
    
    [Required]
    public Guid AppraisalSettingsId { get; set; }

    public AppraisalCycleStatus Status { get; set; } = AppraisalCycleStatus.Draft;

    public DateOnly? GoalSettingOpenDate { get; set; }
    public DateOnly? GoalSettingDeadline { get; set; }
    public DateOnly? Q1ReviewOpenDate { get; set; }
    public DateOnly? Q1ReviewDeadline { get; set; }
    public DateOnly? MidYearOpenDate { get; set; }
    public DateOnly? MidYearDeadline { get; set; }
    public DateOnly? Q3ReviewOpenDate { get; set; }
    public DateOnly? Q3ReviewDeadline { get; set; }
    public DateOnly? PeerNominationDeadline { get; set; }
    public DateOnly? SelfEvaluationOpenDate { get; set; }
    public DateOnly? SelfEvaluationDeadline { get; set; }
    public DateOnly? PeerEvaluationOpenDate { get; set; }
    public DateOnly? PeerEvaluationDeadline { get; set; }
    public DateOnly? ManagerEvaluationOpenDate { get; set; }
    public DateOnly? ManagerEvaluationDeadline { get; set; }
    public DateOnly? CalibrationOpenDate { get; set; }
    public DateOnly? CalibrationDeadline { get; set; }
    public DateOnly? HRReviewOpenDate { get; set; }
    public DateOnly? HRReviewDeadline { get; set; }
    public DateOnly? EmployeeAcknowledgeDeadline { get; set; }
    public DateOnly? FinalConversationDeadline { get; set; }
}

public class UpdateAppraisalCycleDto : UpdateDtoBase
{
    public string CycleCode { get; set; } = string.Empty;
    
    public string CycleName { get; set; } = string.Empty;
    
    [Required]
    [Range(2000, 2100)]
    public int Year { get; set; }
    
    [Required]
    public AppraisalType AppraisalType { get; set; }
    
    [Required]
    public DateOnly StartDate { get; set; }
    
    [Required]
    public DateOnly EndDate { get; set; }
    
    [Required]
    public Guid AppraisalSettingsId { get; set; }

    // Status is deliberately absent. It belongs to the open / close / reopen endpoints, which run
    // the scope-overlap checks and stamp who acted; a plain edit carrying it could move a cycle
    // between states with none of that. The Next.js form sent a hardcoded "Draft" on every save,
    // so editing an Open cycle's phase dates silently reverted it to Draft.

    public DateOnly? GoalSettingOpenDate { get; set; }
    public DateOnly? GoalSettingDeadline { get; set; }
    public DateOnly? Q1ReviewOpenDate { get; set; }
    public DateOnly? Q1ReviewDeadline { get; set; }
    public DateOnly? MidYearOpenDate { get; set; }
    public DateOnly? MidYearDeadline { get; set; }
    public DateOnly? Q3ReviewOpenDate { get; set; }
    public DateOnly? Q3ReviewDeadline { get; set; }
    public DateOnly? PeerNominationDeadline { get; set; }
    public DateOnly? SelfEvaluationOpenDate { get; set; }
    public DateOnly? SelfEvaluationDeadline { get; set; }
    public DateOnly? PeerEvaluationOpenDate { get; set; }
    public DateOnly? PeerEvaluationDeadline { get; set; }
    public DateOnly? ManagerEvaluationOpenDate { get; set; }
    public DateOnly? ManagerEvaluationDeadline { get; set; }
    public DateOnly? CalibrationOpenDate { get; set; }
    public DateOnly? CalibrationDeadline { get; set; }
    public DateOnly? HRReviewOpenDate { get; set; }
    public DateOnly? HRReviewDeadline { get; set; }
    public DateOnly? EmployeeAcknowledgeDeadline { get; set; }
    public DateOnly? FinalConversationDeadline { get; set; }
}

public class OpenAppraisalCycleDto
{
    [Required]
    public Guid CycleId { get; set; }
}

public class CloseAppraisalCycleDto
{
    [Required]
    public Guid CycleId { get; set; }
}

public class AppraisalCycleTargetDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid AppraisalCycleId { get; set; }
    public string? AppraisalCycleCode { get; set; }
    public AppraisalTargetType TargetType { get; set; }
    public Guid? OrganizationLevelId { get; set; }
    public string? OrganizationLevelName { get; set; }
    public Guid? OrganizationUnitId { get; set; }
    public string? OrganizationUnitName { get; set; }
    public Guid? PositionId { get; set; }
    public string? PositionTitle { get; set; }
    public int EstimatedEmployeeCount { get; set; }
    public int ActiveEmployeeCount { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
}

public class CreateAppraisalCycleTargetDto : CreateDtoBase
{
    [Required]
    public Guid AppraisalCycleId { get; set; }

    [Required]
    public AppraisalTargetType TargetType { get; set; }

    public Guid? OrganizationLevelId { get; set; }

    public Guid? OrganizationUnitId { get; set; }

    public Guid? PositionId { get; set; }

    [Range(0, int.MaxValue)]
    public int EstimatedEmployeeCount { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;
}

public class UpdateAppraisalCycleTargetDto : UpdateDtoBase
{
    [Required]
    public Guid AppraisalCycleId { get; set; }

    [Required]
    public AppraisalTargetType TargetType { get; set; }

    public Guid? OrganizationLevelId { get; set; }

    public Guid? OrganizationUnitId { get; set; }

    public Guid? PositionId { get; set; }

    [Range(0, int.MaxValue)]
    public int EstimatedEmployeeCount { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public bool IsActive { get; set; }
}

public class PeerNominationDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid AppraisalId { get; set; }
    public string? AppraisalNumber { get; set; }
    public Guid PeerEmployeeId { get; set; }
    public string PeerEmployeeName { get; set; } = string.Empty;
    public string? PeerEmployeeNumber { get; set; }
    public Guid NominatedById { get; set; }
    public string NominatedByName { get; set; } = string.Empty;
    public DateTime NominationDate { get; set; }
    public DateTime? InvitationSentDate { get; set; }
    public DateTime? DueDate { get; set; }
    public string? InstructionsToPeer { get; set; }
    public PeerNominationStatus NominationStatus { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public string? RejectionReason { get; set; }
}

public class CreatePeerNominationDto : CreateDtoBase
{
    [Required]
    public Guid AppraisalId { get; set; }
    
    [Required]
    public Guid PeerEmployeeId { get; set; }
    
    [Required]
    public Guid NominatedById { get; set; }
    
    public DateTime? DueDate { get; set; }
    
    [MaxLength(500)]
    public string? InstructionsToPeer { get; set; }
    
    public PeerNominationStatus NominationStatus { get; set; } = PeerNominationStatus.Pending;
}

public class UpdatePeerNominationDto : UpdateDtoBase
{
    [Required]
    public Guid AppraisalId { get; set; }
    
    [Required]
    public Guid PeerEmployeeId { get; set; }
    
    [Required]
    public Guid NominatedById { get; set; }
    
    public DateTime? InvitationSentDate { get; set; }
    
    public DateTime? DueDate { get; set; }
    
    [MaxLength(500)]
    public string? InstructionsToPeer { get; set; }
    
    public PeerNominationStatus NominationStatus { get; set; }
}

public class SendPeerEvaluationInvitationDto
{
    [Required]
    public Guid PeerNominationId { get; set; }
}

public class BatchCreatePeerNominationsDto
{
    [Required]
    public Guid AppraisalId { get; set; }
    
    [Required]
    [MinLength(1)]
    public List<Guid> PeerEmployeeIds { get; set; } = new();
    
    public DateTime? DueDate { get; set; }
    
    [MaxLength(500)]
    public string? InstructionsToPeer { get; set; }
}

public class ApprovePeerNominationsDto
{
    [Required]
    public Guid AppraisalId { get; set; }
    
    [Required]
    [MinLength(1)]
    public List<Guid> NominationIds { get; set; } = new();
    
    public DateTime? DueDate { get; set; }
}

public class RejectPeerNominationsDto
{
    [Required]
    public Guid AppraisalId { get; set; }
    
    [Required]
    [MinLength(1)]
    public List<Guid> NominationIds { get; set; } = new();
    
    [Required]
    [MaxLength(500)]
    public string RejectionReason { get; set; } = string.Empty;
}

public class PeerNominationSummaryDto
{
    public Guid AppraisalId { get; set; }
    public int TotalNominations { get; set; }
    public int PendingCount { get; set; }
    public int ApprovedCount { get; set; }
    public int RejectedCount { get; set; }
    public int MinRequired { get; set; }
    public int MaxAllowed { get; set; }
    public bool CanSubmit { get; set; }
    public bool CanEdit { get; set; }
    public PeerNominationMode NominationMode { get; set; }
    public List<PeerNominationDto> Nominations { get; set; } = new();
}

// ===== Appraisal Cycle Progress Dashboard DTOs =====

public class AppraisalCycleProgressDto
{
    // Cycle Snapshot
    public Guid CycleId { get; set; }
    public string CycleName { get; set; } = string.Empty;
    public string CycleCode { get; set; } = string.Empty;
    public int Year { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public AppraisalCycleStatus Status { get; set; }
    public string CurrentPhase { get; set; } = string.Empty;
    public string? AppraisalSettingsName { get; set; }
    public bool RequirePeerReviews { get; set; }
    public bool RequireHRReview { get; set; }
    
    // Deadlines
    public DateOnly? SelfEvaluationDeadline { get; set; }
    public DateOnly? PeerEvaluationDeadline { get; set; }
    public DateOnly? ManagerEvaluationDeadline { get; set; }
    public DateOnly? HRReviewDeadline { get; set; }
    public DateOnly? EmployeeAcknowledgeDeadline { get; set; }
    
    // Overall Progress
    public ProgressMetricDto SelfEvaluationProgress { get; set; } = new();
    public ProgressMetricDto PeerEvaluationProgress { get; set; } = new();
    public ProgressMetricDto ManagerEvaluationProgress { get; set; } = new();
    public ProgressMetricDto HRReviewProgress { get; set; } = new();
    
    // Participation Coverage
    public int TotalEmployeesTargeted { get; set; }
    public int TotalEmployeesExcluded { get; set; }
    public TargetBreakdownDto TargetBreakdown { get; set; } = new();
    
    // Bottlenecks & Risks
    public int EmployeesNotStartedSelfEvaluation { get; set; }
    public int PeerReviewsPendingPastMidpoint { get; set; }
    public int ManagersWithHighWorkload { get; set; }
    public List<DeadlineRiskDto> DeadlineRisks { get; set; } = new();
    public List<BottleneckDto> TopBottlenecks { get; set; } = new();
}

public class ProgressMetricDto
{
    public int Completed { get; set; }
    public int Total { get; set; }
    public decimal PercentageCompleted { get; set; }
    public bool IsRequired { get; set; }
}

public class TargetBreakdownDto
{
    public int OrganizationLevelTargets { get; set; }
    public int OrganizationUnitTargets { get; set; }
    public int PositionTargets { get; set; }
    public int IndividualEmployeeTargets { get; set; }
}

public class DeadlineRiskDto
{
    public string Phase { get; set; } = string.Empty;
    public DateOnly Deadline { get; set; }
    public int DaysUntilDeadline { get; set; }
    public string RelativeTime { get; set; } = string.Empty;
    public bool IsOverdue { get; set; }
    public RiskLevel RiskLevel { get; set; }
}

public class BottleneckDto
{
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Count { get; set; }
    public string Icon { get; set; } = string.Empty;
    public RiskLevel Severity { get; set; }
}

public enum RiskLevel
{
    None = 0,
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

/// <summary>
/// DTO for employee's "My Appraisals" view - shows all appraisals involving this employee
/// </summary>
public class MyAppraisalDto
{
    public Guid AppraisalId { get; set; }
    public string AppraisalNumber { get; set; } = string.Empty;
    public Guid AppraisalCycleId { get; set; }
    public string AppraisalCycleName { get; set; } = string.Empty;
    public int Year { get; set; }
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public AppraisalStatus Status { get; set; }
    
    /// <summary>
    /// The employee's role(s) in this appraisal: Self, Peer, or Both
    /// </summary>
    public string MyRole { get; set; } = string.Empty;
    
    /// <summary>
    /// Whether the employee has any pending action required
    /// </summary>
    public bool ActionRequired { get; set; }
    
    /// <summary>
    /// Contextual action text (e.g., "Complete self-evaluation", "Provide peer feedback")
    /// </summary>
    public string? ActionText { get; set; }
    
    /// <summary>
    /// Due date for the current action (if any)
    /// </summary>
    public DateOnly? DueDate { get; set; }
    
    /// <summary>
    /// Overall score if available — and only once it is released to the employee (performance
    /// closure P2: completed, or in governance with only the acknowledgment outstanding).
    /// </summary>
    public decimal? OverallScore { get; set; }

    /// <summary>The outcome is released to the employee; until it is, <see cref="OverallScore"/> is withheld from them.</summary>
    public bool OutcomeReleased { get; set; }

    /// <summary>
    /// Has the employee acknowledged the final appraisal?
    /// </summary>
    public bool IsAcknowledged { get; set; }
    
    /// <summary>
    /// Has an appeal been filed?
    /// </summary>
    public bool AppealFiled { get; set; }
    
    /// <summary>
    /// Current appeal status if an appeal exists
    /// </summary>
    public AppraisalAppealStatus? AppealStatus { get; set; }
    
    /// <summary>
    /// Can the employee file an appeal?
    /// </summary>
    public bool CanFileAppeal { get; set; }
    
    /// <summary>
    /// Self-evaluation completion status
    /// </summary>
    public bool SelfEvaluationComplete { get; set; }
    
    /// <summary>
    /// If employee is a peer evaluator, their peer evaluation completion status
    /// </summary>
    public bool PeerEvaluationComplete { get; set; }
    
    /// <summary>
    /// Is this appraisal overdue for action?
    /// </summary>
    public bool IsOverdue { get; set; }
}

// ========== SHARED EVALUATION DTOs (Template-based) ==========

/// <summary>
/// Unified grade range DTO used across all evaluation contexts.
/// Sourced from PerformanceAppraisalCriterionConfigGradeRange (appraisal-time snapshot).
/// </summary>
public class EvaluationGradeRangeDto
{
    public Guid GradeDefinitionId { get; set; }
    public string GradeName { get; set; } = string.Empty;
    public string? GradeDescription { get; set; }
    public int LowScore { get; set; }
    public int HighScore { get; set; }
}

/// <summary>
/// Core definition of a single scoreable item within an evaluation section.
/// Carries the appraisal-time snapshot configuration common to all evaluator roles.
/// Role-specific score fields are added in derived role-specific DTOs.
/// </summary>
public class EvaluationItemDto
{
    /// <summary>The AppraisalTemplateItem.Id — the scored line item.</summary>
    public Guid TemplateItemId { get; set; }

    /// <summary>The PerformanceAppraisalCriterionConfig.Id for this appraisal (the frozen snapshot row).</summary>
    public Guid CriterionConfigId { get; set; }

    /// <summary>Display name — competency name or KPI name depending on item type.</summary>
    public string ItemName { get; set; } = string.Empty;
    public string? ItemDescription { get; set; }

    /// <summary>Populated for CustomQuestion items.</summary>
    public string? CustomQuestion { get; set; }

    /// <summary>Whether the evaluator must supply an evidence link for this item.</summary>
    public bool RequireEvidence { get; set; }

    /// <summary>Item weight within its section (snapshotted value, 0-100).</summary>
    public int ItemWeight { get; set; }

    /// <summary>Display position within the section.</summary>
    public int DisplayOrder { get; set; }

    // ── KPI-specific (populated when CriteriaType == KpiBased) ────────────────
    public Guid? KpiDefinitionId { get; set; }
    public string? KpiUnit { get; set; }
    public MeasurementType? MeasurementType { get; set; }
    public decimal? KpiTargetValue { get; set; }
    public decimal? KpiMinValue { get; set; }
    public decimal? KpiMaxValue { get; set; }
    public KpiTargetSource? KpiTargetSource { get; set; }

    /// <summary>Grade bands from the appraisal criterion snapshot (PerformanceAppraisalCriterionConfigGradeRange).</summary>
    public List<EvaluationGradeRangeDto> GradeRanges { get; set; } = new();
}

/// <summary>
/// A single scoreable item in a self-evaluation section.
/// Extends EvaluationItemDto with the employee's existing (draft) score data.
/// </summary>
public class SelfEvaluationItemDto : EvaluationItemDto
{
    /// <summary>CriterionScore.Id for the employee's self-evaluation record (null if not yet scored).</summary>
    public Guid? ExistingCriterionScoreId { get; set; }

    /// <summary>Previously saved numeric score (0-100) for competency / custom-question items.</summary>
    public int? ExistingNumericScore { get; set; }

    /// <summary>Previously saved actual value for KPI-driven items.</summary>
    public decimal? ExistingActualValue { get; set; }

    public string? ExistingNotes { get; set; }
    public string? ExistingEvidenceLinks { get; set; }

    /// <summary>Computed KPI achievement percentage (read-only, populated from existing score).</summary>
    public decimal? AchievementPercent { get; set; }

    /// <summary>Resolved grade name from grade bands (read-only).</summary>
    public string? AchievedGrade { get; set; }
}

/// <summary>
/// A single scoreable item in a manager evaluation section.
/// Carries both the employee's self-score (read-only reference) and the manager's current score.
/// </summary>
public class ManagerEvaluationItemDto : EvaluationItemDto
{
    // ── Employee self-score (read-only reference) ─────────────────────────────
    public Guid? EmployeeSelfCriterionScoreId { get; set; }
    public int? EmployeeSelfNumericScore { get; set; }
    public decimal? EmployeeSelfActualValue { get; set; }
    public string? EmployeeSelfNotes { get; set; }
    public string? EmployeeSelfEvidenceLinks { get; set; }
    public decimal? EmployeeSelfAchievementPercent { get; set; }
    public string? EmployeeSelfAchievedGrade { get; set; }

    // ── Manager score (existing or new) ───────────────────────────────────────
    public Guid? ManagerCriterionScoreId { get; set; }
    public int? ManagerNumericScore { get; set; }
    public decimal? ManagerActualValue { get; set; }
    public string? ManagerNotes { get; set; }
    public string? ManagerEvidenceLinks { get; set; }
    public decimal? ManagerAchievementPercent { get; set; }
    /// <summary>
    /// A KPI whose achievement calibration or an appeal restated (D-22): the percentage above is
    /// the restatement, not the actual against the target (A14).
    /// </summary>
    public bool ManagerAchievementOverridden { get; set; }
    public string? ManagerAchievedGrade { get; set; }

    /// <summary>True when this item is flagged in an active appeal remand (highlights the row).</summary>
    public bool IsAppealed { get; set; }
}

/// <summary>
/// A single scoreable item in a peer evaluation section.
/// IsScoreable is false for KPI items when AllowPeerKpiEvaluation is disabled.
/// </summary>
public class PeerEvaluationItemDto : EvaluationItemDto
{
    public Guid? ExistingCriterionScoreId { get; set; }
    public int? ExistingNumericScore { get; set; }
    public decimal? ExistingActualValue { get; set; }
    public string? ExistingNotes { get; set; }
    public string? ExistingEvidenceLinks { get; set; }
    public decimal? WeightedScore { get; set; }

    /// <summary>
    /// False for KPI-type items when AllowPeerKpiEvaluation is disabled.
    /// The item is shown read-only in that case.
    /// </summary>
    public bool IsScoreable { get; set; } = true;
}

/// <summary>
/// A single item in a read-only submitted evaluation view.
/// </summary>
public class SubmittedEvaluationItemDto
{
    public Guid TemplateItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string? ItemDescription { get; set; }
    public Guid? KpiDefinitionId { get; set; }
    public int ItemWeight { get; set; }
    public int? NumericScore { get; set; }
    public decimal? ActualValue { get; set; }
    public string? AchievedGrade { get; set; }
    public string? Notes { get; set; }
    public string? EvidenceLinks { get; set; }
    // KPI-specific display fields
    public string? KpiUnit { get; set; }
    public decimal? KpiTargetValue { get; set; }
    public decimal? AchievementPercent { get; set; }
}

/// <summary>
/// Section wrapper for a read-only submitted evaluation view.
/// </summary>
public class SubmittedEvaluationSectionDto
{
    public string SectionName { get; set; } = string.Empty;
    public int SectionWeight { get; set; }
    public int DisplayOrder { get; set; }
    public List<SubmittedEvaluationItemDto> Items { get; set; } = new();
}

/// <summary>
/// Unified input DTO for saving a single scored item, covering both KPI and competency types.
/// Keyed by TemplateItemId (matches PerformanceAppraisalCriterionConfig.TemplateItemId).
/// </summary>
public class EvaluationItemInputDto
{
    [Required]
    public Guid TemplateItemId { get; set; }

    /// <summary>
    /// For competency / custom-question items: direct 0-100 numeric score.
    /// </summary>
    [Range(0, 100, ErrorMessage = "Score must be between 0 and 100")]
    public int? NumericScore { get; set; }

    /// <summary>
    /// For KPI-driven items: the actual value achieved.
    /// </summary>
    [Range(0, double.MaxValue, ErrorMessage = "Actual value must be non-negative")]
    public decimal? ActualValue { get; set; }

    [MaxLength(2000, ErrorMessage = "Notes cannot exceed 2000 characters")]
    public string? Notes { get; set; }

    [MaxLength(4000, ErrorMessage = "Evidence links cannot exceed 4000 characters")]
    public string? EvidenceLinks { get; set; }
}

// ── Section wrapper DTOs ──────────────────────────────────────────────────────

/// <summary>Section containing self-evaluation items, in DisplayOrder.</summary>
public class SelfEvaluationSectionDto
{
    public Guid SectionId { get; set; }
    public string SectionName { get; set; } = string.Empty;
    public string? SectionDescription { get; set; }
    public int DisplayOrder { get; set; }
    /// <summary>Section weight (0-100). Should sum to 100 across all sections for the appraisal.</summary>
    public int SectionWeight { get; set; }
    public List<SelfEvaluationItemDto> Items { get; set; } = new();
    /// <summary>Free-text custom questions in this section (items with no CriteriaId).</summary>
    public List<SelfEvaluationCustomQuestionDto> CustomQuestions { get; set; } = new();
}

/// <summary>Section containing manager evaluation items, in DisplayOrder.</summary>
public class ManagerEvaluationSectionDto
{
    public Guid SectionId { get; set; }
    public string SectionName { get; set; } = string.Empty;
    public string? SectionDescription { get; set; }
    public int DisplayOrder { get; set; }
    /// <summary>Section weight (0-100). Should sum to 100 across all sections for the appraisal.</summary>
    public int SectionWeight { get; set; }
    public List<ManagerEvaluationItemDto> Items { get; set; } = new();
}

/// <summary>Section containing peer evaluation items, in DisplayOrder.</summary>
public class PeerEvaluationSectionDto
{
    public Guid SectionId { get; set; }
    public string SectionName { get; set; } = string.Empty;
    public string? SectionDescription { get; set; }
    public int DisplayOrder { get; set; }
    /// <summary>Section weight (0-100). Should sum to 100 across all sections for the appraisal.</summary>
    public int SectionWeight { get; set; }
    public List<PeerEvaluationItemDto> Items { get; set; } = new();
}

// ========== SELF-EVALUATION DTOs ==========

/// <summary>
/// Complete context for an employee's self-evaluation page
/// </summary>
public class SelfEvaluationContextDto
{
    /// <summary>
    /// Appraisal basic information
    /// </summary>
    public Guid AppraisalId { get; set; }
    public string AppraisalNumber { get; set; } = string.Empty;
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;
    
    /// <summary>
    /// Cycle information
    /// </summary>
    public string AppraisalCycleName { get; set; } = string.Empty;
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public AppraisalStatus Status { get; set; }
    
    /// <summary>
    /// Self-evaluation specific state
    /// </summary>
    public bool IsSelfEvaluationSubmitted { get; set; }
    public DateTime? SelfEvaluationSubmittedDate { get; set; }
    public bool IsEditable { get; set; }
    public DateOnly? SelfEvaluationDeadline { get; set; }
    
    /// <summary>
    /// Settings that control self-evaluation behavior
    /// </summary>
    public bool AllowSelfSoftSkillRating { get; set; }
    
    /// <summary>
    /// Full appraisal settings (includes peer review configuration)
    /// </summary>
    public AppraisalSettingsDto? Settings { get; set; }
    
    /// <summary>
    /// Template sections in display order. Each section carries its weight
    /// and contains the items the employee must score.
    /// </summary>
    public List<SelfEvaluationSectionDto> Sections { get; set; } = new();
}

// SelfEvaluationKpiItemDto, KpiGradeRangeDto, SelfEvaluationCompetencyItemDto, CompetencyGradeRangeDto
// removed — superseded by SelfEvaluationItemDto / EvaluationGradeRangeDto (section-based model).

/// <summary>
/// DTO for saving/updating self-evaluation
/// </summary>
public class SaveSelfEvaluationDto
{
    [Required]
    public Guid AppraisalId { get; set; }
    
    [Required]
    public Guid EmployeeId { get; set; }
    
    /// <summary>
    /// All item scores, covering both KPI and competency items, keyed by CriteriaId.
    /// </summary>
    [Required]
    public List<EvaluationItemInputDto> ItemScores { get; set; } = new();

    /// <summary>
    /// Whether this is a draft save or final submission
    /// </summary>
    public bool IsDraft { get; set; }

    /// <summary>Responses to free-text custom question items.</summary>
    public List<CustomQuestionResponseInputDto> CustomQuestionResponses { get; set; } = new();

    /// <summary>Year-end self-assessments for each goal in Section B.</summary>
    public List<GoalAssessmentInputDto> GoalAssessments { get; set; } = new();
}

/// <summary>
/// Represents a single custom (free-text) question item in a self-evaluation section.
/// </summary>
public class SelfEvaluationCustomQuestionDto
{
    public Guid TemplateItemId { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    /// <summary>Null when the employee has not yet answered.</summary>
    public string? ExistingResponse { get; set; }
    public bool IsSubmitted { get; set; }
}

/// <summary>Input model for saving a single custom question response.</summary>
public class CustomQuestionResponseInputDto
{
    public Guid TemplateItemId { get; set; }
    public string? ResponseText { get; set; }
}

// SelfEvaluationKpiInputDto + SelfEvaluationCompetencyInputDto removed — replaced by EvaluationItemInputDto.

/// <summary>
/// Result DTO after saving self-evaluation
/// </summary>
public class SelfEvaluationResultDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public Guid? EvaluatorEvaluationId { get; set; }
    public DateTime? SubmittedDate { get; set; }
}

// ========== MANAGER APPRAISAL DTOs ==========

/// <summary>
/// Summary of an appraisal cycle for team appraisals view
/// </summary>
public class TeamAppraisalCycleSummaryDto
{
    public Guid CycleId { get; set; }
    public string CycleName { get; set; } = string.Empty;
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public AppraisalCycleStatus Status { get; set; }
    
    /// <summary>
    /// Progress statistics for this manager
    /// </summary>
    public int TotalEmployees { get; set; }
    public int EvaluatedCount { get; set; }
    public int PendingCount { get; set; }
    public int InProgressCount { get; set; }
}

/// <summary>
/// Individual team member appraisal status
/// </summary>
public class TeamMemberAppraisalDto
{
    public Guid AppraisalId { get; set; }
    public string CycleName { get; set; } = string.Empty;
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;
    public string? Position { get; set; }
    public string? OrganizationUnit { get; set; }
    
    /// <summary>
    /// Self-evaluation status
    /// </summary>
    public bool SelfEvaluationSubmitted { get; set; }
    public DateTime? SelfEvaluationSubmittedDate { get; set; }
    
    /// <summary>
    /// Manager evaluation status
    /// </summary>
    public bool ManagerEvaluationStarted { get; set; }
    public bool ManagerEvaluationSubmitted { get; set; }
    public DateTime? ManagerEvaluationSubmittedDate { get; set; }
    
    public AppraisalStatus AppraisalStatus { get; set; }
    
    /// <summary>
    /// Appraisal settings and results
    /// </summary>
    public bool RequireSelfEvaluation { get; set; }
    public decimal? OverallScore { get; set; }
    
    /// <summary>
    /// Appeal remand tracking
    /// </summary>
    public bool IsRemandedAppeal { get; set; }
    public DateTime? AppealRemandDeadline { get; set; }
}

/// <summary>
/// Complete context for manager evaluation page
/// </summary>
public class ManagerEvaluationContextDto
{
    /// <summary>
    /// Appraisal basic information
    /// </summary>
    public Guid AppraisalId { get; set; }
    public string AppraisalNumber { get; set; } = string.Empty;
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;
    public string? Position { get; set; }
    public string? Department { get; set; }
    
    /// <summary>
    /// Cycle information
    /// </summary>
    public Guid CycleId { get; set; }
    public string AppraisalCycleName { get; set; } = string.Empty;
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public DateOnly? ManagerEvaluationDeadline { get; set; }
    public AppraisalStatus Status { get; set; }
    
    /// <summary>
    /// Appeal remand information (if applicable)
    /// </summary>
    public bool IsRemandedAppeal { get; set; }
    public DateTime? AppealRemandedDate { get; set; }
    public DateTime? AppealRemandDeadline { get; set; }
    public bool IsRemandDeadlineExceeded { get; set; }
    public List<Guid> AppealedKpiIds { get; set; } = new();
    public List<Guid> AppealedTemplateItemIds { get; set; } = new();
    
    /// <summary>
    /// Manager evaluation state
    /// </summary>
    public Guid? ManagerEvaluatorEvaluationId { get; set; }
    public bool IsManagerEvaluationSubmitted { get; set; }
    public DateTime? ManagerEvaluationSubmittedDate { get; set; }
    public bool IsEditable { get; set; }
    
    /// <summary>
    /// Weight breakdown
    /// </summary>
    public decimal SelfEvaluationWeight { get; set; }
    public decimal ManagerEvaluationWeight { get; set; }
    public decimal PeerEvaluationWeight { get; set; }
    
    /// <summary>
    /// Appraisal settings for peer nomination configuration
    /// </summary>
    public AppraisalSettingsDto? Settings { get; set; }
    
    /// <summary>
    /// Manager Final Assessment & Recommendations (from PerformanceAppraisal entity)
    /// </summary>
    public string? OverallComments { get; set; }
    public string? StrengthsIdentified { get; set; }
    public string? AreasForImprovement { get; set; }
    public string? TrainingNeeds { get; set; }
    public string? CareerAspirations { get; set; }
    public bool RecommendPromotion { get; set; }
    public bool RecommendIncrement { get; set; }
    public bool RecommendTraining { get; set; }
    public bool RecommendPIP { get; set; }
    public bool RecommendTermination { get; set; }
    public string? RecommendationNotes { get; set; }
    
    /// <summary>
    /// Template sections in display order. Each section carries its weight
    /// and contains the items the manager must score (including employee self-scores as reference).
    /// </summary>
    public List<ManagerEvaluationSectionDto> Sections { get; set; } = new();
}

// ManagerEvaluationKpiItemDto + ManagerEvaluationCompetencyItemDto removed — superseded by ManagerEvaluationItemDto / section-based model.

/// <summary>
/// DTO for saving/updating manager evaluation
/// </summary>
public class SaveManagerEvaluationDto
{
    [Required]
    public Guid AppraisalId { get; set; }
    
    [Required]
    public Guid ManagerId { get; set; }
    
    /// <summary>
    /// All item scores, covering both KPI and competency items, keyed by CriteriaId.
    /// </summary>
    [Required]
    public List<EvaluationItemInputDto> ItemScores { get; set; } = new();

    /// <summary>
    /// Overall notes from manager
    /// </summary>
    [MaxLength(2000)]
    public string? OverallNotes { get; set; }
    
    /// <summary>
    /// Manager's recommendation
    /// </summary>
    [MaxLength(500)]
    public string? Recommendation { get; set; }
    
    // Manager Final Assessment & Recommendations (from PerformanceAppraisal entity)
    
    [MaxLength(2000)]
    public string? OverallComments { get; set; }
    
    [MaxLength(2000)]
    public string? StrengthsIdentified { get; set; }
    
    [MaxLength(2000)]
    public string? AreasForImprovement { get; set; }
    
    [MaxLength(2000)]
    public string? TrainingNeeds { get; set; }
    
    [MaxLength(2000)]
    public string? CareerAspirations { get; set; }
    
    public bool RecommendPromotion { get; set; }
    
    public bool RecommendIncrement { get; set; }
    
    public bool RecommendTraining { get; set; }
    
    public bool RecommendPIP { get; set; }
    
    public bool RecommendTermination { get; set; }
    
    [MaxLength(2000)]
    public string? RecommendationNotes { get; set; }
    
    /// <summary>
    /// Whether this is a draft save or final submission
    /// </summary>
    public bool IsDraft { get; set; }

    /// <summary>Year-end manager assessments for each goal in Section B.</summary>
    public List<GoalAssessmentInputDto> GoalAssessments { get; set; } = new();
}

// ManagerEvaluationKpiInputDto + ManagerEvaluationCompetencyInputDto removed — replaced by EvaluationItemInputDto.

/// <summary>
/// Result DTO after saving manager evaluation
/// </summary>
public class ManagerEvaluationResultDto
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public Guid? EvaluatorEvaluationId { get; set; }
    public DateTime? SubmittedDate { get; set; }
}

/// <summary>
/// DTO for viewing a submitted self-evaluation (read-only)
/// </summary>
public class ViewSubmittedEvaluationDto
{
    // Appraisal Context
    public Guid AppraisalId { get; set; }
    public string AppraisalCycleName { get; set; } = string.Empty;
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public int Year { get; set; }
    
    // Employee Information
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string? EmployeePosition { get; set; }
    public string? Department { get; set; }
    
    // Submission Information
    public DateTime SubmittedDate { get; set; }
    public AppraisalStatus CurrentStatus { get; set; }
    
    // Workflow Context
    public bool RequirePeerReviews { get; set; }
    public bool PeerReviewsInProgress { get; set; }
    public bool ManagerReviewComplete { get; set; }
    public bool RequireHRReview { get; set; }
    public bool HRReviewComplete { get; set; }
    
    // Settings
    public bool AllowSelfSoftSkillRating { get; set; }

    // Self-Evaluation Data — sections in DisplayOrder (replaces flat SoftSkillScores + KpiEvaluations)
    public List<SubmittedEvaluationSectionDto> Sections { get; set; } = new();
    public List<SubmittedAttachmentDto> Attachments { get; set; } = new();
}

// SubmittedCriterionScoreDto + SubmittedKpiEvaluationDto removed — superseded by SubmittedEvaluationItemDto / SubmittedEvaluationSectionDto.

/// <summary>
/// DTO for a submitted attachment
/// </summary>
public class SubmittedAttachmentDto
{
    public Guid AttachmentId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime UploadedDate { get; set; }
}
#region Peer Evaluation DTOs

/// <summary>
/// Simple grade DTO for peer evaluation
/// </summary>
public class AppraisalGradeDto
{
    public Guid Id { get; set; }
    public string GradeName { get; set; } = string.Empty;
    public string? Description { get; set; }
}

/// <summary>
/// Peer evaluation assignment list item
/// </summary>
public class PeerEvaluationAssignmentDto
{
    public Guid EvaluationId { get; set; }
    public Guid AppraisalId { get; set; }
    public string AppraisalCycleName { get; set; } = string.Empty;
    public Guid AppraiseeId { get; set; }
    public string AppraiseeName { get; set; } = string.Empty;
    public string AppraiseePosition { get; set; } = string.Empty;
    public string AppraiseeOrganizationUnit { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // Not Started, In Progress, Submitted
    public DateTime? StartedDate { get; set; }
    public DateTime? SubmittedDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public decimal EvaluatorWeight { get; set; }
}

/// <summary>
/// Complete peer evaluation detail with criteria and KPIs.
/// Peer evaluators always see the actual appraisee name.
/// IsAnonymous indicates whether the appraisee will see peer evaluation details.
/// </summary>
public class PeerEvaluationDetailDto
{
    public Guid EvaluationId { get; set; }
    public Guid AppraisalId { get; set; }
    public string AppraisalCycleName { get; set; } = string.Empty;
    public string AppraiseeName { get; set; } = string.Empty; // Always shows actual name to peer evaluators
    public string AppraiseePosition { get; set; } = string.Empty;
    public string AppraiseeOrganizationUnit { get; set; } = string.Empty;
    public decimal PeerEvaluationWeight { get; set; }
    public bool AllowPeerKpiEvaluation { get; set; }
    public bool IsAnonymous { get; set; }
    public bool IsSubmitted { get; set; }
    public DateOnly? DueDate { get; set; }
    
    /// <summary>Template sections in display order, containing scoreable items.</summary>
    public List<PeerEvaluationSectionDto> Sections { get; set; } = new();
}

// CriterionEvaluationDto + KpiPeerEvaluationDto removed — superseded by PeerEvaluationItemDto / section-based model.

/// <summary>
/// DTO for saving peer evaluation draft or submission.
/// ItemScores is keyed by CriteriaId, covering both competency and KPI items.
/// </summary>
public class SavePeerEvaluationDto
{
    public Guid EvaluationId { get; set; }
    public List<EvaluationItemInputDto> ItemScores { get; set; } = new();
}

// CompetencyScoreInputDto removed — replaced by EvaluationItemInputDto.

#endregion Peer Evaluation DTOs
#region HR Review DTOs

/// <summary>
/// Complete HR review context
/// </summary>
public class HRReviewDto
{
    // Appraisal Basic Info
    public Guid AppraisalId { get; set; }
    public string AppraisalNumber { get; set; } = string.Empty;
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public string OrganizationUnit { get; set; } = string.Empty;
    public string AppraisalCycleName { get; set; } = string.Empty;
    public AppraisalStatus Status { get; set; }

    /// <summary>
    /// The outcome is released to the appraisee (performance closure P2). While false, the
    /// appraisee's own copy carries no manager evaluation, peer summary, manager/peer/final
    /// score, grade or HR remarks; HR and the manager see them throughout.
    /// </summary>
    public bool OutcomeReleased { get; set; }

    // Precondition Flags
    public bool IsSelfEvaluationComplete { get; set; }
    public bool IsManagerEvaluationComplete { get; set; }
    public bool RequiresPeerReviews { get; set; }
    public bool ArePeerReviewsComplete { get; set; }
    public int RequiredPeerReviews { get; set; }
    public int CompletedPeerReviews { get; set; }
    public bool CanProceedToHRReview { get; set; }
    
    // Compliance Checks
    public bool WeightTotalValid { get; set; }
    public decimal TotalWeight { get; set; }
    public bool IsCycleActive { get; set; }
    
    // Evaluation Summaries
    public EvaluationSummaryDto? SelfEvaluation { get; set; }
    public EvaluationSummaryDto? ManagerEvaluation { get; set; }
    public PeerEvaluationSummaryDto? PeerEvaluationSummary { get; set; }
    
    // Score Breakdown
    public decimal SelfWeight { get; set; }
    public decimal ManagerWeight { get; set; }
    public decimal PeerWeight { get; set; }
    public decimal? SelfScore { get; set; }
    public decimal? ManagerScore { get; set; }
    public decimal? PeerScore { get; set; }
    public decimal? FinalScore { get; set; }
    public string? FinalGrade { get; set; }
    
    // HR Review State
    public bool IsFinalized { get; set; }
    public string? HRRemarks { get; set; }
    public DateTime? FinalizedDate { get; set; }
    public string? FinalizedByName { get; set; }
    
    // Employee Acknowledgment & Appeal
    public DateTime? EmployeeAcknowledgedDate { get; set; }
    public bool HasAppeal { get; set; }
    public AppraisalAppealStatus? CurrentAppealStatus { get; set; }
    
    // Appeal Remand Tracking
    public bool IsRemandedAppeal { get; set; }
    public DateTime? AppealRemandedDate { get; set; }
    public DateTime? AppealRemandDeadline { get; set; }
    public bool IsRemandDeadlineExceeded { get; set; }
}

/// <summary>
/// Evaluation summary for self or manager evaluation
/// </summary>
public class EvaluationSummaryDto
{
    public List<CompetencyScoreSummaryDto> CompetencyScores { get; set; } = new();
    public List<KpiScoreSummaryDto> KpiScores { get; set; } = new();
    public string? GeneralComments { get; set; }
    public decimal? TotalScore { get; set; }
}

/// <summary>
/// Competency score summary for HR review
/// </summary>
public class CompetencyScoreSummaryDto
{
    public string CriteriaName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? NumericScore { get; set; }
    public int Weight { get; set; }
    public decimal? WeightedScore { get; set; }
    public string? Comments { get; set; }
}

/// <summary>
/// KPI score summary for HR review
/// </summary>
public class KpiScoreSummaryDto
{
    public string KpiName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal? TargetValue { get; set; }
    public decimal? ActualValue { get; set; }
    public decimal? AchievementPercentage { get; set; }
    /// <summary>
    /// The achievement was restated by calibration or an appeal (D-22): it is not the actual
    /// against the target, and the screen says so.
    /// </summary>
    public bool AchievementOverridden { get; set; }
    public string? Unit { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Peer evaluation summary for HR review
/// </summary>
public class PeerEvaluationSummaryDto
{
    public bool IsAnonymous { get; set; }
    public bool AllowKpiEvaluation { get; set; }
    public List<PeerCompetencyScoreSummaryDto> CompetencyScores { get; set; } = new();
    public List<PeerKpiScoreSummaryDto> KpiScores { get; set; } = new();
}

/// <summary>
/// Aggregated peer competency scores
/// </summary>
public class PeerCompetencyScoreSummaryDto
{
    public string CriteriaName { get; set; } = string.Empty;
    public decimal? AverageScore { get; set; }
    public int ResponseCount { get; set; }
}

/// <summary>
/// Aggregated peer KPI evaluations
/// </summary>
public class PeerKpiScoreSummaryDto
{
    public string KpiName { get; set; } = string.Empty;
    public decimal? AverageTarget { get; set; }
    public decimal? AverageActual { get; set; }
    public int ResponseCount { get; set; }
}

/// <summary>
/// DTO for approving and finalizing an appraisal
/// </summary>
public class ApproveAppraisalDto
{
    public string? HRRemarks { get; set; }
}

/// <summary>
/// DTO for returning an appraisal to manager
/// </summary>
public class ReturnAppraisalDto
{
    public string HRRemarks { get; set; } = string.Empty;
}

#endregion HR Review DTOs

/// <summary>
/// HR review list item for command center
/// </summary>
public class HRReviewListItemDto
{
    public Guid AppraisalId { get; set; }
    public string AppraisalNumber { get; set; } = string.Empty;
    public Guid EmployeeId { get; set; }
    public string EmployeeNumber { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public string OrganizationUnit { get; set; } = string.Empty;
    public string AppraisalCycleName { get; set; } = string.Empty;
    public int CycleYear { get; set; }
    
    // Evaluation Status
    public bool IsSelfEvaluationComplete { get; set; }
    public bool IsManagerEvaluationComplete { get; set; }
    public int RequiredPeerReviews { get; set; }
    public int CompletedPeerReviews { get; set; }
    public bool ArePeerReviewsComplete { get; set; }
    
    // HR Review Status
    public bool IsReadyForHRReview { get; set; }
    public string HRReviewStatus { get; set; } = string.Empty; // Not Started, In Review, Finalized
    public bool IsFinalized { get; set; }
    public DateTime? FinalizedDate { get; set; }
    public string? FinalizedByName { get; set; }
    
    // Score Summary
    public decimal? FinalScore { get; set; }
    public string? FinalGrade { get; set; }
}

/// <summary>
/// DTO for manager to view all peer evaluations for an appraisal.
/// Managers always see peer evaluator identities regardless of IsAnonymous setting.
/// IsAnonymous flag indicates whether appraisee can see peer evaluation details.
/// </summary>
public class ManagerPeerEvaluationReviewDto
{
    public Guid AppraisalId { get; set; }  
    public bool IsAnonymous { get; set; } // Indicates if appraisee can see peer details, not whether manager can
    public bool AllowKpiEvaluation { get; set; }
    public int TotalPeerEvaluators { get; set; }
    public int SubmittedEvaluations { get; set; }
    public List<PeerEvaluatorDetailDto> PeerEvaluations { get; set; } = new();
}

/// <summary>
/// Individual peer evaluator's complete evaluation.
/// EvaluatorName, EmployeeNumber, and Position are always populated for managers.
/// Anonymous setting only affects what the appraisee can see.
/// </summary>
public class PeerEvaluatorDetailDto
{
    public Guid EvaluationId { get; set; }
    public Guid EvaluatorId { get; set; }
    public string EvaluatorName { get; set; } = string.Empty; // Always shows actual name for managers
    public string? EvaluatorEmployeeNumber { get; set; } // Always shows for managers
    public string? EvaluatorPosition { get; set; } // Always shows for managers
    public bool IsSubmitted { get; set; }
    public DateTime? SubmittedDate { get; set; }
    public decimal? TotalScore { get; set; }
    
    // Competency evaluations
    public List<PeerCompetencyScoreDto> CompetencyScores { get; set; } = new();
    
    // KPI evaluations (if allowed)
    public List<PeerKpiEvaluationDto> KpiEvaluations { get; set; } = new();
}

/// <summary>
/// Peer's competency score detail
/// </summary>
public class PeerCompetencyScoreDto
{
    public Guid CriterionScoreId { get; set; }
    public string CriteriaName { get; set; } = string.Empty;
    public string? CriteriaDescription { get; set; }
    public int Weight { get; set; }
    public int NumericScore { get; set; }
    public decimal WeightedScore { get; set; }
    public string? Comments { get; set; }
    public string? AchievedGrade { get; set; }
}

/// <summary>
/// Peer's KPI evaluation detail
/// </summary>
public class PeerKpiEvaluationDto
{
    public Guid KpiEvaluationRecordId { get; set; }
    public string KpiName { get; set; } = string.Empty;
    public string? KpiDescription { get; set; }
    public decimal? TargetValue { get; set; }
    public decimal? ActualValue { get; set; }
    public decimal? AchievementPercent { get; set; }
    public string? Unit { get; set; }
    public string? Notes { get; set; }
    public string? AchievedGrade { get; set; }
}
/// <summary>
/// DTO for employee to acknowledge their appraisal
/// </summary>
public class AcknowledgeAppraisalDto
{
    public Guid EmployeeId { get; set; }
}

/// <summary>
/// DTO for appeal page data - provides all info needed to raise an appeal
/// </summary>
public class AppealPageDataDto
{
    public Guid AppraisalId { get; set; }
    public string AppraisalNumber { get; set; } = string.Empty;
    public string CycleName { get; set; } = string.Empty;
    public decimal? FinalScore { get; set; }
    public string? FinalGrade { get; set; }
    public bool CanAppeal { get; set; }
    public string? CannotAppealReason { get; set; }
    
    public List<AppealableKpiDto> AppealableKpis { get; set; } = new();
    public List<AppealableCompetencyDto> AppealableCompetencies { get; set; } = new();
}

/// <summary>
/// Appealable KPI item
/// </summary>
public class AppealableKpiDto
{
    public Guid EmployeeKpiTargetId { get; set; }
    public string KpiName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal? TargetValue { get; set; }
    public decimal? ActualValue { get; set; }
    public decimal? AchievementPercentage { get; set; }
    public string? Unit { get; set; }
    public int Weight { get; set; }
    public decimal? WeightedScore { get; set; }
}

/// <summary>
/// Appealable competency/criterion item
/// </summary>
public class AppealableCompetencyDto
{
    public Guid TemplateItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? NumericScore { get; set; }
    public int Weight { get; set; }
    public decimal? WeightedScore { get; set; }
}

/// <summary>
/// DTO for submitting an appeal from the UI
/// </summary>
public class SubmitAppealDto
{
    [Required]
    public Guid AppraisalId { get; set; }
    
    public string? OverallReason { get; set; }
    
    [Required]
    [MinLength(1, ErrorMessage = "At least one item must be appealed")]
    public List<AppealItemSubmissionDto> AppealedItems { get; set; } = new();
}

/// <summary>
/// Individual appeal item submission
/// </summary>
public class AppealItemSubmissionDto
{
    public Guid? TemplateItemId { get; set; }
    public Guid? EmployeeKpiTargetId { get; set; }
    
    [Required]
    [MaxLength(2000)]
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Appeal review data for HR resolution page
/// </summary>
public class AppealReviewDto
{
    // Appeal metadata
    public Guid AppealId { get; set; }
    public Guid AppraisalId { get; set; }
    public string AppraisalNumber { get; set; } = string.Empty;
    public DateTime SubmittedDate { get; set; }
    public AppraisalAppealStatus Status { get; set; }
    public string OverallAppealReason { get; set; } = string.Empty;
    
    // Employee context
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;
    public string? PositionTitle { get; set; }
    public string? OrganizationUnitName { get; set; }
    
    // Appraisal cycle context
    public string CycleName { get; set; } = string.Empty;
    public Guid CycleId { get; set; }
    public DateOnly CycleStartDate { get; set; }
    public DateOnly CycleEndDate { get; set; }
    
    // Appraisal scores
    public decimal? SelfEvaluationScore { get; set; }
    public decimal? PeerEvaluationScore { get; set; }
    public decimal? ManagerEvaluationScore { get; set; }
    public decimal OverallScore { get; set; }
    
    // Settings that control HR actions
    public bool HRCanModifyScores { get; set; }
    public Guid AppraisalSettingsId { get; set; }
    public string AppraisalSettingsName { get; set; } = string.Empty;
    
    // Appealed items with full evaluation details
    public List<AppealedCriterionReviewDto> AppealedCriteria { get; set; } = new();
    public List<AppealedKpiReviewDto> AppealedKpis { get; set; } = new();
}

/// <summary>
/// Appealed soft skill/competency criterion for review
/// </summary>
public class AppealedCriterionReviewDto
{
    public Guid AppealItemId { get; set; }
    public Guid TemplateItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string ItemDescription { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public string AppealReason { get; set; } = string.Empty;
    
    // Original evaluation scores
    public int? SelfScore { get; set; }
    public decimal? PeerAverageScore { get; set; }
    public int? ManagerScore { get; set; }
    
    // Weighted scores
    public decimal? SelfWeightedScore { get; set; }
    public decimal? PeerWeightedScore { get; set; }
    public decimal? ManagerWeightedScore { get; set; }
    public decimal FinalWeightedScore { get; set; }
    
    // Supporting evidence
    public string? ManagerComments { get; set; }
    public string? SelfComments { get; set; }
}

/// <summary>
/// Appealed KPI for review
/// </summary>
public class AppealedKpiReviewDto
{
    public Guid AppealItemId { get; set; }
    public Guid EmployeeKpiTargetId { get; set; }
    public string KpiName { get; set; } = string.Empty;
    public string KpiDescription { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public string AppealReason { get; set; } = string.Empty;
    
    // KPI target details
    public decimal TargetValue { get; set; }
    public decimal? ActualValue { get; set; }
    public string MeasurementUnit { get; set; } = string.Empty;
    
    // Evaluation scores
    public decimal? SelfScore { get; set; }
    public decimal? PeerAverageScore { get; set; }
    public decimal? ManagerScore { get; set; }
    
    // Weighted scores
    public decimal? SelfWeightedScore { get; set; }
    public decimal? PeerWeightedScore { get; set; }
    public decimal? ManagerWeightedScore { get; set; }
    public decimal FinalWeightedScore { get; set; }
    
    // Supporting evidence
    public string? ManagerComments { get; set; }
    public string? SelfComments { get; set; }
}

/// <summary>
/// Score modification for criterion during appeal resolution
/// </summary>
public class CriterionScoreModificationDto
{
    public Guid TemplateItemId { get; set; }
    public int NewScore { get; set; }
    
    [Required]
    [MaxLength(1000)]
    public string Justification { get; set; } = string.Empty;
}

/// <summary>
/// Score modification for KPI during appeal resolution
/// </summary>
public class KpiScoreModificationDto
{
    public Guid EmployeeKpiTargetId { get; set; }
    public decimal NewActualValue { get; set; }
    
    [Required]
    [MaxLength(1000)]
    public string Justification { get; set; } = string.Empty;
}

/// <summary>
/// Appeal resolution submission from HR
/// </summary>
public class ResolveAppealDto
{
    [Required]
    public AppraisalAppealStatus ResolutionDecision { get; set; }
    
    [Required]
    [MaxLength(4000)]
    public string ResolutionNotes { get; set; } = string.Empty;
    
    // Optional score modifications (only if HRCanModifyScores = true)
    public List<CriterionScoreModificationDto>? CriteriaModifications { get; set; }
    public List<KpiScoreModificationDto>? KpiModifications { get; set; }
}

/// <summary>
/// Post-remand review data for HR final decision
/// Shows comparison between pre-remand and post-remand manager evaluations
/// </summary>
public class PostRemandReviewDto
{
    // Appraisal context
    public Guid AppraisalId { get; set; }
    public string AppraisalNumber { get; set; } = string.Empty;
    public Guid AppealId { get; set; }
    public AppraisalAppealStatus AppealStatus { get; set; }
    
    // Employee context
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;
    public string? PositionTitle { get; set; }
    public string? OrganizationUnitName { get; set; }
    
    // Appraisal cycle context
    public string CycleName { get; set; } = string.Empty;
    public Guid CycleId { get; set; }
    public DateOnly CycleStartDate { get; set; }
    public DateOnly CycleEndDate { get; set; }
    
    // Appeal timeline
    public DateTime AppealSubmittedDate { get; set; }
    public DateTime AppealRemandedDate { get; set; }
    public DateTime AppealRemandDeadline { get; set; }
    public DateTime? ManagerReevaluationDate { get; set; }
    
    // Appeal summary
    public string OverallAppealReason { get; set; } = string.Empty;
    public string HRRemandJustification { get; set; } = string.Empty;
    
    // Score comparisons
    public List<CriterionScoreComparisonDto> CriteriaComparisons { get; set; } = new();
    public List<KpiScoreComparisonDto> KpiComparisons { get; set; } = new();
    
    // Overall score comparison
    public decimal PreRemandOverallScore { get; set; }
    public decimal PostRemandOverallScore { get; set; }
    
    // Settings
    public bool HRCanModifyScores { get; set; }
}

/// <summary>
/// Comparison of criterion scores before and after remand
/// </summary>
public class CriterionScoreComparisonDto
{
    public Guid TemplateItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string ItemDescription { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public bool WasAppealed { get; set; }
    public string? AppealReason { get; set; }
    
    // Pre-remand (from snapshot)
    public int? PreRemandScore { get; set; }
    public decimal? PreRemandWeightedScore { get; set; }
    public string? PreRemandComments { get; set; }
    
    // Post-remand (current manager evaluation)
    public int? PostRemandScore { get; set; }
    public decimal? PostRemandWeightedScore { get; set; }
    public string? PostRemandComments { get; set; }
    
    // Change indicators
    public bool ScoreChanged => PreRemandScore != PostRemandScore;
    public int? ScoreDifference => PostRemandScore.HasValue && PreRemandScore.HasValue 
        ? PostRemandScore.Value - PreRemandScore.Value 
        : null;
}

/// <summary>
/// Comparison of KPI scores before and after remand
/// </summary>
public class KpiScoreComparisonDto
{
    public Guid EmployeeKpiTargetId { get; set; }
    public string KpiName { get; set; } = string.Empty;
    public string KpiDescription { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public bool WasAppealed { get; set; }
    public string? AppealReason { get; set; }
    
    // Target details
    public decimal TargetValue { get; set; }
    public string MeasurementUnit { get; set; } = string.Empty;
    
    // Pre-remand (from snapshot)
    public decimal? PreRemandActualValue { get; set; }
    public decimal? PreRemandAchievementPercent { get; set; }
    public decimal? PreRemandWeightedScore { get; set; }
    public string? PreRemandComments { get; set; }
    
    // Post-remand (current manager evaluation)
    public decimal? PostRemandActualValue { get; set; }
    public decimal? PostRemandAchievementPercent { get; set; }
    public decimal? PostRemandWeightedScore { get; set; }
    public string? PostRemandComments { get; set; }
    
    // Change indicators
    public bool ScoreChanged => PreRemandActualValue != PostRemandActualValue;
    public decimal? ActualValueDifference => PostRemandActualValue.HasValue && PreRemandActualValue.HasValue 
        ? PostRemandActualValue.Value - PreRemandActualValue.Value 
        : null;
}

/// <summary>
/// HR final decision submission for post-remand appeal
/// </summary>
public class PostRemandFinalDecisionDto
{
    [Required]
    public AppraisalAppealStatus FinalDecision { get; set; } // Must be Upheld or Rejected
    
    [Required]
    [MaxLength(4000)]
    public string HRFinalNotes { get; set; } = string.Empty;
}

/// <summary>
/// Employee read-only view of final appeal outcome after HR decision
/// </summary>
public class EmployeeAppealOutcomeDto
{
    // Appraisal Context
    public Guid AppraisalId { get; set; }
    public string AppraisalNumber { get; set; } = "";
    public string CycleName { get; set; } = "";
    public DateOnly CycleStartDate { get; set; }
    public DateOnly CycleEndDate { get; set; }
    public string PositionTitle { get; set; } = "";
    public string OrganizationUnitName { get; set; } = "";
    
    // Appeal Status
    public AppraisalAppealStatus AppealStatus { get; set; }
    public DateTime AppealResolvedDate { get; set; }
    public bool IsUpheld => AppealStatus == AppraisalAppealStatus.Upheld;
    public bool IsRejected => AppealStatus == AppraisalAppealStatus.Rejected;
    
    // Employee's Original Appeal
    public DateTime AppealSubmittedDate { get; set; }
    public string EmployeeAppealReason { get; set; } = "";
    public List<string> AppealedItems { get; set; } = new();
    
    // HR Final Decision
    public string HRFinalNotes { get; set; } = "";
    public string OutcomeMessage { get; set; } = "";
    
    // Final Scores
    public decimal FinalOverallScore { get; set; }
    /// <summary>The overall score the appeal was filed against; null on appeals filed before it was kept.</summary>
    public decimal? OriginalOverallScore { get; set; }
    public List<FinalCriterionScoreDto> FinalCriteriaScores { get; set; } = new();
    public List<FinalKpiScoreDto> FinalKpiScores { get; set; } = new();
    
    // Change Indicators
    public bool ScoresChangedAfterAppeal { get; set; }
}

public class FinalCriterionScoreDto
{
    public Guid TemplateItemId { get; set; }
    public string ItemName { get; set; } = "";
    public string ItemDescription { get; set; } = "";
    public int? FinalScore { get; set; }
    public decimal FinalWeightedScore { get; set; }
    public int Weight { get; set; }
    public string ManagerComments { get; set; } = "";
    public bool WasAppealed { get; set; }
    /// <summary>A KPI whose achievement calibration or an appeal restated to <see cref="FinalScore"/> percent (D-22, A14).</summary>
    public bool AchievementOverridden { get; set; }
}

// ============================================================
// AppraisalTemplate
// ============================================================

public class AppraisalTemplateDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? OrganizationLevelId { get; set; }
    public string? OrganizationLevelName { get; set; }
    public Guid? OrganizationUnitId { get; set; }
    public string? OrganizationUnitName { get; set; }
    public Guid? PositionId { get; set; }
    public string? PositionTitle { get; set; }
    public bool IsActive { get; set; }
    public TemplateApprovalStatus ApprovalStatus { get; set; }
    public DateTime? SubmittedDate { get; set; }
    public DateTime? ApprovalDate { get; set; }
    public string? RejectionReason { get; set; }
}

public class CreateAppraisalTemplateDto : CreateDtoBase
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
}

public class UpdateAppraisalTemplateDto : UpdateDtoBase
{
    [Required]
    [MaxLength(100)]
    public string TemplateName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public Guid? OrganizationLevelId { get; set; }
    public Guid? OrganizationUnitId { get; set; }
    public Guid? PositionId { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// Payload for copying an existing template into a new Organization Level + Unit + Position
/// combination. The scope fields target the copy (any/all may be null for a global template);
/// they are not inherited from the source.
/// </summary>
public class CopyAppraisalTemplateDto
{
    [Required]
    [MaxLength(100)]
    public string NewTemplateName { get; set; } = string.Empty;

    public Guid? OrganizationLevelId { get; set; }
    public Guid? OrganizationUnitId { get; set; }
    public Guid? PositionId { get; set; }
}

/// <summary>
/// Lightweight summary used by the Template List page.
/// Includes section/item counts and cycle-assignment flag without
/// loading full navigation graphs.
/// </summary>
public class AppraisalTemplateSummaryDto : BaseDto
{
    public string TemplateName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? OrganizationLevelId { get; set; }
    public string? OrganizationLevelName { get; set; }
    public Guid? OrganizationUnitId { get; set; }
    public string? OrganizationUnitName { get; set; }
    public Guid? PositionId { get; set; }
    public string? PositionTitle { get; set; }
    public bool IsActive { get; set; }
    public TemplateApprovalStatus ApprovalStatus { get; set; }
    public int SectionsCount { get; set; }
    public int TotalItemsCount { get; set; }
    public bool HasCycleAssignments { get; set; }
}

/// <summary>Reason payload for rejecting a submitted appraisal template.</summary>
public class RejectAppraisalTemplateDto
{
    [MaxLength(1000)]
    public string? Reason { get; set; }
}

// ============================================================
// GoalRequiredSkill (Theme 3 — soft skills per goal)
// ============================================================

public class GoalRequiredSkillDto : BaseDto
{
    public Guid EmployeeGoalId { get; set; }
    public Guid CompetencyId { get; set; }
    public string? CompetencyName { get; set; }
    public bool DevelopmentNeeded { get; set; }
    public string? Note { get; set; }
}

/// <summary>A competency suggested for development, derived from an employee's goals' required skills (Theme 3).</summary>
public class DevelopmentSkillSuggestionDto
{
    public Guid CompetencyId { get; set; }
    public string? CompetencyName { get; set; }
    /// <summary>Titles of the goals that flagged this skill as needing development.</summary>
    public List<string> FromGoals { get; set; } = new();
}

/// <summary>One required-skill entry in a replace-all set for an employee goal.</summary>
public class SetGoalRequiredSkillDto
{
    [Required]
    public Guid CompetencyId { get; set; }
    public bool DevelopmentNeeded { get; set; } = true;
    [MaxLength(500)]
    public string? Note { get; set; }
}

// ============================================================
// CheckInObjectiveLink (Theme 6 — check-in ↔ yearly objective)
// ============================================================

public class CheckInObjectiveLinkDto : BaseDto
{
    public Guid CheckInId { get; set; }
    public Guid CompanyGoalId { get; set; }
    public string? ObjectiveTitle { get; set; }
}

// ============================================================
// Appraisal Calendar (Theme 5 — derived, read-only)
// ============================================================

/// <summary>A single dated entry on the appraisal calendar, derived from cycle activity dates,
/// review events and check-ins. Not persisted.</summary>
public class AppraisalCalendarEventDto
{
    public DateOnly Date { get; set; }
    public string Title { get; set; } = string.Empty;
    /// <summary>e.g. "Deadline", "Opens", "Review", "Check-in".</summary>
    public string Category { get; set; } = string.Empty;
    /// <summary>The workflow phase this belongs to (Goal Setting, Self-Evaluation, …).</summary>
    public string? Phase { get; set; }
    public Guid CycleId { get; set; }
    public string? CycleName { get; set; }
}

// ============================================================
// Appraisal Outcome Recommendation (Theme 8 backbone)
// ============================================================

public class AppraisalOutcomeRecommendationDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid PerformanceAppraisalId { get; set; }
    public string? AppraisalNumber { get; set; }
    public string? EmployeeName { get; set; }
    public RecommendationType RecommendationType { get; set; }
    public RecommendationStatus Status { get; set; }
    public Guid? RecommendedById { get; set; }
    public DateTime? RecommendedDate { get; set; }
    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public DateTime? ActionedDate { get; set; }
    public string? Notes { get; set; }
    public string? ResolutionNotes { get; set; }
    public string? TargetEntityType { get; set; }
    public Guid? TargetEntityId { get; set; }
}

public class CreateAppraisalOutcomeRecommendationDto : CreateDtoBase
{
    [Required]
    public Guid PerformanceAppraisalId { get; set; }

    [Required]
    public RecommendationType RecommendationType { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

/// <summary>Notes payload for approve/reject/dismiss actions.</summary>
public class ResolveRecommendationDto
{
    [MaxLength(1000)]
    public string? Notes { get; set; }
}

// ============================================================
// Performance Analytics (Themes 13-14, read-only)
// ============================================================

/// <summary>One bucket of a rating distribution (Theme 13).</summary>
public class RatingBucketDto
{
    public PerformanceRating Rating { get; set; }
    public string RatingLabel { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal Percent { get; set; }
}

/// <summary>Rating distribution for a cycle, used to surface calibration leniency/skew (Theme 13).</summary>
public class CalibrationDistributionDto
{
    public Guid CycleId { get; set; }
    public string? CycleName { get; set; }
    public int TotalRated { get; set; }
    public decimal? AverageScore { get; set; }
    public List<RatingBucketDto> Buckets { get; set; } = new();
}

/// <summary>One year on an employee's multi-year performance trend (Theme 14).</summary>
public class PerformanceTrendPointDto
{
    public int Year { get; set; }
    public Guid AppraisalId { get; set; }
    public string? CycleName { get; set; }
    public decimal? OverallScore { get; set; }
    public PerformanceRating? Rating { get; set; }
    public string? Status { get; set; }
    /// <summary>
    /// The outcome is released to the employee (performance closure P2). Their own trend carries no
    /// score or rating for an appraisal still in progress.
    /// </summary>
    public bool OutcomeReleased { get; set; }
}

/// <summary>An employee's appraisal scores across cycles/years (Theme 14).</summary>
public class EmployeePerformanceTrendDto
{
    public Guid EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public List<PerformanceTrendPointDto> Points { get; set; } = new();
}

// ============================================================
// Salary Review Proposal (Theme 11 — comp intake record)
// ============================================================

public class SalaryReviewProposalDto : BaseDto
{
    public Guid EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public Guid? SourceAppraisalId { get; set; }
    public string? AppraisalNumber { get; set; }
    public SalaryReviewProposalType ProposalType { get; set; }
    public decimal? ProposedPercent { get; set; }
    public decimal? ProposedAmount { get; set; }
    public SalaryReviewProposalStatus Status { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Puts numbers on a compensation proposal before anyone approves it. The handler that raises
/// the proposal has an appraisal, not a pay decision, so it can only record the intent — without
/// this a merit increase could be approved and handed to payroll with no percentage on it.
/// </summary>
public class UpdateSalaryReviewProposalDto
{
    [Range(0, 100)]
    public decimal? ProposedPercent { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? ProposedAmount { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

public class EmploymentActionProposalDto : BaseDto
{
    public Guid EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public Guid? SourceAppraisalId { get; set; }
    public string? AppraisalNumber { get; set; }
    public EmploymentActionType ActionType { get; set; }
    public EmploymentActionProposalStatus Status { get; set; }
    public string? Notes { get; set; }
}

/// <summary>Notes recorded alongside an employment-action proposal decision.</summary>
public class UpdateEmploymentActionProposalDto
{
    [MaxLength(1000)]
    public string? Notes { get; set; }
}

// ============================================================
// Full interim appraisal (Theme 7 — score a review event's period goals)
// ============================================================

public class InterimGoalScoreDto
{
    public Guid EmployeeGoalId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int Weight { get; set; }
    public string Period { get; set; } = string.Empty;
    public decimal? CurrentProgress { get; set; }
    /// <summary>A score already recorded for this goal in this review event, if any.</summary>
    public decimal? ExistingScore { get; set; }
}

public class FullInterimAppraisalContextDto
{
    public AppraisalReviewEventDto Event { get; set; } = new();
    public string? EmployeeName { get; set; }
    public List<InterimGoalScoreDto> Goals { get; set; } = new();
}

public class InterimGoalScoreInputDto
{
    [Required]
    public Guid EmployeeGoalId { get; set; }
    [Range(0, 100)]
    public decimal Score { get; set; }
    [MaxLength(1000)]
    public string? Note { get; set; }
}

public class FinalizeFullInterimAppraisalDto
{
    public List<InterimGoalScoreInputDto> Scores { get; set; } = new();
    [MaxLength(2000)]
    public string? ManagerNotes { get; set; }
}

// ============================================================
// AppraisalTemplateSection
// ============================================================

public class AppraisalTemplateSectionDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid AppraisalTemplateId { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public string SectionName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DisplayOrder { get; set; }
    public int Weight { get; set; }
}

public class CreateAppraisalTemplateSectionDto : CreateDtoBase
{
    [Required]
    public Guid AppraisalTemplateId { get; set; }

    [Required]
    [MaxLength(200)]
    public string SectionName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public int DisplayOrder { get; set; }

    [Range(0, 100)]
    public int Weight { get; set; }
}

public class UpdateAppraisalTemplateSectionDto : UpdateDtoBase
{
    [Required]
    public Guid AppraisalTemplateId { get; set; }

    [Required]
    [MaxLength(200)]
    public string SectionName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public int DisplayOrder { get; set; }

    [Range(0, 100)]
    public int Weight { get; set; }
}

// ============================================================
// AppraisalTemplateItem
// ============================================================

public class AppraisalTemplateItemDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid AppraisalTemplateSectionId { get; set; }
    public string SectionName { get; set; } = string.Empty;
    public Guid? CompetencyId { get; set; }
    public string? CompetencyName { get; set; }
    public Guid? KpiDefinitionId { get; set; }
    public string? KpiName { get; set; }
    public decimal? KpiTargetValue { get; set; }
    public decimal? KpiMinValue { get; set; }
    public decimal? KpiMaxValue { get; set; }
    public string? CustomQuestion { get; set; }
    public int DisplayOrder { get; set; }
    public int Weight { get; set; }
    public int GradeRangeCount { get; set; }
}

public class CreateAppraisalTemplateItemDto : CreateDtoBase
{
    [Required]
    public Guid AppraisalTemplateSectionId { get; set; }

    public Guid? CompetencyId { get; set; }
    public Guid? KpiDefinitionId { get; set; }
    public decimal? KpiTargetValue { get; set; }
    public decimal? KpiMinValue { get; set; }
    public decimal? KpiMaxValue { get; set; }

    [MaxLength(500)]
    public string? CustomQuestion { get; set; }

    public int DisplayOrder { get; set; }

    [Range(0, 100)]
    public int Weight { get; set; }
}

public class UpdateAppraisalTemplateItemDto : UpdateDtoBase
{
    [Required]
    public Guid AppraisalTemplateSectionId { get; set; }

    public Guid? CompetencyId { get; set; }
    public Guid? KpiDefinitionId { get; set; }
    public decimal? KpiTargetValue { get; set; }
    public decimal? KpiMinValue { get; set; }
    public decimal? KpiMaxValue { get; set; }

    [MaxLength(500)]
    public string? CustomQuestion { get; set; }

    public int DisplayOrder { get; set; }

    [Range(0, 100)]
    public int Weight { get; set; }
}

// ============================================================
// GoalLibrary
// ============================================================

public class GoalLibraryDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? SuccessCriteria { get; set; }
    public Guid? OrganizationLevelId { get; set; }
    public string? OrganizationLevelName { get; set; }
    public Guid? OrganizationUnitId { get; set; }
    public string? OrganizationUnitName { get; set; }
    public Guid? PositionId { get; set; }
    public string? PositionTitle { get; set; }
    public bool IsActive { get; set; }
}

public class CreateGoalLibraryDto : CreateDtoBase
{
    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(1000)]
    public string? SuccessCriteria { get; set; }

    public Guid? OrganizationLevelId { get; set; }
    public Guid? OrganizationUnitId { get; set; }
    public Guid? PositionId { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdateGoalLibraryDto : UpdateDtoBase
{
    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(1000)]
    public string? SuccessCriteria { get; set; }

    public Guid? OrganizationLevelId { get; set; }
    public Guid? OrganizationUnitId { get; set; }
    public Guid? PositionId { get; set; }
    public bool IsActive { get; set; }
}

public class GoalLibraryDetailsDto : GoalLibraryDto
{
    public int TotalGoals { get; set; }
    public int UniqueEmployees { get; set; }
    public int CycleCount { get; set; }
}

public class GoalLibraryUsageStatsDto
{
    public int TotalGoals { get; set; }
    public int UniqueEmployees { get; set; }
    public int CycleCount { get; set; }
}

public class GoalLibraryUsageRowDto
{
    public Guid EmployeeGoalId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string AppraisalCycleName { get; set; } = string.Empty;
    public string GoalTitle { get; set; } = string.Empty;
    public GoalStatus Status { get; set; }
    public decimal ProgressPercent { get; set; }
}

/// <summary>
/// Lightweight projection DTO used by the GoalLibrarySelector modal component.
/// Contains only the fields needed for browsing, searching, and previewing templates.
/// </summary>
public class GoalLibrarySelectorDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? SuccessCriteria { get; set; }

    /// <summary>
    /// Human-readable summary of the template's scope restriction.
    /// Examples: "Global", "Level: Division", "Unit: Finance", "Position: Senior Analyst"
    /// </summary>
    public string ScopeSummary { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

// ============================================================
// AppraisalCycleTargetExclusion
// ============================================================

public class AppraisalCycleTargetExclusionDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid AppraisalCycleTargetId { get; set; }
    public Guid? OrganizationLevelId { get; set; }
    public string? OrganizationLevelName { get; set; }
    public Guid? OrganizationUnitId { get; set; }
    public string? OrganizationUnitName { get; set; }
    public Guid? PositionId { get; set; }
    public string? PositionTitle { get; set; }
    public Guid? EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public string Reason { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class CreateAppraisalCycleTargetExclusionDto : CreateDtoBase
{
    [Required]
    public Guid AppraisalCycleTargetId { get; set; }

    public Guid? OrganizationLevelId { get; set; }
    public Guid? OrganizationUnitId { get; set; }
    public Guid? PositionId { get; set; }
    public Guid? EmployeeId { get; set; }

    [Required]
    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}

public class UpdateAppraisalCycleTargetExclusionDto : UpdateDtoBase
{
    [Required]
    public Guid AppraisalCycleTargetId { get; set; }

    public Guid? OrganizationLevelId { get; set; }
    public Guid? OrganizationUnitId { get; set; }
    public Guid? PositionId { get; set; }
    public Guid? EmployeeId { get; set; }

    [Required]
    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;

    public bool IsActive { get; set; }
}

// ============================================================
// AppraisalCycleTemplate
// ============================================================

/// <summary>
/// Scope fields (OrganizationLevel, OrganizationUnit, Position) are read-through
/// from the linked AppraisalTemplate — they are NOT stored on the cycle-template record.
/// </summary>
public class AppraisalCycleTemplateDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid AppraisalCycleId { get; set; }
    public string? CycleCode { get; set; }
    public Guid AppraisalTemplateId { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    // Scope — sourced from AppraisalTemplate
    public Guid? OrganizationLevelId { get; set; }
    public string? OrganizationLevelName { get; set; }
    public Guid? OrganizationUnitId { get; set; }
    public string? OrganizationUnitName { get; set; }
    public Guid? PositionId { get; set; }
    public string? PositionTitle { get; set; }
    public int Priority { get; set; }
    public bool IsActive { get; set; }
}

public class CreateAppraisalCycleTemplateDto : CreateDtoBase
{
    [Required]
    public Guid AppraisalCycleId { get; set; }

    [Required]
    public Guid AppraisalTemplateId { get; set; }

    public int Priority { get; set; } = 0;
    public bool IsActive { get; set; } = true;
}

public class UpdateAppraisalCycleTemplateDto : UpdateDtoBase
{
    [Required]
    public Guid AppraisalCycleId { get; set; }

    [Required]
    public Guid AppraisalTemplateId { get; set; }

    public int Priority { get; set; }
    public bool IsActive { get; set; }
}

// ============================================================
// CompanyGoal
// ============================================================

// ============================================================
// StrategicGoal (multi-year company vision)
// ============================================================

public class StrategicGoalDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? SuccessCriteria { get; set; }
    public GoalPriority Priority { get; set; }
    public int StartYear { get; set; }
    public int EndYear { get; set; }
    public bool IsActive { get; set; }
    public int YearlyObjectiveCount { get; set; }
}

public class CreateStrategicGoalDto : CreateDtoBase
{
    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(1000)]
    public string? SuccessCriteria { get; set; }

    public GoalPriority Priority { get; set; } = GoalPriority.High;

    [Range(2000, 2100)]
    public int StartYear { get; set; }

    [Range(2000, 2100)]
    public int EndYear { get; set; }

    public bool IsActive { get; set; } = true;
}

public class UpdateStrategicGoalDto : UpdateDtoBase
{
    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(1000)]
    public string? SuccessCriteria { get; set; }

    public GoalPriority Priority { get; set; }

    [Range(2000, 2100)]
    public int StartYear { get; set; }

    [Range(2000, 2100)]
    public int EndYear { get; set; }

    public bool IsActive { get; set; }
}

public class CompanyGoalDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid AppraisalCycleId { get; set; }
    public string? CycleCode { get; set; }
    public Guid? StrategicGoalId { get; set; }
    public string? StrategicGoalTitle { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? SuccessCriteria { get; set; }
    public GoalPriority Priority { get; set; }
    public decimal? TargetValue { get; set; }
    public string? Unit { get; set; }
    public DateOnly? DueDate { get; set; }
    public bool IsVisible { get; set; }
}

public class CreateCompanyGoalDto : CreateDtoBase
{
    [Required]
    public Guid AppraisalCycleId { get; set; }

    public Guid? StrategicGoalId { get; set; }

    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(1000)]
    public string? SuccessCriteria { get; set; }

    public GoalPriority Priority { get; set; } = GoalPriority.High;

    [Range(0, double.MaxValue)]
    public decimal? TargetValue { get; set; }

    [MaxLength(50)]
    public string? Unit { get; set; }

    public DateOnly? DueDate { get; set; }
    public bool IsVisible { get; set; } = true;
}

public class UpdateCompanyGoalDto : UpdateDtoBase
{
    [Required]
    public Guid AppraisalCycleId { get; set; }

    public Guid? StrategicGoalId { get; set; }

    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(1000)]
    public string? SuccessCriteria { get; set; }

    public GoalPriority Priority { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? TargetValue { get; set; }

    [MaxLength(50)]
    public string? Unit { get; set; }

    public DateOnly? DueDate { get; set; }
    public bool IsVisible { get; set; }
}

// ============================================================
// UnitGoal
// ============================================================

public class UnitGoalDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid AppraisalCycleId { get; set; }
    public string? CycleCode { get; set; }
    public Guid? ParentCompanyGoalId { get; set; }
    public string? ParentGoalTitle { get; set; }
    public Guid? ParentUnitGoalId { get; set; }
    public string? ParentUnitGoalTitle { get; set; }
    public Guid OrganizationLevelId { get; set; }
    public string OrganizationLevelName { get; set; } = string.Empty;
    public Guid OrganizationUnitId { get; set; }
    public string OrganizationUnitName { get; set; } = string.Empty;
    public Guid CreatedByManagerId { get; set; }
    public string ManagerName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? SuccessCriteria { get; set; }
    public GoalPriority Priority { get; set; }
    public decimal? TargetValue { get; set; }
    public string? Unit { get; set; }
    public DateOnly? DueDate { get; set; }
}

public class CreateUnitGoalDto : CreateDtoBase
{
    [Required]
    public Guid AppraisalCycleId { get; set; }

    public Guid? ParentCompanyGoalId { get; set; }

    public Guid? ParentUnitGoalId { get; set; }

    [Required]
    public Guid OrganizationLevelId { get; set; }

    [Required]
    public Guid OrganizationUnitId { get; set; }

    [Required]
    public Guid CreatedByManagerId { get; set; }

    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(1000)]
    public string? SuccessCriteria { get; set; }

    public GoalPriority Priority { get; set; } = GoalPriority.High;

    [Range(0, double.MaxValue)]
    public decimal? TargetValue { get; set; }

    [MaxLength(50)]
    public string? Unit { get; set; }

    public DateOnly? DueDate { get; set; }
}

public class UpdateUnitGoalDto : UpdateDtoBase
{
    [Required]
    public Guid AppraisalCycleId { get; set; }

    public Guid? ParentCompanyGoalId { get; set; }

    public Guid? ParentUnitGoalId { get; set; }

    [Required]
    public Guid OrganizationLevelId { get; set; }

    [Required]
    public Guid OrganizationUnitId { get; set; }

    [Required]
    public Guid CreatedByManagerId { get; set; }

    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(1000)]
    public string? SuccessCriteria { get; set; }

    public GoalPriority Priority { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? TargetValue { get; set; }

    [MaxLength(50)]
    public string? Unit { get; set; }

    public DateOnly? DueDate { get; set; }
}

// ============================================================
// UnitGoalListItemDto  — projection DTO for the alignment
// dashboard list view.  Never includes full nav collections.
// ============================================================

public class UnitGoalListItemDto
{
    public Guid Id { get; set; }
    public Guid AppraisalCycleId { get; set; }
    public string? CycleCode { get; set; }
    public Guid? ParentCompanyGoalId { get; set; }
    /// <summary>Title of the parent CompanyGoal — null when unlinked.</summary>
    public string? ParentCompanyGoalTitle { get; set; }
    public Guid OrganizationUnitId { get; set; }
    public string OrganizationUnitName { get; set; } = string.Empty;
    public string OrganizationLevelName { get; set; } = string.Empty;
    public Guid CreatedByManagerId { get; set; }
    public string ManagerName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    /// <summary>First 200 chars of Description — safe for card preview.</summary>
    public string? DescriptionPreview { get; set; }
    public GoalPriority Priority { get; set; }
    public decimal? TargetValue { get; set; }
    public string? Unit { get; set; }
    public DateOnly? DueDate { get; set; }
    public int EmployeeGoalsCount { get; set; }
}

// ============================================================
// UnitGoalDashboardMetricsDto  — aggregated alignment metrics
// for the unit goal dashboard header tiles (one query per cycle).
// ============================================================

public class UnitGoalDashboardMetricsDto
{
    public Guid CycleId { get; set; }
    public int TotalUnitGoals { get; set; }
    public int LinkedToCompanyGoal { get; set; }
    public int UnlinkedCount { get; set; }
    public int TotalEmployeeGoalsCascaded { get; set; }
}

// ============================================================
// UnitGoalCascadeStatsDto  — lightweight projection for the
// create/edit page cascade integrity check.  Only loads count.
// ============================================================

public class UnitGoalCascadeStatsDto
{
    public Guid GoalId { get; set; }
    public int EmployeeGoalsCount { get; set; }

    /// <summary>
    /// Mean progress across the aligned employee goals; null when there are none. The figure
    /// everyone sees — the per-employee rows are the desk's and the unit line's (P11).
    /// </summary>
    public decimal? AverageProgressPercent { get; set; }
}

// ============================================================
// UnitGoalEmployeeGoalSummaryDto — lightweight projection for
// the Employee Goals section on the UnitGoal detail page.
// Only loads fields needed for the cascade depth list.
// ============================================================

public class UnitGoalEmployeeGoalSummaryDto
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public GoalStatus Status { get; set; }
    public GoalPriority Priority { get; set; }
    public decimal ProgressPercent { get; set; }
    public DateOnly DueDate { get; set; }
}

// ============================================================
// EmployeeGoal
// ============================================================

public class EmployeeGoalDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public Guid AppraisalCycleId { get; set; }
    public string? CycleCode { get; set; }
    public Guid? PerformanceAppraisalId { get; set; }
    public Guid? CompanyGoalId { get; set; }
    public Guid? UnitGoalId { get; set; }
    public Guid? ParentGoalId { get; set; }  // self-ref: parent is another EmployeeGoal
    public string? ParentGoalTitle { get; set; }
    public GoalParentType? ParentType { get; set; }
    public Guid? GoalLibraryId { get; set; }
    public string? LibraryItemTitle { get; set; }
    public Guid? KpiDefinitionId { get; set; }
    public string? KpiName { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? SuccessCriteria { get; set; }
    public int Weight { get; set; }
    public GoalPriority Priority { get; set; }
    public GoalStatus Status { get; set; }
    public MeasurementType MeasurementType { get; set; }
    public GoalPeriod Period { get; set; }
    public decimal? TargetValue { get; set; }
    public decimal? MinValue { get; set; }
    public decimal? MaxValue { get; set; }
    public string? Unit { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly DueDate { get; set; }
    public decimal ProgressPercent { get; set; }
    public Guid? SubmittedToManagerId { get; set; }
    public string? ManagerName { get; set; }
    public DateTime? SubmittedDate { get; set; }
    public DateTime? ApprovalDate { get; set; }
    public string? ManagerFeedback { get; set; }
    public bool IsLocked { get; set; }
    public DateTime? LockedDate { get; set; }

    // ── Appraisal-scoped assessments (populated by context-aware load) ─────────
    public decimal? SelfFinalProgressPercent { get; set; }
    public GoalProgressStatus? SelfFinalStatus { get; set; }
    public decimal? SelfFinalActualValue { get; set; }
    public string? SelfAssessmentNotes { get; set; }
    public string? SelfEvidenceLinks { get; set; }

    public decimal? ManagerFinalProgressPercent { get; set; }
    public GoalProgressStatus? ManagerFinalStatus { get; set; }
    public decimal? ManagerFinalActualValue { get; set; }
    public string? ManagerAssessmentNotes { get; set; }
    public string? ManagerEvidenceLinks { get; set; }
}

/// <summary>Input model for saving one goal's year-end assessment (self or manager).</summary>
public class GoalAssessmentInputDto
{
    [Required]
    public Guid GoalId { get; set; }
    public decimal? FinalProgressPercent { get; set; }
    public GoalProgressStatus? FinalStatus { get; set; }
    public decimal? FinalActualValue { get; set; }
    public string? AssessmentNotes { get; set; }
    public string? EvidenceLinks { get; set; }
}

public class CreateEmployeeGoalDto : CreateDtoBase
{
    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    public Guid AppraisalCycleId { get; set; }

    public Guid? PerformanceAppraisalId { get; set; }
    public Guid? CompanyGoalId { get; set; }
    public Guid? UnitGoalId { get; set; }
    public Guid? ParentGoalId { get; set; }  // self-ref: parent is another EmployeeGoal
    public GoalParentType? ParentType { get; set; }  // computed server-side from FK values
    public Guid? GoalLibraryId { get; set; }
    public Guid? KpiDefinitionId { get; set; }

    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(1000)]
    public string? SuccessCriteria { get; set; }

    [Range(0, 100)]
    public int Weight { get; set; }

    public GoalPriority Priority { get; set; } = GoalPriority.Medium;
    public MeasurementType MeasurementType { get; set; } = MeasurementType.NumericAbsolute;
    public GoalPeriod Period { get; set; } = GoalPeriod.FullCycle;

    [Range(0, double.MaxValue)]
    public decimal? TargetValue { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? MinValue { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? MaxValue { get; set; }

    [MaxLength(50)]
    public string? Unit { get; set; }

    [Required]
    public DateOnly StartDate { get; set; }

    [Required]
    public DateOnly DueDate { get; set; }

    public Guid? SubmittedToManagerId { get; set; }
}

public class UpdateEmployeeGoalDto : UpdateDtoBase
{
    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    public Guid AppraisalCycleId { get; set; }

    public Guid? PerformanceAppraisalId { get; set; }
    public Guid? CompanyGoalId { get; set; }
    public Guid? UnitGoalId { get; set; }
    public Guid? ParentGoalId { get; set; }  // self-ref: parent is another EmployeeGoal
    public GoalParentType? ParentType { get; set; }  // computed server-side from FK values
    public Guid? KpiDefinitionId { get; set; }

    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(1000)]
    public string? SuccessCriteria { get; set; }

    [Range(0, 100)]
    public int Weight { get; set; }

    public GoalPriority Priority { get; set; }
    public MeasurementType MeasurementType { get; set; }
    public GoalPeriod Period { get; set; } = GoalPeriod.FullCycle;

    [Range(0, double.MaxValue)]
    public decimal? TargetValue { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? MinValue { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? MaxValue { get; set; }

    [MaxLength(50)]
    public string? Unit { get; set; }

    [Required]
    public DateOnly StartDate { get; set; }

    [Required]
    public DateOnly DueDate { get; set; }

    [Range(0, 100)]
    public decimal ProgressPercent { get; set; }

    // No Status / SubmittedToManagerId / ManagerFeedback here on purpose. Those move only through
    // the submit / approve / reject / lock commands on EmployeeGoalsController, which enforce the
    // transition table and the direct-manager check. Accepting them on a plain edit made every one
    // of those rules optional.
}

// ============================================================
// GoalProgressEntry
// ============================================================

public class GoalProgressEntryDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid EmployeeGoalId { get; set; }
    public string GoalTitle { get; set; } = string.Empty;
    public decimal? ProgressPercent { get; set; }
    public decimal? ActualValue { get; set; }
    public GoalProgressStatus Status { get; set; }
    public string? Challenges { get; set; }
    public string? Notes { get; set; }
    public Guid RecordedById { get; set; }
    public string RecordedByName { get; set; } = string.Empty;
    public DateTime EntryDate { get; set; }
    public Guid? ReviewEventId { get; set; }
}

public class CreateGoalProgressEntryDto : CreateDtoBase
{
    [Required]
    public Guid EmployeeGoalId { get; set; }

    [Range(0, 100)]
    public decimal? ProgressPercent { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? ActualValue { get; set; }

    public GoalProgressStatus Status { get; set; }

    [MaxLength(500)]
    public string? Challenges { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    // RecordedById is deliberately absent: the recorder is taken from the caller's token. Both
    // write paths (EmployeeGoals and AppraisalReviewEvents) stamp it, so a progress entry cannot be
    // attributed to someone who did not make it.

    public Guid? ReviewEventId { get; set; }
}

public class UpdateGoalProgressEntryDto : UpdateDtoBase
{
    [Required]
    public Guid EmployeeGoalId { get; set; }

    [Range(0, 100)]
    public decimal? ProgressPercent { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? ActualValue { get; set; }

    public GoalProgressStatus Status { get; set; }

    [MaxLength(500)]
    public string? Challenges { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    public Guid? ReviewEventId { get; set; }
}

// ============================================================
// CheckIn
// ============================================================

public class CheckInDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid AppraisalCycleId { get; set; }
    public string? CycleCode { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public Guid ConductedById { get; set; }
    public string ConductedByName { get; set; } = string.Empty;
    public CheckInType CheckInType { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime ScheduledDate { get; set; }
    public DateTime? ConductedDate { get; set; }
    public string? Agenda { get; set; }
    public string? SharedNotes { get; set; }
    public string? PrivateNotes { get; set; }
    public string? ActionItems { get; set; }
    public DateTime? FollowUpDate { get; set; }
    public string? EmployeeComments { get; set; }
}

public class CreateCheckInDto : CreateDtoBase
{
    [Required]
    public Guid AppraisalCycleId { get; set; }

    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    public Guid ConductedById { get; set; }

    public CheckInType CheckInType { get; set; }

    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public DateTime ScheduledDate { get; set; }

    [MaxLength(2000)]
    public string? Agenda { get; set; }
}

public class UpdateCheckInDto : UpdateDtoBase
{
    [Required]
    public Guid AppraisalCycleId { get; set; }

    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    public Guid ConductedById { get; set; }

    public CheckInType CheckInType { get; set; }

    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public DateTime ScheduledDate { get; set; }

    public DateTime? ConductedDate { get; set; }

    [MaxLength(2000)]
    public string? Agenda { get; set; }

    [MaxLength(4000)]
    public string? SharedNotes { get; set; }

    [MaxLength(4000)]
    public string? PrivateNotes { get; set; }

    [MaxLength(2000)]
    public string? ActionItems { get; set; }

    public DateTime? FollowUpDate { get; set; }

    [MaxLength(2000)]
    public string? EmployeeComments { get; set; }
}

// ============================================================
// CheckInGoalUpdate
// ============================================================

public class CheckInGoalUpdateDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid CheckInId { get; set; }
    public Guid EmployeeGoalId { get; set; }
    public string GoalTitle { get; set; } = string.Empty;
    public decimal? UpdatedProgress { get; set; }
    public GoalProgressStatus UpdatedStatus { get; set; }
    public bool FlaggedAtRisk { get; set; }
    public string? Note { get; set; }
}

public class CreateCheckInGoalUpdateDto : CreateDtoBase
{
    [Required]
    public Guid CheckInId { get; set; }

    [Required]
    public Guid EmployeeGoalId { get; set; }

    [Range(0, 100)]
    public decimal? UpdatedProgress { get; set; }

    public GoalProgressStatus UpdatedStatus { get; set; }
    public bool FlaggedAtRisk { get; set; } = false;

    [MaxLength(2000)]
    public string? Note { get; set; }
}

public class UpdateCheckInGoalUpdateDto : UpdateDtoBase
{
    [Required]
    public Guid CheckInId { get; set; }

    [Required]
    public Guid EmployeeGoalId { get; set; }

    [Range(0, 100)]
    public decimal? UpdatedProgress { get; set; }

    public GoalProgressStatus UpdatedStatus { get; set; }
    public bool FlaggedAtRisk { get; set; }

    [MaxLength(2000)]
    public string? Note { get; set; }
}

// ============================================================
// PerformanceJournalEntry
// ============================================================

public class PerformanceJournalEntryDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid AppraisalCycleId { get; set; }
    public string? CycleCode { get; set; }
    public Guid OwnerId { get; set; }
    public string OwnerName { get; set; } = string.Empty;
    public Guid? SubjectEmployeeId { get; set; }
    public string? SubjectEmployeeName { get; set; }
    public Guid? RelatedGoalId { get; set; }
    public string? RelatedGoalTitle { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime EntryDate { get; set; }
    public bool IsPrivate { get; set; }
}

public class CreatePerformanceJournalEntryDto : CreateDtoBase
{
    [Required]
    public Guid AppraisalCycleId { get; set; }

    // OwnerId is deliberately absent: the author is taken from the caller's token. See
    // IPerformanceJournalService.CreateAsync.

    public Guid? SubjectEmployeeId { get; set; }
    public Guid? RelatedGoalId { get; set; }

    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(4000)]
    public string Body { get; set; } = string.Empty;

    public DateTime EntryDate { get; set; }
    public bool IsPrivate { get; set; } = true;
}

public class UpdatePerformanceJournalEntryDto : UpdateDtoBase
{
    [Required]
    public Guid AppraisalCycleId { get; set; }

    public Guid? SubjectEmployeeId { get; set; }
    public Guid? RelatedGoalId { get; set; }

    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(4000)]
    public string Body { get; set; } = string.Empty;

    public DateTime EntryDate { get; set; }
    public bool IsPrivate { get; set; }
}

public class TeamJournalListDto
{
    public Guid     Id                  { get; set; }
    public string   Title               { get; set; } = string.Empty;
    public DateTime EntryDate           { get; set; }
    public Guid     WrittenById         { get; set; }
    public string   WrittenByName       { get; set; } = string.Empty;
    public bool     IsWrittenByManager  { get; set; }
    public Guid     SubjectEmployeeId   { get; set; }
    public string   SubjectEmployeeName { get; set; } = string.Empty;
    public bool     IsPrivate           { get; set; }
    public string?  RelatedGoalTitle    { get; set; }
    public string   PreviewText         { get; set; } = string.Empty;
}

// ============================================================
// EmployeeDevelopmentPlan
// ============================================================

public class EmployeeDevelopmentPlanDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public Guid? AppraisalCycleId { get; set; }
    public string? CycleCode { get; set; }
    public string? CycleName { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public DevelopmentPlanStatus PlanStatus { get; set; }
    public string? OverallNotes { get; set; }

    /// <summary>
    /// The login that wrote the plan (performance closure P10); null for a plan older than the
    /// stamp. The employee may complete, cancel or delete only a plan they wrote themselves, so
    /// the screen compares this with the signed-in user's id.
    /// </summary>
    public Guid? AuthorUserId { get; set; }

    // ── Objective rollup ──────────────────────────────────────────────────────
    // A plan on its own says nothing about how it is going. The reads already load the
    // objectives; these three carry what the list rows and tiles need without a second call.

    public int ObjectiveCount { get; set; }
    public int CompletedObjectiveCount { get; set; }
    /// <summary>Mean progress across the plan's objectives; 0 when it has none.</summary>
    public decimal AverageProgressPercent { get; set; }
}

public class CreateEmployeeDevelopmentPlanDto : CreateDtoBase
{
    [Required]
    public Guid EmployeeId { get; set; }

    public Guid? AppraisalCycleId { get; set; }

    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public DevelopmentPlanStatus PlanStatus { get; set; } = DevelopmentPlanStatus.Active;

    [MaxLength(2000)]
    public string? OverallNotes { get; set; }
}

public class UpdateEmployeeDevelopmentPlanDto : UpdateDtoBase
{
    [Required]
    public Guid EmployeeId { get; set; }

    public Guid? AppraisalCycleId { get; set; }

    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public DevelopmentPlanStatus PlanStatus { get; set; }

    [MaxLength(2000)]
    public string? OverallNotes { get; set; }
}

// ============================================================
// EmployeeDevelopmentObjective
// ============================================================

public class EmployeeDevelopmentObjectiveDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid DevelopmentPlanId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Actions { get; set; }
    public DateOnly? TargetDate { get; set; }
    public decimal ProgressPercent { get; set; }
    public string? ProgressNotes { get; set; }
    public DevelopmentObjectiveStatus ObjectiveStatus { get; set; }
    public Guid? UpdatedInReviewEventId { get; set; }
}

/// <summary>
/// Returned by <see cref="IAppraisalWorkflowService.ManuallyAdvanceStepAsync"/>.
/// Provides a full picture of what changed and which data mutations were performed.
/// </summary>
public class ManualAdvanceResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public AppraisalSubStatus PreviousSubStatus { get; set; }
    public AppraisalSubStatus NewSubStatus { get; set; }
    public AppraisalStatus PreviousMajorStatus { get; set; }
    public AppraisalStatus NewMajorStatus { get; set; }
    public List<string> ActionsPerformed { get; set; } = new();
}

/// <summary>
/// Outcome of the HR "advance overdue appraisals" action for a cycle (AutoLockOnDeadline).
/// </summary>
public class DeadlineEnforcementResult
{
    /// <summary>False when the cycle's AutoLockOnDeadline setting is disabled — nothing was advanced.</summary>
    public bool AutoLockEnabled { get; set; }
    /// <summary>Active appraisals examined in the cycle.</summary>
    public int Evaluated { get; set; }
    /// <summary>Appraisals whose overdue step was advanced.</summary>
    public int Advanced { get; set; }
    /// <summary>Per-appraisal notes (advanced steps and any failures).</summary>
    public List<string> Messages { get; set; } = new();
}

public class CreateEmployeeDevelopmentObjectiveDto : CreateDtoBase
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
    public DevelopmentObjectiveStatus ObjectiveStatus { get; set; } = DevelopmentObjectiveStatus.NotStarted;
}

public class UpdateEmployeeDevelopmentObjectiveDto : UpdateDtoBase
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

    [Range(0, 100)]
    public decimal ProgressPercent { get; set; }

    [MaxLength(2000)]
    public string? ProgressNotes { get; set; }

    public DevelopmentObjectiveStatus ObjectiveStatus { get; set; }
    public Guid? UpdatedInReviewEventId { get; set; }
}

// ============================================================
// AppraisalReviewEvent
// ============================================================

public class AppraisalReviewEventDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid AppraisalCycleId { get; set; }
    public string? CycleCode { get; set; }
    public Guid PerformanceAppraisalId { get; set; }
    public string? AppraisalNumber { get; set; }
    /// <summary>The appraisee. A manager's team list is unreadable without a name on each row.</summary>
    public Guid EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public ReviewEventType Type { get; set; }
    public DateOnly EventDate { get; set; }
    public AppraisalReviewStatus Status { get; set; }
    public bool IsLightTouch { get; set; }
    public bool IsFullAppraisal { get; set; }
    public decimal? OverallPeriodScore { get; set; }
    public string? AchievementsSummary { get; set; }
    public string? ChallengesSummary { get; set; }
    public string? Notes { get; set; }
    public string? ManagerNotes { get; set; }
    public Guid? ConversationId { get; set; }
    public Guid? UpdatedDevelopmentPlanId { get; set; }
}

public class CreateAppraisalReviewEventDto : CreateDtoBase
{
    [Required]
    public Guid AppraisalCycleId { get; set; }

    [Required]
    public Guid PerformanceAppraisalId { get; set; }

    public ReviewEventType Type { get; set; }

    [Required]
    public DateOnly EventDate { get; set; }

    public bool IsLightTouch { get; set; } = true;
    public bool IsFullAppraisal { get; set; } = false;
}

public class UpdateAppraisalReviewEventDto : UpdateDtoBase
{
    // The event's cycle, appraisal, status and period score are not editable here — an event
    // cannot be re-pointed at another employee's appraisal, and status/score belong to
    // submit / complete / finalize. See UpdateEntity.

    public ReviewEventType Type { get; set; }

    [Required]
    public DateOnly EventDate { get; set; }

    public bool IsLightTouch { get; set; }
    public bool IsFullAppraisal { get; set; }

    [MaxLength(2000)]
    public string? AchievementsSummary { get; set; }

    [MaxLength(2000)]
    public string? ChallengesSummary { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [MaxLength(2000)]
    public string? ManagerNotes { get; set; }

    public Guid? ConversationId { get; set; }
    public Guid? UpdatedDevelopmentPlanId { get; set; }
}

// ============================================================
// AppraisalConversation
// ============================================================

public class AppraisalConversationDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid AppraisalId { get; set; }
    public string? AppraisalNumber { get; set; }
    public Guid? ScheduledById { get; set; }
    public string? ScheduledByName { get; set; }
    public Guid? ConductedById { get; set; }
    public string? ConductedByName { get; set; }
    public ConversationType Type { get; set; }
    public DateTime? ScheduledDate { get; set; }
    public DateTime? HeldDate { get; set; }
    public string? Agenda { get; set; }
    public string? PostMeetingNotes { get; set; }
    public string? KeyTakeaways { get; set; }
    public bool IsCompleted { get; set; }
    public Guid? ReviewEventId { get; set; }
}

public class CreateAppraisalConversationDto : CreateDtoBase
{
    [Required]
    public Guid AppraisalId { get; set; }

    public Guid? ReviewEventId { get; set; }
    public Guid? ScheduledById { get; set; }
    public Guid? ConductedById { get; set; }

    public ConversationType Type { get; set; } = ConversationType.KickOff;

    public DateTime? ScheduledDate { get; set; }

    [MaxLength(2000)]
    public string? Agenda { get; set; }
}

public class UpdateAppraisalConversationDto : UpdateDtoBase
{
    [Required]
    public Guid AppraisalId { get; set; }

    public Guid? ScheduledById { get; set; }
    public Guid? ConductedById { get; set; }

    public ConversationType Type { get; set; }

    public DateTime? ScheduledDate { get; set; }
    public DateTime? HeldDate { get; set; }

    [MaxLength(2000)]
    public string? Agenda { get; set; }

    [MaxLength(4000)]
    public string? PostMeetingNotes { get; set; }

    [MaxLength(2000)]
    public string? KeyTakeaways { get; set; }

    public bool IsCompleted { get; set; }
    public Guid? ReviewEventId { get; set; }
}

// ============================================================
// AppraisalHRReview (entity CRUD DTO — distinct from the workflow HRReviewDto)
// ============================================================

public class AppraisalHRReviewDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid AppraisalId { get; set; }
    public string? AppraisalNumber { get; set; }
    public Guid ReviewedByHRId { get; set; }
    public string ReviewedByHRName { get; set; } = string.Empty;
    public DateTime ReviewStartedDate { get; set; }
    public DateTime? ReviewCompletedDate { get; set; }
    public bool IsApproved { get; set; }
    public string? HRNotes { get; set; }
}

public class CreateAppraisalHRReviewDto : CreateDtoBase
{
    [Required]
    public Guid AppraisalId { get; set; }

    [Required]
    public Guid ReviewedByHRId { get; set; }

    public DateTime ReviewStartedDate { get; set; } = DateTime.UtcNow;
}

public class UpdateAppraisalHRReviewDto : UpdateDtoBase
{
    [Required]
    public Guid AppraisalId { get; set; }

    [Required]
    public Guid ReviewedByHRId { get; set; }

    public DateTime ReviewStartedDate { get; set; }
    public DateTime? ReviewCompletedDate { get; set; }
    public bool IsApproved { get; set; }

    [MaxLength(2000)]
    public string? HRNotes { get; set; }
}

// ============================================================
// CalibrationSession
// ============================================================

public class CalibrationSessionDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid AppraisalCycleId { get; set; }
    public string? CycleCode { get; set; }
    public string SessionName { get; set; } = string.Empty;
    public Guid? OrganizationLevelId { get; set; }
    public string? OrganizationLevelName { get; set; }
    public Guid? OrganizationUnitId { get; set; }
    public string? OrganizationUnitName { get; set; }
    public CalibrationStatus Status { get; set; }
    public DateTime? ScheduledDate { get; set; }
    public DateTime? StartedDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public Guid? FacilitatedById { get; set; }
    public string? FacilitatedByName { get; set; }
    public Guid? CompletedById { get; set; }
    public string? CompletedByName { get; set; }
    public string? Agenda { get; set; }
    public string? MeetingNotes { get; set; }
}

public class CreateCalibrationSessionDto : CreateDtoBase
{
    [Required]
    public Guid AppraisalCycleId { get; set; }

    [Required]
    [MaxLength(200)]
    public string SessionName { get; set; } = string.Empty;

    public Guid? OrganizationLevelId { get; set; }
    public Guid? OrganizationUnitId { get; set; }

    public DateTime? ScheduledDate { get; set; }
    public Guid? FacilitatedById { get; set; }

    [MaxLength(2000)]
    public string? Agenda { get; set; }
}

/// <summary>
/// Edits the session's own particulars. Deliberately carries no lifecycle fields: status and the
/// started/completed stamps belong to the open/start/complete endpoints, which enforce the order
/// and record who acted. Accepting them here let a caller mark a session Completed — and so lift
/// the calibration gate on every appraisal in it — with a plain PUT.
/// </summary>
public class UpdateCalibrationSessionDto : UpdateDtoBase
{
    [Required]
    public Guid AppraisalCycleId { get; set; }

    [Required]
    [MaxLength(200)]
    public string SessionName { get; set; } = string.Empty;

    public Guid? OrganizationLevelId { get; set; }
    public Guid? OrganizationUnitId { get; set; }

    public DateTime? ScheduledDate { get; set; }
    public Guid? FacilitatedById { get; set; }

    [MaxLength(2000)]
    public string? Agenda { get; set; }

    [MaxLength(4000)]
    public string? MeetingNotes { get; set; }
}

/// <summary>Notes recorded when a calibration session is closed.</summary>
public class CompleteCalibrationSessionDto
{
    [MaxLength(4000)]
    public string? MeetingNotes { get; set; }
}

/// <summary>Attendance mark for one participant.</summary>
public class RecordCalibrationAttendanceDto
{
    public bool Attended { get; set; }
}

/// <summary>
/// What committing a session actually did. <c>AppraisalsCalibrated</c> counts every appraisal at
/// the calibration step — an employee the panel discussed and left alone is still calibrated, and
/// would otherwise sit blocked behind the calibration gate forever. An appraisal the session
/// covers that is not at that step (its manager has not submitted, it is under appeal, or it is
/// already final with nothing adjusted) is left untouched and listed in <c>Skipped</c> with the
/// reason (performance closure A4).
/// </summary>
public class CalibrationApplyResultDto
{
    public int AdjustmentsApplied { get; set; }
    public int ScoresChanged { get; set; }
    public int AppraisalsCalibrated { get; set; }
    public int AppraisalsSkipped => Skipped.Count;
    public List<CalibrationSkippedAppraisalDto> Skipped { get; set; } = new();
}

/// <summary>An appraisal in the session's scope that the commit did not calibrate, and why.</summary>
public class CalibrationSkippedAppraisalDto
{
    public Guid AppraisalId { get; set; }
    public string? EmployeeName { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Performance closure A15 (D-13): what the settle path would store for each finalised appraisal,
/// beside what is stored now. Read-only; finalised scores stay as they are unless HR restates one
/// through the audited reopen.
/// </summary>
public class AppraisalSettleDryRunReportDto
{
    public DateTime GeneratedAt { get; set; }
    public Guid? CycleId { get; set; }
    public int Examined { get; set; }
    public int Changed { get; set; }
    public List<AppraisalSettleDryRunRowDto> Rows { get; set; } = new();
}

public class AppraisalSettleDryRunRowDto
{
    public Guid AppraisalId { get; set; }
    public string AppraisalNumber { get; set; } = string.Empty;
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string CycleName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal? StoredScore { get; set; }
    public decimal? SettledScore { get; set; }
    /// <summary>The score from the submitted legs alone, before any calibrated overall is applied.</summary>
    public decimal? ComputedScore { get; set; }
    public decimal? CalibratedOverallScore { get; set; }
    public string? StoredGrade { get; set; }
    public string? SettledGrade { get; set; }
    public string? StoredRating { get; set; }
    public string? SettledRating { get; set; }
    /// <summary>The employee's talent-pool rating today; null when they are in no pool.</summary>
    public string? TalentPoolRatingNow { get; set; }
    public bool Changed { get; set; }
}

// ============================================================
// CalibrationParticipant
// ============================================================

public class CalibrationParticipantDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid CalibrationSessionId { get; set; }
    public string SessionName { get; set; } = string.Empty;
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string Role { get; set; } = "Manager";
    public bool Attended { get; set; }
}

public class CreateCalibrationParticipantDto : CreateDtoBase
{
    [Required]
    public Guid CalibrationSessionId { get; set; }

    [Required]
    public Guid EmployeeId { get; set; }

    [MaxLength(100)]
    public string Role { get; set; } = "Manager";

    public bool Attended { get; set; } = false;
}

public class UpdateCalibrationParticipantDto : UpdateDtoBase
{
    [Required]
    public Guid CalibrationSessionId { get; set; }

    [Required]
    public Guid EmployeeId { get; set; }

    [MaxLength(100)]
    public string Role { get; set; } = "Manager";

    public bool Attended { get; set; }
}

// ============================================================
// CalibrationRatingAdjustment
// ============================================================

public class CalibrationRatingAdjustmentDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid CalibrationSessionId { get; set; }
    public string SessionName { get; set; } = string.Empty;
    public Guid PerformanceAppraisalId { get; set; }
    public string? AppraisalNumber { get; set; }
    public string? EmployeeName { get; set; }
    public Guid? TemplateItemId { get; set; }
    public string? TemplateItemName { get; set; }
    public decimal? OriginalScore { get; set; }
    public decimal? AdjustedScore { get; set; }
    public Guid AdjustedById { get; set; }
    public string AdjustedByName { get; set; } = string.Empty;
    public DateTime AdjustmentDate { get; set; }
    public string? Rationale { get; set; }
}

/// <summary>
/// One panel decision. A null <see cref="TemplateItemId"/> adjusts the overall score directly;
/// a populated one adjusts that criterion on the manager's evaluation and lets the overall score
/// be recomputed from it.
///
/// <para>The adjuster is taken from the caller's token, never the payload — this is a signed
/// audit trail of who moved someone's rating.</para>
/// </summary>
/// <summary>
/// One frozen criterion of an appraisal, as a calibration panel needs to see it: what it is worth,
/// what the manager scored, and whatever the panel has already moved it to.
/// </summary>
/// <remarks>
/// The criterion snapshot (<c>PerformanceAppraisalCriterionConfig</c>) was previously reachable
/// only inside a manager's or HR's own evaluation context, which a panellist is not entitled to —
/// so the calibration dialog could adjust the overall score and nothing finer. This is the light
/// read that makes per-criterion adjustment possible.
/// </remarks>
public class CalibrationCriterionDto
{
    public Guid TemplateItemId { get; set; }
    public string? TemplateItemName { get; set; }
    public int WeightUsed { get; set; }

    /// <summary>
    /// A KPI: an adjustment restates its achievement percentage (D-22) rather than giving it a
    /// rated score.
    /// </summary>
    public bool IsKpi { get; set; }

    /// <summary>The highest adjustment this row accepts: the item's top grade band, or 100 for a KPI (A11).</summary>
    public decimal ScaleTop { get; set; }

    public decimal? KpiTargetValue { get; set; }

    /// <summary>The manager's score for this criterion — what the panel is moving away from.</summary>
    public decimal? ManagerScore { get; set; }
    public decimal? ManagerActualValue { get; set; }

    /// <summary>For a KPI, the achievement the manager's actual produced — what the score used.</summary>
    public decimal? ManagerAchievementPercent { get; set; }

    /// <summary>Set when this panel has already adjusted this criterion.</summary>
    public Guid? AdjustmentId { get; set; }
    public decimal? AdjustedScore { get; set; }
    public string? Rationale { get; set; }
}

public class CreateCalibrationRatingAdjustmentDto : CreateDtoBase
{
    [Required]
    public Guid PerformanceAppraisalId { get; set; }

    public Guid? TemplateItemId { get; set; }

    [Range(0, 100)]
    public decimal? OriginalScore { get; set; }

    [Range(0, 100)]
    public decimal? AdjustedScore { get; set; }

    [MaxLength(2000)]
    public string? Rationale { get; set; }
}

public class UpdateCalibrationRatingAdjustmentDto : UpdateDtoBase
{
    [Required]
    public Guid PerformanceAppraisalId { get; set; }

    public Guid? TemplateItemId { get; set; }

    [Range(0, 100)]
    public decimal? OriginalScore { get; set; }

    [Range(0, 100)]
    public decimal? AdjustedScore { get; set; }

    [MaxLength(2000)]
    public string? Rationale { get; set; }
}

// ============================================================
// PipGoal
// ============================================================

public class PipGoalDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid PipId { get; set; }
    public string PipNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? SuccessCriteria { get; set; }
    public DateOnly DueDate { get; set; }
    public GoalProgressStatus Status { get; set; }
    public decimal? ProgressPercent { get; set; }
    public string? ProgressNotes { get; set; }
}

public class CreatePipGoalDto : CreateDtoBase
{
    [Required]
    public Guid PipId { get; set; }

    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(1000)]
    public string? SuccessCriteria { get; set; }

    [Required]
    public DateOnly DueDate { get; set; }

    public GoalProgressStatus Status { get; set; } = GoalProgressStatus.NotStarted;
}

public class UpdatePipGoalDto : UpdateDtoBase
{
    [Required]
    public Guid PipId { get; set; }

    [Required]
    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(1000)]
    public string? SuccessCriteria { get; set; }

    [Required]
    public DateOnly DueDate { get; set; }

    public GoalProgressStatus Status { get; set; }

    [Range(0, 100)]
    public decimal? ProgressPercent { get; set; }

    [MaxLength(2000)]
    public string? ProgressNotes { get; set; }
}
public class FinalKpiScoreDto
{
    public Guid EmployeeKpiTargetId { get; set; }
    public string KpiName { get; set; } = "";
    public string KpiDescription { get; set; } = "";
    public decimal TargetValue { get; set; }
    public decimal? FinalActualValue { get; set; }
    public decimal FinalAchievementPercent { get; set; }
    public decimal FinalWeightedScore { get; set; }
    public int Weight { get; set; }
    public string MeasurementUnit { get; set; } = "";
    public string ManagerComments { get; set; } = "";
    public bool WasAppealed { get; set; }
}

// ============================================================
// AppraisalEvaluationSnapshot (read-only — immutable audit snapshot)
// ============================================================

public class AppraisalEvaluationSnapshotDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid AppraisalId { get; set; }
    public Guid EvaluatorId { get; set; }
    public string EvaluatorName { get; set; } = string.Empty;
    public EvaluatorRole EvaluatorRole { get; set; }
    public decimal? TotalScore { get; set; }
    public DateTime SnapshotDate { get; set; }
    public string SnapshotReason { get; set; } = "Appeal Remand";
}

// ============================================================
// AppraisalCriterionScoreSnapshot (read-only — immutable audit snapshot)
// ============================================================

public class AppraisalCriterionScoreSnapshotDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid AppraisalEvaluationSnapshotId { get; set; }
    public Guid? TemplateItemId { get; set; }
    public string? ItemName { get; set; }
    public int? NumericScore { get; set; }
    public decimal WeightedScore { get; set; }
    public string? Notes { get; set; }
    public decimal? KpiTargetValue { get; set; }
    public decimal? KpiMinValue { get; set; }
    public decimal? KpiMaxValue { get; set; }
    public KpiTargetSource? KpiTargetSource { get; set; }
}

// ============================================================
// AppraisalKpiEvaluationSnapshot (read-only — immutable audit snapshot)
// ============================================================

public class AppraisalKpiEvaluationSnapshotDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid AppraisalCriterionScoreSnapshotId { get; set; }
    public decimal? ActualValue { get; set; }
    public decimal AchievementPercent { get; set; }
    public string? Notes { get; set; }
    public string? EvidenceLinks { get; set; }
    public DateTime SnapshotDate { get; set; }
}

// ============================================================
// CompanyGoalCascadeStatsDto (aggregated cascade stats)
// ============================================================

public class CompanyGoalCascadeStatsDto
{
    public Guid CompanyGoalId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int UnitGoalsCount { get; set; }
    public int EmployeeGoalsCount { get; set; }
    public int TotalGoalsCount => UnitGoalsCount + EmployeeGoalsCount;
    public decimal? AverageEmployeeProgress { get; set; }
}

// ============================================================
// CompanyGoalListItemDto  — projection DTO for the strategy
// dashboard list view.  Never includes full navigation collections.
// ============================================================

public class CompanyGoalListItemDto
{
    public Guid Id { get; set; }
    public Guid AppraisalCycleId { get; set; }
    public string? CycleCode { get; set; }
    public string Title { get; set; } = string.Empty;
    /// <summary>First 200 chars of Description — safe for card preview.</summary>
    public string? DescriptionPreview { get; set; }
    public string? SuccessCriteria { get; set; }
    public GoalPriority Priority { get; set; }
    public decimal? TargetValue { get; set; }
    public string? Unit { get; set; }
    public DateOnly? DueDate { get; set; }
    public bool IsVisible { get; set; }
    public int UnitGoalCount { get; set; }
    public int EmployeeGoalCount { get; set; }
}

// ============================================================
// CompanyGoalDashboardMetricsDto  — aggregated metrics for the
// strategy dashboard header cards (one query per cycle load).
// ============================================================

public class CompanyGoalDashboardMetricsDto
{
    public Guid CycleId { get; set; }
    public int TotalGoals { get; set; }
    public int TotalUnitGoalsCascaded { get; set; }
    public int TotalEmployeeGoalsAligned { get; set; }
    public int VisibleGoalsCount { get; set; }
    public int VisibleGoalPercent =>
        TotalGoals == 0 ? 0 : (int)Math.Round((double)VisibleGoalsCount / TotalGoals * 100);
}

// ============================================================
// EmployeeGoalSummaryDto (per-employee goal progress snapshot)
// ============================================================

public class EmployeeGoalSummaryDto
{
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;
    public Guid CycleId { get; set; }
    public string CycleName { get; set; } = string.Empty;
    public int TotalGoals { get; set; }
    public int DraftGoals { get; set; }
    public int PendingApprovalGoals { get; set; }
    public int ApprovedGoals { get; set; }
    public int InProgressGoals { get; set; }
    public int CompletedGoals { get; set; }
    public int AtRiskGoals { get; set; }
    public decimal OverallProgressPercent { get; set; }
    public bool GoalSettingComplete { get; set; }
    public bool MeetsMinGoalCount { get; set; }
}

// ============================================================
// TeamGoalSummaryDto (manager view - one row per direct report)
// ============================================================

public class TeamGoalSummaryDto
{
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;
    public string? PositionName { get; set; }
    public string? DepartmentName { get; set; }
    public int TotalGoals { get; set; }
    public int ApprovedGoals { get; set; }
    public int PendingApprovalGoals { get; set; }
    public int AtRiskGoals { get; set; }
    public decimal OverallProgressPercent { get; set; }
    public bool HasOverdueGoals { get; set; }
    public DateOnly? EarliestOverdueDueDate { get; set; }
}

// ============================================================
// CalibrationMatrixDto (calibration session grid view)
// ============================================================

/// <summary>
/// The calibration grid: every appraisal in the session's scope — the cycle, narrowed to the
/// session's organization unit (and its descendants) or level — not only the ones already
/// adjusted. A panel has to see who it has *not* moved.
/// </summary>
public class CalibrationMatrixDto
{
    public Guid SessionId { get; set; }
    public string SessionName { get; set; } = string.Empty;
    public CalibrationStatus SessionStatus { get; set; }
    public string? OrganizationUnitName { get; set; }
    public string? OrganizationLevelName { get; set; }
    public int TotalEmployees { get; set; }
    /// <summary>How many rows carry at least one recorded adjustment.</summary>
    public int AdjustedCount { get; set; }
    /// <summary>How many have already been committed through the calibration gate.</summary>
    public int CalibratedCount { get; set; }
    /// <summary>Mean of the scores as they currently stand, for spotting a skewed panel.</summary>
    public decimal? AverageScore { get; set; }
    public List<CalibrationMatrixRowDto> Rows { get; set; } = new();
}

public class CalibrationMatrixRowDto
{
    public Guid AppraisalId { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;
    public string? PositionName { get; set; }
    public string? DepartmentName { get; set; }
    public AppraisalStatus AppraisalStatus { get; set; }
    /// <summary>The manager's own evaluation total, before any calibration.</summary>
    public decimal? ManagerProposedScore { get; set; }
    public decimal? PreCalibrationScore { get; set; }
    public decimal? CalibratedScore { get; set; }
    public decimal? ScoreAdjustment { get; set; }
    public string? AdjustmentRationale { get; set; }
    public bool IsCalibrated { get; set; }
    public string? ManagerName { get; set; }
    public List<CalibrationRatingAdjustmentDto> Adjustments { get; set; } = new();
}

// ============================================================
// Coverage Preview
// ============================================================

/// <summary>Result returned by POST /api/AppraisalCycle/{id}/generate-appraisals.</summary>
public class GenerateAppraisalsResultDto
{
    public int AppraisalsCreated { get; set; }
    public int EvaluationsCreated { get; set; }
    public AppraisalCycleDto? Cycle { get; set; }
}

// ============================================================

/// <summary>
/// Top-level result returned by GET /api/appraisal-cycles/{cycleId}/coverage-preview.
/// Simulates appraisal generation without writing any records.
/// </summary>
public class CoveragePreviewDto
{
    public Guid CycleId { get; set; }
    public string CycleName { get; set; } = string.Empty;

    // === Aggregate counts ===
    public int TotalTargetedEmployees { get; set; }
    public int EmployeesWithTemplate { get; set; }
    public int EmployeesWithoutTemplate { get; set; }
    public int ConflictCount { get; set; }
    public int ExcludedCount { get; set; }

    /// <summary>Covered / Total * 100, rounded to 1 decimal place.</summary>
    public decimal CoveragePercentage { get; set; }

    /// <summary>True only when EmployeesWithoutTemplate == 0 and ConflictCount == 0.</summary>
    public bool IsGenerationSafe { get; set; }

    /// <summary>False if no active template assignments exist for the cycle.</summary>
    public bool HasActiveTemplates { get; set; }

    /// <summary>False if no active target groups exist for the cycle.</summary>
    public bool HasActiveTargets { get; set; }

    // === Paging metadata ===
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }

    public List<EmployeeCoverageItemDto> Items { get; set; } = new();
    public List<TemplateCoverageBreakdownDto> TemplateBreakdown { get; set; } = new();

    /// <summary>
    /// Other cycles of the same type and year whose scope overlaps this one's.
    ///
    /// Advisory only — opening is refused solely by the Open / InProgress entries, because a
    /// Draft cycle appraises nobody and may never be opened. Draft entries are reported here
    /// so the clash is visible while there is still time to re-scope, rather than surfacing
    /// as a refusal at the moment someone tries to open.
    /// </summary>
    public List<CycleScopeOverlapDto> ScopeOverlaps { get; set; } = new();
}

/// <summary>One other cycle competing for some of the same employees.</summary>
public class CycleScopeOverlapDto
{
    public Guid CycleId { get; set; }
    public string CycleCode { get; set; } = string.Empty;
    public string CycleName { get; set; } = string.Empty;
    public AppraisalCycleStatus Status { get; set; }
    public int SharedEmployeeCount { get; set; }
    /// <summary>True when this overlap would refuse an attempt to open the cycle.</summary>
    public bool BlocksOpening { get; set; }
}

/// <summary>
/// Per-employee simulation row in CoveragePreviewDto.
/// </summary>
public class EmployeeCoverageItemDto
{
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;
    public string? PositionTitle { get; set; }
    public string? UnitName { get; set; }

    /// <summary>Template resolved for this employee, if any.</summary>
    public Guid? ResolvedTemplateId { get; set; }
    public string? ResolvedTemplateName { get; set; }

    /// <summary>Position / OrgUnit / OrgLevel / Global</summary>
    public string? SourceScope { get; set; }

    public int? TemplatePriority { get; set; }

    public EmployeeCoverageStatus Status { get; set; }

    /// <summary>Populated only when Status == Conflict.</summary>
    public List<string> ConflictingTemplateNames { get; set; } = new();

    /// <summary>Populated only when Status == Excluded.</summary>
    public string? ExclusionReason { get; set; }
}

/// <summary>
/// Grouped breakdown of how many employees each template will service.
/// </summary>
public class TemplateCoverageBreakdownDto
{
    public Guid TemplateId { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public int Priority { get; set; }
    /// <summary>Position / OrgUnit / OrgLevel / Global</summary>
    public string? ScopeType { get; set; }
    public string? ScopeName { get; set; }
    public int AssignedEmployeeCount { get; set; }
}
// ============================================================
// EmployeeDevelopmentPlanFeedback
// ============================================================

public class EmployeeDevelopmentPlanFeedbackDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid DevelopmentPlanId { get; set; }
    public Guid ManagerId { get; set; }
    public FeedbackType FeedbackType { get; set; }
    public string Comment { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}

public class CreateEmployeeDevelopmentPlanFeedbackDto
{
    [Required]
    public Guid DevelopmentPlanId { get; set; }

    [Required]
    public Guid ManagerId { get; set; }

    public FeedbackType FeedbackType { get; set; } = FeedbackType.GeneralComment;

    [Required]
    [MaxLength(3000)]
    public string Comment { get; set; } = string.Empty;
}

// ── Appraisal Notification DTOs ───────────────────────────────────────────────

public class AppraisalNotificationDto
{
    public Guid NotificationId { get; set; }
    public Guid RecipientEmployeeId { get; set; }
    public AppraisalNotificationType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? SubjectEmployeeName { get; set; }
    public string? CycleName { get; set; }
    public string? NavigationUrl { get; set; }
    public Guid? AppraisalId { get; set; }
    public DateTime CreatedDate { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadDate { get; set; }
    public string TimeAgo { get; set; } = string.Empty;
    public NotificationUrgency Urgency { get; set; }
}

public class AppraisalNotificationSummaryDto
{
    public int UnreadCount { get; set; }
    public List<AppraisalNotificationDto> RecentNotifications { get; set; } = new();
}