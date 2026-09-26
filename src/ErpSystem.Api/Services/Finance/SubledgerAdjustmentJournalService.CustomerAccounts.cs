using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance;

public partial class SubledgerAdjustmentJournalService
{
    private static bool IsCustomerAccountPurpose(string? purpose) => purpose is
        SubledgerAdjustmentPurposes.FinanceCharge or SubledgerAdjustmentPurposes.Writeoff or SubledgerAdjustmentPurposes.OverpaymentWriteoff;

    private async Task<SubledgerAdjustmentJournal?> FindCustomerAdjustmentReplayAsync(
        CreateSubledgerAdjustmentJournalDto dto, CancellationToken token)
    {
        if (!dto.RequestId.HasValue || dto.RequestId == Guid.Empty) return null;
        var existing = await LoadAdjustmentAsync(dto.RequestId.Value, true, token);
        if (existing == null) return null;
        if (existing.Module != NormalizeModule(dto.Module, false) || existing.Purpose != NormalizePurpose(dto.Purpose, false) ||
            existing.CustomerId != dto.CustomerId || existing.SupplierId != dto.SupplierId ||
            existing.AdjustmentType != NormalizeAdjustmentType(dto.AdjustmentType) || existing.Amount != dto.Amount ||
            existing.AdjustmentDate != dto.AdjustmentDate.Date || existing.DueDate != dto.DueDate?.Date ||
            existing.CurrencyCode != NormalizeCurrency(dto.CurrencyCode) || existing.ExchangeRate != dto.ExchangeRate ||
            existing.Reason != dto.Reason.Trim() || existing.Reference != NullTrim(dto.Reference) || existing.Notes != NullTrim(dto.Notes) ||
            (dto.ContraAccountId != Guid.Empty && dto.ContraAccountId != existing.ContraAccountId) ||
            existing.Status != SubledgerAdjustmentStatuses.Posted || !existing.JournalEntryId.HasValue)
            throw new InvalidOperationException("This request ID already identifies a different or reversed adjustment. Reload the original result.");
        return existing;
    }

    private static string? NullTrim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task<Guid> ResolveCustomerAdjustmentAccountAsync(CreateSubledgerAdjustmentJournalDto dto,
        string purpose, string direction, BusinessPartner? customer, SubledgerAdjustmentJournal? original, CancellationToken token)
    {
        if (!IsCustomerAccountPurpose(purpose)) return dto.ContraAccountId;
        if (dto.Module.ToUpperInvariant() != "AR" || customer == null || !customer.IsActive || customer.IsBlacklisted)
            throw new InvalidOperationException("Customer account adjustments require an active customer in Accounts Receivable.");
        if (original != null)
        {
            if (!original.ControlAccountId.HasValue || !original.JournalEntryId.HasValue)
                throw new InvalidOperationException("Original posting account evidence is required for reversal.");
            return original.ContraAccountId;
        }
        if (!dto.RequestId.HasValue || dto.RequestId == Guid.Empty)
            throw new InvalidOperationException("A stable request ID is required for customer account adjustments.");
        if (dto.Amount != Math.Round(dto.Amount, 2, MidpointRounding.AwayFromZero))
            throw new InvalidOperationException("Customer balance adjustments support two decimal places.");
        var requiredDirection = purpose == SubledgerAdjustmentPurposes.Writeoff ? "Credit" : "Debit";
        if (direction != requiredDirection)
            throw new InvalidOperationException($"{purpose} requires an AR {requiredDirection.ToLowerInvariant()}.");
        var baseCurrency = NormalizeCurrency(await _tenantSettingsService.GetBaseCurrencyAsync()) ?? "GHS";
        if (NormalizeCurrency(dto.CurrencyCode) != baseCurrency || dto.ExchangeRate != 1m)
            throw new InvalidOperationException("Customer balance adjustments use the functional currency at rate 1; foreign-currency source settlement uses its document workflow.");
        // Check posted source balances under the caller's serializable transaction.
        // The counterparty cache alone must never authorize disposal of a balance.
        var invoices = await _context.Set<Invoice>().Where(i => i.TenantId == TenantId &&
            i.BusinessPartnerId == customer.Id && !i.IsDeleted && i.JournalEntryId.HasValue &&
            (i.Status == InvoiceStatus.Sent || i.Status == InvoiceStatus.PartiallyPaid ||
             i.Status == InvoiceStatus.Paid || i.Status == InvoiceStatus.Overdue))
            .Select(i => (i.TotalAmount - i.PaidAmount - i.CreditedAmount) * i.ExchangeRate).ToListAsync(token);
        var adjustments = await _context.SubledgerAdjustmentJournals.Where(a => a.TenantId == TenantId &&
            a.Module == "AR" && a.CustomerId == customer.Id && !a.IsDeleted && a.JournalEntryId.HasValue &&
            (a.Status == SubledgerAdjustmentStatuses.Posted || a.Status == SubledgerAdjustmentStatuses.Reversed))
            .Select(a => a.AdjustmentType == "Debit" ? a.BaseCurrencyAmount : -a.BaseCurrencyAmount).ToListAsync(token);
        var balance = Math.Round(invoices.Sum() + adjustments.Sum(), 2, MidpointRounding.AwayFromZero);
        if (Math.Round(customer.OutstandingBalance ?? 0m, 2, MidpointRounding.AwayFromZero) != balance)
            throw new InvalidOperationException("The customer balance does not reconcile to posted AR sources. Reconcile it before posting a customer balance adjustment.");
        if (purpose == SubledgerAdjustmentPurposes.Writeoff && (balance <= 0m || dto.Amount > balance))
            throw new InvalidOperationException("The writeoff cannot exceed the customer's outstanding AR balance.");
        if (purpose == SubledgerAdjustmentPurposes.OverpaymentWriteoff && (balance >= 0m || dto.Amount > -balance))
            throw new InvalidOperationException("The overpayment writeoff cannot exceed the customer's AR credit balance. Customer advances are settled separately.");
        var mapping = purpose switch
        {
            SubledgerAdjustmentPurposes.FinanceCharge => customer.CustomerFinanceChargesAccountId,
            SubledgerAdjustmentPurposes.Writeoff => customer.CustomerWriteoffAccountId,
            _ => customer.CustomerOverpaymentWriteoffAccountId
        };
        var accountId = dto.ContraAccountId != Guid.Empty ? dto.ContraAccountId : mapping;
        var account = await _context.Accounts.SingleOrDefaultAsync(a => a.Id == accountId && a.TenantId == TenantId && !a.IsDeleted, token);
        var type = purpose == SubledgerAdjustmentPurposes.Writeoff ? AccountType.Expense : AccountType.Revenue;
        if (account == null || account.Status != AccountStatus.Active || account.IsControlAccount || !account.AllowDirectPosting || account.AccountType != type)
            throw new InvalidOperationException($"Configure an active {type} account for customer {purpose}.");
        return account.Id;
    }
}
