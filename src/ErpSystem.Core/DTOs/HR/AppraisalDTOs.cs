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

    /// <summary>
    /// A score on this criterion needs an evidence link before an evaluation can be submitted — the
    /// employee's, the manager's or a peer's (performance closure B2). The entity had it; no DTO
    /// carried it, so nothing could set it.
    /// </summary>
    public bool RequireEvidence { get; set; }

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

    /// <summary>See <see cref="AppraisalCompetencyDto.RequireEvidence"/>.</summary>
    public bool RequireEvidence { get; set; }

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

    /// <summary>See <see cref="AppraisalCompetencyDto.RequireEvidence"/>.</summary>
    public bool RequireEvidence { get; set; }

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

    // Withdrawal (D-10, performance closure E-d1) — set with Status = Withdrawn. A withdrawn
    // appraisal's scores and grade are left off every reader's copy: they are not a result.
    public string? WithdrawnReason { get; set; }
    public DateTime? WithdrawnDate { get; set; }
    /// <summary>The employee who withdrew it; null when no person did.</summary>
    public Guid? WithdrawnById { get; set; }
    public string? WithdrawnByName { get; set; }
}

/// <summary>Withdrawing an appraisal from its cycle (D-10): the reason is required, and kept.</summary>
public class WithdrawAppraisalDto
{
    [Required]
    [MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;
}

// CreatePerformanceAppraisalDto went with the raw create in performance closure E-d2b (D-20): an appraisal is
// generated, on an Open cycle.

/// <summary>
/// HR's correction of a generated appraisal's window — its year and dates — and nothing else
/// (performance closure E-a). It carried the whole record: the employee, the cycle, the status, the
/// score, the ranks, the manager's narrative and recommendations and the peer count, and the HR
/// review's <i>Correct dates</i> sends none of the manager's fields, so every correction blanked them.
/// Anything else in a body is ignored.
/// </summary>
public class UpdatePerformanceAppraisalDto : UpdateDtoBase
{
    [Required]
    [Range(2000, 2100)]
    public int Year { get; set; }

    [Required]
    public DateOnly StartDate { get; set; }

