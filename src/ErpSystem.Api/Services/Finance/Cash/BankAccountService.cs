using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Numbering;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.Cash;

public class BankAccountService : IBankAccountService
{
    private readonly ApplicationDbContext _context;
    private readonly ITenantSettingsService _tenantSettingsService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IJournalEntryService _journalEntryService;
    private readonly IDocumentNumberingService _documentNumberingService;

    public BankAccountService(
        ApplicationDbContext context,
        ITenantSettingsService tenantSettingsService,
        ICurrentUserService currentUserService,
        IJournalEntryService journalEntryService,
        IDocumentNumberingService documentNumberingService)
    {
        _context = context;
        _tenantSettingsService = tenantSettingsService;
        _currentUserService = currentUserService;
        _journalEntryService = journalEntryService;
        _documentNumberingService = documentNumberingService;
    }

    public async Task<BankAccountDto?> GetByIdAsync(Guid id)
    {
        var account = await _context.BankAccounts
            .Where(a => a.Id == id)
            .Select(a => new BankAccountDto
            {
                Id = a.Id,
                AccountNumber = a.AccountNumber,
                AccountName = a.AccountName,
                BankName = a.BankName,
                BankBranch = a.BankBranch,
                Currency = a.Currency,
                AccountType = a.AccountType,
                GLAccountId = a.GLAccountId,
                CurrentBalance = a.CurrentBalance,
                AvailableBalance = a.AvailableBalance,
                OpeningBalance = a.OpeningBalance,
                IsActive = a.IsActive,
                OpeningDate = a.OpeningDate,
                ClosingDate = a.ClosingDate,
                Notes = a.Notes,
                CreatedAt = a.CreatedAt
            })
            .FirstOrDefaultAsync();

        return account;
    }

    public async Task<IEnumerable<BankAccountDto>> GetAllAsync()
    {
        return await _context.BankAccounts
            .Select(a => new BankAccountDto
            {
                Id = a.Id,
                AccountNumber = a.AccountNumber,
                AccountName = a.AccountName,
                BankName = a.BankName,
                BankBranch = a.BankBranch,
                Currency = a.Currency,
                AccountType = a.AccountType,
                GLAccountId = a.GLAccountId,
                CurrentBalance = a.CurrentBalance,
                AvailableBalance = a.AvailableBalance,
                OpeningBalance = a.OpeningBalance,
                IsActive = a.IsActive,
                OpeningDate = a.OpeningDate,
                ClosingDate = a.ClosingDate,
                Notes = a.Notes,
                CreatedAt = a.CreatedAt
            })
            .ToListAsync();
    }

    public async Task<IEnumerable<BankAccountDto>> GetActiveAccountsAsync()
    {
        return await _context.BankAccounts
            .Where(a => a.IsActive)
            .Select(a => new BankAccountDto
            {
                Id = a.Id,
                AccountNumber = a.AccountNumber,
                AccountName = a.AccountName,
                BankName = a.BankName,
                BankBranch = a.BankBranch,
                Currency = a.Currency,
                AccountType = a.AccountType,
                CurrentBalance = a.CurrentBalance,
                AvailableBalance = a.AvailableBalance,
                IsActive = a.IsActive
            })
            .ToListAsync();
    }

    public async Task<BankAccountDto> CreateAsync(CreateBankAccountDto dto)
    {
        var currency = NormalizeCurrency(dto.Currency);
        var exchangeRate = await ResolveOpeningBalanceExchangeRateAsync(currency, dto.OpeningBalanceExchangeRate);

        await using var dbTransaction = await _context.Database.BeginTransactionAsync();

        var account = new BankAccount
        {
            AccountNumber = dto.AccountNumber,
            AccountName = dto.AccountName,
            BankName = dto.BankName,
            BankBranch = dto.BankBranch,
            Currency = currency,
            AccountType = dto.AccountType,
            GLAccountId = dto.GLAccountId,
            OpeningBalance = dto.OpeningBalance,
            CurrentBalance = dto.OpeningBalance,
            AvailableBalance = dto.OpeningBalance,
            OpeningDate = dto.OpeningDate,
            Notes = dto.Notes,
            IsActive = true
        };

        _context.BankAccounts.Add(account);
        await _context.SaveChangesAsync();

        if (dto.OpeningBalance != 0m)
        {
            await CreateOpeningBalancePostingAsync(account, exchangeRate);
        }

        await dbTransaction.CommitAsync();

        return await GetByIdAsync(account.Id) ?? throw new Exception("Failed to create bank account");
    }

