using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.HR.Performance;

/// <summary>
/// Reusable grade descriptor (e.g., Poor, Good, Excellent) without numeric weights.
/// Weights are assigned at the PositionCriteriaMapping level.
/// </summary>
public class AppraisalGradeDefinition : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string GradeName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public virtual List<MappingGradeRange> MappingGradeRanges { get; set; } = new();
}

/// <summary>
/// Defines a KPI with measurement type and tolerances.
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

    // Tolerance for achievement calculation (e.g., +/- 5%)
    [Column(TypeName = "decimal(5,2)")]
    public decimal? TolerancePercent { get; set; }

    public virtual List<AppraisalCriteria> AppraisalCriterias { get; set; } = new();
    
    public virtual List<EmployeeKpiTarget> EmployeeKpiTargets { get; set; } = new();
}

/// <summary>
/// Criteria definition (competency or KPI-based).
/// Weights are NOT stored here - they belong to PositionCriteriaMapping.
/// </summary>
public class AppraisalCriteria : TenantEntity
{
    [MaxLength(50)]
    public string? Code { get; set; }

    [Required]
    [MaxLength(200)]
    public string CriteriaName { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public CriteriaType CriteriaType { get; set; } = CriteriaType.Competency;

    // For KPI criteria, link to KPI definition
    public Guid? KpiDefinitionId { get; set; }

    [ForeignKey(nameof(KpiDefinitionId))]
    public virtual KpiDefinition? KpiDefinition { get; set; }

    public virtual List<PositionCriteriaMapping> PositionCriteriaMappings { get; set; } = new();
}

/// <summary>
/// Maps criteria to positions with weights and grade ranges.
/// This is where weights live, not in Criteria or GradeDefinition.
/// </summary>
public class PositionCriteriaMapping : TenantEntity
{
    // Scope: Department and/or Position
    public Guid? DepartmentId { get; set; }
    
    public Guid? PositionId { get; set; }

    public Guid CriteriaId { get; set; }

    // Weight is stored HERE (0-100 or other scale)
    public int Weight { get; set; }

