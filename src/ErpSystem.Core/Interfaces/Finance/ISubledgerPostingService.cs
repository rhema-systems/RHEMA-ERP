using System;
using System.Threading;
using System.Threading.Tasks;
using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces.Finance
{
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
        Task<JournalEntryDto> PostApInvoiceAsync(Guid vendorInvoiceId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Posts a vendor payment to the General Ledger, reducing AP Control and Bank balances.
        /// </summary>
        Task<JournalEntryDto> PostApPaymentAsync(Guid vendorPaymentId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Posts a supplier debit note generated from a supplier return to the General Ledger.
        /// </summary>
        Task<JournalEntryDto> PostSupplierDebitNoteAsync(Guid supplierDebitNoteId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Posts a customer invoice to the General Ledger, handling revenue, tax, AR Control, and COGS/Inventory movements.
        /// </summary>
        Task<JournalEntryDto> PostArInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Posts a customer payment to the General Ledger, increasing Bank and reducing AR Control balances.
        /// </summary>
        Task<JournalEntryDto> PostArPaymentAsync(Guid paymentId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Posts a sales credit note to the General Ledger, reducing AR and reversing revenue/tax.
        /// </summary>
        Task<JournalEntryDto> PostSalesCreditNoteAsync(Guid creditNoteId, CancellationToken cancellationToken = default);
    }
}
