using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.GL;

internal sealed record PrimaryBookCompatibilityAuthority(Guid AccountingBookId, string AccountingBookCode,
    string FunctionalCurrencyCode);

internal static class PrimaryBookCompatibilityAuthorityResolver
{
    internal static async Task<PrimaryBookCompatibilityAuthority> ResolveAsync(
        IUnitOfWork unitOfWork, Guid tenantId, CancellationToken cancellationToken)
    {
        // C2 consumers without an explicit book remain a narrow primary-book compatibility boundary.
        // Never choose First/default-by-caption: ambiguity must fail until those contracts become exact-book.
        var books = await unitOfWork.Repository<AccountingBook>()
            .GetQueryable(item => item.TenantId == tenantId && item.IsDefault && item.IsActive
                && item.AllowsPosting && !item.IsDeleted)
            .Take(2)
            .Select(item => new { item.Id, item.Code })
            .ToListAsync(cancellationToken);
        if (books.Count != 1 || string.IsNullOrWhiteSpace(books[0].Code)
            || !string.Equals(books[0].Code, books[0].Code.Trim().ToUpperInvariant(), StringComparison.Ordinal))
            throw new InvalidOperationException(
                "PRIMARY_BOOK_AUTHORITY_AMBIGUOUS: Exactly one active default posting book is required.");

        var settings = await unitOfWork.Repository<FinanceSettings>()
            .GetQueryable(item => item.TenantId == tenantId && !item.IsDeleted)
            .Take(2)
            .Select(item => item.BaseCurrency)
            .ToListAsync(cancellationToken);
        if (settings.Count > 1)
            throw new InvalidOperationException("Tenant functional-currency authority is ambiguous.");

        var currency = settings.Count == 1 ? settings[0] : await unitOfWork.Repository<Tenant>()
            .GetQueryable(item => item.Id == tenantId && !item.IsDeleted)
            .Select(item => item.BaseCurrency)
            .SingleOrDefaultAsync(cancellationToken);
        if (currency?.Length != 3
            || !string.Equals(currency, currency.Trim().ToUpperInvariant(), StringComparison.Ordinal))
            throw new InvalidOperationException("Tenant functional-currency authority is unavailable or invalid.");

        return new PrimaryBookCompatibilityAuthority(books[0].Id, books[0].Code, currency);
    }
}
