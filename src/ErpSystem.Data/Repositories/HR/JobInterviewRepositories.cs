using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.HR;

// ============================================================================
// JOB INTERVIEW QUESTION TYPE REPOSITORY
// ============================================================================

#region Job Interview Question Type Repository

public class JobInterviewQuestionTypeRepository : GenericRepository<JobInterviewQuestionType>, IJobInterviewQuestionTypeRepository
{
    public JobInterviewQuestionTypeRepository(ApplicationDbContext context) : base(context) { }

    public async Task<JobInterviewQuestionType?> GetByCodeAsync(string code)
    {
        return await _dbSet
            .FirstOrDefaultAsync(t => t.Code == code && !t.IsDeleted);
    }

    public async Task<IEnumerable<JobInterviewQuestionType>> GetActiveTypesAsync()
    {
        return await _dbSet
            .Where(t => t.IsActive && !t.IsDeleted)
            .OrderBy(t => t.TypeName)
            .ToListAsync();
    }

    public async Task<JobInterviewQuestionType?> GetWithQuestionsAsync(Guid id)
    {
        return await _dbSet
            .Include(t => t.QuestionDetails.Where(q => q.IsActive && !q.IsDeleted))
            .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);
    }
}

#endregion

// ============================================================================
// JOB INTERVIEW QUESTION DETAIL REPOSITORY
// ============================================================================

#region Job Interview Question Detail Repository

public class JobInterviewQuestionDetailRepository : GenericRepository<JobInterviewQuestionDetail>, IJobInterviewQuestionDetailRepository
{
    public JobInterviewQuestionDetailRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<JobInterviewQuestionDetail>> GetAllWithTypeAsync()
    {
        return await _dbSet
            .Include(q => q.QuestionType)
            .Where(q => !q.IsDeleted)
            .OrderBy(q => q.QuestionType.TypeName)
            .ThenBy(q => q.Weight)
            .ToListAsync();
    }

    public async Task<IEnumerable<JobInterviewQuestionDetail>> GetByQuestionTypeIdAsync(Guid questionTypeId)
    {
        return await _dbSet
            .Include(q => q.QuestionType)
            .Where(q => q.QuestionTypeId == questionTypeId && q.IsActive && !q.IsDeleted)
            .OrderBy(q => q.Weight)
            .ToListAsync();
    }