    // For KPI criteria: target values can be overridden at mapping level
    [Column(TypeName = "decimal(18,2)")]
    public decimal? KpiTargetValue { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? KpiMinValue { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? KpiMaxValue { get; set; }

    [ForeignKey(nameof(DepartmentId))]
    public virtual Department? Department { get; set; }

    [ForeignKey(nameof(PositionId))]
    public virtual EmployeePosition? Position { get; set; }

    [ForeignKey(nameof(CriteriaId))]
    public virtual AppraisalCriteria AppraisalCriteria { get; set; } = null!;

    public virtual List<MappingGradeRange> MappingGradeRanges { get; set; } = new();
}

/// <summary>
/// Connects a grade narrative to numeric ranges for a specific PositionCriteriaMapping.
/// </summary>
public class MappingGradeRange : TenantEntity
{
    public Guid PositionCriteriaMappingId { get; set; }
    
    public Guid GradeDefinitionId { get; set; }

    // Numeric range for this grade in this mapping
    public int LowScore { get; set; }
    
    public int HighScore { get; set; }

    [ForeignKey(nameof(PositionCriteriaMappingId))]
    public virtual PositionCriteriaMapping PositionCriteriaMapping { get; set; } = null!;

    [ForeignKey(nameof(GradeDefinitionId))]
    public virtual AppraisalGradeDefinition GradeDefinition { get; set; } = null!;
}

/// <summary>
/// Per-employee (or per-position) KPI target instance.
/// </summary>
public class EmployeeKpiTarget : TenantEntity
{
    public Guid EmployeeId { get; set; }
    
    public Guid KpiDefinitionId { get; set; }

    // Link to position criteria mapping if applicable
    public Guid? PositionCriteriaMappingId { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? TargetValue { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? MinValue { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? MaxValue { get; set; }

    public DateOnly PeriodStart { get; set; }
    
    public DateOnly PeriodEnd { get; set; }

    // Optional weight override at employee level
    public int? WeightOverride { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    public virtual Employee Employee { get; set; } = null!;

    [ForeignKey(nameof(KpiDefinitionId))]
    public virtual KpiDefinition KpiDefinition { get; set; } = null!;

    [ForeignKey(nameof(PositionCriteriaMappingId))]
    public virtual PositionCriteriaMapping? PositionCriteriaMapping { get; set; }

    public virtual List<KpiEvaluationRecord> KpiEvaluationRecords { get; set; } = new();
}

/// <summary>
/// Performance appraisal for an employee
/// </summary>
public class PerformanceAppraisal : TenantEntity
{
    [MaxLength(50)]
    public string AppraisalNumber { get; set; } = string.Empty;
    
    public Guid EmployeeId { get; set; }

    public int Year { get; set; }
    
    public AppraisalType AppraisalType { get; set; }
    
    public DateOnly StartDate { get; set; }
    
    public DateOnly EndDate { get; set; }

    public AppraisalStatus Status { get; set; } = AppraisalStatus.Open;

    [Column(TypeName = "decimal(5,2)")]
    public decimal? OverallScore { get; set; }

    // Rank among peers in same position
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

    public bool AppealFiled { get; set; }
    
    public DateTime? AppealDate { get; set; }
    
    [MaxLength(2000)]
    public string? AppealReason { get; set; }
    
    [MaxLength(2000)]
    public string? AppealOutcome { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    public Employee Employee { get; set; } = null!;

    public virtual List<EvaluatorEvaluation> EvaluatorEvaluations { get; set; } = new();

    public virtual List<AppraisalEmployeeResponse> EmployeeResponses { get; set; } = new();
    
    public ICollection<AppraisalAttachment> Attachments { get; set; } = new List<AppraisalAttachment>();
}

/// <summary>
/// Stores each evaluator's evaluation with role and recommendation.
/// </summary>
public class EvaluatorEvaluation : TenantEntity
{
    public Guid AppraisalId { get; set; }
    
    public Guid EvaluatorId { get; set; }

    public EvaluatorRole EvaluatorRole { get; set; }

    // Role-based weight (e.g., Manager=0.7, Peer=0.2, Self=0.1)
    [Column(TypeName = "decimal(3,2)")]
    public decimal EvaluatorWeight { get; set; }

    // Is this the authoritative/final evaluation (typically manager)?
    public bool IsAuthoritative { get; set; }

    public DateTime EvaluationDate { get; set; }

    [MaxLength(2000)]
    public string? OverallNotes { get; set; }

    [MaxLength(1000)]
    public string? Recommendation { get; set; }

    [ForeignKey(nameof(AppraisalId))]
    public virtual PerformanceAppraisal Appraisal { get; set; } = null!;

    [ForeignKey(nameof(EvaluatorId))]
    public virtual Employee Evaluator { get; set; } = null!;

    public virtual List<CriterionScore> CriterionScores { get; set; } = new();
}

/// <summary>
/// Stores per-criterion score for an evaluator.
/// For KPI criteria, references KpiEvaluationRecord.
/// </summary>
public class CriterionScore : TenantEntity
{
    public Guid EvaluatorEvaluationId { get; set; }
    
    public Guid CriteriaId { get; set; }

    // For non-KPI: numeric score chosen by evaluator
    public int? NumericScore { get; set; }

    // For KPI: reference to evaluation record
    public Guid? KpiEvaluationRecordId { get; set; }

    // Weighted score (computed)
    [Column(TypeName = "decimal(10,2)")]
    public decimal WeightedScore { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [ForeignKey(nameof(EvaluatorEvaluationId))]
    public virtual EvaluatorEvaluation EvaluatorEvaluation { get; set; } = null!;

    [ForeignKey(nameof(CriteriaId))]
    public virtual AppraisalCriteria AppraisalCriteria { get; set; } = null!;

    [ForeignKey(nameof(KpiEvaluationRecordId))]
    public virtual KpiEvaluationRecord? KpiEvaluationRecord { get; set; }
}

/// <summary>
/// Stores actual measured value from evaluator (self or manager).
/// </summary>
public class KpiEvaluationRecord : TenantEntity
{
    public Guid EmployeeKpiTargetId { get; set; }
    
    public Guid EvaluatorId { get; set; }

    // Is this the employee's self-evaluation or manager evaluation?
    public bool IsSelfEvaluation { get; set; }

    // Manager's evaluation can be marked as final
    public bool IsFinal { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? ActualValue { get; set; }

    // Computed achievement percentage
    [Column(TypeName = "decimal(5,2)")]
    public decimal AchievementPercent { get; set; }

    public DateTime EvaluationDate { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    // Evidence links (comma-separated or JSON)
    [MaxLength(4000)]
    public string? EvidenceLinks { get; set; }

    [ForeignKey(nameof(EmployeeKpiTargetId))]
    public virtual EmployeeKpiTarget EmployeeKpiTarget { get; set; } = null!;

    [ForeignKey(nameof(EvaluatorId))]
    public virtual Employee Evaluator { get; set; } = null!;

    public virtual List<CriterionScore> CriterionScores { get; set; } = new();
}

/// <summary>
/// Employee's response/comment per criteria and overall.
/// </summary>
public class AppraisalEmployeeResponse : TenantEntity
{
    [Required]
    public Guid AppraisalId { get; set; }

    // Null = overall response, otherwise per-criteria
    public Guid? CriteriaId { get; set; }

    [MaxLength(2000)]
    public string? ResponseText { get; set; }

    public DateTime ResponseDate { get; set; }

    [ForeignKey(nameof(AppraisalId))]
    public virtual PerformanceAppraisal Appraisal { get; set; } = null!;

    [ForeignKey(nameof(CriteriaId))]
    public virtual AppraisalCriteria? AppraisalCriteria { get; set; }
}

public class AppraisalAttachment : TenantEntity
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
    
    public DateTime UploadDate { get; set; }

    [ForeignKey(nameof(AppraisalId))]
    public PerformanceAppraisal Appraisal { get; set; } = null!;
}

/// <summary>
/// Performance Improvement Plan
/// </summary>
public class PerformanceImprovementPlan : TenantEntity
{
    [MaxLength(50)]
    public string PipNumber { get; set; } = string.Empty;
    
    [Required]
    public Guid EmployeeId { get; set; }
    
    public Guid? AppraisalId { get; set; }
    
    public DateTime StartDate { get; set; }
    
    public DateTime EndDate { get; set; }

    public PipStatus Status { get; set; } = PipStatus.Active;

    // Issues Identified
    [MaxLength(2000)]
    public string PerformanceIssues { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string ExpectedStandards { get; set; } = string.Empty;

    // Action Plan
    [MaxLength(2000)]
    public string ImprovementActions { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string SupportProvided { get; set; } = string.Empty;
    
    [MaxLength(2000)]
    public string MeasurementCriteria { get; set; } = string.Empty;

    // Monitoring
    public Guid SupervisorId { get; set; }
    
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

    public virtual ICollection<PipReviewMeeting> ReviewMeetings { get; set; } = new List<PipReviewMeeting>();
}

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
