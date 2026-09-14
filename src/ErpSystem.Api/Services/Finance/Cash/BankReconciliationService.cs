using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Finance.Integration;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Api.Services.Finance;
using ErpSystem.Data;
using ErpSystem.Shared;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.Cash;

public class BankReconciliationService : IBankReconciliationService
{
    private static readonly FinancePostingProducerContext ReconciliationAdjustmentProducer =
        new(FinanceDimensionRouteId.FinanceBankReconciliationAdjustment);

    private readonly ApplicationDbContext _context;
    private readonly BankReconciliationEngine _reconciliationEngine;
    private readonly ICurrentUserService _currentUserService;
    private readonly IWorkflowService _workflowService;
    private readonly ICashTransactionService _cashTransactionService;
    private readonly IFinanceAuditService? _financeAuditService;

    public BankReconciliationService(
        ApplicationDbContext context,
        BankReconciliationEngine reconciliationEngine,
        ICurrentUserService currentUserService,
        IWorkflowService workflowService,
        ICashTransactionService cashTransactionService,
        IFinanceAuditService? financeAuditService = null)
    {
        _context = context;
        _reconciliationEngine = reconciliationEngine;
        _currentUserService = currentUserService;
        _workflowService = workflowService;
        _cashTransactionService = cashTransactionService;
        _financeAuditService = financeAuditService;
    }

    private Guid CurrentUserId => Guid.TryParse(_currentUserService.UserId, out var id) ? id : Guid.Empty;
    private Guid TenantId => _currentUserService.GetRequiredFinanceTenantId();

    public async Task<BankReconciliationDto?> GetByIdAsync(Guid id)
    {
        var tenantId = TenantId;
        return await _context.Set<BankReconciliation>()
            .Where(r => r.TenantId == tenantId && r.Id == id && !r.IsDeleted)
            .Select(r => new BankReconciliationDto
            {
                Id = r.Id,
                BankAccountId = r.BankAccountId,
                BankAccountName = r.BankAccount.AccountName,
                ReconciliationDate = r.ReconciliationDate,
                StatementId = r.StatementId,
                StatementBalance = r.StatementBalance,
                BookBalance = r.BookBalance,
                Difference = r.Difference,
                Status = r.Status,
                MatchedCount = r.MatchedCount,
                UnmatchedBookCount = r.UnmatchedBookCount,
                UnmatchedStatementCount = r.UnmatchedStatementCount,
                ReconciledBy = r.ReconciledBy,
                ReconciledAt = r.ReconciledAt,
                ApprovedBy = r.ApprovedBy,
                ApprovedAt = r.ApprovedAt,
                Notes = r.Notes,
                CreatedAt = r.CreatedAt
            })
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<BankReconciliationDto>> GetByBankAccountAsync(Guid bankAccountId)
    {
        var tenantId = TenantId;
        return await _context.Set<BankReconciliation>()
            .Where(r => r.TenantId == tenantId && r.BankAccountId == bankAccountId && !r.IsDeleted)
            .Select(r => new BankReconciliationDto
            {
                Id = r.Id,
                BankAccountId = r.BankAccountId,
                BankAccountName = r.BankAccount.AccountName,
                ReconciliationDate = r.ReconciliationDate,
                StatementBalance = r.StatementBalance,
                BookBalance = r.BookBalance,
                Difference = r.Difference,
                Status = r.Status,
                MatchedCount = r.MatchedCount,
                CreatedAt = r.CreatedAt
            })
            .OrderByDescending(r => r.ReconciliationDate)
            .ToListAsync();
    }

    public async Task<BankReconciliationDto> StartReconciliationAsync(StartReconciliationDto dto)
    {
        var tenantId = TenantId;
        var bankAccount = await _context.BankAccounts
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == dto.BankAccountId && !a.IsDeleted)
            ?? throw new Exception("Bank account not found");

        if (!bankAccount.GLAccountId.HasValue)
        {
            throw new InvalidOperationException("Bank account must be linked to a GL account before reconciliation.");
        }

        if (dto.StatementId.HasValue)
        {
            var statementBelongsToAccount = await _context.Set<BankStatement>()
                .AnyAsync(s => s.TenantId == tenantId && s.Id == dto.StatementId.Value && s.BankAccountId == dto.BankAccountId && !s.IsDeleted);
            if (!statementBelongsToAccount)
            {
                throw new InvalidOperationException("Bank statement does not belong to the selected bank account.");
            }
        }

        var bookBalance = await CalculatePostedBookBalanceAsync(bankAccount, dto.ReconciliationDate);
        var reconciliation = new BankReconciliation
        {
            TenantId = tenantId,
            BankAccountId = dto.BankAccountId,
            ReconciliationDate = dto.ReconciliationDate,
            StatementId = dto.StatementId,
            StatementBalance = dto.StatementBalance,
            BookBalance = bookBalance,
            Difference = RoundMoney(dto.StatementBalance - bookBalance),
            Status = ReconciliationStatus.InProgress,
            MatchedCount = 0,
            UnmatchedBookCount = await CountUnmatchedBookTransactionsAsync(dto.BankAccountId, dto.ReconciliationDate),
            UnmatchedStatementCount = await CountUnmatchedStatementLinesAsync(dto.BankAccountId, dto.StatementId),
            Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim()
        };

        _context.Set<BankReconciliation>().Add(reconciliation);
        await _context.SaveChangesAsync();
        await RecordReconciliationAuditAsync(
            FinanceAuditEvents.BankReconciliationCreated,
            reconciliation,
            afterValues: new
            {
                reconciliation.BankAccountId,
                reconciliation.StatementId,
                reconciliation.StatementBalance,
                reconciliation.BookBalance,
                reconciliation.Difference,
                reconciliation.Status
            },
            comment: "Bank reconciliation created using posted GL book balance.",
            cancellationToken: default);

        return await GetByIdAsync(reconciliation.Id) ?? throw new Exception("Failed to start reconciliation");
    }

    public async Task<IEnumerable<ReconciliationMatchDto>> AutoMatchAsync(Guid reconciliationId)
    {
        var tenantId = TenantId;
        var reconciliation = await _context.Set<BankReconciliation>()
            .Include(r => r.BankAccount)
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == reconciliationId && !r.IsDeleted)
            ?? throw new Exception("Reconciliation not found");
        EnsureReconciliationCanBeMutated(reconciliation, "auto-match");

