using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.HR;

// ============================================================================
// JOB OFFER REPOSITORY
// ============================================================================

#region Job Offer Repository

public class JobOfferRepository : GenericRepository<JobOffer>, IJobOfferRepository
{
    public JobOfferRepository(ApplicationDbContext context) : base(context) { }

    /// <summary>
    /// Everything <c>ToDto</c> and <c>ToSummaryDto</c> read through a navigation.
    ///
    /// <para>⚠ Most reads here already included the candidate, but <b>there was no
    /// <c>GetByIdAsync</c> override</b> — so <c>GET /{id}</c> and every write response that goes
    /// through <c>GetOwnedOfferAsync</c> (update, approve, issue, revoke, record-response) came back
    /// with an empty <c>candidateName</c>: the one field that says whose offer it is.
    /// <c>GetByApplicationIdAsync</c> was missing the chain too. Same shape as the interview and
    /// hire repositories.</para>
    /// </summary>
    private IQueryable<JobOffer> WithSummaryNavigations() =>
        _dbSet
            .Include(o => o.Application).ThenInclude(a => a.JobCandidate)
            .Include(o => o.Application).ThenInclude(a => a.JobVacancy).ThenInclude(v => v.Position)
            .Include(o => o.PreparedBy)
            .Include(o => o.ApprovedBy);

