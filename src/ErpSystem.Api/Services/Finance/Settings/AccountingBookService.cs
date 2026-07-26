using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Api.Services.Finance;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

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
            await BackfillAccountMappingsAsync(cancellationToken);
        }

        public async Task SyncAccountMappingsAsync(Account account, CancellationToken cancellationToken = default)
        {
            await EnsureDefaultBooksAsync(cancellationToken);

            var tenantId = account.TenantId;
            var books = await _context.AccountingBooks
                .Where(book => book.TenantId == tenantId && !book.IsDeleted)
                .ToListAsync(cancellationToken);

            var mappings = await _context.AccountAccountingBooks
                .Where(mapping => mapping.TenantId == tenantId && mapping.AccountId == account.Id && !mapping.IsDeleted)
                .ToListAsync(cancellationToken);

            ApplyMappingsFromLegacyFlags(account, books, mappings);
            await _context.SaveChangesAsync(cancellationToken);
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

        private async Task BackfillAccountMappingsAsync(CancellationToken cancellationToken)
        {
            var tenantId = TenantId;
            var books = await _context.AccountingBooks
                .Where(book => book.TenantId == tenantId && !book.IsDeleted)
                .ToListAsync(cancellationToken);

            var accounts = await _context.Accounts
                .Where(account => account.TenantId == tenantId && !account.IsDeleted)
                .ToListAsync(cancellationToken);

            if (accounts.Count == 0 || books.Count == 0)
            {
                return;
            }

            var accountIds = accounts.Select(account => account.Id).ToList();
            var mappings = await _context.AccountAccountingBooks
                .Where(mapping => mapping.TenantId == tenantId && accountIds.Contains(mapping.AccountId) && !mapping.IsDeleted)
                .ToListAsync(cancellationToken);

            foreach (var account in accounts)
            {
                var accountMappings = mappings
                    .Where(mapping => mapping.AccountId == account.Id)
                    .ToList();

                ApplyMappingsFromLegacyFlags(account, books, accountMappings);
            }

            await _context.SaveChangesAsync(cancellationToken);
        }

        private void ApplyMappingsFromLegacyFlags(
            Account account,
            IReadOnlyCollection<AccountingBook> books,
            ICollection<AccountAccountingBook> mappings)
        {
            UpsertMapping(account, books, mappings, IfrsCode, account.IsIFRSClassified, account.IFRSLineItem);
            UpsertMapping(account, books, mappings, LocalStatutoryCode, account.IsBaseClassified, account.BaseLineItem);
            UpsertMapping(account, books, mappings, ManagementCode, account.IsLocalClassified, account.LocalLineItem);
        }

        private void UpsertMapping(
            Account account,
            IReadOnlyCollection<AccountingBook> books,
            ICollection<AccountAccountingBook> mappings,
            string bookCode,
            bool enabled,
            string? lineItem)
        {
            var book = books.FirstOrDefault(candidate =>
                candidate.Code.Equals(bookCode, StringComparison.OrdinalIgnoreCase));

            if (book == null)
            {
                return;
            }

            var mapping = mappings.FirstOrDefault(candidate => candidate.AccountingBookId == book.Id);
            if (mapping == null)
            {
                mapping = new AccountAccountingBook
                {
                    Id = Guid.NewGuid(),
                    TenantId = account.TenantId,
                    AccountId = account.Id,
                    AccountingBookId = book.Id,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = _currentUserService.UserName ?? "system"
                };

                _context.AccountAccountingBooks.Add(mapping);
                mappings.Add(mapping);
            }

            mapping.IsEnabled = enabled;
            mapping.FinancialStatementLineItem = lineItem;
            mapping.UpdatedAt = DateTime.UtcNow;
            mapping.UpdatedBy = _currentUserService.UserName ?? "system";
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
