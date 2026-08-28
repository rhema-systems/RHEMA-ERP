using ErpSystem.Core.DTOs.Common;
using ErpSystem.Core.DTOs.Procurement;

namespace ErpSystem.Core.Interfaces.Procurement;

public interface IRfqService
{
    Task<PagedResult<RfqDto>> GetRfqsAsync(int page, int pageSize, string? search = null, string? status = null);
    Task<RfqDetailDto?> GetRfqByIdAsync(Guid id);

    Task<RfqDetailDto> CreateRfqFromPurchaseRequisitionAsync(Guid purchaseRequisitionId, CreateRfqFromPurchaseRequisitionDto? dto = null);
    Task<RfqDetailDto> UpdateRfqAsync(Guid id, UpdateRfqDto dto);

    /// <summary>
    /// Sends an RFQ to selected suppliers + optional external recipients (email-only).
    /// This sets the RFQ status to Sent.
    /// </summary>
    Task SendRfqAsync(Guid id, SendRfqDto dto);

    /// <summary>
    /// Supplier portal: gets RFQs invited for a business partner.
    /// </summary>
    Task<List<RfqDto>> GetSupplierRfqsAsync(Guid businessPartnerId, Guid tenantId);

    /// <summary>
    /// Supplier portal: gets RFQ detail for a supplier (must be invited).
    /// </summary>
    Task<RfqDetailDto?> GetSupplierRfqDetailAsync(Guid rfqId, Guid businessPartnerId, Guid tenantId);

    /// <summary>
    /// Supplier portal: submit a quote or revise the same quote and line records before the deadline.
    /// Every submission is retained as an immutable audit snapshot.
    /// </summary>
    Task<RfqQuoteDto> SubmitQuoteAsync(Guid rfqId, Guid businessPartnerId, Guid submittedByUserId, Guid tenantId, SubmitRfqQuoteDto dto);

    /// <summary>
    /// Internal: award an RFQ (winner-takes-all or split-award) and create purchase order(s) from submitted quote(s).
    /// </summary>
    Task<CreatePurchaseOrdersFromRfqResponseDto> CreatePurchaseOrdersFromAwardAsync(Guid rfqId, CreatePurchaseOrdersFromRfqDto dto);
}

public interface IRfqNotificationService
{
    Task SendRfqSentNotificationAsync(Guid rfqId, List<Guid> businessPartnerIds, List<string>? externalRecipientEmails = null);

    /// <summary>
    /// Notify internal users (in-app + email) when a supplier submits a quote for an RFQ.
    /// </summary>
    Task SendRfqQuoteSubmittedNotificationAsync(Guid rfqId, Guid quoteId);

    /// <summary>
    /// Notify awarded suppliers (in-app + email) with awarded RFQ line details.
    /// </summary>
    Task SendRfqAwardedNotificationAsync(Guid rfqId);
}

/// <summary>
/// Generates RFQ invitation PDF (QuestPDF) used as an email attachment.
/// Implementations live in the API project (QuestPDF) and are consumed by core services.
/// </summary>
public interface IRfqInvitationDocumentService
{
    Task<(byte[] Content, string FileName)> GenerateRfqInvitationPdfAsync(Guid rfqId, CancellationToken cancellationToken = default);
}
