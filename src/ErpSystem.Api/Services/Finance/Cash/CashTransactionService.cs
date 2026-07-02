using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Numbering;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.Cash;

public class CashTransactionService : ICashTransactionService
{
    private readonly ApplicationDbContext _context;
    private readonly IBankAccountService _bankAccountService;
    private readonly ITenantSettingsService _tenantSettingsService;
    private readonly IDocumentNumberingService _documentNumberingService;

    public CashTransactionService(
        ApplicationDbContext context,
        IBankAccountService bankAccountService,
        ITenantSettingsService tenantSettingsService,
        IDocumentNumberingService documentNumberingService)
    {
        _context = context;
        _bankAccountService = bankAccountService;
        _tenantSettingsService = tenantSettingsService;
        _documentNumberingService = documentNumberingService;
    }

    public async Task<CashTransactionDto?> GetByIdAsync(Guid id)
    {
        return await _context.Set<CashTransaction>()
            .Where(t => t.Id == id)
            .Select(t => new CashTransactionDto
            {
                Id = t.Id,
                TransactionNumber = t.TransactionNumber,
                TransactionDate = t.TransactionDate,
                TransactionType = t.TransactionType,
                BankAccountId = t.BankAccountId,
                BankAccountName = t.BankAccount.AccountName,
                ToBankAccountId = t.ToBankAccountId,
                ToBankAccountName = t.ToBankAccount != null ? t.ToBankAccount.AccountName : null,
                Amount = t.Amount,
                Currency = t.Currency,
                ExchangeRate = t.ExchangeRate,
                BaseAmount = t.BaseAmount,
                PaymentMethodId = t.PaymentMethodId,
                PaymentMethodName = t.PaymentMethod != null ? t.PaymentMethod.Name : null,
                ReferenceNumber = t.ReferenceNumber,
                PayeeOrPayer = t.PayeeOrPayer,
                Description = t.Description,
                GLAccountId = t.GLAccountId,
                IsReconciled = t.IsReconciled,
                ReconciliationId = t.ReconciliationId,
                ChequeId = t.ChequeId,
                ChequeNumber = t.Cheque != null ? t.Cheque.ChequeNumber : null,
                IsPosted = t.IsPosted,
                PostedDate = t.PostedDate,
                CreatedAt = t.CreatedAt
            })
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<CashTransactionDto>> GetAllAsync(DateTime? fromDate = null, DateTime? toDate = null)
    {
        var query = _context.Set<CashTransaction>().AsQueryable();

        if (fromDate.HasValue)
            query = query.Where(t => t.TransactionDate >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(t => t.TransactionDate <= toDate.Value);

        return await query
            .Select(t => new CashTransactionDto
            {
                Id = t.Id,
                TransactionNumber = t.TransactionNumber,
                TransactionDate = t.TransactionDate,
                TransactionType = t.TransactionType,
                BankAccountId = t.BankAccountId,
                BankAccountName = t.BankAccount.AccountName,
                Amount = t.Amount,
                Currency = t.Currency,
                PayeeOrPayer = t.PayeeOrPayer,
                Description = t.Description,
                IsReconciled = t.IsReconciled
            })
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<CashTransactionDto>> GetByBankAccountAsync(
        Guid bankAccountId, 
        DateTime? fromDate = null, 
        DateTime? toDate = null)
    {
        var query = _context.Set<CashTransaction>()
            .Where(t => t.BankAccountId == bankAccountId);

        if (fromDate.HasValue)
            query = query.Where(t => t.TransactionDate >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(t => t.TransactionDate <= toDate.Value);

        return await query
            .Select(t => new CashTransactionDto
            {
                Id = t.Id,
                TransactionNumber = t.TransactionNumber,
                TransactionDate = t.TransactionDate,
                TransactionType = t.TransactionType,
                Amount = t.Amount,
                Currency = t.Currency,
                PayeeOrPayer = t.PayeeOrPayer,
                Description = t.Description,
                IsReconciled = t.IsReconciled
            })
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<CashTransactionDto>> GetUnreconciledAsync(Guid bankAccountId)
    {
        return await _context.Set<CashTransaction>()
            .Where(t => t.BankAccountId == bankAccountId && !t.IsReconciled)
            .Select(t => new CashTransactionDto
            {
                Id = t.Id,
                TransactionNumber = t.TransactionNumber,
                TransactionDate = t.TransactionDate,
                TransactionType = t.TransactionType,
                Amount = t.Amount,
                Currency = t.Currency,
                PayeeOrPayer = t.PayeeOrPayer,
                Description = t.Description,
                ReferenceNumber = t.ReferenceNumber
            })
            .OrderBy(t => t.TransactionDate)
            .ToListAsync();
    }

    public async Task<CashTransactionDto> CreateReceiptAsync(CreateCashReceiptDto dto)
    {
        var transactionNumber = await GenerateTransactionNumberAsync(FinanceDocumentTypes.CashReceipt, dto.TransactionDate);
        var baseCurrencyCode = await _tenantSettingsService.GetBaseCurrencyAsync();
        var transactionCurrency = string.IsNullOrWhiteSpace(dto.Currency) ? baseCurrencyCode : dto.Currency.Trim().ToUpperInvariant();
        var exchangeRate = ResolveExchangeRate(transactionCurrency, baseCurrencyCode, dto.ExchangeRate);
        var baseAmount = ToBaseAmount(dto.Amount, exchangeRate);

        var transaction = new CashTransaction
        {
            TransactionNumber = transactionNumber,
            TransactionDate = dto.TransactionDate,
            TransactionType = CashTransactionType.Receipt,
            BankAccountId = dto.BankAccountId,
            Amount = dto.Amount,
            Currency = transactionCurrency,
            ExchangeRate = exchangeRate,
            BaseAmount = baseAmount,
            PaymentMethodId = dto.PaymentMethodId,
            ReferenceNumber = dto.ReferenceNumber,
            PayeeOrPayer = dto.PayerName,
            Description = dto.Description,
            GLAccountId = dto.GLAccountId,
            IsReconciled = false,
            IsPosted = false
        };

        _context.Set<CashTransaction>().Add(transaction);
        await _context.SaveChangesAsync();

        // Update bank account balance
        await _bankAccountService.UpdateBalanceAsync(dto.BankAccountId, dto.Amount, false);

        return await GetByIdAsync(transaction.Id) ?? throw new Exception("Failed to create receipt");
    }

    public async Task<CashTransactionDto> CreatePaymentAsync(CreateCashPaymentDto dto)
    {
        var transactionNumber = await GenerateTransactionNumberAsync(FinanceDocumentTypes.CashPayment, dto.TransactionDate);
        var baseCurrencyCode = await _tenantSettingsService.GetBaseCurrencyAsync();
        var transactionCurrency = string.IsNullOrWhiteSpace(dto.Currency) ? baseCurrencyCode : dto.Currency.Trim().ToUpperInvariant();
        var exchangeRate = ResolveExchangeRate(transactionCurrency, baseCurrencyCode, dto.ExchangeRate);
        var baseAmount = ToBaseAmount(dto.Amount, exchangeRate);

        var transaction = new CashTransaction
        {
            TransactionNumber = transactionNumber,
            TransactionDate = dto.TransactionDate,
            TransactionType = CashTransactionType.Payment,
            BankAccountId = dto.BankAccountId,
            Amount = dto.Amount,
            Currency = transactionCurrency,
            ExchangeRate = exchangeRate,
            BaseAmount = baseAmount,
            PaymentMethodId = dto.PaymentMethodId,
            ReferenceNumber = dto.ReferenceNumber,
            PayeeOrPayer = dto.PayeeName,
            Description = dto.Description,
            GLAccountId = dto.GLAccountId,
            ChequeId = dto.ChequeId,
            IsReconciled = false,
            IsPosted = false
        };

        _context.Set<CashTransaction>().Add(transaction);
        await _context.SaveChangesAsync();

        // Update bank account balance
        await _bankAccountService.UpdateBalanceAsync(dto.BankAccountId, dto.Amount, true);

        return await GetByIdAsync(transaction.Id) ?? throw new Exception("Failed to create payment");
    }

    public async Task<(CashTransactionDto FromTransaction, CashTransactionDto ToTransaction)> CreateTransferAsync(CreateBankTransferDto dto)
    {
        var transactionNumber = await GenerateTransactionNumberAsync(FinanceDocumentTypes.BankTransfer, dto.TransactionDate);
        var baseCurrencyCode = await _tenantSettingsService.GetBaseCurrencyAsync();
        var fromBankAccount = await _context.Set<BankAccount>().FindAsync(dto.FromBankAccountId);
        var toBankAccount = await _context.Set<BankAccount>().FindAsync(dto.ToBankAccountId);
        var fromCurrencyCode = string.IsNullOrWhiteSpace(fromBankAccount?.Currency) ? baseCurrencyCode : fromBankAccount.Currency.Trim().ToUpperInvariant();
        var toCurrencyCode = string.IsNullOrWhiteSpace(toBankAccount?.Currency) ? baseCurrencyCode : toBankAccount.Currency.Trim().ToUpperInvariant();
        var fromExchangeRate = ResolveExchangeRate(fromCurrencyCode, baseCurrencyCode, dto.ExchangeRate);
        var toExchangeRate = ResolveExchangeRate(toCurrencyCode, baseCurrencyCode, dto.ExchangeRate);

        // Create debit transaction (from account)
        var fromTransaction = new CashTransaction
        {
            TransactionNumber = $"{transactionNumber}-OUT",
            TransactionDate = dto.TransactionDate,
            TransactionType = CashTransactionType.Transfer,
            BankAccountId = dto.FromBankAccountId,
            ToBankAccountId = dto.ToBankAccountId,
            Amount = dto.Amount,
            Currency = fromCurrencyCode,
            ExchangeRate = fromExchangeRate,
            BaseAmount = ToBaseAmount(dto.Amount, fromExchangeRate),
            ReferenceNumber = dto.ReferenceNumber,
            Description = dto.Description,
            IsReconciled = false,
            IsPosted = false
        };

        // Create credit transaction (to account)
        var toTransaction = new CashTransaction
        {
            TransactionNumber = $"{transactionNumber}-IN",
            TransactionDate = dto.TransactionDate,
            TransactionType = CashTransactionType.Transfer,
            BankAccountId = dto.ToBankAccountId,
            ToBankAccountId = dto.FromBankAccountId,
            Amount = dto.Amount,
            Currency = toCurrencyCode,
            ExchangeRate = toExchangeRate,
            BaseAmount = ToBaseAmount(dto.Amount, toExchangeRate),
            ReferenceNumber = dto.ReferenceNumber,
            Description = dto.Description,
            IsReconciled = false,
            IsPosted = false
        };

        _context.Set<CashTransaction>().AddRange(fromTransaction, toTransaction);
        await _context.SaveChangesAsync();

        // Update bank account balances
        await _bankAccountService.UpdateBalanceAsync(dto.FromBankAccountId, dto.Amount, true);
        await _bankAccountService.UpdateBalanceAsync(dto.ToBankAccountId, dto.Amount, false);

        var fromDto = await GetByIdAsync(fromTransaction.Id) ?? throw new Exception("Failed to create transfer");
        var toDto = await GetByIdAsync(toTransaction.Id) ?? throw new Exception("Failed to create transfer");

        return (fromDto, toDto);
    }

    public async Task DeleteAsync(Guid id)
    {
        var transaction = await _context.Set<CashTransaction>().FindAsync(id)
            ?? throw new Exception("Transaction not found");

        if (transaction.IsReconciled)
        {
            throw new Exception("Cannot delete reconciled transaction");
        }

        if (transaction.IsPosted)
        {
            throw new Exception("Cannot delete posted transaction");
        }

        // Reverse balance update
        var isDebit = transaction.TransactionType == CashTransactionType.Payment;
        await _bankAccountService.UpdateBalanceAsync(transaction.BankAccountId, transaction.Amount, !isDebit);

        _context.Set<CashTransaction>().Remove(transaction);
        await _context.SaveChangesAsync();
    }

    public async Task MarkAsReconciledAsync(Guid id, Guid reconciliationId)
    {
        var transaction = await _context.Set<CashTransaction>().FindAsync(id)
            ?? throw new Exception("Transaction not found");

        transaction.IsReconciled = true;
        transaction.ReconciliationId = reconciliationId;

        await _context.SaveChangesAsync();
    }

    private async Task<string> GenerateTransactionNumberAsync(string documentType, DateTime transactionDate)
    {
        return await _documentNumberingService.GenerateAsync(
            DocumentNumberingModules.Finance,
            documentType,
            documentDate: transactionDate,
            entityType: nameof(CashTransaction));
    }

    private static decimal ResolveExchangeRate(string transactionCurrency, string baseCurrencyCode, decimal? exchangeRate)
    {
        if (string.Equals(transactionCurrency, baseCurrencyCode, StringComparison.OrdinalIgnoreCase))
        {
            return 1m;
        }

        var resolvedRate = exchangeRate.GetValueOrDefault();
        if (resolvedRate <= 0m)
        {
            throw new InvalidOperationException($"An exchange rate is required for {transactionCurrency} cash transactions.");
        }

        return resolvedRate;
    }

    private static decimal ToBaseAmount(decimal amount, decimal exchangeRate)
    {
        return decimal.Round(amount * exchangeRate, 2, MidpointRounding.AwayFromZero);
    }
}
