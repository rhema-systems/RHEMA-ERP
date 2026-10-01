using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ErpSystem.Api.Services.Finance;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Data.Seeders;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.Settings;

public sealed class AccountingBookInitializationService : IAccountingBookInitializationService
{
    public const string WorkflowEntityType = "AccountingBookInitialization";
    private static readonly HashSet<string> PseudoBookCodes = new(StringComparer.Ordinal)
        { "ALL", "ALL_ACTIVE_BOOKS", "ALL_CLASSIFIED_BOOKS", "ALLCLASSIFIEDBOOKS" };
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IWorkflowService _workflow;
    private readonly IFinanceAuditService _audit;
    private readonly IBookBalanceReadModelService? _bookBalances;
    private Guid TenantId => _currentUser.GetRequiredFinanceTenantId();

    public AccountingBookInitializationService(ApplicationDbContext db, ICurrentUserService currentUser, IWorkflowService workflow,
        IFinanceAuditService audit, IBookBalanceReadModelService? bookBalances = null)
        => (_db, _currentUser, _workflow, _audit, _bookBalances) = (db, currentUser, workflow, audit, bookBalances);

    public async Task<int> ApplyGovernedParallelOpeningAsync(Guid accountingBookId,
        CancellationToken cancellationToken = default)
    {
        var book = await _db.AccountingBooks.SingleOrDefaultAsync(item => item.Id == accountingBookId
            && item.TenantId == TenantId && !item.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("Accounting book was not found.");
        if (book.BookType != AccountingBookType.ParallelFull
            || book.ParallelOpeningMode != ParallelBookOpeningMode.GovernedOpeningConversion)
            return 0;
        if (book.LifecycleStatus != AccountingBookLifecycleStatus.Initializing)
            throw new InvalidOperationException("PARALLEL_OPENING_STATE_INVALID: Governed opening conversion may be applied only while the Parallel book is Initializing.");
        if (_bookBalances == null)
            throw new InvalidOperationException("PARALLEL_OPENING_BALANCE_PROJECTION_UNAVAILABLE: Parallel opening balance projection is unavailable.");

        var initialization = await Query().Include(item => item.Lines)
            .Where(item => item.AccountingBookId == book.Id
                && item.InitializationStatus == AccountingBookInitializationStatus.Approved)
            .OrderByDescending(item => item.Version).FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("PARALLEL_OPENING_EVIDENCE_REQUIRED: Approved governed opening evidence is required before activation.");
        if (initialization.Mode != AccountingBookInitializationMode.BaseBookCopyAtCutoff)
            throw new InvalidOperationException("PARALLEL_OPENING_EVIDENCE_INVALID: Governed opening conversion requires base-book copy evidence.");
        ValidateParallelCutoffContinuity(book, initialization.CutoffDate);
        await EnsureEvidenceUnchangedAsync(initialization, cancellationToken);

        if (await _db.FinancePostingEvents.AsNoTracking().AnyAsync(item => item.TenantId == TenantId
            && item.AccountingBookId == book.Id && item.SourceDocumentType == WorkflowEntityType
            && item.SourceDocumentId == initialization.Id && item.PostingAction == "GovernedOpeningConversion",
            cancellationToken))
            return 0;

        var sourceBook = await _db.AccountingBooks.AsNoTracking().SingleOrDefaultAsync(item =>
            item.Id == initialization.SourceAccountingBookId && item.TenantId == TenantId && !item.IsDeleted,
            cancellationToken) ?? throw new InvalidOperationException("PARALLEL_OPENING_SOURCE_INVALID: The governed Primary source book is unavailable.");
        var now = DateTime.UtcNow;
        var actorId = Guid.TryParse(_currentUser.UserId, out var parsedActorId) ? parsedActorId : (Guid?)null;
        var lines = initialization.Lines.Where(item => !item.IsDeleted && (item.OpeningDebit != 0m || item.OpeningCredit != 0m))
            .OrderBy(item => item.AccountId).Select((line, index) => new AccountTransaction
            {
                Id = Guid.NewGuid(), TenantId = TenantId, AccountId = line.AccountId,
                TransactionDate = initialization.CutoffDate.Date,
                Description = $"{book.Code} governed opening conversion",
                DebitAmount = line.OpeningDebit, CreditAmount = line.OpeningCredit,
                FunctionalCurrencyCode = book.FunctionalCurrencyCode!,
                TransactionCurrency = sourceBook.FunctionalCurrencyCode,
                TransactionDebitAmount = line.BaseBookSignedBalance > 0m ? line.BaseBookSignedBalance : 0m,
                TransactionCreditAmount = line.BaseBookSignedBalance < 0m ? Math.Abs(line.BaseBookSignedBalance) : 0m,
                ForeignCurrencyAmount = Math.Abs(line.BaseBookSignedBalance),
                ExchangeRateId = line.TranslationExchangeRateId, ExchangeRate = line.TranslationRate,
                ExchangeRateSource = line.TranslationRateSource, ExchangeRateDate = line.TranslationRateDate,
                SourceModule = "FIN", SourceDocumentId = initialization.Id,
                SourceDocumentType = WorkflowEntityType, SourceReferenceNumber = initialization.IdempotencyKey,
                BookClassification = book.Code, AccountingBookId = book.Id,
                FiscalPeriodId = initialization.CutoffFiscalPeriodId, PostedDate = now,
                PostingStatus = "Posted", LineNumber = index + 1,
                TransactionTag = "Parallel opening conversion", CreatedAt = now,
                CreatedBy = _currentUser.UserName, CreatedById = actorId
            }).ToList();
        if (lines.Count == 0) return 0;
        var debit = RoundMoney(lines.Sum(item => item.DebitAmount));
        var credit = RoundMoney(lines.Sum(item => item.CreditAmount));
        if (debit != credit)
            throw new InvalidOperationException("PARALLEL_OPENING_UNBALANCED: Approved governed opening evidence is not balanced.");

        var distinctRates = initialization.Lines.Where(item => item.TranslationExchangeRateId.HasValue)
            .Select(item => item.TranslationExchangeRateId!.Value).Distinct().ToArray();
        var singleRate = distinctRates.Length == 1
            ? await _db.ExchangeRates.SingleOrDefaultAsync(item => item.Id == distinctRates[0]
                && item.TenantId == TenantId && !item.IsDeleted, cancellationToken)
            : null;
        var journal = new JournalEntry
        {
            Id = Guid.NewGuid(), TenantId = TenantId,
            JournalEntryNumber = $"FXOPEN-{initialization.CutoffDate:yyyyMMdd}-{initialization.Id:N}"[..47],
            JournalType = "Opening Balance", EntryDate = initialization.CutoffDate.Date,
            Description = $"{book.Code} governed opening conversion from {sourceBook.Code}",
            ReferenceNumber = initialization.IdempotencyKey, SourceModule = "FIN",
            OriginModuleCode = "FIN", SourceDocumentId = initialization.Id,
            SourceDocumentType = WorkflowEntityType, TotalDebitAmount = debit,
            TotalCreditAmount = credit, BalanceDifference = debit - credit, IsBalanced = true,
            IsMultiCurrency = true, PrimaryCurrency = book.FunctionalCurrencyCode,
            BookClassification = book.Code, AccountingBookId = book.Id,
            FiscalPeriodId = initialization.CutoffFiscalPeriodId, PostingDate = now,
            PostedByUserId = actorId, PostingStatus = "Posted",
            ApprovalStatus = "Approved initialization evidence",
            ReplicationExchangeRateId = singleRate?.Id, ReplicationExchangeRate = singleRate?.Rate,
            ReplicationRateDate = singleRate?.EffectiveDate.Date, ReplicationRateSource = singleRate?.RateSource,
            CreatedAt = now, CreatedBy = _currentUser.UserName, CreatedById = actorId,
            Transactions = lines
        };
        foreach (var line in lines) line.JournalEntryId = journal.Id;
        var postingEvent = new FinancePostingEvent
        {
            Id = Guid.NewGuid(), TenantId = TenantId, SourceModule = "FIN", OriginModuleCode = "FIN",
            SourceDocumentType = WorkflowEntityType, SourceDocumentId = initialization.Id,
            PostingAction = "GovernedOpeningConversion", SourceDocumentReference = initialization.IdempotencyKey,
            IdempotencyKey = $"parallel-opening:{book.Id:N}:{initialization.Id:N}",
            RequestFingerprintVersion = "FINPOST-PARALLEL-OPENING-V1",
            RequestFingerprint = initialization.ReconciliationFingerprint,
            JournalEntryId = journal.Id, PostingStatus = "Posted", PostingDate = initialization.CutoffDate.Date,
            RequestedAt = now, PostedAt = now, RequestedByUserId = actorId,
            TotalDebitAmount = debit, TotalCreditAmount = credit,
            FunctionalCurrencyCode = book.FunctionalCurrencyCode!, HasForeignCurrencyLines = true,
            PrimaryTransactionCurrencyCode = sourceBook.FunctionalCurrencyCode,
            PrimaryExchangeRateId = singleRate?.Id, PrimaryExchangeRate = singleRate?.Rate,
            PrimaryExchangeRateDate = singleRate?.EffectiveDate.Date,
            BookClassification = book.Code, AccountingBookId = book.Id,
            CreatedAt = now, CreatedBy = _currentUser.UserName, CreatedById = actorId
        };
        _db.JournalEntries.Add(journal);
        _db.FinancePostingEvents.Add(postingEvent);
        await _bookBalances.ApplyPostingAsync(TenantId, book.Id, book.Code,
            initialization.CutoffFiscalPeriodId, book.FunctionalCurrencyCode!, lines, now, actorId, cancellationToken);
        if (distinctRates.Length > 0)
        {
            var rates = await _db.ExchangeRates.Where(item => distinctRates.Contains(item.Id)
                && item.TenantId == TenantId && !item.IsDeleted).ToListAsync(cancellationToken);
            foreach (var rate in rates)
            {
                rate.HasBeenUsedInTransactions = true;
                rate.TransactionCount += lines.Count(item => item.ExchangeRateId == rate.Id);
                rate.FirstUsedDate ??= now;
                rate.LastUsedDate = now;
            }
        }
        await _db.SaveChangesAsync(cancellationToken);
        return 1;
    }

    public async Task<int> ReplayHistoricalParallelTransactionsAsync(Guid accountingBookId,
        CancellationToken cancellationToken = default)
    {
        var book = await _db.AccountingBooks.SingleOrDefaultAsync(item => item.Id == accountingBookId
            && item.TenantId == TenantId && !item.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("Accounting book was not found.");
        if (book.BookType != AccountingBookType.ParallelFull
            || book.ParallelOpeningMode != ParallelBookOpeningMode.HistoricalReplay
            || !book.BaseAccountingBookId.HasValue || !book.ReplicationStartDate.HasValue)
            return 0;
        if (book.LifecycleStatus != AccountingBookLifecycleStatus.Initializing)
            throw new InvalidOperationException("PARALLEL_REPLAY_STATE_INVALID: Historical replay may run only while the Parallel book is Initializing.");
        if (_bookBalances == null)
            throw new InvalidOperationException("PARALLEL_REPLAY_BALANCE_PROJECTION_UNAVAILABLE: Historical replay balance projection is unavailable.");

        var sourceBook = await _db.AccountingBooks.AsNoTracking().SingleOrDefaultAsync(item =>
            item.Id == book.BaseAccountingBookId.Value && item.TenantId == TenantId
            && item.BookType == AccountingBookType.PrimaryFull && item.IsDefault && !item.IsDeleted,
            cancellationToken) ?? throw new InvalidOperationException("PARALLEL_REPLAY_SOURCE_INVALID: The governed Primary source book is unavailable.");
        if (string.IsNullOrWhiteSpace(sourceBook.FunctionalCurrencyCode)
            || string.IsNullOrWhiteSpace(book.FunctionalCurrencyCode)
            || string.Equals(sourceBook.FunctionalCurrencyCode, book.FunctionalCurrencyCode, StringComparison.Ordinal))
            throw new InvalidOperationException("PARALLEL_REPLAY_CURRENCY_INVALID: Historical replay requires different canonical source and target currencies.");

        var initialization = await Query().Include(item => item.Lines)
            .Where(item => item.AccountingBookId == book.Id && item.InitializationStatus == AccountingBookInitializationStatus.Approved)
            .OrderByDescending(item => item.Version).FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("PARALLEL_REPLAY_EVIDENCE_REQUIRED: Approved initialization evidence is required before replay.");
        if (initialization.CutoffDate.Date >= book.ReplicationStartDate.Value.Date)
            throw new InvalidOperationException("PARALLEL_REPLAY_CUTOFF_INVALID: Replay cutoff must be earlier than the replication start date.");

        var alreadyReplayed = await _db.JournalEntries.AsNoTracking().Where(item => item.TenantId == TenantId
                && item.AccountingBookId == book.Id && item.ReplicatedFromJournalEntryId != null && !item.IsDeleted)
            .Select(item => item.ReplicatedFromJournalEntryId!.Value).ToListAsync(cancellationToken);
        var sourceJournals = await _db.JournalEntries.AsNoTracking().Include(item => item.Transactions)
            .Where(item => item.TenantId == TenantId && item.AccountingBookId == sourceBook.Id
                && item.PostingStatus == "Posted" && item.EntryDate.Date <= initialization.CutoffDate.Date
                && !item.IsDeleted && !alreadyReplayed.Contains(item.Id))
            .OrderBy(item => item.EntryDate).ThenBy(item => item.CreatedAt).ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);
        if (sourceJournals.Count == 0) return 0;

        var accountIds = sourceJournals.SelectMany(item => item.Transactions).Where(item => !item.IsDeleted)
            .Select(item => item.AccountId).Distinct().ToArray();
        var mappedIds = await _db.AccountAccountingBooks.AsNoTracking().Where(item => item.TenantId == TenantId
                && item.AccountingBookId == book.Id && accountIds.Contains(item.AccountId) && !item.IsDeleted)
            .Select(item => item.AccountId).Distinct().ToListAsync(cancellationToken);
        var missingMappings = accountIds.Except(mappedIds).ToArray();
        if (missingMappings.Length > 0)
            throw new InvalidOperationException($"PARALLEL_REPLAY_ACCOUNT_MAPPING_REQUIRED: Parallel book {book.Code} is missing {missingMappings.Length} inherited account mapping(s).");

        var rates = await _db.ExchangeRates.Where(item => item.TenantId == TenantId
                && item.BaseCurrencyCode == sourceBook.FunctionalCurrencyCode
                && item.TargetCurrencyCode == book.FunctionalCurrencyCode
                && (item.ApprovalStatus == RateApprovalStatus.Approved || item.ApprovalStatus == RateApprovalStatus.AutoApproved)
                && item.EffectiveDate.Date <= initialization.CutoffDate.Date && !item.IsDeleted)
            .OrderBy(item => item.EffectiveDate).ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var actorId = Guid.TryParse(_currentUser.UserId, out var parsedActorId) ? parsedActorId : (Guid?)null;
        var prepared = new List<(JournalEntry Journal, FinancePostingEvent Event, ExchangeRate Rate)>();
        foreach (var source in sourceJournals)
        {
            var rate = rates.LastOrDefault(item => item.EffectiveDate.Date <= source.EntryDate.Date
                && (!item.EndDate.HasValue || item.EndDate.Value.Date >= source.EntryDate.Date))
                ?? throw new InvalidOperationException(
                    $"PARALLEL_REPLAY_RATE_REQUIRED: {sourceBook.FunctionalCurrencyCode}/{book.FunctionalCurrencyCode} approved rate is missing for accounting date {source.EntryDate:dd/MM/yyyy}.");
            if (rate.Rate <= 0m)
                throw new InvalidOperationException($"PARALLEL_REPLAY_RATE_INVALID: Exchange rate {rate.Id} has no positive source-to-target multiplier.");
            var multiplier = rate.Rate;
            var lines = source.Transactions.Where(item => !item.IsDeleted).OrderBy(item => item.LineNumber).Select(line =>
                new AccountTransaction
                {
                    Id = Guid.NewGuid(), TenantId = TenantId, AccountId = line.AccountId,
                    TransactionDate = source.EntryDate.Date, Description = line.Description,
                    DebitAmount = RoundMoney(line.DebitAmount * multiplier), CreditAmount = RoundMoney(line.CreditAmount * multiplier),
                    FunctionalCurrencyCode = book.FunctionalCurrencyCode!, TransactionCurrency = sourceBook.FunctionalCurrencyCode,
                    TransactionDebitAmount = line.DebitAmount, TransactionCreditAmount = line.CreditAmount,
                    ForeignCurrencyAmount = line.DebitAmount > 0m ? line.DebitAmount : line.CreditAmount,
                    ExchangeRateId = rate.Id, ExchangeRate = multiplier, ExchangeRateSource = rate.RateSource,
                    ExchangeRateDate = rate.EffectiveDate.Date, FinanceDimensionSetId = line.FinanceDimensionSetId,
                    FinanceDimensionSnapshotId = line.FinanceDimensionSnapshotId,
                    SourceModule = line.SourceModule, SourceDocumentId = line.SourceDocumentId,
                    SourceDocumentLineId = line.SourceDocumentLineId, SourceDocumentType = line.SourceDocumentType,
                    SourceReferenceNumber = line.SourceReferenceNumber, BookClassification = book.Code,
                    AccountingBookId = book.Id, FiscalPeriodId = line.FiscalPeriodId, PostedDate = now,
                    PostingStatus = "Posted", SegmentString = line.SegmentString, LineNumber = line.LineNumber,
                    Notes = line.Notes, TransactionTag = "Parallel historical replay",
                    CreatedAt = now, CreatedBy = _currentUser.UserName, CreatedById = actorId
                }).ToList();
            AppendRoundingLine(book, source, lines, rate, now, actorId);
            var debit = RoundMoney(lines.Sum(item => item.DebitAmount));
            var credit = RoundMoney(lines.Sum(item => item.CreditAmount));
            var journal = new JournalEntry
            {
                Id = Guid.NewGuid(), TenantId = TenantId,
                JournalEntryNumber = $"FXHIST-{source.EntryDate:yyyyMMdd}-{source.Id:N}"[..47],
                JournalType = "System Generated", EntryDate = source.EntryDate.Date,
                Description = $"{source.Description} — {book.Code} historical translated replica",
                ReferenceNumber = source.ReferenceNumber, SourceModule = source.SourceModule,
                OriginModuleCode = source.OriginModuleCode, SourceDocumentId = source.SourceDocumentId,
                SourceDocumentType = source.SourceDocumentType, TotalDebitAmount = debit,
                TotalCreditAmount = credit, BalanceDifference = debit - credit, IsBalanced = debit == credit,
                IsMultiCurrency = true, PrimaryCurrency = book.FunctionalCurrencyCode,
                BookClassification = book.Code, AccountingBookId = book.Id, FiscalPeriodId = source.FiscalPeriodId,
                PostingDate = now, PostedByUserId = actorId, PostingStatus = "Posted",
                ApprovalStatus = "System historical replica", ReplicatedFromJournalEntryId = source.Id,
                ReplicationExchangeRateId = rate.Id, ReplicationExchangeRate = multiplier,
                ReplicationRateDate = rate.EffectiveDate.Date, ReplicationRateSource = rate.RateSource,
                CreatedAt = now, CreatedBy = _currentUser.UserName, CreatedById = actorId, Transactions = lines
            };
            foreach (var line in lines) line.JournalEntryId = journal.Id;
            var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                $"PARALLEL-HISTORICAL-V1|{source.Id:N}|{book.Id:N}|{rate.Id:N}|{multiplier.ToString(CultureInfo.InvariantCulture)}")));
            var postingEvent = new FinancePostingEvent
            {
                Id = Guid.NewGuid(), TenantId = TenantId, SourceModule = source.SourceModule ?? "FIN",
                OriginModuleCode = source.OriginModuleCode, SourceDocumentType = source.SourceDocumentType ?? "JournalEntry",
                SourceDocumentId = source.SourceDocumentId ?? source.Id, PostingAction = "HistoricalReplay",
                SourceDocumentReference = source.ReferenceNumber ?? source.JournalEntryNumber,
                IdempotencyKey = $"parallel-replay:{book.Id:N}:{source.Id:N}",
                RequestFingerprintVersion = "FINPOST-PARALLEL-HISTORICAL-V1", RequestFingerprint = fingerprint,
                JournalEntryId = journal.Id, PostingStatus = "Posted", PostingDate = source.EntryDate.Date,
                RequestedAt = now, PostedAt = now, RequestedByUserId = actorId,
                TotalDebitAmount = debit, TotalCreditAmount = credit,
                FunctionalCurrencyCode = book.FunctionalCurrencyCode!, HasForeignCurrencyLines = true,
                PrimaryTransactionCurrencyCode = sourceBook.FunctionalCurrencyCode,
                PrimaryExchangeRateId = rate.Id, PrimaryExchangeRate = multiplier,
                PrimaryExchangeRateDate = rate.EffectiveDate.Date, BookClassification = book.Code,
                AccountingBookId = book.Id, CreatedAt = now, CreatedBy = _currentUser.UserName, CreatedById = actorId
            };
            prepared.Add((journal, postingEvent, rate));
        }

        foreach (var item in prepared)
        {
            _db.JournalEntries.Add(item.Journal);
            _db.FinancePostingEvents.Add(item.Event);
            await _bookBalances.ApplyPostingAsync(TenantId, book.Id, book.Code, item.Journal.FiscalPeriodId,
                book.FunctionalCurrencyCode!, item.Journal.Transactions.ToArray(), now, actorId, cancellationToken);
            item.Rate.HasBeenUsedInTransactions = true;
            item.Rate.TransactionCount += item.Journal.Transactions.Count;
            item.Rate.FirstUsedDate ??= now;
            item.Rate.LastUsedDate = now;
        }
        await _db.SaveChangesAsync(cancellationToken);
        return prepared.Count;
    }

    private static void AppendRoundingLine(AccountingBook book, JournalEntry source, List<AccountTransaction> lines,
        ExchangeRate rate, DateTime now, Guid? actorId)
    {
        var residual = RoundMoney(lines.Sum(item => item.DebitAmount) - lines.Sum(item => item.CreditAmount));
        if (residual == 0m) return;
        if (!book.CurrencyRoundingAccountId.HasValue)
            throw new InvalidOperationException($"PARALLEL_REPLAY_ROUNDING_ACCOUNT_REQUIRED: Parallel book {book.Code} has no protected rounding account.");
        lines.Add(new AccountTransaction
        {
            Id = Guid.NewGuid(), TenantId = book.TenantId, AccountId = book.CurrencyRoundingAccountId.Value,
            TransactionDate = source.EntryDate.Date,
            Description = $"Historical conversion rounding for {source.JournalEntryNumber}",
            DebitAmount = residual < 0m ? Math.Abs(residual) : 0m,
            CreditAmount = residual > 0m ? residual : 0m,
            FunctionalCurrencyCode = book.FunctionalCurrencyCode!, TransactionCurrency = book.FunctionalCurrencyCode,
            TransactionDebitAmount = residual < 0m ? Math.Abs(residual) : 0m,
            TransactionCreditAmount = residual > 0m ? residual : 0m,
            BookClassification = book.Code, AccountingBookId = book.Id, FiscalPeriodId = source.FiscalPeriodId,
            PostedDate = now, PostingStatus = "Posted", LineNumber = lines.Count + 1,
            ExchangeRateId = rate.Id, ExchangeRate = rate.Rate,
            ExchangeRateSource = rate.RateSource, ExchangeRateDate = rate.EffectiveDate.Date,
            TransactionTag = "Parallel historical rounding", CreatedAt = now,
            CreatedById = actorId
        });
    }

    private static decimal RoundMoney(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    public async Task<AccountingBookInitializationDto?> GetAsync(Guid accountingBookId, CancellationToken cancellationToken = default)
    {
        await RequireBookAsync(accountingBookId, cancellationToken);
        var entity = await Query().AsNoTracking().Where(item => item.AccountingBookId == accountingBookId)
            .OrderByDescending(item => item.Version).FirstOrDefaultAsync(cancellationToken);
        return entity == null ? null : await MapAsync(entity, cancellationToken);
    }

    public async Task<AccountingBookInitializationPreparationDto> PrepareAsync(Guid accountingBookId, string mode, DateTime cutoffDate,
        Guid? sourceAccountingBookId, CancellationToken cancellationToken = default)
    {
        var book = await RequireBookAsync(accountingBookId, cancellationToken);
        if (!Enum.TryParse<AccountingBookInitializationMode>(mode, true, out var parsedMode)) throw new InvalidOperationException("Initialization mode is invalid.");
        ValidateModeForBook(book, parsedMode);
        if (cutoffDate == default) throw new InvalidOperationException("An initialization cutoff date is required.");
        ValidateParallelCutoffContinuity(book, cutoffDate);
        var cutoffPeriods = (await _db.FiscalPeriods.AsNoTracking().Where(item => item.TenantId == TenantId && !item.IsDeleted).ToListAsync(cancellationToken))
            .Where(item => item.EndDate.Date == cutoffDate.Date).ToList();
        if (cutoffPeriods.Count != 1) throw new InvalidOperationException("INITIALIZATION_CUTOFF_PERIOD_INVALID: Cutoff must be the end date of exactly one live same-tenant fiscal period.");
        var cutoffPeriod = cutoffPeriods[0];
        var source = await ValidateSourceAsync(book, parsedMode, sourceAccountingBookId, cancellationToken);
        var authorityBook = source ?? book;
        var tenantCurrency = await _db.Tenants.AsNoTracking().Where(item => item.Id == TenantId && !item.IsDeleted).Select(item => item.BaseCurrency).SingleOrDefaultAsync(cancellationToken);
        if (tenantCurrency is not { Length: 3 } || tenantCurrency.Any(ch => ch is < 'A' or > 'Z')) throw new InvalidOperationException("Canonical tenant functional-currency authority is required.");
        var mappings = await _db.AccountAccountingBooks.AsNoTracking().Include(item => item.Account).Include(item => item.AccountClassification)
            .Where(item => item.TenantId == TenantId && item.AccountingBookId == book.Id && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        mappings = mappings.Where(item => (item.IsEnabled || !book.IsActive && FinanceClassificationManifestSeeder.IsUntouchedManifestOwnedMapping(item))
                && (item.Account.EffectiveDate == null || item.Account.EffectiveDate.Value.Date <= cutoffDate.Date))
            .OrderBy(item => item.Account.AccountNumber).ToList();
        if (mappings.Count == 0)
            throw new InvalidOperationException(book.BookType == AccountingBookType.Delta
                ? "DELTA_ACCOUNT_MAPPINGS_REQUIRED: Prepare the Delta book structure from its base book before loading initialization evidence."
                : "Initialization requires at least one eligible enabled account mapping.");
        if (mappings.Any(item => item.Account == null || item.Account.IsDeleted || item.Account.TenantId != TenantId
            || item.AccountClassification == null || item.AccountClassification.IsDeleted || item.AccountClassification.Status != AccountClassificationStatus.Active
            || !item.AccountClassification.IsPostingClassification || item.AccountClassification.TenantId != TenantId
            || item.AccountClassification.AccountingBookId != book.Id || item.AccountClassification.CoreAccountType != item.Account.AccountType))
            throw new InvalidOperationException("Initialization contains an invalid mapped account or classification.");
        var balances = await _db.AccountBalances.AsNoTracking().Include(item => item.FiscalPeriod)
            .Where(item => item.TenantId == TenantId && item.AccountingBookId == authorityBook.Id && !item.IsDeleted && item.FiscalPeriod.EndDate <= cutoffDate.Date)
            .ToListAsync(cancellationToken);
        var latest = balances.GroupBy(item => item.AccountId).ToDictionary(group => group.Key,
            group => group.OrderByDescending(item => item.FiscalPeriod.EndDate).ThenByDescending(item => item.FiscalPeriod.PeriodNumber).First().ClosingBalance);
        var initializationCurrency = book.FunctionalCurrencyCode ?? tenantCurrency;
        var translations = await ResolveTranslationEvidenceAsync(book, source, cutoffDate.Date, mappings, latest, cancellationToken);
        return new AccountingBookInitializationPreparationDto { AccountingBookId = book.Id, AccountingBookCode = book.Code, Mode = parsedMode.ToString(),
            CutoffDate = cutoffDate.Date, CutoffFiscalPeriodId = cutoffPeriod.Id, CutoffFiscalPeriodCode = cutoffPeriod.PeriodCode,
            SourceAccountingBookId = source?.Id, SourceAccountingBookCode = source?.Code, FunctionalCurrencyCode = initializationCurrency,
            TranslationMethod = book.BookType == AccountingBookType.ParallelFull && book.ParallelOpeningMode == ParallelBookOpeningMode.GovernedOpeningConversion
                ? book.ParallelTranslationMethod?.ToString() : null,
            Accounts = mappings.Select(item =>
            {
                translations.TryGetValue(item.AccountId, out var translated);
                var sourceBalance = latest.GetValueOrDefault(item.AccountId);
                return new AccountingBookInitializationPreparationLineDto { AccountId = item.AccountId,
                    AccountNumber = item.Account.AccountNumber, AccountName = item.Account.AccountName, AccountClassificationId = item.AccountClassificationId!.Value,
                    AccountClassificationCode = item.AccountClassification!.Code, SourceSignedBalance = sourceBalance,
                    AuthoritativeSignedBalance = translated?.TranslatedSignedBalance ?? sourceBalance,
                    TranslationExchangeRateId = translated?.ExchangeRateId, TranslationRate = translated?.Multiplier,
                    TranslationRateDate = translated?.RateDate, TranslationRateType = translated?.RateType,
                    TranslationRateSource = translated?.RateSource };
            }).ToList() };
    }

    public Task<DeltaBookStructurePreparationDto> EnsureDeltaStructureAsync(Guid accountingBookId, CancellationToken cancellationToken = default) =>
        AtomicAsync(async () =>
        {
            var book = await _db.AccountingBooks.SingleOrDefaultAsync(item => item.Id == accountingBookId
                && item.TenantId == TenantId && !item.IsDeleted, cancellationToken)
                ?? throw new KeyNotFoundException("Accounting book was not found.");
            if (book.BookType is not (AccountingBookType.Delta or AccountingBookType.ParallelFull)
                || !book.BaseAccountingBookId.HasValue)
                throw new InvalidOperationException("Only a Delta or Parallel book with a governed base book can prepare inherited structure.");
            if (book.LifecycleStatus is not (AccountingBookLifecycleStatus.Configuring or AccountingBookLifecycleStatus.Initializing))
                throw new InvalidOperationException("Derived-book structure may be prepared only while the book is Configuring or Initializing.");
            if (book.IsActive || book.AllowsPosting)
                throw new InvalidOperationException("Structure preparation requires a non-posting book.");
            if (await _db.AccountingBookInitializations.AnyAsync(item => item.TenantId == TenantId
                && item.AccountingBookId == book.Id && !item.IsDeleted
                && (item.InitializationStatus == AccountingBookInitializationStatus.PendingApproval || item.InitializationStatus == AccountingBookInitializationStatus.Approved), cancellationToken))
                throw new InvalidOperationException("Structure cannot change while initialization evidence is pending approval or approved.");

            var baseBook = await _db.AccountingBooks.AsNoTracking().SingleOrDefaultAsync(item => item.Id == book.BaseAccountingBookId.Value
                && item.TenantId == TenantId && !item.IsDeleted, cancellationToken)
                ?? throw new InvalidOperationException("The derived book's governed base book is unavailable.");
            if (baseBook.LifecycleStatus is not (AccountingBookLifecycleStatus.Initializing or AccountingBookLifecycleStatus.Active))
                throw new InvalidOperationException("The derived book's governed base book must be initializing or active before structure can be prepared.");

            var sourceClassifications = await _db.AccountClassifications.AsNoTracking()
                .Where(item => item.TenantId == TenantId && item.AccountingBookId == baseBook.Id
                    && !item.IsDeleted && item.Status == AccountClassificationStatus.Active)
                .OrderBy(item => item.DisplayOrder).ThenBy(item => item.Code).ToListAsync(cancellationToken);
            var sourceMappings = await _db.AccountAccountingBooks.AsNoTracking()
                .Include(item => item.Account).Include(item => item.AccountClassification)
                .Where(item => item.TenantId == TenantId && item.AccountingBookId == baseBook.Id
                    && !item.IsDeleted && item.IsEnabled)
                .OrderBy(item => item.AccountId).ToListAsync(cancellationToken);
            if (sourceMappings.Count == 0 || sourceMappings.Any(item => item.Account == null || item.Account.IsDeleted
                || item.Account.TenantId != TenantId
                || item.AccountClassification == null || item.AccountClassification.IsDeleted
                || item.AccountClassification.TenantId != TenantId || item.AccountClassification.AccountingBookId != baseBook.Id
                || !item.AccountClassification.IsPostingClassification || item.AccountClassification.CoreAccountType != item.Account.AccountType
                || item.AccountClassification.Status != AccountClassificationStatus.Active))
                throw new InvalidOperationException("The base book must have complete enabled account mappings with active classifications before derived structure can be prepared.");

            var targetClassifications = await _db.AccountClassifications
                .Where(item => item.TenantId == TenantId && item.AccountingBookId == book.Id && !item.IsDeleted)
                .ToListAsync(cancellationToken);
            var sourceById = sourceClassifications.ToDictionary(item => item.Id);
            var targetByCode = targetClassifications.ToDictionary(item => item.Code, StringComparer.Ordinal);
            var clonedBySourceId = new Dictionary<Guid, AccountClassification>();
            var resolving = new HashSet<Guid>();

            AccountClassification ResolveClassification(AccountClassification source)
            {
                if (clonedBySourceId.TryGetValue(source.Id, out var resolved)) return resolved;
                if (!resolving.Add(source.Id)) throw new InvalidOperationException("The source classification hierarchy contains a cycle.");
                AccountClassification? parent = null;
                if (source.ParentClassificationId.HasValue)
                {
                    if (!sourceById.TryGetValue(source.ParentClassificationId.Value, out var sourceParent))
                        throw new InvalidOperationException($"Base classification {source.Code} has no active parent classification.");
                    parent = ResolveClassification(sourceParent);
                }
                if (!targetByCode.TryGetValue(source.Code, out resolved))
                {
                    resolved = new AccountClassification
                    {
                        TenantId = TenantId, AccountingBookId = book.Id, ParentClassificationId = parent?.Id,
                        Code = source.Code, Name = source.Name, Description = source.Description,
                        CoreAccountType = source.CoreAccountType, DefaultRevaluationTreatment = source.DefaultRevaluationTreatment,
                        SystemRole = source.SystemRole, IsPostingClassification = source.IsPostingClassification,
                        Status = AccountClassificationStatus.Active, DisplayOrder = source.DisplayOrder,
                        CreatedAt = DateTime.UtcNow, CreatedBy = ActorName()
                    };
                    _db.AccountClassifications.Add(resolved);
                    targetByCode.Add(resolved.Code, resolved);
                }
                else if (resolved.Status != AccountClassificationStatus.Active
                    || resolved.CoreAccountType != source.CoreAccountType
                    || resolved.IsPostingClassification != source.IsPostingClassification)
                {
                    throw new InvalidOperationException($"Derived-book classification {resolved.Code} conflicts with its base-book authority.");
                }
                else
                {
                    resolved.ParentClassificationId = parent?.Id;
                    resolved.Name = source.Name;
                    resolved.Description = source.Description;
                    resolved.DefaultRevaluationTreatment = source.DefaultRevaluationTreatment;
                    resolved.SystemRole = source.SystemRole;
                    resolved.DisplayOrder = source.DisplayOrder;
                    resolved.UpdatedAt = DateTime.UtcNow;
                    resolved.UpdatedBy = ActorName();
                }
                clonedBySourceId[source.Id] = resolved;
                resolving.Remove(source.Id);
                return resolved;
            }

            foreach (var source in sourceClassifications) ResolveClassification(source);

            var targetMappings = await _db.AccountAccountingBooks
                .Where(item => item.TenantId == TenantId && item.AccountingBookId == book.Id && !item.IsDeleted)
                .ToDictionaryAsync(item => item.AccountId, cancellationToken);
            foreach (var sourceMapping in sourceMappings)
            {
                var targetClassification = ResolveClassification(sourceMapping.AccountClassification!);
                if (!targetMappings.TryGetValue(sourceMapping.AccountId, out var targetMapping))
                {
                    targetMapping = new AccountAccountingBook
                    {
                        TenantId = TenantId, AccountId = sourceMapping.AccountId, AccountingBookId = book.Id,
                        AccountClassificationId = targetClassification.Id, IsEnabled = true,
                        FinancialStatementLineItem = sourceMapping.FinancialStatementLineItem,
                        CreatedAt = DateTime.UtcNow, CreatedBy = ActorName()
                    };
                    _db.AccountAccountingBooks.Add(targetMapping);
                    targetMappings.Add(targetMapping.AccountId, targetMapping);
                }
                else
                {
                    targetMapping.AccountClassificationId = targetClassification.Id;
                    targetMapping.IsEnabled = true;
                    targetMapping.FinancialStatementLineItem ??= sourceMapping.FinancialStatementLineItem;
                    targetMapping.UpdatedAt = DateTime.UtcNow;
                    targetMapping.UpdatedBy = ActorName();
                }
            }
            if (book.BookType == AccountingBookType.ParallelFull)
                await EnsureParallelProtectedAccountsAsync(book, targetByCode.Values, targetMappings, cancellationToken);

            // Preserve governed local-only mappings. Delta layers may own elimination accounts;
            // Parallel books own protected translation-reserve and rounding accounts.
            await _db.SaveChangesAsync(cancellationToken);
            await _audit.RecordAsync(new FinanceAuditEventDto
            {
                TenantId = TenantId, EventType = FinanceAuditEvents.AccountingBookDeltaStructurePrepared,
                SourceModule = "GL", SourceDocumentType = "AccountingBook", SourceDocumentId = book.Id,
                Resource = "Finance.AccountingBook", ResourceId = book.Id.ToString(),
                AfterValues = new
                {
                    DerivedAccountingBookId = book.Id, DerivedAccountingBookCode = book.Code,
                    BaseAccountingBookId = baseBook.Id, BaseAccountingBookCode = baseBook.Code,
                    ClassificationCount = clonedBySourceId.Count, AccountMappingCount = sourceMappings.Count
                },
                Reason = "Prepared governed inherited classifications and account mappings from the base book."
            }, cancellationToken);
            return new DeltaBookStructurePreparationDto
            {
                AccountingBookId = book.Id, AccountingBookCode = book.Code,
                BaseAccountingBookId = baseBook.Id, BaseAccountingBookCode = baseBook.Code,
                ClassificationCount = clonedBySourceId.Count, AccountMappingCount = sourceMappings.Count
            };
        }, cancellationToken);

    private async Task EnsureParallelProtectedAccountsAsync(
        AccountingBook book,
        IEnumerable<AccountClassification> classifications,
        IDictionary<Guid, AccountAccountingBook> mappings,
        CancellationToken cancellationToken)
    {
        var available = classifications.ToList();
        var equity = available.FirstOrDefault(item => item.Code == "EQUITY" && item.IsPostingClassification && item.CoreAccountType == AccountType.Equity)
            ?? available.FirstOrDefault(item => item.IsPostingClassification && item.CoreAccountType == AccountType.Equity)
            ?? throw new InvalidOperationException("PARALLEL_CTA_CLASSIFICATION_REQUIRED: The inherited structure has no active posting Equity classification.");
        var rounding = available.FirstOrDefault(item => item.Code == "OTHER_EXPENSE" && item.IsPostingClassification && item.CoreAccountType == AccountType.Expense)
            ?? available.FirstOrDefault(item => item.IsPostingClassification && item.CoreAccountType == AccountType.Expense)
            ?? throw new InvalidOperationException("PARALLEL_ROUNDING_CLASSIFICATION_REQUIRED: The inherited structure has no active posting Expense classification.");
        var currency = book.FunctionalCurrencyCode
            ?? throw new InvalidOperationException("PARALLEL_CURRENCY_REQUIRED: A Parallel book requires a functional currency.");

        async Task<Account> EnsureAccountAsync(string suffix, string name, AccountType type, string category)
        {
            var code = $"{book.Code}_{suffix}";
            if (code.Length > 50) code = code[..50];
            var existing = await _db.Accounts.SingleOrDefaultAsync(item => item.TenantId == TenantId
                && item.AccountCode == code && !item.IsDeleted, cancellationToken);
            if (existing != null)
            {
                if (!existing.IsSystemAccount || existing.AccountType != type
                    || !string.Equals(existing.CurrencyCode, currency, StringComparison.Ordinal))
                    throw new InvalidOperationException($"PARALLEL_PROTECTED_ACCOUNT_CONFLICT: Account code {code} already exists with incompatible authority.");
                return existing;
            }

            var account = new Account
            {
                TenantId = TenantId,
                AccountCode = code,
                AccountNumber = code.Replace('_', '-'),
                AccountName = name,
                AccountType = type,
                AccountCategory = category,
                Description = $"Protected {book.Code} account; available only to system-generated Parallel replication and translation.",
                CurrencyCode = currency,
                IsMultiCurrency = false,
                IsSegmented = false,
                IsIFRSClassified = false,
                IsBaseClassified = false,
                IsLocalClassified = false,
                AllowDirectPosting = false,
                IsControlAccount = false,
                BudgetTrackingEnabled = false,
                Status = AccountStatus.Active,
                IsSystemAccount = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = ActorName()
            };
            _db.Accounts.Add(account);
            return account;
        }

        async Task EnsureMappingAsync(Account account, AccountClassification classification)
        {
            if (mappings.TryGetValue(account.Id, out var existing))
            {
                existing.AccountClassificationId = classification.Id;
                existing.IsEnabled = true;
                existing.UpdatedAt = DateTime.UtcNow;
                existing.UpdatedBy = ActorName();
                return;
            }
            var mapping = new AccountAccountingBook
            {
                TenantId = TenantId,
                AccountId = account.Id,
                AccountingBookId = book.Id,
                AccountClassificationId = classification.Id,
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = ActorName()
            };
            _db.AccountAccountingBooks.Add(mapping);
            mappings.Add(account.Id, mapping);
            await Task.CompletedTask;
        }

        var reserveAccount = book.CurrencyTranslationReserveAccountId.HasValue
            ? await _db.Accounts.SingleOrDefaultAsync(item => item.TenantId == TenantId
                && item.Id == book.CurrencyTranslationReserveAccountId.Value && !item.IsDeleted, cancellationToken)
                ?? throw new InvalidOperationException("PARALLEL_CTA_ACCOUNT_INVALID: The configured translation-reserve account is unavailable.")
            : await EnsureAccountAsync("CTA", $"{book.Code} Currency Translation Reserve", AccountType.Equity, "Other comprehensive income");
        var roundingAccount = book.CurrencyRoundingAccountId.HasValue
            ? await _db.Accounts.SingleOrDefaultAsync(item => item.TenantId == TenantId
                && item.Id == book.CurrencyRoundingAccountId.Value && !item.IsDeleted, cancellationToken)
                ?? throw new InvalidOperationException("PARALLEL_ROUNDING_ACCOUNT_INVALID: The configured rounding account is unavailable.")
            : await EnsureAccountAsync("ROUNDING", $"{book.Code} Currency Translation Rounding", AccountType.Expense, "Other expenses");

        await EnsureMappingAsync(reserveAccount, equity);
        await EnsureMappingAsync(roundingAccount, rounding);
        book.CurrencyTranslationReserveAccountId = reserveAccount.Id;
        book.CurrencyRoundingAccountId = roundingAccount.Id;
    }

    public Task<AccountingBookInitializationDto> ConfigureAsync(Guid accountingBookId, ConfigureAccountingBookInitializationDto request, CancellationToken cancellationToken = default) =>
        AtomicAsync(async () =>
        {
            var book = await RequireBookAsync(accountingBookId, cancellationToken);
            if (book.LifecycleStatus is not (AccountingBookLifecycleStatus.Configuring or AccountingBookLifecycleStatus.Initializing))
                throw new InvalidOperationException("Book initialization may be configured only while the book is Configuring or Initializing.");
            if (!Enum.TryParse<AccountingBookInitializationMode>(request.Mode, true, out var mode)) throw new InvalidOperationException("Initialization mode is invalid.");
            ValidateModeForBook(book, mode);
            if (request.CutoffDate == default) throw new InvalidOperationException("An initialization cutoff date is required.");
            if (string.IsNullOrWhiteSpace(request.IdempotencyKey)) throw new InvalidOperationException("An initialization idempotency key is required.");
            if (string.IsNullOrWhiteSpace(request.Reason)) throw new InvalidOperationException("An initialization reason is required.");
            var source = await ValidateSourceAsync(book, mode, request.SourceAccountingBookId, cancellationToken);
            if (mode == AccountingBookInitializationMode.IndependentOpeningBalances && !string.IsNullOrWhiteSpace(request.SourceAccountingBookCode)
                || source != null && !string.Equals(request.SourceAccountingBookCode, source.Code, StringComparison.Ordinal))
                throw new InvalidOperationException("Initialization source book ID and canonical code evidence must agree exactly.");
            var prepared = await PrepareEvidenceAsync(book, source, mode, request.CutoffDate.Date, request.IdempotencyKey.Trim(), request.Reason.Trim(), request.Lines, cancellationToken);
            var cutoffCode = await _db.FiscalPeriods.AsNoTracking().Where(item => item.Id == prepared.CutoffFiscalPeriodId && item.TenantId == TenantId && !item.IsDeleted)
                .Select(item => item.PeriodCode).SingleAsync(cancellationToken);
            if (request.CutoffFiscalPeriodId != prepared.CutoffFiscalPeriodId
                || !string.Equals(request.CutoffFiscalPeriodCode, cutoffCode, StringComparison.Ordinal))
                throw new InvalidOperationException("Initialization cutoff fiscal-period ID, code, and date must agree exactly.");
            var byKey = await Query().SingleOrDefaultAsync(item => item.IdempotencyKey == request.IdempotencyKey.Trim(), cancellationToken);
            if (byKey != null)
            {
                if (byKey.AccountingBookId != book.Id || byKey.EvidenceFingerprint != prepared.EvidenceFingerprint || byKey.ReconciliationFingerprint != prepared.ReconciliationFingerprint)
                    throw new InvalidOperationException("INITIALIZATION_IDEMPOTENCY_CONFLICT: The key was already used with different opening or authority evidence.");
                return await MapAsync(byKey, cancellationToken);
            }
            var latest = await Query().Where(item => item.AccountingBookId == book.Id).OrderByDescending(item => item.Version).ThenByDescending(item => item.Id).FirstOrDefaultAsync(cancellationToken);
            var existing = latest?.InitializationStatus == AccountingBookInitializationStatus.Draft ? latest : null;
            if (existing != null)
            {
                ApplyRowVersion(existing, request.RowVersion);
            }
            else if (latest?.InitializationStatus is AccountingBookInitializationStatus.PendingApproval or AccountingBookInitializationStatus.Approved)
                throw new InvalidOperationException("Submitted or approved initialization evidence is immutable.");
            var entity = existing ?? new AccountingBookInitialization { TenantId = TenantId, AccountingBookId = book.Id,
                Version = (latest?.Version ?? 0) + 1, SupersedesInitializationId = latest?.Id,
                PreparedByUserId = Actor(), PreparedAtUtc = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, CreatedBy = ActorName() };
            if (existing == null) _db.AccountingBookInitializations.Add(entity);
            entity.Mode = mode; entity.CutoffDate = request.CutoffDate.Date; entity.CutoffFiscalPeriodId = prepared.CutoffFiscalPeriodId;
            entity.SourceAccountingBookId = source?.Id;
            entity.TranslationMethod = book.BookType == AccountingBookType.ParallelFull
                && book.ParallelOpeningMode == ParallelBookOpeningMode.GovernedOpeningConversion
                ? book.ParallelTranslationMethod : null;
            entity.IdempotencyKey = request.IdempotencyKey.Trim(); entity.Reason = request.Reason.Trim(); entity.InitializationStatus = AccountingBookInitializationStatus.Draft;
            entity.TotalDebits = prepared.TotalDebits; entity.TotalCredits = prepared.TotalCredits; entity.RequiredAccountCount = prepared.RequiredCount;
            entity.CoveredAccountCount = prepared.Lines.Count; entity.EvidenceFingerprint = prepared.EvidenceFingerprint; entity.ReconciliationFingerprint = prepared.ReconciliationFingerprint;
            if (existing == null)
                entity.Lines = prepared.Lines.Select(line => NewLine(line)).ToList();
            else
            {
                // The exact account set was revalidated above. Update draft evidence in place so a
                // substantive edit preserves line identity and cannot create delete/reinsert races.
                foreach (var line in prepared.Lines)
                {
                    var persisted = entity.Lines.Single(item => item.AccountId == line.AccountId);
                    persisted.CurrencyCode = line.CurrencyCode; persisted.OpeningDebit = line.OpeningDebit;
                    persisted.OpeningCredit = line.OpeningCredit; persisted.BaseBookSignedBalance = line.BaseBookSignedBalance;
                    persisted.OpeningAdjustment = line.OpeningAdjustment;
                    persisted.TranslationExchangeRateId = line.TranslationExchangeRateId;
                    persisted.TranslationRate = line.TranslationRate; persisted.TranslationRateDate = line.TranslationRateDate;
                    persisted.TranslationRateType = line.TranslationRateType; persisted.TranslationRateSource = line.TranslationRateSource;
                    persisted.UpdatedAt = DateTime.UtcNow; persisted.UpdatedBy = ActorName();
                }
            }
            // The last substantive draft editor is the maker of the evidence eventually submitted.
            // Re-editing a rejected/draft version must not retain a prior checker decision.
            entity.PreparedByUserId = Actor(); entity.PreparedAtUtc = DateTime.UtcNow;
            entity.WorkflowInstanceId = null; entity.ApprovedByUserId = null; entity.ApprovedAtUtc = null;
            entity.RejectedByUserId = null; entity.RejectedAtUtc = null; entity.DecidedByUserId = null;
            entity.DecidedAtUtc = null; entity.DecisionReason = null;
            book.InitializationStartedAtUtc ??= DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            await AuditAsync(FinanceAuditEvents.AccountingBookInitializationConfigured, entity, request.Reason, cancellationToken);
            return await MapAsync(await Query().AsNoTracking().SingleAsync(item => item.Id == entity.Id, cancellationToken), cancellationToken);
        }, cancellationToken);

    public Task<AccountingBookInitializationDto> SubmitAsync(Guid accountingBookId, CancellationToken cancellationToken = default) => AtomicAsync(async () =>
    {
        var entity = await Query().Where(item => item.AccountingBookId == accountingBookId).OrderByDescending(item => item.Version).ThenByDescending(item => item.Id).FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Book initialization has not been configured.");
        if (entity.InitializationStatus == AccountingBookInitializationStatus.PendingApproval) return await MapAsync(entity, cancellationToken);
        if (entity.InitializationStatus != AccountingBookInitializationStatus.Draft) throw new InvalidOperationException("Only Draft initialization evidence can be submitted.");
        await EnsureEvidenceUnchangedAsync(entity, cancellationToken);
        if (!await _workflow.HasActiveApprovalWorkflowAsync(WorkflowEntityType)) throw new InvalidOperationException("A published AccountingBookInitialization approval workflow is required.");
        var result = await _workflow.StartApprovalWorkflowAsync(WorkflowEntityType, entity.Id);
        if (!result.Success) throw new InvalidOperationException(result.Message ?? "Initialization approval workflow could not be started.");
        entity.WorkflowInstanceId = result.WorkflowInstanceId; entity.InitializationStatus = AccountingBookInitializationStatus.PendingApproval;
        await _db.SaveChangesAsync(cancellationToken);
        await AuditAsync(FinanceAuditEvents.AccountingBookInitializationSubmitted, entity, entity.Reason, cancellationToken);
        return await MapAsync(entity, cancellationToken);
    }, cancellationToken);

    public Task<AccountingBookInitializationDto> ApproveAsync(Guid accountingBookId, DecideAccountingBookInitializationDto request, CancellationToken cancellationToken = default) =>
        DecideAsync(accountingBookId, request, true, cancellationToken);
    public Task<AccountingBookInitializationDto> RejectAsync(Guid accountingBookId, DecideAccountingBookInitializationDto request, CancellationToken cancellationToken = default) =>
        DecideAsync(accountingBookId, request, false, cancellationToken);

    private Task<AccountingBookInitializationDto> DecideAsync(Guid accountingBookId, DecideAccountingBookInitializationDto request, bool approve, CancellationToken ct) => AtomicAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(request.Reason)) throw new InvalidOperationException("An initialization decision reason is required.");
        var entity = await Query().Where(item => item.AccountingBookId == accountingBookId).OrderByDescending(item => item.Version).ThenByDescending(item => item.Id).FirstOrDefaultAsync(ct) ?? throw new KeyNotFoundException("Book initialization was not found.");
        ApplyRowVersion(entity, request.RowVersion);
        if (entity.InitializationStatus != AccountingBookInitializationStatus.PendingApproval || !entity.WorkflowInstanceId.HasValue)
            throw new InvalidOperationException("Book initialization is not pending approval.");
        var checker = Actor();
        if (checker == entity.PreparedByUserId) throw new InvalidOperationException("The initialization checker must differ from the maker.");
        if (!await _workflow.CanUserApproveAsync(WorkflowEntityType, entity.Id, checker)) throw new UnauthorizedAccessException("The current user cannot decide this initialization.");
        if (approve) await EnsureEvidenceUnchangedAsync(entity, ct);
        var outcome = await _workflow.ProcessApprovalStepAsync(WorkflowEntityType, entity.Id, checker, approve ? "Approve" : "Reject", request.Reason.Trim());
        if (!outcome.Success) throw new InvalidOperationException(outcome.Message ?? "Initialization workflow decision failed.");
        var completed = outcome.Status == WorkflowInstanceStatus.Completed;
        if (completed) entity.InitializationStatus = approve ? AccountingBookInitializationStatus.Approved : AccountingBookInitializationStatus.Rejected;
        entity.DecidedByUserId = checker; entity.DecidedAtUtc = DateTime.UtcNow;
        entity.ApprovedByUserId = completed && approve ? checker : null; entity.ApprovedAtUtc = completed && approve ? DateTime.UtcNow : null;
        entity.RejectedByUserId = completed && !approve ? checker : null; entity.RejectedAtUtc = completed && !approve ? DateTime.UtcNow : null;
        entity.DecisionReason = request.Reason.Trim(); entity.UpdatedAt = DateTime.UtcNow; entity.UpdatedBy = ActorName();
        await _db.SaveChangesAsync(ct);
        var auditType = !completed ? FinanceAuditEvents.AccountingBookInitializationApprovalStepCompleted
            : approve ? FinanceAuditEvents.AccountingBookInitializationApproved : FinanceAuditEvents.AccountingBookInitializationRejected;
        await AuditAsync(auditType, entity, request.Reason, ct);
        return await MapAsync(entity, ct);
    }, ct);

    public async Task<AccountingBookActivationReadinessDto> GetReadinessAsync(Guid accountingBookId, CancellationToken cancellationToken = default)
    {
        var book = await RequireBookAsync(accountingBookId, cancellationToken);
        var blockers = new List<string>();
        var initialization = await Query().AsNoTracking().Where(item => item.AccountingBookId == book.Id)
            .OrderByDescending(item => item.Version).ThenByDescending(item => item.Id).FirstOrDefaultAsync(cancellationToken);
        if (initialization?.InitializationStatus != AccountingBookInitializationStatus.Approved)
            blockers.Add("The latest initialization version must be approved.");
        else
        {
            try { await EnsureEvidenceUnchangedAsync(initialization, cancellationToken); }
            catch (InvalidOperationException ex) { blockers.Add(ex.Message); }
        }
        // Tenant fiscal authority is the sole period gate. Accounting books no longer maintain a
        // second open/close state that can drift from the tenant calendar.
        var firstPostingDate = initialization == null ? (DateTime?)null : initialization.CutoffDate.Date.AddDays(1);
        if (book.BookType == AccountingBookType.Delta && firstPostingDate.HasValue && book.EffectiveFromUtc.HasValue
            && book.EffectiveFromUtc.Value.Date > firstPostingDate.Value)
            firstPostingDate = book.EffectiveFromUtc.Value.Date;
        var effectivePeriods = firstPostingDate.HasValue ? await _db.FiscalPeriods.AsNoTracking().Where(item => item.TenantId == TenantId && !item.IsDeleted
            && item.StartDate <= firstPostingDate.Value && item.EndDate >= firstPostingDate.Value).Select(item => item.Id).ToListAsync(cancellationToken) : new List<Guid>();
        if (effectivePeriods.Count != 1) blockers.Add("Exactly one tenant fiscal period must contain the book's first posting date.");
        var ready = 0;
        if (effectivePeriods.Count == 1)
        {
            var fiscal = await _db.FiscalPeriods.AsNoTracking().SingleAsync(item => item.Id == effectivePeriods[0], cancellationToken);
            if (!fiscal.IsOpen || fiscal.IsClosed || fiscal.IsLocked) blockers.Add("The tenant fiscal period must remain globally open and unlocked.");
            else ready = 1;
            var fiscalYear = await _db.FiscalYears.AsNoTracking().SingleOrDefaultAsync(item => item.Id == fiscal.FiscalYearId
                && item.TenantId == TenantId && !item.IsDeleted, cancellationToken);
            if (fiscalYear == null || fiscalYear.IsClosed || fiscalYear.IsLocked)
                blockers.Add("The same-tenant parent fiscal year must remain open and unlocked.");
            var financeModuleId = await _db.ModuleDefinitions.AsNoTracking().Where(item => item.TenantId == TenantId && item.ModuleCode == ErpSystem.Core.Finance.FinanceModuleLockCatalog.Finance
                && item.IsActive && !item.IsDeleted).Select(item => (Guid?)item.Id).SingleOrDefaultAsync(cancellationToken);
            if (!financeModuleId.HasValue && fiscal.IsGlobalLockSuspended)
                blockers.Add("The Finance module must be registered and explicitly open during a partial global period lock.");
            else if (financeModuleId.HasValue)
            {
                var moduleLock = await _db.PeriodModuleLocks.AsNoTracking().SingleOrDefaultAsync(item => item.TenantId == TenantId
                    && item.FiscalPeriodId == fiscal.Id && item.ModuleDefinitionId == financeModuleId.Value && !item.IsDeleted, cancellationToken);
                var expired = moduleLock is { IsLocked: false, ReopenExpiresAtUtc: not null }
                    && moduleLock.ReopenExpiresAtUtc <= DateTime.UtcNow;
                if (moduleLock?.IsLocked == true || expired || fiscal.IsGlobalLockSuspended && moduleLock == null)
                    blockers.Add("The Finance module must remain open for the book's first posting period.");
            }
        }
        return new AccountingBookActivationReadinessDto { IsReady = blockers.Count == 0, Blockers = blockers,
            InitializationFingerprint = initialization?.EvidenceFingerprint, RequiredPeriodCount = effectivePeriods.Count, ReadyPeriodCount = ready };
    }

    public async Task<AccountingBookInitializationEvidenceValidationDto> ValidateCurrentApprovedEvidenceAsync(Guid accountingBookId, CancellationToken cancellationToken = default)
    {
        await RequireBookAsync(accountingBookId, cancellationToken);
        var latest = await Query().AsNoTracking().Where(item => item.AccountingBookId == accountingBookId)
            .OrderByDescending(item => item.Version).ThenByDescending(item => item.Id).FirstOrDefaultAsync(cancellationToken);
        if (latest?.InitializationStatus != AccountingBookInitializationStatus.Approved)
            return new() { IsValid = false, InitializationId = latest?.Id, Version = latest?.Version, Blocker = "The latest initialization version is not approved." };
        try
        {
            // Re-derivation is the C4 authority: retained hashes alone are not proof that mappings and balances stayed reconciled.
            await EnsureEvidenceUnchangedAsync(latest, cancellationToken);
            return new() { IsValid = true, InitializationId = latest.Id, Version = latest.Version,
                EvidenceFingerprint = latest.EvidenceFingerprint, ReconciliationFingerprint = latest.ReconciliationFingerprint };
        }
        catch (InvalidOperationException ex)
        {
            return new() { IsValid = false, InitializationId = latest.Id, Version = latest.Version,
                EvidenceFingerprint = latest.EvidenceFingerprint, ReconciliationFingerprint = latest.ReconciliationFingerprint, Blocker = ex.Message };
        }
    }

    private async Task EnsureEvidenceUnchangedAsync(AccountingBookInitialization entity, CancellationToken ct)
    {
        var source = await ValidateSourceAsync(entity.AccountingBook, entity.Mode, entity.SourceAccountingBookId, ct);
        var prepared = await PrepareEvidenceAsync(entity.AccountingBook, source, entity.Mode, entity.CutoffDate,
            entity.IdempotencyKey, entity.Reason, entity.Lines.Select(MapLine).ToList(), ct);
        if (prepared.CutoffFiscalPeriodId != entity.CutoffFiscalPeriodId)
            throw new InvalidOperationException("INITIALIZATION_EVIDENCE_STALE: The authoritative cutoff period changed.");
        if (!prepared.AcceptedEvidenceFingerprints.Contains(entity.EvidenceFingerprint))
            throw new InvalidOperationException("INITIALIZATION_EVIDENCE_STALE: Book identity or opening-line evidence changed.");
        if (!prepared.AcceptedReconciliationFingerprints.Contains(entity.ReconciliationFingerprint))
            throw new InvalidOperationException("INITIALIZATION_EVIDENCE_STALE: Account authority, balances, or posted transactions changed.");
    }

    private async Task<Prepared> PrepareEvidenceAsync(AccountingBook book, AccountingBook? source, AccountingBookInitializationMode mode, DateTime cutoff,
        string idempotencyKey, string reason, IReadOnlyCollection<AccountingBookInitializationLineDto> requested, CancellationToken ct)
    {
        ValidateParallelCutoffContinuity(book, cutoff);
        var cutoffPeriods = (await _db.FiscalPeriods.AsNoTracking().Where(item => item.TenantId == TenantId && !item.IsDeleted).ToListAsync(ct))
            .Where(item => item.EndDate.Date == cutoff.Date).ToList();
        if (cutoffPeriods.Count != 1)
            throw new InvalidOperationException("INITIALIZATION_CUTOFF_PERIOD_INVALID: Cutoff must be the end date of exactly one live same-tenant fiscal period.");
        var cutoffPeriod = cutoffPeriods[0];
        var allMappings = await _db.AccountAccountingBooks.AsNoTracking().Include(item => item.Account).Include(item => item.AccountClassification)
            .Where(item => item.TenantId == TenantId && item.AccountingBookId == book.Id && !item.IsDeleted).ToListAsync(ct);
        allMappings = allMappings.Where(item => item.IsEnabled || !book.IsActive && FinanceClassificationManifestSeeder.IsUntouchedManifestOwnedMapping(item))
            .OrderBy(item => item.AccountId).ToList();
        var mappings = allMappings.Where(item => item.Account.EffectiveDate == null || item.Account.EffectiveDate.Value.Date <= cutoff.Date)
            .ToList();
        if (mappings.Count == 0 || allMappings.Any(item => item.Account == null || item.Account.IsDeleted || item.Account.TenantId != TenantId
            || item.AccountClassification == null || item.AccountClassification.IsDeleted || item.AccountClassification.Status != AccountClassificationStatus.Active
            || !item.AccountClassification.IsPostingClassification || item.AccountClassification.TenantId != TenantId
            || item.AccountClassification.AccountingBookId != book.Id || item.AccountClassification.CoreAccountType != item.Account.AccountType))
            throw new InvalidOperationException("Initialization requires complete enabled account mappings with compatible active posting classifications.");
        var lines = requested.OrderBy(item => item.AccountId).ToList();
        var lineIds = lines.Select(item => item.AccountId).ToHashSet();
        var eligibleIds = mappings.Select(item => item.AccountId).ToHashSet();
        var mappedIds = allMappings.Select(item => item.AccountId).ToHashSet();
        if (lines.Select(item => item.AccountId).Distinct().Count() != lines.Count
            || !eligibleIds.IsSubsetOf(lineIds) || !lineIds.IsSubsetOf(mappedIds))
            throw new InvalidOperationException("Initialization must contain exactly one opening line for every enabled mapped account.");
        // Older approved packs may retain zero lines for accounts that were subsequently given a
        // future effective date. Those frozen lines are harmless historical evidence. They must
        // not make an already-active book unready, while a newly retroactive account still makes
        // the pack stale because every cutoff-eligible mapping remains mandatory.
        var futureLineIds = allMappings.Where(item => item.Account.EffectiveDate?.Date > cutoff.Date)
            .Select(item => item.AccountId).ToHashSet();
        if (lines.Any(item => !eligibleIds.Contains(item.AccountId)
            && (!futureLineIds.Contains(item.AccountId) || item.OpeningDebit != 0 || item.OpeningCredit != 0
                || item.BaseBookSignedBalance != 0 || item.OpeningAdjustment != 0)))
            throw new InvalidOperationException("Initialization contains non-zero evidence for an account that was not effective at the cutoff.");
        var tenantCurrency = await _db.Tenants.AsNoTracking().Where(item => item.Id == TenantId).Select(item => item.BaseCurrency).SingleOrDefaultAsync(ct);
        if (tenantCurrency is not { Length: 3 } || tenantCurrency.Any(ch => ch is < 'A' or > 'Z')) throw new InvalidOperationException("Canonical tenant functional-currency authority is required.");
        var initializationCurrency = book.FunctionalCurrencyCode ?? tenantCurrency;
        foreach (var line in lines)
        {
            line.CurrencyCode = line.CurrencyCode?.Trim() ?? string.Empty;
            if (!string.Equals(line.CurrencyCode, initializationCurrency, StringComparison.Ordinal))
                throw new InvalidOperationException("Every opening line must use the exact initialized-book functional currency.");
            if (line.OpeningDebit < 0 || line.OpeningCredit < 0 || line.OpeningDebit > 0 && line.OpeningCredit > 0) throw new InvalidOperationException("Opening debit/credit evidence is invalid.");
        }
        var authorityBook = source ?? book;
        var sourceTransactions = await _db.AccountTransactions.AsNoTracking().Include(item => item.JournalEntry).Include(item => item.FiscalPeriod)
            .Where(item => item.TenantId == TenantId && item.AccountingBookId == authorityBook.Id && item.TransactionDate <= cutoff && !item.IsDeleted
                && item.PostingStatus == "Posted").ToListAsync(ct);
        // Initialization and C2 rebuild must interpret identical retained ledger evidence. A posted
        // active line under inconsistent header/book/period evidence is corruption, never an omission.
        if (sourceTransactions.Any(item => item.JournalEntry == null || item.JournalEntry.IsDeleted || item.JournalEntry.PostingStatus != "Posted"
            || item.JournalEntry.TenantId != TenantId || item.JournalEntry.AccountingBookId != authorityBook.Id
            || !string.Equals(item.BookClassification, authorityBook.Code, StringComparison.Ordinal)
            || !string.Equals(item.JournalEntry.BookClassification, authorityBook.Code, StringComparison.Ordinal)
            || item.FiscalPeriod == null || item.FiscalPeriod.TenantId != TenantId || item.FiscalPeriod.IsDeleted
            || item.TransactionDate.Date < item.FiscalPeriod.StartDate.Date || item.TransactionDate.Date > item.FiscalPeriod.EndDate.Date))
            throw new InvalidOperationException("INITIALIZATION_SOURCE_EVIDENCE_CORRUPT: Posted line, journal, exact book, code snapshot or fiscal-period lineage disagrees.");
        var balances = await _db.AccountBalances.AsNoTracking().Include(item => item.FiscalPeriod)
            .Where(item => item.TenantId == TenantId && item.AccountingBookId == authorityBook.Id && !item.IsDeleted && item.FiscalPeriod.EndDate <= cutoff)
            .ToListAsync(ct);
        var latest = balances.GroupBy(item => item.AccountId).ToDictionary(group => group.Key,
            group => group.OrderByDescending(item => item.FiscalPeriod.EndDate).ThenByDescending(item => item.FiscalPeriod.PeriodNumber).First());
        var translatedEvidence = await ResolveTranslationEvidenceAsync(book, source, cutoff, mappings,
            latest.ToDictionary(item => item.Key, item => item.Value.ClosingBalance), ct);
        if (mode != AccountingBookInitializationMode.IndependentOpeningBalances)
        {
            foreach (var line in lines)
            {
                var expected = latest.GetValueOrDefault(line.AccountId)?.ClosingBalance ?? 0m;
                if (line.BaseBookSignedBalance != expected) throw new InvalidOperationException("Base-book opening evidence no longer agrees with the authoritative exact-book balance.");
                translatedEvidence.TryGetValue(line.AccountId, out var translation);
                if (translation != null && (line.TranslationExchangeRateId != translation.ExchangeRateId
                    || line.TranslationRate != translation.Multiplier || line.TranslationRateDate?.Date != translation.RateDate?.Date
                    || !string.Equals(line.TranslationRateType, translation.RateType, StringComparison.Ordinal)
                    || !string.Equals(line.TranslationRateSource, translation.RateSource, StringComparison.Ordinal)))
                    throw new InvalidOperationException("PARALLEL_OPENING_RATE_EVIDENCE_STALE: The approved exchange-rate evidence changed.");
                if (translation == null && (line.TranslationExchangeRateId.HasValue || line.TranslationRate.HasValue
                    || line.TranslationRateDate.HasValue || line.TranslationRateType != null || line.TranslationRateSource != null))
                    throw new InvalidOperationException("Initialization contains exchange-rate evidence that is not authoritative for this line.");
                if (mode == AccountingBookInitializationMode.BaseBookCopyAtCutoff && line.OpeningAdjustment != 0)
                    throw new InvalidOperationException("Base-book copy initialization cannot contain opening adjustments.");
                var signedOpening = line.OpeningDebit - line.OpeningCredit;
                var translatedBase = translation?.TranslatedSignedBalance ?? expected;
                var expectedOpening = mode == AccountingBookInitializationMode.BaseBookCopyAtCutoff ? translatedBase : translatedBase + line.OpeningAdjustment;
                if (signedOpening != expectedOpening) throw new InvalidOperationException("Opening evidence does not reconcile to the selected initialization mode.");
            }
        }
        else
        {
            if (lines.Any(item => item.BaseBookSignedBalance != 0 || item.OpeningAdjustment != 0))
                throw new InvalidOperationException("Independent opening balances cannot carry base-book or adjustment evidence.");
            foreach (var line in lines)
                if (line.OpeningDebit - line.OpeningCredit != (latest.GetValueOrDefault(line.AccountId)?.ClosingBalance ?? 0m))
                    throw new InvalidOperationException("Independent opening evidence must reconcile to posted exact-book balances at cutoff; requested lines are not financial authority.");
        }
        var debit = lines.Sum(item => item.OpeningDebit); var credit = lines.Sum(item => item.OpeningCredit);
        if (debit != credit) throw new InvalidOperationException("Initialization opening evidence must be balanced.");
        var evidenceMappings = allMappings.Where(item => lineIds.Contains(item.AccountId)).OrderBy(item => item.AccountId);
        var authority = string.Join('|', evidenceMappings.Select(item => $"{item.AccountId:N}:{item.Account.AccountNumber}:{item.Account.AccountType}:{item.Id:N}:{item.AccountClassificationId:N}:{item.AccountClassification!.Code}:{item.AccountClassification.Status}:{item.AccountClassification.IsPostingClassification}"));
        var balanceEvidence = string.Join('|', latest.OrderBy(item => item.Key).Select(item => $"{item.Key:N}:{item.Value.Id:N}:{item.Value.FiscalPeriodId:N}:{item.Value.FiscalPeriod.PeriodCode}:{item.Value.FiscalPeriod.StartDate:O}:{item.Value.FiscalPeriod.EndDate:O}:{D(item.Value.OpeningBalance)}:{D(item.Value.PeriodDebits)}:{D(item.Value.PeriodCredits)}:{D(item.Value.ClosingBalance)}"));
        var transactionEvidence = string.Join('|', sourceTransactions.OrderBy(item => item.TransactionDate).ThenBy(item => item.JournalEntryId).ThenBy(item => item.Id)
            .Select(item => new { item.Id, item.JournalEntryId, item.FiscalPeriodId, item.AccountId, item.TransactionDate, item.DebitAmount, item.CreditAmount, item.TransactionCurrency, item.ForeignCurrencyAmount, item.ExchangeRate })
            .Select(item => $"{item.Id:N}:{item.JournalEntryId:N}:{item.FiscalPeriodId:N}:{item.AccountId:N}:{item.TransactionDate:O}:{D(item.DebitAmount)}:{D(item.CreditAmount)}:{item.TransactionCurrency}:{D(item.ForeignCurrencyAmount ?? 0)}:{D(item.ExchangeRate ?? 0)}"));
        var lineEvidence = string.Join('|', lines.Select(item => $"{item.AccountId:N}:{item.CurrencyCode}:{D(item.OpeningDebit)}:{D(item.OpeningCredit)}:{D(item.BaseBookSignedBalance)}:{D(item.OpeningAdjustment)}:{item.TranslationExchangeRateId:N}:{D(item.TranslationRate ?? 0)}:{item.TranslationRateDate:O}:{item.TranslationRateType}:{item.TranslationRateSource}"));
        // Lifecycle activation changes the book's state but not its approved opening authority.
        // The source's active/postable state is revalidated separately on every evidence read.
        var evidence = AccountingBookInitializationFingerprint.Evidence(
            TenantId, book.Id, book.Code, book.BookType, book.FunctionalCurrencyCode,
            mode, cutoff, cutoffPeriod.Id, cutoffPeriod.PeriodCode, cutoffPeriod.StartDate,
            cutoffPeriod.EndDate, source?.Id, source?.Code, source?.BookType,
            source?.FunctionalCurrencyCode, book.BookType == AccountingBookType.ParallelFull ? book.ParallelTranslationMethod : null,
            idempotencyKey, reason, lineEvidence);
        var reconciliation = AccountingBookInitializationFingerprint.Reconciliation(
            evidence, authority, balanceEvidence, transactionEvidence, debit, credit);
        // V2 fingerprints encoded the mutable PrimaryFull/ParallelFull designation for both
        // the initialized book and its source. A governed primary replacement legitimately
        // swaps those designations without changing opening evidence. Accept every equivalent
        // legacy full-book designation while issuing only the designation-neutral V3 format.
        var acceptedEvidence = new HashSet<string>(StringComparer.Ordinal) { evidence };
        if (book.BookType != AccountingBookType.ParallelFull || book.ParallelOpeningMode != ParallelBookOpeningMode.GovernedOpeningConversion)
            acceptedEvidence.Add(AccountingBookInitializationFingerprint.LegacyEvidenceV3(
                TenantId, book.Id, book.Code, book.BookType, book.FunctionalCurrencyCode,
                mode, cutoff, cutoffPeriod.Id, cutoffPeriod.PeriodCode, cutoffPeriod.StartDate,
                cutoffPeriod.EndDate, source?.Id, source?.Code, source?.BookType,
                source?.FunctionalCurrencyCode, idempotencyKey, reason,
                string.Join('|', lines.Select(item => $"{item.AccountId:N}:{item.CurrencyCode}:{D(item.OpeningDebit)}:{D(item.OpeningCredit)}:{D(item.BaseBookSignedBalance)}:{D(item.OpeningAdjustment)}"))));
        foreach (var legacyBookType in FingerprintTypeCandidates(book.BookType))
        foreach (var legacySourceType in FingerprintTypeCandidates(source?.BookType))
            acceptedEvidence.Add(AccountingBookInitializationFingerprint.LegacyEvidenceV2(
                TenantId, book.Id, book.Code, legacyBookType!.Value, book.FunctionalCurrencyCode,
                mode, cutoff, cutoffPeriod.Id, cutoffPeriod.PeriodCode, cutoffPeriod.StartDate,
                cutoffPeriod.EndDate, source?.Id, source?.Code, legacySourceType,
                source?.FunctionalCurrencyCode, idempotencyKey, reason, lineEvidence));
        var acceptedReconciliation = acceptedEvidence.Select(fingerprint =>
                AccountingBookInitializationFingerprint.Reconciliation(
                    fingerprint, authority, balanceEvidence, transactionEvidence, debit, credit))
            .ToHashSet(StringComparer.Ordinal);
        return new Prepared(lines, mappings.Count, debit, credit, evidence, reconciliation, cutoffPeriod.Id,
            acceptedEvidence, acceptedReconciliation);
    }

    private static IReadOnlyList<AccountingBookType?> FingerprintTypeCandidates(AccountingBookType? type) => type switch
    {
        AccountingBookType.PrimaryFull or AccountingBookType.ParallelFull =>
            new AccountingBookType?[] { AccountingBookType.PrimaryFull, AccountingBookType.ParallelFull },
        null => new AccountingBookType?[] { null },
        _ => new AccountingBookType?[] { type }
    };

    private static void ValidateModeForBook(AccountingBook book, AccountingBookInitializationMode mode)
    {
        if (book.BookType == AccountingBookType.Delta && mode != AccountingBookInitializationMode.IndependentOpeningBalances)
            throw new InvalidOperationException("DELTA_INITIALIZATION_MODE_INVALID: A Delta book must initialize as an independent adjustment-only layer.");
        if (book.BookType != AccountingBookType.ParallelFull) return;
        var expected = book.ParallelOpeningMode switch
        {
            ParallelBookOpeningMode.ZeroOpening => AccountingBookInitializationMode.IndependentOpeningBalances,
            ParallelBookOpeningMode.GovernedOpeningConversion => AccountingBookInitializationMode.BaseBookCopyAtCutoff,
            ParallelBookOpeningMode.HistoricalReplay => AccountingBookInitializationMode.IndependentOpeningBalances,
            _ => throw new InvalidOperationException("PARALLEL_OPENING_MODE_REQUIRED: Configure a governed Parallel opening mode before initialization.")
        };
        if (mode != expected)
            throw new InvalidOperationException($"PARALLEL_INITIALIZATION_MODE_INVALID: {book.ParallelOpeningMode} requires {expected} evidence.");
    }

    private static void ValidateParallelCutoffContinuity(AccountingBook book, DateTime cutoff)
    {
        if (book.BookType != AccountingBookType.ParallelFull) return;
        if (!book.ReplicationStartDate.HasValue)
            throw new InvalidOperationException("PARALLEL_REPLICATION_START_REQUIRED: A Parallel replication start date is required.");
        if (cutoff.Date.AddDays(1) != book.ReplicationStartDate.Value.Date)
            throw new InvalidOperationException(
                $"PARALLEL_OPENING_CUTOFF_GAP: The opening cutoff must be the day immediately before the replication start date ({book.ReplicationStartDate:dd/MM/yyyy}) so no Primary activity is omitted or duplicated.");
    }

    private sealed record TranslationEvidence(Guid? ExchangeRateId, decimal? Multiplier, DateTime? RateDate,
        string? RateType, string? RateSource, decimal TranslatedSignedBalance);

    private async Task<Dictionary<Guid, TranslationEvidence>> ResolveTranslationEvidenceAsync(
        AccountingBook book, AccountingBook? source, DateTime cutoff,
        IReadOnlyCollection<AccountAccountingBook> mappings, IReadOnlyDictionary<Guid, decimal> sourceBalances,
        CancellationToken ct)
    {
        if (book.BookType != AccountingBookType.ParallelFull
            || book.ParallelOpeningMode != ParallelBookOpeningMode.GovernedOpeningConversion)
            return new Dictionary<Guid, TranslationEvidence>();
        if (source == null || book.BaseAccountingBookId != source.Id)
            throw new InvalidOperationException("PARALLEL_OPENING_SOURCE_INVALID: Governed conversion must use the configured Primary base book.");
        if (!book.ParallelTranslationMethod.HasValue)
            throw new InvalidOperationException("PARALLEL_TRANSLATION_METHOD_REQUIRED: Select a governed opening translation method.");
        var sourceCurrency = source.FunctionalCurrencyCode
            ?? throw new InvalidOperationException("PARALLEL_SOURCE_CURRENCY_REQUIRED: The Primary source currency is unavailable.");
        var targetCurrency = book.FunctionalCurrencyCode
            ?? throw new InvalidOperationException("PARALLEL_CURRENCY_REQUIRED: The Parallel currency is unavailable.");
        if (string.Equals(sourceCurrency, targetCurrency, StringComparison.Ordinal))
            throw new InvalidOperationException("PARALLEL_FOREIGN_CURRENCY_REQUIRED: A Parallel book must use a currency different from Primary.");

        var candidates = await _db.ExchangeRates.AsNoTracking().Where(rate => rate.TenantId == TenantId && !rate.IsDeleted
            && rate.BaseCurrencyCode == sourceCurrency && rate.TargetCurrencyCode == targetCurrency
            && rate.QuoteSide == ExchangeRateQuoteSide.Mid && rate.EffectiveDate.Date <= cutoff.Date
            && (rate.ApprovalStatus == RateApprovalStatus.Approved || rate.ApprovalStatus == RateApprovalStatus.AutoApproved))
            .ToListAsync(ct);
        ExchangeRate SelectRate(DateTime date, params ExchangeRateType[] preferredTypes)
        {
            var eligible = candidates.Where(rate => rate.EffectiveDate.Date <= date.Date).ToList();
            foreach (var type in preferredTypes)
            {
                var selected = eligible.Where(rate => rate.RateType == type)
                    .OrderByDescending(rate => rate.EffectiveDate).ThenByDescending(rate => rate.Priority).FirstOrDefault();
                if (selected != null) return selected;
            }
            throw new InvalidOperationException($"PARALLEL_OPENING_RATE_MISSING: No approved {sourceCurrency}/{targetCurrency} rate is available on or before {date:yyyy-MM-dd} for {string.Join(", ", preferredTypes)}.");
        }

        ExchangeRate? single = null;
        if (book.ParallelTranslationMethod == ParallelBookTranslationMethod.SingleApprovedRate)
            single = SelectRate(cutoff, ExchangeRateType.YearEnd, ExchangeRateType.QuarterEnd,
                ExchangeRateType.MonthEnd, ExchangeRateType.Daily, ExchangeRateType.Spot, ExchangeRateType.Fixed);
        var earliestTransactions = book.ParallelTranslationMethod == ParallelBookTranslationMethod.ClassificationDriven
            ? await _db.AccountTransactions.AsNoTracking().Where(line => line.TenantId == TenantId && !line.IsDeleted
                && line.AccountingBookId == source.Id && line.PostingStatus == "Posted" && line.TransactionDate.Date <= cutoff.Date)
                .GroupBy(line => line.AccountId).Select(group => new { AccountId = group.Key, Date = group.Min(line => line.TransactionDate) })
                .ToDictionaryAsync(item => item.AccountId, item => item.Date, ct)
            : new Dictionary<Guid, DateTime>();

        var result = new Dictionary<Guid, TranslationEvidence>();
        foreach (var mapping in mappings)
        {
            var sourceBalance = sourceBalances.GetValueOrDefault(mapping.AccountId);
            if (sourceBalance == 0m || mapping.AccountId == book.CurrencyTranslationReserveAccountId
                || mapping.AccountId == book.CurrencyRoundingAccountId)
                continue;
            if (book.ParallelTranslationMethod == ParallelBookTranslationMethod.ClassificationDriven
                && mapping.Account.AccountType == AccountType.Equity
                && !earliestTransactions.ContainsKey(mapping.AccountId))
                throw new InvalidOperationException(
                    $"PARALLEL_EQUITY_HISTORICAL_RATE_REQUIRED: Account {mapping.Account.AccountNumber} has a non-zero equity balance but no posted origin date from which to select its historical rate.");
            var rate = single ?? mapping.Account.AccountType switch
            {
                AccountType.Asset or AccountType.Liability => SelectRate(cutoff, ExchangeRateType.YearEnd,
                    ExchangeRateType.QuarterEnd, ExchangeRateType.MonthEnd, ExchangeRateType.Daily, ExchangeRateType.Spot),
                AccountType.Revenue or AccountType.Expense => SelectRate(cutoff, ExchangeRateType.Average, ExchangeRateType.Daily),
                AccountType.Equity => SelectRate(earliestTransactions[mapping.AccountId],
                    ExchangeRateType.Fixed, ExchangeRateType.Daily, ExchangeRateType.Spot),
                _ => SelectRate(cutoff, ExchangeRateType.MonthEnd, ExchangeRateType.Daily)
            };
            var multiplier = rate.Rate;
            if (multiplier <= 0m)
                throw new InvalidOperationException($"PARALLEL_OPENING_RATE_INVALID: Exchange rate {rate.Id} has no positive source-to-target multiplier.");
            result[mapping.AccountId] = new TranslationEvidence(rate.Id, multiplier, rate.EffectiveDate.Date,
                rate.RateType.ToString(), rate.RateSource,
                decimal.Round(sourceBalance * multiplier, 2, MidpointRounding.AwayFromZero));
        }
        var residual = result.Values.Sum(item => item.TranslatedSignedBalance);
        if (residual != 0m)
        {
            if (!book.CurrencyTranslationReserveAccountId.HasValue
                || mappings.All(item => item.AccountId != book.CurrencyTranslationReserveAccountId.Value))
                throw new InvalidOperationException("PARALLEL_CTA_ACCOUNT_REQUIRED: A protected translation-reserve account is required before governed conversion.");
            result[book.CurrencyTranslationReserveAccountId.Value] = new TranslationEvidence(null, null, null,
                null, null, -residual);
        }
        return result;
    }

    private async Task<AccountingBook?> ValidateSourceAsync(AccountingBook book, AccountingBookInitializationMode mode, Guid? sourceId, CancellationToken ct)
    {
        if (mode == AccountingBookInitializationMode.IndependentOpeningBalances)
        { if (sourceId.HasValue) throw new InvalidOperationException("Independent initialization cannot specify a source book."); return null; }
        if (!sourceId.HasValue || sourceId == book.Id) throw new InvalidOperationException("A distinct same-tenant source book is required for this initialization mode.");
        var source = await _db.AccountingBooks.SingleOrDefaultAsync(item => item.Id == sourceId && item.TenantId == TenantId && !item.IsDeleted, ct)
            ?? throw new InvalidOperationException("Initialization source book was not found for this tenant.");
        if (source.BookType == AccountingBookType.Delta || source.LifecycleStatus != AccountingBookLifecycleStatus.Active
            || !source.IsActive || !source.AllowsPosting || !IsCanonicalBookCode(source.Code)
            || source.FunctionalCurrencyCode is not { Length: 3 } currency || currency.Any(character => character is < 'A' or > 'Z'))
            throw new InvalidOperationException("Initialization source must remain a canonical active and postable full accounting book.");
        if (book.BookType == AccountingBookType.ParallelFull)
        {
            if (book.BaseAccountingBookId != source.Id || source.BookType != AccountingBookType.PrimaryFull
                || string.Equals(source.FunctionalCurrencyCode, book.FunctionalCurrencyCode, StringComparison.Ordinal))
                throw new InvalidOperationException("PARALLEL_OPENING_SOURCE_INVALID: The source must be the configured Primary book in a different currency.");
        }
        else if (!string.Equals(source.FunctionalCurrencyCode, book.FunctionalCurrencyCode, StringComparison.Ordinal))
            throw new InvalidOperationException("Initialization source and target currencies must agree unless the target is a governed Parallel book.");
        return source;
    }
    private static bool IsCanonicalBookCode(string? value) => value is { Length: > 0 and <= 20 }
        && !PseudoBookCodes.Contains(value) && char.IsAsciiLetter(value[0])
        && value.All(character => char.IsAsciiLetterUpper(character) || char.IsAsciiDigit(character) || character == '_');
    private IQueryable<AccountingBookInitialization> Query() => _db.AccountingBookInitializations.Include(item => item.AccountingBook)
        .Include(item => item.SourceAccountingBook).Include(item => item.CutoffFiscalPeriod)
        .Include(item => item.Lines).ThenInclude(line => line.Account)
        .Where(item => item.TenantId == TenantId && !item.IsDeleted);
    private async Task<AccountingBook> RequireBookAsync(Guid id, CancellationToken ct) => await _db.AccountingBooks.SingleOrDefaultAsync(item => item.Id == id && item.TenantId == TenantId && !item.IsDeleted, ct)
        ?? throw new KeyNotFoundException("Accounting book was not found.");
    private static AccountingBookInitializationLineDto MapLine(AccountingBookInitializationLine line) => new() { AccountId = line.AccountId,
        AccountNumber = line.Account?.AccountNumber ?? line.AccountId.ToString(), AccountName = line.Account?.AccountName ?? "Account name unavailable",
        AccountType = line.Account?.AccountType.ToString() ?? string.Empty, CurrencyCode = line.CurrencyCode,
        OpeningDebit = line.OpeningDebit, OpeningCredit = line.OpeningCredit, BaseBookSignedBalance = line.BaseBookSignedBalance,
        OpeningAdjustment = line.OpeningAdjustment, TranslationExchangeRateId = line.TranslationExchangeRateId,
        TranslationRate = line.TranslationRate, TranslationRateDate = line.TranslationRateDate,
        TranslationRateType = line.TranslationRateType, TranslationRateSource = line.TranslationRateSource };
    private AccountingBookInitializationLine NewLine(AccountingBookInitializationLineDto line) => new() { TenantId = TenantId, AccountId = line.AccountId,
        CurrencyCode = line.CurrencyCode, OpeningDebit = line.OpeningDebit, OpeningCredit = line.OpeningCredit,
        BaseBookSignedBalance = line.BaseBookSignedBalance, OpeningAdjustment = line.OpeningAdjustment,
        TranslationExchangeRateId = line.TranslationExchangeRateId, TranslationRate = line.TranslationRate,
        TranslationRateDate = line.TranslationRateDate, TranslationRateType = line.TranslationRateType,
        TranslationRateSource = line.TranslationRateSource,
        CreatedAt = DateTime.UtcNow, CreatedBy = ActorName() };
    private static AccountingBookInitializationDto Map(AccountingBookInitialization item) => new() { Id = item.Id, AccountingBookId = item.AccountingBookId,
        AccountingBookCode = item.AccountingBook.Code, Version = item.Version, SupersedesInitializationId = item.SupersedesInitializationId,
        Mode = item.Mode.ToString(), Status = item.InitializationStatus.ToString(), CutoffDate = item.CutoffDate,
        CutoffFiscalPeriodId = item.CutoffFiscalPeriodId, CutoffFiscalPeriodCode = item.CutoffFiscalPeriod?.PeriodCode ?? string.Empty,
        SourceAccountingBookId = item.SourceAccountingBookId, SourceAccountingBookCode = item.SourceAccountingBook?.Code,
        TranslationMethod = item.TranslationMethod?.ToString(), IdempotencyKey = item.IdempotencyKey,
        Reason = item.Reason, TotalDebits = item.TotalDebits, TotalCredits = item.TotalCredits, RequiredAccountCount = item.RequiredAccountCount,
        CoveredAccountCount = item.CoveredAccountCount, IsBalanced = item.TotalDebits == item.TotalCredits,
        IsCoverageComplete = item.RequiredAccountCount == item.CoveredAccountCount, EvidenceFingerprint = item.EvidenceFingerprint,
        ReconciliationFingerprint = item.ReconciliationFingerprint, PreparedByUserId = item.PreparedByUserId, PreparedAtUtc = item.PreparedAtUtc,
        ApprovedByUserId = item.ApprovedByUserId, ApprovedAtUtc = item.ApprovedAtUtc,
        RejectedByUserId = item.RejectedByUserId, RejectedAtUtc = item.RejectedAtUtc,
        DecidedByUserId = item.DecidedByUserId, DecidedAtUtc = item.DecidedAtUtc, DecisionReason = item.DecisionReason,
        RowVersion = item.RowVersion.Length == 0 ? string.Empty : Convert.ToBase64String(item.RowVersion), Lines = item.Lines.OrderBy(line => line.AccountId).Select(MapLine).ToList() };
    private async Task<AccountingBookInitializationDto> MapAsync(AccountingBookInitialization item, CancellationToken ct)
    {
        var dto = Map(item);
        var actorIds = new[] { item.PreparedByUserId, item.ApprovedByUserId, item.RejectedByUserId, item.DecidedByUserId }
            .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToArray();
        if (actorIds.Length == 0) return dto;

        var actors = await _db.Users.AsNoTracking()
            .Where(user => user.TenantId == TenantId && actorIds.Contains(user.Id))
            .Select(user => new { user.Id, user.FirstName, user.LastName, user.UserName })
            .ToListAsync(ct);
        var names = actors.ToDictionary(user => user.Id, user =>
        {
            var fullName = $"{user.FirstName} {user.LastName}".Trim();
            return string.IsNullOrWhiteSpace(fullName) ? user.UserName ?? "Unknown user" : fullName;
        });
        dto.PreparedByName = names.GetValueOrDefault(item.PreparedByUserId)
            ?? FinanceBaselineProvisioningSeeder.AuthorityName(item.PreparedByUserId);
        dto.ApprovedByName = item.ApprovedByUserId.HasValue
            ? names.GetValueOrDefault(item.ApprovedByUserId.Value) ?? FinanceBaselineProvisioningSeeder.AuthorityName(item.ApprovedByUserId.Value)
            : null;
        dto.RejectedByName = item.RejectedByUserId.HasValue
            ? names.GetValueOrDefault(item.RejectedByUserId.Value) ?? FinanceBaselineProvisioningSeeder.AuthorityName(item.RejectedByUserId.Value)
            : null;
        dto.DecidedByName = item.DecidedByUserId.HasValue
            ? names.GetValueOrDefault(item.DecidedByUserId.Value) ?? FinanceBaselineProvisioningSeeder.AuthorityName(item.DecidedByUserId.Value)
            : null;
        return dto;
    }
    private async Task AuditAsync(string type, AccountingBookInitialization entity, string reason, CancellationToken ct) => await _audit.RecordAsync(new FinanceAuditEventDto
    { TenantId = TenantId, EventType = type, SourceModule = "GL", SourceDocumentType = WorkflowEntityType, SourceDocumentId = entity.Id,
        WorkflowInstanceId = entity.WorkflowInstanceId, Resource = "Finance.AccountingBookInitialization", ResourceId = entity.Id.ToString(), AfterValues = new
        { entity.AccountingBookId, Mode = entity.Mode.ToString(), Status = entity.InitializationStatus.ToString(), entity.CutoffDate, entity.SourceAccountingBookId,
          entity.IdempotencyKey, entity.EvidenceFingerprint, entity.ReconciliationFingerprint, entity.TotalDebits, entity.TotalCredits,
          entity.RequiredAccountCount, entity.CoveredAccountCount, entity.PreparedByUserId, entity.ApprovedByUserId,
          entity.RejectedByUserId, entity.DecidedByUserId }, Reason = reason.Trim() }, ct);
    private void ApplyRowVersion(AccountingBookInitialization entity, string? encoded)
    { if (string.IsNullOrWhiteSpace(encoded)) throw new InvalidOperationException("Row version is required."); byte[] value;
      try { value = Convert.FromBase64String(encoded); } catch (FormatException) { throw new InvalidOperationException("Row version is invalid."); }
      if (value.Length == 0 || entity.RowVersion.Length > 0 && !entity.RowVersion.SequenceEqual(value)) throw new DbUpdateConcurrencyException("Book initialization changed after it was loaded.");
      _db.Entry(entity).Property(item => item.RowVersion).OriginalValue = value; }
    private Guid Actor() => Guid.TryParse(_currentUser.UserId, out var id) && id != Guid.Empty ? id : throw new UnauthorizedAccessException("An authenticated Finance user is required.");
    private string ActorName() => _currentUser.UserName ?? "system";
    private Task<T> AtomicAsync<T>(Func<Task<T>> action, CancellationToken ct) => !_db.Database.IsRelational() || _db.Database.CurrentTransaction != null ? action() : _db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
    { await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct); try { var result = await action(); await tx.CommitAsync(ct); return result; }
      catch { await tx.RollbackAsync(CancellationToken.None); _db.ChangeTracker.Clear(); throw; } });
    private static string D(decimal value) => AccountingBookInitializationFingerprint.Decimal(value);
    private sealed record Prepared(IReadOnlyList<AccountingBookInitializationLineDto> Lines, int RequiredCount, decimal TotalDebits,
        decimal TotalCredits, string EvidenceFingerprint, string ReconciliationFingerprint, Guid CutoffFiscalPeriodId,
        IReadOnlySet<string> AcceptedEvidenceFingerprints, IReadOnlySet<string> AcceptedReconciliationFingerprints);
}
