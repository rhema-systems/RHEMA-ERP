using System.Data;
using System.Security.Cryptography;
using System.Text;
using ErpSystem.Api.Services.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Finance;
using ErpSystem.Core.Interfaces.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ErpSystem.Api.Services.Finance.GL;

public sealed partial class FinanceSourceBookAuthorityService
{
    private async Task ValidateFrozenRequestAsync(FinanceSourceBookAuthority authority, NormalizedSource source,
        CancellationToken cancellationToken, bool allowLegacyDateOverride = false, bool ignoreFreezeStage = false)
    {
        RequireSameSourceIdentity(authority, source, compareWorkflow: true);
        if ((!allowLegacyDateOverride && authority.EffectiveDate.Date != source.EffectiveDate) ||
            authority.TransactionCurrencyCode != source.TransactionCurrencyCode ||
            (!ignoreFreezeStage && authority.FreezeStage != source.FreezeStage))
            throw new InvalidOperationException("SOURCE_BOOK_AUTHORITY_REQUEST_MISMATCH: request differs from frozen evidence.");
        await ValidateRetainedCoordinateAsync(authority, true, cancellationToken);
    }

    private async Task ValidateRetainedCoordinateAsync(FinanceSourceBookAuthority authority, bool requireActiveBook,
        CancellationToken cancellationToken)
    {
        var book = await _db.AccountingBooks.AsNoTracking().SingleOrDefaultAsync(item =>
            item.TenantId == TenantId && item.Id == authority.AccountingBookId && !item.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("SOURCE_BOOK_AUTHORITY_BOOK_MISSING: retained book no longer exists.");
        if (book.Code != authority.AccountingBookCode || NormalizeCurrency(book.FunctionalCurrencyCode) != authority.FunctionalCurrencyCode ||
            requireActiveBook && (book.BookType != AccountingBookType.PrimaryFull ||
                book.LifecycleStatus != AccountingBookLifecycleStatus.Active || !book.IsActive || !book.AllowsPosting))
            throw new InvalidOperationException("SOURCE_BOOK_AUTHORITY_BOOK_INVALID: retained exact book coordinate is invalid.");
        if (requireActiveBook && await RequireFunctionalCurrencyAsync(cancellationToken) != authority.FunctionalCurrencyCode)
            throw new InvalidOperationException("SOURCE_BOOK_AUTHORITY_FUNCTIONAL_CURRENCY_CHANGED: tenant evidence differs from frozen authority.");
        var origins = authority.Origins.Count != 0
            ? authority.Origins.ToList()
            : await _db.FinanceSourceBookAuthorityOrigins.AsNoTracking()
                .Where(item => item.TenantId == TenantId && item.FinanceSourceBookAuthorityId == authority.Id && !item.IsDeleted)
                .ToListAsync(cancellationToken);
        var suffix = authority.SelectionBasis == FinanceSourceBookAuthoritySelectionBases.RetainedPostedOriginal
            ? $"LEGACY:{authority.OriginalFinancePostingEventId:D}:{authority.OriginalJournalEntryId:D}" : null;
        if (Fingerprint(authority, origins, suffix) != authority.AuthorityFingerprint)
            throw new InvalidOperationException("SOURCE_BOOK_AUTHORITY_FINGERPRINT_INVALID: retained evidence failed integrity validation.");
    }

    private async Task ValidateOriginalEvidenceAsync(FinanceSourceBookAuthority authority, CancellationToken cancellationToken)
    {
        var eventId = authority.OriginalFinancePostingEventId
            ?? throw new InvalidOperationException("SOURCE_BOOK_AUTHORITY_ORIGINAL_REQUIRED: posting event is not bound.");
        var journalId = authority.OriginalJournalEntryId
            ?? throw new InvalidOperationException("SOURCE_BOOK_AUTHORITY_ORIGINAL_REQUIRED: journal is not bound.");
        var postingEvent = await _db.FinancePostingEvents.AsNoTracking().SingleOrDefaultAsync(item =>
            item.TenantId == TenantId && item.Id == eventId && item.JournalEntryId == journalId &&
            item.SourceDocumentId == authority.SourceDocumentId && item.PostingStatus == "Posted" && !item.IsDeleted,
            cancellationToken) ?? throw new InvalidOperationException("SOURCE_BOOK_AUTHORITY_ORIGINAL_EVENT_INVALID: exact source event is unavailable.");
        var journal = await _db.JournalEntries.AsNoTracking().SingleOrDefaultAsync(item =>
            item.TenantId == TenantId && item.Id == journalId && item.PostingStatus == "Posted" && !item.IsDeleted &&
            !item.IsReversed && item.ReversalJournalEntryId == null && item.ReplicatedFromJournalEntryId == null, cancellationToken)
            ?? throw new InvalidOperationException("SOURCE_BOOK_AUTHORITY_ORIGINAL_JOURNAL_INVALID: exact non-replica, unreversed journal is unavailable.");
        if (!SameNormalized(postingEvent.SourceDocumentType, authority.SourceDocumentType) ||
            !SameNormalized(postingEvent.PostingAction, authority.PostingAction) ||
            FinanceModuleLockCatalog.ResolveOriginModuleCode(postingEvent.SourceModule, postingEvent.OriginModuleCode) != authority.OriginModuleCode ||
            !SameOriginModule(journal.SourceModule, authority.OriginModuleCode) ||
            journal.SourceDocumentId != authority.SourceDocumentId || !SameNormalized(journal.SourceDocumentType, authority.SourceDocumentType) ||
            postingEvent.AccountingBookId != authority.AccountingBookId || journal.AccountingBookId != authority.AccountingBookId ||
            postingEvent.BookClassification != authority.AccountingBookCode || journal.BookClassification != authority.AccountingBookCode ||
            NormalizeCurrency(postingEvent.FunctionalCurrencyCode) != authority.FunctionalCurrencyCode ||
            NormalizeCurrency(postingEvent.PrimaryTransactionCurrencyCode ?? postingEvent.FunctionalCurrencyCode) != authority.TransactionCurrencyCode ||
            postingEvent.PostingDate.Date != authority.EffectiveDate.Date || journal.EntryDate.Date != authority.EffectiveDate.Date)
            throw new InvalidOperationException("SOURCE_BOOK_AUTHORITY_ORIGINAL_MISMATCH: original posting no longer matches retained authority.");
    }

    private async Task<PrimaryCoordinate> ResolvePrimaryAsync(DateTime effectiveDate, CancellationToken cancellationToken)
    {
        _ = effectiveDate; // Primary is perpetual under Accounting Book Model V2.
        var functional = await RequireFunctionalCurrencyAsync(cancellationToken);
        var books = await _db.AccountingBooks.AsNoTracking().Where(item => item.TenantId == TenantId && !item.IsDeleted &&
                item.IsDefault && item.BookType == AccountingBookType.PrimaryFull)
            .Take(2).ToListAsync(cancellationToken);
        if (books.Count != 1)
            throw new InvalidOperationException("SOURCE_BOOK_AUTHORITY_PRIMARY_AMBIGUOUS: exactly one perpetual default PrimaryFull book is required.");
        var book = books[0];
        if (book.LifecycleStatus != AccountingBookLifecycleStatus.Active || !book.IsActive || !book.AllowsPosting ||
            book.EffectiveFromUtc.HasValue || book.EffectiveToUtc.HasValue ||
            !CanonicalBookCode(book.Code) || NormalizeCurrency(book.FunctionalCurrencyCode) != functional)
            throw new InvalidOperationException("SOURCE_BOOK_AUTHORITY_PRIMARY_INVALID: perpetual Primary must be active, postable, canonical, and currency-matched.");
        return new PrimaryCoordinate(book.Id, book.Code, functional, FinanceSourceBookAuthoritySelectionBases.DefaultPrimary);
    }

    private async Task<string> RequireFunctionalCurrencyAsync(CancellationToken cancellationToken)
    {
        var settings = await _db.FinanceSettings.AsNoTracking().Where(item => item.TenantId == TenantId && !item.IsDeleted)
            .Select(item => item.BaseCurrency).Take(2).ToListAsync(cancellationToken);
        var tenants = await _db.Tenants.AsNoTracking().Where(item => item.Id == TenantId && !item.IsDeleted)
            .Select(item => item.BaseCurrency).Take(2).ToListAsync(cancellationToken);
        if (settings.Count != 1 || tenants.Count != 1)
            throw new InvalidOperationException("SOURCE_BOOK_AUTHORITY_FUNCTIONAL_CURRENCY_AMBIGUOUS: one FinanceSettings and tenant row are required.");
        var settingsCurrency = NormalizeCurrency(settings[0]);
        var tenantCurrency = NormalizeCurrency(tenants[0]);
        if (settingsCurrency != tenantCurrency)
            throw new InvalidOperationException("SOURCE_BOOK_AUTHORITY_FUNCTIONAL_CURRENCY_MISMATCH: tenant and FinanceSettings must agree.");
        return settingsCurrency;
    }

    private async Task<WorkflowApprovalEvidence?> RequireWorkflowOwnershipAsync(NormalizedSource source, CancellationToken cancellationToken,
        bool requireCompletedForPosting = false)
    {
        if (!source.WorkflowInstanceId.HasValue) return null;
        var rows = await (from workflow in _db.WorkflowInstances.AsNoTracking()
                join entityType in _db.WorkflowEntityTypes.AsNoTracking()
                    on new { workflow.TenantId, Id = workflow.EntityTypeId }
                    equals new { entityType.TenantId, entityType.Id }
                where workflow.TenantId == TenantId && workflow.Id == source.WorkflowInstanceId &&
                    !workflow.IsDeleted && !entityType.IsDeleted
                select new { Workflow = workflow, EntityTypeCode = entityType.Code, entityType.IsActive })
            .Take(2).ToListAsync(cancellationToken);
        if (rows.Count != 1 || rows[0].Workflow.EntityId != source.SourceDocumentId || !rows[0].IsActive ||
            !SameNormalized(rows[0].EntityTypeCode, source.SourceDocumentType))
            throw new InvalidOperationException("SOURCE_BOOK_AUTHORITY_WORKFLOW_INVALID: workflow must own the exact source identity and type.");
        var workflowEvidence = rows[0].Workflow;
        var completed = workflowEvidence.Status == WorkflowInstanceStatus.Completed && workflowEvidence.CompletedDate.HasValue;
        var acceptable = requireCompletedForPosting || source.FreezeStage is FinanceSourceBookAuthorityFreezeStages.Authorized
                or FinanceSourceBookAuthorityFreezeStages.PrePost
            ? completed
            : source.FreezeStage == FinanceSourceBookAuthorityFreezeStages.Submitted &&
                workflowEvidence.Status is WorkflowInstanceStatus.Created or WorkflowInstanceStatus.InProgress or WorkflowInstanceStatus.Waiting;
        if (!acceptable)
            throw new InvalidOperationException(requireCompletedForPosting
                ? "SOURCE_BOOK_AUTHORITY_WORKFLOW_NOT_APPROVED: posting requires an exact completed workflow outcome."
                : "SOURCE_BOOK_AUTHORITY_WORKFLOW_STAGE_INVALID: workflow state does not support the requested freeze stage.");
        return completed
            ? await RequireCompletedWorkflowApprovalAsync(workflowEvidence, cancellationToken)
            : null;
    }

    private async Task<WorkflowApprovalEvidence> RequireCompletedWorkflowApprovalAsync(
        WorkflowInstance workflow, CancellationToken cancellationToken)
    {
        var approvals = await (from step in _db.WorkflowStepInstances.AsNoTracking()
                join approval in _db.WorkflowApprovals.AsNoTracking()
                    on new { step.TenantId, StepInstanceId = step.Id }
                    equals new { approval.TenantId, approval.StepInstanceId }
                where step.TenantId == TenantId && step.WorkflowInstanceId == workflow.Id &&
                    !step.IsDeleted && !approval.IsDeleted &&
                    step.Status == WorkflowStepInstanceStatus.Completed && step.CompletedDate.HasValue &&
                    approval.Status == WorkflowApprovalStatus.Approved && approval.ProcessedById.HasValue &&
                    approval.ProcessedDate.HasValue && workflow.CompletedDate.HasValue &&
                    approval.ProcessedDate <= workflow.CompletedDate && approval.ProcessedDate <= step.CompletedDate
                select new WorkflowApprovalEvidence(step.Id, approval.Id,
                    approval.ProcessedById!.Value, approval.ProcessedDate!.Value))
            .ToListAsync(cancellationToken);
        if (approvals.Count == 0)
            throw new InvalidOperationException("SOURCE_BOOK_AUTHORITY_WORKFLOW_APPROVAL_REQUIRED: completed workflow has no retained approved step evidence.");
        var finalProcessedAt = approvals.Max(item => item.ProcessedAtUtc);
        var final = approvals.Where(item => item.ProcessedAtUtc == finalProcessedAt).ToList();
        if (final.Count != 1)
            throw new InvalidOperationException("SOURCE_BOOK_AUTHORITY_WORKFLOW_APPROVAL_AMBIGUOUS: final approved workflow processor is not unique.");
        if (final[0].ProcessedByUserId == workflow.InitiatedById)
            throw new InvalidOperationException("SOURCE_BOOK_AUTHORITY_WORKFLOW_MAKER_CHECKER_REQUIRED: final approver must differ from workflow initiator.");
        return final[0];
    }

    private FinanceSourceBookAuthority CreateAuthority(NormalizedSource source, Guid bookId, string bookCode,
        string functionalCurrency, string selectionBasis, int version, Guid? supersedesId,
        Guid? frozenBy, DateTime frozenAt)
    {
        var entity = new FinanceSourceBookAuthority
        {
            TenantId = TenantId, OriginModuleCode = source.OriginModuleCode,
            SourceDocumentType = source.SourceDocumentType, SourceDocumentId = source.SourceDocumentId,
            PostingAction = source.PostingAction, AuthorityVersion = version, SupersedesAuthorityId = supersedesId,
            SourceWorkflowInstanceId = source.WorkflowInstanceId, FreezeStage = source.FreezeStage,
            EffectiveDate = source.EffectiveDate, AccountingBookId = bookId, AccountingBookCode = bookCode,
            FunctionalCurrencyCode = functionalCurrency, TransactionCurrencyCode = source.TransactionCurrencyCode,
            SelectionBasis = selectionBasis, FrozenByUserId = frozenBy, FrozenAtUtc = frozenAt,
            CreatedById = Actor()
        };
        entity.AuthorityFingerprint = Fingerprint(entity, []);
        return entity;
    }

    private IQueryable<FinanceSourceBookAuthority> SourceAuthorities(NormalizedSource source) =>
        _db.FinanceSourceBookAuthorities.Where(item => item.TenantId == TenantId && !item.IsDeleted &&
            item.OriginModuleCode == source.OriginModuleCode && item.SourceDocumentType == source.SourceDocumentType &&
            item.SourceDocumentId == source.SourceDocumentId && item.PostingAction == source.PostingAction);

    private async Task RequireMutationScopeAndLockAsync(NormalizedSource source, CancellationToken cancellationToken)
    {
        RequireSerializableMutationScope();
        await AcquireSourceLockAsync(source, cancellationToken);
    }

    private void RequireSerializableMutationScope()
    {
        if (!_db.Database.IsRelational()) return;
        var transaction = _db.Database.CurrentTransaction
            ?? throw new InvalidOperationException("SOURCE_BOOK_AUTHORITY_TRANSACTION_REQUIRED: caller must own a serializable transaction.");
        if (transaction.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable)
            throw new InvalidOperationException("SOURCE_BOOK_AUTHORITY_SERIALIZABLE_REQUIRED: authority transitions require serializable isolation.");
    }

    private async Task AcquireSourceLockAsync(NormalizedSource source, CancellationToken cancellationToken)
    {
        if (!_db.Database.IsSqlServer()) return;
        var resource = "FinanceSourceBookAuthority:" + Hash(
            $"{TenantId:D}|{source.OriginModuleCode}|{source.SourceDocumentType}|{source.SourceDocumentId:D}|{source.PostingAction}");
        await _db.Database.ExecuteSqlInterpolatedAsync($$"""
            DECLARE @lockResult int;
            EXEC @lockResult = sys.sp_getapplock
                @Resource={{resource}}, @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=15000;
            IF @lockResult < 0 THROW 51000, 'SOURCE_BOOK_AUTHORITY_LOCK_FAILED', 1;
            """, cancellationToken);
    }

    private NormalizedSource Normalize(FinanceSourceBookAuthorityFreezeRequest request, bool allowLegacyStage = false)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.SourceDocumentId == Guid.Empty) throw new InvalidOperationException("Source document id is required.");
        if (request.EffectiveDate == default) throw new InvalidOperationException("Effective date is required.");
        var identity = FinancePreparedIdentityNormalizer.Normalize(request.OriginModuleCode,
            request.SourceDocumentType, request.PostingAction);
        var stage = FinancePreparedIdentityNormalizer.NormalizeValue(request.FreezeStage, "Authority freeze stage");
        if (!FreezeStages.Contains(stage) && !(allowLegacyStage && stage == FinanceSourceBookAuthorityFreezeStages.LegacyPosted))
            throw new InvalidOperationException("SOURCE_BOOK_AUTHORITY_STAGE_INVALID: unsupported freeze stage.");
        if (request.SourceWorkflowInstanceId == Guid.Empty) throw new InvalidOperationException("Workflow instance id cannot be empty.");
        return new NormalizedSource(identity.OriginatingModuleCode, identity.SourceDocumentType, request.SourceDocumentId,
            identity.PostingAction, request.EffectiveDate.Date, NormalizeCurrency(request.TransactionCurrencyCode),
            stage, request.SourceWorkflowInstanceId);
    }

    private static NormalizedSource ToSource(FinanceSourceBookAuthority authority) => new(
        authority.OriginModuleCode, authority.SourceDocumentType, authority.SourceDocumentId,
        authority.PostingAction, authority.EffectiveDate.Date, authority.TransactionCurrencyCode,
        authority.FreezeStage, authority.SourceWorkflowInstanceId);

    private static void RequireSameSourceIdentity(FinanceSourceBookAuthority authority, NormalizedSource source, bool compareWorkflow)
    {
        if (authority.OriginModuleCode != source.OriginModuleCode || authority.SourceDocumentType != source.SourceDocumentType ||
            authority.SourceDocumentId != source.SourceDocumentId || authority.PostingAction != source.PostingAction ||
            compareWorkflow && authority.SourceWorkflowInstanceId != source.WorkflowInstanceId)
            throw new InvalidOperationException("SOURCE_BOOK_AUTHORITY_IDENTITY_MISMATCH: request does not identify frozen source/workflow.");
    }

    private static string Fingerprint(FinanceSourceBookAuthority authority,
        IEnumerable<FinanceSourceBookAuthorityOrigin> origins, string? suffix = null)
    {
        var originEvidence = string.Join("|", origins.OrderBy(item => item.OriginAuthorityId).ThenBy(item => item.Role, StringComparer.Ordinal)
            .Select(item => $"{item.OriginAuthorityId:D}:{item.Role}:{item.OriginalFinancePostingEventId:D}:{item.OriginalJournalEntryId:D}"));
        return Hash($"SOURCE-BOOK-AUTHORITY-V1|{authority.TenantId:D}|{authority.OriginModuleCode}|{authority.SourceDocumentType}|" +
            $"{authority.SourceDocumentId:D}|{authority.PostingAction}|{authority.AuthorityVersion}|{authority.SupersedesAuthorityId:D}|" +
            $"{authority.SourceWorkflowInstanceId:D}|{authority.FreezeStage}|{authority.EffectiveDate:yyyy-MM-dd}|" +
            $"{authority.AccountingBookId:D}|{authority.AccountingBookCode}|{authority.FunctionalCurrencyCode}|" +
            $"{authority.TransactionCurrencyCode}|{authority.SelectionBasis}|{originEvidence}|{suffix}");
    }

    private static bool SameNormalized(string? value, string expected)
    {
        try { return FinancePreparedIdentityNormalizer.NormalizeValue(value ?? string.Empty, "Source identity") == expected; }
        catch (InvalidOperationException) { return false; }
    }

    private static bool SameOriginModule(string? sourceModule, string expected)
    {
        try { return FinanceModuleLockCatalog.ResolveOriginModuleCode(sourceModule ?? string.Empty) == expected; }
        catch (InvalidOperationException) { return false; }
    }

    private static string NormalizeCurrency(string? value)
    {
        var normalized = value?.Trim().ToUpperInvariant() ?? string.Empty;
        if (normalized.Length != 3 || normalized.Any(character => character is < 'A' or > 'Z'))
            throw new InvalidOperationException("Currency code must be a canonical three-letter ASCII code.");
        return normalized;
    }

    private static bool CanonicalBookCode(string? value) => value is { Length: > 0 and <= 20 } &&
        value[0] is >= 'A' and <= 'Z' && value.All(character => character is >= 'A' and <= 'Z' or >= '0' and <= '9' or '_');
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private Guid TenantId => _currentUser.GetRequiredFinanceTenantId();
    private Guid Actor() => _currentUser.IsAuthenticated && Guid.TryParse(_currentUser.UserId, out var actor) && actor != Guid.Empty
        ? actor : throw new UnauthorizedAccessException("An authenticated Finance user is required.");
    private static FinanceSourceBookAuthorityResult Map(FinanceSourceBookAuthority item) => new(
        item.Id, item.AuthorityVersion, item.OriginModuleCode, item.SourceDocumentType, item.SourceDocumentId,
        item.PostingAction, item.EffectiveDate, item.FreezeStage, item.SourceWorkflowInstanceId,
        item.AccountingBookId, item.AccountingBookCode,
        item.FunctionalCurrencyCode, item.TransactionCurrencyCode, item.SelectionBasis,
        item.AuthorityFingerprint, item.OriginalFinancePostingEventId, item.OriginalJournalEntryId);

    private sealed record NormalizedSource(string OriginModuleCode, string SourceDocumentType,
        Guid SourceDocumentId, string PostingAction, DateTime EffectiveDate, string TransactionCurrencyCode,
        string FreezeStage, Guid? WorkflowInstanceId);
    private sealed record PrimaryCoordinate(Guid BookId, string BookCode, string FunctionalCurrency, string SelectionBasis);
    private sealed record OriginInput(Guid AuthorityId, string Role);
    private sealed record WorkflowApprovalEvidence(Guid StepInstanceId, Guid ApprovalId,
        Guid ProcessedByUserId, DateTime ProcessedAtUtc);
}