    [Required]
    public DateOnly EndDate { get; set; }
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

// The legacy appeal DTOs — CreateAppraisalAppealDto, UpdateAppraisalAppealDto, their item DTOs,
// ResolveAppraisalAppealDto and ResolveAppealItemDto — went with the legacy pair (performance
// closure C1). An appeal is filed with SubmitAppealDto and decided with ResolveAppealDto.

public class AppraisalAppealItemDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid AppraisalAppealId { get; set; }
    public Guid? TemplateItemId { get; set; }
    /// <summary>The appraisal's snapshot row appealed; the only key a goal row has (lane L3).</summary>
    public Guid? CriterionConfigId { get; set; }
    public string? TemplateItemName { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? ResolutionNotes { get; set; }
    public bool? ScoreAdjusted { get; set; }
    public decimal? OriginalScore { get; set; }
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
    /// <summary>The overall the appeal was filed against.</summary>
    public decimal? OriginalScore { get; set; }
    /// <summary>The new overall when the decision moved it; null when it did not, or before the decision (A5).</summary>
    public decimal? AdjustedScore { get; set; }
    public bool HasScoreAdjustment => OriginalScore.HasValue && AdjustedScore.HasValue && OriginalScore != AdjustedScore;

    /// <summary>
    /// The overall now — null while it is withheld: during a remand the manager's re-evaluation is
    /// provisional until HR decides (the release rule, P2 and C3).
    /// </summary>
    public decimal? CurrentOverallScore { get; set; }

    /// <summary>
    /// The appraisal's outcome is released to the employee. False while a remand is open: the items'
    /// scores now (<see cref="AppealedItemViewDto.CurrentScore"/>) are withheld until HR decides.
    /// </summary>
    public bool OutcomeReleased { get; set; }

    /// <summary>
    /// The appealed items carry the manager's scores. False when the profile shows the employee only
    /// the overall (<c>ShowScoreBreakdownToEmployee</c> off — performance closure B2).
    /// </summary>
    public bool ScoreBreakdownShown { get; set; } = true;

    // Appealed items
    public List<AppealedItemViewDto> AppealedItems { get; set; } = new();
}

/// <summary>
/// One appealed item, as the appellant follows it (performance closure C6): what it is, what it
/// scored when the appeal was filed, and what it scores now.
/// </summary>
public class AppealedItemViewDto
{
    public Guid ItemId { get; set; }
    /// <summary>The criterion's key: the template item for a template row, the snapshot row for a goal row.</summary>
    public Guid CriterionKey { get; set; }
    public Guid? TemplateItemId { get; set; }
    public Guid? CriterionConfigId { get; set; }
    /// <summary><c>Competency</c>, <c>KPI</c>, <c>Goal</c> or <c>Question</c>. Every template item read "Competency".</summary>
    public string ItemType { get; set; } = string.Empty;
    /// <summary>Measured against a target, or rated on the row's own scale.</summary>
    public CriterionScoringMethod ScoringMethod { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string? SectionName { get; set; }
    /// <summary>The row's weight within its section, as the forms show it.</summary>
    public int? Weight { get; set; }
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// What the manager scored the item when the appeal was filed (D-38): a rated row's score on its
    /// scale, a measured row's achievement %. Null when the profile hides the breakdown, or — on an
    /// appeal filed before it was kept, with no remand to recall it — not known.
    /// </summary>
    public decimal? OriginalScore { get; set; }

    /// <summary>What it scores now, on the same terms. Null while withheld (a remand) or hidden (the breakdown).</summary>
    public decimal? CurrentScore { get; set; }

    /// <summary>After the decision: whether the appeal moved the item's score. Null before it, or when either side is not known.</summary>
    public bool? ScoreChanged { get; set; }

    /// <summary>A measured row's target; null on a rated row.</summary>
    public decimal? TargetValue { get; set; }
    public string? Unit { get; set; }
    /// <summary>A measured row's actual now, beside <see cref="CurrentScore"/>; withheld and hidden with it.</summary>
    public decimal? ActualValue { get; set; }
    /// <summary>The measured row's achievement was restated by calibration or an appeal rather than read from its actual (D-22).</summary>
    public bool AchievementOverridden { get; set; }
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
    /// <summary>Stored (decision D-73): Scheduled until recorded as held or cancelled.</summary>
    public PipMeetingStatus Status { get; set; }
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

    /// <summary>
    /// The tenant's default profile — at most one (performance closure B6, P-2). Moved only by
    /// <c>POST …/{id}/make-default</c>; the create and update bodies do not carry it.
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Appraisals read this profile's rules (performance closure E-e, D-67): it is in use once any appraisal sits on
    /// a cycle using it — finished ones too, which still read its visibility, anonymity and outcome settings. An
    /// in-use profile's rules are changed on a clone; its name, deadline-risk bands, workload threshold and default
    /// HR reviewer stay editable.
    /// </summary>
    public bool IsInUse { get; set; }
    public int InUseAppraisalCount { get; set; }
    public List<string> InUseCycleNames { get; set; } = new();

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

/// <summary>
/// Copies a profile under a new name (performance closure E-e, D-46/D-67): the door to changing the rules of one in
/// use. The copy is not the default; a cycle takes it when created, or once it is made the default.
/// </summary>
public class CloneAppraisalSettingsDto
{
    [Required]
    [MaxLength(100)]
    public string SettingsName { get; set; } = string.Empty;
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

    // Status is deliberately absent (performance closure E-c): a cycle is created as a Draft. The body's
    // status was stored as sent, so a cycle created Open was never overlap-checked, carried no opened
    // date and stayed deletable.

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
    /// <summary>HR's own planning figure, as typed.</summary>
    public int EstimatedEmployeeCount { get; set; }
    /// <summary>How many of the target's staff the cycle appraises, resolved live; 0 for an inactive target.</summary>
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

/// <summary>
/// A target's edit. No cycle (performance closure E-c): a target stays in its cycle — the body's cycle id
/// was copied, so an edit moved a target into any cycle, even another tenant's, and it vanished from its
/// own.
/// </summary>
public class UpdateAppraisalCycleTargetDto : UpdateDtoBase
{
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

    /// <summary>
    /// Pending, or the nomination is refused (422, performance closure D1): it was stored as sent, so
    /// a raw POST made an Approved nomination with no peer evaluation behind it. Approval is the
    /// manager's (or, in Manager mode, the nomination itself).
    /// </summary>
    public PeerNominationStatus NominationStatus { get; set; } = PeerNominationStatus.Pending;
}

/// <summary>
/// What may change on a pending nomination (performance closure D1): its due date and its
/// instructions. It carried the appraisal, the peer, the nominator, the invitation date and the
/// status, all copied as sent.
/// </summary>
public class UpdatePeerNominationDto : UpdateDtoBase
{
    public DateTime? DueDate { get; set; }

    [MaxLength(500)]
    public string? InstructionsToPeer { get; set; }
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
    /// <summary>Every nomination, rejected ones included.</summary>
    public int TotalNominations { get; set; }
    /// <summary>Pending and approved — what the minimum and the maximum count (D2: a rejected one leaves room for a replacement).</summary>
    public int ActiveNominations { get; set; }
    public int PendingCount { get; set; }
    public int ApprovedCount { get; set; }
    public int RejectedCount { get; set; }
    public int MinRequired { get; set; }
    public int MaxAllowed { get; set; }
    /// <summary>The active nominations are within the cycle's range.</summary>
    public bool CanSubmit { get; set; }
    /// <summary>Nominations may be made or decided: in Employee mode while Draft or Active, in Manager mode until completed or closed.</summary>
    public bool CanEdit { get; set; }
    public PeerNominationMode NominationMode { get; set; }

    /// <summary>
    /// The list is withheld from this reader (D-40): the appraisee, in Manager mode with anonymous peer
    /// reviews — the manager chose the peers, so the appraisee is told the counts only.
    /// </summary>
    public bool PeersWithheld { get; set; }
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
    /// <summary>The scope: active staff the active targets reach, less the excluded (E-d1; it was the appraisal count).</summary>
    public int TotalEmployeesTargeted { get; set; }
    public int TotalEmployeesExcluded { get; set; }
    /// <summary>The cycle's appraisals in play — every progress denominator (performance closure E-d1).</summary>
    public int TotalAppraisals { get; set; }
    /// <summary>Appraisals withdrawn from the cycle, which no count above includes (E-d1).</summary>
    public int TotalWithdrawn { get; set; }
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
    /// <summary>The AppraisalTemplateItem.Id — the scored line item. Null on a goal row (lane L3).</summary>
    public Guid? TemplateItemId { get; set; }

    /// <summary>The PerformanceAppraisalCriterionConfig.Id for this appraisal (the frozen snapshot row).</summary>
    public Guid CriterionConfigId { get; set; }

    /// <summary>
    /// The criterion's key — the template item for a template row, the snapshot row for a goal row.
    /// What a form keys its rows by, and what a save may send back as the item's id.
    /// </summary>
    public Guid CriterionKey { get; set; }

    /// <summary>Measured against a target (the input is an actual value) or rated on the grade bands (a score).</summary>
    public CriterionScoringMethod ScoringMethod { get; set; }

    /// <summary>The goal a goal row scores; null on a template row.</summary>
    public Guid? EmployeeGoalId { get; set; }

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
    /// <summary>Null on a goal row (lane L3).</summary>
    public Guid? TemplateItemId { get; set; }
    public Guid? CriterionConfigId { get; set; }
    /// <summary>The criterion's key: the template item for a template row, the snapshot row for a goal row.</summary>
    public Guid CriterionKey { get; set; }
    /// <summary>Measured (an actual against a target) or rated — a goal row has no KPI id to tell by.</summary>
    public CriterionScoringMethod ScoringMethod { get; set; }
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
    /// <summary>A fixed section, or the employee's goals (lane L).</summary>
    public AppraisalSectionKind Kind { get; set; } = AppraisalSectionKind.Fixed;
    public int SectionWeight { get; set; }
    public int DisplayOrder { get; set; }
    public List<SubmittedEvaluationItemDto> Items { get; set; } = new();
}

/// <summary>
/// Unified input DTO for saving a single scored item, covering both KPI and competency types.
/// Names its criterion by <see cref="TemplateItemId"/> (a template row's template item — or any
/// row's criterion key) or by <see cref="CriterionConfigId"/> (the snapshot row); a goal row has no
/// template item (lane L3). An input naming none of the appraisal's criteria is refused.
/// </summary>
public class EvaluationItemInputDto
{
    public Guid? TemplateItemId { get; set; }

    public Guid? CriterionConfigId { get; set; }

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
    /// <summary>A fixed section, or the employee's goals (lane L).</summary>
    public AppraisalSectionKind Kind { get; set; } = AppraisalSectionKind.Fixed;
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
    /// <summary>A fixed section, or the employee's goals (lane L).</summary>
    public AppraisalSectionKind Kind { get; set; } = AppraisalSectionKind.Fixed;
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
    /// <summary>A fixed section, or the employee's goals (lane L).</summary>
    public AppraisalSectionKind Kind { get; set; } = AppraisalSectionKind.Fixed;
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
    /// The self-evaluation is submitted but its entries are not this reader's to see yet — the line
    /// manager, while the profile hides self scores until they have submitted their own evaluation
    /// (performance closure B2). The entries on the items are then empty.
    /// </summary>
    public bool SelfEntriesWithheld { get; set; }

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

    /// <summary>
    /// The appealed criteria's keys — the template item for a template row, the snapshot row for a
    /// goal row (lane L) — KPI rows among them. Named for the template item it held before goal rows
    /// existed. (<c>AppealedKpiIds</c>, always empty since employee KPI targets went, was removed in C6.)
    /// </summary>
    public List<Guid> AppealedTemplateItemIds { get; set; } = new();
    
    /// <summary>
    /// Manager evaluation state
    /// </summary>
    public Guid? ManagerEvaluatorEvaluationId { get; set; }
    public bool IsManagerEvaluationSubmitted { get; set; }
    public DateTime? ManagerEvaluationSubmittedDate { get; set; }
    public bool IsEditable { get; set; }

    /// <summary>
    /// The employee has submitted their self-evaluation. Until they have, no self score is on this
    /// form — a draft is theirs alone (P12, B2).
    /// </summary>
    public bool SelfEvaluationSubmitted { get; set; }

    /// <summary>
    /// The employee's self-evaluation is submitted, but the profile shows self scores to the manager
    /// only after they have submitted their own evaluation (<c>ShowSelfScoreToManager</c> off —
    /// performance closure B2). The <c>EmployeeSelf*</c> fields on every item are then empty. A
    /// self-evaluation that is still a draft is never on this form.
    /// </summary>
    public bool SelfScoresWithheld { get; set; }
    
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

    /// <summary>
    /// The entries are withheld from this reader — the line manager, while the profile hides self
    /// scores until they have submitted their own evaluation (performance closure B2). The sections
    /// keep their items, without the employee's scores, actuals, notes or evidence.
    /// </summary>
    public bool EntriesWithheld { get; set; }

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
    /// <summary>
    /// The nomination's due date, else the cycle's peer deadline (performance closure D5): it was
    /// always the cycle's, though the approval told the peer the nomination's.
    /// </summary>
    public DateOnly? DueDate { get; set; }
    public decimal EvaluatorWeight { get; set; }
    /// <summary>What the nominator asked this peer to comment on — collected on the nomination, and shown nowhere.</summary>
    public string? InstructionsToPeer { get; set; }
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
    /// <summary>The nomination's due date, else the cycle's peer deadline (D5).</summary>
    public DateOnly? DueDate { get; set; }
    /// <summary>What the nominator asked this peer to comment on.</summary>
    public string? InstructionsToPeer { get; set; }

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

    /// <summary>
    /// The appraisee's copy carries the score breakdown — each evaluator's criteria and totals.
    /// False before the outcome is released, and after it when the profile shows the employee only
    /// the overall, the grade and the narrative (<c>ShowScoreBreakdownToEmployee</c> off — B2).
    /// Always true for HR and the manager.
    /// </summary>
    public bool ScoreBreakdownShown { get; set; } = true;

    /// <summary>
    /// The self-evaluation is withheld from this reader: the line manager before they have submitted
    /// their own evaluation when the profile hides self scores (B2), or anyone but the employee while
    /// it is still a draft (P12).
    /// </summary>
    public bool SelfScoresWithheld { get; set; }

    /// <summary>
    /// The peer scores are withheld from the line manager until they have submitted their own
    /// evaluation (<c>ShowPeerScoresToManager</c> off — B2).
    /// </summary>
    public bool PeerScoresWithheld { get; set; }

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

    // Withdrawal (performance closure E-d1): set once the appraisal is taken out of its cycle.
    public string? WithdrawnReason { get; set; }
    public DateTime? WithdrawnDate { get; set; }
    public string? WithdrawnByName { get; set; }

    /// <summary>
    /// HR may withdraw it: Draft, Active, or Governance before it is final (D-52). The page's
    /// Withdraw button reads it; the server decides again on the write.
    /// </summary>
    public bool CanWithdraw { get; set; }

    /// <summary>
    /// Its form has no rows and nobody has scored it, so HR may rebuild the form from its template (performance closure
    /// E-g2, D-86). The page's *Rebuild form* button reads it; the server decides again on the write.
    /// </summary>
    public bool CanRebuildForm { get; set; }
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
    /// <summary>One of the employee's goals rather than a template item (lane L3).</summary>
    public bool IsGoal { get; set; }
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
    /// <summary>One of the employee's goals rather than a template item (lane L3).</summary>
    public bool IsGoal { get; set; }
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
    /// <summary>One of the employee's goals rather than a template item (lane L3).</summary>
    public bool IsGoal { get; set; }
    public decimal? AverageScore { get; set; }
    public int ResponseCount { get; set; }
}

/// <summary>
/// Aggregated peer KPI evaluations
/// </summary>
public class PeerKpiScoreSummaryDto
{
    public string KpiName { get; set; } = string.Empty;
    /// <summary>One of the employee's goals rather than a template item (lane L3).</summary>
    public bool IsGoal { get; set; }
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

    /// <summary>
    /// The peers' scores and comments are withheld from the line manager until they have submitted
    /// their own evaluation (<c>ShowPeerScoresToManager</c> off — performance closure B2). Who the
    /// peers are and whether each has submitted are still listed.
    /// </summary>
    public bool ScoresWithheld { get; set; }

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

    /// <summary>
    /// Every criterion the peer scored — competency, and KPI or goal rows where the cycle lets peers
    /// score them — in the forms' order (performance closure lane D). It listed competencies only, so
    /// a peer's KPI or goal score never reached the manager; its KPI list was always empty.
    /// </summary>
    public List<PeerCriterionScoreDto> CriterionScores { get; set; } = new();
}

/// <summary>One criterion a peer scored, named, weighted and scored as the appeal reads describe a row (C6).</summary>
public class PeerCriterionScoreDto
{
    public Guid CriterionScoreId { get; set; }
    /// <summary>The criterion's key: the template item for a template row, the snapshot row for a goal row.</summary>
    public Guid CriterionKey { get; set; }
    public Guid? TemplateItemId { get; set; }
    public Guid? CriterionConfigId { get; set; }
    /// <summary><c>Competency</c>, <c>KPI</c>, <c>Goal</c> or <c>Question</c>.</summary>
    public string ItemType { get; set; } = string.Empty;
    public CriterionScoringMethod ScoringMethod { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? SectionName { get; set; }
    /// <summary>The row's weight within its section, from the snapshot (it was 0).</summary>
    public int Weight { get; set; }
    /// <summary>A rated row's score on its scale, a measured row's achievement %.</summary>
    public decimal? Score { get; set; }
    /// <summary>A measured row's actual, and its target; null on a rated row.</summary>
    public decimal? ActualValue { get; set; }
    public decimal? TargetValue { get; set; }
    public string? Unit { get; set; }
    public decimal WeightedScore { get; set; }
    public string? Comments { get; set; }
}
/// <summary>
/// DTO for employee to acknowledge their appraisal
/// </summary>
public class AcknowledgeAppraisalDto
{
    /// <summary>Ignored: the acknowledging employee is the caller.</summary>
    public Guid EmployeeId { get; set; }

    /// <summary>The employee's comment on acknowledging, kept on the appraisal (performance closure E-a).</summary>
    [MaxLength(2000)]
    public string? Comments { get; set; }
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

    /// <summary>
    /// Each item carries the manager's score. False when the profile shows the employee only the
    /// overall, the grade and the narrative (<c>ShowScoreBreakdownToEmployee</c> off — performance
    /// closure B2): the items are listed to appeal, without their scores. Before the outcome is
    /// released nothing is listed and there is no score to show.
    /// </summary>
    public bool ScoreBreakdownShown { get; set; } = true;

    /// <summary>
    /// Every criterion the manager's submitted evaluation scored — competency, KPI and goal rows, in
    /// the forms' order — the list the submit accepts (C8). Performance closure C6: it offered
    /// competencies only, and a separate KPI list that was always empty.
    /// </summary>
    public List<AppealableCriterionDto> AppealableCriteria { get; set; } = new();
}

/// <summary>
/// One criterion an employee may appeal (performance closure C6). Sent back on the appeal by
/// <see cref="CriterionConfigId"/> — and <see cref="TemplateItemId"/> too on a template row, as the
/// forms send their rows.
/// </summary>
public class AppealableCriterionDto
{
    /// <summary>The criterion's key: the template item for a template row, the snapshot row for a goal row.</summary>
    public Guid CriterionKey { get; set; }
    /// <summary>Null on a goal row.</summary>
    public Guid? TemplateItemId { get; set; }
    public Guid? CriterionConfigId { get; set; }
    /// <summary><c>Competency</c>, <c>KPI</c>, <c>Goal</c> or <c>Question</c>.</summary>
    public string ItemType { get; set; } = string.Empty;
    public CriterionScoringMethod ScoringMethod { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? SectionName { get; set; }
    /// <summary>The section's weight on the form, as scored (A0).</summary>
    public int? SectionWeight { get; set; }
    /// <summary>The row's weight within its section, as scored (A12).</summary>
    public int Weight { get; set; }
    /// <summary>The top of the row's own scale: its highest grade band, or 100 for a measured row's achievement %.</summary>
    public decimal ScaleTop { get; set; }
    /// <summary>A measured row's target; null on a rated row.</summary>
    public decimal? TargetValue { get; set; }
    public string? Unit { get; set; }

    // The manager's score — withheld when the profile shows the employee only the overall (B2).
    /// <summary>A rated row's score on its scale, a measured row's achievement %.</summary>
    public decimal? Score { get; set; }
    /// <summary>A measured row's actual.</summary>
    public decimal? ActualValue { get; set; }
    /// <summary>The measured row's achievement was restated by calibration or an appeal rather than read from its actual (D-22).</summary>
    public bool AchievementOverridden { get; set; }
    /// <summary>What the row contributes to the manager's evaluation: its achievement × its share of the whole form.</summary>
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

    /// <summary>
    /// The snapshot row appealed. A goal row has no template item and is named only by this
    /// (lane L3); a template row may be named by either, or both — which must then name one row.
    /// </summary>
    public Guid? CriterionConfigId { get; set; }

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
    
    // Appraisal scores — what this reader may see of each leg (AppraisalVisibility, B2): the desk
    // reads a self-evaluation only once it is submitted, and every submitted peer.
    public decimal? SelfEvaluationScore { get; set; }
    /// <summary>The employee submitted a self-evaluation. A draft — one HR waived — is not read here.</summary>
    public bool SelfEvaluationSubmitted { get; set; }
    public decimal? PeerEvaluationScore { get; set; }
    public decimal? ManagerEvaluationScore { get; set; }
    /// <summary>The overall now. Null only for an appellant reading their own appeal while a remand withholds it.</summary>
    public decimal? OverallScore { get; set; }

    // Settings that control HR actions
    public bool HRCanModifyScores { get; set; }
    public Guid AppraisalSettingsId { get; set; }
    public string AppraisalSettingsName { get; set; } = string.Empty;

    /// <summary>
    /// Why the reader may not act on this appeal — they are its appellant, wrote the contested
    /// evaluation, or are the appellant's line manager (D-35) — or null. The page offers no action then.
    /// </summary>
    public string? PartyToAppealReason { get; set; }

    // The decision, once made (D-37: a decided appeal opens read-only; the read refused it). A remand
    // records its reasoning here too.
    public string? ReviewedByName { get; set; }
    public DateTime? ResolvedDate { get; set; }
    public string? ResolutionNotes { get; set; }
    /// <summary>The overall the appeal was filed against.</summary>
    public decimal? OriginalOverallScore { get; set; }
    /// <summary>The new overall when the decision moved it; null when it did not (A5).</summary>
    public decimal? AdjustedScore { get; set; }

    // Appealed items with full evaluation details
    public List<AppealedCriterionReviewDto> AppealedCriteria { get; set; } = new();
}

/// <summary>
/// One appealed criterion on HR's review — competency, KPI or goal row (performance closure C6, C9):
/// named and weighted from the snapshot, with every leg's score on the row's own terms.
/// </summary>
public class AppealedCriterionReviewDto
{
    public Guid AppealItemId { get; set; }
    /// <summary>The template item appealed; null on a goal row (lane L3).</summary>
    public Guid? TemplateItemId { get; set; }
    public Guid? CriterionConfigId { get; set; }
    /// <summary>The criterion's key: the template item for a template row, the snapshot row for a goal row.</summary>
    public Guid CriterionKey { get; set; }
    /// <summary><c>Competency</c>, <c>KPI</c>, <c>Goal</c> or <c>Question</c>.</summary>
    public string ItemType { get; set; } = string.Empty;
    public CriterionScoringMethod ScoringMethod { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string ItemDescription { get; set; } = string.Empty;
    public string? SectionName { get; set; }
    /// <summary>The row's weight within its section, from the snapshot (C9: it was 0 on every row).</summary>
    public decimal Weight { get; set; }
    /// <summary>The top of a new score on this row: its highest grade band, or 100 — a measured row's new score is an achievement % (D-22).</summary>
    public decimal ScaleTop { get; set; }
    /// <summary>A measured row's target; null on a rated row.</summary>
    public decimal? TargetValue { get; set; }
    public string? Unit { get; set; }
    public string AppealReason { get; set; } = string.Empty;

    /// <summary>What the manager scored it when the appeal was filed (D-38); null when not known.</summary>
    public decimal? ScoreWhenAppealed { get; set; }

    // Each leg's score: a rated row's score on its scale, a measured row's achievement % — with the
    // actual behind it. A self draft is not read (B2).
    public decimal? SelfScore { get; set; }
    public decimal? SelfActualValue { get; set; }
    /// <summary>The submitted peers' average, on the same terms (C9: it was never set).</summary>
    public decimal? PeerAverageScore { get; set; }
    public decimal? ManagerScore { get; set; }
    public decimal? ManagerActualValue { get; set; }
    /// <summary>The manager's measured score was restated by calibration or an appeal rather than read from the actual (D-22).</summary>
    public bool AchievementOverridden { get; set; }

    // Weighted scores: the row's achievement × its share of the whole form
    public decimal? SelfWeightedScore { get; set; }
    /// <summary>What the row contributes to the manager's evaluation — the contribution under appeal.</summary>
    public decimal? ManagerWeightedScore { get; set; }

    // Supporting evidence
    public string? ManagerComments { get; set; }
    public string? SelfComments { get; set; }
}

/// <summary>
/// A score HR restates on an upheld appeal — on one of the criteria the appeal contests (C-b). A
/// measured row's new score is an achievement % (D-22), not a new actual.
/// </summary>
public class CriterionScoreModificationDto
{
    /// <summary>The template item restated. A goal row has none, and is named by <see cref="CriterionConfigId"/>.</summary>
    public Guid? TemplateItemId { get; set; }
    public Guid? CriterionConfigId { get; set; }
    public int NewScore { get; set; }

    [Required]
    [MaxLength(1000)]
    public string Justification { get; set; } = string.Empty;
}

/// <summary>
/// Appeal resolution submission from HR
/// </summary>
public class ResolveAppealDto
{
    /// <summary>
    /// Upheld, Rejected or Remanded (C4). Nullable so a body without one is refused — [Required] on
    /// a non-nullable enum is a no-op, and a missing decision arrived as 0.
    /// </summary>
    [Required]
    public AppraisalAppealStatus? ResolutionDecision { get; set; }
    
    [Required]
    [MaxLength(4000)]
    public string ResolutionNotes { get; set; } = string.Empty;
    
    // Optional score modifications (only with Upheld, only if HRCanModifyScores = true, only on a
    // contested criterion). A KPI or goal row is restated here too, by its achievement % (D-22).
    public List<CriterionScoreModificationDto>? CriteriaModifications { get; set; }
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
    /// <summary>The re-evaluation deadline while the manager owes it; null once they have re-evaluated.</summary>
    public DateTime? AppealRemandDeadline { get; set; }
    public DateTime? ManagerReevaluationDate { get; set; }

    // Where the remand stands (performance closure C3, D-34)
    /// <summary>The manager has not re-evaluated yet; the comparison waits for it.</summary>
    public bool AwaitingReevaluation { get; set; }
    /// <summary>The deadline has passed without a re-evaluation.</summary>
    public bool DeadlinePassed { get; set; }
    /// <summary>HR can make the final decision: the manager has re-evaluated, or the deadline passed without it.</summary>
    public bool CanDecide { get; set; }
    /// <summary>HR can move the deadline: the manager has not re-evaluated.</summary>
    public bool CanExtend { get; set; }

    // Appeal summary
    public string OverallAppealReason { get; set; } = string.Empty;
    public string HRRemandJustification { get; set; } = string.Empty;

    // Score comparisons — every criterion the manager scored, KPI and goal rows among them (C6)
    public List<CriterionScoreComparisonDto> CriteriaComparisons { get; set; } = new();

    // Overall score comparison: the overall the appeal was filed against, and the overall now
    public decimal PreRemandOverallScore { get; set; }
    public decimal PostRemandOverallScore { get; set; }
    // The manager's total before the remand, and after the re-evaluation
    public decimal? PreRemandManagerScore { get; set; }
    public decimal? PostRemandManagerScore { get; set; }
    
    // Settings
    public bool HRCanModifyScores { get; set; }
}

/// <summary>
/// One criterion before the remand and after the re-evaluation — competency, KPI or goal row
/// (performance closure C6). A score is a rated row's score on its scale or a measured row's
/// achievement %, with the actual behind it: a measured row compared <c>NumericScore</c>, which it
/// holds only when restated, so a KPI whose actual moved read "— → —" and unchanged.
/// </summary>
public class CriterionScoreComparisonDto
{
    /// <summary>Null on a goal row (lane L3).</summary>
    public Guid? TemplateItemId { get; set; }
    public Guid? CriterionConfigId { get; set; }
    /// <summary>The criterion's key: the template item for a template row, the snapshot row for a goal row.</summary>
    public Guid CriterionKey { get; set; }
    /// <summary><c>Competency</c>, <c>KPI</c>, <c>Goal</c> or <c>Question</c>.</summary>
    public string ItemType { get; set; } = string.Empty;
    public CriterionScoringMethod ScoringMethod { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string ItemDescription { get; set; } = string.Empty;
    public string? SectionName { get; set; }
    public decimal Weight { get; set; }
    /// <summary>A measured row's target; null on a rated row.</summary>
    public decimal? TargetValue { get; set; }
    public string? Unit { get; set; }
    public bool WasAppealed { get; set; }
    public string? AppealReason { get; set; }

    // Pre-remand (from snapshot)
    public decimal? PreRemandScore { get; set; }
    public decimal? PreRemandActualValue { get; set; }
    public decimal? PreRemandWeightedScore { get; set; }
    public string? PreRemandComments { get; set; }

    // Post-remand (current manager evaluation)
    public decimal? PostRemandScore { get; set; }
    public decimal? PostRemandActualValue { get; set; }
    public decimal? PostRemandWeightedScore { get; set; }
    public string? PostRemandComments { get; set; }

    // Change indicators — the score or, on a measured row, the actual behind it
    public bool ScoreChanged => PreRemandScore != PostRemandScore || PreRemandActualValue != PostRemandActualValue;
    public decimal? ScoreDifference => PostRemandScore.HasValue && PreRemandScore.HasValue
        ? PostRemandScore.Value - PreRemandScore.Value
        : null;
}

/// <summary>
/// HR final decision submission for post-remand appeal
/// </summary>
public class PostRemandFinalDecisionDto
{
    /// <summary>Upheld or Rejected. Nullable so a body without one is refused (see ResolveAppealDto).</summary>
    [Required]
    public AppraisalAppealStatus? FinalDecision { get; set; }

    [Required]
    [MaxLength(4000)]
    public string HRFinalNotes { get; set; } = string.Empty;
}

/// <summary>
/// HR moves a remand's re-evaluation deadline (performance closure D-34) — while the manager has
/// not re-evaluated, to a later day. The manager is told, with the reason.
/// </summary>
public class ExtendRemandDeadlineDto
{
    /// <summary>The new last day for the re-evaluation; the deadline is the end of that day (UTC).</summary>
    [Required]
    public DateOnly? NewDeadline { get; set; }

    [Required]
    [MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;
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
    /// <summary>The contested criteria, each as "&lt;kind&gt;: &lt;name&gt;" — "KPI: …", "Competency: …", "Goal: …" (a KPI read "Criterion: …").</summary>
    public List<string> AppealedItems { get; set; } = new();

    // HR Final Decision
    public string HRFinalNotes { get; set; } = "";
    /// <summary>
    /// The outcome in words, saying whether the appeal moved a score (C-b). An upheld appeal said the
    /// scores "were adjusted" whether or not anything had moved.
    /// </summary>
    public string OutcomeMessage { get; set; } = "";

    // Final Scores
    public decimal FinalOverallScore { get; set; }
    /// <summary>The overall score the appeal was filed against; null on appeals filed before it was kept.</summary>
    public decimal? OriginalOverallScore { get; set; }

    /// <summary>
    /// <see cref="FinalCriteriaScores"/> is filled. False when the profile shows the employee only
    /// the overall, the grade and the narrative (<c>ShowScoreBreakdownToEmployee</c> off —
    /// performance closure B2); the list is then empty.
    /// </summary>
    public bool ScoreBreakdownShown { get; set; } = true;
    /// <summary>Every criterion the manager scored — competency, KPI and goal rows, in the forms' order.</summary>
    public List<FinalCriterionScoreDto> FinalCriteriaScores { get; set; } = new();

    // Change Indicators
    /// <summary>The appeal moved the overall, or a score it contested (D-38).</summary>
    public bool ScoresChangedAfterAppeal { get; set; }
}

/// <summary>One criterion on the appeal's outcome — competency, KPI or goal row (performance closure C6).</summary>
public class FinalCriterionScoreDto
{
    /// <summary>Null on a goal row (lane L3).</summary>
    public Guid? TemplateItemId { get; set; }
    public Guid? CriterionConfigId { get; set; }
    /// <summary>The criterion's key: the template item for a template row, the snapshot row for a goal row.</summary>
    public Guid CriterionKey { get; set; }
    /// <summary><c>Competency</c>, <c>KPI</c>, <c>Goal</c> or <c>Question</c>.</summary>
    public string ItemType { get; set; } = "";
    public CriterionScoringMethod ScoringMethod { get; set; }
    public string ItemName { get; set; } = "";
    public string ItemDescription { get; set; } = "";
    public string? SectionName { get; set; }
    /// <summary>
    /// A rated row's score on its scale, a measured row's achievement %. A measured row read its
    /// <c>NumericScore</c>, so a KPI scored by its actual read "—".
    /// </summary>
    public decimal? FinalScore { get; set; }
    /// <summary>A measured row's actual, and its target; null on a rated row.</summary>
    public decimal? FinalActualValue { get; set; }
    public decimal? TargetValue { get; set; }
    public string? Unit { get; set; }
    public decimal FinalWeightedScore { get; set; }
    public int Weight { get; set; }
    public string ManagerComments { get; set; } = "";
    public bool WasAppealed { get; set; }
    /// <summary>On a contested row: what it scored when the appeal was filed (D-38); null when not known.</summary>
    public decimal? ScoreWhenAppealed { get; set; }
    /// <summary>On a contested row: whether the appeal moved it. Null on a row not contested, or when not known.</summary>
    public bool? ChangedOnAppeal { get; set; }
    /// <summary>A measured row whose achievement calibration or an appeal restated to <see cref="FinalScore"/> percent (D-22, A14).</summary>
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

    /// <summary>
    /// The template's structure is frozen (performance closure E-e, D-66): appraisals are scored on it, or it is
    /// assigned to an open cycle. Its name and description stay editable; a structural change is made on a copy.
    /// </summary>
    public bool IsLocked { get; set; }

    /// <summary>Why it is locked, e.g. "107 appraisals are scored on it, and it is assigned to the open cycle 'X'".</summary>
    public string? LockReason { get; set; }
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

    /// <summary>As <see cref="AppraisalTemplateDto.IsLocked"/>: appraisals are scored on it, or an open cycle has it.</summary>
    public bool IsLocked { get; set; }
    public string? LockReason { get; set; }
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
    /// <summary>Who rejected or dismissed it, and when (performance closure batch 2, D-45).</summary>
    public Guid? DecidedById { get; set; }
    public DateTime? DecidedDate { get; set; }
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
    /// <summary>What fills the section: the template's own items, or each employee's locked goals (lane L).</summary>
    public AppraisalSectionKind Kind { get; set; } = AppraisalSectionKind.Fixed;
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

    /// <summary>Fixed when omitted. One goals section per template, and it takes no items (lane L).</summary>
    public AppraisalSectionKind? Kind { get; set; }
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

    /// <summary>Unchanged when omitted. A section with items cannot become a goals section (lane L).</summary>
    public AppraisalSectionKind? Kind { get; set; }
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

    /// <summary>The cycle's status (P-7): a Closed cycle's links are frozen, an Open cycle's pinned while used.</summary>
    public AppraisalCycleStatus? CycleStatus { get; set; }

    /// <summary>
    /// Appraisals in this cycle are scored on this template (performance closure E-e, D-68), so the link is neither
    /// removed nor changed — removing it was a way round the template's lock.
    /// </summary>
    public bool TemplateInUseInCycle { get; set; }

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

    // No SubmittedToManagerId (performance closure D-76): the manager a goal goes to is the one the
    // submit reads from the employee's HR record. The body's id was saved unchecked, so a goal could
    // be filed in another tenant's employee's approval queue.
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

    // No ProgressPercent (performance closure D-72): a goal's progress is what its progress entries
    // say, with who recorded it and when. The edit wrote it straight onto the goal — no entry, no
    // recorder, no status — and both forms sent back the value they had loaded, so an entry made while
    // the dialog was open was overwritten on save.

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

/// <summary>
/// The transition report (performance closure B8): a cycle's in-flight appraisals whose recorded
/// state runs ahead of where the gates now hold them — an evaluation submitted before a step the
/// gates put before it, typically because the gates arrived (lane B) or a profile changed after the
/// work was done. For HR to waive through the audited advance; nothing is moved automatically.
/// </summary>
public class AppraisalTransitionReportDto
{
    public Guid CycleId { get; set; }
    public string? CycleName { get; set; }
    public DateTime GeneratedAt { get; set; }

    /// <summary>In-flight appraisals examined: not completed, closed, appealed or withdrawn.</summary>
    public int Examined { get; set; }

    public List<AppraisalTransitionRowDto> Rows { get; set; } = new();
}

/// <summary>One appraisal whose records run ahead of the gates.</summary>
public class AppraisalTransitionRowDto
{
    public Guid AppraisalId { get; set; }
    public string? AppraisalNumber { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public AppraisalStatus Status { get; set; }

    /// <summary>Where the gates hold it, and why.</summary>
    public AppraisalSubStatus SubStatus { get; set; }
    public string StepLabel { get; set; } = string.Empty;
    public string? Reason { get; set; }

    /// <summary>What is recorded for steps the gates put after it — "the self-evaluation is submitted", ….</summary>
    public List<string> RecordedAhead { get; set; } = new();

    /// <summary>
    /// HR's audited advance can move it past the step (the four before the manager's evaluation).
    /// Otherwise the step itself has to be completed — the reason says what is missing.
    /// </summary>
    public bool CanWaive { get; set; }
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
    /// <summary>
    /// The appraisee (decision D-74): they read their conversations and write none, so the screens
    /// offer Save and Mark held to everyone else on it.
    /// </summary>
    public Guid? AppraiseeEmployeeId { get; set; }
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

    /// <summary>One of this appraisal's review events (D-76), when it is logged at one.</summary>
    public Guid? ReviewEventId { get; set; }

    // No ScheduledById / ConductedById (decision D-74): the scheduler is who books it and the
    // conductor who marks it held, both from the token.

    /// <summary>
    /// Which conversation this is — required (performance closure B2). It defaulted to KickOff, so
    /// a body that named no type booked a kick-off, which the goal-setting gate then counted.
    /// Nullable so that <c>[Required]</c> can see it missing: on a plain enum it is a no-op.
    /// </summary>
    [Required]
    public ConversationType? Type { get; set; }

    public DateTime? ScheduledDate { get; set; }

    [MaxLength(2000)]
    public string? Agenda { get; set; }
}

/// <remarks>
/// Decision D-74: the meeting's details only. The type (the employee was told which conversation was
/// booked; an edit with none wrote 0 — B2), the appraisal (B2), the scheduler and conductor (the
/// token's), and held with its date (<c>CompleteAsync</c>'s) are not the edit's, so the DTO no longer
/// carries them; a body that sends them is read without them.
/// </remarks>
public class UpdateAppraisalConversationDto : UpdateDtoBase
{
    public DateTime? ScheduledDate { get; set; }

    [MaxLength(2000)]
    public string? Agenda { get; set; }

    [MaxLength(4000)]
    public string? PostMeetingNotes { get; set; }

    [MaxLength(2000)]
    public string? KeyTakeaways { get; set; }

    /// <summary>One of this appraisal's review events (D-76), or none.</summary>
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

/// <summary>
/// A new session. It carries no facilitator: the creator facilitates until someone opens it, and
/// the opener after that (performance closure E-b — the body named one, so a session could be
/// recorded as run by someone else).
/// </summary>
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

    [MaxLength(2000)]
    public string? Agenda { get; set; }
}

/// <summary>
/// Edits the session's own particulars. Deliberately carries no lifecycle fields: status and the
/// started/completed stamps belong to the open/complete/cancel endpoints, which enforce the order
/// and record who acted. Accepting them here let a caller mark a session Completed — and so lift
/// the calibration gate on every appraisal in it — with a plain PUT. The scope — cycle, unit,
/// level — changes only while the session is Pending (E-b), and the facilitator not at all.
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

/// <summary>Why a session that has not completed is called off (performance closure E-b, D-46).</summary>
public class CancelCalibrationSessionDto
{
    [Required]
    [MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;
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
    /// <summary>The snapshot row adjusted; the only key a goal row has (lane L3).</summary>
    public Guid? CriterionConfigId { get; set; }
    /// <summary>A restatement of the overall score rather than of one criterion.</summary>
    public bool IsOverall { get; set; }
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
    /// <summary>Null on a goal row (lane L3).</summary>
    public Guid? TemplateItemId { get; set; }
    public Guid CriterionConfigId { get; set; }
    /// <summary>The criterion's key: the template item for a template row, the snapshot row for a goal row.</summary>
    public Guid CriterionKey { get; set; }
    /// <summary>One of the employee's goals rather than a template item.</summary>
    public bool IsGoal { get; set; }
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

    /// <summary>
    /// The criterion restated: a template item, or — for a goal row, which has none —
    /// <see cref="CriterionConfigId"/>. Neither restates the overall score (lane L3).
    /// </summary>
    public Guid? TemplateItemId { get; set; }

    public Guid? CriterionConfigId { get; set; }

    [Range(0, 100)]
    public decimal? OriginalScore { get; set; }

    [Range(0, 100)]
    public decimal? AdjustedScore { get; set; }

    [MaxLength(2000)]
    public string? Rationale { get; set; }
}

/// <summary>
/// A new score and rationale for a recorded adjustment. The appraisal and the criterion must be
/// the adjustment's own (performance closure E-b): an adjustment stays on what it was recorded
/// against, and a body naming anything else is refused.
/// </summary>
public class UpdateCalibrationRatingAdjustmentDto : UpdateDtoBase
{
    [Required]
    public Guid PerformanceAppraisalId { get; set; }

    /// <summary>
    /// The criterion restated: a template item, or — for a goal row, which has none —
    /// <see cref="CriterionConfigId"/>. Neither restates the overall score (lane L3).
    /// </summary>
    public Guid? TemplateItemId { get; set; }

    public Guid? CriterionConfigId { get; set; }

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

    /// <summary>The goal's starting point (P-55), as the form sends it.</summary>
    [Range(0, 100)]
    public decimal? ProgressPercent { get; set; }

    [MaxLength(2000)]
    public string? ProgressNotes { get; set; }
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
    /// <summary>
    /// HR returned the appraisal to its manager, who has not submitted again: there is no proposal or starting point to
    /// show (performance closure E-g2, D-82 — the grid read the returned evaluation's old total).
    /// </summary>
    public bool ManagerReevaluating { get; set; }
    public decimal? PreCalibrationScore { get; set; }

    /// <summary>
    /// A calibrated appraisal's settled score, unless this session is still proposing another for
    /// it; otherwise this session's proposed overall (P-41, E-b — it was always the proposal).
    /// </summary>
    public decimal? CalibratedScore { get; set; }
    public decimal? ScoreAdjustment { get; set; }
    public string? AdjustmentRationale { get; set; }
    public bool IsCalibrated { get; set; }

    /// <summary>
    /// Why committing this session would leave the row alone — not at the calibration step, final
    /// and unadjusted, calibrated by this session already, its manager's evaluation submitted after
    /// the panel closed — or null when a commit would calibrate it (E-b).
    /// </summary>
    public string? CommitSkipReason { get; set; }
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

    /// <summary>
    /// True only when generation would go through: the cycle is Open (performance closure E-d2b), nobody resolves to no
    /// template or to a tie, and nobody it would create is already covered by another open cycle of the same type and
    /// year (D-60). <see cref="GenerationBlockedBy"/> says why not.
    /// </summary>
    public bool IsGenerationSafe { get; set; }

    /// <summary>Why generation would be refused, in the order it checks; empty when it would go through.</summary>
    public List<string> GenerationBlockedBy { get; set; } = new();

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
    /// Other cycles of the same type and year whose scope overlaps this one's — for an Open one, also whoever
    /// already holds an appraisal there (D-60).
    ///
    /// Advisory only for a Draft entry — opening, and generation, are refused solely by the Open entries, because a
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
    /// <summary>
    /// True when this overlap would refuse an attempt to open the cycle — the other cycle is Open — and so, once this
    /// one is open, generation for the people it shares (D-60).
    /// </summary>
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