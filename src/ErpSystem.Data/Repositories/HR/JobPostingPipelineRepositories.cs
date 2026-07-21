using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.HR;

// ============================================================================
// JOB POSTING REPOSITORY
// ============================================================================

#region Job Posting Repository

public class JobPostingRepository : GenericRepository<JobPosting>, IJobPostingRepository
{
    public JobPostingRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<JobPosting>> GetByVacancyIdAsync(Guid vacancyId)
    {
        return await _dbSet
            .Include(p => p.PostedBy)
            .Where(p => p.JobVacancyId == vacancyId && !p.IsDeleted)
            .OrderByDescending(p => p.PublishDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<JobPosting>> GetByStatusAsync(JobPostingStatus status)
    {
        return await _dbSet
            .Include(p => p.JobVacancy).ThenInclude(v => v.Position)
            .Include(p => p.PostedBy)
            .Where(p => p.Status == status && !p.IsDeleted)
            .OrderByDescending(p => p.PublishDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<JobPosting>> GetActivePostingsAsync()
    {
        return await _dbSet
            .Include(p => p.JobVacancy).ThenInclude(v => v.Position)
            .Include(p => p.PostedBy)
            .Where(p => p.IsActive && p.Status == JobPostingStatus.Published && !p.IsDeleted)
            .OrderByDescending(p => p.PublishDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<JobPosting>> GetByChannelAsync(JobPostingChannel channel)
    {
        return await _dbSet
            .Include(p => p.JobVacancy).ThenInclude(v => v.Position)
            .Where(p => p.Channel == channel && !p.IsDeleted)
            .OrderByDescending(p => p.PublishDate)
            .ToListAsync();
    }

    public async Task<JobPosting?> GetByExternalPostingIdAsync(string externalPostingId)
    {
        return await _dbSet
            .Include(p => p.JobVacancy)
            .FirstOrDefaultAsync(p => p.ExternalPostingId == externalPostingId && !p.IsDeleted);
    }

    public async Task<IEnumerable<JobPosting>> GetExpiredActivePostingsAsync()
    {
        var now = DateTime.UtcNow;
        return await _dbSet
            .Include(p => p.JobVacancy).ThenInclude(v => v.Position)
            .Where(p => p.IsActive
                     && p.ExpiryDate != null
                     && p.ExpiryDate < now
                     && !p.IsDeleted)
            .OrderBy(p => p.ExpiryDate)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// RECRUITMENT PIPELINE REPOSITORY
// ============================================================================

#region Recruitment Pipeline Repository

public class RecruitmentPipelineRepository : GenericRepository<RecruitmentPipeline>, IRecruitmentPipelineRepository
{
    public RecruitmentPipelineRepository(ApplicationDbContext context) : base(context) { }

    public async Task<RecruitmentPipeline?> GetDefaultPipelineAsync()
    {
        return await _dbSet
            .Include(p => p.Stages.OrderBy(s => s.Order))
            .FirstOrDefaultAsync(p => p.IsDefault && !p.IsDeleted);
    }

    public async Task<RecruitmentPipeline?> GetByNameAsync(string name)
    {
        return await _dbSet
            .Include(p => p.Stages.OrderBy(s => s.Order))
            .FirstOrDefaultAsync(p => p.Name == name && !p.IsDeleted);
    }

    public async Task<IEnumerable<RecruitmentPipeline>> GetAllWithStagesAsync()
    {
        return await _dbSet
            .Include(p => p.Stages.OrderBy(s => s.Order))
            .Where(p => !p.IsDeleted)
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<RecruitmentPipeline?> GetWithStagesAsync(Guid id)
    {
        return await _dbSet
            .Include(p => p.Stages.OrderBy(s => s.Order))
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
    }
}

#endregion

// ============================================================================
// RECRUITMENT PIPELINE STAGE REPOSITORY
// ============================================================================

#region Recruitment Pipeline Stage Repository

public class RecruitmentPipelineStageRepository : GenericRepository<RecruitmentPipelineStage>, IRecruitmentPipelineStageRepository
{
    public RecruitmentPipelineStageRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<RecruitmentPipelineStage>> GetByPipelineIdAsync(Guid pipelineId)
    {
        return await _dbSet
            .Where(s => s.RecruitmentPipelineId == pipelineId && !s.IsDeleted)
            .OrderBy(s => s.Order)
            .ToListAsync();
    }

    public async Task<IEnumerable<RecruitmentPipelineStage>> GetByStageTypeAsync(RecruitmentPipelineStageType stageType)
    {
        return await _dbSet
            .Include(s => s.Pipeline)
            .Where(s => s.StageType == stageType && !s.IsDeleted)
            .OrderBy(s => s.Pipeline.Name)
            .ThenBy(s => s.Order)
            .ToListAsync();
    }

    public async Task<RecruitmentPipelineStage?> GetFinalStageAsync(Guid pipelineId)
    {
        return await _dbSet
            .FirstOrDefaultAsync(s => s.RecruitmentPipelineId == pipelineId
                                   && s.IsFinalStage
                                   && !s.IsDeleted);
    }

    public async Task<int> GetMaxStageOrderAsync(Guid pipelineId)
    {
        return await _dbSet
            .Where(s => s.RecruitmentPipelineId == pipelineId && !s.IsDeleted)
            .MaxAsync(s => (int?)s.Order) ?? 0;
    }
}

#endregion

// ============================================================================
// JOB SHORTLISTING CRITERIA REPOSITORY
// ============================================================================

#region Job Shortlisting Criteria Repository

public class JobShortlistingCriteriaRepository : GenericRepository<JobShortlistingCriteria>, IJobShortlistingCriteriaRepository
{
    public JobShortlistingCriteriaRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<JobShortlistingCriteria>> GetByVacancyIdAsync(Guid vacancyId)
    {
        return await _dbSet
            .Where(c => c.JobVacancyId == vacancyId && !c.IsDeleted)
            .OrderByDescending(c => c.Weight)
            .ToListAsync();
    }

    public async Task<IEnumerable<JobShortlistingCriteria>> GetMandatoryCriteriaAsync(Guid vacancyId)
    {
        return await _dbSet
            .Where(c => c.JobVacancyId == vacancyId && c.IsMandatory && !c.IsDeleted)
            .OrderByDescending(c => c.Weight)
            .ToListAsync();
    }
}

#endregion
