using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.Cash;

public class BankAccountService : IBankAccountService
{
    private readonly ApplicationDbContext _context;

    public BankAccountService(ApplicationDbContext context)
    {
        _context = context;
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
        var account = new BankAccount
        {
            AccountNumber = dto.AccountNumber,
            AccountName = dto.AccountName,
            BankName = dto.BankName,
            BankBranch = dto.BankBranch,
            Currency = dto.Currency,
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
}