    public async Task<IEnumerable<JobInterviewQuestionDetail>> GetActiveQuestionsAsync(Guid? questionTypeId = null)
    {
        var query = _dbSet
            .Include(q => q.QuestionType)
            .Where(q => q.IsActive && !q.IsDeleted);

        if (questionTypeId.HasValue)
            query = query.Where(q => q.QuestionTypeId == questionTypeId.Value);

        return await query
            .OrderBy(q => q.QuestionType.TypeName)
            .ThenBy(q => q.Weight)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// JOB INTERVIEW REPOSITORY
// ============================================================================

#region Job Interview Repository

public class JobInterviewRepository : GenericRepository<JobInterview>, IJobInterviewRepository
{
    public JobInterviewRepository(ApplicationDbContext context) : base(context) { }

    public async Task<JobInterview?> GetByInterviewNumberAsync(string interviewNumber)
    {
        return await _dbSet
            .Include(i => i.JobVacancy).ThenInclude(v => v.Position)
            .FirstOrDefaultAsync(i => i.InterviewNumber == interviewNumber && !i.IsDeleted);
    }

    public async Task<JobInterview?> GetWithFullDetailsAsync(Guid id)
    {
        return await _dbSet
            .Include(i => i.JobVacancy).ThenInclude(v => v.Position)
            .Include(i => i.Interviewees).ThenInclude(ie => ie.JobApplication).ThenInclude(a => a.JobCandidate)
            .Include(i => i.Panelists).ThenInclude(p => p.Employee)
            .Include(i => i.ExternalPanelists).ThenInclude(ep => ep.ExternalAssociate)
            .Include(i => i.Questions).ThenInclude(q => q.QuestionType)
            .Include(i => i.Questions).ThenInclude(q => q.SelectedQuestions).ThenInclude(sq => sq.Question)
            .FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted);
    }

    public async Task<IEnumerable<JobInterview>> GetByVacancyIdAsync(Guid vacancyId)
    {
        return await _dbSet
            .Include(i => i.Interviewees).ThenInclude(ie => ie.JobApplication).ThenInclude(a => a.JobCandidate)
            .Include(i => i.Panelists).ThenInclude(p => p.Employee)
            .Where(i => i.JobVacancyId == vacancyId && !i.IsDeleted)
            .OrderBy(i => i.Round)
            .ThenBy(i => i.ScheduledDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<JobInterview>> GetByStatusAsync(JobInterviewStatus status)
    {
        return await _dbSet
            .Include(i => i.JobVacancy).ThenInclude(v => v.Position)
            .Where(i => i.Status == status && !i.IsDeleted)
            .OrderBy(i => i.ScheduledDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<JobInterview>> GetByDateRangeAsync(DateOnly from, DateOnly to)
    {
        return await _dbSet
            .Include(i => i.JobVacancy).ThenInclude(v => v.Position)
            .Include(i => i.Interviewees).ThenInclude(ie => ie.JobApplication).ThenInclude(a => a.JobCandidate)
            .Include(i => i.Panelists).ThenInclude(p => p.Employee)
            .Include(i => i.ExternalPanelists).ThenInclude(ep => ep.ExternalAssociate)
            .Where(i => !i.IsDeleted
                     && i.ScheduledDate >= from
                     && i.ScheduledDate <= to)
            .OrderBy(i => i.ScheduledDate)
            .ThenBy(i => i.StartTime)
            .ToListAsync();
    }

    public async Task<IEnumerable<JobInterview>> GetByRoundAsync(Guid vacancyId, int round)
    {
        return await _dbSet
            .Include(i => i.Interviewees).ThenInclude(ie => ie.JobApplication).ThenInclude(a => a.JobCandidate)
            .Where(i => i.JobVacancyId == vacancyId && i.Round == round && !i.IsDeleted)
            .OrderBy(i => i.ScheduledDate)
            .ToListAsync();
    }

    public async Task<string> GetNextInterviewNumberAsync()
    {
        var last = await _dbSet
            .Where(i => !i.IsDeleted)
            .OrderByDescending(i => i.InterviewNumber)
            .Select(i => i.InterviewNumber)
            .FirstOrDefaultAsync();

        var next = 1;
        if (last != null && int.TryParse(last.Replace("INT-", ""), out var parsed))
            next = parsed + 1;

        return $"INT-{next:D6}";
    }
}

#endregion

// ============================================================================
// JOB INTERVIEW PANELIST REPOSITORY
// ============================================================================

#region Job Interview Panelist Repository

public class JobInterviewPanelistRepository : GenericRepository<JobInterviewPanelist>, IJobInterviewPanelistRepository
{
    public JobInterviewPanelistRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<JobInterviewPanelist>> GetByInterviewIdAsync(Guid interviewId)
    {
        return await _dbSet
            .Include(p => p.Employee)
            .Where(p => p.JobInterviewId == interviewId && !p.IsDeleted)
            .OrderBy(p => p.Employee.LastName)
            .ToListAsync();
    }

    public async Task<IEnumerable<JobInterviewPanelist>> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(p => p.JobInterview).ThenInclude(i => i.JobVacancy).ThenInclude(v => v.Position)
            .Where(p => p.EmployeeId == employeeId && !p.IsDeleted)
            .OrderByDescending(p => p.JobInterview.ScheduledDate)
            .ToListAsync();
    }

    public async Task<JobInterviewPanelist?> GetByConfirmationTokenAsync(string token)
        => await _dbSet
            .Include(p => p.Employee)
            .Include(p => p.JobInterview).ThenInclude(i => i.JobVacancy)
            .FirstOrDefaultAsync(p => p.ConfirmationToken == token && !p.IsDeleted);
}

#endregion

// ============================================================================
// JOB INTERVIEW EXTERNAL PANELIST REPOSITORY
// ============================================================================

#region Job Interview External Panelist Repository

public class JobInterviewExternalPanelistRepository : GenericRepository<JobInterviewExternalPanelist>, IJobInterviewExternalPanelistRepository
{
    public JobInterviewExternalPanelistRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<JobInterviewExternalPanelist>> GetByInterviewIdAsync(Guid interviewId)
    {
        return await _dbSet
            .Include(p => p.ExternalAssociate)
            .Where(p => p.JobInterviewId == interviewId && !p.IsDeleted)
            .OrderBy(p => p.ExternalAssociate.LastName)
            .ToListAsync();
    }

    public async Task<JobInterviewExternalPanelist?> GetByConfirmationTokenAsync(string token)
        => await _dbSet
            .Include(p => p.ExternalAssociate)
            .Include(p => p.JobInterview).ThenInclude(i => i.JobVacancy)
            .FirstOrDefaultAsync(p => p.ConfirmationToken == token && !p.IsDeleted);
}

#endregion

// ============================================================================
// JOB INTERVIEWEE REPOSITORY
// ============================================================================

#region Job Interviewee Repository

public class JobIntervieweeRepository : GenericRepository<JobInterviewee>, IJobIntervieweeRepository
{
    public JobIntervieweeRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<JobInterviewee>> GetByInterviewIdAsync(Guid interviewId)
    {
        return await _dbSet
            .Include(ie => ie.JobApplication).ThenInclude(a => a.JobCandidate)
            .Where(ie => ie.JobInterviewId == interviewId && !ie.IsDeleted)
            .ToListAsync();
    }

    public async Task<IEnumerable<JobInterviewee>> GetByApplicationIdAsync(Guid applicationId)
    {
        return await _dbSet
            .Include(ie => ie.JobInterview)
            .Where(ie => ie.JobApplicationId == applicationId && !ie.IsDeleted)
            .OrderByDescending(ie => ie.JobInterview.ScheduledDate)
            .ToListAsync();
    }

    public async Task<JobInterviewee?> GetByConfirmationTokenAsync(string token)
    {
        return await _dbSet
            .Include(ie => ie.JobApplication).ThenInclude(a => a.JobCandidate)
            .Include(ie => ie.JobInterview).ThenInclude(i => i.JobVacancy)
            .FirstOrDefaultAsync(ie => ie.ConfirmationToken == token && !ie.IsDeleted);
    }
}

#endregion

// ============================================================================
// JOB INTERVIEW QUESTION REPOSITORY (per-interview question plan)
// ============================================================================

#region Job Interview Question Repository

public class JobInterviewQuestionRepository : GenericRepository<JobInterviewQuestion>, IJobInterviewQuestionRepository
{
    public JobInterviewQuestionRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<JobInterviewQuestion>> GetByInterviewIdAsync(Guid interviewId)
    {
        return await _dbSet
            .Include(q => q.QuestionType)
            .Include(q => q.SelectedQuestions).ThenInclude(sq => sq.Question)
            .Where(q => q.JobInterviewId == interviewId && !q.IsDeleted)
            .OrderBy(q => q.QuestionType.TypeName)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// JOB INTERVIEW SELECTED QUESTION REPOSITORY
// ============================================================================

#region Job Interview Selected Question Repository

public class JobInterviewSelectedQuestionRepository : GenericRepository<JobInterviewSelectedQuestion>, IJobInterviewSelectedQuestionRepository
{
    public JobInterviewSelectedQuestionRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<JobInterviewSelectedQuestion>> GetByInterviewQuestionIdAsync(Guid interviewQuestionId)
    {
        return await _dbSet
            .Include(sq => sq.Question)
            .Where(sq => sq.JobInterviewQuestionId == interviewQuestionId && !sq.IsDeleted)
            .OrderBy(sq => sq.DisplayOrder)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// JOB INTERVIEW SCORE SUMMARY REPOSITORY
// ============================================================================

#region Job Interview Score Summary Repository

public class JobInterviewScoreSummaryRepository : GenericRepository<JobInterviewScoreSummary>, IJobInterviewScoreSummaryRepository
{
    public JobInterviewScoreSummaryRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<JobInterviewScoreSummary>> GetByIntervieweeIdAsync(Guid intervieweeId)
    {
        return await _dbSet
            .Include(s => s.InternalPanelist).ThenInclude(p => p!.Employee)
            .Include(s => s.ExternalPanelist)
            .Where(s => s.JobIntervieweeId == intervieweeId && !s.IsDeleted)
            .OrderBy(s => s.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<JobInterviewScoreSummary>> GetByInternalPanelistIdAsync(Guid panelistId)
    {
        return await _dbSet
            .Include(s => s.JobInterviewee).ThenInclude(ie => ie.JobApplication).ThenInclude(a => a.JobCandidate)
            .Where(s => s.InternalPanelistId == panelistId && !s.IsDeleted)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<JobInterviewScoreSummary>> GetByExternalPanelistIdAsync(Guid externalPanelistId)
    {
        return await _dbSet
            .Include(s => s.JobInterviewee).ThenInclude(ie => ie.JobApplication).ThenInclude(a => a.JobCandidate)
            .Where(s => s.ExternalPanelistId == externalPanelistId && !s.IsDeleted)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();
    }

    public async Task<JobInterviewScoreSummary?> GetWithScoreEntriesAsync(Guid id)
    {
        return await _dbSet
            .Include(s => s.ScoreEntries).ThenInclude(e => e.Question)
            .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);
    }

    public async Task<IEnumerable<JobInterviewScoreSummary>> GetFinalizedForIntervieweeAsync(Guid intervieweeId)
    {
        return await _dbSet
            .Include(s => s.InternalPanelist).ThenInclude(p => p!.Employee)
            .Include(s => s.ExternalPanelist)
            .Where(s => s.JobIntervieweeId == intervieweeId && s.IsFinalized && !s.IsDeleted)
            .OrderBy(s => s.CreatedAt)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// JOB INTERVIEW SCORE ENTRY REPOSITORY
// ============================================================================

#region Job Interview Score Entry Repository

public class JobInterviewScoreEntryRepository : GenericRepository<JobInterviewScoreEntry>, IJobInterviewScoreEntryRepository
{
    public JobInterviewScoreEntryRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<JobInterviewScoreEntry>> GetBySummaryIdAsync(Guid scoreSummaryId)
    {
        return await _dbSet
            .Include(e => e.Question)
            .Where(e => e.ScoreSummaryId == scoreSummaryId && !e.IsDeleted)
            .OrderBy(e => e.QuestionDetailId)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// INTERVIEW QUESTION PRESET REPOSITORY
// ============================================================================

#region Interview Question Preset Repository

public class InterviewQuestionPresetRepository : GenericRepository<InterviewQuestionPreset>, IInterviewQuestionPresetRepository
{
    public InterviewQuestionPresetRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<InterviewQuestionPreset>> GetAllWithItemsAsync()
    {
        return await _dbSet
            .Include(p => p.Items.Where(i => !i.IsDeleted))
                .ThenInclude(i => i.QuestionType)
            .Where(p => !p.IsDeleted)
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<InterviewQuestionPreset?> GetWithItemsAsync(Guid id)
    {
        return await _dbSet
            .Include(p => p.Items.Where(i => !i.IsDeleted))
                .ThenInclude(i => i.QuestionType)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
    }
}

public class InterviewQuestionPresetItemRepository : GenericRepository<InterviewQuestionPresetItem>, IInterviewQuestionPresetItemRepository
{
    public InterviewQuestionPresetItemRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<InterviewQuestionPresetItem>> GetByPresetIdAsync(Guid presetId)
    {
        return await _dbSet
            .Include(i => i.QuestionType)
            .Where(i => i.PresetId == presetId && !i.IsDeleted)
            .OrderBy(i => i.DisplayOrder)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// JOB INTERVIEW SCORE DRAFT REPOSITORY
// ============================================================================

#region Job Interview Score Draft Repository

public class JobInterviewScoreDraftRepository
    : GenericRepository<JobInterviewScoreDraft>, IJobInterviewScoreDraftRepository
{
    public JobInterviewScoreDraftRepository(ApplicationDbContext context) : base(context) { }

    public async Task<JobInterviewScoreDraft?> GetByPanelistAsync(
        Guid intervieweeId,
        Guid? internalPanelistId,
        Guid? externalPanelistId,
        CancellationToken ct = default)
    {
        return await _dbSet.FirstOrDefaultAsync(d =>
            d.JobIntervieweeId   == intervieweeId &&
            d.InternalPanelistId == internalPanelistId &&
            d.ExternalPanelistId == externalPanelistId &&
            !d.IsDeleted, ct);
    }
}

#endregion