        var unreconciledTransactions = await _context.Set<CashTransaction>()
            .Where(t =>
                t.TenantId == tenantId &&
                t.BankAccountId == reconciliation.BankAccountId &&
                t.TransactionDate <= reconciliation.ReconciliationDate.Date.AddDays(1).AddTicks(-1) &&
                t.IsPosted &&
                t.ApprovalStatus == CashTransactionApprovalStatus.Posted &&
                t.JournalEntryId.HasValue &&
                _context.JournalEntries.Any(j =>
                    j.TenantId == tenantId &&
                    j.Id == t.JournalEntryId.Value &&
                    j.PostingStatus == "Posted" &&
                    !j.IsDeleted) &&
                !t.IsReconciled &&
                !t.IsDeleted)
            .ToListAsync();

        var unmatchedLinesQuery = _context.Set<BankStatementLine>()
            .Where(l => l.TenantId == tenantId && l.BankStatement.TenantId == tenantId && l.BankStatement.BankAccountId == reconciliation.BankAccountId && !l.IsMatched && !l.IsDeleted);

        if (reconciliation.StatementId.HasValue)
        {
            unmatchedLinesQuery = unmatchedLinesQuery.Where(l => l.BankStatementId == reconciliation.StatementId.Value);
        }

        var unmatchedLines = await unmatchedLinesQuery.ToListAsync();

        var dateToleranceDays = await _context.FinanceSettings
            .AsNoTracking()
            .Where(settings => settings.TenantId == tenantId)
            .Select(settings => (int?)settings.BankStatementMatchDateToleranceDays)
            .FirstOrDefaultAsync() ?? 3;

        // Run auto-matching algorithm
        var matches = _reconciliationEngine.AutoMatch(
            unreconciledTransactions,
            unmatchedLines,
            dateToleranceDays);

        // Save matches
        var matchDtos = new List<ReconciliationMatchDto>();
        foreach (var match in matches)
        {
            var reconciliationMatch = new ReconciliationMatch
            {
                TenantId = tenantId,
                ReconciliationId = reconciliationId,
                CashTransactionId = match.TransactionId,
                BankStatementLineId = match.StatementLineId,
                IsAutoMatched = true,
                MatchConfidence = match.Confidence,
                MatchedBy = CurrentUserId == Guid.Empty ? null : CurrentUserId,
                MatchedAt = DateTime.UtcNow
            };

            _context.Set<ReconciliationMatch>().Add(reconciliationMatch);

            // Mark transaction and statement line as matched
            var transaction = unreconciledTransactions.First(t => t.Id == match.TransactionId);
            transaction.IsReconciled = true;
            transaction.ReconciliationId = reconciliationId;

            var statementLine = unmatchedLines.First(l => l.Id == match.StatementLineId);
            statementLine.IsMatched = true;
            statementLine.MatchedTransactionId = match.TransactionId;
            statementLine.ReconciliationMatchId = reconciliationMatch.Id;

            matchDtos.Add(new ReconciliationMatchDto
            {
                Id = reconciliationMatch.Id,
                ReconciliationId = reconciliationId,
                CashTransactionId = match.TransactionId,
                BankStatementLineId = match.StatementLineId,
                IsAutoMatched = true,
                MatchConfidence = match.Confidence,
                MatchedAt = reconciliationMatch.MatchedAt
            });
        }

        await _context.SaveChangesAsync();
        await RecalculateCountsAsync(reconciliation);
        reconciliation.BookBalance = await CalculatePostedBookBalanceAsync(reconciliation.BankAccountId, reconciliation.ReconciliationDate);
        reconciliation.Difference = RoundMoney(reconciliation.StatementBalance - reconciliation.BookBalance);
        await _context.SaveChangesAsync();

        foreach (var match in matchDtos)
        {
            await RecordReconciliationAuditAsync(
                FinanceAuditEvents.BankReconciliationTransactionMatched,
                reconciliation,
                afterValues: new
                {
                    match.Id,
                    match.CashTransactionId,
                    match.BankStatementLineId,
                    match.IsAutoMatched,
                    match.MatchConfidence
                },
                comment: "Bank reconciliation auto-match created.",
                cancellationToken: default);
        }

