using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.Cash;

public class BankReconciliationService : IBankReconciliationService
{
    private readonly ApplicationDbContext _context;
    private readonly BankReconciliationEngine _reconciliationEngine;
    private readonly ICurrentUserService _currentUserService;
    private readonly IWorkflowService _workflowService;

    public BankReconciliationService(
        ApplicationDbContext context,
        BankReconciliationEngine reconciliationEngine,
        ICurrentUserService currentUserService,
        IWorkflowService workflowService)
    {
        _context = context;
        _reconciliationEngine = reconciliationEngine;
        _currentUserService = currentUserService;
        _workflowService = workflowService;
    }

    private Guid CurrentUserId => Guid.TryParse(_currentUserService.UserId, out var id) ? id : Guid.Empty;

    public async Task<BankReconciliationDto?> GetByIdAsync(Guid id)
    {
        return await _context.Set<BankReconciliation>()
            .Where(r => r.Id == id)
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
        return await _context.Set<BankReconciliation>()
            .Where(r => r.BankAccountId == bankAccountId)
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
        // Get current book balance
        var bankAccount = await _context.BankAccounts.FindAsync(dto.BankAccountId)
            ?? throw new Exception("Bank account not found");

        var reconciliation = new BankReconciliation
        {
            BankAccountId = dto.BankAccountId,
            ReconciliationDate = dto.ReconciliationDate,
            StatementId = dto.StatementId,
            StatementBalance = dto.StatementBalance,
            BookBalance = bankAccount.CurrentBalance,
            Difference = dto.StatementBalance - bankAccount.CurrentBalance,
            Status = ReconciliationStatus.InProgress,
            MatchedCount = 0,
            UnmatchedBookCount = 0,
            UnmatchedStatementCount = 0
        };

        _context.Set<BankReconciliation>().Add(reconciliation);
        await _context.SaveChangesAsync();

        return await GetByIdAsync(reconciliation.Id) ?? throw new Exception("Failed to start reconciliation");
    }

