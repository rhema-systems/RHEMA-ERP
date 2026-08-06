using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class CrossCurrencySettlementMigrationTests
{
    [Fact]
    [Trait("Category", "Architecture")]
    public void Up_ShouldBackfillExistingSameCurrencyAllocationAndRealizedFxEvidence()
    {
        var migrationBuilder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        new TestableMigration().ApplyUp(migrationBuilder);

        var dataOperations = migrationBuilder.Operations.OfType<SqlOperation>().ToArray();
        dataOperations.Should().HaveCount(2);

        var allocationBackfill = dataOperations[0].Sql;
        allocationBackfill.Should().Contain("Cannot safely backfill pre-migration AP cross-currency allocations");
        allocationBackfill.Should().Contain("Cannot safely backfill pre-migration AR cross-currency allocations");
        allocationBackfill.Should().Contain("[PaymentCurrencyAmount] = [a].[AllocatedAmount]");
        allocationBackfill.Should().Contain("[SettlementFunctionalAmount] = ROUND");
        allocationBackfill.Should().Contain("INNER JOIN [VendorInvoice]");
        allocationBackfill.Should().Contain("INNER JOIN [Invoices]");

        var realizedFxBackfill = dataOperations[1].Sql;
        realizedFxBackfill.Should().Contain("FROM [FxRealizedSettlements]");
        realizedFxBackfill.Should().Contain("INNER JOIN [VendorPaymentAllocation]");
        realizedFxBackfill.Should().Contain("INNER JOIN [PaymentAllocation]");
        realizedFxBackfill.Should().Contain("[PaymentExchangeRate] = [a].[PaymentExchangeRate]");
    }

    private sealed class TestableMigration : AddApArCrossCurrencySettlement
    {
        public void ApplyUp(MigrationBuilder migrationBuilder) => Up(migrationBuilder);
    }
}