    public async Task<BankAccountDto> UpdateAsync(Guid id, UpdateBankAccountDto dto)
    {
        var account = await _context.BankAccounts.FindAsync(id)
            ?? throw new Exception("Bank account not found");

        account.AccountName = dto.AccountName;
        account.BankName = dto.BankName;
        account.BankBranch = dto.BankBranch;
        account.GLAccountId = dto.GLAccountId;
        account.IsActive = dto.IsActive;
        account.Notes = dto.Notes;

        await _context.SaveChangesAsync();

        return await GetByIdAsync(id) ?? throw new Exception("Failed to update bank account");
    }

    public async Task DeleteAsync(Guid id)
    {
        var account = await _context.BankAccounts.FindAsync(id)
            ?? throw new Exception("Bank account not found");

        // Check if account has transactions
        var hasTransactions = await _context.Set<CashTransaction>()
            .AnyAsync(t => t.BankAccountId == id);

        if (hasTransactions)
        {
            throw new Exception("Cannot delete bank account with existing transactions");
        }

        _context.BankAccounts.Remove(account);
        await _context.SaveChangesAsync();
    }

    public async Task<BankAccountBalanceDto> GetBalanceAsync(Guid id)
    {
        var account = await _context.BankAccounts.FindAsync(id)
            ?? throw new Exception("Bank account not found");

        return new BankAccountBalanceDto
        {
            BankAccountId = account.Id,
            AccountNumber = account.AccountNumber,
            AccountName = account.AccountName,
            CurrentBalance = account.CurrentBalance,
            AvailableBalance = account.AvailableBalance,
            Currency = account.Currency,
            AsOfDate = DateTime.UtcNow
        };
    }

