using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.HR.Performance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.HR.Appraisal;

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

#region Appraisal Competency

public interface IAppraisalCompetencyService
{
    Task<AppraisalCompetencyDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<AppraisalCompetencyDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<AppraisalCompetencyDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<AppraisalCompetencyDto> CreateAsync(CreateAppraisalCompetencyDto createDto, CancellationToken cancellationToken = default);
    Task<AppraisalCompetencyDto> UpdateAsync(UpdateAppraisalCompetencyDto updateDto, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

#endregion Appraisal Competency

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
    Task<AppraisalAppealDto> FileAppealAsync(CreateAppraisalAppealDto appealDto, CancellationToken cancellationToken = default);
    Task<bool> ResolveAppealAsync(ResolveAppraisalAppealDto resolveDto, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> CalculateOverallScoreAsync(Guid appraisalId, CancellationToken cancellationToken = default);
    Task<bool> ProgressToHRReviewAsync(Guid appraisalId, CancellationToken cancellationToken = default);

    // The raw EvaluatorEvaluation / CriterionScore CRUD was removed in performance closure lane P1:
    // no screen called it, and its read exposed every evaluator row to the appraisee.

    // AppraisalEmployeeResponse operations
    /// <summary>HR transcribing a response. ⚠ Deliberately unwired — see AddOwnEmployeeResponseAsync.</summary>
    Task<AppraisalEmployeeResponseDto> AddEmployeeResponseAsync(Guid appraisalId, CreateAppraisalEmployeeResponseDto createDto, Guid respondingUserId, CancellationToken cancellationToken = default);

    /// <summary>The employee's own answer to their own appraisal. Someone else's appraisal is a 404.</summary>
    Task<AppraisalEmployeeResponseDto> AddOwnEmployeeResponseAsync(Guid appraisalId, CreateAppraisalEmployeeResponseDto createDto, Guid respondingEmployeeId, Guid respondingUserId, CancellationToken cancellationToken = default);
    Task<IEnumerable<AppraisalEmployeeResponseDto>> GetEmployeeResponsesAsync(Guid appraisalId, CancellationToken cancellationToken = default);

    // AppraisalAttachment operations
    // See the note on ICheckInService's attachment methods.
    Task<AppraisalAttachmentDto> AddAttachmentAsync(
        Guid appraisalId, Guid uploadedById, string fileName, long? fileSizeBytes, string? description,
        CancellationToken cancellationToken = default,
        Guid? fileUploadRecordId = null, Guid? documentRecordId = null, Guid? documentVersionId = null);
    Task<IEnumerable<AppraisalAttachmentDto>> GetAttachmentsAsync(Guid appraisalId, CancellationToken cancellationToken = default);
    Task<AppraisalAttachmentDto?> GetAttachmentAsync(Guid appraisalId, Guid attachmentId, CancellationToken cancellationToken = default);
    // P9: the uploader, or the HR desk (actorIsDesk) when it is not the appraisee; before completion.
    Task<bool> DeleteAttachmentAsync(Guid appraisalId, Guid attachmentId, bool actorIsDesk, CancellationToken cancellationToken = default);
    
    // Employee-centric operations
    Task<IEnumerable<MyAppraisalDto>> GetMyAppraisalsAsync(Guid employeeId, string? cycleFilter = null, CancellationToken cancellationToken = default);
    
    // Self-evaluation operations
    Task<SelfEvaluationContextDto> GetSelfEvaluationContextAsync(Guid appraisalId, CancellationToken cancellationToken = default);
    Task<SelfEvaluationResultDto> SaveSelfEvaluationAsync(SaveSelfEvaluationDto saveDto, CancellationToken cancellationToken = default);
    Task<ViewSubmittedEvaluationDto> GetViewSubmittedEvaluationAsync(Guid appraisalId, CancellationToken cancellationToken = default);
    
    // Manager evaluation operations
    Task<IEnumerable<TeamAppraisalCycleSummaryDto>> GetTeamAppraisalCyclesAsync(Guid managerId, CancellationToken cancellationToken = default);
    Task<IEnumerable<TeamMemberAppraisalDto>> GetTeamMemberAppraisalsAsync(Guid cycleId, Guid managerId, CancellationToken cancellationToken = default);
    Task<ManagerEvaluationContextDto> GetManagerEvaluationContextAsync(Guid appraisalId, Guid managerId, CancellationToken cancellationToken = default);
    Task<ManagerEvaluationResultDto> SaveManagerEvaluationAsync(SaveManagerEvaluationDto saveDto, CancellationToken cancellationToken = default);
    Task<ManagerPeerEvaluationReviewDto> GetManagerPeerEvaluationReviewAsync(Guid appraisalId, CancellationToken cancellationToken = default);
    
    // HR Review operations
    Task<HRReviewDto> GetHRReviewAsync(Guid appraisalId, Guid? requestingEmployeeId = null, CancellationToken cancellationToken = default);
    Task<IEnumerable<HRReviewListItemDto>> GetHRReviewListAsync(Guid? cycleId = null, string? status = null, CancellationToken cancellationToken = default);
    Task<HRReviewDto> ApproveAndFinalizeAsync(Guid appraisalId, ApproveAppraisalDto dto, Guid? reviewerId = null, CancellationToken cancellationToken = default);
    Task<HRReviewDto> ReturnToManagerAsync(Guid appraisalId, ReturnAppraisalDto dto, Guid? reviewerId = null, CancellationToken cancellationToken = default);
    
    // Employee actions
    Task AcknowledgeAppraisalAsync(Guid appraisalId, Guid employeeId, CancellationToken cancellationToken = default);
    Task<AppealPageDataDto> GetAppealPageDataAsync(Guid appraisalId, Guid employeeId, CancellationToken cancellationToken = default);
    Task<AppraisalAppealDto> SubmitAppealAsync(SubmitAppealDto submitDto, Guid employeeId, CancellationToken cancellationToken = default);
    Task<AppealStatusViewDto> GetAppealStatusAsync(Guid appraisalId, Guid employeeId, CancellationToken cancellationToken = default);
    
    // HR/Manager actions
    Task<List<AppealListItemDto>> GetAppealsListAsync(Guid? cycleId = null, AppraisalAppealStatus? status = null, CancellationToken cancellationToken = default);
    Task<AppealReviewDto> GetAppealReviewDataAsync(Guid appraisalId, CancellationToken cancellationToken = default);
    /// <summary>Moves a Submitted appeal to UnderReview and records who picked it up.</summary>
    Task<AppraisalAppealDto> BeginAppealReviewAsync(Guid appraisalId, Guid reviewerId, CancellationToken cancellationToken = default);
    Task ResolveAppealAsync(Guid appraisalId, ResolveAppealDto resolveDto, Guid reviewerId, CancellationToken cancellationToken = default);
    
    // Post-remand HR final decision
    Task<PostRemandReviewDto> GetPostRemandReviewDataAsync(Guid appraisalId, CancellationToken cancellationToken = default);
    Task FinalizePostRemandAppealAsync(Guid appraisalId, PostRemandFinalDecisionDto decisionDto, Guid reviewerId, CancellationToken cancellationToken = default);
    
    // Employee appeal outcome view
    Task<EmployeeAppealOutcomeDto> GetEmployeeAppealOutcomeAsync(Guid appraisalId, Guid employeeId, CancellationToken cancellationToken = default);
}

#endregion Performance Appraisal

#region Performance Improvement Plan

public interface IPerformanceImprovementPlanService
{
    Task<PerformanceImprovementPlanDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<PerformanceImprovementPlanDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<PerformanceImprovementPlanDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<IEnumerable<PerformanceImprovementPlanDto>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default);
    /// <summary>Plans where the given employee is the named supervisor or HR owner.</summary>
    Task<IEnumerable<PerformanceImprovementPlanDto>> GetBySupervisorAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<IEnumerable<PerformanceImprovementPlanDto>> GetByStatusAsync(PipStatus status, CancellationToken cancellationToken = default);
    Task<IEnumerable<PerformanceImprovementPlanDto>> GetActivePipsAsync(CancellationToken cancellationToken = default);
    Task<PerformanceImprovementPlanDto> CreateAsync(CreatePerformanceImprovementPlanDto createDto, CancellationToken cancellationToken = default);
    Task<PerformanceImprovementPlanDto> UpdateAsync(UpdatePerformanceImprovementPlanDto updateDto, CancellationToken cancellationToken = default);
    Task<bool> UpdateStatusAsync(UpdatePipStatusDto statusDto, CancellationToken cancellationToken = default);
    Task<bool> CompletePipAsync(CompletePipDto completeDto, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    // Approval workflow — Draft → PendingApproval → Active on the generic workflow engine.
    // ⚠ This used to say "Inoperable until a PerformanceImprovementPlan workflow definition has
    // been published". It was NOT inoperable — it auto-approved, putting an unreviewed plan into
    // force against the employee (corrected 2026-09-15). With no definition published, submitting
    // now lands the plan at PendingApproval and a HR.Performance.Admin holder rules on it. See
    // HrWorkflowFallbackAuthority.
    Task<PerformanceImprovementPlanDto> SubmitForApprovalAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PerformanceImprovementPlanDto> ApproveAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PerformanceImprovementPlanDto> RejectAsync(Guid id, string? reason, CancellationToken cancellationToken = default);
    Task<PerformanceImprovementPlanDto> RecallAsync(Guid id, CancellationToken cancellationToken = default);

    // PipReviewMeeting operations
    Task<PipReviewMeetingDto> AddReviewMeetingAsync(Guid pipId, CreatePipReviewMeetingDto createDto, CancellationToken cancellationToken = default);
    Task<IEnumerable<PipReviewMeetingDto>> GetReviewMeetingsAsync(Guid pipId, CancellationToken cancellationToken = default);
    Task<PipReviewMeetingDto?> GetReviewMeetingByIdAsync(Guid meetingId, CancellationToken cancellationToken = default);
    Task<PipReviewMeetingDto> UpdateReviewMeetingAsync(Guid pipId, UpdatePipReviewMeetingDto updateDto, CancellationToken cancellationToken = default);
    // P13: the plan's subject only (actorEmployeeId from the token); UnauthorizedAccessException otherwise.
    Task<PipReviewMeetingDto> SetEmployeeCommentsAsync(Guid pipId, Guid meetingId, Guid actorEmployeeId, string? comments, CancellationToken cancellationToken = default);
    Task<bool> DeleteReviewMeetingAsync(Guid pipId, Guid meetingId, CancellationToken cancellationToken = default);
    Task<PipReviewMeetingDto> GetLatestReviewMeetingAsync(Guid pipId, CancellationToken cancellationToken = default);

    // PipGoal operations
    Task<PipGoalDto> AddPipGoalAsync(Guid pipId, CreatePipGoalDto dto, CancellationToken cancellationToken = default);
    Task<IEnumerable<PipGoalDto>> GetPipGoalsAsync(Guid pipId, CancellationToken cancellationToken = default);
    Task<PipGoalDto?> GetGoalByIdAsync(Guid goalId, CancellationToken cancellationToken = default);
    Task<PipGoalDto> UpdatePipGoalAsync(Guid pipId, UpdatePipGoalDto dto, CancellationToken cancellationToken = default);
    Task<bool> DeletePipGoalAsync(Guid pipId, Guid goalId, CancellationToken cancellationToken = default);
    Task<PipGoalDto> UpdatePipGoalProgressAsync(Guid pipId, Guid goalId, decimal progressPercent, string? notes, GoalProgressStatus status, CancellationToken cancellationToken = default);

    // PipAttachment operations
    Task<IEnumerable<PipAttachmentDto>> GetPipAttachmentsAsync(Guid pipId, CancellationToken cancellationToken = default);
    Task<PipAttachmentDto?> GetAttachmentByIdAsync(Guid attachmentId, CancellationToken cancellationToken = default);
    /// <summary>
    /// Records an attachment against a PIP. The upload/DMS identifiers come from
    /// <c>IHrControlledDocumentService</c>; <paramref name="filePath"/> and
    /// <paramref name="publicUrl"/> stay empty for new rows — the file is private and is
    /// served only through the authorizing download endpoint.
    /// </summary>
    Task<PipAttachmentDto> CreatePipAttachmentAsync(Guid pipId, Guid uploadedById, string fileName, string filePath, string? publicUrl, long? fileSizeBytes, string? description, CancellationToken cancellationToken = default, Guid? fileUploadRecordId = null, Guid? documentRecordId = null, Guid? documentVersionId = null);
    Task<bool> DeletePipAttachmentAsync(Guid attachmentId, CancellationToken cancellationToken = default);
}

#endregion Performance Improvement Plan


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

#region Appraisal Settings

public interface IAppraisalSettingsService
{
    Task<AppraisalSettingsDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<AppraisalSettingsDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<AppraisalSettingsDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<AppraisalSettingsDto> CreateAsync(CreateAppraisalSettingsDto createDto, CancellationToken cancellationToken = default);
    Task<AppraisalSettingsDto> UpdateAsync(UpdateAppraisalSettingsDto updateDto, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<AppraisalSettingsDto?> GetDefaultSettingsAsync(CancellationToken cancellationToken = default);
    Task<bool> ValidateWeightsAsync(Guid id, CancellationToken cancellationToken = default);
}

#endregion Appraisal Settings

#region Appraisal Cycle

public interface IAppraisalCycleService
{
    Task<AppraisalCycleDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<AppraisalCycleDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<AppraisalCycleDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<IEnumerable<AppraisalCycleDto>> GetByYearAsync(int year, CancellationToken cancellationToken = default);
    Task<IEnumerable<AppraisalCycleDto>> GetByTypeAsync(AppraisalType type, CancellationToken cancellationToken = default);
    Task<IEnumerable<AppraisalCycleDto>> GetActiveCyclesAsync(CancellationToken cancellationToken = default);
    Task<AppraisalCycleDto> CreateAsync(CreateAppraisalCycleDto createDto, CancellationToken cancellationToken = default);
    Task<AppraisalCycleDto> UpdateAsync(UpdateAppraisalCycleDto updateDto, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> OpenCycleAsync(OpenAppraisalCycleDto openDto, Guid openedById, CancellationToken cancellationToken = default);
    Task<bool> CloseCycleAsync(CloseAppraisalCycleDto closeDto, Guid closedById, CancellationToken cancellationToken = default);
    Task<AppraisalCycleProgressDto> GetCycleProgressAsync(Guid cycleId, CancellationToken cancellationToken = default);
    /// <summary>Builds a read-only calendar of activity dates (deadlines, windows, review events, check-ins) for a cycle.</summary>
    Task<IEnumerable<AppraisalCalendarEventDto>> GetCalendarAsync(Guid cycleId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Guid>> GetEmployeesInScopeAsync(Guid cycleId, CancellationToken cancellationToken = default);
    Task<(int Created, int EvaluationsCreated, int ReviewEventsCreated)> GenerateAppraisalsAsync(Guid cycleId, Guid generatedById, CancellationToken cancellationToken = default);

    /// <summary>
    /// Raises in-app reminders for every cycle phase whose deadline has passed or falls
    /// inside the settings' low-risk window, addressed to the employees in scope. On demand —
    /// there is no background job — and repeat-safe: an identical unread reminder is skipped.
    /// Returns how many notifications were written.
    /// </summary>
    Task<int> SendDeadlineRemindersAsync(Guid cycleId, CancellationToken cancellationToken = default);
    
    // AppraisalCycleTarget operations
    Task<AppraisalCycleTargetDto> AddCycleTargetAsync(Guid cycleId, CreateAppraisalCycleTargetDto createDto, CancellationToken cancellationToken = default);
    Task<IEnumerable<AppraisalCycleTargetDto>> GetCycleTargetsAsync(Guid cycleId, CancellationToken cancellationToken = default);
    Task<AppraisalCycleTargetDto> UpdateCycleTargetAsync(Guid cycleId, UpdateAppraisalCycleTargetDto updateDto, CancellationToken cancellationToken = default);
    Task<bool> RemoveCycleTargetAsync(Guid cycleId, Guid targetId, CancellationToken cancellationToken = default);
}

#endregion Appraisal Cycle

#region Appraisal Cycle Target

public interface IAppraisalCycleTargetService
{
    Task<AppraisalCycleTargetDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<AppraisalCycleTargetDto>> GetByCycleIdAsync(Guid cycleId, CancellationToken cancellationToken = default);
    Task<IEnumerable<AppraisalCycleTargetDto>> GetByTargetTypeAsync(AppraisalTargetType targetType, CancellationToken cancellationToken = default);
    Task<AppraisalCycleTargetDto> CreateAsync(CreateAppraisalCycleTargetDto createDto, CancellationToken cancellationToken = default);
    Task<AppraisalCycleTargetDto> UpdateAsync(UpdateAppraisalCycleTargetDto updateDto, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ValidateTargetAsync(Guid id, CancellationToken cancellationToken = default);

    // Exclusion operations
    Task<AppraisalCycleTargetExclusionDto> AddExclusionAsync(Guid targetId, CreateAppraisalCycleTargetExclusionDto dto, CancellationToken cancellationToken = default);
    Task<IEnumerable<AppraisalCycleTargetExclusionDto>> GetExclusionsAsync(Guid targetId, CancellationToken cancellationToken = default);
    Task<AppraisalCycleTargetExclusionDto> UpdateExclusionAsync(Guid targetId, UpdateAppraisalCycleTargetExclusionDto dto, CancellationToken cancellationToken = default);
    Task<bool> RemoveExclusionAsync(Guid targetId, Guid exclusionId, CancellationToken cancellationToken = default);
}

#endregion Appraisal Cycle Target

#region Peer Nomination

public interface IPeerNominationService
{
    Task<PeerNominationDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<PeerNominationDto>> GetByAppraisalIdAsync(Guid appraisalId, CancellationToken cancellationToken = default);
    Task<IEnumerable<PeerNominationDto>> GetByPeerEmployeeIdAsync(Guid peerEmployeeId, CancellationToken cancellationToken = default);
    Task<IEnumerable<PeerNominationDto>> GetPendingNominationsAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<PeerNominationDto> CreateAsync(CreatePeerNominationDto createDto, CancellationToken cancellationToken = default);
    Task<PeerNominationDto> UpdateAsync(UpdatePeerNominationDto updateDto, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> SendInvitationAsync(SendPeerEvaluationInvitationDto invitationDto, CancellationToken cancellationToken = default);
    
    // Batch operations
    Task<PeerNominationSummaryDto> GetNominationSummaryAsync(Guid appraisalId, CancellationToken cancellationToken = default);
    // P14: nominatedById is the caller (from the token); Manager mode refuses the appraisee.
    Task<IEnumerable<PeerNominationDto>> BatchCreateAsync(BatchCreatePeerNominationsDto batchDto, Guid nominatedById, CancellationToken cancellationToken = default);
    Task<IEnumerable<PeerNominationDto>> ApproveNominationsAsync(ApprovePeerNominationsDto approveDto, CancellationToken cancellationToken = default);
    Task<IEnumerable<PeerNominationDto>> RejectNominationsAsync(RejectPeerNominationsDto rejectDto, CancellationToken cancellationToken = default);
}

#endregion Peer Nomination

#region Appraisal Template

public interface IAppraisalTemplateService
{
    Task<AppraisalTemplateDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<AppraisalTemplateDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<AppraisalTemplateDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<IEnumerable<AppraisalTemplateSummaryDto>> GetSummariesAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<AppraisalTemplateDto>> GetByPositionIdAsync(Guid positionId, CancellationToken cancellationToken = default);
    Task<IEnumerable<AppraisalTemplateDto>> GetByOrganizationUnitIdAsync(Guid orgUnitId, CancellationToken cancellationToken = default);
    Task<IEnumerable<AppraisalTemplateDto>> GetByOrganizationLevelIdAsync(Guid orgLevelId, CancellationToken cancellationToken = default);
    Task<IEnumerable<AppraisalTemplateDto>> GetActiveTemplatesAsync(CancellationToken cancellationToken = default);
    Task<AppraisalTemplateDto> CreateAsync(CreateAppraisalTemplateDto createDto, CancellationToken cancellationToken = default);
    Task<AppraisalTemplateDto> UpdateAsync(UpdateAppraisalTemplateDto updateDto, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> SetActiveStatusAsync(Guid id, bool isActive, CancellationToken cancellationToken = default);
    /// <summary>
    /// Deep-copies a template (sections, items, item grade ranges) into a new template,
    /// applying the target Level/Unit/Position scope from <paramref name="dto"/>. The copy
    /// starts inactive until explicitly activated.
    /// </summary>
    Task<AppraisalTemplateDto> CloneAsync(Guid sourceTemplateId, CopyAppraisalTemplateDto dto, CancellationToken cancellationToken = default);

    // Section operations
    Task<AppraisalTemplateSectionDto> AddSectionAsync(Guid templateId, CreateAppraisalTemplateSectionDto dto, CancellationToken cancellationToken = default);
    Task<IEnumerable<AppraisalTemplateSectionDto>> GetSectionsAsync(Guid templateId, CancellationToken cancellationToken = default);
    Task<AppraisalTemplateSectionDto> UpdateSectionAsync(Guid templateId, UpdateAppraisalTemplateSectionDto dto, CancellationToken cancellationToken = default);
    Task<bool> DeleteSectionAsync(Guid templateId, Guid sectionId, CancellationToken cancellationToken = default);
    Task<bool> ReorderSectionsAsync(Guid templateId, IEnumerable<Guid> orderedSectionIds, CancellationToken cancellationToken = default);

    // Item operations
    Task<AppraisalTemplateItemDto> AddItemAsync(Guid sectionId, CreateAppraisalTemplateItemDto dto, CancellationToken cancellationToken = default);
    Task<IEnumerable<AppraisalTemplateItemDto>> GetItemsAsync(Guid sectionId, CancellationToken cancellationToken = default);
    Task<AppraisalTemplateItemDto> UpdateItemAsync(Guid sectionId, UpdateAppraisalTemplateItemDto dto, CancellationToken cancellationToken = default);
    Task<bool> DeleteItemAsync(Guid sectionId, Guid itemId, CancellationToken cancellationToken = default);
    Task<bool> ReorderItemsAsync(Guid sectionId, IEnumerable<Guid> orderedItemIds, CancellationToken cancellationToken = default);

    // Item grade-range operations (replace-all pattern)
    Task<IEnumerable<TemplateItemGradeRangeDto>> GetItemGradeRangesAsync(Guid itemId, CancellationToken cancellationToken = default);
    Task<IEnumerable<TemplateItemGradeRangeDto>> UpdateItemGradeRangesAsync(Guid itemId, UpsertTemplateItemGradeRangesDto dto, CancellationToken cancellationToken = default);

    /// <summary>Returns all active grade definitions available for selection in the template editor.</summary>
    Task<IEnumerable<AppraisalGradeDefinitionDto>> GetActiveGradeDefinitionsAsync(CancellationToken cancellationToken = default);

    // ── Approval workflow ──────────────────────────────────────────────────
    // Driven by the generic workflow engine; who approves is defined by the published
    // AppraisalTemplate workflow definition, not by a role attribute on the controller.
    // The employee id each method takes is stamped on the template's own audit columns —
    // the engine separately resolves the acting ApplicationUser from the token.

    /// <summary>Submit a Draft/Rejected template for approval, starting its workflow.</summary>
    Task<AppraisalTemplateDto> SubmitForApprovalAsync(Guid id, Guid submittedByEmployeeId, CancellationToken cancellationToken = default);
    /// <summary>Record an approval on the current workflow step. Approves the template outright once the last step passes.</summary>
    Task<AppraisalTemplateDto> ApproveAsync(Guid id, Guid approvedByEmployeeId, CancellationToken cancellationToken = default);
    /// <summary>Reject the template at the current workflow step, with an optional reason.</summary>
    Task<AppraisalTemplateDto> RejectAsync(Guid id, Guid rejectedByEmployeeId, string? reason, CancellationToken cancellationToken = default);
    /// <summary>Pull a still-pending template back to Draft so its author can keep editing.</summary>
    Task<AppraisalTemplateDto> RecallAsync(Guid id, CancellationToken cancellationToken = default);
}

#endregion Appraisal Template

#region Goal Library

public interface IGoalLibraryService
{
    Task<GoalLibraryDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<GoalLibraryDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<PagedResult<GoalLibraryDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<IEnumerable<GoalLibraryDto>> GetByPositionIdAsync(Guid positionId, CancellationToken cancellationToken = default);
    Task<IEnumerable<GoalLibraryDto>> GetByOrganizationUnitIdAsync(Guid orgUnitId, CancellationToken cancellationToken = default);
    Task<IEnumerable<GoalLibraryDto>> GetActiveItemsAsync(CancellationToken cancellationToken = default);
    Task<GoalLibraryDto> CreateAsync(CreateGoalLibraryDto createDto, CancellationToken cancellationToken = default);
    Task<GoalLibraryDto> UpdateAsync(UpdateGoalLibraryDto updateDto, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> SetActiveStatusAsync(Guid id, bool isActive, CancellationToken cancellationToken = default);
    Task<int> GetEmployeeGoalUsageCountAsync(Guid id, CancellationToken cancellationToken = default);
    Task<GoalLibraryDetailsDto> GetDetailsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<GoalLibraryUsageStatsDto> GetUsageStatsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<GoalLibraryUsageRowDto>> GetUsagePagedAsync(Guid id, int pageNumber, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a paged, projected list of goal library templates for use by the selector modal.
    /// Supports full-text search across Title/Description and optional scope filters.
    /// Uses direct IQueryable projection — no entity materialisation.
    /// </summary>
    Task<PagedResult<GoalLibrarySelectorDto>> GetSelectorPagedAsync(
        string? search,
        bool activeOnly,
        Guid? organizationLevelId,
        Guid? organizationUnitId,
        Guid? positionId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);
}

#endregion Goal Library

#region Appraisal Cycle Template

public interface IAppraisalCycleTemplateService
{
    Task<AppraisalCycleTemplateDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<AppraisalCycleTemplateDto>> GetByCycleIdAsync(Guid cycleId, CancellationToken cancellationToken = default);
    Task<IEnumerable<AppraisalCycleTemplateDto>> GetByTemplateIdAsync(Guid templateId, CancellationToken cancellationToken = default);
    Task<AppraisalCycleTemplateDto> CreateAsync(CreateAppraisalCycleTemplateDto createDto, CancellationToken cancellationToken = default);
    Task<AppraisalCycleTemplateDto> UpdateAsync(UpdateAppraisalCycleTemplateDto updateDto, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<AppraisalCycleTemplateDto>> BulkAssignAsync(Guid cycleId, IEnumerable<CreateAppraisalCycleTemplateDto> assignments, CancellationToken cancellationToken = default);
    /// <summary>Resolves the highest-priority template applicable to an employee in the cycle.</summary>
    Task<AppraisalTemplateDto?> ResolveTemplateForEmployeeAsync(Guid cycleId, Guid employeeId, CancellationToken cancellationToken = default);
}

#endregion Appraisal Cycle Template

#region Talent rating sync (Theme 9)

/// <summary>Pushes a finalized appraisal's score (as a PerformanceRating) onto the employee's
/// succession/talent-pool records so the 9-box and dashboards stay current.</summary>
public interface ITalentRatingSyncService
{
    /// <summary>
    /// Publishes the appraisal's settled score. Does nothing — and says why in the log — when the
    /// score is null or a newer appraisal of the same employee has already published a rating.
    /// True when a rating was written.
    /// </summary>
    Task<bool> SyncFromAppraisalAsync(Guid appraisalId, CancellationToken cancellationToken = default);
}

#endregion

#region Appraisal score (performance closure lane A)

/// <summary>
/// The appraisal's score in one place: the only arithmetic path from an evaluator's raw inputs to a
/// stored number, and the only writer of <c>PerformanceAppraisal.OverallScore</c>.
/// </summary>
public interface IAppraisalScoreService
{
    /// <summary>The appraisal's scoring inputs, from its criterion snapshot, loaded once.</summary>
    Task<AppraisalCriterionScoring> LoadScoringAsync(Guid appraisalId, CancellationToken cancellationToken = default);

    /// <summary>Sets <c>WeightedScore</c> on one row from its raw inputs.</summary>
    Task ScoreCriterionAsync(CriterionScore score, AppraisalCriterionScoring scoring, CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-weights every row from its raw inputs and returns the evaluator's 0–100 total over the
    /// items they scored; null when they scored nothing that carries weight.
    /// </summary>
    Task<decimal?> ScoreEvaluatorAsync(IEnumerable<CriterionScore> scores, AppraisalCriterionScoring scoring, CancellationToken cancellationToken = default);

    /// <summary>
    /// The top of an item's own scale: its highest grade band, or 100 when it has none. A KPI's
    /// score is an achievement percentage, so its top is always 100.
    /// </summary>
    Task<decimal> GetScaleTopAsync(AppraisalCriterionScoring scoring, Guid templateItemId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks each input against its item's own scale (A11). Returns the message to show, or null
    /// when every input is in range.
    /// </summary>
    Task<string?> ValidateItemScoresAsync(Guid appraisalId, IEnumerable<EvaluationItemInputDto> items, CancellationToken cancellationToken = default);

    /// <summary>
    /// Settles the appraisal's overall score: recomputes every weighted score and evaluator total
    /// from raw inputs, takes the SUBMITTED legs by role, and stores
    /// <c>CalibratedOverallScore ?? computed</c> (null when nothing was scored) with its grade. When
    /// <paramref name="publish"/> is true and the appraisal is final, the rating goes to the talent
    /// pools. Saves. A caller inside a transaction passes <c>publish: false</c> and calls
    /// <see cref="PublishAsync"/> after the commit.
    /// </summary>
    Task<AppraisalSettleResult> SettleAsync(Guid appraisalId, AppraisalScoreChangeSource source, bool publish = true, CancellationToken cancellationToken = default);

    /// <summary>Publishes a final appraisal's settled rating to the talent pools; false when it is not final or nothing was written.</summary>
    Task<bool> PublishAsync(Guid appraisalId, CancellationToken cancellationToken = default);

    /// <summary>
    /// A15 (D-13): what a settle would store for every Completed/Closed appraisal, beside what is
    /// stored. Read-only — nothing is written.
    /// </summary>
    Task<AppraisalSettleDryRunReportDto> DryRunAsync(Guid? cycleId, CancellationToken cancellationToken = default);
}

#endregion

#region Salary Review Proposals (Theme 11)

public interface ISalaryReviewProposalService
{
    Task<IEnumerable<SalaryReviewProposalDto>> GetAllAsync(SalaryReviewProposalStatus? status = null, CancellationToken cancellationToken = default);
    Task<SalaryReviewProposalDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the amount before approval. Refused once the proposal has left Proposed — an approved
    /// figure is what payroll acts on, so it cannot be edited afterwards.
    /// </summary>
    Task<SalaryReviewProposalDto> UpdateAsync(Guid id, UpdateSalaryReviewProposalDto dto, CancellationToken cancellationToken = default);

    // ── Approval, on the generic workflow engine ────────────────────────────
    // Who signs off a pay change is configuration, not code: the proposal's type, percentage and
    // amount go into the workflow entity context so a definition can route on them.

    /// <summary>Sends the proposal for approval. Requires a figure to have been set.</summary>
    Task<SalaryReviewProposalDto> SubmitForApprovalAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SalaryReviewProposalDto> ApproveAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SalaryReviewProposalDto> RejectAsync(Guid id, string? reason, CancellationToken cancellationToken = default);
    /// <summary>Pulls a pending proposal back so its figure can be reworked.</summary>
    Task<SalaryReviewProposalDto> RecallAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that payroll has made the change. Not an approval — it is the receipt for one —
    /// so it stays off the engine and is refused unless the proposal is Approved.
    /// </summary>
    Task<SalaryReviewProposalDto> MarkAppliedAsync(Guid id, string? notes, CancellationToken cancellationToken = default);
}

/// <summary>Read + status-manage the employment-action proposals raised from appraisal recommendations.</summary>
public interface IEmploymentActionProposalService
{
    Task<IEnumerable<EmploymentActionProposalDto>> GetAllAsync(EmploymentActionProposalStatus? status = null, CancellationToken cancellationToken = default);
    Task<EmploymentActionProposalDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    // ── Approval, on the generic workflow engine ────────────────────────────
    // The action type is in the workflow entity context, so a definition can send a recognition
    // and a termination to different approvers.

    Task<EmploymentActionProposalDto> SubmitForApprovalAsync(Guid id, CancellationToken cancellationToken = default);
    Task<EmploymentActionProposalDto> ApproveAsync(Guid id, CancellationToken cancellationToken = default);
    Task<EmploymentActionProposalDto> RejectAsync(Guid id, string? reason, CancellationToken cancellationToken = default);
    Task<EmploymentActionProposalDto> RecallAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that the owning module has created the real record. Refused unless the proposal
    /// is Approved.
    /// </summary>
    Task<EmploymentActionProposalDto> MarkActionedAsync(Guid id, string? notes, CancellationToken cancellationToken = default);
}

/// <summary>
/// The one resolver of an overall appraisal score (0–100): the grade band it falls in and the 5-point
/// <see cref="Enums.PerformanceRating"/> that band maps to, from the tenant's configurable
/// <c>AppraisalGradeDefinition</c> overall bands, falling back to the fixed
/// <c>AppraisalScoring.MapScoreToRating</c> bands when none are configured. Bands are cached per instance
/// (scoped/per-request) so repeated mapping in analytics doesn't re-query.
/// </summary>
public interface IPerformanceRatingResolver
{
    /// <summary>Maps a single score to a rating.</summary>
    Task<Enums.PerformanceRating?> ResolveAsync(decimal? score, CancellationToken cancellationToken = default);

    /// <summary>Returns a synchronous mapper (bands captured) for mapping many scores in a loop/LINQ.</summary>
    Task<Func<decimal?, Enums.PerformanceRating?>> GetMapperAsync(CancellationToken cancellationToken = default);

    /// <summary>The grade definition whose band the score reaches; null when no band covers it.</summary>
    Task<Guid?> ResolveGradeDefinitionIdAsync(decimal? score, CancellationToken cancellationToken = default);
}

#endregion

#region Performance Analytics (Themes 13-14, read-only)

public interface IPerformanceAnalyticsService
{
    /// <summary>Rating distribution for a cycle (Theme 13) from finalized appraisal scores.</summary>
    Task<CalibrationDistributionDto> GetCycleRatingDistributionAsync(Guid cycleId, CancellationToken cancellationToken = default);
    /// <summary>An employee's appraisal score trend across cycles/years (Theme 14).</summary>
    Task<EmployeePerformanceTrendDto> GetEmployeeTrendAsync(Guid employeeId, CancellationToken cancellationToken = default);
}

#endregion

#region Appraisal Outcome Recommendations (Theme 8 backbone)

/// <summary>
/// Manages the lifecycle of appraisal outcome recommendations and dispatches approved ones
/// to the owning module via registered <see cref="IOutcomeRecommendationHandler"/>s.
/// </summary>
public interface IAppraisalOutcomeService
{
    Task<IEnumerable<AppraisalOutcomeRecommendationDto>> GetByAppraisalAsync(Guid performanceAppraisalId, CancellationToken cancellationToken = default);
    Task<IEnumerable<AppraisalOutcomeRecommendationDto>> GetWorklistAsync(RecommendationStatus? status = null, CancellationToken cancellationToken = default);
    /// <summary>
    /// Propose an outcome for an appraisal. <paramref name="isPrivilegedActor"/> is true for HR
    /// and SuperAdmin; anyone else must be the appraisee's own manager, since a recommendation
    /// here is what later creates a real promotion, demotion or termination record.
    /// </summary>
    Task<AppraisalOutcomeRecommendationDto> ProposeAsync(
        CreateAppraisalOutcomeRecommendationDto dto, Guid recommendedById, bool isPrivilegedActor, CancellationToken cancellationToken = default);

    /// <summary>Approve a Proposed recommendation and dispatch it to the owning module (idempotent).</summary>
    Task<AppraisalOutcomeRecommendationDto> ApproveAsync(Guid id, Guid approverId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-run the dispatch for a recommendation that was approved but whose handler failed, so
    /// it does not sit Approved-but-not-Actioned with no way to complete it. The handlers reuse
    /// an existing downstream record, so this is safe to repeat.
    /// </summary>
    Task<AppraisalOutcomeRecommendationDto> RetryDispatchAsync(Guid id, CancellationToken cancellationToken = default);

    Task<AppraisalOutcomeRecommendationDto> RejectAsync(Guid id, Guid reviewerId, string? notes, CancellationToken cancellationToken = default);
    Task<AppraisalOutcomeRecommendationDto> DismissAsync(Guid id, Guid reviewerId, string? notes, CancellationToken cancellationToken = default);
}

/// <summary>
/// Pluggable handler that creates the real downstream record for a recommendation type.
/// Integration themes (Training/Succession/Compensation/Probation) register implementations.
/// </summary>
public interface IOutcomeRecommendationHandler
{
    RecommendationType Type { get; }
    /// <summary>Creates the downstream record. Returns the (entityType, entityId) back-link, or null if it could not be created.</summary>
    Task<(string TargetEntityType, Guid TargetEntityId)?> HandleAsync(AppraisalOutcomeRecommendation recommendation, CancellationToken cancellationToken = default);
}

#endregion

#region Performance Links (goal required skills + check-in objectives)

public interface IPerformanceLinkService
{
    // Theme 3 — soft skills per goal
    Task<IEnumerable<GoalRequiredSkillDto>> GetGoalRequiredSkillsAsync(Guid employeeGoalId, CancellationToken cancellationToken = default);
    Task<IEnumerable<GoalRequiredSkillDto>> SetGoalRequiredSkillsAsync(Guid employeeGoalId, IEnumerable<SetGoalRequiredSkillDto> skills, CancellationToken cancellationToken = default);

    // Theme 6 — check-in ↔ yearly objective
    Task<IEnumerable<CheckInObjectiveLinkDto>> GetCheckInObjectivesAsync(Guid checkInId, CancellationToken cancellationToken = default);
    Task<IEnumerable<CheckInObjectiveLinkDto>> SetCheckInObjectivesAsync(Guid checkInId, IEnumerable<Guid> companyGoalIds, CancellationToken cancellationToken = default);

    // Theme 3 — development-plan skill suggestions from an employee's goals' required skills
    Task<IEnumerable<DevelopmentSkillSuggestionDto>> GetDevelopmentSkillSuggestionsAsync(Guid employeeId, Guid cycleId, CancellationToken cancellationToken = default);
}

#endregion

#region Strategic Goal

public interface IStrategicGoalService
{
    Task<StrategicGoalDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<StrategicGoalDto>> GetAllAsync(bool activeOnly = false, CancellationToken cancellationToken = default);
    Task<PagedResult<StrategicGoalDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<StrategicGoalDto> CreateAsync(CreateStrategicGoalDto createDto, CancellationToken cancellationToken = default);
    Task<StrategicGoalDto> UpdateAsync(UpdateStrategicGoalDto updateDto, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> SetActiveStatusAsync(Guid id, bool isActive, CancellationToken cancellationToken = default);
}

#endregion Strategic Goal

#region Company Goal

public interface ICompanyGoalService
{
    Task<CompanyGoalDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<CompanyGoalDto>> GetByCycleIdAsync(Guid cycleId, CancellationToken cancellationToken = default);
    Task<PagedResult<CompanyGoalDto>> GetPagedAsync(int pageNumber, int pageSize, Guid? cycleId = null, CancellationToken cancellationToken = default);
    Task<IEnumerable<CompanyGoalDto>> GetVisibleGoalsAsync(Guid cycleId, CancellationToken cancellationToken = default);
    Task<CompanyGoalDto> CreateAsync(CreateCompanyGoalDto createDto, CancellationToken cancellationToken = default);
    Task<CompanyGoalDto> UpdateAsync(UpdateCompanyGoalDto updateDto, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> SetVisibilityAsync(Guid id, bool isVisible, CancellationToken cancellationToken = default);
    Task<CompanyGoalCascadeStatsDto> GetCascadeStatsAsync(Guid goalId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Strategy-dashboard projection query. Returns list items with cascade counts only.
    /// Never loads full navigation collections.
    /// </summary>
    Task<PagedResult<CompanyGoalListItemDto>> GetDashboardPagedAsync(
        Guid cycleId,
        string? search,
        GoalPriority? priority,
        bool? isVisible,
        DateOnly? dueDateTo,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Aggregated metrics for the strategy-dashboard header cards.
    /// Single projection query — does not load any navigation collections.
    /// </summary>
    Task<CompanyGoalDashboardMetricsDto> GetDashboardMetricsAsync(
        Guid cycleId,
        CancellationToken cancellationToken = default);
}

#endregion Company Goal

#region Unit Goal

public interface IUnitGoalService
{
    Task<UnitGoalDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<UnitGoalDto>> GetByCycleIdAsync(Guid cycleId, CancellationToken cancellationToken = default);
    Task<IEnumerable<UnitGoalDto>> GetByOrganizationUnitIdAsync(Guid orgUnitId, CancellationToken cancellationToken = default);
    Task<IEnumerable<UnitGoalDto>> GetByCreatedByManagerIdAsync(Guid managerId, Guid? cycleId = null, CancellationToken cancellationToken = default);
    Task<IEnumerable<UnitGoalDto>> GetByParentCompanyGoalAsync(Guid companyGoalId, CancellationToken cancellationToken = default);
    Task<PagedResult<UnitGoalDto>> GetPagedAsync(int pageNumber, int pageSize, Guid? cycleId = null, CancellationToken cancellationToken = default);
    /// <summary>Filtered, projected paged list for the alignment management dashboard.</summary>
    Task<PagedResult<UnitGoalListItemDto>> GetDashboardPagedAsync(
        Guid cycleId, string? search, GoalPriority? priority, Guid? orgUnitId,
        bool? isLinked, Guid? managerEmployeeId, int pageNumber, int pageSize,
        CancellationToken cancellationToken = default);
    /// <summary>Aggregated alignment metrics for a cycle's dashboard header tiles.</summary>
    Task<UnitGoalDashboardMetricsDto> GetDashboardMetricsAsync(Guid cycleId, CancellationToken cancellationToken = default);
    /// <summary>Cascade integrity check — returns employee goal count only (no nav collection loading).</summary>
    Task<UnitGoalCascadeStatsDto> GetCascadeStatsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<UnitGoalEmployeeGoalSummaryDto>> GetEmployeeGoalSummariesAsync(Guid unitGoalId, CancellationToken cancellationToken = default);
    Task<UnitGoalDto> CreateAsync(CreateUnitGoalDto createDto, CancellationToken cancellationToken = default);
    Task<UnitGoalDto> UpdateAsync(UpdateUnitGoalDto updateDto, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    // See the note on ICheckInService's attachment methods.
    Task<AppraisalAttachmentDto> AddAttachmentAsync(
        Guid goalId, Guid uploadedById, string fileName, long? fileSizeBytes, string? description,
        CancellationToken cancellationToken = default,
        Guid? fileUploadRecordId = null, Guid? documentRecordId = null, Guid? documentVersionId = null);
    Task<IEnumerable<AppraisalAttachmentDto>> GetAttachmentsAsync(Guid goalId, CancellationToken cancellationToken = default);
    Task<AppraisalAttachmentDto?> GetAttachmentAsync(Guid goalId, Guid attachmentId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAttachmentAsync(Guid goalId, Guid attachmentId, CancellationToken cancellationToken = default);
}

#endregion Unit Goal

#region Employee Goal

public interface IEmployeeGoalService
{
    Task<EmployeeGoalDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<EmployeeGoalDto>> GetByEmployeeIdAsync(Guid employeeId, Guid? cycleId = null, CancellationToken cancellationToken = default);
    Task<IEnumerable<EmployeeGoalDto>> GetByAppraisalIdAsync(Guid appraisalId, CancellationToken cancellationToken = default);
    Task<IEnumerable<EmployeeGoalDto>> GetPendingApprovalAsync(Guid managerId, Guid? cycleId = null, CancellationToken cancellationToken = default);
    Task<PagedResult<EmployeeGoalDto>> GetPagedAsync(int pageNumber, int pageSize, Guid? employeeId = null, Guid? cycleId = null, CancellationToken cancellationToken = default);
    Task<EmployeeGoalDto> CreateAsync(CreateEmployeeGoalDto createDto, CancellationToken cancellationToken = default);
    Task<EmployeeGoalDto> UpdateAsync(UpdateEmployeeGoalDto updateDto, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    // Approval workflow
    Task<EmployeeGoalDto> SubmitForApprovalAsync(Guid goalId, Guid managerId, CancellationToken cancellationToken = default);
    Task<EmployeeGoalDto> ApproveGoalAsync(Guid goalId, Guid managerId, string? feedback = null, CancellationToken cancellationToken = default);
    Task<EmployeeGoalDto> RejectGoalAsync(Guid goalId, Guid managerId, string? feedback = null, CancellationToken cancellationToken = default);

    // Progress tracking
    /// <summary>recordedById comes from the caller's token — never from the payload.</summary>
    Task<GoalProgressEntryDto> AddProgressEntryAsync(Guid goalId, CreateGoalProgressEntryDto dto, Guid recordedById, CancellationToken cancellationToken = default);
    Task<IEnumerable<GoalProgressEntryDto>> GetProgressEntriesAsync(Guid goalId, CancellationToken cancellationToken = default);
    // P8: the recorder (actorEmployeeId, from the token), or the HR desk when it is not the goal's owner.
    Task<GoalProgressEntryDto> UpdateProgressEntryAsync(Guid goalId, UpdateGoalProgressEntryDto dto, Guid? actorEmployeeId, bool actorIsDesk, CancellationToken cancellationToken = default);
    Task<bool> DeleteProgressEntryAsync(Guid goalId, Guid entryId, Guid? actorEmployeeId, bool actorIsDesk, CancellationToken cancellationToken = default);

    // Lock management (goals locked at start of evaluation phase)
    Task<bool> LockGoalAsync(Guid goalId, CancellationToken cancellationToken = default);
    Task<bool> UnlockGoalAsync(Guid goalId, CancellationToken cancellationToken = default);

    // Summaries
    Task<EmployeeGoalSummaryDto> GetGoalSummaryAsync(Guid employeeId, Guid cycleId, CancellationToken cancellationToken = default);
    Task<IEnumerable<TeamGoalSummaryDto>> GetTeamGoalSummaryAsync(Guid managerId, Guid cycleId, CancellationToken cancellationToken = default);
}

#endregion Employee Goal

#region Check-In

public interface ICheckInService
{
    Task<CheckInDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<CheckInDto>> GetByEmployeeIdAsync(Guid employeeId, Guid? cycleId = null, CancellationToken cancellationToken = default);
    Task<IEnumerable<CheckInDto>> GetByConductedByIdAsync(Guid conductedById, Guid? cycleId = null, CancellationToken cancellationToken = default);
    Task<PagedResult<CheckInDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<IEnumerable<CheckInDto>> GetUpcomingAsync(Guid employeeId, int daysAhead = 30, CancellationToken cancellationToken = default);
    Task<CheckInDto> CreateAsync(CreateCheckInDto createDto, CancellationToken cancellationToken = default);
    Task<CheckInDto> UpdateAsync(UpdateCheckInDto updateDto, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CheckInDto> CompleteAsync(Guid checkInId, string? sharedNotes, string? privateNotes, string? actionItems, CancellationToken cancellationToken = default);

    // Goal update operations
    Task<CheckInGoalUpdateDto> AddGoalUpdateAsync(Guid checkInId, CreateCheckInGoalUpdateDto dto, CancellationToken cancellationToken = default);
    Task<IEnumerable<CheckInGoalUpdateDto>> GetGoalUpdatesAsync(Guid checkInId, CancellationToken cancellationToken = default);
    Task<CheckInGoalUpdateDto> UpdateGoalUpdateAsync(Guid checkInId, UpdateCheckInGoalUpdateDto dto, CancellationToken cancellationToken = default);
    Task<bool> DeleteGoalUpdateAsync(Guid checkInId, Guid updateId, CancellationToken cancellationToken = default);

    // Attachment operations
    // The file itself goes through the controlled-upload gate in the controller, which hands the
    // stored document's identifiers over here. uploadedById comes from the token, never the payload.
    Task<AppraisalAttachmentDto> AddAttachmentAsync(
        Guid checkInId, Guid uploadedById, string fileName, long? fileSizeBytes, string? description,
        CancellationToken cancellationToken = default,
        Guid? fileUploadRecordId = null, Guid? documentRecordId = null, Guid? documentVersionId = null);
    Task<IEnumerable<AppraisalAttachmentDto>> GetAttachmentsAsync(Guid checkInId, CancellationToken cancellationToken = default);
    Task<AppraisalAttachmentDto?> GetAttachmentAsync(Guid checkInId, Guid attachmentId, CancellationToken cancellationToken = default);
    // P9: the uploader, or the HR desk (actorIsDesk) when it is not the subject; before completion.
    Task<bool> DeleteAttachmentAsync(Guid checkInId, Guid attachmentId, bool actorIsDesk, CancellationToken cancellationToken = default);
}

#endregion Check-In

#region Performance Journal

public interface IPerformanceJournalService
{
    Task<PerformanceJournalEntryDto> GetByIdAsync(Guid id, Guid requestingEmployeeId, CancellationToken cancellationToken = default);
    /// <summary>
    /// Entries owned by <paramref name="ownerId"/>. <paramref name="includePrivate"/> must be false
    /// for any caller who is not the owner — a private entry is theirs alone.
    /// </summary>
    Task<IEnumerable<PerformanceJournalEntryDto>> GetByOwnerIdAsync(Guid ownerId, Guid? cycleId = null, bool includePrivate = true, CancellationToken cancellationToken = default);
    /// <summary>Returns entries a manager wrote about a specific direct report (SubjectEmployeeId).</summary>
    Task<IEnumerable<PerformanceJournalEntryDto>> GetAboutSubjectAsync(Guid managerId, Guid subjectEmployeeId, Guid? cycleId = null, CancellationToken cancellationToken = default);
    /// <summary>Returns entries shared with the manager (IsPrivate = false) for a given employee.</summary>
    Task<IEnumerable<PerformanceJournalEntryDto>> GetSharedWithManagerAsync(Guid ownerId, Guid? cycleId = null, CancellationToken cancellationToken = default);
    /// <inheritdoc cref="GetByOwnerIdAsync"/>
    Task<PagedResult<PerformanceJournalEntryDto>> GetPagedAsync(Guid ownerId, int pageNumber, int pageSize, Guid? cycleId = null, bool includePrivate = true, CancellationToken cancellationToken = default);
    /// <summary>
    /// Writes an entry owned by <paramref name="ownerId"/>, which the caller takes from the token —
    /// the owner is never read from the payload.
    /// </summary>
    Task<PerformanceJournalEntryDto> CreateAsync(CreatePerformanceJournalEntryDto createDto, Guid ownerId, CancellationToken cancellationToken = default);
    Task<PerformanceJournalEntryDto> UpdateAsync(UpdatePerformanceJournalEntryDto updateDto, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> SetPrivacyAsync(Guid id, bool isPrivate, CancellationToken cancellationToken = default);
    /// <summary>Returns all journal entries visible to the manager: their own entries about direct reports + shared employee entries.</summary>
    Task<IEnumerable<TeamJournalListDto>> GetTeamJournalAsync(
        Guid managerId,
        IEnumerable<Guid> directReportIds,
        Guid? cycleId = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken cancellationToken = default);
}

#endregion Performance Journal

#region Development Plan

public interface IDevelopmentPlanService
{
    Task<EmployeeDevelopmentPlanDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<EmployeeDevelopmentPlanDto>> GetByEmployeeIdAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<EmployeeDevelopmentPlanDto?> GetActivePlanAsync(Guid employeeId, Guid? cycleId = null, CancellationToken cancellationToken = default);
    Task<PagedResult<EmployeeDevelopmentPlanDto>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<EmployeeDevelopmentPlanDto> CreateAsync(CreateEmployeeDevelopmentPlanDto createDto, CancellationToken cancellationToken = default);
    Task<EmployeeDevelopmentPlanDto> UpdateAsync(UpdateEmployeeDevelopmentPlanDto updateDto, CancellationToken cancellationToken = default);
    // P10: actorEmployeeId (from the token) — the plan's subject cannot delete, complete or cancel
    // a plan they did not write; UnauthorizedAccessException when they try.
    Task<bool> DeleteAsync(Guid id, Guid? actorEmployeeId, CancellationToken cancellationToken = default);
    Task<bool> UpdateStatusAsync(Guid id, DevelopmentPlanStatus status, Guid? actorEmployeeId, CancellationToken cancellationToken = default);

    // Manager operations
    Task<IEnumerable<EmployeeDevelopmentPlanDto>> GetByManagerIdAsync(Guid managerId, CancellationToken cancellationToken = default);

    // Objective operations
    Task<EmployeeDevelopmentObjectiveDto> AddObjectiveAsync(Guid planId, CreateEmployeeDevelopmentObjectiveDto dto, CancellationToken cancellationToken = default);
    Task<IEnumerable<EmployeeDevelopmentObjectiveDto>> GetObjectivesAsync(Guid planId, CancellationToken cancellationToken = default);
    Task<EmployeeDevelopmentObjectiveDto> UpdateObjectiveAsync(Guid planId, UpdateEmployeeDevelopmentObjectiveDto dto, CancellationToken cancellationToken = default);
    Task<bool> DeleteObjectiveAsync(Guid planId, Guid objectiveId, CancellationToken cancellationToken = default);
    Task<EmployeeDevelopmentObjectiveDto> UpdateObjectiveProgressAsync(Guid planId, Guid objectiveId, decimal progressPercent, string? notes, DevelopmentObjectiveStatus status, CancellationToken cancellationToken = default);
}

#endregion Development Plan

#region Development Plan Feedback

public interface IDevelopmentPlanFeedbackService
{
    Task<IEnumerable<EmployeeDevelopmentPlanFeedbackDto>> GetByPlanIdAsync(Guid planId, CancellationToken cancellationToken = default);
    Task<EmployeeDevelopmentPlanFeedbackDto> AddAsync(CreateEmployeeDevelopmentPlanFeedbackDto dto, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid feedbackId, CancellationToken cancellationToken = default);
}

#endregion Development Plan Feedback

#region Appraisal Review Event

public interface IAppraisalReviewEventService
{
    Task<AppraisalReviewEventDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<AppraisalReviewEventDto>> GetByAppraisalIdAsync(Guid appraisalId, CancellationToken cancellationToken = default);
    Task<IEnumerable<AppraisalReviewEventDto>> GetByCycleIdAsync(Guid cycleId, CancellationToken cancellationToken = default);
    Task<IEnumerable<AppraisalReviewEventDto>> GetByTypeAsync(Guid appraisalId, ReviewEventType type, CancellationToken cancellationToken = default);
    /// <summary>Every review event across one employee's appraisals — the "my checkpoints" list.</summary>
    Task<IEnumerable<AppraisalReviewEventDto>> GetForEmployeeAsync(Guid employeeId, Guid? cycleId = null, CancellationToken cancellationToken = default);
    /// <summary>Review events for everyone reporting to <paramref name="managerId"/>.</summary>
    Task<IEnumerable<AppraisalReviewEventDto>> GetForManagerAsync(Guid managerId, Guid? cycleId = null, CancellationToken cancellationToken = default);
    Task<AppraisalReviewEventDto> CreateAsync(CreateAppraisalReviewEventDto createDto, CancellationToken cancellationToken = default);
    Task<AppraisalReviewEventDto> UpdateAsync(UpdateAppraisalReviewEventDto updateDto, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>Employee submits their side — moves status to <c>EmployeeSubmitted</c>.</summary>
    Task<AppraisalReviewEventDto> SubmitEventAsync(Guid eventId, string? achievementsSummary, string? challengesSummary, CancellationToken cancellationToken = default);
    /// <summary>Manager closes the event — moves status to <c>Completed</c>.</summary>
    Task<AppraisalReviewEventDto> CompleteEventAsync(Guid eventId, string? notes, string? managerNotes, CancellationToken cancellationToken = default);

    // Full interim appraisal (Theme 7) — score the period's goals and aggregate a period score
    Task<FullInterimAppraisalContextDto> GetFullAppraisalContextAsync(Guid eventId, CancellationToken cancellationToken = default);
    Task<AppraisalReviewEventDto> FinalizeFullAppraisalAsync(Guid eventId, FinalizeFullInterimAppraisalDto dto, Guid recordedById, CancellationToken cancellationToken = default);

    // Progress entries recorded during this review event.
    // recordedById comes from the caller's token — never from the payload.
    Task<GoalProgressEntryDto> RecordProgressEntryAsync(Guid eventId, CreateGoalProgressEntryDto dto, Guid recordedById, CancellationToken cancellationToken = default);
    Task<IEnumerable<GoalProgressEntryDto>> GetProgressEntriesAsync(Guid eventId, CancellationToken cancellationToken = default);

    // Attachment operations. The file itself goes through the controlled-upload gate in the
    // controller, which hands the stored document's identifiers over here.
    Task<AppraisalAttachmentDto> AddAttachmentAsync(
        Guid eventId, Guid uploadedById, string fileName, long? fileSizeBytes, string? description,
        CancellationToken cancellationToken = default,
        Guid? fileUploadRecordId = null, Guid? documentRecordId = null, Guid? documentVersionId = null);
    Task<IEnumerable<AppraisalAttachmentDto>> GetAttachmentsAsync(Guid eventId, CancellationToken cancellationToken = default);
    Task<AppraisalAttachmentDto?> GetAttachmentAsync(Guid eventId, Guid attachmentId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAttachmentAsync(Guid eventId, Guid attachmentId, CancellationToken cancellationToken = default);
}

#endregion Appraisal Review Event

#region Appraisal Conversation

public interface IAppraisalConversationService
{
    Task<AppraisalConversationDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<AppraisalConversationDto>> GetByAppraisalIdAsync(Guid appraisalId, CancellationToken cancellationToken = default);
    Task<IEnumerable<AppraisalConversationDto>> GetByTypeAsync(Guid appraisalId, ConversationType type, CancellationToken cancellationToken = default);
    Task<IEnumerable<AppraisalConversationDto>> GetScheduledByManagerAsync(Guid managerId, CancellationToken cancellationToken = default);
    /// <summary>Conversations about a given employee, keyed on the appraisal rather than the scheduler.</summary>
    Task<IEnumerable<AppraisalConversationDto>> GetByAppraiseeAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<AppraisalConversationDto> CreateAsync(CreateAppraisalConversationDto createDto, CancellationToken cancellationToken = default);
    Task<AppraisalConversationDto> UpdateAsync(UpdateAppraisalConversationDto updateDto, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<AppraisalConversationDto> CompleteAsync(Guid conversationId, string? postMeetingNotes, string? keyTakeaways, CancellationToken cancellationToken = default);
}

#endregion Appraisal Conversation

#region Calibration

public interface ICalibrationSessionService
{
    Task<CalibrationSessionDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    // P3: panellistEmployeeId narrows the list to the sessions that employee sits on (participant
    // or facilitator); null is the desk's whole list.
    Task<IEnumerable<CalibrationSessionDto>> GetByCycleIdAsync(Guid cycleId, Guid? panellistEmployeeId, CancellationToken cancellationToken = default);
    Task<PagedResult<CalibrationSessionDto>> GetPagedAsync(int pageNumber, int pageSize, Guid? panellistEmployeeId, CancellationToken cancellationToken = default);
    Task<CalibrationSessionDto> CreateAsync(CreateCalibrationSessionDto createDto, CancellationToken cancellationToken = default);
    Task<CalibrationSessionDto> UpdateAsync(UpdateCalibrationSessionDto updateDto, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    // Lifecycle
    Task<CalibrationSessionDto> OpenSessionAsync(Guid sessionId, Guid facilitatedById, CancellationToken cancellationToken = default);
    Task<CalibrationSessionDto> StartSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<CalibrationSessionDto> CompleteSessionAsync(Guid sessionId, Guid completedById, string? meetingNotes, CancellationToken cancellationToken = default);

    // Participant management
    Task<CalibrationParticipantDto> AddParticipantAsync(Guid sessionId, CreateCalibrationParticipantDto dto, CancellationToken cancellationToken = default);
    Task<IEnumerable<CalibrationParticipantDto>> GetParticipantsAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<bool> RemoveParticipantAsync(Guid sessionId, Guid participantId, CancellationToken cancellationToken = default);
    Task<bool> RecordAttendanceAsync(Guid sessionId, Guid participantId, bool attended, CancellationToken cancellationToken = default);

    // Rating adjustment management
    Task<CalibrationRatingAdjustmentDto> AddRatingAdjustmentAsync(Guid sessionId, CreateCalibrationRatingAdjustmentDto dto, Guid adjustedById, CancellationToken cancellationToken = default);
    // P3: viewerEmployeeId's own appraisals are left out of every calibration read (the two-actor
    // rule — a panellist's own row is their outcome before it is released).
    Task<IEnumerable<CalibrationRatingAdjustmentDto>> GetRatingAdjustmentsAsync(Guid sessionId, Guid? viewerEmployeeId, CancellationToken cancellationToken = default);
    Task<IEnumerable<CalibrationRatingAdjustmentDto>> GetAdjustmentsByAppraisalAsync(Guid sessionId, Guid appraisalId, Guid? viewerEmployeeId, CancellationToken cancellationToken = default);
    Task<CalibrationRatingAdjustmentDto> UpdateRatingAdjustmentAsync(Guid sessionId, UpdateCalibrationRatingAdjustmentDto dto, Guid adjustedById, CancellationToken cancellationToken = default);
    Task<bool> DeleteRatingAdjustmentAsync(Guid sessionId, Guid adjustmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits the session: applies each appraisal's latest adjustments and lifts the calibration
    /// gate on <em>every</em> appraisal in the session's scope, not only the adjusted ones.
    /// </summary>
    Task<CalibrationApplyResultDto> ApplyAllAdjustmentsAsync(Guid sessionId, Guid appliedById, CancellationToken cancellationToken = default);

    /// <summary>Appraisals the session covers: its cycle, narrowed to its organization unit (with descendants) or level.</summary>
    Task<IReadOnlyCollection<Guid>> GetScopedAppraisalIdsAsync(Guid sessionId, CancellationToken cancellationToken = default);

    // Calibration matrix view
    Task<CalibrationMatrixDto> GetCalibrationMatrixAsync(Guid sessionId, Guid? viewerEmployeeId, CancellationToken cancellationToken = default);

    // Attachment operations
    // See the note on ICheckInService's attachment methods — the file goes through the gate in the
    // controller and uploadedById comes from the token.
    Task<AppraisalAttachmentDto> AddAttachmentAsync(
        Guid sessionId, Guid uploadedById, string fileName, long? fileSizeBytes, string? description,
        CancellationToken cancellationToken = default,
        Guid? fileUploadRecordId = null, Guid? documentRecordId = null, Guid? documentVersionId = null);
    Task<IEnumerable<AppraisalAttachmentDto>> GetAttachmentsAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<AppraisalAttachmentDto?> GetAttachmentAsync(Guid sessionId, Guid attachmentId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAttachmentAsync(Guid sessionId, Guid attachmentId, CancellationToken cancellationToken = default);
    /// <summary>An appraisal's frozen criteria, weights, manager scores and existing adjustments.</summary>
    // P3: only an appraisal in the session's scope, and never the viewer's own.
    Task<IEnumerable<CalibrationCriterionDto>> GetAppraisalCriteriaAsync(Guid sessionId, Guid appraisalId, Guid? viewerEmployeeId, CancellationToken cancellationToken = default);
}

#endregion Calibration

#region Appraisal Workflow

/// <summary>
/// Provides appraisal lifecycle helpers that are decoupled from data-persistence operations:
/// fine-grained phase computation, lifecycle transition enforcement, and role-based edit guards.
/// </summary>
public interface IAppraisalWorkflowService
{
    /// <summary>
    /// Computes the current fine-grained <see cref="AppraisalPhase"/> from appraisal data.
    /// The result is NOT persisted — it is always derived from live entity state.
    /// <para>The <paramref name="appraisal"/> must have <c>AppraisalCycle.AppraisalSettings</c> loaded.</para>
    /// </summary>
    AppraisalPhase GetCurrentPhase(PerformanceAppraisal appraisal);

    /// <summary>
    /// Enforces the lifecycle transition table and persists the new <see cref="AppraisalStatus"/>
    /// on the specified appraisal.
    /// Throws <see cref="InvalidOperationException"/> when the transition is not permitted.
    /// </summary>
    Task TransitionAsync(Guid appraisalId, AppraisalStatus newStatus, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns <c>true</c> when the given role-holder is permitted to submit edits to the appraisal
    /// in its current lifecycle state and phase.
    /// </summary>
    /// <param name="appraisal">The appraisal (must have <c>AppraisalCycle.AppraisalSettings</c> loaded).</param>
    /// <param name="role">One of: <c>Employee</c>, <c>Manager</c>, <c>Peer</c>, <c>HR</c>.</param>
    bool IsEditableByRole(PerformanceAppraisal appraisal, string role);

    /// <summary>
    /// Loads the appraisal with required navigations and returns the current
    /// <see cref="AppraisalPhase"/>. Controller-friendly async wrapper.
    /// </summary>
    Task<AppraisalPhase> GetCurrentPhaseAsync(Guid appraisalId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the appraisal with required navigations and returns whether the
    /// specified role may currently submit edits. Controller-friendly async wrapper.
    /// </summary>
    Task<bool> IsEditableByRoleAsync(Guid appraisalId, string role, CancellationToken cancellationToken = default);

    /// <summary>
    /// HR-initiated manual advance: completes a single stalled pipeline sub-step, performs the
    /// appropriate auto-completion data actions, automatically triggers any required major-status
    /// transition (Active→Governance, Governance→Completed), and writes an audit log record.
    /// </summary>
    /// <param name="appraisalId">Target appraisal.</param>
    /// <param name="targetSubStatus">
    /// The sub-step to advance past. When <c>null</c> (or omitted), advances past the current
    /// blocking sub-step as resolved from live entity state.
    /// </param>
    /// <param name="reason">HR-provided justification stored verbatim in the audit log.</param>
    /// <param name="advancedByEmployeeId">Employee ID of the HR officer performing the action.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<ManualAdvanceResult> ManuallyAdvanceStepAsync(
        Guid appraisalId,
        AppraisalSubStatus? targetSubStatus,
        string reason,
        Guid advancedByEmployeeId,
        CancellationToken ct = default);

    /// <summary>
    /// HR-initiated "advance overdue appraisals" for a cycle. Honours the cycle's
    /// <c>AutoLockOnDeadline</c> setting: when enabled, every active appraisal whose current blocking
    /// sub-step's deadline has passed is advanced via the manual-advance path (audit-logged). When the
    /// setting is disabled, nothing is advanced. There is no background job — HR triggers this on demand.
    /// </summary>
    Task<DeadlineEnforcementResult> AdvanceOverdueAppraisalsAsync(
        Guid cycleId,
        Guid advancedByEmployeeId,
        CancellationToken ct = default);
}

#endregion Appraisal Workflow

#region Effective Appraisal Configuration

/// <summary>
/// Resolves the effective appraisal configuration (criteria, weights, grade bands) for
/// an employee in a given cycle by merging the template defaults with any matching
/// PositionCriteriaMapping overrides. Scope resolution is deterministic:
/// Employee > Position > OrganizationUnit > OrganizationLevel.
/// Throws InvalidOperationException if two same-level PCMs match the same employee.
/// </summary>
public interface IEffectiveAppraisalConfigurationService
{
    /// <summary>
    /// Resolves the full effective appraisal config for a given employee and cycle.
    /// </summary>
    Task<EffectiveAppraisalConfigDto> ResolveForEmployeeAsync(Guid employeeId, Guid cycleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates and persists a PerformanceAppraisalCriterionConfig snapshot for a new appraisal
    /// based on the resolved effective config.
    /// When <paramref name="templateId"/> is supplied the snapshot is anchored to that specific
    /// template (no re-resolution); omit it to let the service resolve automatically.
    /// </summary>
    Task SnapshotConfigAsync(Guid performanceAppraisalId, Guid employeeId, Guid cycleId, Guid? templateId = null, CancellationToken cancellationToken = default);
}

#endregion Effective Appraisal Configuration

#region Cycle Coverage

/// <summary>
/// Simulates appraisal generation for a cycle and returns coverage statistics.
/// Read-only — no records are written.
/// </summary>
public interface ICycleCoverageService
{
    /// <summary>
    /// Returns a full pre-flight coverage simulation for the given cycle.
    /// Supports pagination for cycles with large employee populations.
    /// </summary>
    Task<CoveragePreviewDto> GetCoveragePreviewAsync(
        Guid cycleId,
        int pageNumber = 1,
        int pageSize = 200,
        CancellationToken cancellationToken = default);
}

#endregion Cycle Coverage

#region Team Goals Query

/// <summary>
/// Read-only governance query service powering the Advanced Manager Workspace
/// at route /performance/team-goals.
///
/// Design contract:
///   - All methods resolve the calling manager from ICurrentUserService.
///   - Security is enforced at the query layer: only direct reports
///     (Employee.ManagerId == currentManagerId) are ever returned.
///   - No mutation logic lives here. Approve / reject operations belong
///     to IEmployeeGoalService.
///   - Uses projection DTOs only. No navigation graph loading (Include-free).
///   - All queries are AsNoTracking() and fully async.
/// </summary>
public interface ITeamGoalsQueryService
{
    /// <summary>
    /// Returns one <see cref="TeamMemberOverviewDto"/> per direct report
    /// that includes goal counts split by workflow/execution state,
    /// weight balance, overdue count, and a derived GovernanceStatus.
    /// Employees with zero goals appear with GovernanceStatus.NotStarted.
    /// </summary>
    Task<List<TeamMemberOverviewDto>> GetTeamOverviewAsync(
        Guid appraisalCycleId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all direct-report goals with Status == PendingApproval.
    /// Powers the "Awaiting Approval" tab.
    /// </summary>
    Task<List<TeamGoalFlatDto>> GetGoalsAwaitingApprovalAsync(
        Guid appraisalCycleId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all goals classified as at-risk:
    ///   • Status == AtRisk
    ///   • OR (Status is InProgress/OnTrack AND DueDate &lt;= Today+14d AND ProgressPercent &lt; 50)
    /// Powers the "At Risk" tab.
    /// </summary>
    Task<List<TeamGoalFlatDto>> GetAtRiskGoalsAsync(
        Guid appraisalCycleId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all goals where DueDate &lt; Today AND Status is not Completed or Locked.
    /// Status is the single truth; the legacy IsLocked flag is not consulted.
    /// Powers the "Overdue" tab.
    /// </summary>
    Task<List<TeamGoalFlatDto>> GetOverdueGoalsAsync(
        Guid appraisalCycleId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all goals with Status == Locked.
    /// Powers the "Locked" tab.
    /// </summary>
    Task<List<TeamGoalFlatDto>> GetLockedGoalsAsync(
        Guid appraisalCycleId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns ALL goals for a specific direct report in the given cycle,
    /// regardless of status. Enforces direct-report security: throws
    /// <see cref="UnauthorizedAccessException"/> if <paramref name="employeeId"/>
    /// is not a direct report of the currently authenticated manager.
    /// Powers the Employee Drill-Down page.
    /// </summary>
    Task<List<TeamGoalFlatDto>> GetEmployeeGoalsAsync(
        Guid employeeId,
        Guid appraisalCycleId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns one <see cref="TeamGoalProgressDto"/> per direct report
    /// showing execution progress: goal counts by status, average progress %,
    /// and a per-goal summary list.
    /// Powers the Manager Team Goal Progress page.
    /// </summary>
    Task<List<TeamGoalProgressDto>> GetTeamGoalProgressAsync(
        Guid appraisalCycleId,
        CancellationToken cancellationToken = default);
}

#endregion Team Goals Query

#region Org-Wide At-Risk Goals Query

/// <summary>
/// Read-only org-wide at-risk goal query service for HR and admin roles.
///
/// Unlike <see cref="ITeamGoalsQueryService"/>, which enforces manager →
/// direct-report scoping, this service queries across ALL employees in the
/// organisation for the given appraisal cycle. Access control is enforced
/// at the controller layer via [Authorize(Roles = ...)].
///
/// Architecture contracts:
///   - All queries are AsNoTracking() and fully async.
///   - Two-phase pipeline: SQL pre-filter using indexed columns →
///     in-memory IGoalRiskEvaluator for full rule evaluation.
///   - IGoalRiskSettingsProvider is called once per request.
///   - Results are sorted by RiskSeverityScore descending, then DaysRemaining ascending.
/// </summary>
public interface IAtRiskGoalsQueryService
{
    /// <summary>
    /// Returns all at-risk goals across the entire organisation for the cycle
    /// specified in <paramref name="request"/>.
    /// Optionally filtered to one department via <see cref="AtRiskGoalsRequest.DepartmentId"/>.
    ///
    /// A goal qualifies when:
    ///   • Status == AtRisk, OR
    ///   • Status is InProgress/OnTrack
    ///     AND DueDate &lt;= today + <c>GoalRiskSetting.DaysRemainingThreshold</c>
    ///     AND ProgressPercent &lt; <c>GoalRiskSetting.MinimumProgressPercent</c>
    ///
    /// The evaluator is authoritative — SQL pre-filter widens the candidate set;
    /// IGoalRiskEvaluator narrows it to confirmed at-risk goals only.
    /// </summary>
    Task<List<TeamGoalFlatDto>> GetOrgWideAtRiskGoalsAsync(
        AtRiskGoalsRequest request,
        CancellationToken cancellationToken = default);
}

#endregion Org-Wide At-Risk Goals Query

#region Appraisal Notifications

/// <summary>
/// One in-app appraisal notification to be written. <c>RecipientEmployeeId</c> is a real
/// <c>Employee</c> foreign key — a request naming an unknown employee is dropped rather than
/// failing the batch, because notifications are a side effect of a real action and must never
/// be the reason that action fails.
/// </summary>
public sealed record AppraisalNotificationRequest(
    Guid RecipientEmployeeId,
    AppraisalNotificationType Type,
    string Title,
    string Message,
    string? CycleName = null,
    string? NavigationUrl = null,
    Guid? AppraisalId = null,
    string? SubjectEmployeeName = null,
    NotificationUrgency Urgency = NotificationUrgency.Normal);

public interface IAppraisalNotificationService
{
    Task<AppraisalNotificationSummaryDto> GetNotificationSummaryAsync(
        Guid employeeId, int recentCount = 20, CancellationToken ct = default);

    Task<List<AppraisalNotificationDto>> GetAllNotificationsAsync(
        Guid employeeId, int page = 1, int pageSize = 20, CancellationToken ct = default);

    Task MarkAsReadAsync(Guid notificationId, CancellationToken ct = default);

    Task MarkAllAsReadAsync(Guid employeeId, CancellationToken ct = default);

    Task<int> GetUnreadCountAsync(Guid employeeId, CancellationToken ct = default);

    /// <summary>
    /// Writes a batch of notifications and returns how many were stored. Duplicates of an
    /// unread notification of the same type for the same recipient and cycle are skipped, so
    /// re-running a reminder does not pile up identical rows.
    /// </summary>
    Task<int> RaiseAsync(IEnumerable<AppraisalNotificationRequest> requests, CancellationToken ct = default);
}

#endregion Appraisal Notifications
#region Goal Risk Settings

/// <summary>
/// Read/write access to the tenant's goal-risk thresholds — the numbers
/// <c>IGoalRiskEvaluator</c> uses to decide a goal is at risk.
///
/// Distinct from <c>IGoalRiskSettingsProvider</c>, which is the read-only path the
/// evaluation pipeline uses and which falls back to defaults when nothing is stored.
/// </summary>
public interface IGoalRiskSettingsService
{
    /// <summary>
    /// The thresholds in force. Returns the documented defaults, with
    /// <c>IsConfigured</c> false, when the tenant has never saved its own.
    /// </summary>
    Task<GoalRiskSettingsDto> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the tenant's thresholds, creating the row on first save and
    /// retiring any other active row so exactly one stays active.
    /// </summary>
    Task<GoalRiskSettingsDto> SaveAsync(UpdateGoalRiskSettingsDto dto, CancellationToken cancellationToken = default);

    /// <summary>Drops the tenant's stored thresholds, returning it to the defaults.</summary>
    Task<GoalRiskSettingsDto> ResetAsync(CancellationToken cancellationToken = default);
}

#endregion Goal Risk Settings
