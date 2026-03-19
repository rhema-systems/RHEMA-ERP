using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Sales;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Core.Services.Sales;

public class QuoteService : IQuoteService
{
    private readonly IGenericRepository<Quote> _quoteRepo;
    private readonly IGenericRepository<QuoteLineItem> _lineRepo;
    private readonly ISalesOrderService _salesOrderService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<QuoteService> _logger;

    public QuoteService(
        IGenericRepository<Quote> quoteRepo,
        IGenericRepository<QuoteLineItem> lineRepo,
        ISalesOrderService salesOrderService,
        IUnitOfWork unitOfWork,
        ICurrentUserProvider currentUserProvider,
        ILogger<QuoteService> logger)
    {
        _quoteRepo = quoteRepo;
        _lineRepo = lineRepo;
        _salesOrderService = salesOrderService;
        _unitOfWork = unitOfWork;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
    }

    #region CRUD

    public async Task<QuoteDetailDto> CreateAsync(CreateQuoteDto dto)
    {
        var quoteNumber = await GenerateQuoteNumberAsync();
        var tenantId = _currentUserProvider.TenantId;

        var quote = new Quote
        {
            DocumentNumber = quoteNumber,
            DocumentDate = DateTime.UtcNow,
            OpportunityId = dto.OpportunityId,
            CustomerId = dto.CustomerId,
            QuoteName = dto.QuoteName,
            ValidUntil = dto.ValidUntil,
            QuoteStatus = "Draft",
            ShippingAmount = dto.ShippingAmount ?? 0,
            Proposal = dto.Proposal,
            TenantId = tenantId
        };

        await _quoteRepo.AddAsync(quote);

        decimal subTotal = 0;
        decimal totalTax = 0;

        foreach (var lineDto in dto.LineItems)
        {
            var line = new QuoteLineItem
            {
                QuoteId = quote.Id,
                Description = lineDto.Description,
                Quantity = lineDto.Quantity,
                UnitPrice = lineDto.UnitPrice,
                ProductCode = lineDto.ProductCode,
                Unit = lineDto.Unit,
                DiscountPercentage = lineDto.DiscountPercentage ?? 0,
                TaxAmount = lineDto.TaxAmount ?? 0,
                TaxCode = lineDto.TaxCode,
                TenantId = tenantId
            };

            line.DiscountAmount = line.LineTotal * (line.DiscountPercentage / 100);
            subTotal += line.LineTotal - line.DiscountAmount;
            totalTax += line.TaxAmount;

            await _lineRepo.AddAsync(line);
        }

        quote.SubTotal = subTotal;
        quote.TaxAmount = totalTax;
        quote.TotalAmount = subTotal + totalTax + quote.ShippingAmount;

        await _quoteRepo.UpdateAsync(quote);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Created Quote {QuoteNumber} for {Amount}", quoteNumber, quote.TotalAmount);
        return await GetByIdAsync(quote.Id) ?? throw new InvalidOperationException("Failed to retrieve created Quote");
    }

    public async Task<QuoteDetailDto> UpdateAsync(Guid id, UpdateQuoteDto dto)
    {
        var quote = await _quoteRepo.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"Quote {id} not found");

        if (quote.QuoteStatus != "Draft")
            throw new InvalidOperationException($"Cannot edit quote in {quote.QuoteStatus} status");

        if (dto.QuoteName != null) quote.QuoteName = dto.QuoteName;
        if (dto.ValidUntil.HasValue) quote.ValidUntil = dto.ValidUntil.Value;
        if (dto.ShippingAmount.HasValue) quote.ShippingAmount = dto.ShippingAmount.Value;
        if (dto.Proposal != null) quote.Proposal = dto.Proposal;

