using System.Reflection;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementPurchaseOrderSourceMigrationTests
{
    [Fact]
    public void RfqSourceHardStopRequiresAwardLineOwnedByPurchaseOrderSupplier()
    {
        var initialSql = Sql(new TDC0403MandatoryPurchaseOrderSources());

        initialSql.Should().Contain(
            "FROM RequestForQuotationAwardLines awardLine");
        initialSql.Should().Contain(
            "awardLine.TenantId = i.TenantId");
        initialSql.Should().Contain(
            "awardLine.RfqId = rfq.Id");
        initialSql.Should().Contain(
            "awardLine.BusinessPartnerId = i.BusinessPartnerId");
        initialSql.Should().Contain(
            "awardLine.IsDeleted = 0");
        initialSql.Should().Contain("THROW 51207");
    }

    [Fact]
    public void CorrectiveMigrationUpgradesAlreadyAppliedSourceTriggerFailClosed()
    {
        var correctiveSql = Sql(
            new TDC0403RfqAwardSupplierHardStop());

        correctiveSql.Should().Contain(
            "OBJECT_DEFINITION(OBJECT_ID(");
        correctiveSql.Should().Contain(
            "awardLine.BusinessPartnerId = i.BusinessPartnerId");
        correctiveSql.Should().Contain(
            "N'CREATE OR ALTER '");
        correctiveSql.Should().Contain(
            "CHARINDEX(N'TRIGGER', UPPER(@definition))");
        correctiveSql.Should().Contain(
            "EXEC sys.sp_executesql @definition");
        correctiveSql.Should().Contain("THROW 51216");
        correctiveSql.Should().NotContain("DISABLE TRIGGER");
    }

    [Fact]
    public void FrameworkTriggerUsesExceptionalTenderAndRejectsSupersededReadiness()
    {
        var initialSql = Sql(
            new TDC0403FrameworkLineageFailClosed());

        initialSql.Should().Contain(
            "exceptional.TenderId = readiness.SourceId");
        initialSql.Should().NotContain(
            "exceptional.Id = readiness.SourceId");
        initialSql.Should().Contain(
            "newer.SourceType = readiness.SourceType");
        initialSql.Should().Contain(
            "newer.SourceId = readiness.SourceId");
        initialSql.Should().Contain(
            "newer.DecisionSequence >");
        initialSql.Should().Contain(
            "readiness.DecisionSequence");
    }

    [Fact]
    public void CorrectiveFrameworkMigrationUpgradesAppliedTriggerFailClosed()
    {
        var correctiveSql = Sql(
            new TDC0403ExceptionalFrameworkLineage());

        correctiveSql.Should().Contain(
            "OBJECT_DEFINITION(");
        correctiveSql.Should().Contain(
            "exceptional.TenderId = readiness.SourceId");
        correctiveSql.Should().Contain(
            "newer.DecisionSequence >");
        correctiveSql.Should().Contain(
            "N'CREATE OR ALTER '");
        correctiveSql.Should().Contain(
            "EXEC sys.sp_executesql @definition");
        correctiveSql.Should().Contain("THROW 51217");
        correctiveSql.Should().NotContain("DISABLE TRIGGER");
    }

    private static string Sql(Migration migration)
    {
        var builder = new MigrationBuilder(
            "Microsoft.EntityFrameworkCore.SqlServer");
        migration.GetType()
            .GetMethod(
                "Up",
                BindingFlags.Instance |
                BindingFlags.NonPublic)!
            .Invoke(migration, [builder]);
        return string.Join(
            Environment.NewLine,
            builder.Operations.OfType<SqlOperation>()
                .Select(item => item.Sql));
    }
}
