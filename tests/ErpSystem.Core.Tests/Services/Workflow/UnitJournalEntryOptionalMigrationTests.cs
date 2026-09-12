using System.Reflection;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Workflow;

public sealed class UnitJournalEntryOptionalMigrationTests
{
    [Fact]
    public void Schema_is_additive_and_preserves_historical_approval_requirement()
    {
        var operations = Operations("Up");
        var columns = operations.OfType<AddColumnOperation>().ToList();
        columns.Select(column => column.Name).Should().BeEquivalentTo("ApprovalRequired", "WorkflowInstanceId");
        columns.Should().OnlyContain(column => column.Table == "UnitJournalEntries");
        columns.Single(column => column.Name == "ApprovalRequired").DefaultValue.Should().Be(true);
        columns.Single(column => column.Name == "WorkflowInstanceId").IsNullable.Should().BeTrue();
        operations.OfType<DropColumnOperation>().Should().BeEmpty();
    }

    [Fact]
    public void Direct_ready_mode_requires_absent_central_policy_and_no_fabricated_approval()
    {
        var sql = Operations("Up").OfType<SqlOperation>().Single().Sql;
        sql.Should().Contain("WorkflowApprovalRequiredAtSubmission(i.TenantId,N'UnitJournalEntry',i.Id)");
        sql.Should().Contain("i.Status=6 AND i.ApprovalRequired<>0");
        sql.Should().Contain("i.ApprovedBy IS NOT NULL OR i.ApprovedAt IS NOT NULL");
        sql.Should().Contain("i.WorkflowInstanceId IS NOT NULL");
        sql.Should().Contain("w.EntityId=i.Id");
        sql.Should().Contain("w.TenantId=i.TenantId");
        sql.Should().Contain("d.Id IS NULL OR i.Status NOT IN (4,5,6)");
        sql.Should().Contain("d.Status IN (0,3) AND i.Status=6 AND i.ApprovalRequired=0");
        sql.Should().Contain("d.ApprovedBy IS NULL AND d.ApprovedAt IS NULL");
        sql.Should().NotContain("d.Status IN (0,1,3)");
        sql.Should().NotContain("DISABLE TRIGGER");
    }

    [Fact]
    public void Downgrade_refuses_to_discard_direct_readiness_or_workflow_lineage()
    {
        Operations("Down")[0].Should().BeOfType<SqlOperation>().Which.Sql
            .Should().Contain("ApprovalRequired=0 OR WorkflowInstanceId IS NOT NULL OR Status=6")
            .And.Contain("THROW 51987");
    }

    private static IReadOnlyList<MigrationOperation> Operations(string direction)
    {
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        typeof(UnitJournalEntryOptionalWorkflow).GetMethod(direction, BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(new UnitJournalEntryOptionalWorkflow(), [builder]);
        return builder.Operations;
    }
}
