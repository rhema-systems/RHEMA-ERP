using System.Reflection;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementWorksCloseoutMigrationTests
{
    [Fact]
    public void MigrationCreatesTenantScopedActionAndEvidenceLedgers()
    {
        var operations = Operations(new TDC0409WorksCloseoutControl());

        operations.OfType<CreateTableOperation>()
            .Select(item => item.Name)
            .Should().Contain([
                "ProcurementWorksCloseoutActions",
                "ProcurementWorksCloseoutEvidence"
            ]);
        operations.OfType<CreateIndexOperation>().Should().Contain(item =>
            item.Name ==
            "IX_ProcurementWorksCloseoutActions_TenantId_ContractId_Sequence" &&
            item.IsUnique);
        operations.OfType<CreateIndexOperation>().Should().Contain(item =>
            item.Name ==
            "IX_ProcurementWorksCloseoutActions_TenantId_IdempotencyKey" &&
            item.IsUnique);
        operations.OfType<CreateIndexOperation>().Should().Contain(item =>
            item.Name ==
            "IX_ProcurementWorksCloseoutEvidence_TenantId_ActionId_RequirementKey" &&
            item.IsUnique);
    }

    [Fact]
    public void MigrationCreatesFailClosedWorkflowFinanceAndContractGuards()
    {
        var sql = Sql(Operations(new TDC0409WorksCloseoutControl()));

        sql.Should().Contain(
            "TR_ProcurementWorksCloseoutActions_TDC0409Protected");
        sql.Should().Contain(
            "TR_ProcurementWorksCloseoutEvidence_TDC0409Immutable");
        sql.Should().Contain("TR_Contracts_TDC0409CloseoutGuard");
        sql.Should().Contain("TDC0409_WORKS_CLOSEOUT_ACTION_ID");
        sql.Should().Contain("wi.[Status] <> 2");
        sql.Should().Contain("i.[AmountAutoPosted] <> 0");
        sql.Should().Contain("a.[ActionType] <> 10");
        sql.Should().Contain("a.[ActionType] <> 8");
        sql.Should().NotContain("DISABLE TRIGGER");
    }

    [Fact]
    public void DownDropsGuardsBeforeTables()
    {
        var builder = new MigrationBuilder(
            "Microsoft.EntityFrameworkCore.SqlServer");
        var migration = new TDC0409WorksCloseoutControl();
        migration.GetType().GetMethod(
                "Down", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, [builder]);

        builder.Operations.OfType<SqlOperation>().First().Sql
            .Should().Contain(
                "DROP TRIGGER IF EXISTS [TR_Contracts_TDC0409CloseoutGuard]");
        builder.Operations.IndexOf(
                builder.Operations.OfType<DropTableOperation>().First())
            .Should().BeGreaterThan(0);
    }

    private static IReadOnlyList<MigrationOperation> Operations(Migration migration)
    {
        var builder = new MigrationBuilder(
            "Microsoft.EntityFrameworkCore.SqlServer");
        migration.GetType().GetMethod(
                "Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, [builder]);
        return builder.Operations;
    }

    private static string Sql(IEnumerable<MigrationOperation> operations) =>
        string.Join(
            Environment.NewLine,
            operations.OfType<SqlOperation>().Select(item => item.Sql));
}
