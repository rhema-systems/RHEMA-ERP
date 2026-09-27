using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.Procurement;

#region Tender Repository

public class TenderRepository : GenericRepository<Tender>, ITenderRepository
{
    public TenderRepository(ApplicationDbContext context) : base(context) { }

    public override async Task<Tender?> GetByIdAsync(Guid id)
    {
        return await _dbSet
            .Where(t => t.Id == id && !t.IsDeleted)
            .Include(t => t.Lots)
                .ThenInclude(l => l.Items.Where(i => !i.IsDeleted))
            .Include(t => t.Items)
            .Include(t => t.Documents)
            .Include(t => t.EvaluationTemplate)
            .FirstOrDefaultAsync();
    }

    public async Task<Tender?> GetWithAllRelatedDataAsync(Guid id)
    {
        return await _dbSet
            .Where(t => t.Id == id && !t.IsDeleted)
            .Include(t => t.Lots)
                .ThenInclude(l => l.Items.Where(i => !i.IsDeleted))
            .Include(t => t.Items)
            .Include(t => t.Documents)
            .Include(t => t.Invitations)
                .ThenInclude(i => i.BusinessPartner)
            .Include(t => t.Bids)
                .ThenInclude(b => b.BusinessPartner)
            .Include(t => t.Fees)
            .Include(t => t.Evaluators)
            .Include(t => t.Clarifications)
            .Include(t => t.Revisions)
            .Include(t => t.Awards)
            .Include(t => t.CreatedBy)
            .Include(t => t.PublishedBy)
            .Include(t => t.EvaluationTemplate)
            .FirstOrDefaultAsync();
    }

    public async Task<Tender?> GetByTenderNumberAsync(string tenderNumber)
    {
        return await _dbSet
            .Where(t => t.TenderNumber == tenderNumber && !t.IsDeleted)
            .Include(t => t.Lots)
                .ThenInclude(l => l.Items.Where(i => !i.IsDeleted))
            .Include(t => t.Items)
            .Include(t => t.Documents)
            .FirstOrDefaultAsync();
    }

