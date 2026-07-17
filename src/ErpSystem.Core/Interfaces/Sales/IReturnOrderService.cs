using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Sales;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.Sales;

/// <summary>
/// Service interface for Return Order, Credit Note, and Refund management.
/// Handles the full post-sale lifecycle: Return → Credit → Refund.
/// </summary>
public interface IReturnOrderService
{
    // ── Return Orders ──
    Task<ReturnOrderDetailDto> CreateReturnOrderAsync(CreateReturnOrderDto dto);
    Task<ReturnOrderDetailDto?> GetReturnOrderByIdAsync(Guid id);
    Task<PagedResult<ReturnOrderSummaryDto>> GetReturnOrdersAsync(
        int page = 1, int pageSize = 20,
        string? search = null, ReturnOrderStatus? status = null,
        Guid? businessPartnerId = null, Guid? salesOrderId = null,
        DateTime? startDate = null, DateTime? endDate = null);
    Task<ReturnOrderDetailDto> ApproveReturnOrderAsync(Guid id);
    Task<ReturnOrderDetailDto> ReceiveReturnOrderAsync(Guid id);
    Task<ReturnOrderDetailDto> InspectReturnOrderAsync(Guid id, string? notes = null);
    Task<ReturnOrderDetailDto> RejectReturnOrderAsync(Guid id, string? reason = null);
    Task<ReturnOrderDetailDto> CancelReturnOrderAsync(Guid id, string? reason = null);

    // ── Credit Notes ──
    Task<CreditNoteDetailDto> CreateCreditNoteAsync(CreateCreditNoteDto dto);
    Task<CreditNoteDetailDto> CreateCreditNoteFromReturnAsync(Guid returnOrderId);
    Task<CreditNoteDetailDto?> GetCreditNoteByIdAsync(Guid id);
    Task<PagedResult<CreditNoteSummaryDto>> GetCreditNotesAsync(
        int page = 1, int pageSize = 20,
        string? search = null, CreditNoteStatus? status = null,
        Guid? businessPartnerId = null, DateTime? startDate = null, DateTime? endDate = null);
    Task<CreditNoteDetailDto> SubmitCreditNoteForApprovalAsync(Guid id);
    Task<CreditNoteDetailDto> ProcessCreditNoteApprovalAsync(Guid id, CreditNoteApprovalDto dto);
    Task<CreditNoteDetailDto> ApproveCreditNoteAsync(Guid id);
    Task<CreditNoteDetailDto> PostCreditNoteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CreditNoteDetailDto> ApplyCreditNoteAsync(Guid id, Guid? invoiceId = null);
    Task<CreditNoteDetailDto> VoidCreditNoteAsync(Guid id, string? reason = null);

    // ── Refunds ──
    Task<RefundDetailDto> CreateRefundAsync(CreateRefundDto dto);
    Task<RefundDetailDto?> GetRefundByIdAsync(Guid id);
    Task<PagedResult<RefundSummaryDto>> GetRefundsAsync(
        int page = 1, int pageSize = 20,
        string? search = null, RefundStatus? status = null,
        Guid? businessPartnerId = null, DateTime? startDate = null, DateTime? endDate = null);
    Task<RefundDetailDto> SubmitRefundForApprovalAsync(Guid id);
    Task<RefundDetailDto> ProcessRefundApprovalAsync(Guid id, RefundApprovalDto dto);
    Task<RefundDetailDto> ApproveRefundAsync(Guid id);
    Task<RefundDetailDto> ProcessRefundAsync(Guid id, string? paymentReference = null);
    Task<RefundDetailDto> RejectRefundAsync(Guid id, string? reason = null);
}
