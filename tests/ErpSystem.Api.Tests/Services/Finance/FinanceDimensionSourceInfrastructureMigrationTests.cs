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
        var sql = ArchivedMigrationSource.Read("20260830193635_AddFinanceDimensionSourceInfrastructure.cs");
        foreach (var table in new[] {
                "FinanceDimensionSnapshots",
                "FinanceDimensionSnapshotItems",
                "FinanceDimensionRouteCertifications",
                "FinanceDimensionCertificationTransitions",
                "FinanceDimensionReadinessAssessments",
                "FinanceSourceDimensionAssignments"
            }) sql.Should().Contain($"name: \"{table}\"");
        sql.Should().Contain("name: \"FinanceDimensionSnapshotId\"")
            .And.Contain("FK_AccountTransactions_FinanceDimensionSnapshots_FinanceDimensionSnapshotId")
            .And.Contain("onDelete: ReferentialAction.Restrict");
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

        sql.Should().Contain("SourceDocumentType")
            .And.Contain("SourceDocumentId")
            .And.Contain("SourceLineId")
            .And.Contain("IX_FinanceSourceDimensionAssignments_SourceKey")
            .And.Contain("filter: \"[IsDeleted] = 0\"");
    }

    [Fact]
    public void MigrationDownRemovesOnlyTheNewInfrastructure()
    {
        var source = ArchivedMigrationSource.Read("20260830193635_AddFinanceDimensionSourceInfrastructure.cs");
        foreach (var table in new[] {
                "FinanceDimensionCertificationTransitions",
                "FinanceDimensionReadinessAssessments",
                "FinanceDimensionRouteCertifications",
                "FinanceDimensionSnapshotItems",
                "FinanceSourceDimensionAssignments",
                "FinanceDimensionSnapshots"
            }) source.Should().Contain(table);
        source.Should().Contain("migrationBuilder.DropColumn(").And.Contain("FinanceDimensionSnapshotId")
            .And.Contain("Finance.Dimensions.Certification.Manage");
    }
}
