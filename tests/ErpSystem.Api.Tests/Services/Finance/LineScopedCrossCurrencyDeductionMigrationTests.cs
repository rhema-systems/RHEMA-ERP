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
        var migration = new AddLineScopedCrossCurrencyDeductions();
        var upBuilder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        InvokeMigrationMethod(migration, "Up", upBuilder);

        var added = upBuilder.Operations.OfType<AddColumnOperation>().ToArray();
        added.Should().HaveCount(7);
        added.Select(operation => $"{operation.Table}.{operation.Name}").Should().BeEquivalentTo(
        [
            "VendorPaymentAllocation.DiscountFunctionalAmount",
            "VendorPaymentAllocation.WithholdingTaxFunctionalAmount",
            "PaymentAllocation.DiscountFunctionalAmount",
            "PaymentAllocation.WithholdingTaxAmount",
            "PaymentAllocation.WithholdingTaxFunctionalAmount",
            "PaymentAllocation.VatWithholdingAmount",
            "PaymentAllocation.VatWithholdingFunctionalAmount"
        ]);
        added.Should().OnlyContain(operation =>
            operation.ColumnType == "decimal(18,2)" &&
            operation.IsNullable == false &&
            Equals(operation.DefaultValue, 0m));

        // Down symmetry matters in development because the application is still allowed to reset
        // and replay its migration chain while Finance functionality is being completed.
        var downBuilder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        InvokeMigrationMethod(migration, "Down", downBuilder);
        downBuilder.Operations.OfType<DropColumnOperation>()
            .Select(operation => $"{operation.Table}.{operation.Name}")
            .Should().BeEquivalentTo(added.Select(operation => $"{operation.Table}.{operation.Name}"));
    }

    private static void InvokeMigrationMethod(
        AddLineScopedCrossCurrencyDeductions migration,
        string methodName,
        MigrationBuilder migrationBuilder)
    {
        // This migration is intentionally sealed. Reflection gives the test access to EF's
        // protected lifecycle methods without weakening the production type solely for testing.
        typeof(AddLineScopedCrossCurrencyDeductions)
            .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, [migrationBuilder]);
    }
}
