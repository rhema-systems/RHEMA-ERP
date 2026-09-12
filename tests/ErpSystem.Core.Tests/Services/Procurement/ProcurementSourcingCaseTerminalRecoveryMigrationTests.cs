using System.Reflection;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementSourcingCaseTerminalRecoveryMigrationTests
{
    [Fact]
    public void Forward_trigger_validates_new_lineage_but_allows_immutable_historical_updates()
    {
        var sql = MigrationSql(new AllowStaleSourcingCaseTerminalRecovery(), "Up");

        sql.Should().Contain("LEFT JOIN deleted lineageHistory")
            .And.Contain("WHERE lineageHistory.[Id] IS NULL AND (")
            .And.Contain("THROW 51061")
            .And.Contain("THROW 51062",
                "historical cases must remain immutable while moving to a terminal state");
    }

    [Fact]
    public void Rollback_restores_lineage_validation_on_every_update()
    {
        var sql = MigrationSql(new AllowStaleSourcingCaseTerminalRecovery(), "Down");

        sql.Should().NotContain("LEFT JOIN deleted lineageHistory")
            .And.NotContain("WHERE lineageHistory.[Id] IS NULL AND (")
            .And.Contain("WHERE i.[IsDeleted] = 1");
    }

    [Fact]
    public void Successor_index_allows_terminal_history_but_only_one_active_case()
    {
        var migration = new AllowTerminalSourcingCaseRetry();
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        Invoke(migration, "Up", builder);

        var index = builder.Operations.OfType<CreateIndexOperation>().Should().ContainSingle().Which;
        index.Name.Should().Be("IX_ProcurementSourcingCases_TenantId_SourcingReleaseId");
        index.IsUnique.Should().BeTrue();
        index.Filter.Should().Be("[IsDeleted] = 0 AND [Status] IN (0, 1)");
    }

    private static string MigrationSql(Migration migration, string methodName)
    {
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        Invoke(migration, methodName, builder);
        return string.Join(Environment.NewLine,
            builder.Operations.OfType<SqlOperation>().Select(operation => operation.Sql));
    }

    private static void Invoke(Migration migration, string methodName, MigrationBuilder builder) =>
        migration.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, [builder]);
}
