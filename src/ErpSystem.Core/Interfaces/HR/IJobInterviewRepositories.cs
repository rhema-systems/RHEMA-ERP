using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// JOB INTERVIEW QUESTION TYPE
// ============================================================================

#region Job Interview Question Type

public interface IJobInterviewQuestionTypeRepository : IGenericRepository<JobInterviewQuestionType>
{
    /// <summary>Returns a question type by its code.</summary>
    Task<JobInterviewQuestionType?> GetByCodeAsync(string code);

    /// <summary>Returns all active question types, with question detail counts.</summary>
    Task<IEnumerable<JobInterviewQuestionType>> GetActiveTypesAsync();

    /// <summary>Returns a question type with all its active question details loaded.</summary>
    Task<JobInterviewQuestionType?> GetWithQuestionsAsync(Guid id);
}

#endregion

// ============================================================================
// JOB INTERVIEW QUESTION DETAIL
// ============================================================================

#region Job Interview Question Detail

public interface IJobInterviewQuestionDetailRepository : IGenericRepository<JobInterviewQuestionDetail>
{
    /// <summary>Returns all question details (including inactive) with the QuestionType navigation property loaded.</summary>
    Task<IEnumerable<JobInterviewQuestionDetail>> GetAllWithTypeAsync();

    /// <summary>Returns all active question details for a given question type.</summary>
    Task<IEnumerable<JobInterviewQuestionDetail>> GetByQuestionTypeIdAsync(Guid questionTypeId);

    /// <summary>Returns active question details, optionally filtered by type — used for random question pool selection.</summary>
    Task<IEnumerable<JobInterviewQuestionDetail>> GetActiveQuestionsAsync(Guid? questionTypeId = null);
}

#endregion

// ============================================================================
// INTERVIEW QUESTION PRESET
// ============================================================================

#region Interview Question Preset

public interface IInterviewQuestionPresetRepository : IGenericRepository<InterviewQuestionPreset>
{
    Task<IEnumerable<InterviewQuestionPreset>> GetAllWithItemsAsync();
    Task<InterviewQuestionPreset?> GetWithItemsAsync(Guid id);
}

public interface IInterviewQuestionPresetItemRepository : IGenericRepository<InterviewQuestionPresetItem>
{
    Task<IEnumerable<InterviewQuestionPresetItem>> GetByPresetIdAsync(Guid presetId);
}

#endregion

// ============================================================================
// JOB INTERVIEW
// ============================================================================

#region Job Interview

public interface IJobInterviewRepository : IGenericRepository<JobInterview>
{
    /// <summary>Returns the interview matching the unique interview number.</summary>
    Task<JobInterview?> GetByInterviewNumberAsync(string interviewNumber);

    /// <summary>
    /// Returns a fully-loaded interview including interviewees, panelists, external panelists,
    /// question sets, and selected questions.
    /// </summary>
    Task<JobInterview?> GetWithFullDetailsAsync(Guid id);

    /// <summary>Returns all interviews for a vacancy, ordered by scheduled date.</summary>
    Task<IEnumerable<JobInterview>> GetByVacancyIdAsync(Guid vacancyId);

    /// <summary>Returns interviews filtered by status.</summary>
    Task<IEnumerable<JobInterview>> GetByStatusAsync(JobInterviewStatus status);

    /// <summary>Returns interviews scheduled within the given date range.</summary>
    Task<IEnumerable<JobInterview>> GetByDateRangeAsync(DateOnly from, DateOnly to);

    /// <summary>Returns interviews for a specific round number across all vacancies.</summary>
    Task<IEnumerable<JobInterview>> GetByRoundAsync(Guid vacancyId, int round);

    /// <summary>Returns the next interview number for auto-generation.</summary>
    Task<string> GetNextInterviewNumberAsync();
}

#endregion

// ============================================================================
// JOB INTERVIEW PANELIST
// ============================================================================

#region Job Interview Panelist

public interface IJobInterviewPanelistRepository : IGenericRepository<JobInterviewPanelist>
{
    /// <summary>Returns all panelists for an interview, with employee details loaded.</summary>
    Task<IEnumerable<JobInterviewPanelist>> GetByInterviewIdAsync(Guid interviewId);

    /// <summary>Returns all interviews the given employee is panelising, with interview details loaded.</summary>
    Task<IEnumerable<JobInterviewPanelist>> GetByEmployeeIdAsync(Guid employeeId);

    Task<JobInterviewPanelist?> GetByConfirmationTokenAsync(string token);
}

#endregion

