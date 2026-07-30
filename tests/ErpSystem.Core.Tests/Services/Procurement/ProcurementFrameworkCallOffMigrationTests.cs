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
