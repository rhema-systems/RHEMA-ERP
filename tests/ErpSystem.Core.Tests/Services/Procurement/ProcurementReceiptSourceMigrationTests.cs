using System.Reflection;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementReceiptSourceMigrationTests
{
    [Fact]
    public void MigrationAddsGovernedSnapshotsConcurrencyAndIdempotency()
    {
        var operations = Operations();

        operations.OfType<AddColumnOperation>()
            .Where(item => item.Name == "RowVersion")
            .Select(item => item.Table)
            .Should()
            .BeEquivalentTo(
                "PurchaseOrderReceipts",
                "GoodsReceiptNotes");

        operations.OfType<AddColumnOperation>()
            .Where(item => item.Name == "ReceiptSourceIntegrityHash")
            .Select(item => item.Table)
            .Should()
            .BeEquivalentTo(
                "PurchaseOrderReceipts",
                "GoodsReceiptNotes");

        operations.OfType<CreateIndexOperation>()
            .Where(item => item.IsUnique)
            .Select(item => item.Name)
            .Should()
            .Contain(
                "IX_PurchaseOrderReceipts_TenantId_PurchaseOrderId_IdempotencyKey",
                "IX_GoodsReceiptNotes_TenantId_PurchaseOrderReceiptId",
                "IX_PurchaseOrderReceiptItems_TenantId_PurchaseOrderItemId_ReceiptId",
                "IX_GoodsReceiptNoteItems_TenantId_PurchaseOrderItemId_GoodsReceiptNoteId");
    }

    [Fact]
    public void ReceiptTriggersSerializeCapacityAndRejectCrossTenantSources()
    {
        var sql = Sql();

        sql.Should().Contain(
            "TR_PurchaseOrderReceipts_GovernedSource");
        sql.Should().Contain(
            "TR_PurchaseOrderReceiptItems_GovernedCapacity");
        sql.Should().Contain(
            "TR_GoodsReceiptNotes_GovernedSource");
        sql.Should().Contain(
            "TR_GoodsReceiptNoteItems_GovernedCapacity");
        sql.Should().Contain("WITH (UPDLOCK, HOLDLOCK)");
        sql.Should().Contain("RCV_CAPACITY_EXCEEDED");
        sql.Should().Contain("RCV_SOURCE_TENANT_MISMATCH");
        sql.Should().Contain("grn.PurchaseOrderReceiptId IS NULL");
        sql.Should().Contain("grn.Status NOT IN (6, 8)");
        sql.Should().NotContain("DISABLE TRIGGER");
    }

    [Fact]
    public void InventoryPostingTriggersBlockPhantomAndVendorInvoiceStock()
    {
        var sql = Sql();

        sql.Should().Contain(
            "TR_InventoryMovements_GovernedPurchaseReceipt");
        sql.Should().Contain(
            "TR_StockMovements_GovernedPurchaseReceipt");
        sql.Should().Contain(
            "i.ReferenceType <> 1");
        sql.Should().Contain(
            "RCV_PHANTOM_STOCK_BLOCKED");
        sql.Should().Contain(
            "posted.Quantity > COALESCE(porAllowed.Quantity, grnAllowed.Quantity)");
        sql.Should().Contain(
            "ISJSON(receipt.ReceiptSourceSnapshotJson) = 1");
    }

    private static IReadOnlyList<MigrationOperation> Operations()
    {
        var migration = new TDC0501ReceiptSourceControl();
        var builder = new MigrationBuilder(
            "Microsoft.EntityFrameworkCore.SqlServer");
        migration.GetType()
            .GetMethod(
                "Up",
                BindingFlags.Instance |
                BindingFlags.NonPublic)!
            .Invoke(migration, [builder]);
        return builder.Operations;
    }

    private static string Sql() =>
        string.Join(
            Environment.NewLine,
            Operations().OfType<SqlOperation>()
                .Select(item => item.Sql));
}
