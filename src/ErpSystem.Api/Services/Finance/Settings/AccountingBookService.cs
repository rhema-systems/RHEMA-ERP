using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Api.Services.Finance;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace ErpSystem.Api.Services.Finance.Settings
{
    public class AccountingBookService : IAccountingBookService
    {
        public const string IfrsCode = "IFRS";
        public const string LocalStatutoryCode = "LOCAL_STATUTORY";
        public const string ManagementCode = "MANAGEMENT";

        private readonly ApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;

        public AccountingBookService(
            ApplicationDbContext context,
            ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }

        private Guid TenantId => _currentUserService.GetRequiredFinanceTenantId();

        public async Task<IReadOnlyList<AccountingBookDto>> GetBooksAsync(
            bool includeInactive = false,
            CancellationToken cancellationToken = default)
        {
            await EnsureTenantDefaultsAsync(cancellationToken);

            var query = _context.AccountingBooks
                .AsNoTracking()
                .Where(book => book.TenantId == TenantId && !book.IsDeleted);

            if (!includeInactive)
            {
                query = query.Where(book => book.IsActive);
            }

            var books = await query
                .OrderBy(book => book.SortOrder)
                .ThenBy(book => book.Name)
                .ToListAsync(cancellationToken);

            return books.Select(MapToDto).ToList();
        }

        public async Task EnsureTenantDefaultsAsync(CancellationToken cancellationToken = default)
        {
            await EnsureDefaultBooksAsync(cancellationToken);
        }

        public async Task SyncAccountMappingsAsync(Account account, IReadOnlyCollection<AccountAccountingBookUpdateDto> requestedMappings, CancellationToken cancellationToken = default)
        {
            if (!_context.Database.IsRelational())
            {
                await SyncAccountMappingsCoreAsync(account, requestedMappings, cancellationToken);
                return;
            }
            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                try
                {
                    await SyncAccountMappingsCoreAsync(account, requestedMappings, cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                }
                catch
                {
                    await transaction.RollbackAsync(cancellationToken);
                    throw;
                }
            });
        }

        private async Task SyncAccountMappingsCoreAsync(Account account, IReadOnlyCollection<AccountAccountingBookUpdateDto> requestedMappings, CancellationToken cancellationToken)
        {
            var tenantId = account.TenantId;
            if (requestedMappings == null || requestedMappings.Count == 0 || !requestedMappings.Any(item => item.IsEnabled))
                throw new InvalidOperationException("At least one enabled accounting-book assignment is required.");
            var books = await _context.AccountingBooks
                .Where(book => book.TenantId == tenantId && !book.IsDeleted)
                .ToListAsync(cancellationToken);
            var mappings = await _context.AccountAccountingBooks
                .Where(mapping => mapping.TenantId == tenantId && mapping.AccountId == account.Id && !mapping.IsDeleted)
                .ToListAsync(cancellationToken);
            var classifications = await _context.AccountClassifications
                .Where(item => item.TenantId == tenantId && !item.IsDeleted)
                .ToListAsync(cancellationToken);
            var parentIds = classifications.Where(item => item.ParentClassificationId.HasValue)
                .Select(item => item.ParentClassificationId!.Value).ToHashSet();
            var resolved = new List<(AccountAccountingBookUpdateDto Request, AccountingBook Book, AccountClassification? Classification)>();
            foreach (var request in requestedMappings)
            {
                var book = request.AccountingBookId.HasValue
                    ? books.SingleOrDefault(item => item.Id == request.AccountingBookId.Value)
                    : books.SingleOrDefault(item => string.Equals(item.Code, request.AccountingBookCode?.Trim(), StringComparison.OrdinalIgnoreCase));
                if (book == null || !book.IsActive || (request.IsEnabled && !book.AllowsPosting))
                    throw new InvalidOperationException("An accounting-book assignment is invalid or inactive for this tenant.");
                var classification = request.AccountClassificationId.HasValue
                    ? classifications.SingleOrDefault(item => item.Id == request.AccountClassificationId.Value)
                    : null;
                if (request.IsEnabled && (classification == null || classification.AccountingBookId != book.Id
                    || classification.Status != AccountClassificationStatus.Active
                    || !classification.IsPostingClassification || parentIds.Contains(classification.Id)
                    || classification.CoreAccountType != account.AccountType))
                    throw new InvalidOperationException("Each enabled accounting-book assignment requires a compatible active posting classification.");
                resolved.Add((request, book, classification));
            }
            if (resolved.Select(item => item.Book.Id).Distinct().Count() != resolved.Count)
                throw new InvalidOperationException("Accounting-book assignments must be unique.");
            var requestedBookIds = resolved.Select(item => item.Book.Id).ToHashSet();
            if (mappings.Any(item => !requestedBookIds.Contains(item.AccountingBookId)))
                throw new InvalidOperationException(
                    "Every existing account-book assignment must be included with its row version.");
            var now = DateTime.UtcNow;
            foreach (var item in resolved)
            {
                var mapping = mappings.SingleOrDefault(candidate => candidate.AccountingBookId == item.Book.Id);
                if (mapping == null)
                {
                    mapping = new AccountAccountingBook
                    {
                        TenantId = tenantId, AccountId = account.Id, AccountingBookId = item.Book.Id,
                        CreatedAt = now, CreatedBy = _currentUserService.UserName ?? "system"
                    };
                    _context.AccountAccountingBooks.Add(mapping);
                }
                else
                {
                    ApplyRowVersion(mapping, item.Request.RowVersion);
                }
                mapping.IsEnabled = item.Request.IsEnabled;
                mapping.AccountClassificationId = item.Classification?.Id;
                mapping.FinancialStatementLineItem = item.Request.FinancialStatementLineItem;
                mapping.UpdatedAt = now;
                mapping.UpdatedBy = _currentUserService.UserName ?? "system";
            }
            await _context.SaveChangesAsync(cancellationToken);
        }

        private void ApplyRowVersion(AccountAccountingBook mapping, string? encodedRowVersion)
        {
            if (string.IsNullOrWhiteSpace(encodedRowVersion))
                throw new InvalidOperationException("Row version is required for an existing account-book assignment.");

            byte[] originalRowVersion;
            try
            {
                originalRowVersion = Convert.FromBase64String(encodedRowVersion);
            }
            catch (FormatException)
            {
                throw new InvalidOperationException("Account-book assignment row version is invalid.");
            }

            if (originalRowVersion.Length == 0)
                throw new InvalidOperationException("Account-book assignment row version is invalid.");
            if (mapping.RowVersion.Length > 0 && !mapping.RowVersion.SequenceEqual(originalRowVersion))
                throw new DbUpdateConcurrencyException(
                    "The account-book assignment changed after it was loaded. Reload the account and retry.");

            _context.Entry(mapping).Property(item => item.RowVersion).OriginalValue = originalRowVersion;
        }

        private async Task EnsureDefaultBooksAsync(CancellationToken cancellationToken)
        {
            var tenantId = TenantId;
            var now = DateTime.UtcNow;
            var userName = _currentUserService.UserName ?? "system";

            var existingBooks = await _context.AccountingBooks
                .Where(book => book.TenantId == tenantId && !book.IsDeleted)
                .ToListAsync(cancellationToken);

            var existingCodes = existingBooks
                .Select(book => book.Code)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var defaults = GetDefaultBooks(tenantId, now, userName)
                .Where(book => !existingCodes.Contains(book.Code))
                .ToList();

            if (defaults.Count > 0)
            {
                _context.AccountingBooks.AddRange(defaults);
                await _context.SaveChangesAsync(cancellationToken);
            }
        }


        private static IReadOnlyList<AccountingBook> GetDefaultBooks(Guid tenantId, DateTime now, string userName)
        {
            return new List<AccountingBook>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Code = IfrsCode,
                    Name = "IFRS",
                    Description = "Primary corporate reporting book for IFRS financial statements.",
                    Purpose = "Primary",
                    IsActive = true,
                    IsDefault = true,
                    AllowsPosting = true,
                    IsSystemDefined = true,
                    SortOrder = 10,
                    CreatedAt = now,
                    CreatedBy = userName
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Code = LocalStatutoryCode,
                    Name = "Local Statutory",
                    Description = "Local statutory or tax compliance reporting book.",
                    Purpose = "Statutory",
                    IsActive = true,
                    IsDefault = false,
                    AllowsPosting = true,
                    IsSystemDefined = true,
                    SortOrder = 20,
                    CreatedAt = now,
                    CreatedBy = userName
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Code = ManagementCode,
                    Name = "Management",
                    Description = "Internal management reporting book.",
                    Purpose = "Management",
                    IsActive = true,
                    IsDefault = false,
                    AllowsPosting = true,
                    IsSystemDefined = true,
                    SortOrder = 30,
                    CreatedAt = now,
                    CreatedBy = userName
                }
            };
        }

        private static AccountingBookDto MapToDto(AccountingBook book)
        {
            return new AccountingBookDto
            {
                Id = book.Id,
                TenantId = book.TenantId,
                Code = book.Code,
                Name = book.Name,
                Description = book.Description,
                Purpose = book.Purpose,
                IsActive = book.IsActive,
                IsDefault = book.IsDefault,
                AllowsPosting = book.AllowsPosting,
                IsSystemDefined = book.IsSystemDefined,
                SortOrder = book.SortOrder
            };
        }
    }
}
