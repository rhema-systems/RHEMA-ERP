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
        var migration = new AddFinanceSourceDimensionsToApAr();
        var up = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        migration.GetType().GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, [up]);

        up.Operations.OfType<CreateTableOperation>().Should().ContainSingle(table =>
            table.Name == "FinanceSourceDimensionChanges");
        up.Operations.OfType<CreateTableOperation>().Should().NotContain(table =>
            new[] { "VendorInvoices", "SupplierDebitNotes", "Invoices", "AccountTransactions" }
                .Contains(table.Name));
        up.Operations.OfType<AlterColumnOperation>().Should().ContainSingle(column =>
            column.Table == "FinanceSourceDimensionAssignments"
            && column.Name == "FinanceDimensionSetId"
            && column.IsNullable);
        up.Operations.OfType<AddColumnOperation>().Should().Contain(column =>
            column.Table == "FinanceSourceDimensionAssignments"
            && column.Name == "BudgetEvidenceStatus"
            && Equals(column.DefaultValue, "NotApplicable"));
        up.Operations.OfType<AddColumnOperation>().Should().Contain(column =>
            column.Table == "FinanceSourceDimensionAssignments"
            && column.Name == "EvidenceFrozenAt"
            && column.IsNullable);

        var changeTable = up.Operations.OfType<CreateTableOperation>().Single();
        changeTable.ForeignKeys.Should().OnlyContain(key => key.OnDelete == ReferentialAction.Restrict);
        var sql = string.Join('\n', up.Operations.OfType<SqlOperation>().Select(operation => operation.Sql));
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
        typeof(AddFinanceSourceDimensionsToApAr).GetCustomAttribute<MigrationAttribute>()
            ?.Id.Should().Be("20260830232009_AddFinanceSourceDimensionsToApAr");
    }
}
