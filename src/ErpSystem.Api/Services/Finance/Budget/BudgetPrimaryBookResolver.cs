using ErpSystem.Core.Enums;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.Finance.Budget;

internal sealed record BudgetPrimaryBook(Guid Id, string Code);

internal static class BudgetPrimaryBookResolver
{
    internal static async Task<BudgetPrimaryBook> ResolveAsync(
        ApplicationDbContext db,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var books = await db.AccountingBooks.AsNoTracking()
            .Where(book => book.TenantId == tenantId
                && !book.IsDeleted
                && book.IsDefault
                && book.BookType == AccountingBookType.PrimaryFull)
            .OrderBy(book => book.Id)
            .Select(book => new BudgetPrimaryBook(book.Id, book.Code))
            .Take(2)
            .ToListAsync(cancellationToken);

        return books.Count switch
        {
            1 => books[0],
            0 => throw new InvalidOperationException(
                "Finance budgeting requires one default Primary Full accounting book for this tenant."),
            _ => throw new InvalidOperationException(
                "Finance budgeting found more than one default Primary Full accounting book for this tenant.")
        };
    }
}
