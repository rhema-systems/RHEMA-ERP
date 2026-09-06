using ErpSystem.Data;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class AccountingBookPeriodInitializationC4MigrationTests
{
    private const string MigrationId = "20260906140533_AddAccountingBookPeriodInitializationFoundation";

    [Fact]
    public void Migration_PutsCompleteReadinessPreflightBeforeEveryMutation()
    {
        var operations = new ExposedMigration().BuildUpOperations();

        operations[0].Should().BeOfType<SqlOperation>();
        var sql = ((SqlOperation)operations[0]).Sql;
        sql.Should().Contain("C4_SCHEMA_PREFLIGHT")
            .And.Contain("C4_BOOK_PREFLIGHT")
            .And.Contain("C4_PRIMARY_PREFLIGHT")
            .And.Contain("C4_READINESS_PREFLIGHT")
            .And.Contain("C4_PERIOD_PREFLIGHT")
            .And.Contain("C4_LINEAGE_PREFLIGHT")
            .And.Contain("Latin1_General_100_BIN2")
            .And.Contain("DATALENGTH")
            .And.Contain("ALL_ACTIVE_BOOKS")
            .And.Contain("[PendingLifecycleStatus] = 4")
            .And.Contain("[FiscalYears]");

        operations.Skip(1).Should().OnlyContain(operation => operation.GetType() != typeof(SqlOperation),
            "the complete predecessor audit must precede every C4 schema mutation");
    }

    [Fact]
    public void Migration_CreatesExactTenantAuthority_ImmutableGrains_AndBoundedDown()
    {
        var migration = new ExposedMigration();
        var up = migration.BuildUpOperations();
        var tables = up.OfType<CreateTableOperation>().ToDictionary(item => item.Name);

        tables.Keys.Should().BeEquivalentTo(new[]
        {
            "AccountingBookPeriods", "AccountingBookInitializations", "AccountingBookInitializationLines"
        });

        tables["AccountingBookPeriods"].ForeignKeys.Should().Contain(item =>
            item.Name == "FK_AccountingBookPeriods_AccountingBooks_TenantId_AccountingBookId"
            && item.Columns.SequenceEqual(new[] { "TenantId", "AccountingBookId" }));
        tables["AccountingBookPeriods"].ForeignKeys.Should().Contain(item =>
            item.Name == "FK_AccountingBookPeriods_FiscalPeriods_TenantId_FiscalPeriodId"
            && item.Columns.SequenceEqual(new[] { "TenantId", "FiscalPeriodId" }));
        tables["AccountingBookInitializationLines"].ForeignKeys.Should().Contain(item =>
            item.Name == "FK_AccountingBookInitializationLines_Accounts_TenantId_AccountId"
            && item.Columns.SequenceEqual(new[] { "TenantId", "AccountId" }));
        tables["AccountingBookInitializationLines"].ForeignKeys.Should().Contain(item =>
            item.Name == "FK_AccountingBookInitializationLines_AccountingBookInitializations_TenantId_AccountingBookInitializationId"
            && item.Columns.SequenceEqual(new[] { "TenantId", "AccountingBookInitializationId" }));

        up.OfType<CreateIndexOperation>().Should().Contain(item => item.IsUnique
            && item.Name == "IX_AccountingBookPeriods_TenantId_AccountingBookId_FiscalPeriodId"
            && item.Filter == null);
        up.OfType<CreateIndexOperation>().Should().Contain(item => item.IsUnique
            && item.Name == "IX_AccountingBookInitializations_TenantId_AccountingBookId_Version");
        up.OfType<CreateIndexOperation>().Should().Contain(item => item.IsUnique
            && item.Name == "IX_AccountingBookInitializations_TenantId_AccountingBookId_InitializationStatus"
            && item.Filter!.Contains("[InitializationStatus] = 3", StringComparison.Ordinal));

        tables.SelectMany(pair => pair.Value.CheckConstraints).Select(item => item.Name).Should().Contain(new[]
        {
            "CK_AccountingBookPeriods_NoDelete",
            "CK_AccountingBookInitializations_NoDelete",
            "CK_AccountingBookInitializations_SourceShape",
            "CK_AccountingBookInitializations_ApprovalShape",
            "CK_AccountingBookInitializations_EvidenceFingerprint",
            "CK_AccountingBookInitializations_ReconciliationFingerprint",
            "CK_AccountingBookInitializationLines_NoDelete",
            "CK_AccountingBookInitializationLines_Currency"
        });

        var down = migration.BuildDownOperations();
        down[0].Should().BeOfType<SqlOperation>();
        ((SqlOperation)down[0]).Sql.Should().Contain("C4_DOWN_GUARD")
            .And.Contain("cannot be represented by the predecessor schema");
        down.OfType<DropTableOperation>().Select(item => item.Name).Should().BeEquivalentTo(tables.Keys);
    }

    [Fact]
    public void Migration_IsDiscoverableWithoutOpeningDatabaseConnection()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=C4MigrationDiscovery;Trusted_Connection=True")
            .Options;
        using var context = new ApplicationDbContext(options);

        context.GetService<IMigrationsAssembly>().Migrations.Should().ContainKey(MigrationId);
    }

    private sealed class ExposedMigration : AddAccountingBookPeriodInitializationFoundation
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
