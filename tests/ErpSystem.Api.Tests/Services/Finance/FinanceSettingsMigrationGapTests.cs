using ErpSystem.Data;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public class FinanceSettingsMigrationGapTests
{
    private const string MigrationId = "20260731140309_RepairMissingFinanceSettingsColumns";
    private const string ReconciliationMigrationId = "20260801160552_ReconcileFinanceMasterModelSnapshot";

    private static readonly string[] MissingColumns =
    {
        "MigrationClearingAccountId",
        "RealizedFxGainAccountId",
        "RealizedFxLossAccountId",
        "SegmentClearingAccountId",
        "SubledgerPostingMode",
        "WriteOffExpenseAccountId",
        "WriteOffRecoveryAccountId",
        "RequireSubledgerJournalApproval"
    };

    private static readonly string[] AccountRelationshipColumns =
    {
        "MigrationClearingAccountId",
        "RealizedFxGainAccountId",
        "RealizedFxLossAccountId",
        "WriteOffExpenseAccountId",
        "WriteOffRecoveryAccountId"
    };

    [Fact]
    [Trait("Category", "Architecture")]
    public void RepairMigration_ShouldBeDiscoveredBeforeTheMetadataReconciliationMigration()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=MigrationDiscovery;Trusted_Connection=True")
            .Options;

        using var context = new ApplicationDbContext(options);
        var migrations = context.GetService<IMigrationsAssembly>().Migrations;

        migrations.Should().ContainKey(MigrationId);
        migrations.Should().ContainKey(ReconciliationMigrationId);
        // This regression protects the dependency between these two historical repair migrations.
        // Do not require reconciliation to remain the latest migration: legitimate Finance schema
        // work (such as AP reversal lineage) must be allowed to follow it.
        string.CompareOrdinal(MigrationId, ReconciliationMigrationId).Should().BeNegative();
    }

    [Fact]
    [Trait("Category", "Architecture")]
    public void Up_ShouldRepairAllColumnsDefaultsIndexesAndForeignKeysIdempotently()
    {
        var migrationBuilder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        new TestableMigration().ApplyUp(migrationBuilder);

        var sql = migrationBuilder.Operations.Should().ContainSingle()
            .Which.Should().BeOfType<SqlOperation>().Which.Sql;

        sql.Should().Contain("OBJECT_ID(N'dbo.FinanceSettings', N'U')");
        foreach (var column in MissingColumns)
        {
            sql.Should().Contain($"COL_LENGTH(N'dbo.FinanceSettings', N'{column}')");
            sql.Should().Contain($"[{column}]");
        }

        sql.Should().Contain("SET [SubledgerPostingMode] = N'IFRS'");
        sql.Should().Contain("ALTER COLUMN [SubledgerPostingMode] nvarchar(30) NOT NULL");
        sql.Should().Contain("SET [RequireSubledgerJournalApproval] = 0");
        sql.Should().Contain("ALTER COLUMN [RequireSubledgerJournalApproval] bit NOT NULL");

        foreach (var column in AccountRelationshipColumns)
        {
            sql.Should().Contain($"IX_FinanceSettings_{column}");
            sql.Should().Contain($"FK_FinanceSettings_Accounts_{column}");
            sql.Should().Contain($"FOREIGN KEY ([{column}]) REFERENCES [dbo].[Accounts] ([Id])");
        }
    }

    [Fact]
    [Trait("Category", "Architecture")]
    public void Down_ShouldRemoveRepairOwnedConstraintsBeforeColumns()
    {
        var migrationBuilder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        new TestableMigration().ApplyDown(migrationBuilder);

        var sql = migrationBuilder.Operations.Should().ContainSingle()
            .Which.Should().BeOfType<SqlOperation>().Which.Sql;

        foreach (var column in AccountRelationshipColumns)
        {
            sql.Should().Contain($"DROP CONSTRAINT [FK_FinanceSettings_Accounts_{column}]");
            sql.Should().Contain($"DROP INDEX [IX_FinanceSettings_{column}]");
        }

        foreach (var column in MissingColumns)
        {
            sql.Should().Contain($"DROP COLUMN [{column}]");
        }

        sql.IndexOf("DROP CONSTRAINT", StringComparison.Ordinal).Should()
            .BeLessThan(sql.IndexOf("DROP COLUMN", StringComparison.Ordinal));
    }

    private sealed class TestableMigration : RepairMissingFinanceSettingsColumns
    {
        public void ApplyUp(MigrationBuilder migrationBuilder) => Up(migrationBuilder);

        public void ApplyDown(MigrationBuilder migrationBuilder) => Down(migrationBuilder);
    }
}
