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

    public async Task<JobOffer?> GetByOfferNumberAsync(string offerNumber)
    {
        return await _dbSet
            .Include(o => o.Application).ThenInclude(a => a.JobCandidate)
            .Include(o => o.Application).ThenInclude(a => a.JobVacancy).ThenInclude(v => v.Position)
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
        return await _dbSet
            .Include(o => o.PreparedBy)
            .Include(o => o.Location)
            .Include(o => o.Benefits)
            .Where(o => o.JobApplicationId == applicationId && !o.IsDeleted)
            .OrderByDescending(o => o.OfferDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<JobOffer>> GetByStatusAsync(JobOfferStatus status)
    {
        return await _dbSet
            .Include(o => o.Application).ThenInclude(a => a.JobCandidate)
            .Include(o => o.Application).ThenInclude(a => a.JobVacancy).ThenInclude(v => v.Position)
            .Where(o => o.OfferStatus == status && !o.IsDeleted)
            .OrderByDescending(o => o.OfferDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<JobOffer>> GetByPreparedByAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(o => o.Application).ThenInclude(a => a.JobCandidate)
            .Include(o => o.Application).ThenInclude(a => a.JobVacancy).ThenInclude(v => v.Position)
            .Where(o => o.PreparedById == employeeId && !o.IsDeleted)
            .OrderByDescending(o => o.OfferDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<JobOffer>> GetExpiringOffersAsync(int daysAhead = 3)
    {
        var threshold = DateTime.UtcNow.AddDays(daysAhead);
        return await _dbSet
            .Include(o => o.Application).ThenInclude(a => a.JobCandidate)
            .Where(o => o.OfferStatus == JobOfferStatus.Sent
                     && o.ExpiryDate != null
                     && o.ExpiryDate <= threshold
                     && !o.IsDeleted)
            .OrderBy(o => o.ExpiryDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<JobOffer>> GetAllForSummaryAsync()
    {
        return await _dbSet
            .Include(o => o.Application).ThenInclude(a => a.JobCandidate)
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

    public async Task<JobHireRecord?> GetByHireNumberAsync(string hireNumber)
    {
        return await _dbSet
            .Include(h => h.Application).ThenInclude(a => a.JobCandidate)
            .Include(h => h.Application).ThenInclude(a => a.JobVacancy).ThenInclude(v => v.Position)
            .FirstOrDefaultAsync(h => h.HireNumber == hireNumber && !h.IsDeleted);
    }

    public async Task<JobHireRecord?> GetByApplicationIdAsync(Guid applicationId)
    {
        return await _dbSet
            .Include(h => h.Application).ThenInclude(a => a.JobCandidate)
            .Include(h => h.Application).ThenInclude(a => a.JobVacancy).ThenInclude(v => v.Position)
            .Include(h => h.Offer)
            .FirstOrDefaultAsync(h => h.ApplicationId == applicationId && !h.IsDeleted);
    }

    public async Task<IEnumerable<JobHireRecord>> GetByStatusAsync(JobHireStatus status)
    {
        return await _dbSet
            .Include(h => h.Application).ThenInclude(a => a.JobCandidate)
            .Include(h => h.Application).ThenInclude(a => a.JobVacancy).ThenInclude(v => v.Position)
            .Where(h => h.Status == status && !h.IsDeleted)
            .OrderByDescending(h => h.CreatedAt)
            .ToListAsync();
    }

    public async Task<JobHireRecord?> GetByEmployeeIdAsync(Guid employeeId)
    {
        return await _dbSet
            .Include(h => h.Application).ThenInclude(a => a.JobVacancy).ThenInclude(v => v.Position)
            .Include(h => h.Offer)
            .FirstOrDefaultAsync(h => h.EmployeeId == employeeId && !h.IsDeleted);
    }

    public async Task<IEnumerable<JobHireRecord>> GetWithStartDateApproachingAsync(int daysAhead = 14)
    {
        var threshold = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(daysAhead));
        return await _dbSet
            .Include(h => h.Application).ThenInclude(a => a.JobCandidate)
            .Include(h => h.Application).ThenInclude(a => a.JobVacancy).ThenInclude(v => v.Position)
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