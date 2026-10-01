using ErpSystem.Core.DTOs.Ehc;
using ErpSystem.Core.Entities.Ehc;

namespace ErpSystem.Core.Interfaces.Ehc;

public interface IPropertyEnquiryProspectService
{
    Task<PropertyEnquiryProspectDto?> GetAsync(Guid ticketId, CancellationToken cancellationToken = default);
    Task<PropertyEnquiryProspectDto> RecordContactAsync(Guid ticketId, RecordPropertyEnquiryContactRequest request, CancellationToken cancellationToken = default);
    Task<PropertyEnquiryProspectDto> QualifyAsync(Guid ticketId, QualifyPropertyEnquiryRequest request, CancellationToken cancellationToken = default);
    Task<PropertyEnquiryProspectDto> DisqualifyAsync(Guid ticketId, DisqualifyPropertyEnquiryRequest request, CancellationToken cancellationToken = default);
    Task<PropertyEnquiryProspectDto> CreateOpportunityAsync(Guid ticketId, CreatePropertyEnquiryOpportunityRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PropertyEnquiryBusinessPartnerMatchDto>> FindBusinessPartnerMatchesAsync(Guid ticketId, CancellationToken cancellationToken = default);
    Task<PropertyEnquiryProspectDto> LinkBusinessPartnerAsync(Guid ticketId, Guid businessPartnerId, CancellationToken cancellationToken = default);
    Task<PropertyEnquiryProspectDto> CreateBusinessPartnerAsync(Guid ticketId, CreatePropertyEnquiryBusinessPartnerRequest request, CancellationToken cancellationToken = default);
    Task<PropertyEnquiryProspectDto> FinalizeBusinessPartnerAsync(Guid ticketId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProspectDepositReceiptDto>> GetDepositsAsync(Guid ticketId, CancellationToken cancellationToken = default);
    Task<ProspectDepositReceiptDto> RecordDepositAsync(Guid ticketId, RecordProspectDepositRequest request, CancellationToken cancellationToken = default);
    Task<ProspectDepositReceiptDto> ClearDepositAsync(Guid ticketId, Guid receiptId, ClearProspectDepositRequest request, CancellationToken cancellationToken = default);
    Task<ProspectDepositReceiptDto> ReverseDepositAsync(Guid ticketId, Guid receiptId, ReverseProspectDepositRequest request, CancellationToken cancellationToken = default);
    Task<PropertyEnquiryEmailResultDto> SendEmailAsync(Guid ticketId, SendPropertyEnquiryEmailRequest request, CancellationToken cancellationToken = default);
    Task<PropertyProspectDepositPolicyDto?> GetDepositPolicyAsync(Guid salesSaleableSourceId, CancellationToken cancellationToken = default);
    Task UpsertDepositPolicyAsync(UpsertPropertyProspectDepositPolicyRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Finance-owned adapter. Prospect deposits post Dr cash/liquidity, Cr prospect-deposit liability.
/// Customer conversion reclassifies the existing liability into AR customer advances; it must never
/// debit cash a second time.
/// </summary>
public interface IProspectDepositFinancePostingService
{
    Task<ProspectDepositFinancePostingResult> PostClearedReceiptAsync(ProspectDepositReceipt receipt, CancellationToken cancellationToken = default);
    Task<ProspectDepositFinancePostingResult> ReverseReceiptAsync(ProspectDepositReceipt receipt, string reason, DateTime? reversalDate, CancellationToken cancellationToken = default);
    Task<ProspectDepositCustomerAdvanceTransferResult> TransferToCustomerAdvanceAsync(ProspectDepositReceipt receipt, Guid businessPartnerId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Applies converted public-prospect deposits to the posted invoice produced by the same Sales
/// opportunity. A deposit cannot enter this workflow until customer conversion has created an
/// approved Business Partner and reclassified the cleared receipt as a customer advance.
/// </summary>
public interface IPropertyEnquiryDepositApplicationService
{
    Task<ProspectDepositApplicationResult> ApplyToPostedSalesInvoiceAsync(
        Guid salesOrderId,
        Guid invoiceId,
        CancellationToken cancellationToken = default);
}

public sealed record ProspectDepositFinancePostingResult(Guid PostingEventId, Guid JournalEntryId, bool WasDuplicate);
public sealed record ProspectDepositCustomerAdvanceTransferResult(Guid PostingEventId, Guid JournalEntryId, Guid CustomerPaymentId, bool WasDuplicate);
public sealed record ProspectDepositApplicationResult(int AppliedReceiptCount, decimal AppliedAmount)
{
    public static ProspectDepositApplicationResult None { get; } = new(0, 0m);
}
