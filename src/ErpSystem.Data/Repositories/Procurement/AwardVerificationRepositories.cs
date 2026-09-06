using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.Procurement;

// Use alias to avoid conflict with PagedResult in ErpSystem.Data.Repositories namespace
using CorePagedResult = ErpSystem.Core.DTOs.Common;

#region Checklist Template Repository

public class AwardVerificationChecklistTemplateRepository : GenericRepository<AwardVerificationChecklistTemplate>, IAwardVerificationChecklistTemplateRepository
{
    public AwardVerificationChecklistTemplateRepository(ApplicationDbContext context) : base(context) { }

    public new async Task<AwardVerificationChecklistTemplate?> GetByIdAsync(Guid id)
    {
        return await _dbSet
            .Where(t => t.Id == id && !t.IsDeleted)
            .FirstOrDefaultAsync();
    }

    public async Task<AwardVerificationChecklistTemplate?> GetByIdWithItemsAsync(Guid id)
    {
        return await _dbSet
            .Where(t => t.Id == id && !t.IsDeleted)
            .Include(t => t.Items.Where(i => !i.IsDeleted).OrderBy(i => i.DisplayOrder))
            .FirstOrDefaultAsync();
    }

    public async Task<AwardVerificationChecklistTemplate?> GetByNameAsync(string name)
    {
        return await _dbSet
            .Where(t => t.Name == name && !t.IsDeleted)
            .FirstOrDefaultAsync();
    }

