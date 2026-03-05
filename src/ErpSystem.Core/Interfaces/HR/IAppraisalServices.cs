using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

#region Appraisal Grade Definition

public interface IAppraisalGradeDefinitionService
{
    Task<AppraisalGradeDefinitionDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<AppraisalGradeDefinitionDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<AppraisalGradeDefinitionDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<AppraisalGradeDefinitionDto> CreateAsync(CreateAppraisalGradeDefinitionDto createDto, CancellationToken cancellationToken = default);
    Task<AppraisalGradeDefinitionDto> UpdateAsync(UpdateAppraisalGradeDefinitionDto updateDto, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

#endregion AppraisalGradeDefinition

#region Kpi Definition

public interface IKpiDefinitionService
{
    Task<KpiDefinitionDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<KpiDefinitionDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<KpiDefinitionDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<KpiDefinitionDto> CreateAsync(CreateKpiDefinitionDto createDto, CancellationToken cancellationToken = default);
    Task<KpiDefinitionDto> UpdateAsync(UpdateKpiDefinitionDto updateDto, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

#endregion Kpi Definition

#region Appraisal Criteria

public interface IAppraisalCriteriaService
{
    Task<AppraisalCriteriaDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<AppraisalCriteriaDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<AppraisalCriteriaDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<AppraisalCriteriaDto> CreateAsync(CreateAppraisalCriteriaDto createDto, CancellationToken cancellationToken = default);
    Task<AppraisalCriteriaDto> UpdateAsync(UpdateAppraisalCriteriaDto updateDto, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

#endregion Appraisal Criteria

#region Performance Appraisal

public interface IPerformanceAppraisalService
{
    Task<PerformanceAppraisalDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<PerformanceAppraisalDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<PerformanceAppraisalDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<IEnumerable<PerformanceAppraisalDto>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<IEnumerable<PerformanceAppraisalDto>> GetByYearAsync(int year, CancellationToken cancellationToken = default);
    Task<IEnumerable<PerformanceAppraisalDto>> GetByStatusAsync(AppraisalStatus status, CancellationToken cancellationToken = default);
    Task<PerformanceAppraisalDto> CreateAsync(CreatePerformanceAppraisalDto createDto, CancellationToken cancellationToken = default);
    Task<PerformanceAppraisalDto> UpdateAsync(UpdatePerformanceAppraisalDto updateDto, CancellationToken cancellationToken = default);
    Task<bool> UpdateStatusAsync(UpdateAppraisalStatusDto statusDto, CancellationToken cancellationToken = default);
    Task<bool> FileAppealAsync(FileAppraisalAppealDto appealDto, CancellationToken cancellationToken = default);
    Task<bool> ResolveAppealAsync(ResolveAppraisalAppealDto resolveDto, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> CalculateOverallScoreAsync(Guid appraisalId, CancellationToken cancellationToken = default);

    // EvaluatorEvaluation operations
    Task<EvaluatorEvaluationDto> AddEvaluatorEvaluationAsync(Guid appraisalId, CreateEvaluatorEvaluationDto createDto, CancellationToken cancellationToken = default);
    Task<IEnumerable<EvaluatorEvaluationDto>> GetEvaluatorEvaluationsAsync(Guid appraisalId, CancellationToken cancellationToken = default);
    Task<EvaluatorEvaluationDto> UpdateEvaluatorEvaluationAsync(Guid appraisalId, UpdateEvaluatorEvaluationDto updateDto, CancellationToken cancellationToken = default);
    Task<bool> DeleteEvaluatorEvaluationAsync(Guid appraisalId, Guid evaluationId, CancellationToken cancellationToken = default);

    // CriterionScore operations
    Task<CriterionScoreDto> AddCriterionScoreAsync(Guid evaluationId, CreateCriterionScoreDto createDto, CancellationToken cancellationToken = default);
    Task<IEnumerable<CriterionScoreDto>> GetCriterionScoresAsync(Guid evaluationId, CancellationToken cancellationToken = default);
    Task<CriterionScoreDto> UpdateCriterionScoreAsync(Guid evaluationId, UpdateCriterionScoreDto updateDto, CancellationToken cancellationToken = default);
    Task<bool> DeleteCriterionScoreAsync(Guid evaluationId, Guid scoreId, CancellationToken cancellationToken = default);

    // AppraisalEmployeeResponse operations
    Task<AppraisalEmployeeResponseDto> AddEmployeeResponseAsync(Guid appraisalId, CreateAppraisalEmployeeResponseDto createDto, CancellationToken cancellationToken = default);
    Task<IEnumerable<AppraisalEmployeeResponseDto>> GetEmployeeResponsesAsync(Guid appraisalId, CancellationToken cancellationToken = default);

    // AppraisalAttachment operations
    Task<AppraisalAttachmentDto> AddAttachmentAsync(Guid appraisalId, CreateAppraisalAttachmentDto createDto, CancellationToken cancellationToken = default);
    Task<IEnumerable<AppraisalAttachmentDto>> GetAttachmentsAsync(Guid appraisalId, CancellationToken cancellationToken = default);
}

#endregion Performance Appraisal

#region Performance Improvement Plan

public interface IPerformanceImprovementPlanService
{
    Task<PerformanceImprovementPlanDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<PerformanceImprovementPlanDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<PerformanceImprovementPlanDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<IEnumerable<PerformanceImprovementPlanDto>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<IEnumerable<PerformanceImprovementPlanDto>> GetByStatusAsync(PipStatus status, CancellationToken cancellationToken = default);
    Task<IEnumerable<PerformanceImprovementPlanDto>> GetActivePipsAsync(CancellationToken cancellationToken = default);
    Task<PerformanceImprovementPlanDto> CreateAsync(CreatePerformanceImprovementPlanDto createDto, CancellationToken cancellationToken = default);
    Task<PerformanceImprovementPlanDto> UpdateAsync(UpdatePerformanceImprovementPlanDto updateDto, CancellationToken cancellationToken = default);
    Task<bool> UpdateStatusAsync(UpdatePipStatusDto statusDto, CancellationToken cancellationToken = default);
    Task<bool> CompletePipAsync(CompletePipDto completeDto, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    // PipReviewMeeting operations
    Task<PipReviewMeetingDto> AddReviewMeetingAsync(Guid pipId, CreatePipReviewMeetingDto createDto, CancellationToken cancellationToken = default);
    Task<IEnumerable<PipReviewMeetingDto>> GetReviewMeetingsAsync(Guid pipId, CancellationToken cancellationToken = default);
    Task<PipReviewMeetingDto> UpdateReviewMeetingAsync(Guid pipId, UpdatePipReviewMeetingDto updateDto, CancellationToken cancellationToken = default);
    Task<bool> DeleteReviewMeetingAsync(Guid pipId, Guid meetingId, CancellationToken cancellationToken = default);
    Task<PipReviewMeetingDto> GetLatestReviewMeetingAsync(Guid pipId, CancellationToken cancellationToken = default);
}

#endregion Performance Improvement Plan

#region Position Criteria Mapping

public interface IPositionCriteriaMappingService
{
    Task<PositionCriteriaMappingDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<PositionCriteriaMappingDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<PositionCriteriaMappingDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<IEnumerable<PositionCriteriaMappingDto>> GetByPositionIdAsync(Guid positionId, CancellationToken cancellationToken = default);
    Task<IEnumerable<PositionCriteriaMappingDto>> GetByDepartmentIdAsync(Guid departmentId, CancellationToken cancellationToken = default);
    Task<PositionCriteriaMappingDto> CreateAsync(CreatePositionCriteriaMappingDto createDto, CancellationToken cancellationToken = default);
    Task<PositionCriteriaMappingDto> UpdateAsync(UpdatePositionCriteriaMappingDto updateDto, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    // MappingGradeRange operations
    Task<MappingGradeRangeDto> AddGradeRangeAsync(Guid mappingId, CreateMappingGradeRangeDto createDto, CancellationToken cancellationToken = default);
    Task<IEnumerable<MappingGradeRangeDto>> GetGradeRangesAsync(Guid mappingId, CancellationToken cancellationToken = default);
    Task<MappingGradeRangeDto> UpdateGradeRangeAsync(Guid mappingId, UpdateMappingGradeRangeDto updateDto, CancellationToken cancellationToken = default);
    Task<bool> DeleteGradeRangeAsync(Guid mappingId, Guid rangeId, CancellationToken cancellationToken = default);
}

#endregion Position Criteria Mapping

#region Mapping Grade Range

public interface IMappingGradeRangeService
{
    Task<MappingGradeRangeDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<MappingGradeRangeDto>> GetByMappingIdAsync(Guid mappingId, CancellationToken cancellationToken = default);
    Task<MappingGradeRangeDto> CreateAsync(CreateMappingGradeRangeDto createDto, CancellationToken cancellationToken = default);
    Task<MappingGradeRangeDto> UpdateAsync(UpdateMappingGradeRangeDto updateDto, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

#endregion Mapping Grade Range

#region Employee Kpi Target

public interface IEmployeeKpiTargetService
{
    Task<EmployeeKpiTargetDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<EmployeeKpiTargetDto>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<IEnumerable<EmployeeKpiTargetDto>> GetByPeriodAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default);
    Task<EmployeeKpiTargetDto> CreateAsync(CreateEmployeeKpiTargetDto createDto, CancellationToken cancellationToken = default);
    Task<EmployeeKpiTargetDto> UpdateAsync(UpdateEmployeeKpiTargetDto updateDto, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    // KpiEvaluationRecord operations
    Task<KpiEvaluationRecordDto> AddEvaluationRecordAsync(Guid targetId, CreateKpiEvaluationRecordDto createDto, CancellationToken cancellationToken = default);
    Task<IEnumerable<KpiEvaluationRecordDto>> GetEvaluationRecordsAsync(Guid targetId, CancellationToken cancellationToken = default);
    Task<KpiEvaluationRecordDto> UpdateEvaluationRecordAsync(Guid targetId, UpdateKpiEvaluationRecordDto updateDto, CancellationToken cancellationToken = default);
    Task<bool> DeleteEvaluationRecordAsync(Guid targetId, Guid recordId, CancellationToken cancellationToken = default);
    Task<KpiEvaluationRecordDto> GetFinalEvaluationAsync(Guid targetId, CancellationToken cancellationToken = default);
}

#endregion Employee Kpi Target

#region Evaluator Evaluation

public interface IEvaluatorEvaluationService
{
    Task<EvaluatorEvaluationDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<EvaluatorEvaluationDto>> GetByAppraisalIdAsync(Guid appraisalId, CancellationToken cancellationToken = default);
    Task<IEnumerable<EvaluatorEvaluationDto>> GetByEvaluatorIdAsync(Guid evaluatorId, CancellationToken cancellationToken = default);
    Task<EvaluatorEvaluationDto> CreateAsync(CreateEvaluatorEvaluationDto createDto, CancellationToken cancellationToken = default);
    Task<EvaluatorEvaluationDto> UpdateAsync(UpdateEvaluatorEvaluationDto updateDto, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

#endregion Evaluator Evaluation

#region Criterion Score

public interface ICriterionScoreService
{
    Task<CriterionScoreDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<CriterionScoreDto>> GetByEvaluationIdAsync(Guid evaluationId, CancellationToken cancellationToken = default);
    Task<CriterionScoreDto> CreateAsync(CreateCriterionScoreDto createDto, CancellationToken cancellationToken = default);
    Task<CriterionScoreDto> UpdateAsync(UpdateCriterionScoreDto updateDto, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

#endregion Criterion Score

#region Kpi Evaluation Record

public interface IKpiEvaluationRecordService
{
    Task<KpiEvaluationRecordDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<KpiEvaluationRecordDto>> GetByTargetIdAsync(Guid targetId, CancellationToken cancellationToken = default);
    Task<IEnumerable<KpiEvaluationRecordDto>> GetByEvaluatorIdAsync(Guid evaluatorId, CancellationToken cancellationToken = default);
    Task<KpiEvaluationRecordDto> CreateAsync(CreateKpiEvaluationRecordDto createDto, CancellationToken cancellationToken = default);
    Task<KpiEvaluationRecordDto> UpdateAsync(UpdateKpiEvaluationRecordDto updateDto, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

#endregion Kpi Evaluation Record

#region Appraisal Employee Response

public interface IAppraisalEmployeeResponseService
{
    Task<AppraisalEmployeeResponseDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<AppraisalEmployeeResponseDto>> GetByAppraisalIdAsync(Guid appraisalId, CancellationToken cancellationToken = default);
    Task<AppraisalEmployeeResponseDto> CreateAsync(CreateAppraisalEmployeeResponseDto createDto, CancellationToken cancellationToken = default);
    Task<AppraisalEmployeeResponseDto> UpdateAsync(UpdateAppraisalEmployeeResponseDto updateDto, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

#endregion Appraisal Employee Response

#region Appraisal Attachment

public interface IAppraisalAttachmentService
{
    Task<AppraisalAttachmentDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<AppraisalAttachmentDto>> GetByAppraisalIdAsync(Guid appraisalId, CancellationToken cancellationToken = default);
    Task<AppraisalAttachmentDto> CreateAsync(CreateAppraisalAttachmentDto createDto, CancellationToken cancellationToken = default);
    Task<AppraisalAttachmentDto> UpdateAsync(UpdateAppraisalAttachmentDto updateDto, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

#endregion Appraisal Attachment

#region Pip Review Meeting

public interface IPipReviewMeetingService
{
    Task<PipReviewMeetingDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<PipReviewMeetingDto>> GetByPipIdAsync(Guid pipId, CancellationToken cancellationToken = default);
    Task<PipReviewMeetingDto> CreateAsync(CreatePipReviewMeetingDto createDto, CancellationToken cancellationToken = default);
    Task<PipReviewMeetingDto> UpdateAsync(UpdatePipReviewMeetingDto updateDto, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

#endregion Pip Review Meeting
