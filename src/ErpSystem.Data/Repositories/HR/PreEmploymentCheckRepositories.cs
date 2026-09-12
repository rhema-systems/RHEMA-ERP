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

    /// <summary>
    /// ⚠ Both reads take an explicit <paramref name="tenantId"/>.
    ///
    /// <para>They previously took none, and <c>PreEmploymentCheckTemplateService</c> had no tenant
    /// provider either — so the list read returned <b>every tenant's</b> templates and every
    /// by-id operation (read, update, delete, add/update/remove item) acted on any tenant's row.
    /// The <c>ApplicationDbContext</c> is registered without a tenant, so its global query filter is
    /// inert and cannot be relied on to catch this; scoping has to be explicit, as everywhere else
    /// in HR.</para>
    /// </summary>
    public async Task<IEnumerable<PreEmploymentCheckTemplate>> GetAllActiveAsync(Guid tenantId)
    {
        return await _dbSet
            .Include(t => t.Items.Where(i => !i.IsDeleted))
            .Where(t => t.TenantId == tenantId && t.IsActive && !t.IsDeleted)
            .OrderBy(t => t.Name)
            .ToListAsync();
    }

    public async Task<PreEmploymentCheckTemplate?> GetByIdWithItemsAsync(Guid id, Guid tenantId)
    {
        return await _dbSet
            .Include(t => t.Items.Where(i => !i.IsDeleted))
            .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId && !t.IsDeleted);
    }

    public async Task AddTemplateItemAsync(PreEmploymentCheckTemplateItem item)
    {
        await _context.Set<PreEmploymentCheckTemplateItem>().AddAsync(item);
    }
}

#endregion
