using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Core.Services.Workflow;
using ErpSystem.Data.Migrations;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementOptionalApprovalTests
{
    [Fact]
    public void LegacyEntitiesDefaultToApprovalRequired()
    {
        new PurchaseOrder().ApprovalRequired.Should().BeTrue();
        new PurchaseRequisition().ApprovalRequired.Should().BeTrue();
        new ProcurementBudget().ApprovalRequired.Should().BeTrue();
        new ProcurementPlan().ApprovalRequired.Should().BeTrue();
        new ProcurementBudgetRevision().ApprovalRequired.Should().BeTrue();
    }

    [Theory]
    [InlineData("PurchaseOrder")]
    [InlineData("PurchaseRequisition")]
    [InlineData("ProcurementPlan")]
    [InlineData("ProcurementBudget")]
    [InlineData("ProcurementBudgetRevision")]
    public void DirectCompletionKeepsBusinessReadyStatusWithoutHumanApproval(string entityType)
    {
        (object Entity, IWorkflowStatusAdapter Adapter) pair = entityType switch
        {
            "PurchaseOrder" => (new PurchaseOrder(), new PurchaseOrderWorkflowStatusAdapter()),
            "PurchaseRequisition" => (new PurchaseRequisition(), new PurchaseRequisitionWorkflowStatusAdapter()),
            "ProcurementPlan" => (new ProcurementPlan(), new ProcurementPlanWorkflowStatusAdapter()),
            "ProcurementBudget" => (new ProcurementBudget(), new ProcurementBudgetWorkflowStatusAdapter()),
            _ => (new ProcurementBudgetRevision(), new ProcurementBudgetRevisionWorkflowStatusAdapter())
        };
        pair.Adapter.ApplySubmitOutcome(pair.Entity, new WorkflowIntegrationResult(
            new WorkflowExecutionResult { Success = true, Status = WorkflowInstanceStatus.Completed },
            WorkflowOutcome.Approved, approvalRequired: false), Guid.NewGuid());

        pair.Entity.GetType().GetProperty("Status")!.GetValue(pair.Entity).Should().Be("Approved");
        pair.Entity.GetType().GetProperty("ApprovedById")!.GetValue(pair.Entity).Should().BeNull();
        var date = pair.Entity.GetType().GetProperty("ApprovedAt") ?? pair.Entity.GetType().GetProperty("ApprovedDate");
        date!.GetValue(pair.Entity).Should().BeNull();
    }

    [Fact]
    public void DirectBudgetIsUsableWithoutInventedApprovalLineage()
    {
        ProcurementPurchaseOrderComplianceRules.ValidateCommitment(DirectBudget())
            .IsValid.Should().BeTrue();
    }

    [Fact]
    public void ControlledBudgetStillRequiresItsHumanApprovalLineage()
    {
        ProcurementPurchaseOrderComplianceRules.ValidateCommitment(DirectBudget() with { BudgetApprovalRequired = true })
            .Code.Should().Be("PO_BUDGET_APPROVAL_INCOMPLETE");
    }

    [Theory]
    [InlineData("tenant", "PO_BUDGET_COMMITMENT_TENANT_MISMATCH")]
    [InlineData("lineage", "PO_BUDGET_COMMITMENT_LINEAGE_MISMATCH")]
    [InlineData("status", "PO_BUDGET_NOT_APPROVED")]
    [InlineData("currency", "PO_BUDGET_COMMITMENT_CURRENCY_MISMATCH")]
    [InlineData("amount", "PO_BUDGET_COMMITMENT_INSUFFICIENT")]
    [InlineData("ledger", "PO_BUDGET_RESERVATION_LEDGER_MISMATCH")]
    [InlineData("expired", "PO_BUDGET_EXPIRED")]
    [InlineData("future", "PO_BUDGET_NOT_EFFECTIVE")]
    public void OptionalApprovalDoesNotRelaxBudgetAndSourceControls(string drift, string code)
    {
        var source = DirectBudget();
        source = drift switch
        {
            "tenant" => source with { BudgetTenantId = Guid.NewGuid() },
            "lineage" => source with { ReleaseRequisitionId = Guid.NewGuid() },
            "status" => source with { BudgetStatus = "Draft" },
            "currency" => source with { BudgetCurrency = "USD" },
            "amount" => source with { RequiredExposure = 1001m },
            "ledger" => source with { BudgetReservedAmount = 999m },
            "expired" => source with { BudgetEffectiveToUtc = source.AtUtc.AddDays(-1) },
            _ => source with { BudgetEffectiveFromUtc = source.AtUtc.AddDays(1) }
        };
        var result = ProcurementPurchaseOrderComplianceRules.ValidateCommitment(source);
        result.IsValid.Should().BeFalse();
        result.Code.Should().Be(code);
    }

    [Fact]
    public void MigrationPreservesLegacyDefaultsAndUsesCentralPolicyGuardForEveryEntity()
    {
        var operations = new AllowOptionalProcurementApproval().UpOperations;
        var columns = operations.OfType<AddColumnOperation>().ToList();
        columns.Should().HaveCount(5);
        columns.Should().OnlyContain(column => column.Name == "ApprovalRequired" &&
            !column.IsNullable && Equals(column.DefaultValue, true));
        var guards = operations.OfType<SqlOperation>()
            .Where(operation => operation.Sql.Contains("CREATE OR ALTER TRIGGER")).ToList();
        guards.Should().HaveCount(5);
        foreach (var guard in guards)
        {
            guard.Sql.Should().Contain("dbo.WorkflowApprovalRequiredAtSubmission(i.TenantId,")
                .And.Contain("d.ApprovalRequired = 1 AND i.ApprovalRequired = 0")
                .And.Contain("d.ApprovedById IS NULL AND d.[")
                .And.Contain("PROCUREMENT_APPROVAL_POLICY_IMMUTABLE")
                .And.Contain("PROCUREMENT_DIRECT_APPROVAL_METADATA")
                .And.Contain("PROCUREMENT_APPROVAL_STILL_REQUIRED");
            guard.Sql.Should().NotContain("DISABLE TRIGGER");
        }
        string.Join('\n', operations.OfType<SqlOperation>().Select(operation => operation.Sql))
            .Should().Contain("PROCUREMENT_APPROVAL_TRIGGER_BASELINE")
            .And.Contain("b.ApprovalRequired = 1")
            .And.Contain("pr.[ApprovalRequired] = 1")
            .And.Contain("purchaseOrder.ApprovalRequired = 1");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ControlledRecommendationMigrationsRejectInventedDirectApprovalHistory(bool tender)
    {
        var operations = tender
            ? new OptionalControlledTenderApproval().UpOperations
            : new OptionalRfqEvaluationApproval().UpOperations;
        var column = operations.OfType<AddColumnOperation>().Should().ContainSingle().Subject;
        column.Name.Should().Be("ApprovalRequired");
        column.DefaultValue.Should().Be(true);
        var guards = operations.OfType<SqlOperation>().Where(operation => operation.Sql.Contains("CREATE OR ALTER TRIGGER")).ToList();
        var sourceGuard = guards.Single(operation => operation.Sql.Contains("WorkflowApprovalRequiredAtSubmission"));
        sourceGuard.Sql.Should().Contain("d.WorkflowInstanceId IS NULL")
            .And.Contain("d.ApprovedAtUtc IS NULL")
            .And.Contain("OPENJSON(d.ApprovalActorsJson)");
        var readiness = guards.Single(operation => operation.Sql.Contains("READINESS_APPROVAL_POLICY"));
        readiness.Sql.Should().Contain("$.approvalRequired")
            .And.Contain("JSON_VALUE(i.AuthorityLineageJson, '$.approvalReference') IS NOT NULL")
            .And.Contain("JSON_VALUE(i.AuthorityLineageJson, '$.workflowStatus') IS NOT NULL")
            .And.Contain("JSON_QUERY(i.AuthorityLineageJson, '$.approvalActorUserIds') IS NULL")
            .And.Contain("EXISTS (SELECT 1 FROM OPENJSON(i.AuthorityLineageJson, '$.approvalActorUserIds'))")
            .And.Contain("$.approvedByUserId")
            .And.Contain("$.approvedAtUtc");
        readiness.Sql.Should().NotContain("DISABLE TRIGGER");
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void GeneratedRecommendationMigrationSqlNormalizesBothRuntimePatchOperands(bool tender, bool down)
    {
        // Use the actual SQL Server generator without opening a connection or
        // building the large application model. Windows rewrites embedded LFs.
        using var context = new DbContext(new DbContextOptionsBuilder()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=NeverConnected;Integrated Security=True")
            .Options);
        Migration migration = tender ? new OptionalControlledTenderApproval() : new OptionalRfqEvaluationApproval();
        var operations = down ? migration.DownOperations : migration.UpOperations;
        var patches = operations.OfType<SqlOperation>()
            .Where(operation => operation.Sql.Contains("DECLARE @before"))
            .ToArray();
        patches.Should().NotBeEmpty();
        var generated = context.GetService<IMigrationsSqlGenerator>()
            .Generate(patches.Cast<MigrationOperation>().ToArray())
            .Select(command => command.CommandText.Replace("\r", "").Replace("\n", "\r\n"))
            .ToArray();
        generated.Should().HaveCount(patches.Length);
        static string Operand(string sql, string name)
        {
            var match = Regex.Match(sql,
                $@"DECLARE @{name} nvarchar\(max\) = REPLACE\(N'((?:''|[^'])*)', CHAR\(13\), N''\);");
            match.Success.Should().BeTrue($"the {name} operand must normalize CRLF at SQL execution time");
            return match.Groups[1].Value.Replace("''", "'");
        }
        for (var index = 0; index < generated.Length; index++)
        {
            generated[index].Should().Contain("CHARINDEX(@before, @definition)")
                .And.Contain("REPLACE(@definition, @before, @after)");
            foreach (var name in new[] { "before", "after" })
                Operand(generated[index], name).Replace("\r", "")
                    .Should().Be(Operand(patches[index].Sql, name).Replace("\r", ""));
        }
        generated.Select(sql => Operand(sql, "before")).Should().Contain(operand => operand.Contains("\r\n"),
            "this regression must include an actual multiline operand after Windows script generation");
    }

    private static ProcurementCommitmentLifecycleSnapshot DirectBudget()
    {
        var tenant = Guid.NewGuid();
        var requisition = Guid.NewGuid();
        var commitment = Guid.NewGuid();
        var budget = Guid.NewGuid();
        var at = DateTime.UtcNow;
        return new ProcurementCommitmentLifecycleSnapshot(
            tenant, requisition, tenant, "GHS", budget, tenant, requisition,
            commitment, "BCR-001", commitment, tenant, requisition, budget,
            "BCR-001", ProcurementBudgetCommitmentStatus.Reserved, 1000m, 0m,
            "GHS", budget, tenant, "Active", "GHS", 0m, 1000m, null, null,
            at.AddDays(-10), at.AddDays(10), 750m, "GHS", at, BudgetApprovalRequired: false);
    }
}
