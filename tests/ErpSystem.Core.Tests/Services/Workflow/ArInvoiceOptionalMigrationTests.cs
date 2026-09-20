using System.Reflection;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Workflow;

public sealed class ArInvoiceOptionalMigrationTests
{
    [Fact]
    public void Preserves_historical_required_mode_without_changing_existing_invoice_statuses()
    {
        var columns = Operations("Up").OfType<AddColumnOperation>().ToList();
        columns.Should().HaveCount(2).And.OnlyContain(column => column.Table == "Invoices");
        columns.Single(column => column.Name == "ApprovalRequired").DefaultValue.Should().Be(true);
        columns.Single(column => column.Name == "WorkflowInstanceId").IsNullable.Should().BeTrue();
        Operations("Up").OfType<SqlOperation>().Single().Sql.Should().NotContain("UPDATE dbo.Invoices");
    }

    [Fact]
    public void Direct_readiness_and_completed_approval_are_distinct_and_tenant_bound()
    {
        var sql = Operations("Up").OfType<SqlOperation>().Single().Sql;
        sql.Should().Contain("d.Status IN (1,9) AND i.Status=10 AND i.ApprovalRequired=0");
        sql.Should().Contain("WorkflowApprovalRequiredAtSubmission(i.TenantId,N'Invoice',i.Id)=0");
        sql.Should().Contain("d.Id IS NULL OR i.Status NOT IN (2,3,4,5,6,10) OR i.WorkflowInstanceId IS NOT NULL");
        sql.Should().Contain("w.TenantId=i.TenantId AND w.EntityId=i.Id AND w.IsDeleted=0");
        sql.Should().Contain("d.Status IN (7,8) AND i.Status=2");
        sql.Should().Contain("w.Status<>2 OR w.CompletedDate IS NULL");
        sql.Should().NotContain("DISABLE TRIGGER");
    }

    [Fact]
    public void Downgrade_refuses_to_erase_retained_submission_mode()
    {
        Operations("Down")[0].Should().BeOfType<SqlOperation>().Which.Sql.Should()
            .Contain("ApprovalRequired=0 OR WorkflowInstanceId IS NOT NULL OR Status=10").And.Contain("THROW 51994");
    }

    private static IReadOnlyList<MigrationOperation> Operations(string direction)
    {
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        typeof(ArInvoiceOptionalWorkflow).GetMethod(direction, BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(new ArInvoiceOptionalWorkflow(), [builder]);
        return builder.Operations;
    }
}
