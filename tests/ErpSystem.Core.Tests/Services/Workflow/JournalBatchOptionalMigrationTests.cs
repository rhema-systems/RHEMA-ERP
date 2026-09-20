using System.Reflection;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Workflow;

public sealed class JournalBatchOptionalMigrationTests
{
    [Fact]
    public void Adds_only_required_history_default_without_rewriting_saved_batches()
    {
        var column = Operations("Up").OfType<AddColumnOperation>().Should().ContainSingle().Which;
        column.Table.Should().Be("JournalBatches");
        column.Name.Should().Be("ApprovalRequired");
        column.DefaultValue.Should().Be(true);
        var sql = string.Join('\n', Operations("Up").OfType<SqlOperation>().Select(x => x.Sql));
        sql.Should().NotContain("UPDATE dbo.JournalBatches").And.NotContain("DISABLE TRIGGER");
    }

    [Fact]
    public void Direct_ready_mode_is_draft_only_tenant_bound_and_cannot_replace_approval_history()
    {
        var sql = Operations("Up").OfType<SqlOperation>().First().Sql;
        sql.Should().Contain("d.ApprovalRequired=1 AND d.ApprovalStatus=N'Draft' AND i.ApprovalRequired=0");
        sql.Should().Contain("d.Id IS NULL OR i.ApprovalStatus<>N'ReadyToPost'");
        sql.Should().Contain("WorkflowApprovalRequiredAtSubmission(i.TenantId,N'JournalBatch',i.Id)=0");
        sql.Should().Contain("i.TenantId=d.TenantId AND d.WorkflowInstanceId IS NULL");
        sql.Should().Contain("w.TenantId=i.TenantId AND w.EntityId=i.Id AND w.IsDeleted=0");
        sql.Should().Contain("d.ApprovalStatus=N'PendingApproval' AND i.ApprovalStatus IN (N'Approved',N'PartiallyApproved')");
        sql.Should().Contain("w.Status<>2 OR w.CompletedDate IS NULL");
    }

    [Fact]
    public void Child_rows_require_their_saved_direct_batch_and_no_fake_human_approval()
    {
        var sql = string.Join('\n', Operations("Up").OfType<SqlOperation>().Skip(1).Select(x => x.Sql));
        sql.Should().Contain("TR_JournalBatchItems_OptionalApproval").And.Contain("TR_JournalEntries_BatchOptionalApproval");
        sql.Should().Contain("b.TenantId=i.TenantId AND b.IsDeleted=0");
        sql.Should().Contain("d.ReviewStatus NOT IN (N'Pending',N'NotRequired')");
        sql.Should().Contain("EXISTS (SELECT 1 FROM dbo.JournalBatchItemReviews r WHERE r.JournalBatchItemId=i.Id)");
        sql.Should().Contain("b.ApprovalRequired<>0 OR b.ApprovalStatus<>N'ReadyToPost' OR i.RequiresApproval<>0");
        sql.Should().Contain("i.ApprovedByUserId IS NOT NULL OR i.ApprovedDate IS NOT NULL");
    }

    [Fact]
    public void Downgrade_refuses_to_erase_direct_submission_history()
    {
        Operations("Down")[0].Should().BeOfType<SqlOperation>().Which.Sql.Should()
            .Contain("ApprovalRequired=0 OR ApprovalStatus=N'ReadyToPost'").And.Contain("THROW 51990");
    }

    private static IReadOnlyList<MigrationOperation> Operations(string direction)
    {
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        typeof(JournalBatchOptionalWorkflow).GetMethod(direction, BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(new JournalBatchOptionalWorkflow(), [builder]);
        return builder.Operations;
    }
}
