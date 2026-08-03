using System.Reflection;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementReceiptReplayLineageMigrationTests
{
    [Fact]
    public void MigrationPersistsUniqueReplacementLineageAndGrnRequestFingerprint()
    {
        var operations = Operations();

        operations.OfType<AddColumnOperation>().Should().Contain(item =>
            item.Table == "GoodsReceiptNotes" &&
            item.Name == "IdempotencyRequestHash" &&
            item.MaxLength == 64);
        operations.OfType<CreateIndexOperation>().Should().Contain(item =>
            item.Name == "IX_ProcurementReceiptInspectionCases_TenantId_ReplacementPurchaseOrderReceiptId" &&
            item.IsUnique &&
            item.Filter == "[ReplacementPurchaseOrderReceiptId] IS NOT NULL");
        operations.OfType<AddForeignKeyOperation>().Select(item => item.PrincipalTable)
            .Should().Contain(new[]
            {
                "PurchaseOrderReceipts",
                "ProcurementReceiptInspectionCases"
            });
    }

    [Fact]
    public void MigrationHardStopsFingerprintMutationAndInvalidReplacementReuse()
    {
        var sql = string.Join(
            Environment.NewLine,
            Operations().OfType<SqlOperation>().Select(item => item.Sql));

        sql.Should().Contain("TR_TDC0501_GoodsReceiptIdempotencyFingerprint");
        sql.Should().Contain("RCV_IDEMPOTENCY_FINGERPRINT_IMMUTABLE");
        sql.Should().Contain("TR_TDC0502_ReceiptReplacementLineage");
        sql.Should().Contain("RCV_REPLACEMENT_LINEAGE_IMMUTABLE");
        sql.Should().Contain("replacementReceipt.PurchaseOrderId <> sourceReceipt.PurchaseOrderId");
        sql.Should().Contain("action.ActionType = 10");
        sql.Should().Contain("replacementReceipt.CreatedAt < replacementRequest.OccurredAtUtc");
        sql.Should().Contain("replacementCase.Status <> 8");
        sql.Should().NotContain("DISABLE TRIGGER");
    }

    private static IReadOnlyList<MigrationOperation> Operations()
    {
        var migration = new TDC0502ReceiptReplayAndReplacementLineage();
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        migration.GetType()
            .GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, [builder]);
        return builder.Operations;
    }
}
