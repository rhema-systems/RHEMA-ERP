using System.Reflection;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementContractActivationMigrationTests
{
    [Fact]
    public void MigrationCreatesActivationAndEvidenceLedgers()
    {
        var operations = Operations(
            new TDC0407ContractActivationGate());

        operations.OfType<CreateTableOperation>()
            .Select(item => item.Name)
            .Should().Contain([
                "ProcurementContractActivations",
                "ProcurementContractActivationEvidence"
            ]);
        operations.OfType<AddColumnOperation>()
            .Should().Contain(item =>
                item.Table == "Contracts" &&
                item.Name == "RowVersion");
        operations.OfType<AddColumnOperation>()
            .Select(item => $"{item.Table}.{item.Name}")
            .Should().Contain([
                "ContractDocuments.FileUploadRecordId",
                "ContractDocuments.CentralDocumentRecordId",
                "ContractDocuments.CentralDocumentVersionId"
            ]);
    }

    [Fact]
    public void MigrationCreatesFailClosedActivationAndDmsHardStops()
    {
        var sql = Sql(Operations(
            new TDC0407ContractActivationGate()));

        sql.Should().Contain("TR_Contracts_TDC0407ActivationGuard");
        sql.Should().Contain("TDC0407_CONTRACT_ACTIVATION_ID");
        sql.Should().Contain(
            "TR_ContractDocuments_TDC0407DmsRequired");
        sql.Should().Contain("VirusScanStatus = 2");
        sql.Should().Contain(
            "TR_ProcurementContractActivations_TDC0407Protected");
        sql.Should().Contain(
            "TR_ProcurementContractActivationEvidence_TDC0407Immutable");
        sql.Should().Contain("workflow.Status NOT IN (3, 4)");
        sql.Should().Contain("THROW 51421");
        sql.Should().NotContain("DISABLE TRIGGER");
    }

    [Fact]
    public void ActivationAndEvidenceUniquenessIsTenantScoped()
    {
        var indexes = Operations(
                new TDC0407ContractActivationGate())
            .OfType<CreateIndexOperation>()
            .ToList();

        indexes.Should().Contain(item =>
            item.Name ==
            "IX_ProcurementContractActivations_TenantId_ContractId_Sequence" &&
            item.IsUnique);
        indexes.Should().Contain(item =>
            item.Name ==
            "IX_ProcurementContractActivations_TenantId_IdempotencyKey" &&
            item.IsUnique);
        indexes.Should().Contain(item =>
            item.Name ==
            "IX_ProcurementContractActivationEvidence_TenantId_ActivationId_RequirementKey" &&
            item.IsUnique);
    }

    [Fact]
    public void DownDropsHardStopsBeforeSchema()
    {
        var builder = new MigrationBuilder(
            "Microsoft.EntityFrameworkCore.SqlServer");
        var migration = new TDC0407ContractActivationGate();
        migration.GetType()
            .GetMethod(
                "Down",
                BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, [builder]);

        var firstSql = builder.Operations
            .OfType<SqlOperation>()
            .First().Sql;
        firstSql.Should().Contain(
            "DROP TRIGGER IF EXISTS [TR_Contracts_TDC0407ActivationGuard]");
        builder.Operations.IndexOf(
                builder.Operations.OfType<DropTableOperation>()
                    .First())
            .Should().BeGreaterThan(0);
    }

    private static IReadOnlyList<MigrationOperation> Operations(
        Migration migration)
    {
        var builder = new MigrationBuilder(
            "Microsoft.EntityFrameworkCore.SqlServer");
        migration.GetType()
            .GetMethod(
                "Up",
                BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, [builder]);
        return builder.Operations;
    }

    private static string Sql(
        IEnumerable<MigrationOperation> operations) =>
        string.Join(
            Environment.NewLine,
            operations.OfType<SqlOperation>()
                .Select(item => item.Sql));
}