// ============================================================================
// JOB INTERVIEW EXTERNAL PANELIST
// ============================================================================

#region Job Interview External Panelist

public interface IJobInterviewExternalPanelistRepository : IGenericRepository<JobInterviewExternalPanelist>
{
    /// <summary>Returns all external panelists for an interview, with associate details loaded.</summary>
    Task<IEnumerable<JobInterviewExternalPanelist>> GetByInterviewIdAsync(Guid interviewId);

    Task<JobInterviewExternalPanelist?> GetByConfirmationTokenAsync(string token);
}

#endregion

// ============================================================================
// JOB INTERVIEWEE
// ============================================================================

#region Job Interviewee

public interface IJobIntervieweeRepository : IGenericRepository<JobInterviewee>
{
    /// <summary>Returns all interviewees for an interview session, with application and candidate details loaded.</summary>
    Task<IEnumerable<JobInterviewee>> GetByInterviewIdAsync(Guid interviewId);

    /// <summary>Returns all interview slots for a specific application.</summary>
    Task<IEnumerable<JobInterviewee>> GetByApplicationIdAsync(Guid applicationId);

    /// <summary>Looks up a single interviewee by their confirmation token. Returns null if not found.</summary>
    Task<JobInterviewee?> GetByConfirmationTokenAsync(string token);
}

#endregion

// ============================================================================
// JOB INTERVIEW QUESTION (per-interview question plan)
// ============================================================================

#region Job Interview Question

public interface IJobInterviewQuestionRepository : IGenericRepository<JobInterviewQuestion>
{
    /// <summary>Returns all question-type plans for an interview, with selected questions loaded.</summary>
    Task<IEnumerable<JobInterviewQuestion>> GetByInterviewIdAsync(Guid interviewId);
}

#endregion

// ============================================================================
// JOB INTERVIEW SELECTED QUESTION
// ============================================================================

#region Job Interview Selected Question

public interface IJobInterviewSelectedQuestionRepository : IGenericRepository<JobInterviewSelectedQuestion>
{
    /// <summary>Returns all selected questions for a question-plan entry, ordered by display order.</summary>
    Task<IEnumerable<JobInterviewSelectedQuestion>> GetByInterviewQuestionIdAsync(Guid interviewQuestionId);
}

#endregion

// ============================================================================
// JOB INTERVIEW SCORE SUMMARY
// ============================================================================

#region Job Interview Score Summary

public interface IJobInterviewScoreSummaryRepository : IGenericRepository<JobInterviewScoreSummary>
{
    /// <summary>Returns all score summaries for an interviewee, with panelist details loaded.</summary>
    Task<IEnumerable<JobInterviewScoreSummary>> GetByIntervieweeIdAsync(Guid intervieweeId);

    /// <summary>Returns all score summaries submitted by an internal panelist.</summary>
    Task<IEnumerable<JobInterviewScoreSummary>> GetByInternalPanelistIdAsync(Guid panelistId);

    /// <summary>Returns all score summaries submitted by an external panelist.</summary>
    Task<IEnumerable<JobInterviewScoreSummary>> GetByExternalPanelistIdAsync(Guid externalPanelistId);

    /// <summary>Returns a fully-loaded score summary including all score entries.</summary>
    Task<JobInterviewScoreSummary?> GetWithScoreEntriesAsync(Guid id);

    /// <summary>Returns all finalized score summaries for an interviewee.</summary>
    Task<IEnumerable<JobInterviewScoreSummary>> GetFinalizedForIntervieweeAsync(Guid intervieweeId);
}

#endregion

// ============================================================================
// JOB INTERVIEW SCORE ENTRY
// ============================================================================

#region Job Interview Score Entry

public interface IJobInterviewScoreEntryRepository : IGenericRepository<JobInterviewScoreEntry>
{
    /// <summary>Returns all score entries for a score summary, with question details loaded.</summary>
    Task<IEnumerable<JobInterviewScoreEntry>> GetBySummaryIdAsync(Guid scoreSummaryId);
}

#endregion

// ============================================================================
// JOB INTERVIEW SCORE DRAFT
// ============================================================================

#region Job Interview Score Draft

public interface IJobInterviewScoreDraftRepository : IGenericRepository<JobInterviewScoreDraft>
{
    /// <summary>
    /// Returns the draft record for the given interviewee/panelist combination, or null if none exists.
    /// </summary>
    Task<JobInterviewScoreDraft?> GetByPanelistAsync(
        Guid intervieweeId,
        Guid? internalPanelistId,
        Guid? externalPanelistId,
        CancellationToken ct = default);
}

#endregion
