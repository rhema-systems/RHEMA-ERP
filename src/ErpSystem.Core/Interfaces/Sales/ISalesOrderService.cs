using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Sales;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.Sales;

/// <summary>
/// Service interface for Sales Order management — the core of the order-to-cash cycle.
/// </summary>
public interface ISalesOrderService
{
    // ── CRUD ────────────────────────────────────────────────────────────

    Task<SalesOrderDetailDto> CreateSalesOrderAsync(CreateSalesOrderDto dto);
    Task<SalesOrderDetailDto> UpdateSalesOrderAsync(Guid id, UpdateSalesOrderDto dto);
    Task<SalesOrderDetailDto?> GetSalesOrderByIdAsync(Guid id);
    Task<PagedResult<SalesOrderSummaryDto>> GetSalesOrdersAsync(
        int page, int pageSize,
        string? search = null,
        SalesOrderStatus? status = null,
        Guid? customerId = null,
        Guid? salesRepId = null,
        SalesOrderType? orderType = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? orderPriority = null,
        bool projectLinkedOnly = false,
        bool releasedUnitsOnly = false);

    // ── Lifecycle Actions ───────────────────────────────────────────────

    /// <summary>
    /// Submit a Draft SO for approval
    /// </summary>
    Task<SalesOrderDetailDto> SubmitForApprovalAsync(Guid id);

    /// <summary>
    /// Approve or reject a pending SO
    /// </summary>
    Task<SalesOrderDetailDto> ProcessApprovalAsync(Guid id, SalesOrderApprovalDto dto);

    /// <summary>
    /// Confirm an approved SO — validates credit limit, reserves stock
    /// </summary>
    Task<SalesOrderDetailDto> ConfirmSalesOrderAsync(Guid id);

    /// <summary>
    /// Cancel a SO — releases reserved stock, records reason
    /// </summary>
    Task<SalesOrderDetailDto> CancelSalesOrderAsync(Guid id, CancelSalesOrderDto dto);

    /// <summary>
    /// Put a SO on hold
    /// </summary>
    Task<SalesOrderDetailDto> PutOnHoldAsync(Guid id, string? reason = null);

    /// <summary>
    /// Release a SO from hold
    /// </summary>
    Task<SalesOrderDetailDto> ReleaseFromHoldAsync(Guid id);

    /// <summary>
    /// Close a fully delivered and invoiced SO
    /// </summary>
    Task<SalesOrderDetailDto> CloseSalesOrderAsync(Guid id);

    // ── Conversion ──────────────────────────────────────────────────────

    /// <summary>
    /// Convert an accepted Quote into a Sales Order
    /// </summary>
    Task<SalesOrderDetailDto> ConvertQuoteToSalesOrderAsync(Guid quoteId);

    /// <summary>
    /// Generate an Invoice from a confirmed SO
    /// </summary>
    Task<Guid> GenerateInvoiceAsync(Guid salesOrderId);

    // ── Utilities ───────────────────────────────────────────────────────

    Task<string> GenerateOrderNumberAsync();
    Task<bool> ValidateCreditLimitAsync(Guid businessPartnerId, decimal orderAmount);
    Task<decimal> GetCustomerOutstandingBalanceAsync(Guid businessPartnerId);
}
