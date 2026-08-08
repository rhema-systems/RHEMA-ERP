using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public class CrossCurrencyBankTransferMigrationTests
{
    [Fact]
    [Trait("Category", "Architecture")]
    public void Up_ShouldReconcilePartiallyExistingColumnsIndexesAndForeignKey()
    {
        var migrationBuilder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        new TestableMigration().ApplyUp(migrationBuilder);

        var operations = migrationBuilder.Operations;
        operations.Should().HaveCount(3);
        operations[0].Should().BeOfType<AlterColumnOperation>();

        var columnSql = operations[1].Should().BeOfType<SqlOperation>().Which.Sql;
        foreach (var column in new[]
                 {
                     "ExchangeRateDate",
                     "ExchangeRateId",
                     "ExchangeRateQuoteSide",
                     "ExchangeRateSource",
                     "TransferCrossRate",
                     "TransferFxGainLossBaseAmount",
                     "TransferLeg",
                     "TransferPairId"
                 })
        {
            columnSql.Should().Contain($"COL_LENGTH(N'dbo.CashTransaction', N'{column}')");
        }

        var relationshipSql = operations[2].Should().BeOfType<SqlOperation>().Which.Sql;
        relationshipSql.Should().Contain("IX_CashTransaction_ExchangeRateId");
        relationshipSql.Should().Contain("IX_CashTransaction_TenantId_ExchangeRateId");
        relationshipSql.Should().Contain("IX_CashTransaction_TenantId_TransferPairId_TransferLeg");
        relationshipSql.Should().Contain("FK_CashTransaction_ExchangeRates_ExchangeRateId");
        relationshipSql.Should().Contain("IF NOT EXISTS");
    }

    private sealed class TestableMigration : AddCrossCurrencyBankTransfers
    {
        public void ApplyUp(MigrationBuilder migrationBuilder) => Up(migrationBuilder);
    }
}
