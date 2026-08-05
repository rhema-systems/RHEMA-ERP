using System.Reflection;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementReceiptSodMigrationTests
{
    [Fact]
    public void MigrationCreatesOnlyFourReceiptSodHardStops()
    {
        var operations = Operations();

        operations.Should().OnlyContain(item => item is SqlOperation);
        var sql = Sql();
        sql.Should().Contain("TR_GoodsReceiptNotes_TDC0503SodHardStop");
        sql.Should().Contain(
            "TR_ProcurementReceiptInspectionActions_TDC0503SodHardStop");
        sql.Should().Contain("TR_InventoryMovements_TDC0503ReceiptSod");
        sql.Should().Contain("TR_StockMovements_TDC0503ReceiptSod");
        sql.Should().NotContain("DISABLE TRIGGER");
    }

    [Fact]
    public void SqlFailsClosedForCreatorReceiptAndPositiveStockActions()
    {
        var sql = Sql();

        sql.Should().Contain("THROW 51561");
        sql.Should().Contain("THROW 51562");
        sql.Should().Contain("THROW 51563");
        sql.Should().Contain("THROW 51564");
        sql.Should().Contain("THROW 51565");
        sql.Should().Contain("action.ActionType IN (3, 11, 12)");
        sql.Should().Contain("grn.ReceivedById = purchaseOrder.CreatedById");
        sql.Should().Contain("grn.LastModifiedById = purchaseOrder.CreatedById");
        sql.Should().Contain("movement.CreatedById = purchaseOrder.CreatedById");
        sql.Should().Contain("movement.ProcessedById = purchaseOrder.CreatedById");
    }

    [Fact]
    public void EveryHardStopMaintainsTenantScopedSourceLineage()
    {
        var sql = Sql();

        sql.Should().Contain("purchaseOrder.TenantId = grn.TenantId");
        sql.Should().Contain("inspection.TenantId = action.TenantId");
        sql.Should().Contain("receipt.TenantId = action.TenantId");
        sql.Should().Contain("receipt.TenantId = movement.TenantId");
        sql.Should().Contain("grn.TenantId = movement.TenantId");
    }

    private static IReadOnlyList<MigrationOperation> Operations()
    {
        var migration = new TDC0503ReceiptSodClosure();
        var builder = new MigrationBuilder(
            "Microsoft.EntityFrameworkCore.SqlServer");
        migration.GetType()
            .GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, [builder]);
        return builder.Operations;
    }

    private static string Sql() => string.Join(
        Environment.NewLine,
        Operations().OfType<SqlOperation>().Select(item => item.Sql));
}
