using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Services.Finance;
using ErpSystem.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ErpSystem.Api.Services.Finance.GL
{
    public class AccountService : IAccountService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUser;
        private readonly ILogger<AccountService> _logger;

        public AccountService(
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUser,
            ILogger<AccountService> logger)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
            _logger = logger;
        }

        private Guid TenantId => _currentUser.TenantId ?? Guid.Empty;
        private string UserName => _currentUser.UserName ?? "system";

        public async Task<AccountDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            // DIAGNOSTIC
            _logger.LogInformation("GetByIdAsync: Looking for Account {Id} for Tenant {TenantId}", id, TenantId);
            
            var account = await _unitOfWork.Accounts
                .GetQueryable(a => a.TenantId == TenantId && a.Id == id)
                .Include(a => a.SegmentValues)
                    .ThenInclude(v => v.SegmentStructure)
                .FirstOrDefaultAsync(cancellationToken);

            if (account == null)
            {
                _logger.LogWarning("GetByIdAsync: Account NOT FOUND. Checking if it exists under ANY tenant...");
                var anyAccount = await _unitOfWork.Accounts.GetQueryable(a => a.Id == id).FirstOrDefaultAsync(cancellationToken);
                if (anyAccount != null)
                {
                    _logger.LogWarning("GetByIdAsync: Account FOUND but Tenant mismatch! Account Tenant: {AccountTenantId}, User Tenant: {UserTenantId}", anyAccount.TenantId, TenantId);
                }
                else
                {
                    _logger.LogWarning("GetByIdAsync: Account definitely does not exist with this ID.");
                }
                return null;
            }

            return MapToDto(account);
        }

        public async Task<AccountDto?> GetByAccountNumberAsync(
            string accountNumber,
            CancellationToken cancellationToken = default)
        {
            var account = await _unitOfWork.Accounts
                .GetQueryable(a => a.TenantId == TenantId && a.AccountNumber == accountNumber)
                .Include(a => a.SegmentValues)
                    .ThenInclude(v => v.SegmentStructure)
                .FirstOrDefaultAsync(cancellationToken);

            return account == null ? null : MapToDto(account);
        }

        public async Task<IReadOnlyList<AccountDto>> GetAllAsync(
            string? accountType = null,
            string? status = null,
            bool? isMultiCurrency = null,
            string? coaType = null,
            string? search = null,
            int? take = null,
            CancellationToken cancellationToken = default)
        {
            IQueryable<Account> query = _unitOfWork.Accounts
                .GetQueryable(a => a.TenantId == TenantId)
                .Include(a => a.SegmentValues);

            if (!string.IsNullOrWhiteSpace(accountType) &&
                Enum.TryParse<AccountType>(accountType.Trim(), ignoreCase: true, out var parsedAccountType))
            {
                query = query.Where(a => a.AccountType == parsedAccountType);
            }

            if (!string.IsNullOrWhiteSpace(status) &&
                Enum.TryParse<AccountStatus>(status.Trim(), ignoreCase: true, out var parsedStatus))
            {
                query = query.Where(a => a.Status == parsedStatus);
            }

            if (isMultiCurrency.HasValue)
            {
                query = query.Where(a => a.IsMultiCurrency == isMultiCurrency.Value);
            }

            if (!string.IsNullOrWhiteSpace(coaType))
            {
                var normalizedCoaType = coaType.Trim();
                if (normalizedCoaType.Equals("Segmented", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(a => a.IsSegmented);
                }
                else if (normalizedCoaType.Equals("Standard", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(a => !a.IsSegmented);
                }
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(a =>
                    a.AccountCode.Contains(term) ||
                    a.AccountNumber.Contains(term) ||
                    a.AccountName.Contains(term) ||
                    (a.AccountCategory != null && a.AccountCategory.Contains(term)) ||
                    (a.AccountSubCategory != null && a.AccountSubCategory.Contains(term)));
            }

            IQueryable<Account> orderedQuery = query
                .OrderBy(a => a.AccountNumber)
                .ThenBy(a => a.AccountName);

            if (take.HasValue)
            {
                var boundedTake = Math.Clamp(take.Value, 1, 5000);
                orderedQuery = orderedQuery.Take(boundedTake);
            }

            var accounts = await orderedQuery.ToListAsync(cancellationToken);

            return accounts.Select(MapToDto).ToList();
        }

        public async Task<IReadOnlyList<AccountDto>> GetByTypeAsync(
            string accountType,
            CancellationToken cancellationToken = default)
        {
            var accounts = await _unitOfWork.Accounts
                .GetByTypeAsync(TenantId, Enum.Parse<AccountType>(accountType));

            return accounts.Select(MapToDto).ToList();
        }

        public async Task<AccountDto> CreateAsync(
            AccountCreateDto dto,
            CancellationToken cancellationToken = default)
        {
            // Enforce Segmented structure
            dto.IsSegmented = true;

            // Validate segments are provided
            if (dto.SegmentValues == null || dto.SegmentValues.Count == 0)
            {
                throw new InvalidOperationException("Segment values are required for account creation.");
            }

            var now = DateTime.UtcNow;

            var account = new Account
            {
                TenantId = TenantId,
                AccountCode = dto.AccountCode ?? string.Empty,
                AccountNumber = string.Empty, // Will be generated from segments
                AccountName = dto.AccountName,
                AccountType = Enum.Parse<AccountType>(dto.AccountType),
                AccountCategory = dto.AccountCategory,
                AccountSubCategory = dto.AccountSubCategory,
                CurrencyCode = dto.CurrencyCode,
                IsMultiCurrency = dto.IsMultiCurrency,
                // Adjust property names to match Account entity:
                IsIFRSClassified = dto.IsIFRSClassified,
                IsBaseClassified = dto.IsBaseFrameworkClassified,
                IsLocalClassified = dto.IsLocalFrameworkClassified,
                IsControlAccount = dto.IsControlAccount,
                AllowDirectPosting = dto.IsPostingAllowed,
                ReferenceNumber = dto.ReferenceNumber ?? string.Empty,
                Status = Enum.TryParse<AccountStatus>(dto.Status, out var status) ? status : AccountStatus.Active,
                EffectiveDate = dto.EffectiveDate,
                ExpirationDate = dto.ExpirationDate,
                Metadata = dto.Metadata,
                Tags = dto.Tags,
                Priority = dto.Priority,
                IsSegmented = true,
                CreatedAt = now,
                CreatedBy = UserName
            };

            // Handle segments
            if (dto.SegmentValues is { Count: > 0 })
            {
                var positions = dto.SegmentValues.Select(v => v.SegmentPosition).ToList();
                if (positions.Count != positions.Distinct().Count())
                    throw new InvalidOperationException("Segment positions must be unique per account.");

                foreach (var seg in dto.SegmentValues.OrderBy(v => v.SegmentPosition))
                {
                    var segEntity = new AccountSegmentValue
                    {
                        TenantId = TenantId,
                        AccountId = account.Id, // Id will be set by AddAsync, but EF will fix relationships
                        SegmentStructureId = seg.SegmentStructureId,
                        SegmentValue = seg.SegmentValue,
                        SegmentLookupValueId = seg.SegmentLookupValueId,
                        SegmentPosition = seg.SegmentPosition,
                        IsLocked = seg.IsLocked,
                        EffectiveDate = seg.EffectiveDate ?? now,
                        EndDate = seg.EndDate,
                        CreatedAt = now,
                        CreatedBy = UserName
                    };

                    account.SegmentValues.Add(segEntity);
                }

                // Auto-generate AccountNumber from segments
                var orderedSegmentValues = account.SegmentValues
                    .OrderBy(v => v.SegmentPosition)
                    .Select(v => v.SegmentValue);

                // Get separator from settings
                var settings = await _unitOfWork.Repository<FinanceSettings>()
                    .GetQueryable(s => s.TenantId == TenantId)
                    .FirstOrDefaultAsync(cancellationToken);
                var separator = settings?.AccountSeparator ?? "-";

                account.AccountNumber = string.Join(separator, orderedSegmentValues);
            }

            // Persist via repository/UnitOfWork
            await _unitOfWork.Accounts.AddAsync(account);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return MapToDto(account);
        }

        public async Task<AccountDto> UpdateAsync(
            AccountUpdateDto dto,
            CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;

            var query = _unitOfWork.Accounts
                .GetQueryable(a => a.TenantId == TenantId && a.Id == dto.Id)
                .Include(a => a.SegmentValues);

            var account = await query.FirstOrDefaultAsync(cancellationToken);

            if (account == null)
                throw new KeyNotFoundException($"Account with Id '{dto.Id}' not found.");

            account.UpdatedAt = now;
            account.UpdatedBy = UserName;

            // Validation: Check for existing transactions before allowing classification changes
            bool isClassificationChanged = 
                account.AccountType != Enum.Parse<AccountType>(dto.AccountType) ||
                account.AccountCategory != dto.AccountCategory ||
                account.AccountSubCategory != dto.AccountSubCategory;

            if (isClassificationChanged)
            {
                var transactionCount = await _unitOfWork.Repository<AccountTransaction>()
                    .GetQueryable(t => t.AccountId == account.Id && !t.IsDeleted)
                    .CountAsync(cancellationToken);

                if (transactionCount > 0)
                {
                    throw new InvalidOperationException("Cannot change Account Type or Category because transactions already exist for this account.");
                }

                // Apply changes if no transactions
                account.AccountType = Enum.Parse<AccountType>(dto.AccountType);
                account.AccountCategory = dto.AccountCategory;
                account.AccountSubCategory = dto.AccountSubCategory;
            }

            account.AccountCode = dto.AccountCode ?? account.AccountCode;
            account.AccountNumber = dto.AccountNumber;
            account.AccountName = dto.AccountName;
            // account.AccountType = Enum.Parse<AccountType>(dto.AccountType); // Handled above
            // account.AccountCategory = dto.AccountCategory; // Handled above
            // account.AccountSubCategory = dto.AccountSubCategory; // Handled above
            account.CurrencyCode = dto.CurrencyCode;
            account.IsMultiCurrency = dto.IsMultiCurrency;
            account.IsIFRSClassified = dto.IsIFRSClassified;
            account.IsBaseClassified = dto.IsBaseFrameworkClassified;
            account.IsLocalClassified = dto.IsLocalFrameworkClassified;
            account.IsControlAccount = dto.IsControlAccount;
            account.AllowDirectPosting = dto.IsPostingAllowed;
            account.ReferenceNumber = dto.ReferenceNumber ?? account.ReferenceNumber;
            if (!string.IsNullOrEmpty(dto.Status) && Enum.TryParse<AccountStatus>(dto.Status, out var newStatus))
            {
                account.Status = newStatus;
            }
            account.EffectiveDate = dto.EffectiveDate;
            account.ExpirationDate = dto.ExpirationDate;
            account.Metadata = dto.Metadata;
            account.Tags = dto.Tags;
            account.Priority = dto.Priority;
            account.IsSegmented = true; // Always true

            // Update segments if provided
            if (dto.SegmentValues is { Count: > 0 })
            {
                // Simple: remove all + re-add
                await _unitOfWork.AccountSegmentValues.DeleteRangeAsync(
                    v => v.TenantId == TenantId && v.AccountId == account.Id);

                account.SegmentValues.Clear();

                foreach (var seg in dto.SegmentValues.OrderBy(v => v.SegmentPosition))
                {
                    var segEntity = new AccountSegmentValue
                    {
                        TenantId = TenantId,
                        AccountId = account.Id,
                        SegmentStructureId = seg.SegmentStructureId,
                        SegmentValue = seg.SegmentValue,
                        SegmentLookupValueId = seg.SegmentLookupValueId,
                        SegmentPosition = seg.SegmentPosition,
                        IsLocked = seg.IsLocked,
                        EffectiveDate = seg.EffectiveDate ?? now,
                        EndDate = seg.EndDate,
                        CreatedAt = now,
                        CreatedBy = UserName
                    };

                    account.SegmentValues.Add(segEntity);
                }

                var orderedSegmentValues = account.SegmentValues
                    .OrderBy(v => v.SegmentPosition)
                    .Select(v => v.SegmentValue);

                // Get separator from settings
                var settings = await _unitOfWork.Repository<FinanceSettings>()
                    .GetQueryable(s => s.TenantId == TenantId)
                    .FirstOrDefaultAsync(cancellationToken);
                var separator = settings?.AccountSeparator ?? "-";

                account.AccountNumber = string.Join(separator, orderedSegmentValues);
            }

            await _unitOfWork.Accounts.UpdateAsync(account);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return MapToDto(account);
        }

        public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var account = await _unitOfWork.Accounts
                .FirstOrDefaultAsync(a => a.TenantId == TenantId && a.Id == id);

            if (account == null)
                return;

            // Validation 1: Cannot delete Control Accounts
            if (account.IsControlAccount)
            {
                throw new InvalidOperationException($"Cannot delete account '{account.AccountCode}' because it is a Control Account. Control accounts are critical for system integration. Please deactivate it instead.");
            }

            // Validation 2: Cannot delete Accounts with Transactions
            var transactionCount = await _unitOfWork.Repository<AccountTransaction>()
                .GetQueryable(t => t.AccountId == id && !t.IsDeleted)
                .CountAsync(cancellationToken);

            if (transactionCount > 0)
            {
                throw new InvalidOperationException($"Cannot delete account '{account.AccountCode}' because it has {transactionCount} associated transaction(s). Please deactivate the account instead.");
            }

            await _unitOfWork.Accounts.DeleteAsync(account);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        public async Task ActivateAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var account = await _unitOfWork.Accounts
                .FirstOrDefaultAsync(a => a.TenantId == TenantId && a.Id == id);

            if (account == null)
                throw new KeyNotFoundException($"Account with Id '{id}' not found.");

            account.Status = AccountStatus.Active;
            account.UpdatedAt = DateTime.UtcNow;
            account.UpdatedBy = UserName;

            await _unitOfWork.Accounts.UpdateAsync(account);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        public async Task DeactivateAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var account = await _unitOfWork.Accounts
                .FirstOrDefaultAsync(a => a.TenantId == TenantId && a.Id == id);

            if (account == null)
                throw new KeyNotFoundException($"Account with Id '{id}' not found.");

            account.Status = AccountStatus.Inactive;
            account.UpdatedAt = DateTime.UtcNow;
            account.UpdatedBy = UserName;

            await _unitOfWork.Accounts.UpdateAsync(account);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // For now, a simple manual mapper.
        // Later we can replace this with AutoMapper.
        private AccountDto MapToDto(Account account)
        {
            var dto = new AccountDto
            {
                Id = account.Id,
                TenantId = account.TenantId,
                AccountCode = account.AccountCode,
                AccountNumber = account.AccountNumber,
                AccountName = account.AccountName,
                AccountType = account.AccountType.ToString(),
                AccountCategory = account.AccountCategory,
                AccountSubCategory = account.AccountSubCategory,
                CurrencyCode = account.CurrencyCode,
                IsMultiCurrency = account.IsMultiCurrency,
                IsIFRSClassified = account.IsIFRSClassified,
                IsBaseFrameworkClassified = account.IsBaseClassified,
                IsLocalFrameworkClassified = account.IsLocalClassified,
                IsControlAccount = account.IsControlAccount,
                IsPostingAllowed = account.AllowDirectPosting,
                ReferenceNumber = account.ReferenceNumber,
                Status = account.Status.ToString(),
                EffectiveDate = account.EffectiveDate,
                ExpirationDate = account.ExpirationDate,
                Metadata = account.Metadata,
                Tags = account.Tags,
                Priority = account.Priority,
                CreatedAt = account.CreatedAt,
                UpdatedAt = account.UpdatedAt,
                CreatedBy = account.CreatedBy,
                UpdatedBy = account.UpdatedBy,
                IsSegmented = account.IsSegmented
            };

            dto.IsActive =
                account.Status == AccountStatus.Active &&
                (account.EffectiveDate == null || account.EffectiveDate <= DateTime.UtcNow) &&
                (account.ExpirationDate == null || account.ExpirationDate > DateTime.UtcNow);

            dto.SegmentValues = account.SegmentValues
                .OrderBy(v => v.SegmentPosition)
                .Select(v => new AccountSegmentValueDto
                {
                    Id = v.Id,
                    TenantId = v.TenantId,
                    AccountId = v.AccountId,
                    SegmentStructureId = v.SegmentStructureId,
                    SegmentValue = v.SegmentValue,
                    SegmentLookupValueId = v.SegmentLookupValueId,
                    SegmentPosition = v.SegmentPosition,
                    IsLocked = v.IsLocked,
                    EffectiveDate = v.EffectiveDate,
                    EndDate = v.EndDate,
                    SegmentName = v.SegmentStructure?.SegmentName,
                    SegmentCode = v.SegmentStructure?.SegmentCode,
                    CreatedAt = v.CreatedAt,
                    UpdatedAt = v.UpdatedAt,
                    CreatedBy = v.CreatedBy,
                    UpdatedBy = v.UpdatedBy
                })
                .ToList();

            return dto;
        }

        #region Multi-Currency Management

        public async Task<CurrencyLinkDto> AddCurrencyLinkAsync(AddCurrencyLinkDto dto)
        {
            var account = await _unitOfWork.Accounts
                .GetQueryable(a => a.TenantId == TenantId && a.Id == dto.AccountId)
                .Include(a => a.CurrencyLinks)
                .FirstOrDefaultAsync();

            if (account == null)
                throw new ArgumentException($"Account {dto.AccountId} not found");

            var existingLink = account.CurrencyLinks.FirstOrDefault(c => c.LinkedCurrencyCode == dto.CurrencyCode);
            if (existingLink != null)
            {
                if (existingLink.IsActive)
                    throw new InvalidOperationException($"Currency {dto.CurrencyCode} is already linked");
                existingLink.IsActive = true;
                await _unitOfWork.SaveChangesAsync();
                return MapCurrencyLinkToDto(existingLink, account);
            }

            var currencyLink = new AccountCurrencyLink
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                AccountId = dto.AccountId,
                LinkedCurrencyCode = dto.CurrencyCode,
                ForeignCurrencyBalance = dto.OpeningBalance ?? 0,
                IsActive = dto.IsActive
            };

            account.CurrencyLinks.Add(currencyLink);
            await _unitOfWork.SaveChangesAsync();
            return MapCurrencyLinkToDto(currencyLink, account);
        }

        public async Task<CurrencyLinkRemovalResultDto> RemoveCurrencyLinkAsync(RemoveCurrencyLinkDto dto)
        {
            var result = new CurrencyLinkRemovalResultDto();
            var account = await _unitOfWork.Accounts
                .GetQueryable(a => a.TenantId == TenantId && a.Id == dto.AccountId)
                .Include(a => a.CurrencyLinks)
                .FirstOrDefaultAsync();

            if (account == null)
                throw new ArgumentException($"Account {dto.AccountId} not found");

            var currencyLink = account.CurrencyLinks.FirstOrDefault(c => c.LinkedCurrencyCode == dto.CurrencyCode);
            if (currencyLink == null)
            {
                result.Success = false;
                result.Message = $"Currency {dto.CurrencyCode} not linked";
                return result;
            }

            // Check if account has any transactions
            var transactionCount = await _unitOfWork.Repository<AccountTransaction>()
                .GetQueryable(t => t.AccountId == dto.AccountId && !t.IsDeleted)
                .CountAsync();

            result.TransactionCount = transactionCount;

            if (transactionCount > 0 && !dto.ForceRemove)
            {
                currencyLink.IsActive = false;
                await _unitOfWork.SaveChangesAsync();
                result.Success = true;
                result.WasInactivated = true;
                result.Message = $"Currency inactivated ({transactionCount} transactions)";
                return result;
            }

            account.CurrencyLinks.Remove(currencyLink);
            await _unitOfWork.SaveChangesAsync();
            result.Success = true;
            result.WasDeleted = true;
            result.Message = "Currency removed";
            return result;
        }

        public async Task<CurrencyLinkDto> InactivateCurrencyLinkAsync(Guid accountId, string currencyCode)
        {
            var account = await _unitOfWork.Accounts
                .GetQueryable(a => a.TenantId == TenantId && a.Id == accountId)
                .Include(a => a.CurrencyLinks)
                .FirstOrDefaultAsync();

            if (account == null)
                throw new ArgumentException($"Account {accountId} not found");

            var currencyLink = account.CurrencyLinks.FirstOrDefault(c => c.LinkedCurrencyCode == currencyCode);
            if (currencyLink == null)
                throw new ArgumentException($"Currency {currencyCode} not linked");

            currencyLink.IsActive = false;
            await _unitOfWork.SaveChangesAsync();
            return MapCurrencyLinkToDto(currencyLink, account);
        }

        public async Task<List<CurrencyLinkDto>> GetAccountCurrencyLinksAsync(Guid accountId, bool includeInactive = false)
        {
            var account = await _unitOfWork.Accounts
                .GetQueryable(a => a.TenantId == TenantId && a.Id == accountId)
                .Include(a => a.CurrencyLinks)
                .FirstOrDefaultAsync();

            if (account == null)
                throw new ArgumentException($"Account {accountId} not found");

            var links = includeInactive ? account.CurrencyLinks.ToList() : account.CurrencyLinks.Where(c => c.IsActive).ToList();
            var result = new List<CurrencyLinkDto>();

            foreach (var link in links)
            {
                var dto = MapCurrencyLinkToDto(link, account);
                // Get transaction count for account
                dto.TransactionCount = await _unitOfWork.Repository<AccountTransaction>()
                    .GetQueryable(t => t.AccountId == accountId && !t.IsDeleted)
                    .CountAsync();
                dto.HasTransactions = dto.TransactionCount > 0;
                result.Add(dto);
            }

            return result;
        }

        private CurrencyLinkDto MapCurrencyLinkToDto(AccountCurrencyLink link, Account account)
        {
            return new CurrencyLinkDto
            {
                Id = link.Id,
                AccountId = link.AccountId,
                AccountNumber = account.AccountNumber,
                AccountName = account.AccountName,
                CurrencyCode = link.LinkedCurrencyCode,
                CurrencyName = link.LinkedCurrencyCode, // Use code as name for now
                CurrentBalance = link.ForeignCurrencyBalance,
                CurrentBalanceBaseCurrency = 0, // TODO: Calculate from exchange rate
                IsActive = link.IsActive,
                CreatedDate = link.CreatedAt
            };
        }

        #endregion
    }
}
