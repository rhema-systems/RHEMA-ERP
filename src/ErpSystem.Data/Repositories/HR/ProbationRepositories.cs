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

    // Every read on this repository includes the reviews, and the include is FILTERED on
    // !IsDeleted. Both halves matter: ProbationPeriodDto.ReviewCount is mapped from
    // entity.Reviews.Count, so a read without the include reported 0 for every row (the register
    // review column was a lie), and an unfiltered include would count soft-deleted reviews back
    // in - DeleteAsync here is a soft delete.
    public async Task<IEnumerable<ProbationPeriod>> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(p => p.Employee)
            .Include(p => p.Reviews.Where(r => !r.IsDeleted))
            .Where(p => p.EmployeeId == employeeId && !p.IsDeleted)
            .OrderByDescending(p => p.StartDate)
            .ToListAsync();
    }

    /// <summary>
    /// A single probation with the navigations its DTO reads. The generic <c>GetByIdAsync</c> has
    /// no includes, so <c>GET {id}</c> and every write response resolved a blank employee name
    /// while the lists beside them resolved it correctly.
    /// </summary>
    public async Task<ProbationPeriod?> GetByIdWithDetailsAsync(Guid id)
    {
        return await _dbSet
            .Include(p => p.Employee)
            .Include(p => p.Reviews.Where(r => !r.IsDeleted))
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
    }

    /// <summary>The register read: one page, active first, then by end date.</summary>
    public async Task<(IEnumerable<ProbationPeriod> Items, int TotalCount)> GetPagedAsync(
        Guid tenantId, int page, int pageSize, ProbationStatus? status, Guid? employeeId, string? search)
    {
        var query = _dbSet
            .Include(p => p.Employee)
            .Include(p => p.Reviews.Where(r => !r.IsDeleted))
            .Where(p => p.TenantId == tenantId && !p.IsDeleted);

        if (status.HasValue) query = query.Where(p => p.Status == status.Value);
        if (employeeId.HasValue) query = query.Where(p => p.EmployeeId == employeeId.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(p =>
                EF.Functions.Like(p.Employee.FirstName + " " + p.Employee.LastName, "%" + term + "%")
                || EF.Functions.Like(p.Employee.EmployeeNumber, "%" + term + "%"));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(p => p.Status == ProbationStatus.Active ? 0 : 1)
            .ThenBy(p => p.CurrentEndDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public async Task<IEnumerable<ProbationPeriod>> GetByStatusAsync(ProbationStatus status)
    {
        return await _dbSet
            .Include(p => p.Employee)
            .Include(p => p.Reviews.Where(r => !r.IsDeleted))
            .Where(p => p.Status == status && !p.IsDeleted)
            .OrderBy(p => p.CurrentEndDate)
            .ToListAsync();
    }

    public async Task<ProbationPeriod?> GetWithReviewsAsync(Guid id)
    {
        return await _dbSet
            .Include(p => p.Employee)
            .Include(p => p.Reviews.Where(r => !r.IsDeleted)).ThenInclude(r => r.ReviewedBy)
            .Include(p => p.Reviews.Where(r => !r.IsDeleted)).ThenInclude(r => r.SecondReviewer)
            .Include(p => p.Reviews.Where(r => !r.IsDeleted)).ThenInclude(r => r.HrApprovedBy)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
    }

    public async Task<IEnumerable<ProbationPeriod>> GetActiveProbationsAsync()
    {
        return await _dbSet
            .Include(p => p.Employee)
            .Include(p => p.Reviews.Where(r => !r.IsDeleted))
            .Where(p => p.Status == ProbationStatus.Active && !p.IsDeleted)
            .OrderBy(p => p.CurrentEndDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<ProbationPeriod>> GetEndingWithinAsync(int daysAhead)
    {
        var threshold = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(daysAhead));
        return await _dbSet
            .Include(p => p.Employee)
            .Include(p => p.Reviews.Where(r => !r.IsDeleted))
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

    // All four reads below carry the SAME three actor navigations on purpose. Before slice 1 they
    // did not: the by-probation read included only ReviewedBy (so secondReviewerName and
    // hrApprovedByName were always blank) and the reviewer own queue included none at all (so
    // reviewedByName was blank in exactly the screen that lists a reviewer work). A navigation
    // that is populated on one list and empty on another gives the caller no way to tell which
    // read is telling the truth.
    public async Task<IEnumerable<ProbationReview>> GetByProbationPeriodIdAsync(Guid probationPeriodId)
    {
        return await _dbSet
            .Include(r => r.ReviewedBy)
            .Include(r => r.SecondReviewer)
            .Include(r => r.HrApprovedBy)
            .Where(r => r.ProbationPeriodId == probationPeriodId && !r.IsDeleted)
            .OrderBy(r => r.ScheduledDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<ProbationReview>> GetByStatusAsync(ProbationReviewStatus status)
    {
        return await _dbSet
            .Include(r => r.ProbationPeriod).ThenInclude(p => p.Employee)
            .Include(r => r.ReviewedBy)
            .Include(r => r.SecondReviewer)
            .Include(r => r.HrApprovedBy)
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
            .Include(r => r.SecondReviewer)
            .Include(r => r.HrApprovedBy)
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
            .Include(r => r.ReviewedBy)
            .Include(r => r.SecondReviewer)
            .Include(r => r.HrApprovedBy)
            .Where(r => (r.ReviewedById == reviewerEmployeeId || r.SecondReviewerId == reviewerEmployeeId)
                     && !r.IsDeleted)
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