    public async Task<IEnumerable<CashTransactionDto>> GetTransactionsAsync(
        Guid id, 
        DateTime? fromDate = null, 
        DateTime? toDate = null)
    {
        var query = _context.Set<CashTransaction>()
            .Where(t => t.BankAccountId == id);

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
                Description = t.Description,
                IsReconciled = t.IsReconciled
            })
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync();
    }

    public async Task UpdateBalanceAsync(Guid id, decimal amount, bool isDebit)
    {
        var account = await _context.BankAccounts.FindAsync(id)
            ?? throw new Exception("Bank account not found");

        if (isDebit)
        {
            account.CurrentBalance -= amount;
            account.AvailableBalance -= amount;
        }
        else
        {
            account.CurrentBalance += amount;
            account.AvailableBalance += amount;
        }

        await _context.SaveChangesAsync();
    }

    private async Task CreateOpeningBalancePostingAsync(BankAccount account, decimal exchangeRate)
    {
        if (!account.GLAccountId.HasValue)
        {
            throw new InvalidOperationException("A linked GL account is required when creating a bank account with an opening balance.");
        }

        var tenantId = _currentUserService.TenantId
            ?? throw new InvalidOperationException("Tenant context is required to post a bank opening balance.");

        var settings = await _context.FinanceSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && !s.IsDeleted)
            ?? throw new InvalidOperationException("Finance settings not configured for this tenant.");

        if (!settings.MigrationClearingAccountId.HasValue)
        {
            throw new InvalidOperationException("Migration Clearing Account is not configured in Finance Settings.");
        }

        var openingAmount = Math.Abs(account.OpeningBalance);
        var baseAmount = ToBaseAmount(openingAmount, exchangeRate);
        if (baseAmount <= 0m)
        {
            throw new InvalidOperationException("Bank opening balance base amount must be greater than zero.");
        }

        var isPositiveBankBalance = account.OpeningBalance > 0m;
        var documentType = isPositiveBankBalance
            ? FinanceDocumentTypes.CashReceipt
            : FinanceDocumentTypes.CashPayment;
        var transactionNumber = await _documentNumberingService.GenerateAsync(
            DocumentNumberingModules.Finance,
            documentType,
            tenantId,
            account.OpeningDate,
            nameof(CashTransaction));

        var cashTransaction = new CashTransaction
        {
            Id = Guid.NewGuid(),
            TransactionNumber = transactionNumber,
            TransactionDate = account.OpeningDate,
            TransactionType = isPositiveBankBalance ? CashTransactionType.Receipt : CashTransactionType.Payment,
            BankAccountId = account.Id,
            Amount = openingAmount,
            Currency = account.Currency,
            ExchangeRate = exchangeRate,
            BaseAmount = baseAmount,
            ReferenceNumber = $"OPEN-{account.AccountNumber}",
            PayeeOrPayer = "Opening Balance",
            Description = $"Opening bank balance - {account.AccountName}",
            GLAccountId = settings.MigrationClearingAccountId.Value,
            IsReconciled = false,
            IsPosted = false
        };

        _context.Set<CashTransaction>().Add(cashTransaction);
        await _context.SaveChangesAsync();

        var journalTransactions = isPositiveBankBalance
            ? new List<CreateAccountTransactionDto>
            {
                new()
                {
                    AccountId = account.GLAccountId.Value,
                    Description = $"Opening bank balance - {account.AccountName}",
                    TransactionType = "Debit",
                    Amount = baseAmount,
                    Reference = cashTransaction.TransactionNumber,
                    CurrencyCode = account.Currency,
                    ExchangeRate = exchangeRate,
                    ForeignAmount = openingAmount
                },
                new()
                {
                    AccountId = settings.MigrationClearingAccountId.Value,
                    Description = $"Opening bank balance - {account.AccountName}",
                    TransactionType = "Credit",
                    Amount = baseAmount,
                    Reference = cashTransaction.TransactionNumber,
                    CurrencyCode = account.Currency,
                    ExchangeRate = exchangeRate,
                    ForeignAmount = openingAmount
                }
            }
            : new List<CreateAccountTransactionDto>
            {
                new()
                {
                    AccountId = settings.MigrationClearingAccountId.Value,
                    Description = $"Opening bank overdraft/balance - {account.AccountName}",
                    TransactionType = "Debit",
                    Amount = baseAmount,
                    Reference = cashTransaction.TransactionNumber,
                    CurrencyCode = account.Currency,
                    ExchangeRate = exchangeRate,
                    ForeignAmount = openingAmount
                },
                new()
                {
                    AccountId = account.GLAccountId.Value,
                    Description = $"Opening bank overdraft/balance - {account.AccountName}",
                    TransactionType = "Credit",
                    Amount = baseAmount,
                    Reference = cashTransaction.TransactionNumber,
                    CurrencyCode = account.Currency,
                    ExchangeRate = exchangeRate,
                    ForeignAmount = openingAmount
                }
            };

        var journal = await _journalEntryService.CreateJournalEntryAsync(new CreateJournalEntryDto
        {
            TransactionDate = account.OpeningDate,
            JournalType = "Opening Balance",
            Description = $"Opening Bank Balance {account.AccountName}",
            Reference = cashTransaction.TransactionNumber,
            SourceModule = "BANK",
            SourceDocumentId = cashTransaction.Id,
            SourceDocumentType = "BankOpeningBalance",
            Transactions = journalTransactions
        });

        await _journalEntryService.PostJournalEntryAsync(journal.Id);

        cashTransaction.IsPosted = true;
        cashTransaction.PostedDate = DateTime.UtcNow;
        cashTransaction.PostedBy = Guid.TryParse(_currentUserService.UserId, out var postedById) ? postedById : null;
        await _context.SaveChangesAsync();
    }

    private async Task<decimal> ResolveOpeningBalanceExchangeRateAsync(string currency, decimal? exchangeRate)
    {
        var baseCurrency = NormalizeCurrency(await _tenantSettingsService.GetBaseCurrencyAsync());
        if (string.Equals(currency, baseCurrency, StringComparison.OrdinalIgnoreCase))
        {
            return 1m;
        }

        var resolvedRate = exchangeRate.GetValueOrDefault();
        if (resolvedRate <= 0m)
        {
            throw new InvalidOperationException($"An exchange rate is required for {currency} bank opening balances.");
        }

        return resolvedRate;
    }

    private static decimal ToBaseAmount(decimal amount, decimal exchangeRate)
    {
        return decimal.Round(amount * exchangeRate, 2, MidpointRounding.AwayFromZero);
    }

    private static string NormalizeCurrency(string? currency)
    {
        return string.IsNullOrWhiteSpace(currency)
            ? "GHS"
            : currency.Trim().ToUpperInvariant();
    }
}