    public async Task<ErpSystem.Core.DTOs.Common.PagedResult<Tender>> GetTendersAsync(int page, int pageSize, string? search = null, string? status = null, string? tenderType = null)
    {
        var query = _dbSet.Where(t => !t.IsDeleted);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(t => t.TenderNumber.Contains(search) || t.Title.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(t => t.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(tenderType))
        {
            query = query.Where(t => t.TenderType == tenderType);
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .Include(t => t.Items)
            .Include(t => t.Bids)
            .Include(t => t.Invitations)
            .Include(t => t.CreatedBy)
            .Include(t => t.SourcingCase)
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new ErpSystem.Core.DTOs.Common.PagedResult<Tender>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<IEnumerable<Tender>> GetPublishedTendersAsync()
    {
        return await _dbSet
            .Where(t => !t.IsDeleted && t.Status == "Published" && t.PublishDate <= DateTime.UtcNow)
            .Include(t => t.Items)
            .Include(t => t.Documents.Where(d => d.IsPublic))
            .Include(t => t.SourcingCase)
            .OrderByDescending(t => t.PublishDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<Tender>> GetActiveTendersAsync()
    {
        var now = DateTime.UtcNow;
        return await _dbSet
            .Where(t => !t.IsDeleted &&
                       t.Status == "Published" &&
                       t.PublishDate <= now &&
                       t.SubmissionDeadline > now)
            .Include(t => t.Items)
            .Include(t => t.SourcingCase)
            .OrderBy(t => t.SubmissionDeadline)
            .ToListAsync();
    }

    public async Task<IEnumerable<Tender>> GetTendersByStatusAsync(string status)
    {
        return await _dbSet
            .Where(t => !t.IsDeleted && t.Status == status)
            .Include(t => t.Items)
            .Include(t => t.SourcingCase)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<Tender> CreateAsync(Tender tender)
    {
        await _dbSet.AddAsync(tender);
        return tender;
    }

    public new async Task<Tender> UpdateAsync(Tender tender)
    {
        _dbSet.Update(tender);
        return await Task.FromResult(tender);
    }

    public override async Task DeleteAsync(Guid id)
    {
        var tender = await _dbSet.FindAsync(id);
        if (tender != null)
        {
            tender.IsDeleted = true;
            _dbSet.Update(tender);
        }
    }

    public async Task<string> GenerateTenderNumberAsync()
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"TND-{year}-";
        
        var lastTender = await _dbSet
            .Where(t => t.TenderNumber.StartsWith(prefix))
            .OrderByDescending(t => t.TenderNumber)
            .FirstOrDefaultAsync();

        if (lastTender == null)
        {
            return $"{prefix}0001";
        }

        var lastNumber = int.Parse(lastTender.TenderNumber.Substring(prefix.Length));
        return $"{prefix}{(lastNumber + 1):D4}";
    }

    public async Task<int> GetTotalViewsAsync(Guid tenderId)
    {
        return await _context.TenderViewLogs
            .Where(v => v.TenderId == tenderId)
            .CountAsync();
    }

    public async Task<int> GetTotalDownloadsAsync(Guid tenderId)
    {
        return await _context.TenderViewLogs
            .Where(v => v.TenderId == tenderId && v.ActionType == "Download")
            .CountAsync();
    }
}

#endregion

#region Tender Item Repository

public class TenderItemRepository : GenericRepository<TenderItem>, ITenderItemRepository
{
    public TenderItemRepository(ApplicationDbContext context) : base(context) { }

    public override async Task<TenderItem?> GetByIdAsync(Guid id)
    {
        return await _dbSet
            .Where(i => i.Id == id && !i.IsDeleted)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<TenderItem>> GetByTenderIdAsync(Guid tenderId)
    {
        return await _dbSet
            .Where(i => i.TenderId == tenderId && !i.IsDeleted)
            .OrderBy(i => i.LineNumber)
            .ToListAsync();
    }

    public async Task<TenderItem> CreateAsync(TenderItem item)
    {
        await _dbSet.AddAsync(item);
        return item;
    }

    public new async Task<TenderItem> UpdateAsync(TenderItem item)
    {
        _dbSet.Update(item);
        return await Task.FromResult(item);
    }

    public override async Task DeleteAsync(Guid id)
    {
        var item = await _dbSet.FindAsync(id);
        if (item != null)
        {
            item.IsDeleted = true;
            _dbSet.Update(item);
        }
    }

    public async Task DeleteByTenderIdAsync(Guid tenderId)
    {
        var items = await _dbSet.Where(i => i.TenderId == tenderId).ToListAsync();
        foreach (var item in items)
        {
            item.IsDeleted = true;
        }
        _dbSet.UpdateRange(items);
    }
}

#endregion

#region Tender Document Repository

public class TenderDocumentRepository : GenericRepository<TenderDocument>, ITenderDocumentRepository
{
    public TenderDocumentRepository(ApplicationDbContext context) : base(context) { }

    public override async Task<TenderDocument?> GetByIdAsync(Guid id)
    {
        return await _dbSet
            .Where(d => d.Id == id && !d.IsDeleted)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<TenderDocument>> GetByTenderIdAsync(Guid tenderId)
    {
        return await _dbSet
            .Where(d => d.TenderId == tenderId && !d.IsDeleted)
            .OrderBy(d => d.DocumentType)
            .ThenBy(d => d.UploadedDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TenderDocument>> GetPublicDocumentsByTenderIdAsync(Guid tenderId)
    {
        return await _dbSet
            .Where(d => d.TenderId == tenderId && !d.IsDeleted && d.IsPublic)
            .OrderBy(d => d.DocumentType)
            .ThenBy(d => d.UploadedDate)
            .ToListAsync();
    }

    public async Task<TenderDocument> CreateAsync(TenderDocument document)
    {
        await _dbSet.AddAsync(document);
        return document;
    }

    public new async Task<TenderDocument> UpdateAsync(TenderDocument document)
    {
        _dbSet.Update(document);
        return await Task.FromResult(document);
    }

    public override async Task DeleteAsync(Guid id)
    {
        var document = await _dbSet.FindAsync(id);
        if (document != null)
        {
            document.IsDeleted = true;
            _dbSet.Update(document);
        }
    }
}

#endregion

#region Tender Invitation Repository

public class TenderInvitationRepository : GenericRepository<TenderInvitation>, ITenderInvitationRepository
{
    public TenderInvitationRepository(ApplicationDbContext context) : base(context) { }

    public override async Task<TenderInvitation?> GetByIdAsync(Guid id)
    {
        return await _dbSet
            .Where(i => i.Id == id && !i.IsDeleted)
            .Include(i => i.BusinessPartner)
            .FirstOrDefaultAsync();
    }

    public async Task<TenderInvitation?> GetByTenderAndPartnerAsync(Guid tenderId, Guid businessPartnerId)
    {
        return await _dbSet
            .Where(i => i.TenderId == tenderId && i.BusinessPartnerId == businessPartnerId && !i.IsDeleted)
            .Include(i => i.BusinessPartner)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<TenderInvitation>> GetByTenderIdAsync(Guid tenderId)
    {
        return await _dbSet
            .Where(i => i.TenderId == tenderId && !i.IsDeleted)
            .Include(i => i.BusinessPartner)
            .OrderBy(i => i.InvitedDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TenderInvitation>> GetByBusinessPartnerIdAsync(Guid businessPartnerId)
    {
        return await _dbSet
            .Where(i => i.BusinessPartnerId == businessPartnerId && !i.IsDeleted)
            .Include(i => i.Tender)
            .OrderByDescending(i => i.InvitedDate)
            .ToListAsync();
    }

    public async Task<TenderInvitation> CreateAsync(TenderInvitation invitation)
    {
        await _dbSet.AddAsync(invitation);
        return invitation;
    }

    public new async Task<TenderInvitation> UpdateAsync(TenderInvitation invitation)
    {
        _dbSet.Update(invitation);
        return await Task.FromResult(invitation);
    }

    public override async Task DeleteAsync(Guid id)
    {
        var invitation = await _dbSet.FindAsync(id);
        if (invitation != null)
        {
            invitation.IsDeleted = true;
            _dbSet.Update(invitation);
        }
    }

    public async Task<int> GetInvitationCountByTenderIdAsync(Guid tenderId)
    {
        return await _dbSet
            .Where(i => i.TenderId == tenderId && !i.IsDeleted)
            .CountAsync();
    }
}

#endregion

#region Tender Bid Repository

public class TenderBidRepository : GenericRepository<TenderBid>, ITenderBidRepository
{
    private readonly ICurrentUserProvider _currentUserProvider;

    public TenderBidRepository(ApplicationDbContext context, ICurrentUserProvider currentUserProvider) : base(context)
    {
        _currentUserProvider = currentUserProvider;
    }

    public override async Task<TenderBid?> GetByIdAsync(Guid id)
    {
        var query = _dbSet.Where(b => b.Id == id && !b.IsDeleted);

        // Note: No user filtering here because access control is handled at the controller level
        // The controller verifies the user has access to the business partner (either as main owner or sub-user)

        return await query
            .Include(b => b.BusinessPartner)
            .Include(b => b.Items)
            .Include(b => b.BidLots)
                .ThenInclude(bl => bl.Lot)
            .Include(b => b.BidLots)
                .ThenInclude(bl => bl.Items)
                    .ThenInclude(item => item.TenderItem)
            .FirstOrDefaultAsync();
    }

    public async Task<TenderBid?> GetWithAllRelatedDataAsync(Guid id)
    {
        var query = _dbSet.Where(b => b.Id == id && !b.IsDeleted);

        // Note: No user filtering here because access control is handled at the controller level
        // The controller verifies the user has access to the business partner (either as main owner or sub-user)

        return await query
            .Include(b => b.Tender)
            .Include(b => b.BusinessPartner)
            .Include(b => b.Items)
            .Include(b => b.BidLots)
                .ThenInclude(bl => bl.Lot)
            .Include(b => b.BidLots)
                .ThenInclude(bl => bl.Items)
                    .ThenInclude(item => item.TenderItem)
            .Include(b => b.Documents)
            .Include(b => b.Evaluations)
                .ThenInclude(e => e.TenderEvaluator)
                    .ThenInclude(te => te.User)
            .Include(b => b.Interviews)
            .FirstOrDefaultAsync();
    }

    public async Task<TenderBid?> GetByBidNumberAsync(string bidNumber)
    {
        return await _dbSet
            .Where(b => b.BidNumber == bidNumber && !b.IsDeleted)
            .Include(b => b.BusinessPartner)
            .Include(b => b.Items)
            .Include(b => b.BidLots)
                .ThenInclude(bl => bl.Lot)
            .Include(b => b.BidLots)
                .ThenInclude(bl => bl.Items)
                    .ThenInclude(item => item.TenderItem)
            .FirstOrDefaultAsync();
    }

    public async Task<TenderBid?> GetByTenderAndPartnerAsync(Guid tenderId, Guid businessPartnerId)
    {
        return await _dbSet
            .Where(b => b.TenderId == tenderId && b.BusinessPartnerId == businessPartnerId && !b.IsDeleted)
            .Include(b => b.Items)
            .Include(b => b.BidLots)
                .ThenInclude(bl => bl.Lot)
            .Include(b => b.BidLots)
                .ThenInclude(bl => bl.Items)
                    .ThenInclude(item => item.TenderItem)
            .Include(b => b.Documents)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<TenderBid>> GetByTenderIdAsync(Guid tenderId)
    {
        var query = _dbSet.Where(b => b.TenderId == tenderId && !b.IsDeleted);

        // Note: No user filtering here because this method is used to get all bids for a tender
        // Access control should be handled at the service/controller level based on user permissions

        return await query
            .Include(b => b.BusinessPartner)
            .Include(b => b.Items)
            .OrderBy(b => b.SubmittedDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TenderBid>> GetByBusinessPartnerIdAsync(Guid businessPartnerId)
    {
        var query = _dbSet.Where(b => b.BusinessPartnerId == businessPartnerId && !b.IsDeleted);

        // Note: No additional user filtering here because the businessPartnerId is already validated
        // by the controller to ensure the user has access to this business partner
        // (either as main account owner or as sub-user via BusinessPartnerUser table)

        return await query
            .Include(b => b.BusinessPartner)
            .Include(b => b.Tender)
            .Include(b => b.Items)
            .OrderByDescending(b => b.SubmittedDate)
            .ToListAsync();
    }

    public async Task<ErpSystem.Core.DTOs.Common.PagedResult<TenderBid>> GetBidsAsync(int page, int pageSize, string? search = null, string? status = null, Guid? tenderId = null)
    {
        var query = _dbSet.Where(b => !b.IsDeleted);

        // External users can only see their own bids
        if (_currentUserProvider.IsExternalUser)
        {
            query = query.Where(b => b.BusinessPartner != null && b.BusinessPartner.UserId == _currentUserProvider.UserId);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(b => b.BidNumber.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(b => b.Status == status);
        }

        if (tenderId.HasValue)
        {
            query = query.Where(b => b.TenderId == tenderId.Value);
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .Include(b => b.BusinessPartner)
            .Include(b => b.Tender)
            .OrderByDescending(b => b.SubmittedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new ErpSystem.Core.DTOs.Common.PagedResult<TenderBid>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<TenderBid> CreateAsync(TenderBid bid)
    {
        await _dbSet.AddAsync(bid);
        return bid;
    }

    public new Task<TenderBid> UpdateAsync(TenderBid bid)
    {
        // Update only the aggregate root. DbSet.Update traverses the complete bid
        // graph and changes a newly-added bid lot with a client-generated Guid to
        // Modified. The subsequent bid-item insert then points at a lot row that
        // was never inserted and SQL Server rejects it through
        // FK_TenderBidItems_TenderBidLots_BidLotId.
        var entry = _context.Entry(bid);
        if (entry.State != EntityState.Added)
        {
            entry.State = EntityState.Modified;
        }

        return Task.FromResult(bid);
    }

    public override async Task DeleteAsync(Guid id)
    {
        var bid = await _dbSet.FindAsync(id);
        if (bid != null)
        {
            bid.IsDeleted = true;
            _dbSet.Update(bid);
        }
    }

    public async Task<string> GenerateBidNumberAsync()
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"BID-{year}-";

        var lastBid = await _dbSet
            .Where(b => b.BidNumber.StartsWith(prefix))
            .OrderByDescending(b => b.BidNumber)
            .FirstOrDefaultAsync();

        if (lastBid == null)
        {
            return $"{prefix}0001";
        }

        var lastNumber = int.Parse(lastBid.BidNumber.Substring(prefix.Length));
        return $"{prefix}{(lastNumber + 1):D4}";
    }

    public async Task<int> GetBidCountByTenderIdAsync(Guid tenderId)
    {
        return await _dbSet
            .Where(b => b.TenderId == tenderId && !b.IsDeleted)
            .CountAsync();
    }

    public async Task UpdateBidRankingsAsync(Guid tenderId)
    {
        var bids = await _dbSet
            .Where(b => b.TenderId == tenderId && !b.IsDeleted && b.TotalScore.HasValue)
            .OrderByDescending(b => b.TotalScore)
            .ToListAsync();

        int rank = 1;
        foreach (var bid in bids)
        {
            bid.Rank = rank++;
            _dbSet.Update(bid);
        }
    }
}

#endregion

#region Tender Bid Item Repository

public class TenderBidItemRepository : GenericRepository<TenderBidItem>, ITenderBidItemRepository
{
    public TenderBidItemRepository(ApplicationDbContext context) : base(context) { }

    public override async Task<TenderBidItem?> GetByIdAsync(Guid id)
    {
        return await _dbSet
            .Where(i => i.Id == id && !i.IsDeleted)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<TenderBidItem>> GetByBidIdAsync(Guid bidId)
    {
        return await _dbSet
            .Where(i => i.TenderBidId == bidId && !i.IsDeleted)
            .Include(i => i.TenderItem)
            .OrderBy(i => i.TenderItemId)
            .ToListAsync();
    }

    public async Task<TenderBidItem> CreateAsync(TenderBidItem item)
    {
        await _dbSet.AddAsync(item);
        return item;
    }

    public new async Task<TenderBidItem> UpdateAsync(TenderBidItem item)
    {
        _dbSet.Update(item);
        return await Task.FromResult(item);
    }

    public override async Task DeleteAsync(Guid id)
    {
        var item = await _dbSet.FindAsync(id);
        if (item != null)
        {
            item.IsDeleted = true;
            _dbSet.Update(item);
        }
    }

    public async Task DeleteByBidIdAsync(Guid bidId)
    {
        var items = await _dbSet.Where(i => i.TenderBidId == bidId).ToListAsync();
        foreach (var item in items)
        {
            item.IsDeleted = true;
        }
        _dbSet.UpdateRange(items);
    }
}

#endregion

#region Tender Bid Document Repository

public class TenderBidDocumentRepository : GenericRepository<TenderBidDocument>, ITenderBidDocumentRepository
{
    public TenderBidDocumentRepository(ApplicationDbContext context) : base(context) { }

    public override async Task<TenderBidDocument?> GetByIdAsync(Guid id)
    {
        return await _dbSet
            .Where(d => d.Id == id && !d.IsDeleted)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<TenderBidDocument>> GetByBidIdAsync(Guid bidId)
    {
        return await _dbSet
            .Where(d => d.TenderBidId == bidId && !d.IsDeleted)
            .Include(d => d.TenderBidItem).ThenInclude(item => item!.TenderItem).ThenInclude(item => item.Lot)
            .OrderBy(d => d.DocumentType)
            .ThenBy(d => d.UploadedDate)
            .ToListAsync();
    }

    public async Task<TenderBidDocument> CreateAsync(TenderBidDocument document)
    {
        await _dbSet.AddAsync(document);
        return document;
    }

    public new async Task<TenderBidDocument> UpdateAsync(TenderBidDocument document)
    {
        _dbSet.Update(document);
        return await Task.FromResult(document);
    }

    public override async Task DeleteAsync(Guid id)
    {
        var document = await _dbSet.FindAsync(id);
        if (document != null)
        {
            document.IsDeleted = true;
            _dbSet.Update(document);
        }
    }
}

#endregion

#region Tender Fee Repository

public class TenderFeeRepository : GenericRepository<TenderFee>, ITenderFeeRepository
{
    public TenderFeeRepository(ApplicationDbContext context) : base(context) { }

    public override async Task<TenderFee?> GetByIdAsync(Guid id)
    {
        return await _dbSet
            .Where(f => f.Id == id && !f.IsDeleted)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<TenderFee>> GetByTenderIdAsync(Guid tenderId)
    {
        return await _dbSet
            .Where(f => f.TenderId == tenderId && !f.IsDeleted)
            .OrderBy(f => f.FeeType)
            .ToListAsync();
    }

    public async Task<TenderFee> CreateAsync(TenderFee fee)
    {
        await _dbSet.AddAsync(fee);
        return fee;
    }

    public new async Task<TenderFee> UpdateAsync(TenderFee fee)
    {
        _dbSet.Update(fee);
        return await Task.FromResult(fee);
    }

    public override async Task DeleteAsync(Guid id)
    {
        var fee = await _dbSet.FindAsync(id);
        if (fee != null)
        {
            fee.IsDeleted = true;
            _dbSet.Update(fee);
        }
    }
}

#endregion

#region Tender Payment Repository

public class TenderPaymentRepository : GenericRepository<TenderPayment>, ITenderPaymentRepository
{
    public TenderPaymentRepository(ApplicationDbContext context) : base(context) { }

    public override async Task<TenderPayment?> GetByIdAsync(Guid id)
    {
        return await _dbSet
            .Where(p => p.Id == id && !p.IsDeleted)
            .Include(p => p.TenderFee)
                .ThenInclude(f => f.Tender)
            .FirstOrDefaultAsync();
    }

    public async Task<TenderPayment?> GetByReferenceAsync(string paymentReference)
    {
        return await _dbSet
            .Where(p => p.PaymentReference == paymentReference && !p.IsDeleted)
            .Include(p => p.TenderFee)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<TenderPayment>> GetByFeeIdAsync(Guid feeId)
    {
        return await _dbSet
            .Where(p => p.TenderFeeId == feeId && !p.IsDeleted)
            .OrderByDescending(p => p.PaymentDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TenderPayment>> GetByBusinessPartnerIdAsync(Guid businessPartnerId)
    {
        return await _dbSet
            .Where(p => p.BusinessPartnerId == businessPartnerId && !p.IsDeleted)
            .Include(p => p.TenderFee)
                .ThenInclude(f => f.Tender)
            .OrderByDescending(p => p.PaymentDate)
            .ToListAsync();
    }

    public async Task<TenderPayment> CreateAsync(TenderPayment payment)
    {
        await _dbSet.AddAsync(payment);
        return payment;
    }

    public new async Task<TenderPayment> UpdateAsync(TenderPayment payment)
    {
        _dbSet.Update(payment);
        return await Task.FromResult(payment);
    }

    public override async Task DeleteAsync(Guid id)
    {
        var payment = await _dbSet.FindAsync(id);
        if (payment != null)
        {
            payment.IsDeleted = true;
            _dbSet.Update(payment);
        }
    }

    public async Task<string> GeneratePaymentReferenceAsync()
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"PAY-{year}-";

        var lastPayment = await _dbSet
            .Where(p => p.PaymentReference.StartsWith(prefix))
            .OrderByDescending(p => p.PaymentReference)
            .FirstOrDefaultAsync();

        if (lastPayment == null)
        {
            return $"{prefix}0001";
        }

        var lastNumber = int.Parse(lastPayment.PaymentReference.Substring(prefix.Length));
        return $"{prefix}{(lastNumber + 1):D4}";
    }
}

#endregion

#region Tender Evaluator Repository

public class TenderEvaluatorRepository : GenericRepository<TenderEvaluator>, ITenderEvaluatorRepository
{
    public TenderEvaluatorRepository(ApplicationDbContext context) : base(context) { }

    public override async Task<TenderEvaluator?> GetByIdAsync(Guid id)
    {
        return await _dbSet
            .Where(e => e.Id == id && !e.IsDeleted)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<TenderEvaluator>> GetByTenderIdAsync(Guid tenderId)
    {
        return await _dbSet
            .Where(e => e.TenderId == tenderId && !e.IsDeleted)
            .OrderBy(e => e.AssignedDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TenderEvaluator>> GetByUserIdAsync(Guid userId)
    {
        return await _dbSet
            .Where(e => e.UserId == userId && !e.IsDeleted)
            .Include(e => e.Tender)
            .OrderByDescending(e => e.AssignedDate)
            .ToListAsync();
    }

    public async Task<TenderEvaluator?> GetByTenderAndUserAsync(Guid tenderId, Guid userId)
    {
        return await _dbSet
            .Where(e => e.TenderId == tenderId && e.UserId == userId && !e.IsDeleted)
            .FirstOrDefaultAsync();
    }

    public async Task<TenderEvaluator> CreateAsync(TenderEvaluator evaluator)
    {
        await _dbSet.AddAsync(evaluator);
        return evaluator;
    }

    public new async Task<TenderEvaluator> UpdateAsync(TenderEvaluator evaluator)
    {
        _dbSet.Update(evaluator);
        return await Task.FromResult(evaluator);
    }

    public override async Task DeleteAsync(Guid id)
    {
        var evaluator = await _dbSet.FindAsync(id);
        if (evaluator != null)
        {
            evaluator.IsDeleted = true;
            _dbSet.Update(evaluator);
        }
    }
}

#endregion

#region Tender Evaluation Repository

public class TenderEvaluationRepository : GenericRepository<TenderEvaluation>, ITenderEvaluationRepository
{
    public TenderEvaluationRepository(ApplicationDbContext context) : base(context) { }

    public override async Task<TenderEvaluation?> GetByIdAsync(Guid id)
    {
        return await _dbSet
            .Where(e => e.Id == id && !e.IsDeleted)
            .Include(e => e.TenderEvaluator)
                .ThenInclude(te => te.User)
            .Include(e => e.TenderBid)
                .ThenInclude(b => b.BusinessPartner)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<TenderEvaluation>> GetByBidIdAsync(Guid bidId)
    {
        return await _dbSet
            .Where(e => e.TenderBidId == bidId && !e.IsDeleted)
            .Include(e => e.TenderEvaluator)
                .ThenInclude(te => te.User)
            .OrderBy(e => e.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<TenderEvaluation>> GetByEvaluatorIdAsync(Guid evaluatorId)
    {
        return await _dbSet
            .Where(e => e.TenderEvaluatorId == evaluatorId && !e.IsDeleted)
            .Include(e => e.TenderBid)
                .ThenInclude(b => b.BusinessPartner)
            .Include(e => e.TenderBid)
                .ThenInclude(b => b.Tender)
            .Include(e => e.TenderEvaluator)
                .ThenInclude(ev => ev.User)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync();
    }

    public async Task<TenderEvaluation?> GetByBidAndEvaluatorAsync(Guid bidId, Guid evaluatorId)
    {
        return await _dbSet
            .Where(e => e.TenderBidId == bidId && e.TenderEvaluatorId == evaluatorId && !e.IsDeleted)
            .FirstOrDefaultAsync();
    }

    public async Task<TenderEvaluation> CreateAsync(TenderEvaluation evaluation)
    {
        await _dbSet.AddAsync(evaluation);
        return evaluation;
    }

    public new async Task<TenderEvaluation> UpdateAsync(TenderEvaluation evaluation)
    {
        _dbSet.Update(evaluation);
        return await Task.FromResult(evaluation);
    }

    public override async Task DeleteAsync(Guid id)
    {
        var evaluation = await _dbSet.FindAsync(id);
        if (evaluation != null)
        {
            evaluation.IsDeleted = true;
            _dbSet.Update(evaluation);
        }
    }

    public async Task<decimal?> GetAverageScoreByBidIdAsync(Guid bidId)
    {
        var evaluations = await _dbSet
            .Where(e => e.TenderBidId == bidId && !e.IsDeleted && e.TotalScore.HasValue)
            .ToListAsync();

        if (!evaluations.Any())
        {
            return null;
        }

        return evaluations.Average(e => e.TotalScore!.Value);
    }

    public async Task<int> CountByEvaluatorIdAsync(Guid evaluatorId)
    {
        return await _dbSet
            .Where(e => e.TenderEvaluatorId == evaluatorId && !e.IsDeleted)
            .CountAsync();
    }
}

#endregion

#region Tender Interview Repository

public class TenderInterviewRepository : GenericRepository<TenderInterview>, ITenderInterviewRepository
{
    public TenderInterviewRepository(ApplicationDbContext context) : base(context) { }

    public override async Task<TenderInterview?> GetByIdAsync(Guid id)
    {
        return await _dbSet
            .Where(i => i.Id == id && !i.IsDeleted)
            .Include(i => i.Tender)
            .Include(i => i.TenderBid)
                .ThenInclude(b => b.BusinessPartner)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<TenderInterview>> GetByTenderIdAsync(Guid tenderId)
    {
        return await _dbSet
            .Where(i => i.TenderId == tenderId && !i.IsDeleted)
            .Include(i => i.TenderBid)
                .ThenInclude(b => b.BusinessPartner)
            .OrderBy(i => i.ScheduledDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TenderInterview>> GetByBidIdAsync(Guid bidId)
    {
        return await _dbSet
            .Where(i => i.TenderBidId == bidId && !i.IsDeleted)
            .OrderBy(i => i.ScheduledDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TenderInterview>> GetUpcomingInterviewsAsync()
    {
        var now = DateTime.UtcNow;
        return await _dbSet
            .Where(i => !i.IsDeleted && i.ScheduledDate > now && i.Status != "Cancelled")
            .Include(i => i.Tender)
            .Include(i => i.TenderBid)
                .ThenInclude(b => b.BusinessPartner)
            .OrderBy(i => i.ScheduledDate)
            .ToListAsync();
    }

    public async Task<TenderInterview> CreateAsync(TenderInterview interview)
    {
        await _dbSet.AddAsync(interview);
        return interview;
    }

    public new async Task<TenderInterview> UpdateAsync(TenderInterview interview)
    {
        _dbSet.Update(interview);
        return await Task.FromResult(interview);
    }

    public override async Task DeleteAsync(Guid id)
    {
        var interview = await _dbSet.FindAsync(id);
        if (interview != null)
        {
            interview.IsDeleted = true;
            _dbSet.Update(interview);
        }
    }
}

#endregion

#region Tender Clarification Repository

public class TenderClarificationRepository : GenericRepository<TenderClarification>, ITenderClarificationRepository
{
    public TenderClarificationRepository(ApplicationDbContext context) : base(context) { }

    public override async Task<TenderClarification?> GetByIdAsync(Guid id)
    {
        return await _dbSet
            .Where(c => c.Id == id && !c.IsDeleted)
            .Include(c => c.BusinessPartner)
            .Include(c => c.QuestionBy)
            .Include(c => c.AnsweredBy)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<TenderClarification>> GetByTenderIdAsync(Guid tenderId)
    {
        return await _dbSet
            .Where(c => c.TenderId == tenderId && !c.IsDeleted)
            .Include(c => c.BusinessPartner)
            .Include(c => c.QuestionBy)
            .Include(c => c.AnsweredBy)
            .OrderBy(c => c.QuestionDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TenderClarification>> GetPublicClarificationsByTenderIdAsync(Guid tenderId)
    {
        return await _dbSet
            .Where(c => c.TenderId == tenderId && !c.IsDeleted && c.IsPublic && c.AnswerDate.HasValue)
            .OrderBy(c => c.QuestionDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TenderClarification>> GetByBusinessPartnerIdAsync(Guid businessPartnerId)
    {
        return await _dbSet
            .Where(c => c.BusinessPartnerId == businessPartnerId && !c.IsDeleted)
            .Include(c => c.Tender)
            .OrderByDescending(c => c.QuestionDate)
            .ToListAsync();
    }

    public async Task<TenderClarification> CreateAsync(TenderClarification clarification)
    {
        await _dbSet.AddAsync(clarification);
        return clarification;
    }

    public new async Task<TenderClarification> UpdateAsync(TenderClarification clarification)
    {
        _dbSet.Update(clarification);
        return await Task.FromResult(clarification);
    }

    public override async Task DeleteAsync(Guid id)
    {
        var clarification = await _dbSet.FindAsync(id);
        if (clarification != null)
        {
            clarification.IsDeleted = true;
            _dbSet.Update(clarification);
        }
    }
}

#endregion

#region Tender Revision Repository

public class TenderRevisionRepository : GenericRepository<TenderRevision>, ITenderRevisionRepository
{
    public TenderRevisionRepository(ApplicationDbContext context) : base(context) { }

    public override async Task<TenderRevision?> GetByIdAsync(Guid id)
    {
        return await _dbSet
            .Where(r => r.Id == id && !r.IsDeleted)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<TenderRevision>> GetByTenderIdAsync(Guid tenderId)
    {
        return await _dbSet
            .Where(r => r.TenderId == tenderId && !r.IsDeleted)
            .OrderBy(r => r.RevisionDate)
            .ToListAsync();
    }

    public async Task<TenderRevision> CreateAsync(TenderRevision revision)
    {
        await _dbSet.AddAsync(revision);
        return revision;
    }

    public new async Task<TenderRevision> UpdateAsync(TenderRevision revision)
    {
        _dbSet.Update(revision);
        return await Task.FromResult(revision);
    }

    public override async Task DeleteAsync(Guid id)
    {
        var revision = await _dbSet.FindAsync(id);
        if (revision != null)
        {
            revision.IsDeleted = true;
            _dbSet.Update(revision);
        }
    }

    public async Task<string> GenerateRevisionNumberAsync(Guid tenderId)
    {
        var revisionCount = await _dbSet
            .Where(r => r.TenderId == tenderId && !r.IsDeleted)
            .CountAsync();

        return $"REV-{(revisionCount + 1):D2}";
    }
}

#endregion

#region Tender Award Repository

public class TenderAwardRepository : GenericRepository<TenderAward>, ITenderAwardRepository
{
    public TenderAwardRepository(ApplicationDbContext context) : base(context) { }

    public override async Task<TenderAward?> GetByIdAsync(Guid id)
    {
        return await _dbSet
            .Where(a => a.Id == id && !a.IsDeleted)
            .Include(a => a.Tender)
            .Include(a => a.TenderBid)
                .ThenInclude(b => b.BusinessPartner)
            .Include(a => a.AwardedBy)
            .Include(a => a.Negotiation)
            .FirstOrDefaultAsync();
    }

    public async Task<TenderAward?> GetByTenderIdAsync(Guid tenderId)
    {
        return await _dbSet
            .Where(a => a.TenderId == tenderId && !a.IsDeleted)
            .Include(a => a.TenderBid)
                .ThenInclude(b => b.BusinessPartner)
            .Include(a => a.AwardedBy)
            .Include(a => a.Negotiation)
            .FirstOrDefaultAsync();
    }

    public async Task<TenderAward?> GetByBidIdAsync(Guid bidId)
    {
        return await _dbSet
            .Where(a => a.TenderBidId == bidId && !a.IsDeleted)
            .Include(a => a.Tender)
            .Include(a => a.AwardedBy)
            .Include(a => a.Negotiation)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<TenderAward>> GetAwardsAsync()
    {
        return await _dbSet
            .Where(a => !a.IsDeleted)
            .Include(a => a.Tender)
            .Include(a => a.TenderBid)
                .ThenInclude(b => b.BusinessPartner)
            .Include(a => a.Negotiation)
            .OrderByDescending(a => a.AwardDate)
            .ToListAsync();
    }

    public async Task<ErpSystem.Core.DTOs.Common.PagedResult<TenderAward>> GetAwardsPagedAsync(int page, int pageSize, string? search = null, string? status = null)
    {
        var query = _dbSet.Where(a => !a.IsDeleted);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(a => a.Tender.TenderNumber.Contains(search) || a.TenderBid.BidNumber.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(a => a.Status == status);
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .Include(a => a.Tender)
            .Include(a => a.TenderBid)
                .ThenInclude(b => b.BusinessPartner)
            .Include(a => a.Negotiation)
            .OrderByDescending(a => a.AwardDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new ErpSystem.Core.DTOs.Common.PagedResult<TenderAward>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<TenderAward> CreateAsync(TenderAward award)
    {
        await _dbSet.AddAsync(award);
        return award;
    }

    public new async Task<TenderAward> UpdateAsync(TenderAward award)
    {
        _dbSet.Update(award);
        return await Task.FromResult(award);
    }

    public override async Task DeleteAsync(Guid id)
    {
        var award = await _dbSet.FindAsync(id);
        if (award != null)
        {
            award.IsDeleted = true;
            _dbSet.Update(award);
        }
    }
}

#endregion

#region Tender Template Repository

public class TenderTemplateRepository : GenericRepository<TenderTemplate>, ITenderTemplateRepository
{
    public TenderTemplateRepository(ApplicationDbContext context) : base(context) { }

    public override async Task<TenderTemplate?> GetByIdAsync(Guid id)
    {
        return await _dbSet
            .Where(t => t.Id == id && !t.IsDeleted)
            .Include(t => t.CreatedBy)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<TenderTemplate>> GetActiveTemplatesAsync()
    {
        return await _dbSet
            .Where(t => !t.IsDeleted && t.IsActive)
            .OrderBy(t => t.TemplateName)
            .ToListAsync();
    }

    public async Task<IEnumerable<TenderTemplate>> GetByTenderTypeAsync(string tenderType)
    {
        return await _dbSet
            .Where(t => !t.IsDeleted && t.TenderType == tenderType)
            .OrderBy(t => t.TemplateName)
            .ToListAsync();
    }

    public async Task<IEnumerable<TenderTemplate>> GetByCategoryAsync(string category)
    {
        return await _dbSet
            .Where(t => !t.IsDeleted && t.Category == category)
            .OrderBy(t => t.TemplateName)
            .ToListAsync();
    }

    public async Task<TenderTemplate> CreateAsync(TenderTemplate template)
    {
        await _dbSet.AddAsync(template);
        return template;
    }

    public new async Task<TenderTemplate> UpdateAsync(TenderTemplate template)
    {
        _dbSet.Update(template);
        return await Task.FromResult(template);
    }

    public override async Task DeleteAsync(Guid id)
    {
        var template = await _dbSet.FindAsync(id);
        if (template != null)
        {
            template.IsDeleted = true;
            _dbSet.Update(template);
        }
    }
}

#endregion

#region Tender View Log Repository

public class TenderViewLogRepository : GenericRepository<TenderViewLog>, ITenderViewLogRepository
{
    public TenderViewLogRepository(ApplicationDbContext context) : base(context) { }

    public async Task<TenderViewLog> CreateAsync(TenderViewLog viewLog)
    {
        await _dbSet.AddAsync(viewLog);
        return viewLog;
    }

    public async Task<int> GetViewCountByTenderIdAsync(Guid tenderId)
    {
        return await _dbSet
            .Where(v => v.TenderId == tenderId)
            .CountAsync();
    }

    public async Task<int> GetDownloadCountByTenderIdAsync(Guid tenderId)
    {
        return await _dbSet
            .Where(v => v.TenderId == tenderId && v.ActionType == "Download")
            .CountAsync();
    }

    public async Task<IEnumerable<TenderViewLog>> GetByTenderIdAsync(Guid tenderId)
    {
        return await _dbSet
            .Where(v => v.TenderId == tenderId)
            .OrderByDescending(v => v.ActionDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TenderViewLog>> GetByBusinessPartnerIdAsync(Guid businessPartnerId)
    {
        return await _dbSet
            .Where(v => v.BusinessPartnerId == businessPartnerId)
            .Include(v => v.Tender)
            .OrderByDescending(v => v.ActionDate)
            .ToListAsync();
    }
}

#endregion

#region Evaluation Criterion Repository

public class EvaluationCriterionRepository : GenericRepository<EvaluationCriterion>, IEvaluationCriterionRepository
{
    public EvaluationCriterionRepository(ApplicationDbContext context) : base(context) { }

    public override async Task<EvaluationCriterion?> GetByIdAsync(Guid id)
    {
        return await _dbSet
            .Where(c => c.Id == id && !c.IsDeleted)
            .Include(c => c.CreatedBy)
            .FirstOrDefaultAsync();
    }

    public async Task<EvaluationCriterion?> GetByCodeAsync(string code)
    {
        return await _dbSet
            .Where(c => c.CriterionCode == code && !c.IsDeleted)
            .FirstOrDefaultAsync();
    }

    public override async Task<IEnumerable<EvaluationCriterion>> GetAllAsync()
    {
        return await _dbSet
            .Where(c => !c.IsDeleted)
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.CriterionName)
            .ToListAsync();
    }

    public async Task<IEnumerable<EvaluationCriterion>> GetActiveAsync()
    {
        return await _dbSet
            .Where(c => !c.IsDeleted && c.IsActive)
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.CriterionName)
            .ToListAsync();
    }

    public async Task<IEnumerable<EvaluationCriterion>> GetByCategoryAsync(string category)
    {
        return await _dbSet
            .Where(c => !c.IsDeleted && c.Category == category)
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.CriterionName)
            .ToListAsync();
    }

    public async Task<IEnumerable<EvaluationCriterion>> GetByIdsAsync(IEnumerable<Guid> ids)
    {
        var idList = ids.ToList();
        return await _dbSet
            .Where(c => !c.IsDeleted && idList.Contains(c.Id))
            .ToListAsync();
    }

    public async Task<EvaluationCriterion> CreateAsync(EvaluationCriterion criterion)
    {
        await _dbSet.AddAsync(criterion);
        return criterion;
    }

    public new async Task<EvaluationCriterion> UpdateAsync(EvaluationCriterion criterion)
    {
        _dbSet.Update(criterion);
        return await Task.FromResult(criterion);
    }

    public override async Task DeleteAsync(Guid id)
    {
        var criterion = await _dbSet.FindAsync(id);
        if (criterion != null)
        {
            criterion.IsDeleted = true;
            _dbSet.Update(criterion);
        }
    }
}

#endregion

#region Tender Document Type Repository

public class TenderDocumentTypeRepository : GenericRepository<TenderDocumentType>, ITenderDocumentTypeRepository
{
    public TenderDocumentTypeRepository(ApplicationDbContext context) : base(context) { }

    public override async Task<TenderDocumentType?> GetByIdAsync(Guid id)
    {
        return await _dbSet
            .Where(d => d.Id == id && !d.IsDeleted)
            .Include(d => d.CreatedBy)
            .FirstOrDefaultAsync();
    }

    public async Task<TenderDocumentType?> GetByCodeAsync(string code)
    {
        return await _dbSet
            .Where(d => d.DocumentCode == code && !d.IsDeleted)
            .FirstOrDefaultAsync();
    }

    public override async Task<IEnumerable<TenderDocumentType>> GetAllAsync()
    {
        return await _dbSet
            .Where(d => !d.IsDeleted)
            .OrderBy(d => d.DisplayOrder)
            .ThenBy(d => d.DocumentName)
            .ToListAsync();
    }

    public async Task<IEnumerable<TenderDocumentType>> GetActiveAsync()
    {
        return await _dbSet
            .Where(d => !d.IsDeleted && d.IsActive)
            .OrderBy(d => d.DisplayOrder)
            .ThenBy(d => d.DocumentName)
            .ToListAsync();
    }

    public async Task<IEnumerable<TenderDocumentType>> GetByCategoryAsync(string category)
    {
        return await _dbSet
            .Where(d => !d.IsDeleted && d.Category == category)
            .OrderBy(d => d.DisplayOrder)
            .ThenBy(d => d.DocumentName)
            .ToListAsync();
    }

    public async Task<TenderDocumentType> CreateAsync(TenderDocumentType documentType)
    {
        await _dbSet.AddAsync(documentType);
        return documentType;
    }

    public new async Task<TenderDocumentType> UpdateAsync(TenderDocumentType documentType)
    {
        _dbSet.Update(documentType);
        return await Task.FromResult(documentType);
    }

    public override async Task DeleteAsync(Guid id)
    {
        var documentType = await _dbSet.FindAsync(id);
        if (documentType != null)
        {
            documentType.IsDeleted = true;
            _dbSet.Update(documentType);
        }
    }
}

public class EvaluationTemplateRepository : GenericRepository<EvaluationTemplate>, IEvaluationTemplateRepository
{
    public EvaluationTemplateRepository(ApplicationDbContext context) : base(context) { }

    public override async Task<EvaluationTemplate?> GetByIdAsync(Guid id)
    {
        return await _dbSet
            .Where(t => t.Id == id && !t.IsDeleted)
            .Include(t => t.CreatedBy)
            .FirstOrDefaultAsync();
    }

    public async Task<EvaluationTemplate?> GetByIdWithCriteriaAsync(Guid id)
    {
        return await _dbSet
            .Where(t => t.Id == id && !t.IsDeleted)
            .Include(t => t.CreatedBy)
            .Include(t => t.TemplateCriteria.Where(c => !c.IsDeleted))
                .ThenInclude(tc => tc.EvaluationCriterion)
            .FirstOrDefaultAsync();
    }

    public async Task<EvaluationTemplate?> GetByCodeAsync(string code)
    {
        return await _dbSet
            .Where(t => t.TemplateCode == code && !t.IsDeleted)
            .FirstOrDefaultAsync();
    }

    public override async Task<IEnumerable<EvaluationTemplate>> GetAllAsync()
    {
        return await _dbSet
            .Where(t => !t.IsDeleted)
            .Include(t => t.CreatedBy)
            .Include(t => t.TemplateCriteria.Where(c => !c.IsDeleted))
            .OrderBy(t => t.DisplayOrder)
            .ThenBy(t => t.TemplateName)
            .ToListAsync();
    }

    public async Task<IEnumerable<EvaluationTemplate>> GetActiveAsync()
    {
        return await _dbSet
            .Where(t => !t.IsDeleted && t.IsActive)
            .Include(t => t.CreatedBy)
            .Include(t => t.TemplateCriteria.Where(c => !c.IsDeleted))
            .OrderBy(t => t.DisplayOrder)
            .ThenBy(t => t.TemplateName)
            .ToListAsync();
    }

    public async Task<IEnumerable<EvaluationTemplate>> GetByCategoryAsync(string category)
    {
        return await _dbSet
            .Where(t => !t.IsDeleted && t.Category == category)
            .Include(t => t.CreatedBy)
            .Include(t => t.TemplateCriteria.Where(c => !c.IsDeleted))
            .OrderBy(t => t.DisplayOrder)
            .ThenBy(t => t.TemplateName)
            .ToListAsync();
    }

    public async Task<IEnumerable<EvaluationTemplate>> GetByTenderTypeAsync(string tenderType)
    {
        return await _dbSet
            .Where(t => !t.IsDeleted && t.TenderType == tenderType)
            .Include(t => t.CreatedBy)
            .Include(t => t.TemplateCriteria.Where(c => !c.IsDeleted))
            .OrderBy(t => t.DisplayOrder)
            .ThenBy(t => t.TemplateName)
            .ToListAsync();
    }

    public async Task<EvaluationTemplate?> GetDefaultAsync(string category, string tenderType)
    {
        return await _dbSet
            .Where(t => !t.IsDeleted && t.IsActive && t.IsDefault && t.Category == category && t.TenderType == tenderType)
            .Include(t => t.TemplateCriteria.Where(c => !c.IsDeleted))
                .ThenInclude(tc => tc.EvaluationCriterion)
            .FirstOrDefaultAsync();
    }

    public async Task<EvaluationTemplate> CreateAsync(EvaluationTemplate template)
    {
        await _dbSet.AddAsync(template);
        return template;
    }

    public new async Task<EvaluationTemplate> UpdateAsync(EvaluationTemplate template)
    {
        _dbSet.Update(template);
        return await Task.FromResult(template);
    }

    public override async Task DeleteAsync(Guid id)
    {
        var template = await _dbSet.FindAsync(id);
        if (template != null)
        {
            template.IsDeleted = true;
            _dbSet.Update(template);
        }
    }
}

public class EvaluationTemplateCriterionRepository : GenericRepository<EvaluationTemplateCriterion>, IEvaluationTemplateCriterionRepository
{
    public EvaluationTemplateCriterionRepository(ApplicationDbContext context) : base(context) { }

    public override async Task<EvaluationTemplateCriterion?> GetByIdAsync(Guid id)
    {
        return await _dbSet
            .Where(c => c.Id == id && !c.IsDeleted)
            .Include(c => c.EvaluationCriterion)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<EvaluationTemplateCriterion>> GetByTemplateIdAsync(Guid templateId)
    {
        return await _dbSet
            .Where(c => c.EvaluationTemplateId == templateId && !c.IsDeleted)
            .Include(c => c.EvaluationCriterion)
            .OrderBy(c => c.DisplayOrder)
            .ToListAsync();
    }

    public async Task<EvaluationTemplateCriterion> CreateAsync(EvaluationTemplateCriterion criterion)
    {
        await _dbSet.AddAsync(criterion);
        return criterion;
    }

    public new async Task<EvaluationTemplateCriterion> UpdateAsync(EvaluationTemplateCriterion criterion)
    {
        _dbSet.Update(criterion);
        return await Task.FromResult(criterion);
    }

    public override async Task DeleteAsync(Guid id)
    {
        var criterion = await _dbSet.FindAsync(id);
        if (criterion != null)
        {
            criterion.IsDeleted = true;
            _dbSet.Update(criterion);
        }
    }

    public async Task DeleteByTemplateIdAsync(Guid templateId)
    {
        // Use HardDeleteRangeAsync to bypass soft delete interceptor
        // This is necessary to avoid unique constraint violations when re-adding criteria
        var criteria = await _dbSet
            .IgnoreQueryFilters()
            .Where(c => c.EvaluationTemplateId == templateId)
            .ToListAsync();

        await HardDeleteRangeAsync(criteria);
    }
}

#endregion

#region Tender Lot Repository

public class TenderLotRepository : GenericRepository<TenderLot>, ITenderLotRepository
{
    public TenderLotRepository(ApplicationDbContext context) : base(context) { }

    public override async Task<TenderLot?> GetByIdAsync(Guid id)
    {
        return await _dbSet
            .Where(l => l.Id == id && !l.IsDeleted)
            .FirstOrDefaultAsync();
    }

    public async Task<TenderLot?> GetByIdWithItemsAsync(Guid id)
    {
        return await _dbSet
            .Where(l => l.Id == id && !l.IsDeleted)
            .Include(l => l.Items.Where(i => !i.IsDeleted))
            .Include(l => l.BidLots.Where(bl => !bl.IsDeleted))
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<TenderLot>> GetByTenderIdAsync(Guid tenderId)
    {
        return await _dbSet
            .Where(l => l.TenderId == tenderId && !l.IsDeleted)
            .Include(l => l.Items.Where(i => !i.IsDeleted))
            .OrderBy(l => l.LotCode)
            .ToListAsync();
    }

    public async Task<TenderLot?> GetByTenderAndLotCodeAsync(Guid tenderId, string lotCode)
    {
        return await _dbSet
            .Where(l => l.TenderId == tenderId && l.LotCode == lotCode && !l.IsDeleted)
            .FirstOrDefaultAsync();
    }

    public async Task<TenderLot> CreateAsync(TenderLot lot)
    {
        await _dbSet.AddAsync(lot);
        return lot;
    }

    public new async Task<TenderLot> UpdateAsync(TenderLot lot)
    {
        _dbSet.Update(lot);
        return await Task.FromResult(lot);
    }

    public override async Task DeleteAsync(Guid id)
    {
        var lot = await _dbSet.FindAsync(id);
        if (lot != null)
        {
            lot.IsDeleted = true;
            _dbSet.Update(lot);
        }
    }

    public async Task DeleteByTenderIdAsync(Guid tenderId)
    {
        var lots = await _dbSet
            .Where(l => l.TenderId == tenderId && !l.IsDeleted)
            .ToListAsync();

        foreach (var lot in lots)
        {
            lot.IsDeleted = true;
            _dbSet.Update(lot);
        }
    }

    public async Task<string> GenerateLotCodeAsync(Guid tenderId)
    {
        var existingLots = await _dbSet
            .Where(l => l.TenderId == tenderId)
            .CountAsync();

        return $"LOT-{existingLots + 1:D3}";
    }
}

#endregion

#region Tender Bid Lot Repository

public class TenderBidLotRepository : GenericRepository<TenderBidLot>, ITenderBidLotRepository
{
    public TenderBidLotRepository(ApplicationDbContext context) : base(context) { }

    public override async Task<TenderBidLot?> GetByIdAsync(Guid id)
    {
        return await _dbSet
            .Where(bl => bl.Id == id && !bl.IsDeleted)
            .Include(bl => bl.Lot)
            .FirstOrDefaultAsync();
    }

    public async Task<TenderBidLot?> GetByIdWithItemsAsync(Guid id)
    {
        return await _dbSet
            .Where(bl => bl.Id == id && !bl.IsDeleted)
            .Include(bl => bl.Lot)
            .Include(bl => bl.Items.Where(i => !i.IsDeleted))
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<TenderBidLot>> GetByBidIdAsync(Guid bidId)
    {
        return await _dbSet
            .Where(bl => bl.TenderBidId == bidId && !bl.IsDeleted)
            .Include(bl => bl.Lot)
            .Include(bl => bl.Items.Where(i => !i.IsDeleted))
            .OrderBy(bl => bl.Lot.LotCode)
            .ToListAsync();
    }

    public async Task<IEnumerable<TenderBidLot>> GetByLotIdAsync(Guid lotId)
    {
        return await _dbSet
            .Where(bl => bl.LotId == lotId && !bl.IsDeleted)
            .Include(bl => bl.TenderBid)
                .ThenInclude(b => b.BusinessPartner)
            .ToListAsync();
    }

    public async Task<TenderBidLot?> GetByBidAndLotAsync(Guid bidId, Guid lotId)
    {
        return await _dbSet
            .Where(bl => bl.TenderBidId == bidId && bl.LotId == lotId && !bl.IsDeleted)
            .Include(bl => bl.Items.Where(i => !i.IsDeleted))
            .FirstOrDefaultAsync();
    }

    public async Task<TenderBidLot> CreateAsync(TenderBidLot bidLot)
    {
        await _dbSet.AddAsync(bidLot);
        return bidLot;
    }

    public new Task<TenderBidLot> UpdateAsync(TenderBidLot bidLot)
    {
        // Preserve Added so a selected lot is inserted before its bid items.
        // DbSet.Update treats a client-generated Guid as evidence that the row
        // already exists and incorrectly changes a new lot to Modified.
        var entry = _context.Entry(bidLot);
        if (entry.State != EntityState.Added)
        {
            entry.State = EntityState.Modified;
        }

        return Task.FromResult(bidLot);
    }

    public override async Task DeleteAsync(Guid id)
    {
        var bidLot = await _dbSet.FindAsync(id);
        if (bidLot != null)
        {
            bidLot.IsDeleted = true;
            _dbSet.Update(bidLot);
        }
    }

    public async Task DeleteByBidIdAsync(Guid bidId)
    {
        var bidLots = await _dbSet
            .Where(bl => bl.TenderBidId == bidId && !bl.IsDeleted)
            .ToListAsync();

        foreach (var bidLot in bidLots)
        {
            bidLot.IsDeleted = true;
            _dbSet.Update(bidLot);
        }
    }
}

#endregion

#region Performance Bond Request Repository

public class PerformanceBondRequestRepository : GenericRepository<PerformanceBondRequest>, IPerformanceBondRequestRepository
{
    public PerformanceBondRequestRepository(ApplicationDbContext context) : base(context) { }

    public override async Task<PerformanceBondRequest?> GetByIdAsync(Guid id)
    {
        return await _dbSet
            .Where(p => p.Id == id && !p.IsDeleted)
            .Include(p => p.TenderAward)
                .ThenInclude(a => a.Tender)
            .Include(p => p.TenderBid)
            .Include(p => p.BusinessPartner)
            .Include(p => p.RequestedBy)
            .Include(p => p.SubmittedBy)
            .Include(p => p.ReviewedBy)
            .FirstOrDefaultAsync();
    }

    public async Task<PerformanceBondRequest?> GetByAwardIdAsync(Guid awardId)
    {
        return await _dbSet
            .Where(p => p.TenderAwardId == awardId && !p.IsDeleted)
            .Include(p => p.TenderAward)
                .ThenInclude(a => a.Tender)
            .Include(p => p.TenderBid)
            .Include(p => p.BusinessPartner)
            .Include(p => p.RequestedBy)
            .Include(p => p.SubmittedBy)
            .Include(p => p.ReviewedBy)
            .FirstOrDefaultAsync();
    }

    public async Task<PerformanceBondRequest?> GetByBidIdAsync(Guid bidId)
    {
        return await _dbSet
            .Where(p => p.TenderBidId == bidId && !p.IsDeleted)
            .Include(p => p.TenderAward)
                .ThenInclude(a => a.Tender)
            .Include(p => p.TenderBid)
            .Include(p => p.BusinessPartner)
            .Include(p => p.RequestedBy)
            .Include(p => p.SubmittedBy)
            .Include(p => p.ReviewedBy)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<PerformanceBondRequest>> GetByBusinessPartnerIdAsync(Guid businessPartnerId)
    {
        return await _dbSet
            .Where(p => p.BusinessPartnerId == businessPartnerId && !p.IsDeleted)
            .Include(p => p.TenderAward)
                .ThenInclude(a => a.Tender)
            .Include(p => p.TenderBid)
            .Include(p => p.BusinessPartner)
            .Include(p => p.RequestedBy)
            .OrderByDescending(p => p.RequestedDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<PerformanceBondRequest>> GetPendingRequestsAsync()
    {
        return await _dbSet
            .Where(p => p.Status == "Pending" && !p.IsDeleted)
            .Include(p => p.TenderAward)
                .ThenInclude(a => a.Tender)
            .Include(p => p.TenderBid)
            .Include(p => p.BusinessPartner)
            .Include(p => p.RequestedBy)
            .OrderByDescending(p => p.RequestedDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<PerformanceBondRequest>> GetByStatusAsync(string status)
    {
        return await _dbSet
            .Where(p => p.Status == status && !p.IsDeleted)
            .Include(p => p.TenderAward)
                .ThenInclude(a => a.Tender)
            .Include(p => p.TenderBid)
            .Include(p => p.BusinessPartner)
            .Include(p => p.RequestedBy)
            .OrderByDescending(p => p.RequestedDate)
            .ToListAsync();
    }

    public async Task<PerformanceBondRequest> CreateAsync(PerformanceBondRequest request)
    {
        await _dbSet.AddAsync(request);
        return request;
    }

    public new Task<PerformanceBondRequest> UpdateAsync(PerformanceBondRequest request)
    {
        _dbSet.Update(request);
        return Task.FromResult(request);
    }

    public override async Task DeleteAsync(Guid id)
    {
        var request = await _dbSet.FindAsync(id);
        if (request != null)
        {
            request.IsDeleted = true;
            _dbSet.Update(request);
        }
    }
}

#endregion

#region Tender Negotiation Repository

public class TenderNegotiationRepository : GenericRepository<TenderNegotiation>, ITenderNegotiationRepository
{
    public TenderNegotiationRepository(ApplicationDbContext context) : base(context) { }

    public override async Task<TenderNegotiation?> GetByIdAsync(Guid id)
    {
        return await _dbSet
            .Include(n => n.Tender)
            .Include(n => n.TenderBid)
            .Include(n => n.BusinessPartner)
            .Include(n => n.Lot)
            .Include(n => n.BidLot)
            .Include(n => n.InvitedBy)
            .Include(n => n.CompletedBy)
            .FirstOrDefaultAsync(n => n.Id == id && !n.IsDeleted);
    }

    public async Task<TenderNegotiation?> GetByIdWithItemsAsync(Guid id)
    {
        return await _dbSet
            .Include(n => n.Tender)
            .Include(n => n.TenderBid)
            .Include(n => n.BusinessPartner)
            .Include(n => n.Lot)
            .Include(n => n.BidLot)
            .Include(n => n.InvitedBy)
            .Include(n => n.CompletedBy)
            .Include(n => n.Items)
                .ThenInclude(i => i.TenderBidItem)
            .FirstOrDefaultAsync(n => n.Id == id && !n.IsDeleted);
    }

    public async Task<TenderNegotiation?> GetByTenderAndBidAsync(Guid tenderId, Guid bidId)
    {
        return await _dbSet
            .Include(n => n.Items)
            .FirstOrDefaultAsync(n => n.TenderId == tenderId && n.TenderBidId == bidId && !n.IsDeleted);
    }

    public async Task<TenderNegotiation?> GetByTenderBidAndLotAsync(Guid tenderId, Guid bidId, Guid? lotId)
    {
        return await _dbSet
            .Include(n => n.Items)
            .FirstOrDefaultAsync(n => n.TenderId == tenderId && n.TenderBidId == bidId && n.LotId == lotId && !n.IsDeleted);
    }

    public async Task<IEnumerable<TenderNegotiation>> GetByTenderIdAsync(Guid tenderId)
    {
        return await _dbSet
            .Include(n => n.TenderBid)
            .Include(n => n.BusinessPartner)
            .Include(n => n.Lot)
            .Include(n => n.Items)
            .Where(n => n.TenderId == tenderId && !n.IsDeleted)
            .OrderByDescending(n => n.InvitedDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TenderNegotiation>> GetByBidIdAsync(Guid bidId)
    {
        return await _dbSet
            .Include(n => n.Tender)
            .Include(n => n.Lot)
            .Include(n => n.Items)
            .Where(n => n.TenderBidId == bidId && !n.IsDeleted)
            .OrderByDescending(n => n.InvitedDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<TenderNegotiation>> GetByBusinessPartnerIdAsync(Guid businessPartnerId)
    {
        return await _dbSet
            .Include(n => n.Tender)
            .Include(n => n.TenderBid)
            .Include(n => n.Lot)
            .Include(n => n.Items)
            .Where(n => n.BusinessPartnerId == businessPartnerId && !n.IsDeleted)
            .OrderByDescending(n => n.InvitedDate)
            .ToListAsync();
    }

    public async Task<TenderNegotiation> CreateAsync(TenderNegotiation negotiation)
    {
        await _dbSet.AddAsync(negotiation);
        return negotiation;
    }

    public new Task<TenderNegotiation> UpdateAsync(TenderNegotiation negotiation)
    {
        _dbSet.Update(negotiation);
        return Task.FromResult(negotiation);
    }

    public async Task<TenderNegotiationItem?> GetItemByIdAsync(Guid itemId)
    {
        return await _context.Set<TenderNegotiationItem>()
            .Include(i => i.Negotiation)
            .Include(i => i.TenderBidItem)
            .FirstOrDefaultAsync(i => i.Id == itemId && !i.IsDeleted);
    }

    public Task<TenderNegotiationItem> UpdateItemAsync(TenderNegotiationItem item)
    {
        _context.Set<TenderNegotiationItem>().Update(item);
        return Task.FromResult(item);
    }

    public override async Task DeleteAsync(Guid id)
    {
        var negotiation = await _dbSet.FindAsync(id);
        if (negotiation != null)
        {
            negotiation.IsDeleted = true;
            _dbSet.Update(negotiation);
        }
    }
}

#endregion
