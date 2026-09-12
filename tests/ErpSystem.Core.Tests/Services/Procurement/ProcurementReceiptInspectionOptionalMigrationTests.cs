using System.Reflection;
using System.Text.RegularExpressions;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementReceiptInspectionOptionalMigrationTests
{
    [Fact]
    public void Historical_rows_remain_required_and_only_receipt_mode_schema_changes()
    {
        var operations = Operations(new ReceiptInspectionOptionalWorkflow());
        var column = operations.OfType<AddColumnOperation>().Should().ContainSingle().Subject;
        column.Name.Should().Be("ApprovalRequired");
        column.Table.Should().Be("ProcurementReceiptInspectionCases");
        column.DefaultValue.Should().Be(true);
        column.IsNullable.Should().BeFalse();
        var definition = operations.OfType<AlterColumnOperation>().Should().ContainSingle().Subject;
        definition.Name.Should().Be("WorkflowDefinitionId");
        definition.Table.Should().Be("ProcurementReceiptInspectionCases");
        definition.IsNullable.Should().BeTrue();
        operations.OfType<CreateTableOperation>().Should().BeEmpty();
        operations.OfType<DropTableOperation>().Should().BeEmpty();
        operations.OfType<DropColumnOperation>().Should().BeEmpty();
    }

    [Fact]
    public void Surgical_fragments_match_the_actual_retained_migration_baseline()
    {
        var sql = Operations(new TDC0502ReceiptInspectionClosure()).OfType<SqlOperation>().Select(item => item.Sql.Replace("\r", "")).ToArray();
        var caseGuard = sql.Single(item => item.Contains("CREATE OR ALTER TRIGGER [dbo].[TR_ProcurementReceiptInspectionCases_TDC0502Protected]"));
        caseGuard.Should().Contain(Constant("OldAcceptance"));
        var receiptGuard = sql.Single(item => item.Contains("CREATE OR ALTER TRIGGER [dbo].[TR_PurchaseOrderReceiptItems_TDC0502AcceptanceProtected]"));
        receiptGuard.Should().Contain(Constant("OldReceiptAcceptance"));
        var rebindSql = Operations(new INVREQFU004AllowDraftInspectionWorkflowRebind()).OfType<SqlOperation>().Single().Sql;
        var actualRebind = Regex.Match(rebindSql, @"DECLARE @new nvarchar\(max\) = N'((?:''|[^'])*)';")
            .Groups[1].Value.Replace("''", "'").Replace("\r", "");
        actualRebind.Should().Be(Constant("OldRebind"));
    }

    [Theory]
    [InlineData("Up")]
    [InlineData("Down")]
    public void Generated_windows_sql_normalizes_patch_literals_at_execution_time(string direction)
    {
        // No connection is opened; a small empty context supplies the real SQL
        // generator without rebuilding the full application model.
        using var context = new DbContext(new DbContextOptionsBuilder()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=NeverConnected;Integrated Security=True")
            .Options);
        var patches = Operations(new ReceiptInspectionOptionalWorkflow(), direction)
            .OfType<SqlOperation>().Where(operation => operation.Sql.Contains("DECLARE @before"))
            .Cast<MigrationOperation>().ToArray();
        var generated = context.GetService<IMigrationsSqlGenerator>().Generate(patches)
            .Select(command => command.CommandText.Replace("\r", "").Replace("\n", "\r\n")).ToArray();
        generated.Should().NotBeEmpty();
        foreach (var command in generated)
        {
            command.Should().Contain("DECLARE @before nvarchar(max)=REPLACE(N'");
            command.Should().Contain("SET @body=REPLACE(@body,@before,REPLACE(N'");
            var literal = Regex.Match(command,
                @"DECLARE @before nvarchar\(max\)=REPLACE\(N'((?:''|[^'])*)',CHAR\(13\),N''\);");
            literal.Success.Should().BeTrue("both the before and replacement fragments must normalize CRLF in SQL");
            var normalizedBefore = literal.Groups[1].Value.Replace("''", "'").Replace("\r", "");
            if (normalizedBefore.Contains("TDC0502_RECEIPT_INSPECTION_WORKFLOW_ID"))
                normalizedBefore.Should().Be(Constant(direction == "Up" ? "OldRebind" : "NewRebind"));
        }
    }

    [Fact]
    public void Direct_completion_keeps_exact_receipt_line_quantities_tenant_and_current_policy_guards()
    {
        var acceptance = Constant("NewReceiptAcceptance");
        acceptance.Should().Contain("c.Id IS NULL OR l.Id IS NULL");
        acceptance.Should().Contain("c.Status <> 1");
        acceptance.Should().Contain("i.AcceptedQuantity <> l.AcceptedQuantity");
        acceptance.Should().Contain("i.RejectedQuantity <> l.RejectedQuantity");
        acceptance.Should().Contain("c.ApprovalRequired = 1 AND (wi.Id IS NULL OR wi.Status <> 2");
        acceptance.Should().Contain("wi.EntityId <> c.Id");
        acceptance.Should().Contain("wi.WorkflowDefinitionId <> c.WorkflowDefinitionId");
        acceptance.Should().Contain("c.ApprovalRequired = 0");
        acceptance.Should().Contain("c.DecidedByUserId IS NOT NULL");
        acceptance.Should().Contain("dbo.WorkflowApprovalRequiredAtSubmission(c.TenantId,N'PROCUREMENT_RECEIPT_INSPECTION',c.Id)<>0");
    }

    [Fact]
    public void Required_approval_still_requires_independent_human_and_completed_exact_workflow()
    {
        var acceptance = Constant("NewAcceptance");
        acceptance.Should().Contain("i.ApprovalRequired = 1 AND");
        acceptance.Should().Contain("wi.Id IS NULL OR wi.Status <> 2");
        acceptance.Should().Contain("i.DecidedByUserId IS NULL");
        acceptance.Should().Contain("i.DecidedByUserId = i.SubmittedByUserId");
        acceptance.Should().Contain("i.DecidedByUserId = i.CreatedByUserId");
        acceptance.Should().Contain("ISNULL(TRY_CONVERT(uniqueidentifier, SESSION_CONTEXT");
    }

    [Fact]
    public void Direct_mode_cannot_masquerade_as_human_approval_or_drop_an_inflight_workflow()
    {
        var mode = Constant("ModeGuard");
        mode.Should().Contain("i.WorkflowDefinitionId IS NOT NULL OR i.WorkflowInstanceId IS NOT NULL");
        mode.Should().Contain("i.DecidedByUserId IS NOT NULL OR i.DecidedAtUtc IS NOT NULL");
        mode.Should().Contain("i.SubmittedByUserId IS NULL OR i.SubmittedAtUtc IS NULL");
        mode.Should().Contain("WorkflowApprovalRequiredAtSubmission");
        var lineage = Constant("NewRebind");
        lineage.Should().Contain("d.Status = 0 AND i.Status = 1");
        lineage.Should().Contain("d.WorkflowInstanceId IS NULL AND i.WorkflowInstanceId IS NULL");
        lineage.Should().Contain("i.ApprovalRequired <> d.ApprovalRequired");
        lineage.Should().Contain("TDC0502_RECEIPT_INSPECTION_WORKFLOW_ID");
        lineage.Should().Contain("WorkflowApprovalRequiredAtSubmission");
        var completion = Constant("CompletionIdentityGuard");
        completion.Should().Contain("action.ActionType=3 AND c.ApprovalRequired=0");
        completion.Should().Contain("action.ActorUserId<>c.SubmittedByUserId");
        var sql = string.Join("\n", Operations(new ReceiptInspectionOptionalWorkflow()).OfType<SqlOperation>().Select(item => item.Sql));
        sql.Should().Contain("action.ActionType IN (3, 11, 12, 15)");
        sql.Should().Contain("RCV_OPTIONAL_APPROVAL_TRIGGER_DRIFT");
        sql.Should().NotContain("DISABLE TRIGGER");
        sql.Should().NotContain("DROP TRIGGER");
    }

    [Fact]
    public void Downgrade_refuses_to_fabricate_definitions_or_destroy_direct_history()
    {
        var operations = Operations(new ReceiptInspectionOptionalWorkflow(), "Down");
        operations[0].Should().BeOfType<SqlOperation>().Which.Sql.Should()
            .Contain("WHERE ApprovalRequired=0 OR WorkflowDefinitionId IS NULL")
            .And.Contain("WHERE ActionType=15")
            .And.Contain("THROW 51983");
    }

    private static string Constant(string name) => ((string)typeof(ReceiptInspectionOptionalWorkflow)
        .GetField(name, BindingFlags.Static | BindingFlags.NonPublic)!.GetRawConstantValue()!).Replace("\r", "");

    private static IReadOnlyList<MigrationOperation> Operations(Migration migration, string direction = "Up")
    {
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        migration.GetType().GetMethod(direction, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(migration, [builder]);
        return builder.Operations;
    }
}
