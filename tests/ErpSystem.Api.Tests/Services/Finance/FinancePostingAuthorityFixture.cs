using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Data;

namespace ErpSystem.Api.Tests.Services.Finance;

internal static class FinancePostingAuthorityFixture
{
    public static void SeedEnabledBookMappings(
        ApplicationDbContext db,
        Guid tenantId,
        AccountingBook book,
        params Account[] accounts)
    {
        foreach (var account in accounts)
        {
            if (account.TenantId != tenantId || book.TenantId != tenantId)
                throw new InvalidOperationException("Test posting authority must remain within one tenant.");

            if (db.AccountAccountingBooks.Local.Any(item =>
                    item.TenantId == tenantId
                    && item.AccountId == account.Id
                    && item.AccountingBookId == book.Id
                    && !item.IsDeleted))
                continue;

            db.AccountAccountingBooks.Add(new AccountAccountingBook
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                AccountId = account.Id,
                AccountingBookId = book.Id,
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "test-fixture"
            });
        }
    }

    public static void SeedExactBookPeriod(
        ApplicationDbContext db,
        Guid tenantId,
        FiscalPeriod period,
        string accountingBookCode = "IFRS")
    {
        var book = db.AccountingBooks.Local.SingleOrDefault(item =>
            item.TenantId == tenantId && item.Code == accountingBookCode && !item.IsDeleted);
        if (book == null)
            throw new InvalidOperationException(
                $"The test fixture must seed exact accounting book {accountingBookCode} before its period authority.");

        if (db.AccountingBookPeriods.Local.Any(item => item.TenantId == tenantId
                && item.AccountingBookId == book.Id && item.FiscalPeriodId == period.Id))
            return;

        db.AccountingBookPeriods.Add(new AccountingBookPeriod
        {
            Id = Guid.NewGuid(), TenantId = tenantId, AccountingBookId = book.Id,
            FiscalPeriodId = period.Id,
            PeriodStatus = period.IsLocked ? AccountingBookPeriodStatus.Locked
                : period.IsClosed ? AccountingBookPeriodStatus.Closed
                : period.IsOpen ? AccountingBookPeriodStatus.Open
                : AccountingBookPeriodStatus.Future
        });
    }
}
