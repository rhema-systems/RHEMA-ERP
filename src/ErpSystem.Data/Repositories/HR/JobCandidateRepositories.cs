using ErpSystem.Core.DTOs.HR;
using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.HR;

// ============================================================================
// JOB CANDIDATE REPOSITORY
// ============================================================================

#region Job Candidate Repository

public class JobCandidateRepository : GenericRepository<JobCandidate>, IJobCandidateRepository
{
    private readonly INumberSequenceService _sequences;

    public JobCandidateRepository(ApplicationDbContext context, INumberSequenceService sequences)
        : base(context)
    {
        _sequences = sequences;
    }

    public async Task<JobCandidate?> GetByCandidateNumberAsync(string candidateNumber)
    {
        return await _dbSet
            .FirstOrDefaultAsync(c => c.CandidateNumber == candidateNumber && !c.IsDeleted);
    }

    public async Task<JobCandidate?> GetByEmailAsync(string email, Guid? tenantId = null)
    {
        return await _dbSet
            .Where(c => tenantId == null || c.TenantId == tenantId)
            .FirstOrDefaultAsync(c => c.Email == email && !c.IsDeleted);
    }

    public async Task<JobCandidate?> GetWithFullDetailsAsync(Guid id)
    {
        // Every child include filters IsDeleted, matching the per-collection reads. Unfiltered,
        // this read echoed soft-deleted children — found when the candidate profile's
        // replace-set save deleted an interest and the response served it straight back.
        return await _dbSet
            .Include(c => c.Country)
            .Include(c => c.Qualifications.Where(q => !q.IsDeleted))
            .Include(c => c.WorkHistories.Where(w => !w.IsDeleted))
            .Include(c => c.Referees.Where(r => !r.IsDeleted))
            .Include(c => c.Skills.Where(s => !s.IsDeleted))
            .Include(c => c.Languages.Where(l => !l.IsDeleted))
            .Include(c => c.Interests.Where(i => !i.IsDeleted))
            .Include(c => c.Documents.Where(d => !d.IsDeleted))
            .Include(c => c.Notes.Where(n => !n.IsDeleted))
            .Include(c => c.Applications.Where(a => !a.IsDeleted)).ThenInclude(a => a.JobVacancy).ThenInclude(v => v.Position)
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);
    }

    public async Task<IEnumerable<JobCandidate>> GetTalentPoolCandidatesAsync(Guid tenantId)
    {
        return await _dbSet
            .Include(c => c.Country)
            .Include(c => c.SegmentMemberships).ThenInclude(m => m.Segment)
            .Where(c => c.TenantId == tenantId && c.IsInTalentPool && !c.IsDeleted)
            .OrderBy(c => c.LastName)
            .ThenBy(c => c.FirstName)
            .ToListAsync();
    }

    public async Task<IEnumerable<JobCandidate>> GetByVacancyIdAsync(Guid vacancyId)
    {
        return await _dbSet
            .Include(c => c.Applications)
            .Where(c => !c.IsDeleted
                     && c.Applications.Any(a => a.JobVacancyId == vacancyId && !a.IsDeleted))
            .OrderBy(c => c.LastName)
            .ThenBy(c => c.FirstName)
            .ToListAsync();
    }

    public async Task<string> GetNextCandidateNumberAsync()
    {
        // Not year-scoped — the printed number carries no year.
        var next = await _sequences.NextAsync("CAND");
        return $"CAND-{next:D6}";
    }

    public async Task<string> GetNextCandidateNumberAsync(Guid tenantId)
    {
        var next = await _sequences.NextAsync("CAND", tenantId);
        return $"CAND-{next:D6}";
    }

    // ── Talent pool — filtered queries ────────────────────────────────────────

    public async Task<(List<JobCandidate> Items, int TotalCount)> GetTalentPoolFilteredAsync(
        TalentPoolFilterDto filter,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet
            .Include(c => c.Country)
            .Include(c => c.SegmentMemberships).ThenInclude(m => m.Segment)
            .Include(c => c.EngagementEvents)
            .Where(c => c.TenantId == tenantId && c.IsInTalentPool && !c.IsDeleted);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = filter.Search.ToLower();
            query = query.Where(c =>
                c.FirstName.ToLower().Contains(s) ||
                c.LastName.ToLower().Contains(s)  ||
                c.Email.ToLower().Contains(s)     ||
                (c.Headline != null && c.Headline.ToLower().Contains(s)) ||
                (c.CurrentJobTitle != null && c.CurrentJobTitle.ToLower().Contains(s)) ||
                (c.CurrentEmployer != null && c.CurrentEmployer.ToLower().Contains(s)));
        }

        if (filter.SegmentIds.Count > 0)
            query = query.Where(c => c.SegmentMemberships
                .Any(m => filter.SegmentIds.Contains(m.SegmentId) && !m.IsDeleted));

        if (filter.Status.HasValue)
            query = query.Where(c => c.TalentPoolStatus == filter.Status.Value);

        if (filter.Source.HasValue)
            query = query.Where(c => c.TalentPoolSource == filter.Source.Value);

        if (filter.WorkArrangement.HasValue)
            query = query.Where(c => c.PreferredWorkArrangement == filter.WorkArrangement.Value);

        if (filter.MinExperienceYears.HasValue)
            query = query.Where(c => c.TotalYearsExperience >= filter.MinExperienceYears.Value);

        if (filter.MaxExperienceYears.HasValue)
            query = query.Where(c => c.TotalYearsExperience <= filter.MaxExperienceYears.Value);

        if (filter.AvailableBefore.HasValue)
            query = query.Where(c => c.AvailableFrom <= filter.AvailableBefore.Value);

        if (filter.OverdueForReview == true)
            query = query.Where(c => c.TalentPoolReviewDate != null && c.TalentPoolReviewDate < DateTime.UtcNow);

        if (filter.DormantMoreThanDays.HasValue)
        {
            var threshold = DateTime.UtcNow.AddDays(-filter.DormantMoreThanDays.Value);
            query = query.Where(c => c.LastEngagedDate == null || c.LastEngagedDate < threshold);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        query = (filter.SortBy?.ToLower(), filter.SortDescending) switch
        {
            ("fullname",       false) => query.OrderBy(c => c.LastName).ThenBy(c => c.FirstName),
            ("fullname",       true)  => query.OrderByDescending(c => c.LastName),
            ("dateadded",      false) => query.OrderBy(c => c.TalentPoolAddedDate),
            ("dateadded",      true)  => query.OrderByDescending(c => c.TalentPoolAddedDate),
            ("lastengaged",    false) => query.OrderBy(c => c.LastEngagedDate),
            ("lastengaged",    true)  => query.OrderByDescending(c => c.LastEngagedDate),
            ("reviewdate",     false) => query.OrderBy(c => c.TalentPoolReviewDate),
            ("reviewdate",     true)  => query.OrderByDescending(c => c.TalentPoolReviewDate),
            ("experience",     false) => query.OrderBy(c => c.TotalYearsExperience),
            ("experience",     true)  => query.OrderByDescending(c => c.TotalYearsExperience),
            _                         => query.OrderBy(c => c.LastName).ThenBy(c => c.FirstName),
        };

        var items = await query
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<IEnumerable<JobCandidate>> GetDormantPoolCandidatesAsync(
        DateTime engagedBefore,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(c => c.IsInTalentPool && !c.IsDeleted &&
                        (c.LastEngagedDate == null || c.LastEngagedDate < engagedBefore))
            .OrderBy(c => c.LastEngagedDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<JobCandidate>> GetOverdueReviewPoolCandidatesAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(c => c.IsInTalentPool && !c.IsDeleted &&
                        c.TalentPoolReviewDate != null && c.TalentPoolReviewDate < DateTime.UtcNow)
            .OrderBy(c => c.TalentPoolReviewDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<JobCandidate?> GetWithSegmentsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(c => c.Country)
            .Include(c => c.SegmentMemberships).ThenInclude(m => m.Segment)
            .Include(c => c.EngagementEvents)
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted, cancellationToken);
    }
}

#endregion

// ============================================================================
// CANDIDATE TALENT SEGMENT REPOSITORY
// ============================================================================

#region Candidate Talent Segment Repository

public class CandidateTalentSegmentRepository
    : GenericRepository<CandidateTalentSegment>, ICandidateTalentSegmentRepository
{
    public CandidateTalentSegmentRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<CandidateTalentSegment>> GetActiveByTenantAsync(Guid tenantId)
    {
        return await _dbSet
            .Where(s => s.TenantId == tenantId && s.IsActive && !s.IsDeleted)
            .OrderBy(s => s.Name)
            .ToListAsync();
    }

    public async Task<CandidateTalentSegment?> GetWithMembersAsync(Guid segmentId)
    {
        return await _dbSet
            .Include(s => s.Memberships)
                .ThenInclude(m => m.JobCandidate)
            .FirstOrDefaultAsync(s => s.Id == segmentId && !s.IsDeleted);
    }

    public async Task<bool> NameExistsAsync(Guid tenantId, string name, Guid? excludeId = null)
    {
        return await _dbSet.AnyAsync(s =>
            s.TenantId == tenantId &&
            !s.IsDeleted &&
            s.Name.ToLower() == name.ToLower() &&
            (excludeId == null || s.Id != excludeId.Value));
    }
}

#endregion

// ============================================================================
// CANDIDATE SEGMENT MEMBERSHIP REPOSITORY
// ============================================================================

#region Candidate Segment Membership Repository

public class CandidateSegmentMembershipRepository
    : GenericRepository<CandidateSegmentMembership>, ICandidateSegmentMembershipRepository
{
    public CandidateSegmentMembershipRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<CandidateSegmentMembership>> GetByCandidateIdAsync(Guid candidateId)
    {
        return await _dbSet
            .Include(m => m.Segment)
            .Where(m => m.JobCandidateId == candidateId && !m.IsDeleted)
            .OrderBy(m => m.Segment.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<CandidateSegmentMembership>> GetBySegmentIdAsync(Guid segmentId)
    {
        return await _dbSet
            .Include(m => m.JobCandidate)
            .Where(m => m.SegmentId == segmentId && !m.IsDeleted)
            .ToListAsync();
    }

    public async Task<CandidateSegmentMembership?> GetByCandidateAndSegmentAsync(Guid candidateId, Guid segmentId)
    {
        // Segment is included so the write paths can map SegmentName on the row they just
        // created — without it the add-to-segment response returned a nameless membership.
        return await _dbSet
            .Include(m => m.Segment)
            .FirstOrDefaultAsync(m =>
                m.JobCandidateId == candidateId &&
                m.SegmentId == segmentId &&
                !m.IsDeleted);
    }
}

#endregion

// ============================================================================
// CANDIDATE ENGAGEMENT EVENT REPOSITORY
// ============================================================================

#region Candidate Engagement Event Repository

public class CandidateEngagementEventRepository
    : GenericRepository<CandidateEngagementEvent>, ICandidateEngagementEventRepository
{
    public CandidateEngagementEventRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<CandidateEngagementEvent>> GetByCandidateIdAsync(Guid candidateId)
    {
        return await _dbSet
            .Where(e => e.JobCandidateId == candidateId && !e.IsDeleted)
            .OrderByDescending(e => e.EventDate)
            .ToListAsync();
    }

    public async Task<CandidateEngagementEvent?> GetLatestByCandidateIdAsync(Guid candidateId)
    {
        return await _dbSet
            .Where(e => e.JobCandidateId == candidateId && !e.IsDeleted)
            .OrderByDescending(e => e.EventDate)
            .FirstOrDefaultAsync();
    }
}

#endregion

// ============================================================================
// JOB CANDIDATE QUALIFICATION REPOSITORY
// ============================================================================

#region Job Candidate Qualification Repository

public class JobCandidateQualificationRepository : GenericRepository<JobCandidateQualification>, IJobCandidateQualificationRepository
{
    public JobCandidateQualificationRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<JobCandidateQualification>> GetByCandidateIdAsync(Guid candidateId)
    {
        return await _dbSet
            .Where(q => q.JobCandidateId == candidateId && !q.IsDeleted)
            .OrderByDescending(q => q.DateAwarded)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// JOB CANDIDATE WORK HISTORY REPOSITORY
// ============================================================================

#region Job Candidate Work History Repository

public class JobCandidateWorkHistoryRepository : GenericRepository<JobCandidateWorkHistory>, IJobCandidateWorkHistoryRepository
{
    public JobCandidateWorkHistoryRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<JobCandidateWorkHistory>> GetByCandidateIdAsync(Guid candidateId)
    {
        return await _dbSet
            .Where(w => w.JobCandidateId == candidateId && !w.IsDeleted)
            .OrderByDescending(w => w.StartDate)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// JOB CANDIDATE REFEREE REPOSITORY
// ============================================================================

#region Job Candidate Referee Repository

public class JobCandidateRefereeRepository : GenericRepository<JobCandidateReferee>, IJobCandidateRefereeRepository
{
    public JobCandidateRefereeRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<JobCandidateReferee>> GetByCandidateIdAsync(Guid candidateId)
    {
        return await _dbSet
            .Where(r => r.JobCandidateId == candidateId && !r.IsDeleted)
            .OrderBy(r => r.FullName)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// JOB CANDIDATE SKILL REPOSITORY
// ============================================================================

#region Job Candidate Skill Repository

public class JobCandidateSkillRepository : GenericRepository<JobCandidateSkill>, IJobCandidateSkillRepository
{
    public JobCandidateSkillRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<JobCandidateSkill>> GetByCandidateIdAsync(Guid candidateId)
    {
        return await _dbSet
            .Where(s => s.JobCandidateId == candidateId && !s.IsDeleted)
            .OrderBy(s => s.SkillName)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// JOB CANDIDATE INTEREST REPOSITORY
// ============================================================================

#region Job Candidate Interest Repository

public class JobCandidateInterestRepository : GenericRepository<JobCandidateInterest>, IJobCandidateInterestRepository
{
    public JobCandidateInterestRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<JobCandidateInterest>> GetByCandidateIdAsync(Guid candidateId)
    {
        return await _dbSet
            .Where(i => i.JobCandidateId == candidateId && !i.IsDeleted)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// JOB CANDIDATE DOCUMENT REPOSITORY
// ============================================================================

#region Job Candidate Document Repository

public class JobCandidateDocumentRepository : GenericRepository<JobCandidateDocument>, IJobCandidateDocumentRepository
{
    public JobCandidateDocumentRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<JobCandidateDocument>> GetByCandidateIdAsync(Guid candidateId)
    {
        return await _dbSet
            .Where(d => d.JobCandidateId == candidateId && !d.IsDeleted)
            .OrderByDescending(d => d.UploadDate)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// JOB CANDIDATE NOTE REPOSITORY
// ============================================================================

#region Job Candidate Note Repository

public class JobCandidateNoteRepository : GenericRepository<JobCandidateNote>, IJobCandidateNoteRepository
{
    public JobCandidateNoteRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<JobCandidateNote>> GetByCandidateIdAsync(Guid candidateId)
    {
        return await _dbSet
            .Where(n => n.JobCandidateId == candidateId && !n.IsPrivate && !n.IsDeleted)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<JobCandidateNote>> GetAllByCandidateIdAsync(Guid candidateId)
    {
        return await _dbSet
            .Where(n => n.JobCandidateId == candidateId && !n.IsDeleted)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// JOB CANDIDATE LANGUAGE REPOSITORY
// ============================================================================

#region Job Candidate Language Repository

public class JobCandidateLanguageRepository : GenericRepository<JobCandidateLanguage>, IJobCandidateLanguageRepository
{
    public JobCandidateLanguageRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<JobCandidateLanguage>> GetByCandidateIdAsync(Guid candidateId)
    {
        return await _dbSet
            .Where(l => l.JobCandidateId == candidateId && !l.IsDeleted)
            .OrderBy(l => l.LanguageName)
            .ToListAsync();
    }
}

#endregion
