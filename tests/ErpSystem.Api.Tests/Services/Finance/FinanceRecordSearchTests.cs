using ErpSystem.Api.Services.Finance.FixedAssets;
using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Services;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class FinanceRecordSearchTests
{
    [Fact]
    public async Task JournalsFilterBeforeLimitAndNeverExposeOtherTenantsOrDeletedEntries()
    {
        await using var db = Context();
        var tenantId = Guid.NewGuid();
        for (var index = 0; index < 30; index++)
            db.JournalEntries.Add(Journal(tenantId, $"JE-MATCH-{index:00}"));
        db.JournalEntries.AddRange(Journal(Guid.NewGuid(), "JE-MATCH-FOREIGN"),
            Journal(tenantId, "JE-MATCH-DELETED", deleted: true), Journal(tenantId, "JE-OTHER"));
        foreach (var journalTenant in db.JournalEntries.Local.Select(item => item.TenantId).Distinct().ToArray())
        {
            var book = new AccountingBook { Id = Guid.NewGuid(), TenantId = journalTenant, Code = "IFRS", Name = "IFRS" };
            db.AccountingBooks.Add(book);
            foreach (var journal in db.JournalEntries.Local.Where(item => item.TenantId == journalTenant))
                journal.AccountingBookId = book.Id;
        }
        await db.SaveChangesAsync();
        var service = Journals(db, tenantId);

        var results = await service.SearchAsync(" match ", 2);
        Assert.Equal(2, results.Count);
        Assert.All(results, item => Assert.StartsWith("JE-MATCH-", item.Number));
        Assert.DoesNotContain(results, item => item.Number.EndsWith("FOREIGN") || item.Number.EndsWith("DELETED"));
        Assert.Empty(await service.SearchAsync("foreign", 5));
        Assert.Empty(await service.SearchAsync("deleted", 5));
        Assert.Equal(25, (await service.SearchAsync("match", 10000)).Count);
        Assert.Single(await service.SearchAsync("other", 1));
        Assert.All(results, item => Assert.Equal("General", item.Title));
    }

    [Fact]
    public async Task FixedAssetsSearchCodeOrNameWithinTenantAndEnforceMaximumResultCount()
    {
        await using var db = Context();
        var tenantId = Guid.NewGuid();
        for (var index = 0; index < 30; index++)
            db.FixedAssets.Add(Asset(tenantId, $"FA-{index:00}", "Office equipment"));
        db.FixedAssets.AddRange(Asset(Guid.NewGuid(), "FA-FOREIGN", "Office equipment"),
            Asset(tenantId, "FA-DELETED", "Office equipment", deleted: true), Asset(tenantId, "FA-TARGET", "Generator"));
        await db.SaveChangesAsync();
        var service = new FixedAssetService(db, User(tenantId));

        var results = await service.SearchAsync(" OFFICE ", 10000);
        Assert.Equal(25, results.Count);
        Assert.DoesNotContain(results, item => item.Number is "FA-FOREIGN" or "FA-DELETED");
        Assert.Empty(await service.SearchAsync("fa-foreign", 5));
        Assert.Empty(await service.SearchAsync("fa-deleted", 5));
        Assert.Equal("FA-TARGET", Assert.Single(await service.SearchAsync("generator", 1)).Number);
        Assert.Equal("Generator", Assert.Single(await service.SearchAsync("fa-target", 1)).Title);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("a")]
    public async Task IncompleteTermsDoNotEnumerateRegisters(string? term)
    {
        await using var db = Context();
        var tenantId = Guid.NewGuid();
        Assert.Empty(await Journals(db, tenantId).SearchAsync(term));
        Assert.Empty(await new FixedAssetService(db, User(tenantId)).SearchAsync(term));
    }

    private static ApplicationDbContext Context() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static ICurrentUserService User(Guid tenantId)
    {
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(item => item.TenantId).Returns(tenantId);
        return user.Object;
    }

    private static JournalEntryService Journals(ApplicationDbContext db, Guid tenantId) => new(db, User(tenantId),
        Mock.Of<IGeneralLedgerService>(), Mock.Of<IAuditLogService>(), Mock.Of<INotificationService>(), Mock.Of<IAccountingBookService>());

    private static JournalEntry Journal(Guid tenantId, string number, bool deleted = false) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenantId, JournalEntryNumber = number,
        Description = "Search record", JournalType = "General", PostingStatus = "Draft",
        EntryDate = new DateTime(2026, 9, 27), BookClassification = "IFRS", IsDeleted = deleted
    };

    private static FixedAsset Asset(Guid tenantId, string code, string name, bool deleted = false) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenantId, AssetCode = code, Name = name, IsDeleted = deleted
    };
}
