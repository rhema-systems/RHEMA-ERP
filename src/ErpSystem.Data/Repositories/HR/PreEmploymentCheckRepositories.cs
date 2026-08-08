using ErpSystem.Core.Entities.HR.Recruitment;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.HR;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.HR;

// ============================================================================
// PRE-EMPLOYMENT CHECK REPOSITORY
// ============================================================================

#region Pre-Employment Check Repository

public class PreEmploymentCheckRepository : GenericRepository<PreEmploymentCheck>, IPreEmploymentCheckRepository
{
    public PreEmploymentCheckRepository(ApplicationDbContext context) : base(context) { }

    public async Task<PreEmploymentCheck?> GetByOfferIdAsync(Guid offerId)
    {
        return await _dbSet
            .Include(c => c.JobOffer).ThenInclude(o => o.Application).ThenInclude(a => a.JobCandidate)
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.JobOfferId == offerId && !c.IsDeleted);
    }

    public async Task<PreEmploymentCheck?> GetWithItemsAsync(Guid id)
    {
        return await _dbSet
            .Include(c => c.JobOffer).ThenInclude(o => o.Application).ThenInclude(a => a.JobCandidate)
            .Include(c => c.Items).ThenInclude(i => i.ReferenceResponse!).ThenInclude(r => r.Referee)
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);
    }

    public async Task<IEnumerable<PreEmploymentCheck>> GetByStatusAsync(PreEmploymentCheckStatus status)
    {
        return await _dbSet
            .Include(c => c.JobOffer).ThenInclude(o => o.Application).ThenInclude(a => a.JobCandidate)
            .Where(c => c.OverallStatus == status && !c.IsDeleted)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// PRE-EMPLOYMENT CHECK ITEM REPOSITORY
// ============================================================================

#region Pre-Employment Check Item Repository

public class PreEmploymentCheckItemRepository : GenericRepository<PreEmploymentCheckItem>, IPreEmploymentCheckItemRepository
{
    public PreEmploymentCheckItemRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<PreEmploymentCheckItem>> GetByPreEmploymentCheckIdAsync(Guid preEmploymentCheckId)
    {
        return await _dbSet
            .Include(i => i.ReferenceResponse)
            .Where(i => i.PreEmploymentCheckId == preEmploymentCheckId && !i.IsDeleted)
            .OrderBy(i => i.CheckType)
            .ToListAsync();
    }

    public async Task<IEnumerable<PreEmploymentCheckItem>> GetByStatusAsync(CheckItemStatus status, Guid? preEmploymentCheckId = null)
    {
        var query = _dbSet
            .Include(i => i.PreEmploymentCheck)
            .Where(i => i.Status == status && !i.IsDeleted);

        if (preEmploymentCheckId.HasValue)
            query = query.Where(i => i.PreEmploymentCheckId == preEmploymentCheckId.Value);

        return await query
            .OrderBy(i => i.CheckType)
            .ToListAsync();
    }

    public async Task<IEnumerable<PreEmploymentCheckItem>> GetMandatoryItemsAsync(Guid preEmploymentCheckId)
    {
        return await _dbSet
            .Where(i => i.PreEmploymentCheckId == preEmploymentCheckId && i.IsMandatory && !i.IsDeleted)
            .OrderBy(i => i.CheckType)
            .ToListAsync();
    }

    public async Task<IEnumerable<PreEmploymentCheckItem>> GetBlockingFailuresAsync(Guid preEmploymentCheckId)
    {
        return await _dbSet
            .Where(i => i.PreEmploymentCheckId == preEmploymentCheckId
                     && i.IsBlockingOnFail
                     && i.Passed == false
                     && !i.IsDeleted)
            .OrderBy(i => i.CheckType)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// REFERENCE CHECK RESPONSE REPOSITORY
// ============================================================================

#region Reference Check Response Repository

public class ReferenceCheckResponseRepository : GenericRepository<ReferenceCheckResponse>, IReferenceCheckResponseRepository
{
    public ReferenceCheckResponseRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<ReferenceCheckResponse>> GetByCheckItemIdAsync(Guid checkItemId)
    {
        return await _dbSet
            .Include(r => r.Referee)
            .Where(r => r.CheckItemId == checkItemId && !r.IsDeleted)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<ReferenceCheckResponse>> GetByRefereeIdAsync(Guid refereeId)
    {
        return await _dbSet
            .Include(r => r.CheckItem).ThenInclude(i => i.PreEmploymentCheck)
            .Where(r => r.RefereeId == refereeId && !r.IsDeleted)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }
}

#endregion

// ============================================================================
// PRE-EMPLOYMENT CHECK TEMPLATE
// ============================================================================

#region Pre-Employment Check Template

public class PreEmploymentCheckTemplateRepository : GenericRepository<PreEmploymentCheckTemplate>, IPreEmploymentCheckTemplateRepository
{
    public PreEmploymentCheckTemplateRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<PreEmploymentCheckTemplate>> GetAllActiveAsync()
    {
        return await _dbSet
            .Include(t => t.Items)
            .Where(t => t.IsActive && !t.IsDeleted)
            .OrderBy(t => t.Name)
            .ToListAsync();
    }

    public async Task<PreEmploymentCheckTemplate?> GetByIdWithItemsAsync(Guid id)
    {
        return await _dbSet
            .Include(t => t.Items)
            .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);
    }

    public async Task AddTemplateItemAsync(PreEmploymentCheckTemplateItem item)
    {
        await _context.Set<PreEmploymentCheckTemplateItem>().AddAsync(item);
    }
}

#endregion
