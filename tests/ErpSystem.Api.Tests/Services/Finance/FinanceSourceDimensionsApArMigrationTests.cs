using System.Reflection;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class FinanceSourceDimensionsApArMigrationTests
{
    [Fact]
    public void MigrationIsFocusedAndPreservesExistingPostingFacts()
    {
        var sql = ArchivedMigrationSource.Read("20260830232009_AddFinanceSourceDimensionsToApAr.cs");
        sql.Should().Contain("name: \"FinanceSourceDimensionChanges\"")
            .And.Contain("name: \"FinanceDimensionSetId\"")
            .And.Contain("name: \"BudgetEvidenceStatus\"")
            .And.Contain("defaultValue: \"NotApplicable\"")
            .And.Contain("name: \"EvidenceFrozenAt\"")
            .And.Contain("onDelete: ReferentialAction.Restrict");
        sql.Should().Contain("SnapshotCapturedAt");
        sql.Should().Contain("EvidenceFrozenAt");
        sql.Should().NotContain("AccountTransactions");
        sql.Should().NotContain("DebitAmount");
        sql.Should().NotContain("CreditAmount");
        sql.Should().NotContain("FinanceDimensionSets] SET");
    }

    [Fact]
    public void FastDebugBuildCanDiscoverTheMigrationWithoutItsDesigner()
    {
        ArchivedMigrationSource.Read("20260830232009_AddFinanceSourceDimensionsToApAr.cs")
            .Should().Contain("Migration(\"20260830232009_AddFinanceSourceDimensionsToApAr\")");
        typeof(DisposableDevelopmentCurrentModelBaseline).GetCustomAttribute<MigrationAttribute>()
            ?.Id.Should().Be("20260916132000_DisposableDevelopmentCurrentModelBaseline");
    }
}
