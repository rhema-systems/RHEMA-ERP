using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.HR;

// ============================================================================
// JOB POSTING
// ============================================================================

#region Job Posting

public interface IJobPostingRepository : IGenericRepository<JobPosting>
{
    /// <summary>Returns all postings for a vacancy, ordered by publish date descending.</summary>
    Task<IEnumerable<JobPosting>> GetByVacancyIdAsync(Guid vacancyId);

    /// <summary>Returns postings filtered by status.</summary>
    Task<IEnumerable<JobPosting>> GetByStatusAsync(JobPostingStatus status);

    /// <summary>Returns all active postings across all vacancies.</summary>
    Task<IEnumerable<JobPosting>> GetActivePostingsAsync();

    /// <summary>Returns postings for a specific channel (e.g. LinkedIn, website).</summary>
    Task<IEnumerable<JobPosting>> GetByChannelAsync(JobPostingChannel channel);

    /// <summary>Returns a posting by its external platform reference ID.</summary>
    Task<JobPosting?> GetByExternalPostingIdAsync(string externalPostingId);

    /// <summary>Returns postings whose expiry date has passed but are still marked active.</summary>
    Task<IEnumerable<JobPosting>> GetExpiredActivePostingsAsync();
}

#endregion

// ============================================================================
// RECRUITMENT PIPELINE
// ============================================================================

#region Recruitment Pipeline

public interface IRecruitmentPipelineRepository : IGenericRepository<RecruitmentPipeline>
{
    /// <summary>Returns the default pipeline, with stages loaded and ordered.</summary>
    Task<RecruitmentPipeline?> GetDefaultPipelineAsync();

    /// <summary>Returns a pipeline by name.</summary>
    Task<RecruitmentPipeline?> GetByNameAsync(string name);

    /// <summary>Returns all pipelines with their stages loaded and ordered by stage order.</summary>
    Task<IEnumerable<RecruitmentPipeline>> GetAllWithStagesAsync();

    /// <summary>Returns a fully-loaded pipeline including all stages.</summary>
    Task<RecruitmentPipeline?> GetWithStagesAsync(Guid id);
}

#endregion

// ============================================================================
// RECRUITMENT PIPELINE STAGE
// ============================================================================

#region Recruitment Pipeline Stage

public interface IRecruitmentPipelineStageRepository : IGenericRepository<RecruitmentPipelineStage>
{
    /// <summary>Returns all stages for a pipeline, ordered by display order.</summary>
    Task<IEnumerable<RecruitmentPipelineStage>> GetByPipelineIdAsync(Guid pipelineId);

    /// <summary>Returns stages of a specific type across all pipelines.</summary>
    Task<IEnumerable<RecruitmentPipelineStage>> GetByStageTypeAsync(RecruitmentPipelineStageType stageType);

    /// <summary>Returns the final stage for a given pipeline, or null if none is marked as final.</summary>
    Task<RecruitmentPipelineStage?> GetFinalStageAsync(Guid pipelineId);

    /// <summary>Returns the maximum stage order value for a given pipeline, used when appending a new stage.</summary>
    Task<int> GetMaxStageOrderAsync(Guid pipelineId);
}

#endregion

// ============================================================================
// JOB SHORTLISTING CRITERIA
// ============================================================================

#region Job Shortlisting Criteria

public interface IJobShortlistingCriteriaRepository : IGenericRepository<JobShortlistingCriteria>
{
    /// <summary>Returns all shortlisting criteria for a vacancy, ordered by weight descending.</summary>
    Task<IEnumerable<JobShortlistingCriteria>> GetByVacancyIdAsync(Guid vacancyId);

    /// <summary>Returns mandatory criteria for a vacancy.</summary>
    Task<IEnumerable<JobShortlistingCriteria>> GetMandatoryCriteriaAsync(Guid vacancyId);
}

#endregion
