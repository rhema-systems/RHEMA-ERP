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
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        new TestableMigration().ApplyUp(builder);

        var table = builder.Operations.OfType<CreateTableOperation>().Should().ContainSingle().Subject;
        table.Name.Should().Be("ProcurementFixedAssetCapitalizations");
        table.Columns.Select(value => value.Name).Should().Contain(new[]
        {
            "FixedAssetId", "AcceptedSupplySourceId", "PurchaseOrderId", "PurchaseOrderItemId",
            "InventoryItemId", "SourceIntegrityHash", "SourceSnapshotJson", "ReceiptPostingEvidenceJson",
            "PostingEventId", "JournalEntryId"
        });
        table.CheckConstraints.Select(value => value.Name).Should().BeEquivalentTo(
            "CK_ProcurementFixedAssetCapitalizations_Quantity",
            "CK_ProcurementFixedAssetCapitalizations_Amounts");
        table.ForeignKeys.Should().Contain(value => value.PrincipalTable == "FixedAssets");
        table.ForeignKeys.Should().Contain(value => value.PrincipalTable == "PurchaseOrderItems");
        table.ForeignKeys.Should().OnlyContain(value => value.OnDelete == ReferentialAction.Restrict);

        builder.Operations.OfType<CreateIndexOperation>().Should().Contain(value =>
            value.Name == "IX_ProcurementFixedAssetCapitalizations_TenantId_IdempotencyKey" && value.IsUnique);
    }

    [Fact]
    [Trait("Category", "Architecture")]
    public void Down_ShouldDropOnlyTheHandoffTable()
    {
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        new TestableMigration().ApplyDown(builder);

        builder.Operations.Should().ContainSingle().Which.Should().BeOfType<DropTableOperation>()
            .Which.Name.Should().Be("ProcurementFixedAssetCapitalizations");
    }

    private sealed class TestableMigration : AddProcurementFixedAssetCapitalization
    {
        public void ApplyUp(MigrationBuilder builder) => Up(builder);
        public void ApplyDown(MigrationBuilder builder) => Down(builder);
    }
}
