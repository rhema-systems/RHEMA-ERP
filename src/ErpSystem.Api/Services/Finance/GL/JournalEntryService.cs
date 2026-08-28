using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Data;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Services;
using ErpSystem.Api.Services.Finance;
using ErpSystem.Core.Interfaces.Numbering;
using ErpSystem.Shared;

namespace ErpSystem.Api.Services.Finance.GL
{
    public class JournalEntryService : IJournalEntryService
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;
        private readonly IGeneralLedgerService _generalLedgerService;
        private readonly IAuditLogService _auditLogService;
        private readonly INotificationService _notificationService;
        private readonly IAccountingBookService _accountingBookService;
        private readonly IFinancePostingEngine? _financePostingEngine;
        private readonly IFinanceAuditService? _financeAuditService;
        private readonly IDocumentNumberingService? _documentNumberingService;
        private readonly IFinanceBudgetControlService? _budgetControl;
        private readonly FinanceDimensionAdministrationService? _financeDimensions;
        private const string AllActiveBooksCode = "ALL_ACTIVE_BOOKS";

        public JournalEntryService(
            ApplicationDbContext context,
            ICurrentUserService currentUserService,
            IGeneralLedgerService generalLedgerService,
            IAuditLogService auditLogService,
            INotificationService notificationService,
            IAccountingBookService accountingBookService,
            IFinancePostingEngine? financePostingEngine = null,
            IFinanceAuditService? financeAuditService = null,
            IDocumentNumberingService? documentNumberingService = null,
            IFinanceBudgetControlService? budgetControl = null,
            FinanceDimensionAdministrationService? financeDimensions = null)
        {
            _context = context;
            _currentUserService = currentUserService;
            _generalLedgerService = generalLedgerService;
            _auditLogService = auditLogService;
            _notificationService = notificationService;
            _accountingBookService = accountingBookService;
            _financePostingEngine = financePostingEngine;
            _financeAuditService = financeAuditService;
            _documentNumberingService = documentNumberingService;
            _budgetControl = budgetControl;
            _financeDimensions = financeDimensions;
        }

        private Guid TenantId => _currentUserService.GetRequiredFinanceTenantId();

        public async Task<IReadOnlyList<JournalEntryDto>> GetJournalEntriesAsync(CancellationToken cancellationToken = default)
        {
            var tenantId = TenantId;
            var entries = await _context.JournalEntries
                .Include(j => j.Transactions)
                .ThenInclude(t => t.Account)
                .Include(j => j.Transactions)
                .ThenInclude(t => t.FinanceDimensionSet)
                .ThenInclude(set => set!.Items)
                .Include(j => j.Attachments)
                .Include(j => j.JournalBatchItem)
                .ThenInclude(i => i!.JournalBatch)
                .Where(j => j.TenantId == tenantId && !j.IsDeleted)
                .OrderByDescending(j => j.EntryDate)
                .ToListAsync(cancellationToken);

            return entries.Select(MapToDto).ToList();
        }

        public async Task<JournalEntryDto?> GetJournalEntryByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var tenantId = TenantId;
            var entry = await _context.JournalEntries
                .Include(j => j.Transactions)
                .ThenInclude(t => t.Account)
                .Include(j => j.Transactions)
                .ThenInclude(t => t.FinanceDimensionSet)
                .ThenInclude(set => set!.Items)
                .Include(j => j.Attachments)
                .Include(j => j.JournalBatchItem)
                .ThenInclude(i => i!.JournalBatch)
                .FirstOrDefaultAsync(j => j.TenantId == tenantId && j.Id == id && !j.IsDeleted, cancellationToken);

