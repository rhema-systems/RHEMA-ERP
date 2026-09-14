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
        var sql = ArchivedMigrationSource.Read("20260906140533_AddAccountingBookPeriodInitializationFoundation.cs");
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

        sql.IndexOf("C4_SCHEMA_PREFLIGHT", StringComparison.Ordinal).Should().BeLessThan(
            sql.IndexOf("migrationBuilder.CreateTable", StringComparison.Ordinal));
    }

    [Fact]
    public void Migration_CreatesExactTenantAuthority_ImmutableGrains_AndBoundedDown()
    {
        var source = ArchivedMigrationSource.Read("20260906140533_AddAccountingBookPeriodInitializationFoundation.cs");
        foreach (var table in new[]
        {
            "AccountingBookPeriods", "AccountingBookInitializations", "AccountingBookInitializationLines"
        }) source.Should().Contain($"name: \"{table}\"");
        foreach (var token in new[]
        {
            "FK_AccountingBookPeriods_AccountingBooks_TenantId_AccountingBookId",
            "FK_AccountingBookPeriods_FiscalPeriods_TenantId_FiscalPeriodId",
            "FK_AccountingBookInitializationLines_Accounts_TenantId_AccountId",
            "FK_AccountingBookInitializationLines_AccountingBookInitializations_TenantId_AccountingBookInitializationId",
            "FK_AccountingBookInitializations_FiscalPeriods_TenantId_CutoffFiscalPeriodId",
            "FK_AccountingBookInitializations_AccountingBookInitializations_TenantId_AccountingBookId_SupersedesInitializationId",
            "IX_AccountingBookPeriods_TenantId_AccountingBookId_FiscalPeriodId",
            "IX_AccountingBookInitializations_TenantId_AccountingBookId_Version",
            "IX_AccountingBookInitializations_TenantId_AccountingBookId_InitializationStatus",
            "CK_AccountingBookPeriods_NoDelete",
            "CK_AccountingBookInitializations_NoDelete",
            "CK_AccountingBookInitializations_SourceShape",
            "CK_AccountingBookInitializations_ApprovalShape",
            "CK_AccountingBookInitializations_EvidenceFingerprint",
            "CK_AccountingBookInitializations_ReconciliationFingerprint",
            "CK_AccountingBookInitializationLines_NoDelete",
            "CK_AccountingBookInitializationLines_Currency"
        }) source.Should().Contain(token);
        source.Should().Contain("CutoffFiscalPeriodId")
            .And.Contain("LEN([EvidenceFingerprint]) = 64")
            .And.Contain("LEN([CurrencyCode]) = 3")
            .And.Contain("C4_DOWN_GUARD")
            .And.Contain("cannot be represented by the predecessor schema");
    }

    [Fact]
    public void Migration_IsDiscoverableWithoutOpeningDatabaseConnection()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=C4MigrationDiscovery;Trusted_Connection=True")
            .Options;
        using var context = new ApplicationDbContext(options);

        context.GetService<IMigrationsAssembly>().Migrations.Keys.Should()
            .Equal("20260913162402_DisposableDevelopmentCurrentModelBaseline");
        ArchivedMigrationSource.Read("20260906140533_AddAccountingBookPeriodInitializationFoundation.cs")
            .Should().Contain($"Migration(\"{MigrationId}\")");
    }
}