    public async Task<IEnumerable<ReconciliationMatchDto>> AutoMatchAsync(Guid reconciliationId)
    {
        var reconciliation = await _context.Set<BankReconciliation>()
            .Include(r => r.BankAccount)
            .FirstOrDefaultAsync(r => r.Id == reconciliationId)
            ?? throw new Exception("Reconciliation not found");

        // Get unreconciled transactions
        var unreconciledTransactions = await _context.Set<CashTransaction>()
            .Where(t => t.BankAccountId == reconciliation.BankAccountId && !t.IsReconciled)
            .ToListAsync();

        // Get unmatched statement lines
        var unmatchedLines = await _context.Set<BankStatementLine>()
            .Where(l => l.BankStatement.BankAccountId == reconciliation.BankAccountId && !l.IsMatched)
            .ToListAsync();

        // Run auto-matching algorithm
        var matches = _reconciliationEngine.AutoMatch(unreconciledTransactions, unmatchedLines);

        // Save matches
        var matchDtos = new List<ReconciliationMatchDto>();
        foreach (var match in matches)
        {
            var reconciliationMatch = new ReconciliationMatch
            {
                ReconciliationId = reconciliationId,
                CashTransactionId = match.TransactionId,
                BankStatementLineId = match.StatementLineId,
                IsAutoMatched = true,
                MatchConfidence = match.Confidence,
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

        // Update reconciliation counts
        reconciliation.MatchedCount = matches.Count;
        reconciliation.UnmatchedBookCount = unreconciledTransactions.Count - matches.Count;
        reconciliation.UnmatchedStatementCount = unmatchedLines.Count - matches.Count;

        await _context.SaveChangesAsync();

        return matchDtos;
    }

    public async Task<ReconciliationMatchDto> CreateManualMatchAsync(CreateManualMatchDto dto)
    {
        var reconciliation = await _context.Set<BankReconciliation>().FindAsync(dto.ReconciliationId)
            ?? throw new Exception("Reconciliation not found");

        var transaction = await _context.Set<CashTransaction>().FindAsync(dto.CashTransactionId)
            ?? throw new Exception("Transaction not found");

        var statementLine = await _context.Set<BankStatementLine>().FindAsync(dto.BankStatementLineId)
            ?? throw new Exception("Statement line not found");

        if (transaction.IsReconciled)
        {
            throw new Exception("Transaction is already reconciled");
        }

        if (statementLine.IsMatched)
        {
            throw new Exception("Statement line is already matched");
        }

        var match = new ReconciliationMatch
        {
            ReconciliationId = dto.ReconciliationId,
            CashTransactionId = dto.CashTransactionId,
            BankStatementLineId = dto.BankStatementLineId,
            IsAutoMatched = false,
            MatchConfidence = 100, // Manual matches are 100% confident
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

        // Update counts
        reconciliation.MatchedCount++;
        reconciliation.UnmatchedBookCount--;
        reconciliation.UnmatchedStatementCount--;

        await _context.SaveChangesAsync();

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
        var match = await _context.Set<ReconciliationMatch>()
            .Include(m => m.CashTransaction)
            .Include(m => m.BankStatementLine)
            .Include(m => m.Reconciliation)
            .FirstOrDefaultAsync(m => m.Id == matchId)
            ?? throw new Exception("Match not found");

        // Unmark transaction and statement line
        match.CashTransaction.IsReconciled = false;
        match.CashTransaction.ReconciliationId = null;
        match.BankStatementLine.IsMatched = false;
        match.BankStatementLine.MatchedTransactionId = null;
        match.BankStatementLine.ReconciliationMatchId = null;

        // Update counts
        match.Reconciliation.MatchedCount--;
        match.Reconciliation.UnmatchedBookCount++;
        match.Reconciliation.UnmatchedStatementCount++;

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

        return matchDto;
    }

    public async Task<BankReconciliationDto> ApproveReconciliationAsync(Guid id)
    {
        var reconciliation = await _context.Set<BankReconciliation>().FindAsync(id)
            ?? throw new Exception("Reconciliation not found");

        if (reconciliation.Status != ReconciliationStatus.Completed)
        {
            throw new Exception("Reconciliation must be completed before approval");
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

        return await GetByIdAsync(id) ?? throw new Exception("Failed to approve reconciliation");
    }

    public async Task<ReconciliationSummaryDto> GetSummaryAsync(Guid id)
    {
        var reconciliation = await _context.Set<BankReconciliation>()
            .Include(r => r.Matches)
            .FirstOrDefaultAsync(r => r.Id == id)
            ?? throw new Exception("Reconciliation not found");

        // Get unmatched transactions
        var unmatchedTransactions = await _context.Set<CashTransaction>()
            .Where(t => t.BankAccountId == reconciliation.BankAccountId && !t.IsReconciled)
            .Select(t => new UnmatchedTransactionDto
            {
                Id = t.Id,
                TransactionDate = t.TransactionDate,
                Description = t.Description ?? "",
                Amount = t.Amount,
                ReferenceNumber = t.ReferenceNumber
            })
            .ToListAsync();

        // Get unmatched statement lines
        var unmatchedLines = await _context.Set<BankStatementLine>()
            .Where(l => l.BankStatement.BankAccountId == reconciliation.BankAccountId && !l.IsMatched)
            .Select(l => new UnmatchedStatementLineDto
            {
                Id = l.Id,
                TransactionDate = l.TransactionDate,
                Description = l.Description ?? "",
                Amount = l.CreditAmount > 0 ? l.CreditAmount : l.DebitAmount,
                ReferenceNumber = l.ReferenceNumber
            })
            .ToListAsync();

        return new ReconciliationSummaryDto
        {
            ReconciliationId = id,
            StatementBalance = reconciliation.StatementBalance,
            BookBalance = reconciliation.BookBalance,
            Difference = reconciliation.Difference,
            TotalMatches = reconciliation.MatchedCount,
            AutoMatches = reconciliation.Matches.Count(m => m.IsAutoMatched),
            ManualMatches = reconciliation.Matches.Count(m => !m.IsAutoMatched),
            UnmatchedBookTransactions = unmatchedTransactions,
            UnmatchedStatementLines = unmatchedLines
        };
    }
}
