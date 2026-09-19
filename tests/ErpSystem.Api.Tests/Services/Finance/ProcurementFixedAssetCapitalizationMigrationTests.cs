using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

/// <summary>
/// Provider-neutral schema contract for FIN-INT-007. SQL Server UAT still rehearses the generated
/// migration; these tests keep CI sensitive to lost idempotency, quantity or source-line controls.
/// </summary>
public sealed class ProcurementFixedAssetCapitalizationMigrationTests
{
    [Fact]
    [Trait("Category", "Architecture")]
    [Trait("Contract", "FIN-INT-007")]
    public void Up_ShouldCreateTenantScopedEvidenceAndRestrictedSourceLinks()
    {
        var source = ArchivedMigrationSource.Read("20260814103000_AddProcurementFixedAssetCapitalization.cs");
        source.Should().Contain("name: \"ProcurementFixedAssetCapitalizations\"");
        foreach (var token in new[]
        {
            "FixedAssetId", "AcceptedSupplySourceId", "PurchaseOrderId", "PurchaseOrderItemId",
            "InventoryItemId", "SourceIntegrityHash", "SourceSnapshotJson", "ReceiptPostingEvidenceJson",
            "PostingEventId", "JournalEntryId",
            "CK_ProcurementFixedAssetCapitalizations_Quantity", "CK_ProcurementFixedAssetCapitalizations_Amounts",
            "FixedAssets", "PurchaseOrderItems",
            "onDelete: ReferentialAction.Restrict",
            "IX_ProcurementFixedAssetCapitalizations_TenantId_IdempotencyKey"
        }) source.Should().Contain(token);
    }

    [Fact]
    [Trait("Category", "Architecture")]
    public void Down_ShouldDropOnlyTheHandoffTable()
    {
        ArchivedMigrationSource.Read("20260814103000_AddProcurementFixedAssetCapitalization.cs")
            .Should().Contain("migrationBuilder.DropTable(\"ProcurementFixedAssetCapitalizations\")");
    }
}
