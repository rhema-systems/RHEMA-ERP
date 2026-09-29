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
    private const string BankAccountOpening = "BankAccountOpening";
    private const string OpeningBalanceBatch = "OpeningBalanceBatch";
    private const string MigrationSourceModule = "MIGRATION";
    private const string StatusPosted = "Posted";
    private const string StatusRejected = "Rejected";
    private readonly ApplicationDbContext _context;
    private readonly ITenantSettingsService _tenantSettingsService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IFinanceAccessScopeService? _financeAccessScopeService;

    public BankAccountService(
        ApplicationDbContext context,
        ITenantSettingsService tenantSettingsService,
        ICurrentUserService currentUserService,
        IFinanceAccessScopeService? financeAccessScopeService = null)
    {
        _context = context;
        _tenantSettingsService = tenantSettingsService;
        _currentUserService = currentUserService;
        _financeAccessScopeService = financeAccessScopeService;
    }

    private Guid TenantId => _currentUserService.GetRequiredFinanceTenantId();

    public async Task<BankAccountDto?> GetByIdAsync(Guid id)
    {
        var tenantId = TenantId;
        var query = _context.BankAccounts
            .Where(a => a.TenantId == tenantId && a.Id == id && !a.IsDeleted);
        var permittedIds = await GetPermittedBankAccountIdsAsync(FinanceAccessLevel.Read);
        if (permittedIds != null)
            query = query.Where(a => permittedIds.Contains(a.Id));

        var account = await query
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
                IsActive = a.IsActive,
                GovernedOpeningDate = _context.OpeningBalanceLines
                    .Where(line => line.TenantId == tenantId &&
                        line.BankAccountId == a.Id &&
                        line.CounterpartyType == BankAccountOpening &&
                        line.Batch.PostedAt != null &&
                        !line.Batch.Reversals.Any(reversal => reversal.Status == "Posted"))
                    .Select(line => (DateTime?)line.Batch.OpeningDate)
                    .FirstOrDefault(),
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
        var query = _context.BankAccounts
            .Where(a => a.TenantId == tenantId && !a.IsDeleted);
        var permittedIds = await GetPermittedBankAccountIdsAsync(FinanceAccessLevel.Read);
        if (permittedIds != null)
            query = query.Where(a => permittedIds.Contains(a.Id));

        return await query
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
                IsActive = a.IsActive,
                GovernedOpeningDate = _context.OpeningBalanceLines
                    .Where(line => line.TenantId == tenantId &&
                        line.BankAccountId == a.Id &&
                        line.CounterpartyType == BankAccountOpening &&
                        line.Batch.PostedAt != null &&
                        !line.Batch.Reversals.Any(reversal => reversal.Status == "Posted"))
                    .Select(line => (DateTime?)line.Batch.OpeningDate)
                    .FirstOrDefault(),
                ClosingDate = a.ClosingDate,
                Notes = a.Notes,
                CreatedAt = a.CreatedAt
            })
            .ToListAsync();
    }

    public async Task<IEnumerable<BankAccountDto>> GetActiveAccountsAsync()
    {
        var tenantId = TenantId;
        var query = _context.BankAccounts
            .Where(a => a.TenantId == tenantId && a.IsActive && !a.IsDeleted);
        var permittedIds = await GetPermittedBankAccountIdsAsync(FinanceAccessLevel.Read);
        if (permittedIds != null)
            query = query.Where(a => permittedIds.Contains(a.Id));

        return await query
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
        var accountNumber = dto.AccountNumber?.Trim()
            ?? throw new ArgumentException("Bank account number is required.");
        if (accountNumber.Length == 0)
            throw new ArgumentException("Bank account number is required.");
        var duplicateExists = await _context.BankAccounts.AnyAsync(account =>
            account.TenantId == tenantId &&
            !account.IsDeleted &&
            account.AccountNumber == accountNumber);
        if (duplicateExists)
            throw new InvalidOperationException($"Bank account number '{accountNumber}' already exists for this tenant.");

        if (dto.GLAccountId.HasValue)
        {
            await ValidateGLAccountAsync(dto.GLAccountId.Value, tenantId, "bank", currency);
        }

        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var dbTransaction = await _context.Database.BeginTransactionAsync();

            var account = new BankAccount
            {
                TenantId = tenantId,
                AccountNumber = accountNumber,
                AccountName = dto.AccountName,
                BankName = dto.BankName,
                BankBranch = dto.BankBranch,
                Currency = currency,
                AccountType = dto.AccountType,
                GLAccountId = dto.GLAccountId,
                // These legacy persistence fields are no longer client inputs. A newly
                // created bank master starts at zero; the governed opening workflow is
                // the only authority that can establish its ledger opening and snapshots.
                OpeningBalance = 0m,
                CurrentBalance = 0m,
                AvailableBalance = 0m,
                OpeningDate = default,
                Notes = dto.Notes,
                IsActive = true
            };

            _context.BankAccounts.Add(account);
            await _context.SaveChangesAsync();
            if (account.GLAccountId.HasValue)
            {
                _context.LiquidityAccounts.Add(new LiquidityAccount
                {
                    TenantId = tenantId,
                    Code = await BuildUniqueBankLiquidityCodeAsync(account, tenantId),
                    Name = account.AccountName,
                    AccountType = LiquidityAccountType.Bank,
                    Currency = account.Currency,
                    GLAccountId = account.GLAccountId.Value,
                    BankAccountId = account.Id,
                    IsActive = account.IsActive,
                    IsSystemAccount = true,
                    AllowsManualAllocations = false,
                    Notes = "Bank subtype maintained automatically from the bank account master.",
                    CreatedById = account.CreatedById,
                    CreatedBy = account.CreatedBy
                });
                await _context.SaveChangesAsync();
            }

            await dbTransaction.CommitAsync();

            return await GetByIdAsync(account.Id) ?? throw new Exception("Failed to create bank account");
        });
    }

    public async Task<BankAccountDto> UpdateAsync(Guid id, UpdateBankAccountDto dto)
    {
        var tenantId = TenantId;
        await EnsureBankAccountAccessAsync(id, FinanceAccessLevel.Administer);
        var account = await _context.BankAccounts
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == id && !a.IsDeleted)
            ?? throw new Exception("Bank account not found");

        var changesGovernedIdentity = account.GLAccountId != dto.GLAccountId;
        var deactivatesGovernedMaster = account.IsActive && !dto.IsActive;
        if ((changesGovernedIdentity || deactivatesGovernedMaster) &&
            await HasBlockingGovernedOpeningEvidenceAsync(tenantId, account.Id))
        {
            throw new InvalidOperationException(
                "This bank account has active or posted governed opening evidence; its GL mapping and active state are locked to preserve bank/GL reconciliation.");
        }

        if (dto.GLAccountId.HasValue)
        {
            await ValidateGLAccountAsync(dto.GLAccountId.Value, tenantId, "bank", account.Currency);
        }

        account.AccountName = dto.AccountName;
        account.BankName = dto.BankName;
        account.BankBranch = dto.BankBranch;
        account.GLAccountId = dto.GLAccountId;
        account.IsActive = dto.IsActive;
        account.Notes = dto.Notes;

        var liquidityAccount = await _context.LiquidityAccounts
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.BankAccountId == account.Id);
        if (account.GLAccountId.HasValue)
        {
            if (liquidityAccount == null)
            {
                liquidityAccount = new LiquidityAccount
                {
                    TenantId = tenantId,
                    Code = await BuildUniqueBankLiquidityCodeAsync(account, tenantId),
                    AccountType = LiquidityAccountType.Bank,
                    Currency = account.Currency,
                    BankAccountId = account.Id,
                    IsSystemAccount = true,
                    AllowsManualAllocations = false,
                    CreatedBy = _currentUserService.UserName
                };
                _context.LiquidityAccounts.Add(liquidityAccount);
            }
            liquidityAccount.Name = account.AccountName;
            liquidityAccount.GLAccountId = account.GLAccountId.Value;
            liquidityAccount.IsActive = account.IsActive;
            liquidityAccount.UpdatedAt = DateTime.UtcNow;
            liquidityAccount.UpdatedBy = _currentUserService.UserName;
        }
        else if (liquidityAccount != null)
        {
            liquidityAccount.IsActive = false;
            liquidityAccount.UpdatedAt = DateTime.UtcNow;
            liquidityAccount.UpdatedBy = _currentUserService.UserName;
        }

        await _context.SaveChangesAsync();

        return await GetByIdAsync(id) ?? throw new Exception("Failed to update bank account");
    }

    public async Task DeleteAsync(Guid id)
    {
        var tenantId = TenantId;
        await EnsureBankAccountAccessAsync(id, FinanceAccessLevel.Administer);
        var account = await _context.BankAccounts
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == id && !a.IsDeleted)
            ?? throw new Exception("Bank account not found");

        if (await HasBlockingGovernedOpeningEvidenceAsync(tenantId, account.Id))
        {
            throw new InvalidOperationException(
                "This bank account has active or posted governed opening evidence and cannot be deleted.");
        }

        // Check if account has transactions
        var hasTransactions = await _context.Set<CashTransaction>()
            .AnyAsync(t => t.TenantId == tenantId && t.BankAccountId == id);

        if (hasTransactions)
        {
            throw new Exception("Cannot delete bank account with existing transactions");
        }

        var hasActiveLiquidityAccount = await _context.LiquidityAccounts
            .AnyAsync(item =>
                item.TenantId == tenantId &&
                item.BankAccountId == id &&
                item.IsActive &&
                !item.IsDeleted);
        if (hasActiveLiquidityAccount)
        {
            // Finance owns both sides of this master-data boundary. Do not silently mutate the
            // liquidity register as a side effect of deleting a bank master; the controller must
            // first deactivate the bank, which explicitly synchronises its system wrapper.
            throw new InvalidOperationException(
                "Deactivate this bank account before deleting it; Finance will also deactivate its linked Bank liquidity account.");
        }

        account.IsDeleted = true;
        account.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    public async Task<BankAccountBalanceDto> GetBalanceAsync(Guid id)
    {
        var tenantId = TenantId;
        await EnsureBankAccountAccessAsync(id, FinanceAccessLevel.Read);
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
        await EnsureBankAccountAccessAsync(id, FinanceAccessLevel.Read);
        var bankAccountExists = await _context.BankAccounts
            .AnyAsync(a => a.TenantId == tenantId && a.Id == id && !a.IsDeleted);
        if (!bankAccountExists)
        {
            throw new Exception("Bank account not found");
        }

        var query = _context.Set<CashTransaction>()
            .Where(t => t.TenantId == tenantId && t.BankAccountId == id && !t.IsDeleted);

        if (fromDate.HasValue)
            query = query.Where(t => t.TransactionDate >= fromDate.Value.Date);

        if (toDate.HasValue)
        {
            var endExclusive = toDate.Value.Date.AddDays(1);
            query = query.Where(t => t.TransactionDate < endExclusive);
        }

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
        await Task.CompletedTask;
        throw new InvalidOperationException(
            "Direct bank-balance mutation is disabled. Post or reverse the source document through IFinancePostingEngine so the GL and bank snapshot remain reconcilable.");
    }

    private Task<IReadOnlyCollection<Guid>?> GetPermittedBankAccountIdsAsync(FinanceAccessLevel level)
        => _financeAccessScopeService == null
            ? Task.FromResult<IReadOnlyCollection<Guid>?>(null)
            : _financeAccessScopeService.GetPermittedBankAccountIdsAsync(level);

    private Task EnsureBankAccountAccessAsync(Guid bankAccountId, FinanceAccessLevel level)
        => _financeAccessScopeService == null
            ? Task.CompletedTask
            : _financeAccessScopeService.EnsureBankAccountAccessAsync(bankAccountId, level);

    private async Task<string> BuildUniqueBankLiquidityCodeAsync(BankAccount account, Guid tenantId)
    {
        var compact = new string(account.AccountNumber.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        var baseCode = $"BANK-{compact}";
        if (baseCode.Length > 30)
        {
            baseCode = baseCode[..30];
        }
        if (!await _context.LiquidityAccounts.AnyAsync(item => item.TenantId == tenantId && item.Code == baseCode))
        {
            return baseCode;
        }
        return $"BANK-{account.Id.ToString("N")[..8]}".ToUpperInvariant();
    }

    private Task<bool> HasBlockingGovernedOpeningEvidenceAsync(Guid tenantId, Guid bankAccountId)
        => _context.OpeningBalanceLines.AsNoTracking().AnyAsync(line =>
            line.TenantId == tenantId &&
            line.BankAccountId == bankAccountId &&
            line.CounterpartyType == BankAccountOpening &&
            !line.IsDeleted &&
            !line.Batch.IsDeleted &&
            !(line.Batch.Status == StatusRejected &&
              line.Batch.JournalEntryId == null &&
              line.Batch.PostingEventId == null &&
              line.Batch.PostedAt == null &&
              !_context.FinancePostingEvents.Any(postingEvent =>
                  postingEvent.TenantId == tenantId &&
                  postingEvent.SourceModule == MigrationSourceModule &&
                  postingEvent.SourceDocumentType == OpeningBalanceBatch &&
                  postingEvent.SourceDocumentId == line.Batch.Id &&
                  postingEvent.PostingStatus == StatusPosted &&
                  !postingEvent.IsDeleted)));

    private static string NormalizeCurrency(string? currency)
    {
        return string.IsNullOrWhiteSpace(currency)
            ? "GHS"
            : currency.Trim().ToUpperInvariant();
    }

    private async Task ValidateGLAccountAsync(
        Guid accountId,
        Guid tenantId,
        string label,
        string currency)
    {
        var account = await _context.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a =>
                a.TenantId == tenantId &&
                a.Id == accountId &&
                !a.IsDeleted);

        if (account == null)
        {
            throw new InvalidOperationException($"The {label} GL account was not found for this tenant.");
        }
        var now = DateTime.UtcNow;
        if (account.Status != AccountStatus.Active ||
            (account.EffectiveDate.HasValue && account.EffectiveDate.Value > now) ||
            (account.ExpirationDate.HasValue && account.ExpirationDate.Value <= now))
        {
            throw new InvalidOperationException($"The {label} GL account must be currently effective and active.");
        }
        if (account.AccountType != AccountType.Asset)
        {
            throw new InvalidOperationException($"The {label} GL account must be an Asset account.");
        }
        if (!account.IsMultiCurrency &&
            !account.CurrencyCode.Equals(currency, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"The {label} account currency ({currency}) must match the GL account currency ({account.CurrencyCode}).");
        }
        if (!account.IsControlAccount && !account.AllowDirectPosting)
        {
            throw new InvalidOperationException(
                $"The {label} GL account must be a protected control account or allow direct posting.");
        }
    }
}
