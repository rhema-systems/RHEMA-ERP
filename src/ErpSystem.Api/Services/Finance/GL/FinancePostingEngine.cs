using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Finance;
using ErpSystem.Core.Finance.Integration;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ErpSystem.Api.Services.Finance.GL;

public sealed class FinancePostingEngine : IFinancePostingEngine, IAccountingEventPostingLeaf
{
    private const string PostedStatus = "Posted";
    private const string RequestFingerprintVersion = "FINPOST-REQUEST-V1";
    private const string RequestFingerprintDomain = "RHEMA-FINANCE-POSTING-REQUEST";
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<FinancePostingEngine> _logger;
    private readonly IFinanceAuditService? _financeAuditService;
    private readonly IFinanceBudgetControlService? _budgetControl;
    private readonly IFinanceBudgetCommitmentService? _budgetCommitments;
    private readonly IBookBalanceReadModelService _bookBalances;

    public FinancePostingEngine(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ILogger<FinancePostingEngine> logger,
        IFinanceAuditService? financeAuditService = null,
        IFinanceBudgetControlService? budgetControl = null,
        IFinanceBudgetCommitmentService? budgetCommitments = null,
        IBookBalanceReadModelService? bookBalances = null)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
        _financeAuditService = financeAuditService;
        _budgetControl = budgetControl;
        _budgetCommitments = budgetCommitments;
        _bookBalances = bookBalances ?? new BookBalanceReadModelService(context);
    }

    public async Task<FinancePostingResultDto> PostAsync(
        FinancePostingRequestV2Dto request,
        CancellationToken cancellationToken = default) =>
        await PostCoreAsync(request, request.AccountingBookCode, producerContext: null, allowHistoricalMappingException: false, cancellationToken);

    public async Task<FinancePostingResultDto> PostAsync(
        FinancePostingRequestV2Dto request,
        FinancePostingProducerContext producerContext,
        CancellationToken cancellationToken = default) =>
        await PostCoreAsync(request, request.AccountingBookCode, producerContext ?? throw new ArgumentNullException(nameof(producerContext)), allowHistoricalMappingException: false, cancellationToken);

    public async Task<FinancePostingResultDto> PostYearEndAsync(
        FinancePostingRequestV2Dto request, Guid bookCloseCycleId, CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUserService.GetRequiredFinanceTenantId();
        var actor = GetCurrentUserGuid() ?? throw new UnauthorizedAccessException("An authenticated Finance user is required.");
        if (_context.Database.IsRelational() && (_context.Database.CurrentTransaction is null
            || _context.Database.CurrentTransaction.GetDbTransaction().IsolationLevel != System.Data.IsolationLevel.Serializable))
            throw new InvalidOperationException("Year-end posting requires the caller's serializable transaction.");
        var cycle = _context.YearEndBookCloseCycles.Local.SingleOrDefault(item =>
            item.Id == bookCloseCycleId && item.TenantId == tenantId && !item.IsDeleted);
        var stored = await _context.YearEndBookCloseCycles.AsNoTracking().SingleOrDefaultAsync(item =>
            item.Id == bookCloseCycleId && item.TenantId == tenantId && !item.IsDeleted, cancellationToken);
        if (cycle == null || stored == null || cycle.AccountingBookId != stored.AccountingBookId
            || cycle.AccountingBookCode != stored.AccountingBookCode || cycle.FunctionalCurrencyCode != stored.FunctionalCurrencyCode
            || cycle.FiscalYearId != stored.FiscalYearId || cycle.Status != stored.Status
            || cycle.ClosingJournalEntryId != stored.ClosingJournalEntryId
            || cycle.RetainedEarningsAccountId != stored.RetainedEarningsAccountId || cycle.IdempotencyKey != stored.IdempotencyKey
            || cycle.ClosedByUserId != stored.ClosedByUserId || cycle.ClosedAtUtc != stored.ClosedAtUtc
            || cycle.PeriodAuthoritySnapshotJson != stored.PeriodAuthoritySnapshotJson)
            throw new InvalidOperationException("Year-end posting requires its durable, unchanged book-close cycle authority.");
        var reverse = request.SourceDocumentType == "YearEndCloseReversal";
        if (request.SourceModule != "GL" || request.SourceDocumentTenantId != tenantId || request.SourceDocumentId != cycle.Id
            || request.AccountingBookCode != cycle.AccountingBookCode || request.FunctionalCurrencyCode != cycle.FunctionalCurrencyCode
            || request.ExistingJournalEntryId != null || !request.AllowPostingToClosedPeriod
            || (reverse
                ? cycle.Status != "Closed" || cycle.ClosingJournalEntryId == null
                    || request.ReversalOfJournalEntryId != cycle.ClosingJournalEntryId || request.PostingAction != "Reverse"
                : request.SourceDocumentType != "YearEndClose" || cycle.Status != "Closing"
                    || cycle.ClosedByUserId != actor || request.ReversalOfJournalEntryId != null || request.PostingAction != "Post")
            || request.IdempotencyKey != $"GL:{(reverse ? "YearEndCloseReversal" : "YearEndClose")}:{tenantId:N}:{cycle.AccountingBookId:N}:{cycle.Id:N}")
            throw new InvalidOperationException("The year-end request does not match its exact book-close cycle.");
        var period = await _context.FiscalPeriods.Include(item => item.FiscalYear).SingleOrDefaultAsync(item =>
            item.Id == request.FiscalPeriodId && item.TenantId == tenantId && item.FiscalYearId == cycle.FiscalYearId
            && !item.IsDeleted, cancellationToken);
        if (period == null || period.FiscalYear.IsLocked || period.FiscalYear.IsClosed
            || request.PostingDate.Date != period.FiscalYear.EndDate.Date || period.IsLocked)
            throw new InvalidOperationException("Year-end posting date and fiscal-year authority do not match.");
        var currentPeriodAuthority = await YearEndTenantPeriodAuthority.CaptureAsync(
            _context, tenantId, cycle.FiscalYearId, cancellationToken);
        if (!string.Equals(cycle.PeriodAuthoritySnapshotJson, currentPeriodAuthority, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Year-end posting requires unchanged captured tenant fiscal-period close authority.");
        var validation = await ValidatePostingRequestAsync(tenantId, request, cycle.AccountingBookCode,
            producerContext: null, allowHistoricalMappingException: reverse, cancellationToken, cycle);
        if (validation.AccountingBookId != cycle.AccountingBookId)
            throw new InvalidOperationException("The resolved accounting book differs from the frozen close cycle.");
        return await ExecutePostingAsync(tenantId, validation, request, accountingEventContext: null, cancellationToken, cycle);
    }

    private async Task<FinancePostingResultDto> PostCoreAsync(
        FinancePostingCommandDto request,
        string accountingBookCode,
        FinancePostingProducerContext? producerContext,
        bool allowHistoricalMappingException,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var tenantId = _currentUserService.GetRequiredFinanceTenantId();
        var validation = await ValidatePostingRequestAsync(tenantId, request, accountingBookCode, producerContext, allowHistoricalMappingException, cancellationToken);

        await EnsureNoParallelBookPostingAsync(tenantId, validation, acquireLock: false,
            accountingEventContext: null, cancellationToken);
        var existingPosting = await FindExistingPostingAsync(tenantId, validation, accountingEventContext: null, cancellationToken);
        if (existingPosting != null)
        {
            if (!request.ReturnExistingOnDuplicate)
            {
                throw new InvalidOperationException("This source document/action has already been posted.");
            }

            await RecordDuplicatePostingAuditAsync(tenantId, validation, existingPosting, cancellationToken);
            return ToResult(existingPosting, wasDuplicate: true);
        }

        if (_context.Database.CurrentTransaction != null)
        {
            return await ExecutePostingAsync(tenantId, validation, request,
                accountingEventContext: null, cancellationToken);
        }

        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var result = await ExecutePostingAsync(tenantId, validation, request,
                    accountingEventContext: null, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch (DbUpdateException ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                _context.ChangeTracker.Clear();

                var racedPosting = await FindExistingPostingAsync(tenantId, validation, accountingEventContext: null, cancellationToken);
                if (racedPosting != null)
                {
                    if (request.ReturnExistingOnDuplicate)
                    {
                        await RecordDuplicatePostingAuditAsync(tenantId, validation, racedPosting, cancellationToken);
                        return ToResult(racedPosting, wasDuplicate: true);
                    }

                    throw new InvalidOperationException("This source document/action has already been posted.", ex);
                }

                _logger.LogError(ex,
                    "Finance posting persistence failed and was rolled back for tenant {TenantId}, source {SourceModule}/{SourceDocumentType}/{SourceDocumentId}, action {PostingAction}, book {AccountingBookCode}.",
                    tenantId,
                    validation.SourceModule,
                    validation.SourceDocumentType,
                    validation.SourceDocumentId,
                    validation.PostingAction,
                    validation.AccountingBookCode);
                throw new InvalidOperationException(
                    "FINANCE_POSTING_PERSISTENCE_FAILED: Posting was rolled back because ledger evidence could not be saved. No Primary or Parallel ledger posting was committed. Review the server log for the underlying database constraint and retry after correcting it.",
                    ex);
            }
        });
    }

    private async Task<FinancePostingResultDto> ExecutePostingAsync(
        Guid tenantId,
        ValidatedPosting validation,
        FinancePostingCommandDto request,
        AccountingEventPostingAuthority? accountingEventContext,
        CancellationToken cancellationToken,
        YearEndBookCloseCycle? yearEndCycle = null)
    {
        await EnsureNoParallelBookPostingAsync(tenantId, validation, acquireLock: true,
            accountingEventContext, cancellationToken);
        if (yearEndCycle == null)
            await EnsureBookYearIsOpenAsync(tenantId, validation.AccountingBookId, validation.FiscalPeriod.FiscalYearId, cancellationToken);
        var duplicateInsideTransaction = await FindExistingPostingAsync(tenantId, validation, accountingEventContext, cancellationToken);
        if (duplicateInsideTransaction != null)
        {
            if (!request.ReturnExistingOnDuplicate)
            {
                throw new InvalidOperationException("This source document/action has already been posted.");
            }

            await RecordDuplicatePostingAuditAsync(tenantId, validation, duplicateInsideTransaction, cancellationToken);
            return ToResult(duplicateInsideTransaction, wasDuplicate: true);
        }

        var now = DateTime.UtcNow;
        var postedByUserId = GetCurrentUserGuid();
        await EnsureDimensionSetsAsync(tenantId, validation.Lines, now, postedByUserId, cancellationToken);
        var journalEntry = validation.ExistingJournalEntryId.HasValue
            ? await ApplyExistingJournalPostingAsync(tenantId, validation, now, postedByUserId, cancellationToken)
            : await CreatePostedJournalEntryAsync(tenantId, validation, now, postedByUserId, cancellationToken);
        await EnsureDimensionSnapshotsAsync(tenantId, validation, journalEntry, now, postedByUserId, cancellationToken);

        var postingEvent = BuildPostingEvent(tenantId, validation, journalEntry.Id, now, postedByUserId);
        await MarkExchangeRatesUsedAsync(tenantId, validation, postingEvent.Id, now, cancellationToken);

        // Resolve every active foreign-currency representation before any ledger row is
        // committed. Missing rates, mappings, or rounding authority therefore roll the
        // Primary posting back instead of creating an inconsistent Parallel ledger.
        var parallelReplicas = yearEndCycle != null ? Array.Empty<ParallelReplica>() : await BuildParallelReplicasAsync(
            tenantId, validation, journalEntry, now, postedByUserId, cancellationToken);

        if (validation.BudgetReservationIds.Count > 0)
        {
            if (string.IsNullOrWhiteSpace(validation.BudgetReservationSourceDocumentType))
            {
                if (_budgetControl == null)
                    throw new InvalidOperationException("Finance budget control is not configured for this budget-controlled posting.");
                await _budgetControl.ConsumeReservationsAsync(
                    tenantId,
                    validation.SourceDocumentId,
                    validation.BudgetReservationIds,
                    journalEntry.Id,
                    postingEvent.Id,
                    cancellationToken);
            }
            else
            {
                if (_budgetCommitments == null)
                    throw new InvalidOperationException("Finance budget commitments are not configured for this producer posting.");
                await _budgetCommitments.ConsumeForPostingAsync(
                    tenantId,
                    validation.BudgetReservationSourceDocumentType,
                    validation.SourceDocumentId,
                    validation.BudgetReservationIds,
                    journalEntry.Id,
                    postingEvent.Id,
                    cancellationToken);
            }
        }

        if (!validation.ExistingJournalEntryId.HasValue)
        {
            _context.JournalEntries.Add(journalEntry);
        }

        // Journal lines are authoritative. Both exact-book projections are updated inside this same
        // posting transaction; no unscoped account balance snapshot is maintained.
        await _bookBalances.ApplyPostingAsync(tenantId, validation.AccountingBookId,
            validation.AccountingBookCode, validation.FiscalPeriod.Id, validation.FunctionalCurrencyCode,
            journalEntry.Transactions.ToArray(),
            now, postedByUserId, cancellationToken);

        _context.FinancePostingEvents.Add(postingEvent);
        foreach (var replica in parallelReplicas)
        {
            _context.JournalEntries.Add(replica.JournalEntry);
            _context.FinancePostingEvents.Add(replica.PostingEvent);
            await _bookBalances.ApplyPostingAsync(tenantId, replica.Book.Id,
                replica.Book.Code, validation.FiscalPeriod.Id, replica.Book.FunctionalCurrencyCode!,
                replica.JournalEntry.Transactions.ToArray(), now, postedByUserId, cancellationToken);
        }
        await _context.SaveChangesAsync(cancellationToken);
        await RecordPostingEventCreatedAuditAsync(tenantId, validation, postingEvent, journalEntry.Id, cancellationToken);
        await RecordCurrencySnapshotAuditAsync(tenantId, validation, postingEvent, journalEntry.Id, cancellationToken);
        await RecordExchangeRatePolicyOverrideAuditAsync(tenantId, validation, postingEvent, journalEntry.Id, cancellationToken);

        _logger.LogInformation(
            "Finance posting completed for tenant {TenantId}, source {SourceModule}/{SourceDocumentType}/{SourceDocumentId}, action {PostingAction}, journal {JournalEntryId}.",
            tenantId,
            validation.SourceModule,
            validation.SourceDocumentType,
            validation.SourceDocumentId,
            validation.PostingAction,
            journalEntry.Id);

        postingEvent.JournalEntry = journalEntry;
        return ToResult(postingEvent, wasDuplicate: false);
    }

    async Task<FinancePostingResultDto> IAccountingEventPostingLeaf.PostAsync(
        FinancePostingRequestV2Dto request,
        AccountingEventPostingAuthority authority,
        CancellationToken cancellationToken)
    {
        RequireCompleteAccountingEventAuthority(authority);
        if (_context.Database.IsRelational() && _context.Database.CurrentTransaction is null)
            throw new InvalidOperationException("AccountingEvent representations require one caller-owned database transaction.");

        var tenantId = _currentUserService.GetRequiredFinanceTenantId();
        // The aggregate must be tracked in this scoped context. This makes the authority unforgeable by
        // an ordinary request and ensures its event/evidence rows share the leaf's transaction boundary.
        var eventTracked = _context.AccountingEvents.Local.SingleOrDefault(item =>
            item.Id == authority.AccountingEventId && item.TenantId == tenantId
            && item.AccountingBookSelectionEvidenceId == authority.AccountingBookSelectionEvidenceId);
        if (eventTracked is null)
            throw new InvalidOperationException("AccountingEvent posting authority is not tracked in the active Finance unit of work.");

        if (string.IsNullOrWhiteSpace(request.AccountingBookCode))
            throw new InvalidOperationException("AccountingEvent representation requires its exact frozen accounting-book code.");
        await RequireAccountingEventBookAuthorityAsync(tenantId, eventTracked, authority.AccountingBookId,
            request.AccountingBookCode.Trim().ToUpperInvariant(), authority, cancellationToken);
        await EnsureNoUnrelatedAccountingEventMatchesBeforeValidationAsync(tenantId, request, authority, cancellationToken);
        var validation = await ValidatePostingRequestAsync(tenantId, request, request.AccountingBookCode,
            producerContext: null, allowHistoricalMappingException: false, cancellationToken);
        await RequireAccountingEventBookAuthorityAsync(tenantId, eventTracked, validation.AccountingBookId,
            validation.AccountingBookCode, authority, cancellationToken);
        return await ExecutePostingAsync(tenantId, validation, request,
            authority, cancellationToken);
    }

    async Task<FinancePostingResultDto> IAccountingEventPostingLeaf.ReverseAsync(
        Guid financePostingEventId,
        DateTime reversalDate,
        string reason,
        string idempotencyKey,
        AccountingEventPostingAuthority authority,
        CancellationToken cancellationToken)
    {
        RequireCompleteAccountingEventAuthority(authority);
        var tenantId = _currentUserService.GetRequiredFinanceTenantId();
        if (_context.Database.IsRelational() && _context.Database.CurrentTransaction is null)
            throw new InvalidOperationException("AccountingEvent representations require one caller-owned database transaction.");
        var eventTracked = _context.AccountingEvents.Local.SingleOrDefault(item => item.Id == authority.AccountingEventId
                && item.TenantId == tenantId && item.AccountingBookSelectionEvidenceId == authority.AccountingBookSelectionEvidenceId);
        if (eventTracked is null)
            throw new InvalidOperationException("AccountingEvent posting authority is not tracked in the active Finance unit of work.");
        await RequireAccountingEventBookAuthorityAsync(tenantId, eventTracked, authority.AccountingBookId,
            exactBookCode: null, authority: authority, cancellationToken: cancellationToken);
        var original = await _context.FinancePostingEvents.AsNoTracking().Include(item => item.JournalEntry)
            .ThenInclude(item => item!.Transactions).SingleAsync(item => item.TenantId == tenantId
                && item.Id == financePostingEventId && !item.IsDeleted, cancellationToken);
        EnsureStoredBookEvidence(original);
        var journal = original.JournalEntry!;
        var request = new FinancePostingRequestV2Dto
        {
            SourceModule = "GL", OriginModuleCode = FinanceModuleLockCatalog.Finance,
            SourceDocumentType = "FinancePostingEventReversal", SourceDocumentId = original.Id,
            SourceDocumentTenantId = tenantId, ReversalOfJournalEntryId = journal.Id,
            ReversalReason = reason.Trim(), ReversalType = "Exact", PostingAction = "Reverse",
            SourceDocumentReference = original.SourceDocumentReference,
            Description = $"Exact AccountingEvent reversal of {journal.JournalEntryNumber}: {reason.Trim()}",
            PostingDate = reversalDate.Date, JournalType = "System Generated",
            AccountingBookCode = original.BookClassification, FunctionalCurrencyCode = original.FunctionalCurrencyCode,
            IdempotencyKey = idempotencyKey, ReturnExistingOnDuplicate = true,
            Lines = journal.Transactions.OrderBy(item => item.LineNumber).Select(item => new FinancePostingLineDto
            {
                AccountId = item.AccountId, SourceDocumentLineId = item.SourceDocumentLineId,
                Description = $"Reversal: {item.Description}", DebitAmount = item.CreditAmount,
                CreditAmount = item.DebitAmount, TransactionCurrency = item.TransactionCurrency,
                ForeignCurrencyAmount = item.ForeignCurrencyAmount, ExchangeRate = item.ExchangeRate,
                ExchangeRateId = item.ExchangeRateId, ExchangeRateSource = item.ExchangeRateSource,
                ExchangeRateDate = item.ExchangeRateDate, TransactionDebitAmount = item.TransactionCreditAmount,
                TransactionCreditAmount = item.TransactionDebitAmount, SourceReferenceNumber = item.SourceReferenceNumber,
                LineNumber = item.LineNumber, FinanceDimensionSetId = item.FinanceDimensionSetId,
                SegmentString = item.SegmentString, Notes = reason.Trim(), TransactionTag = "Reversal"
            }).ToList()
        };
        await EnsureNoUnrelatedAccountingEventMatchesBeforeValidationAsync(tenantId, request, authority, cancellationToken);
        var validation = await ValidatePostingRequestAsync(tenantId, request, request.AccountingBookCode,
            producerContext: null, allowHistoricalMappingException: true, cancellationToken);
        await RequireAccountingEventBookAuthorityAsync(tenantId, eventTracked, validation.AccountingBookId,
            validation.AccountingBookCode, authority, cancellationToken);
        return await ExecutePostingAsync(tenantId, validation, request,
            authority, cancellationToken);
    }

    private static void RequireCompleteAccountingEventAuthority(AccountingEventPostingAuthority authority)
    {
        if (authority.AccountingEventId == Guid.Empty || authority.AccountingBookSelectionEvidenceId == Guid.Empty
            || authority.AccountingBookId == Guid.Empty || string.IsNullOrWhiteSpace(authority.AuthorityFingerprint)
            || authority.OrderedSelectedBookIds.IsDefaultOrEmpty
            || authority.OrderedSelectedBookIds.Distinct().Count() != authority.OrderedSelectedBookIds.Length
            || !authority.OrderedSelectedBookIds.Contains(authority.AccountingBookId))
            throw new InvalidOperationException("Canonical AccountingEvent, frozen selection, and exact-book authority are required.");
    }

    private async Task RequireAccountingEventBookAuthorityAsync(
        Guid tenantId,
        AccountingEvent eventTracked,
        Guid validatedBookId,
        string? exactBookCode,
        AccountingEventPostingAuthority authority,
        CancellationToken cancellationToken)
    {
        var locallyOrderedBooks = eventTracked.Postings.OrderBy(item => item.SelectionOrder)
            .Select(item => item.AccountingBookId).ToArray();
        if (!authority.OrderedSelectedBookIds.SequenceEqual(locallyOrderedBooks)
            || validatedBookId != authority.AccountingBookId
            || !eventTracked.Postings.Any(item => item.AccountingBookId == authority.AccountingBookId
                && (exactBookCode is null || string.Equals(item.AccountingBookCodeSnapshot, exactBookCode, StringComparison.Ordinal))
                && string.Equals(item.AuthorityFingerprint, authority.AuthorityFingerprint, StringComparison.Ordinal)))
            throw new InvalidOperationException("The requested exact book is not part of the AccountingEvent frozen authority.");

        if (!_context.Database.IsRelational()) return;
        var durablyOrderedBooks = await _context.AccountingBookSelectionEvidenceBooks.AsNoTracking()
            .Where(item => item.TenantId == tenantId
                && item.AccountingBookSelectionEvidenceId == authority.AccountingBookSelectionEvidenceId
                && !item.IsDeleted)
            .OrderBy(item => item.SelectionOrder).Select(item => item.AccountingBookId).ToListAsync(cancellationToken);
        if (!authority.OrderedSelectedBookIds.SequenceEqual(durablyOrderedBooks))
            throw new InvalidOperationException("AccountingEvent authority does not match the complete ordered frozen selection.");

        var bound = await _context.AccountingEventPostings.AsNoTracking().AnyAsync(posting =>
            posting.TenantId == tenantId
            && posting.AccountingEventId == authority.AccountingEventId
            && posting.EventVersion == eventTracked.Version
            && posting.AccountingBookId == authority.AccountingBookId
            && (exactBookCode == null || posting.AccountingBookCodeSnapshot == exactBookCode)
            && posting.AuthorityFingerprint == authority.AuthorityFingerprint
            && posting.Status == AccountingEventStatuses.Pending
            && !posting.IsDeleted
            && _context.AccountingEvents.Any(accountingEvent =>
                accountingEvent.TenantId == tenantId
                && accountingEvent.Id == authority.AccountingEventId
                && accountingEvent.Version == posting.EventVersion
                && accountingEvent.AccountingBookSelectionEvidenceId == authority.AccountingBookSelectionEvidenceId
                && accountingEvent.Status == AccountingEventStatuses.Pending
                && !accountingEvent.IsDeleted)
            && _context.AccountingBookSelectionEvidenceBooks.Any(selectionBook =>
                selectionBook.TenantId == tenantId
                && selectionBook.AccountingBookSelectionEvidenceId == authority.AccountingBookSelectionEvidenceId
                && selectionBook.AccountingBookId == authority.AccountingBookId
                && (exactBookCode == null || selectionBook.AccountingBookCodeSnapshot == exactBookCode)
                && selectionBook.AuthorityFingerprint == authority.AuthorityFingerprint
                && !selectionBook.IsDeleted), cancellationToken);
        if (!bound)
            throw new InvalidOperationException("AccountingEvent exact-book authority is not durably bound to its frozen selection.");
    }

    public async Task<FinanceReversalPlanDto> GetReversalPlanAsync(
        Guid postingEventId,
        string reason,
        DateTime? reversalDate = null,
        CancellationToken cancellationToken = default)
    {
        if (postingEventId == Guid.Empty)
        {
            throw new ArgumentException("Posting event id is required.", nameof(postingEventId));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A reversal reason is required.", nameof(reason));
        }

        var tenantId = _currentUserService.GetRequiredFinanceTenantId();
        var postingEvent = await _context.FinancePostingEvents
            .Include(e => e.JournalEntry)
                .ThenInclude(j => j!.Transactions)
            .FirstOrDefaultAsync(
                e => e.TenantId == tenantId
                    && e.Id == postingEventId
                    && !e.IsDeleted
                    && e.PostingStatus == PostedStatus,
                cancellationToken);

        if (postingEvent?.JournalEntry == null)
        {
            throw new InvalidOperationException("Posted finance event was not found for this tenant.");
        }

        EnsureStoredBookEvidence(postingEvent);

        var lines = postingEvent.JournalEntry.Transactions
            .OrderBy(t => t.LineNumber)
            .Select(t => new FinancePostingLineDto
            {
                AccountId = t.AccountId,
                SourceDocumentLineId = t.SourceDocumentLineId,
                Description = $"Reversal: {t.Description}",
                DebitAmount = t.CreditAmount,
                CreditAmount = t.DebitAmount,
                TransactionCurrency = t.TransactionCurrency,
                ForeignCurrencyAmount = t.ForeignCurrencyAmount,
                ExchangeRate = t.ExchangeRate,
                ExchangeRateId = t.ExchangeRateId,
                ExchangeRateSource = t.ExchangeRateSource,
                ExchangeRateDate = t.ExchangeRateDate,
                TransactionDebitAmount = t.TransactionCreditAmount,
                TransactionCreditAmount = t.TransactionDebitAmount,
                SourceReferenceNumber = t.SourceReferenceNumber,
                LineNumber = t.LineNumber,
                FinanceDimensionSetId = t.FinanceDimensionSetId,
                SegmentString = t.SegmentString,
                Notes = reason,
                TransactionTag = "Reversal"
            })
            .ToList();
        var resolvedReversalDate = reversalDate?.Date
            ?? await ResolveDefaultReversalDateAsync(tenantId, cancellationToken);

        return new FinanceReversalPlanDto
        {
            IsDefined = true,
            OriginalPostingEventId = postingEvent.Id,
            OriginalJournalEntryId = postingEvent.JournalEntryId!.Value,
            PostingAction = "Reverse",
            ReversalDate = resolvedReversalDate,
            Reason = reason.Trim(),
            ReversalLines = lines
        };
    }

    public async Task<FinancePostingResultDto> ReverseAsync(
        Guid postingEventId,
        string reason,
        DateTime? reversalDate = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUserService.GetRequiredFinanceTenantId();
        var plan = await GetReversalPlanAsync(postingEventId, reason, reversalDate, cancellationToken);
        var original = await _context.FinancePostingEvents.AsNoTracking()
            .Include(item => item.JournalEntry)
            .SingleAsync(item => item.Id == postingEventId && item.TenantId == tenantId && !item.IsDeleted, cancellationToken);
        var journal = original.JournalEntry ?? throw new InvalidOperationException("Original Finance journal evidence is missing.");
        EnsureStoredBookEvidence(original);
        var request = new FinancePostingRequestV2Dto
        {
            SourceModule = "GL",
            OriginModuleCode = FinanceModuleLockCatalog.Finance,
            SourceDocumentType = "FinancePostingEventReversal",
            SourceDocumentId = postingEventId,
            SourceDocumentTenantId = tenantId,
            ReversalOfJournalEntryId = plan.OriginalJournalEntryId,
            ReversalReason = plan.Reason,
            ReversalType = "Exact",
            PostingAction = "Reverse",
            SourceDocumentReference = original.SourceDocumentReference,
            Description = $"Exact reversal of {journal.JournalEntryNumber}: {plan.Reason}",
            PostingDate = plan.ReversalDate,
            JournalType = "System Generated",
            AccountingBookCode = original.BookClassification,
            FunctionalCurrencyCode = original.FunctionalCurrencyCode,
            IdempotencyKey = $"exact-reversal:{postingEventId:N}",
            ReturnExistingOnDuplicate = true,
            Lines = plan.ReversalLines
        };
        // Only this server-derived command can admit inactive historical book mappings. Ordinary
        // V2 requests cannot set or influence the exception.
        return await PostCoreAsync(request, request.AccountingBookCode, producerContext: null,
            allowHistoricalMappingException: true, cancellationToken);
    }

    private async Task<DateTime> ResolveDefaultReversalDateAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        // A missing date must never silently mean an accounting date in a closed calendar
        // period. Source services with richer tenant policies pass an explicit date; older
        // reversal callers receive the same safe current-open-period fallback here at the final
        // posting boundary.
        var period = await _context.FiscalPeriods
            .AsNoTracking()
            .Where(item =>
                item.TenantId == tenantId &&
                item.IsOpen &&
                !item.IsClosed &&
                !item.IsLocked &&
                !item.IsDeleted)
            .OrderByDescending(item => item.StartDate)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("No open fiscal period is available for the Finance reversal.");
        var today = DateTime.UtcNow.Date;
        if (today < period.StartDate.Date)
            return period.StartDate.Date;
        return today > period.EndDate.Date ? period.EndDate.Date : today;
    }

    private async Task<JournalEntry> ApplyExistingJournalPostingAsync(
        Guid tenantId,
        ValidatedPosting validation,
        DateTime now,
        Guid? postedByUserId,
        CancellationToken cancellationToken)
    {
        var journalEntry = await _context.JournalEntries
            .Include(j => j.Transactions)
            .FirstOrDefaultAsync(
                j => j.TenantId == tenantId
                    && j.Id == validation.ExistingJournalEntryId!.Value
                    && !j.IsDeleted,
                cancellationToken);

        if (journalEntry == null)
        {
            throw new InvalidOperationException("Existing journal entry was not found for this tenant.");
        }

        if (journalEntry.PostingStatus == PostedStatus)
        {
            throw new InvalidOperationException("Existing journal entry is already posted without a matching posting event.");
        }

        if (!string.Equals(journalEntry.PostingStatus, "Approved", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Manual journal entries must be approved before posting.");
        }

        if (journalEntry.AccountingBookId != validation.AccountingBookId
            || !string.Equals(journalEntry.BookClassification, validation.AccountingBookCode, StringComparison.Ordinal)
            || journalEntry.Transactions.Any(line => line.TenantId != tenantId
                || line.AccountingBookId != validation.AccountingBookId
                || !string.Equals(line.BookClassification, validation.AccountingBookCode, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("Existing journal accounting-book evidence does not match the posting request.");
        }

        var existingLines = journalEntry.Transactions
            .OrderBy(t => t.LineNumber)
            .ToList();

        if (existingLines.Count != validation.Lines.Count)
        {
            throw new InvalidOperationException("Existing journal lines do not match the posting request.");
        }

        for (var i = 0; i < existingLines.Count; i++)
        {
            var existingLine = existingLines[i];
            var requestLine = validation.Lines[i];
            if (existingLine.AccountId != requestLine.AccountId ||
                RoundMoney(existingLine.DebitAmount, validation.FunctionalDecimalPlaces) != requestLine.DebitAmount ||
                RoundMoney(existingLine.CreditAmount, validation.FunctionalDecimalPlaces) != requestLine.CreditAmount)
            {
                throw new InvalidOperationException("Existing journal lines do not match the posting request.");
            }
        }

        journalEntry.EntryDate = validation.PostingDate;
        journalEntry.Description = validation.Description;
        journalEntry.ReferenceNumber = validation.SourceDocumentReference;
        journalEntry.SourceModule = validation.SourceModule;
        journalEntry.OriginModuleCode = validation.OriginModuleCode;
        journalEntry.SourceDocumentId = validation.SourceDocumentId;
        journalEntry.SourceDocumentType = validation.SourceDocumentType;
        journalEntry.TotalDebitAmount = validation.TotalDebitAmount;
        journalEntry.TotalCreditAmount = validation.TotalCreditAmount;
        journalEntry.BalanceDifference = 0;
        journalEntry.IsBalanced = true;
        journalEntry.IsMultiCurrency = validation.IsMultiCurrency;
        journalEntry.PrimaryCurrency = validation.PrimaryCurrency;
        journalEntry.BookClassification = validation.AccountingBookCode;
        journalEntry.AccountingBookId = validation.AccountingBookId;
        journalEntry.FiscalPeriodId = validation.FiscalPeriod.Id;
        journalEntry.PostingDate = now;
        journalEntry.PostedByUserId = postedByUserId;
        journalEntry.PostingStatus = PostedStatus;
        journalEntry.UpdatedAt = now;
        journalEntry.UpdatedBy = _currentUserService.UserName;
        journalEntry.LastModifiedById = postedByUserId;

        for (var i = 0; i < existingLines.Count; i++)
        {
            var transaction = existingLines[i];
            var requestLine = validation.Lines[i];
            transaction.TransactionDate = validation.PostingDate;
            transaction.SourceModule = validation.SourceModule;
            transaction.SourceDocumentId = validation.SourceDocumentId;
            transaction.SourceDocumentLineId = requestLine.SourceDocumentLineId;
            transaction.SourceDocumentType = validation.SourceDocumentType;
            transaction.Description = requestLine.Description ?? validation.Description;
            transaction.SourceReferenceNumber = requestLine.SourceReferenceNumber ?? validation.SourceDocumentReference;
            transaction.SegmentString = requestLine.SegmentString;
            transaction.Notes = requestLine.Notes;
            transaction.TransactionTag = requestLine.TransactionTag;
            transaction.BookClassification = validation.AccountingBookCode;
            transaction.AccountingBookId = validation.AccountingBookId;
            transaction.FunctionalCurrencyCode = validation.FunctionalCurrencyCode;
            transaction.TransactionCurrency = requestLine.TransactionCurrency;
            transaction.TransactionDebitAmount = requestLine.TransactionDebitAmount;
            transaction.TransactionCreditAmount = requestLine.TransactionCreditAmount;
            transaction.ForeignCurrencyAmount = requestLine.ForeignCurrencyAmount;
            transaction.ExchangeRateId = requestLine.ExchangeRateId;
            transaction.ExchangeRate = requestLine.ExchangeRate;
            transaction.ExchangeRateSource = requestLine.ExchangeRateSource;
            transaction.ExchangeRateDate = requestLine.ExchangeRateDate;
            transaction.FinanceDimensionSetId = requestLine.DimensionSet?.Id;
            transaction.FiscalPeriodId = validation.FiscalPeriod.Id;
            transaction.PostingStatus = PostedStatus;
            transaction.PostedDate = now;
            transaction.UpdatedAt = now;
            transaction.UpdatedBy = _currentUserService.UserName;
            transaction.LastModifiedById = postedByUserId;
        }

        return journalEntry;
    }

    private async Task<JournalEntry> CreatePostedJournalEntryAsync(
        Guid tenantId,
        ValidatedPosting validation,
        DateTime now,
        Guid? postedByUserId,
        CancellationToken cancellationToken)
    {
        var original = await LoadOriginalForReversalAsync(tenantId, validation, cancellationToken);
        var journalEntry = new JournalEntry
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            JournalEntryNumber = GeneratePostingJournalNumber(validation.SourceModule, now),
            JournalType = validation.JournalType,
            EntryDate = validation.PostingDate,
            Description = validation.Description,
            ReferenceNumber = validation.SourceDocumentReference,
            SourceModule = validation.SourceModule,
            OriginModuleCode = validation.OriginModuleCode,
            SourceDocumentId = validation.SourceDocumentId,
            SourceDocumentType = validation.SourceDocumentType,
            TotalDebitAmount = validation.TotalDebitAmount,
            TotalCreditAmount = validation.TotalCreditAmount,
            BalanceDifference = validation.TotalDebitAmount - validation.TotalCreditAmount,
            IsBalanced = true,
            IsMultiCurrency = validation.IsMultiCurrency,
            PrimaryCurrency = validation.PrimaryCurrency,
            BookClassification = validation.AccountingBookCode,
            AccountingBookId = validation.AccountingBookId,
            FiscalPeriodId = validation.FiscalPeriod.Id,
            PostingDate = now,
            PostedByUserId = postedByUserId,
            PostingStatus = PostedStatus,
            ApprovalStatus = "Not Required",
            OriginalJournalEntryId = validation.ReversalOfJournalEntryId,
            ReversalType = validation.ReversalType,
            ReversalReason = validation.ReversalReason,
            Notes = validation.ReversalOfJournalEntryId.HasValue
                ? $"Posted reversal. Reason: {validation.ReversalReason}"
                : null,
            CreatedAt = now,
            CreatedBy = _currentUserService.UserName,
            CreatedById = postedByUserId
        };

        foreach (var line in validation.Lines)
        {
            journalEntry.Transactions.Add(new AccountTransaction
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                AccountId = line.AccountId,
                SourceDocumentLineId = line.SourceDocumentLineId,
                JournalEntryId = journalEntry.Id,
                TransactionDate = validation.PostingDate,
                Description = line.Description ?? validation.Description,
                DebitAmount = line.DebitAmount,
                CreditAmount = line.CreditAmount,
                FunctionalCurrencyCode = validation.FunctionalCurrencyCode,
                TransactionCurrency = line.TransactionCurrency,
                TransactionDebitAmount = line.TransactionDebitAmount,
                TransactionCreditAmount = line.TransactionCreditAmount,
                ForeignCurrencyAmount = line.ForeignCurrencyAmount,
                ExchangeRateId = line.ExchangeRateId,
                ExchangeRate = line.ExchangeRate,
                ExchangeRateSource = line.ExchangeRateSource,
                ExchangeRateDate = line.ExchangeRateDate,
                FinanceDimensionSetId = line.DimensionSet?.Id,
                SourceModule = validation.SourceModule,
                SourceDocumentId = validation.SourceDocumentId,
                SourceDocumentType = validation.SourceDocumentType,
                SourceReferenceNumber = line.SourceReferenceNumber ?? validation.SourceDocumentReference,
                BookClassification = validation.AccountingBookCode,
                AccountingBookId = validation.AccountingBookId,
                FiscalPeriodId = validation.FiscalPeriod.Id,
                PostedDate = now,
                PostingStatus = PostedStatus,
                SegmentString = line.SegmentString,
                LineNumber = line.LineNumber,
                Notes = line.Notes,
                TransactionTag = line.TransactionTag,
                ReversalType = validation.ReversalOfJournalEntryId.HasValue ? validation.ReversalType : null,
                ReversalReason = validation.ReversalOfJournalEntryId.HasValue ? validation.ReversalReason : null,
                CreatedAt = now,
                CreatedBy = _currentUserService.UserName,
                CreatedById = postedByUserId
            });
        }

        if (original != null)
        {
            ApplyOriginalReversalLinks(original, journalEntry, validation, now);
        }

        return journalEntry;
    }

    private async Task<JournalEntry?> LoadOriginalForReversalAsync(
        Guid tenantId,
        ValidatedPosting validation,
        CancellationToken cancellationToken)
    {
        if (!validation.ReversalOfJournalEntryId.HasValue)
        {
            return null;
        }

        var original = await _context.JournalEntries
            .Include(j => j.Transactions)
            .FirstOrDefaultAsync(
                j => j.TenantId == tenantId
                    && j.Id == validation.ReversalOfJournalEntryId.Value
                    && !j.IsDeleted,
                cancellationToken);

        if (original == null)
        {
            throw new InvalidOperationException("Original journal entry was not found for this tenant.");
        }

        if (original.PostingStatus != PostedStatus)
        {
            throw new InvalidOperationException("Only posted journal entries can be reversed.");
        }

        if (original.AccountingBookId != validation.AccountingBookId
            || !string.Equals(original.BookClassification, validation.AccountingBookCode, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Exact reversal accounting-book evidence does not match the original journal entry.");
        }

        if (original.Transactions.Any(line => line.TenantId != tenantId
                || line.AccountingBookId != original.AccountingBookId
                || !string.Equals(line.BookClassification, original.BookClassification, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("Original journal transaction accounting-book evidence is inconsistent.");
        }

        if (original.IsReversed || original.ReversalJournalEntryId.HasValue)
        {
            throw new InvalidOperationException("Journal entry has already been reversed.");
        }

        if (original.OriginalJournalEntryId.HasValue)
        {
            throw new InvalidOperationException("Reversal journal entries cannot be reversed from this action.");
        }

        // Every reversal entry point must reproduce immutable posting evidence exactly. The
        // trusted server path's historical exception relaxes only current book/account-mapping
        // availability; it never relaxes account, amount, currency, rate, lineage, or dimension
        // equality for caller-supplied V1/V2 reversal metadata.
        EnsureExactReversalLines(original, validation);

        return original;
    }

    private static void EnsureExactReversalLines(JournalEntry original, ValidatedPosting validation)
    {
        var source = original.Transactions.OrderBy(item => item.LineNumber).ThenBy(item => item.Id).ToList();
        var reversal = validation.Lines.OrderBy(item => item.LineNumber).ToList();
        if (source.Count != reversal.Count) throw new InvalidOperationException("Exact reversal evidence line count is inconsistent.");
        for (var index = 0; index < source.Count; index++)
        {
            var a = source[index];
            var b = reversal[index];
            if (a.AccountId != b.AccountId || a.DebitAmount != b.CreditAmount || a.CreditAmount != b.DebitAmount
                || a.TransactionDebitAmount != b.TransactionCreditAmount || a.TransactionCreditAmount != b.TransactionDebitAmount
                || a.ForeignCurrencyAmount != b.ForeignCurrencyAmount || a.ExchangeRateId != b.ExchangeRateId
                || a.ExchangeRate != b.ExchangeRate || a.ExchangeRateDate != b.ExchangeRateDate
                || a.SourceDocumentLineId != b.SourceDocumentLineId
                || !string.Equals(a.ExchangeRateSource, b.ExchangeRateSource, StringComparison.Ordinal)
                || !string.Equals(a.TransactionCurrency, b.TransactionCurrency, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(a.SourceReferenceNumber, b.SourceReferenceNumber, StringComparison.Ordinal)
                || !string.Equals(a.SegmentString, b.SegmentString, StringComparison.Ordinal)
                || a.FinanceDimensionSetId != b.DimensionSet?.Id || a.LineNumber != b.LineNumber)
                throw new InvalidOperationException("Exact reversal lines do not match immutable original posting evidence.");
        }
    }

    private static void ApplyOriginalReversalLinks(
        JournalEntry original,
        JournalEntry reversal,
        ValidatedPosting validation,
        DateTime now)
    {
        var originalLines = original.Transactions.OrderBy(t => t.LineNumber).ToList();
        var reversalLines = reversal.Transactions.OrderBy(t => t.LineNumber).ToList();
        if (originalLines.Count != reversalLines.Count)
        {
            throw new InvalidOperationException("Reversal line count does not match the original journal entry.");
        }

        // A reversal is a new posted accounting event; it does not make the original
        // journal unposted. Keep both entries reportable and use the linkage flags to
        // prevent duplicate reversals and explain the correction trail.
        original.IsReversed = true;
        original.ReversalDate = validation.PostingDate;
        original.ReversalJournalEntryId = reversal.Id;
        original.ReversalType = validation.ReversalType;
        original.ReversalReason = validation.ReversalReason;
        original.UpdatedAt = now;

        for (var i = 0; i < originalLines.Count; i++)
        {
            var originalLine = originalLines[i];
            var reversalLine = reversalLines[i];

            originalLine.IsReversed = true;
            originalLine.ReversalDate = validation.PostingDate;
            originalLine.ReversalTransactionId = reversalLine.Id;
            originalLine.ReversalType = validation.ReversalType;
            originalLine.ReversalReason = validation.ReversalReason;
            originalLine.UpdatedAt = now;

            reversalLine.OriginalTransactionId = originalLine.Id;
        }
    }

    private FinancePostingEvent BuildPostingEvent(
        Guid tenantId,
        ValidatedPosting validation,
        Guid journalEntryId,
        DateTime now,
        Guid? postedByUserId)
    {
        return new FinancePostingEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SourceModule = validation.SourceModule,
            OriginModuleCode = validation.OriginModuleCode,
            SourceDocumentType = validation.SourceDocumentType,
            SourceDocumentId = validation.SourceDocumentId,
            PostingAction = validation.PostingAction,
            SourceDocumentReference = validation.SourceDocumentReference,
            IdempotencyKey = validation.IdempotencyKey,
            RequestFingerprintVersion = validation.RequestFingerprintVersion,
            RequestFingerprint = validation.RequestFingerprint,
            JournalEntryId = journalEntryId,
            PostingStatus = PostedStatus,
            PostingDate = validation.PostingDate,
            RequestedAt = now,
            PostedAt = now,
            RequestedByUserId = postedByUserId,
            TotalDebitAmount = validation.TotalDebitAmount,
            TotalCreditAmount = validation.TotalCreditAmount,
            FunctionalCurrencyCode = validation.FunctionalCurrencyCode,
            HasForeignCurrencyLines = validation.IsMultiCurrency,
            PrimaryTransactionCurrencyCode = validation.PrimaryCurrency,
            PrimaryExchangeRateId = validation.PrimaryExchangeRateId,
            PrimaryExchangeRate = validation.PrimaryExchangeRate,
            PrimaryExchangeRateDate = validation.PrimaryExchangeRateDate,
            BookClassification = validation.AccountingBookCode,
            AccountingBookId = validation.AccountingBookId,
            CreatedAt = now,
            CreatedBy = _currentUserService.UserName,
            CreatedById = postedByUserId
        };
    }

    private async Task<IReadOnlyList<ParallelReplica>> BuildParallelReplicasAsync(
        Guid tenantId,
        ValidatedPosting validation,
        JournalEntry primaryJournal,
        DateTime now,
        Guid? postedByUserId,
        CancellationToken cancellationToken)
    {
        var primaryBook = await _context.AccountingBooks.AsNoTracking().SingleAsync(item =>
            item.TenantId == tenantId && item.Id == validation.AccountingBookId && !item.IsDeleted,
            cancellationToken);
        if (primaryBook.BookType != AccountingBookType.PrimaryFull)
            return Array.Empty<ParallelReplica>();

        var books = await _context.AccountingBooks.AsNoTracking()
            .Where(item => item.TenantId == tenantId
                && item.BookType == AccountingBookType.ParallelFull
                && item.BaseAccountingBookId == primaryBook.Id
                && item.LifecycleStatus == AccountingBookLifecycleStatus.Active
                && item.IsActive && item.AllowsPosting && !item.IsDeleted
                && item.ReplicationStartDate != null
                && item.ReplicationStartDate <= validation.PostingDate)
            .OrderBy(item => item.SortOrder).ThenBy(item => item.Code)
            .ToListAsync(cancellationToken);
        if (books.Count == 0) return Array.Empty<ParallelReplica>();

        var sourceAccountIds = primaryJournal.Transactions.Select(item => item.AccountId).Distinct().ToArray();
        var persistedSnapshotIds = primaryJournal.Transactions
            .Where(item => item.FinanceDimensionSnapshotId.HasValue && item.FinanceDimensionSnapshot is null)
            .Select(item => item.FinanceDimensionSnapshotId!.Value)
            .Distinct()
            .ToArray();
        var persistedSnapshots = persistedSnapshotIds.Length == 0
            ? new Dictionary<Guid, FinanceDimensionSnapshot>()
            : await _context.FinanceDimensionSnapshots.AsNoTracking()
                .Include(item => item.Items)
                .Where(item => item.TenantId == tenantId && persistedSnapshotIds.Contains(item.Id) && !item.IsDeleted)
                .ToDictionaryAsync(item => item.Id, cancellationToken);
        var replicas = new List<ParallelReplica>(books.Count);
        foreach (var book in books)
        {
            await EnsureBookYearIsOpenAsync(tenantId, book.Id, validation.FiscalPeriod.FiscalYearId, cancellationToken);
            if (string.IsNullOrWhiteSpace(book.FunctionalCurrencyCode)
                || string.Equals(book.FunctionalCurrencyCode, validation.FunctionalCurrencyCode, StringComparison.Ordinal))
                throw new InvalidOperationException($"PARALLEL_CURRENCY_INVALID: Parallel book {book.Code} must use a foreign currency.");
            var bookDecimalPlaces = await ResolveCurrencyDecimalPlacesAsync(
                tenantId, book.FunctionalCurrencyCode, cancellationToken);

            var mappedIds = await _context.AccountAccountingBooks.AsNoTracking()
                .Where(item => item.TenantId == tenantId && item.AccountingBookId == book.Id
                    && sourceAccountIds.Contains(item.AccountId) && item.IsEnabled && !item.IsDeleted)
                .Select(item => item.AccountId).Distinct().ToListAsync(cancellationToken);
            var missing = sourceAccountIds.Except(mappedIds).ToArray();
            if (missing.Length > 0)
                throw new InvalidOperationException($"PARALLEL_ACCOUNT_MAPPING_REQUIRED: Parallel book {book.Code} is missing {missing.Length} inherited account mapping(s).");

            var originalReplica = validation.ReversalOfJournalEntryId.HasValue
                ? await _context.JournalEntries.Include(item => item.Transactions).SingleOrDefaultAsync(item =>
                    item.TenantId == tenantId && item.AccountingBookId == book.Id
                    && item.ReplicatedFromJournalEntryId == validation.ReversalOfJournalEntryId.Value
                    && !item.IsDeleted, cancellationToken)
                : null;
            var rate = originalReplica is null
                ? await ResolveParallelRateAsync(tenantId, validation.FunctionalCurrencyCode,
                    book.FunctionalCurrencyCode, validation.PostingDate, cancellationToken)
                : await ResolveOriginalParallelRateAsync(originalReplica, cancellationToken);

            var lines = primaryJournal.Transactions.OrderBy(item => item.LineNumber).Select(source =>
            {
                var debit = RoundMoney(source.DebitAmount * rate.Rate, bookDecimalPlaces);
                var credit = RoundMoney(source.CreditAmount * rate.Rate, bookDecimalPlaces);
                FinanceDimensionSnapshot? sourceSnapshot = source.FinanceDimensionSnapshot;
                if (source.FinanceDimensionSnapshotId.HasValue && sourceSnapshot is null
                    && !persistedSnapshots.TryGetValue(source.FinanceDimensionSnapshotId.Value, out sourceSnapshot))
                    throw new InvalidOperationException(
                        $"PARALLEL_DIMENSION_EVIDENCE_MISSING: Source line {source.LineNumber} has no resolvable immutable dimension snapshot.");
                var replicaSnapshot = sourceSnapshot is null
                    ? null
                    : CloneParallelDimensionSnapshot(sourceSnapshot, tenantId, now, postedByUserId);
                if (replicaSnapshot is not null)
                    _context.FinanceDimensionSnapshots.Add(replicaSnapshot);
                return new AccountTransaction
                {
                    Id = Guid.NewGuid(), TenantId = tenantId, AccountId = source.AccountId,
                    TransactionDate = validation.PostingDate, Description = source.Description,
                    DebitAmount = debit, CreditAmount = credit,
                    FunctionalCurrencyCode = book.FunctionalCurrencyCode,
                    TransactionCurrency = validation.FunctionalCurrencyCode,
                    TransactionDebitAmount = source.DebitAmount,
                    TransactionCreditAmount = source.CreditAmount,
                    ForeignCurrencyAmount = source.DebitAmount > 0m ? source.DebitAmount : source.CreditAmount,
                    ExchangeRateId = rate.Id, ExchangeRate = rate.Rate,
                    ExchangeRateSource = rate.Source, ExchangeRateDate = rate.Date,
                    FinanceDimensionSetId = source.FinanceDimensionSetId,
                    FinanceDimensionSnapshotId = replicaSnapshot?.Id,
                    SourceModule = source.SourceModule, SourceDocumentId = source.SourceDocumentId,
                    SourceDocumentLineId = source.SourceDocumentLineId,
                    SourceDocumentType = source.SourceDocumentType,
                    SourceReferenceNumber = source.SourceReferenceNumber,
                    BookClassification = book.Code, AccountingBookId = book.Id,
                    FiscalPeriodId = validation.FiscalPeriod.Id, PostedDate = now,
                    PostingStatus = PostedStatus, SegmentString = source.SegmentString,
                    LineNumber = source.LineNumber, Notes = source.Notes,
                    TransactionTag = "Parallel replica", CreatedAt = now,
                    CreatedBy = _currentUserService.UserName, CreatedById = postedByUserId
                };
            }).ToList();

            var debitTotal = RoundMoney(lines.Sum(item => item.DebitAmount), bookDecimalPlaces);
            var creditTotal = RoundMoney(lines.Sum(item => item.CreditAmount), bookDecimalPlaces);
            var residual = RoundMoney(debitTotal - creditTotal, bookDecimalPlaces);
            if (residual != 0m)
            {
                if (!book.CurrencyRoundingAccountId.HasValue)
                    throw new InvalidOperationException($"PARALLEL_ROUNDING_ACCOUNT_REQUIRED: Parallel book {book.Code} has no protected rounding account.");
                var roundingMapped = await _context.AccountAccountingBooks.AsNoTracking().AnyAsync(item =>
                    item.TenantId == tenantId && item.AccountingBookId == book.Id
                    && item.AccountId == book.CurrencyRoundingAccountId.Value
                    && item.IsEnabled && !item.IsDeleted, cancellationToken);
                if (!roundingMapped)
                    throw new InvalidOperationException($"PARALLEL_ROUNDING_ACCOUNT_MAPPING_REQUIRED: Parallel book {book.Code} rounding account is not enabled.");
                lines.Add(new AccountTransaction
                {
                    Id = Guid.NewGuid(), TenantId = tenantId,
                    AccountId = book.CurrencyRoundingAccountId.Value,
                    TransactionDate = validation.PostingDate,
                    Description = $"Parallel conversion rounding for {primaryJournal.JournalEntryNumber}",
                    DebitAmount = residual < 0m ? Math.Abs(residual) : 0m,
                    CreditAmount = residual > 0m ? residual : 0m,
                    FunctionalCurrencyCode = book.FunctionalCurrencyCode,
                    TransactionCurrency = book.FunctionalCurrencyCode,
                    TransactionDebitAmount = residual < 0m ? Math.Abs(residual) : 0m,
                    TransactionCreditAmount = residual > 0m ? residual : 0m,
                    BookClassification = book.Code, AccountingBookId = book.Id,
                    FiscalPeriodId = validation.FiscalPeriod.Id, PostedDate = now,
                    PostingStatus = PostedStatus, LineNumber = lines.Count + 1,
                    TransactionTag = "Parallel rounding", CreatedAt = now,
                    CreatedBy = _currentUserService.UserName, CreatedById = postedByUserId
                });
                debitTotal = RoundMoney(lines.Sum(item => item.DebitAmount), bookDecimalPlaces);
                creditTotal = RoundMoney(lines.Sum(item => item.CreditAmount), bookDecimalPlaces);
            }

            var journal = new JournalEntry
            {
                Id = Guid.NewGuid(), TenantId = tenantId,
                JournalEntryNumber = GeneratePostingJournalNumber("FXREP", now),
                JournalType = "System Generated", EntryDate = validation.PostingDate,
                Description = $"{primaryJournal.Description} — {book.Code} translated replica",
                ReferenceNumber = primaryJournal.ReferenceNumber,
                SourceModule = primaryJournal.SourceModule, OriginModuleCode = primaryJournal.OriginModuleCode,
                SourceDocumentId = primaryJournal.SourceDocumentId,
                SourceDocumentType = primaryJournal.SourceDocumentType,
                TotalDebitAmount = debitTotal, TotalCreditAmount = creditTotal,
                BalanceDifference = debitTotal - creditTotal, IsBalanced = debitTotal == creditTotal,
                IsMultiCurrency = true, PrimaryCurrency = book.FunctionalCurrencyCode,
                BookClassification = book.Code, AccountingBookId = book.Id,
                FiscalPeriodId = validation.FiscalPeriod.Id, PostingDate = now,
                PostedByUserId = postedByUserId, PostingStatus = PostedStatus,
                ApprovalStatus = "System replica", ReplicatedFromJournalEntryId = primaryJournal.Id,
                ReplicationExchangeRateId = rate.Id, ReplicationExchangeRate = rate.Rate,
                ReplicationRateDate = rate.Date, ReplicationRateSource = rate.Source,
                OriginalJournalEntryId = originalReplica?.Id,
                ReversalType = originalReplica is null ? null : validation.ReversalType,
                ReversalReason = originalReplica is null ? null : validation.ReversalReason,
                CreatedAt = now, CreatedBy = _currentUserService.UserName, CreatedById = postedByUserId,
                Transactions = lines
            };
            foreach (var line in lines) line.JournalEntryId = journal.Id;
            if (originalReplica != null)
            {
                originalReplica.IsReversed = true;
                originalReplica.ReversalDate = validation.PostingDate;
                originalReplica.ReversalJournalEntryId = journal.Id;
                originalReplica.ReversalType = validation.ReversalType;
                originalReplica.ReversalReason = validation.ReversalReason;
                var originalLines = originalReplica.Transactions.OrderBy(item => item.LineNumber).ToList();
                var reversalLines = lines.OrderBy(item => item.LineNumber).ToList();
                for (var index = 0; index < Math.Min(originalLines.Count, reversalLines.Count); index++)
                {
                    originalLines[index].IsReversed = true;
                    originalLines[index].ReversalDate = validation.PostingDate;
                    originalLines[index].ReversalTransactionId = reversalLines[index].Id;
                    reversalLines[index].OriginalTransactionId = originalLines[index].Id;
                }
            }

            var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                $"PARALLEL-REPLICA-V1|{validation.RequestFingerprint}|{book.Id:N}|{rate.Id:N}|{rate.Rate.ToString(CultureInfo.InvariantCulture)}")));
            var eventRow = new FinancePostingEvent
            {
                Id = Guid.NewGuid(), TenantId = tenantId,
                SourceModule = validation.SourceModule, OriginModuleCode = validation.OriginModuleCode,
                SourceDocumentType = validation.SourceDocumentType, SourceDocumentId = validation.SourceDocumentId,
                PostingAction = validation.PostingAction, SourceDocumentReference = validation.SourceDocumentReference,
                IdempotencyKey = ParallelIdempotencyKey(validation.IdempotencyKey, book.Code),
                RequestFingerprintVersion = "FINPOST-PARALLEL-V1", RequestFingerprint = fingerprint,
                JournalEntryId = journal.Id, PostingStatus = PostedStatus,
                PostingDate = validation.PostingDate, RequestedAt = now, PostedAt = now,
                RequestedByUserId = postedByUserId, TotalDebitAmount = debitTotal,
                TotalCreditAmount = creditTotal, FunctionalCurrencyCode = book.FunctionalCurrencyCode,
                HasForeignCurrencyLines = true, PrimaryTransactionCurrencyCode = validation.FunctionalCurrencyCode,
                PrimaryExchangeRateId = rate.Id, PrimaryExchangeRate = rate.Rate,
                PrimaryExchangeRateDate = rate.Date, BookClassification = book.Code,
                AccountingBookId = book.Id, CreatedAt = now,
                CreatedBy = _currentUserService.UserName, CreatedById = postedByUserId
            };

            rate.Entity.HasBeenUsedInTransactions = true;
            rate.Entity.TransactionCount += lines.Count;
            rate.Entity.FirstUsedDate ??= now;
            rate.Entity.LastUsedDate = now;
            replicas.Add(new ParallelReplica(book, journal, eventRow));
        }
        return replicas;
    }

    private FinanceDimensionSnapshot CloneParallelDimensionSnapshot(
        FinanceDimensionSnapshot source,
        Guid tenantId,
        DateTime now,
        Guid? actorId)
    {
        var clone = new FinanceDimensionSnapshot
        {
            Id = Guid.NewGuid(), TenantId = tenantId,
            FinanceDimensionSetId = source.FinanceDimensionSetId,
            CombinationHashSnapshot = source.CombinationHashSnapshot,
            DisplayValueSnapshot = source.DisplayValueSnapshot,
            SnapshotSource = "ParallelReplica", SnapshotCapturedAt = now,
            SnapshotQuality = source.SnapshotQuality,
            HistoricalNameReconstructed = source.HistoricalNameReconstructed,
            RuleEvidenceHash = source.RuleEvidenceHash,
            ProducerModule = source.ProducerModule,
            SourceRoute = source.SourceRoute,
            SourceDocumentType = source.SourceDocumentType,
            ContractVersion = source.ContractVersion,
            CreatedAt = now, CreatedBy = _currentUserService.UserName, CreatedById = actorId
        };
        foreach (var item in source.Items)
        {
            clone.Items.Add(new FinanceDimensionSnapshotItem
            {
                Id = Guid.NewGuid(), TenantId = tenantId, FinanceDimensionSnapshotId = clone.Id,
                FinanceDimensionDefinitionId = item.FinanceDimensionDefinitionId,
                FinanceDimensionValueId = item.FinanceDimensionValueId,
                DimensionCodeSnapshot = item.DimensionCodeSnapshot,
                DimensionNameSnapshot = item.DimensionNameSnapshot,
                DimensionValueCodeSnapshot = item.DimensionValueCodeSnapshot,
                DimensionValueNameSnapshot = item.DimensionValueNameSnapshot,
                FinanceDimensionAccountRuleId = item.FinanceDimensionAccountRuleId,
                RuleFamilyIdSnapshot = item.RuleFamilyIdSnapshot,
                RuleVersionSnapshot = item.RuleVersionSnapshot,
                RuleTypeSnapshot = item.RuleTypeSnapshot,
                RuleEffectiveDateSnapshot = item.RuleEffectiveDateSnapshot,
                RuleExpiryDateSnapshot = item.RuleExpiryDateSnapshot,
                SnapshotSource = "ParallelReplica", SnapshotCapturedAt = now,
                SnapshotQuality = item.SnapshotQuality,
                HistoricalNameReconstructed = item.HistoricalNameReconstructed,
                CreatedAt = now, CreatedBy = _currentUserService.UserName, CreatedById = actorId
            });
        }
        return clone;
    }

    private async Task<ParallelRate> ResolveParallelRateAsync(Guid tenantId, string sourceCurrency,
        string targetCurrency, DateTime accountingDate, CancellationToken cancellationToken)
    {
        var accountingDay = accountingDate.Date;
        var nextAccountingDay = accountingDay.AddDays(1);
        var rate = await _context.ExchangeRates
            .Where(item => item.TenantId == tenantId && !item.IsDeleted && item.IsActive
                && (item.ApprovalStatus == RateApprovalStatus.Approved
                    || item.ApprovalStatus == RateApprovalStatus.AutoApproved)
                && item.BaseCurrencyCode == sourceCurrency && item.TargetCurrencyCode == targetCurrency
                && item.QuoteSide == ExchangeRateQuoteSide.Mid
                && item.RateType == ExchangeRateType.Daily
                && item.EffectiveDate >= accountingDay
                && item.EffectiveDate < nextAccountingDay
                && (item.EndDate == null || item.EndDate >= accountingDay))
            .OrderByDescending(item => item.EffectiveDate).ThenByDescending(item => item.Priority)
            .ThenByDescending(item => item.CreatedDate)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException(
                $"PARALLEL_EXCHANGE_RATE_REQUIRED: Posting was not completed because the active {targetCurrency} Parallel book requires an approved exact-date Daily {sourceCurrency}/{targetCurrency} exchange rate for accounting date {accountingDate:yyyy-MM-dd}. No Primary or Parallel ledger posting was committed. Add and approve the missing rate under Finance > Exchange Rates, then retry the posting action.");
        // Canonical storage is source-to-target: 1 BaseCurrency = Rate TargetCurrency.
        if (rate.Rate <= 0m)
            throw new InvalidOperationException("PARALLEL_EXCHANGE_RATE_INVALID: Approved Parallel source-to-target rate must be greater than zero.");
        return new ParallelRate(rate, rate.Id, rate.Rate, rate.EffectiveDate.Date, rate.RateSource);
    }

    private async Task<ParallelRate> ResolveOriginalParallelRateAsync(JournalEntry originalReplica,
        CancellationToken cancellationToken)
    {
        if (!originalReplica.ReplicationExchangeRateId.HasValue
            || !originalReplica.ReplicationExchangeRate.HasValue
            || !originalReplica.ReplicationRateDate.HasValue)
            throw new InvalidOperationException("PARALLEL_REVERSAL_RATE_EVIDENCE_MISSING: Original Parallel replica lacks immutable rate evidence.");
        var entity = await _context.ExchangeRates.SingleAsync(item =>
            item.Id == originalReplica.ReplicationExchangeRateId.Value && !item.IsDeleted, cancellationToken);
        return new ParallelRate(entity, entity.Id, originalReplica.ReplicationExchangeRate.Value,
            originalReplica.ReplicationRateDate.Value.Date,
            originalReplica.ReplicationRateSource ?? entity.RateSource);
    }

    private static string? ParallelIdempotencyKey(string? source, string bookCode)
    {
        if (string.IsNullOrWhiteSpace(source)) return null;
        var value = $"{source}:parallel:{bookCode}";
        return value.Length <= 450 ? value : value[..450];
    }

    private static decimal GetAccountBalanceDelta(AccountType accountType, AccountTransaction transaction)
    {
        if (transaction.DebitAmount > 0)
        {
            if (accountType == AccountType.Asset || accountType == AccountType.Expense)
            {
                return transaction.DebitAmount;
            }

            return -transaction.DebitAmount;
        }

        if (accountType == AccountType.Liability || accountType == AccountType.Equity || accountType == AccountType.Revenue)
        {
            return transaction.CreditAmount;
        }

        return -transaction.CreditAmount;
    }

    private static decimal GetTransactionCurrencyBalanceDelta(AccountType accountType, AccountTransaction transaction)
    {
        var debitAmount = transaction.TransactionDebitAmount
            ?? (transaction.DebitAmount > 0m ? transaction.DebitAmount : 0m);
        var creditAmount = transaction.TransactionCreditAmount
            ?? (transaction.CreditAmount > 0m ? transaction.CreditAmount : 0m);

        if (debitAmount > 0m)
        {
            if (accountType == AccountType.Asset || accountType == AccountType.Expense)
            {
                return debitAmount;
            }

            return -debitAmount;
        }

        if (accountType == AccountType.Liability || accountType == AccountType.Equity || accountType == AccountType.Revenue)
        {
            return creditAmount;
        }

        return -creditAmount;
    }


    private async Task ApplyAccountCurrencyLinkMovementsAsync(
        Guid tenantId,
        IEnumerable<AccountTransaction> transactions,
        DateTime now,
        Guid? postedByUserId,
        CancellationToken cancellationToken)
    {
        var transactionList = transactions
            .Where(t => !t.IsDeleted && !string.IsNullOrWhiteSpace(t.TransactionCurrency))
            .OrderBy(t => t.TransactionDate)
            .ThenBy(t => t.LineNumber)
            .ToList();

        if (transactionList.Count == 0)
        {
            return;
        }

        var accountIds = transactionList
            .Select(t => t.AccountId)
            .Distinct()
            .ToList();

        var accounts = await _context.Accounts
            .Where(a => a.TenantId == tenantId && accountIds.Contains(a.Id) && !a.IsDeleted)
            .Select(a => new { a.Id, a.AccountType })
            .ToDictionaryAsync(a => a.Id, a => a.AccountType, cancellationToken);

        if (accounts.Count == 0)
        {
            return;
        }

        var links = await _context.AccountCurrencyLinks
            .Where(l => l.TenantId == tenantId
                && accountIds.Contains(l.AccountId)
                && l.IsActive
                && !l.IsDeleted)
            .ToListAsync(cancellationToken);

        if (links.Count == 0)
        {
            return;
        }

        var linksByAccountCurrency = links
            .GroupBy(l => new
            {
                l.AccountId,
                CurrencyCode = NormalizeCurrency(l.LinkedCurrencyCode, "Linked currency")
            })
            .ToDictionary(
                g => (g.Key.AccountId, g.Key.CurrencyCode),
                g => g.OrderByDescending(l => l.EffectiveDate).First());

        foreach (var group in transactionList.GroupBy(t => new
        {
            t.AccountId,
            CurrencyCode = NormalizeCurrency(t.TransactionCurrency, "Transaction currency")
        }))
        {
            if (!accounts.TryGetValue(group.Key.AccountId, out var accountType)
                || !linksByAccountCurrency.TryGetValue((group.Key.AccountId, group.Key.CurrencyCode), out var link))
            {
                continue;
            }

            var foreignDelta = group.Sum(t => GetTransactionCurrencyBalanceDelta(accountType, t));
            var functionalDelta = group.Sum(t => GetAccountBalanceDelta(accountType, t));
            var groupLines = group.ToList();
            var firstDate = groupLines.Min(t => t.TransactionDate.Date);
            var lastLine = groupLines
                .OrderByDescending(t => t.TransactionDate)
                .ThenByDescending(t => t.LineNumber)
                .First();
            var transactionDecimalPlaces = await ResolveCurrencyDecimalPlacesAsync(
                tenantId, group.Key.CurrencyCode, cancellationToken);
            var functionalDecimalPlaces = await ResolveCurrencyDecimalPlacesAsync(
                tenantId, lastLine.FunctionalCurrencyCode, cancellationToken);

            link.ForeignCurrencyBalance = RoundMoney(
                link.ForeignCurrencyBalance + foreignDelta, transactionDecimalPlaces);
            link.BaseCurrencyEquivalent = RoundMoney(
                link.BaseCurrencyEquivalent + functionalDelta, functionalDecimalPlaces);
            link.HasTransactionHistory = true;
            link.TransactionCount += groupLines.Count;
            link.FirstTransactionDate = link.FirstTransactionDate.HasValue
                ? (link.FirstTransactionDate.Value.Date <= firstDate ? link.FirstTransactionDate.Value.Date : firstDate)
                : firstDate;
            link.LastTransactionDate = link.LastTransactionDate.HasValue
                ? (link.LastTransactionDate.Value.Date >= lastLine.TransactionDate.Date ? link.LastTransactionDate.Value.Date : lastLine.TransactionDate.Date)
                : lastLine.TransactionDate.Date;

            if (lastLine.ExchangeRate is > 0m)
            {
                link.CurrentExchangeRate = RoundRate(lastLine.ExchangeRate.Value);
                link.RateEffectiveDate = lastLine.ExchangeRateDate?.Date ?? lastLine.TransactionDate.Date;
            }
            else if (string.Equals(group.Key.CurrencyCode, lastLine.FunctionalCurrencyCode, StringComparison.OrdinalIgnoreCase))
            {
                link.CurrentExchangeRate = 1m;
                link.RateEffectiveDate = lastLine.TransactionDate.Date;
            }

            link.UpdatedAt = now;
            link.UpdatedBy = _currentUserService.UserName;
            link.LastModifiedById = postedByUserId;
            link.ModifiedByUserId = postedByUserId;
            link.ModifiedDate = now;
        }
    }

    private async Task<ValidatedPosting> ValidatePostingRequestAsync(
        Guid tenantId,
        FinancePostingCommandDto request,
        string accountingBookCode,
        FinancePostingProducerContext? producerContext,
        bool allowHistoricalMappingException,
        CancellationToken cancellationToken,
        YearEndBookCloseCycle? yearEndCycle = null)
    {
        YearEndClosingPlan? yearEndPlan = null;
        if (yearEndCycle?.Status == "Closing")
        {
            yearEndPlan = await YearEndClosingPlan.BuildAsync(_context, yearEndCycle, cancellationToken);
            yearEndPlan.RequireExactLines(request.Lines);
        }
        if (!await _context.Tenants.AnyAsync(t => t.Id == tenantId && !t.IsDeleted, cancellationToken))
        {
            throw new InvalidOperationException("Finance tenant context is invalid.");
        }

        if (request.SourceDocumentTenantId.HasValue && request.SourceDocumentTenantId.Value != tenantId)
        {
            throw new InvalidOperationException("Source document belongs to another tenant.");
        }

        var sourceModule = NormalizeRequired(request.SourceModule, "Source module", 50);
        var originModuleCode = FinanceModuleLockCatalog.ResolveOriginModuleCode(
            sourceModule,
            request.OriginModuleCode);
        var sourceDocumentType = NormalizeRequired(request.SourceDocumentType, "Source document type", 100);
        if (yearEndCycle == null && (string.Equals(sourceDocumentType, "YearEndClose", StringComparison.OrdinalIgnoreCase)
            || string.Equals(sourceDocumentType, "YearEndCloseReversal", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Year-end postings must use the governed book-close cycle workflow.");
        var route = producerContext?.Definition
            ?? FinanceDimensionRouteCatalog.MatchLegacyPosting(sourceModule, sourceDocumentType);
        if (producerContext is not null
            && (!string.Equals(route!.PostingSourceModule, sourceModule, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(route.DocumentType, sourceDocumentType, StringComparison.Ordinal)))
            throw new InvalidOperationException("Finance posting producer context does not match the request source module/document type.");
        var certificationState = route is null
            ? FinanceDimensionCertificationState.LegacyReadOnly
            : await ResolveDimensionCertificationStateAsync(tenantId, route, cancellationToken);
        var postingAction = NormalizeRequired(request.PostingAction, "Posting action", 50);
        var description = NormalizeRequired(request.Description, "Posting description", 500);
        var journalType = NormalizeRequired(request.JournalType, "Journal type", 50);
        var normalizedAccountingBookCode = NormalizeRequired(accountingBookCode, "Accounting book code", 20).ToUpperInvariant();
        if (string.Equals(normalizedAccountingBookCode, "ALL_ACTIVE_BOOKS", StringComparison.Ordinal))
        {
            await RecordAccountingBookAuthorityDenialAsync(
                tenantId, request, normalizedAccountingBookCode, "BOOK_CODE_PSEUDO", cancellationToken);
            throw new InvalidOperationException("ALL_ACTIVE_BOOKS must be expanded by source orchestration before single-book posting.");
        }
        // Resolve the concrete tenant-owned book once. Every persisted row and every duplicate,
        // retry, and reversal lookup below carries this ID plus the immutable code snapshot.
        var accountingBook = await _context.AccountingBooks.AsNoTracking().Include(item => item.BaseAccountingBook)
            .SingleOrDefaultAsync(item => item.TenantId == tenantId && item.Code == normalizedAccountingBookCode && !item.IsDeleted, cancellationToken);
        if (accountingBook == null)
        {
            await RecordAccountingBookAuthorityDenialAsync(
                tenantId, request, normalizedAccountingBookCode, "BOOK_UNAVAILABLE", cancellationToken);
            throw new InvalidOperationException("Accounting book is unavailable for this tenant.");
        }
        if ((!accountingBook.IsActive || !accountingBook.AllowsPosting) && !allowHistoricalMappingException)
        {
            await RecordAccountingBookAuthorityDenialAsync(
                tenantId, request, normalizedAccountingBookCode, "BOOK_NOT_POSTABLE", cancellationToken);
            throw new InvalidOperationException("Accounting book is unavailable for posting.");
        }
        if (accountingBook.BookType == AccountingBookType.ParallelFull && yearEndCycle == null)
            throw new InvalidOperationException("PARALLEL_DIRECT_POSTING_FORBIDDEN: Parallel books accept only immutable system-generated replicas of Primary postings.");
        if (accountingBook.BookType == AccountingBookType.PrimaryFull
            && (accountingBook.EffectiveFromUtc.HasValue || accountingBook.EffectiveToUtc.HasValue))
            throw new InvalidOperationException("PRIMARY_BOOK_EFFECTIVE_DATE_INVALID: The tenant Primary book must be perpetual.");
        var defaultPostingBooks = await _context.AccountingBooks.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.IsDefault && !item.IsDeleted
                && (allowHistoricalMappingException || (item.IsActive && item.AllowsPosting)))
            .Take(2).ToListAsync(cancellationToken);
        if (defaultPostingBooks.Count != 1)
            throw new InvalidOperationException(
                "PRIMARY_BOOK_AUTHORITY_AMBIGUOUS: Exactly one active default posting book is required before posting.");
        var requestedFunctionalCurrency = NormalizeCurrency(request.FunctionalCurrencyCode, "Functional currency");
        var functionalCurrencyConfig = await ResolveTenantFunctionalCurrencyAsync(tenantId, cancellationToken);
        var functionalCurrency = accountingBook.BookType == AccountingBookType.Delta
            ? accountingBook.BaseAccountingBook?.FunctionalCurrencyCode
                ?? throw new InvalidOperationException("DELTA_BASE_CURRENCY_REQUIRED: Delta base-book currency authority is missing.")
            : accountingBook.FunctionalCurrencyCode
                ?? throw new InvalidOperationException("BOOK_FUNCTIONAL_CURRENCY_REQUIRED: Full-book currency authority is missing.");
        var functionalDecimalPlaces = await ResolveCurrencyDecimalPlacesAsync(
            tenantId, functionalCurrency, cancellationToken);
        if (!string.Equals(requestedFunctionalCurrency, functionalCurrency, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Posting functional currency does not match the selected book currency.");
        }
        var sourceReference = NormalizeOptional(request.SourceDocumentReference, 100, "Source document reference");
        var idempotencyKey = NormalizeOptional(request.IdempotencyKey, 450, "Idempotency key");

        if (request.SourceDocumentId == Guid.Empty)
        {
            throw new InvalidOperationException("Source document id is required.");
        }

        if (request.ExistingJournalEntryId.HasValue && request.ReversalOfJournalEntryId.HasValue)
        {
            throw new InvalidOperationException("A posting request cannot both post an existing journal and reverse another journal.");
        }

        if (request.ExistingJournalEntryId.HasValue && request.ExistingJournalEntryId.Value == Guid.Empty)
        {
            throw new InvalidOperationException("Existing journal entry id is invalid.");
        }

        if (request.ExistingJournalEntryId.HasValue && request.SourceDocumentId != request.ExistingJournalEntryId.Value)
        {
            throw new InvalidOperationException("Existing journal postings must use the journal entry id as the source document id.");
        }

        if (request.ReversalOfJournalEntryId.HasValue && request.ReversalOfJournalEntryId.Value == Guid.Empty)
        {
            throw new InvalidOperationException("Original journal entry id is invalid.");
        }

        var reversalReason = NormalizeOptional(request.ReversalReason, 500, "Reversal reason");
        var reversalType = NormalizeOptional(request.ReversalType, 20, "Reversal type");
        if (request.ReversalOfJournalEntryId.HasValue)
        {
            if (string.IsNullOrWhiteSpace(reversalReason))
            {
                throw new InvalidOperationException("A reversal reason is required.");
            }

            reversalType ??= "Manual";
        }

        if (request.PostingDate == default)
        {
            throw new InvalidOperationException("Posting date is required.");
        }

        var postingDate = request.PostingDate.Date;
        if (accountingBook.BookType == AccountingBookType.Delta)
        {
            if (accountingBook.BaseAccountingBook is not { LifecycleStatus: AccountingBookLifecycleStatus.Active, IsActive: true, AllowsPosting: true })
                throw new InvalidOperationException("DELTA_BASE_NOT_ACTIVE: New Delta postings require an active full base book.");
            if (accountingBook.EffectiveFromUtc?.Date > postingDate || accountingBook.EffectiveToUtc?.Date < postingDate)
                throw new InvalidOperationException("DELTA_POSTING_WINDOW_CLOSED: The accounting date is outside this Delta book's optional posting window.");
        }
        var fiscalPeriod = await ResolveFiscalPeriodAsync(tenantId, postingDate, request.FiscalPeriodId, cancellationToken);
        // Year-end closing may target the closed final period, but an explicit period lock is
        // still authoritative and must be lifted through the controlled reopen process first.
        var isYearEndClosePosting = yearEndCycle != null && request.AllowPostingToClosedPeriod
            && !fiscalPeriod.IsLocked
            && string.Equals(sourceModule, "GL", StringComparison.OrdinalIgnoreCase)
            && (string.Equals(sourceDocumentType, "YearEndClose", StringComparison.OrdinalIgnoreCase)
                || string.Equals(sourceDocumentType, "YearEndCloseReversal", StringComparison.OrdinalIgnoreCase));

        if (!isYearEndClosePosting)
        {
            var accountingBookPeriod = await _context.AccountingBookPeriods
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.TenantId == tenantId
                    && item.AccountingBookId == accountingBook.Id
                    && item.FiscalPeriodId == fiscalPeriod.Id
                    && !item.IsDeleted,
                    cancellationToken);
            if (accountingBookPeriod == null)
                throw new InvalidOperationException(
                    "ACCOUNTING_BOOK_PERIOD_REQUIRED: The selected accounting book has no governed authority for this fiscal period.");
            if (accountingBookPeriod.PeriodStatus != AccountingBookPeriodStatus.Open)
            {
                await RecordPostingBlockedByBookPeriodAuditAsync(
                    tenantId,
                    request,
                    accountingBook,
                    accountingBookPeriod,
                    fiscalPeriod,
                    postingDate,
                    cancellationToken);
                throw new InvalidOperationException(
                    $"ACCOUNTING_BOOK_PERIOD_NOT_OPEN: Accounting book '{accountingBook.Code}' is {accountingBookPeriod.PeriodStatus} for fiscal period '{fiscalPeriod.PeriodCode}'.");
            }
        }

        if (yearEndCycle == null)
            await EnsureBookYearIsOpenAsync(tenantId, accountingBook.Id, fiscalPeriod.FiscalYearId, cancellationToken);

        if ((!fiscalPeriod.IsOpen || fiscalPeriod.IsClosed || fiscalPeriod.IsLocked) && !isYearEndClosePosting)
        {
            await RecordPostingBlockedByPeriodAuditAsync(tenantId, request, fiscalPeriod, postingDate, cancellationToken);
            var periodState = fiscalPeriod.IsLocked ? "locked" : fiscalPeriod.IsClosed ? "closed" : "not open";
            throw new InvalidOperationException(
                $"Posting period is not open. Posting date {postingDate:yyyy-MM-dd}, book '{accountingBook.Code}', " +
                $"fiscal period '{fiscalPeriod.PeriodCode}' ({fiscalPeriod.PeriodName}) is {periodState}. " +
                "Ask Finance to open the tenant fiscal period through the approved workflow, or correct the document date.");
        }

        if (!isYearEndClosePosting)
            await EnsureOriginModuleCanPostAsync(
                tenantId,
                fiscalPeriod,
                originModuleCode,
                request,
                cancellationToken);

        if (postingDate < fiscalPeriod.StartDate.Date || postingDate > fiscalPeriod.EndDate.Date)
        {
            throw new InvalidOperationException("Posting date does not fall inside the fiscal period.");
        }

        // Period status answers whether the ledger accepts postings at all. This independent
        // policy answers whether Finance may recognize a transaction after today's business date.
        // Drafting and workflow approval remain possible; only the irreversible posting boundary
        // is blocked unless an administrator has explicitly enabled and audited future dating.
        if (postingDate > DateTime.UtcNow.Date && !fiscalPeriod.AllowFutureDating)
        {
            await RecordFutureDatedPostingBlockedAuditAsync(
                tenantId,
                request,
                fiscalPeriod,
                postingDate,
                cancellationToken);
            throw new InvalidOperationException(
                $"Future-dated posting is not allowed for fiscal period '{fiscalPeriod.PeriodCode}'.");
        }

        var requestedLines = request.Lines?.ToList() ?? new List<FinancePostingLineDto>();
        if (requestedLines.Count == 0)
        {
            throw new InvalidOperationException("At least two posting lines are required.");
        }

        var lineNumber = 1;
        var normalizedLines = new List<ValidatedPostingLine>();
        var ratePolicySettings = await _context.FinanceSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && !s.IsDeleted, cancellationToken);
        var selectedPolicies = new Dictionary<string, ExchangeRatePolicy>(StringComparer.OrdinalIgnoreCase);
        var exchangeRatePolicyOverrideUsed = false;
        foreach (var line in requestedLines)
        {
            if (line.AccountId == Guid.Empty)
            {
                throw new InvalidOperationException("Posting line account is required.");
            }

            var lineCurrency = NormalizeCurrency(line.TransactionCurrency, "Transaction currency", functionalCurrency);
            var transactionDecimalPlaces = await ResolveCurrencyDecimalPlacesAsync(
                tenantId, lineCurrency, cancellationToken);
            var debit = RoundMoney(line.DebitAmount, functionalDecimalPlaces);
            var credit = RoundMoney(line.CreditAmount, functionalDecimalPlaces);
            if (debit < 0 || credit < 0)
            {
                throw new InvalidOperationException("Posting amounts cannot be negative.");
            }

            if ((debit > 0 && credit > 0) || (debit == 0 && credit == 0))
            {
                throw new InvalidOperationException("Each posting line must contain either a debit or a credit amount.");
            }

            var exchangeRate = line.ExchangeRate;
            var foreignAmount = line.ForeignCurrencyAmount;
            var transactionDebit = line.TransactionDebitAmount;
            var transactionCredit = line.TransactionCreditAmount;
            Guid? exchangeRateId = line.ExchangeRateId;
            DateTime? exchangeRateDate = line.ExchangeRateDate;
            var exchangeRateSource = NormalizeOptional(line.ExchangeRateSource, 100, "Exchange-rate source");
            if (!string.Equals(lineCurrency, functionalCurrency, StringComparison.OrdinalIgnoreCase))
            {
                if (!functionalCurrencyConfig.IsConfigured)
                {
                    await RecordForeignCurrencyPostingBlockedAuditAsync(
                        tenantId,
                        request,
                        lineCurrency,
                        "Tenant functional currency must be configured before foreign-currency posting.",
                        cancellationToken);
                    throw new InvalidOperationException("Tenant functional currency must be configured before foreign-currency posting.");
                }

                if (!foreignAmount.HasValue || foreignAmount.Value == 0)
                {
                    throw new InvalidOperationException("Foreign-currency posting lines require the original foreign amount.");
                }

                var ratePolicy = await ResolveExchangeRatePolicyAsync(
                    tenantId,
                    line.AccountId,
                    lineCurrency,
                    sourceModule,
                    sourceDocumentType,
                    postingDate,
                    ratePolicySettings,
                    request,
                    cancellationToken);

                if (selectedPolicies.TryGetValue(lineCurrency, out var selectedPolicy)
                    && (selectedPolicy.RateType != ratePolicy.RateType || selectedPolicy.QuoteSide != ratePolicy.QuoteSide))
                {
                    throw new InvalidOperationException(
                        $"Foreign-currency lines for {lineCurrency} have conflicting account rate policies. Use one approved document-level rate override so the posting remains balanced.");
                }

                selectedPolicies[lineCurrency] = ratePolicy;
                exchangeRatePolicyOverrideUsed |= ratePolicy.IsOverride;

                var rateSnapshot = await ResolveExchangeRateSnapshotAsync(
                    tenantId,
                    functionalCurrency,
                    lineCurrency,
                    postingDate,
                    line.ExchangeRateId,
                    line.ExchangeRate,
                    ratePolicy,
                    line.TransactionTag,
                    ratePolicySettings?.RequireExchangeRateOverrideApproval ?? true,
                    request,
                    cancellationToken);

                exchangeRatePolicyOverrideUsed |= rateSnapshot.PolicyOverrideUsed;
                exchangeRateId = rateSnapshot.ExchangeRateId;
                exchangeRate = rateSnapshot.Rate;
                exchangeRateSource = rateSnapshot.RateSource;
                exchangeRateDate = rateSnapshot.RateDate;
                transactionDebit ??= debit > 0 ? foreignAmount : 0m;
                transactionCredit ??= credit > 0 ? foreignAmount : 0m;
                transactionDebit = RoundMoney(transactionDebit.GetValueOrDefault(), transactionDecimalPlaces);
                transactionCredit = RoundMoney(transactionCredit.GetValueOrDefault(), transactionDecimalPlaces);
                foreignAmount = RoundMoney(foreignAmount.Value, transactionDecimalPlaces);

                if ((transactionDebit.GetValueOrDefault() > 0 && transactionCredit.GetValueOrDefault() > 0)
                    || (transactionDebit.GetValueOrDefault() == 0 && transactionCredit.GetValueOrDefault() == 0))
                {
                    throw new InvalidOperationException("Foreign-currency posting lines require either a transaction-currency debit or credit amount.");
                }

                var expectedFunctional = RoundMoney((transactionDebit.GetValueOrDefault() > 0
                    ? transactionDebit.GetValueOrDefault()
                    : transactionCredit.GetValueOrDefault()) * exchangeRate.Value, functionalDecimalPlaces);
                var actualFunctional = debit > 0 ? debit : credit;
                if (expectedFunctional != actualFunctional)
                {
                    throw new InvalidOperationException("Foreign-currency posting line functional amount does not match the exchange-rate snapshot.");
                }
            }
            else if (exchangeRate.HasValue && exchangeRate.Value <= 0)
            {
                throw new InvalidOperationException("Exchange-rate snapshot must be positive when supplied.");
            }
            else
            {
                transactionDebit ??= debit > 0 ? debit : 0m;
                transactionCredit ??= credit > 0 ? credit : 0m;
                transactionDebit = RoundMoney(transactionDebit.GetValueOrDefault(), transactionDecimalPlaces);
                transactionCredit = RoundMoney(transactionCredit.GetValueOrDefault(), transactionDecimalPlaces);
                foreignAmount = null;
                exchangeRateId = null;
                exchangeRate = null;
                exchangeRateSource = null;
                exchangeRateDate = null;
            }

            // Only a validated immutable close-cycle plan may carry historical coding into
            // a new nominal/equity transfer. Ordinary producers retain the existing resolver.
            var dimensionSet = yearEndPlan != null
                ? ResolveYearEndDimensionSet(yearEndPlan.Sources[line.SourceDocumentLineId!.Value])
                : await ResolveDimensionSetAsync(
                tenantId,
                postingDate,
                request,
                route,
                certificationState,
                line,
                debit,
                credit,
                cancellationToken);

            normalizedLines.Add(new ValidatedPostingLine(
                line.AccountId,
                line.SourceDocumentLineId,
                NormalizeOptional(line.Description, 500, "Line description"),
                debit,
                credit,
                lineCurrency,
                transactionDebit,
                transactionCredit,
                foreignAmount,
                exchangeRateId,
                exchangeRate,
                exchangeRateSource,
                exchangeRateDate,
                NormalizeOptional(line.SourceReferenceNumber, 100, "Line source reference"),
                line.LineNumber.GetValueOrDefault(lineNumber++),
                dimensionSet,
                NormalizeOptional(line.SegmentString, 200, "Segment string"),
                NormalizeOptional(line.Notes, 1000, "Line notes"),
                NormalizeOptional(line.TransactionTag, 50, "Transaction tag")));
        }

        if (!normalizedLines.Any(l => l.DebitAmount > 0) || !normalizedLines.Any(l => l.CreditAmount > 0))
        {
            throw new InvalidOperationException("Posting must contain at least one debit and one credit line.");
        }

        var totalDebit = RoundMoney(normalizedLines.Sum(l => l.DebitAmount), functionalDecimalPlaces);
        var totalCredit = RoundMoney(normalizedLines.Sum(l => l.CreditAmount), functionalDecimalPlaces);
        if (totalDebit != totalCredit)
        {
            throw new InvalidOperationException("Posting is not balanced. Total debits must equal total credits.");
        }

        var accountIds = normalizedLines.Select(l => l.AccountId).Distinct().ToList();
        var accounts = await _context.Accounts
            .Where(a => a.TenantId == tenantId && accountIds.Contains(a.Id) && !a.IsDeleted)
            .ToDictionaryAsync(a => a.Id, cancellationToken);

        if (accounts.Count != accountIds.Count)
        {
            await RecordAccountingBookAuthorityDenialAsync(
                tenantId, request, normalizedAccountingBookCode, "ACCOUNT_UNAVAILABLE", cancellationToken);
            throw new InvalidOperationException("One or more posting accounts were not found for this tenant.");
        }

        var inactiveAccounts = accounts.Values
            .Where(a => a.Status != AccountStatus.Active)
            .Select(a => a.AccountNumber)
            .ToList();

        if (inactiveAccounts.Count > 0 && !allowHistoricalMappingException)
        {
            throw new InvalidOperationException($"Cannot post to inactive GL account(s): {string.Join(", ", inactiveAccounts)}.");
        }

        if (!allowHistoricalMappingException && string.Equals(sourceModule, "GL", StringComparison.OrdinalIgnoreCase))
        {
            var prohibited = accounts.Values.Where(item => !item.AllowDirectPosting || item.IsControlAccount)
                .Select(item => item.AccountNumber).ToList();
            if (prohibited.Count > 0)
                throw new InvalidOperationException($"Direct GL posting is not allowed for account(s): {string.Join(", ", prohibited)}.");
        }

        var bookMappings = await _context.AccountAccountingBooks.AsNoTracking()
            .Include(item => item.AccountClassification)
            .Where(item => item.TenantId == tenantId && item.AccountingBookId == accountingBook.Id
                && accountIds.Contains(item.AccountId) && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        if (bookMappings.Select(item => item.AccountId).Distinct().Count() != accountIds.Count)
        {
            await RecordAccountingBookAuthorityDenialAsync(
                tenantId, request, normalizedAccountingBookCode, "ACCOUNT_BOOK_MAPPING_UNAVAILABLE", cancellationToken);
            throw new InvalidOperationException("One or more posting accounts are not enabled for the requested accounting book.");
        }
        if (!allowHistoricalMappingException)
        {
            foreach (var mapping in bookMappings)
            {
                var classification = mapping.AccountClassification;
                // Nullable classifications are a visible transition state for pre-Phase-1A mappings.
                // Finance account create/edit never produces this state, while readiness diagnostics
                // identify it for migration. Once classified, the mapping must be fully valid here.
                if (!mapping.IsEnabled || (classification != null && (classification.TenantId != tenantId
                    || classification.AccountingBookId != accountingBook.Id || classification.IsDeleted
                    || classification.Status != AccountClassificationStatus.Active || !classification.IsPostingClassification
                    || classification.CoreAccountType != accounts[mapping.AccountId].AccountType)))
                {
                    await RecordAccountingBookAuthorityDenialAsync(
                        tenantId, request, normalizedAccountingBookCode, "ACCOUNT_BOOK_MAPPING_INVALID", cancellationToken);
                    throw new InvalidOperationException("One or more posting accounts lack an enabled, compatible accounting-book classification.");
                }
            }
        }

        var multiCurrencyAccountIds = accounts.Values
            .Where(a => a.IsMultiCurrency)
            .Select(a => a.Id)
            .ToList();
        var currencyLinks = multiCurrencyAccountIds.Count == 0
            ? new List<AccountCurrencyLink>()
            : await _context.AccountCurrencyLinks
                .AsNoTracking()
                .Where(l => l.TenantId == tenantId
                    && multiCurrencyAccountIds.Contains(l.AccountId)
                    && !l.IsDeleted)
                .ToListAsync(cancellationToken);

        foreach (var line in normalizedLines)
        {
            var account = accounts[line.AccountId];
            // Parallel year-end transfers close that book's functional balances; they do not
            // introduce a new foreign transaction into the primary account's native ledger.
            if (yearEndCycle != null && accountingBook.BookType == AccountingBookType.ParallelFull
                && string.Equals(line.TransactionCurrency, functionalCurrency, StringComparison.Ordinal))
                continue;
            var accountCurrency = NormalizeCurrency(account.CurrencyCode, "Account currency", functionalCurrency);
            if (!account.IsMultiCurrency
                && !string.Equals(accountCurrency, line.TransactionCurrency, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Account '{account.AccountNumber}' only accepts {accountCurrency} transactions and cannot be posted in {line.TransactionCurrency}.");
            }

            if (account.IsMultiCurrency
                && !string.Equals(accountCurrency, line.TransactionCurrency, StringComparison.OrdinalIgnoreCase)
                && !currencyLinks.Any(link =>
                    link.AccountId == account.Id
                    && string.Equals(
                        NormalizeCurrency(link.LinkedCurrencyCode, "Linked currency", functionalCurrency),
                        line.TransactionCurrency,
                        StringComparison.OrdinalIgnoreCase)
                    && IsCurrencyLinkEffectiveForPosting(link, postingDate)))
            {
                throw new InvalidOperationException(
                    $"Account '{account.AccountNumber}' does not have an active {line.TransactionCurrency} currency link for {postingDate:yyyy-MM-dd}. Add or reactivate the currency link before posting.");
            }
        }

        var distinctCurrencies = normalizedLines
            .Select(l => l.TransactionCurrency)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var primaryCurrency = distinctCurrencies.Count == 1 ? distinctCurrencies[0] : null;
        var primaryExchangeRateLine = normalizedLines.FirstOrDefault(l => l.ExchangeRateId.HasValue);
        var budgetReservationIds = (request.BudgetReservationIds ?? Array.Empty<Guid>())
            .OrderBy(id => id)
            .ToArray();
        if (budgetReservationIds.Any(id => id == Guid.Empty)
            || budgetReservationIds.Distinct().Count() != budgetReservationIds.Length)
            throw new InvalidOperationException("Budget reservation identities must be non-empty and unique.");
        var budgetReservationSourceDocumentType = NormalizeOptional(
            request.BudgetReservationSourceDocumentType, 100, "Budget reservation source document type");
        var requestFingerprint = BuildRequestFingerprint(
            tenantId,
            sourceModule,
            originModuleCode,
            sourceDocumentType,
            request.SourceDocumentId,
            postingAction,
            sourceReference,
            idempotencyKey,
            request.ExistingJournalEntryId,
            request.ReversalOfJournalEntryId,
            reversalReason,
            reversalType,
            description,
            postingDate,
            fiscalPeriod.Id,
            journalType,
            accountingBook.Id,
            normalizedAccountingBookCode,
            functionalCurrency,
            request,
            normalizedLines,
            budgetReservationIds,
            budgetReservationSourceDocumentType);

        return new ValidatedPosting(
            route,
            certificationState,
            sourceModule,
            originModuleCode,
            sourceDocumentType,
            request.SourceDocumentId,
            postingAction,
            sourceReference,
            idempotencyKey,
            request.ExistingJournalEntryId,
            request.ReversalOfJournalEntryId,
            reversalReason,
            reversalType,
            description,
            postingDate,
            journalType,
            normalizedAccountingBookCode,
            accountingBook.Id,
            functionalCurrency,
            functionalDecimalPlaces,
            fiscalPeriod,
            normalizedLines,
            totalDebit,
            totalCredit,
            normalizedLines.Any(l => !string.Equals(l.TransactionCurrency, functionalCurrency, StringComparison.OrdinalIgnoreCase)),
            primaryCurrency,
            primaryExchangeRateLine?.ExchangeRateId,
            primaryExchangeRateLine?.ExchangeRate,
            primaryExchangeRateLine?.ExchangeRateDate,
            exchangeRatePolicyOverrideUsed,
            NormalizeOptional(request.ExchangeRateOverrideReason, 500, "Exchange-rate override reason"),
            request.ExchangeRateOverrideApprovedByUserId,
            request.ExchangeRateOverrideApprovedAt,
            budgetReservationIds,
            budgetReservationSourceDocumentType,
            RequestFingerprintVersion,
            requestFingerprint,
            allowHistoricalMappingException);
    }

    private async Task<FiscalPeriod> ResolveFiscalPeriodAsync(
        Guid tenantId,
        DateTime postingDate,
        Guid? fiscalPeriodId,
        CancellationToken cancellationToken)
    {
        if (fiscalPeriodId.HasValue)
        {
            var selectedPeriod = await _context.FiscalPeriods
                .FirstOrDefaultAsync(
                    p => p.TenantId == tenantId
                        && p.Id == fiscalPeriodId.Value
                        && !p.IsDeleted,
                    cancellationToken);

            return selectedPeriod ?? throw new InvalidOperationException("Fiscal period was not found for this tenant.");
        }

        var period = await _context.FiscalPeriods
            .FirstOrDefaultAsync(
                p => p.TenantId == tenantId
                    && !p.IsDeleted
                    && p.StartDate <= postingDate
                    && p.EndDate >= postingDate,
                cancellationToken);

        return period ?? throw new InvalidOperationException("No fiscal period covers the posting date for this tenant.");
    }

    private async Task EnsureBookYearIsOpenAsync(Guid tenantId, Guid bookId, Guid fiscalYearId, CancellationToken ct)
    {
        if (await _context.YearEndBookCloseCycles.AsNoTracking().AnyAsync(item => item.TenantId == tenantId
            && item.AccountingBookId == bookId && item.FiscalYearId == fiscalYearId && item.Status != "Reopened", ct))
            throw new InvalidOperationException("The accounting book is closed for this fiscal year. Reopen its year-end close cycle first.");
    }

    private async Task EnsureOriginModuleCanPostAsync(
        Guid tenantId,
        FiscalPeriod fiscalPeriod,
        string originModuleCode,
        FinancePostingCommandDto request,
        CancellationToken cancellationToken)
    {
        var module = await _context.ModuleDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(item =>
                item.TenantId == tenantId
                && item.IsActive
                && item.ModuleCode == originModuleCode
                && !item.IsDeleted,
                cancellationToken);

        // Existing tenants may briefly have no reconciled module definitions during
        // deployment. A normal open period remains compatible, while a partial lock
        // fails closed so an unknown/missing module cannot evade the global lock.
        if (module == null)
        {
            if (!fiscalPeriod.IsGlobalLockSuspended)
                return;

            await RecordPostingBlockedByModuleAuditAsync(
                tenantId,
                request,
                fiscalPeriod,
                originModuleCode,
                "The module is not registered in this period's partial-lock state.",
                null,
                cancellationToken);
            throw new InvalidOperationException(
                $"Posting blocked: {originModuleCode} is locked for {fiscalPeriod.PeriodName}. " +
                "You may continue editing this transaction, but it cannot be posted until a Finance administrator reopens the module.");
        }

        var moduleLock = await _context.PeriodModuleLocks
            .AsNoTracking()
            .FirstOrDefaultAsync(item =>
                item.TenantId == tenantId
                && item.FiscalPeriodId == fiscalPeriod.Id
                && item.ModuleDefinitionId == module.Id
                && !item.IsDeleted,
                cancellationToken);
        var now = DateTime.UtcNow;
        var reopeningExpired = moduleLock != null
            && !moduleLock.IsLocked
            && moduleLock.ReopenExpiresAtUtc.HasValue
            && moduleLock.ReopenExpiresAtUtc <= now;
        var isLocked = fiscalPeriod.IsGlobalLockSuspended
            ? moduleLock == null || moduleLock.IsLocked || reopeningExpired
            : moduleLock != null && (moduleLock.IsLocked || reopeningExpired);

        if (!isLocked)
            return;

        var reason = reopeningExpired
            ? $"Temporary reopening expired at {moduleLock!.ReopenExpiresAtUtc:u}."
            : moduleLock?.LockReason ?? fiscalPeriod.LockReason ?? "Accounting period module lock";
        await RecordPostingBlockedByModuleAuditAsync(
            tenantId,
            request,
            fiscalPeriod,
            originModuleCode,
            reason,
            moduleLock,
            cancellationToken);

        throw new InvalidOperationException(
            $"Posting blocked: {module.ModuleName} is locked for {fiscalPeriod.PeriodName} " +
            $"({fiscalPeriod.StartDate:MMM d, yyyy} - {fiscalPeriod.EndDate:MMM d, yyyy}). " +
            "You may continue editing this transaction, but it cannot be posted until a Finance administrator reopens the module. " +
            $"Reason: {reason}");
    }

    private async Task<FunctionalCurrencyConfig> ResolveTenantFunctionalCurrencyAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var settings = await _context.FinanceSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && !s.IsDeleted, cancellationToken);

        if (!string.IsNullOrWhiteSpace(settings?.BaseCurrency))
        {
            return new FunctionalCurrencyConfig(NormalizeCurrency(settings.BaseCurrency, "Tenant functional currency"), true);
        }

        var tenantCurrency = await _context.Tenants
            .AsNoTracking()
            .Where(t => t.Id == tenantId && !t.IsDeleted)
            .Select(t => t.BaseCurrency)
            .FirstOrDefaultAsync(cancellationToken);

        return new FunctionalCurrencyConfig(NormalizeCurrency(tenantCurrency, "Tenant functional currency"), false);
    }

    private async Task<ExchangeRatePolicy> ResolveExchangeRatePolicyAsync(
        Guid tenantId,
        Guid accountId,
        string transactionCurrency,
        string sourceModule,
        string sourceDocumentType,
        DateTime postingDate,
        FinanceSettings? settings,
        FinancePostingCommandDto request,
        CancellationToken cancellationToken)
    {
        var isRevaluation = sourceDocumentType.Contains("Revaluation", StringComparison.OrdinalIgnoreCase);
        var isSettlement = sourceDocumentType.Contains("Payment", StringComparison.OrdinalIgnoreCase)
            || sourceDocumentType.Contains("Receipt", StringComparison.OrdinalIgnoreCase)
            || sourceDocumentType.Contains("Settlement", StringComparison.OrdinalIgnoreCase)
            || sourceDocumentType.Contains("Application", StringComparison.OrdinalIgnoreCase);
        var directionalPolicyEnabled = settings?.DirectionalExchangeRatePolicyEnabled == true;

        var rateType = isRevaluation ? ExchangeRateType.MonthEnd : ExchangeRateType.Daily;
        var quoteSide = ExchangeRateQuoteSide.Mid;

        if (string.Equals(sourceModule, "AR", StringComparison.OrdinalIgnoreCase))
        {
            quoteSide = directionalPolicyEnabled
                ? (isSettlement
                    ? settings?.ArSettlementQuoteSide ?? ExchangeRateQuoteSide.Buying
                    : settings?.ArInvoiceQuoteSide ?? ExchangeRateQuoteSide.Mid)
                : ExchangeRateQuoteSide.Mid;
        }
        else if (string.Equals(sourceModule, "AP", StringComparison.OrdinalIgnoreCase))
        {
            quoteSide = directionalPolicyEnabled
                ? (isSettlement
                    ? settings?.ApSettlementQuoteSide ?? ExchangeRateQuoteSide.Selling
                    : settings?.ApInvoiceQuoteSide ?? ExchangeRateQuoteSide.Mid)
                : ExchangeRateQuoteSide.Mid;
        }
        else
        {
            var link = await _context.AccountCurrencyLinks
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    l => l.TenantId == tenantId
                        && l.AccountId == accountId
                        && l.LinkedCurrencyCode == transactionCurrency
                        && !l.IsDeleted
                        && l.IsActive
                        && l.EffectiveDate.Date <= postingDate.Date
                        && (!l.EffectiveEndDate.HasValue || l.EffectiveEndDate.Value.Date >= postingDate.Date),
                    cancellationToken);

            if (link != null && directionalPolicyEnabled)
            {
                rateType = ParseExchangeRateType(
                    isRevaluation ? link.RevaluationRateType : link.TransactionRateType,
                    isRevaluation ? ExchangeRateType.MonthEnd : ExchangeRateType.Daily);
                quoteSide = isRevaluation ? link.RevaluationQuoteSide : link.TransactionQuoteSide;
            }
            else
            {
                quoteSide = directionalPolicyEnabled
                    ? (isRevaluation
                        ? settings?.ClosingQuoteSide ?? ExchangeRateQuoteSide.Mid
                        : settings?.DefaultTransactionQuoteSide ?? ExchangeRateQuoteSide.Mid)
                    : ExchangeRateQuoteSide.Mid;
            }
        }

        var selectedRateType = string.IsNullOrWhiteSpace(request.ExchangeRateTypeOverride)
            ? rateType
            : ParseExchangeRateType(request.ExchangeRateTypeOverride, rateType);
        var selectedQuoteSide = string.IsNullOrWhiteSpace(request.ExchangeRateQuoteSideOverride)
            ? quoteSide
            : ParseExchangeRateQuoteSide(request.ExchangeRateQuoteSideOverride);
        var isOverride = selectedRateType != rateType || selectedQuoteSide != quoteSide;

        if (isOverride)
            EnsureExchangeRateOverrideApproval(request, settings?.RequireExchangeRateOverrideApproval ?? true);

        return new ExchangeRatePolicy(selectedRateType, selectedQuoteSide, isOverride);
    }

    private async Task<ExchangeRateSnapshot> ResolveExchangeRateSnapshotAsync(
        Guid tenantId,
        string functionalCurrency,
        string transactionCurrency,
        DateTime postingDate,
        Guid? exchangeRateId,
        decimal? suppliedRate,
        ExchangeRatePolicy policy,
        string? transactionTag,
        bool requireOverrideApproval,
        FinancePostingCommandDto request,
        CancellationToken cancellationToken)
    {
        ExchangeRate? rate;
        var policyOverrideUsed = policy.IsOverride;
        var preservesHistoricalSourceMeasurement =
            request.PreserveHistoricalExchangeRateSnapshot &&
            ((string.Equals(request.SourceModule, "AP", StringComparison.OrdinalIgnoreCase) &&
              string.Equals(request.SourceDocumentType, "SupplierDebitNote", StringComparison.OrdinalIgnoreCase)) ||
             (request.ReversalOfJournalEntryId.HasValue &&
              string.Equals(request.SourceModule, "GL", StringComparison.OrdinalIgnoreCase) &&
              string.Equals(request.SourceDocumentType, "RecurringJournalOccurrence", StringComparison.Ordinal) &&
              string.Equals(request.PostingAction, "AutoReverseRecurringJournal", StringComparison.Ordinal))) &&
            exchangeRateId.HasValue &&
            suppliedRate.HasValue;
        if (request.PreserveHistoricalExchangeRateSnapshot && !preservesHistoricalSourceMeasurement)
            throw new InvalidOperationException(
                "Historical exchange-rate preservation requires an approved Finance correction or exact recurring-journal reversal with explicit source-rate evidence.");
        if (preservesHistoricalSourceMeasurement)
        {
            // A supplier debit note corrects the approved source invoice at its immutable rate;
            // this is not a user-entered current-period FX override.
            if (!request.ReversalOfJournalEntryId.HasValue)
                EnsureExchangeRateOverrideApproval(request, requireApproval: true);
            policyOverrideUsed = true;
        }
        if (exchangeRateId.HasValue)
        {
            rate = await _context.ExchangeRates
                .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == exchangeRateId.Value && !r.IsDeleted, cancellationToken);
        }
        else
        {
            rate = await _context.ExchangeRates
                .Where(r => r.TenantId == tenantId
                    && !r.IsDeleted
                    && r.BaseCurrencyCode == functionalCurrency
                    && r.TargetCurrencyCode == transactionCurrency
                    && r.RateType == policy.RateType
                    && r.QuoteSide == policy.QuoteSide
                    && r.IsActive
                    && r.Rate > 0
                    && (r.ApprovalStatus == RateApprovalStatus.Approved || r.ApprovalStatus == RateApprovalStatus.AutoApproved)
                    && r.EffectiveDate.Date <= postingDate.Date
                    && (r.EndDate == null || r.EndDate.Value.Date >= postingDate.Date))
                .OrderByDescending(r => r.EffectiveDate)
                .ThenByDescending(r => r.Priority)
                .ThenByDescending(r => r.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (rate == null)
        {
            await RecordForeignCurrencyPostingBlockedAuditAsync(
                tenantId,
                request,
                transactionCurrency,
                "No tenant-owned effective exchange rate was found for the posting date.",
                cancellationToken);
            throw new InvalidOperationException($"No effective exchange rate exists for {transactionCurrency} to {functionalCurrency} on {postingDate:yyyy-MM-dd}.");
        }

        if (rate.Rate <= 0)
        {
            await RecordForeignCurrencyPostingBlockedAuditAsync(
                tenantId,
                request,
                transactionCurrency,
                "Exchange rate is zero or negative.",
                cancellationToken);
            throw new InvalidOperationException("Exchange rate must be greater than zero.");
        }

        if (!string.Equals(rate.BaseCurrencyCode, functionalCurrency, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(rate.TargetCurrencyCode, transactionCurrency, StringComparison.OrdinalIgnoreCase))
        {
            await RecordForeignCurrencyPostingBlockedAuditAsync(
                tenantId,
                request,
                transactionCurrency,
                "Exchange rate currency pair does not match the posting currency pair.",
                cancellationToken);
            throw new InvalidOperationException("Exchange rate currency pair does not match the posting currency pair.");
        }

        if (!preservesHistoricalSourceMeasurement &&
            (rate.EffectiveDate.Date > postingDate.Date || (rate.EndDate.HasValue && rate.EndDate.Value.Date < postingDate.Date)))
        {
            await RecordForeignCurrencyPostingBlockedAuditAsync(
                tenantId,
                request,
                transactionCurrency,
                "Exchange rate is not effective for the posting date.",
                cancellationToken);
            throw new InvalidOperationException("Exchange rate is not effective for the posting date.");
        }

        if (!preservesHistoricalSourceMeasurement &&
            (!rate.IsActive || rate.ApprovalStatus is not (RateApprovalStatus.Approved or RateApprovalStatus.AutoApproved)))
        {
            await RecordForeignCurrencyPostingBlockedAuditAsync(
                tenantId,
                request,
                transactionCurrency,
                "Exchange rate is inactive or not approved.",
                cancellationToken);
            throw new InvalidOperationException("Exchange rate must be active and approved before posting.");
        }

        // Ghana WHT is a GHS statutory liability measured at the exact-date Bank of Ghana
        // reference rate, not at the commercial Daily rate used by the rest of the payment.
        // Admit that rate only on the trusted AP vendor-payment WHT line; every other line and
        // producer continues to use the ordinary policy/override approval boundary.
        var isGovernedGhanaStatutoryWht =
            string.Equals(request.SourceModule, "AP", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(request.SourceDocumentType, "VendorPayment", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(transactionTag, "AP-WHT", StringComparison.Ordinal) &&
            rate.RateType == ExchangeRateType.GhanaStatutory &&
            rate.QuoteSide == ExchangeRateQuoteSide.Mid &&
            rate.EffectiveDate.Date == postingDate.Date &&
            IsBankOfGhanaRateSource(rate.RateSource) &&
            !string.IsNullOrWhiteSpace(rate.APIResponseMetadata);

        if (rate.RateType != policy.RateType || rate.QuoteSide != policy.QuoteSide)
        {
            // A reversal must reproduce the original immutable rate snapshot even
            // when the tenant's current policy has since changed.
            if (!request.ReversalOfJournalEntryId.HasValue && !preservesHistoricalSourceMeasurement &&
                !isGovernedGhanaStatutoryWht)
            {
                EnsureExchangeRateOverrideApproval(request, requireOverrideApproval);
                policyOverrideUsed = true;
            }
        }

        var functionalMultiplier = rate.InverseRate;
        if (functionalMultiplier <= 0m)
            throw new InvalidOperationException("Exchange rate has no positive target-to-functional reciprocal.");
        if (suppliedRate.HasValue && RoundRate(suppliedRate.Value) != RoundRate(functionalMultiplier))
        {
            await RecordForeignCurrencyPostingBlockedAuditAsync(
                tenantId,
                request,
                transactionCurrency,
                "Supplied exchange-rate snapshot does not match the tenant exchange-rate record.",
                cancellationToken);
            throw new InvalidOperationException("Supplied exchange-rate snapshot does not match the tenant exchange-rate record.");
        }

        return new ExchangeRateSnapshot(rate.Id, functionalMultiplier, rate.RateSource, rate.EffectiveDate.Date, policyOverrideUsed);
    }

    private static bool IsBankOfGhanaRateSource(string? source) =>
        !string.IsNullOrWhiteSpace(source) &&
        (source.Contains("Bank of Ghana", StringComparison.OrdinalIgnoreCase) ||
         string.Equals(source.Trim(), "BoG", StringComparison.OrdinalIgnoreCase));

    private static ExchangeRateType ParseExchangeRateType(string? value, ExchangeRateType fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        var normalized = value.Trim().Replace("-", string.Empty).Replace("_", string.Empty).Replace(" ", string.Empty);
        return Enum.TryParse<ExchangeRateType>(normalized, ignoreCase: true, out var rateType)
            ? rateType
            : throw new InvalidOperationException("Exchange-rate type override is invalid.");
    }

    private static ExchangeRateQuoteSide ParseExchangeRateQuoteSide(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !Enum.TryParse<ExchangeRateQuoteSide>(value.Trim(), ignoreCase: true, out var quoteSide))
        {
            throw new InvalidOperationException("Exchange-rate quote-side override must be Mid, Buying, or Selling.");
        }

        return quoteSide;
    }

    private static void EnsureExchangeRateOverrideApproval(
        FinancePostingCommandDto request,
        bool requireApproval)
    {
        var reason = request.ExchangeRateOverrideReason?.Trim();
        if (string.IsNullOrWhiteSpace(reason) || reason.Length < 10)
            throw new InvalidOperationException("An exchange-rate policy override requires a reason of at least 10 characters.");

        if (!requireApproval)
            return;

        if (!request.ExchangeRateOverrideApprovedByUserId.HasValue
            || request.ExchangeRateOverrideApprovedByUserId.Value == Guid.Empty
            || !request.ExchangeRateOverrideApprovedAt.HasValue)
        {
            throw new InvalidOperationException("An exchange-rate policy override requires recorded approval by an authorised user.");
        }

        if (request.ExchangeRateOverrideApprovedAt.Value > DateTime.UtcNow.AddMinutes(5))
            throw new InvalidOperationException("Exchange-rate override approval time cannot be in the future.");
    }

    private async Task MarkExchangeRatesUsedAsync(
        Guid tenantId,
        ValidatedPosting validation,
        Guid postingEventId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var rateIds = validation.Lines
            .Where(l => l.ExchangeRateId.HasValue)
            .Select(l => l.ExchangeRateId!.Value)
            .Distinct()
            .ToList();

        if (rateIds.Count == 0)
        {
            return;
        }

        var rates = await _context.ExchangeRates
            .Where(r => r.TenantId == tenantId && rateIds.Contains(r.Id) && !r.IsDeleted)
            .ToListAsync(cancellationToken);

        if (rates.Count != rateIds.Count)
        {
            throw new InvalidOperationException("One or more exchange-rate snapshots were not found for this tenant.");
        }

        foreach (var rate in rates)
        {
            rate.HasBeenUsedInTransactions = true;
            rate.TransactionCount += validation.Lines.Count(l => l.ExchangeRateId == rate.Id);
            rate.FirstUsedDate ??= now;
            rate.LastUsedDate = now;
            rate.ModifiedDate = now;
            rate.ModifiedByUserId = GetCurrentUserGuid();
        }
    }

    private async Task<FinancePostingEvent?> FindExistingPostingAsync(
        Guid tenantId,
        ValidatedPosting validation,
        AccountingEventPostingAuthority? accountingEventContext,
        CancellationToken cancellationToken)
    {
        var matches = await _context.FinancePostingEvents
            .Include(e => e.JournalEntry)
                .ThenInclude(journal => journal!.Transactions)
            .Where(
                e => e.TenantId == tenantId
                    && !e.IsDeleted
                    && e.AccountingBookId == validation.AccountingBookId
                    && (((e.SourceDocumentType == validation.SourceDocumentType
                            && e.SourceDocumentId == validation.SourceDocumentId
                            && e.PostingAction == validation.PostingAction)
                        || (e.SourceModule == validation.SourceModule
                            && e.SourceDocumentType == validation.SourceDocumentType
                            && e.SourceDocumentId == validation.SourceDocumentId
                            && e.PostingAction == validation.PostingAction))
                        || (!string.IsNullOrWhiteSpace(validation.IdempotencyKey)
                            && e.IdempotencyKey == validation.IdempotencyKey)))
            .Take(2)
            .ToListAsync(cancellationToken);

        if (matches.Count > 1)
            throw new InvalidOperationException("Conflicting Finance posting identity evidence exists for this accounting book.");

        var existing = matches.SingleOrDefault();
        if (existing is not null)
        {
            if (accountingEventContext is not null)
                await RequireAccountingEventPostingOwnershipAsync(tenantId, existing.Id,
                    validation.AccountingBookId, accountingEventContext.Value, cancellationToken);
            EnsureExistingPostingMatchesRequest(existing, validation);
        }
        return existing;
    }

    private async Task EnsureNoParallelBookPostingAsync(
        Guid tenantId,
        ValidatedPosting validation,
        bool acquireLock,
        AccountingEventPostingAuthority? accountingEventContext,
        CancellationToken cancellationToken)
    {
        if (acquireLock && _context.Database.IsSqlServer())
        {
            if (_context.Database.CurrentTransaction is null)
                throw new InvalidOperationException("Parallel-book posting protection requires an active transaction.");

            // C1 deliberately serializes every posting representation for a tenant. SQL Server
            // collations can equate case/trailing-space variants that ordinal CLR strings do not;
            // a tenant-wide lock is therefore provably at least as coarse as every database key.
            // Keep this temporary lock until C2/C3 introduces book-aware balances and a Finance
            // orchestrator that can atomically release several representations.
            var resource = $"FIN:POSTING-REPRESENTATION:{tenantId:N}";
            await _context.Database.ExecuteSqlInterpolatedAsync($@"
DECLARE @result int;
EXEC @result = sys.sp_getapplock
    @Resource = {resource},
    @LockMode = 'Exclusive',
    @LockOwner = 'Transaction',
    @LockTimeout = 15000;
IF @result < 0 THROW 51000, 'Could not acquire Finance posting representation lock.', 1;", cancellationToken);
        }

        var matchingIdentities = await _context.FinancePostingEvents.AsNoTracking()
            .Where(
            item => item.TenantId == tenantId && !item.IsDeleted
                && (item.JournalEntry == null || item.JournalEntry.ReplicatedFromJournalEntryId == null)
                && ((item.SourceDocumentType == validation.SourceDocumentType
                        && item.SourceDocumentId == validation.SourceDocumentId
                        && item.PostingAction == validation.PostingAction)
                    || (!string.IsNullOrWhiteSpace(validation.IdempotencyKey)
                        && item.IdempotencyKey == validation.IdempotencyKey)))
            .Select(item => new { item.Id, item.AccountingBookId, item.BookClassification })
            .ToListAsync(cancellationToken);

        // A matching identity in the resolved book is a legitimate retry only when both
        // relational and snapshot evidence agree. A different book remains representable
        // in the schema, but is deliberately blocked until Finance owns book-aware balances.
        if (matchingIdentities.Any(item =>
                (item.AccountingBookId == validation.AccountingBookId
                    && !string.Equals(item.BookClassification, validation.AccountingBookCode, StringComparison.Ordinal))
                || (item.AccountingBookId != validation.AccountingBookId
                    && string.Equals(item.BookClassification, validation.AccountingBookCode, StringComparison.Ordinal))))
            throw new InvalidOperationException("Finance posting accounting-book ID/code evidence is inconsistent.");

        if (accountingEventContext is null)
        {
            if (matchingIdentities.Any(item => item.AccountingBookId != validation.AccountingBookId))
                throw new InvalidOperationException(
                    "PARALLEL_BOOK_POSTING_DISABLED: A representation of this economic source or idempotency identity already exists in another accounting book.");
            return;
        }

        var context = accountingEventContext.Value;
        foreach (var match in matchingIdentities)
        {
            if (!context.OrderedSelectedBookIds.Contains(match.AccountingBookId))
                throw new InvalidOperationException("ACCOUNTING_EVENT_UNRELATED_POSTING_MATCH: a matching posting is outside the exact frozen book set.");
            await RequireAccountingEventPostingOwnershipAsync(tenantId, match.Id, match.AccountingBookId, context, cancellationToken);
        }
    }

    private async Task RequireAccountingEventPostingOwnershipAsync(
        Guid tenantId,
        Guid financePostingEventId,
        Guid accountingBookId,
        AccountingEventPostingAuthority context,
        CancellationToken cancellationToken)
    {
        var owned = await _context.AccountingEventPostings.AsNoTracking().AnyAsync(item =>
            item.TenantId == tenantId
            && item.AccountingEventId == context.AccountingEventId
            && item.AccountingBookId == accountingBookId
            && item.FinancePostingEventId == financePostingEventId
            && item.JournalEntryId != null
            && item.Status == AccountingEventStatuses.Posted
            && (accountingBookId != context.AccountingBookId || item.AuthorityFingerprint == context.AuthorityFingerprint)
            && !item.IsDeleted
            && _context.AccountingEvents.Any(accountingEvent => accountingEvent.TenantId == tenantId
                && accountingEvent.Id == context.AccountingEventId
                && accountingEvent.Version == item.EventVersion
                && accountingEvent.AccountingBookSelectionEvidenceId == context.AccountingBookSelectionEvidenceId
                && !accountingEvent.IsDeleted)
            && _context.AccountingBookSelectionEvidenceBooks.Any(selectionBook =>
                selectionBook.TenantId == tenantId
                && selectionBook.AccountingBookSelectionEvidenceId == context.AccountingBookSelectionEvidenceId
                && selectionBook.AccountingBookId == accountingBookId
                && selectionBook.SelectionOrder == item.SelectionOrder
                && selectionBook.AccountingBookCodeSnapshot == item.AccountingBookCodeSnapshot
                && selectionBook.AuthorityFingerprint == item.AuthorityFingerprint
                && !selectionBook.IsDeleted)
            && _context.FinancePostingEvents.Any(postingEvent =>
                postingEvent.TenantId == tenantId
                && postingEvent.Id == financePostingEventId
                && postingEvent.AccountingBookId == accountingBookId
                && postingEvent.BookClassification == item.AccountingBookCodeSnapshot
                && postingEvent.JournalEntryId == item.JournalEntryId
                && postingEvent.PostingStatus == PostedStatus
                && !postingEvent.IsDeleted)
            && _context.JournalEntries.Any(journal =>
                journal.TenantId == tenantId
                && journal.Id == item.JournalEntryId
                && journal.AccountingBookId == accountingBookId
                && journal.BookClassification == item.AccountingBookCodeSnapshot
                && journal.PostingStatus == PostedStatus
                && !journal.IsDeleted), cancellationToken);
        if (!owned)
            throw new InvalidOperationException("ACCOUNTING_EVENT_UNRELATED_POSTING_MATCH: matching posting evidence is not owned by the exact AccountingEvent context.");
    }

    private async Task EnsureNoUnrelatedAccountingEventMatchesBeforeValidationAsync(
        Guid tenantId,
        FinancePostingCommandDto request,
        AccountingEventPostingAuthority context,
        CancellationToken cancellationToken)
    {
        var documentType = request.SourceDocumentType?.Trim().ToUpperInvariant() ?? string.Empty;
        var action = request.PostingAction?.Trim().ToUpperInvariant() ?? string.Empty;
        var matches = await _context.FinancePostingEvents.AsNoTracking().Where(item =>
            item.TenantId == tenantId && !item.IsDeleted
            && ((item.SourceDocumentType == documentType && item.SourceDocumentId == request.SourceDocumentId
                    && item.PostingAction == action)
                || (!string.IsNullOrWhiteSpace(request.IdempotencyKey) && item.IdempotencyKey == request.IdempotencyKey)))
            .Select(item => new { item.Id, item.AccountingBookId }).ToListAsync(cancellationToken);
        foreach (var match in matches)
        {
            if (!context.OrderedSelectedBookIds.Contains(match.AccountingBookId))
                throw new InvalidOperationException("ACCOUNTING_EVENT_UNRELATED_POSTING_MATCH: a matching posting is outside the exact frozen book set.");
            await RequireAccountingEventPostingOwnershipAsync(tenantId, match.Id, match.AccountingBookId, context, cancellationToken);
        }
    }

    private static void EnsureStoredBookEvidence(FinancePostingEvent postingEvent)
    {
        var journal = postingEvent.JournalEntry
            ?? throw new InvalidOperationException("Finance posting event is not linked to journal evidence.");
        if (postingEvent.AccountingBookId == Guid.Empty
            || journal.TenantId != postingEvent.TenantId
            || journal.AccountingBookId != postingEvent.AccountingBookId
            || !string.Equals(journal.BookClassification, postingEvent.BookClassification, StringComparison.Ordinal)
            || journal.Transactions.Any(line => line.TenantId != postingEvent.TenantId
                || line.AccountingBookId != postingEvent.AccountingBookId
                || !string.Equals(line.BookClassification, postingEvent.BookClassification, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("Finance posting accounting-book evidence is inconsistent.");
        }
    }

    private static void EnsureExistingPostingMatchesRequest(
        FinancePostingEvent postingEvent,
        ValidatedPosting validation)
    {
        EnsureStoredBookEvidence(postingEvent);
        // Historical events do not contain enough immutable producer evidence to reconstruct an
        // exhaustive fingerprint. Returning them as a successful retry would invent evidence, so
        // callers must reconcile them explicitly instead of receiving a potentially false success.
        if (string.IsNullOrWhiteSpace(postingEvent.RequestFingerprintVersion)
            || string.IsNullOrWhiteSpace(postingEvent.RequestFingerprint))
            throw new InvalidOperationException(
                "LEGACY_POSTING_RETRY_UNAVAILABLE: Existing posting lacks canonical request evidence.");
        byte[] storedFingerprint;
        try
        {
            if (postingEvent.RequestFingerprint.Length != 64)
                throw new FormatException();
            storedFingerprint = Convert.FromHexString(postingEvent.RequestFingerprint);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException(
                "POSTING_REQUEST_FINGERPRINT_INVALID: Existing posting fingerprint evidence is malformed.");
        }
        if (!string.Equals(postingEvent.RequestFingerprintVersion, validation.RequestFingerprintVersion, StringComparison.Ordinal)
            || !CryptographicOperations.FixedTimeEquals(storedFingerprint, Convert.FromHexString(validation.RequestFingerprint)))
            throw new InvalidOperationException("Existing Finance posting identity has conflicting canonical request evidence.");

        var journal = postingEvent.JournalEntry!;
        if (postingEvent.AccountingBookId != validation.AccountingBookId
            || !string.Equals(postingEvent.BookClassification, validation.AccountingBookCode, StringComparison.Ordinal)
            || !string.Equals(postingEvent.SourceModule, validation.SourceModule, StringComparison.Ordinal)
            || !string.Equals(postingEvent.SourceDocumentType, validation.SourceDocumentType, StringComparison.Ordinal)
            || postingEvent.SourceDocumentId != validation.SourceDocumentId
            || !string.Equals(postingEvent.PostingAction, validation.PostingAction, StringComparison.Ordinal)
            || postingEvent.PostingDate.Date != validation.PostingDate.Date
            || postingEvent.TotalDebitAmount != validation.TotalDebitAmount
            || postingEvent.TotalCreditAmount != validation.TotalCreditAmount
            || !string.Equals(postingEvent.FunctionalCurrencyCode, validation.FunctionalCurrencyCode, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(journal.Description, validation.Description, StringComparison.Ordinal)
            || !string.Equals(journal.JournalType, validation.JournalType, StringComparison.Ordinal)
            || !string.Equals(journal.ReferenceNumber, validation.SourceDocumentReference, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Existing Finance posting identity has conflicting immutable request evidence.");
        }

        var storedLines = journal.Transactions.OrderBy(line => line.LineNumber).ThenBy(line => line.Id).ToList();
        var requestedLines = validation.Lines.OrderBy(line => line.LineNumber).ToList();
        if (storedLines.Count != requestedLines.Count)
            throw new InvalidOperationException("Existing Finance posting identity has conflicting immutable request evidence.");

        for (var index = 0; index < storedLines.Count; index++)
        {
            var stored = storedLines[index];
            var requested = requestedLines[index];
            if (stored.AccountId != requested.AccountId
                || stored.SourceDocumentLineId != requested.SourceDocumentLineId
                || stored.DebitAmount != requested.DebitAmount
                || stored.CreditAmount != requested.CreditAmount
                || stored.TransactionDebitAmount != requested.TransactionDebitAmount
                || stored.TransactionCreditAmount != requested.TransactionCreditAmount
                || stored.ForeignCurrencyAmount != requested.ForeignCurrencyAmount
                || stored.ExchangeRateId != requested.ExchangeRateId
                || stored.ExchangeRate != requested.ExchangeRate
                || stored.ExchangeRateDate != requested.ExchangeRateDate
                || stored.FinanceDimensionSetId != requested.DimensionSet?.Id
                || stored.LineNumber != requested.LineNumber
                || !string.Equals(stored.TransactionCurrency, requested.TransactionCurrency, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(stored.ExchangeRateSource, requested.ExchangeRateSource, StringComparison.Ordinal)
                || !string.Equals(stored.SegmentString, requested.SegmentString, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Existing Finance posting identity has conflicting immutable request evidence.");
            }
        }
    }

    private static FinancePostingResultDto ToResult(FinancePostingEvent postingEvent, bool wasDuplicate)
    {
        if (postingEvent.JournalEntryId == null || postingEvent.JournalEntry == null)
        {
            throw new InvalidOperationException("Existing posting event is not linked to a posted journal entry.");
        }

        return new FinancePostingResultDto
        {
            PostingEventId = postingEvent.Id,
            JournalEntryId = postingEvent.JournalEntryId.Value,
            JournalEntryNumber = postingEvent.JournalEntry.JournalEntryNumber,
            PostingStatus = postingEvent.PostingStatus,
            WasDuplicate = wasDuplicate,
            TotalDebitAmount = postingEvent.TotalDebitAmount,
            TotalCreditAmount = postingEvent.TotalCreditAmount,
            FunctionalCurrencyCode = postingEvent.FunctionalCurrencyCode,
            PostingDate = postingEvent.PostingDate,
            SourceModule = postingEvent.SourceModule,
            OriginModuleCode = postingEvent.OriginModuleCode
                ?? FinanceModuleLockCatalog.ResolveOriginModuleCode(postingEvent.SourceModule),
            SourceDocumentType = postingEvent.SourceDocumentType,
            SourceDocumentId = postingEvent.SourceDocumentId,
            PostingAction = postingEvent.PostingAction
        };
    }

    private async Task RecordPostingEventCreatedAuditAsync(
        Guid tenantId,
        ValidatedPosting validation,
        FinancePostingEvent postingEvent,
        Guid journalEntryId,
        CancellationToken cancellationToken)
    {
        if (_financeAuditService == null)
        {
            return;
        }

        await _financeAuditService.RecordAsync(new FinanceAuditEventDto
        {
            EventType = FinanceAuditEvents.PostingEventCreated,
            TenantId = tenantId,
            SourceModule = validation.SourceModule,
            SourceDocumentType = validation.SourceDocumentType,
            SourceDocumentId = validation.SourceDocumentId,
            JournalEntryId = journalEntryId,
            PostingEventId = postingEvent.Id,
            AfterValues = new
            {
                postingEvent.Id,
                postingEvent.SourceModule,
                postingEvent.SourceDocumentType,
                postingEvent.SourceDocumentId,
                postingEvent.PostingAction,
                postingEvent.JournalEntryId,
                postingEvent.PostingStatus,
                postingEvent.PostingDate,
                postingEvent.TotalDebitAmount,
                postingEvent.TotalCreditAmount,
                postingEvent.FunctionalCurrencyCode,
                postingEvent.AccountingBookId,
                postingEvent.BookClassification,
                postingEvent.RequestFingerprintVersion,
                postingEvent.RequestFingerprint
            },
            Context = new
            {
                validation.SourceDocumentReference,
                validation.IdempotencyKey,
                validation.ExistingJournalEntryId,
                validation.ReversalOfJournalEntryId,
                validation.ReversalType,
                validation.ReversalReason
            },
            Resource = "Finance.PostingEvent",
            ResourceId = postingEvent.Id.ToString()
        }, cancellationToken);
    }

    private async Task RecordDuplicatePostingAuditAsync(
        Guid tenantId,
        ValidatedPosting validation,
        FinancePostingEvent postingEvent,
        CancellationToken cancellationToken)
    {
        if (_financeAuditService == null)
        {
            return;
        }

        await _financeAuditService.RecordAsync(new FinanceAuditEventDto
        {
            EventType = FinanceAuditEvents.DuplicatePostingAttempt,
            TenantId = tenantId,
            SourceModule = validation.SourceModule,
            SourceDocumentType = validation.SourceDocumentType,
            SourceDocumentId = validation.SourceDocumentId,
            JournalEntryId = postingEvent.JournalEntryId,
            PostingEventId = postingEvent.Id,
            AfterValues = new
            {
                postingEvent.Id,
                postingEvent.SourceModule,
                postingEvent.SourceDocumentType,
                postingEvent.SourceDocumentId,
                postingEvent.PostingAction,
                postingEvent.JournalEntryId,
                postingEvent.PostingStatus,
                postingEvent.PostingDate,
                postingEvent.AccountingBookId,
                postingEvent.BookClassification,
                postingEvent.RequestFingerprintVersion,
                postingEvent.RequestFingerprint
            },
            Context = new
            {
                validation.IdempotencyKey,
                returnExistingPosting = true
            },
            Resource = "Finance.PostingEvent",
            ResourceId = postingEvent.Id.ToString()
        }, cancellationToken);
    }

    private async Task RecordPostingBlockedByPeriodAuditAsync(
        Guid tenantId,
        FinancePostingCommandDto request,
        FiscalPeriod fiscalPeriod,
        DateTime postingDate,
        CancellationToken cancellationToken)
    {
        if (_financeAuditService == null)
        {
            return;
        }

        await _financeAuditService.RecordAsync(new FinanceAuditEventDto
        {
            EventType = FinanceAuditEvents.PostingBlockedPeriodClosedLocked,
            TenantId = tenantId,
            SourceModule = request.SourceModule,
            SourceDocumentType = request.SourceDocumentType,
            SourceDocumentId = request.SourceDocumentId == Guid.Empty ? null : request.SourceDocumentId,
            AfterValues = new
            {
                request.SourceModule,
                request.SourceDocumentType,
                request.SourceDocumentId,
                request.PostingAction,
                request.SourceDocumentReference,
                PostingDate = postingDate,
                FiscalPeriodId = fiscalPeriod.Id,
                fiscalPeriod.PeriodCode,
                fiscalPeriod.PeriodName,
                fiscalPeriod.PeriodStatus,
                fiscalPeriod.IsOpen,
                fiscalPeriod.IsClosed,
                fiscalPeriod.IsLocked
            },
            Comment = "Posting blocked because the accounting period is closed, locked, or not open.",
            Resource = "Finance.FiscalPeriod",
            ResourceId = fiscalPeriod.Id.ToString()
        }, cancellationToken);
    }

    private async Task RecordPostingBlockedByBookPeriodAuditAsync(
        Guid tenantId,
        FinancePostingCommandDto request,
        AccountingBook accountingBook,
        AccountingBookPeriod accountingBookPeriod,
        FiscalPeriod fiscalPeriod,
        DateTime postingDate,
        CancellationToken cancellationToken)
    {
        if (_financeAuditService == null)
        {
            return;
        }

        await _financeAuditService.RecordAsync(new FinanceAuditEventDto
        {
            EventType = FinanceAuditEvents.PostingBlockedPeriodClosedLocked,
            TenantId = tenantId,
            SourceModule = request.SourceModule,
            SourceDocumentType = request.SourceDocumentType,
            SourceDocumentId = request.SourceDocumentId == Guid.Empty ? null : request.SourceDocumentId,
            AfterValues = new
            {
                request.SourceModule,
                request.SourceDocumentType,
                request.SourceDocumentId,
                request.PostingAction,
                request.SourceDocumentReference,
                PostingDate = postingDate,
                FiscalPeriodId = fiscalPeriod.Id,
                fiscalPeriod.PeriodCode,
                fiscalPeriod.PeriodName,
                AccountingBookId = accountingBook.Id,
                accountingBook.Code,
                AccountingBookPeriodId = accountingBookPeriod.Id,
                accountingBookPeriod.PeriodStatus
            },
            Comment = "Posting blocked because the selected accounting book is not open for the fiscal period.",
            Resource = "Finance.AccountingBookPeriod",
            ResourceId = accountingBookPeriod.Id.ToString()
        }, cancellationToken);
    }

    private async Task RecordAccountingBookAuthorityDenialAsync(
        Guid tenantId,
        FinancePostingCommandDto request,
        string accountingBookCode,
        string denialReasonCode,
        CancellationToken cancellationToken)
    {
        if (_financeAuditService == null)
            return;

        await _financeAuditService.RecordAsync(new FinanceAuditEventDto
        {
            EventType = FinanceAuditEvents.PostingBlockedAccountingBookAuthority,
            TenantId = tenantId,
            SourceModule = request.SourceModule,
            SourceDocumentType = request.SourceDocumentType,
            SourceDocumentId = request.SourceDocumentId == Guid.Empty ? null : request.SourceDocumentId,
            Reason = denialReasonCode,
            AfterValues = new
            {
                DenialReasonCode = denialReasonCode,
                AccountingBookCode = accountingBookCode,
                request.PostingAction,
                request.SourceDocumentReference,
                request.PostingDate
            },
            Comment = "Posting blocked by canonical accounting-book authority validation.",
            Resource = "Finance.AccountingBook",
            ResourceId = accountingBookCode
        }, cancellationToken);
    }

    private async Task RecordFutureDatedPostingBlockedAuditAsync(
        Guid tenantId,
        FinancePostingCommandDto request,
        FiscalPeriod fiscalPeriod,
        DateTime postingDate,
        CancellationToken cancellationToken)
    {
        if (_financeAuditService == null)
            return;

        await _financeAuditService.RecordAsync(new FinanceAuditEventDto
        {
            EventType = FinanceAuditEvents.PostingBlockedFutureDated,
            TenantId = tenantId,
            SourceModule = request.SourceModule,
            SourceDocumentType = request.SourceDocumentType,
            SourceDocumentId = request.SourceDocumentId == Guid.Empty ? null : request.SourceDocumentId,
            AfterValues = new
            {
                request.PostingAction,
                request.SourceDocumentReference,
                PostingDate = postingDate,
                CurrentUtcDate = DateTime.UtcNow.Date,
                FiscalPeriodId = fiscalPeriod.Id,
                fiscalPeriod.PeriodCode,
                fiscalPeriod.AllowFutureDating
            },
            Comment = "Posting blocked because the period does not permit future-dated Finance postings.",
            Resource = "Finance.FiscalPeriod",
            ResourceId = fiscalPeriod.Id.ToString()
        }, cancellationToken);
    }

    private async Task RecordPostingBlockedByModuleAuditAsync(
        Guid tenantId,
        FinancePostingCommandDto request,
        FiscalPeriod fiscalPeriod,
        string originModuleCode,
        string reason,
        PeriodModuleLock? moduleLock,
        CancellationToken cancellationToken)
    {
        if (_financeAuditService == null)
            return;

        await _financeAuditService.RecordAsync(new FinanceAuditEventDto
        {
            EventType = FinanceAuditEvents.PostingBlockedModuleLocked,
            TenantId = tenantId,
            SourceModule = request.SourceModule,
            SourceDocumentType = request.SourceDocumentType,
            SourceDocumentId = request.SourceDocumentId == Guid.Empty ? null : request.SourceDocumentId,
            Reason = reason,
            AfterValues = new
            {
                request.SourceModule,
                OriginModuleCode = originModuleCode,
                request.SourceDocumentType,
                request.SourceDocumentId,
                request.PostingAction,
                request.SourceDocumentReference,
                request.PostingDate,
                FiscalPeriodId = fiscalPeriod.Id,
                fiscalPeriod.PeriodCode,
                fiscalPeriod.PeriodName,
                fiscalPeriod.IsGlobalLockSuspended,
                ModuleLockId = moduleLock?.Id,
                ModuleLockIsLocked = moduleLock?.IsLocked,
                ModuleLockReason = moduleLock?.LockReason,
                ModuleUnlockReason = moduleLock?.UnlockReason,
                ModuleReopenExpiresAtUtc = moduleLock?.ReopenExpiresAtUtc
            },
            Comment = "Posting blocked by a fiscal-period module lock.",
            Resource = "Finance.FiscalPeriod",
            ResourceId = fiscalPeriod.Id.ToString()
        }, cancellationToken);
    }

    private async Task RecordForeignCurrencyPostingBlockedAuditAsync(
        Guid tenantId,
        FinancePostingCommandDto request,
        string transactionCurrency,
        string reason,
        CancellationToken cancellationToken)
    {
        if (_financeAuditService == null)
        {
            return;
        }

        await _financeAuditService.RecordAsync(new FinanceAuditEventDto
        {
            EventType = FinanceAuditEvents.ForeignCurrencyPostingBlockedInvalidRate,
            TenantId = tenantId,
            SourceModule = request.SourceModule,
            SourceDocumentType = request.SourceDocumentType,
            SourceDocumentId = request.SourceDocumentId == Guid.Empty ? null : request.SourceDocumentId,
            AfterValues = new
            {
                request.SourceModule,
                request.SourceDocumentType,
                request.SourceDocumentId,
                request.PostingAction,
                request.PostingDate,
                TransactionCurrency = transactionCurrency,
                request.FunctionalCurrencyCode
            },
            Reason = reason,
            Resource = "Finance.PostingEvent",
            ResourceId = request.SourceDocumentId == Guid.Empty ? null : request.SourceDocumentId.ToString()
        }, cancellationToken);
    }

    private async Task RecordCurrencySnapshotAuditAsync(
        Guid tenantId,
        ValidatedPosting validation,
        FinancePostingEvent postingEvent,
        Guid journalEntryId,
        CancellationToken cancellationToken)
    {
        if (_financeAuditService == null || !validation.IsMultiCurrency)
        {
            return;
        }

        var rateIds = validation.Lines
            .Where(l => l.ExchangeRateId.HasValue)
            .Select(l => l.ExchangeRateId!.Value)
            .Distinct()
            .ToArray();

        await _financeAuditService.RecordAsync(new FinanceAuditEventDto
        {
            EventType = FinanceAuditEvents.CurrencySnapshotCapturedInPosting,
            TenantId = tenantId,
            SourceModule = validation.SourceModule,
            SourceDocumentType = validation.SourceDocumentType,
            SourceDocumentId = validation.SourceDocumentId,
            JournalEntryId = journalEntryId,
            PostingEventId = postingEvent.Id,
            AfterValues = new
            {
                postingEvent.Id,
                postingEvent.FunctionalCurrencyCode,
                postingEvent.PrimaryTransactionCurrencyCode,
                postingEvent.PrimaryExchangeRateId,
                postingEvent.PrimaryExchangeRate,
                postingEvent.PrimaryExchangeRateDate,
                RateIds = rateIds,
                ForeignCurrencyLineCount = validation.Lines.Count(l => l.ExchangeRateId.HasValue)
            },
            Resource = "Finance.PostingEvent",
            ResourceId = postingEvent.Id.ToString()
        }, cancellationToken);

        foreach (var rateId in rateIds)
        {
            await _financeAuditService.RecordAsync(new FinanceAuditEventDto
            {
                EventType = FinanceAuditEvents.ExchangeRateUsedInPosting,
                TenantId = tenantId,
                SourceModule = validation.SourceModule,
                SourceDocumentType = validation.SourceDocumentType,
                SourceDocumentId = validation.SourceDocumentId,
                JournalEntryId = journalEntryId,
                PostingEventId = postingEvent.Id,
                AfterValues = new
                {
                    ExchangeRateId = rateId,
                    postingEvent.Id,
                    validation.PostingDate,
                    validation.FunctionalCurrencyCode
                },
                Resource = "Finance.ExchangeRate",
                ResourceId = rateId.ToString()
            }, cancellationToken);
        }
    }

    private async Task RecordExchangeRatePolicyOverrideAuditAsync(
        Guid tenantId,
        ValidatedPosting validation,
        FinancePostingEvent postingEvent,
        Guid journalEntryId,
        CancellationToken cancellationToken)
    {
        if (_financeAuditService == null || !validation.ExchangeRatePolicyOverrideUsed)
            return;

        await _financeAuditService.RecordAsync(new FinanceAuditEventDto
        {
            EventType = FinanceAuditEvents.ExchangeRatePolicyOverrideUsed,
            TenantId = tenantId,
            SourceModule = validation.SourceModule,
            SourceDocumentType = validation.SourceDocumentType,
            SourceDocumentId = validation.SourceDocumentId,
            JournalEntryId = journalEntryId,
            PostingEventId = postingEvent.Id,
            Reason = validation.ExchangeRateOverrideReason,
            AfterValues = new
            {
                validation.ExchangeRateOverrideApprovedByUserId,
                validation.ExchangeRateOverrideApprovedAt,
                ExchangeRateIds = validation.Lines
                    .Where(l => l.ExchangeRateId.HasValue)
                    .Select(l => l.ExchangeRateId!.Value)
                    .Distinct()
                    .ToArray()
            },
            Resource = "Finance.PostingEvent",
            ResourceId = postingEvent.Id.ToString()
        }, cancellationToken);
    }

    private static string BuildRequestFingerprint(
        Guid tenantId,
        string sourceModule,
        string originModuleCode,
        string sourceDocumentType,
        Guid sourceDocumentId,
        string postingAction,
        string? sourceDocumentReference,
        string? idempotencyKey,
        Guid? existingJournalEntryId,
        Guid? reversalOfJournalEntryId,
        string? reversalReason,
        string? reversalType,
        string description,
        DateTime postingDate,
        Guid fiscalPeriodId,
        string journalType,
        Guid accountingBookId,
        string accountingBookCode,
        string functionalCurrencyCode,
        FinancePostingCommandDto request,
        IReadOnlyList<ValidatedPostingLine> lines,
        IReadOnlyList<Guid> budgetReservationIds,
        string? budgetReservationSourceDocumentType)
    {
        // Domain/version separation makes any future canonical-form change explicit. Never alter
        // this V1 grammar in place: historical duplicate decisions depend on byte-for-byte stability.
        var canonical = new StringBuilder(4096);
        void Add(string name, string? value)
        {
            canonical.Append(name).Append('=');
            if (value is null)
                canonical.Append("-1:");
            else
                canonical.Append(value.Length).Append(':').Append(value);
            canonical.Append('\n');
        }
        void AddGuid(string name, Guid? value) => Add(name, value?.ToString("N"));
        void AddDecimal(string name, decimal? value) => Add(name, value?.ToString("G29", CultureInfo.InvariantCulture));
        void AddDate(string name, DateTime? value) => Add(name, value?.ToString("O", CultureInfo.InvariantCulture));
        void AddBool(string name, bool value) => Add(name, value ? "1" : "0");
        void AddInt(string name, int? value) => Add(name, value?.ToString(CultureInfo.InvariantCulture));

        Add("domain", RequestFingerprintDomain);
        Add("version", RequestFingerprintVersion);
        AddGuid("tenantId", tenantId);
        Add("sourceModule", sourceModule);
        Add("originModuleCode", originModuleCode);
        Add("sourceDocumentType", sourceDocumentType);
        AddGuid("sourceDocumentId", sourceDocumentId);
        AddGuid("sourceDocumentTenantId", tenantId);
        Add("postingAction", postingAction);
        Add("sourceDocumentReference", sourceDocumentReference);
        Add("idempotencyKey", idempotencyKey);
        AddGuid("existingJournalEntryId", existingJournalEntryId);
        AddGuid("reversalOfJournalEntryId", reversalOfJournalEntryId);
        Add("reversalReason", reversalReason);
        Add("reversalType", reversalType);
        Add("description", description);
        AddDate("postingDate", postingDate);
        AddGuid("fiscalPeriodId", fiscalPeriodId);
        Add("journalType", journalType);
        AddGuid("accountingBookId", accountingBookId);
        Add("accountingBookCode", accountingBookCode);
        Add("functionalCurrencyCode", functionalCurrencyCode);
        Add("exchangeRateTypeOverride", request.ExchangeRateTypeOverride?.Trim().ToUpperInvariant());
        Add("exchangeRateQuoteSideOverride", request.ExchangeRateQuoteSideOverride?.Trim().ToUpperInvariant());
        Add("exchangeRateOverrideReason", NormalizeOptional(request.ExchangeRateOverrideReason, 500, "Exchange-rate override reason"));
        AddGuid("exchangeRateOverrideApprovedByUserId", request.ExchangeRateOverrideApprovedByUserId);
        AddDate("exchangeRateOverrideApprovedAt", request.ExchangeRateOverrideApprovedAt);
        AddBool("preserveHistoricalExchangeRateSnapshot", request.PreserveHistoricalExchangeRateSnapshot);
        AddBool("allowPostingToClosedPeriod", request.AllowPostingToClosedPeriod);
        Add("budgetReservationSourceDocumentType", budgetReservationSourceDocumentType);
        Add("budgetReservationCount", budgetReservationIds.Count.ToString(CultureInfo.InvariantCulture));
        for (var index = 0; index < budgetReservationIds.Count; index++)
            AddGuid($"budgetReservation[{index}]", budgetReservationIds[index]);

        Add("lineCount", lines.Count.ToString(CultureInfo.InvariantCulture));
        for (var lineIndex = 0; lineIndex < lines.Count; lineIndex++)
        {
            var line = lines[lineIndex];
            var prefix = $"line[{lineIndex}]";
            AddGuid($"{prefix}.accountId", line.AccountId);
            AddGuid($"{prefix}.sourceDocumentLineId", line.SourceDocumentLineId);
            Add($"{prefix}.description", line.Description);
            AddDecimal($"{prefix}.debitAmount", line.DebitAmount);
            AddDecimal($"{prefix}.creditAmount", line.CreditAmount);
            Add($"{prefix}.transactionCurrency", line.TransactionCurrency);
            AddDecimal($"{prefix}.transactionDebitAmount", line.TransactionDebitAmount);
            AddDecimal($"{prefix}.transactionCreditAmount", line.TransactionCreditAmount);
            AddDecimal($"{prefix}.foreignCurrencyAmount", line.ForeignCurrencyAmount);
            AddGuid($"{prefix}.exchangeRateId", line.ExchangeRateId);
            AddDecimal($"{prefix}.exchangeRate", line.ExchangeRate);
            Add($"{prefix}.exchangeRateSource", line.ExchangeRateSource);
            AddDate($"{prefix}.exchangeRateDate", line.ExchangeRateDate);
            Add($"{prefix}.sourceReferenceNumber", line.SourceReferenceNumber);
            AddInt($"{prefix}.lineNumber", line.LineNumber);
            Add($"{prefix}.segmentString", line.SegmentString);
            Add($"{prefix}.notes", line.Notes);
            Add($"{prefix}.transactionTag", line.TransactionTag);
            AddGuid($"{prefix}.dimensionSetId", line.DimensionSet?.Id);
            Add($"{prefix}.dimensionHash", line.DimensionSet?.CombinationHash);
            var dimensionItems = line.DimensionSet?.Items
                .OrderBy(item => item.DisplayOrder)
                .ThenBy(item => item.DimensionCode, StringComparer.Ordinal)
                .ThenBy(item => item.DefinitionId)
                .ToArray() ?? [];
            Add($"{prefix}.dimensionCount", dimensionItems.Length.ToString(CultureInfo.InvariantCulture));
            for (var dimensionIndex = 0; dimensionIndex < dimensionItems.Length; dimensionIndex++)
            {
                var item = dimensionItems[dimensionIndex];
                var dimensionPrefix = $"{prefix}.dimension[{dimensionIndex}]";
                AddGuid($"{dimensionPrefix}.definitionId", item.DefinitionId);
                AddGuid($"{dimensionPrefix}.valueId", item.ValueId);
                Add($"{dimensionPrefix}.code", item.DimensionCode);
                Add($"{dimensionPrefix}.name", item.DimensionName);
                Add($"{dimensionPrefix}.valueCode", item.ValueCode);
                Add($"{dimensionPrefix}.valueName", item.ValueName);
                AddInt($"{dimensionPrefix}.displayOrder", item.DisplayOrder);
                AddGuid($"{dimensionPrefix}.ruleId", item.RuleId);
                AddGuid($"{dimensionPrefix}.ruleFamilyId", item.RuleFamilyId);
                AddInt($"{dimensionPrefix}.ruleVersion", item.RuleVersion);
                Add($"{dimensionPrefix}.ruleType", item.RuleType);
                AddDate($"{dimensionPrefix}.ruleEffectiveDate", item.RuleEffectiveDate);
                AddDate($"{dimensionPrefix}.ruleExpiryDate", item.RuleExpiryDate);
            }
        }

        var taxSnapshots = (request.TaxCalculationSnapshots ?? Array.Empty<FinanceTaxCalculationSnapshotDto>())
            .OrderBy(item => item.DocumentType, StringComparer.Ordinal)
            .ThenBy(item => item.DocumentId)
            .ThenBy(item => item.DocumentLineId)
            .ThenBy(item => item.CalculationOrder)
            .ThenBy(item => item.TaxId)
            .ToArray();
        Add("taxSnapshotCount", taxSnapshots.Length.ToString(CultureInfo.InvariantCulture));
        for (var index = 0; index < taxSnapshots.Length; index++)
        {
            var item = taxSnapshots[index];
            var prefix = $"tax[{index}]";
            Add($"{prefix}.documentType", item.DocumentType?.Trim());
            AddGuid($"{prefix}.documentId", item.DocumentId);
            AddGuid($"{prefix}.documentLineId", item.DocumentLineId);
            AddGuid($"{prefix}.taxId", item.TaxId);
            AddGuid($"{prefix}.taxGroupId", item.TaxGroupId);
            AddGuid($"{prefix}.postingAccountId", item.PostingAccountId);
            AddDecimal($"{prefix}.baseAmount", item.BaseAmount);
            AddDecimal($"{prefix}.taxableAmount", item.TaxableAmount);
            AddDecimal($"{prefix}.taxRate", item.TaxRate);
            AddDecimal($"{prefix}.taxAmount", item.TaxAmount);
            AddInt($"{prefix}.compoundBasis", (int)item.CompoundBasis);
            AddInt($"{prefix}.calculationOrder", item.CalculationOrder);
            AddDate($"{prefix}.calculationDate", item.CalculationDate);
            AddBool($"{prefix}.manualOverride", item.IsManualOverride);
            Add($"{prefix}.overrideReason", item.OverrideReason?.Trim());
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }

    private static string GeneratePostingJournalNumber(string sourceModule, DateTime now)
    {
        var prefix = new string(sourceModule
            .Where(char.IsLetterOrDigit)
            .Take(6)
            .ToArray());

        if (string.IsNullOrWhiteSpace(prefix))
        {
            prefix = "FIN";
        }

        return $"FP-{prefix.ToUpperInvariant()}-{now:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}"[..50];
    }

    private Guid? GetCurrentUserGuid()
    {
        return Guid.TryParse(_currentUserService.UserId, out var parsedUserId)
            ? parsedUserId
            : null;
    }

    private static decimal RoundMoney(decimal amount, int decimalPlaces = 2) =>
        CurrencyMinorUnitPolicy.Round(amount, decimalPlaces);

    private async Task<int> ResolveCurrencyDecimalPlacesAsync(
        Guid tenantId,
        string currencyCode,
        CancellationToken cancellationToken)
    {
        var normalized = NormalizeCurrency(currencyCode, "Currency");
        var configured = await _context.Currencies
            .AsNoTracking()
            .Where(currency => currency.TenantId == tenantId
                && currency.CurrencyCode == normalized
                && !currency.IsDeleted)
            .Select(currency => (int?)currency.DecimalPlaces)
            .SingleOrDefaultAsync(cancellationToken);

        if (configured.HasValue)
        {
            CurrencyMinorUnitPolicy.Validate(normalized, configured.Value);
            return configured.Value;
        }

        return CurrencyMinorUnitPolicy.ExpectedDecimalPlaces(normalized)
            ?? throw new InvalidOperationException(
                $"Currency {normalized} must be configured with its ISO 4217 minor-unit precision before posting.");
    }

    private static decimal RoundRate(decimal amount)
    {
        return decimal.Round(amount, 6, MidpointRounding.AwayFromZero);
    }

    private static string NormalizeCurrency(string? value, string fieldName, string defaultValue = "GHS")
    {
        var normalized = string.IsNullOrWhiteSpace(value)
            ? defaultValue
            : value.Trim().ToUpperInvariant();

        if (normalized.Length != 3)
        {
            throw new InvalidOperationException($"{fieldName} must be a three-character ISO currency code.");
        }

        return normalized;
    }

    private static string NormalizeRequired(string? value, string fieldName, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException($"{fieldName} is required.");
        }

        if (normalized.Length > maxLength)
        {
            throw new InvalidOperationException($"{fieldName} cannot exceed {maxLength} characters.");
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value, int maxLength, string fieldName)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        if (normalized.Length > maxLength)
        {
            throw new InvalidOperationException($"{fieldName} cannot exceed {maxLength} characters.");
        }

        return normalized;
    }

    private async Task<ValidatedDimensionSet?> ResolveDimensionSetAsync(
        Guid tenantId,
        DateTime postingDate,
        FinancePostingCommandDto request,
        FinanceDimensionRouteDefinition? route,
        FinanceDimensionCertificationState certificationState,
        FinancePostingLineDto line,
        decimal normalizedDebit,
        decimal normalizedCredit,
        CancellationToken cancellationToken)
    {
        var dimensions = line.Dimensions?.ToList() ?? new List<FinancePostingDimensionValueDto>();
        if (line.FinanceDimensionSetId.HasValue)
        {
            if (dimensions.Count > 0)
                throw new InvalidOperationException("A posting line cannot supply both dimension values and a historical dimension-set ID.");
            if (!request.ReversalOfJournalEntryId.HasValue && !request.ExistingJournalEntryId.HasValue)
                throw new InvalidOperationException("Stored Finance dimension-set IDs may only be reused from an exact Finance journal line.");

            var originalLine = await _context.AccountTransactions.AsNoTracking()
                .Include(x => x.FinanceDimensionSnapshot)!.ThenInclude(snapshot => snapshot!.Items)
                .SingleOrDefaultAsync(x =>
                x.TenantId == tenantId
                && !x.IsDeleted
                && x.JournalEntryId == (request.ReversalOfJournalEntryId ?? request.ExistingJournalEntryId)!.Value
                && x.AccountId == line.AccountId
                && x.FinanceDimensionSetId == line.FinanceDimensionSetId.Value
                && (request.ReversalOfJournalEntryId.HasValue
                    ? x.DebitAmount == normalizedCredit && x.CreditAmount == normalizedDebit
                    : x.DebitAmount == normalizedDebit && x.CreditAmount == normalizedCredit)
                && (!line.LineNumber.HasValue || x.LineNumber == line.LineNumber), cancellationToken);
            if (originalLine is null)
                throw new InvalidOperationException("The historical Finance dimension set does not belong to the exact original journal line being reversed.");
            if (originalLine.FinanceDimensionSnapshot is not null)
                return ToValidatedDimensionSet(originalLine.FinanceDimensionSnapshot);

            var historical = await _context.FinanceDimensionSets.AsNoTracking()
                .Include(x => x.Items)
                .SingleOrDefaultAsync(x => x.TenantId == tenantId
                    && x.Id == line.FinanceDimensionSetId.Value && !x.IsDeleted, cancellationToken)
                ?? throw new InvalidOperationException("The historical Finance dimension set was not found for this tenant.");
            return ToValidatedDimensionSet(historical, requiresInsert: false);
        }

        if (dimensions.Count > 20)
            throw new InvalidOperationException("A posting line cannot contain more than 20 Finance dimensions.");

        var normalized = dimensions.Select(input =>
        {
            var dimensionCode = NormalizeRequired(input.DimensionCode, "Dimension code", 30).ToUpperInvariant();
            var valueCode = NormalizeOptional(input.ValueCode, 50, "Dimension value code")?.ToUpperInvariant();
            var sourceEntityType = NormalizeOptional(input.SourceEntityType, 100, "Dimension source entity type");
            var hasLookup = valueCode is not null;
            var hasEntity = sourceEntityType is not null || input.SourceEntityId.HasValue;
            if (hasLookup == hasEntity || (hasEntity && (sourceEntityType is null || !input.SourceEntityId.HasValue)))
                throw new InvalidOperationException($"Dimension {dimensionCode} must identify exactly one lookup value or one source entity.");
            return new NormalizedDimensionInput(dimensionCode, valueCode, sourceEntityType, input.SourceEntityId);
        }).ToList();

        var repeatedCodes = normalized.GroupBy(x => x.DimensionCode, StringComparer.Ordinal)
            .Where(x => x.Count() > 1).Select(x => x.Key).OrderBy(x => x).ToArray();
        if (repeatedCodes.Length > 0)
            throw new InvalidOperationException($"A posting line contains duplicate Finance dimension(s): {string.Join(", ", repeatedCodes)}.");

        var inputs = normalized.ToDictionary(item => item.DimensionCode, StringComparer.Ordinal);
        var rules = await ApplicableDimensionRulesAsync(
            tenantId, line.AccountId, postingDate, request, route, cancellationToken);
        foreach (var rule in rules)
        {
            var code = rule.FinanceDimensionDefinition.Code;
            var hasInput = inputs.TryGetValue(code, out var supplied);
            switch (rule.RuleType)
            {
                case "Prohibited" when hasInput:
                    throw new InvalidOperationException($"Dimension {code} is prohibited for account {line.AccountId} on route '{route?.SourceRoute ?? "uncertified"}'.");
                case "Fixed":
                    if (rule.DefaultDimensionValue is null
                        || !IsEffective(rule.DefaultDimensionValue, postingDate))
                        throw new InvalidOperationException($"Fixed dimension rule {code} has no active, effective value.");
                    if (hasInput && !MatchesDimensionInput(supplied!, rule.DefaultDimensionValue))
                        throw new InvalidOperationException($"Dimension {code} is fixed at {rule.DefaultDimensionValue.Code}.");
                    inputs[code] = ToNormalizedInput(rule.FinanceDimensionDefinition, rule.DefaultDimensionValue);
                    break;
                case "Required" when !hasInput && rule.DefaultDimensionValue is not null:
                    if (!IsEffective(rule.DefaultDimensionValue, postingDate))
                        throw new InvalidOperationException($"Default value for required dimension {code} is not effective.");
                    inputs[code] = ToNormalizedInput(rule.FinanceDimensionDefinition, rule.DefaultDimensionValue);
                    break;
                case "Required" when !hasInput && certificationState == FinanceDimensionCertificationState.Enforced:
                    throw new InvalidOperationException($"Dimension {code} is required for account {line.AccountId} on certified route '{route?.SourceRoute}'.");
                case "Optional" when !hasInput && rule.DefaultDimensionValue is not null:
                    if (IsEffective(rule.DefaultDimensionValue, postingDate))
                        inputs[code] = ToNormalizedInput(rule.FinanceDimensionDefinition, rule.DefaultDimensionValue);
                    break;
            }
        }

        if (inputs.Count == 0)
            return null;

        var requestedCodes = inputs.Keys.ToArray();
        var definitions = await _context.FinanceDimensionDefinitions.AsNoTracking()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted && x.IsActive && requestedCodes.Contains(x.Code))
            .ToListAsync(cancellationToken);
        if (definitions.Count != inputs.Count)
            throw new InvalidOperationException("One or more Finance dimensions were not found or are inactive for this tenant.");

        var clientCodes = normalized.Select(item => item.DimensionCode).ToHashSet(StringComparer.Ordinal);
        var suppliedDerived = definitions.FirstOrDefault(definition =>
            definition.Classification == "Derived" && clientCodes.Contains(definition.Code));
        if (suppliedDerived is not null)
            throw new InvalidOperationException($"Derived dimension {suppliedDerived.Code} must be resolved by Finance, not supplied by a producer.");

        var resolvedItems = new List<ValidatedDimensionSetItem>();
        foreach (var input in inputs.Values)
        {
            var definition = definitions.Single(x => x.Code == input.DimensionCode);
            if (definition.ValueSourceType == "Lookup" && input.ValueCode is null)
                throw new InvalidOperationException($"Dimension {definition.Code} requires a Finance lookup value.");
            if (definition.ValueSourceType == "EntityBacked" && !input.SourceEntityId.HasValue)
                throw new InvalidOperationException($"Dimension {definition.Code} requires canonical source-entity lineage.");

            var value = await _context.FinanceDimensionValues.AsNoTracking().SingleOrDefaultAsync(x =>
                x.TenantId == tenantId && !x.IsDeleted && x.IsActive
                && x.FinanceDimensionDefinitionId == definition.Id
                && x.EffectiveDate.Date <= postingDate.Date
                && (!x.ExpiryDate.HasValue || x.ExpiryDate.Value.Date >= postingDate.Date)
                && (input.ValueCode != null
                    ? x.Code == input.ValueCode
                    : x.SourceEntityType == input.SourceEntityType && x.SourceEntityId == input.SourceEntityId), cancellationToken)
                ?? throw new InvalidOperationException($"Dimension value for {definition.Code} was not found, active, and effective for the posting date.");

            var appliedRule = rules.SingleOrDefault(rule =>
                rule.FinanceDimensionDefinitionId == definition.Id);
            resolvedItems.Add(new ValidatedDimensionSetItem(
                definition.Id,
                value.Id,
                definition.Code,
                definition.Name,
                value.Code,
                value.Name,
                definition.DisplayOrder,
                appliedRule?.Id,
                appliedRule?.RuleFamilyId,
                appliedRule?.RuleVersion,
                appliedRule?.RuleType,
                appliedRule?.EffectiveDate,
                appliedRule?.ExpiryDate));
        }

        resolvedItems = resolvedItems.OrderBy(x => x.DisplayOrder).ThenBy(x => x.DimensionCode, StringComparer.Ordinal).ToList();
        var canonical = string.Join("|", resolvedItems.Select(x => $"{x.DefinitionId:N}:{x.ValueId:N}"));
        var combinationHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        var deterministicBytes = SHA256.HashData(Encoding.UTF8.GetBytes($"FIN-DIMSET|{tenantId:N}|{combinationHash}"));
        var dimensionSetId = new Guid(deterministicBytes.AsSpan(0, 16));
        var displayValue = string.Join(" · ", resolvedItems.Select(x => $"{x.DimensionCode}={x.ValueCode}"));

        var existing = await _context.FinanceDimensionSets.AsNoTracking().Include(x => x.Items)
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && !x.IsDeleted
                && (x.Id == dimensionSetId || x.CombinationHash == combinationHash), cancellationToken);
        if (existing is not null)
        {
            if (existing.Id != dimensionSetId || existing.CombinationHash != combinationHash)
                throw new InvalidOperationException("Finance dimension-set identity collision detected.");
            EnsureSetItemsMatch(existing, resolvedItems);
            // Canonical sets intentionally store only reusable definition/value membership.
            // Keep the effective account-rule evidence resolved for this posting so the exact
            // rule version is frozen on the transaction-specific snapshot below.
            return new ValidatedDimensionSet(
                existing.Id,
                existing.CombinationHash,
                existing.DisplayValue,
                resolvedItems,
                RequiresInsert: false);
        }

        return new ValidatedDimensionSet(dimensionSetId, combinationHash, displayValue, resolvedItems, true);
    }

    private static ValidatedDimensionSet? ResolveYearEndDimensionSet(AccountTransaction source) =>
        source.FinanceDimensionSnapshot != null ? ToValidatedDimensionSet(source.FinanceDimensionSnapshot)
        : source.FinanceDimensionSet != null ? ToValidatedDimensionSet(source.FinanceDimensionSet, requiresInsert: false)
        : null;

    private async Task EnsureDimensionSetsAsync(
        Guid tenantId,
        IReadOnlyList<ValidatedPostingLine> lines,
        DateTime now,
        Guid? actorId,
        CancellationToken cancellationToken)
    {
        foreach (var plan in lines.Select(x => x.DimensionSet).Where(x => x is { RequiresInsert: true })
                     .Cast<ValidatedDimensionSet>().DistinctBy(x => x.Id))
        {
            if (_context.Database.IsSqlServer())
            {
                if (_context.Database.CurrentTransaction is null)
                    throw new InvalidOperationException("Finance dimension-set creation requires an active database transaction.");
                var resource = $"FIN:DIMSET:{tenantId:N}:{plan.CombinationHash}";
                await _context.Database.ExecuteSqlInterpolatedAsync($@"
DECLARE @result int;
EXEC @result = sys.sp_getapplock
    @Resource = {resource},
    @LockMode = 'Exclusive',
    @LockOwner = 'Transaction',
    @LockTimeout = 15000;
IF @result < 0 THROW 51000, 'Could not acquire Finance dimension-set lock.', 1;", cancellationToken);
            }

            var existing = _context.FinanceDimensionSets.Local.FirstOrDefault(x => x.Id == plan.Id)
                ?? await _context.FinanceDimensionSets.Include(x => x.Items).SingleOrDefaultAsync(x =>
                    x.TenantId == tenantId && !x.IsDeleted
                    && (x.Id == plan.Id || x.CombinationHash == plan.CombinationHash), cancellationToken);
            if (existing is not null)
            {
                if (existing.Id != plan.Id || existing.CombinationHash != plan.CombinationHash)
                    throw new InvalidOperationException("Finance dimension-set identity collision detected.");
                EnsureSetItemsMatch(existing, plan.Items);
                continue;
            }

            var set = new FinanceDimensionSet
            {
                Id = plan.Id,
                TenantId = tenantId,
                CombinationHash = plan.CombinationHash,
                DisplayValue = plan.DisplayValue,
                CreatedAt = now,
                CreatedBy = _currentUserService.UserName,
                CreatedById = actorId
            };
            foreach (var item in plan.Items)
            {
                var itemIdBytes = SHA256.HashData(Encoding.UTF8.GetBytes(
                    $"FIN-DIMSET-ITEM|{tenantId:N}|{plan.Id:N}|{item.DefinitionId:N}"));
                set.Items.Add(new FinanceDimensionSetItem
                {
                    Id = new Guid(itemIdBytes.AsSpan(0, 16)),
                    TenantId = tenantId,
                    FinanceDimensionSetId = set.Id,
                    FinanceDimensionDefinitionId = item.DefinitionId,
                    FinanceDimensionValueId = item.ValueId,
                    DimensionCodeSnapshot = item.DimensionCode,
                    DimensionNameSnapshot = item.DimensionName,
                    DimensionValueCodeSnapshot = item.ValueCode,
                    DimensionValueNameSnapshot = item.ValueName,
                    SnapshotSource = "CanonicalResolution",
                    SnapshotCapturedAt = now,
                    SnapshotQuality = "Exact",
                    CreatedAt = now,
                    CreatedBy = _currentUserService.UserName,
                    CreatedById = actorId
                });
            }
            _context.FinanceDimensionSets.Add(set);
        }
    }

    private async Task EnsureDimensionSnapshotsAsync(
        Guid tenantId,
        ValidatedPosting validation,
        JournalEntry journalEntry,
        DateTime now,
        Guid? actorId,
        CancellationToken cancellationToken)
    {
        var matchedTransactions = new HashSet<Guid>();
        var ruleIds = validation.Lines.SelectMany(line => line.DimensionSet?.Items ?? [])
            .Where(item => item.RuleId.HasValue).Select(item => item.RuleId!.Value).Distinct().ToArray();
        if (ruleIds.Length > 0)
        {
            var rules = await _context.FinanceDimensionAccountRules
                .Where(rule => rule.TenantId == tenantId && ruleIds.Contains(rule.Id) && !rule.IsDeleted)
                .ToListAsync(cancellationToken);
            if (rules.Count != ruleIds.Length)
                throw new InvalidOperationException("One or more Finance dimension rule versions were not found while freezing posting evidence.");
            foreach (var rule in rules) rule.IsEvidenceLocked = true;
        }

        foreach (var line in validation.Lines.Where(item => item.DimensionSet is not null))
        {
            var transaction = journalEntry.Transactions.SingleOrDefault(item =>
                !matchedTransactions.Contains(item.Id) && item.AccountId == line.AccountId
                && item.LineNumber == line.LineNumber && item.DebitAmount == line.DebitAmount
                && item.CreditAmount == line.CreditAmount)
                ?? throw new InvalidOperationException("Could not bind Finance dimension evidence to the exact journal line.");
            matchedTransactions.Add(transaction.Id);
            if (transaction.FinanceDimensionSnapshotId.HasValue) continue;

            var set = line.DimensionSet!;
            var rulePayload = string.Join("|", set.Items.OrderBy(item => item.DimensionCode, StringComparer.Ordinal)
                .Select(item => $"{item.DefinitionId:N}:{item.ValueId:N}:{item.RuleFamilyId?.ToString("N") ?? "NONE"}:{item.RuleVersion?.ToString() ?? "NONE"}:{item.RuleType ?? "NONE"}"));
            var snapshot = new FinanceDimensionSnapshot
            {
                Id = Guid.NewGuid(), TenantId = tenantId, FinanceDimensionSetId = set.Id,
                CombinationHashSnapshot = set.CombinationHash, DisplayValueSnapshot = set.DisplayValue,
                SnapshotSource = "PostingResolution", SnapshotCapturedAt = now, SnapshotQuality = "Exact",
                RuleEvidenceHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rulePayload))),
                ProducerModule = validation.DimensionRoute?.ProducerModule,
                SourceRoute = validation.DimensionRoute?.SourceRoute,
                SourceDocumentType = validation.SourceDocumentType,
                ContractVersion = validation.DimensionRoute?.ContractVersion,
                CreatedAt = now, CreatedBy = _currentUserService.UserName, CreatedById = actorId
            };
            foreach (var item in set.Items)
            {
                snapshot.Items.Add(new FinanceDimensionSnapshotItem
                {
                    Id = Guid.NewGuid(), TenantId = tenantId, FinanceDimensionSnapshotId = snapshot.Id,
                    FinanceDimensionDefinitionId = item.DefinitionId, FinanceDimensionValueId = item.ValueId,
                    DimensionCodeSnapshot = item.DimensionCode, DimensionNameSnapshot = item.DimensionName,
                    DimensionValueCodeSnapshot = item.ValueCode, DimensionValueNameSnapshot = item.ValueName,
                    FinanceDimensionAccountRuleId = item.RuleId, RuleFamilyIdSnapshot = item.RuleFamilyId,
                    RuleVersionSnapshot = item.RuleVersion, RuleTypeSnapshot = item.RuleType,
                    RuleEffectiveDateSnapshot = item.RuleEffectiveDate, RuleExpiryDateSnapshot = item.RuleExpiryDate,
                    SnapshotSource = "PostingResolution", SnapshotCapturedAt = now, SnapshotQuality = "Exact",
                    CreatedAt = now, CreatedBy = _currentUserService.UserName, CreatedById = actorId
                });
            }
            _context.FinanceDimensionSnapshots.Add(snapshot);
            transaction.FinanceDimensionSnapshotId = snapshot.Id;
            transaction.FinanceDimensionSnapshot = snapshot;
        }
    }

    private static ValidatedDimensionSet ToValidatedDimensionSet(FinanceDimensionSet source, bool requiresInsert) =>
        new(source.Id, source.CombinationHash, source.DisplayValue,
            source.Items.OrderBy(x => x.DimensionCodeSnapshot, StringComparer.Ordinal).Select(x =>
                new ValidatedDimensionSetItem(x.FinanceDimensionDefinitionId, x.FinanceDimensionValueId,
                    x.DimensionCodeSnapshot, x.DimensionNameSnapshot, x.DimensionValueCodeSnapshot,
                    x.DimensionValueNameSnapshot, 0, null, null, null, null, null, null)).ToList(),
            requiresInsert);

    private static ValidatedDimensionSet ToValidatedDimensionSet(FinanceDimensionSnapshot snapshot) =>
        new(snapshot.FinanceDimensionSetId, snapshot.CombinationHashSnapshot, snapshot.DisplayValueSnapshot,
            snapshot.Items.OrderBy(item => item.DimensionCodeSnapshot, StringComparer.Ordinal).Select(item =>
                new ValidatedDimensionSetItem(
                    item.FinanceDimensionDefinitionId, item.FinanceDimensionValueId,
                    item.DimensionCodeSnapshot, item.DimensionNameSnapshot,
                    item.DimensionValueCodeSnapshot, item.DimensionValueNameSnapshot, 0,
                    item.FinanceDimensionAccountRuleId, item.RuleFamilyIdSnapshot,
                    item.RuleVersionSnapshot, item.RuleTypeSnapshot,
                    item.RuleEffectiveDateSnapshot, item.RuleExpiryDateSnapshot)).ToList(),
            RequiresInsert: false);

    private async Task<IReadOnlyList<FinanceDimensionAccountRule>> ApplicableDimensionRulesAsync(
        Guid tenantId,
        Guid accountId,
        DateTime date,
        FinancePostingCommandDto request,
        FinanceDimensionRouteDefinition? route,
        CancellationToken cancellationToken)
    {
        var routeId = route?.Id;
        var candidates = await _context.FinanceDimensionAccountRules.AsNoTracking()
            .Include(item => item.FinanceDimensionDefinition)
            .Include(item => item.DefaultDimensionValue)
            .Where(item => item.TenantId == tenantId && item.AccountId == accountId
                && !item.IsDeleted && item.IsActive
                && item.EffectiveDate.Date <= date.Date
                && (!item.ExpiryDate.HasValue || item.ExpiryDate.Value.Date >= date.Date)
                && (!item.RouteId.HasValue || item.RouteId == routeId)
                && (item.SourceModule == null || item.SourceModule == request.SourceModule)
                && (item.SourceDocumentType == null || item.SourceDocumentType == request.SourceDocumentType)
                && (item.PostingAction == null || item.PostingAction == request.PostingAction))
            .ToListAsync(cancellationToken);

        return candidates.GroupBy(item => item.FinanceDimensionDefinitionId).Select(group =>
        {
            var ordered = group.OrderByDescending(item => DimensionRuleSpecificity(item, routeId))
                .ThenByDescending(item => item.RuleVersion).ToList();
            if (ordered.Count > 1
                && DimensionRuleSpecificity(ordered[0], routeId) == DimensionRuleSpecificity(ordered[1], routeId))
                throw new InvalidOperationException($"Ambiguous Finance dimension rules exist for {ordered[0].FinanceDimensionDefinition.Code}.");
            return ordered[0];
        }).ToList();
    }

    private static int DimensionRuleSpecificity(
        FinanceDimensionAccountRule rule,
        FinanceDimensionRouteId? routeId) =>
        (rule.RouteId.HasValue && rule.RouteId == routeId ? 8 : 0)
        + (rule.SourceModule is null ? 0 : 4)
        + (rule.SourceDocumentType is null ? 0 : 2)
        + (rule.PostingAction is null ? 0 : 1);

    private async Task<FinanceDimensionCertificationState> ResolveDimensionCertificationStateAsync(
        Guid tenantId,
        FinanceDimensionRouteDefinition route,
        CancellationToken cancellationToken) =>
        await _context.FinanceDimensionRouteCertifications.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.RouteId == route.Id && !item.IsDeleted
                && item.EffectiveDate <= DateTime.UtcNow)
            .Select(item => (FinanceDimensionCertificationState?)item.State)
            .SingleOrDefaultAsync(cancellationToken)
        ?? route.DefaultState;

    private static bool MatchesDimensionInput(NormalizedDimensionInput input, FinanceDimensionValue value) =>
        input.ValueCode is not null
            ? string.Equals(input.ValueCode, value.Code, StringComparison.Ordinal)
            : input.SourceEntityId == value.SourceEntityId
              && string.Equals(input.SourceEntityType, value.SourceEntityType, StringComparison.Ordinal);

    private static NormalizedDimensionInput ToNormalizedInput(
        FinanceDimensionDefinition definition,
        FinanceDimensionValue value) =>
        definition.ValueSourceType == "Lookup"
            ? new NormalizedDimensionInput(definition.Code, value.Code, null, null)
            : new NormalizedDimensionInput(definition.Code, null, value.SourceEntityType, value.SourceEntityId);

    private static bool IsEffective(FinanceDimensionValue value, DateTime date) =>
        !value.IsDeleted && value.IsActive && value.EffectiveDate.Date <= date.Date
        && (!value.ExpiryDate.HasValue || value.ExpiryDate.Value.Date >= date.Date);

    private static void EnsureSetItemsMatch(FinanceDimensionSet existing, IReadOnlyList<ValidatedDimensionSetItem> requested)
    {
        var stored = existing.Items.Select(x => $"{x.FinanceDimensionDefinitionId:N}:{x.FinanceDimensionValueId:N}")
            .OrderBy(x => x, StringComparer.Ordinal).ToArray();
        var expected = requested.Select(x => $"{x.DefinitionId:N}:{x.ValueId:N}")
            .OrderBy(x => x, StringComparer.Ordinal).ToArray();
        if (!stored.SequenceEqual(expected, StringComparer.Ordinal))
            throw new InvalidOperationException("Stored Finance dimension-set items do not match their canonical combination hash.");
    }

    private static bool IsCurrencyLinkEffectiveForPosting(AccountCurrencyLink link, DateTime postingDate)
    {
        var postingDay = postingDate.Date;
        return link.IsActive
            && link.EffectiveDate.Date <= postingDay
            && (!link.EffectiveEndDate.HasValue || link.EffectiveEndDate.Value.Date >= postingDay);
    }

    private sealed record ValidatedPosting(
        FinanceDimensionRouteDefinition? DimensionRoute,
        FinanceDimensionCertificationState DimensionCertificationState,
        string SourceModule,
        string OriginModuleCode,
        string SourceDocumentType,
        Guid SourceDocumentId,
        string PostingAction,
        string? SourceDocumentReference,
        string? IdempotencyKey,
        Guid? ExistingJournalEntryId,
        Guid? ReversalOfJournalEntryId,
        string? ReversalReason,
        string? ReversalType,
        string Description,
        DateTime PostingDate,
        string JournalType,
        string AccountingBookCode,
        Guid AccountingBookId,
        string FunctionalCurrencyCode,
        int FunctionalDecimalPlaces,
        FiscalPeriod FiscalPeriod,
        IReadOnlyList<ValidatedPostingLine> Lines,
        decimal TotalDebitAmount,
        decimal TotalCreditAmount,
        bool IsMultiCurrency,
        string? PrimaryCurrency,
        Guid? PrimaryExchangeRateId,
        decimal? PrimaryExchangeRate,
        DateTime? PrimaryExchangeRateDate,
        bool ExchangeRatePolicyOverrideUsed,
        string? ExchangeRateOverrideReason,
        Guid? ExchangeRateOverrideApprovedByUserId,
        DateTime? ExchangeRateOverrideApprovedAt,
        IReadOnlyList<Guid> BudgetReservationIds,
        string? BudgetReservationSourceDocumentType,
        string RequestFingerprintVersion,
        string RequestFingerprint,
        bool AllowsHistoricalMappingException);

    private sealed record ValidatedPostingLine(
        Guid AccountId,
        Guid? SourceDocumentLineId,
        string? Description,
        decimal DebitAmount,
        decimal CreditAmount,
        string TransactionCurrency,
        decimal? TransactionDebitAmount,
        decimal? TransactionCreditAmount,
        decimal? ForeignCurrencyAmount,
        Guid? ExchangeRateId,
        decimal? ExchangeRate,
        string? ExchangeRateSource,
        DateTime? ExchangeRateDate,
        string? SourceReferenceNumber,
        int LineNumber,
        ValidatedDimensionSet? DimensionSet,
        string? SegmentString,
        string? Notes,
        string? TransactionTag);

    private sealed record NormalizedDimensionInput(
        string DimensionCode,
        string? ValueCode,
        string? SourceEntityType,
        Guid? SourceEntityId);

    private sealed record ValidatedDimensionSet(
        Guid Id,
        string CombinationHash,
        string DisplayValue,
        IReadOnlyList<ValidatedDimensionSetItem> Items,
        bool RequiresInsert);

    private sealed record ValidatedDimensionSetItem(
        Guid DefinitionId,
        Guid ValueId,
        string DimensionCode,
        string DimensionName,
        string ValueCode,
        string ValueName,
        int DisplayOrder,
        Guid? RuleId,
        Guid? RuleFamilyId,
        int? RuleVersion,
        string? RuleType,
        DateTime? RuleEffectiveDate,
        DateTime? RuleExpiryDate);

    private sealed record FunctionalCurrencyConfig(string CurrencyCode, bool IsConfigured);

    private sealed record ParallelReplica(
        AccountingBook Book,
        JournalEntry JournalEntry,
        FinancePostingEvent PostingEvent);

    private sealed record ParallelRate(
        ExchangeRate Entity,
        Guid Id,
        decimal Rate,
        DateTime Date,
        string Source);

    private sealed record ExchangeRateSnapshot(
        Guid ExchangeRateId,
        decimal Rate,
        string RateSource,
        DateTime RateDate,
        bool PolicyOverrideUsed);

    private sealed record ExchangeRatePolicy(
        ExchangeRateType RateType,
        ExchangeRateQuoteSide QuoteSide,
        bool IsOverride);
}

internal static class YearEndTenantPeriodAuthority
{
    private const string SnapshotVersion = "TENANT-FISCAL-PERIOD-CLOSE-V2";

    internal static async Task<string> CaptureAsync(
        ApplicationDbContext context,
        Guid tenantId,
        Guid fiscalYearId,
        CancellationToken cancellationToken = default)
    {
        var year = await context.FiscalYears.AsNoTracking().SingleOrDefaultAsync(item =>
            item.TenantId == tenantId && item.Id == fiscalYearId && !item.IsDeleted,
            cancellationToken)
            ?? throw new InvalidOperationException("The tenant fiscal year is unavailable.");
        if (year.IsLocked)
            throw new InvalidOperationException("The tenant fiscal year is locked.");
        if (year.IsClosed)
            throw new InvalidOperationException("The tenant fiscal year is already globally closed.");

        var periods = await context.FiscalPeriods.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.FiscalYearId == fiscalYearId && !item.IsDeleted)
            .OrderBy(item => item.StartDate)
            .ThenBy(item => item.EndDate)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);
        if (periods.Count == 0)
            throw new InvalidOperationException("The fiscal year has no tenant fiscal periods.");

        var expectedStart = year.StartDate.Date;
        foreach (var period in periods)
        {
            if (period.StartDate.Date != expectedStart || period.EndDate.Date < period.StartDate.Date
                || period.EndDate.Date > year.EndDate.Date)
                throw new InvalidOperationException(
                    "Tenant fiscal periods must cover the fiscal year exactly once without gaps or overlaps.");
            expectedStart = period.EndDate.Date.AddDays(1);
            if (!period.IsClosed || period.IsOpen || period.IsLocked
                || !string.Equals(period.PeriodStatus, "Closed", StringComparison.Ordinal)
                || period.ClosedByUserId == null || period.ClosedDate == null)
                throw new InvalidOperationException(
                    $"Fiscal period '{period.PeriodCode}' is not an unlocked, consistently closed tenant period.");
        }
        if (expectedStart != year.EndDate.Date.AddDays(1))
            throw new InvalidOperationException(
                "Tenant fiscal periods must cover the fiscal year exactly once without gaps or overlaps.");

        var periodIds = periods.Select(item => item.Id).ToArray();
        var closeCycles = await context.FinanceCloseCycles.AsNoTracking()
            .Where(item => item.TenantId == tenantId && periodIds.Contains(item.FiscalPeriodId)
                && !item.IsDeleted && item.Status == FinanceCloseStatuses.Closed)
            .ToListAsync(cancellationToken);
        var cycleIds = closeCycles.Select(item => item.Id).ToArray();
        var certifications = await context.FinanceCloseCertifications.AsNoTracking()
            .Where(item => item.TenantId == tenantId && cycleIds.Contains(item.FinanceCloseCycleId)
                && !item.IsDeleted && !item.IsSuperseded)
            .ToListAsync(cancellationToken);

        var snapshots = new List<TenantPeriodCloseEvidence>(periods.Count);
        foreach (var period in periods)
        {
            var cycleRows = closeCycles.Where(item => item.FiscalPeriodId == period.Id).ToArray();
            if (cycleRows.Length != 1)
                throw new InvalidOperationException(
                    $"Fiscal period '{period.PeriodCode}' must retain exactly one current closed Finance close cycle.");
            var cycle = cycleRows[0];
            var certificateRows = certifications.Where(item => item.FinanceCloseCycleId == cycle.Id).ToArray();
            if (certificateRows.Length != 1)
                throw new InvalidOperationException(
                    $"Fiscal period '{period.PeriodCode}' must retain one active Finance close certification.");
            var certificate = certificateRows[0];
            if (cycle.PreparedAt == null || cycle.ClosedAt == null
                || certificate.PreparedByUserId == null || certificate.PreparedAt == null
                || certificate.ReviewedByUserId == null || certificate.ReviewedAt == null
                || certificate.ApprovedByUserId == null || certificate.ApprovedAt == null
                || certificate.PreparedByUserId == certificate.ApprovedByUserId
                || certificate.ReviewedByUserId != certificate.ApprovedByUserId
                || certificate.ReviewedAt != certificate.ApprovedAt
                || cycle.PreparedAt != certificate.PreparedAt
                || cycle.ClosedAt != certificate.ApprovedAt
                || period.ClosedByUserId != certificate.ApprovedByUserId
                || string.IsNullOrWhiteSpace(certificate.PreparerDeclaration)
                || string.IsNullOrWhiteSpace(certificate.ReviewerDeclaration))
                throw new InvalidOperationException(
                    $"Fiscal period '{period.PeriodCode}' lacks consistent independent preparation and approval evidence.");

            snapshots.Add(new TenantPeriodCloseEvidence(
                period.Id,
                period.PeriodCode,
                period.PeriodNumber,
                period.StartDate,
                period.EndDate,
                period.PeriodStatus,
                period.IsOpen,
                period.IsClosed,
                period.IsLocked,
                period.IsGlobalLockSuspended,
                period.ClosedDate.Value,
                period.ClosedByUserId.Value,
                cycle.Id,
                cycle.CycleNumber,
                cycle.TemplateCode,
                cycle.CloseType,
                cycle.TemplateVersion,
                cycle.PreparedAt.Value,
                cycle.ClosedAt.Value,
                certificate.Id,
                certificate.PreparedByUserId.Value,
                certificate.PreparedAt.Value,
                certificate.ReviewedByUserId.Value,
                certificate.ReviewedAt.Value,
                certificate.ApprovedByUserId.Value,
                certificate.ApprovedAt.Value));
        }

        return System.Text.Json.JsonSerializer.Serialize(new TenantFiscalYearCloseAuthority(
            SnapshotVersion,
            tenantId,
            fiscalYearId,
            year.StartDate,
            year.EndDate,
            snapshots));
    }

    private sealed record TenantFiscalYearCloseAuthority(
        string Version,
        Guid TenantId,
        Guid FiscalYearId,
        DateTime FiscalYearStart,
        DateTime FiscalYearEnd,
        IReadOnlyList<TenantPeriodCloseEvidence> Periods);

    private sealed record TenantPeriodCloseEvidence(
        Guid FiscalPeriodId,
        string PeriodCode,
        int PeriodNumber,
        DateTime StartDate,
        DateTime EndDate,
        string PeriodStatus,
        bool IsOpen,
        bool IsClosed,
        bool IsLocked,
        bool IsGlobalLockSuspended,
        DateTime ClosedDate,
        Guid ClosedByUserId,
        Guid FinanceCloseCycleId,
        int FinanceCloseCycleNumber,
        string TemplateCode,
        string CloseType,
        int TemplateVersion,
        DateTime CyclePreparedAt,
        DateTime CycleClosedAt,
        Guid CertificationId,
        Guid PreparedByUserId,
        DateTime PreparedAt,
        Guid ReviewedByUserId,
        DateTime ReviewedAt,
        Guid ApprovedByUserId,
        DateTime ApprovedAt);
}
