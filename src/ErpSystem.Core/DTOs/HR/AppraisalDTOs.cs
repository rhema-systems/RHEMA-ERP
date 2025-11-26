using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.HR;

public class AppraisalGradeDefinitionDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string GradeName { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class CreateAppraisalGradeDefinitionDto : CreateDtoBase
{
    [Required]
    [MaxLength(50)]
    public string GradeName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }
    
    public Guid TenantId { get; set; }
}

public class UpdateAppraisalGradeDefinitionDto : UpdateDtoBase
{
    [Required]
    [MaxLength(50)]
    public string GradeName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }
    
    public Guid TenantId { get; set; }
}

public class KpiDefinitionDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string KpiName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public MeasurementType MeasurementType { get; set; }
    public string? Unit { get; set; }
    public decimal? TolerancePercent { get; set; }
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
    
    public Guid TenantId { get; internal set; }
}

public class AppraisalCriteriaDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string? Code { get; set; }
    public string CriteriaName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public CriteriaType CriteriaType { get; set; }
    public Guid? KpiDefinitionId { get; set; }
    public string? KpiDefinitionName { get; set; }
}

public class CreateAppraisalCriteriaDto : CreateDtoBase
{
    [MaxLength(50)]
    public string? Code { get; set; }

    [Required]
    [MaxLength(200)]
    public string CriteriaName { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    public CriteriaType CriteriaType { get; set; } = CriteriaType.Competency;

    public Guid? KpiDefinitionId { get; set; }
}

public class UpdateAppraisalCriteriaDto : UpdateDtoBase
{
    [MaxLength(50)]
    public string? Code { get; set; }

    [Required]
    [MaxLength(200)]
    public string CriteriaName { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    public CriteriaType CriteriaType { get; set; }

    public Guid? KpiDefinitionId { get; set; }
}

public class PositionCriteriaMappingDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public Guid? PositionId { get; set; }
    public string? PositionTitle { get; set; }
    public Guid CriteriaId { get; set; }
    public string CriteriaName { get; set; } = string.Empty;
    public int Weight { get; set; }
    public decimal? KpiTargetValue { get; set; }
    public decimal? KpiMinValue { get; set; }
    public decimal? KpiMaxValue { get; set; }
}

public class CreatePositionCriteriaMappingDto : CreateDtoBase
{
    public Guid? DepartmentId { get; set; }
    
    public Guid? PositionId { get; set; }

    [Required]
    public Guid CriteriaId { get; set; }

