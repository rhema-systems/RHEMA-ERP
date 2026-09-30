using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Finance;
using ErpSystem.Core.Interfaces.Finance;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.GL;

public sealed partial class FinanceSourceBookAuthorityService
{
    public async Task<FinanceSourceBookAuthorityResult> RetainExistingPostedOriginalAsync(
        FinanceSourceBookAuthorityFreezeRequest request, Guid retainedJournalEntryId,
        Guid? retainedFinancePostingEventId = null, CancellationToken cancellationToken = default)
    {
        var source = Normalize(request, allowLegacyStage: true);
        _ = Actor();
        if (retainedJournalEntryId == Guid.Empty || retainedFinancePostingEventId == Guid.Empty)
            throw new InvalidOperationException("SOURCE_BOOK_AUTHORITY_LEGACY_LINK_REQUIRED: posting links must be non-empty.");
        await RequireMutationScopeAndLockAsync(source, cancellationToken);
        var existing = await SourceAuthorities(source).Take(2).ToListAsync(cancellationToken);
        if (existing.Count != 0)
        {
            if (existing.Count != 1 || existing[0].OriginalJournalEntryId != retainedJournalEntryId ||
                retainedFinancePostingEventId.HasValue && existing[0].OriginalFinancePostingEventId != retainedFinancePostingEventId)
                throw new InvalidOperationException("SOURCE_BOOK_AUTHORITY_LEGACY_CONFLICT: evidence conflicts with existing authority.");
            await ValidateFrozenRequestAsync(existing[0], source, cancellationToken, allowLegacyDateOverride: true);
            await ValidateOriginalEvidenceAsync(existing[0], cancellationToken);
            return Map(existing[0]);
        }
        var journal = await _db.JournalEntries.AsNoTracking().SingleOrDefaultAsync(item =>
            item.TenantId == TenantId && item.Id == retainedJournalEntryId && !item.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("SOURCE_BOOK_AUTHORITY_LEGACY_JOURNAL_MISSING: retained journal was not found.");
        if (!string.Equals(journal.PostingStatus, "Posted", StringComparison.OrdinalIgnoreCase) ||
            journal.ReplicatedFromJournalEntryId.HasValue || journal.IsReversed || journal.ReversalJournalEntryId.HasValue ||
            !SameOriginModule(journal.SourceModule, source.OriginModuleCode) ||
            journal.SourceDocumentId != source.SourceDocumentId || !SameNormalized(journal.SourceDocumentType, source.SourceDocumentType))
            throw new InvalidOperationException("SOURCE_BOOK_AUTHORITY_LEGACY_JOURNAL_INVALID: journal must be posted, non-replica, unreversed, and exact-source.");
        var eventQuery = _db.FinancePostingEvents.AsNoTracking().Where(item =>
            item.TenantId == TenantId && item.JournalEntryId == retainedJournalEntryId &&
            item.SourceDocumentId == source.SourceDocumentId && item.PostingStatus == "Posted" && !item.IsDeleted &&
            item.SourceDocumentType.Trim().ToUpper() == source.SourceDocumentType &&
            item.PostingAction.Trim().ToUpper() == source.PostingAction &&
            (item.OriginModuleCode != null
                ? item.OriginModuleCode.Trim().ToUpper() == source.OriginModuleCode
                : source.OriginModuleCode == FinanceModuleLockCatalog.HumanResources
                    ? item.SourceModule.Trim().ToUpper() == "PAYROLL"
                    : source.OriginModuleCode == FinanceModuleLockCatalog.Inventory
                        ? item.SourceModule.Trim().ToUpper() == "INV" || item.SourceModule.Trim().ToUpper() == "INVENTORY"
                        : source.OriginModuleCode == FinanceModuleLockCatalog.Procurement
                            ? item.SourceModule.Trim().ToUpper() == "PROC" || item.SourceModule.Trim().ToUpper() == "PROCUREMENT"
                            : source.OriginModuleCode == FinanceModuleLockCatalog.Sales
                                ? item.SourceModule.Trim().ToUpper() == "SALES"
                                : item.SourceModule.Trim().ToUpper() != "PAYROLL" &&
                                    item.SourceModule.Trim().ToUpper() != "INV" && item.SourceModule.Trim().ToUpper() != "INVENTORY" &&
                                    item.SourceModule.Trim().ToUpper() != "PROC" && item.SourceModule.Trim().ToUpper() != "PROCUREMENT" &&
                                    item.SourceModule.Trim().ToUpper() != "SALES"));
        if (retainedFinancePostingEventId.HasValue)
            eventQuery = eventQuery.Where(item => item.Id == retainedFinancePostingEventId.Value);
        var events = await eventQuery.ToListAsync(cancellationToken);
        if (events.Count != 1)
            throw new InvalidOperationException("SOURCE_BOOK_AUTHORITY_LEGACY_EVENT_AMBIGUOUS: journal must resolve to one exact posted event.");
        var postingEvent = events[0];
        var transactionCurrency = NormalizeCurrency(postingEvent.PrimaryTransactionCurrencyCode ?? postingEvent.FunctionalCurrencyCode);
        if (transactionCurrency != source.TransactionCurrencyCode || postingEvent.AccountingBookId != journal.AccountingBookId ||
            !string.Equals(postingEvent.BookClassification, journal.BookClassification, StringComparison.Ordinal) ||
            postingEvent.PostingDate.Date != journal.EntryDate.Date)
            throw new InvalidOperationException("SOURCE_BOOK_AUTHORITY_LEGACY_COORDINATE_MISMATCH: event and journal coordinates differ.");
        var book = await _db.AccountingBooks.AsNoTracking().SingleOrDefaultAsync(item =>
            item.TenantId == TenantId && item.Id == postingEvent.AccountingBookId && !item.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("SOURCE_BOOK_AUTHORITY_LEGACY_BOOK_MISSING: accounting book was not found.");
        if (!string.Equals(book.Code, postingEvent.BookClassification, StringComparison.Ordinal) ||
            NormalizeCurrency(postingEvent.FunctionalCurrencyCode) != NormalizeCurrency(book.FunctionalCurrencyCode))
            throw new InvalidOperationException("SOURCE_BOOK_AUTHORITY_LEGACY_BOOK_MISMATCH: event snapshot differs from exact book.");
        var retainedSource = source with
        {
            EffectiveDate = postingEvent.PostingDate.Date,
            FreezeStage = FinanceSourceBookAuthorityFreezeStages.LegacyPosted,
            WorkflowInstanceId = null
        };
        var frozenAt = postingEvent.PostedAt ?? journal.PostingDate ?? postingEvent.RequestedAt;
        var entity = CreateAuthority(retainedSource, book.Id, book.Code,
            NormalizeCurrency(postingEvent.FunctionalCurrencyCode),
            FinanceSourceBookAuthoritySelectionBases.RetainedPostedOriginal, 1, null, null, frozenAt);
        entity.OriginalFinancePostingEventId = postingEvent.Id;
        entity.OriginalJournalEntryId = journal.Id;
        entity.BoundAtUtc = frozenAt;
        entity.AuthorityFingerprint = Fingerprint(entity, [], $"LEGACY:{postingEvent.Id:D}:{journal.Id:D}");
        _db.FinanceSourceBookAuthorities.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<FinanceSourceBookAuthorityResult> RequireForPostingAsync(
        FinanceSourceBookAuthorityFreezeRequest request, CancellationToken cancellationToken = default)
    {
        var source = Normalize(request);
        var candidates = await SourceAuthorities(source).Include(item => item.Origins)
            .OrderByDescending(item => item.AuthorityVersion).Take(2).ToListAsync(cancellationToken);
        var authority = source.WorkflowInstanceId.HasValue
            ? candidates.SingleOrDefault(item => item.SourceWorkflowInstanceId == source.WorkflowInstanceId)
            : candidates.Count == 1 ? candidates[0] : null;
        if (authority is null)
            throw new InvalidOperationException("SOURCE_BOOK_AUTHORITY_REQUIRED: exact source/workflow authority was not found.");
        if (authority.Id != candidates[0].Id)
            throw new InvalidOperationException("SOURCE_BOOK_AUTHORITY_STALE: only the latest governed source authority can post.");
        await ValidateFrozenRequestAsync(authority, source, cancellationToken, ignoreFreezeStage: true);
        await RequireWorkflowOwnershipAsync(source, cancellationToken, requireCompletedForPosting: true);
        return Map(authority);
    }

    public async Task<FinanceSourceBookAuthorityResult> BindOriginalPostingAsync(
        Guid authorityId, Guid financePostingEventId, Guid journalEntryId,
        CancellationToken cancellationToken = default)
    {
        if (authorityId == Guid.Empty || financePostingEventId == Guid.Empty || journalEntryId == Guid.Empty)
            throw new InvalidOperationException("SOURCE_BOOK_AUTHORITY_BINDING_REQUIRED: authority, event, and journal ids are required.");
        RequireSerializableMutationScope();
        var authority = await _db.FinanceSourceBookAuthorities.SingleOrDefaultAsync(item =>
            item.TenantId == TenantId && item.Id == authorityId && !item.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("SOURCE_BOOK_AUTHORITY_REQUIRED: authority was not found.");
        await AcquireSourceLockAsync(ToSource(authority), cancellationToken);
        var versions = await SourceAuthorities(ToSource(authority)).OrderByDescending(item => item.AuthorityVersion)
            .Take(2).ToListAsync(cancellationToken);
        if (versions.Count == 0 || versions[0].Id != authority.Id)
            throw new InvalidOperationException("SOURCE_BOOK_AUTHORITY_STALE: only the latest governed source authority can bind posting evidence.");
        await ValidateRetainedCoordinateAsync(authority, true, cancellationToken);
        await RequireWorkflowOwnershipAsync(ToSource(authority), cancellationToken, requireCompletedForPosting: true);
        if (authority.OriginalFinancePostingEventId.HasValue || authority.OriginalJournalEntryId.HasValue)
        {
            if (authority.OriginalFinancePostingEventId == financePostingEventId && authority.OriginalJournalEntryId == journalEntryId)
            {
                await ValidateOriginalEvidenceAsync(authority, cancellationToken);
                return Map(authority);
            }
            throw new InvalidOperationException("SOURCE_BOOK_AUTHORITY_BINDING_IMMUTABLE: original evidence cannot be replaced.");
        }
        var postingEvent = await _db.FinancePostingEvents.AsNoTracking().SingleOrDefaultAsync(item =>
            item.TenantId == TenantId && item.Id == financePostingEventId && item.JournalEntryId == journalEntryId &&
            item.SourceDocumentId == authority.SourceDocumentId && item.PostingStatus == "Posted" && !item.IsDeleted,
            cancellationToken) ?? throw new InvalidOperationException("SOURCE_BOOK_AUTHORITY_EVENT_INVALID: exact posted event was not found.");
        var journal = await _db.JournalEntries.AsNoTracking().SingleOrDefaultAsync(item =>
            item.TenantId == TenantId && item.Id == journalEntryId && item.PostingStatus == "Posted" &&
            !item.IsDeleted && !item.IsReversed && item.ReversalJournalEntryId == null &&
            item.ReplicatedFromJournalEntryId == null, cancellationToken)
            ?? throw new InvalidOperationException("SOURCE_BOOK_AUTHORITY_JOURNAL_INVALID: exact posted non-replica journal was not found.");
        var eventCurrency = NormalizeCurrency(postingEvent.PrimaryTransactionCurrencyCode ?? postingEvent.FunctionalCurrencyCode);
        if (!SameNormalized(postingEvent.SourceDocumentType, authority.SourceDocumentType) ||
            !SameNormalized(postingEvent.PostingAction, authority.PostingAction) ||
            FinanceModuleLockCatalog.ResolveOriginModuleCode(postingEvent.SourceModule, postingEvent.OriginModuleCode) != authority.OriginModuleCode ||
            !SameOriginModule(journal.SourceModule, authority.OriginModuleCode) ||
            journal.SourceDocumentId != authority.SourceDocumentId || !SameNormalized(journal.SourceDocumentType, authority.SourceDocumentType) ||
            postingEvent.AccountingBookId != authority.AccountingBookId || journal.AccountingBookId != authority.AccountingBookId ||
            postingEvent.BookClassification != authority.AccountingBookCode || journal.BookClassification != authority.AccountingBookCode ||
            NormalizeCurrency(postingEvent.FunctionalCurrencyCode) != authority.FunctionalCurrencyCode ||
            eventCurrency != authority.TransactionCurrencyCode || postingEvent.PostingDate.Date != authority.EffectiveDate.Date ||
            journal.EntryDate.Date != authority.EffectiveDate.Date)
            throw new InvalidOperationException("SOURCE_BOOK_AUTHORITY_POSTING_MISMATCH: posted event/journal differs from frozen authority.");
        authority.OriginalFinancePostingEventId = postingEvent.Id;
        authority.OriginalJournalEntryId = journal.Id;
        authority.BoundByUserId = Actor();
        authority.BoundAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return Map(authority);
    }

    public async Task<FinanceSourceBookAuthorityResult> RequireBoundOriginalAsync(
        Guid authorityId, CancellationToken cancellationToken = default)
    {
        var authority = await _db.FinanceSourceBookAuthorities.AsNoTracking().Include(item => item.Origins)
            .SingleOrDefaultAsync(item => item.TenantId == TenantId && item.Id == authorityId && !item.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("SOURCE_BOOK_AUTHORITY_REQUIRED: authority was not found.");
        if (!authority.OriginalFinancePostingEventId.HasValue || !authority.OriginalJournalEntryId.HasValue)
            throw new InvalidOperationException("SOURCE_BOOK_AUTHORITY_ORIGINAL_REQUIRED: original evidence is not bound.");
        await ValidateRetainedCoordinateAsync(authority, false, cancellationToken);
        await ValidateOriginalEvidenceAsync(authority, cancellationToken);
        return Map(authority);
    }
}
