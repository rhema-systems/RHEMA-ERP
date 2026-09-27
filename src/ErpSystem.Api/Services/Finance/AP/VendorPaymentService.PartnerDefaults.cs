using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Services.Procurement;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.AP;

public partial class VendorPaymentService
{
    private async Task<Guid> ResolvePaymentDiscountAccountAsync(
        VendorPayment payment, BusinessPartner supplier, FinanceSettings settings, CancellationToken cancellationToken)
    {
        if (payment.JournalEntryId.HasValue)
        {
            // Rebuild an idempotent request from the original posted account, not a
            // later master-data choice. The posting engine still verifies the full
            // document/amount/dimension fingerprint before returning the old journal.
            var originalJournalId = payment.JournalEntryId.Value;
            var originalExists = await _unitOfWork.Repository<JournalEntry>()
                .GetQueryable(journal => journal.Id == originalJournalId && journal.TenantId == TenantId &&
                    !journal.IsDeleted && !journal.IsReversed && journal.PostingStatus == "Posted" &&
                    journal.SourceModule == "AP" && journal.SourceDocumentType == "VendorPayment" && journal.SourceDocumentId == payment.Id)
                .AnyAsync(cancellationToken);
            if (!originalExists)
                throw new InvalidOperationException("AP_PAYMENT_DISCOUNT_LINEAGE_UNAVAILABLE: the original posted payment journal could not be verified.");
            var accounts = await _unitOfWork.Repository<AccountTransaction>()
                .GetQueryable(line => line.TenantId == TenantId && !line.IsDeleted &&
                    line.JournalEntryId == originalJournalId && line.SourceDocumentId == payment.Id &&
                    line.TransactionTag == "AP-Discount" && line.CreditAmount > 0m)
                .Select(line => line.AccountId).Distinct().ToListAsync(cancellationToken);
            if (accounts.Count != 1)
                throw new InvalidOperationException("AP_PAYMENT_DISCOUNT_LINEAGE_UNAVAILABLE: the original payment discount account is missing or ambiguous.");
            return accounts[0];
        }

        return settings.DiscountReceivedAccountId
            ?? throw new InvalidOperationException("Purchase discount received account is not configured for this tenant.");
    }
}
