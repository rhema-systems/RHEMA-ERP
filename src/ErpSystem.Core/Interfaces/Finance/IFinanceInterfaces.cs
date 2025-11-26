// using System;
// using System.Collections.Generic;
// using System.Threading.Tasks;
// using ErpSystem.Core.Entities.Finance;
// using ErpSystem.Core.DTOs.Finance;

// namespace ErpSystem.Core.Interfaces.Finance
// {
//     // Repository Interfaces
//     public interface IAccountRepository : IGenericRepository<Account>
//     {
//         Task<Account?> GetByAccountNumberAsync(string accountNumber);
//         Task<IEnumerable<Account>> GetByTypeAsync(AccountType accountType);
//         Task<IEnumerable<Account>> GetChildAccountsAsync(Guid parentAccountId);
//         Task<IEnumerable<Account>> GetRootAccountsAsync();
//         Task<IEnumerable<Account>> GetActiveAccountsAsync();
//         Task<bool> IsAccountNumberUniqueAsync(string accountNumber, Guid? excludeAccountId = null);
//         Task<decimal> GetAccountBalanceAsync(Guid accountId);
//         Task UpdateAccountBalanceAsync(Guid accountId, decimal newBalance);
//     }

//     public interface IInvoiceRepository : IGenericRepository<Invoice>
//     {
//         Task<Invoice?> GetByInvoiceNumberAsync(string invoiceNumber);
//         Task<IEnumerable<Invoice>> GetByCustomerAsync(Guid customerId);
//         Task<IEnumerable<Invoice>> GetByStatusAsync(InvoiceStatus status);
//         Task<IEnumerable<Invoice>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);
//         Task<IEnumerable<Invoice>> GetOverdueInvoicesAsync();
//         Task<IEnumerable<Invoice>> GetInvoicesWithDetailsAsync(InvoiceSearchDto searchDto);
//         Task<bool> IsInvoiceNumberUniqueAsync(string invoiceNumber, Guid? excludeInvoiceId = null);
//         Task<decimal> GetTotalOutstandingAsync();
//         Task<decimal> GetTotalOutstandingByCustomerAsync(Guid customerId);
//     }

//     public interface IPaymentRepository : IGenericRepository<Payment>
//     {
//         Task<Payment?> GetByPaymentNumberAsync(string paymentNumber);
//         Task<IEnumerable<Payment>> GetByInvoiceAsync(Guid invoiceId);
//         Task<IEnumerable<Payment>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);
//         Task<IEnumerable<Payment>> GetByStatusAsync(PaymentStatus status);
//         Task<IEnumerable<Payment>> GetByPaymentMethodAsync(PaymentMethod paymentMethod);
//         Task<bool> IsPaymentNumberUniqueAsync(string paymentNumber, Guid? excludePaymentId = null);
//         Task<decimal> GetTotalPaymentsByInvoiceAsync(Guid invoiceId);
//     }

//     public interface IJournalEntryRepository : IGenericRepository<JournalEntry>
//     {
//         Task<JournalEntry?> GetByJournalNumberAsync(string journalNumber);
//         Task<IEnumerable<JournalEntry>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);
//         Task<IEnumerable<JournalEntry>> GetByStatusAsync(JournalStatus status);
//         Task<IEnumerable<JournalEntry>> GetWithTransactionsAsync(Guid journalEntryId);
//         Task<bool> IsJournalNumberUniqueAsync(string journalNumber, Guid? excludeJournalEntryId = null);
//         Task<bool> IsBalancedAsync(Guid journalEntryId);
//     }

//     public interface IAccountTransactionRepository : IGenericRepository<AccountTransaction>
//     {
//         Task<IEnumerable<AccountTransaction>> GetByAccountAsync(Guid accountId);
//         Task<IEnumerable<AccountTransaction>> GetByJournalEntryAsync(Guid journalEntryId);
//         Task<IEnumerable<AccountTransaction>> GetByAccountAndDateRangeAsync(Guid accountId, DateTime startDate, DateTime endDate);
//         Task<IEnumerable<AccountTransaction>> GetByTransactionTypeAsync(TransactionType transactionType);
//         Task<decimal> GetAccountBalanceAsync(Guid accountId, DateTime? asOfDate = null);
//     }

//     // Service Interfaces
//     public interface IAccountService
//     {
//         Task<AccountDto?> GetByIdAsync(Guid id);
//         Task<AccountDto?> GetByAccountNumberAsync(string accountNumber);
//         Task<IEnumerable<AccountDto>> GetAllAsync();
//         Task<IEnumerable<AccountDto>> GetByTypeAsync(AccountType accountType);
//         Task<IEnumerable<AccountDto>> GetChildAccountsAsync(Guid parentAccountId);
//         Task<IEnumerable<AccountDto>> GetRootAccountsAsync();
//         Task<IEnumerable<AccountDto>> GetActiveAccountsAsync();
//         Task<AccountDto> CreateAsync(CreateAccountDto createDto);
//         Task<AccountDto> UpdateAsync(Guid id, UpdateAccountDto updateDto);
//         Task<bool> DeleteAsync(Guid id);
//         Task<bool> IsAccountNumberUniqueAsync(string accountNumber, Guid? excludeAccountId = null);
//         Task<decimal> GetBalanceAsync(Guid accountId);
//         Task<IEnumerable<AccountDto>> SearchAsync(AccountSearchDto searchDto);
//     }

