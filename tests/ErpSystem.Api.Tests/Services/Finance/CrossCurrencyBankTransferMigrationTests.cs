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
        var columnSql = ArchivedMigrationSource.Read("20260803213749_AddCrossCurrencyBankTransfers.cs");
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

        var relationshipSql = columnSql;
        relationshipSql.Should().Contain("IX_CashTransaction_ExchangeRateId");
        relationshipSql.Should().Contain("IX_CashTransaction_TenantId_ExchangeRateId");
        relationshipSql.Should().Contain("IX_CashTransaction_TenantId_TransferPairId_TransferLeg");
        relationshipSql.Should().Contain("FK_CashTransaction_ExchangeRates_ExchangeRateId");
        relationshipSql.Should().Contain("IF NOT EXISTS");
    }
}
