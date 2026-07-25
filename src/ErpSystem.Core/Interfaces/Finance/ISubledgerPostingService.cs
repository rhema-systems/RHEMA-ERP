using System;
using System.Threading;
using System.Threading.Tasks;
using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces.Finance
{
    /// <summary>
    /// Legacy subledger posting surface retained only for historical migration references.
    /// Normal Finance runtime posting must use the owning module service and <see cref="IFinancePostingEngine"/>.
    /// This interface is intentionally not registered in normal application DI.
    /// </summary>
    [Obsolete("Legacy subledger posting is disabled for normal runtime flows. Use IFinancePostingEngine through the owning Finance module.")]
    public interface ISubledgerPostingService
    {
        /// <summary>
        /// Posts a finance purchase receipt/GRV to the General Ledger, recognizing the received
        /// expense or inventory amount against the configured GRV accrual control account.
        /// </summary>
        Task<JournalEntryDto> PostFinancePurchaseOrderReceiptAsync(Guid receiptId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Posts a vendor invoice to the General Ledger, generating appropriate expense, inventory, and AP Control entries.
        /// </summary>
        [Obsolete("Normal AP invoice posting uses IVendorInvoiceService.PostAsync and IFinancePostingEngine. This legacy method is reserved for guarded opening-balance/migration paths.")]
        Task<JournalEntryDto> PostApInvoiceAsync(Guid vendorInvoiceId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Posts a vendor payment to the General Ledger, reducing AP Control and Bank balances.
        /// </summary>
        [Obsolete("Normal AP payment posting uses IVendorPaymentService.PostAsync and IFinancePostingEngine. This legacy method is reserved for migration compatibility only.")]
        Task<JournalEntryDto> PostApPaymentAsync(Guid vendorPaymentId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Posts a purchase discount adjustment for a vendor payment allocation created after the payment journal.
        /// </summary>
        [Obsolete("AP payment discount posting must use the central finance posting engine. This legacy method is reserved for migration compatibility only.")]
        Task<JournalEntryDto> PostApPaymentDiscountAdjustmentAsync(Guid allocationId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Posts a supplier debit note generated from a supplier return to the General Ledger.
        /// </summary>
        Task<JournalEntryDto> PostSupplierDebitNoteAsync(Guid supplierDebitNoteId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Posts a customer invoice to the General Ledger, handling revenue, tax, AR Control, and COGS/Inventory movements.
        /// </summary>
        [Obsolete("Normal AR invoice posting uses IInvoiceService.PostAsync and IFinancePostingEngine. This legacy method is reserved for migration compatibility only.")]
        Task<JournalEntryDto> PostArInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Posts a customer payment to the General Ledger, increasing Bank and reducing AR Control balances.
        /// </summary>
        [Obsolete("Normal AR receipt and customer credit-note posting use IPaymentService and IFinancePostingEngine. This legacy method is disabled for normal workflows.")]
        Task<JournalEntryDto> PostArPaymentAsync(Guid paymentId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Posts a sales discount adjustment for a customer payment allocation created after the payment journal.
        /// </summary>
        [Obsolete("AR receipt discount posting must use the central finance posting engine. This legacy method is disabled for normal receipt workflows.")]
        Task<JournalEntryDto> PostArPaymentDiscountAdjustmentAsync(Guid allocationId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Posts a sales credit note to the General Ledger, reducing AR and reversing revenue/tax.
        /// </summary>
        [Obsolete("Sales credit-note posting uses IReturnOrderService.PostCreditNoteAsync and IFinancePostingEngine. This legacy method is disabled for normal workflows.")]
        Task<JournalEntryDto> PostSalesCreditNoteAsync(Guid creditNoteId, CancellationToken cancellationToken = default);
    }
}
