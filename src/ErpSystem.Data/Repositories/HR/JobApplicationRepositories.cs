using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.HR;

// ============================================================================
// JOB APPLICATION REPOSITORY
// ============================================================================

#region Job Application Repository

public class JobApplicationRepository : GenericRepository<JobApplication>, IJobApplicationRepository
{
    private readonly INumberSequenceService _sequences;

    public JobApplicationRepository(ApplicationDbContext context, INumberSequenceService sequences)
        : base(context)
    {
        _sequences = sequences;
    }

    /// <summary>
    /// Overrides the generic GetByIdAsync to eagerly load the navigation properties
    /// required by ToDto(): JobCandidate, JobVacancy, StageHistories → PipelineStage,
    /// JobPosting, ShortlistedBy, and RejectedBy.
    /// </summary>
    public override async Task<JobApplication> GetByIdAsync(Guid id)
    {
        return await _dbSet
            .Include(a => a.JobCandidate)
            .Include(a => a.JobVacancy)
            .Include(a => a.StageHistories).ThenInclude(h => h.PipelineStage)
            .Include(a => a.JobPosting)
            .Include(a => a.ShortlistedBy)
            .Include(a => a.RejectedBy)
            .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted);
    }

    public override IQueryable<JobApplication> GetQueryable()
    {
        return _dbSet
            .Include(a => a.JobCandidate)
            .Include(a => a.JobVacancy)
            .Include(a => a.StageHistories).ThenInclude(h => h.PipelineStage)
            .Where(e => !e.IsDeleted);
    }

    public async Task<JobApplication?> GetByApplicationNumberAsync(string applicationNumber)
    {
        return await _dbSet
            .Include(a => a.JobCandidate)
            .Include(a => a.JobVacancy).ThenInclude(v => v.Position)
            .FirstOrDefaultAsync(a => a.ApplicationNumber == applicationNumber && !a.IsDeleted);
    }

    public async Task<JobApplication?> GetForOfferSeedingAsync(Guid id)
    {
        return await _dbSet
            .Include(a => a.JobVacancy)
            .Include(a => a.JobVacancy.Position).ThenInclude(p => p.SalaryGrade)
            .Include(a => a.JobVacancy.Position).ThenInclude(p => p.ReportsToPosition)
            .Include(a => a.JobVacancy.Position).ThenInclude(p => p.OrganizationUnit)
            .Include(a => a.JobVacancy.Position).ThenInclude(p => p.PositionBenefits).ThenInclude(pb => pb.BenefitPolicy)
            .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted);
    }

    public async Task<JobApplication?> GetWithFullDetailsAsync(Guid id)
    {
        return await _dbSet
            .Include(a => a.JobVacancy).ThenInclude(v => v.Position)
            .Include(a => a.JobVacancy).ThenInclude(v => v.ShortlistingCriteria)
            .Include(a => a.JobCandidate).ThenInclude(c => c.Qualifications).ThenInclude(q => q.Qualification)
            .Include(a => a.JobCandidate).ThenInclude(c => c.Skills)
            .Include(a => a.JobCandidate).ThenInclude(c => c.Languages)
            .Include(a => a.JobPosting)
            .Include(a => a.ShortlistedBy)
            .Include(a => a.RejectedBy)
            .Include(a => a.StageHistories).ThenInclude(h => h.PipelineStage)
            .Include(a => a.StageHistories).ThenInclude(h => h.MovedBy)
            .Include(a => a.TestResults)
            .Include(a => a.InterviewSlots).ThenInclude(i => i.JobInterview)
            .Include(a => a.Communications)
            .Include(a => a.Offer)
            .Include(a => a.HireRecord)
            .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted);
    }

    public async Task<IEnumerable<JobApplication>> GetByVacancyIdAsync(Guid vacancyId)
    {
        return await _dbSet
            .Include(a => a.JobCandidate)
            .Include(a => a.StageHistories).ThenInclude(h => h.PipelineStage)
            .Where(a => a.JobVacancyId == vacancyId
                     && a.Status != ApplicationStatus.Draft
                     && !a.IsDeleted)
            .OrderByDescending(a => a.ApplicationDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<JobApplication>> GetAllWithFullDetailsByVacancyIdAsync(Guid vacancyId)
    {
        // Single query that loads every navigation property required by the scoring engine —
        // same shape as GetWithFullDetailsAsync but scoped to the whole vacancy.
        return await _dbSet
            .Include(a => a.JobVacancy).ThenInclude(v => v.Position)
            .Include(a => a.JobVacancy).ThenInclude(v => v.ShortlistingCriteria)
            .Include(a => a.JobCandidate).ThenInclude(c => c.Qualifications).ThenInclude(q => q.Qualification)
            .Include(a => a.JobCandidate).ThenInclude(c => c.Skills)
            .Include(a => a.JobCandidate).ThenInclude(c => c.Languages)
            .Include(a => a.JobPosting)
            .Include(a => a.StageHistories).ThenInclude(h => h.PipelineStage)
            .Include(a => a.TestResults)
            .Where(a => a.JobVacancyId == vacancyId
                     && a.Status != ApplicationStatus.Draft
                     && !a.IsDeleted)
            .OrderByDescending(a => a.ApplicationDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<JobApplication>> GetByCandidateIdAsync(Guid candidateId)
    {
        return await _dbSet
            .Include(a => a.JobVacancy).ThenInclude(v => v.Position)
            .Where(a => a.JobCandidateId == candidateId && !a.IsDeleted)
            .OrderByDescending(a => a.ApplicationDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<JobApplication>> GetByStatusAsync(ApplicationStatus status, Guid? vacancyId = null)
    {
        var query = _dbSet
            .Include(a => a.JobCandidate)
            .Include(a => a.JobVacancy).ThenInclude(v => v.Position)
            .Include(a => a.StageHistories).ThenInclude(h => h.PipelineStage)
            .Where(a => a.Status == status && !a.IsDeleted);

        if (vacancyId.HasValue)
            query = query.Where(a => a.JobVacancyId == vacancyId.Value);

        return await query
            .OrderByDescending(a => a.ApplicationDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<JobApplication>> GetShortlistedAsync(Guid? vacancyId = null)
    {
        var query = _dbSet
            .Include(a => a.JobCandidate)
            .Include(a => a.JobVacancy)
            .Include(a => a.ShortlistedBy)
            .Include(a => a.StageHistories).ThenInclude(h => h.PipelineStage)
            .Where(a => a.ShortlistedDate.HasValue
                     && a.Status != ApplicationStatus.Rejected
                     && a.Status != ApplicationStatus.Withdrawn
                     && a.Status != ApplicationStatus.Hired
                     && !a.IsDeleted);

        if (vacancyId.HasValue)
            query = query.Where(a => a.JobVacancyId == vacancyId.Value);

        return await query
            .OrderByDescending(a => a.AutoScore ?? 0)
            .ToListAsync();
    }

    public async Task<IEnumerable<JobApplication>> GetByCurrentStageAsync(Guid pipelineStageId)
    {
        return await _dbSet
            .Include(a => a.JobCandidate)
            .Include(a => a.JobVacancy).ThenInclude(v => v.Position)
            .Where(a => !a.IsDeleted
                     && a.StageHistories.Any(h => h.PipelineStageId == pipelineStageId
                                               && h.IsCurrent
                                               && !h.IsDeleted))
            .OrderByDescending(a => a.ApplicationDate)
            .ToListAsync();
    }

    public async Task<string> GetNextApplicationNumberAsync()
    {
        // Not year-scoped: the printed number carries no year, so the counter must keep climbing
        // across year boundaries.
        var next = await _sequences.NextAsync("APP");
        return $"APP-{next:D7}";
    }

    public async Task<string> GetNextApplicationNumberAsync(Guid tenantId)
    {
        var next = await _sequences.NextAsync("APP", tenantId);
        return $"APP-{next:D7}";
    }

    public async Task<JobApplication?> GetByTrackingTokenAsync(string token)
    {
        return await _dbSet
            .Include(a => a.JobCandidate)
            .Include(a => a.JobVacancy)
            .FirstOrDefaultAsync(a => a.ExternalTrackingToken == token && !a.IsDeleted);
    }
}

#endregion

// ============================================================================
// JOB APPLICATION STAGE HISTORY REPOSITORY
// ============================================================================

#region Job Application Stage History Repository

public class JobApplicationStageHistoryRepository : GenericRepository<JobApplicationStageHistory>, IJobApplicationStageHistoryRepository
{
    public JobApplicationStageHistoryRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<JobApplicationStageHistory>> GetByApplicationIdAsync(Guid applicationId)
    {
        return await _dbSet
            .Include(h => h.PipelineStage)
            .Include(h => h.MovedBy)
            .Where(h => h.JobApplicationId == applicationId && !h.IsDeleted)
            .OrderBy(h => h.EnteredAt)
            .ToListAsync();
    }

    public async Task<JobApplicationStageHistory?> GetCurrentStageAsync(Guid applicationId)
    {
        return await _dbSet
            .Include(h => h.PipelineStage)
            .FirstOrDefaultAsync(h => h.JobApplicationId == applicationId
                                   && h.IsCurrent
                                   && !h.IsDeleted);
    }
}

#endregion

// ============================================================================
// JOB APPLICANT TEST RESULT REPOSITORY
// ============================================================================

#region Job Applicant Test Result Repository

public class JobApplicantTestResultRepository : GenericRepository<JobApplicantTestResult>, IJobApplicantTestResultRepository
{
    public JobApplicantTestResultRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<JobApplicantTestResult>> GetByApplicationIdAsync(Guid applicationId)
    {
        return await _dbSet
            .Include(t => t.InvigilatedBy)
            .Include(t => t.MarkedBy)
            .Where(t => t.JobApplicationId == applicationId && !t.IsDeleted)
            .OrderBy(t => t.TestDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<JobApplicantTestResult>> GetByVacancyIdAsync(Guid vacancyId)
    {
        return await _dbSet
            .Include(t => t.JobApplication).ThenInclude(a => a.JobCandidate)
            .Where(t => !t.IsDeleted
                     && t.JobApplication.JobVacancyId == vacancyId
                     && !t.JobApplication.IsDeleted)
            .OrderBy(t => t.TestType)
            .ThenBy(t => t.TestDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<JobApplicantTestResult>> GetByTestTypeAsync(Guid applicationId, JobApplicantTestType testType)
    {
        return await _dbSet
            .Where(t => t.JobApplicationId == applicationId
                     && t.TestType == testType
                     && !t.IsDeleted)
            .OrderBy(t => t.TestDate)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// JOB APPLICANT COMMUNICATION REPOSITORY
// ============================================================================

#region Job Applicant Communication Repository

public class JobApplicantCommunicationRepository : GenericRepository<JobApplicantCommunication>, IJobApplicantCommunicationRepository
{
    public JobApplicantCommunicationRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<JobApplicantCommunication>> GetByApplicationIdAsync(Guid applicationId)
    {
        return await _dbSet
            .Where(c => c.JobApplicationId == applicationId && !c.IsDeleted)
            .OrderByDescending(c => c.SentAt)
            .ToListAsync();
    }
}

#endregion

#region Shortlist Decision Log Repository

public class ShortlistDecisionLogRepository : GenericRepository<ShortlistDecisionLog>, IShortlistDecisionLogRepository
{
    public ShortlistDecisionLogRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<ShortlistDecisionLog>> GetByApplicationIdAsync(Guid applicationId)
    {
        return await _dbSet
            .Where(x => x.JobApplicationId == applicationId && !x.IsDeleted)
            .OrderByDescending(x => x.DecisionAt)
            .ToListAsync();
    }
}

#endregion

#region Shortlist Review Repository

public class ShortlistReviewRepository : GenericRepository<ShortlistReview>, IShortlistReviewRepository
{
    public ShortlistReviewRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<ShortlistReview>> GetByApplicationIdAsync(Guid applicationId)
    {
        return await _dbSet
            .Include(x => x.Reviewer)
            .Where(x => x.JobApplicationId == applicationId && !x.IsDeleted)
            .OrderByDescending(x => x.ReviewedAt)
            .ToListAsync();
    }
}

#endregion
