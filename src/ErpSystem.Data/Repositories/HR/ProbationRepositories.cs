using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.HR;

// ============================================================================
// PROBATION PERIOD REPOSITORY
// ============================================================================

#region Probation Period Repository

public class ProbationPeriodRepository : GenericRepository<ProbationPeriod>, IProbationPeriodRepository
{
    public ProbationPeriodRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<ProbationPeriod>> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(p => p.Employee)
            .Where(p => p.EmployeeId == employeeId && !p.IsDeleted)
            .OrderByDescending(p => p.StartDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<ProbationPeriod>> GetByStatusAsync(ProbationStatus status)
    {
        return await _dbSet
            .Include(p => p.Employee)
            .Where(p => p.Status == status && !p.IsDeleted)
            .OrderBy(p => p.CurrentEndDate)
            .ToListAsync();
    }

    public async Task<ProbationPeriod?> GetWithReviewsAsync(Guid id)
    {
        return await _dbSet
            .Include(p => p.Employee)
            .Include(p => p.Reviews).ThenInclude(r => r.ReviewedBy)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
    }

    public async Task<IEnumerable<ProbationPeriod>> GetActiveProbationsAsync()
    {
        return await _dbSet
            .Include(p => p.Employee)
            .Where(p => p.Status == ProbationStatus.Active && !p.IsDeleted)
            .OrderBy(p => p.CurrentEndDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<ProbationPeriod>> GetEndingWithinAsync(int daysAhead)
    {
        var threshold = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(daysAhead));
        return await _dbSet
            .Include(p => p.Employee)
            .Where(p => p.Status == ProbationStatus.Active
                     && p.CurrentEndDate <= threshold
                     && !p.IsDeleted)
            .OrderBy(p => p.CurrentEndDate)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// PROBATION REVIEW REPOSITORY
// ============================================================================

#region Probation Review Repository

public class ProbationReviewRepository : GenericRepository<ProbationReview>, IProbationReviewRepository
{
    public ProbationReviewRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<ProbationReview>> GetByProbationPeriodIdAsync(Guid probationPeriodId)
    {
        return await _dbSet
            .Include(r => r.ReviewedBy)
            .Where(r => r.ProbationPeriodId == probationPeriodId && !r.IsDeleted)
            .OrderBy(r => r.ScheduledDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<ProbationReview>> GetByStatusAsync(ProbationReviewStatus status)
    {
        return await _dbSet
            .Include(r => r.ProbationPeriod).ThenInclude(p => p.Employee)
            .Include(r => r.ReviewedBy)
            .Where(r => r.Status == status && !r.IsDeleted)
            .OrderBy(r => r.ScheduledDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<ProbationReview>> GetOverdueReviewsAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return await _dbSet
            .Include(r => r.ProbationPeriod).ThenInclude(p => p.Employee)
            .Include(r => r.ReviewedBy)
            .Where(r => !r.IsDeleted
                     && r.Status != ProbationReviewStatus.Completed
                     && r.ScheduledDate < today)
            .OrderBy(r => r.ScheduledDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<ProbationReview>> GetByReviewerIdAsync(Guid reviewerEmployeeId)
    {
        return await _dbSet
            .Include(r => r.ProbationPeriod).ThenInclude(p => p.Employee)
            .Where(r => r.ReviewedById == reviewerEmployeeId && !r.IsDeleted)
            .OrderByDescending(r => r.ScheduledDate)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// PROBATION EXTENSION REPOSITORY
// ============================================================================

#region Probation Extension Repository

public class ProbationExtensionRepository : GenericRepository<ProbationExtension>, IProbationExtensionRepository
{
    public ProbationExtensionRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<ProbationExtension>> GetByProbationIdAsync(Guid probationPeriodId)
    {
        return await _dbSet
            .Include(e => e.ExtendedBy)
            .Where(e => e.ProbationPeriodId == probationPeriodId && !e.IsDeleted)
            .OrderByDescending(e => e.ExtendedDate)
            .ToListAsync();
    }
}

#endregion
