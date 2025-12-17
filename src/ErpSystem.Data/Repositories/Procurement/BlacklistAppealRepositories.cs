using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Data.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.Procurement;

public class BlacklistAppealRepository : GenericRepository<BlacklistAppeal>, IBlacklistAppealRepository
{
    public BlacklistAppealRepository(ApplicationDbContext context) : base(context) { }

    public async Task<BlacklistAppeal?> GetByIdAsync(Guid id)
    {
        return await _dbSet
            .Where(a => a.Id == id && !a.IsDeleted)
            .Include(a => a.BusinessPartner)
            .Include(a => a.ReviewedBy)
            .Include(a => a.ApprovedBy)
            .FirstOrDefaultAsync();
    }

    public async Task<BlacklistAppeal?> GetByAppealNumberAsync(string appealNumber)
    {
        return await _dbSet
            .Where(a => a.AppealNumber == appealNumber && !a.IsDeleted)
            .Include(a => a.BusinessPartner)
            .Include(a => a.ReviewedBy)
            .Include(a => a.ApprovedBy)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<BlacklistAppeal>> GetByBusinessPartnerAsync(Guid businessPartnerId)
    {
        return await _dbSet
            .Where(a => a.BusinessPartnerId == businessPartnerId && !a.IsDeleted)
            .Include(a => a.ReviewedBy)
            .Include(a => a.ApprovedBy)
            .OrderByDescending(a => a.AppealDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<BlacklistAppeal>> GetByStatusAsync(string status)
    {
        return await _dbSet
            .Where(a => a.Status == status && !a.IsDeleted)
            .Include(a => a.BusinessPartner)
            .Include(a => a.ReviewedBy)
            .Include(a => a.ApprovedBy)
            .OrderByDescending(a => a.AppealDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<BlacklistAppeal>> GetPendingAppealsAsync()
    {
        return await _dbSet
            .Where(a => (a.Status == "Pending" || a.Status == "UnderReview") && !a.IsDeleted)
            .Include(a => a.BusinessPartner)
            .Include(a => a.ReviewedBy)
            .OrderBy(a => a.AppealDate)
            .ToListAsync();
    }

    public async Task<string> GenerateAppealNumberAsync()
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"APL-{year}-";
        
        var lastAppeal = await _dbSet
            .Where(a => a.AppealNumber.StartsWith(prefix))
            .OrderByDescending(a => a.AppealNumber)
            .FirstOrDefaultAsync();

        if (lastAppeal == null)
        {
            return $"{prefix}0001";
        }

        var lastNumber = int.Parse(lastAppeal.AppealNumber.Substring(prefix.Length));
        return $"{prefix}{(lastNumber + 1):D4}";
    }

    public async Task<BlacklistAppeal> CreateAsync(BlacklistAppeal appeal)
    {
        return await AddAsync(appeal);
    }

    public async Task<BlacklistAppeal> UpdateAsync(BlacklistAppeal appeal)
    {
        await base.UpdateAsync(appeal);
        return appeal;
    }

    public async Task DeleteAsync(Guid id)
    {
        var appeal = await GetByIdAsync(id);
        if (appeal != null)
        {
            await base.DeleteAsync(appeal);
        }
    }
}

public class BlacklistHistoryRepository : GenericRepository<BlacklistHistory>, IBlacklistHistoryRepository
{
    public BlacklistHistoryRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<BlacklistHistory>> GetByBusinessPartnerAsync(Guid businessPartnerId)
    {
        return await _dbSet
            .Where(h => h.BusinessPartnerId == businessPartnerId && !h.IsDeleted)
            .Include(h => h.ActionBy)
            .Include(h => h.RelatedAppeal)
            .OrderByDescending(h => h.ActionDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<BlacklistHistory>> GetByActionAsync(string action)
    {
        return await _dbSet
            .Where(h => h.Action == action && !h.IsDeleted)
            .Include(h => h.BusinessPartner)
            .Include(h => h.ActionBy)
            .OrderByDescending(h => h.ActionDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<BlacklistHistory>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        return await _dbSet
            .Where(h => h.ActionDate >= startDate && h.ActionDate <= endDate && !h.IsDeleted)
            .Include(h => h.BusinessPartner)
            .Include(h => h.ActionBy)
            .OrderByDescending(h => h.ActionDate)
            .ToListAsync();
    }

    public async Task<BlacklistHistory> CreateAsync(BlacklistHistory history)
    {
        return await AddAsync(history);
    }
}

