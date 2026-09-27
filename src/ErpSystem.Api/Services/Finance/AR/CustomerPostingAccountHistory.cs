using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.AR;

/// <summary>Recovers account defaults for a retry without changing its document or canonical request.</summary>
internal sealed class CustomerPostingAccountHistory
{
    private readonly IReadOnlyList<AccountTransaction> _lines;

    private CustomerPostingAccountHistory(IReadOnlyList<AccountTransaction> lines) => _lines = lines;

    public static async Task<CustomerPostingAccountHistory?> LoadAsync(
        IUnitOfWork unitOfWork, Guid tenantId, Guid? journalId, string documentType,
        Guid documentId, CancellationToken cancellationToken)
    {
        if (!journalId.HasValue) return null;
        var events = await unitOfWork.Repository<FinancePostingEvent>().GetQueryable(item =>
                item.TenantId == tenantId && !item.IsDeleted && item.SourceModule == "AR" &&
                item.SourceDocumentType == documentType && item.SourceDocumentId == documentId &&
                item.PostingAction == "Post" && item.PostingStatus == "Posted" && item.JournalEntryId == journalId)
            .AsNoTracking().Take(2).ToListAsync(cancellationToken);
        var journalMatches = await unitOfWork.Repository<JournalEntry>().GetQueryable(item =>
                item.Id == journalId && item.TenantId == tenantId && !item.IsDeleted && !item.IsReversed &&
                item.PostingStatus == "Posted" && item.SourceModule == "AR" &&
                item.SourceDocumentType == documentType && item.SourceDocumentId == documentId)
            .AsNoTracking().AnyAsync(cancellationToken);
        if (events.Count != 1 || !journalMatches)
            throw new InvalidOperationException("AR_POSTED_ACCOUNT_EVIDENCE_INVALID: the linked journal is not the original posted journal for this tenant and document.");
        var lines = await unitOfWork.Repository<AccountTransaction>().GetQueryable(item =>
                item.TenantId == tenantId && item.JournalEntryId == journalId && !item.IsDeleted)
            .AsNoTracking().ToListAsync(cancellationToken);
        return new CustomerPostingAccountHistory(lines);
    }

    public Guid Account(string transactionTag, Guid? sourceLineId = null)
    {
        var accounts = _lines.Where(item => item.TransactionTag == transactionTag &&
                (!sourceLineId.HasValue || item.SourceDocumentLineId == sourceLineId))
            .Select(item => item.AccountId).Distinct().ToArray();
        if (accounts.Length != 1)
            throw new InvalidOperationException($"AR_POSTED_ACCOUNT_EVIDENCE_INVALID: original {transactionTag} account evidence is missing or ambiguous.");
        return accounts[0];
    }
}
