using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Api.Services.Finance;

namespace ErpSystem.Api.Services.Finance.UnitAccounting
{
    /// <summary>
    /// Service implementation for managing Unit Accounts.
    /// Unit Accounts are hierarchical accounts for tracking non-financial quantities.
    /// </summary>
    public class UnitAccountService : IUnitAccountService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<UnitAccountService> _logger;

        public UnitAccountService(
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUserService,
            ILogger<UnitAccountService> logger)
        {
            _unitOfWork = unitOfWork;
            _currentUserService = currentUserService;
            _logger = logger;
        }

        private Guid TenantId => _currentUserService.GetRequiredFinanceTenantId();
        private string UserName => _currentUserService.UserName ?? "system";

        public async Task<IReadOnlyList<UnitAccountDto>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            var accounts = await _unitOfWork.Repository<UnitAccount>()
                .GetQueryable(ua => ua.TenantId == TenantId && !ua.IsDeleted)
                .Include(ua => ua.UnitType)
                .OrderBy(ua => ua.AccountNumber)
                .ToListAsync(cancellationToken);

            return accounts.Select(MapToDto).ToList();
        }

        public async Task<IReadOnlyList<UnitAccountHierarchyDto>> GetHierarchyAsync(CancellationToken cancellationToken = default)
        {
            var accounts = await _unitOfWork.Repository<UnitAccount>()
                .GetQueryable(ua => ua.TenantId == TenantId && !ua.IsDeleted)
                .Include(ua => ua.UnitType)
                .OrderBy(ua => ua.AccountNumber)
                .ToListAsync(cancellationToken);

            // Build hierarchy - get root accounts (no parent)
            var rootAccounts = accounts.Where(a => a.ParentAccountId == null).ToList();
            return rootAccounts.Select(r => MapToHierarchyDto(r, accounts)).ToList();
        }

        public async Task<IReadOnlyList<UnitAccountDto>> GetByUnitTypeAsync(Guid unitTypeId, CancellationToken cancellationToken = default)
        {
            var accounts = await _unitOfWork.Repository<UnitAccount>()
                .GetQueryable(ua => ua.TenantId == TenantId && ua.UnitTypeId == unitTypeId && !ua.IsDeleted)
                .Include(ua => ua.UnitType)
                .OrderBy(ua => ua.AccountNumber)
                .ToListAsync(cancellationToken);

            return accounts.Select(MapToDto).ToList();
        }

        public async Task<IReadOnlyList<UnitAccountDto>> GetPostingAccountsAsync(CancellationToken cancellationToken = default)
        {
            var accounts = await _unitOfWork.Repository<UnitAccount>()
                .GetQueryable(ua => ua.TenantId == TenantId && ua.IsPostingAccount && ua.IsActive && !ua.IsDeleted)
                .Include(ua => ua.UnitType)
                .OrderBy(ua => ua.AccountNumber)
                .ToListAsync(cancellationToken);

            return accounts.Select(MapToDto).ToList();
        }

        public async Task<UnitAccountDetailDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var account = await _unitOfWork.Repository<UnitAccount>()
                .GetQueryable(ua => ua.Id == id && ua.TenantId == TenantId && !ua.IsDeleted)
                .Include(ua => ua.UnitType)
                .Include(ua => ua.ParentAccount)
                .FirstOrDefaultAsync(cancellationToken);

            if (account == null) return null;

            var balances = await _unitOfWork.Repository<UnitAccountBalance>()
                .GetQueryable(b => b.UnitAccountId == id && !b.IsDeleted)
                .OrderByDescending(b => b.FiscalYear!.Year)
                .ThenByDescending(b => b.FiscalPeriod!.PeriodNumber)
                .Take(12)
                .ToListAsync(cancellationToken);

            return MapToDetailDto(account, balances);
        }

        public async Task<UnitAccountDto?> GetByAccountNumberAsync(string accountNumber, CancellationToken cancellationToken = default)
        {
            var account = await _unitOfWork.Repository<UnitAccount>()
                .GetQueryable(ua => ua.AccountNumber == accountNumber && ua.TenantId == TenantId && !ua.IsDeleted)
                .Include(ua => ua.UnitType)
                .FirstOrDefaultAsync(cancellationToken);

            return account == null ? null : MapToDto(account);
        }

        public async Task<UnitAccountDto> CreateAsync(CreateUnitAccountDto dto, CancellationToken cancellationToken = default)
        {
            // Check for duplicate account number
            var exists = await _unitOfWork.Repository<UnitAccount>()
                .GetQueryable(ua => ua.TenantId == TenantId && ua.AccountNumber == dto.AccountNumber && !ua.IsDeleted)
                .AnyAsync(cancellationToken);

            if (exists)
                throw new InvalidOperationException($"Unit account with number '{dto.AccountNumber}' already exists.");

            // Validate unit type exists
            var unitType = await _unitOfWork.Repository<UnitType>()
                .FirstOrDefaultAsync(ut => ut.Id == dto.UnitTypeId && ut.TenantId == TenantId && !ut.IsDeleted);

            if (unitType == null)
                throw new ArgumentException($"Unit type with ID '{dto.UnitTypeId}' not found.");

            int accountLevel = 1;
            if (dto.ParentAccountId.HasValue)
            {
                var parent = await _unitOfWork.Repository<UnitAccount>()
                    .FirstOrDefaultAsync(ua => ua.TenantId == TenantId && ua.Id == dto.ParentAccountId.Value && !ua.IsDeleted);
                if (parent == null)
                    throw new ArgumentException($"Parent unit account with ID '{dto.ParentAccountId}' not found.");

                accountLevel = parent.AccountLevel + 1;
            }

            var now = DateTime.UtcNow;
            var account = new UnitAccount
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                AccountNumber = dto.AccountNumber,
                Name = dto.Name,
                Description = dto.Description,
                UnitTypeId = dto.UnitTypeId,
                ParentAccountId = dto.ParentAccountId,
                AccountLevel = accountLevel,
                IsPostingAccount = dto.IsPostingAccount,
                IsActive = true,
                CurrentBalance = 0,
                CreatedAt = now,
                CreatedBy = UserName
            };

            await _unitOfWork.Repository<UnitAccount>().AddAsync(account);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Unit account {AccountNumber} created by {User}", account.AccountNumber, UserName);

            // Reload with includes
            var created = await GetByAccountNumberAsync(dto.AccountNumber, cancellationToken);
            return created!;
        }

        public async Task<UnitAccountDto> UpdateAsync(Guid id, UpdateUnitAccountDto dto, CancellationToken cancellationToken = default)
        {
            var account = await _unitOfWork.Repository<UnitAccount>()
                .GetQueryable(ua => ua.Id == id && ua.TenantId == TenantId && !ua.IsDeleted)
                .Include(ua => ua.UnitType)
                .FirstOrDefaultAsync(cancellationToken);

            if (account == null)
                throw new ArgumentException($"Unit account with ID '{id}' not found.");

            if (!string.IsNullOrEmpty(dto.Name))
                account.Name = dto.Name;
            if (!string.IsNullOrEmpty(dto.Description))
                account.Description = dto.Description;
            if (dto.IsPostingAccount.HasValue)
                account.IsPostingAccount = dto.IsPostingAccount.Value;

            account.UpdatedAt = DateTime.UtcNow;
            account.UpdatedBy = UserName;

            await _unitOfWork.Repository<UnitAccount>().UpdateAsync(account);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Unit account {AccountNumber} updated by {User}", account.AccountNumber, UserName);

            return MapToDto(account);
        }

        public async Task ActivateAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var account = await _unitOfWork.Repository<UnitAccount>()
                .FirstOrDefaultAsync(ua => ua.Id == id && ua.TenantId == TenantId && !ua.IsDeleted);

            if (account == null)
                throw new ArgumentException($"Unit account with ID '{id}' not found.");

            account.IsActive = true;
            account.UpdatedAt = DateTime.UtcNow;
            account.UpdatedBy = UserName;

            await _unitOfWork.Repository<UnitAccount>().UpdateAsync(account);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Unit account {AccountNumber} activated by {User}", account.AccountNumber, UserName);
        }

        public async Task DeactivateAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var account = await _unitOfWork.Repository<UnitAccount>()
                .FirstOrDefaultAsync(ua => ua.Id == id && ua.TenantId == TenantId && !ua.IsDeleted);

            if (account == null)
                throw new ArgumentException($"Unit account with ID '{id}' not found.");

            account.IsActive = false;
            account.UpdatedAt = DateTime.UtcNow;
            account.UpdatedBy = UserName;

            await _unitOfWork.Repository<UnitAccount>().UpdateAsync(account);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Unit account {AccountNumber} deactivated by {User}", account.AccountNumber, UserName);
        }

        public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var account = await _unitOfWork.Repository<UnitAccount>()
                .FirstOrDefaultAsync(ua => ua.Id == id && ua.TenantId == TenantId && !ua.IsDeleted);

            if (account == null)
                return;

            // Check for posted transactions
            var hasTransactions = await _unitOfWork.Repository<UnitJournalEntryLine>()
                .GetQueryable(l => l.UnitAccountId == id && !l.IsDeleted)
                .AnyAsync(cancellationToken);

            if (hasTransactions)
                throw new InvalidOperationException($"Cannot delete unit account '{account.AccountNumber}' because it has posted transactions.");

            // Check for child accounts
            var hasChildren = await _unitOfWork.Repository<UnitAccount>()
                .GetQueryable(ua => ua.ParentAccountId == id && !ua.IsDeleted)
                .AnyAsync(cancellationToken);

            if (hasChildren)
                throw new InvalidOperationException($"Cannot delete unit account '{account.AccountNumber}' because it has child accounts.");

            // Soft delete
            account.IsDeleted = true;
            account.DeletedAt = DateTime.UtcNow;
            account.DeletedBy = UserName;

            await _unitOfWork.Repository<UnitAccount>().UpdateAsync(account);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Unit account {AccountNumber} deleted by {User}", account.AccountNumber, UserName);
        }

        public async Task RecalculateBalancesAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var account = await _unitOfWork.Repository<UnitAccount>()
                .FirstOrDefaultAsync(ua => ua.Id == id && ua.TenantId == TenantId && !ua.IsDeleted);

            if (account == null)
                throw new ArgumentException($"Unit account with ID '{id}' not found.");

            // Sum all posted journal entry lines for this account
            var totalBalance = await _unitOfWork.Repository<UnitJournalEntryLine>()
                .GetQueryable(l => l.UnitAccountId == id && !l.IsDeleted)
                .Where(l => l.UnitJournalEntry!.Status == UnitJournalEntryStatus.Posted)
                .SumAsync(l => l.Quantity, cancellationToken);

            account.CurrentBalance = totalBalance;
            account.UpdatedAt = DateTime.UtcNow;
            account.UpdatedBy = UserName;

            await _unitOfWork.Repository<UnitAccount>().UpdateAsync(account);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Unit account {AccountNumber} balance recalculated to {Balance} by {User}", 
                account.AccountNumber, totalBalance, UserName);
        }

        public async Task<decimal> GetCurrentBalanceAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var account = await _unitOfWork.Repository<UnitAccount>()
                .FirstOrDefaultAsync(ua => ua.Id == id && ua.TenantId == TenantId && !ua.IsDeleted);

            return account?.CurrentBalance ?? 0;
        }

        public async Task<IReadOnlyList<UnitAccountBalanceDto>> GetBalancesAsync(
            Guid id,
            Guid? fiscalYearId = null,
            CancellationToken cancellationToken = default)
        {
            // Use IQueryable to avoid type mismatch with Include/Where chain
            IQueryable<UnitAccountBalance> query = _unitOfWork.Repository<UnitAccountBalance>()
                .GetQueryable(b => b.UnitAccountId == id && !b.IsDeleted)
                .Include(b => b.FiscalYear)
                .Include(b => b.FiscalPeriod);

            if (fiscalYearId.HasValue)
                query = query.Where(b => b.FiscalYearId == fiscalYearId.Value);

            var balances = await query
                .OrderByDescending(b => b.FiscalYear!.Year)
                .ThenByDescending(b => b.FiscalPeriod!.PeriodNumber)
                .ToListAsync(cancellationToken);

            return balances.Select(b => new UnitAccountBalanceDto
            {
                Id = b.Id,
                UnitAccountId = b.UnitAccountId,
                FiscalYearId = b.FiscalYearId,
                FiscalPeriodId = b.FiscalPeriodId,
                PeriodName = b.FiscalPeriod?.PeriodName ?? "",
                OpeningBalance = b.OpeningBalance,
                PeriodActivity = b.PeriodActivity,
                ClosingBalance = b.ClosingBalance
            }).ToList();
        }

        private UnitAccountDto MapToDto(UnitAccount account)
        {
            return new UnitAccountDto
            {
                Id = account.Id,
                AccountNumber = account.AccountNumber,
                Name = account.Name,
                Description = account.Description,
                UnitTypeId = account.UnitTypeId,
                UnitTypeName = account.UnitType?.Name ?? "",
                UnitTypeCode = account.UnitType?.Code ?? "",
                ParentAccountId = account.ParentAccountId,
                AccountLevel = account.AccountLevel,
                IsPostingAccount = account.IsPostingAccount,
                IsActive = account.IsActive,
                CurrentBalance = account.CurrentBalance,
                CreatedAt = account.CreatedAt,
                CreatedBy = account.CreatedBy,
                UpdatedAt = account.UpdatedAt,
                UpdatedBy = account.UpdatedBy
            };
        }

        private UnitAccountDetailDto MapToDetailDto(UnitAccount account, List<UnitAccountBalance> balances)
        {
            return new UnitAccountDetailDto
            {
                Id = account.Id,
                AccountNumber = account.AccountNumber,
                Name = account.Name,
                Description = account.Description,
                UnitTypeId = account.UnitTypeId,
                UnitTypeName = account.UnitType?.Name ?? "",
                ParentAccountId = account.ParentAccountId,
                ParentAccountNumber = account.ParentAccount?.AccountNumber,
                AccountLevel = account.AccountLevel,
                IsPostingAccount = account.IsPostingAccount,
                IsActive = account.IsActive,
                CurrentBalance = account.CurrentBalance,
                Balances = balances.Select(b => new UnitAccountBalanceDto
                {
                    Id = b.Id,
                    UnitAccountId = b.UnitAccountId,
                    FiscalYearId = b.FiscalYearId,
                    FiscalPeriodId = b.FiscalPeriodId,
                    PeriodName = b.FiscalPeriod?.PeriodName ?? "",
                    OpeningBalance = b.OpeningBalance,
                    PeriodActivity = b.PeriodActivity,
                    ClosingBalance = b.ClosingBalance
                }).ToList(),
                CreatedAt = account.CreatedAt,
                CreatedBy = account.CreatedBy,
                UpdatedAt = account.UpdatedAt,
                UpdatedBy = account.UpdatedBy
            };
        }

        private UnitAccountHierarchyDto MapToHierarchyDto(UnitAccount account, List<UnitAccount> allAccounts)
        {
            var children = allAccounts.Where(a => a.ParentAccountId == account.Id).ToList();
            return new UnitAccountHierarchyDto
            {
                Id = account.Id,
                AccountNumber = account.AccountNumber,
                Name = account.Name,
                UnitTypeName = account.UnitType?.Name ?? "",
                AccountLevel = account.AccountLevel,
                IsPostingAccount = account.IsPostingAccount,
                IsActive = account.IsActive,
                CurrentBalance = account.CurrentBalance,
                Children = children.Select(c => MapToHierarchyDto(c, allAccounts)).ToList()
            };
        }
    }
}
