using System.Reflection;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementReceiptInspectionMigrationTests
{
    [Fact]
    public void MigrationCreatesOnlyInspectionLifecycleTablesWithoutUnrelatedDrift()
    {
        var operations = Operations();

        operations.OfType<CreateTableOperation>().Select(item => item.Name)
            .Should().BeEquivalentTo(
                "ProcurementReceiptInspectionCases",
                "ProcurementReceiptInspectionLines",
                "ProcurementReceiptInspectionEvidence",
                "ProcurementReceiptInspectionActions");
        operations.Any(item =>
                item is AlterColumnOperation or AddColumnOperation or DropColumnOperation or DropIndexOperation)
            .Should().BeFalse();
    }

    [Fact]
    public void SqlHardStopsProtectAcceptanceWorkflowStockAndImmutableEvidence()
    {
        var sql = Sql();

        sql.Should().Contain("TR_ProcurementReceiptInspectionCases_TDC0502Protected");
        sql.Should().Contain("TR_ProcurementReceiptInspectionEvidence_TDC0502Immutable");
        sql.Should().Contain("TR_ProcurementReceiptInspectionActions_TDC0502Immutable");
        sql.Should().Contain("TR_PurchaseOrderReceiptItems_TDC0502AcceptanceProtected");
        sql.Should().Contain("TR_PurchaseOrderItems_TDC0502AcceptedQuantity");
        sql.Should().Contain("TR_GoodsReceiptNoteItems_TDC0502AcceptanceProtected");
        sql.Should().Contain("TR_InventoryMovements_TDC0502InspectionContext");
        sql.Should().Contain("TDC0502_RECEIPT_INSPECTION_CASE_ID");
        sql.Should().Contain("wi.Status <> 2");
        sql.Should().Contain("wi.Status NOT IN (3,4)");
        sql.Should().Contain("i.DecidedByUserId = i.SubmittedByUserId");
        sql.Should().Contain("RCV_DIRECT_ACCEPTANCE_BLOCKED");
        sql.Should().Contain("RCV_STOCK_INSPECTION_CONTEXT_REQUIRED");
        sql.Should().NotContain("DISABLE TRIGGER");
    }

    [Fact]
    public void RejectedQuantitiesReleaseReplacementCapacityInBothReceiptPaths()
    {
        var sql = Sql();

        sql.Should().Contain("SUM(otherLine.ReceivedQuantity - otherLine.RejectedQuantity)");
        sql.Should().Contain("SUM(grnLine.ReceivedQuantity - grnLine.RejectedQuantity)");
        sql.Should().Contain("SUM(porLine.ReceivedQuantity - porLine.RejectedQuantity)");
        sql.Should().Contain("RCV_TDC0501_TRIGGER_DRIFT");
    }

    private static IReadOnlyList<MigrationOperation> Operations()
    {
        var migration = new TDC0502ReceiptInspectionClosure();
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        migration.GetType().GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, [builder]);
        return builder.Operations;
    }

    private static string Sql() => string.Join(
        Environment.NewLine,
        Operations().OfType<SqlOperation>().Select(item => item.Sql));
}
