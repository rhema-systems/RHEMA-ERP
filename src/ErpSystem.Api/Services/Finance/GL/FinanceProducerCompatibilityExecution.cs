using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.GL;

/// <summary>
/// Finance-owned legacy compatibility evidence. Owner modules receive one already-resolved representation;
/// they never inspect the frozen book set or choose a book themselves.
/// </summary>
internal sealed record FinanceProducerApprovedExecutionResult(
    Guid AccountingEventId,
    string AccountingEventRequestFingerprint,
    string Status,
    Guid FinancePostingEventId,
    Guid JournalEntryId);

internal sealed record FinanceProducerIntentGroupMemberExecutionResult(
    int MemberOrder,
    string MemberFingerprint,
    Guid AccountingEventId,
    string AccountingEventRequestFingerprint,
    Guid FinancePostingEventId,
    Guid JournalEntryId);

internal sealed record FinanceProducerIntentGroupApprovedExecutionResult(
    Guid ProducerIntentGroupId,
    string GroupFingerprint,
    string Status,
    IReadOnlyList<FinanceProducerIntentGroupMemberExecutionResult> Members);

internal static class FinanceProducerCompatibilityAuthority
{
    internal static async Task<FinanceProducerApprovedExecutionResult> ResolveAsync(
        ApplicationDbContext db,
        Guid tenantId,
        AccountingEventDto accountingEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(accountingEvent);
        if (tenantId == Guid.Empty || accountingEvent.Id == Guid.Empty)
            throw new InvalidOperationException("PRODUCER_COMPATIBILITY_EVENT_IDENTITY_REQUIRED: tenant and AccountingEvent identities are required.");
        if (!string.Equals(accountingEvent.Status, "Posted", StringComparison.Ordinal))
            throw new InvalidOperationException("PRODUCER_COMPATIBILITY_EVENT_NOT_POSTED: compatibility evidence requires a posted AccountingEvent.");

        // This is the same Finance-owned authority used by the posting engine when it selects the
        // exact default-book representation. IsDefault is restricted to the
        // tenant's PrimaryFull book and becomes immutable after accounting use. Do not re-filter
        // lifecycle flags here: exact retries and reversals may validly return historical frozen evidence.
        var compatibilityBookIds = await db.AccountingBooks.AsNoTracking()
            .Where(book => book.TenantId == tenantId && book.IsDefault && !book.IsDeleted)
            .Select(book => book.Id)
            .Take(2)
            .ToListAsync(cancellationToken);
        if (compatibilityBookIds.Count != 1)
            throw new InvalidOperationException(
                "PRIMARY_BOOK_AUTHORITY_AMBIGUOUS: Exactly one tenant default book must own the legacy compatibility representation.");

        var representations = accountingEvent.Postings
            .Where(posting => posting.AccountingBookId == compatibilityBookIds[0])
            .ToList();
        if (representations.Count == 0)
            throw new InvalidOperationException(
                "PRODUCER_COMPATIBILITY_REPRESENTATION_ABSENT: the frozen AccountingEvent book set has no default-book representation.");
        if (representations.Count != 1)
            throw new InvalidOperationException(
                "PRODUCER_COMPATIBILITY_REPRESENTATION_AMBIGUOUS: the frozen AccountingEvent book set has duplicate default-book representations.");

        var representation = representations[0];
        if (!string.Equals(representation.Status, "Posted", StringComparison.Ordinal)
            || !representation.FinancePostingEventId.HasValue
            || representation.FinancePostingEventId == Guid.Empty
            || !representation.JournalEntryId.HasValue
            || representation.JournalEntryId == Guid.Empty)
            throw new InvalidOperationException(
                "PRODUCER_COMPATIBILITY_REPRESENTATION_INCOMPLETE: the default-book representation is not completely posted.");

        return new FinanceProducerApprovedExecutionResult(
            accountingEvent.Id,
            accountingEvent.RequestFingerprint,
            accountingEvent.Status,
            representation.FinancePostingEventId.Value,
            representation.JournalEntryId.Value);
    }

    internal static async Task<FinanceProducerIntentGroupApprovedExecutionResult> ResolveGroupAsync(
        ApplicationDbContext db,
        Guid tenantId,
        ProducerIntentGroupDto group,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(group);
        if (!string.Equals(group.Status, "Posted", StringComparison.Ordinal))
            throw new InvalidOperationException("PRODUCER_COMPATIBILITY_GROUP_NOT_POSTED: compatibility evidence requires a posted producer-intent group.");

        var ordered = group.Members.OrderBy(member => member.MemberOrder).ToList();
        if (ordered.Count == 0
            || ordered.Select(member => member.MemberOrder).Distinct().Count() != ordered.Count
            || ordered.Select(member => member.AccountingEvent.Id).Distinct().Count() != ordered.Count)
            throw new InvalidOperationException(
                "PRODUCER_COMPATIBILITY_GROUP_MEMBER_CONFLICT: compatibility results require distinct ordered group members.");

        var results = new List<FinanceProducerIntentGroupMemberExecutionResult>(ordered.Count);
        foreach (var member in ordered)
        {
            var execution = await ResolveAsync(db, tenantId, member.AccountingEvent, cancellationToken);
            results.Add(new FinanceProducerIntentGroupMemberExecutionResult(
                member.MemberOrder, member.MemberFingerprint,
                execution.AccountingEventId, execution.AccountingEventRequestFingerprint,
                execution.FinancePostingEventId, execution.JournalEntryId));
        }

        return new FinanceProducerIntentGroupApprovedExecutionResult(
            group.Id, group.GroupFingerprint, group.Status, results);
    }
}
