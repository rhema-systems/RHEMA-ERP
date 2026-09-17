using ErpSystem.Data;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class AccountingBookLifecycleC3MigrationTests
{
    private const string MigrationId = "20260905213000_AddGovernedAccountingBookLifecycle";

    [Fact]
    public void Migration_PutsCompleteFailClosedPreflightBeforeEveryMutation()
    {
        var preflight = ArchivedMigrationSource.Read("20260905213000_AddGovernedAccountingBookLifecycle.cs");

        preflight.Should().Contain("C3_BOOK_PREFLIGHT")
            .And.Contain("C3_PRIMARY_PREFLIGHT")
            .And.Contain("C3_LIFECYCLE_PREFLIGHT")
            .And.Contain("C3_CURRENCY_PREFLIGHT")
            .And.Contain("C3_USE_PREFLIGHT")
            .And.Contain("Latin1_General_100_BIN2")
            .And.Contain("DATALENGTH")
            .And.Contain("UPPER(LTRIM(RTRIM")
            .And.Contain("NOT LIKE N'[A-Z]'")
            .And.Contain("LIKE N'%[^A-Z0-9_]%'")
            .And.Contain("NOT LIKE N'[A-Z][A-Z][A-Z]'")
            .And.Contain("ALL_ACTIVE_BOOKS")
            .And.Contain("ALL_CLASSIFIED_BOOKS")
            .And.Contain("ALLCLASSIFIEDBOOKS")
            .And.Contain("AccountingBookId")
            .And.Contain("BookClassification")
            .And.Contain("FinancePostingEvents");
        preflight.Should().Contain("[IsActive] <> [AllowsPosting]");
    }

    [Fact]
    public void Migration_AddsGovernedShape_TenantBaseConstraint_AndBoundedDown()
    {
        var source = ArchivedMigrationSource.Read("20260905213000_AddGovernedAccountingBookLifecycle.cs");
        var columns = new[]
        {
            "BookType", "LifecycleStatus", "FunctionalCurrencyCode", "EffectiveFromUtc", "EffectiveToUtc",
            "BaseAccountingBookId", "InitializationStartedAtUtc", "PendingLifecycleStatus",
            "PendingTransitionReason", "TransitionRequestedByUserId", "TransitionRequestedAtUtc",
            "TransitionWorkflowInstanceId", "TransitionDecidedByUserId", "TransitionDecidedAtUtc",
            "TransitionDecisionReason", "RowVersion"
        };
        foreach (var column in columns)
            source.Should().Contain($"name: \"{column}\"");
        source.Should().Contain("IX_AccountingBooks_TenantId_IsDefault")
            .And.Contain("[IsDeleted] = 0 AND [IsDefault] = 1")
            .And.Contain("FK_AccountingBooks_AccountingBooks_TenantId_BaseAccountingBookId")
            .And.Contain("columns: new[] { \"TenantId\", \"BaseAccountingBookId\" }")
            .And.Contain("principalColumns: new[] { \"TenantId\", \"Id\" }")
            .And.Contain("onDelete: ReferentialAction.Restrict");
        foreach (var constraint in new[]
        {
            "CK_AccountingBooks_BookType", "CK_AccountingBooks_LifecycleStatus",
            "CK_AccountingBooks_BaseShape", "CK_AccountingBooks_DefaultType",
            "CK_AccountingBooks_NoSelfBase", "CK_AccountingBooks_PostingLifecycle",
            "CK_AccountingBooks_CodeCanonical", "CK_AccountingBooks_FunctionalCurrencyCanonical",
            "CK_Tenants_BaseCurrencyCanonical_C3", "CK_FinanceSettings_BaseCurrencyCanonical_C3"
        }) source.Should().Contain(constraint);
        source.Should().Contain("C3_DOWN_GUARD")
            .And.Contain("Delta/base-book").And.Contain("pending lifecycle governance evidence");
        source.Should().Contain("protected override void Down(MigrationBuilder migrationBuilder)")
            .And.Contain("migrationBuilder.DropColumn(");
    }

    [Fact]
    public void Migration_IsDiscoverableWithoutOpeningDatabaseConnection()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=C3MigrationDiscovery;Trusted_Connection=True")
            .Options;
        using var context = new ApplicationDbContext(options);

        context.GetService<IMigrationsAssembly>().Migrations.Keys.Should()
            .Equal("20260916132000_DisposableDevelopmentCurrentModelBaseline");
        ArchivedMigrationSource.Read("20260905213000_AddGovernedAccountingBookLifecycle.cs")
            .Should().Contain($"[Migration(\"{MigrationId}\")]");
    }
}
