using System.Reflection;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class FinanceDimensionSourceInfrastructureMigrationTests
{
    [Fact]
    public void MigrationAddsLineSpecificEvidenceAndReconstructedLegacyProvenance()
    {
        var migration = new AddFinanceDimensionSourceInfrastructure();
        var up = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        migration.GetType().GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, [up]);

        up.Operations.OfType<CreateTableOperation>().Select(operation => operation.Name)
            .Should().Contain([
                "FinanceDimensionSnapshots",
                "FinanceDimensionSnapshotItems",
                "FinanceDimensionRouteCertifications",
                "FinanceDimensionCertificationTransitions",
                "FinanceDimensionReadinessAssessments",
                "FinanceSourceDimensionAssignments"
            ]);
        up.Operations.OfType<AddColumnOperation>().Should().Contain(column =>
            column.Table == "AccountTransactions" &&
            column.Name == "FinanceDimensionSnapshotId" &&
            column.IsNullable);
        up.Operations.OfType<AddForeignKeyOperation>().Should().Contain(foreignKey =>
            foreignKey.Name == "FK_AccountTransactions_FinanceDimensionSnapshots_FinanceDimensionSnapshotId" &&
            foreignKey.OnDelete == ReferentialAction.Restrict);

        var sql = string.Join("\n", up.Operations.OfType<SqlOperation>().Select(operation => operation.Sql));
        sql.Should().Contain("SnapshotSource = 'MigrationCurrentMasterData'");
        sql.Should().Contain("HistoricalNameReconstructed = 1");
        sql.Should().Contain("SnapshotQuality = 'Reconstructed'");
        sql.Should().Contain("[Unavailable definition ");
        sql.Should().Contain("[Unavailable value ");
        sql.Should().Contain("MERGE FinanceDimensionSnapshots");
        sql.Should().Contain("UPDATE accountRule");
        sql.Should().Contain("FROM FinanceDimensionAccountRules accountRule");
        sql.Should().NotContain("UPDATE rule");
        sql.Should().NotContain("FinanceDimensionAccountRules rule");
        sql.Should().NotContain("UPDATE FinanceDimensionSets SET CombinationHash");
        sql.Should().NotContain("UPDATE AccountTransactions SET DebitAmount");
        sql.Should().NotContain("UPDATE AccountTransactions SET CreditAmount");
        sql.Should().Contain("Finance.Dimensions.Certification.Manage");
        sql.Should().Contain("N'Financial Controller', N'TenantAdmin', N'SuperAdmin'");
        sql.Should().Contain("NOT EXISTS");

        var sourceTable = up.Operations.OfType<CreateTableOperation>().Single(operation =>
            operation.Name == "FinanceSourceDimensionAssignments");
        sourceTable.ForeignKeys.Should().OnlyContain(foreignKey =>
            foreignKey.OnDelete == ReferentialAction.Restrict);
        sourceTable.Columns.Should().Contain(column => column.Name == "SourceDocumentType" && !column.IsNullable);
        sourceTable.Columns.Should().Contain(column => column.Name == "SourceDocumentId" && !column.IsNullable);
        sourceTable.Columns.Should().Contain(column => column.Name == "SourceLineId" && column.IsNullable);
        up.Operations.OfType<CreateIndexOperation>().Should().Contain(index =>
            index.Name == "IX_FinanceSourceDimensionAssignments_SourceKey"
            && index.IsUnique
            && index.Filter == "[IsDeleted] = 0");
    }

    [Fact]
    public void MigrationDownRemovesOnlyTheNewInfrastructure()
    {
        var migration = new AddFinanceDimensionSourceInfrastructure();
        var down = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        migration.GetType().GetMethod("Down", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, [down]);

        down.Operations.OfType<DropTableOperation>().Select(operation => operation.Name)
            .Should().Contain([
                "FinanceDimensionCertificationTransitions",
                "FinanceDimensionReadinessAssessments",
                "FinanceDimensionRouteCertifications",
                "FinanceDimensionSnapshotItems",
                "FinanceSourceDimensionAssignments",
                "FinanceDimensionSnapshots"
            ]);
        down.Operations.OfType<DropColumnOperation>().Should().Contain(column =>
            column.Table == "AccountTransactions" &&
            column.Name == "FinanceDimensionSnapshotId");
        string.Join("\n", down.Operations.OfType<SqlOperation>().Select(operation => operation.Sql))
            .Should().Contain("Finance.Dimensions.Certification.Manage");
    }
}
