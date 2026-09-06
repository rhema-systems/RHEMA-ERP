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
        var operations = new ExposedMigration().BuildUpOperations();
        operations[0].Should().BeOfType<SqlOperation>();
        var preflight = ((SqlOperation)operations[0]).Sql;

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
        var migration = new ExposedMigration();
        var up = migration.BuildUpOperations();
        var added = up.OfType<AddColumnOperation>().Where(item => item.Table == "AccountingBooks")
            .Select(item => item.Name).ToHashSet(StringComparer.Ordinal);
        added.Should().Contain(new[]
        {
            "BookType", "LifecycleStatus", "FunctionalCurrencyCode", "EffectiveFromUtc", "EffectiveToUtc",
            "BaseAccountingBookId", "InitializationStartedAtUtc", "PendingLifecycleStatus",
            "PendingTransitionReason", "TransitionRequestedByUserId", "TransitionRequestedAtUtc",
            "TransitionWorkflowInstanceId", "TransitionDecidedByUserId", "TransitionDecidedAtUtc",
            "TransitionDecisionReason", "RowVersion"
        });

        up.OfType<CreateIndexOperation>().Should().ContainSingle(item =>
            item.Name == "IX_AccountingBooks_TenantId_IsDefault" && item.IsUnique
            && item.Filter == "[IsDeleted] = 0 AND [IsDefault] = 1");
        up.OfType<AddForeignKeyOperation>().Should().ContainSingle(item =>
            item.Name == "FK_AccountingBooks_AccountingBooks_TenantId_BaseAccountingBookId"
            && item.Columns.SequenceEqual(new[] { "TenantId", "BaseAccountingBookId" })
            && item.PrincipalColumns.SequenceEqual(new[] { "TenantId", "Id" })
            && item.OnDelete == ReferentialAction.Restrict);
        up.OfType<AddCheckConstraintOperation>().Select(item => item.Name).Should().Contain(new[]
        {
            "CK_AccountingBooks_BookType", "CK_AccountingBooks_LifecycleStatus",
            "CK_AccountingBooks_BaseShape", "CK_AccountingBooks_DefaultType",
            "CK_AccountingBooks_NoSelfBase", "CK_AccountingBooks_PostingLifecycle",
            "CK_AccountingBooks_CodeCanonical", "CK_AccountingBooks_FunctionalCurrencyCanonical",
            "CK_Tenants_BaseCurrencyCanonical_C3", "CK_FinanceSettings_BaseCurrencyCanonical_C3"
        });

        var down = migration.BuildDownOperations();
        ((SqlOperation)down[0]).Sql.Should().Contain("C3_DOWN_GUARD")
            .And.Contain("Delta/base-book").And.Contain("pending lifecycle governance evidence");
        down.OfType<DropColumnOperation>().Where(item => item.Table == "AccountingBooks")
            .Select(item => item.Name).Should().Contain(added);
    }

    [Fact]
    public void Migration_IsDiscoverableWithoutOpeningDatabaseConnection()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=C3MigrationDiscovery;Trusted_Connection=True")
            .Options;
        using var context = new ApplicationDbContext(options);

        context.GetService<IMigrationsAssembly>().Migrations.Should().ContainKey(MigrationId);
    }

    private sealed class ExposedMigration : AddGovernedAccountingBookLifecycle
    {
        public IReadOnlyList<MigrationOperation> BuildUpOperations()
        {
            var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
            Up(builder);
            return builder.Operations;
        }

        public IReadOnlyList<MigrationOperation> BuildDownOperations()
        {
            var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
            Down(builder);
            return builder.Operations;
        }
    }
}
