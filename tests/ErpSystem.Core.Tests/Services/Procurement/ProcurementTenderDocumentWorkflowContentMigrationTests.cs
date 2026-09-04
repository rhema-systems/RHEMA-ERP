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
            .And.Contain("@lockedMatches <> 1 OR @lineageMatches <> 1")
            .And.Contain("single expected source shape")
            .And.Contain("@updated = @definition")
            .And.Contain("could not be upgraded safely")
            .And.Contain("d.ContentWorkflowEvidenceDocumentId IS NULL")
            .And.Contain("contentStep.WorkflowInstanceId = i.WorkflowInstanceId")
            .And.Contain("i.Status IN (2, 3) AND i.ContentWorkflowEvidenceDocumentId IS NULL");
    }

    [Fact]
    public void Rollback_refuses_to_restore_strict_constraint_over_unbound_history()
    {
        var sql = MigrationSql(new AllowTenderDocumentWorkflowContentBinding(), "Down");

        sql.Should().Contain("Cannot restore the former tender-document constraint")
            .And.Contain("single expected upgraded shape")
            .And.Contain("could not be restored safely")
            .And.Contain("LEN(ContentChecksumSha256) <> 64");
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
