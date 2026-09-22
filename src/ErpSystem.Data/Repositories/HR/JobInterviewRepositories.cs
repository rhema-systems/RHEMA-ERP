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

    /// <summary>
    /// Every question of a type, active or not — this backs the bank's per-type admin list, and an
    /// <c>IsActive</c> filter here hid deactivated questions from the only screen that could bring them
    /// back. The randomisers apply their own <c>IsActive</c> filter; <see cref="GetActiveQuestionsAsync"/>
    /// remains the active-only read.
    /// </summary>
    public async Task<IEnumerable<JobInterviewQuestionDetail>> GetByQuestionTypeIdAsync(Guid questionTypeId)
    {
        return await _dbSet
            .Include(q => q.QuestionType)
            .Where(q => q.QuestionTypeId == questionTypeId && !q.IsDeleted)
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

    /// <summary>
    /// Everything <c>ToDto</c>, <c>ToSummaryDto</c> and the invitation emails read through a navigation.
    ///
    /// <para>Factored out for the same reason as <c>JobApplicationRepository.WithSummaryNavigations</c>:
    /// these six reads each included a different subset, so the same interview came back with a job
    /// title on one endpoint and an empty one on the next — and <c>GetByIdAsync</c> (which backs
    /// <c>GET /{id}</c> <b>and</b> every create/update response) inherited the bare generic repository
    /// and included nothing at all.</para>
    ///
    /// <para><c>JobVacancy.Requisition.JobDescription</c> is the load-bearing one.
    /// <c>JobVacancy.JobTitle</c> is <c>[NotMapped]</c> and resolves as
    /// <c>CustomAdvertTitle ?? Requisition.JobDescription.JobTitle</c>. Without the chain every
    /// interview read returned an empty <c>jobTitle</c>, and — worse — the candidate invitation, the
    /// reschedule notice and the panelist assignment email all fell back to the literal
    /// <i>"the position"</i>.</para>
    /// </summary>
    private IQueryable<JobInterview> WithSummaryNavigations() =>
        _dbSet
            .Include(i => i.JobVacancy).ThenInclude(v => v.Position)
            .Include(i => i.JobVacancy).ThenInclude(v => v.Requisition).ThenInclude(r => r.JobDescription)
            .Include(i => i.Interviewees).ThenInclude(ie => ie.JobApplication).ThenInclude(a => a.JobCandidate)
            .Include(i => i.Panelists).ThenInclude(p => p.Employee)
            .Include(i => i.ExternalPanelists).ThenInclude(ep => ep.ExternalAssociate)
            // ⚠ Round 4, lane D8. `ToDto` reads `RoomBooking?.Room?.RoomName`, so without this the
            // room name and booking number came back null on EVERY read while `roomBookingId` was
            // set — a field declared and populated by nothing, which is the shape this round keeps
            // recording. Caught by the lane D harness asserting the name rather than just the id.
            .Include(i => i.RoomBooking).ThenInclude(b => b.Room);

    // GET /{id} and every write response map through ToDto, which reads VacancyNumber and JobTitle off
    // the vacancy navigation — the generic base loaded neither.
    public override async Task<JobInterview?> GetByIdAsync(Guid id)
    {
        return await WithSummaryNavigations()
            .FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted);
    }

    public async Task<JobInterview?> GetByInterviewNumberAsync(string interviewNumber)
    {
        return await WithSummaryNavigations()
            .FirstOrDefaultAsync(i => i.InterviewNumber == interviewNumber && !i.IsDeleted);
    }

    public async Task<JobInterview?> GetWithFullDetailsAsync(Guid id)
    {
        return await WithSummaryNavigations()
            .Include(i => i.Panelists).ThenInclude(p => p.Employee).ThenInclude(e => e.Position)
            .Include(i => i.Questions).ThenInclude(q => q.QuestionType)
            .Include(i => i.Questions).ThenInclude(q => q.SelectedQuestions.OrderBy(sq => sq.DisplayOrder))
                .ThenInclude(sq => sq.Question)
            .FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted);
    }

    public async Task<IEnumerable<JobInterview>> GetByVacancyIdAsync(Guid vacancyId)
    {
        return await WithSummaryNavigations()
            .Where(i => i.JobVacancyId == vacancyId && !i.IsDeleted)
            .OrderBy(i => i.Round)
            .ThenBy(i => i.ScheduledDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<JobInterview>> GetByStatusAsync(JobInterviewStatus status)
    {
        return await WithSummaryNavigations()
            .Where(i => i.Status == status && !i.IsDeleted)
            .OrderBy(i => i.ScheduledDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<JobInterview>> GetByDateRangeAsync(DateOnly from, DateOnly to)
    {
        return await WithSummaryNavigations()
            .Where(i => !i.IsDeleted
                     && i.ScheduledDate >= from
                     && i.ScheduledDate <= to)
            .OrderBy(i => i.ScheduledDate)
            .ThenBy(i => i.StartTime)
            .ToListAsync();
    }

    public async Task<IEnumerable<JobInterview>> GetByRoundAsync(Guid vacancyId, int round)
    {
        return await WithSummaryNavigations()
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

    /// <summary>
    /// <c>ToDto</c> reads the interview number, the employee's name and the employee's position title.
    /// Only the employee was ever included, so <c>interviewNumber</c> came back empty and
    /// <c>employeePositionTitle</c> — the column that tells a recruiter who is on the panel and in what
    /// capacity — was null on every row.
    /// </summary>
    private IQueryable<JobInterviewPanelist> WithSummaryNavigations() =>
        _dbSet
            .Include(p => p.Employee).ThenInclude(e => e.Position)
            .Include(p => p.JobInterview).ThenInclude(i => i.JobVacancy)
                .ThenInclude(v => v.Requisition).ThenInclude(r => r.JobDescription);

    public override async Task<JobInterviewPanelist?> GetByIdAsync(Guid id)
        => await WithSummaryNavigations().FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

    public async Task<IEnumerable<JobInterviewPanelist>> GetByInterviewIdAsync(Guid interviewId)
    {
        return await WithSummaryNavigations()
            .Where(p => p.JobInterviewId == interviewId && !p.IsDeleted)
            .OrderBy(p => p.Employee.LastName)
            .ToListAsync();
    }

    public async Task<IEnumerable<JobInterviewPanelist>> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await WithSummaryNavigations()
            .Include(p => p.JobInterview).ThenInclude(i => i.JobVacancy).ThenInclude(v => v.Position)
            .Where(p => p.EmployeeId == employeeId && !p.IsDeleted)
            .OrderByDescending(p => p.JobInterview.ScheduledDate)
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<IEnumerable<JobInterviewPanelist>> GetWorklistByEmployeeIdAsync(Guid employeeId)
    {
        return await WithSummaryNavigations()
            .Include(p => p.JobInterview).ThenInclude(i => i.JobVacancy).ThenInclude(v => v.Position)
            .Include(p => p.JobInterview).ThenInclude(i => i.Interviewees)
                .ThenInclude(ie => ie.JobApplication).ThenInclude(a => a.JobCandidate)
            .Include(p => p.JobInterview).ThenInclude(i => i.Questions)
            .Where(p => p.EmployeeId == employeeId && !p.IsDeleted)
            // Newest first: a panelist's outstanding work is almost always the session they have
            // just sat in, and the tail is history they scroll to rather than look for.
            .OrderByDescending(p => p.JobInterview.ScheduledDate)
            .ThenByDescending(p => p.JobInterview.StartTime)
            .ToListAsync();
    }

    public async Task<JobInterviewPanelist?> GetByConfirmationTokenAsync(string token)
        => await WithSummaryNavigations()
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

    private IQueryable<JobInterviewExternalPanelist> WithSummaryNavigations() =>
        _dbSet
            .Include(p => p.ExternalAssociate)
            .Include(p => p.JobInterview).ThenInclude(i => i.JobVacancy)
                .ThenInclude(v => v.Requisition).ThenInclude(r => r.JobDescription);

    public override async Task<JobInterviewExternalPanelist?> GetByIdAsync(Guid id)
        => await WithSummaryNavigations().FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

    public async Task<IEnumerable<JobInterviewExternalPanelist>> GetByInterviewIdAsync(Guid interviewId)
    {
        return await WithSummaryNavigations()
            .Where(p => p.JobInterviewId == interviewId && !p.IsDeleted)
            .OrderBy(p => p.ExternalAssociate.LastName)
            .ToListAsync();
    }

    public async Task<JobInterviewExternalPanelist?> GetByConfirmationTokenAsync(string token)
        => await WithSummaryNavigations()
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

    /// <summary>
    /// <c>ToDto</c> reads the candidate's name and email off <c>JobApplication.JobCandidate</c> and the
    /// interview number off <c>JobInterview</c>; <c>ToSummaryDto</c> also reads the interview's date and
    /// status. Each read included a different half, and the invitation emails need the vacancy's job
    /// title through the requisition chain.
    /// </summary>
    private IQueryable<JobInterviewee> WithSummaryNavigations() =>
        _dbSet
            .Include(ie => ie.JobApplication).ThenInclude(a => a.JobCandidate)
            .Include(ie => ie.JobInterview).ThenInclude(i => i.JobVacancy)
                .ThenInclude(v => v.Requisition).ThenInclude(r => r.JobDescription);

    public override async Task<JobInterviewee?> GetByIdAsync(Guid id)
        => await WithSummaryNavigations().FirstOrDefaultAsync(ie => ie.Id == id && !ie.IsDeleted);

    public async Task<IEnumerable<JobInterviewee>> GetByInterviewIdAsync(Guid interviewId)
    {
        return await WithSummaryNavigations()
            .Where(ie => ie.JobInterviewId == interviewId && !ie.IsDeleted)
            .ToListAsync();
    }

    public async Task<IEnumerable<JobInterviewee>> GetByApplicationIdAsync(Guid applicationId)
    {
        return await WithSummaryNavigations()
            .Where(ie => ie.JobApplicationId == applicationId && !ie.IsDeleted)
            .OrderByDescending(ie => ie.JobInterview.ScheduledDate)
            .ToListAsync();
    }

    public async Task<JobInterviewee?> GetByConfirmationTokenAsync(string token)
    {
        return await WithSummaryNavigations()
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

    /// <summary>
    /// Plans in the order the preset laid them out, each with its questions in the order they were
    /// committed.
    ///
    /// <para>Both orderings were being discarded. The plans were sorted by question-type name rather
    /// than <c>DisplayOrder</c>, and the selected questions had no ordering at all — an unordered
    /// <c>Include</c> returns whatever order the database happens to give, so the sequence the panel
    /// agreed in the commit step survived only by luck. <c>DisplayOrder</c> is written on both tables
    /// and was read by nothing.</para>
    /// </summary>
    public async Task<IEnumerable<JobInterviewQuestion>> GetByInterviewIdAsync(Guid interviewId)
    {
        return await _dbSet
            .Include(q => q.QuestionType)
            .Include(q => q.SelectedQuestions.OrderBy(sq => sq.DisplayOrder)).ThenInclude(sq => sq.Question)
            .Where(q => q.JobInterviewId == interviewId && !q.IsDeleted)
            .OrderBy(q => q.DisplayOrder)
            .ThenBy(q => q.QuestionType.TypeName)
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

    /// <summary>
    /// <c>ToDto</c> reads the candidate's name and application number off the interviewee, the internal
    /// panelist's name off <c>InternalPanelist.Employee</c>, and the external panelist's name off
    /// <c>ExternalPanelist.ExternalAssociate</c>. No single read loaded all three: the by-interviewee
    /// reads had the panelists but not the candidate, the by-panelist reads had the candidate but not
    /// the panelists, and <c>ExternalAssociate</c> was never included anywhere — so an external
    /// panelist's scorecard always came back with a blank name on every endpoint.
    ///
    /// <para>Round 4 lane F4 adds <c>FiledByHrOnBehalfOf</c>, for the same reason: the provenance
    /// note names the person who filed the card, and without the include it would name nobody on
    /// every read while the id sat right there in the row.</para>
    /// </summary>
    private IQueryable<JobInterviewScoreSummary> WithSummaryNavigations() =>
        _dbSet
            .Include(s => s.JobInterviewee).ThenInclude(ie => ie.JobApplication).ThenInclude(a => a.JobCandidate)
            .Include(s => s.InternalPanelist).ThenInclude(p => p!.Employee)
            .Include(s => s.ExternalPanelist).ThenInclude(p => p!.ExternalAssociate)
            .Include(s => s.FiledByHrOnBehalfOf);

    public override async Task<JobInterviewScoreSummary?> GetByIdAsync(Guid id)
        => await WithSummaryNavigations().FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);

    public async Task<IEnumerable<JobInterviewScoreSummary>> GetByIntervieweeIdAsync(Guid intervieweeId)
    {
        return await WithSummaryNavigations()
            .Where(s => s.JobIntervieweeId == intervieweeId && !s.IsDeleted)
            .OrderBy(s => s.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<JobInterviewScoreSummary>> GetByInternalPanelistIdAsync(Guid panelistId)
    {
        return await WithSummaryNavigations()
            .Where(s => s.InternalPanelistId == panelistId && !s.IsDeleted)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<IEnumerable<JobInterviewScoreSummary>> GetByInternalPanelistIdsAsync(
        IReadOnlyCollection<Guid> panelistIds)
    {
        // An empty IN () is valid SQL here but pointless work; more to the point, EF translates it
        // to a constant-false predicate and still round-trips.
        if (panelistIds is null || panelistIds.Count == 0)
            return Array.Empty<JobInterviewScoreSummary>();

        return await WithSummaryNavigations()
            .Where(s => s.InternalPanelistId != null
                     && panelistIds.Contains(s.InternalPanelistId.Value)
                     && !s.IsDeleted)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<JobInterviewScoreSummary>> GetByExternalPanelistIdAsync(Guid externalPanelistId)
    {
        return await WithSummaryNavigations()
            .Where(s => s.ExternalPanelistId == externalPanelistId && !s.IsDeleted)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();
    }

    public async Task<JobInterviewScoreSummary?> GetWithScoreEntriesAsync(Guid id)
    {
        return await WithSummaryNavigations()
            .Include(s => s.ScoreEntries).ThenInclude(e => e.Question)
            .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);
    }

    public async Task<IEnumerable<JobInterviewScoreSummary>> GetFinalizedForIntervieweeAsync(Guid intervieweeId)
    {
        return await WithSummaryNavigations()
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

    /// <inheritdoc />
    public async Task<IReadOnlyList<Guid>> GetIntervieweeIdsWithDraftAsync(
        IReadOnlyCollection<Guid> internalPanelistIds,
        CancellationToken ct = default)
    {
        if (internalPanelistIds is null || internalPanelistIds.Count == 0)
            return Array.Empty<Guid>();

        // Projected to ids in the database: the worklist asks whether a draft exists, and the draft
        // itself holds the panelist's unfinished marks and comments.
        return await _dbSet
            .Where(d => d.InternalPanelistId != null
                     && internalPanelistIds.Contains(d.InternalPanelistId.Value)
                     && !d.IsDeleted)
            .Select(d => d.JobIntervieweeId)
            .Distinct()
            .ToListAsync(ct);
    }
}

#endregion
