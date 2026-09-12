using System.Reflection;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementExceptionalPrequalificationOptionalApprovalTests
{
    [Fact]
    public void MigrationAddsOnlyServerCapturedApprovalModeAndNullableSelectedDefinitions()
    {
        var operations = Operations();
        operations.OfType<AddColumnOperation>().Should().HaveCount(2).And.OnlyContain(column =>
            column.Name == "ApprovalRequired" && column.ClrType == typeof(bool) &&
            !column.IsNullable && Equals(column.DefaultValue, true));
        operations.OfType<AlterColumnOperation>().Should().HaveCount(2).And.OnlyContain(column =>
            column.Name == "WorkflowDefinitionId" && column.IsNullable && !column.OldColumn.IsNullable);
        operations.OfType<DropColumnOperation>().Should().BeEmpty();
    }

    [Fact]
    public void DirectGuardsPreserveTenantInstanceHistoryEvidenceAndFirstCompletionBoundaries()
    {
        var sql = string.Join("\n", Operations().OfType<SqlOperation>().Select(operation => operation.Sql));
        sql.Should().Contain("dbo.WorkflowApprovalRequiredAtSubmission(i.TenantId,N'TenderException',i.TenderId)=0")
            .And.Contain("dbo.WorkflowApprovalRequiredAtSubmission(i.TenantId,N'ProcurementSourcing',i.Id)=0")
            .And.Contain("d.Status=0 AND i.Status=2")
            .And.Contain("d.Status=3 AND i.Status=5")
            .And.Contain("d.ApprovedAtUtc IS NULL AND d.ApprovedById IS NULL")
            .And.Contain("d.DecidedAtUtc IS NULL AND d.DecidedById IS NULL")
            .And.Contain("d.WorkflowInstanceId IS NULL")
            .And.Contain("i.DecidedById<>i.SubmittedForApprovalById")
            .And.Contain("c.TenderId=i.SourceId AND c.TenantId=i.TenantId AND c.IsDeleted=0")
            .And.Contain("'$.approvalReference'")
            .And.Contain("'$.approvalActorUserIds'");
        sql.Should().NotContain("DISABLE TRIGGER").And.NotContain("NOCHECK")
            .And.NotContain("UPDATE dbo.ProcurementExceptionalSourcingControls SET ApprovedById");
        // Only the internal workflow/approver fragments are patched. The installed
        // check body, including Board/MD/PPA and signed decision evidence, is retained.
        sql.Should().Contain("FROM sys.check_constraints")
            .And.Contain("WITH CHECK ADD CONSTRAINT CK_ProcurementExceptionalSourcingControls_Lifecycle")
            .And.Contain("WITH CHECK ADD CONSTRAINT CK_ProcurementPrequalificationExercises_Evidence")
            .And.Contain("CHARINDEX(@before,@definition)+LEN(@before)");
    }

    private static IReadOnlyList<MigrationOperation> Operations()
    {
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        typeof(OptionalExceptionalAndPrequalificationApproval)
            .GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(new OptionalExceptionalAndPrequalificationApproval(), [builder]);
        return builder.Operations;
    }
}
