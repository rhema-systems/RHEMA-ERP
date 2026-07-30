using System.Reflection;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementPurchaseOrderAmendmentMigrationTests
{
    [Fact]
    public void MigrationCreatesCompleteLedgerAndFailClosedSqlProtection()
    {
        var operations = Operations(
            new TDC0406PurchaseOrderAmendments());
        operations.OfType<CreateTableOperation>()
            .Select(item => item.Name)
            .Should().BeEquivalentTo([
                "ProcurementPurchaseOrderAmendments",
                "ProcurementPurchaseOrderCommitmentAdjustments",
                "ProcurementPurchaseOrderAmendmentDispatches",
                "ProcurementPurchaseOrderAmendmentAcknowledgements"
            ]);

        var sql = string.Join(
            Environment.NewLine,
            operations.OfType<SqlOperation>()
                .Select(item => item.Sql));
        sql.Should().Contain(
            "TR_ProcurementPurchaseOrderAmendments_Protected");
        sql.Should().Contain(
            "TR_ProcurementPurchaseOrderCommitmentAdjustments_Immutable");
        sql.Should().Contain(
            "TR_ProcurementPurchaseOrderAmendmentDispatches_Immutable");
        sql.Should().Contain(
            "TR_ProcurementPurchaseOrderAmendmentAcknowledgements_Immutable");
        sql.Should().Contain("TDC0406_PO_AMENDMENT_ID");
        sql.Should().Contain(
            "outside an exact applied TDC-0406 amendment");
        sql.Should().Contain("THROW 51331");
        sql.Should().NotContain("DISABLE TRIGGER");
    }

    [Fact]
    public void MigrationRetainsTenantAndIdempotencyUniqueness()
    {
        var indexes = Operations(
                new TDC0406PurchaseOrderAmendments())
            .OfType<CreateIndexOperation>()
            .ToList();

        indexes.Should().Contain(item =>
            item.Name ==
            "IX_ProcurementPurchaseOrderAmendments_TenantId_PurchaseOrderId_AmendmentSequence" &&
            item.IsUnique);
        indexes.Should().Contain(item =>
            item.Name ==
            "IX_ProcurementPurchaseOrderAmendmentDispatches_TenantId_IdempotencyKey" &&
            item.IsUnique);
        indexes.Should().Contain(item =>
            item.Name ==
            "IX_ProcurementPurchaseOrderAmendmentAcknowledgements_TenantId_IdempotencyKey" &&
            item.IsUnique);
        indexes.Should().Contain(item =>
            item.Name ==
            "IX_ProcurementPurchaseOrderCommitmentAdjustments_AmendmentId" &&
            item.IsUnique);
    }

    [Fact]
    public void DownMigrationUsesSemanticTriggerMarkersForSafeRestoration()
    {
        var builder =
            new MigrationBuilder(
                "Microsoft.EntityFrameworkCore.SqlServer");
        var migration = new TDC0406PurchaseOrderAmendments();
        migration.GetType()
            .GetMethod(
                "Down",
                BindingFlags.Instance |
                BindingFlags.NonPublic)!
            .Invoke(migration, [builder]);

        var sql = string.Join(
            Environment.NewLine,
            builder.Operations.OfType<SqlOperation>()
                .Select(item => item.Sql));
        sql.Should().Contain(
            "CHARINDEX(N'TDC0406_PO_AMENDMENT_ID', @definition)");
        sql.Should().Contain(
            "N'ISNULL(i.ProcurementSourceType, -1)'");
        sql.Should().Contain("WHILE @candidate > 0");
        sql.Should().NotContain(
            "N'                    THROW 51203");
    }

    private static IReadOnlyList<MigrationOperation> Operations(
        Migration migration)
    {
        var builder =
            new MigrationBuilder(
                "Microsoft.EntityFrameworkCore.SqlServer");
        migration.GetType()
            .GetMethod(
                "Up",
                BindingFlags.Instance |
                BindingFlags.NonPublic)!
            .Invoke(migration, [builder]);
        return builder.Operations;
    }
}
