using ErpSystem.Core.DTOs.Sales;

namespace ErpSystem.Core.Interfaces.Finance;

public interface ICreditNotePostingService
{
    Task<CreditNoteDetailDto> PostCreditNoteAsync(Guid creditNoteId, Guid? invoiceId = null, CancellationToken cancellationToken = default);
    Task<CreditNoteDetailDto> VoidCreditNoteAsync(Guid creditNoteId, string reason, CancellationToken cancellationToken = default);
    Task<CreditNoteDetailDto> ApplyUnappliedCreditAsync(Guid creditNoteId, Guid targetInvoiceId, decimal? amountToApply = null, CancellationToken cancellationToken = default);
}