    public override async Task<JobOffer?> GetByIdAsync(Guid id)
        => await WithSummaryNavigations().FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted);

    public async Task<JobOffer?> GetByOfferNumberAsync(string offerNumber)
    {
        return await WithSummaryNavigations()
            .FirstOrDefaultAsync(o => o.OfferNumber == offerNumber && !o.IsDeleted);
    }

    public async Task<JobOffer?> GetWithFullDetailsAsync(Guid id)
    {
        return await _dbSet
            .Include(o => o.Application).ThenInclude(a => a.JobCandidate)
            .Include(o => o.Application).ThenInclude(a => a.JobVacancy).ThenInclude(v => v.Position)
            .Include(o => o.Position).ThenInclude(p => p.PositionBenefits).ThenInclude(pb => pb.BenefitPolicy)
            .Include(o => o.Benefits)
            .Include(o => o.PreparedBy)
            .Include(o => o.ApprovedBy)
            .FirstOrDefaultAsync(o => o.Id == id && !o.IsDeleted);
    }

    public async Task<IEnumerable<JobOffer>> GetByApplicationIdAsync(Guid applicationId)
    {
        return await WithSummaryNavigations()
            .Include(o => o.Location)
            .Include(o => o.Benefits)
            .Where(o => o.JobApplicationId == applicationId && !o.IsDeleted)
            .OrderByDescending(o => o.OfferDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<JobOffer>> GetByStatusAsync(JobOfferStatus status)
    {
        return await WithSummaryNavigations()
            .Where(o => o.OfferStatus == status && !o.IsDeleted)
            .OrderByDescending(o => o.OfferDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<JobOffer>> GetByPreparedByAsync(Guid employeeId)
    {
        return await WithSummaryNavigations()
            .Where(o => o.PreparedById == employeeId && !o.IsDeleted)
            .OrderByDescending(o => o.OfferDate)
            .ToListAsync();
    }

    /// <summary>
    /// Offers that need chasing: sent, unanswered, and running out within
    /// <paramref name="daysAhead"/> days.
    /// </summary>
    /// <remarks>
    /// <para>⚠ G-2.4 / G-15.2 (2026-09-15). This query feeds the landing page's *Offers expiring
    /// soon* tile and the dashboard's *Offers expiring* card, and it had <b>no lower bound</b> — an
    /// offer whose expiry passed six months ago satisfied <c>ExpiryDate &lt;= now + 7 days</c> just
    /// as well as one expiring tomorrow. Combined with the fact that <c>JobOfferStatus.Expired</c>
    /// was never written by anything in the solution, a lapsed offer stayed <c>Sent</c> for ever
    /// and accumulated here permanently. The tile is labelled "Within 7 days" and was in practice
    /// "every offer ever sent that was not answered"; its amber tone became permanent the moment
    /// the first offer lapsed.</para>
    ///
    /// <para>Two things fix it and both are deliberate. The nightly sweep now writes
    /// <c>Expired</c>, so lapsed offers leave <c>Sent</c> — and this query is bounded anyway, so a
    /// sweep that does not run (a host down overnight, a tenant added between runs) cannot inflate
    /// the tile again. The failure mode being designed against is silence: nobody noticed the
    /// absence of that job for the life of the module.</para>
    ///
    /// <para><c>IsLatestVersion</c> filters out superseded versions. <c>ReviseOfferAsync</c> now
    /// moves those to <c>Superseded</c> (G-10.2), but every offer revised before that shipped is
    /// still sitting in <c>Sent</c>, and chasing a candidate about terms that have been replaced is
    /// the wrong conversation.</para>
    /// </remarks>
    public async Task<IEnumerable<JobOffer>> GetExpiringOffersAsync(int daysAhead = 3)
    {
        var now = DateTime.UtcNow;
        var threshold = now.AddDays(daysAhead);
        return await WithSummaryNavigations()
            .Where(o => o.OfferStatus == JobOfferStatus.Sent
                     && o.IsLatestVersion
                     && o.ExpiryDate != null
                     && o.ExpiryDate >= now
                     && o.ExpiryDate <= threshold
                     && !o.IsDeleted)
            .OrderBy(o => o.ExpiryDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<JobOffer>> GetAllForSummaryAsync()
    {
        return await WithSummaryNavigations()
            .Where(o => !o.IsDeleted)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();
    }

    public async Task<string> GetNextOfferNumberAsync()
    {
        var last = await _dbSet
            .Where(o => !o.IsDeleted)
            .OrderByDescending(o => o.OfferNumber)
            .Select(o => o.OfferNumber)
            .FirstOrDefaultAsync();

        var next = 1;
        if (last != null && int.TryParse(last.Replace("OFR-", ""), out var parsed))
            next = parsed + 1;

        return $"OFR-{next:D6}";
    }
}

#endregion

// ============================================================================
// JOB HIRE RECORD REPOSITORY
// ============================================================================

#region Job Hire Record Repository

public class JobHireRecordRepository : GenericRepository<JobHireRecord>, IJobHireRecordRepository
{
    public JobHireRecordRepository(ApplicationDbContext context) : base(context) { }

    /// <summary>
    /// Everything <c>ToDto</c> and <c>ToSummaryDto</c> read through a navigation.
    ///
    /// <para>⚠ <c>Employee</c> and <c>ConfirmedBy</c> were included by <b>no</b> read, while the DTO
    /// reads <c>Employee.EmployeeNumber</c>, <c>Employee.FullName</c> and
    /// <c>ConfirmedBy.FullName</c>. A hire record exists to record that a candidate became an
    /// employee, and that link came back empty on every endpoint — including immediately after
    /// <c>ConfirmStartAsync</c> had just created the employee.</para>
    /// </summary>
    private IQueryable<JobHireRecord> WithSummaryNavigations() =>
        _dbSet
            .Include(h => h.Application).ThenInclude(a => a.JobCandidate)
            .Include(h => h.Application).ThenInclude(a => a.JobVacancy).ThenInclude(v => v.Position)
            .Include(h => h.Offer)
            .Include(h => h.Employee)
            .Include(h => h.ConfirmedBy);

    public override async Task<JobHireRecord?> GetByIdAsync(Guid id)
        => await WithSummaryNavigations().FirstOrDefaultAsync(h => h.Id == id && !h.IsDeleted);

    public async Task<JobHireRecord?> GetByHireNumberAsync(string hireNumber)
    {
        return await WithSummaryNavigations()
            .FirstOrDefaultAsync(h => h.HireNumber == hireNumber && !h.IsDeleted);
    }

    public async Task<JobHireRecord?> GetByApplicationIdAsync(Guid applicationId)
    {
        return await WithSummaryNavigations()
            .FirstOrDefaultAsync(h => h.ApplicationId == applicationId && !h.IsDeleted);
    }

    public async Task<IEnumerable<JobHireRecord>> GetByStatusAsync(JobHireStatus status)
    {
        return await WithSummaryNavigations()
            .Where(h => h.Status == status && !h.IsDeleted)
            .OrderByDescending(h => h.CreatedAt)
            .ToListAsync();
    }

    public async Task<JobHireRecord?> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await WithSummaryNavigations()
            .FirstOrDefaultAsync(h => h.EmployeeId == employeeId && !h.IsDeleted);
    }

    public async Task<IEnumerable<JobHireRecord>> GetWithStartDateApproachingAsync(int daysAhead = 14)
    {
        var threshold = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(daysAhead));
        return await WithSummaryNavigations()
            .Where(h => h.ExpectedStartDate != null
                     && h.ExpectedStartDate <= threshold
                     && h.Status == JobHireStatus.PendingOnboarding
                     && !h.IsDeleted)
            .OrderBy(h => h.ExpectedStartDate)
            .ToListAsync();
    }

    public async Task<string> GetNextHireNumberAsync()
    {
        var last = await _dbSet
            .Where(h => !h.IsDeleted)
            .OrderByDescending(h => h.HireNumber)
            .Select(h => h.HireNumber)
            .FirstOrDefaultAsync();

        var next = 1;
        if (last != null && int.TryParse(last.Replace("HIR-", ""), out var parsed))
            next = parsed + 1;

        return $"HIR-{next:D6}";
    }

    public async Task<JobHireRecord?> GetForConfirmStartAsync(Guid id)
    {
        return await _dbSet
            // Offer → Position (org structure + salary grade fallback)
            .Include(h => h.Offer)
                .ThenInclude(o => o.Position)
                    .ThenInclude(p => p.SalaryGrade)
            // Offer → SalaryLevel (primary salary grade source)
            .Include(h => h.Offer)
                .ThenInclude(o => o.SalaryLevel)
            // Offer → Application → Candidate (profile collections)
            .Include(h => h.Offer)
                .ThenInclude(o => o.Application)
                    .ThenInclude(a => a.JobCandidate)
                        .ThenInclude(c => c.Qualifications)
            .Include(h => h.Offer)
                .ThenInclude(o => o.Application)
                    .ThenInclude(a => a.JobCandidate)
                        .ThenInclude(c => c.WorkHistories)
            .Include(h => h.Offer)
                .ThenInclude(o => o.Application)
                    .ThenInclude(a => a.JobCandidate)
                        .ThenInclude(c => c.Referees)
            .Include(h => h.Offer)
                .ThenInclude(o => o.Application)
                    .ThenInclude(a => a.JobCandidate)
                        .ThenInclude(c => c.Skills)
            // Direct Application → Candidate (same collections, alternate path)
            .Include(h => h.Application)
                .ThenInclude(a => a.JobCandidate)
                    .ThenInclude(c => c.Qualifications)
            .Include(h => h.Application)
                .ThenInclude(a => a.JobCandidate)
                    .ThenInclude(c => c.WorkHistories)
            .Include(h => h.Application)
                .ThenInclude(a => a.JobCandidate)
                    .ThenInclude(c => c.Referees)
            .Include(h => h.Application)
                .ThenInclude(a => a.JobCandidate)
                    .ThenInclude(c => c.Skills)
            .FirstOrDefaultAsync(h => h.Id == id && !h.IsDeleted);
    }
}

#endregion

// ============================================================================
// JOB OFFER BENEFIT REPOSITORY
// ============================================================================

#region Job Offer Benefit Repository

public class JobOfferBenefitRepository : GenericRepository<JobOfferBenefit>, IJobOfferBenefitRepository
{
    public JobOfferBenefitRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<JobOfferBenefit>> GetByOfferIdAsync(Guid offerId)
    {
        return await _dbSet
            .Where(b => b.JobOfferId == offerId && !b.IsDeleted)
            .OrderBy(b => b.DisplayOrder)
            .ThenBy(b => b.BenefitName)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// JOB OFFER NOTE REPOSITORY
// ============================================================================

#region Job Offer Note Repository

public class JobOfferNoteRepository : GenericRepository<JobOfferNote>, IJobOfferNoteRepository
{
    public JobOfferNoteRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<JobOfferNote>> GetByOfferIdAsync(Guid offerId)
    {
        return await _dbSet
            .Where(n => n.JobOfferId == offerId && !n.IsDeleted)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();
    }
}

#endregion