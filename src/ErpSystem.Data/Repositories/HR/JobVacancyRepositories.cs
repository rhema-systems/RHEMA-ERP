using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.HR;

// ============================================================================
// JOB VACANCY REPOSITORY
// ============================================================================

#region Job Vacancy Repository

public class JobVacancyRepository : GenericRepository<JobVacancy>, IJobVacancyRepository
{
    private readonly INumberSequenceService _sequences;

    public JobVacancyRepository(ApplicationDbContext context, INumberSequenceService sequences)
        : base(context)
    {
        _sequences = sequences;
    }

    public override async Task<IEnumerable<JobVacancy>> GetAllAsync()
    {
        return await _dbSet
            .Include(v => v.HiringManager)
            .Include(v => v.Recruiter)
            .Include(v => v.Requisition).ThenInclude(r => r.OrganizationUnit)
            .Where(v => !v.IsDeleted)
            .OrderByDescending(v => v.CreatedAt)
            .ToListAsync();
    }

    public override async Task<JobVacancy?> GetByIdAsync(Guid id)
    {
        return await _dbSet
            .Include(v => v.HiringManager)
            .Include(v => v.Recruiter)
            .Include(v => v.Requisition).ThenInclude(r => r.OrganizationUnit)
            .Include(v => v.Position)
            .Include(v => v.Pipeline)
            .FirstOrDefaultAsync(v => v.Id == id && !v.IsDeleted);
    }

    public async Task<JobVacancy?> GetByVacancyNumberAsync(string vacancyNumber)
    {
        return await _dbSet
            .Include(v => v.Position)
            .Include(v => v.Requisition)
            .FirstOrDefaultAsync(v => v.VacancyNumber == vacancyNumber && !v.IsDeleted);
    }

    public async Task<JobVacancy?> GetWithFullDetailsAsync(Guid id)
    {
        return await _dbSet
            .Include(v => v.Requisition).ThenInclude(r => r.JobDescription)
            .Include(v => v.Position)
            .Include(v => v.HiringManager)
            .Include(v => v.Recruiter)
            .Include(v => v.Pipeline).ThenInclude(p => p!.Stages)
            .Include(v => v.Attachments).ThenInclude(a => a.UploadedBy)
            .Include(v => v.JobPostings)
            .Include(v => v.ShortlistingCriteria)
            .Include(v => v.StatusHistory).ThenInclude(h => h.ChangedBy)
            .FirstOrDefaultAsync(v => v.Id == id && !v.IsDeleted);
    }

