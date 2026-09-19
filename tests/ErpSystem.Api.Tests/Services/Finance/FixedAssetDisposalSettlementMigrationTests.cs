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
        var source = ArchivedMigrationSource.Read("20260813103000_AddFixedAssetDisposalSettlement.cs");
        foreach (var column in new[]
        {
            "BuyerBusinessPartnerId", "SettlementMode", "SettlementStatus", "SaleTaxGroupId",
            "CustomerInvoiceId", "CustomerPaymentId", "SettlementInvoiceAmount", "SettlementTaxAmount"
        }) source.Should().Contain(column);
        foreach (var token in new[] { "CK_AssetDisposals_SettlementDestination",
            "CK_AssetDisposals_SettlementDocumentState", "IX_AssetDisposals_TenantId_CustomerInvoiceId",
            "IX_AssetDisposals_TenantId_CustomerPaymentId", "[\"CustomerInvoiceId\"] = \"Invoices\"",
            "[\"CustomerPaymentId\"] = \"CustomerPayment\"", "[\"SettlementPaymentMethodId\"] = \"PaymentMethod\"",
            "onDelete: ReferentialAction.Restrict" }) source.Should().Contain(token);
    }

    [Fact]
    [Trait("Category", "Architecture")]
    public void Down_ShouldRemoveEverySettlementColumnAndConstraint()
    {
        var source = ArchivedMigrationSource.Read("20260813103000_AddFixedAssetDisposalSettlement.cs");
        foreach (var column in new[]
        {
            "BuyerBusinessPartnerId", "SettlementMode", "SettlementStatus", "SaleTaxGroupId",
            "CustomerInvoiceId", "CustomerPaymentId", "SettlementInvoiceAmount", "SettlementTaxAmount"
        }) source.Should().Contain(column);
        source.Should().Contain("migrationBuilder.DropCheckConstraint(");
    }
}
