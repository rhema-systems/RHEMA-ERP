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
            IsActive = entity.IsActive,
            OverallMinScore = entity.OverallMinScore,
            OverallMaxScore = entity.OverallMaxScore,
            MappedRating = entity.MappedRating,
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
            Description = dto.Description,
            IsActive = dto.IsActive,
            OverallMinScore = dto.OverallMinScore,
            OverallMaxScore = dto.OverallMaxScore,
            MappedRating = dto.MappedRating
        };
    }

    public static void UpdateEntity(this UpdateAppraisalGradeDefinitionDto dto, AppraisalGradeDefinition entity)
    {
        entity.GradeName = dto.GradeName;
        entity.Description = dto.Description;
        entity.IsActive = dto.IsActive;
        entity.OverallMinScore = dto.OverallMinScore;
        entity.OverallMaxScore = dto.OverallMaxScore;
        entity.MappedRating = dto.MappedRating;
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
            IsActive = entity.IsActive,
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
            TolerancePercent = dto.TolerancePercent,
            IsActive = dto.IsActive
        };
    }

    public static void UpdateEntity(this UpdateKpiDefinitionDto dto, KpiDefinition entity)
    {
        entity.KpiName = dto.KpiName;
        entity.Description = dto.Description;
        entity.MeasurementType = dto.MeasurementType;
        entity.Unit = dto.Unit;
        entity.TolerancePercent = dto.TolerancePercent;
        entity.IsActive = dto.IsActive;
    }

    public static List<KpiDefinitionDto> ToDtoList(this IEnumerable<KpiDefinition> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }
    #endregion

    #region AppraisalCompetency

    public static AppraisalCompetencyDto ToDto(this AppraisalCompetency entity)
    {
        return new AppraisalCompetencyDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            Code = entity.Code,
            CriteriaName = entity.CriteriaName,
            Description = entity.Description,
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static AppraisalCompetency ToEntity(this CreateAppraisalCompetencyDto dto)
    {
        return new AppraisalCompetency
        {
            Code = dto.Code,
            CriteriaName = dto.CriteriaName,
            Description = dto.Description,
            IsActive = dto.IsActive
        };
    }

    public static void UpdateEntity(this UpdateAppraisalCompetencyDto dto, AppraisalCompetency entity)
    {
        entity.Code = dto.Code;
        entity.CriteriaName = dto.CriteriaName;
        entity.Description = dto.Description;
        entity.IsActive = dto.IsActive;
    }

    public static List<AppraisalCompetencyDto> ToDtoList(this IEnumerable<AppraisalCompetency> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    #endregion

    #region TemplateItemGradeRange

    public static TemplateItemGradeRangeDto ToDto(this TemplateItemGradeRange entity)
    {
        return new TemplateItemGradeRangeDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            AppraisalTemplateItemId = entity.AppraisalTemplateItemId,
            GradeDefinitionId = entity.GradeDefinitionId,
            GradeName = entity.GradeDefinition?.GradeName ?? string.Empty,
            LowScore = entity.LowScore,
            HighScore = entity.HighScore,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static TemplateItemGradeRange ToEntity(this CreateTemplateItemGradeRangeDto dto)
    {
        return new TemplateItemGradeRange
        {
            AppraisalTemplateItemId = dto.AppraisalTemplateItemId,
            GradeDefinitionId = dto.GradeDefinitionId,
            LowScore = dto.LowScore,
            HighScore = dto.HighScore
        };
    }

    public static void UpdateEntity(this UpdateTemplateItemGradeRangeDto dto, TemplateItemGradeRange entity)
    {
        entity.AppraisalTemplateItemId = dto.AppraisalTemplateItemId;
        entity.GradeDefinitionId = dto.GradeDefinitionId;
        entity.LowScore = dto.LowScore;
        entity.HighScore = dto.HighScore;
    }

    public static List<TemplateItemGradeRangeDto> ToDtoList(this IEnumerable<TemplateItemGradeRange> entities)
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
            // Null-guarded for the same reason as the PIP mapper: a caller that maps a freshly
            // written appraisal has no navigations loaded, and `POST api/PerformanceAppraisals`
            // did exactly that — the create endpoint 500'd on a record it had just saved.
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            EmployeeNumber = entity.Employee?.EmployeeNumber ?? string.Empty,
            DepartmentName = entity.Employee?.Department?.Name,
            PositionTitle = entity.Employee?.Position?.Title,
            Year = entity.Year,
            AppraisalCycleId = entity.AppraisalCycleId,
            AppraisalCycleCode = entity.AppraisalCycle?.CycleCode,
            StartDate = entity.StartDate,
            EndDate = entity.EndDate,
            Status = entity.Status,
            PeerEvaluatorsCount = entity.PeerEvaluatorsCount,
            OverallScore = entity.OverallScore,
            AdjustedScore = entity.AdjustedScore,
            RankInPosition = entity.RankInPosition,
            RankInUnit = entity.RankInUnit,
            OverallComments = entity.OverallComments,
            StrengthsIdentified = entity.StrengthsIdentified,
            AreasForImprovement = entity.AreasForImprovement,
            TrainingNeeds = entity.TrainingNeeds,
            CareerAspirations = entity.CareerAspirations,
            RecommendPromotion = entity.RecommendPromotion,
            RecommendIncrement = entity.RecommendIncrement,
            RecommendTraining = entity.RecommendTraining,
            RecommendPIP = entity.RecommendPIP,
            RecommendTermination = entity.RecommendTermination,
            RecommendAward = entity.RecommendAward,
            RecommendationNotes = entity.RecommendationNotes,
            NextAppraisalDate = entity.NextAppraisalDate,
            AppraisalTemplateId = entity.AppraisalTemplateId,
            DevelopmentPlanId = entity.DevelopmentPlanId,
            IsCalibrated = entity.IsCalibrated,
            CalibrationSessionId = entity.CalibrationSessionId,
            PreCalibrationScore = entity.PreCalibrationScore,
            OverallGradeDefinitionId = entity.OverallGradeDefinitionId,
            EmployeeAcknowledged = entity.EmployeeAcknowledged,
            EmployeeAcknowledgedDate = entity.EmployeeAcknowledgedDate,
            EmployeeAcknowledgmentComments = entity.EmployeeAcknowledgmentComments,
            HasAppeal = entity.HasAppeal,
            CurrentAppealStatus = entity.CurrentAppealStatus,
            IsRemandedAppeal = entity.AppealRemandedDate.HasValue,
            AppealRemandedDate = entity.AppealRemandedDate,
            AppealRemandDeadline = entity.AppealRemandDeadline,
            IsRemandDeadlineExceeded = entity.AppealRemandDeadline.HasValue && DateTime.UtcNow > entity.AppealRemandDeadline.Value,
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
            AppraisalCycleId = dto.AppraisalCycleId,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            Status = AppraisalStatus.Draft
        };
    }

    /// <summary>
    /// Applies a header correction. <b>Three fields on the DTO are deliberately ignored.</b>
    /// </summary>
    /// <remarks>
    /// <para>⚠ This mapper used to copy <c>EmployeeId</c>, <c>AppraisalCycleId</c> and
    /// <c>Status</c> straight onto the entity, and all three are <c>[Required]</c> on the DTO — so
    /// "correct the header" was, in fact, a route that could <b>re-point an appraisal at a different
    /// person</b>, carrying its goals, self-evaluation, peer reviews and scores with it; move it into
    /// a different cycle; and walk it from Draft to Completed. Proven against the running API by
    /// <c>hr-performance/probe-lane3-appraisals.mjs</c>, which did all three before this changed.</para>
    ///
    /// <para>The employee and the cycle are what the appraisal IS, not attributes of it: an appraisal
    /// raised against the wrong person is deleted (the Admin-tier route exists for exactly that) and
    /// regenerated, not edited onto someone else. And <c>UpdateStatusAsync</c> already implements a
    /// forward-only state machine with per-transition preconditions — a plain edit that assigns
    /// Status walks around the whole of it.</para>
    ///
    /// <para>They stay on the DTO rather than being removed, because the update is a REPLACE and a
    /// caller sending the record back unchanged must not be rejected for including them. Ignoring
    /// them is the behaviour; the DTO shape is unchanged.</para>
    ///
    /// <para>⚠ This mapper still assigns <c>OverallScore</c> from the body. The performance closure
    /// removes that line (lane A6): the settle path becomes the only writer of the score, and HR
    /// restates a score only through calibration or an appeal (D-11, which dropped the HR review's
    /// never-applied <c>AdjustedOverallScore</c>).</para>
    /// </remarks>
    public static void UpdateEntity(this UpdatePerformanceAppraisalDto dto, PerformanceAppraisal entity)
    {
        // entity.EmployeeId       — NOT assigned. See the remarks above.
        // entity.AppraisalCycleId — NOT assigned.
        // entity.Status           — NOT assigned; use UpdateStatusAsync, which enforces the transitions.
        entity.Year = dto.Year;
        entity.StartDate = dto.StartDate;
        entity.EndDate = dto.EndDate;
        entity.PeerEvaluatorsCount = dto.PeerEvaluatorsCount;
        entity.OverallScore = dto.OverallScore;
        entity.RankInPosition = dto.RankInPosition;
        entity.RankInUnit = dto.RankInUnit;
        entity.OverallComments = dto.OverallComments;
        entity.StrengthsIdentified = dto.StrengthsIdentified;
        entity.AreasForImprovement = dto.AreasForImprovement;
        entity.TrainingNeeds = dto.TrainingNeeds;
        entity.CareerAspirations = dto.CareerAspirations;
        entity.RecommendPromotion = dto.RecommendPromotion;
        entity.RecommendIncrement = dto.RecommendIncrement;
        entity.RecommendTraining = dto.RecommendTraining;
        entity.RecommendPIP = dto.RecommendPIP;
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
            StartedDate = entity.StartedDate,
            SubmittedDate = entity.SubmittedDate,
            TotalScore = entity.TotalScore,
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
            StartedDate = DateTime.UtcNow,
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
            TemplateItemId = entity.TemplateItemId,
            TemplateItemName = entity.TemplateItem?.Competency?.CriteriaName ?? entity.TemplateItem?.KpiDefinition?.KpiName ?? string.Empty,
            GradeDefinitionId = entity.GradeDefinitionId,
            GradeName = entity.GradeDefinition?.GradeName,
            NumericScore = entity.NumericScore,
            ActualValue = entity.ActualValue,
            WeightedScore = entity.WeightedScore,
            Notes = entity.Notes,
            EvidenceLinks = entity.EvidenceLinks,
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
            TemplateItemId = dto.TemplateItemId,
            NumericScore = dto.NumericScore,
            ActualValue = dto.ActualValue,
            Notes = dto.Notes
        };
    }

    public static void UpdateEntity(this UpdateCriterionScoreDto dto, CriterionScore entity)
    {
        entity.EvaluatorEvaluationId = dto.EvaluatorEvaluationId;
        entity.TemplateItemId = dto.TemplateItemId;
        entity.NumericScore = dto.NumericScore;
        entity.ActualValue = dto.ActualValue;
        entity.Notes = dto.Notes;
    }

    public static List<CriterionScoreDto> ToDtoList(this IEnumerable<CriterionScore> entities)
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
            TemplateItemId = entity.TemplateItemId,
            TemplateItemName = entity.TemplateItem?.Competency?.CriteriaName ?? entity.TemplateItem?.KpiDefinition?.KpiName,
            ResponseText = entity.ResponseText,
            ResponseDate = entity.ResponseDate,
            ResponseStatus = entity.ResponseStatus,
            SubmittedDate = entity.SubmittedDate,
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
            TemplateItemId = dto.TemplateItemId,
            ResponseText = dto.ResponseText,
            ResponseDate = DateTime.UtcNow
        };
    }

    public static void UpdateEntity(this UpdateAppraisalEmployeeResponseDto dto, AppraisalEmployeeResponse entity)
    {
        entity.AppraisalId = dto.AppraisalId;
        entity.TemplateItemId = dto.TemplateItemId;
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
            AppraisalId = entity.PerformanceAppraisalId ?? Guid.Empty,
            FileName = entity.FileName,
            FilePath = entity.FilePath,
            Description = entity.Description,
            UploadDate = entity.UploadDate,
            FileSizeBytes = entity.FileSizeBytes,
            UploadedById = entity.UploadedById,
            UploadedByName = entity.UploadedBy?.FullName ?? string.Empty,
            ReviewEventId = entity.ReviewEventId,
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
            PerformanceAppraisalId = dto.AppraisalId,
            FileName = dto.FileName,
            FilePath = dto.FilePath,
            Description = dto.Description,
            UploadDate = DateTime.UtcNow
        };
    }

    public static void UpdateEntity(this UpdateAppraisalAttachmentDto dto, AppraisalAttachment entity)
    {
        entity.PerformanceAppraisalId = dto.AppraisalId;
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
            // Null-guarded: a caller that maps a freshly written plan has no navigations loaded,
            // and a blank name beats a NullReferenceException surfacing as a 500 on a save that
            // in fact succeeded.
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            AppraisalId = entity.AppraisalId,
            AppraisalNumber = entity.Appraisal?.AppraisalNumber,
            StartDate = entity.StartDate,
            EndDate = entity.EndDate,
            Status = entity.Status,
            PerformanceIssues = entity.PerformanceIssues,
            ExpectedStandards = entity.ExpectedStandards,
            ImprovementActions = entity.ImprovementActions,
            SupportProvided = entity.SupportProvided ?? string.Empty,
            MeasurementCriteria = entity.MeasurementCriteria ?? string.Empty,
            SupervisorId = entity.SupervisorId,
            SupervisorName = entity.Supervisor?.FullName ?? string.Empty,
            HROwnerId = entity.HROwnerId,
            HROwnerName = entity.HROwner?.FullName,
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
            // Draft, not Active: a plan is approved through the workflow engine before it is in
            // force. The service sets this too; keeping the mapper honest means no future caller
            // can create a live plan by accident.
            Status = PipStatus.Draft,
            PerformanceIssues = dto.PerformanceIssues,
            ExpectedStandards = dto.ExpectedStandards,
            ImprovementActions = dto.ImprovementActions,
            SupportProvided = dto.SupportProvided,
            MeasurementCriteria = dto.MeasurementCriteria,
            SupervisorId = dto.SupervisorId,
            HROwnerId = dto.HROwnerId,
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
        entity.HROwnerId = dto.HROwnerId;
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
            EmployeeAttended = entity.EmployeeAttended,
            ProgressNotes = entity.ProgressNotes,
            IssuesDiscussed = entity.IssuesDiscussed,
            ActionsAgreed = entity.ActionsAgreed,
            EmployeeComments = entity.EmployeeComments,
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
            EmployeeAttended = dto.EmployeeAttended,
            ProgressNotes = dto.ProgressNotes,
            IssuesDiscussed = dto.IssuesDiscussed,
            ActionsAgreed = dto.ActionsAgreed,
            EmployeeComments = dto.EmployeeComments,
            ConductedById = dto.ConductedById
        };
    }

    public static void UpdateEntity(this UpdatePipReviewMeetingDto dto, PipReviewMeeting entity)
    {
        entity.PipId = dto.PipId;
        entity.MeetingDate = dto.MeetingDate;
        entity.EmployeeAttended = dto.EmployeeAttended;
        entity.ProgressNotes = dto.ProgressNotes;
        entity.IssuesDiscussed = dto.IssuesDiscussed;
        entity.ActionsAgreed = dto.ActionsAgreed;
        entity.EmployeeComments = dto.EmployeeComments;
        entity.ConductedById = dto.ConductedById;
    }

    public static List<PipReviewMeetingDto> ToDtoList(this IEnumerable<PipReviewMeeting> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }
    
    #endregion

    #region AppraisalSettings

    public static AppraisalSettingsDto ToDto(this AppraisalSettings entity)
    {
        return new AppraisalSettingsDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            SettingsName = entity.SettingsName,
            RequireSelfEvaluation = entity.RequireSelfEvaluation,
            AllowSelfSoftSkillRating = entity.AllowSelfSoftSkillRating,
            SelfEvaluationWeight = entity.SelfEvaluationWeight,
            RequirePeerReviews = entity.RequirePeerReviews,
            PeerNominationMode = entity.PeerNominationMode,
            MinPeerEvaluators = entity.MinPeerEvaluators,
            MaxPeerEvaluators = entity.MaxPeerEvaluators,
            PeerReviewsAnonymous = entity.PeerReviewsAnonymous,
            AllowPeerKpiEvaluation = entity.AllowPeerKpiEvaluation,
            PeerEvaluationWeight = entity.PeerEvaluationWeight,
            PeerEvaluationOpenMode = entity.PeerEvaluationOpenMode,
            RequireManagerEvaluation = entity.RequireManagerEvaluation,
            ManagerEvaluationWeight = entity.ManagerEvaluationWeight,
            ShowSelfScoreToManager = entity.ShowSelfScoreToManager,
            ShowPeerScoresToManager = entity.ShowPeerScoresToManager,
            ShowScoreBreakdownToEmployee = entity.ShowScoreBreakdownToEmployee,
            RequireCalibration = entity.RequireCalibration,
            RequireHRReview = entity.RequireHRReview,
            HRCanModifyScores = entity.HRCanModifyScores,
            HRReviewTiming = entity.HRReviewTiming,
            RequireEmployeeAcknowledgment = entity.RequireEmployeeAcknowledgment,
            AllowEmployeeResponse = entity.AllowEmployeeResponse,
            AllowAcknowledgmentWithoutConversation = entity.AllowAcknowledgmentWithoutConversation,
            EnableAppeals = entity.EnableAppeals,
            AppealWindowDays = entity.AppealWindowDays,
            AppealReevaluationWindowDays = entity.AppealReevaluationWindowDays,
            RequireGoalSetting = entity.RequireGoalSetting,
            RequireManagerGoalApproval = entity.RequireManagerGoalApproval,
            MaxGoalsPerEmployee = entity.MaxGoalsPerEmployee,
            MinGoalsPerEmployee = entity.MinGoalsPerEmployee,
            EnableCheckIns = entity.EnableCheckIns,
            EnablePrivateJournal = entity.EnablePrivateJournal,
            RequireKickOffConversation = entity.RequireKickOffConversation,
            RequireMidYearConversation = entity.RequireMidYearConversation,
            RequireFinalConversation = entity.RequireFinalConversation,
            ReviewFrequency = entity.ReviewFrequency,
            InterimReviewDepth = entity.InterimReviewDepth,
            RequireMidYearSelfAssessment = entity.RequireMidYearSelfAssessment,
            RequireGoalProgressUpdateAtReview = entity.RequireGoalProgressUpdateAtReview,
            AutoLockOnDeadline = entity.AutoLockOnDeadline,
            DefaultHRReviewerId = entity.DefaultHRReviewerId,
            ProbationExtensionMonths = entity.ProbationExtensionMonths,
            ManagerWorkloadThreshold = entity.ManagerWorkloadThreshold,
            DeadlineRiskHighDays = entity.DeadlineRiskHighDays,
            DeadlineRiskMediumDays = entity.DeadlineRiskMediumDays,
            DeadlineRiskLowDays = entity.DeadlineRiskLowDays,
            SuccessionPoolName = entity.SuccessionPoolName,
            SuccessionDefaultReadiness = entity.SuccessionDefaultReadiness,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static AppraisalSettings ToEntity(this CreateAppraisalSettingsDto dto)
    {
        return new AppraisalSettings
        {
            SettingsName = dto.SettingsName,
            RequireSelfEvaluation = dto.RequireSelfEvaluation,
            AllowSelfSoftSkillRating = dto.AllowSelfSoftSkillRating,
            SelfEvaluationWeight = dto.SelfEvaluationWeight,
            RequirePeerReviews = dto.RequirePeerReviews,
            PeerNominationMode = dto.PeerNominationMode,
            MinPeerEvaluators = dto.MinPeerEvaluators,
            MaxPeerEvaluators = dto.MaxPeerEvaluators,
            PeerReviewsAnonymous = dto.PeerReviewsAnonymous,
            AllowPeerKpiEvaluation = dto.AllowPeerKpiEvaluation,
            PeerEvaluationWeight = dto.PeerEvaluationWeight,
            PeerEvaluationOpenMode = dto.PeerEvaluationOpenMode,
            RequireManagerEvaluation = dto.RequireManagerEvaluation,
            ManagerEvaluationWeight = dto.ManagerEvaluationWeight,
            ShowSelfScoreToManager = dto.ShowSelfScoreToManager,
            ShowPeerScoresToManager = dto.ShowPeerScoresToManager,
            ShowScoreBreakdownToEmployee = dto.ShowScoreBreakdownToEmployee,
            RequireCalibration = dto.RequireCalibration,
            RequireHRReview = dto.RequireHRReview,
            HRCanModifyScores = dto.HRCanModifyScores,
            HRReviewTiming = dto.HRReviewTiming,
            RequireEmployeeAcknowledgment = dto.RequireEmployeeAcknowledgment,
            AllowEmployeeResponse = dto.AllowEmployeeResponse,
            AllowAcknowledgmentWithoutConversation = dto.AllowAcknowledgmentWithoutConversation,
            EnableAppeals = dto.EnableAppeals,
            AppealWindowDays = dto.AppealWindowDays,
            AppealReevaluationWindowDays = dto.AppealReevaluationWindowDays,
            RequireGoalSetting = dto.RequireGoalSetting,
            RequireManagerGoalApproval = dto.RequireManagerGoalApproval,
            MaxGoalsPerEmployee = dto.MaxGoalsPerEmployee,
            MinGoalsPerEmployee = dto.MinGoalsPerEmployee,
            EnableCheckIns = dto.EnableCheckIns,
            EnablePrivateJournal = dto.EnablePrivateJournal,
            RequireKickOffConversation = dto.RequireKickOffConversation,
            RequireMidYearConversation = dto.RequireMidYearConversation,
            RequireFinalConversation = dto.RequireFinalConversation,
            ReviewFrequency = dto.ReviewFrequency,
            InterimReviewDepth = dto.InterimReviewDepth,
            RequireMidYearSelfAssessment = dto.RequireMidYearSelfAssessment,
            RequireGoalProgressUpdateAtReview = dto.RequireGoalProgressUpdateAtReview,
            AutoLockOnDeadline = dto.AutoLockOnDeadline,
            DefaultHRReviewerId = dto.DefaultHRReviewerId,
            ProbationExtensionMonths = dto.ProbationExtensionMonths,
            ManagerWorkloadThreshold = dto.ManagerWorkloadThreshold,
            DeadlineRiskHighDays = dto.DeadlineRiskHighDays,
            DeadlineRiskMediumDays = dto.DeadlineRiskMediumDays,
            DeadlineRiskLowDays = dto.DeadlineRiskLowDays,
            SuccessionPoolName = dto.SuccessionPoolName,
            SuccessionDefaultReadiness = dto.SuccessionDefaultReadiness
        };
    }

    public static void UpdateEntity(this UpdateAppraisalSettingsDto dto, AppraisalSettings entity)
    {
        entity.SettingsName = dto.SettingsName;
        entity.RequireSelfEvaluation = dto.RequireSelfEvaluation;
        entity.AllowSelfSoftSkillRating = dto.AllowSelfSoftSkillRating;
        entity.SelfEvaluationWeight = dto.SelfEvaluationWeight;
        entity.RequirePeerReviews = dto.RequirePeerReviews;
        entity.PeerNominationMode = dto.PeerNominationMode;
        entity.MinPeerEvaluators = dto.MinPeerEvaluators;
        entity.MaxPeerEvaluators = dto.MaxPeerEvaluators;
        entity.PeerReviewsAnonymous = dto.PeerReviewsAnonymous;
        entity.AllowPeerKpiEvaluation = dto.AllowPeerKpiEvaluation;
        entity.PeerEvaluationWeight = dto.PeerEvaluationWeight;
        entity.PeerEvaluationOpenMode = dto.PeerEvaluationOpenMode;
        entity.RequireManagerEvaluation = dto.RequireManagerEvaluation;
        entity.ManagerEvaluationWeight = dto.ManagerEvaluationWeight;
        entity.ShowSelfScoreToManager = dto.ShowSelfScoreToManager;
        entity.ShowPeerScoresToManager = dto.ShowPeerScoresToManager;
        entity.ShowScoreBreakdownToEmployee = dto.ShowScoreBreakdownToEmployee;
        entity.RequireCalibration = dto.RequireCalibration;
        entity.RequireHRReview = dto.RequireHRReview;
        entity.HRCanModifyScores = dto.HRCanModifyScores;
        entity.HRReviewTiming = dto.HRReviewTiming;
        entity.RequireEmployeeAcknowledgment = dto.RequireEmployeeAcknowledgment;
        entity.AllowEmployeeResponse = dto.AllowEmployeeResponse;
        entity.AllowAcknowledgmentWithoutConversation = dto.AllowAcknowledgmentWithoutConversation;
        entity.EnableAppeals = dto.EnableAppeals;
        entity.AppealWindowDays = dto.AppealWindowDays;
        entity.AppealReevaluationWindowDays = dto.AppealReevaluationWindowDays;
        entity.RequireGoalSetting = dto.RequireGoalSetting;
        entity.RequireManagerGoalApproval = dto.RequireManagerGoalApproval;
        entity.MaxGoalsPerEmployee = dto.MaxGoalsPerEmployee;
        entity.MinGoalsPerEmployee = dto.MinGoalsPerEmployee;
        entity.EnableCheckIns = dto.EnableCheckIns;
        entity.EnablePrivateJournal = dto.EnablePrivateJournal;
        entity.RequireKickOffConversation = dto.RequireKickOffConversation;
        entity.RequireMidYearConversation = dto.RequireMidYearConversation;
        entity.RequireFinalConversation = dto.RequireFinalConversation;
        entity.ReviewFrequency = dto.ReviewFrequency;
        entity.InterimReviewDepth = dto.InterimReviewDepth;
        entity.RequireMidYearSelfAssessment = dto.RequireMidYearSelfAssessment;
        entity.RequireGoalProgressUpdateAtReview = dto.RequireGoalProgressUpdateAtReview;
        entity.AutoLockOnDeadline = dto.AutoLockOnDeadline;
        entity.DefaultHRReviewerId = dto.DefaultHRReviewerId;
        entity.ProbationExtensionMonths = dto.ProbationExtensionMonths;
        entity.ManagerWorkloadThreshold = dto.ManagerWorkloadThreshold;
        entity.DeadlineRiskHighDays = dto.DeadlineRiskHighDays;
        entity.DeadlineRiskMediumDays = dto.DeadlineRiskMediumDays;
        entity.DeadlineRiskLowDays = dto.DeadlineRiskLowDays;
        entity.SuccessionPoolName = dto.SuccessionPoolName;
        entity.SuccessionDefaultReadiness = dto.SuccessionDefaultReadiness;
    }

    public static List<AppraisalSettingsDto> ToDtoList(this IEnumerable<AppraisalSettings> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    #endregion

    #region AppraisalCycle

    public static AppraisalCycleDto ToDto(this AppraisalCycle entity)
    {
        return new AppraisalCycleDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CycleCode = entity.CycleCode,
            CycleName = entity.CycleName,
            Year = entity.Year,
            AppraisalType = entity.AppraisalType,
            StartDate = entity.StartDate,
            EndDate = entity.EndDate,
            AppraisalSettingsId = entity.AppraisalSettingsId,
            AppraisalSettingsName = entity.AppraisalSettings?.SettingsName,
            Status = entity.Status,
            GoalSettingOpenDate = entity.GoalSettingOpenDate,
            GoalSettingDeadline = entity.GoalSettingDeadline,
            Q1ReviewOpenDate = entity.Q1ReviewOpenDate,
            Q1ReviewDeadline = entity.Q1ReviewDeadline,
            MidYearOpenDate = entity.MidYearOpenDate,
            MidYearDeadline = entity.MidYearDeadline,
            Q3ReviewOpenDate = entity.Q3ReviewOpenDate,
            Q3ReviewDeadline = entity.Q3ReviewDeadline,
            PeerNominationDeadline = entity.PeerNominationDeadline,
            SelfEvaluationOpenDate = entity.SelfEvaluationOpenDate,
            SelfEvaluationDeadline = entity.SelfEvaluationDeadline,
            PeerEvaluationOpenDate = entity.PeerEvaluationOpenDate,
            PeerEvaluationDeadline = entity.PeerEvaluationDeadline,
            ManagerEvaluationOpenDate = entity.ManagerEvaluationOpenDate,
            ManagerEvaluationDeadline = entity.ManagerEvaluationDeadline,
            CalibrationOpenDate = entity.CalibrationOpenDate,
            CalibrationDeadline = entity.CalibrationDeadline,
            HRReviewOpenDate = entity.HRReviewOpenDate,
            HRReviewDeadline = entity.HRReviewDeadline,
            EmployeeAcknowledgeDeadline = entity.EmployeeAcknowledgeDeadline,
            FinalConversationDeadline = entity.FinalConversationDeadline,
            OpenedById = entity.OpenedById,
            OpenedByName = entity.OpenedBy?.FullName,
            OpenedDate = entity.OpenedDate,
            ClosedById = entity.ClosedById,
            ClosedByName = entity.ClosedBy?.FullName,
            ClosedDate = entity.ClosedDate,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static AppraisalCycle ToEntity(this CreateAppraisalCycleDto dto)
    {
        return new AppraisalCycle
        {
            CycleCode = dto.CycleCode,
            CycleName = dto.CycleName,
            Year = dto.Year,
            AppraisalType = dto.AppraisalType,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            AppraisalSettingsId = dto.AppraisalSettingsId,
            Status = dto.Status,
            GoalSettingOpenDate = dto.GoalSettingOpenDate,
            GoalSettingDeadline = dto.GoalSettingDeadline,
            Q1ReviewOpenDate = dto.Q1ReviewOpenDate,
            Q1ReviewDeadline = dto.Q1ReviewDeadline,
            MidYearOpenDate = dto.MidYearOpenDate,
            MidYearDeadline = dto.MidYearDeadline,
            Q3ReviewOpenDate = dto.Q3ReviewOpenDate,
            Q3ReviewDeadline = dto.Q3ReviewDeadline,
            PeerNominationDeadline = dto.PeerNominationDeadline,
            SelfEvaluationOpenDate = dto.SelfEvaluationOpenDate,
            SelfEvaluationDeadline = dto.SelfEvaluationDeadline,
            PeerEvaluationOpenDate = dto.PeerEvaluationOpenDate,
            PeerEvaluationDeadline = dto.PeerEvaluationDeadline,
            ManagerEvaluationOpenDate = dto.ManagerEvaluationOpenDate,
            ManagerEvaluationDeadline = dto.ManagerEvaluationDeadline,
            CalibrationOpenDate = dto.CalibrationOpenDate,
            CalibrationDeadline = dto.CalibrationDeadline,
            HRReviewOpenDate = dto.HRReviewOpenDate,
            HRReviewDeadline = dto.HRReviewDeadline,
            EmployeeAcknowledgeDeadline = dto.EmployeeAcknowledgeDeadline,
            FinalConversationDeadline = dto.FinalConversationDeadline
        };
    }

    public static void UpdateEntity(this UpdateAppraisalCycleDto dto, AppraisalCycle entity)
    {
        entity.CycleCode = dto.CycleCode;
        entity.CycleName = dto.CycleName;
        entity.Year = dto.Year;
        entity.AppraisalType = dto.AppraisalType;
        entity.StartDate = dto.StartDate;
        entity.EndDate = dto.EndDate;
        entity.AppraisalSettingsId = dto.AppraisalSettingsId;
        // Status is not copied — see UpdateAppraisalCycleDto. Open/close own it.
        entity.GoalSettingOpenDate = dto.GoalSettingOpenDate;
        entity.GoalSettingDeadline = dto.GoalSettingDeadline;
        entity.Q1ReviewOpenDate = dto.Q1ReviewOpenDate;
        entity.Q1ReviewDeadline = dto.Q1ReviewDeadline;
        entity.MidYearOpenDate = dto.MidYearOpenDate;
        entity.MidYearDeadline = dto.MidYearDeadline;
        entity.Q3ReviewOpenDate = dto.Q3ReviewOpenDate;
        entity.Q3ReviewDeadline = dto.Q3ReviewDeadline;
        entity.PeerNominationDeadline = dto.PeerNominationDeadline;
        entity.SelfEvaluationOpenDate = dto.SelfEvaluationOpenDate;
        entity.SelfEvaluationDeadline = dto.SelfEvaluationDeadline;
        entity.PeerEvaluationOpenDate = dto.PeerEvaluationOpenDate;
        entity.PeerEvaluationDeadline = dto.PeerEvaluationDeadline;
        entity.ManagerEvaluationOpenDate = dto.ManagerEvaluationOpenDate;
        entity.ManagerEvaluationDeadline = dto.ManagerEvaluationDeadline;
        entity.CalibrationOpenDate = dto.CalibrationOpenDate;
        entity.CalibrationDeadline = dto.CalibrationDeadline;
        entity.HRReviewOpenDate = dto.HRReviewOpenDate;
        entity.HRReviewDeadline = dto.HRReviewDeadline;
        entity.EmployeeAcknowledgeDeadline = dto.EmployeeAcknowledgeDeadline;
        entity.FinalConversationDeadline = dto.FinalConversationDeadline;
    }

    public static List<AppraisalCycleDto> ToDtoList(this IEnumerable<AppraisalCycle> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    #endregion

    #region AppraisalCycleTarget

    public static AppraisalCycleTargetDto ToDto(this AppraisalCycleTarget entity)
    {
        return new AppraisalCycleTargetDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            AppraisalCycleId = entity.AppraisalCycleId,
            AppraisalCycleCode = entity.AppraisalCycle?.CycleCode,
            TargetType = entity.TargetType,
            OrganizationLevelId = entity.OrganizationLevelId,
            OrganizationLevelName = entity.OrganizationLevel?.Name,
            OrganizationUnitId = entity.OrganizationUnitId,
            OrganizationUnitName = entity.OrganizationUnit?.Name,
            PositionId = entity.PositionId,
            PositionTitle = entity.Position?.Title,
            EstimatedEmployeeCount = entity.EstimatedEmployeeCount,
            ActiveEmployeeCount = entity.ActiveEmployeeCount,
            Notes = entity.Notes,
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static AppraisalCycleTarget ToEntity(this CreateAppraisalCycleTargetDto dto)
    {
        return new AppraisalCycleTarget
        {
            AppraisalCycleId = dto.AppraisalCycleId,
            TargetType = dto.TargetType,
            OrganizationLevelId = dto.OrganizationLevelId,
            OrganizationUnitId = dto.OrganizationUnitId,
            PositionId = dto.PositionId,
            EstimatedEmployeeCount = dto.EstimatedEmployeeCount,
            Notes = dto.Notes,
            IsActive = dto.IsActive
        };
    }

    public static void UpdateEntity(this UpdateAppraisalCycleTargetDto dto, AppraisalCycleTarget entity)
    {
        entity.AppraisalCycleId = dto.AppraisalCycleId;
        entity.TargetType = dto.TargetType;
        entity.OrganizationLevelId = dto.OrganizationLevelId;
        entity.OrganizationUnitId = dto.OrganizationUnitId;
        entity.PositionId = dto.PositionId;
        entity.EstimatedEmployeeCount = dto.EstimatedEmployeeCount;
        entity.Notes = dto.Notes;
        entity.IsActive = dto.IsActive;
    }

    public static List<AppraisalCycleTargetDto> ToDtoList(this IEnumerable<AppraisalCycleTarget> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    #endregion

    #region PeerNomination

    public static PeerNominationDto ToDto(this PeerNomination entity)
    {
        return new PeerNominationDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            AppraisalId = entity.AppraisalId,
            AppraisalNumber = entity.Appraisal?.AppraisalNumber,
            PeerEmployeeId = entity.PeerEmployeeId,
            PeerEmployeeName = entity.PeerEmployee?.FullName ?? string.Empty,
            PeerEmployeeNumber = entity.PeerEmployee?.EmployeeNumber,
            NominatedById = entity.NominatedById,
            NominatedByName = entity.NominatedBy?.FullName ?? string.Empty,
            NominationDate = entity.NominationDate,
            InvitationSentDate = entity.InvitationSentDate,
            DueDate = entity.DueDate,
            InstructionsToPeer = entity.InstructionsToPeer,
            NominationStatus = entity.NominationStatus,
            ApprovedDate = entity.ApprovedDate,
            RejectionReason = entity.RejectionReason,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static PeerNomination ToEntity(this CreatePeerNominationDto dto)
    {
        return new PeerNomination
        {
            AppraisalId = dto.AppraisalId,
            PeerEmployeeId = dto.PeerEmployeeId,
            NominatedById = dto.NominatedById,
            NominationDate = DateTime.UtcNow,
            DueDate = dto.DueDate,
            InstructionsToPeer = dto.InstructionsToPeer,
            NominationStatus = dto.NominationStatus
        };
    }

    public static void UpdateEntity(this UpdatePeerNominationDto dto, PeerNomination entity)
    {
        entity.AppraisalId = dto.AppraisalId;
        entity.PeerEmployeeId = dto.PeerEmployeeId;
        entity.NominatedById = dto.NominatedById;
        entity.InvitationSentDate = dto.InvitationSentDate;
        entity.DueDate = dto.DueDate;
        entity.InstructionsToPeer = dto.InstructionsToPeer;
        entity.NominationStatus = dto.NominationStatus;
    }

    public static List<PeerNominationDto> ToDtoList(this IEnumerable<PeerNomination> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    #endregion

    #region AppraisalAppeal

    public static AppraisalAppealDto ToDto(this AppraisalAppeal entity)
    {
        return new AppraisalAppealDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            PerformanceAppraisalId = entity.PerformanceAppraisalId,
            AppraisalNumber = entity.PerformanceAppraisal?.AppraisalNumber ?? string.Empty,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            SubmittedDate = entity.SubmittedDate,
            AppealReason = entity.AppealReason,
            Status = entity.Status,
            ReviewedById = entity.ReviewedById,
            ReviewerName = entity.Reviewer?.FullName,
            ResolutionNotes = entity.ResolutionNotes,
            ResolvedDate = entity.ResolvedDate,
            Items = entity.Items?.Select(i => i.ToDto()).ToList() ?? new List<AppraisalAppealItemDto>(),
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static List<AppraisalAppealDto> ToDtoList(this IEnumerable<AppraisalAppeal> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    #endregion

    #region AppraisalAppealItem

    public static AppraisalAppealItemDto ToDto(this AppraisalAppealItem entity)
    {
        return new AppraisalAppealItemDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            AppraisalAppealId = entity.AppraisalAppealId,
            TemplateItemId = entity.TemplateItemId,
            TemplateItemName = entity.TemplateItem?.Competency?.CriteriaName ?? entity.TemplateItem?.KpiDefinition?.KpiName,
            Reason = entity.Reason,
            ResolutionNotes = entity.ResolutionNotes,
            ScoreAdjusted = entity.ScoreAdjusted,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static List<AppraisalAppealItemDto> ToDtoList(this IEnumerable<AppraisalAppealItem> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    #endregion

    #region AppraisalTemplate

    public static AppraisalTemplateDto ToDto(this AppraisalTemplate entity)
    {
        return new AppraisalTemplateDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            TemplateName = entity.TemplateName,
            Description = entity.Description,
            OrganizationLevelId = entity.OrganizationLevelId,
            OrganizationLevelName = entity.OrganizationLevel?.Name,
            OrganizationUnitId = entity.OrganizationUnitId,
            OrganizationUnitName = entity.OrganizationUnit?.Name,
            PositionId = entity.PositionId,
            PositionTitle = entity.Position?.Title,
            IsActive = entity.IsActive,
            ApprovalStatus = entity.ApprovalStatus,
            SubmittedDate = entity.SubmittedDate,
            ApprovalDate = entity.ApprovalDate,
            RejectionReason = entity.RejectionReason,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static AppraisalTemplate ToEntity(this CreateAppraisalTemplateDto dto)
    {
        return new AppraisalTemplate
        {
            TemplateName = dto.TemplateName,
            Description = dto.Description,
            OrganizationLevelId = dto.OrganizationLevelId,
            OrganizationUnitId = dto.OrganizationUnitId,
            PositionId = dto.PositionId,
            IsActive = dto.IsActive
        };
    }

    public static void UpdateEntity(this UpdateAppraisalTemplateDto dto, AppraisalTemplate entity)
    {
        entity.TemplateName = dto.TemplateName;
        entity.Description = dto.Description;
        entity.OrganizationLevelId = dto.OrganizationLevelId;
        entity.OrganizationUnitId = dto.OrganizationUnitId;
        entity.PositionId = dto.PositionId;
        entity.IsActive = dto.IsActive;
    }

    public static List<AppraisalTemplateDto> ToDtoList(this IEnumerable<AppraisalTemplate> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    #endregion

    #region AppraisalTemplateSection

    public static AppraisalTemplateSectionDto ToDto(this AppraisalTemplateSection entity)
    {
        return new AppraisalTemplateSectionDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            AppraisalTemplateId = entity.AppraisalTemplateId,
            TemplateName = entity.AppraisalTemplate?.TemplateName ?? string.Empty,
            SectionName = entity.SectionName,
            Description = entity.Description,
            DisplayOrder = entity.DisplayOrder,
            Weight = entity.Weight,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static AppraisalTemplateSection ToEntity(this CreateAppraisalTemplateSectionDto dto)
    {
        return new AppraisalTemplateSection
        {
            AppraisalTemplateId = dto.AppraisalTemplateId,
            SectionName = dto.SectionName,
            Description = dto.Description,
            DisplayOrder = dto.DisplayOrder,
            Weight = dto.Weight
        };
    }

    public static void UpdateEntity(this UpdateAppraisalTemplateSectionDto dto, AppraisalTemplateSection entity)
    {
        entity.AppraisalTemplateId = dto.AppraisalTemplateId;
        entity.SectionName = dto.SectionName;
        entity.Description = dto.Description;
        entity.DisplayOrder = dto.DisplayOrder;
        entity.Weight = dto.Weight;
    }

    public static List<AppraisalTemplateSectionDto> ToDtoList(this IEnumerable<AppraisalTemplateSection> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    #endregion

    #region AppraisalTemplateItem

    public static AppraisalTemplateItemDto ToDto(this AppraisalTemplateItem entity)
    {
        return new AppraisalTemplateItemDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            AppraisalTemplateSectionId = entity.AppraisalTemplateSectionId,
            SectionName = entity.Section?.SectionName ?? string.Empty,
            CompetencyId = entity.CompetencyId,
            CompetencyName = entity.Competency?.CriteriaName,
            KpiDefinitionId = entity.KpiDefinitionId,
            KpiName = entity.KpiDefinition?.KpiName,
            KpiTargetValue = entity.KpiTargetValue,
            KpiMinValue = entity.KpiMinValue,
            KpiMaxValue = entity.KpiMaxValue,
            CustomQuestion = entity.CustomQuestion,
            DisplayOrder = entity.DisplayOrder,
            Weight = entity.Weight,
            GradeRangeCount = entity.GradeRanges?.Count ?? 0,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static AppraisalTemplateItem ToEntity(this CreateAppraisalTemplateItemDto dto)
    {
        return new AppraisalTemplateItem
        {
            AppraisalTemplateSectionId = dto.AppraisalTemplateSectionId,
            CompetencyId = dto.CompetencyId,
            KpiDefinitionId = dto.KpiDefinitionId,
            KpiTargetValue = dto.KpiTargetValue,
            KpiMinValue = dto.KpiMinValue,
            KpiMaxValue = dto.KpiMaxValue,
            CustomQuestion = dto.CustomQuestion,
            DisplayOrder = dto.DisplayOrder,
            Weight = dto.Weight
        };
    }

    public static void UpdateEntity(this UpdateAppraisalTemplateItemDto dto, AppraisalTemplateItem entity)
    {
        entity.AppraisalTemplateSectionId = dto.AppraisalTemplateSectionId;
        entity.CompetencyId = dto.CompetencyId;
        entity.KpiDefinitionId = dto.KpiDefinitionId;
        entity.KpiTargetValue = dto.KpiTargetValue;
        entity.KpiMinValue = dto.KpiMinValue;
        entity.KpiMaxValue = dto.KpiMaxValue;
        entity.CustomQuestion = dto.CustomQuestion;
        entity.DisplayOrder = dto.DisplayOrder;
        entity.Weight = dto.Weight;
    }

    public static List<AppraisalTemplateItemDto> ToDtoList(this IEnumerable<AppraisalTemplateItem> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    #endregion

    #region GoalLibrary

    public static GoalLibraryDto ToDto(this GoalLibrary entity)
    {
        return new GoalLibraryDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            Title = entity.Title,
            Description = entity.Description,
            SuccessCriteria = entity.SuccessCriteria,
            OrganizationLevelId = entity.OrganizationLevelId,
            OrganizationLevelName = entity.OrganizationLevel?.Name,
            OrganizationUnitId = entity.OrganizationUnitId,
            OrganizationUnitName = entity.OrganizationUnit?.Name,
            PositionId = entity.PositionId,
            PositionTitle = entity.Position?.Title,
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static GoalLibrary ToEntity(this CreateGoalLibraryDto dto)
    {
        return new GoalLibrary
        {
            Title = dto.Title,
            Description = dto.Description,
            SuccessCriteria = dto.SuccessCriteria,
            OrganizationLevelId = dto.OrganizationLevelId,
            OrganizationUnitId = dto.OrganizationUnitId,
            PositionId = dto.PositionId,
            IsActive = dto.IsActive
        };
    }

    public static void UpdateEntity(this UpdateGoalLibraryDto dto, GoalLibrary entity)
    {
        entity.Title = dto.Title;
        entity.Description = dto.Description;
        entity.SuccessCriteria = dto.SuccessCriteria;
        entity.OrganizationLevelId = dto.OrganizationLevelId;
        entity.OrganizationUnitId = dto.OrganizationUnitId;
        entity.PositionId = dto.PositionId;
        entity.IsActive = dto.IsActive;
    }

    public static List<GoalLibraryDto> ToDtoList(this IEnumerable<GoalLibrary> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    #endregion

    #region AppraisalCycleTargetExclusion

    public static AppraisalCycleTargetExclusionDto ToDto(this AppraisalCycleTargetExclusion entity)
    {
        return new AppraisalCycleTargetExclusionDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            AppraisalCycleTargetId = entity.AppraisalCycleTargetId,
            OrganizationLevelId = entity.OrganizationLevelId,
            OrganizationLevelName = entity.OrganizationLevel?.Name,
            OrganizationUnitId = entity.OrganizationUnitId,
            OrganizationUnitName = entity.OrganizationUnit?.Name,
            PositionId = entity.PositionId,
            PositionTitle = entity.Position?.Title,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName,
            Reason = entity.Reason,
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static AppraisalCycleTargetExclusion ToEntity(this CreateAppraisalCycleTargetExclusionDto dto)
    {
        return new AppraisalCycleTargetExclusion
        {
            AppraisalCycleTargetId = dto.AppraisalCycleTargetId,
            OrganizationLevelId = dto.OrganizationLevelId,
            OrganizationUnitId = dto.OrganizationUnitId,
            PositionId = dto.PositionId,
            EmployeeId = dto.EmployeeId,
            Reason = dto.Reason,
            IsActive = dto.IsActive
        };
    }

    public static void UpdateEntity(this UpdateAppraisalCycleTargetExclusionDto dto, AppraisalCycleTargetExclusion entity)
    {
        entity.AppraisalCycleTargetId = dto.AppraisalCycleTargetId;
        entity.OrganizationLevelId = dto.OrganizationLevelId;
        entity.OrganizationUnitId = dto.OrganizationUnitId;
        entity.PositionId = dto.PositionId;
        entity.EmployeeId = dto.EmployeeId;
        entity.Reason = dto.Reason;
        entity.IsActive = dto.IsActive;
    }

    public static List<AppraisalCycleTargetExclusionDto> ToDtoList(this IEnumerable<AppraisalCycleTargetExclusion> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    #endregion

    #region AppraisalCycleTemplate

    public static AppraisalCycleTemplateDto ToDto(this AppraisalCycleTemplate entity)
    {
        // Scope is owned by AppraisalTemplate — read through the navigation property.
        var tmpl = entity.AppraisalTemplate;
        return new AppraisalCycleTemplateDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            AppraisalCycleId = entity.AppraisalCycleId,
            CycleCode = entity.AppraisalCycle?.CycleCode,
            AppraisalTemplateId = entity.AppraisalTemplateId,
            TemplateName = tmpl?.TemplateName ?? string.Empty,
            OrganizationLevelId = tmpl?.OrganizationLevelId,
            OrganizationLevelName = tmpl?.OrganizationLevel?.Name,
            OrganizationUnitId = tmpl?.OrganizationUnitId,
            OrganizationUnitName = tmpl?.OrganizationUnit?.Name,
            PositionId = tmpl?.PositionId,
            PositionTitle = tmpl?.Position?.Title,
            Priority = entity.Priority,
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static AppraisalCycleTemplate ToEntity(this CreateAppraisalCycleTemplateDto dto)
    {
        return new AppraisalCycleTemplate
        {
            AppraisalCycleId = dto.AppraisalCycleId,
            AppraisalTemplateId = dto.AppraisalTemplateId,
            Priority = dto.Priority,
            IsActive = dto.IsActive
        };
    }

    public static void UpdateEntity(this UpdateAppraisalCycleTemplateDto dto, AppraisalCycleTemplate entity)
    {
        entity.AppraisalCycleId = dto.AppraisalCycleId;
        entity.AppraisalTemplateId = dto.AppraisalTemplateId;
        entity.Priority = dto.Priority;
        entity.IsActive = dto.IsActive;
    }

    public static List<AppraisalCycleTemplateDto> ToDtoList(this IEnumerable<AppraisalCycleTemplate> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    #endregion

    #region StrategicGoal

    public static StrategicGoalDto ToDto(this StrategicGoal entity)
    {
        return new StrategicGoalDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            Title = entity.Title,
            Description = entity.Description,
            SuccessCriteria = entity.SuccessCriteria,
            Priority = entity.Priority,
            StartYear = entity.StartYear,
            EndYear = entity.EndYear,
            IsActive = entity.IsActive,
            YearlyObjectiveCount = entity.CompanyGoals?.Count ?? 0,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static StrategicGoal ToEntity(this CreateStrategicGoalDto dto)
    {
        return new StrategicGoal
        {
            Title = dto.Title,
            Description = dto.Description,
            SuccessCriteria = dto.SuccessCriteria,
            Priority = dto.Priority,
            StartYear = dto.StartYear,
            EndYear = dto.EndYear,
            IsActive = dto.IsActive
        };
    }

    public static void UpdateEntity(this UpdateStrategicGoalDto dto, StrategicGoal entity)
    {
        entity.Title = dto.Title;
        entity.Description = dto.Description;
        entity.SuccessCriteria = dto.SuccessCriteria;
        entity.Priority = dto.Priority;
        entity.StartYear = dto.StartYear;
        entity.EndYear = dto.EndYear;
        entity.IsActive = dto.IsActive;
    }

    public static List<StrategicGoalDto> ToDtoList(this IEnumerable<StrategicGoal> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    #endregion

    #region CompanyGoal

    public static CompanyGoalDto ToDto(this CompanyGoal entity)
    {
        return new CompanyGoalDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            AppraisalCycleId = entity.AppraisalCycleId,
            CycleCode = entity.AppraisalCycle?.CycleCode,
            StrategicGoalId = entity.StrategicGoalId,
            StrategicGoalTitle = entity.StrategicGoal?.Title,
            Title = entity.Title,
            Description = entity.Description,
            SuccessCriteria = entity.SuccessCriteria,
            Priority = entity.Priority,
            TargetValue = entity.TargetValue,
            Unit = entity.Unit,
            DueDate = entity.DueDate,
            IsVisible = entity.IsVisible,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static CompanyGoal ToEntity(this CreateCompanyGoalDto dto)
    {
        return new CompanyGoal
        {
            AppraisalCycleId = dto.AppraisalCycleId,
            StrategicGoalId = dto.StrategicGoalId,
            Title = dto.Title,
            Description = dto.Description,
            SuccessCriteria = dto.SuccessCriteria,
            Priority = dto.Priority,
            TargetValue = dto.TargetValue,
            Unit = dto.Unit,
            DueDate = dto.DueDate,
            IsVisible = dto.IsVisible
        };
    }

    public static void UpdateEntity(this UpdateCompanyGoalDto dto, CompanyGoal entity)
    {
        entity.AppraisalCycleId = dto.AppraisalCycleId;
        entity.StrategicGoalId = dto.StrategicGoalId;
        entity.Title = dto.Title;
        entity.Description = dto.Description;
        entity.SuccessCriteria = dto.SuccessCriteria;
        entity.Priority = dto.Priority;
        entity.TargetValue = dto.TargetValue;
        entity.Unit = dto.Unit;
        entity.DueDate = dto.DueDate;
        entity.IsVisible = dto.IsVisible;
    }

    public static List<CompanyGoalDto> ToDtoList(this IEnumerable<CompanyGoal> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    #endregion

    #region UnitGoal

    public static UnitGoalDto ToDto(this UnitGoal entity)
    {
        return new UnitGoalDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            AppraisalCycleId = entity.AppraisalCycleId,
            CycleCode = entity.AppraisalCycle?.CycleCode,
            ParentCompanyGoalId = entity.ParentCompanyGoalId,
            ParentGoalTitle = entity.ParentCompanyGoal?.Title,
            ParentUnitGoalId = entity.ParentUnitGoalId,
            ParentUnitGoalTitle = entity.ParentUnitGoal?.Title,
            OrganizationLevelId = entity.OrganizationLevelId,
            OrganizationLevelName = entity.OrganizationLevel?.Name ?? string.Empty,
            OrganizationUnitId = entity.OrganizationUnitId,
            OrganizationUnitName = entity.OrganizationUnit?.Name ?? string.Empty,
            CreatedByManagerId = entity.CreatedByManagerId,
            ManagerName = entity.CreatedByManager?.FullName ?? string.Empty,
            Title = entity.Title,
            Description = entity.Description,
            SuccessCriteria = entity.SuccessCriteria,
            Priority = entity.Priority,
            TargetValue = entity.TargetValue,
            Unit = entity.Unit,
            DueDate = entity.DueDate,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static UnitGoal ToEntity(this CreateUnitGoalDto dto)
    {
        return new UnitGoal
        {
            AppraisalCycleId = dto.AppraisalCycleId,
            ParentCompanyGoalId = dto.ParentCompanyGoalId,
            ParentUnitGoalId = dto.ParentUnitGoalId,
            OrganizationLevelId = dto.OrganizationLevelId,
            OrganizationUnitId = dto.OrganizationUnitId,
            CreatedByManagerId = dto.CreatedByManagerId,
            Title = dto.Title,
            Description = dto.Description,
            SuccessCriteria = dto.SuccessCriteria,
            Priority = dto.Priority,
            TargetValue = dto.TargetValue,
            Unit = dto.Unit,
            DueDate = dto.DueDate
        };
    }

    public static void UpdateEntity(this UpdateUnitGoalDto dto, UnitGoal entity)
    {
        entity.AppraisalCycleId = dto.AppraisalCycleId;
        entity.ParentCompanyGoalId = dto.ParentCompanyGoalId;
        entity.ParentUnitGoalId = dto.ParentUnitGoalId;
        entity.OrganizationLevelId = dto.OrganizationLevelId;
        entity.OrganizationUnitId = dto.OrganizationUnitId;
        entity.CreatedByManagerId = dto.CreatedByManagerId;
        entity.Title = dto.Title;
        entity.Description = dto.Description;
        entity.SuccessCriteria = dto.SuccessCriteria;
        entity.Priority = dto.Priority;
        entity.TargetValue = dto.TargetValue;
        entity.Unit = dto.Unit;
        entity.DueDate = dto.DueDate;
    }

    public static List<UnitGoalDto> ToDtoList(this IEnumerable<UnitGoal> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    #endregion

    #region EmployeeGoal

    public static EmployeeGoalDto ToDto(this EmployeeGoal entity)
    {
        return new EmployeeGoalDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            AppraisalCycleId = entity.AppraisalCycleId,
            CycleCode = entity.AppraisalCycle?.CycleCode,
            PerformanceAppraisalId = entity.PerformanceAppraisalId,
            CompanyGoalId = entity.CompanyGoalId,
            UnitGoalId = entity.UnitGoalId,
            ParentGoalId = entity.ParentGoalId,
            ParentGoalTitle = entity.ParentCompanyGoal?.Title
                           ?? entity.ParentUnitGoal?.Title
                           ?? entity.ParentGoal?.Title,
            ParentType = entity.ParentType,
            GoalLibraryId = entity.GoalLibraryId,
            LibraryItemTitle = entity.LibraryItem?.Title,
            KpiDefinitionId = entity.KpiDefinitionId,
            KpiName = entity.KpiDefinition?.KpiName,
            Title = entity.Title,
            Description = entity.Description,
            SuccessCriteria = entity.SuccessCriteria,
            Weight = entity.Weight,
            Priority = entity.Priority,
            Status = entity.Status,
            MeasurementType = entity.MeasurementType,
            Period = entity.Period,
            TargetValue = entity.TargetValue,
            MinValue = entity.MinValue,
            MaxValue = entity.MaxValue,
            Unit = entity.Unit,
            StartDate = entity.StartDate,
            DueDate = entity.DueDate,
            ProgressPercent = entity.ProgressPercent,
            SubmittedToManagerId = entity.SubmittedToManagerId,
            ManagerName = entity.Manager?.FullName,
            SubmittedDate = entity.SubmittedDate,
            ApprovalDate = entity.ApprovalDate,
            ManagerFeedback = entity.ManagerFeedback,
            IsLocked = entity.IsLocked,
            LockedDate = entity.LockedDate,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static EmployeeGoal ToEntity(this CreateEmployeeGoalDto dto)
    {
        return new EmployeeGoal
        {
            EmployeeId = dto.EmployeeId,
            AppraisalCycleId = dto.AppraisalCycleId,
            PerformanceAppraisalId = dto.PerformanceAppraisalId,
            CompanyGoalId = dto.CompanyGoalId,
            UnitGoalId = dto.UnitGoalId,
            ParentGoalId = dto.ParentGoalId,
            ParentType = dto.CompanyGoalId.HasValue ? GoalParentType.Company
                       : dto.UnitGoalId.HasValue    ? GoalParentType.Unit
                       : (GoalParentType?)null,
            GoalLibraryId = dto.GoalLibraryId,
            KpiDefinitionId = dto.KpiDefinitionId,
            Title = dto.Title,
            Description = dto.Description,
            SuccessCriteria = dto.SuccessCriteria,
            Weight = dto.Weight,
            Priority = dto.Priority,
            MeasurementType = dto.MeasurementType,
            Period = dto.Period,
            TargetValue = dto.TargetValue,
            MinValue = dto.MinValue,
            MaxValue = dto.MaxValue,
            Unit = dto.Unit,
            StartDate = dto.StartDate,
            DueDate = dto.DueDate,
            SubmittedToManagerId = dto.SubmittedToManagerId
        };
    }

    public static void UpdateEntity(this UpdateEmployeeGoalDto dto, EmployeeGoal entity)
    {
        entity.EmployeeId = dto.EmployeeId;
        entity.AppraisalCycleId = dto.AppraisalCycleId;
        entity.PerformanceAppraisalId = dto.PerformanceAppraisalId;
        entity.CompanyGoalId = dto.CompanyGoalId;
        entity.UnitGoalId = dto.UnitGoalId;
        entity.ParentGoalId = dto.ParentGoalId;
        entity.ParentType = dto.CompanyGoalId.HasValue ? GoalParentType.Company
                          : dto.UnitGoalId.HasValue    ? GoalParentType.Unit
                          : (GoalParentType?)null;
        entity.KpiDefinitionId = dto.KpiDefinitionId;
        entity.Title = dto.Title;
        entity.Description = dto.Description;
        entity.SuccessCriteria = dto.SuccessCriteria;
        entity.Weight = dto.Weight;
        entity.Priority = dto.Priority;
        entity.MeasurementType = dto.MeasurementType;
        entity.Period = dto.Period;
        entity.TargetValue = dto.TargetValue;
        entity.MinValue = dto.MinValue;
        entity.MaxValue = dto.MaxValue;
        entity.Unit = dto.Unit;
        entity.StartDate = dto.StartDate;
        entity.DueDate = dto.DueDate;
        entity.ProgressPercent = dto.ProgressPercent;

        // Status, SubmittedToManagerId and ManagerFeedback are deliberately NOT copied from the
        // update payload. They belong to the approval lifecycle, which IGoalWorkflowCommandService
        // owns: it enforces the transition table, derives the target manager from the employee's HR
        // record, and only lets the employee's direct manager approve or reject. Assigning them here
        // let any caller PUT `status: "Approved"` onto their own draft and skip all of that.
        // Everything above is goal content, which the owner may edit until the goal is locked.
    }

    public static List<EmployeeGoalDto> ToDtoList(this IEnumerable<EmployeeGoal> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    #endregion

    #region GoalProgressEntry

    public static GoalProgressEntryDto ToDto(this GoalProgressEntry entity)
    {
        return new GoalProgressEntryDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            EmployeeGoalId = entity.EmployeeGoalId,
            GoalTitle = entity.EmployeeGoal?.Title ?? string.Empty,
            ProgressPercent = entity.ProgressPercent,
            ActualValue = entity.ActualValue,
            Status = entity.Status,
            Challenges = entity.Challenges,
            Notes = entity.Notes,
            RecordedById = entity.RecordedById,
            RecordedByName = entity.RecordedBy?.FullName ?? string.Empty,
            EntryDate = entity.EntryDate,
            ReviewEventId = entity.ReviewEventId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static GoalProgressEntry ToEntity(this CreateGoalProgressEntryDto dto)
    {
        return new GoalProgressEntry
        {
            EmployeeGoalId = dto.EmployeeGoalId,
            ProgressPercent = dto.ProgressPercent,
            ActualValue = dto.ActualValue,
            Status = dto.Status,
            Challenges = dto.Challenges,
            Notes = dto.Notes,
            // RecordedById is stamped by the service from the caller's token, not mapped here.
            EntryDate = DateTime.UtcNow,
            ReviewEventId = dto.ReviewEventId
        };
    }

    public static void UpdateEntity(this UpdateGoalProgressEntryDto dto, GoalProgressEntry entity)
    {
        entity.EmployeeGoalId = dto.EmployeeGoalId;
        entity.ProgressPercent = dto.ProgressPercent;
        entity.ActualValue = dto.ActualValue;
        entity.Status = dto.Status;
        entity.Challenges = dto.Challenges;
        entity.Notes = dto.Notes;
        entity.ReviewEventId = dto.ReviewEventId;
    }

    public static List<GoalProgressEntryDto> ToDtoList(this IEnumerable<GoalProgressEntry> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    #endregion

    #region CheckIn

    public static CheckInDto ToDto(this CheckIn entity)
    {
        return new CheckInDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            AppraisalCycleId = entity.AppraisalCycleId,
            CycleCode = entity.Cycle?.CycleCode,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            ConductedById = entity.ConductedById,
            ConductedByName = entity.ConductedBy?.FullName ?? string.Empty,
            CheckInType = entity.CheckInType,
            Title = entity.Title,
            ScheduledDate = entity.ScheduledDate,
            ConductedDate = entity.ConductedDate,
            Agenda = entity.Agenda,
            SharedNotes = entity.SharedNotes,
            PrivateNotes = entity.PrivateNotes,
            ActionItems = entity.ActionItems,
            FollowUpDate = entity.FollowUpDate,
            EmployeeComments = entity.EmployeeComments,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static CheckIn ToEntity(this CreateCheckInDto dto)
    {
        return new CheckIn
        {
            AppraisalCycleId = dto.AppraisalCycleId,
            EmployeeId = dto.EmployeeId,
            ConductedById = dto.ConductedById,
            CheckInType = dto.CheckInType,
            Title = dto.Title,
            ScheduledDate = dto.ScheduledDate,
            Agenda = dto.Agenda
        };
    }

    public static void UpdateEntity(this UpdateCheckInDto dto, CheckIn entity)
    {
        entity.AppraisalCycleId = dto.AppraisalCycleId;
        entity.EmployeeId = dto.EmployeeId;
        entity.ConductedById = dto.ConductedById;
        entity.CheckInType = dto.CheckInType;
        entity.Title = dto.Title;
        entity.ScheduledDate = dto.ScheduledDate;
        entity.ConductedDate = dto.ConductedDate;
        entity.Agenda = dto.Agenda;
        entity.SharedNotes = dto.SharedNotes;
        entity.PrivateNotes = dto.PrivateNotes;
        entity.ActionItems = dto.ActionItems;
        entity.FollowUpDate = dto.FollowUpDate;
        entity.EmployeeComments = dto.EmployeeComments;
    }

    public static List<CheckInDto> ToDtoList(this IEnumerable<CheckIn> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    #endregion

    #region CheckInGoalUpdate

    public static CheckInGoalUpdateDto ToDto(this CheckInGoalUpdate entity)
    {
        return new CheckInGoalUpdateDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CheckInId = entity.CheckInId,
            EmployeeGoalId = entity.EmployeeGoalId,
            GoalTitle = entity.EmployeeGoal?.Title ?? string.Empty,
            UpdatedProgress = entity.UpdatedProgress,
            UpdatedStatus = entity.UpdatedStatus,
            FlaggedAtRisk = entity.FlaggedAtRisk,
            Note = entity.Note,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static CheckInGoalUpdate ToEntity(this CreateCheckInGoalUpdateDto dto)
    {
        return new CheckInGoalUpdate
        {
            CheckInId = dto.CheckInId,
            EmployeeGoalId = dto.EmployeeGoalId,
            UpdatedProgress = dto.UpdatedProgress,
            UpdatedStatus = dto.UpdatedStatus,
            FlaggedAtRisk = dto.FlaggedAtRisk,
            Note = dto.Note
        };
    }

    public static void UpdateEntity(this UpdateCheckInGoalUpdateDto dto, CheckInGoalUpdate entity)
    {
        entity.CheckInId = dto.CheckInId;
        entity.EmployeeGoalId = dto.EmployeeGoalId;
        entity.UpdatedProgress = dto.UpdatedProgress;
        entity.UpdatedStatus = dto.UpdatedStatus;
        entity.FlaggedAtRisk = dto.FlaggedAtRisk;
        entity.Note = dto.Note;
    }

    public static List<CheckInGoalUpdateDto> ToDtoList(this IEnumerable<CheckInGoalUpdate> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    #endregion

    #region PerformanceJournalEntry

    public static PerformanceJournalEntryDto ToDto(this PerformanceJournalEntry entity)
    {
        return new PerformanceJournalEntryDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            AppraisalCycleId = entity.AppraisalCycleId,
            CycleCode = entity.AppraisalCycle?.CycleCode,
            OwnerId = entity.OwnerId,
            OwnerName = entity.Owner?.FullName ?? string.Empty,
            SubjectEmployeeId = entity.SubjectEmployeeId,
            SubjectEmployeeName = entity.SubjectEmployee?.FullName,
            RelatedGoalId = entity.RelatedGoalId,
            RelatedGoalTitle = entity.RelatedGoal?.Title,
            Title = entity.Title,
            Body = entity.Body,
            EntryDate = entity.EntryDate,
            IsPrivate = entity.IsPrivate,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static PerformanceJournalEntry ToEntity(this CreatePerformanceJournalEntryDto dto)
    {
        return new PerformanceJournalEntry
        {
            AppraisalCycleId = dto.AppraisalCycleId,
            // OwnerId is stamped by the service from the caller's token, not mapped from the payload.
            SubjectEmployeeId = dto.SubjectEmployeeId,
            RelatedGoalId = dto.RelatedGoalId,
            Title = dto.Title,
            Body = dto.Body,
            EntryDate = dto.EntryDate,
            IsPrivate = dto.IsPrivate
        };
    }

    public static void UpdateEntity(this UpdatePerformanceJournalEntryDto dto, PerformanceJournalEntry entity)
    {
        entity.AppraisalCycleId = dto.AppraisalCycleId;
        entity.SubjectEmployeeId = dto.SubjectEmployeeId;
        entity.RelatedGoalId = dto.RelatedGoalId;
        entity.Title = dto.Title;
        entity.Body = dto.Body;
        // The client's update payload has never carried EntryDate, so an unguarded copy
        // stamped default(DateTime) over the real date on every edit — sinking the entry to
        // year 0001 in the date-ordered lists and outside every date-range filter.
        if (dto.EntryDate != default)
            entity.EntryDate = dto.EntryDate;
        entity.IsPrivate = dto.IsPrivate;
    }

    public static List<PerformanceJournalEntryDto> ToDtoList(this IEnumerable<PerformanceJournalEntry> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    #endregion

    #region EmployeeDevelopmentPlan

    public static EmployeeDevelopmentPlanDto ToDto(this EmployeeDevelopmentPlan entity)
    {
        // Objectives are eagerly loaded by every read in DevelopmentPlanService and were then
        // thrown away. The rollup is what a plan list is actually asking about.
        var objectives = (entity.Objectives ?? new List<EmployeeDevelopmentObjective>())
            .Where(o => !o.IsDeleted)
            .ToList();

        return new EmployeeDevelopmentPlanDto
        {
            ObjectiveCount = objectives.Count,
            CompletedObjectiveCount = objectives.Count(o => o.ObjectiveStatus == DevelopmentObjectiveStatus.Completed),
            AverageProgressPercent = objectives.Count == 0
                ? 0m
                : Math.Round(objectives.Average(o => o.ProgressPercent), 2),
            Id = entity.Id,
            TenantId = entity.TenantId,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            AppraisalCycleId = entity.AppraisalCycleId,
            CycleCode = entity.Cycle?.CycleCode,
            CycleName = entity.Cycle?.CycleName,
            Title = entity.Title,
            StartDate = entity.StartDate,
            EndDate = entity.EndDate,
            PlanStatus = entity.PlanStatus,
            OverallNotes = entity.OverallNotes,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static EmployeeDevelopmentPlan ToEntity(this CreateEmployeeDevelopmentPlanDto dto)
    {
        return new EmployeeDevelopmentPlan
        {
            EmployeeId = dto.EmployeeId,
            AppraisalCycleId = dto.AppraisalCycleId,
            Title = dto.Title,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            PlanStatus = dto.PlanStatus,
            OverallNotes = dto.OverallNotes
        };
    }

    public static void UpdateEntity(this UpdateEmployeeDevelopmentPlanDto dto, EmployeeDevelopmentPlan entity)
    {
        entity.EmployeeId = dto.EmployeeId;
        entity.AppraisalCycleId = dto.AppraisalCycleId;
        entity.Title = dto.Title;
        entity.StartDate = dto.StartDate;
        entity.EndDate = dto.EndDate;
        entity.PlanStatus = dto.PlanStatus;
        entity.OverallNotes = dto.OverallNotes;
    }

    public static List<EmployeeDevelopmentPlanDto> ToDtoList(this IEnumerable<EmployeeDevelopmentPlan> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    #endregion

    #region EmployeeDevelopmentObjective

    public static EmployeeDevelopmentObjectiveDto ToDto(this EmployeeDevelopmentObjective entity)
    {
        return new EmployeeDevelopmentObjectiveDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            DevelopmentPlanId = entity.DevelopmentPlanId,
            Title = entity.Title,
            Description = entity.Description,
            Actions = entity.Actions,
            TargetDate = entity.TargetDate,
            ProgressPercent = entity.ProgressPercent,
            ProgressNotes = entity.ProgressNotes,
            ObjectiveStatus = entity.ObjectiveStatus,
            UpdatedInReviewEventId = entity.UpdatedInReviewEventId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static EmployeeDevelopmentObjective ToEntity(this CreateEmployeeDevelopmentObjectiveDto dto)
    {
        return new EmployeeDevelopmentObjective
        {
            DevelopmentPlanId = dto.DevelopmentPlanId,
            Title = dto.Title,
            Description = dto.Description,
            Actions = dto.Actions,
            TargetDate = dto.TargetDate,
            ObjectiveStatus = dto.ObjectiveStatus
        };
    }

    public static void UpdateEntity(this UpdateEmployeeDevelopmentObjectiveDto dto, EmployeeDevelopmentObjective entity)
    {
        entity.DevelopmentPlanId = dto.DevelopmentPlanId;
        entity.Title = dto.Title;
        entity.Description = dto.Description;
        entity.Actions = dto.Actions;
        entity.TargetDate = dto.TargetDate;
        entity.ProgressPercent = dto.ProgressPercent;
        entity.ProgressNotes = dto.ProgressNotes;
        entity.ObjectiveStatus = dto.ObjectiveStatus;
        entity.UpdatedInReviewEventId = dto.UpdatedInReviewEventId;
    }

    public static List<EmployeeDevelopmentObjectiveDto> ToDtoList(this IEnumerable<EmployeeDevelopmentObjective> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    #endregion

    #region AppraisalReviewEvent

    public static AppraisalReviewEventDto ToDto(this AppraisalReviewEvent entity)
    {
        return new AppraisalReviewEventDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            AppraisalCycleId = entity.AppraisalCycleId,
            CycleCode = entity.Cycle?.CycleCode,
            PerformanceAppraisalId = entity.PerformanceAppraisalId,
            AppraisalNumber = entity.Appraisal?.AppraisalNumber,
            EmployeeId = entity.Appraisal?.EmployeeId ?? Guid.Empty,
            EmployeeName = entity.Appraisal?.Employee?.FullName,
            Type = entity.Type,
            EventDate = entity.EventDate,
            Status = entity.Status,
            IsLightTouch = entity.IsLightTouch,
            IsFullAppraisal = entity.IsFullAppraisal,
            OverallPeriodScore = entity.OverallPeriodScore,
            AchievementsSummary = entity.AchievementsSummary,
            ChallengesSummary = entity.ChallengesSummary,
            Notes = entity.Notes,
            ManagerNotes = entity.ManagerNotes,
            ConversationId = entity.ConversationId,
            UpdatedDevelopmentPlanId = entity.UpdatedDevelopmentPlanId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static AppraisalReviewEvent ToEntity(this CreateAppraisalReviewEventDto dto)
    {
        return new AppraisalReviewEvent
        {
            AppraisalCycleId = dto.AppraisalCycleId,
            PerformanceAppraisalId = dto.PerformanceAppraisalId,
            Type = dto.Type,
            EventDate = dto.EventDate,
            IsLightTouch = dto.IsLightTouch,
            IsFullAppraisal = dto.IsFullAppraisal
        };
    }

    public static void UpdateEntity(this UpdateAppraisalReviewEventDto dto, AppraisalReviewEvent entity)
    {
        // AppraisalCycleId, PerformanceAppraisalId, Status and OverallPeriodScore are deliberately
        // NOT copied from the payload. The first two would re-point the event at a different
        // employee's appraisal; the last two are owned by submit / complete / finalize, and a plain
        // PUT carrying them was a way to mark a review Completed — or award a period score — without
        // passing any of the gates those operations enforce.
        entity.Type = dto.Type;
        entity.EventDate = dto.EventDate;
        entity.IsLightTouch = dto.IsLightTouch;
        entity.IsFullAppraisal = dto.IsFullAppraisal;
        // Use null-coalescing so only non-null values overwrite — prevents one role from
        // accidentally clearing fields owned by the other role on a full-PUT save.
        entity.AchievementsSummary = dto.AchievementsSummary ?? entity.AchievementsSummary;
        entity.ChallengesSummary = dto.ChallengesSummary ?? entity.ChallengesSummary;
        entity.Notes = dto.Notes ?? entity.Notes;
        entity.ManagerNotes = dto.ManagerNotes ?? entity.ManagerNotes;
        entity.ConversationId = dto.ConversationId ?? entity.ConversationId;
        entity.UpdatedDevelopmentPlanId = dto.UpdatedDevelopmentPlanId;
    }

    public static List<AppraisalReviewEventDto> ToDtoList(this IEnumerable<AppraisalReviewEvent> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    #endregion

    #region AppraisalConversation

    public static AppraisalConversationDto ToDto(this AppraisalConversation entity)
    {
        return new AppraisalConversationDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            AppraisalId = entity.AppraisalId,
            AppraisalNumber = entity.Appraisal?.AppraisalNumber,
            ScheduledById = entity.ScheduledById,
            ScheduledByName = entity.ScheduledBy?.FullName,
            ConductedById = entity.ConductedById,
            ConductedByName = entity.ConductedBy?.FullName,
            Type = entity.Type,
            ScheduledDate = entity.ScheduledDate,
            HeldDate = entity.HeldDate,
            Agenda = entity.Agenda,
            PostMeetingNotes = entity.PostMeetingNotes,
            KeyTakeaways = entity.KeyTakeaways,
            IsCompleted = entity.IsCompleted,
            ReviewEventId = entity.ReviewEventId,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static AppraisalConversation ToEntity(this CreateAppraisalConversationDto dto)
    {
        return new AppraisalConversation
        {
            AppraisalId   = dto.AppraisalId,
            ReviewEventId = dto.ReviewEventId,
            ScheduledById = dto.ScheduledById,
            ConductedById = dto.ConductedById,
            Type          = dto.Type,
            ScheduledDate = dto.ScheduledDate,
            Agenda        = dto.Agenda
        };
    }

    public static void UpdateEntity(this UpdateAppraisalConversationDto dto, AppraisalConversation entity)
    {
        entity.AppraisalId = dto.AppraisalId;
        entity.ScheduledById = dto.ScheduledById;
        entity.ConductedById = dto.ConductedById;
        entity.Type = dto.Type;
        entity.ScheduledDate = dto.ScheduledDate;
        entity.HeldDate = dto.HeldDate;
        entity.Agenda = dto.Agenda;
        entity.PostMeetingNotes = dto.PostMeetingNotes;
        entity.KeyTakeaways = dto.KeyTakeaways;
        entity.IsCompleted = dto.IsCompleted;
        entity.ReviewEventId = dto.ReviewEventId;
    }

    public static List<AppraisalConversationDto> ToDtoList(this IEnumerable<AppraisalConversation> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    #endregion

    #region AppraisalHRReview

    public static AppraisalHRReviewDto ToDto(this AppraisalHRReview entity)
    {
        return new AppraisalHRReviewDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            AppraisalId = entity.AppraisalId,
            AppraisalNumber = entity.Appraisal?.AppraisalNumber,
            ReviewedByHRId = entity.ReviewedByHRId,
            ReviewedByHRName = entity.ReviewedByHR?.FullName ?? string.Empty,
            ReviewStartedDate = entity.ReviewStartedDate,
            ReviewCompletedDate = entity.ReviewCompletedDate,
            IsApproved = entity.IsApproved,
            HRNotes = entity.HRNotes,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static AppraisalHRReview ToEntity(this CreateAppraisalHRReviewDto dto)
    {
        return new AppraisalHRReview
        {
            AppraisalId = dto.AppraisalId,
            ReviewedByHRId = dto.ReviewedByHRId,
            ReviewStartedDate = dto.ReviewStartedDate
        };
    }

    public static void UpdateEntity(this UpdateAppraisalHRReviewDto dto, AppraisalHRReview entity)
    {
        entity.AppraisalId = dto.AppraisalId;
        entity.ReviewedByHRId = dto.ReviewedByHRId;
        entity.ReviewStartedDate = dto.ReviewStartedDate;
        entity.ReviewCompletedDate = dto.ReviewCompletedDate;
        entity.IsApproved = dto.IsApproved;
        entity.HRNotes = dto.HRNotes;
    }

    public static List<AppraisalHRReviewDto> ToDtoList(this IEnumerable<AppraisalHRReview> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    #endregion

    #region CalibrationSession

    public static CalibrationSessionDto ToDto(this CalibrationSession entity)
    {
        return new CalibrationSessionDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            AppraisalCycleId = entity.AppraisalCycleId,
            CycleCode = entity.AppraisalCycle?.CycleCode,
            SessionName = entity.SessionName,
            OrganizationLevelId = entity.OrganizationLevelId,
            OrganizationLevelName = entity.OrganizationLevel?.Name,
            OrganizationUnitId = entity.OrganizationUnitId,
            OrganizationUnitName = entity.OrganizationUnit?.Name,
            Status = entity.Status,
            ScheduledDate = entity.ScheduledDate,
            StartedDate = entity.StartedDate,
            CompletedDate = entity.CompletedDate,
            FacilitatedById = entity.FacilitatedById,
            FacilitatedByName = entity.FacilitatedBy?.FullName,
            CompletedById = entity.CompletedById,
            CompletedByName = entity.CompletedBy?.FullName,
            Agenda = entity.Agenda,
            MeetingNotes = entity.MeetingNotes,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static CalibrationSession ToEntity(this CreateCalibrationSessionDto dto)
    {
        return new CalibrationSession
        {
            AppraisalCycleId = dto.AppraisalCycleId,
            SessionName = dto.SessionName,
            OrganizationLevelId = dto.OrganizationLevelId,
            OrganizationUnitId = dto.OrganizationUnitId,
            ScheduledDate = dto.ScheduledDate,
            FacilitatedById = dto.FacilitatedById,
            Agenda = dto.Agenda
        };
    }

    public static void UpdateEntity(this UpdateCalibrationSessionDto dto, CalibrationSession entity)
    {
        entity.AppraisalCycleId = dto.AppraisalCycleId;
        entity.SessionName = dto.SessionName;
        entity.OrganizationLevelId = dto.OrganizationLevelId;
        entity.OrganizationUnitId = dto.OrganizationUnitId;
        entity.ScheduledDate = dto.ScheduledDate;
        entity.Agenda = dto.Agenda;
        entity.MeetingNotes = dto.MeetingNotes;

        // ⚠ Only reassign the facilitator when one is actually named. Opening a session records
        // who opened it, and a later edit of the session's name or agenda does not mention the
        // facilitator — so overwriting unconditionally silently blanked the record of who ran it.
        if (dto.FacilitatedById.HasValue)
            entity.FacilitatedById = dto.FacilitatedById;

        // Status and the started/completed stamps are set only by the lifecycle endpoints.
    }

    public static List<CalibrationSessionDto> ToDtoList(this IEnumerable<CalibrationSession> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    #endregion

    #region CalibrationParticipant

    public static CalibrationParticipantDto ToDto(this CalibrationParticipant entity)
    {
        return new CalibrationParticipantDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CalibrationSessionId = entity.CalibrationSessionId,
            SessionName = entity.CalibrationSession?.SessionName ?? string.Empty,
            EmployeeId = entity.EmployeeId,
            EmployeeName = entity.Employee?.FullName ?? string.Empty,
            Role = entity.Role,
            Attended = entity.Attended,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static CalibrationParticipant ToEntity(this CreateCalibrationParticipantDto dto)
    {
        return new CalibrationParticipant
        {
            CalibrationSessionId = dto.CalibrationSessionId,
            EmployeeId = dto.EmployeeId,
            Role = dto.Role,
            Attended = dto.Attended
        };
    }

    public static void UpdateEntity(this UpdateCalibrationParticipantDto dto, CalibrationParticipant entity)
    {
        entity.CalibrationSessionId = dto.CalibrationSessionId;
        entity.EmployeeId = dto.EmployeeId;
        entity.Role = dto.Role;
        entity.Attended = dto.Attended;
    }

    public static List<CalibrationParticipantDto> ToDtoList(this IEnumerable<CalibrationParticipant> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    #endregion

    #region CalibrationRatingAdjustment

    public static CalibrationRatingAdjustmentDto ToDto(this CalibrationRatingAdjustment entity)
    {
        return new CalibrationRatingAdjustmentDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CalibrationSessionId = entity.CalibrationSessionId,
            SessionName = entity.CalibrationSession?.SessionName ?? string.Empty,
            PerformanceAppraisalId = entity.PerformanceAppraisalId,
            AppraisalNumber = entity.PerformanceAppraisal?.AppraisalNumber,
            EmployeeName = entity.PerformanceAppraisal?.Employee?.FullName,
            TemplateItemId = entity.TemplateItemId,
            TemplateItemName = entity.TemplateItem?.Competency?.CriteriaName ?? entity.TemplateItem?.KpiDefinition?.KpiName,
            OriginalScore = entity.OriginalScore,
            AdjustedScore = entity.AdjustedScore,
            AdjustedById = entity.AdjustedById,
            AdjustedByName = entity.AdjustedBy?.FullName ?? string.Empty,
            AdjustmentDate = entity.AdjustmentDate,
            Rationale = entity.Rationale,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    /// <summary>The session id and the adjuster are supplied by the service, from the route and the token.</summary>
    public static CalibrationRatingAdjustment ToEntity(this CreateCalibrationRatingAdjustmentDto dto)
    {
        return new CalibrationRatingAdjustment
        {
            PerformanceAppraisalId = dto.PerformanceAppraisalId,
            TemplateItemId = dto.TemplateItemId,
            // Until goal rows exist (lane L), no item still means the overall — the rule the
            // batch-1 backfill wrote, kept true for every adjustment written after it.
            IsOverall = !dto.TemplateItemId.HasValue,
            OriginalScore = dto.OriginalScore,
            AdjustedScore = dto.AdjustedScore,
            AdjustmentDate = DateTime.UtcNow,
            Rationale = dto.Rationale
        };
    }

    public static void UpdateEntity(this UpdateCalibrationRatingAdjustmentDto dto, CalibrationRatingAdjustment entity)
    {
        entity.PerformanceAppraisalId = dto.PerformanceAppraisalId;
        entity.TemplateItemId = dto.TemplateItemId;
        entity.IsOverall = !dto.TemplateItemId.HasValue;
        entity.OriginalScore = dto.OriginalScore;
        entity.AdjustedScore = dto.AdjustedScore;
        entity.Rationale = dto.Rationale;
    }

    public static List<CalibrationRatingAdjustmentDto> ToDtoList(this IEnumerable<CalibrationRatingAdjustment> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    #endregion

    #region PipGoal

    public static PipGoalDto ToDto(this PipGoal entity)
    {
        return new PipGoalDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            PipId = entity.PipId,
            PipNumber = entity.Pip?.PipNumber ?? string.Empty,
            Title = entity.Title,
            Description = entity.Description,
            SuccessCriteria = entity.SuccessCriteria,
            DueDate = entity.DueDate,
            Status = entity.Status,
            ProgressPercent = entity.ProgressPercent,
            ProgressNotes = entity.ProgressNotes,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static PipGoal ToEntity(this CreatePipGoalDto dto)
    {
        return new PipGoal
        {
            PipId = dto.PipId,
            Title = dto.Title,
            Description = dto.Description,
            SuccessCriteria = dto.SuccessCriteria,
            DueDate = dto.DueDate,
            Status = dto.Status
        };
    }

    public static void UpdateEntity(this UpdatePipGoalDto dto, PipGoal entity)
    {
        entity.PipId = dto.PipId;
        entity.Title = dto.Title;
        entity.Description = dto.Description;
        entity.SuccessCriteria = dto.SuccessCriteria;
        entity.DueDate = dto.DueDate;
        entity.Status = dto.Status;
        entity.ProgressPercent = dto.ProgressPercent;
        entity.ProgressNotes = dto.ProgressNotes;
    }

    public static List<PipGoalDto> ToDtoList(this IEnumerable<PipGoal> entities)
    {
        return entities.Select(e => e.ToDto()).ToList();
    }

    #endregion

    #region PipAttachment

    public static PipAttachmentDto ToPipAttachmentDto(this AppraisalAttachment entity)
    {
        return new PipAttachmentDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            PipId = entity.PipId ?? Guid.Empty,
            FileName = entity.FileName,
            FilePath = entity.FilePath,
            PublicUrl = null,
            FileSizeBytes = entity.FileSizeBytes,
            Description = entity.Description,
            UploadDate = entity.UploadDate,
            UploadedById = entity.UploadedById,
            UploadedByName = entity.UploadedBy?.FullName ?? string.Empty,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
        };
    }

    public static List<PipAttachmentDto> ToPipAttachmentDtoList(this IEnumerable<AppraisalAttachment> entities)
    {
        return entities.Select(e => e.ToPipAttachmentDto()).ToList();
    }

    #endregion

    #region Snapshots (read-only ToDto)

    public static AppraisalEvaluationSnapshotDto ToDto(this AppraisalEvaluationSnapshot entity)
    {
        return new AppraisalEvaluationSnapshotDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            AppraisalId = entity.AppraisalId,
            EvaluatorId = entity.EvaluatorId,
            EvaluatorName = entity.Evaluator?.FullName ?? string.Empty,
            EvaluatorRole = entity.EvaluatorRole,
            TotalScore = entity.TotalScore,
            SnapshotDate = entity.SnapshotDate,
            SnapshotReason = entity.SnapshotReason,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static AppraisalCriterionScoreSnapshotDto ToDto(this AppraisalCriterionScoreSnapshot entity)
    {
        return new AppraisalCriterionScoreSnapshotDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            AppraisalEvaluationSnapshotId = entity.AppraisalEvaluationSnapshotId,
            TemplateItemId = entity.TemplateItemId,
            ItemName = entity.TemplateItem?.Competency?.CriteriaName ?? entity.TemplateItem?.KpiDefinition?.KpiName,
            NumericScore = entity.NumericScore,
            WeightedScore = entity.WeightedScore,
            Notes = entity.Notes,
            KpiTargetValue = entity.KpiTargetValue,
            KpiMinValue = entity.KpiMinValue,
            KpiMaxValue = entity.KpiMaxValue,
            KpiTargetSource = entity.KpiTargetSource,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    public static AppraisalKpiEvaluationSnapshotDto ToDto(this AppraisalKpiEvaluationSnapshot entity)
    {
        return new AppraisalKpiEvaluationSnapshotDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            AppraisalCriterionScoreSnapshotId = entity.AppraisalCriterionScoreSnapshotId,
            ActualValue = entity.ActualValue,
            AchievementPercent = entity.AchievementPercent,
            Notes = entity.Notes,
            EvidenceLinks = entity.EvidenceLinks,
            SnapshotDate = entity.SnapshotDate,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy ?? string.Empty,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy
        };
    }

    #endregion

    #region EmployeeDevelopmentPlanFeedback

    public static EmployeeDevelopmentPlanFeedbackDto ToDto(this EmployeeDevelopmentPlanFeedback entity)
    {
        return new EmployeeDevelopmentPlanFeedbackDto
        {
            Id            = entity.Id,
            TenantId      = entity.TenantId,
            DevelopmentPlanId = entity.DevelopmentPlanId,
            ManagerId     = entity.ManagerId,
            FeedbackType  = entity.FeedbackType,
            Comment       = entity.Comment,
            CreatedAt     = entity.CreatedAt,
            CreatedBy     = entity.CreatedBy ?? string.Empty
        };
    }

    public static EmployeeDevelopmentPlanFeedback ToEntity(this CreateEmployeeDevelopmentPlanFeedbackDto dto, Guid tenantId)
    {
        return new EmployeeDevelopmentPlanFeedback
        {
            DevelopmentPlanId = dto.DevelopmentPlanId,
            ManagerId         = dto.ManagerId,
            FeedbackType      = dto.FeedbackType,
            Comment           = dto.Comment,
            TenantId          = tenantId
        };
    }

    public static List<EmployeeDevelopmentPlanFeedbackDto> ToDtoList(this IEnumerable<EmployeeDevelopmentPlanFeedback> entities)
        => entities.Select(e => e.ToDto()).ToList();

    #endregion
}