    public async Task<IEnumerable<JobVacancy>> GetByStatusAsync(JobVacancyStatus status)
    {
        return await _dbSet
            .Include(v => v.Position)
            .Include(v => v.HiringManager)
            .Include(v => v.Recruiter)
            .Where(v => v.VacancyStatus == status && !v.IsDeleted)
            .OrderByDescending(v => v.PublishDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<JobVacancy>> GetActiveVacanciesAsync()
    {
        return await _dbSet
            .Include(v => v.Position)
            .Include(v => v.HiringManager)
            .Include(v => v.Recruiter)
            .Where(v => !v.IsDeleted
                     && (v.VacancyStatus == JobVacancyStatus.Published
                         || v.VacancyStatus == JobVacancyStatus.Approved))
            .OrderByDescending(v => v.PublishDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<JobVacancy>> GetByRequisitionAsync(Guid requisitionId)
    {
        return await _dbSet
            .Include(v => v.Position)
            .Include(v => v.Requisition)
            .Where(v => v.StaffRequisitionId == requisitionId && !v.IsDeleted)
            .OrderByDescending(v => v.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<JobVacancy>> GetByPositionAsync(Guid positionId)
    {
        return await _dbSet
            .Include(v => v.Position)
            .Include(v => v.HiringManager)
            .Where(v => v.PositionId == positionId && !v.IsDeleted)
            .OrderByDescending(v => v.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<JobVacancy>> GetByHiringManagerAsync(Guid hiringManagerId)
    {
        return await _dbSet
            .Include(v => v.Position)
            .Include(v => v.Recruiter)
            .Where(v => v.HiringManagerId == hiringManagerId && !v.IsDeleted)
            .OrderByDescending(v => v.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<JobVacancy>> GetByRecruiterAsync(Guid recruiterId)
    {
        return await _dbSet
            .Include(v => v.Position)
            .Include(v => v.HiringManager)
            .Where(v => v.RecruiterId == recruiterId && !v.IsDeleted)
            .OrderByDescending(v => v.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<JobVacancy>> GetWithDeadlineApproachingAsync(int daysAhead = 7)
    {
        var cutoff = DateTime.UtcNow.AddDays(daysAhead);
        return await _dbSet
            .Include(v => v.Position)
            .Include(v => v.Recruiter)
            .Where(v => !v.IsDeleted
                     && v.ApplicationDeadline != null
                     && v.ApplicationDeadline <= cutoff
                     && v.VacancyStatus == JobVacancyStatus.Published)
            .OrderBy(v => v.ApplicationDeadline)
            .ToListAsync();
    }

    public async Task ReconcileCountersAsync(Guid vacancyId, CancellationToken cancellationToken = default)
    {
        var vacancy = await _dbSet.FirstOrDefaultAsync(v => v.Id == vacancyId && !v.IsDeleted, cancellationToken);
        if (vacancy is null) return;

        var appQuery = _context.Set<JobApplication>().Where(a => a.JobVacancyId == vacancyId && !a.IsDeleted);

        vacancy.ApplicationCount = await appQuery
            .CountAsync(a => a.Status != ApplicationStatus.Withdrawn, cancellationToken);
        vacancy.ShortlistedCount = await appQuery
            .CountAsync(a => a.Status == ApplicationStatus.Shortlisted, cancellationToken);
        vacancy.InterviewCount = await appQuery
            .CountAsync(a => a.Status == ApplicationStatus.InterviewScheduled
                          || a.Status == ApplicationStatus.InterviewCompleted, cancellationToken);
        vacancy.OfferCount = await appQuery
            .CountAsync(a => a.Status == ApplicationStatus.OfferExtended
                          || a.Status == ApplicationStatus.OfferAccepted, cancellationToken);
        vacancy.HireCount = await appQuery
            .CountAsync(a => a.Status == ApplicationStatus.Hired, cancellationToken);

        _context.Entry(vacancy).State = EntityState.Modified;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<string> GetNextVacancyNumberAsync()
    {
        // Not year-scoped — the printed number carries no year.
        var next = await _sequences.NextAsync("VAC");
        return $"VAC-{next:D6}";
    }

    public async Task<IEnumerable<JobVacancy>> GetPublishedForPublicPortalAsync(
        DateTime asOfUtc,
        EmploymentType? employmentType = null,
        WorkMode? workMode = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet
            .AsNoTracking()
            .Include(v => v.Position)
            .Include(v => v.Requisition).ThenInclude(r => r.JobDescription)
            .Include(v => v.Requisition).ThenInclude(r => r.OrganizationUnit)
            .Include(v => v.Requisition).ThenInclude(r => r.Location)
            .Where(v => v.VacancyStatus == JobVacancyStatus.Published && !v.IsDeleted)
            .Where(v => v.ApplicationDeadline == null || v.ApplicationDeadline >= asOfUtc);

        if (employmentType.HasValue)
            query = query.Where(v => v.EmploymentType == employmentType.Value);

        if (workMode.HasValue)
            query = query.Where(v => v.WorkMode == workMode.Value);

        return await query
            .OrderByDescending(v => v.PublishDate)
            .ThenBy(v => v.ApplicationDeadline)
            .ToListAsync(cancellationToken);
    }
}

#endregion

// ============================================================================
// JOB VACANCY ATTACHMENT REPOSITORY
// ============================================================================

#region Job Vacancy Attachment Repository

public class JobVacancyAttachmentRepository : GenericRepository<JobVacancyAttachment>, IJobVacancyAttachmentRepository
{
    public JobVacancyAttachmentRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<JobVacancyAttachment>> GetByVacancyIdAsync(Guid vacancyId)
    {
        return await _dbSet
            .Include(a => a.UploadedBy)
            .Where(a => a.JobVacancyId == vacancyId && !a.IsDeleted)
            .OrderByDescending(a => a.UploadDate)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// JOB VACANCY STATUS HISTORY REPOSITORY
// ============================================================================

#region Job Vacancy Status History Repository

public class JobVacancyStatusHistoryRepository : GenericRepository<JobVacancyStatusHistory>, IJobVacancyStatusHistoryRepository
{
    public JobVacancyStatusHistoryRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<JobVacancyStatusHistory>> GetByVacancyIdAsync(Guid vacancyId)
    {
        return await _dbSet
            .Include(h => h.ChangedBy)
            .Where(h => h.JobVacancyId == vacancyId && !h.IsDeleted)
            .OrderByDescending(h => h.ChangedDate)
            .ToListAsync();
    }

    public async Task<JobVacancyStatusHistory?> GetLatestForVacancyAsync(Guid vacancyId)
    {
        return await _dbSet
            .Include(h => h.ChangedBy)
            .Where(h => h.JobVacancyId == vacancyId && !h.IsDeleted)
            .OrderByDescending(h => h.ChangedDate)
            .FirstOrDefaultAsync();
    }
}

#endregion

// ============================================================================
// VACANCY PIPELINE STAGE ASSIGNMENT REPOSITORY
// ============================================================================

#region Vacancy Pipeline Stage Assignment Repository

public class VacancyPipelineStageAssignmentRepository
    : GenericRepository<VacancyPipelineStageAssignment>, IVacancyPipelineStageAssignmentRepository
{
    public VacancyPipelineStageAssignmentRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<VacancyPipelineStageAssignment>> GetByVacancyIdAsync(Guid vacancyId)
    {
        return await _dbSet
            .Include(a => a.PipelineStage)
            .Include(a => a.AssignedTo)
            .Include(a => a.AssignedBy)
            .Include(a => a.CompletedBy)
            .Include(a => a.EscalateTo)
            .Where(a => a.JobVacancyId == vacancyId && !a.IsDeleted)
            .OrderBy(a => a.PipelineStage.Order)
            .ToListAsync();
    }

    public async Task<VacancyPipelineStageAssignment?> GetByVacancyAndStageAsync(Guid vacancyId, Guid stageId)
    {
        return await _dbSet
            .Include(a => a.PipelineStage)
            .Include(a => a.AssignedTo)
            .Include(a => a.AssignedBy)
            .Include(a => a.CompletedBy)
            .Include(a => a.EscalateTo)
            .FirstOrDefaultAsync(a => a.JobVacancyId == vacancyId
                                   && a.PipelineStageId == stageId
                                   && !a.IsDeleted);
    }

    public async Task<IEnumerable<VacancyPipelineStageAssignment>> GetOverdueAsync(DateTime asOf)
    {
        return await _dbSet
            .Include(a => a.JobVacancy)
            .Include(a => a.PipelineStage)
            .Include(a => a.AssignedTo)
            .Include(a => a.EscalateTo)
            .Where(a => !a.IsDeleted
                     && a.DueDate.HasValue
                     && a.DueDate.Value < asOf
                     && a.Status != VacancyStageAssignmentStatus.Completed
                     && a.Status != VacancyStageAssignmentStatus.Skipped)
            .ToListAsync();
    }
}

#endregion