        if (dto.LineItems != null)
        {
            var existingLines = await _lineRepo.FindAsync(l => l.QuoteId == id);
            foreach (var line in existingLines)
                await _lineRepo.DeleteAsync(line);

            decimal subTotal = 0;
            decimal totalTax = 0;

            foreach (var lineDto in dto.LineItems)
            {
                var line = new QuoteLineItem
                {
                    QuoteId = id,
                    Description = lineDto.Description,
                    Quantity = lineDto.Quantity,
                    UnitPrice = lineDto.UnitPrice,
                    ProductCode = lineDto.ProductCode,
                    Unit = lineDto.Unit,
                    DiscountPercentage = lineDto.DiscountPercentage ?? 0,
                    TaxAmount = lineDto.TaxAmount ?? 0,
                    TaxCode = lineDto.TaxCode,
                    TenantId = quote.TenantId
                };

                line.DiscountAmount = line.LineTotal * (line.DiscountPercentage / 100);
                subTotal += line.LineTotal - line.DiscountAmount;
                totalTax += line.TaxAmount;

                await _lineRepo.AddAsync(line);
            }

            quote.SubTotal = subTotal;
            quote.TaxAmount = totalTax;
            quote.TotalAmount = subTotal + totalTax + quote.ShippingAmount;
        }

        await _quoteRepo.UpdateAsync(quote);
        await _unitOfWork.SaveChangesAsync();

