using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.Cash;

public class CashTransactionService : ICashTransactionService
{
    private readonly ApplicationDbContext _context;
    private readonly IBankAccountService _bankAccountService;
    private readonly ITenantSettingsService _tenantSettingsService;

    public CashTransactionService(
        ApplicationDbContext context,
        IBankAccountService bankAccountService,
        ITenantSettingsService tenantSettingsService)
    {
        _context = context;
        _bankAccountService = bankAccountService;
        _tenantSettingsService = tenantSettingsService;
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
        var transactionNumber = await GenerateTransactionNumberAsync("RCT");
        var baseCurrencyCode = await _tenantSettingsService.GetBaseCurrencyAsync();
        var transactionCurrency = string.IsNullOrWhiteSpace(dto.Currency) ? baseCurrencyCode : dto.Currency.Trim().ToUpperInvariant();

        var transaction = new CashTransaction
        {
            TransactionNumber = transactionNumber,
            TransactionDate = dto.TransactionDate,
            TransactionType = CashTransactionType.Receipt,
            BankAccountId = dto.BankAccountId,
            Amount = dto.Amount,
            Currency = transactionCurrency,
            BaseAmount = dto.Amount, // TODO: Apply exchange rate if needed
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
        var transactionNumber = await GenerateTransactionNumberAsync("PMT");
        var baseCurrencyCode = await _tenantSettingsService.GetBaseCurrencyAsync();
        var transactionCurrency = string.IsNullOrWhiteSpace(dto.Currency) ? baseCurrencyCode : dto.Currency.Trim().ToUpperInvariant();

        var transaction = new CashTransaction
        {
            TransactionNumber = transactionNumber,
            TransactionDate = dto.TransactionDate,
            TransactionType = CashTransactionType.Payment,
            BankAccountId = dto.BankAccountId,
            Amount = dto.Amount,
            Currency = transactionCurrency,
            BaseAmount = dto.Amount, // TODO: Apply exchange rate if needed
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
        var transactionNumber = await GenerateTransactionNumberAsync("TRF");
        var baseCurrencyCode = await _tenantSettingsService.GetBaseCurrencyAsync();
        var fromBankAccount = await _context.Set<BankAccount>().FindAsync(dto.FromBankAccountId);
        var toBankAccount = await _context.Set<BankAccount>().FindAsync(dto.ToBankAccountId);
        var fromCurrencyCode = string.IsNullOrWhiteSpace(fromBankAccount?.Currency) ? baseCurrencyCode : fromBankAccount.Currency.Trim().ToUpperInvariant();
        var toCurrencyCode = string.IsNullOrWhiteSpace(toBankAccount?.Currency) ? baseCurrencyCode : toBankAccount.Currency.Trim().ToUpperInvariant();

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
            BaseAmount = dto.Amount,
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
            BaseAmount = dto.Amount,
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

    private async Task<string> GenerateTransactionNumberAsync(string prefix)
    {
        var today = DateTime.UtcNow;
        var yearMonth = today.ToString("yyyyMM");
        
        var lastNumber = await _context.Set<CashTransaction>()
            .Where(t => t.TransactionNumber.StartsWith($"{prefix}-{yearMonth}"))
            .OrderByDescending(t => t.TransactionNumber)
            .Select(t => t.TransactionNumber)
            .FirstOrDefaultAsync();

        int sequence = 1;
        if (lastNumber != null)
        {
            var parts = lastNumber.Split('-');
            if (parts.Length == 3 && int.TryParse(parts[2], out int lastSeq))
            {
                sequence = lastSeq + 1;
            }
        }

        return $"{prefix}-{yearMonth}-{sequence:D4}";
    }
}