//     public interface IInvoiceService
//     {
//         Task<InvoiceDto?> GetByIdAsync(Guid id);
//         Task<InvoiceDto?> GetByInvoiceNumberAsync(string invoiceNumber);
//         Task<IEnumerable<InvoiceDto>> GetAllAsync();
//         Task<IEnumerable<InvoiceDto>> GetByCustomerAsync(Guid customerId);
//         Task<IEnumerable<InvoiceDto>> GetByStatusAsync(InvoiceStatus status);
//         Task<IEnumerable<InvoiceDto>> GetOverdueInvoicesAsync();
//         Task<InvoiceDto> CreateAsync(CreateInvoiceDto createDto);
//         Task<InvoiceDto> UpdateAsync(Guid id, UpdateInvoiceDto updateDto);
//         Task<bool> DeleteAsync(Guid id);
//         Task<InvoiceDto> SendInvoiceAsync(Guid id);
//         Task<InvoiceDto> MarkAsPaidAsync(Guid id);
//         Task<InvoiceDto> CancelInvoiceAsync(Guid id);
//         Task<bool> IsInvoiceNumberUniqueAsync(string invoiceNumber, Guid? excludeInvoiceId = null);
//         Task<IEnumerable<InvoiceDto>> SearchAsync(InvoiceSearchDto searchDto);
//         Task<decimal> GetTotalOutstandingAsync();
//         Task<decimal> GetTotalOutstandingByCustomerAsync(Guid customerId);
//     }

//     public interface IPaymentService
//     {
//         Task<PaymentDto?> GetByIdAsync(Guid id);
//         Task<PaymentDto?> GetByPaymentNumberAsync(string paymentNumber);
//         Task<IEnumerable<PaymentDto>> GetAllAsync();
//         Task<IEnumerable<PaymentDto>> GetByInvoiceAsync(Guid invoiceId);
//         Task<IEnumerable<PaymentDto>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);
//         Task<PaymentDto> CreateAsync(CreatePaymentDto createDto);
//         Task<PaymentDto> UpdateAsync(Guid id, CreatePaymentDto updateDto);
//         Task<bool> DeleteAsync(Guid id);
//         Task<PaymentDto> ProcessPaymentAsync(Guid id);
//         Task<PaymentDto> RefundPaymentAsync(Guid id);
//         Task<bool> IsPaymentNumberUniqueAsync(string paymentNumber, Guid? excludePaymentId = null);
//         Task<decimal> GetTotalPaymentsByInvoiceAsync(Guid invoiceId);
//     }

//     public interface IJournalEntryService
//     {
//         Task<JournalEntryDto?> GetByIdAsync(Guid id);
//         Task<JournalEntryDto?> GetByJournalNumberAsync(string journalNumber);
//         Task<IEnumerable<JournalEntryDto>> GetAllAsync();
//         Task<IEnumerable<JournalEntryDto>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);
//         Task<IEnumerable<JournalEntryDto>> GetByStatusAsync(JournalStatus status);
//         Task<JournalEntryDto> CreateAsync(CreateJournalEntryDto createDto);
//         Task<JournalEntryDto> UpdateAsync(Guid id, CreateJournalEntryDto updateDto);
//         Task<bool> DeleteAsync(Guid id);
//         Task<JournalEntryDto> PostJournalEntryAsync(Guid id, Guid postedByUserId);
//         Task<JournalEntryDto> ReverseJournalEntryAsync(Guid id, Guid reversedByUserId);
//         Task<bool> IsJournalNumberUniqueAsync(string journalNumber, Guid? excludeJournalEntryId = null);
//         Task<bool> IsBalancedAsync(Guid journalEntryId);
//     }

//     public interface IAccountTransactionService
//     {
//         Task<AccountTransactionDto?> GetByIdAsync(Guid id);
//         Task<IEnumerable<AccountTransactionDto>> GetByAccountAsync(Guid accountId);
//         Task<IEnumerable<AccountTransactionDto>> GetByJournalEntryAsync(Guid journalEntryId);
//         Task<IEnumerable<AccountTransactionDto>> GetByAccountAndDateRangeAsync(Guid accountId, DateTime startDate, DateTime endDate);
//         Task<decimal> GetAccountBalanceAsync(Guid accountId, DateTime? asOfDate = null);
//     }

//     // Additional Finance Service Interfaces
//     public interface IFinancialReportService
//     {
//         Task<object> GetTrialBalanceAsync(DateTime? asOfDate = null);
//         Task<object> GetIncomeStatementAsync(DateTime startDate, DateTime endDate);
//         Task<object> GetBalanceSheetAsync(DateTime? asOfDate = null);
//         Task<object> GetCashFlowStatementAsync(DateTime startDate, DateTime endDate);
//         Task<object> GetAccountsReceivableAgingAsync(DateTime? asOfDate = null);
//         Task<object> GetAccountsPayableAgingAsync(DateTime? asOfDate = null);
//     }

//     public interface IFinanceDashboardService
//     {
//         Task<object> GetDashboardDataAsync();
//         Task<decimal> GetTotalRevenueAsync(DateTime startDate, DateTime endDate);
//         Task<decimal> GetTotalExpensesAsync(DateTime startDate, DateTime endDate);
//         Task<decimal> GetNetIncomeAsync(DateTime startDate, DateTime endDate);
//         Task<decimal> GetAccountsReceivableAsync();
//         Task<decimal> GetAccountsPayableAsync();
//         Task<object> GetRevenueByMonthAsync(int months = 12);
//         Task<object> GetExpensesByMonthAsync(int months = 12);
//         Task<object> GetTopCustomersByRevenueAsync(int count = 10);
//         Task<int> GetOverdueInvoicesCountAsync();
//     }
// }