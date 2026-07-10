using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Api.Services.Finance;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.Cash;

public class BankAccountService : IBankAccountService
{
    private readonly ApplicationDbContext _context;
    private readonly ITenantSettingsService _tenantSettingsService;
    private readonly ICurrentUserService _currentUserService;

    public BankAccountService(
        ApplicationDbContext context,
        ITenantSettingsService tenantSettingsService,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _tenantSettingsService = tenantSettingsService;
        _currentUserService = currentUserService;
    }

    private Guid TenantId => _currentUserService.GetRequiredFinanceTenantId();

    public async Task<BankAccountDto?> GetByIdAsync(Guid id)
    {
        var tenantId = TenantId;
        var account = await _context.BankAccounts
            .Where(a => a.TenantId == tenantId && a.Id == id && !a.IsDeleted)
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
        var tenantId = TenantId;
        return await _context.BankAccounts
            .Where(a => a.TenantId == tenantId && !a.IsDeleted)
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
        var tenantId = TenantId;
        return await _context.BankAccounts
            .Where(a => a.TenantId == tenantId && a.IsActive && !a.IsDeleted)
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
        var tenantId = TenantId;
        var currency = NormalizeCurrency(dto.Currency);

        if (dto.OpeningBalance != 0m)
        {
            throw new InvalidOperationException(
                "Bank account opening-balance posting is disabled until the FIN-LIM-0006 opening-balance migration batch routes opening balances through IFinancePostingEngine.");
        }

        if (dto.GLAccountId.HasValue)
        {
            await ValidateGLAccountAsync(dto.GLAccountId.Value, tenantId, "bank");
        }

        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var dbTransaction = await _context.Database.BeginTransactionAsync();

            var account = new BankAccount
            {
                TenantId = tenantId,
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

            await dbTransaction.CommitAsync();

            return await GetByIdAsync(account.Id) ?? throw new Exception("Failed to create bank account");
        });
    }

    public async Task<BankAccountDto> UpdateAsync(Guid id, UpdateBankAccountDto dto)
    {
        var tenantId = TenantId;
        var account = await _context.BankAccounts
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == id && !a.IsDeleted)
            ?? throw new Exception("Bank account not found");

        if (dto.GLAccountId.HasValue)
        {
            await ValidateGLAccountAsync(dto.GLAccountId.Value, tenantId, "bank");
        }

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
        var tenantId = TenantId;
        var account = await _context.BankAccounts
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == id && !a.IsDeleted)
            ?? throw new Exception("Bank account not found");

        // Check if account has transactions
        var hasTransactions = await _context.Set<CashTransaction>()
            .AnyAsync(t => t.TenantId == tenantId && t.BankAccountId == id);

        if (hasTransactions)
        {
            throw new Exception("Cannot delete bank account with existing transactions");
        }

        account.IsDeleted = true;
        account.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    public async Task<BankAccountBalanceDto> GetBalanceAsync(Guid id)
    {
        var tenantId = TenantId;
        var account = await _context.BankAccounts
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == id && !a.IsDeleted)
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
        var tenantId = TenantId;
        var bankAccountExists = await _context.BankAccounts
            .AnyAsync(a => a.TenantId == tenantId && a.Id == id && !a.IsDeleted);
        if (!bankAccountExists)
        {
            throw new Exception("Bank account not found");
        }

        var query = _context.Set<CashTransaction>()
            .Where(t => t.TenantId == tenantId && t.BankAccountId == id && !t.IsDeleted);

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
        var tenantId = TenantId;
        var account = await _context.BankAccounts
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == id && !a.IsDeleted)
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

    private static string NormalizeCurrency(string? currency)
    {
        return string.IsNullOrWhiteSpace(currency)
            ? "GHS"
            : currency.Trim().ToUpperInvariant();
    }

    private async Task ValidateGLAccountAsync(Guid accountId, Guid tenantId, string label)
    {
        var accountExists = await _context.Accounts
            .AsNoTracking()
            .AnyAsync(a => a.TenantId == tenantId && a.Id == accountId && !a.IsDeleted && a.Status == AccountStatus.Active);

        if (!accountExists)
        {
            throw new InvalidOperationException($"The {label} GL account was not found for this tenant.");
        }
    }
}
