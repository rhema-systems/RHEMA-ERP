using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;

namespace ErpSystem.Core.Interfaces
{
    public interface IGeneralLedgerService
    {
        #region Account Management
        Task<Account> CreateSegmentedAccountAsync(AccountCreateDto accountDto);
        Task<bool> ValidateAccountStructureAsync(string accountNumber);
        Task<Account?> GetAccountByIdAsync(Guid accountId);
        Task<Account?> GetAccountByCodeAsync(string accountCode);
        Task<IEnumerable<Account>> GetAllAccountsAsync();
        #endregion

        #region Transaction Processing
        [Obsolete("Legacy direct GL posting is disabled. Use IJournalEntryService for manual journals or IFinancePostingEngine through the owning Finance module.")]
        Task<JournalEntry> PostJournalEntryAsync(CreateJournalEntryDto entryDto);
        Task<decimal> GetAccountBalanceAsync(Guid accountId, string? currencyCode);
        Task<string> GenerateJournalEntryNumberAsync(CancellationToken cancellationToken = default);
        #endregion

        #region Currency Management
        Task<IEnumerable<ExchangeRate>> GetExchangeRatesAsync(DateTime date);
        Task<JournalEntry> RunCurrencyRevaluationAsync(RevaluationRequestDto requestDto);
        #endregion

        #region Financial Statements
        Task<BalanceSheetDto> GenerateBalanceSheetAsync(BalanceSheetRequestDto request);
        Task<IncomeStatementDto> GenerateIncomeStatementAsync(IncomeStatementRequestDto request);
        Task<TrialBalanceDto> GenerateTrialBalanceAsync(TrialBalanceRequestDto request);
        Task<DetailedLedgerReportDto> GenerateDetailedLedgerAsync(DetailedLedgerRequestDto request);
        Task<CashBankLedgerReportDto> GenerateCashBankLedgerAsync(CashBankLedgerRequestDto request);
        Task<CashFlowStatementDto> GenerateCashFlowStatementAsync(CashFlowStatementRequestDto request);
        Task<MultiCurrencyDetailReportDto> GenerateMultiCurrencyDetailReportAsync(MultiCurrencyDetailRequestDto request);
        #endregion

        #region Period-End Close
        Task<PeriodCloseValidationDto> ValidatePeriodCloseAsync(Guid fiscalPeriodId);
        Task<PeriodCloseResultDto> CloseFiscalPeriodAsync(PeriodCloseRequestDto request);
        Task<PeriodCloseResultDto> ReopenFiscalPeriodAsync(PeriodReopenRequestDto request);
        Task LockFiscalPeriodAsync(Guid fiscalPeriodId, string lockReason);
        Task UnlockFiscalPeriodAsync(Guid fiscalPeriodId, string unlockReason);
        Task<PeriodCloseResultDto> CloseFiscalYearAsync(YearEndCloseRequestDto request);
        #endregion
    }
}
