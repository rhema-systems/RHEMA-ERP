using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Finance.Reporting;
using ErpSystem.Data;
using ErpSystem.Data.Migrations;
using ErpSystem.Data.Seeders;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class FinancialStatementClassificationLayoutPhase3Tests
{
    [Fact]
    [Trait("Category", "Reporting")]
    public void PublicationFingerprints_ShouldBeOrderIndependentAndDetectTampering()
    {
        var tenantId = Guid.NewGuid();
        var bookId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var parent = Classification(tenantId, bookId, "ASSET", "Assets");
        var child = Classification(tenantId, bookId, "CASH", "Cash", parent.Id);
        var firstHierarchy = FinancialStatementPublicationFingerprint.Hierarchy(tenantId, bookId, new[] { child, parent });
        var secondHierarchy = FinancialStatementPublicationFingerprint.Hierarchy(tenantId, bookId, new[] { parent, child });
        firstHierarchy.Should().Be(secondHierarchy).And.HaveLength(64);

        var first = Snapshot(tenantId, versionId, bookId, "1000", "Cash");
        var second = Snapshot(tenantId, versionId, bookId, "1100", "Receivable");
        FinancialStatementPublicationFingerprint.SnapshotSchemaVersion.Should().Be("2");
        var fingerprint = FinancialStatementPublicationFingerprint.Resolution(
            tenantId, versionId, bookId, "IFRS", "IFRS Primary", firstHierarchy, new[] { second, first });
        FinancialStatementPublicationFingerprint.Resolution(
                tenantId, versionId, bookId, "IFRS", "IFRS Primary", firstHierarchy, new[] { first, second })
            .Should().Be(fingerprint);

        FinancialStatementPublicationFingerprint.Resolution(
                tenantId, versionId, Guid.NewGuid(), "IFRS", "IFRS Primary", firstHierarchy, new[] { first, second })
            .Should().NotBe(fingerprint, "the frozen header book id is evidence");
        FinancialStatementPublicationFingerprint.Resolution(
                tenantId, versionId, bookId, "LOCAL", "IFRS Primary", firstHierarchy, new[] { first, second })
            .Should().NotBe(fingerprint, "the frozen header book code is evidence");
        FinancialStatementPublicationFingerprint.Resolution(
                tenantId, versionId, bookId, "IFRS", "Renamed book", firstHierarchy, new[] { first, second })
            .Should().NotBe(fingerprint, "the frozen header book name is evidence");

        var originalRowBookId = second.AccountingBookId;
        second.AccountingBookId = Guid.NewGuid();
        FinancialStatementPublicationFingerprint.Resolution(
                tenantId, versionId, bookId, "IFRS", "IFRS Primary", firstHierarchy, new[] { first, second })
            .Should().NotBe(fingerprint, "each frozen row book id is evidence");
        second.AccountingBookId = originalRowBookId;
        second.AccountingBookCode = "LOCAL";
        FinancialStatementPublicationFingerprint.Resolution(
                tenantId, versionId, bookId, "IFRS", "IFRS Primary", firstHierarchy, new[] { first, second })
            .Should().NotBe(fingerprint, "each frozen row book code is evidence");
        second.AccountingBookCode = "IFRS";

        second.AccountName = "Tampered receivable";
        FinancialStatementPublicationFingerprint.Resolution(
                tenantId, versionId, bookId, "IFRS", "IFRS Primary", firstHierarchy, new[] { first, second })
            .Should().NotBe(fingerprint);
        child.Name = "Renamed cash";
        FinancialStatementPublicationFingerprint.Hierarchy(tenantId, bookId, new[] { parent, child })
            .Should().NotBe(firstHierarchy);
    }

    [Fact]
    [Trait("Category", "Migration")]
    public void Phase3Migration_ShouldBeNarrowAndAddImmutableSnapshotStorage()
    {
        var source = ArchivedMigrationSource.Read("20260903130000_AddFinancialStatementClassificationSnapshots.cs");
        source.Should().Contain("FinancialStatementPublicationAccounts")
            .And.Contain("FinancialStatementRowMappings").And.Contain("AccountClassificationId")
            .And.Contain("FinancialStatementLayoutVersions").And.Contain("ResolutionFingerprint");
    }

    [Fact]
    [Trait("Category", "Seeding")]
    public async Task ProtectedStandards_ShouldSeedPerBookAndRemainIdempotent()
    {
        var tenantId = Guid.NewGuid();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"phase3-standards-{Guid.NewGuid():N}").Options);
        db.Tenants.Add(new Tenant { Id = tenantId, Code = "TEN", Name = "Tenant", Status = TenantStatus.Active, BaseCurrency = "GHS" });
        var book = new AccountingBook { Id = Guid.NewGuid(), TenantId = tenantId, Code = "IFRS", Name = "IFRS Primary", Purpose = "Primary", IsActive = true, AllowsPosting = true };
        db.AccountingBooks.Add(book);
        db.AccountClassifications.AddRange(
            Classification(tenantId, book.Id, "ASSETS", "Assets"),
            Classification(tenantId, book.Id, "LIABILITIES", "Liabilities"),
            Classification(tenantId, book.Id, "EQUITY_ROOT", "Equity"),
            Classification(tenantId, book.Id, "REVENUE_ROOT", "Revenue"),
            Classification(tenantId, book.Id, "EXPENSE_ROOT", "Expenses"));
        await db.SaveChangesAsync();
        var seeder = new FinanceFinancialStatementStandardSeeder(db, NullLogger.Instance);

        await seeder.SeedAsync(tenantId, DateTime.UtcNow);
        await seeder.SeedAsync(tenantId, DateTime.UtcNow.AddMinutes(1));

        var layouts = await db.FinancialStatementLayouts.Include(item => item.Versions).ThenInclude(item => item.Rows)
            .ThenInclude(item => item.Mappings).ToListAsync();
        layouts.Should().HaveCount(2);
        layouts.Should().OnlyContain(item => item.IsProtectedStandard && !item.IsDefault && item.Versions.Single().Status == FinancialStatementLayoutVersionStatus.Draft);
        layouts.SelectMany(item => item.Versions).SelectMany(item => item.Rows).SelectMany(item => item.Mappings)
            .Should().OnlyContain(item => item.MappingType == FinancialStatementRowMappingType.Classification && item.IncludeClassificationDescendants);
        layouts.SelectMany(item => item.Versions).SelectMany(item => item.Rows)
            .Should().OnlyContain(item => item.SignMultiplier == 1);
    }

    private static AccountClassification Classification(Guid tenantId, Guid bookId, string code, string name, Guid? parentId = null)
        => new()
        {
            Id = Guid.NewGuid(), TenantId = tenantId, AccountingBookId = bookId, ParentClassificationId = parentId,
            Code = code, Name = name,
            CoreAccountType = code.StartsWith("REVENUE", StringComparison.Ordinal) ? AccountType.Revenue
                : code.StartsWith("EXPENSE", StringComparison.Ordinal) ? AccountType.Expense
                : code.StartsWith("LIABILITY", StringComparison.Ordinal) ? AccountType.Liability
                : code.StartsWith("EQUITY", StringComparison.Ordinal) ? AccountType.Equity
                : AccountType.Asset,
            Status = AccountClassificationStatus.Active, IsPostingClassification = parentId.HasValue
        };

    private static FinancialStatementPublicationAccount Snapshot(Guid tenantId, Guid versionId, Guid bookId, string number, string name)
        => new()
        {
            Id = Guid.NewGuid(), TenantId = tenantId, FinancialStatementLayoutVersionId = versionId,
            FinancialStatementRowId = Guid.NewGuid(), FinancialStatementRowMappingId = Guid.NewGuid(),
            MappingType = FinancialStatementRowMappingType.Classification, AccountId = Guid.NewGuid(), RowCode = number,
            AccountNumber = number, AccountName = name, AccountType = AccountType.Asset,
            AccountingBookId = bookId, AccountingBookCode = "IFRS", ClassificationCode = "ASSET",
            ClassificationName = "Assets", ClassificationPath = "ASSET", MappingSelector = "ASSET+DESC"
        };

}
