using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces.Finance;

public interface IBankAccountService
{
    Task<BankAccountDto?> GetByIdAsync(Guid id);
    Task<IEnumerable<BankAccountDto>> GetAllAsync();
    Task<IEnumerable<BankAccountDto>> GetActiveAccountsAsync();
    Task<BankAccountDto> CreateAsync(CreateBankAccountDto dto);
    Task<BankAccountDto> UpdateAsync(Guid id, UpdateBankAccountDto dto);
    Task DeleteAsync(Guid id);
    Task<BankAccountBalanceDto> GetBalanceAsync(Guid id);
    Task<IEnumerable<CashTransactionDto>> GetTransactionsAsync(Guid id, DateTime? fromDate = null, DateTime? toDate = null);
    Task UpdateBalanceAsync(Guid id, decimal amount, bool isDebit);
}

public interface ICashTransactionService
{
    Task<CashTransactionDto?> GetByIdAsync(Guid id);
    Task<IEnumerable<CashTransactionDto>> GetAllAsync(DateTime? fromDate = null, DateTime? toDate = null);
    Task<IEnumerable<CashTransactionDto>> GetByBankAccountAsync(Guid bankAccountId, DateTime? fromDate = null, DateTime? toDate = null);
    Task<IEnumerable<CashTransactionDto>> GetUnreconciledAsync(Guid bankAccountId);
    Task<CashTransactionDto> CreateReceiptAsync(CreateCashReceiptDto dto);
    Task<CashTransactionDto> CreatePaymentAsync(CreateCashPaymentDto dto);
    Task<(CashTransactionDto FromTransaction, CashTransactionDto ToTransaction)> CreateTransferAsync(CreateBankTransferDto dto);
    Task<CashTransactionDto> SubmitAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CashTransactionDto> ApproveAsync(Guid id, string? comments = null, CancellationToken cancellationToken = default);
    Task<CashTransactionDto> RejectAsync(Guid id, string? reason = null, CancellationToken cancellationToken = default);
    Task<CashTransactionDto> ReturnAsync(Guid id, string? comments = null, CancellationToken cancellationToken = default);
    Task<CashTransactionDto> CancelAsync(Guid id, string reason, CancellationToken cancellationToken = default);
    Task<CashTransactionDto> PostAsync(Guid id, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id);
    Task MarkAsReconciledAsync(Guid id, Guid reconciliationId);
}

public interface IBankReconciliationService
{
    Task<BankReconciliationDto?> GetByIdAsync(Guid id);
    Task<IEnumerable<BankReconciliationDto>> GetByBankAccountAsync(Guid bankAccountId);
    Task<BankReconciliationDto> StartReconciliationAsync(StartReconciliationDto dto);
    Task<IEnumerable<ReconciliationMatchDto>> AutoMatchAsync(Guid reconciliationId);
    Task<IEnumerable<ReconciliationMatchDto>> GetMatchesAsync(Guid reconciliationId);
    Task<ReconciliationMatchDto> CreateManualMatchAsync(CreateManualMatchDto dto);
    Task<ReconciliationMatchDto> RemoveMatchAsync(Guid matchId);
    Task<ReconciliationAdjustmentDto> CreateAndPostAdjustmentAsync(Guid reconciliationId, CreateReconciliationAdjustmentDto dto, CancellationToken cancellationToken = default);
    Task<BankReconciliationDto> FinalizeReconciliationAsync(Guid id, CancellationToken cancellationToken = default);
    Task<BankReconciliationDto> CancelReconciliationAsync(Guid id, string reason, CancellationToken cancellationToken = default);
    Task<BankReconciliationDto> ApproveReconciliationAsync(Guid id);
    Task<ReconciliationSummaryDto> GetSummaryAsync(Guid id);
}
