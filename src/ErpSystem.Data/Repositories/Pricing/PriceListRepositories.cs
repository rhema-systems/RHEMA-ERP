using ErpSystem.Core.Entities.Pricing;
using ErpSystem.Core.Interfaces.Pricing;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.Pricing;

#region Price List Repository

/// <summary>
/// Repository implementation for Price Lists
/// </summary>
public class PriceListRepository : GenericRepository<PriceList>, IPriceListRepository
{
    public PriceListRepository(ApplicationDbContext context) : base(context) { }

    public async Task<PriceList?> GetByCodeAsync(string priceListCode)
    {
        if (string.IsNullOrWhiteSpace(priceListCode))
            return null;

        return await _dbSet
            .Where(p => p.PriceListCode == priceListCode && !p.IsDeleted)
            .FirstOrDefaultAsync();
    }

    public async Task<PriceList?> GetByIdWithLinesAsync(Guid id)
    {
        if (id == Guid.Empty)
            return null;

        return await _dbSet
            .Where(p => p.Id == id && !p.IsDeleted)
            .Include(p => p.Lines.Where(l => !l.IsDeleted))
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<PriceList>> GetActiveByTypeAsync(PriceListType type)
    {
        return await _dbSet
            .Where(p => p.Type == type && 
                       p.Status == PriceListStatus.Active &&
                       p.ApprovalStatus == PriceListApprovalStatus.Approved &&
                       !p.IsDeleted)
            .OrderByDescending(p => p.Priority)
            .ThenBy(p => p.PriceListCode)
            .ToListAsync();
    }

    public async Task<IEnumerable<PriceList>> GetBySupplierAsync(Guid supplierId)
    {
        if (supplierId == Guid.Empty)
            return new List<PriceList>();

        return await _dbSet
            .Where(p => p.ApplicableEntityType == PriceListApplicableEntityType.Supplier &&
                       p.ApplicableEntityId == supplierId &&
                       !p.IsDeleted)
            .OrderByDescending(p => p.EffectiveFrom)
            .ToListAsync();
    }

    public async Task<IEnumerable<PriceList>> GetByCustomerAsync(Guid customerId)
    {
        if (customerId == Guid.Empty)
            return new List<PriceList>();

        return await _dbSet
            .Where(p => p.ApplicableEntityType == PriceListApplicableEntityType.Customer &&
                       p.ApplicableEntityId == customerId &&
                       !p.IsDeleted)
            .OrderByDescending(p => p.EffectiveFrom)
            .ToListAsync();
    }

    public async Task<PriceList?> GetDefaultByTypeAsync(PriceListType type)
    {
        return await _dbSet
            .Where(p => p.Type == type &&
                       p.IsDefault &&
                       p.Status == PriceListStatus.Active &&
                       p.ApprovalStatus == PriceListApprovalStatus.Approved &&
                       !p.IsDeleted)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<PriceList>> GetEffectivePriceListsAsync(
        PriceListType type, DateTime? asOfDate = null)
    {
        var date = asOfDate ?? DateTime.UtcNow;

        return await _dbSet
            .Where(p => p.Type == type &&
                       p.Status == PriceListStatus.Active &&
                       p.ApprovalStatus == PriceListApprovalStatus.Approved &&
                       p.EffectiveFrom <= date &&
                       (p.EffectiveTo == null || p.EffectiveTo > date) &&
                       !p.IsDeleted)
            .OrderByDescending(p => p.Priority)
            .ThenByDescending(p => p.EffectiveFrom)
            .ToListAsync();
    }

    public async Task<IEnumerable<PriceList>> GetPendingApprovalAsync()
    {
        return await _dbSet
            .Where(p => p.ApprovalStatus == PriceListApprovalStatus.PendingApproval && !p.IsDeleted)
            .OrderBy(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<PriceList>> GetByStatusAsync(PriceListStatus status)
    {
        return await _dbSet
            .Where(p => p.Status == status && !p.IsDeleted)
            .OrderBy(p => p.PriceListCode)
            .ToListAsync();
    }

    public async Task<IEnumerable<PriceList>> SearchAsync(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return await GetAllAsync();

        var term = searchTerm.ToLower();
        return await _dbSet
            .Where(p => !p.IsDeleted &&
                       (p.PriceListCode.ToLower().Contains(term) ||
                        p.Name.ToLower().Contains(term) ||
                        (p.Description != null && p.Description.ToLower().Contains(term))))
            .OrderBy(p => p.PriceListCode)
            .ToListAsync();
    }

    public async Task<IEnumerable<PriceList>> GetExpiringSoonAsync(int daysAhead = 30)
    {
        var futureDate = DateTime.UtcNow.AddDays(daysAhead);
        return await _dbSet
            .Where(p => p.Status == PriceListStatus.Active &&
                       p.EffectiveTo != null &&
                       p.EffectiveTo <= futureDate &&
                       p.EffectiveTo > DateTime.UtcNow &&
                       !p.IsDeleted)
            .OrderBy(p => p.EffectiveTo)
            .ToListAsync();
    }

    public async Task<bool> CodeExistsAsync(string priceListCode, Guid? excludeId = null)
    {
        if (string.IsNullOrWhiteSpace(priceListCode))
            return false;

        return await _dbSet
            .AnyAsync(p => p.PriceListCode == priceListCode &&
                          !p.IsDeleted &&
                          (excludeId == null || p.Id != excludeId));
    }
}

#endregion

#region Price List Line Repository

/// <summary>
/// Repository implementation for Price List Lines
/// </summary>
public class PriceListLineRepository : GenericRepository<PriceListLine>, IPriceListLineRepository
{
    public PriceListLineRepository(ApplicationDbContext context) : base(context) { }

    public override async Task<PriceListLine?> GetByIdAsync(Guid id)
    {
        return await _dbSet
            .Where(l => l.Id == id && !l.IsDeleted)
            .Include(l => l.InventoryItem)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<PriceListLine>> GetByPriceListIdAsync(Guid priceListId)
    {
        if (priceListId == Guid.Empty)
            return new List<PriceListLine>();

        return await _dbSet
            .Where(l => l.PriceListId == priceListId && !l.IsDeleted)
            .Include(l => l.InventoryItem)
            .OrderBy(l => l.MinQuantity)
            .ToListAsync();
    }

    public async Task<IEnumerable<PriceListLine>> GetByInventoryItemAsync(Guid inventoryItemId)
    {
        if (inventoryItemId == Guid.Empty)
            return new List<PriceListLine>();

        return await _dbSet
            .Where(l => l.InventoryItemId == inventoryItemId &&
                       l.IsActive &&
                       !l.IsDeleted)
            .Include(l => l.PriceList)
            .Include(l => l.InventoryItem)
            .OrderByDescending(l => l.PriceList.Priority)
            .ToListAsync();
    }

    public async Task<PriceListLine?> GetByPriceListAndItemAsync(Guid priceListId, Guid inventoryItemId)
    {
        if (priceListId == Guid.Empty || inventoryItemId == Guid.Empty)
            return null;

        return await _dbSet
            .Where(l => l.PriceListId == priceListId &&
                       l.InventoryItemId == inventoryItemId &&
                       !l.IsDeleted)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<PriceListLine>> GetActivePriceLinesForItemAsync(
        Guid inventoryItemId,
        PriceListType priceListType,
        decimal quantity = 1)
    {
        if (inventoryItemId == Guid.Empty)
            return new List<PriceListLine>();

        var now = DateTime.UtcNow;
        return await _dbSet
            .Where(l => l.InventoryItemId == inventoryItemId &&
                       l.IsActive &&
                       !l.IsDeleted &&
                       l.PriceList.Type == priceListType &&
                       l.PriceList.Status == PriceListStatus.Active &&
                       l.PriceList.ApprovalStatus == PriceListApprovalStatus.Approved &&
                       l.PriceList.EffectiveFrom <= now &&
                       (l.PriceList.EffectiveTo == null || l.PriceList.EffectiveTo > now) &&
                       l.MinQuantity <= quantity &&
                       (l.MaxQuantity == null || l.MaxQuantity >= quantity))
            .Include(l => l.PriceList)
            .OrderByDescending(l => l.PriceList.Priority)
            .ThenByDescending(l => l.MinQuantity)
            .ToListAsync();
    }

    public async Task<IEnumerable<PriceListLine>> GetByInventoryItemsAsync(IEnumerable<Guid> inventoryItemIds)
    {
        var ids = inventoryItemIds.ToList();
        if (!ids.Any())
            return new List<PriceListLine>();

        return await _dbSet
            .Where(l => ids.Contains(l.InventoryItemId) &&
                       l.IsActive &&
                       !l.IsDeleted)
            .Include(l => l.PriceList)
            .ToListAsync();
    }

    public async Task<IEnumerable<PriceListLine>> GetLinesWithRecentChangesAsync(DateTime since)
    {
        return await _dbSet
            .Where(l => l.LastPriceUpdate != null &&
                       l.LastPriceUpdate >= since &&
                       !l.IsDeleted)
            .Include(l => l.PriceList)
            .OrderByDescending(l => l.LastPriceUpdate)
            .ToListAsync();
    }

    public async Task BulkUpdatePricesAsync(IEnumerable<PriceListLine> lines)
    {
        foreach (var line in lines)
        {
            _context.Entry(line).State = EntityState.Modified;
        }
        // SaveChanges is called via UnitOfWork
    }

    public async Task DeleteByPriceListIdAsync(Guid priceListId)
    {
        if (priceListId == Guid.Empty)
            return;

        var lines = await _dbSet
            .Where(l => l.PriceListId == priceListId)
            .ToListAsync();

        foreach (var line in lines)
        {
            line.IsDeleted = true;
            line.DeletedAt = DateTime.UtcNow;
        }
    }
}

#endregion

#region Customer Group Repository

/// <summary>
/// Repository implementation for Customer Groups
/// </summary>
public class CustomerGroupRepository : GenericRepository<CustomerGroup>, ICustomerGroupRepository
{
    public CustomerGroupRepository(ApplicationDbContext context) : base(context) { }

    public async Task<CustomerGroup?> GetByCodeAsync(string groupCode)
    {
        if (string.IsNullOrWhiteSpace(groupCode))
            return null;

        return await _dbSet
            .Where(g => g.GroupCode == groupCode && !g.IsDeleted)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<CustomerGroup>> GetActiveGroupsAsync()
    {
        return await _dbSet
            .Where(g => g.IsActive && !g.IsDeleted)
            .OrderBy(g => g.Name)
            .ToListAsync();
    }

    public async Task<bool> CodeExistsAsync(string groupCode, Guid? excludeId = null)
    {
        if (string.IsNullOrWhiteSpace(groupCode))
            return false;

        return await _dbSet
            .AnyAsync(g => g.GroupCode == groupCode &&
                          !g.IsDeleted &&
                          (excludeId == null || g.Id != excludeId));
    }
}

#endregion

#region Supplier Group Repository

/// <summary>
/// Repository implementation for Supplier Groups
/// </summary>
public class SupplierGroupRepository : GenericRepository<SupplierGroup>, ISupplierGroupRepository
{
    public SupplierGroupRepository(ApplicationDbContext context) : base(context) { }

    public async Task<SupplierGroup?> GetByCodeAsync(string groupCode)
    {
        if (string.IsNullOrWhiteSpace(groupCode))
            return null;

        return await _dbSet
            .Where(g => g.GroupCode == groupCode && !g.IsDeleted)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<SupplierGroup>> GetActiveGroupsAsync()
    {
        return await _dbSet
            .Where(g => g.IsActive && !g.IsDeleted)
            .OrderBy(g => g.Name)
            .ToListAsync();
    }

    public async Task<bool> CodeExistsAsync(string groupCode, Guid? excludeId = null)
    {
        if (string.IsNullOrWhiteSpace(groupCode))
            return false;

        return await _dbSet
            .AnyAsync(g => g.GroupCode == groupCode &&
                          !g.IsDeleted &&
                          (excludeId == null || g.Id != excludeId));
    }
}

#endregion

#region Price List Change History Repository

/// <summary>
/// Repository implementation for Price List Change History
/// </summary>
public class PriceListChangeHistoryRepository : GenericRepository<PriceListChangeHistory>, IPriceListChangeHistoryRepository
{
    public PriceListChangeHistoryRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<PriceListChangeHistory>> GetByInventoryItemAsync(Guid inventoryItemId)
    {
        if (inventoryItemId == Guid.Empty)
            return new List<PriceListChangeHistory>();

        return await _dbSet
            .Where(h => h.InventoryItemId == inventoryItemId && !h.IsDeleted)
            .OrderByDescending(h => h.EffectiveDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<PriceListChangeHistory>> GetByPriceListLineAsync(Guid priceListLineId)
    {
        if (priceListLineId == Guid.Empty)
            return new List<PriceListChangeHistory>();

        return await _dbSet
            .Where(h => h.PriceListLineId == priceListLineId && !h.IsDeleted)
            .OrderByDescending(h => h.EffectiveDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<PriceListChangeHistory>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        return await _dbSet
            .Where(h => h.EffectiveDate >= startDate &&
                       h.EffectiveDate <= endDate &&
                       !h.IsDeleted)
            .OrderByDescending(h => h.EffectiveDate)
            .ToListAsync();
    }
}

#endregion

