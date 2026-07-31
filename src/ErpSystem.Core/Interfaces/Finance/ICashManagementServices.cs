using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Enums;

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

/// <summary>
/// Operational banking subledger used to settle cash, cheque, card, and mobile-money
/// collections into real bank accounts before bank reconciliation.
/// </summary>
public interface IBankingSettlementService
{
    Task<IReadOnlyList<LiquidityAccountDto>> GetLiquidityAccountsAsync(bool activeOnly = false, CancellationToken cancellationToken = default);
    Task<LiquidityAccountDto?> GetLiquidityAccountAsync(Guid id, CancellationToken cancellationToken = default);
    Task<LiquidityAccountDto> CreateLiquidityAccountAsync(CreateLiquidityAccountDto dto, CancellationToken cancellationToken = default);
    Task<LiquidityAccountDto> UpdateLiquidityAccountAsync(Guid id, UpdateLiquidityAccountDto dto, CancellationToken cancellationToken = default);
    Task<BankingSetupStatusDto> GetSetupStatusAsync(CancellationToken cancellationToken = default);
    Task<BankingSetupStatusDto> CompleteSetupAsync(CompleteBankingSetupDto dto, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LiquidityAccountEntryDto>> GetEligibleEntriesAsync(
        Guid? liquidityAccountId = null,
        string? currency = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PostedLiquidityPaymentCandidateDto>> GetPostedPaymentCandidatesAsync(
        Guid? liquidityAccountId = null,
        CancellationToken cancellationToken = default);
    Task<LiquidityAccountEntryDto> RegisterPostedPaymentAsync(
        RegisterPostedLiquidityPaymentDto dto,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BankDepositDto>> GetDepositsAsync(BankDepositStatus? status = null, CancellationToken cancellationToken = default);
    Task<BankDepositDto?> GetDepositAsync(Guid id, CancellationToken cancellationToken = default);
    Task<BankDepositDto> CreateDepositAsync(CreateBankDepositDto dto, CancellationToken cancellationToken = default);
    Task<BankDepositDto> UpdateDepositAsync(Guid id, UpdateBankDepositDto dto, CancellationToken cancellationToken = default);
    Task<BankDepositDto> LinkDepositAttachmentAsync(Guid id, LinkBankingAttachmentDto dto, CancellationToken cancellationToken = default);
    Task<BankDepositDto> UnlinkDepositAttachmentAsync(Guid id, Guid attachmentId, CancellationToken cancellationToken = default);
    Task<BankDepositDto> SubmitDepositAsync(Guid id, CancellationToken cancellationToken = default);
    Task<BankDepositDto> ApproveDepositAsync(Guid id, string? comments = null, CancellationToken cancellationToken = default);
    Task<BankDepositDto> RejectDepositAsync(Guid id, string? reason = null, CancellationToken cancellationToken = default);
    Task<BankDepositDto> ReturnDepositAsync(Guid id, string? comments = null, CancellationToken cancellationToken = default);
    Task<BankDepositDto> CancelDepositAsync(Guid id, string reason, CancellationToken cancellationToken = default);
    Task<BankDepositDto> PostDepositAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReturnedChequeCaseDto>> GetReturnedChequesAsync(ReturnedChequeCaseStatus? status = null, CancellationToken cancellationToken = default);
    Task<ReturnedChequeCaseDto?> GetReturnedChequeAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ReturnedChequeCaseDto> CreateReturnedChequeAsync(CreateReturnedChequeCaseDto dto, CancellationToken cancellationToken = default);
    Task<ReturnedChequeCaseDto> LinkReturnedChequeAttachmentAsync(Guid id, LinkBankingAttachmentDto dto, CancellationToken cancellationToken = default);
    Task<ReturnedChequeCaseDto> SubmitReturnedChequeAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ReturnedChequeCaseDto> ApproveReturnedChequeAsync(Guid id, string? comments = null, CancellationToken cancellationToken = default);
    Task<ReturnedChequeCaseDto> RejectReturnedChequeAsync(Guid id, string? reason = null, CancellationToken cancellationToken = default);
}
