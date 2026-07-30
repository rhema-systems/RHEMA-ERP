using System.Reflection;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementFrameworkCallOffMigrationTests
{
    [Fact]
    public void SourceConsistencyValidatesEffectiveEndOnlyAtCreation()
    {
        var sql = Sql(new TDC0402FrameworkCallOffs());

        sql.Should().Contain(
            "OR (prior.Id IS NULL");
        sql.Should().Contain(
            "AND i.AgreementEffectiveEndUtc");
        sql.Should().Contain(
            "i.AgreementEffectiveEndUtc <> d.AgreementEffectiveEndUtc");
        sql.Should().Contain("THROW 51102");
        sql.Should().Contain("THROW 51103");
    }

    [Fact]
    public void CorrectiveMigrationUpgradesAppliedSourceConsistencyTrigger()
    {
        var sql = Sql(new TDC0402ExtensionSafeCallOffLineage());

        sql.Should().Contain(
            "OBJECT_DEFINITION(");
        sql.Should().Contain(
            "TR_ProcurementFrameworkCallOffs_Lifecycle");
        sql.Should().Contain(
            "prior.Id IS NULL");
        sql.Should().Contain(
            "i.AgreementEffectiveEndUtc");
        sql.Should().Contain(
            "N'CREATE OR ALTER '");
        sql.Should().Contain(
            "EXEC sys.sp_executesql @definition");
        sql.Should().Contain(
            "CHARINDEX(");
        sql.Should().Contain(
            "N'OR i.AuthorityKind'");
        sql.Should().Contain("THROW 51219");
        sql.Should().NotContain("DISABLE TRIGGER");
    }

    [Fact]
    public void TerminalCallOffsDoNotRequireAStillApprovedRequisition()
    {
        var freshSql = Sql(new TDC0402FrameworkCallOffs());
        var correctiveSql = Sql(new TDC0402TerminalReviewPaths());

        freshSql.Should().Contain(
            "prior.Id IS NULL OR i.Status NOT IN (4, 5)");
        freshSql.Should().Contain(
            "AND requisition.Status <> 'Approved'");
        correctiveSql.Should().Contain(
            "prior.Id IS NULL OR i.Status NOT IN (4, 5)");
        correctiveSql.Should().Contain(
            "TR_ProcurementFrameworkCallOffs_Lifecycle");
        correctiveSql.Should().Contain("THROW 51220");
    }

    [Fact]
    public void RejectedExtensionsDoNotRequireAStillPublishedAgreement()
    {
        var freshSql = Sql(new TDC0401FrameworkAgreements());
        var correctiveSql = Sql(new TDC0402TerminalReviewPaths());

        freshSql.Should().Contain(
            "LEFT JOIN deleted prior ON prior.Id = i.Id");
        freshSql.Should().Contain(
            "prior.Id IS NULL OR i.Status = 1");
        freshSql.Should().Contain(
            "AND agreement.Status <> 2");
        correctiveSql.Should().Contain(
            "TR_ProcurementFrameworkAgreementExtensions_Lifecycle");
        correctiveSql.Should().Contain(
            "prior.Id IS NULL OR i.Status = 1");
        correctiveSql.Should().Contain("THROW 51221");
        correctiveSql.Should().NotContain("DISABLE TRIGGER");
    }

    [Fact]
    public void SharedFrameworkPriceLineMigrationRetainsDemandLineUniqueness()
    {
        var builder = new MigrationBuilder(
            "Microsoft.EntityFrameworkCore.SqlServer");
        var migration = new TDC0402SharedFrameworkPriceLines();
        migration.GetType()
            .GetMethod(
                "Up",
                BindingFlags.Instance |
                BindingFlags.NonPublic)!
            .Invoke(migration, [builder]);

        builder.Operations.OfType<DropIndexOperation>()
            .Should().ContainSingle(item =>
                item.Name ==
                "IX_ProcurementFrameworkCallOffLines_TenantId_CallOffId_AgreementPriceLineId");
        builder.Operations.OfType<CreateIndexOperation>()
            .Should().ContainSingle(item =>
                item.Name ==
                "IX_ProcurementFrameworkCallOffLines_TenantId_CallOffId_AgreementPriceLineId" &&
                !item.IsUnique);
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
