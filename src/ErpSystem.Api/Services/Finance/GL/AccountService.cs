using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Services.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Api.Services.Finance;
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
        private readonly IAccountingBookService _accountingBookService;
        private readonly ILogger<AccountService> _logger;
        private readonly IAccountSegmentIdentityService? _segmentIdentityService;

        public AccountService(
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUser,
            IAccountingBookService accountingBookService,
            ILogger<AccountService> logger,
            IAccountSegmentIdentityService? segmentIdentityService = null)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
            _accountingBookService = accountingBookService;
            _logger = logger;
            _segmentIdentityService = segmentIdentityService;
        }

        private Guid TenantId => _currentUser.GetRequiredFinanceTenantId();
        private string UserName => _currentUser.UserName ?? "system";

        public async Task<AccountDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            // DIAGNOSTIC
            _logger.LogInformation("GetByIdAsync: Looking for Account {Id} for Tenant {TenantId}", id, TenantId);
            
            var account = await _unitOfWork.Accounts
                .GetQueryable(a => a.TenantId == TenantId && a.Id == id)
                .Include(a => a.SegmentValues)
                    .ThenInclude(v => v.SegmentStructure)
                .Include(a => a.AccountingBooks)
                    .ThenInclude(mapping => mapping.AccountingBook)
                .Include(a => a.AccountingBooks)
                    .ThenInclude(mapping => mapping.AccountClassification)
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
                .Include(a => a.AccountingBooks)
                    .ThenInclude(mapping => mapping.AccountingBook)
                .Include(a => a.AccountingBooks)
                    .ThenInclude(mapping => mapping.AccountClassification)
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
            await _accountingBookService.EnsureTenantDefaultsAsync(cancellationToken);

            IQueryable<Account> query = _unitOfWork.Accounts
                .GetQueryable(a => a.TenantId == TenantId)
                .Include(a => a.SegmentValues)
                .Include(a => a.AccountingBooks)
                    .ThenInclude(mapping => mapping.AccountingBook)
                .Include(a => a.AccountingBooks)
                    .ThenInclude(mapping => mapping.AccountClassification);

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
                    a.AccountingBooks.Any(mapping => !mapping.IsDeleted
                        && mapping.AccountClassification != null
                        && !mapping.AccountClassification.IsDeleted
                        && (mapping.AccountClassification.Code.Contains(term)
                            || mapping.AccountClassification.Name.Contains(term))));
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
            await _accountingBookService.EnsureTenantDefaultsAsync(cancellationToken);
            var identityService = _segmentIdentityService ?? throw new InvalidOperationException("The Finance account identity validator is unavailable.");
            var identity = await identityService.ValidateAndComposeAsync(TenantId, dto.SegmentValues, dto.AccountNumber, cancellationToken: cancellationToken);
            EnsureNaturalAccountCode(dto.AccountCode, identity.NaturalAccountCode);

            var account = new Account
            {
                TenantId = TenantId,
                AccountCode = identity.NaturalAccountCode,
                AccountNumber = identity.AccountNumber,
                AccountName = dto.AccountName,
                AccountType = Enum.Parse<AccountType>(dto.AccountType),
                AccountCategory = dto.AccountCategory,
                AccountSubCategory = dto.AccountSubCategory,
                CashFlowClassification = NormalizeCashFlowClassification(dto.CashFlowClassification),
                CurrencyCode = dto.CurrencyCode,
                IsMultiCurrency = dto.IsMultiCurrency,
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
            if (identity.Values.Count > 0)
            {
                foreach (var seg in identity.Values.OrderBy(v => v.SegmentPosition))
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

            }

            // Persist via repository/UnitOfWork
            await _unitOfWork.Accounts.AddAsync(account);
            // Mapping validation runs before the shared context commits so a rejected book or
            // classification cannot leave a partially-created account behind.
            await _accountingBookService.SyncAccountMappingsAsync(account, dto.AccountingBooks, cancellationToken);

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

            var submittedSegments = dto.SegmentValues.Count > 0
                ? dto.SegmentValues.Select(seg => new AccountSegmentValueCreateDto
                {
                    SegmentStructureId = seg.SegmentStructureId, SegmentPosition = seg.SegmentPosition,
                    SegmentValue = seg.SegmentValue, SegmentLookupValueId = seg.SegmentLookupValueId,
                    IsLocked = seg.IsLocked, EffectiveDate = seg.EffectiveDate, EndDate = seg.EndDate
                }).ToList()
                : account.SegmentValues.Select(seg => new AccountSegmentValueCreateDto
                {
                    SegmentStructureId = seg.SegmentStructureId, SegmentPosition = seg.SegmentPosition,
                    SegmentValue = seg.SegmentValue, SegmentLookupValueId = seg.SegmentLookupValueId,
                    IsLocked = seg.IsLocked, EffectiveDate = seg.EffectiveDate, EndDate = seg.EndDate
                }).ToList();
            var identityService = _segmentIdentityService ?? throw new InvalidOperationException("The Finance account identity validator is unavailable.");
            var identity = await identityService.ValidateAndComposeAsync(TenantId, submittedSegments, dto.AccountNumber, account.Id, cancellationToken);
            EnsureNaturalAccountCode(dto.AccountCode, identity.NaturalAccountCode);

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

            account.AccountCode = identity.NaturalAccountCode;
            account.AccountNumber = identity.AccountNumber;
            account.AccountName = dto.AccountName;
            account.CashFlowClassification = NormalizeCashFlowClassification(dto.CashFlowClassification);
            // account.AccountType = Enum.Parse<AccountType>(dto.AccountType); // Handled above
            // account.AccountCategory = dto.AccountCategory; // Handled above
            // account.AccountSubCategory = dto.AccountSubCategory; // Handled above
            account.CurrencyCode = dto.CurrencyCode;
            account.IsMultiCurrency = dto.IsMultiCurrency;
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

                foreach (var seg in identity.Values.OrderBy(v => v.SegmentPosition))
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

                    await _unitOfWork.Repository<AccountSegmentValue>().AddAsync(segEntity);
                }

                account.AccountNumber = identity.AccountNumber;
            }

            // The account and its replacement segment rows are already tracked. Calling Update on the
            // aggregate here would reclassify newly-added segment rows as Modified after EF generated
            // their GUIDs, producing updates for rows that do not yet exist.
            await _accountingBookService.SyncAccountMappingsAsync(account, dto.AccountingBooks, cancellationToken);

            return MapToDto(account);
        }

        private static void EnsureNaturalAccountCode(string? submittedCode, string naturalAccountCode)
        {
            if (!string.IsNullOrWhiteSpace(submittedCode)
                && !string.Equals(submittedCode.Trim(), naturalAccountCode, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"Account code must match the Natural Account segment value '{naturalAccountCode}'.");
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
                CashFlowClassification = account.CashFlowClassification,
                CurrencyCode = account.CurrencyCode,
                IsMultiCurrency = account.IsMultiCurrency,
                IsIFRSClassified = account.IsIFRSClassified,
                IsBaseFrameworkClassified = account.IsBaseClassified,
                IsLocalFrameworkClassified = account.IsLocalClassified,
                IsBaseClassified = account.IsBaseClassified,
                IsLocalClassified = account.IsLocalClassified,
                IsManagementClassified = account.IsLocalClassified,
                IsControlAccount = account.IsControlAccount,
                IsPostingAllowed = account.AllowDirectPosting,
                AllowDirectPosting = account.AllowDirectPosting,
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

            dto.AccountingBooks = account.AccountingBooks
                .Where(mapping => mapping.AccountingBook != null && !mapping.IsDeleted)
                .OrderBy(mapping => mapping.AccountingBook.SortOrder)
                .Select(mapping => new AccountAccountingBookDto
                {
                    Id = mapping.Id,
                    AccountId = mapping.AccountId,
                    AccountingBookId = mapping.AccountingBookId,
                    AccountingBookCode = mapping.AccountingBook.Code,
                    AccountingBookName = mapping.AccountingBook.Name,
                    AccountingBookIsDefault = mapping.AccountingBook.IsDefault,
                    IsEnabled = mapping.IsEnabled,
                    AccountClassificationId = mapping.AccountClassificationId,
                    AccountClassificationCode = mapping.AccountClassification?.Code,
                    AccountClassificationName = mapping.AccountClassification?.Name,
                    AccountClassificationSystemRole = mapping.AccountClassification?.SystemRole?.ToString(),
                    AccountClassificationStatus = mapping.AccountClassification?.Status.ToString(),
                    IsMigrationReady = !mapping.IsEnabled || mapping.AccountClassification is
                    {
                        Status: AccountClassificationStatus.Active,
                        IsPostingClassification: true
                    },
                    FinancialStatementLineItem = mapping.FinancialStatementLineItem,
                    RowVersion = mapping.RowVersion.Length == 0
                        ? string.Empty
                        : Convert.ToBase64String(mapping.RowVersion)
                })
                .ToList();

            return dto;
        }

        private static string? NormalizeCashFlowClassification(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            return value.Trim().ToUpperInvariant() switch
            {
                "OPERATING" => "Operating",
                "INVESTING" => "Investing",
                "FINANCING" => "Financing",
                _ => throw new ArgumentException(
                    "Cash-flow classification must be Operating, Investing, Financing, or blank.",
                    nameof(value))
            };
        }

        #region Multi-Currency Management

        public async Task<CurrencyLinkDto> AddCurrencyLinkAsync(AddCurrencyLinkDto dto)
        {
            var currencyCode = ResolveCurrencyCode(dto);
            var account = await _unitOfWork.Accounts
                .GetQueryable(a => a.TenantId == TenantId && a.Id == dto.AccountId)
                .Include(a => a.CurrencyLinks)
                .FirstOrDefaultAsync();

            if (account == null)
                throw new ArgumentException($"Account {dto.AccountId} not found");

            if (!account.IsMultiCurrency)
                throw new InvalidOperationException("Currency links can only be added to accounts with multi-currency enabled.");

            if (string.Equals(NormalizeCurrencyCode(account.CurrencyCode), currencyCode, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Currency {currencyCode} is the account primary currency and is linked implicitly.");

            var now = DateTime.UtcNow;
            var currentUserId = TryGetCurrentUserId();
            var existingLink = account.CurrencyLinks.FirstOrDefault(c =>
                string.Equals(NormalizeCurrencyCode(c.LinkedCurrencyCode), currencyCode, StringComparison.OrdinalIgnoreCase));
            if (existingLink != null)
            {
                if (existingLink.IsActive)
                    throw new InvalidOperationException($"Currency {currencyCode} is already linked");

                existingLink.LinkedCurrencyCode = currencyCode;
                existingLink.IsActive = true;
                existingLink.EffectiveEndDate = null;
                existingLink.InactivationReason = null;
                ApplyCurrencyLinkSettings(existingLink, dto, now, currentUserId);
                await _unitOfWork.SaveChangesAsync();
                return MapCurrencyLinkToDto(existingLink, account);
            }

            var currencyLink = new AccountCurrencyLink
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                AccountId = dto.AccountId,
                LinkedCurrencyCode = currencyCode,
                ForeignCurrencyBalance = RoundMoney(dto.OpeningBalance ?? 0m),
                BaseCurrencyEquivalent = RoundMoney(dto.OpeningBalanceBaseCurrency ?? 0m),
                CurrentExchangeRate = CalculateOpeningRate(dto.OpeningBalance, dto.OpeningBalanceBaseCurrency),
                RateEffectiveDate = dto.OpeningBalanceDate?.Date,
                RevaluationFrequency = ParseRevaluationFrequency(dto.RevaluationFrequency),
                TransactionRateType = NormalizeTransactionRateType(dto.TransactionRateType),
                TransactionQuoteSide = ParseQuoteSide(dto.TransactionQuoteSide),
                RevaluationRateType = NormalizeRateType(dto.RevaluationRateType, "Month-End"),
                RevaluationQuoteSide = ParseQuoteSide(dto.RevaluationQuoteSide),
                Notes = dto.Notes,
                IsActive = dto.IsActive,
                EffectiveDate = dto.OpeningBalanceDate?.Date ?? now,
                CreatedAt = now,
                CreatedBy = UserName,
                CreatedById = currentUserId,
                CreatedByUserId = currentUserId ?? Guid.Empty,
                CreatedDate = now
            };

            // The link has an application-assigned Guid. Adding it only through a
            // tracked account navigation can make EF infer Modified for the detached
            // dependent, which produces an UPDATE and then a concurrency exception
            // because the row does not exist yet. Explicitly register the new link as
            // Added while retaining the stable identity used by Finance evidence.
            await _unitOfWork.Repository<AccountCurrencyLink>().AddAsync(currencyLink);
            await _unitOfWork.SaveChangesAsync();
            return MapCurrencyLinkToDto(currencyLink, account);
        }

        public async Task<CurrencyLinkRemovalResultDto> RemoveCurrencyLinkAsync(RemoveCurrencyLinkDto dto)
        {
            var result = new CurrencyLinkRemovalResultDto();
            var currencyCode = NormalizeCurrencyCode(dto.CurrencyCode);
            var account = await _unitOfWork.Accounts
                .GetQueryable(a => a.TenantId == TenantId && a.Id == dto.AccountId)
                .Include(a => a.CurrencyLinks)
                .FirstOrDefaultAsync();

            if (account == null)
                throw new ArgumentException($"Account {dto.AccountId} not found");

            if (string.Equals(NormalizeCurrencyCode(account.CurrencyCode), currencyCode, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The account primary currency is linked implicitly and cannot be removed.");

            var currencyLink = account.CurrencyLinks.FirstOrDefault(c =>
                string.Equals(NormalizeCurrencyCode(c.LinkedCurrencyCode), currencyCode, StringComparison.OrdinalIgnoreCase));
            if (currencyLink == null)
            {
                result.Success = false;
                result.Message = $"Currency {currencyCode} not linked";
                return result;
            }

            // Check transaction history only for this account-currency pair.
            var transactionCount = await _unitOfWork.Repository<AccountTransaction>()
                .GetQueryable(t => t.AccountId == dto.AccountId
                    && t.TransactionCurrency == currencyCode
                    && !t.IsDeleted)
                .CountAsync();

            result.TransactionCount = transactionCount;

            if (transactionCount > 0 && !dto.ForceRemove)
            {
                currencyLink.IsActive = false;
                currencyLink.EffectiveEndDate = DateTime.UtcNow;
                currencyLink.InactivationReason = dto.Reason;
                currencyLink.UpdatedAt = DateTime.UtcNow;
                currencyLink.UpdatedBy = UserName;
                currencyLink.LastModifiedById = TryGetCurrentUserId();
                currencyLink.ModifiedByUserId = currencyLink.LastModifiedById;
                currencyLink.ModifiedDate = DateTime.UtcNow;
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
            var normalizedCurrencyCode = NormalizeCurrencyCode(currencyCode);
            var account = await _unitOfWork.Accounts
                .GetQueryable(a => a.TenantId == TenantId && a.Id == accountId)
                .Include(a => a.CurrencyLinks)
                .FirstOrDefaultAsync();

            if (account == null)
                throw new ArgumentException($"Account {accountId} not found");

            if (string.Equals(NormalizeCurrencyCode(account.CurrencyCode), normalizedCurrencyCode, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The account primary currency is linked implicitly and cannot be inactivated.");

            var currencyLink = account.CurrencyLinks.FirstOrDefault(c =>
                string.Equals(NormalizeCurrencyCode(c.LinkedCurrencyCode), normalizedCurrencyCode, StringComparison.OrdinalIgnoreCase));
            if (currencyLink == null)
                throw new ArgumentException($"Currency {normalizedCurrencyCode} not linked");

            currencyLink.IsActive = false;
            currencyLink.EffectiveEndDate = DateTime.UtcNow;
            currencyLink.UpdatedAt = DateTime.UtcNow;
            currencyLink.UpdatedBy = UserName;
            currencyLink.LastModifiedById = TryGetCurrentUserId();
            currencyLink.ModifiedByUserId = currencyLink.LastModifiedById;
            currencyLink.ModifiedDate = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync();
            return MapCurrencyLinkToDto(currencyLink, account);
        }

        public async Task<CurrencyLinkDto> UpdateCurrencyLinkRatePolicyAsync(
            Guid accountId,
            string currencyCode,
            UpdateCurrencyLinkRatePolicyDto dto)
        {
            var normalizedCurrencyCode = NormalizeCurrencyCode(currencyCode);
            var account = await _unitOfWork.Accounts
                .GetQueryable(a => a.TenantId == TenantId && a.Id == accountId)
                .Include(a => a.CurrencyLinks)
                .FirstOrDefaultAsync();

            if (account == null)
                throw new ArgumentException($"Account {accountId} not found");

            var link = account.CurrencyLinks.FirstOrDefault(c =>
                string.Equals(NormalizeCurrencyCode(c.LinkedCurrencyCode), normalizedCurrencyCode, StringComparison.OrdinalIgnoreCase));

            if (link == null)
                throw new ArgumentException($"Currency {normalizedCurrencyCode} not linked");

            if (!link.IsActive)
                throw new InvalidOperationException("Inactive currency-link policies are locked. Reactivate the link before changing its rate policy.");

            link.RevaluationFrequency = ParseRevaluationFrequency(dto.RevaluationFrequency);
            link.TransactionRateType = NormalizeTransactionRateType(dto.TransactionRateType);
            link.TransactionQuoteSide = ParseQuoteSide(dto.TransactionQuoteSide);
            link.RevaluationRateType = NormalizeRateType(dto.RevaluationRateType, "Month-End");
            link.RevaluationQuoteSide = ParseQuoteSide(dto.RevaluationQuoteSide);
            link.Notes = dto.Notes?.Trim();
            link.UpdatedAt = DateTime.UtcNow;
            link.UpdatedBy = UserName;
            link.LastModifiedById = TryGetCurrentUserId();
            link.ModifiedByUserId = link.LastModifiedById;
            link.ModifiedDate = link.UpdatedAt;

            await _unitOfWork.SaveChangesAsync();
            return MapCurrencyLinkToDto(link, account);
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
                var currencyCode = NormalizeCurrencyCode(link.LinkedCurrencyCode);
                var transactionCount = await _unitOfWork.Repository<AccountTransaction>()
                    .GetQueryable(t => t.AccountId == accountId
                        && t.TransactionCurrency == currencyCode
                        && !t.IsDeleted)
                    .CountAsync();
                dto.TransactionCount = Math.Max(link.TransactionCount, transactionCount);
                dto.HasTransactions = dto.TransactionCount > 0;
                result.Add(dto);
            }

            return result;
        }

        private CurrencyLinkDto MapCurrencyLinkToDto(AccountCurrencyLink link, Account account)
        {
            var currencyCode = NormalizeCurrencyCode(link.LinkedCurrencyCode);
            var currentExchangeRate = link.CurrentExchangeRate > 0m ? link.CurrentExchangeRate : (decimal?)null;
            return new CurrencyLinkDto
            {
                Id = link.Id,
                AccountId = link.AccountId,
                AccountNumber = account.AccountNumber,
                AccountName = account.AccountName,
                CurrencyCode = currencyCode,
                LinkedCurrencyCode = currencyCode,
                CurrencyName = currencyCode, // Use code as name for now
                CurrentBalance = link.ForeignCurrencyBalance,
                CurrentBalanceBaseCurrency = link.BaseCurrencyEquivalent,
                ForeignCurrencyBalance = link.ForeignCurrencyBalance,
                BaseCurrencyBalance = link.BaseCurrencyEquivalent,
                CurrentExchangeRate = currentExchangeRate,
                RevaluationFrequency = link.RevaluationFrequency.ToString(),
                TransactionRateType = link.TransactionRateType,
                TransactionQuoteSide = link.TransactionQuoteSide.ToString(),
                RevaluationRateType = link.RevaluationRateType,
                RevaluationQuoteSide = link.RevaluationQuoteSide.ToString(),
                EffectiveDate = link.EffectiveDate,
                EffectiveEndDate = link.EffectiveEndDate,
                LastRevaluationDate = link.LastRevaluationDate,
                LastRevaluationRate = currentExchangeRate,
                LastRevaluationAdjustment = link.LastRevaluationAdjustment,
                CumulativeRevaluationAdjustment = link.CumulativeRevaluationAdjustment,
                UnrealizedGainLoss = link.LastRevaluationAdjustment,
                IsActive = link.IsActive,
                HasTransactions = link.HasTransactionHistory,
                TransactionCount = link.TransactionCount,
                Notes = link.Notes,
                CreatedDate = link.CreatedAt,
                CreatedAt = link.CreatedAt,
                UpdatedAt = link.UpdatedAt
            };
        }

        private static string ResolveCurrencyCode(AddCurrencyLinkDto dto)
            => NormalizeCurrencyCode(!string.IsNullOrWhiteSpace(dto.CurrencyCode)
                ? dto.CurrencyCode
                : dto.LinkedCurrencyCode);

        private static string NormalizeCurrencyCode(string? currencyCode)
        {
            var normalized = currencyCode?.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(normalized) || normalized.Length != 3)
                throw new InvalidOperationException("Currency code must be a valid 3-character ISO code.");

            return normalized;
        }

        private static string NormalizeRateType(string? value, string fallback)
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
            if (normalized.Length > 20)
                throw new InvalidOperationException("Rate type cannot exceed 20 characters.");

            return normalized;
        }

        private static string NormalizeTransactionRateType(string? value)
        {
            var normalized = NormalizeRateType(value, "Daily");
            if (!Enum.TryParse<ExchangeRateType>(
                    normalized.Replace("-", string.Empty).Replace(" ", string.Empty),
                    ignoreCase: true,
                    out var rateType)
                || rateType is not (ExchangeRateType.Daily or ExchangeRateType.Fixed))
            {
                throw new InvalidOperationException(
                    "Transaction rate type must be Daily or Fixed. Average is reserved for reporting/valuation, and Spot requires a governed provider workflow.");
            }

            return rateType.ToString();
        }

        private static RevaluationFrequency ParseRevaluationFrequency(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return RevaluationFrequency.Monthly;

            var normalized = value.Trim().Replace("-", string.Empty).Replace(" ", string.Empty);
            return Enum.TryParse<RevaluationFrequency>(normalized, ignoreCase: true, out var frequency)
                ? frequency
                : RevaluationFrequency.Monthly;
        }

        private static ExchangeRateQuoteSide ParseQuoteSide(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return ExchangeRateQuoteSide.Mid;

            return Enum.TryParse<ExchangeRateQuoteSide>(value.Trim(), ignoreCase: true, out var quoteSide)
                ? quoteSide
                : throw new InvalidOperationException("Exchange-rate quote side must be Mid, Buying, or Selling.");
        }

        private static decimal RoundMoney(decimal amount)
            => decimal.Round(amount, 2, MidpointRounding.AwayFromZero);

        private static decimal CalculateOpeningRate(decimal? foreignBalance, decimal? baseBalance)
        {
            if (!foreignBalance.HasValue || foreignBalance.Value == 0m || !baseBalance.HasValue)
                return 0m;

            return decimal.Round(baseBalance.Value / foreignBalance.Value, 6, MidpointRounding.AwayFromZero);
        }

        private void ApplyCurrencyLinkSettings(
            AccountCurrencyLink link,
            AddCurrencyLinkDto dto,
            DateTime now,
            Guid? currentUserId)
        {
            link.RevaluationFrequency = ParseRevaluationFrequency(dto.RevaluationFrequency);
            link.TransactionRateType = NormalizeTransactionRateType(dto.TransactionRateType);
            link.TransactionQuoteSide = ParseQuoteSide(dto.TransactionQuoteSide);
            link.RevaluationRateType = NormalizeRateType(dto.RevaluationRateType, "Month-End");
            link.RevaluationQuoteSide = ParseQuoteSide(dto.RevaluationQuoteSide);
            link.Notes = dto.Notes;
            link.UpdatedAt = now;
            link.UpdatedBy = UserName;
            link.LastModifiedById = currentUserId;
            link.ModifiedByUserId = currentUserId;
            link.ModifiedDate = now;
        }

        private Guid? TryGetCurrentUserId()
            => Guid.TryParse(_currentUser.UserId, out var userId) ? userId : null;

        #endregion
    }
}