    public async Task<AwardVerificationChecklistTemplate?> GetDefaultTemplateAsync()
    {
        return await _dbSet
            .Where(t => t.IsDefault && t.IsActive && !t.IsDeleted)
            .Include(t => t.Items.Where(i => !i.IsDeleted && i.IsActive).OrderBy(i => i.DisplayOrder))
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<AwardVerificationChecklistTemplate>> GetAllAsync(bool includeInactive = false)
    {
        var query = _dbSet.Where(t => !t.IsDeleted);
        if (!includeInactive)
            query = query.Where(t => t.IsActive);
        return await query.OrderBy(t => t.DisplayOrder).ThenBy(t => t.Name).ToListAsync();
    }

    public async Task<IEnumerable<AwardVerificationChecklistTemplate>> GetByContractValueAsync(decimal contractValue)
    {
        return await _dbSet
            .Where(t => !t.IsDeleted && t.IsActive)
            .Where(t => (!t.MinContractValue.HasValue || t.MinContractValue <= contractValue) &&
                        (!t.MaxContractValue.HasValue || t.MaxContractValue >= contractValue))
            .Include(t => t.Items.Where(i => !i.IsDeleted && i.IsActive).OrderBy(i => i.DisplayOrder))
            .OrderBy(t => t.DisplayOrder)
            .ToListAsync();
    }

    public async Task<CorePagedResult.PagedResult<AwardVerificationChecklistTemplate>> GetPagedAsync(int page, int pageSize, string? search = null, bool includeInactive = false)
    {
        var query = _dbSet.Where(t => !t.IsDeleted);
        if (!includeInactive)
            query = query.Where(t => t.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(t => t.Name.Contains(search) || (t.Description != null && t.Description.Contains(search)));

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderBy(t => t.DisplayOrder).ThenBy(t => t.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new CorePagedResult.PagedResult<AwardVerificationChecklistTemplate>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<AwardVerificationChecklistTemplate> CreateAsync(AwardVerificationChecklistTemplate template)
    {
        await _dbSet.AddAsync(template);
        return template;
    }

    public new async Task<AwardVerificationChecklistTemplate> UpdateAsync(AwardVerificationChecklistTemplate template)
    {
        template.UpdatedAt = DateTime.UtcNow;
        _dbSet.Update(template);
        return await Task.FromResult(template);
    }

    public new async Task DeleteAsync(Guid id)
    {
        var template = await _dbSet.FindAsync(id);
        if (template != null)
        {
            template.IsDeleted = true;
            template.DeletedAt = DateTime.UtcNow;
        }
    }
}

#endregion

#region Checklist Item Repository

public class AwardVerificationChecklistItemRepository : GenericRepository<AwardVerificationChecklistItem>, IAwardVerificationChecklistItemRepository
{
    public AwardVerificationChecklistItemRepository(ApplicationDbContext context) : base(context) { }

    public new async Task<AwardVerificationChecklistItem?> GetByIdAsync(Guid id)
    {
        return await _dbSet.Where(i => i.Id == id && !i.IsDeleted).FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<AwardVerificationChecklistItem>> GetByTemplateIdAsync(Guid templateId)
    {
        return await _dbSet
            .Where(i => i.TemplateId == templateId && !i.IsDeleted)
            .OrderBy(i => i.DisplayOrder)
            .ToListAsync();
    }

    public async Task<AwardVerificationChecklistItem> CreateAsync(AwardVerificationChecklistItem item)
    {
        await _dbSet.AddAsync(item);
        return item;
    }

    public new async Task<AwardVerificationChecklistItem> UpdateAsync(AwardVerificationChecklistItem item)
    {
        item.UpdatedAt = DateTime.UtcNow;
        _dbSet.Update(item);
        return await Task.FromResult(item);
    }

    public new async Task DeleteAsync(Guid id)
    {
        var item = await _dbSet.FindAsync(id);
        if (item != null)
        {
            item.IsDeleted = true;
            item.DeletedAt = DateTime.UtcNow;
        }
    }

    public async Task DeleteByTemplateIdAsync(Guid templateId)
    {
        var items = await _dbSet.Where(i => i.TemplateId == templateId && !i.IsDeleted).ToListAsync();
        foreach (var item in items)
        {
            item.IsDeleted = true;
            item.DeletedAt = DateTime.UtcNow;
        }
    }
}

#endregion

#region Tender Award Verification Repository

public class TenderAwardVerificationRepository : GenericRepository<TenderAwardVerification>, ITenderAwardVerificationRepository
{
    public TenderAwardVerificationRepository(ApplicationDbContext context) : base(context) { }

    public new async Task<TenderAwardVerification?> GetByIdAsync(Guid id)
    {
        return await _dbSet.Where(v => v.Id == id && !v.IsDeleted).FirstOrDefaultAsync();
    }

    public async Task<TenderAwardVerification?> GetByIdWithDetailsAsync(Guid id)
    {
        var verification = await _dbSet
            .Where(v => v.Id == id && !v.IsDeleted)
            .Include(v => v.Tender)
            .Include(v => v.Template)
                .ThenInclude(t => t!.Items.Where(i => !i.IsDeleted && i.IsActive).OrderBy(i => i.DisplayOrder))
            .Include(v => v.StartedBy)
            .Include(v => v.CompletedBy)
            .FirstOrDefaultAsync();

        if (verification != null)
        {
            // Load bidders with all their related data separately to avoid EF Core filtered include issues
            await _context.Entry(verification)
                .Collection(v => v.Bidders)
                .Query()
                .Where(b => !b.IsDeleted)
                .Include(b => b.TenderBid)
                .Include(b => b.BusinessPartner)
                .Include(b => b.ItemResults.Where(r => !r.IsDeleted))
                    .ThenInclude(r => r.ChecklistItem)
                .Include(b => b.ItemResults.Where(r => !r.IsDeleted))
                    .ThenInclude(r => r.Documents.Where(d => !d.IsDeleted))
                .LoadAsync();
        }

        return verification;
    }

    public async Task<TenderAwardVerification?> GetByTenderIdAsync(Guid tenderId)
    {
        return await _dbSet.Where(v => v.TenderId == tenderId && !v.IsDeleted).FirstOrDefaultAsync();
    }

    public async Task<TenderAwardVerification?> GetByTenderIdWithDetailsAsync(Guid tenderId)
    {
        var verification = await _dbSet
            .Where(v => v.TenderId == tenderId && !v.IsDeleted)
            .Include(v => v.Tender)
            .Include(v => v.Template)
                .ThenInclude(t => t!.Items.Where(i => !i.IsDeleted && i.IsActive).OrderBy(i => i.DisplayOrder))
            .Include(v => v.StartedBy)
            .Include(v => v.CompletedBy)
            .FirstOrDefaultAsync();

        if (verification != null)
        {
            // Load bidders with all their related data separately to avoid EF Core filtered include issues
            await _context.Entry(verification)
                .Collection(v => v.Bidders)
                .Query()
                .Where(b => !b.IsDeleted)
                .Include(b => b.TenderBid)
                .Include(b => b.BusinessPartner)
                .Include(b => b.ItemResults.Where(r => !r.IsDeleted))
                    .ThenInclude(r => r.ChecklistItem)
                .Include(b => b.ItemResults.Where(r => !r.IsDeleted))
                    .ThenInclude(r => r.Documents.Where(d => !d.IsDeleted))
                .LoadAsync();
        }

        return verification;
    }

    public async Task<IEnumerable<TenderAwardVerification>> GetPendingVerificationsAsync()
    {
        return await _dbSet
            .Where(v => !v.IsDeleted && (v.Status == "Pending" || v.Status == "InProgress"))
            .Include(v => v.Tender)
            .Include(v => v.Bidders.Where(b => !b.IsDeleted))
            .OrderByDescending(v => v.CreatedAt)
            .ToListAsync();
    }

    public async Task<CorePagedResult.PagedResult<TenderAwardVerification>> GetPagedAsync(int page, int pageSize, string? status = null)
    {
        var query = _dbSet.Where(v => !v.IsDeleted);
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(v => v.Status == status);

        var totalCount = await query.CountAsync();
        var items = await query
            .Include(v => v.Tender)
            .Include(v => v.Bidders.Where(b => !b.IsDeleted))
            .OrderByDescending(v => v.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new CorePagedResult.PagedResult<TenderAwardVerification>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<TenderAwardVerification> CreateAsync(TenderAwardVerification verification)
    {
        await _dbSet.AddAsync(verification);
        return verification;
    }

    public new async Task<TenderAwardVerification> UpdateAsync(TenderAwardVerification verification)
    {
        verification.UpdatedAt = DateTime.UtcNow;
        _dbSet.Update(verification);
        return await Task.FromResult(verification);
    }

    public new async Task DeleteAsync(Guid id)
    {
        var verification = await _dbSet.FindAsync(id);
        if (verification != null)
        {
            verification.IsDeleted = true;
            verification.DeletedAt = DateTime.UtcNow;
        }
    }
}

#endregion

#region Verification Bidder Repository

public class TenderAwardVerificationBidderRepository : GenericRepository<TenderAwardVerificationBidder>, ITenderAwardVerificationBidderRepository
{
    public TenderAwardVerificationBidderRepository(ApplicationDbContext context) : base(context) { }

    public new async Task<TenderAwardVerificationBidder?> GetByIdAsync(Guid id)
    {
        return await _dbSet.Where(b => b.Id == id && !b.IsDeleted).FirstOrDefaultAsync();
    }

    public async Task<TenderAwardVerificationBidder?> GetByIdWithItemsAsync(Guid id)
    {
        return await _dbSet
            .Where(b => b.Id == id && !b.IsDeleted)
            .Include(b => b.TenderBid)
            .Include(b => b.BusinessPartner)
            .Include(b => b.ItemResults.Where(r => !r.IsDeleted))
                .ThenInclude(r => r.ChecklistItem)
            .Include(b => b.ItemResults.Where(r => !r.IsDeleted))
                .ThenInclude(r => r.Documents.Where(d => !d.IsDeleted))
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<TenderAwardVerificationBidder>> GetByVerificationIdAsync(Guid verificationId)
    {
        return await _dbSet
            .Where(b => b.VerificationId == verificationId && !b.IsDeleted)
            .Include(b => b.TenderBid)
            .Include(b => b.BusinessPartner)
            .Include(b => b.ItemResults.Where(r => !r.IsDeleted))
            .ToListAsync();
    }

    public async Task<TenderAwardVerificationBidder> CreateAsync(TenderAwardVerificationBidder bidder)
    {
        await _dbSet.AddAsync(bidder);
        return bidder;
    }

    public new async Task<TenderAwardVerificationBidder> UpdateAsync(TenderAwardVerificationBidder bidder)
    {
        bidder.UpdatedAt = DateTime.UtcNow;
        _dbSet.Update(bidder);
        return await Task.FromResult(bidder);
    }

    public new async Task DeleteAsync(Guid id)
    {
        var bidder = await _dbSet.FindAsync(id);
        if (bidder != null)
        {
            bidder.IsDeleted = true;
            bidder.DeletedAt = DateTime.UtcNow;
        }
    }
}

#endregion

#region Verification Item Result Repository

public class TenderAwardVerificationItemResultRepository : GenericRepository<TenderAwardVerificationItemResult>, ITenderAwardVerificationItemResultRepository
{
    public TenderAwardVerificationItemResultRepository(ApplicationDbContext context) : base(context) { }

    public new async Task<TenderAwardVerificationItemResult?> GetByIdAsync(Guid id)
    {
        return await _dbSet.Where(r => r.Id == id && !r.IsDeleted).FirstOrDefaultAsync();
    }

    public async Task<TenderAwardVerificationItemResult?> GetByIdWithDocumentsAsync(Guid id)
    {
        return await _dbSet
            .Where(r => r.Id == id && !r.IsDeleted)
            .Include(r => r.ChecklistItem)
            .Include(r => r.Documents.Where(d => !d.IsDeleted))
            .FirstOrDefaultAsync();
    }

    public async Task<TenderAwardVerificationItemResult?> GetByBidderAndItemAsync(Guid bidderId, Guid checklistItemId)
    {
        return await _dbSet
            .Where(r => r.BidderId == bidderId && r.ChecklistItemId == checklistItemId && !r.IsDeleted)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<TenderAwardVerificationItemResult>> GetByBidderIdAsync(Guid bidderId)
    {
        return await _dbSet
            .Where(r => r.BidderId == bidderId && !r.IsDeleted)
            .Include(r => r.ChecklistItem)
            .ToListAsync();
    }

    public async Task<IEnumerable<TenderAwardVerificationItemResult>> GetByBidderIdWithDocumentsAsync(Guid bidderId)
    {
        return await _dbSet
            .Where(r => r.BidderId == bidderId && !r.IsDeleted)
            .Include(r => r.ChecklistItem)
            .Include(r => r.Documents.Where(d => !d.IsDeleted))
            .ToListAsync();
    }

    public async Task<TenderAwardVerificationItemResult> CreateAsync(TenderAwardVerificationItemResult result)
    {
        await _dbSet.AddAsync(result);
        return result;
    }

    public new async Task<TenderAwardVerificationItemResult> UpdateAsync(TenderAwardVerificationItemResult result)
    {
        result.UpdatedAt = DateTime.UtcNow;
        _dbSet.Update(result);
        return await Task.FromResult(result);
    }

    public new async Task DeleteAsync(Guid id)
    {
        var result = await _dbSet.FindAsync(id);
        if (result != null)
        {
            result.IsDeleted = true;
            result.DeletedAt = DateTime.UtcNow;
        }
    }

    public async Task CreateRangeAsync(IEnumerable<TenderAwardVerificationItemResult> results)
    {
        await _dbSet.AddRangeAsync(results);
    }
}

#endregion

#region Verification Item Document Repository

public class TenderAwardVerificationItemDocumentRepository : GenericRepository<TenderAwardVerificationItemDocument>, ITenderAwardVerificationItemDocumentRepository
{
    public TenderAwardVerificationItemDocumentRepository(ApplicationDbContext context) : base(context) { }

    public new async Task<TenderAwardVerificationItemDocument?> GetByIdAsync(Guid id)
    {
        return await _dbSet.Where(d => d.Id == id && !d.IsDeleted).FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<TenderAwardVerificationItemDocument>> GetByItemResultIdAsync(Guid itemResultId)
    {
        return await _dbSet
            .Where(d => d.ItemResultId == itemResultId && !d.IsDeleted)
            .OrderByDescending(d => d.UploadedDate)
            .ToListAsync();
    }

    public async Task<TenderAwardVerificationItemDocument> CreateAsync(TenderAwardVerificationItemDocument document)
    {
        await _dbSet.AddAsync(document);
        return document;
    }

    public new async Task DeleteAsync(Guid id)
    {
        var document = await _dbSet.FindAsync(id);
        if (document != null)
        {
            document.IsDeleted = true;
            document.DeletedAt = DateTime.UtcNow;
        }
    }
}

#endregion
