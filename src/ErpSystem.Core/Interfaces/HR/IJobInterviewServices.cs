using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// INTERVIEW QUESTION PRESET SERVICE
// ============================================================================

public interface IInterviewQuestionPresetService
{
    Task<IEnumerable<InterviewQuestionPresetSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<InterviewQuestionPresetDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<InterviewQuestionPresetDto> CreateAsync(CreateInterviewQuestionPresetDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<InterviewQuestionPresetDto> UpdateAsync(UpdateInterviewQuestionPresetDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    // Items
    Task<InterviewQuestionPresetItemDto> AddItemAsync(CreateInterviewQuestionPresetItemDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<InterviewQuestionPresetItemDto> UpdateItemAsync(UpdateInterviewQuestionPresetItemDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteItemAsync(Guid itemId, CancellationToken cancellationToken = default);
}

// ============================================================================
// INTERVIEW QUESTION BANK SERVICE
// ============================================================================

public interface IJobInterviewQuestionBankService
{
    // Question types
    Task<JobInterviewQuestionTypeDto> GetQuestionTypeByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobInterviewQuestionTypeSummaryDto>> GetAllQuestionTypesAsync(CancellationToken cancellationToken = default);
    Task<JobInterviewQuestionTypeDto> CreateQuestionTypeAsync(CreateJobInterviewQuestionTypeDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<JobInterviewQuestionTypeDto> UpdateQuestionTypeAsync(UpdateJobInterviewQuestionTypeDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteQuestionTypeAsync(Guid questionTypeId, CancellationToken cancellationToken = default);

    Task<JobInterviewQuestionTypeDto?> GetQuestionTypeByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<JobInterviewQuestionTypeDto?> GetQuestionTypeWithQuestionsAsync(Guid id, CancellationToken cancellationToken = default);

    // Question details
    Task<JobInterviewQuestionDetailDto> GetQuestionDetailByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobInterviewQuestionDetailDto>> GetQuestionDetailsByTypeAsync(Guid questionTypeId, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobInterviewQuestionDetailDto>> GetAllQuestionDetailsAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<JobInterviewQuestionDetailDto>> GetActiveQuestionsAsync(Guid? questionTypeId = null, CancellationToken cancellationToken = default);
    Task<JobInterviewQuestionDetailDto> CreateQuestionDetailAsync(CreateJobInterviewQuestionDetailDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<JobInterviewQuestionDetailDto> UpdateQuestionDetailAsync(UpdateJobInterviewQuestionDetailDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteQuestionDetailAsync(Guid questionDetailId, CancellationToken cancellationToken = default);
}

// ============================================================================
// INTERVIEW SERVICE
// ============================================================================

public interface IJobInterviewService
{
    // Queries
    Task<JobInterviewDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<JobInterviewDto?> GetByInterviewNumberAsync(string interviewNumber, CancellationToken cancellationToken = default);
    Task<JobInterviewDetailDto> GetWithFullDetailsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobInterviewSummaryDto>> GetByVacancyIdAsync(Guid vacancyId, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobInterviewSummaryDto>> GetByStatusAsync(JobInterviewStatus status, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobInterviewSummaryDto>> GetByDateRangeAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobInterviewSummaryDto>> GetByRoundAsync(Guid vacancyId, int round, CancellationToken cancellationToken = default);

    /// <summary>
    /// Advisory conflict check over a proposed slot. Internal panelists are checked for overlapping
    /// interviews plus approved/pending leave and travel; external panelists (associates) are checked
    /// only for overlapping interviews, since we don't track their leave/travel. Mirrors trainer availability.
    /// </summary>
    Task<PanelistAvailabilityCheckDto> CheckPanelistAvailabilityAsync(
        IReadOnlyList<Guid> panelistEmployeeIds, IReadOnlyList<Guid> externalAssociateIds,
        DateOnly date, TimeSpan start, TimeSpan end,
        Guid? excludeInterviewId, CancellationToken cancellationToken = default);

    // CRUD
    Task<JobInterviewDto> CreateAsync(CreateJobInterviewDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<JobInterviewDto> UpdateAsync(UpdateJobInterviewDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    // Workflow
    Task<bool> RescheduleAsync(RescheduleJobInterviewDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> CancelAsync(CancelJobInterviewDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> CompleteAsync(Guid interviewId, Guid updatedByUserId, CancellationToken cancellationToken = default);

    // Panelists (internal)
    Task<JobInterviewPanelistDto> AddPanelistAsync(AddJobInterviewPanelistDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<JobInterviewPanelistDto> UpdatePanelistAsync(UpdateJobInterviewPanelistDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> RemovePanelistAsync(Guid panelistId, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobInterviewPanelistDto>> GetPanelistsAsync(Guid interviewId, CancellationToken cancellationToken = default);
    Task<bool> ConfirmPanelistAsync(Guid panelistId, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> RecordPanelistAttendanceAsync(Guid panelistId, bool? attended, string? noShowReason, Guid updatedByUserId, CancellationToken cancellationToken = default);

    // External panelists
    Task<JobInterviewExternalPanelistDto> AddExternalPanelistAsync(AddJobInterviewExternalPanelistDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<JobInterviewExternalPanelistDto> UpdateExternalPanelistAsync(UpdateJobInterviewExternalPanelistDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> RemoveExternalPanelistAsync(Guid externalPanelistId, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobInterviewExternalPanelistDto>> GetExternalPanelistsAsync(Guid interviewId, CancellationToken cancellationToken = default);
    Task<bool> RecordExternalPanelistAttendanceAsync(Guid extPanelistId, bool? attended, string? noShowReason, Guid updatedByUserId, CancellationToken cancellationToken = default);

    // Interviewees
    Task<JobIntervieweeDto> AddIntervieweeAsync(AddJobIntervieweeDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<bool> RemoveIntervieweeAsync(Guid intervieweeId, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobIntervieweeDto>> GetIntervieweesAsync(Guid interviewId, CancellationToken cancellationToken = default);
    Task<bool> UpdateIntervieweeSlotAsync(UpdateIntervieweeSlotDto dto, CancellationToken cancellationToken = default);
    Task<ConfirmPanelistAssignmentResultDto> ConfirmPanelistAssignmentByTokenAsync(string token, CancellationToken cancellationToken = default);
    Task<bool> RecordAttendanceAsync(Guid intervieweeId, bool? attended, string? noShowReason, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> RecordOutcomeAsync(RecordIntervieweeOutcomeDto dto, Guid updatedByUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Send interview invite emails to the specified candidates (or all interviewees when
    /// <paramref name="applicationIds"/> is empty). Updates <c>InvitationSentDate</c> and
    /// <c>ConfirmationToken</c> on each successful send.
    /// </summary>
    Task<SendInterviewInvitesResultDto> SendInvitesAsync(Guid interviewId, List<Guid> applicationIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Send panel assignment notification emails to the specified internal and/or external
    /// panelists. Updates <c>InvitationSentDate</c> on each successful send.
    /// </summary>
    Task<SendPanelistNotificationsResultDto> SendPanelistNotificationsAsync(
        Guid interviewId,
        List<Guid>? employeeIds,
        List<Guid>? externalAssociateIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// token embedded in their invitation email. Idempotent — succeeds silently if already confirmed.
    /// </summary>
    Task<ConfirmInterviewAttendanceResultDto> ConfirmAttendanceByTokenAsync(string token, CancellationToken cancellationToken = default);

    // Question plans
    Task<JobInterviewQuestionDto> AddQuestionPlanAsync(CreateJobInterviewQuestionDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobInterviewQuestionDto>> GetQuestionPlansAsync(Guid interviewId, CancellationToken cancellationToken = default);
    Task<JobInterviewQuestionDto> UpdateQuestionPlanAsync(UpdateJobInterviewQuestionDto updateDto, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteQuestionPlanAsync(Guid questionPlanId, CancellationToken cancellationToken = default);
    Task<bool> AddSelectedQuestionAsync(Guid questionPlanId, Guid questionDetailId, Guid updatedByUserId, CancellationToken cancellationToken = default);
    Task<bool> RemoveSelectedQuestionAsync(Guid questionPlanId, Guid questionDetailId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Randomly selects questions from the active pool for each question plan of an interview,
    /// replacing any existing selections. Up to <c>AllowedPoolSize</c> questions are drawn per plan.
    /// </summary>
    Task SelectQuestionsAsync(Guid interviewId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Dry-run randomisation — returns proposed question selections for each plan without persisting.
    /// </summary>
    Task<List<QuestionPlanPreviewDto>> PreviewSelectQuestionsAsync(Guid interviewId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists an explicit, user-confirmed list of selected questions for each plan,
    /// replacing any existing selections.
    /// </summary>
    Task CommitSelectedQuestionsAsync(Guid interviewId, CommitInterviewQuestionsDto dto, CancellationToken cancellationToken = default);

    // Panelist / interviewee cross-queries
    Task<IEnumerable<JobInterviewPanelistDto>> GetInterviewsByPanelistAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobIntervieweeDto>> GetInterviewsByApplicationAsync(Guid applicationId, CancellationToken cancellationToken = default);

    // Score summaries
    Task<JobInterviewScoreSummaryDto> CreateScoreSummaryAsync(CreateJobInterviewScoreSummaryDto createDto, Guid tenantId, Guid createdByUserId, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobInterviewScoreSummaryDto>> GetScoreSummariesAsync(Guid intervieweeId, CancellationToken cancellationToken = default);
    Task<JobInterviewScoreSummaryDetailDto> GetScoreSummaryDetailAsync(Guid scoreSummaryId, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobInterviewScoreSummaryDto>> GetScoresByInternalPanelistAsync(Guid panelistId, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobInterviewScoreSummaryDto>> GetScoresByExternalPanelistAsync(Guid externalPanelistId, CancellationToken cancellationToken = default);
    Task<IEnumerable<JobInterviewScoreSummaryDto>> GetFinalizedScoresForIntervieweeAsync(Guid intervieweeId, CancellationToken cancellationToken = default);
    Task<bool> FinalizeScoreAsync(Guid scoreSummaryId, Guid updatedByUserId, CancellationToken cancellationToken = default);

    // Score entries
    Task<IEnumerable<JobInterviewScoreEntryDto>> GetScoreEntriesAsync(Guid scoreSummaryId, CancellationToken cancellationToken = default);

    // Score drafts
    Task<InterviewScoreDraftDto?> GetScoreDraftAsync(
        Guid intervieweeId,
        Guid? internalPanelistId,
        Guid? externalPanelistId,
        CancellationToken cancellationToken = default);

    Task<InterviewScoreDraftDto> SaveScoreDraftAsync(
        Guid interviewId,
        SaveInterviewScoreDraftDto dto,
        Guid tenantId,
        CancellationToken cancellationToken = default);
}
