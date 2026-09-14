using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using System.Reflection;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class LineScopedCrossCurrencyDeductionMigrationTests
{
    [Fact]
    [Trait("Batch", "FinanceGoLive-CrossCurrencyDeductions")]
    [Trait("Category", "Architecture")]
    public void Migration_ShouldAddAndRemoveEveryApArDeductionEvidenceColumn()
    {
        var source = ArchivedMigrationSource.Read("20260806143000_AddLineScopedCrossCurrencyDeductions.cs");
        foreach (var column in new[]
        {
            "VendorPaymentAllocation.DiscountFunctionalAmount",
            "VendorPaymentAllocation.WithholdingTaxFunctionalAmount",
            "PaymentAllocation.DiscountFunctionalAmount",
            "PaymentAllocation.WithholdingTaxAmount",
            "PaymentAllocation.WithholdingTaxFunctionalAmount",
            "PaymentAllocation.VatWithholdingAmount",
            "PaymentAllocation.VatWithholdingFunctionalAmount"
        })
        {
            var parts = column.Split('.');
            source.Should().Contain($"AddColumn<decimal>(\"{parts[1]}\", \"{parts[0]}\", \"decimal(18,2)\", nullable: false, defaultValue: 0m)");
            source.Should().Contain("migrationBuilder.DropColumn(").And.Contain(parts[1]);
        }
    }
}