            return entry == null ? null : MapToDto(entry);
        }

        public async Task<JournalEntryDto?> GetJournalEntryByNumberAsync(string journalNumber, CancellationToken cancellationToken = default)
        {
            var tenantId = TenantId;
            var entry = await _context.JournalEntries
                .Include(j => j.Transactions)
                .ThenInclude(t => t.Account)
                .Include(j => j.Transactions)
                .ThenInclude(t => t.FinanceDimensionSet)
                .ThenInclude(set => set!.Items)
                .Include(j => j.Attachments)
                .Include(j => j.JournalBatchItem)
                .ThenInclude(i => i!.JournalBatch)
                .FirstOrDefaultAsync(j => j.TenantId == tenantId && j.JournalEntryNumber == journalNumber, cancellationToken);

            return entry == null ? null : MapToDto(entry);
        }

        public async Task<IReadOnlyList<JournalEntryDto>> GetJournalEntriesByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            var tenantId = TenantId;
            var entries = await _context.JournalEntries
                .Include(j => j.Transactions)
                .ThenInclude(t => t.Account)
                .Include(j => j.Transactions)
                .ThenInclude(t => t.FinanceDimensionSet)
                .ThenInclude(set => set!.Items)
                .Include(j => j.Attachments)
                .Include(j => j.JournalBatchItem)
                .ThenInclude(i => i!.JournalBatch)
                .Where(j => j.TenantId == tenantId && !j.IsDeleted && j.EntryDate >= startDate && j.EntryDate <= endDate)
                .OrderByDescending(j => j.EntryDate)
                .ToListAsync(cancellationToken);

            return entries.Select(MapToDto).ToList();
        }

        public async Task<JournalEntryDto> CreateJournalEntryAsync(CreateJournalEntryDto dto, CancellationToken cancellationToken = default)
        {
            var tenantId = TenantId;

            // Basic validation
            if (dto.Transactions == null || !dto.Transactions.Any())
                throw new InvalidOperationException("Journal entry must have at least one transaction line.");

            var fiscalPeriodId = dto.FiscalPeriodId ?? await GetOpenFiscalPeriodIdAsync(dto.TransactionDate, tenantId);
            await EnsureFiscalPeriodOpenAsync(fiscalPeriodId, tenantId, dto.TransactionDate, cancellationToken);
            var bookClassification = string.IsNullOrWhiteSpace(dto.BookClassification) ? "IFRS" : dto.BookClassification.Trim();
            Guid.TryParse(_currentUserService.UserId, out var currentUserId);

            if (IsAllActiveBooks(bookClassification) && !IsOpeningBalanceJournalType(dto.JournalType))
                throw new InvalidOperationException("All Active Books can only be used for Opening Balance journal entries.");

            var transactions = dto.Transactions.ToList();
            await ApplyOpeningBalanceAutoRoutingAsync(
                dto.JournalType,
                transactions,
                tenantId,
                dto.Reference,
                cancellationToken);

            var debitSum = transactions
                .Where(t => string.Equals(t.TransactionType, "Debit", StringComparison.OrdinalIgnoreCase))
                .Sum(t => t.Amount);
            var creditSum = transactions
                .Where(t => string.Equals(t.TransactionType, "Credit", StringComparison.OrdinalIgnoreCase))
                .Sum(t => t.Amount);

            if (debitSum != creditSum)
                throw new InvalidOperationException($"Journal entry is not balanced. Total Debit: {debitSum}, Total Credit: {creditSum}");

            var currencyMetadata = await ResolveJournalCurrencyMetadataAsync(transactions, tenantId, cancellationToken);
            var journalEntryNumber = await ResolveJournalEntryNumberAsync(dto.JournalNumber, dto.TransactionDate, cancellationToken);
            var journalNumberExists = await _context.JournalEntries
                .AnyAsync(j => j.TenantId == tenantId && !j.IsDeleted && j.JournalEntryNumber == journalEntryNumber, cancellationToken);
            if (journalNumberExists)
            {
                throw new InvalidOperationException($"Journal entry '{journalEntryNumber}' already exists.");
            }

            // Create Entity
            var journalEntry = new JournalEntry
            {
                Id = Guid.NewGuid(),
                JournalEntryNumber = journalEntryNumber,
                EntryDate = dto.TransactionDate,
                JournalType = string.IsNullOrWhiteSpace(dto.JournalType) ? "General" : dto.JournalType.Trim(),
                Description = dto.Description ?? string.Empty,
                ReferenceNumber = dto.Reference,
                SourceModule = dto.SourceModule,
                SourceDocumentId = dto.SourceDocumentId,
                SourceDocumentType = dto.SourceDocumentType,
                Notes = dto.Notes,
                TotalDebitAmount = debitSum,
                TotalCreditAmount = creditSum,
                PostingStatus = "Draft", // Created as Draft
                IsBalanced = true,
                IsMultiCurrency = currencyMetadata.IsMultiCurrency,
                PrimaryCurrency = currencyMetadata.PrimaryCurrency,
                TenantId = tenantId,
                BookClassification = bookClassification,
                FiscalPeriodId = fiscalPeriodId,
                CreatedById = currentUserId == Guid.Empty ? null : currentUserId,
                CreatedBy = _currentUserService.UserName
            };

            // Process Transactions
            int lineNum = 1;
            foreach (var txnDto in transactions)
            {
                if (txnDto.Amount <= 0)
                    throw new InvalidOperationException("Transaction line amounts must be greater than zero.");

                var isDebit = string.Equals(txnDto.TransactionType, "Debit", StringComparison.OrdinalIgnoreCase);
                var isCredit = string.Equals(txnDto.TransactionType, "Credit", StringComparison.OrdinalIgnoreCase);
                if (!isDebit && !isCredit)
                    throw new InvalidOperationException("Transaction type must be either Debit or Credit.");

                await GetValidManualPostingAccountAsync(tenantId, txnDto.AccountId, cancellationToken);
                var dimensionSet = await ResolveManualJournalDimensionsAsync(
                    txnDto.AccountId, dto.TransactionDate, txnDto.Dimensions, cancellationToken);

                var transaction = new AccountTransaction
                {
                    Id = Guid.NewGuid(),
                    JournalEntryId = journalEntry.Id,
                    AccountId = txnDto.AccountId,
                    Description = txnDto.Description,
                    SourceReferenceNumber = txnDto.Reference,
                    DebitAmount = isDebit ? txnDto.Amount : 0,
                    CreditAmount = isCredit ? txnDto.Amount : 0,
                    TransactionDate = dto.TransactionDate,
                    LineNumber = lineNum++,
                    TransactionCurrency = txnDto.CurrencyCode,
                    ExchangeRate = txnDto.ExchangeRate,
                    ForeignCurrencyAmount = txnDto.ForeignAmount,
                    TenantId = tenantId,
                    FiscalPeriodId = journalEntry.FiscalPeriodId,
                    BookClassification = bookClassification,
                    FinanceDimensionSetId = dimensionSet?.Id,
                    FinanceDimensionSet = dimensionSet
                };
                
                _context.AccountTransactions.Add(transaction);
                journalEntry.Transactions.Add(transaction);
            }

            _context.JournalEntries.Add(journalEntry);
            await PersistJournalMutationWithAuditAsync(
                () => LogJournalAuditAsync(FinanceAuditEvents.JournalCreated, journalEntry, null, BuildJournalAuditSnapshot(journalEntry)),
                cancellationToken);

            var createdEntry = await LoadJournalEntryAsync(journalEntry.Id, cancellationToken)
                ?? throw new InvalidOperationException("Journal entry was saved but could not be reloaded.");

            return MapToDto(createdEntry);
        }

        public async Task<JournalEntryDto> UpdateJournalEntryAsync(Guid id, UpdateJournalEntryDto dto, CancellationToken cancellationToken = default)
        {
            var currentTenantId = TenantId;
            var entry = await _context.JournalEntries
                .Include(j => j.Transactions)
                .Include(j => j.Attachments)
                .FirstOrDefaultAsync(j => j.TenantId == currentTenantId && j.Id == id && !j.IsDeleted, cancellationToken);

            if (entry == null) throw new ArgumentException($"Journal Entry {id} not found.");
            if (entry.PostingStatus != "Draft") throw new InvalidOperationException("Only Draft journal entries can be updated.");

            var before = BuildJournalAuditSnapshot(entry);

            // Update Header
            var transactionDate = dto.TransactionDate ?? entry.EntryDate;
            if (dto.TransactionDate.HasValue)
            {
                entry.EntryDate = transactionDate;
                entry.FiscalPeriodId = await GetOpenFiscalPeriodIdAsync(transactionDate, entry.TenantId);
            }

            await EnsureFiscalPeriodOpenAsync(entry.FiscalPeriodId, entry.TenantId, transactionDate, cancellationToken);

            if (dto.Description != null) entry.Description = dto.Description;
            if (dto.Reference != null) entry.ReferenceNumber = dto.Reference;
            if (!string.IsNullOrWhiteSpace(dto.BookClassification))
            {
                if (IsAllActiveBooks(dto.BookClassification) && !IsOpeningBalanceJournalType(entry.JournalType))
                    throw new InvalidOperationException("All Active Books can only be used for Opening Balance journal entries.");

                entry.BookClassification = dto.BookClassification.Trim();
            }

            if (dto.Transactions != null)
            {
                if (!dto.Transactions.Any())
                    throw new InvalidOperationException("Journal entry must have at least one transaction line.");

                var transactionDtos = dto.Transactions.ToList();
                await ApplyOpeningBalanceAutoRoutingAsync(
                    entry.JournalType,
                    transactionDtos,
                    tenantId: entry.TenantId,
                    referenceNumber: dto.Reference ?? entry.ReferenceNumber,
                    cancellationToken);

                var debitSum = transactionDtos
                    .Where(t => string.Equals(t.TransactionType, "Debit", StringComparison.OrdinalIgnoreCase))
                    .Sum(t => t.Amount);
                var creditSum = transactionDtos
                    .Where(t => string.Equals(t.TransactionType, "Credit", StringComparison.OrdinalIgnoreCase))
                    .Sum(t => t.Amount);

                if (debitSum <= 0 || creditSum <= 0)
                    throw new InvalidOperationException("Journal entry must include at least one debit and one credit line.");

                if (debitSum != creditSum)
                    throw new InvalidOperationException($"Journal entry is not balanced. Total Debit: {debitSum}, Total Credit: {creditSum}");

                var tenantId = entry.TenantId;
                var fiscalPeriodId = await GetOpenFiscalPeriodIdAsync(transactionDate, tenantId);
                var bookClassification = string.IsNullOrWhiteSpace(dto.BookClassification)
                    ? (string.IsNullOrWhiteSpace(entry.BookClassification) ? "IFRS" : entry.BookClassification.Trim())
                    : dto.BookClassification.Trim();
                var currencyMetadata = await ResolveJournalCurrencyMetadataAsync(transactionDtos, tenantId, cancellationToken);

                _context.AccountTransactions.RemoveRange(entry.Transactions);
                entry.Transactions.Clear();

                var lineNum = 1;
                foreach (var txnDto in transactionDtos)
                {
                    if (txnDto.Amount <= 0)
                        throw new InvalidOperationException("Transaction line amounts must be greater than zero.");

                    var isDebit = string.Equals(txnDto.TransactionType, "Debit", StringComparison.OrdinalIgnoreCase);
                    var isCredit = string.Equals(txnDto.TransactionType, "Credit", StringComparison.OrdinalIgnoreCase);
                    if (!isDebit && !isCredit)
                        throw new InvalidOperationException("Transaction type must be either Debit or Credit.");

                    await GetValidManualPostingAccountAsync(tenantId, txnDto.AccountId, cancellationToken);
                    var dimensionSet = await ResolveManualJournalDimensionsAsync(
                        txnDto.AccountId, transactionDate, txnDto.Dimensions, cancellationToken);

                    var transaction = new AccountTransaction
                    {
                        Id = Guid.NewGuid(),
                        JournalEntryId = entry.Id,
                        AccountId = txnDto.AccountId,
                        Description = txnDto.Description,
                        SourceReferenceNumber = txnDto.Reference,
                        DebitAmount = isDebit ? txnDto.Amount : 0,
                        CreditAmount = isCredit ? txnDto.Amount : 0,
                        TransactionDate = transactionDate,
                        LineNumber = lineNum++,
                        TransactionCurrency = txnDto.CurrencyCode,
                        ExchangeRate = txnDto.ExchangeRate,
                        ForeignCurrencyAmount = txnDto.ForeignAmount,
                        TenantId = tenantId,
                        FiscalPeriodId = fiscalPeriodId,
                        BookClassification = bookClassification,
                        FinanceDimensionSetId = dimensionSet?.Id,
                        FinanceDimensionSet = dimensionSet,
                        PostingStatus = "Draft"
                    };

                    _context.AccountTransactions.Add(transaction);
                    entry.Transactions.Add(transaction);
                }

                entry.TotalDebitAmount = debitSum;
                entry.TotalCreditAmount = creditSum;
                entry.IsBalanced = true;
                entry.IsMultiCurrency = currencyMetadata.IsMultiCurrency;
                entry.PrimaryCurrency = currencyMetadata.PrimaryCurrency;
                entry.FiscalPeriodId = fiscalPeriodId;
                entry.BookClassification = bookClassification;
            }

            await PersistJournalMutationWithAuditAsync(
                () => LogJournalAuditAsync(FinanceAuditEvents.JournalUpdated, entry, before, BuildJournalAuditSnapshot(entry)),
                cancellationToken);

            if (_budgetControl != null)
            {
                await _budgetControl.InvalidateManualJournalOverridesAsync(
                    entry.Id,
                    "The source journal was edited after the budget override was evaluated.",
                    cancellationToken);
            }

            var updatedEntry = await LoadJournalEntryAsync(entry.Id, cancellationToken)
                ?? throw new InvalidOperationException("Journal entry was updated but could not be reloaded.");

            return MapToDto(updatedEntry);
        }

        public async Task DeleteJournalEntryAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var tenantId = TenantId;
            var entry = await _context.JournalEntries
                .FirstOrDefaultAsync(j => j.TenantId == tenantId && j.Id == id && !j.IsDeleted, cancellationToken);
            if (entry == null) throw new ArgumentException($"Journal Entry {id} not found.");
            if (entry.PostingStatus != "Draft") throw new InvalidOperationException("Only Draft journal entries can be deleted.");

            var before = BuildJournalAuditSnapshot(entry);

            entry.IsDeleted = true;
            entry.DeletedAt = DateTime.UtcNow;
            
            // Soft delete transactions too
            var transactions = await _context.AccountTransactions
                .Where(t => t.TenantId == tenantId && t.JournalEntryId == id)
                .ToListAsync(cancellationToken);
            foreach(var t in transactions)
            {
                t.IsDeleted = true;
            }

            await PersistJournalMutationWithAuditAsync(
                () => LogJournalAuditAsync(FinanceAuditEvents.JournalDeleted, entry, before, BuildJournalAuditSnapshot(entry)),
                cancellationToken);

            if (_budgetControl != null)
            {
                await _budgetControl.InvalidateManualJournalOverridesAsync(
                    entry.Id,
                    "The source journal was deleted after the budget override was evaluated.",
                    cancellationToken);
            }
        }

        public Task<JournalEntryDto> PostJournalEntryAsync(
            Guid id,
            CancellationToken cancellationToken = default)
            => PostJournalEntryCoreAsync(id, notifyOwner: true, cancellationToken);

        public Task<JournalEntryDto> PostJournalEntryForBatchAsync(
            Guid id,
            CancellationToken cancellationToken = default)
            => PostJournalEntryCoreAsync(id, notifyOwner: false, cancellationToken);

        public async Task NotifyJournalPostedAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            var postedEntry = await LoadJournalEntryAsync(id, cancellationToken)
                ?? throw new ArgumentException($"Journal Entry {id} not found.");
            if (!string.Equals(postedEntry.PostingStatus, "Posted", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Only a committed posted journal entry can be notified.");

            await NotifyJournalOwnerAsync(
                postedEntry,
                "Journal entry posted",
                $"{postedEntry.JournalEntryNumber} has been posted to the General Ledger.",
                "FinanceJournalPosted");
        }

        private async Task<JournalEntryDto> PostJournalEntryCoreAsync(
            Guid id,
            bool notifyOwner,
            CancellationToken cancellationToken)
        {
            if (_financePostingEngine == null)
                throw new InvalidOperationException("Finance posting engine is not configured.");

            var tenantId = TenantId;
            var entry = await _context.JournalEntries
                .Include(j => j.Transactions)
                .FirstOrDefaultAsync(j => j.TenantId == tenantId && j.Id == id && !j.IsDeleted, cancellationToken);
            
            if (entry == null) throw new ArgumentException($"Journal Entry {id} not found.");
            if (entry.PostingStatus == "Posted")
            {
                var existingPostedEntry = await LoadJournalEntryAsync(entry.Id, cancellationToken)
                    ?? throw new InvalidOperationException("Posted journal entry could not be reloaded.");
                var existingPostingEvent = await _context.FinancePostingEvents
                    .AsNoTracking()
                    .FirstOrDefaultAsync(e =>
                        e.TenantId == tenantId &&
                        e.SourceDocumentType == "ManualJournalEntry" &&
                        e.SourceDocumentId == entry.Id &&
                        e.PostingAction == "Post" &&
                        !e.IsDeleted,
                        cancellationToken);

                await LogJournalAuditAsync(
                    FinanceAuditEvents.DuplicatePostingAttempt,
                    existingPostedEntry,
                    null,
                    BuildJournalAuditSnapshot(existingPostedEntry),
                    new
                    {
                        sourceDocumentType = "ManualJournalEntry",
                        postingAction = "Post",
                        existingPostingEventId = existingPostingEvent?.Id
                    },
                    postingEventId: existingPostingEvent?.Id);
                return MapToDto(existingPostedEntry);
            }

            if (entry.PostingStatus != "Approved")
                throw new InvalidOperationException("Manual journal entries must be approved before posting.");

            if (IsAllActiveBooks(entry.BookClassification))
                throw new InvalidOperationException("All Active Books manual posting must be migrated to the posting engine before it can be used.");

            var before = BuildJournalAuditSnapshot(entry);
            FinancePostingResultDto postingResult;
            try
            {
                await ValidateManualJournalEntryAsync(entry, requireApproved: true, cancellationToken);

                if (_budgetControl == null)
                    throw new InvalidOperationException("Finance budget control is not configured for manual-journal posting.");
                var budgetReservationIds = (await _budgetControl
                    .ValidateManualJournalForPostingAsync(entry.Id, cancellationToken))
                    .ToArray();

                var functionalCurrency = await GetBaseCurrencyCodeForTenantAsync(entry.TenantId, cancellationToken);
                postingResult = await _financePostingEngine.PostAsync(
                    BuildManualJournalPostingRequest(entry, functionalCurrency, budgetReservationIds),
                    cancellationToken);
            }
            catch (Exception ex)
            {
                await LogJournalAuditAsync(
                    FinanceAuditEvents.JournalPostingFailed,
                    entry,
                    before,
                    BuildJournalAuditSnapshot(entry),
                    new { errorMessage = ex.Message, sourceDocumentType = "ManualJournalEntry", postingAction = "Post" },
                    reason: ex.Message);
                throw;
            }

            var postedEntry = await LoadJournalEntryAsync(entry.Id, cancellationToken)
                ?? throw new InvalidOperationException("Journal entry was posted but could not be reloaded.");

            await LogJournalAuditAsync(
                FinanceAuditEvents.JournalPosted,
                postedEntry,
                before,
                BuildJournalAuditSnapshot(postedEntry),
                new { postingResult.PostingEventId, postingResult.JournalEntryId, postingResult.PostingAction },
                postingEventId: postingResult.PostingEventId);
            if (notifyOwner)
            {
                await NotifyJournalOwnerAsync(
                    postedEntry,
                    "Journal entry posted",
                    $"{postedEntry.JournalEntryNumber} has been posted to the General Ledger.",
                    "FinanceJournalPosted");
            }
            return MapToDto(postedEntry);
        }

        public async Task<JournalEntryDto> ReverseJournalEntryAsync(
            Guid id,
            string reason,
            DateTime? reversalDate = null,
            CancellationToken cancellationToken = default)
        {
            if (_financePostingEngine == null)
                throw new InvalidOperationException("Finance posting engine is not configured.");

            if (string.IsNullOrWhiteSpace(reason))
                throw new InvalidOperationException("A reversal reason is required.");

            var tenantId = TenantId;
            var original = await _context.JournalEntries
                .Include(j => j.Transactions)
                .FirstOrDefaultAsync(j => j.TenantId == tenantId && j.Id == id && !j.IsDeleted, cancellationToken);

            if (original == null) throw new ArgumentException($"Journal Entry {id} not found.");
            if (original.PostingStatus != "Posted") throw new InvalidOperationException("Only posted entries can be reversed.");
            if (original.IsReversed || original.ReversalJournalEntryId.HasValue)
                throw new InvalidOperationException("Journal Entry has already been reversed.");
            if (original.OriginalJournalEntryId.HasValue)
                throw new InvalidOperationException("Reversal journal entries cannot be reversed from this action. Reverse the original business transaction instead.");

            var originalBefore = BuildJournalAuditSnapshot(original);
            var effectiveReversalDate = (reversalDate ?? DateTime.UtcNow).Date;
            var reversalFiscalPeriodId = await GetOpenFiscalPeriodIdAsync(effectiveReversalDate, original.TenantId);
            var trimmedReason = reason.Trim();

            var postingResult = await _financePostingEngine.PostAsync(
                BuildManualJournalReversalRequest(
                    original,
                    effectiveReversalDate,
                    reversalFiscalPeriodId,
                    trimmedReason,
                    await GetBaseCurrencyCodeForTenantAsync(original.TenantId, cancellationToken)),
                cancellationToken);

            var updatedOriginal = await LoadJournalEntryAsync(original.Id, cancellationToken)
                ?? throw new InvalidOperationException("Original journal entry was reversed but could not be reloaded.");

            var reversal = await LoadJournalEntryAsync(updatedOriginal.ReversalJournalEntryId!.Value, cancellationToken)
                ?? throw new InvalidOperationException("Reversal journal entry was posted but could not be reloaded.");

            await LogJournalAuditAsync(
                FinanceAuditEvents.JournalReversed,
                updatedOriginal,
                originalBefore,
                BuildJournalAuditSnapshot(updatedOriginal),
                new
                {
                    reversalJournalEntryId = reversal.Id,
                    reversalJournalNumber = reversal.JournalEntryNumber,
                    reason = trimmedReason,
                    postingResult.PostingEventId
                },
                postingEventId: postingResult.PostingEventId,
                reason: trimmedReason);
            await LogJournalAuditAsync(
                FinanceAuditEvents.JournalReversalCreated,
                reversal,
                null,
                BuildJournalAuditSnapshot(reversal),
                new
                {
                    originalJournalEntryId = original.Id,
                    originalJournalNumber = original.JournalEntryNumber,
                    reason = trimmedReason,
                    postingResult.PostingEventId
                },
                postingEventId: postingResult.PostingEventId,
                reason: trimmedReason);
            await NotifyJournalOwnerAsync(
                updatedOriginal,
                "Journal entry reversed",
                $"{updatedOriginal.JournalEntryNumber} has been reversed. Reason: {trimmedReason}",
                "FinanceJournalReversed");

            return MapToDto(reversal);
        }

        public async Task<bool> ValidateBalanceAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var tenantId = TenantId;
            var entry = await _context.JournalEntries
                .Include(j => j.Transactions)
                .FirstOrDefaultAsync(j => j.TenantId == tenantId && j.Id == id && !j.IsDeleted, cancellationToken);
            
            if (entry == null) return false;

            return entry.Transactions.Sum(t => t.DebitAmount) == entry.Transactions.Sum(t => t.CreditAmount);
        }

        public async Task<string> GenerateJournalEntryNumberAsync(CancellationToken cancellationToken = default)
        {
            return await _generalLedgerService.GenerateJournalEntryNumberAsync(cancellationToken);
        }

        private async Task<string> GenerateJournalEntryNumberAsync(DateTime entryDate, CancellationToken cancellationToken)
        {
            if (_documentNumberingService == null)
            {
                return await _generalLedgerService.GenerateJournalEntryNumberAsync(cancellationToken);
            }

            return await _documentNumberingService.GenerateAsync(
                DocumentNumberingModules.Finance,
                FinanceDocumentTypes.JournalEntry,
                TenantId,
                entryDate,
                nameof(JournalEntry),
                cancellationToken: cancellationToken);
        }

        private async Task<string> ResolveJournalEntryNumberAsync(
            string? requestedJournalNumber,
            DateTime entryDate,
            CancellationToken cancellationToken)
        {
            var manualJournalNumber = requestedJournalNumber?.Trim();
            if (string.IsNullOrWhiteSpace(manualJournalNumber))
            {
                return await GenerateJournalEntryNumberAsync(entryDate, cancellationToken);
            }

            if (_documentNumberingService == null)
            {
                return manualJournalNumber;
            }

            var definitions = await _documentNumberingService.GetDefinitionsAsync(
                DocumentNumberingModules.Finance,
                TenantId,
                cancellationToken);

            var definition = definitions
                .Where(d => d.DocumentType == FinanceDocumentTypes.JournalEntry
                    && d.IsActive
                    && d.IsDefault
                    && (d.EffectiveFrom == null || d.EffectiveFrom <= entryDate)
                    && (d.EffectiveTo == null || d.EffectiveTo >= entryDate))
                .OrderByDescending(d => d.EffectiveFrom ?? DateTime.MinValue)
                .FirstOrDefault();

            if (definition?.AllowManualEntry == true)
            {
                return manualJournalNumber;
            }

            return await GenerateJournalEntryNumberAsync(entryDate, cancellationToken);
        }
        
        // Helper to get fiscal period (Duplicated from GeneralLedgerService - should be centralized)
        private async Task<Guid> GetOpenFiscalPeriodIdAsync(DateTime date, Guid tenantId)
        {
            var targetDate = date.Date;
            var period = await _context.FiscalPeriods
                .FirstOrDefaultAsync(p =>
                    p.StartDate <= targetDate
                    && p.EndDate >= targetDate
                    && p.TenantId == tenantId
                    && p.IsOpen
                    && !p.IsClosed
                    && !p.IsLocked);
            
            if (period != null) return period.Id;

            throw new InvalidOperationException(
                $"No open fiscal period exists for journal date {targetDate:yyyy-MM-dd}. Open or seed the fiscal period before saving the journal entry.");
        }

        private async Task EnsureFiscalPeriodOpenAsync(
            Guid fiscalPeriodId,
            Guid tenantId,
            DateTime journalDate,
            CancellationToken cancellationToken)
        {
            var targetDate = journalDate.Date;
            var period = await _context.FiscalPeriods
                .FirstOrDefaultAsync(p =>
                    p.TenantId == tenantId
                    && p.Id == fiscalPeriodId
                    && !p.IsDeleted,
                    cancellationToken);

            if (period == null)
                throw new InvalidOperationException("Fiscal period was not found for this tenant.");

            if (!period.IsOpen || period.IsClosed || period.IsLocked)
                throw new InvalidOperationException($"Fiscal period '{period.PeriodName}' is not open for posting.");

            if (targetDate < period.StartDate.Date || targetDate > period.EndDate.Date)
                throw new InvalidOperationException($"Journal date {targetDate:yyyy-MM-dd} does not fall inside fiscal period '{period.PeriodName}'.");
        }

        private async Task<Account> GetValidManualPostingAccountAsync(
            Guid tenantId,
            Guid accountId,
            CancellationToken cancellationToken)
        {
            if (accountId == Guid.Empty)
                throw new InvalidOperationException("Transaction line account is required.");

            var account = await _context.Accounts
                .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == accountId && !a.IsDeleted, cancellationToken);

            if (account == null)
                throw new InvalidOperationException($"Account {accountId} was not found.");

            if (account.Status != AccountStatus.Active)
                throw new InvalidOperationException($"Account '{account.AccountNumber}' is not active.");

            if (!account.AllowDirectPosting)
                throw new InvalidOperationException($"Account '{account.AccountNumber}' does not allow direct posting.");

            if (account.IsControlAccount)
                throw new InvalidOperationException($"Direct manual posting to Control Account '{account.AccountName}' is not allowed.");

            return account;
        }

        public async Task ValidateJournalEntryReadyForSubmissionAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var tenantId = TenantId;
            var entry = await _context.JournalEntries
                .Include(j => j.Transactions)
                .FirstOrDefaultAsync(j => j.TenantId == tenantId && j.Id == id && !j.IsDeleted, cancellationToken);

            if (entry == null) throw new ArgumentException($"Journal Entry {id} not found.");
            if (entry.PostingStatus != "Draft")
                throw new InvalidOperationException($"Only Draft journal entries can be submitted for approval. Current status: {entry.PostingStatus}.");

            await ValidateManualJournalEntryAsync(entry, requireApproved: false, cancellationToken);
        }

        private async Task ValidateManualJournalEntryAsync(
            JournalEntry entry,
            bool requireApproved,
            CancellationToken cancellationToken)
        {
            if (requireApproved && !string.Equals(entry.PostingStatus, "Approved", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Manual journal entries must be approved before posting.");

            var transactions = entry.Transactions
                .Where(t => !t.IsDeleted)
                .OrderBy(t => t.LineNumber)
                .ToList();

            if (transactions.Count < 2)
                throw new InvalidOperationException("Journal entry must have at least two transaction lines.");

            decimal debit = 0;
            decimal credit = 0;
            foreach (var transaction in transactions)
            {
                if (transaction.TenantId != entry.TenantId)
                    throw new InvalidOperationException("Journal line tenant does not match the journal header tenant.");

                if (transaction.DebitAmount < 0 || transaction.CreditAmount < 0)
                    throw new InvalidOperationException("Journal line debit and credit amounts cannot be negative.");

                if ((transaction.DebitAmount > 0 && transaction.CreditAmount > 0) ||
                    (transaction.DebitAmount == 0 && transaction.CreditAmount == 0))
                    throw new InvalidOperationException("Each journal line must contain either a debit or a credit amount.");

                await GetValidManualPostingAccountAsync(entry.TenantId, transaction.AccountId, cancellationToken);
                debit += transaction.DebitAmount;
                credit += transaction.CreditAmount;
            }

            if (debit <= 0 || credit <= 0)
                throw new InvalidOperationException("Journal entry must include at least one debit and one credit line.");

            if (decimal.Round(debit, 2, MidpointRounding.AwayFromZero) != decimal.Round(credit, 2, MidpointRounding.AwayFromZero))
                throw new InvalidOperationException("Journal Entry must be balanced.");

            await EnsureFiscalPeriodOpenAsync(entry.FiscalPeriodId, entry.TenantId, entry.EntryDate, cancellationToken);
        }

        private FinancePostingRequestDto BuildManualJournalPostingRequest(
            JournalEntry entry,
            string functionalCurrency,
            IReadOnlyList<Guid> budgetReservationIds)
        {
            return new FinancePostingRequestDto
            {
                SourceModule = "GL",
                SourceDocumentType = "ManualJournalEntry",
                SourceDocumentId = entry.Id,
                SourceDocumentTenantId = entry.TenantId,
                ExistingJournalEntryId = entry.Id,
                PostingAction = "Post",
                SourceDocumentReference = entry.JournalEntryNumber,
                Description = entry.Description,
                PostingDate = entry.EntryDate,
                FiscalPeriodId = entry.FiscalPeriodId,
                JournalType = entry.JournalType,
                BookClassification = entry.BookClassification,
                FunctionalCurrencyCode = functionalCurrency,
                BudgetReservationIds = budgetReservationIds,
                Lines = entry.Transactions
                    .Where(t => !t.IsDeleted)
                    .OrderBy(t => t.LineNumber)
                    .Select(t => new FinancePostingLineDto
                    {
                        AccountId = t.AccountId,
                        Description = t.Description,
                        DebitAmount = t.DebitAmount,
                        CreditAmount = t.CreditAmount,
                        TransactionCurrency = t.TransactionCurrency,
                        ForeignCurrencyAmount = t.ForeignCurrencyAmount,
                        ExchangeRate = t.ExchangeRate,
                        ExchangeRateSource = t.ExchangeRateSource,
                        ExchangeRateDate = t.ExchangeRateDate,
                        SourceReferenceNumber = t.SourceReferenceNumber,
                        LineNumber = t.LineNumber,
                        FinanceDimensionSetId = t.FinanceDimensionSetId,
                        SegmentString = t.SegmentString,
                        Notes = t.Notes,
                        TransactionTag = t.TransactionTag
                    })
                    .ToList()
            };
        }

        private FinancePostingRequestDto BuildManualJournalReversalRequest(
            JournalEntry original,
            DateTime reversalDate,
            Guid reversalFiscalPeriodId,
            string reason,
            string functionalCurrency)
        {
            return new FinancePostingRequestDto
            {
                SourceModule = "GL",
                SourceDocumentType = "ManualJournalReversal",
                SourceDocumentId = original.Id,
                SourceDocumentTenantId = original.TenantId,
                ReversalOfJournalEntryId = original.Id,
                ReversalReason = reason,
                ReversalType = "Manual",
                PostingAction = "Reverse",
                SourceDocumentReference = $"REV-{original.JournalEntryNumber}",
                Description = $"Reversal of {original.JournalEntryNumber}: {reason}",
                PostingDate = reversalDate,
                FiscalPeriodId = reversalFiscalPeriodId,
                JournalType = "Reversing",
                BookClassification = original.BookClassification,
                FunctionalCurrencyCode = functionalCurrency,
                Lines = original.Transactions
                    .Where(t => !t.IsDeleted)
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
                        ExchangeRateSource = t.ExchangeRateSource,
                        ExchangeRateDate = t.ExchangeRateDate,
                        SourceReferenceNumber = t.SourceReferenceNumber,
                        LineNumber = t.LineNumber,
                        FinanceDimensionSetId = t.FinanceDimensionSetId,
                        SegmentString = t.SegmentString,
                        Notes = reason,
                        TransactionTag = "Reversal"
                    })
                    .ToList()
            };
        }

        private async Task<(bool IsMultiCurrency, string? PrimaryCurrency)> ResolveJournalCurrencyMetadataAsync(
            IReadOnlyCollection<CreateAccountTransactionDto> transactions,
            Guid tenantId,
            CancellationToken cancellationToken)
        {
            var baseCurrency = NormalizeCurrencyCode(await GetBaseCurrencyCodeForTenantAsync(tenantId, cancellationToken));
            var foreignCurrencies = transactions
                .Select(t => NormalizeCurrencyCode(t.CurrencyCode))
                .Where(currency => !string.IsNullOrWhiteSpace(currency))
                .Where(currency => !string.Equals(currency, baseCurrency, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return (
                foreignCurrencies.Count > 0,
                foreignCurrencies.Count == 1 ? foreignCurrencies[0] : null);
        }

        private async Task<string> GetBaseCurrencyCodeForTenantAsync(Guid tenantId, CancellationToken cancellationToken)
        {
            var financeBaseCurrency = await _context.FinanceSettings
                .AsNoTracking()
                .Where(settings => settings.TenantId == tenantId && !settings.IsDeleted)
                .Select(settings => settings.BaseCurrency)
                .FirstOrDefaultAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(financeBaseCurrency))
                return financeBaseCurrency;

            var tenantBaseCurrency = await _context.Tenants
                .AsNoTracking()
                .Where(tenant => tenant.Id == tenantId)
                .Select(tenant => tenant.BaseCurrency)
                .FirstOrDefaultAsync(cancellationToken);

            return string.IsNullOrWhiteSpace(tenantBaseCurrency) ? "GHS" : tenantBaseCurrency;
        }

        private static (string? CurrencyCode, decimal? ExchangeRate, decimal? ForeignAmount) ResolveAutoRoutingCurrencyMetadata(
            IReadOnlyCollection<CreateAccountTransactionDto> transactions,
            decimal baseAmount)
        {
            var foreignLines = transactions
                .Select(t => new
                {
                    CurrencyCode = NormalizeCurrencyCode(t.CurrencyCode),
                    ExchangeRate = t.ExchangeRate.GetValueOrDefault()
                })
                .Where(t => !string.IsNullOrWhiteSpace(t.CurrencyCode) && t.ExchangeRate > 0m)
                .Distinct()
                .ToList();

            if (foreignLines.Count != 1)
                return (null, null, null);

            var foreignLine = foreignLines[0];
            return (
                foreignLine.CurrencyCode,
                foreignLine.ExchangeRate,
                decimal.Round(baseAmount / foreignLine.ExchangeRate, 2, MidpointRounding.AwayFromZero));
        }

        private static string? NormalizeCurrencyCode(string? currencyCode)
        {
            return string.IsNullOrWhiteSpace(currencyCode)
                ? null
                : currencyCode.Trim().ToUpperInvariant();
        }

        private async Task ApplyOpeningBalanceAutoRoutingAsync(
            string? journalType,
            List<CreateAccountTransactionDto> transactions,
            Guid tenantId,
            string? referenceNumber,
            CancellationToken cancellationToken)
        {
            if (!IsOpeningBalanceJournalType(journalType))
                return;

            var debitSum = transactions
                .Where(t => string.Equals(t.TransactionType, "Debit", StringComparison.OrdinalIgnoreCase))
                .Sum(t => t.Amount);
            var creditSum = transactions
                .Where(t => string.Equals(t.TransactionType, "Credit", StringComparison.OrdinalIgnoreCase))
                .Sum(t => t.Amount);

            var difference = debitSum - creditSum;
            if (Math.Abs(difference) < 0.01m)
                return;

            var settings = await _context.FinanceSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.TenantId == tenantId && !s.IsDeleted, cancellationToken);

            if (settings?.OpeningBalanceAutoRoutingEnabled != true)
                return;

            if (!settings.MigrationClearingAccountId.HasValue)
                throw new InvalidOperationException("Opening Balance auto-routing requires Migration Clearing Account in Finance Settings.");

            var clearingAccount = await _context.Accounts
                .AsNoTracking()
                .FirstOrDefaultAsync(a =>
                    a.Id == settings.MigrationClearingAccountId.Value
                    && a.TenantId == tenantId
                    && !a.IsDeleted,
                    cancellationToken);

            if (clearingAccount == null)
                throw new InvalidOperationException("The configured Migration Clearing Account was not found.");

            var autoRouteCurrency = ResolveAutoRoutingCurrencyMetadata(transactions, Math.Abs(difference));

            transactions.Add(new CreateAccountTransactionDto
            {
                AccountId = clearingAccount.Id,
                Amount = Math.Abs(difference),
                TransactionType = difference > 0 ? "Credit" : "Debit",
                Description = "Opening balance auto-balance to Migration Clearing Account",
                Reference = string.IsNullOrWhiteSpace(referenceNumber) ? "Opening Balance" : referenceNumber.Trim(),
                CurrencyCode = autoRouteCurrency.CurrencyCode,
                ExchangeRate = autoRouteCurrency.ExchangeRate,
                ForeignAmount = autoRouteCurrency.ForeignAmount,
                LineNumber = transactions.Count == 0 ? 1 : transactions.Max(t => t.LineNumber) + 1
            });
        }

        private async Task<JournalEntryDto> PostOpeningBalanceToAllActiveBooksAsync(
            JournalEntry entry,
            object? before,
            CancellationToken cancellationToken)
        {
            if (!IsOpeningBalanceJournalType(entry.JournalType))
                throw new InvalidOperationException("All Active Books posting is only available for Opening Balance journal entries.");

            var targetBooks = (await _accountingBookService.GetBooksAsync(includeInactive: false, cancellationToken))
                .Where(book => book.IsActive && book.AllowsPosting)
                .OrderBy(book => book.SortOrder)
                .ThenBy(book => book.Name)
                .ToList();

            if (targetBooks.Count == 0)
                throw new InvalidOperationException("No active accounting books are available for posting.");

            await ValidateEntryAccountsForBooksAsync(entry, targetBooks, cancellationToken);

            var baseJournalNumber = entry.JournalEntryNumber.Trim();
            var proposedNumbers = targetBooks
                .Select(book => BuildBookJournalNumber(baseJournalNumber, book))
                .ToList();

            if (proposedNumbers.Distinct(StringComparer.OrdinalIgnoreCase).Count() != proposedNumbers.Count)
                throw new InvalidOperationException("Generated all-book journal numbers are not unique. Check active accounting book codes.");

            var conflictingNumbers = await _context.JournalEntries
                .Where(j =>
                    j.TenantId == entry.TenantId
                    && j.Id != entry.Id
                    && proposedNumbers.Contains(j.JournalEntryNumber))
                .Select(j => j.JournalEntryNumber)
                .ToListAsync(cancellationToken);

            if (conflictingNumbers.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Cannot post to all active books because generated journal number(s) already exist: {string.Join(", ", conflictingNumbers)}.");
            }

            var postingDate = DateTime.UtcNow;
            var postingUserId = Guid.TryParse(_currentUserService.UserId, out var parsedUserId)
                ? parsedUserId
                : (Guid?)null;
            var defaultBook = targetBooks.FirstOrDefault(book => book.IsDefault) ?? targetBooks[0];
            var firstBook = targetBooks[0];
            var sourceEntryId = entry.Id;
            var sourceTransactions = entry.Transactions
                .OrderBy(t => t.LineNumber)
                .ToList();
            var sourceAttachments = entry.Attachments.ToList();
            var childEntries = new List<JournalEntry>();

            entry.JournalEntryNumber = proposedNumbers[0];
            entry.BookClassification = firstBook.Code;
            entry.PostingStatus = "Posted";
            entry.PostingDate = postingDate;
            entry.PostedByUserId = postingUserId;
            entry.SourceDocumentId ??= sourceEntryId;
            entry.SourceDocumentType = "AllActiveBooksOpeningBalance";
            entry.UpdatedAt = postingDate;
            entry.UpdatedBy = _currentUserService.UserName;

            foreach (var transaction in sourceTransactions)
            {
                var account = await _context.Accounts
                    .FirstOrDefaultAsync(a => a.TenantId == entry.TenantId && a.Id == transaction.AccountId && !a.IsDeleted, cancellationToken);
                if (account == null) throw new InvalidOperationException($"Account {transaction.AccountId} not found.");

                ValidateControlAccountPosting(account, entry);
                if (firstBook.Id == defaultBook.Id)
                {
                    ApplyAccountBalanceMovement(account, transaction);
                }

                transaction.BookClassification = firstBook.Code;
                transaction.PostingStatus = "Posted";
                transaction.PostedDate = postingDate;
            }

            for (var i = 1; i < targetBooks.Count; i++)
            {
                var book = targetBooks[i];
                var childEntry = ClonePostedEntryForBook(
                    entry,
                    sourceEntryId,
                    proposedNumbers[i],
                    book.Code,
                    postingDate,
                    postingUserId);

                var lineNumber = 1;
                foreach (var sourceTransaction in sourceTransactions)
                {
                    var childTransaction = ClonePostedTransactionForBook(
                        sourceTransaction,
                        childEntry.Id,
                        book.Code,
                        lineNumber++,
                        postingDate);

                    var account = await _context.Accounts
                        .FirstOrDefaultAsync(a => a.TenantId == childEntry.TenantId && a.Id == childTransaction.AccountId && !a.IsDeleted, cancellationToken);
                    if (account == null) throw new InvalidOperationException($"Account {childTransaction.AccountId} not found.");

                    ValidateControlAccountPosting(account, childEntry);
                    if (book.Id == defaultBook.Id)
                    {
                        ApplyAccountBalanceMovement(account, childTransaction);
                    }

                    childEntry.Transactions.Add(childTransaction);
                    _context.AccountTransactions.Add(childTransaction);
                }

                foreach (var attachment in sourceAttachments)
                {
                    childEntry.Attachments.Add(new JournalEntryAttachment
                    {
                        Id = Guid.NewGuid(),
                        TenantId = childEntry.TenantId,
                        JournalEntryId = childEntry.Id,
                        FileUploadRecordId = attachment.FileUploadRecordId,
                        CreatedAt = postingDate,
                        CreatedBy = attachment.CreatedBy
                    });
                }

                childEntries.Add(childEntry);
                _context.JournalEntries.Add(childEntry);
            }

            await _context.SaveChangesAsync(cancellationToken);

            await LogJournalAuditAsync(
                "Finance.JournalEntry.PostedAllBooks",
                entry,
                before,
                BuildJournalAuditSnapshot(entry),
                new
                {
                    targetBooks = targetBooks.Select(book => new { book.Code, book.Name }).ToList(),
                    generatedJournalNumbers = proposedNumbers
                });

            foreach (var childEntry in childEntries)
            {
                await LogJournalAuditAsync(
                    "Finance.JournalEntry.AllBooksChildPosted",
                    childEntry,
                    null,
                    BuildJournalAuditSnapshot(childEntry),
                    new { sourceEntryId, sourceJournalNumber = baseJournalNumber });
            }

            await NotifyJournalOwnerAsync(
                entry,
                "Opening balance posted to all active books",
                $"{baseJournalNumber} has been posted to {targetBooks.Count} active accounting books.",
                "FinanceJournalPosted");

            return MapToDto(entry);
        }

        private async Task ValidateEntryAccountsForBooksAsync(
            JournalEntry entry,
            IReadOnlyCollection<AccountingBookDto> targetBooks,
            CancellationToken cancellationToken)
        {
            var accountIds = entry.Transactions
                .Select(t => t.AccountId)
                .Distinct()
                .ToList();

            var accounts = await _context.Accounts
                .Include(account => account.AccountingBooks)
                    .ThenInclude(mapping => mapping.AccountingBook)
                .Where(account =>
                    account.TenantId == entry.TenantId
                    && accountIds.Contains(account.Id)
                    && !account.IsDeleted)
                .ToListAsync(cancellationToken);

            foreach (var accountId in accountIds)
            {
                var account = accounts.FirstOrDefault(candidate => candidate.Id == accountId);
                if (account == null)
                    throw new InvalidOperationException($"Account {accountId} was not found.");

                foreach (var book in targetBooks)
                {
                    if (!IsAccountEligibleForBook(account, book.Code))
                    {
                        var accountLabel = string.IsNullOrWhiteSpace(account.AccountNumber)
                            ? account.AccountName
                            : $"{account.AccountNumber} - {account.AccountName}";
                        throw new InvalidOperationException(
                            $"Cannot post to all active books. Account '{accountLabel}' is not classified for {book.Name}.");
                    }
                }
            }
        }

        private static JournalEntry ClonePostedEntryForBook(
            JournalEntry source,
            Guid sourceEntryId,
            string journalNumber,
            string bookCode,
            DateTime postingDate,
            Guid? postingUserId)
        {
            return new JournalEntry
            {
                Id = Guid.NewGuid(),
                JournalEntryNumber = journalNumber,
                JournalType = source.JournalType,
                EntryDate = source.EntryDate,
                Description = source.Description,
                ReferenceNumber = source.ReferenceNumber,
                SourceModule = source.SourceModule,
                SourceDocumentId = sourceEntryId,
                SourceDocumentType = "AllActiveBooksOpeningBalance",
                TotalDebitAmount = source.TotalDebitAmount,
                TotalCreditAmount = source.TotalCreditAmount,
                BalanceDifference = 0,
                IsBalanced = source.IsBalanced,
                IsMultiCurrency = source.IsMultiCurrency,
                PrimaryCurrency = source.PrimaryCurrency,
                BookClassification = bookCode,
                FiscalPeriodId = source.FiscalPeriodId,
                PostingDate = postingDate,
                PostedByUserId = postingUserId,
                PostingStatus = "Posted",
                RequiresApproval = source.RequiresApproval,
                ApprovalStatus = source.ApprovalStatus,
                ApprovalWorkflowId = source.ApprovalWorkflowId,
                ApprovedByUserId = source.ApprovedByUserId,
                ApprovedDate = source.ApprovedDate,
                TenantId = source.TenantId,
                Notes = source.Notes,
                CreatedAt = postingDate,
                CreatedBy = source.CreatedBy,
                CreatedById = source.CreatedById,
                UpdatedAt = postingDate,
                UpdatedBy = source.UpdatedBy
            };
        }

        private static AccountTransaction ClonePostedTransactionForBook(
            AccountTransaction source,
            Guid journalEntryId,
            string bookCode,
            int lineNumber,
            DateTime postingDate)
        {
            return new AccountTransaction
            {
                Id = Guid.NewGuid(),
                JournalEntryId = journalEntryId,
                AccountId = source.AccountId,
                Description = source.Description,
                SourceModule = source.SourceModule,
                SourceDocumentId = source.SourceDocumentId,
                SourceDocumentType = source.SourceDocumentType,
                SourceReferenceNumber = source.SourceReferenceNumber,
                DebitAmount = source.DebitAmount,
                CreditAmount = source.CreditAmount,
                TransactionDate = source.TransactionDate,
                LineNumber = lineNumber,
                TransactionCurrency = source.TransactionCurrency,
                ExchangeRate = source.ExchangeRate,
                ForeignCurrencyAmount = source.ForeignCurrencyAmount,
                TenantId = source.TenantId,
                FiscalPeriodId = source.FiscalPeriodId,
                FinanceDimensionSetId = source.FinanceDimensionSetId,
                BookClassification = bookCode,
                PostingStatus = "Posted",
                PostedDate = postingDate
            };
        }

        private static bool IsAccountEligibleForBook(Account account, string bookCode)
        {
            var normalized = NormalizeBookCode(bookCode);
            var mapping = account.AccountingBooks?
                .FirstOrDefault(candidate =>
                    candidate.AccountingBook != null
                    && NormalizeBookCode(candidate.AccountingBook.Code) == normalized
                    && !candidate.IsDeleted);

            if (mapping != null)
                return mapping.IsEnabled;

            return normalized switch
            {
                "IFRS" => account.IsIFRSClassified,
                "LOCAL_STATUTORY" => account.IsBaseClassified,
                "MANAGEMENT" => account.IsLocalClassified,
                _ => true
            };
        }

        private static void ValidateControlAccountPosting(Account account, JournalEntry entry)
        {
            if (!account.IsControlAccount)
                return;

            var allowedModules = new[] { "AP", "AR", "INVENTORY", "TAX", "BANK", "SYSTEM", "POS", "PAYROLL" };
            var isSystemPosting = !string.IsNullOrEmpty(entry.SourceModule)
                && allowedModules.Contains(entry.SourceModule.ToUpperInvariant());

            if (!isSystemPosting)
            {
                throw new InvalidOperationException($"Direct manual posting to Control Account '{account.AccountName}' is not allowed.");
            }
        }

        private static void ApplyAccountBalanceMovement(Account account, AccountTransaction transaction)
        {
            if (transaction.DebitAmount > 0)
            {
                if (account.AccountType == AccountType.Asset || account.AccountType == AccountType.Expense)
                    account.Balance += transaction.DebitAmount;
                else
                    account.Balance -= transaction.DebitAmount;
            }
            else
            {
                if (account.AccountType == AccountType.Liability || account.AccountType == AccountType.Equity || account.AccountType == AccountType.Revenue)
                    account.Balance += transaction.CreditAmount;
                else
                    account.Balance -= transaction.CreditAmount;
            }
        }

        private static string BuildBookJournalNumber(string baseJournalNumber, AccountingBookDto book)
        {
            var suffix = GetBookNumberSuffix(book);
            var number = $"{baseJournalNumber}-{suffix}";
            if (number.Length > 50)
                throw new InvalidOperationException($"Generated journal number '{number}' exceeds the 50 character limit.");

            return number;
        }

        private static string GetBookNumberSuffix(AccountingBookDto book)
        {
            var normalized = NormalizeBookCode(book.Code);
            return normalized switch
            {
                "IFRS" => "IFRS",
                "LOCAL_STATUTORY" => "LOCAL",
                "MANAGEMENT" => "MGMT",
                _ => new string(normalized.Where(char.IsLetterOrDigit).ToArray())
            };
        }

        private static bool IsOpeningBalanceJournalType(string? journalType)
        {
            return string.Equals(journalType?.Trim(), "Opening Balance", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsAllActiveBooks(string? bookClassification)
        {
            return string.Equals(bookClassification?.Trim(), AllActiveBooksCode, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeBookCode(string? bookCode)
        {
            var normalized = (bookCode ?? string.Empty).Trim().ToUpperInvariant();
            return normalized switch
            {
                "BASE" or "LOCAL" => "LOCAL_STATUTORY",
                _ => normalized
            };
        }

        private Task<JournalEntry?> LoadJournalEntryAsync(Guid id, CancellationToken cancellationToken)
        {
            var tenantId = TenantId;
            return _context.JournalEntries
                .Include(j => j.Transactions)
                .ThenInclude(t => t.Account)
                .Include(j => j.Transactions)
                .ThenInclude(t => t.FinanceDimensionSet)
                .ThenInclude(set => set!.Items)
                .Include(j => j.Attachments)
                .Include(j => j.JournalBatchItem)
                .ThenInclude(i => i!.JournalBatch)
                .FirstOrDefaultAsync(j => j.TenantId == tenantId && j.Id == id && !j.IsDeleted, cancellationToken);
        }

        private static JournalEntryDto MapToDto(JournalEntry entry)
        {
            return new JournalEntryDto
            {
                Id = entry.Id,
                JournalNumber = entry.JournalEntryNumber,
                TransactionDate = entry.EntryDate,
                JournalType = entry.JournalType,
                Description = entry.Description,
                Reference = entry.ReferenceNumber,
                BookClassification = entry.BookClassification,
                TotalDebit = entry.TotalDebitAmount,
                TotalCredit = entry.TotalCreditAmount,
                Status = entry.PostingStatus,
                IsReversed = entry.IsReversed,
                ReversalJournalId = entry.ReversalJournalEntryId,
                OriginalJournalId = entry.OriginalJournalEntryId,
                ReversalDate = entry.ReversalDate,
                ReversalReason = entry.ReversalReason,
                ReversalType = entry.ReversalType,
                JournalBatchId = entry.JournalBatchItem?.JournalBatchId,
                JournalBatchNumber = entry.JournalBatchItem?.JournalBatch?.BatchNumber,
                JournalBatchItemId = entry.JournalBatchItem?.Id,
                PostedDate = entry.PostingDate,
                PostedByUserId = entry.PostedByUserId,
                RequiresApproval = entry.RequiresApproval,
                ApprovalStatus = entry.ApprovalStatus,
                ApprovedByUserId = entry.ApprovedByUserId,
                ApprovedDate = entry.ApprovedDate,
                RejectionReason = entry.RejectionReason,
                HasAttachments = entry.HasAttachments || entry.Attachments.Any(),
                AttachmentCount = entry.Attachments.Any() ? entry.Attachments.Count : entry.AttachmentCount,
                AttachmentIds = entry.Attachments.Select(a => a.FileUploadRecordId).ToList(),
                Transactions = entry.Transactions
                    .OrderBy(t => t.LineNumber)
                    .Select(MapTransactionToDto)
                    .ToList(),
                CreatedAt = entry.CreatedAt,
                CreatedById = entry.CreatedById,
                CreatedBy = entry.CreatedBy,
                UpdatedAt = entry.UpdatedAt,
                UpdatedBy = entry.UpdatedBy
            };
        }

        private static AccountTransactionDto MapTransactionToDto(AccountTransaction transaction)
        {
            var isDebit = transaction.DebitAmount > 0;

            return new AccountTransactionDto
            {
                Id = transaction.Id,
                AccountId = transaction.AccountId,
                AccountName = transaction.Account?.AccountName,
                AccountNumber = transaction.Account?.AccountNumber,
                JournalEntryId = transaction.JournalEntryId,
                Amount = isDebit ? transaction.DebitAmount : transaction.CreditAmount,
                TransactionType = isDebit ? "Debit" : "Credit",
                Description = transaction.Description,
                TransactionDate = transaction.TransactionDate,
                Reference = transaction.SourceReferenceNumber ?? string.Empty,
                BalanceAfter = 0,
                CurrencyCode = transaction.TransactionCurrency,
                ForeignAmount = transaction.ForeignCurrencyAmount,
                ExchangeRate = transaction.ExchangeRate,
                LineNumber = transaction.LineNumber,
                FinanceDimensionSetId = transaction.FinanceDimensionSetId,
                FinanceDimensionDisplayValue = transaction.FinanceDimensionSet?.DisplayValue,
                Dimensions = transaction.FinanceDimensionSet?.Items
                    .OrderBy(item => item.DimensionCodeSnapshot)
                    .Select(item => new FinanceDimensionAssignmentDto
                    {
                        DefinitionId = item.FinanceDimensionDefinitionId,
                        ValueId = item.FinanceDimensionValueId,
                        DimensionCode = item.DimensionCodeSnapshot,
                        DimensionName = item.DimensionCodeSnapshot,
                        ValueCode = item.DimensionValueCodeSnapshot,
                        ValueName = item.DimensionValueNameSnapshot
                    }).ToList() ?? []
            };
        }

        private async Task<FinanceDimensionSet?> ResolveManualJournalDimensionsAsync(
            Guid accountId,
            DateTime transactionDate,
            IReadOnlyList<FinancePostingDimensionValueDto>? dimensions,
            CancellationToken cancellationToken)
        {
            if (_financeDimensions is null)
            {
                if (dimensions is { Count: > 0 })
                    throw new InvalidOperationException("Finance dimension controls are unavailable; the journal was not saved.");
                return null;
            }

            return await _financeDimensions.ResolveManualJournalLineAsync(
                accountId, transactionDate, dimensions, cancellationToken);
        }

        public async Task UpdateApprovalStatusAsync(
            Guid id,
            string postingStatus,
            string approvalStatus,
            Guid? approvedByUserId = null,
            string? rejectionReason = null,
            CancellationToken cancellationToken = default)
        {
            var tenantId = TenantId;
            var entry = await _context.JournalEntries
                .FirstOrDefaultAsync(j => j.TenantId == tenantId && j.Id == id && !j.IsDeleted, cancellationToken);
            if (entry == null) throw new ArgumentException($"Journal Entry {id} not found.");

            var before = BuildJournalAuditSnapshot(entry);

            if (approvalStatus == "Approved" && _budgetControl != null)
                await _budgetControl.ValidateManualJournalForPostingAsync(id, cancellationToken);

            entry.PostingStatus = postingStatus;
            entry.ApprovalStatus = approvalStatus;

            if (approvedByUserId.HasValue)
            {
                entry.ApprovedByUserId = approvedByUserId;
                entry.ApprovedDate = DateTime.UtcNow;
            }

            if (rejectionReason != null)
            {
                entry.RejectionReason = rejectionReason;
            }

            if (approvalStatus == "Pending")
            {
                entry.RequiresApproval = true;
            }

            await _context.SaveChangesAsync(cancellationToken);

            if ((approvalStatus == "Rejected" || approvalStatus == "Withdrawn") && _budgetControl != null)
                await _budgetControl.ReleaseManualJournalAsync(id, rejectionReason ?? $"Journal approval status changed to {approvalStatus}.", cancellationToken);
            await LogJournalAuditAsync(
                GetApprovalAuditAction(approvalStatus),
                entry,
                before,
                BuildJournalAuditSnapshot(entry),
                new { approvedByUserId, rejectionReason },
                reason: rejectionReason);

            if (approvalStatus == "Pending")
            {
                await NotifyJournalApproversAsync(entry);
            }
            else if (approvalStatus == "Approved")
            {
                await NotifyJournalOwnerAsync(
                    entry,
                    "Journal entry approved",
                    $"{entry.JournalEntryNumber} has been approved and is ready to post.",
                    "FinanceJournalApproved");
            }
            else if (approvalStatus == "Rejected")
            {
                await NotifyJournalOwnerAsync(
                    entry,
                    "Journal entry rejected",
                    $"{entry.JournalEntryNumber} was rejected. {rejectionReason}",
                    "FinanceJournalRejected");
            }
        }

        public async Task LinkAttachmentAsync(Guid journalEntryId, Guid fileUploadRecordId, CancellationToken cancellationToken = default)
        {
            var tenantId = TenantId;

            var journal = await _context.JournalEntries
                .FirstOrDefaultAsync(j => j.TenantId == tenantId && j.Id == journalEntryId && !j.IsDeleted, cancellationToken);
            if (journal == null) throw new ArgumentException($"Journal Entry {journalEntryId} not found.");
            if (journal.PostingStatus != "Draft") throw new InvalidOperationException("Only Draft journal entries can be modified.");

            var exists = await _context.Set<JournalEntryAttachment>()
                .AnyAsync(x => x.TenantId == tenantId && x.JournalEntryId == journalEntryId && x.FileUploadRecordId == fileUploadRecordId, cancellationToken);

            if (exists) return;

            var link = new JournalEntryAttachment
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                JournalEntryId = journalEntryId,
                FileUploadRecordId = fileUploadRecordId
            };

            _context.Set<JournalEntryAttachment>().Add(link);
            await _context.SaveChangesAsync(cancellationToken);
            await LogJournalAuditAsync(
                FinanceAuditEvents.JournalAttachmentLinked,
                journalEntryId,
                null,
                new { journalEntryId, fileUploadRecordId });
        }

        public async Task UnlinkAttachmentAsync(Guid journalEntryId, Guid fileUploadRecordId, CancellationToken cancellationToken = default)
        {
            var tenantId = TenantId;

            var journal = await _context.JournalEntries
                .FirstOrDefaultAsync(j => j.TenantId == tenantId && j.Id == journalEntryId && !j.IsDeleted, cancellationToken);
            if (journal == null) throw new ArgumentException($"Journal Entry {journalEntryId} not found.");
            if (journal.PostingStatus != "Draft") throw new InvalidOperationException("Only Draft journal entries can be modified.");

            var link = await _context.Set<JournalEntryAttachment>()
                .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.JournalEntryId == journalEntryId && x.FileUploadRecordId == fileUploadRecordId, cancellationToken);

            if (link == null) return;

            _context.Set<JournalEntryAttachment>().Remove(link);
            await _context.SaveChangesAsync(cancellationToken);
            await LogJournalAuditAsync(
                FinanceAuditEvents.JournalAttachmentUnlinked,
                journalEntryId,
                new { journalEntryId, fileUploadRecordId },
                null);
        }

        public async Task<IReadOnlyList<Guid>> GetAttachmentIdsAsync(Guid journalEntryId, CancellationToken cancellationToken = default)
        {
            var tenantId = TenantId;

            return await _context.Set<JournalEntryAttachment>()
                .Where(x => x.TenantId == tenantId && x.JournalEntryId == journalEntryId)
                .Select(x => x.FileUploadRecordId)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<JournalEntryAttachmentDto>> GetAttachmentsAsync(Guid journalEntryId, CancellationToken cancellationToken = default)
        {
            var tenantId = TenantId;

            var rows = await _context.Set<JournalEntryAttachment>()
                .Include(x => x.FileUploadRecord)
                .Where(x => x.TenantId == tenantId && x.JournalEntryId == journalEntryId)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync(cancellationToken);

            return rows.Select(x => new JournalEntryAttachmentDto
            {
                Id = x.Id,
                JournalEntryId = x.JournalEntryId,
                FileId = x.FileUploadRecordId,
                FileName = x.FileUploadRecord.OriginalFileName,
                FileUrl = x.FileUploadRecord.FilePath,
                ContentType = x.FileUploadRecord.ContentType ?? "application/octet-stream",
                FileSize = x.FileUploadRecord.FileSize,
                UploadedAt = x.FileUploadRecord.CreatedAt,
                UploadedBy = x.FileUploadRecord.UploadedByUserId.ToString()
            }).ToList();
        }

        private async Task LogJournalAuditAsync(
            string action,
            JournalEntry entry,
            object? oldValues,
            object? newValues,
            object? context = null,
            Guid? postingEventId = null,
            string? reason = null,
            string? comment = null,
            Guid? workflowInstanceId = null,
            Guid? workflowApprovalId = null)
        {
            if (_financeAuditService != null)
            {
                var workflowContext = await ResolveJournalWorkflowAuditContextAsync(entry.Id, entry.TenantId);
                await _financeAuditService.RecordAsync(new FinanceAuditEventDto
                {
                    EventType = action,
                    TenantId = entry.TenantId,
                    SourceModule = string.IsNullOrWhiteSpace(entry.SourceModule) ? "GL" : entry.SourceModule,
                    SourceDocumentType = string.IsNullOrWhiteSpace(entry.SourceDocumentType) ? "ManualJournalEntry" : entry.SourceDocumentType,
                    SourceDocumentId = entry.SourceDocumentId.HasValue && entry.SourceDocumentId.Value != Guid.Empty ? entry.SourceDocumentId : entry.Id,
                    JournalEntryId = entry.Id,
                    PostingEventId = postingEventId,
                    WorkflowInstanceId = workflowInstanceId ?? workflowContext.WorkflowInstanceId,
                    WorkflowApprovalId = workflowApprovalId ?? workflowContext.WorkflowApprovalId,
                    BeforeValues = oldValues,
                    AfterValues = newValues,
                    Context = context,
                    Reason = reason,
                    Comment = comment
                });
                return;
            }

            await LogJournalAuditAsync(action, entry.Id, oldValues, newValues, context);
        }

        private async Task PersistJournalMutationWithAuditAsync(
            Func<Task> auditOperation,
            CancellationToken cancellationToken)
        {
            if (_financeAuditService != null)
            {
                await auditOperation();
                return;
            }

            await _context.SaveChangesAsync(cancellationToken);
            await auditOperation();
        }

        private async Task LogJournalAuditAsync(
            string action,
            Guid journalEntryId,
            object? oldValues,
            object? newValues,
            object? context = null)
        {
            if (!Guid.TryParse(_currentUserService.UserId, out var userId))
                return;

            var username = string.IsNullOrWhiteSpace(_currentUserService.UserName)
                ? "Unknown"
                : _currentUserService.UserName;

            await _auditLogService.LogUserActionAsync(
                userId,
                username,
                action,
                "Finance.JournalEntry",
                journalEntryId.ToString(),
                oldValues,
                new { values = newValues, context },
                _currentUserService.IpAddress,
                _currentUserService.UserAgent);
        }

        private async Task<(Guid? WorkflowInstanceId, Guid? WorkflowApprovalId)> ResolveJournalWorkflowAuditContextAsync(
            Guid journalEntryId,
            Guid tenantId)
        {
            var workflow = await _context.WorkflowInstances
                .Where(i => i.TenantId == tenantId && i.EntityId == journalEntryId && !i.IsDeleted)
                .OrderByDescending(i => i.CreatedDate)
                .Select(i => new
                {
                    WorkflowInstanceId = (Guid?)i.Id,
                    WorkflowApprovalId = i.StepInstances
                        .SelectMany(si => si.Approvals)
                        .OrderByDescending(a => a.ProcessedDate ?? a.RequestedDate)
                        .Select(a => (Guid?)a.Id)
                        .FirstOrDefault()
                })
                .FirstOrDefaultAsync();

            return workflow == null
                ? (null, null)
                : (workflow.WorkflowInstanceId, workflow.WorkflowApprovalId);
        }

        private static string GetApprovalAuditAction(string approvalStatus)
        {
            return approvalStatus switch
            {
                "Pending" => FinanceAuditEvents.JournalSubmitted,
                "Approved" => FinanceAuditEvents.JournalApproved,
                "Rejected" => FinanceAuditEvents.JournalRejected,
                "Withdrawn" => FinanceAuditEvents.JournalWithdrawn,
                "Returned" => FinanceAuditEvents.JournalReturned,
                _ => FinanceAuditEvents.JournalStatusChanged
            };
        }

        private static object BuildJournalAuditSnapshot(JournalEntry entry)
        {
            return new
            {
                entry.Id,
                entry.JournalEntryNumber,
                entry.EntryDate,
                entry.JournalType,
                entry.Description,
                entry.ReferenceNumber,
                entry.PostingStatus,
                entry.BookClassification,
                entry.ApprovalStatus,
                entry.RequiresApproval,
                entry.TotalDebitAmount,
                entry.TotalCreditAmount,
                entry.PostingDate,
                entry.PostedByUserId,
                entry.ApprovedByUserId,
                entry.ApprovedDate,
                entry.RejectionReason,
                entry.IsReversed,
                entry.ReversalDate,
                entry.ReversalJournalEntryId,
                entry.OriginalJournalEntryId,
                entry.ReversalType,
                entry.ReversalReason,
                LineCount = entry.Transactions?.Count ?? 0,
                Lines = entry.Transactions?
                    .OrderBy(t => t.LineNumber)
                    .Select(t => new
                    {
                        t.Id,
                        t.LineNumber,
                        t.AccountId,
                        t.Description,
                        t.DebitAmount,
                        t.CreditAmount,
                        t.TransactionCurrency,
                        t.ExchangeRate,
                        t.ForeignCurrencyAmount,
                        t.FinanceDimensionSetId,
                        FinanceDimensionDisplayValue = t.FinanceDimensionSet != null
                            ? t.FinanceDimensionSet.DisplayValue
                            : null,
                        t.BookClassification,
                        t.PostingStatus
                    })
                    .ToList()
            };
        }

        private async Task NotifyJournalApproversAsync(JournalEntry entry)
        {
            try
            {
                var currentUserId = Guid.TryParse(_currentUserService.UserId, out var parsedCurrentUserId)
                    ? parsedCurrentUserId
                    : (Guid?)null;

                var excludedUserIds = new HashSet<Guid>();
                if (currentUserId.HasValue) excludedUserIds.Add(currentUserId.Value);
                if (entry.CreatedById.HasValue) excludedUserIds.Add(entry.CreatedById.Value);

                var approverIds = await GetWorkflowApproverUserIdsAsync(entry, excludedUserIds);
                if (approverIds.Count == 0)
                    approverIds = await GetFinanceApproverUserIdsAsync(entry.TenantId, excludedUserIds);

                foreach (var approverId in approverIds)
                {
                    await _notificationService.CreateInAppNotificationAsync(
                        approverId,
                        "Journal entry awaiting approval",
                        $"{entry.JournalEntryNumber} requires your approval.",
                        "FinanceJournalApproval",
                        BuildJournalNotificationData(entry, "ApprovalRequested"),
                        entry.TenantId);
                }
            }
            catch
            {
                // Notification failures must not block finance state transitions.
            }
        }

        private async Task NotifyJournalOwnerAsync(JournalEntry entry, string title, string message, string type)
        {
            try
            {
                if (!entry.CreatedById.HasValue || entry.CreatedById.Value == Guid.Empty)
                    return;

                if (Guid.TryParse(_currentUserService.UserId, out var currentUserId)
                    && currentUserId == entry.CreatedById.Value)
                    return;

                await _notificationService.CreateInAppNotificationAsync(
                    entry.CreatedById.Value,
                    title,
                    message,
                    type,
                    BuildJournalNotificationData(entry, type),
                    entry.TenantId);
            }
            catch
            {
                // Notification failures must not block finance state transitions.
            }
        }

        private static readonly WorkflowInstanceStatus[] ActiveWorkflowStatuses =
        {
            WorkflowInstanceStatus.Created,
            WorkflowInstanceStatus.InProgress,
            WorkflowInstanceStatus.Waiting,
            WorkflowInstanceStatus.Suspended
        };

        private async Task<List<Guid>> GetWorkflowApproverUserIdsAsync(JournalEntry entry, HashSet<Guid> excludedUserIds)
        {
            var assignedStepUserIdValues = await _context.WorkflowInstances
                .Where(i =>
                    i.TenantId == entry.TenantId &&
                    i.EntityId == entry.Id &&
                    ActiveWorkflowStatuses.Contains(i.Status) &&
                    (i.EntityType.Code == "JournalEntry" ||
                     i.EntityType.Name == "JournalEntry" ||
                     i.EntityType.Name == "Journal Entry"))
                .SelectMany(i => i.StepInstances
                    .Where(si =>
                        (si.Status == WorkflowStepInstanceStatus.Pending ||
                         si.Status == WorkflowStepInstanceStatus.InProgress) &&
                        si.AssignedToId.HasValue)
                    .Select(si => si.AssignedToId))
                .ToListAsync();

            var assignedStepUserIds = assignedStepUserIdValues
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .ToList();

            var approvalTargets = await _context.WorkflowInstances
                .Where(i =>
                    i.TenantId == entry.TenantId &&
                    i.EntityId == entry.Id &&
                    ActiveWorkflowStatuses.Contains(i.Status) &&
                    (i.EntityType.Code == "JournalEntry" ||
                     i.EntityType.Name == "JournalEntry" ||
                     i.EntityType.Name == "Journal Entry"))
                .SelectMany(i => i.StepInstances
                    .Where(si =>
                        si.Status == WorkflowStepInstanceStatus.Pending ||
                        si.Status == WorkflowStepInstanceStatus.InProgress)
                    .SelectMany(si => si.Approvals
                        .Where(a => a.Status == WorkflowApprovalStatus.Pending)
                        .Select(a => new
                        {
                            a.ApproverId,
                            a.ApproverRole
                        })))
                .ToListAsync();

            if (assignedStepUserIds.Count == 0 && approvalTargets.Count == 0)
                return new List<Guid>();

            var approverIds = assignedStepUserIds
                .Concat(approvalTargets
                    .Where(t => t.ApproverId.HasValue)
                    .Select(t => t.ApproverId!.Value))
                .Where(id => id != Guid.Empty && !excludedUserIds.Contains(id))
                .ToHashSet();

            var approverRoles = approvalTargets
                .Select(t => t.ApproverRole)
                .Where(role => !string.IsNullOrWhiteSpace(role))
                .Select(role => role!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (approverRoles.Count > 0)
            {
                var roleUserIds = await _context.UserTenants
                    .Where(ut => ut.TenantId == entry.TenantId
                                 && !ut.IsDeleted
                                 && ut.Status == UserTenantStatus.Active
                                 && (ut.ExpiresAt == null || ut.ExpiresAt > DateTime.UtcNow)
                                 && ut.User.IsActive
                                 && !excludedUserIds.Contains(ut.UserId)
                                 && ut.User.UserRoles.Any(ur => ur.Role.Name != null && approverRoles.Contains(ur.Role.Name)))
                    .Select(ut => ut.UserId)
                    .Distinct()
                    .ToListAsync();

                foreach (var roleUserId in roleUserIds)
                    approverIds.Add(roleUserId);
            }

            return approverIds.ToList();
        }

        private async Task<List<Guid>> GetFinanceApproverUserIdsAsync(Guid tenantId, HashSet<Guid> excludedUserIds)
        {
            return await _context.UserTenants
                .Where(ut => ut.TenantId == tenantId
                             && !ut.IsDeleted
                             && ut.Status == UserTenantStatus.Active
                             && (ut.ExpiresAt == null || ut.ExpiresAt > DateTime.UtcNow)
                             && ut.User.IsActive
                             && !excludedUserIds.Contains(ut.UserId)
                             && ut.User.UserRoles.Any(ur =>
                                 ur.Role.Name == "SuperAdmin"
                                 || ur.Role.Name == "TenantAdmin"
                                 || ur.Role.RolePermissions.Any(rp => rp.Permission.Name == "Finance.JournalEntries.Approve")))
                .Select(ut => ut.UserId)
                .Distinct()
                .ToListAsync();
        }

        private static Dictionary<string, object> BuildJournalNotificationData(JournalEntry entry, string eventType)
        {
            return new Dictionary<string, object>
            {
                ["EntityType"] = "JournalEntry",
                ["EntityId"] = entry.Id,
                ["ActionUrl"] = $"/finance/journal-entries/{entry.Id}",
                ["journalEntryId"] = entry.Id,
                ["journalNumber"] = entry.JournalEntryNumber,
                ["eventType"] = eventType,
                ["postingStatus"] = entry.PostingStatus,
                ["approvalStatus"] = entry.ApprovalStatus ?? string.Empty,
                ["totalDebit"] = entry.TotalDebitAmount,
                ["totalCredit"] = entry.TotalCreditAmount
            };
        }
    }
}
