using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Services.Procurement;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.AP;

public partial class VendorPaymentService
{
    private async Task<Guid> ResolvePaymentDiscountAccountAsync(
        VendorPayment payment, Supplier supplier, FinanceSettings settings, CancellationToken cancellationToken)
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

        var defaults = await GetPaymentPartnerDefaultsAsync(supplier, cancellationToken);
        return defaults?.DefaultTermsDiscountsTakenAccountId ?? settings.DiscountReceivedAccountId
            ?? throw new InvalidOperationException("Purchase discount received account is not configured for this tenant.");
    }

    private async Task<Guid?> GetPartnerPaymentBankDefaultAsync(Supplier supplier, CancellationToken cancellationToken)
    {
        var defaults = await GetPaymentPartnerDefaultsAsync(supplier, cancellationToken);
        if (defaults == null) return null;
        var banks = _unitOfWork.Repository<BankAccount>().GetQueryable(bank =>
            bank.TenantId == TenantId && !bank.IsDeleted && bank.IsActive);
        if (defaults.CashAccountSource == "BusinessPartner")
        {
            if (!defaults.DefaultCashAccountId.HasValue)
                throw new InvalidOperationException("Configure the supplier cash account or explicitly select the payment bank account.");
            // Bank ledger and GL must agree. Resolve a bank linked to the cash GL;
            // never substitute an arbitrary GL account underneath a different bank.
            var candidates = await banks.Where(bank => bank.GLAccountId == defaults.DefaultCashAccountId)
                .Select(bank => bank.Id).Take(2).ToListAsync(cancellationToken);
            if (candidates.Count != 1)
                throw new InvalidOperationException("The supplier cash account must identify one active bank account. Explicitly select the payment bank account to resolve a missing or ambiguous mapping.");
            return candidates[0];
        }
        if (!defaults.DefaultBankAccountId.HasValue) return null;
        if (!await banks.AnyAsync(bank => bank.Id == defaults.DefaultBankAccountId.Value, cancellationToken))
            throw new InvalidOperationException("The supplier ChequeBook default is not an active bank account in the current tenant.");
        return defaults.DefaultBankAccountId;
    }

    // Resolve through the existing Finance identity bridge: a linked partner can have
    // a different ID/code from the historical Supplier projection. Never match names.
    private async Task<BusinessPartnerPostingDefaultsDto?> GetPaymentPartnerDefaultsAsync(
        Supplier supplier, CancellationToken cancellationToken)
    {
        var hasLink = await _unitOfWork.Repository<ApSupplierIdentityLink>()
            .GetQueryable(link => link.TenantId == TenantId && !link.IsDeleted && link.SupplierId == supplier.Id)
            .AnyAsync(cancellationToken);
        var hasCandidate = hasLink || await _unitOfWork.Repository<BusinessPartner>()
            .GetQueryable(partner => partner.TenantId == TenantId && !partner.IsDeleted &&
                (partner.Id == supplier.Id ||
                 (!string.IsNullOrWhiteSpace(supplier.SupplierCode) && partner.PartnerCode == supplier.SupplierCode)))
            .AnyAsync(cancellationToken);
        // Legacy suppliers without a partner continue to use Finance defaults.
        if (!hasCandidate) return null;
        if (_apSupplierIdentityService == null)
            throw new InvalidOperationException("The AP supplier identity service is required to resolve business partner posting accounts.");

        var identity = await _apSupplierIdentityService.LookupAsync(supplier.Id, cancellationToken);
        if (identity.SupplierId != supplier.Id)
            throw new InvalidOperationException("The payment supplier identity does not match its business partner.");
        var partner = await _unitOfWork.Repository<BusinessPartner>()
            .GetQueryable(candidate => candidate.Id == identity.BusinessPartnerId &&
                candidate.TenantId == TenantId && !candidate.IsDeleted)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("The payment business partner was not found in the current tenant.");
        return BusinessPartnerPostingDefaults.FromPartner(partner);
    }
}