    [Required]
    [Range(0, 100)]
    public int Weight { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? KpiTargetValue { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? KpiMinValue { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? KpiMaxValue { get; set; }
}

public class UpdatePositionCriteriaMappingDto : UpdateDtoBase
{
    public Guid? DepartmentId { get; set; }

    public Guid? PositionId { get; set; }

    [Required]
    public Guid CriteriaId { get; set; }

    [Required]
    [Range(0, 100)]
    public int Weight { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? KpiTargetValue { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? KpiMinValue { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? KpiMaxValue { get; set; }
}

public class MappingGradeRangeDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid PositionCriteriaMappingId { get; set; }
    public Guid GradeDefinitionId { get; set; }
    public string GradeName { get; set; } = string.Empty;
    public int LowScore { get; set; }
    public int HighScore { get; set; }
}

public class CreateMappingGradeRangeDto : CreateDtoBase
{
    [Required]
    public Guid PositionCriteriaMappingId { get; set; }
    
    [Required]
    public Guid GradeDefinitionId { get; set; }

    [Required]
    [Range(0, int.MaxValue)]
    public int LowScore { get; set; }
    
    [Required]
    [Range(0, int.MaxValue)]
    public int HighScore { get; set; }
}

public class UpdateMappingGradeRangeDto : UpdateDtoBase
{
    [Required]
    public Guid PositionCriteriaMappingId { get; set; }

    [Required]
    public Guid GradeDefinitionId { get; set; }

    [Required]
    [Range(0, int.MaxValue)]
    public int LowScore { get; set; }

    [Required]
    [Range(0, int.MaxValue)]
    public int HighScore { get; set; }
}

public class EmployeeKpiTargetDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public Guid KpiDefinitionId { get; set; }
    public string KpiName { get; set; } = string.Empty;
    public Guid? PositionCriteriaMappingId { get; set; }
    public decimal? TargetValue { get; set; }
    public decimal? MinValue { get; set; }
    public decimal? MaxValue { get; set; }
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public int? WeightOverride { get; set; }
}

public class CreateEmployeeKpiTargetDto : CreateDtoBase
{
    [Required]
    public Guid EmployeeId { get; set; }
    
    [Required]
    public Guid KpiDefinitionId { get; set; }

    public Guid? PositionCriteriaMappingId { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? TargetValue { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? MinValue { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? MaxValue { get; set; }

    [Required]
    public DateOnly PeriodStart { get; set; }
    
    [Required]
    public DateOnly PeriodEnd { get; set; }

    [Range(0, 100)]
    public int? WeightOverride { get; set; }
}

public class UpdateEmployeeKpiTargetDto : UpdateDtoBase
{
    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    public Guid KpiDefinitionId { get; set; }

    public Guid? PositionCriteriaMappingId { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? TargetValue { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? MinValue { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? MaxValue { get; set; }

    [Required]
    public DateOnly PeriodStart { get; set; }

    [Required]
    public DateOnly PeriodEnd { get; set; }

    [Range(0, 100)]
    public int? WeightOverride { get; set; }
}

public class PerformanceAppraisalDto : BaseDto
{
    public Guid TenantId { get; set; }
    public string AppraisalNumber { get; set; } = string.Empty;
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? DepartmentName { get; set; }
    public string? PositionTitle { get; set; }
    public int Year { get; set; }
    public AppraisalType AppraisalType { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public AppraisalStatus Status { get; set; }
    public decimal? OverallScore { get; set; }
    public int? RankInPosition { get; set; }
    public string? OverallComments { get; set; }
    public string? StrengthsIdentified { get; set; }
    public string? AreasForImprovement { get; set; }
    public string? TrainingNeeds { get; set; }
    public string? CareerAspirations { get; set; }
    public bool RecommendPromotion { get; set; }
    public bool RecommendIncrement { get; set; }
    public bool RecommendTraining { get; set; }
    public bool RecommendTermination { get; set; }
    public string? RecommendationNotes { get; set; }
    public DateOnly? NextAppraisalDate { get; set; }
    public bool AppealFiled { get; set; }
    public DateTime? AppealDate { get; set; }
    public string? AppealReason { get; set; }
    public string? AppealOutcome { get; set; }
}

public class CreatePerformanceAppraisalDto : CreateDtoBase
{
    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    [Range(2000, 2100)]
    public int Year { get; set; }
    
    [Required]
    public AppraisalType AppraisalType { get; set; }
    
    [Required]
    public DateOnly StartDate { get; set; }
    
    [Required]
    public DateOnly EndDate { get; set; }
}

public class UpdatePerformanceAppraisalDto : UpdateDtoBase
{
    [Required]
    public Guid EmployeeId { get; set; }

    [Required]
    [Range(2000, 2100)]
    public int Year { get; set; }
    
    [Required]
    public AppraisalType AppraisalType { get; set; }
    
    [Required]
    public DateOnly StartDate { get; set; }
    
    [Required]
    public DateOnly EndDate { get; set; }

    public AppraisalStatus Status { get; set; }

    [Range(0, 100)]
    public decimal? OverallScore { get; set; }

    public int? RankInPosition { get; set; }

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

public class FileAppraisalAppealDto
{
    [Required]
    public Guid AppraisalId { get; set; }

    [Required]
    [MaxLength(2000)]
    public string AppealReason { get; set; } = string.Empty;
}

public class ResolveAppraisalAppealDto
{
    [Required]
    public Guid AppraisalId { get; set; }

    [Required]
    [MaxLength(2000)]
    public string AppealOutcome { get; set; } = string.Empty;
}

public class EvaluatorEvaluationDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid AppraisalId { get; set; }
    public Guid EvaluatorId { get; set; }
    public string EvaluatorName { get; set; } = string.Empty;
    public EvaluatorRole EvaluatorRole { get; set; }
    public decimal EvaluatorWeight { get; set; }
    public bool IsAuthoritative { get; set; }
    public DateTime EvaluationDate { get; set; }
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

    public bool IsAuthoritative { get; set; }

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

    public bool IsAuthoritative { get; set; }

    [MaxLength(2000)]
    public string? OverallNotes { get; set; }

    [MaxLength(1000)]
    public string? Recommendation { get; set; }
}

public class CriterionScoreDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid EvaluatorEvaluationId { get; set; }
    public Guid CriteriaId { get; set; }
    public string CriteriaName { get; set; } = string.Empty;
    public int? NumericScore { get; set; }
    public Guid? KpiEvaluationRecordId { get; set; }
    public decimal WeightedScore { get; set; }
    public string? Notes { get; set; }
}

public class CreateCriterionScoreDto : CreateDtoBase
{
    [Required]
    public Guid EvaluatorEvaluationId { get; set; }
    
    [Required]
    public Guid CriteriaId { get; set; }

    [Range(0, int.MaxValue)]
    public int? NumericScore { get; set; }

    public Guid? KpiEvaluationRecordId { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

public class UpdateCriterionScoreDto : UpdateDtoBase
{
    [Required]
    public Guid EvaluatorEvaluationId { get; set; }

    [Required]
    public Guid CriteriaId { get; set; }

    [Range(0, int.MaxValue)]
    public int? NumericScore { get; set; }

    public Guid? KpiEvaluationRecordId { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

public class KpiEvaluationRecordDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid EmployeeKpiTargetId { get; set; }
    public Guid EvaluatorId { get; set; }
    public string EvaluatorName { get; set; } = string.Empty;
    public bool IsSelfEvaluation { get; set; }
    public bool IsFinal { get; set; }
    public decimal? ActualValue { get; set; }
    public decimal AchievementPercent { get; set; }
    public DateTime EvaluationDate { get; set; }
    public string? Notes { get; set; }
    public string? EvidenceLinks { get; set; }
}

public class CreateKpiEvaluationRecordDto : CreateDtoBase
{
    [Required]
    public Guid EmployeeKpiTargetId { get; set; }
    
    [Required]
    public Guid EvaluatorId { get; set; }

    public bool IsSelfEvaluation { get; set; }

    public bool IsFinal { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? ActualValue { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [MaxLength(4000)]
    public string? EvidenceLinks { get; set; }
}

public class UpdateKpiEvaluationRecordDto : UpdateDtoBase
{
    [Required]
    public Guid EmployeeKpiTargetId { get; set; }

    [Required]
    public Guid EvaluatorId { get; set; }

    public bool IsSelfEvaluation { get; set; }

    public bool IsFinal { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? ActualValue { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [MaxLength(4000)]
    public string? EvidenceLinks { get; set; }
}

public class AppraisalEmployeeResponseDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid AppraisalId { get; set; }
    public Guid? CriteriaId { get; set; }
    public string? CriteriaName { get; set; }
    public string? ResponseText { get; set; }
    public DateTime ResponseDate { get; set; }
}

public class CreateAppraisalEmployeeResponseDto : CreateDtoBase
{
    [Required]
    public Guid AppraisalId { get; set; }

    public Guid? CriteriaId { get; set; }

    [Required]
    [MaxLength(2000)]
    public string ResponseText { get; set; } = string.Empty;
}

public class UpdateAppraisalEmployeeResponseDto : UpdateDtoBase
{
    [Required]
    public Guid AppraisalId { get; set; }

    public Guid? CriteriaId { get; set; }

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
}

public class PipReviewMeetingDto : BaseDto
{
    public Guid TenantId { get; set; }
    public Guid PipId { get; set; }
    public DateTime MeetingDate { get; set; }
    public string ProgressNotes { get; set; } = string.Empty;
    public string? IssuesDiscussed { get; set; }
    public string? ActionsAgreed { get; set; }
    public Guid ConductedById { get; set; }
    public string ConductedByName { get; set; } = string.Empty;
}

public class CreatePipReviewMeetingDto : CreateDtoBase
{
    [Required]
    public Guid PipId { get; set; }

    [Required]
    public DateTime MeetingDate { get; set; }

    [Required]
    [MaxLength(2000)]
    public string ProgressNotes { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? IssuesDiscussed { get; set; }

    [MaxLength(2000)]
    public string? ActionsAgreed { get; set; }

    [Required]
    public Guid ConductedById { get; set; }
}

public class UpdatePipReviewMeetingDto : UpdateDtoBase
{
    [Required]
    public Guid PipId { get; set; }

    [Required]
    public DateTime MeetingDate { get; set; }

    [Required]
    [MaxLength(2000)]
    public string ProgressNotes { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? IssuesDiscussed { get; set; }

    [MaxLength(2000)]
    public string? ActionsAgreed { get; set; }

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
