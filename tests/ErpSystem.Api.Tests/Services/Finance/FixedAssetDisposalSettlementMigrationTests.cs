using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

/// <summary>
/// Provider-neutral contract tests for the focused FIN-LIM-0040 migration. They catch accidental
/// loss of the statutory document links or persistence controls without requiring every pull
/// request runner to own a disposable SQL Server instance; the generated SQL is still rehearsed
/// against the TDC UAT/dry-run database before deployment.
/// </summary>
public sealed class FixedAssetDisposalSettlementMigrationTests
{
    [Fact]
    [Trait("Category", "Architecture")]
    public void Up_ShouldAddSettlementEvidenceConstraintsAndRestrictedDocumentLinks()
    {
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        new TestableMigration().ApplyUp(builder);

        var columns = builder.Operations.OfType<AddColumnOperation>().Select(item => item.Name).ToArray();
        columns.Should().Contain(new[]
        {
            "BuyerBusinessPartnerId", "SettlementMode", "SettlementStatus", "SaleTaxGroupId",
            "CustomerInvoiceId", "CustomerPaymentId", "SettlementInvoiceAmount", "SettlementTaxAmount"
        });

        var checks = builder.Operations.OfType<AddCheckConstraintOperation>().ToArray();
        checks.Select(item => item.Name).Should().BeEquivalentTo(
            "CK_AssetDisposals_SettlementDestination",
            "CK_AssetDisposals_SettlementDocumentState");

        var documentIndexes = builder.Operations.OfType<CreateIndexOperation>()
            .Where(item => item.Name is "IX_AssetDisposals_TenantId_CustomerInvoiceId" or "IX_AssetDisposals_TenantId_CustomerPaymentId")
            .ToArray();
        documentIndexes.Should().HaveCount(2);
        documentIndexes.Should().OnlyContain(item => item.IsUnique && item.Filter != null);

        var foreignKeys = builder.Operations.OfType<AddForeignKeyOperation>().ToArray();
        foreignKeys.Should().Contain(item => item.Columns.Single() == "CustomerInvoiceId" && item.PrincipalTable == "Invoices");
        foreignKeys.Should().Contain(item => item.Columns.Single() == "CustomerPaymentId" && item.PrincipalTable == "CustomerPayments");
        foreignKeys.Should().OnlyContain(item => item.OnDelete == ReferentialAction.Restrict);
    }

    [Fact]
    [Trait("Category", "Architecture")]
    public void Down_ShouldRemoveEverySettlementColumnAndConstraint()
    {
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        new TestableMigration().ApplyDown(builder);

        builder.Operations.OfType<DropCheckConstraintOperation>().Should().HaveCount(2);
        var droppedColumns = builder.Operations.OfType<DropColumnOperation>().Select(item => item.Name).ToArray();
        droppedColumns.Should().Contain(new[]
        {
            "BuyerBusinessPartnerId", "SettlementMode", "SettlementStatus", "SaleTaxGroupId",
            "CustomerInvoiceId", "CustomerPaymentId", "SettlementInvoiceAmount", "SettlementTaxAmount"
        });
    }

    private sealed class TestableMigration : AddFixedAssetDisposalSettlement
    {
        public void ApplyUp(MigrationBuilder builder) => Up(builder);
        public void ApplyDown(MigrationBuilder builder) => Down(builder);
    }
}
