using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.AP;

public partial class VendorInvoiceService
{
    public async Task<VendorInvoiceDistributionDto> GetDistributionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var invoice = await _unitOfWork.Repository<VendorInvoice>().GetQueryable(value =>
                value.TenantId == TenantId && value.Id == id && !value.IsDeleted)
            .AsNoTracking().Include(value => value.Supplier).Include(value => value.LineItems)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Vendor invoice was not found for this tenant.");
        var posting = await _unitOfWork.Repository<FinancePostingEvent>().GetQueryable(value =>
                value.TenantId == TenantId && !value.IsDeleted && value.SourceModule == "AP" &&
                value.SourceDocumentType == "VendorInvoice" && value.SourceDocumentId == id &&
                value.PostingAction == "Post" && value.PostingStatus == "Posted" && value.JournalEntryId.HasValue)
            .AsNoTracking().OrderByDescending(value => value.PostedAt).FirstOrDefaultAsync(cancellationToken);
        if (posting?.JournalEntryId is not null && invoice.JournalEntryId.HasValue && posting.JournalEntryId != invoice.JournalEntryId)
            throw new InvalidOperationException("The invoice journal reference differs from its original posting event. Reconcile the source before viewing distribution.");
        var journalId = posting?.JournalEntryId ?? invoice.JournalEntryId;
        if (journalId.HasValue)
        {
            // Always show the original journal, including after a reversal. Never recalculate
            // a historical account from today's supplier/item default or receipt balance.
            var journal = await _unitOfWork.Repository<JournalEntry>().GetQueryable(value =>
                    value.TenantId == TenantId && value.Id == journalId.Value && !value.IsDeleted)
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException("The invoice's original posted journal is unavailable.");
            if (!string.Equals(journal.PostingStatus, "Posted", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The invoice's referenced journal has not been posted.");
            var transactions = await _unitOfWork.Repository<AccountTransaction>().GetQueryable(value =>
                    value.TenantId == TenantId && value.JournalEntryId == journalId.Value && !value.IsDeleted)
                .AsNoTracking().OrderBy(value => value.LineNumber).ThenBy(value => value.Id).ToListAsync(cancellationToken);
            if (transactions.Count == 0)
                throw new InvalidOperationException("The invoice's original journal has no available distribution lines.");
            var accountIds = transactions.Select(value => value.AccountId).Distinct().ToArray();
            var accounts = await _unitOfWork.Repository<Account>().GetQueryable(value =>
                    value.TenantId == TenantId && accountIds.Contains(value.Id))
                .AsNoTracking().ToDictionaryAsync(value => value.Id, cancellationToken);
            return new VendorInvoiceDistributionDto
            {
                InvoiceId = id, Status = "Posted", Currency = posting?.FunctionalCurrencyCode ?? transactions[0].FunctionalCurrencyCode,
                Basis = "Original posted journal", JournalEntryId = journal.Id, JournalEntryNumber = journal.JournalEntryNumber,
                Lines = transactions.Select(value => new VendorInvoiceDistributionLineDto
                {
                    LineId = value.Id.ToString(), SourceDocumentLineId = value.SourceDocumentLineId,
                    AccountId = value.AccountId, AccountCode = accounts.GetValueOrDefault(value.AccountId)?.AccountNumber ?? "Unavailable",
                    AccountName = accounts.GetValueOrDefault(value.AccountId)?.AccountName ?? string.Empty,
                    Type = DistributionType(value.TransactionTag), Source = "Posted journal", Description = value.Description,
                    Debit = value.DebitAmount, Credit = value.CreditAmount
                }).ToList()
            };
        }

        // Approval, budget reservation and tax-review actions are not performed by a preview.
        // All account/source/value resolution is identical to the actual posting builder.
        var request = await BuildApInvoiceDistributionRequestAsync(invoice, Array.Empty<Guid>(), null, cancellationToken);
        var ids = request.Lines.Select(value => value.AccountId).Distinct().ToArray();
        var proposedAccounts = await _unitOfWork.Repository<Account>().GetQueryable(value =>
                value.TenantId == TenantId && ids.Contains(value.Id))
            .AsNoTracking().ToDictionaryAsync(value => value.Id, cancellationToken);
        return new VendorInvoiceDistributionDto
        {
            InvoiceId = id, Currency = request.FunctionalCurrencyCode, Basis = "Current invoice posting distribution",
            Lines = request.Lines.Select(value => new VendorInvoiceDistributionLineDto
            {
                LineId = $"{id:N}:{value.LineNumber}", SourceDocumentLineId = value.SourceDocumentLineId,
                AccountId = value.AccountId, AccountCode = proposedAccounts[value.AccountId].AccountNumber,
                AccountName = proposedAccounts[value.AccountId].AccountName,
                Type = DistributionType(value.TransactionTag), Source = "Invoice posting rules", Description = value.Description,
                Debit = value.DebitAmount, Credit = value.CreditAmount
            }).ToList()
        };
    }

    private static string DistributionType(string? tag) => tag switch
    {
        "AP-Control" => "Accounts payable",
        "AP-GRV" => "Receipt accrual",
        "AP-LANDED-COST-ACCRUAL" => "Landed-cost accrual",
        "AP-Discount" => "Purchase discount",
        "AP-Expense" => "Expense",
        "AP-Inventory" => "Inventory",
        "AP-FixedAsset" => "Fixed asset",
        "AP-MigrationClearing" => "Opening balance clearing",
        _ when tag?.StartsWith("AP-Tax-", StringComparison.Ordinal) == true => "Input tax",
        _ => tag ?? "Invoice"
    };
}
