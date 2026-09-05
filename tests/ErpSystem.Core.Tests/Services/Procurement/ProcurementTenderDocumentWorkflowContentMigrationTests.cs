using System.Reflection;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementTenderDocumentWorkflowContentMigrationTests
{
    [Fact]
    public void Migration_is_discoverable_and_relaxes_content_only_before_publication()
    {
        var type = typeof(AllowTenderDocumentWorkflowContentBinding);
        type.GetCustomAttribute<DbContextAttribute>().Should().NotBeNull();
        type.GetCustomAttribute<MigrationAttribute>()!.Id.Should()
            .Be("20260904203000_AllowTenderDocumentWorkflowContentBinding");

        var builder = Operations(new AllowTenderDocumentWorkflowContentBinding(), "Up");
        var check = builder.Operations.OfType<AddCheckConstraintOperation>()
            .Should().ContainSingle().Which;
        check.Sql.Should().Contain("[Status] IN (0, 1)")
            .And.Contain("LEN([ContentChecksumSha256]) IN (0, 64)")
            .And.Contain("[Status] IN (2, 3)")
            .And.Contain("LEN([ContentChecksumSha256]) = 64");
    }

    [Fact]
    public void Forward_trigger_rewrite_fails_closed_and_preserves_exact_workflow_lineage()
    {
        var sql = MigrationSql(new AllowTenderDocumentWorkflowContentBinding(), "Up");

        sql.Should().Contain("OBJECT_DEFINITION")
            .And.Contain("recognized governed source variant")
            .And.Contain("DECLARE @lockedOldGrouped")
            .And.Contain("DECLARE @lockedOldDirect")
            .And.Contain("SET @lockedOldDirect = REPLACE(@lockedOldDirect, CHAR(13) + CHAR(10), CHAR(10))")
            .And.Contain("SET @lineageOld = REPLACE(@lineageOld, CHAR(13) + CHAR(10), CHAR(10))")
            .And.Contain("@groupedMatches + @directMatches <> 1")
            .And.Contain("@lineageMatches <> 1")
            .And.Contain("REPLACE(")
            .And.Contain("TDC-F05B-CONTENT-BINDING-LOCK-BEGIN")
            .And.Contain("TDC-F05B-CONTENT-BINDING-LINEAGE-BEGIN")
            .And.Contain("@hasLockedMarker = 0")
            .And.Contain("incomplete content-binding upgrade")
            .And.Contain("@updated = @definition")
            .And.Contain("could not be upgraded safely")
            .And.Contain("UNICODE(LEFT(@executable, 1)) IN (9, 10, 13, 32)")
            .And.Contain("SET @header = REPLACE(REPLACE(REPLACE(@header, CHAR(9), N' '), CHAR(10), N' '), CHAR(13), N' ')")
            .And.Contain("WHILE CHARINDEX(N'  ', @header) > 0")
            .And.Contain("@header LIKE N'create trigger %'")
            .And.Contain("@header NOT LIKE N'create or alter trigger %'")
            .And.Contain("@header NOT LIKE N'alter trigger %'")
            .And.Contain("SET @executable = N'ALTER' + SUBSTRING(@executable, 7, LEN(@executable))")
            .And.Contain("unsupported statement header")
            .And.Contain("d.ContentWorkflowEvidenceDocumentId IS NULL")
            .And.Contain("contentStep.WorkflowInstanceId = i.WorkflowInstanceId")
            .And.Contain("i.Status IN (2, 3) AND i.ContentWorkflowEvidenceDocumentId IS NULL");
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Both_known_legacy_lock_shapes_transform_to_the_same_hardened_clause(
        bool grouped,
        bool windowsLineEndings)
    {
        const string direct = "where d.status in (1, 2)\n          and exists (";
        const string groupedSource = "where (d.status in (1, 2)\n               or (d.status = 0 and i.status <> 0))\n          and exists (";
        const string canonical = "where ((d.status in (1, 2) and not (workflow content binding))\n               or (d.status = 0 and i.status <> 0))\n          and exists (";
        var source = "before\n" + (grouped ? groupedSource : direct) + "\nafter";
        if (windowsLineEndings)
        {
            source = source.Replace("\n", "\r\n", StringComparison.Ordinal);
        }

        source = source.Replace("\r\n", "\n", StringComparison.Ordinal);

        var groupedMatches = source.Split(groupedSource, StringSplitOptions.None).Length - 1;
        var directMatches = source.Split(direct, StringSplitOptions.None).Length - 1;
        (groupedMatches + directMatches).Should().Be(1);

        var matched = groupedMatches == 1 ? groupedSource : direct;
        source.Replace(matched, canonical, StringComparison.Ordinal).Should()
            .Be("before\n" + canonical + "\nafter");
    }

    [Fact]
    public void Rollback_refuses_to_restore_strict_constraint_over_unbound_history()
    {
        var sql = MigrationSql(new AllowTenderDocumentWorkflowContentBinding(), "Down");

        sql.Should().Contain("Cannot restore the former tender-document constraint")
            .And.Contain("recognized upgraded source shape")
            .And.Contain("could not be restored safely")
            .And.Contain("DECLARE @lockedOld nvarchar(max)")
            .And.Contain("DECLARE @lineageOld nvarchar(max) = N'           OR LEN")
            .And.Contain("OR (d.Status = 0 AND i.Status <> 0)")
            .And.Contain("STUFF(")
            .And.Contain("UNICODE(LEFT(@executable, 1)) IN (9, 10, 13, 32)")
            .And.Contain("@header LIKE N'create trigger %'")
            .And.Contain("@header NOT LIKE N'create or alter trigger %'")
            .And.Contain("@header NOT LIKE N'alter trigger %'")
            .And.Contain("LEN(ContentChecksumSha256) <> 64");

        sql.Should().NotContain("DECLARE @lockedOldDirect");
    }

    private static MigrationBuilder Operations(Migration migration, string methodName)
    {
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        migration.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, [builder]);
        return builder;
    }

    private static string MigrationSql(Migration migration, string methodName)
    {
        var builder = Operations(migration, methodName);
        return string.Join(Environment.NewLine,
            builder.Operations.OfType<SqlOperation>().Select(operation => operation.Sql));
    }
}
