using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.Finance
{
    // ============================================================================
    // PAYMENT TERM INTERFACES
    // ============================================================================

    public interface IPaymentTermRepository : IGenericRepository<PaymentTerm>
    {
        Task<PaymentTerm?> GetByCodeAsync(string code);
        Task<IEnumerable<PaymentTerm>> GetActiveAsync();
        Task<IEnumerable<PaymentTerm>> GetByApplicableToAsync(string applicableTo);
        Task<PaymentTerm?> GetDefaultAsync(string? applicableTo = null);
        Task<bool> IsCodeUniqueAsync(string code, Guid? excludeId = null);
    }

    public interface IPaymentTermService
    {
        Task<PaymentTermDto?> GetByIdAsync(Guid id);
        Task<PaymentTermDto?> GetByCodeAsync(string code);
        Task<IEnumerable<PaymentTermDto>> GetAllAsync();
        Task<IEnumerable<PaymentTermDto>> GetActiveAsync();
        Task<IEnumerable<PaymentTermDto>> GetByApplicableToAsync(string applicableTo);
        Task<PaymentTermDto?> GetDefaultAsync(string? applicableTo = null);
        Task<PaymentTermDto> CreateAsync(CreatePaymentTermDto dto);
        Task<PaymentTermDto> UpdateAsync(Guid id, UpdatePaymentTermDto dto);
        Task<bool> DeleteAsync(Guid id);
        Task<bool> SetDefaultAsync(Guid id);
        Task<bool> IsCodeUniqueAsync(string code, Guid? excludeId = null);
    }

    // ============================================================================
    // CURRENCY REPOSITORY INTERFACE
    // ============================================================================

    public interface ICurrencyRepository : IGenericRepository<Currency>
    {
        Task<Currency?> GetByCodeAsync(string code);
        Task<IEnumerable<Currency>> GetActiveAsync();
        Task<Currency?> GetBaseCurrencyAsync();
        Task<bool> IsCodeUniqueAsync(string code, Guid? excludeId = null);
    }

    // ============================================================================
    // REPOSITORY INTERFACES (AccountTransaction)
    // ============================================================================
    
    // Note: IAccountRepository, IInvoiceRepository, IPaymentRepository, IJournalEntryRepository 
    // are now defined in their own files.

    public interface IAccountTransactionRepository : IGenericRepository<AccountTransaction>
    {
        Task<IEnumerable<AccountTransaction>> GetByAccountAsync(Guid accountId);
        Task<IEnumerable<AccountTransaction>> GetByJournalEntryAsync(Guid journalEntryId);
        Task<IEnumerable<AccountTransaction>> GetByAccountAndDateRangeAsync(Guid accountId, DateTime startDate, DateTime endDate);
        Task<IEnumerable<AccountTransaction>> GetByTransactionTypeAsync(TransactionType transactionType);
        Task<decimal> GetAccountBalanceAsync(Guid accountId, DateTime? asOfDate = null);
    }

    // ============================================================================
    // SERVICE INTERFACES (AccountTransaction, Reports, Dashboard)
    // ============================================================================
    
    // Note: IAccountService, IInvoiceService, IPaymentService, IJournalEntryService
    // are now defined in their own files.

    public interface IAccountTransactionService
    {
        Task<AccountTransactionDto?> GetByIdAsync(Guid id);
        Task<IEnumerable<AccountTransactionDto>> GetByAccountAsync(Guid accountId);
        Task<IEnumerable<AccountTransactionDto>> GetByJournalEntryAsync(Guid journalEntryId);
        Task<IEnumerable<AccountTransactionDto>> GetByAccountAndDateRangeAsync(Guid accountId, DateTime startDate, DateTime endDate);
        Task<decimal> GetAccountBalanceAsync(Guid accountId, DateTime? asOfDate = null);
    }

    public interface IFinancialReportService
    {
        Task<object> GetTrialBalanceAsync(DateTime? asOfDate = null);
        Task<object> GetIncomeStatementAsync(DateTime startDate, DateTime endDate);
        Task<object> GetBalanceSheetAsync(DateTime? asOfDate = null);
        Task<object> GetCashFlowStatementAsync(DateTime startDate, DateTime endDate);
        Task<object> GetAccountsReceivableAgingAsync(DateTime? asOfDate = null);
        Task<object> GetAccountsPayableAgingAsync(DateTime? asOfDate = null);
    }

    public interface IFinanceDashboardService
    {
        Task<object> GetDashboardDataAsync();
        Task<decimal> GetTotalRevenueAsync(DateTime startDate, DateTime endDate);
        Task<decimal> GetTotalExpensesAsync(DateTime startDate, DateTime endDate);
        Task<decimal> GetNetIncomeAsync(DateTime startDate, DateTime endDate);
        Task<decimal> GetAccountsReceivableAsync();
        Task<decimal> GetAccountsPayableAsync();
        Task<object> GetRevenueByMonthAsync(int months = 12);
        Task<object> GetExpensesByMonthAsync(int months = 12);
        Task<object> GetTopCustomersByRevenueAsync(int count = 10);
        Task<int> GetOverdueInvoicesCountAsync();
    }
}