        return matchDtos;
    }

    public async Task<IEnumerable<ReconciliationMatchDto>> GetMatchesAsync(Guid reconciliationId)
    {
        var tenantId = TenantId;
        var reconciliationExists = await _context.Set<BankReconciliation>()
            .AnyAsync(r => r.TenantId == tenantId && r.Id == reconciliationId && !r.IsDeleted);
        if (!reconciliationExists)
        {
            throw new Exception("Reconciliation not found");
        }

        return await _context.Set<ReconciliationMatch>()
            .Where(m => m.TenantId == tenantId && m.ReconciliationId == reconciliationId && !m.IsDeleted)
            .OrderByDescending(m => m.MatchedAt)
            .Select(m => new ReconciliationMatchDto
            {
                Id = m.Id,
                ReconciliationId = m.ReconciliationId,
                CashTransactionId = m.CashTransactionId,
                BankStatementLineId = m.BankStatementLineId,
                IsAutoMatched = m.IsAutoMatched,
                MatchConfidence = m.MatchConfidence,
                MatchedAt = m.MatchedAt,
                Notes = m.Notes,
                CashTransactionNumber = m.CashTransaction.TransactionNumber,
                CashTransactionDate = m.CashTransaction.TransactionDate,
                CashTransactionType = m.CashTransaction.TransactionType,
                CashTransactionDescription = m.CashTransaction.Description ?? string.Empty,
                CashTransactionReference = m.CashTransaction.ReferenceNumber,
                CashTransactionAmount = m.CashTransaction.Amount,
                StatementTransactionDate = m.BankStatementLine.TransactionDate,
                StatementDescription = m.BankStatementLine.Description ?? string.Empty,
                StatementReference = m.BankStatementLine.ReferenceNumber,
                StatementDebitAmount = m.BankStatementLine.DebitAmount,
                StatementCreditAmount = m.BankStatementLine.CreditAmount
            })
            .ToListAsync();
    }

    public async Task<ReconciliationMatchDto> CreateManualMatchAsync(CreateManualMatchDto dto)
    {
        var tenantId = TenantId;
        var reconciliation = await _context.Set<BankReconciliation>()
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == dto.ReconciliationId && !r.IsDeleted)
            ?? throw new Exception("Reconciliation not found");
        EnsureReconciliationCanBeMutated(reconciliation, "manual-match");

        var transaction = await _context.Set<CashTransaction>()
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Id == dto.CashTransactionId && !t.IsDeleted)
            ?? throw new Exception("Transaction not found");

        var statementLine = await _context.Set<BankStatementLine>()
            .Include(l => l.BankStatement)
            .FirstOrDefaultAsync(l => l.TenantId == tenantId && l.Id == dto.BankStatementLineId && !l.IsDeleted)
            ?? throw new Exception("Statement line not found");

        if (statementLine.BankStatement.TenantId != tenantId)
        {
            throw new InvalidOperationException("Statement line does not belong to this tenant.");
        }

        if (transaction.BankAccountId != reconciliation.BankAccountId)
        {
            throw new InvalidOperationException("Cash transaction does not belong to this reconciliation bank account.");
        }

        await EnsureCashTransactionIsPostedForReconciliationAsync(transaction, tenantId);

        if (statementLine.BankStatement.BankAccountId != reconciliation.BankAccountId)
        {
            throw new InvalidOperationException("Statement line does not belong to this reconciliation bank account.");
        }

        if (reconciliation.StatementId.HasValue && statementLine.BankStatementId != reconciliation.StatementId.Value)
        {
            throw new InvalidOperationException("Statement line does not belong to this reconciliation statement.");
        }

        if (transaction.IsReconciled)
        {
            throw new Exception("Transaction is already reconciled");
        }

        if (await _context.Set<ReconciliationMatch>().AnyAsync(m =>
                m.TenantId == tenantId &&
                m.CashTransactionId == transaction.Id &&
                !m.IsDeleted))
        {
            throw new InvalidOperationException("Cash transaction is already matched in another reconciliation.");
        }

        if (statementLine.IsMatched)
        {
            throw new Exception("Statement line is already matched");
        }

        if (!BankReconciliationEngine.IsDirectionCompatible(transaction, statementLine))
        {
            throw new InvalidOperationException("Cash transaction direction does not match the bank statement line.");
        }

        var expectedStatementAmount = BankReconciliationEngine.GetExpectedStatementAmount(transaction, statementLine);
        if (RoundMoney(expectedStatementAmount) != RoundMoney(transaction.Amount))
        {
            throw new InvalidOperationException("Cash transaction amount does not match the bank statement line amount.");
        }

        var match = new ReconciliationMatch
        {
            TenantId = tenantId,
            ReconciliationId = dto.ReconciliationId,
            CashTransactionId = dto.CashTransactionId,
            BankStatementLineId = dto.BankStatementLineId,
            IsAutoMatched = false,
            MatchConfidence = 100, // Manual matches are 100% confident
            MatchedBy = CurrentUserId == Guid.Empty ? null : CurrentUserId,
            MatchedAt = DateTime.UtcNow,
            Notes = dto.Notes
        };

        _context.Set<ReconciliationMatch>().Add(match);

        // Mark as matched
        transaction.IsReconciled = true;
        transaction.ReconciliationId = dto.ReconciliationId;
        statementLine.IsMatched = true;
        statementLine.MatchedTransactionId = dto.CashTransactionId;
        statementLine.ReconciliationMatchId = match.Id;

        await _context.SaveChangesAsync();
        await RecalculateCountsAsync(reconciliation);
        reconciliation.BookBalance = await CalculatePostedBookBalanceAsync(reconciliation.BankAccountId, reconciliation.ReconciliationDate);
        reconciliation.Difference = RoundMoney(reconciliation.StatementBalance - reconciliation.BookBalance);
        await _context.SaveChangesAsync();
        await RecordReconciliationAuditAsync(
            FinanceAuditEvents.BankReconciliationTransactionMatched,
            reconciliation,
            afterValues: new
            {
                match.Id,
                match.CashTransactionId,
                match.BankStatementLineId,
                match.IsAutoMatched,
                match.MatchConfidence
            },
            comment: dto.Notes,
            cancellationToken: default);

        return new ReconciliationMatchDto
        {
            Id = match.Id,
            ReconciliationId = match.ReconciliationId,
            CashTransactionId = match.CashTransactionId,
            BankStatementLineId = match.BankStatementLineId,
            IsAutoMatched = false,
            MatchConfidence = 100,
            MatchedAt = match.MatchedAt,
            Notes = match.Notes
        };
    }

    public async Task<ReconciliationMatchDto> RemoveMatchAsync(Guid matchId)
    {
        var tenantId = TenantId;
        var match = await _context.Set<ReconciliationMatch>()
            .Include(m => m.CashTransaction)
            .Include(m => m.BankStatementLine)
            .Include(m => m.Reconciliation)
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.Id == matchId && !m.IsDeleted)
            ?? throw new Exception("Match not found");
        EnsureReconciliationCanBeMutated(match.Reconciliation, "remove-match");

        if (match.CashTransaction.TenantId != tenantId ||
            match.BankStatementLine.TenantId != tenantId ||
            match.Reconciliation.TenantId != tenantId)
        {
            throw new InvalidOperationException("Reconciliation match contains cross-tenant references.");
        }

        // Unmark transaction and statement line
        match.CashTransaction.IsReconciled = false;
        match.CashTransaction.ReconciliationId = null;
        match.BankStatementLine.IsMatched = false;
        match.BankStatementLine.MatchedTransactionId = null;
        match.BankStatementLine.ReconciliationMatchId = null;

        var matchDto = new ReconciliationMatchDto
        {
            Id = match.Id,
            ReconciliationId = match.ReconciliationId,
            CashTransactionId = match.CashTransactionId,
            BankStatementLineId = match.BankStatementLineId,
            IsAutoMatched = match.IsAutoMatched,
            MatchConfidence = match.MatchConfidence,
            MatchedAt = match.MatchedAt
        };

        _context.Set<ReconciliationMatch>().Remove(match);
        await _context.SaveChangesAsync();
        await RecalculateCountsAsync(match.Reconciliation);
        match.Reconciliation.BookBalance = await CalculatePostedBookBalanceAsync(match.Reconciliation.BankAccountId, match.Reconciliation.ReconciliationDate);
        match.Reconciliation.Difference = RoundMoney(match.Reconciliation.StatementBalance - match.Reconciliation.BookBalance);
        await _context.SaveChangesAsync();
        await RecordReconciliationAuditAsync(
            FinanceAuditEvents.BankReconciliationTransactionUnmatched,
            match.Reconciliation,
            afterValues: new
            {
                match.Id,
                match.CashTransactionId,
                match.BankStatementLineId
            },
            comment: "Bank reconciliation match removed.",
            cancellationToken: default);

        return matchDto;
    }

    private Task<int> CountUnmatchedBookTransactionsAsync(Guid bankAccountId, DateTime reconciliationDate)
    {
        var tenantId = TenantId;
        return _context.Set<CashTransaction>()
            .CountAsync(t =>
                t.TenantId == tenantId &&
                t.BankAccountId == bankAccountId &&
                t.TransactionDate <= reconciliationDate.Date.AddDays(1).AddTicks(-1) &&
                t.IsPosted &&
                t.ApprovalStatus == CashTransactionApprovalStatus.Posted &&
                t.JournalEntryId.HasValue &&
                _context.JournalEntries.Any(j =>
                    j.TenantId == tenantId &&
                    j.Id == t.JournalEntryId.Value &&
                    j.PostingStatus == "Posted" &&
                    !j.IsDeleted) &&
                !t.IsReconciled &&
                !t.IsDeleted);
    }

    private Task<int> CountUnmatchedStatementLinesAsync(Guid bankAccountId, Guid? statementId)
    {
        var tenantId = TenantId;
        var query = _context.Set<BankStatementLine>()
            .Where(l => l.TenantId == tenantId && l.BankStatement.TenantId == tenantId && l.BankStatement.BankAccountId == bankAccountId && !l.IsMatched && !l.IsDeleted);

        if (statementId.HasValue)
        {
            query = query.Where(l => l.BankStatementId == statementId.Value);
        }

        return query.CountAsync();
    }

    private async Task RecalculateCountsAsync(BankReconciliation reconciliation)
    {
        reconciliation.MatchedCount = await _context.Set<ReconciliationMatch>()
            .CountAsync(m => m.TenantId == reconciliation.TenantId && m.ReconciliationId == reconciliation.Id && !m.IsDeleted);
        reconciliation.UnmatchedBookCount = await CountUnmatchedBookTransactionsAsync(reconciliation.BankAccountId, reconciliation.ReconciliationDate);
        reconciliation.UnmatchedStatementCount = await CountUnmatchedStatementLinesAsync(
            reconciliation.BankAccountId,
            reconciliation.StatementId);
    }

    public async Task<ReconciliationAdjustmentDto> CreateAndPostAdjustmentAsync(
        Guid reconciliationId,
        CreateReconciliationAdjustmentDto dto,
        CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var reconciliation = await _context.Set<BankReconciliation>()
            .Include(r => r.BankAccount)
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == reconciliationId && !r.IsDeleted, cancellationToken)
            ?? throw new Exception("Reconciliation not found");
        EnsureReconciliationCanBeMutated(reconciliation, "post-adjustment");

        if (dto.Amount <= 0m)
        {
            throw new InvalidOperationException("Reconciliation adjustment amount must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(dto.IdempotencyKey))
        {
            throw new InvalidOperationException("Reconciliation adjustment idempotency key is required.");
        }

        var normalizedIdempotencyKey = dto.IdempotencyKey.Trim();

        if (reconciliation.BankAccount.TenantId != tenantId || reconciliation.BankAccount.IsDeleted)
        {
            throw new InvalidOperationException("Reconciliation bank account belongs to another tenant.");
        }

        if (!reconciliation.BankAccount.GLAccountId.HasValue)
        {
            throw new InvalidOperationException("Reconciliation bank account must be linked to a GL account before posting adjustments.");
        }

        var offsetAccount = await _context.Accounts.FirstOrDefaultAsync(
            a => a.TenantId == tenantId && a.Id == dto.OffsetAccountId && !a.IsDeleted,
            cancellationToken)
            ?? throw new InvalidOperationException("Reconciliation adjustment offset account was not found.");

        if (offsetAccount.Status != AccountStatus.Active || !offsetAccount.AllowDirectPosting)
        {
            throw new InvalidOperationException("Reconciliation adjustment offset account must be active and postable.");
        }

        if (offsetAccount.Id == reconciliation.BankAccount.GLAccountId.Value)
        {
            throw new InvalidOperationException("Reconciliation adjustment offset account cannot be the bank GL account.");
        }

        var existingAdjustment = await _context.Set<CashTransaction>()
            .FirstOrDefaultAsync(t =>
                t.TenantId == tenantId &&
                t.ReconciliationId == reconciliation.Id &&
                t.ReferenceNumber == normalizedIdempotencyKey &&
                t.IsPosted &&
                t.JournalEntryId.HasValue &&
                !t.IsDeleted,
                cancellationToken);

        if (existingAdjustment != null)
        {
            var existingSourceType = ReconciliationAdjustmentProducer.Definition.DocumentType;
            var existingPostingEvent = await _context.FinancePostingEvents
                .AsNoTracking()
                .FirstOrDefaultAsync(e =>
                    e.TenantId == tenantId &&
                    e.SourceDocumentType == existingSourceType &&
                    e.SourceDocumentId == existingAdjustment.Id &&
                    e.PostingAction == "Post" &&
                    e.PostingStatus == "Posted" &&
                    !e.IsDeleted,
                    cancellationToken);

            await RecordReconciliationAuditAsync(
                FinanceAuditEvents.BankReconciliationAdjustmentDuplicatePostingAttempt,
                reconciliation,
                postingEventId: existingPostingEvent?.Id,
                journalEntryId: existingAdjustment.JournalEntryId,
                afterValues: new
                {
                    existingAdjustment.Id,
                    existingAdjustment.TransactionNumber,
                    existingAdjustment.ReferenceNumber,
                    existingAdjustment.JournalEntryId
                },
                comment: "Duplicate reconciliation adjustment request returned the existing posted adjustment.",
                cancellationToken: cancellationToken);

            var existingDetails = await _cashTransactionService.GetByIdAsync(existingAdjustment.Id)
                ?? throw new InvalidOperationException(
                    "The existing reconciliation adjustment could not be loaded for evidence display.");
            return new ReconciliationAdjustmentDto
            {
                ReconciliationId = reconciliation.Id,
                CashTransactionId = existingAdjustment.Id,
                JournalEntryId = existingAdjustment.JournalEntryId,
                PostingEventId = existingPostingEvent?.Id,
                AdjustmentType = dto.AdjustmentType,
                CashTransactionType = existingAdjustment.TransactionType,
                Amount = existingAdjustment.Amount,
                BankAccountId = existingAdjustment.BankAccountId,
                OffsetAccountId = existingAdjustment.GLAccountId ?? dto.OffsetAccountId,
                ReferenceNumber = existingAdjustment.ReferenceNumber,
                TransactionDate = existingAdjustment.TransactionDate,
                WasDuplicate = true,
                Currency = existingDetails.Currency,
                BaseAmount = existingDetails.BaseAmount,
                ExchangeRate = existingDetails.ExchangeRate ?? 1m,
                ExchangeRateId = existingDetails.ExchangeRateId,
                ExchangeRateSource = existingDetails.ExchangeRateSource,
                ExchangeRateDate = existingDetails.ExchangeRateDate,
                ExchangeRateQuoteSide = existingDetails.ExchangeRateQuoteSide,
                FinanceDimensions = existingDetails.FinanceDimensions
            };
        }

        var cashTransactionType = ResolveAdjustmentCashTransactionType(dto.AdjustmentType);
        var referenceNumber = normalizedIdempotencyKey;
        var description = BuildAdjustmentDescription(dto);
        CashTransactionDto adjustment;

        if (cashTransactionType == CashTransactionType.Receipt)
        {
            adjustment = await _cashTransactionService.CreateReceiptForProducerAsync(new CreateCashReceiptDto
            {
                BankAccountId = reconciliation.BankAccountId,
                GLAccountId = dto.OffsetAccountId,
                TransactionDate = dto.TransactionDate.Date,
                Amount = dto.Amount,
                Currency = reconciliation.BankAccount.Currency,
                ReferenceNumber = referenceNumber,
                PayerName = "Bank reconciliation adjustment",
                Description = description,
                FinanceDimensions = dto.FinanceDimensions
            }, ReconciliationAdjustmentProducer, cancellationToken);
        }
        else
        {
            adjustment = await _cashTransactionService.CreatePaymentForProducerAsync(new CreateCashPaymentDto
            {
                BankAccountId = reconciliation.BankAccountId,
                GLAccountId = dto.OffsetAccountId,
                TransactionDate = dto.TransactionDate.Date,
                Amount = dto.Amount,
                Currency = reconciliation.BankAccount.Currency,
                ReferenceNumber = referenceNumber,
                PayeeName = "Bank reconciliation adjustment",
                Description = description,
                FinanceDimensions = dto.FinanceDimensions
            }, ReconciliationAdjustmentProducer, cancellationToken);
        }

        var adjustmentEntity = await _context.Set<CashTransaction>()
            .FirstAsync(t => t.TenantId == tenantId && t.Id == adjustment.Id && !t.IsDeleted, cancellationToken);
        await _cashTransactionService.ValidateDimensionsForProducerAsync(
            adjustmentEntity.Id, ReconciliationAdjustmentProducer, cancellationToken);
        var now = DateTime.UtcNow;
        adjustmentEntity.ReconciliationId = reconciliation.Id;
        adjustmentEntity.ApprovalStatus = CashTransactionApprovalStatus.Approved;
        adjustmentEntity.ApprovedAt = now;
        adjustmentEntity.ApprovedById = CurrentUserId == Guid.Empty ? null : CurrentUserId;
        adjustmentEntity.ApprovalComments = "Approved as an explicit bank reconciliation adjustment.";
        adjustmentEntity.UpdatedAt = now;
        adjustmentEntity.UpdatedBy = _currentUserService.UserName;
        await _context.SaveChangesAsync(cancellationToken);

        var postedAdjustment = await _cashTransactionService.PostForProducerAsync(
            adjustmentEntity.Id, ReconciliationAdjustmentProducer, cancellationToken);
        adjustmentEntity = await _context.Set<CashTransaction>()
            .FirstAsync(t => t.TenantId == tenantId && t.Id == adjustmentEntity.Id && !t.IsDeleted, cancellationToken);
        adjustmentEntity.IsReconciled = true;
        adjustmentEntity.ReconciliationId = reconciliation.Id;
        adjustmentEntity.UpdatedAt = DateTime.UtcNow;
        adjustmentEntity.UpdatedBy = _currentUserService.UserName;

        await RecalculateCountsAsync(reconciliation);
        reconciliation.BookBalance = await CalculatePostedBookBalanceAsync(reconciliation.BankAccountId, reconciliation.ReconciliationDate);
        reconciliation.Difference = RoundMoney(reconciliation.StatementBalance - reconciliation.BookBalance);
        await _context.SaveChangesAsync(cancellationToken);

        var sourceDocumentType = ReconciliationAdjustmentProducer.Definition.DocumentType;
        var postingEvent = await _context.FinancePostingEvents
            .AsNoTracking()
            .FirstOrDefaultAsync(e =>
                e.TenantId == tenantId &&
                e.SourceDocumentType == sourceDocumentType &&
                e.SourceDocumentId == adjustmentEntity.Id &&
                e.PostingAction == "Post" &&
                e.PostingStatus == "Posted" &&
                !e.IsDeleted,
                cancellationToken);

        await RecordReconciliationAuditAsync(
            FinanceAuditEvents.BankReconciliationAdjustmentPosted,
            reconciliation,
            postingEventId: postingEvent?.Id,
            journalEntryId: postedAdjustment.JournalEntryId,
            afterValues: new
            {
                adjustmentEntity.Id,
                adjustmentEntity.TransactionNumber,
                dto.AdjustmentType,
                adjustmentEntity.TransactionType,
                adjustmentEntity.Amount,
                adjustmentEntity.Currency,
                adjustmentEntity.BaseAmount,
                adjustmentEntity.ExchangeRateId,
                adjustmentEntity.ExchangeRate,
                adjustmentEntity.ExchangeRateSource,
                adjustmentEntity.ExchangeRateDate,
                adjustmentEntity.GLAccountId,
                adjustmentEntity.JournalEntryId,
                postingEventId = postingEvent?.Id,
                dimensionLines = postedAdjustment.FinanceDimensions?.Lines.Select(line => new
                {
                    line.SourceLineId,
                    line.FinanceDimensionSetId,
                    line.CombinationHash,
                    line.IsFrozen
                })
            },
            comment: dto.Notes,
            cancellationToken: cancellationToken);

        return new ReconciliationAdjustmentDto
        {
            ReconciliationId = reconciliation.Id,
            CashTransactionId = adjustmentEntity.Id,
            JournalEntryId = postedAdjustment.JournalEntryId,
            PostingEventId = postingEvent?.Id,
            AdjustmentType = dto.AdjustmentType,
            CashTransactionType = adjustmentEntity.TransactionType,
            Amount = adjustmentEntity.Amount,
            BankAccountId = adjustmentEntity.BankAccountId,
            OffsetAccountId = adjustmentEntity.GLAccountId ?? dto.OffsetAccountId,
            ReferenceNumber = adjustmentEntity.ReferenceNumber,
            TransactionDate = adjustmentEntity.TransactionDate,
            WasDuplicate = false,
            Currency = postedAdjustment.Currency,
            BaseAmount = postedAdjustment.BaseAmount,
            ExchangeRate = postedAdjustment.ExchangeRate ?? 1m,
            ExchangeRateId = postedAdjustment.ExchangeRateId,
            ExchangeRateSource = postedAdjustment.ExchangeRateSource,
            ExchangeRateDate = postedAdjustment.ExchangeRateDate,
            ExchangeRateQuoteSide = postedAdjustment.ExchangeRateQuoteSide,
            FinanceDimensions = postedAdjustment.FinanceDimensions
        };
    }

    public async Task<BankReconciliationDto> FinalizeReconciliationAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var tenantId = TenantId;
        var reconciliation = await _context.Set<BankReconciliation>()
            .Include(r => r.BankAccount)
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == id && !r.IsDeleted, cancellationToken)
            ?? throw new Exception("Reconciliation not found");
        EnsureReconciliationCanBeMutated(reconciliation, "finalize");

        await RecalculateCountsAsync(reconciliation);
        reconciliation.BookBalance = await CalculatePostedBookBalanceAsync(reconciliation.BankAccountId, reconciliation.ReconciliationDate);
        reconciliation.Difference = RoundMoney(reconciliation.StatementBalance - reconciliation.BookBalance);
        if (RoundMoney(reconciliation.Difference) != 0m)
        {
            throw new InvalidOperationException("Reconciliation cannot be finalized while the statement and posted GL book balance differ. Post an approved adjustment first.");
        }

        var now = DateTime.UtcNow;
        reconciliation.Status = ReconciliationStatus.Completed;
        reconciliation.ReconciledAt = now;
        reconciliation.ReconciledBy = CurrentUserId == Guid.Empty ? null : CurrentUserId;
        reconciliation.UpdatedAt = now;
        reconciliation.UpdatedBy = _currentUserService.UserName;
        await _context.SaveChangesAsync(cancellationToken);
        await RecordReconciliationAuditAsync(
            FinanceAuditEvents.BankReconciliationFinalized,
            reconciliation,
            afterValues: new
            {
                reconciliation.Status,
                reconciliation.StatementBalance,
                reconciliation.BookBalance,
                reconciliation.Difference,
                reconciliation.MatchedCount,
                reconciliation.UnmatchedBookCount,
                reconciliation.UnmatchedStatementCount
            },
            comment: "Bank reconciliation finalized from posted GL book balance.",
            cancellationToken: cancellationToken);

        return await GetByIdAsync(id) ?? throw new Exception("Failed to finalize reconciliation");
    }

    public async Task<BankReconciliationDto> CancelReconciliationAsync(
        Guid id,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new InvalidOperationException("A cancellation reason is required.");
        }

        var tenantId = TenantId;
        var reconciliation = await _context.Set<BankReconciliation>()
            .Include(r => r.Matches)
                .ThenInclude(m => m.CashTransaction)
            .Include(r => r.Matches)
                .ThenInclude(m => m.BankStatementLine)
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == id && !r.IsDeleted, cancellationToken)
            ?? throw new Exception("Reconciliation not found");

        if (reconciliation.Status is ReconciliationStatus.Completed or ReconciliationStatus.Approved)
        {
            throw new InvalidOperationException("Finalized bank reconciliations cannot be cancelled by mutation.");
        }

        var now = DateTime.UtcNow;
        foreach (var match in reconciliation.Matches.Where(m => !m.IsDeleted))
        {
            if (match.CashTransaction.ReconciliationId == reconciliation.Id)
            {
                match.CashTransaction.IsReconciled = false;
                match.CashTransaction.ReconciliationId = null;
            }

            if (match.BankStatementLine.ReconciliationMatchId == match.Id)
            {
                match.BankStatementLine.IsMatched = false;
                match.BankStatementLine.MatchedTransactionId = null;
                match.BankStatementLine.ReconciliationMatchId = null;
            }

            match.IsDeleted = true;
            match.DeletedAt = now;
            match.DeletedBy = _currentUserService.UserName;
        }

        reconciliation.Status = ReconciliationStatus.Cancelled;
        reconciliation.Notes = string.IsNullOrWhiteSpace(reconciliation.Notes)
            ? reason.Trim()
            : $"{reconciliation.Notes}{Environment.NewLine}Cancellation: {reason.Trim()}";
        reconciliation.UpdatedAt = now;
        reconciliation.UpdatedBy = _currentUserService.UserName;
        await _context.SaveChangesAsync(cancellationToken);
        await RecordReconciliationAuditAsync(
            FinanceAuditEvents.BankReconciliationCancelled,
            reconciliation,
            afterValues: new
            {
                reconciliation.Status,
                reconciliation.Notes
            },
            reason: reason,
            cancellationToken: cancellationToken);

        return await GetByIdAsync(id) ?? throw new Exception("Failed to cancel reconciliation");
    }

    public async Task<BankReconciliationDto> ApproveReconciliationAsync(Guid id)
    {
        var tenantId = TenantId;
        var reconciliation = await _context.Set<BankReconciliation>()
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == id && !r.IsDeleted)
            ?? throw new Exception("Reconciliation not found");

        if (reconciliation.Status != ReconciliationStatus.Completed)
        {
            throw new Exception("Reconciliation must be completed before approval");
        }

        reconciliation.BookBalance = await CalculatePostedBookBalanceAsync(reconciliation.BankAccountId, reconciliation.ReconciliationDate);
        reconciliation.Difference = RoundMoney(reconciliation.StatementBalance - reconciliation.BookBalance);
        if (RoundMoney(reconciliation.Difference) != 0m)
        {
            throw new InvalidOperationException("Reconciliation cannot be approved while the statement and posted GL book balance differ.");
        }

        if (CurrentUserId == Guid.Empty)
            throw new Exception("Unable to resolve the current approver");

        var startResult = await _workflowService.StartApprovalWorkflowAsync("BankReconciliation", id);
        if (!startResult.Success)
            throw new Exception(startResult.Message ?? "Unable to start bank reconciliation approval workflow");

        if (!await _workflowService.CanUserApproveAsync("BankReconciliation", id, CurrentUserId))
            throw new Exception("This bank reconciliation is assigned to another workflow approver");

        var workflowResult = await _workflowService.ProcessApprovalStepAsync("BankReconciliation", id, CurrentUserId, "Approve");
        if (!workflowResult.Success)
            throw new Exception(workflowResult.Message ?? "Unable to process bank reconciliation approval");

        if (workflowResult.Status != WorkflowInstanceStatus.Completed)
            return await GetByIdAsync(id) ?? throw new Exception("Failed to retrieve reconciliation");

        reconciliation.Status = ReconciliationStatus.Approved;
        reconciliation.ApprovedAt = DateTime.UtcNow;
        reconciliation.ApprovedBy = CurrentUserId;

        await _context.SaveChangesAsync();
        await RecordReconciliationAuditAsync(
            FinanceAuditEvents.BankReconciliationApproved,
            reconciliation,
            afterValues: new
            {
                reconciliation.Status,
                reconciliation.ApprovedAt,
                reconciliation.ApprovedBy,
                reconciliation.StatementBalance,
                reconciliation.BookBalance,
                reconciliation.Difference
            },
            comment: "Bank reconciliation approved through the configured workflow engine.",
            cancellationToken: default);

        return await GetByIdAsync(id) ?? throw new Exception("Failed to approve reconciliation");
    }

    public async Task<ReconciliationSummaryDto> GetSummaryAsync(Guid id)
    {
        var tenantId = TenantId;
        var reconciliation = await _context.Set<BankReconciliation>()
            .Include(r => r.Matches)
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == id && !r.IsDeleted)
            ?? throw new Exception("Reconciliation not found");

        var bookBalance = await CalculatePostedBookBalanceAsync(reconciliation.BankAccountId, reconciliation.ReconciliationDate);
        var difference = RoundMoney(reconciliation.StatementBalance - bookBalance);

        var unmatchedTransactions = await _context.Set<CashTransaction>()
            .Where(t =>
                t.TenantId == tenantId &&
                t.BankAccountId == reconciliation.BankAccountId &&
                t.TransactionDate <= reconciliation.ReconciliationDate.Date.AddDays(1).AddTicks(-1) &&
                t.IsPosted &&
                t.ApprovalStatus == CashTransactionApprovalStatus.Posted &&
                t.JournalEntryId.HasValue &&
                _context.JournalEntries.Any(j =>
                    j.TenantId == tenantId &&
                    j.Id == t.JournalEntryId.Value &&
                    j.PostingStatus == "Posted" &&
                    !j.IsDeleted) &&
                !t.IsReconciled &&
                !t.IsDeleted)
            .Select(t => new UnmatchedTransactionDto
            {
                Id = t.Id,
                TransactionNumber = t.TransactionNumber,
                TransactionDate = t.TransactionDate,
                Description = t.Description ?? "",
                Amount = t.Amount,
                ReferenceNumber = t.ReferenceNumber,
                TransactionType = t.TransactionType
            })
            .ToListAsync();

        // Get unmatched statement lines
        var unmatchedLinesQuery = _context.Set<BankStatementLine>()
            .Where(l => l.TenantId == tenantId && l.BankStatement.TenantId == tenantId && l.BankStatement.BankAccountId == reconciliation.BankAccountId && !l.IsMatched && !l.IsDeleted);

        if (reconciliation.StatementId.HasValue)
        {
            unmatchedLinesQuery = unmatchedLinesQuery.Where(l => l.BankStatementId == reconciliation.StatementId.Value);
        }

        var unmatchedLines = await unmatchedLinesQuery
            .Select(l => new UnmatchedStatementLineDto
            {
                Id = l.Id,
                TransactionDate = l.TransactionDate,
                Description = l.Description ?? "",
                Amount = l.CreditAmount > 0 ? l.CreditAmount : l.DebitAmount,
                ReferenceNumber = l.ReferenceNumber,
                DebitAmount = l.DebitAmount,
                CreditAmount = l.CreditAmount
            })
            .ToListAsync();

        return new ReconciliationSummaryDto
        {
            ReconciliationId = id,
            StatementBalance = reconciliation.StatementBalance,
            BookBalance = bookBalance,
            Difference = difference,
            TotalMatches = reconciliation.MatchedCount,
            AutoMatches = reconciliation.Matches.Count(m => m.IsAutoMatched),
            ManualMatches = reconciliation.Matches.Count(m => !m.IsAutoMatched),
            UnmatchedBookTransactions = unmatchedTransactions,
            UnmatchedStatementLines = unmatchedLines
        };
    }

    private async Task<decimal> CalculatePostedBookBalanceAsync(Guid bankAccountId, DateTime reconciliationDate)
    {
        var tenantId = TenantId;
        var bankAccount = await _context.BankAccounts
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == bankAccountId && !a.IsDeleted)
            ?? throw new InvalidOperationException("Bank account not found.");
        return await CalculatePostedBookBalanceAsync(bankAccount, reconciliationDate);
    }

    private async Task<decimal> CalculatePostedBookBalanceAsync(BankAccount bankAccount, DateTime reconciliationDate)
    {
        var tenantId = TenantId;
        if (bankAccount.TenantId != tenantId)
        {
            throw new InvalidOperationException("Bank account belongs to another tenant.");
        }

        if (!bankAccount.GLAccountId.HasValue)
        {
            throw new InvalidOperationException("Bank account must be linked to a GL account before reconciliation.");
        }

        var throughDate = reconciliationDate.Date.AddDays(1).AddTicks(-1);
        var postedLines = await _context.AccountTransactions
            .Where(t =>
                t.TenantId == tenantId &&
                t.AccountId == bankAccount.GLAccountId.Value &&
                t.TransactionDate <= throughDate &&
                t.PostingStatus == "Posted" &&
                !t.IsDeleted &&
                _context.JournalEntries.Any(j =>
                    j.TenantId == tenantId &&
                    j.Id == t.JournalEntryId &&
                    j.PostingStatus == "Posted" &&
                    !j.IsDeleted))
            .Select(t => new
            {
                t.DebitAmount,
                t.CreditAmount,
                t.FunctionalCurrencyCode,
                t.TransactionCurrency,
                t.TransactionDebitAmount,
                t.TransactionCreditAmount
            })
            .ToListAsync();

        var bankCurrency = bankAccount.Currency.Trim().ToUpperInvariant();
        decimal postedMovement = 0m;
        foreach (var line in postedLines)
        {
            if (string.Equals(line.TransactionCurrency, bankCurrency, StringComparison.OrdinalIgnoreCase) &&
                line.TransactionDebitAmount.HasValue && line.TransactionCreditAmount.HasValue)
            {
                postedMovement += line.TransactionDebitAmount.Value - line.TransactionCreditAmount.Value;
                continue;
            }

            if (string.Equals(line.FunctionalCurrencyCode, bankCurrency, StringComparison.OrdinalIgnoreCase))
            {
                postedMovement += line.DebitAmount - line.CreditAmount;
                continue;
            }

            throw new InvalidOperationException(
                $"Posted bank GL movement is missing {bankCurrency} transaction-currency evidence required for reconciliation.");
        }

        return RoundMoney(bankAccount.OpeningBalance + postedMovement);
    }

    private async Task EnsureCashTransactionIsPostedForReconciliationAsync(CashTransaction transaction, Guid tenantId)
    {
        if (transaction.TenantId != tenantId)
        {
            throw new InvalidOperationException("Cash transaction belongs to another tenant.");
        }

        if (!transaction.IsPosted ||
            transaction.ApprovalStatus != CashTransactionApprovalStatus.Posted ||
            !transaction.JournalEntryId.HasValue)
        {
            throw new InvalidOperationException("Only posted cash/bank transactions can be reconciled.");
        }

        var hasPostedJournal = await _context.JournalEntries.AnyAsync(j =>
            j.TenantId == tenantId &&
            j.Id == transaction.JournalEntryId.Value &&
            j.PostingStatus == "Posted" &&
            !j.IsDeleted);
        if (!hasPostedJournal)
        {
            throw new InvalidOperationException("Cash transaction does not have a same-tenant posted journal entry.");
        }
    }

    private static void EnsureReconciliationCanBeMutated(BankReconciliation reconciliation, string action)
    {
        if (reconciliation.Status is ReconciliationStatus.Completed or ReconciliationStatus.Approved or ReconciliationStatus.Cancelled)
        {
            throw new InvalidOperationException($"Finalized bank reconciliations cannot be changed by {action}.");
        }
    }

    private static CashTransactionType ResolveAdjustmentCashTransactionType(ReconciliationAdjustmentType adjustmentType)
    {
        return adjustmentType switch
        {
            ReconciliationAdjustmentType.InterestIncome => CashTransactionType.Receipt,
            ReconciliationAdjustmentType.AdjustmentReceipt => CashTransactionType.Receipt,
            ReconciliationAdjustmentType.CorrectionReceipt => CashTransactionType.Receipt,
            ReconciliationAdjustmentType.BankCharge => CashTransactionType.Payment,
            ReconciliationAdjustmentType.BankFee => CashTransactionType.Payment,
            ReconciliationAdjustmentType.AdjustmentPayment => CashTransactionType.Payment,
            ReconciliationAdjustmentType.CorrectionPayment => CashTransactionType.Payment,
            _ => throw new InvalidOperationException("Unsupported reconciliation adjustment type.")
        };
    }

    private static string BuildAdjustmentDescription(CreateReconciliationAdjustmentDto dto)
    {
        if (!string.IsNullOrWhiteSpace(dto.Description))
        {
            return dto.Description.Trim();
        }

        return dto.AdjustmentType switch
        {
            ReconciliationAdjustmentType.BankCharge => "Bank reconciliation bank charge adjustment",
            ReconciliationAdjustmentType.BankFee => "Bank reconciliation bank fee adjustment",
            ReconciliationAdjustmentType.InterestIncome => "Bank reconciliation interest income adjustment",
            ReconciliationAdjustmentType.AdjustmentReceipt => "Bank reconciliation receipt adjustment",
            ReconciliationAdjustmentType.AdjustmentPayment => "Bank reconciliation payment adjustment",
            ReconciliationAdjustmentType.CorrectionReceipt => "Bank reconciliation correction receipt",
            ReconciliationAdjustmentType.CorrectionPayment => "Bank reconciliation correction payment",
            _ => "Bank reconciliation adjustment"
        };
    }

    private async Task RecordReconciliationAuditAsync(
        string eventType,
        BankReconciliation reconciliation,
        object? afterValues = null,
        string? comment = null,
        string? reason = null,
        Guid? postingEventId = null,
        Guid? journalEntryId = null,
        CancellationToken cancellationToken = default)
    {
        if (_financeAuditService == null)
        {
            return;
        }

        await _financeAuditService.RecordAsync(new FinanceAuditEventDto
        {
            EventType = eventType,
            TenantId = reconciliation.TenantId,
            SourceModule = "CASHBANK",
            SourceDocumentType = "BankReconciliation",
            SourceDocumentId = reconciliation.Id,
            JournalEntryId = journalEntryId,
            PostingEventId = postingEventId,
            AfterValues = afterValues,
            Comment = comment,
            Reason = reason,
            Resource = "Finance.BankReconciliation",
            ResourceId = reconciliation.Id.ToString()
        }, cancellationToken);
    }

    private static decimal RoundMoney(decimal value)
        => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
