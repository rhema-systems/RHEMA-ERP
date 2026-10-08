using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Sales;

namespace ErpSystem.Core.Interfaces.Sales;

/// <summary>
/// Service interface for Quote/Proposal management.
/// Handles full lifecycle: Draft → Sent → Accepted/Rejected → Converted to Sales Order.
/// </summary>
public interface IQuoteService
{
    // ── CRUD ─────────────────────────────────────────────────────────────

    Task<QuoteDetailDto> CreateAsync(CreateQuoteDto dto);
    Task<QuoteDetailDto> CreatePropertyOpportunityQuoteAsync(Guid opportunityId, CancellationToken cancellationToken = default);
    Task<QuoteDetailDto> UpdateAsync(Guid id, UpdateQuoteDto dto);
    Task<QuoteDetailDto?> GetByIdAsync(Guid id);
    Task<PagedResult<QuoteSummaryDto>> GetAllAsync(
        int page = 1, int pageSize = 20,
        string? search = null,
        string? status = null,
        Guid? opportunityId = null,
        Guid? customerId = null,
        DateTime? startDate = null,
        DateTime? endDate = null);

    // ── Lifecycle ────────────────────────────────────────────────────────

    /// <summary>
    /// Mark quote as sent to customer
    /// </summary>
    Task<QuoteDetailDto> SendAsync(Guid id);

    /// <summary>
    /// Customer accepted the quote
    /// </summary>
    Task<QuoteDetailDto> AcceptAsync(Guid id);

    /// <summary>
    /// Customer rejected the quote
    /// </summary>
    Task<QuoteDetailDto> RejectAsync(Guid id, string? reason = null);

    /// <summary>
    /// Convert accepted quote to Sales Order (delegates to ISalesOrderService.ConvertQuoteToSalesOrderAsync)
    /// </summary>
    Task<Guid> ConvertToSalesOrderAsync(Guid id);

    // ── Queries ──────────────────────────────────────────────────────────

    Task<List<QuoteSummaryDto>> GetByOpportunityAsync(Guid opportunityId);
    Task<List<QuoteSummaryDto>> GetExpiringQuotesAsync(int daysAhead = 7);
}