        return await GetByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve updated Quote");
    }

    public async Task<QuoteDetailDto?> GetByIdAsync(Guid id)
    {
        var quote = await _quoteRepo.GetByIdAsync(id,
            q => q.Opportunity,
            q => q.Customer!,
            q => q.LineItems);

        return quote == null ? null : MapToDetailDto(quote);
    }

    public async Task<PagedResult<QuoteSummaryDto>> GetAllAsync(
        int page = 1, int pageSize = 20,
        string? search = null, string? status = null, Guid? opportunityId = null,
        Guid? customerId = null, DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _quoteRepo.GetQueryable();

        if (!string.IsNullOrEmpty(search))
            query = query.Where(q => q.QuoteName.Contains(search) || q.DocumentNumber.Contains(search));
        if (!string.IsNullOrEmpty(status))
            query = query.Where(q => q.QuoteStatus == status);
        if (opportunityId.HasValue)
            query = query.Where(q => q.OpportunityId == opportunityId.Value);
        if (customerId.HasValue)
            query = query.Where(q => q.CustomerId == customerId.Value);
        if (startDate.HasValue)
            query = query.Where(q => q.DocumentDate >= startDate.Value);
        if (endDate.HasValue)
            query = query.Where(q => q.DocumentDate <= endDate.Value);

        var totalCount = await query.CountAsync();
        var items = await query
            .Include(q => q.Opportunity)
            .Include(q => q.Customer)
            .Include(q => q.LineItems)
            .OrderByDescending(q => q.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<QuoteSummaryDto>
        {
            Items = items.Select(MapToSummaryDto).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    #endregion

    #region Lifecycle

    public async Task<QuoteDetailDto> SendAsync(Guid id)
    {
        var quote = await _quoteRepo.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"Quote {id} not found");

        if (quote.QuoteStatus != "Draft")
            throw new InvalidOperationException("Only draft quotes can be sent");

        quote.QuoteStatus = "Sent";
        quote.SentDate = DateTime.UtcNow;

        await _quoteRepo.UpdateAsync(quote);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Quote {QuoteNumber} sent to customer", quote.DocumentNumber);
        return await GetByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve");
    }

    public async Task<QuoteDetailDto> AcceptAsync(Guid id)
    {
        var quote = await _quoteRepo.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"Quote {id} not found");

        if (quote.QuoteStatus != "Sent")
            throw new InvalidOperationException("Only sent quotes can be accepted");

        quote.QuoteStatus = "Accepted";
        quote.AcceptedDate = DateTime.UtcNow;

        await _quoteRepo.UpdateAsync(quote);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Quote {QuoteNumber} accepted", quote.DocumentNumber);
        return await GetByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve");
    }

    public async Task<QuoteDetailDto> RejectAsync(Guid id, string? reason = null)
    {
        var quote = await _quoteRepo.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"Quote {id} not found");

        if (quote.QuoteStatus != "Sent")
            throw new InvalidOperationException("Only sent quotes can be rejected");

        quote.QuoteStatus = "Rejected";

        await _quoteRepo.UpdateAsync(quote);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Quote {QuoteNumber} rejected: {Reason}", quote.DocumentNumber, reason);
        return await GetByIdAsync(id) ?? throw new InvalidOperationException("Failed to retrieve");
    }

    public async Task<Guid> ConvertToSalesOrderAsync(Guid id)
    {
        var quote = await _quoteRepo.GetByIdAsync(id)
            ?? throw new InvalidOperationException($"Quote {id} not found");

        if (quote.QuoteStatus != "Accepted")
            throw new InvalidOperationException("Only accepted quotes can be converted to Sales Orders");

        var salesOrder = await _salesOrderService.ConvertQuoteToSalesOrderAsync(id);
        return salesOrder.Id;
    }

    #endregion

    #region Queries

    public async Task<List<QuoteSummaryDto>> GetByOpportunityAsync(Guid opportunityId)
    {
        var items = await _quoteRepo.GetQueryable()
            .Where(q => q.OpportunityId == opportunityId)
            .Include(q => q.Opportunity)
            .Include(q => q.Customer)
            .Include(q => q.LineItems)
            .OrderByDescending(q => q.CreatedAt)
            .ToListAsync();
        return items.Select(MapToSummaryDto).ToList();
    }

    public async Task<List<QuoteSummaryDto>> GetExpiringQuotesAsync(int daysAhead = 7)
    {
        var cutoff = DateTime.UtcNow.AddDays(daysAhead);
        var items = await _quoteRepo.GetQueryable()
            .Where(q => q.QuoteStatus == "Sent" && q.ValidUntil <= cutoff && q.ValidUntil >= DateTime.UtcNow)
            .Include(q => q.Opportunity)
            .Include(q => q.Customer)
            .OrderBy(q => q.ValidUntil)
            .ToListAsync();
        return items.Select(MapToSummaryDto).ToList();
    }

    #endregion

    #region Helpers

    private async Task<string> GenerateQuoteNumberAsync()
    {
        var count = await _quoteRepo.CountAsync() + 1;
        return $"QT-{count:D6}";
    }

    private static QuoteSummaryDto MapToSummaryDto(Quote q) => new()
    {
        Id = q.Id,
        DocumentNumber = q.DocumentNumber,
        QuoteName = q.QuoteName,
        QuoteStatus = q.QuoteStatus,
        OpportunityId = q.OpportunityId,
        OpportunityName = q.Opportunity?.Name,
        CustomerName = q.Customer?.CustomerName,
        TotalAmount = q.TotalAmount,
        TaxAmount = q.TaxAmount,
        ValidUntil = q.ValidUntil,
        SentDate = q.SentDate,
        AcceptedDate = q.AcceptedDate,
        LineCount = q.LineItems?.Count ?? 0,
        CreatedAt = q.CreatedAt
    };

    private static QuoteDetailDto MapToDetailDto(Quote q) => new()
    {
        Id = q.Id,
        DocumentNumber = q.DocumentNumber,
        QuoteName = q.QuoteName,
        QuoteStatus = q.QuoteStatus,
        OpportunityId = q.OpportunityId,
        OpportunityName = q.Opportunity?.Name,
        CustomerId = q.CustomerId,
        CustomerName = q.Customer?.CustomerName,
        TotalAmount = q.TotalAmount,
        TaxAmount = q.TaxAmount,
        SubTotal = q.SubTotal,
        DiscountAmount = q.DiscountAmount,
        ShippingAmount = q.ShippingAmount,
        ValidUntil = q.ValidUntil,
        SentDate = q.SentDate,
        AcceptedDate = q.AcceptedDate,
        Proposal = q.Proposal,
        ConvertedInvoiceId = q.ConvertedInvoiceId,
        LineCount = q.LineItems?.Count ?? 0,
        CreatedAt = q.CreatedAt,
        LineItems = q.LineItems?.Select(li => new QuoteLineItemDto
        {
            Id = li.Id,
            QuoteId = li.QuoteId,
            Description = li.Description,
            Quantity = li.Quantity,
            UnitPrice = li.UnitPrice,
            LineTotal = li.LineTotal,
            ProductCode = li.ProductCode,
            Unit = li.Unit,
            DiscountPercentage = li.DiscountPercentage,
            DiscountAmount = li.DiscountAmount,
            TaxAmount = li.TaxAmount,
            TaxCode = li.TaxCode
        }).ToList() ?? new()
    };

    #endregion
}
