using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Enums;

namespace ErpSystem.Application.HR.Extensions;

public static class AppraisalMappingExtensions
{
    #region AppraisalGradeDefinition

    public static AppraisalGradeDefinitionDto ToDto(this AppraisalGradeDefinition entity)
    {
        return new AppraisalGradeDefinitionDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            GradeName = entity.GradeName,
            Description = entity.Description,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static AppraisalGradeDefinition ToEntity(this CreateAppraisalGradeDefinitionDto dto)
    {
        return new AppraisalGradeDefinition
        {
            GradeName = dto.GradeName,
            Description = dto.Description
        };
    }

    public static void UpdateEntity(this UpdateAppraisalGradeDefinitionDto dto, AppraisalGradeDefinition entity)
    {
        entity.GradeName = dto.GradeName;
        entity.Description = dto.Description;
    }

    public static List<AppraisalGradeDefinitionDto> ToDtoList(this IEnumerable<AppraisalGradeDefinition> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    #endregion

    #region KpiDefinition

    public static KpiDefinitionDto ToDto(this KpiDefinition entity)
    {
        return new KpiDefinitionDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            KpiName = entity.KpiName,
            Description = entity.Description,
            MeasurementType = entity.MeasurementType,
            Unit = entity.Unit,
            TolerancePercent = entity.TolerancePercent,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static KpiDefinition ToEntity(this CreateKpiDefinitionDto dto)
    {
        return new KpiDefinition
        {
            KpiName = dto.KpiName,
            Description = dto.Description,
            MeasurementType = dto.MeasurementType,
            Unit = dto.Unit,
            TolerancePercent = dto.TolerancePercent
        };
    }

    public static void UpdateEntity(this UpdateKpiDefinitionDto dto, KpiDefinition entity)
    {
        entity.KpiName = dto.KpiName;
        entity.Description = dto.Description;
        entity.MeasurementType = dto.MeasurementType;
        entity.Unit = dto.Unit;
        entity.TolerancePercent = dto.TolerancePercent;
    }

    public static List<KpiDefinitionDto> ToDtoList(this IEnumerable<KpiDefinition> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }
    #endregion

    #region AppraisalCriteria

    public static AppraisalCriteriaDto ToDto(this AppraisalCriteria entity)
    {
        return new AppraisalCriteriaDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            Code = entity.Code,
            CriteriaName = entity.CriteriaName,
            Description = entity.Description,
            CriteriaType = entity.CriteriaType,
            KpiDefinitionId = entity.KpiDefinitionId,
            KpiDefinitionName = entity.KpiDefinition?.KpiName,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static AppraisalCriteria ToEntity(this CreateAppraisalCriteriaDto dto)
    {
        return new AppraisalCriteria
        {
            Code = dto.Code,
            CriteriaName = dto.CriteriaName,
            Description = dto.Description,
            CriteriaType = dto.CriteriaType,
            KpiDefinitionId = dto.KpiDefinitionId
        };
    }

    public static void UpdateEntity(this UpdateAppraisalCriteriaDto dto, AppraisalCriteria entity)
    {
        entity.Code = dto.Code;
        entity.CriteriaName = dto.CriteriaName;
        entity.Description = dto.Description;
        entity.CriteriaType = dto.CriteriaType;
        entity.KpiDefinitionId = dto.KpiDefinitionId;
    }

    public static List<AppraisalCriteriaDto> ToDtoList(this IEnumerable<AppraisalCriteria> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    #endregion

    #region PositionCriteriaMapping

    public static PositionCriteriaMappingDto ToDto(this PositionCriteriaMapping entity)
    {
        return new PositionCriteriaMappingDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            DepartmentId = entity.DepartmentId,
            DepartmentName = entity.Department?.Name,
            PositionId = entity.PositionId,
            PositionTitle = entity.Position?.Title,
            CriteriaId = entity.CriteriaId,
            CriteriaName = entity.AppraisalCriteria.CriteriaName,
            Weight = entity.Weight,
            KpiTargetValue = entity.KpiTargetValue,
            KpiMinValue = entity.KpiMinValue,
            KpiMaxValue = entity.KpiMaxValue,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static PositionCriteriaMapping ToEntity(this CreatePositionCriteriaMappingDto dto)
    {
        return new PositionCriteriaMapping
        {
            DepartmentId = dto.DepartmentId,
            PositionId = dto.PositionId,
            CriteriaId = dto.CriteriaId,
            Weight = dto.Weight,
            KpiTargetValue = dto.KpiTargetValue,
            KpiMinValue = dto.KpiMinValue,
            KpiMaxValue = dto.KpiMaxValue
        };
    }

    public static void UpdateEntity(this UpdatePositionCriteriaMappingDto dto, PositionCriteriaMapping entity)
    {
        entity.DepartmentId = dto.DepartmentId;
        entity.PositionId = dto.PositionId;
        entity.CriteriaId = dto.CriteriaId;
        entity.Weight = dto.Weight;
        entity.KpiTargetValue = dto.KpiTargetValue;
        entity.KpiMinValue = dto.KpiMinValue;
        entity.KpiMaxValue = dto.KpiMaxValue;
    }

    public static List<PositionCriteriaMappingDto> ToDtoList(this IEnumerable<PositionCriteriaMapping> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }
    #endregion

    #region MappingGradeRange

    public static MappingGradeRangeDto ToDto(this MappingGradeRange entity)
    {
        return new MappingGradeRangeDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            PositionCriteriaMappingId = entity.PositionCriteriaMappingId,
            GradeDefinitionId = entity.GradeDefinitionId,
            GradeName = entity.GradeDefinition.GradeName,
            LowScore = entity.LowScore,
            HighScore = entity.HighScore,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static MappingGradeRange ToEntity(this CreateMappingGradeRangeDto dto)
    {
        return new MappingGradeRange
        {
            PositionCriteriaMappingId = dto.PositionCriteriaMappingId,
            GradeDefinitionId = dto.GradeDefinitionId,
            LowScore = dto.LowScore,
            HighScore = dto.HighScore
        };
    }

    public static void UpdateEntity(this UpdateMappingGradeRangeDto dto, MappingGradeRange entity)
    {
        entity.PositionCriteriaMappingId = dto.PositionCriteriaMappingId;
        entity.GradeDefinitionId = dto.GradeDefinitionId;
        entity.LowScore = dto.LowScore;
        entity.HighScore = dto.HighScore;
    }

    public static List<MappingGradeRangeDto> ToDtoList(this IEnumerable<MappingGradeRange> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }
    #endregion

    #region EmployeeKpiTarget

    public static EmployeeKpiTargetDto ToDto(this EmployeeKpiTarget entity)
    {
        return new EmployeeKpiTargetDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee.FullName,
            KpiDefinitionId = entity.KpiDefinitionId,
            KpiName = entity.KpiDefinition.KpiName,
            PositionCriteriaMappingId = entity.PositionCriteriaMappingId,
            TargetValue = entity.TargetValue,
            MinValue = entity.MinValue,
            MaxValue = entity.MaxValue,
            PeriodStart = entity.PeriodStart,
            PeriodEnd = entity.PeriodEnd,
            WeightOverride = entity.WeightOverride,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static EmployeeKpiTarget ToEntity(this CreateEmployeeKpiTargetDto dto)
    {
        return new EmployeeKpiTarget
        {
            EmployeeId = dto.EmployeeId,
            KpiDefinitionId = dto.KpiDefinitionId,
            PositionCriteriaMappingId = dto.PositionCriteriaMappingId,
            TargetValue = dto.TargetValue,
            MinValue = dto.MinValue,
            MaxValue = dto.MaxValue,
            PeriodStart = dto.PeriodStart,
            PeriodEnd = dto.PeriodEnd,
            WeightOverride = dto.WeightOverride
        };
    }

    public static void UpdateEntity(this UpdateEmployeeKpiTargetDto dto, EmployeeKpiTarget entity)
    {
        entity.EmployeeId = dto.EmployeeId;
        entity.KpiDefinitionId = dto.KpiDefinitionId;
        entity.PositionCriteriaMappingId = dto.PositionCriteriaMappingId;
        entity.TargetValue = dto.TargetValue;
        entity.MinValue = dto.MinValue;
        entity.MaxValue = dto.MaxValue;
        entity.PeriodStart = dto.PeriodStart;
        entity.PeriodEnd = dto.PeriodEnd;
        entity.WeightOverride = dto.WeightOverride;
    }

    public static List<EmployeeKpiTargetDto> ToDtoList(this IEnumerable<EmployeeKpiTarget> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }
    #endregion

    #region PerformanceAppraisal

    public static PerformanceAppraisalDto ToDto(this PerformanceAppraisal entity)
    {
        return new PerformanceAppraisalDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            AppraisalNumber = entity.AppraisalNumber,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee.FullName,
            EmployeeNumber = entity.Employee.EmployeeNumber,
            DepartmentName = entity.Employee.Department?.Name,
            PositionTitle = entity.Employee.Position?.Title,
            Year = entity.Year,
            AppraisalType = entity.AppraisalType,
            StartDate = entity.StartDate,
            EndDate = entity.EndDate,
            Status = entity.Status,
            OverallScore = entity.OverallScore,
            RankInPosition = entity.RankInPosition,
            OverallComments = entity.OverallComments,
            StrengthsIdentified = entity.StrengthsIdentified,
            AreasForImprovement = entity.AreasForImprovement,
            TrainingNeeds = entity.TrainingNeeds,
            CareerAspirations = entity.CareerAspirations,
            RecommendPromotion = entity.RecommendPromotion,
            RecommendIncrement = entity.RecommendIncrement,
            RecommendTraining = entity.RecommendTraining,
            RecommendTermination = entity.RecommendTermination,
            RecommendationNotes = entity.RecommendationNotes,
            NextAppraisalDate = entity.NextAppraisalDate,
            AppealFiled = entity.AppealFiled,
            AppealDate = entity.AppealDate,
            AppealReason = entity.AppealReason,
            AppealOutcome = entity.AppealOutcome,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static PerformanceAppraisal ToEntity(this CreatePerformanceAppraisalDto dto)
    {
        return new PerformanceAppraisal
        {
            EmployeeId = dto.EmployeeId,
            Year = dto.Year,
            AppraisalType = dto.AppraisalType,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            Status = AppraisalStatus.Open
        };
    }

    public static void UpdateEntity(this UpdatePerformanceAppraisalDto dto, PerformanceAppraisal entity)
    {
        entity.EmployeeId = dto.EmployeeId;
        entity.Year = dto.Year;
        entity.AppraisalType = dto.AppraisalType;
        entity.StartDate = dto.StartDate;
        entity.EndDate = dto.EndDate;
        entity.Status = dto.Status;
        entity.OverallScore = dto.OverallScore;
        entity.RankInPosition = dto.RankInPosition;
        entity.OverallComments = dto.OverallComments;
        entity.StrengthsIdentified = dto.StrengthsIdentified;
        entity.AreasForImprovement = dto.AreasForImprovement;
        entity.TrainingNeeds = dto.TrainingNeeds;
        entity.CareerAspirations = dto.CareerAspirations;
        entity.RecommendPromotion = dto.RecommendPromotion;
        entity.RecommendIncrement = dto.RecommendIncrement;
        entity.RecommendTraining = dto.RecommendTraining;
        entity.RecommendTermination = dto.RecommendTermination;
        entity.RecommendationNotes = dto.RecommendationNotes;
        entity.NextAppraisalDate = dto.NextAppraisalDate;
    }

    public static List<PerformanceAppraisalDto> ToDtoList(this IEnumerable<PerformanceAppraisal> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    #endregion

    #region EvaluatorEvaluation

    public static EvaluatorEvaluationDto ToDto(this EvaluatorEvaluation entity)
    {
        return new EvaluatorEvaluationDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            AppraisalId = entity.AppraisalId,
            EvaluatorId = entity.EvaluatorId,
            EvaluatorName = entity.Evaluator.FullName,
            EvaluatorRole = entity.EvaluatorRole,
            EvaluatorWeight = entity.EvaluatorWeight,
            IsAuthoritative = entity.IsAuthoritative,
            EvaluationDate = entity.EvaluationDate,
            OverallNotes = entity.OverallNotes,
            Recommendation = entity.Recommendation,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static EvaluatorEvaluation ToEntity(this CreateEvaluatorEvaluationDto dto)
    {
        return new EvaluatorEvaluation
        {
            AppraisalId = dto.AppraisalId,
            EvaluatorId = dto.EvaluatorId,
            EvaluatorRole = dto.EvaluatorRole,
            EvaluatorWeight = dto.EvaluatorWeight,
            IsAuthoritative = dto.IsAuthoritative,
            EvaluationDate = DateTime.UtcNow,
            OverallNotes = dto.OverallNotes,
            Recommendation = dto.Recommendation
        };
    }

    public static void UpdateEntity(this UpdateEvaluatorEvaluationDto dto, EvaluatorEvaluation entity)
    {
        entity.AppraisalId = dto.AppraisalId;
        entity.EvaluatorId = dto.EvaluatorId;
        entity.EvaluatorRole = dto.EvaluatorRole;
        entity.EvaluatorWeight = dto.EvaluatorWeight;
        entity.IsAuthoritative = dto.IsAuthoritative;
        entity.OverallNotes = dto.OverallNotes;
        entity.Recommendation = dto.Recommendation;
    }

    public static List<EvaluatorEvaluationDto> ToDtoList(this IEnumerable<EvaluatorEvaluation> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    #endregion

    #region CriterionScore

    public static CriterionScoreDto ToDto(this CriterionScore entity)
    {
        return new CriterionScoreDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            EvaluatorEvaluationId = entity.EvaluatorEvaluationId,
            CriteriaId = entity.CriteriaId,
            CriteriaName = entity.AppraisalCriteria.CriteriaName,
            NumericScore = entity.NumericScore,
            KpiEvaluationRecordId = entity.KpiEvaluationRecordId,
            WeightedScore = entity.WeightedScore,
            Notes = entity.Notes,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static CriterionScore ToEntity(this CreateCriterionScoreDto dto)
    {
        return new CriterionScore
        {
            EvaluatorEvaluationId = dto.EvaluatorEvaluationId,
            CriteriaId = dto.CriteriaId,
            NumericScore = dto.NumericScore,
            KpiEvaluationRecordId = dto.KpiEvaluationRecordId,
            Notes = dto.Notes
        };
    }

    public static void UpdateEntity(this UpdateCriterionScoreDto dto, CriterionScore entity)
    {
        entity.EvaluatorEvaluationId = dto.EvaluatorEvaluationId;
        entity.CriteriaId = dto.CriteriaId;
        entity.NumericScore = dto.NumericScore;
        entity.KpiEvaluationRecordId = dto.KpiEvaluationRecordId;
        entity.Notes = dto.Notes;
    }

    public static List<CriterionScoreDto> ToDtoList(this IEnumerable<CriterionScore> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    #endregion

    #region KpiEvaluationRecord

    public static KpiEvaluationRecordDto ToDto(this KpiEvaluationRecord entity)
    {
        return new KpiEvaluationRecordDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            EmployeeKpiTargetId = entity.EmployeeKpiTargetId,
            EvaluatorId = entity.EvaluatorId,
            EvaluatorName = entity.Evaluator.FullName,
            IsSelfEvaluation = entity.IsSelfEvaluation,
            IsFinal = entity.IsFinal,
            ActualValue = entity.ActualValue,
            AchievementPercent = entity.AchievementPercent,
            EvaluationDate = entity.EvaluationDate,
            Notes = entity.Notes,
            EvidenceLinks = entity.EvidenceLinks,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static KpiEvaluationRecord ToEntity(this CreateKpiEvaluationRecordDto dto)
    {
        return new KpiEvaluationRecord
        {
            EmployeeKpiTargetId = dto.EmployeeKpiTargetId,
            EvaluatorId = dto.EvaluatorId,
            IsSelfEvaluation = dto.IsSelfEvaluation,
            IsFinal = dto.IsFinal,
            ActualValue = dto.ActualValue,
            EvaluationDate = DateTime.UtcNow,
            Notes = dto.Notes,
            EvidenceLinks = dto.EvidenceLinks
        };
    }

    public static void UpdateEntity(this UpdateKpiEvaluationRecordDto dto, KpiEvaluationRecord entity)
    {
        entity.EmployeeKpiTargetId = dto.EmployeeKpiTargetId;
        entity.EvaluatorId = dto.EvaluatorId;
        entity.IsSelfEvaluation = dto.IsSelfEvaluation;
        entity.IsFinal = dto.IsFinal;
        entity.ActualValue = dto.ActualValue;
        entity.Notes = dto.Notes;
        entity.EvidenceLinks = dto.EvidenceLinks;
    }

    public static List<KpiEvaluationRecordDto> ToDtoList(this IEnumerable<KpiEvaluationRecord> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    #endregion

    #region AppraisalEmployeeResponse

    public static AppraisalEmployeeResponseDto ToDto(this AppraisalEmployeeResponse entity)
    {
        return new AppraisalEmployeeResponseDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            AppraisalId = entity.AppraisalId,
            CriteriaId = entity.CriteriaId,
            CriteriaName = entity.AppraisalCriteria?.CriteriaName,
            ResponseText = entity.ResponseText,
            ResponseDate = entity.ResponseDate,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static AppraisalEmployeeResponse ToEntity(this CreateAppraisalEmployeeResponseDto dto)
    {
        return new AppraisalEmployeeResponse
        {
            AppraisalId = dto.AppraisalId,
            CriteriaId = dto.CriteriaId,
            ResponseText = dto.ResponseText,
            ResponseDate = DateTime.UtcNow
        };
    }

    public static void UpdateEntity(this UpdateAppraisalEmployeeResponseDto dto, AppraisalEmployeeResponse entity)
    {
        entity.AppraisalId = dto.AppraisalId;
        entity.CriteriaId = dto.CriteriaId;
        entity.ResponseText = dto.ResponseText;
        entity.ResponseDate = DateTime.UtcNow;
    }

    public static List<AppraisalEmployeeResponseDto> ToDtoList(this IEnumerable<AppraisalEmployeeResponse> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    #endregion

    #region AppraisalAttachment

    public static AppraisalAttachmentDto ToDto(this AppraisalAttachment entity)
    {
        return new AppraisalAttachmentDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            AppraisalId = entity.AppraisalId,
            FileName = entity.FileName,
            FilePath = entity.FilePath,
            Description = entity.Description,
            UploadDate = entity.UploadDate,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static AppraisalAttachment ToEntity(this CreateAppraisalAttachmentDto dto)
    {
        return new AppraisalAttachment
        {
            AppraisalId = dto.AppraisalId,
            FileName = dto.FileName,
            FilePath = dto.FilePath,
            Description = dto.Description,
            UploadDate = DateTime.UtcNow
        };
    }

    public static void UpdateEntity(this UpdateAppraisalAttachmentDto dto, AppraisalAttachment entity)
    {
        entity.AppraisalId = dto.AppraisalId;
        entity.FileName = dto.FileName;
        entity.FilePath = dto.FilePath;
        entity.Description = dto.Description;
    }

    public static List<AppraisalAttachmentDto> ToDtoList(this IEnumerable<AppraisalAttachment> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    #endregion

    #region PerformanceImprovementPlan

    public static PerformanceImprovementPlanDto ToDto(this PerformanceImprovementPlan entity)
    {
        return new PerformanceImprovementPlanDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            PipNumber = entity.PipNumber,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee.FullName,
            AppraisalId = entity.AppraisalId,
            AppraisalNumber = entity.Appraisal?.AppraisalNumber,
            StartDate = entity.StartDate,
            EndDate = entity.EndDate,
            Status = entity.Status,
            PerformanceIssues = entity.PerformanceIssues,
            ExpectedStandards = entity.ExpectedStandards,
            ImprovementActions = entity.ImprovementActions,
            SupportProvided = entity.SupportProvided,
            MeasurementCriteria = entity.MeasurementCriteria,
            SupervisorId = entity.SupervisorId,
            SupervisorName = entity.Supervisor.FullName,
            ReviewSchedule = entity.ReviewSchedule,
            CompletionDate = entity.CompletionDate,
            Outcome = entity.Outcome,
            OutcomeNotes = entity.OutcomeNotes,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static PerformanceImprovementPlan ToEntity(this CreatePerformanceImprovementPlanDto dto)
    {
        return new PerformanceImprovementPlan
        {
            EmployeeId = dto.EmployeeId,
            AppraisalId = dto.AppraisalId,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            Status = PipStatus.Active,
            PerformanceIssues = dto.PerformanceIssues,
            ExpectedStandards = dto.ExpectedStandards,
            ImprovementActions = dto.ImprovementActions,
            SupportProvided = dto.SupportProvided,
            MeasurementCriteria = dto.MeasurementCriteria,
            SupervisorId = dto.SupervisorId,
            ReviewSchedule = dto.ReviewSchedule
        };
    }

    public static void UpdateEntity(this UpdatePerformanceImprovementPlanDto dto, PerformanceImprovementPlan entity)
    {
        entity.EmployeeId = dto.EmployeeId;
        entity.AppraisalId = dto.AppraisalId;
        entity.StartDate = dto.StartDate;
        entity.EndDate = dto.EndDate;
        entity.Status = dto.Status;
        entity.PerformanceIssues = dto.PerformanceIssues;
        entity.ExpectedStandards = dto.ExpectedStandards;
        entity.ImprovementActions = dto.ImprovementActions;
        entity.SupportProvided = dto.SupportProvided;
        entity.MeasurementCriteria = dto.MeasurementCriteria;
        entity.SupervisorId = dto.SupervisorId;
        entity.ReviewSchedule = dto.ReviewSchedule;
        entity.CompletionDate = dto.CompletionDate;
        entity.Outcome = dto.Outcome;
        entity.OutcomeNotes = dto.OutcomeNotes;
    }

    public static List<PerformanceImprovementPlanDto> ToDtoList(this IEnumerable<PerformanceImprovementPlan> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    #endregion

    #region PipReviewMeeting

    public static PipReviewMeetingDto ToDto(this PipReviewMeeting entity)
    {
        return new PipReviewMeetingDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            PipId = entity.PipId,
            MeetingDate = entity.MeetingDate,
            ProgressNotes = entity.ProgressNotes,
            IssuesDiscussed = entity.IssuesDiscussed,
            ActionsAgreed = entity.ActionsAgreed,
            ConductedById = entity.ConductedById,
            ConductedByName = entity.ConductedBy.FullName,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static PipReviewMeeting ToEntity(this CreatePipReviewMeetingDto dto)
    {
        return new PipReviewMeeting
        {
            PipId = dto.PipId,
            MeetingDate = dto.MeetingDate,
            ProgressNotes = dto.ProgressNotes,
            IssuesDiscussed = dto.IssuesDiscussed,
            ActionsAgreed = dto.ActionsAgreed,
            ConductedById = dto.ConductedById
        };
    }

    public static void UpdateEntity(this UpdatePipReviewMeetingDto dto, PipReviewMeeting entity)
    {
        entity.PipId = dto.PipId;
        entity.MeetingDate = dto.MeetingDate;
        entity.ProgressNotes = dto.ProgressNotes;
        entity.IssuesDiscussed = dto.IssuesDiscussed;
        entity.ActionsAgreed = dto.ActionsAgreed;
        entity.ConductedById = dto.ConductedById;
    }

    public static List<PipReviewMeetingDto> ToDtoList(this IEnumerable<PipReviewMeeting> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }
    
    #endregion
}
