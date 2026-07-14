using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.GL;

public sealed class FinancePostingEngine : IFinancePostingEngine
{
    private const string PostedStatus = "Posted";
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<FinancePostingEngine> _logger;
    private readonly IFinanceAuditService? _financeAuditService;

    public FinancePostingEngine(
        ApplicationDbContext context,
        ICurrentUserService currentUserService,
        ILogger<FinancePostingEngine> logger,
        IFinanceAuditService? financeAuditService = null)
    {
        _context = context;
        _currentUserService = currentUserService;
        _logger = logger;
        _financeAuditService = financeAuditService;
    }

    public async Task<FinancePostingResultDto> PostAsync(
        FinancePostingRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var tenantId = _currentUserService.GetRequiredFinanceTenantId();
        var validation = await ValidatePostingRequestAsync(tenantId, request, cancellationToken);

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
        var journalEntry = validation.ExistingJournalEntryId.HasValue
            ? await ApplyExistingJournalPostingAsync(tenantId, validation, now, postedByUserId, cancellationToken)
            : await CreatePostedJournalEntryAsync(tenantId, validation, now, postedByUserId, cancellationToken);

        var postingEvent = BuildPostingEvent(tenantId, validation, journalEntry.Id, now, postedByUserId);
        await MarkExchangeRatesUsedAsync(tenantId, validation, postingEvent.Id, now, cancellationToken);

        if (!validation.ExistingJournalEntryId.HasValue)
        {
            _context.JournalEntries.Add(journalEntry);
        }

        // Account.Balance is a read-side snapshot used by existing balance APIs; posted journals remain the accounting source of truth.
        await ApplyAccountBalanceMovementsAsync(
            tenantId,
            journalEntry.Transactions,
            cancellationToken);

        _context.FinancePostingEvents.Add(postingEvent);
        await _context.SaveChangesAsync(cancellationToken);
        await RecordPostingEventCreatedAuditAsync(tenantId, validation, postingEvent, journalEntry.Id, cancellationToken);
        await RecordCurrencySnapshotAuditAsync(tenantId, validation, postingEvent, journalEntry.Id, cancellationToken);

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
                SegmentString = t.SegmentString,
                Notes = reason,
                TransactionTag = "Reversal"
            })
            .ToList();

        return new FinanceReversalPlanDto
        {
            IsDefined = true,
            OriginalPostingEventId = postingEvent.Id,
            OriginalJournalEntryId = postingEvent.JournalEntryId!.Value,
            PostingAction = "Reverse",
            ReversalDate = (reversalDate ?? DateTime.UtcNow).Date,
            Reason = reason.Trim(),
            ReversalLines = lines
        };
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

        original.PostingStatus = "Reversed";
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
            originalLine.PostingStatus = "Reversed";
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

        if (_context.Database.IsRelational())
        {
            await ApplyRelationalAccountBalanceDeltasAsync(tenantId, balanceDeltas, cancellationToken);
            return;
        }

        await ApplyTrackedAccountBalanceDeltasAsync(tenantId, balanceDeltas, cancellationToken);
    }

    private async Task ApplyRelationalAccountBalanceDeltasAsync(
        Guid tenantId,
        IReadOnlyCollection<AccountBalanceDelta> balanceDeltas,
        CancellationToken cancellationToken)
    {
        foreach (var delta in balanceDeltas)
        {
            // UPDLOCK serializes concurrent snapshot increments for the same account while the surrounding posting transaction is active.
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
        foreach (var entry in _context.ChangeTracker.Entries<Account>())
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

    private sealed record AccountBalanceDelta(Guid AccountId, decimal Amount);

    private async Task<ValidatedPosting> ValidatePostingRequestAsync(
        Guid tenantId,
        FinancePostingRequestDto request,
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
        var sourceDocumentType = NormalizeRequired(request.SourceDocumentType, "Source document type", 100);
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
        if (!fiscalPeriod.IsOpen || fiscalPeriod.IsClosed || fiscalPeriod.IsLocked)
        {
            // Year-end closing entries are the one legitimate post into a closed (not locked)
            // period: the close itself requires every period closed first. The exception is
            // limited to the GL year-end source types so it cannot become a general bypass.
            var isYearEndClosePosting = request.AllowPostingToClosedPeriod
                && !fiscalPeriod.IsLocked
                && string.Equals(request.SourceModule, "GL", StringComparison.OrdinalIgnoreCase)
                && (string.Equals(request.SourceDocumentType, "YearEndClose", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(request.SourceDocumentType, "YearEndCloseReversal", StringComparison.OrdinalIgnoreCase));

            if (!isYearEndClosePosting)
            {
                await RecordPostingBlockedByPeriodAuditAsync(tenantId, request, fiscalPeriod, postingDate, cancellationToken);
                throw new InvalidOperationException("Posting period is not open.");
            }
        }

        if (postingDate < fiscalPeriod.StartDate.Date || postingDate > fiscalPeriod.EndDate.Date)
        {
            throw new InvalidOperationException("Posting date does not fall inside the fiscal period.");
        }

        var requestedLines = request.Lines?.ToList() ?? new List<FinancePostingLineDto>();
        if (requestedLines.Count == 0)
        {
            throw new InvalidOperationException("At least two posting lines are required.");
        }

        var lineNumber = 1;
        var normalizedLines = new List<ValidatedPostingLine>();
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

                var rateSnapshot = await ResolveExchangeRateSnapshotAsync(
                    tenantId,
                    functionalCurrency,
                    lineCurrency,
                    postingDate,
                    line.ExchangeRateId,
                    line.ExchangeRate,
                    request,
                    cancellationToken);

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

            normalizedLines.Add(new ValidatedPostingLine(
                line.AccountId,
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
        }

        var distinctCurrencies = normalizedLines
            .Select(l => l.TransactionCurrency)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var primaryCurrency = distinctCurrencies.Count == 1 ? distinctCurrencies[0] : null;
        var primaryExchangeRateLine = normalizedLines.FirstOrDefault(l => l.ExchangeRateId.HasValue);

        return new ValidatedPosting(
            sourceModule,
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
            primaryExchangeRateLine?.ExchangeRateDate);
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

    private async Task<ExchangeRateSnapshot> ResolveExchangeRateSnapshotAsync(
        Guid tenantId,
        string functionalCurrency,
        string transactionCurrency,
        DateTime postingDate,
        Guid? exchangeRateId,
        decimal? suppliedRate,
        FinancePostingRequestDto request,
        CancellationToken cancellationToken)
    {
        ExchangeRate? rate;
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
                    && r.RateType == ExchangeRateType.Daily
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

        if (rate.EffectiveDate.Date > postingDate.Date || (rate.EndDate.HasValue && rate.EndDate.Value.Date < postingDate.Date))
        {
            await RecordForeignCurrencyPostingBlockedAuditAsync(
                tenantId,
                request,
                transactionCurrency,
                "Exchange rate is not effective for the posting date.",
                cancellationToken);
            throw new InvalidOperationException("Exchange rate is not effective for the posting date.");
        }

        if (!rate.IsActive || rate.ApprovalStatus is not (RateApprovalStatus.Approved or RateApprovalStatus.AutoApproved))
        {
            await RecordForeignCurrencyPostingBlockedAuditAsync(
                tenantId,
                request,
                transactionCurrency,
                "Exchange rate is inactive or not approved.",
                cancellationToken);
            throw new InvalidOperationException("Exchange rate must be active and approved before posting.");
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

        return new ExchangeRateSnapshot(rate.Id, rate.Rate, rate.RateSource, rate.EffectiveDate.Date);
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

    private sealed record ValidatedPosting(
        string SourceModule,
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
        DateTime? PrimaryExchangeRateDate);

    private sealed record ValidatedPostingLine(
        Guid AccountId,
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
        string? SegmentString,
        string? Notes,
        string? TransactionTag);

    private sealed record FunctionalCurrencyConfig(string CurrencyCode, bool IsConfigured);

    private sealed record ExchangeRateSnapshot(
        Guid ExchangeRateId,
        decimal Rate,
        string RateSource,
        DateTime RateDate);
}
