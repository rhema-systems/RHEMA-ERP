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
using System.Security.Cryptography;
using System.Text;

namespace ErpSystem.Api.Services.Finance.GL;

public sealed class FinancePostingEngine : IFinancePostingEngine
{
    private const string PostedStatus = "Posted";
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<FinancePostingEngine> _logger;
    private readonly IFinanceAuditService? _financeAuditService;
    private readonly IFinanceBudgetControlService? _budgetControl;
    private readonly IFinanceBudgetCommitmentService? _budgetCommitments;

    public FinancePostingEngine(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ILogger<FinancePostingEngine> logger,
        IFinanceAuditService? financeAuditService = null,
        IFinanceBudgetControlService? budgetControl = null,
        IFinanceBudgetCommitmentService? budgetCommitments = null)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
        _financeAuditService = financeAuditService;
        _budgetControl = budgetControl;
        _budgetCommitments = budgetCommitments;
    }

    public async Task<FinancePostingResultDto> PostAsync(
        FinancePostingRequestDto request,
        CancellationToken cancellationToken = default) =>
        await PostCoreAsync(request, producerContext: null, cancellationToken);

    public async Task<FinancePostingResultDto> PostAsync(
        FinancePostingRequestDto request,
        FinancePostingProducerContext producerContext,
        CancellationToken cancellationToken = default) =>
        await PostCoreAsync(request, producerContext ?? throw new ArgumentNullException(nameof(producerContext)), cancellationToken);

    private async Task<FinancePostingResultDto> PostCoreAsync(
        FinancePostingRequestDto request,
        FinancePostingProducerContext? producerContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var tenantId = _currentUserService.GetRequiredFinanceTenantId();
        var validation = await ValidatePostingRequestAsync(tenantId, request, producerContext, cancellationToken);

        var existingPosting = await FindExistingPostingAsync(tenantId, validation, cancellationToken);
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
            return await ExecutePostingAsync(tenantId, validation, request, cancellationToken);
        }

        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var result = await ExecutePostingAsync(tenantId, validation, request, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch (DbUpdateException ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                _context.ChangeTracker.Clear();

                var racedPosting = await FindExistingPostingAsync(tenantId, validation, cancellationToken);
                if (racedPosting != null && request.ReturnExistingOnDuplicate)
                {
                    await RecordDuplicatePostingAuditAsync(tenantId, validation, racedPosting, cancellationToken);
                    return ToResult(racedPosting, wasDuplicate: true);
                }

                throw new InvalidOperationException(
                    "Finance posting failed. The source document/action may have already been posted by another request.",
                    ex);
            }
        });
    }

    private async Task<FinancePostingResultDto> ExecutePostingAsync(
        Guid tenantId,
        ValidatedPosting validation,
        FinancePostingRequestDto request,
        CancellationToken cancellationToken)
    {
        var duplicateInsideTransaction = await FindExistingPostingAsync(tenantId, validation, cancellationToken);
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

        if (request.BudgetReservationIds.Count > 0)
        {
            if (string.IsNullOrWhiteSpace(request.BudgetReservationSourceDocumentType))
            {
                if (_budgetControl == null)
                    throw new InvalidOperationException("Finance budget control is not configured for this budget-controlled posting.");
                await _budgetControl.ConsumeReservationsAsync(
                    tenantId,
                    validation.SourceDocumentId,
                    request.BudgetReservationIds,
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
                    request.BudgetReservationSourceDocumentType,
                    validation.SourceDocumentId,
                    request.BudgetReservationIds,
                    journalEntry.Id,
                    postingEvent.Id,
                    cancellationToken);
            }
        }

        if (!validation.ExistingJournalEntryId.HasValue)
        {
            _context.JournalEntries.Add(journalEntry);
        }

        // Account.Balance is a read-side snapshot used by existing balance APIs; posted journals remain the accounting source of truth.
        await ApplyAccountBalanceMovementsAsync(
            tenantId,
            journalEntry.Transactions,
            cancellationToken);

        await ApplyAccountCurrencyLinkMovementsAsync(
            tenantId,
            journalEntry.Transactions,
            now,
            postedByUserId,
            cancellationToken);

        _context.FinancePostingEvents.Add(postingEvent);
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
                RoundMoney(existingLine.DebitAmount) != requestLine.DebitAmount ||
                RoundMoney(existingLine.CreditAmount) != requestLine.CreditAmount)
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
        journalEntry.BookClassification = validation.BookClassification;
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
            transaction.BookClassification = validation.BookClassification;
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
            BookClassification = validation.BookClassification,
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
                BookClassification = validation.BookClassification,
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

        if (original.IsReversed || original.ReversalJournalEntryId.HasValue)
        {
            throw new InvalidOperationException("Journal entry has already been reversed.");
        }

        if (original.OriginalJournalEntryId.HasValue)
        {
            throw new InvalidOperationException("Reversal journal entries cannot be reversed from this action.");
        }

        return original;
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
            BookClassification = validation.BookClassification,
            CreatedAt = now,
            CreatedBy = _currentUserService.UserName,
            CreatedById = postedByUserId
        };
    }

    private async Task ApplyAccountBalanceMovementsAsync(
        Guid tenantId,
        IEnumerable<AccountTransaction> transactions,
        CancellationToken cancellationToken)
    {
        var transactionList = transactions
            .Where(t => !t.IsDeleted)
            .OrderBy(t => t.LineNumber)
            .ToList();

        if (transactionList.Count == 0)
        {
            throw new InvalidOperationException("Posting must contain journal transaction lines.");
        }

        var accountIds = transactionList
            .Select(t => t.AccountId)
            .Distinct()
            .ToList();

        var accountTypes = await _context.Accounts
            .Where(a => a.TenantId == tenantId && accountIds.Contains(a.Id) && !a.IsDeleted)
            .Select(a => new { a.Id, a.AccountType })
            .ToDictionaryAsync(a => a.Id, a => a.AccountType, cancellationToken);

        if (accountTypes.Count != accountIds.Count)
        {
            throw new InvalidOperationException("One or more posting accounts were not found for this tenant.");
        }

        var balanceDeltas = transactionList
            .GroupBy(transaction => transaction.AccountId)
            .Select(group => new AccountBalanceDelta(
                group.Key,
                group.Sum(transaction => GetAccountBalanceDelta(accountTypes[transaction.AccountId], transaction))))
            .Where(delta => delta.Amount != 0m)
            .ToArray();

        if (balanceDeltas.Length == 0)
        {
            return;
        }

        if (_context.Database.IsSqlServer())
        {
            await ApplySqlServerAccountBalanceDeltasAsync(tenantId, balanceDeltas, cancellationToken);
            return;
        }

        await ApplyTrackedAccountBalanceDeltasAsync(tenantId, balanceDeltas, cancellationToken);
    }

    private async Task ApplySqlServerAccountBalanceDeltasAsync(
        Guid tenantId,
        IReadOnlyCollection<AccountBalanceDelta> balanceDeltas,
        CancellationToken cancellationToken)
    {
        foreach (var delta in balanceDeltas)
        {
            // SQL Server lock hints serialize concurrent snapshot increments while the posting transaction is active.
            var rows = await _context.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE [Accounts] WITH (UPDLOCK, ROWLOCK)
SET [Balance] = [Balance] + {delta.Amount}
WHERE [Id] = {delta.AccountId}
  AND [TenantId] = {tenantId}
  AND [IsDeleted] = CAST(0 AS bit);", cancellationToken);

            if (rows != 1)
            {
                throw new InvalidOperationException("One or more posting accounts were not found for this tenant.");
            }

            SyncTrackedAccountBalanceSnapshot(tenantId, delta);
        }
    }

    private async Task ApplyTrackedAccountBalanceDeltasAsync(
        Guid tenantId,
        IReadOnlyCollection<AccountBalanceDelta> balanceDeltas,
        CancellationToken cancellationToken)
    {
        var accountIds = balanceDeltas.Select(delta => delta.AccountId).ToArray();
        await _context.Accounts
            .Where(account => account.TenantId == tenantId && accountIds.Contains(account.Id) && !account.IsDeleted)
            .LoadAsync(cancellationToken);

        var deltasByAccountId = balanceDeltas.ToDictionary(delta => delta.AccountId, delta => delta.Amount);
        foreach (var entry in _context.ChangeTracker.Entries<Account>())
        {
            if (entry.Entity.TenantId == tenantId &&
                !entry.Entity.IsDeleted &&
                deltasByAccountId.TryGetValue(entry.Entity.Id, out var delta))
            {
                entry.Entity.Balance += delta;
            }
        }
    }

    private void SyncTrackedAccountBalanceSnapshot(Guid tenantId, AccountBalanceDelta delta)
    {
        // Materialize the tracker query before changing property state below. EF Core's
        // Entries<T>() iterator may run DetectChanges while it is being enumerated, and assigning
        // OriginalValue/IsModified can in turn mutate tracker state. Procurement and other module
        // integrations commonly enter Finance with the posting accounts already tracked, so walking
        // the live iterator here caused "Collection was modified" after SQL Server had applied the
        // atomic balance update. A stable snapshot keeps the raw-SQL balance and the tracked read-side
        // entity synchronized without invalidating EF's enumerator.
        var trackedAccounts = _context.ChangeTracker
            .Entries<Account>()
            .ToArray();

        foreach (var entry in trackedAccounts)
        {
            if (entry.Entity.TenantId != tenantId ||
                entry.Entity.Id != delta.AccountId ||
                entry.Entity.IsDeleted)
            {
                continue;
            }

            var balanceProperty = entry.Property(account => account.Balance);
            balanceProperty.CurrentValue += delta.Amount;
            balanceProperty.OriginalValue = balanceProperty.CurrentValue;
            balanceProperty.IsModified = false;
        }
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

    private sealed record AccountBalanceDelta(Guid AccountId, decimal Amount);

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

            link.ForeignCurrencyBalance = RoundMoney(link.ForeignCurrencyBalance + foreignDelta);
            link.BaseCurrencyEquivalent = RoundMoney(link.BaseCurrencyEquivalent + functionalDelta);
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
        FinancePostingRequestDto request,
        FinancePostingProducerContext? producerContext,
        CancellationToken cancellationToken)
    {
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
        var bookClassification = NormalizeRequired(request.BookClassification, "Book classification", 20);
        var requestedFunctionalCurrency = NormalizeCurrency(request.FunctionalCurrencyCode, "Functional currency");
        var functionalCurrencyConfig = await ResolveTenantFunctionalCurrencyAsync(tenantId, cancellationToken);
        var functionalCurrency = functionalCurrencyConfig.CurrencyCode;
        if (!string.Equals(requestedFunctionalCurrency, functionalCurrency, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Posting functional currency does not match the tenant functional currency.");
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
        var fiscalPeriod = await ResolveFiscalPeriodAsync(tenantId, postingDate, request.FiscalPeriodId, cancellationToken);
        // Year-end closing may target the closed final period, but an explicit period lock is
        // still authoritative and must be lifted through the controlled reopen process first.
        var isYearEndClosePosting = request.AllowPostingToClosedPeriod
            && !fiscalPeriod.IsLocked
            && string.Equals(sourceModule, "GL", StringComparison.OrdinalIgnoreCase)
            && (string.Equals(sourceDocumentType, "YearEndClose", StringComparison.OrdinalIgnoreCase)
                || string.Equals(sourceDocumentType, "YearEndCloseReversal", StringComparison.OrdinalIgnoreCase));

        if ((!fiscalPeriod.IsOpen || fiscalPeriod.IsClosed || fiscalPeriod.IsLocked) && !isYearEndClosePosting)
        {
            await RecordPostingBlockedByPeriodAuditAsync(tenantId, request, fiscalPeriod, postingDate, cancellationToken);
            throw new InvalidOperationException("Posting period is not open.");
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

            var debit = RoundMoney(line.DebitAmount);
            var credit = RoundMoney(line.CreditAmount);
            if (debit < 0 || credit < 0)
            {
                throw new InvalidOperationException("Posting amounts cannot be negative.");
            }

            if ((debit > 0 && credit > 0) || (debit == 0 && credit == 0))
            {
                throw new InvalidOperationException("Each posting line must contain either a debit or a credit amount.");
            }

            var lineCurrency = NormalizeCurrency(line.TransactionCurrency, "Transaction currency", functionalCurrency);
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

                if ((transactionDebit.GetValueOrDefault() > 0 && transactionCredit.GetValueOrDefault() > 0)
                    || (transactionDebit.GetValueOrDefault() == 0 && transactionCredit.GetValueOrDefault() == 0))
                {
                    throw new InvalidOperationException("Foreign-currency posting lines require either a transaction-currency debit or credit amount.");
                }

                var expectedFunctional = RoundMoney((transactionDebit.GetValueOrDefault() > 0
                    ? transactionDebit.GetValueOrDefault()
                    : transactionCredit.GetValueOrDefault()) * exchangeRate.Value);
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
                foreignAmount = null;
                exchangeRateId = null;
                exchangeRate = null;
                exchangeRateSource = null;
                exchangeRateDate = null;
            }

            var dimensionSet = await ResolveDimensionSetAsync(
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

        var totalDebit = RoundMoney(normalizedLines.Sum(l => l.DebitAmount));
        var totalCredit = RoundMoney(normalizedLines.Sum(l => l.CreditAmount));
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
            throw new InvalidOperationException("One or more posting accounts were not found for this tenant.");
        }

        var inactiveAccounts = accounts.Values
            .Where(a => a.Status != AccountStatus.Active)
            .Select(a => a.AccountNumber)
            .ToList();

        if (inactiveAccounts.Count > 0)
        {
            throw new InvalidOperationException($"Cannot post to inactive GL account(s): {string.Join(", ", inactiveAccounts)}.");
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
            bookClassification,
            functionalCurrency,
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
            request.ExchangeRateOverrideApprovedAt);
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

    private async Task EnsureOriginModuleCanPostAsync(
        Guid tenantId,
        FiscalPeriod fiscalPeriod,
        string originModuleCode,
        FinancePostingRequestDto request,
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
        FinancePostingRequestDto request,
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
        bool requireOverrideApproval,
        FinancePostingRequestDto request,
        CancellationToken cancellationToken)
    {
        ExchangeRate? rate;
        var policyOverrideUsed = policy.IsOverride;
        var preservesHistoricalSourceMeasurement =
            request.PreserveHistoricalExchangeRateSnapshot &&
            string.Equals(request.SourceModule, "AP", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(request.SourceDocumentType, "SupplierDebitNote", StringComparison.OrdinalIgnoreCase) &&
            exchangeRateId.HasValue &&
            suppliedRate.HasValue;
        if (request.PreserveHistoricalExchangeRateSnapshot && !preservesHistoricalSourceMeasurement)
            throw new InvalidOperationException(
                "Historical exchange-rate preservation is restricted to an AP supplier debit note with explicit source-rate evidence.");
        if (preservesHistoricalSourceMeasurement)
        {
            // A supplier debit note corrects the approved source invoice at its immutable rate;
            // this is not a user-entered current-period FX override.
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

        if (rate.RateType != policy.RateType || rate.QuoteSide != policy.QuoteSide)
        {
            // A reversal must reproduce the original immutable rate snapshot even
            // when the tenant's current policy has since changed.
            if (!request.ReversalOfJournalEntryId.HasValue && !preservesHistoricalSourceMeasurement)
            {
                EnsureExchangeRateOverrideApproval(request, requireOverrideApproval);
                policyOverrideUsed = true;
            }
        }

        if (suppliedRate.HasValue && RoundRate(suppliedRate.Value) != RoundRate(rate.Rate))
        {
            await RecordForeignCurrencyPostingBlockedAuditAsync(
                tenantId,
                request,
                transactionCurrency,
                "Supplied exchange-rate snapshot does not match the tenant exchange-rate record.",
                cancellationToken);
            throw new InvalidOperationException("Supplied exchange-rate snapshot does not match the tenant exchange-rate record.");
        }

        return new ExchangeRateSnapshot(rate.Id, rate.Rate, rate.RateSource, rate.EffectiveDate.Date, policyOverrideUsed);
    }

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
        FinancePostingRequestDto request,
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
        CancellationToken cancellationToken)
    {
        return await _context.FinancePostingEvents
            .Include(e => e.JournalEntry)
            .FirstOrDefaultAsync(
                e => e.TenantId == tenantId
                    && !e.IsDeleted
                    && (((e.SourceDocumentType == validation.SourceDocumentType
                            && e.SourceDocumentId == validation.SourceDocumentId
                            && e.PostingAction == validation.PostingAction)
                        || (e.SourceModule == validation.SourceModule
                            && e.SourceDocumentType == validation.SourceDocumentType
                            && e.SourceDocumentId == validation.SourceDocumentId
                            && e.PostingAction == validation.PostingAction))
                        || (!string.IsNullOrWhiteSpace(validation.IdempotencyKey)
                            && e.IdempotencyKey == validation.IdempotencyKey)),
                cancellationToken);
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
                postingEvent.BookClassification
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
                postingEvent.PostingDate
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
        FinancePostingRequestDto request,
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

    private async Task RecordFutureDatedPostingBlockedAuditAsync(
        Guid tenantId,
        FinancePostingRequestDto request,
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
        FinancePostingRequestDto request,
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
        FinancePostingRequestDto request,
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

    private static decimal RoundMoney(decimal amount)
    {
        return decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
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
        FinancePostingRequestDto request,
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
            return ToValidatedDimensionSet(existing, requiresInsert: false);
        }

        return new ValidatedDimensionSet(dimensionSetId, combinationHash, displayValue, resolvedItems, true);
    }

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
        FinancePostingRequestDto request,
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
        string BookClassification,
        string FunctionalCurrencyCode,
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
        DateTime? ExchangeRateOverrideApprovedAt);

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
