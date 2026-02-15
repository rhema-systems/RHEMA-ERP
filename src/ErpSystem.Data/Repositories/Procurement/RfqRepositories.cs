using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.Procurement;

public class RequestForQuotationRepository : GenericRepository<RequestForQuotation>, IRequestForQuotationRepository
{
    private readonly ApplicationDbContext _context;

    public RequestForQuotationRepository(ApplicationDbContext context) : base(context)
    {
        _context = context;
    }

    public async Task<RequestForQuotation?> GetByRfqNumberAsync(string rfqNumber)
    {
        return await _dbSet
            .Where(r => !r.IsDeleted && r.RfqNumber == rfqNumber)
            .FirstOrDefaultAsync();
    }

    public async Task<RequestForQuotation?> GetWithDetailsAsync(Guid rfqId)
    {
        return await _dbSet
            .Where(r => r.Id == rfqId && !r.IsDeleted)
            .Include(r => r.Items)
            .Include(r => r.Invitations)
            .ThenInclude(i => i.BusinessPartner)
            .Include(r => r.Quotes)
            .ThenInclude(q => q.BusinessPartner)
            .Include(r => r.Quotes)
            .ThenInclude(q => q.Items)
            .ThenInclude(i => i.RfqItem)
            .Include(r => r.AwardLines)
            .ThenInclude(a => a.BusinessPartner)
            .FirstOrDefaultAsync();
    }

    public async Task<ErpSystem.Core.DTOs.Common.PagedResult<RequestForQuotation>> GetRfqsAsync(int page, int pageSize, string? search = null, string? status = null)
    {
        IQueryable<RequestForQuotation> query = _dbSet
            .Where(r => !r.IsDeleted)
            .Include(r => r.Invitations)
            .Include(r => r.Quotes);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(r =>
                r.RfqNumber.Contains(search) ||
                r.Title.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(r => r.Status == status);
        }

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new ErpSystem.Core.DTOs.Common.PagedResult<RequestForQuotation>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<string> GenerateRfqNumberAsync()
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"RFQ-{year}-";

        var last = await _dbSet
            .Where(r => r.RfqNumber.StartsWith(prefix))
            .OrderByDescending(r => r.RfqNumber)
            .FirstOrDefaultAsync();

        if (last == null)
        {
            return $"{prefix}0001";
        }

        var lastNumber = int.Parse(last.RfqNumber.Substring(prefix.Length));
        return $"{prefix}{(lastNumber + 1):D4}";
    }
}

public class RequestForQuotationItemRepository : GenericRepository<RequestForQuotationItem>, IRequestForQuotationItemRepository
{
    public RequestForQuotationItemRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<RequestForQuotationItem>> GetByRfqIdAsync(Guid rfqId)
    {
        return await _dbSet
            .Where(i => i.RfqId == rfqId && !i.IsDeleted)
            .OrderBy(i => i.LineNumber)
            .ToListAsync();
    }
}

public class RequestForQuotationInvitationRepository : GenericRepository<RequestForQuotationInvitation>, IRequestForQuotationInvitationRepository
{
    public RequestForQuotationInvitationRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<RequestForQuotationInvitation>> GetByRfqIdAsync(Guid rfqId)
    {
        return await _dbSet
            .Where(i => i.RfqId == rfqId && !i.IsDeleted)
            .Include(i => i.BusinessPartner)
            .OrderByDescending(i => i.InvitedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<RequestForQuotationInvitation>> GetForSupplierAsync(Guid businessPartnerId, Guid tenantId)
    {
        return await _dbSet
            .Where(i =>
                i.BusinessPartnerId == businessPartnerId &&
                i.TenantId == tenantId &&
                !i.IsDeleted)
            .Include(i => i.Rfq)
            .OrderByDescending(i => i.InvitedAt)
            .ToListAsync();
    }
}

public class RequestForQuotationQuoteRepository : GenericRepository<RequestForQuotationQuote>, IRequestForQuotationQuoteRepository
{
    public RequestForQuotationQuoteRepository(ApplicationDbContext context) : base(context) { }

    public async Task<RequestForQuotationQuote?> GetByRfqAndSupplierAsync(Guid rfqId, Guid businessPartnerId, Guid tenantId)
    {
        return await _dbSet
            .Where(q =>
                q.RfqId == rfqId &&
                q.BusinessPartnerId == businessPartnerId &&
                q.TenantId == tenantId &&
                !q.IsDeleted)
            .Include(q => q.Items)
            .ThenInclude(i => i.RfqItem)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<RequestForQuotationQuote>> GetByRfqIdAsync(Guid rfqId)
    {
        return await _dbSet
            .Where(q => q.RfqId == rfqId && !q.IsDeleted)
            .Include(q => q.BusinessPartner)
            .Include(q => q.Items)
            .ThenInclude(i => i.RfqItem)
            .OrderByDescending(q => q.SubmittedAt ?? q.CreatedAt)
            .ToListAsync();
    }
}

public class RequestForQuotationQuoteItemRepository : GenericRepository<RequestForQuotationQuoteItem>, IRequestForQuotationQuoteItemRepository
{
    public RequestForQuotationQuoteItemRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<RequestForQuotationQuoteItem>> GetByQuoteIdAsync(Guid quoteId)
    {
        return await _dbSet
            .Where(i => i.QuoteId == quoteId && !i.IsDeleted)
            .Include(i => i.RfqItem)
            .OrderBy(i => i.RfqItem.LineNumber)
            .ToListAsync();
    }
}
