using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Services.Inventory;
using ErpSystem.Data;
using ErpSystem.Data.Migrations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Inventory;

public sealed class InventoryOptionalApprovalTests
{
    [Fact]
    public void Existing_entity_defaults_preserve_approval_obligations()
    {
        new StockAdjustment().ApprovalRequired.Should().BeTrue();
        new PhysicalCount().ApprovalRequired.Should().BeTrue();
        new InventoryReturnVoucher().ApprovalRequired.Should().BeTrue();
    }

    [Fact]
    public void No_workflow_marks_adjustment_ready_without_fabricating_human_approval()
    {
        var adjustment = new StockAdjustment();
        InventoryOptionalApprovalPolicy.ApplySubmission(adjustment, NoWorkflow());
        adjustment.ApprovalRequired.Should().BeFalse();
        adjustment.Status.Should().Be("ReadyToPost");
        adjustment.WorkflowInstanceId.Should().BeNull();
        adjustment.ApprovedById.Should().BeNull();
        adjustment.ApprovedAt.Should().BeNull();
        InventoryOptionalApprovalPolicy.CanPostState(adjustment).Should().BeTrue();
    }

    [Fact]
    public void No_workflow_marks_return_ready_without_fabricating_human_approval()
    {
        var voucher = new InventoryReturnVoucher();
        InventoryOptionalApprovalPolicy.ApplySubmission(voucher, NoWorkflow());
        voucher.ApprovalRequired.Should().BeFalse();
        voucher.Status.Should().Be(InventoryReturnVoucherStatus.ReadyToPost);
        voucher.WorkflowInstanceId.Should().BeNull();
        voucher.ApprovedById.Should().BeNull();
        voucher.ApprovedAtUtc.Should().BeNull();
        InventoryOptionalApprovalPolicy.CanPostState(voucher).Should().BeTrue();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Active_workflow_keeps_pending_state_and_exact_workflow_instance(bool isAdjustment)
    {
        var workflowId = Guid.NewGuid();
        var result = new WorkflowIntegrationResult(new WorkflowExecutionResult
        {
            Success = true, WorkflowInstanceId = workflowId, Status = WorkflowInstanceStatus.InProgress
        }, WorkflowOutcome.Pending);
        if (isAdjustment)
        {
            var adjustment = new StockAdjustment();
            InventoryOptionalApprovalPolicy.ApplySubmission(adjustment, result);
            adjustment.ApprovalRequired.Should().BeTrue();
            adjustment.Status.Should().Be("PendingApproval");
            adjustment.WorkflowInstanceId.Should().Be(workflowId);
            InventoryOptionalApprovalPolicy.CanPostState(adjustment).Should().BeFalse();
        }
        else
        {
            var voucher = new InventoryReturnVoucher();
            InventoryOptionalApprovalPolicy.ApplySubmission(voucher, result);
            voucher.ApprovalRequired.Should().BeTrue();
            voucher.Status.Should().Be(InventoryReturnVoucherStatus.PendingApproval);
            voucher.WorkflowInstanceId.Should().Be(workflowId);
            InventoryOptionalApprovalPolicy.CanPostState(voucher).Should().BeFalse();
        }
    }

    [Theory]
    [InlineData(false, true, WorkflowOutcome.Pending, true)]
    [InlineData(true, true, WorkflowOutcome.Approved, true)]
    [InlineData(true, true, WorkflowOutcome.Pending, false)]
    [InlineData(true, false, WorkflowOutcome.Pending, false)]
    [InlineData(true, false, WorkflowOutcome.Rejected, false)]
    [InlineData(true, false, WorkflowOutcome.Approved, true)]
    public void Failed_or_inconsistent_decisions_cannot_mutate_either_record(
        bool success, bool required, WorkflowOutcome outcome, bool hasInstance)
    {
        var result = new WorkflowIntegrationResult(new WorkflowExecutionResult
        {
            Success = success, WorkflowInstanceId = hasInstance ? Guid.NewGuid() : null
        }, outcome, required);
        var adjustment = new StockAdjustment();
        var voucher = new InventoryReturnVoucher();
        Action applyAdjustment = () => InventoryOptionalApprovalPolicy.ApplySubmission(adjustment, result);
        Action applyReturn = () => InventoryOptionalApprovalPolicy.ApplySubmission(voucher, result);
        applyAdjustment.Should().Throw<InvalidOperationException>();
        applyReturn.Should().Throw<InvalidOperationException>();
        adjustment.ApprovalRequired.Should().BeTrue();
        adjustment.Status.Should().Be("Draft");
        voucher.ApprovalRequired.Should().BeTrue();
        voucher.Status.Should().Be(InventoryReturnVoucherStatus.PendingApproval);
    }

    [Theory]
    [InlineData("workflow")]
    [InlineData("approver")]
    [InlineData("approvalDate")]
    [InlineData("wrongStatus")]
    public void Direct_post_rejects_inconsistent_approval_lineage(string mutation)
    {
        var adjustment = new StockAdjustment();
        var voucher = new InventoryReturnVoucher();
        InventoryOptionalApprovalPolicy.ApplySubmission(adjustment, NoWorkflow());
        InventoryOptionalApprovalPolicy.ApplySubmission(voucher, NoWorkflow());
        switch (mutation)
        {
            case "workflow": adjustment.WorkflowInstanceId = voucher.WorkflowInstanceId = Guid.NewGuid(); break;
            case "approver": adjustment.ApprovedById = voucher.ApprovedById = Guid.NewGuid(); break;
            case "approvalDate": adjustment.ApprovedAt = voucher.ApprovedAtUtc = DateTime.UtcNow; break;
            case "wrongStatus": adjustment.Status = "Approved"; voucher.Status = InventoryReturnVoucherStatus.Approved; break;
        }
        InventoryOptionalApprovalPolicy.CanPostState(adjustment).Should().BeFalse();
        InventoryOptionalApprovalPolicy.CanPostState(voucher).Should().BeFalse();
    }

    [Fact]
    public void Existing_approved_states_remain_postable_and_ready_cannot_bypass_required_approval()
    {
        InventoryOptionalApprovalPolicy.CanPostState(new StockAdjustment { Status = "Approved" }).Should().BeTrue();
        InventoryOptionalApprovalPolicy.CanPostState(new InventoryReturnVoucher { Status = InventoryReturnVoucherStatus.Approved }).Should().BeTrue();
        InventoryOptionalApprovalPolicy.CanPostState(new StockAdjustment { Status = "ReadyToPost" }).Should().BeFalse();
        InventoryOptionalApprovalPolicy.CanPostState(new InventoryReturnVoucher { Status = InventoryReturnVoucherStatus.ReadyToPost }).Should().BeFalse();
    }

    [Fact]
    public void Client_mutation_requests_do_not_expose_approval_snapshot()
    {
        foreach (var type in new[] { typeof(CreateStockAdjustmentDto), typeof(StockAdjustmentActionRequest),
                     typeof(ReturnRequisitionDto), typeof(PhysicalCountMutationRequest) })
            type.GetProperty("ApprovalRequired").Should().BeNull($"{type.Name} must not accept a client approval bypass");
    }

    [Fact]
    public void Ef_and_migration_defaults_are_fail_closed_without_rewriting_history()
    {
        using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);
        var model = context.GetService<IDesignTimeModel>().Model;
        foreach (var type in new[] { typeof(PhysicalCount), typeof(StockAdjustment), typeof(InventoryReturnVoucher) })
        {
            var property = model.FindEntityType(type)!.FindProperty("ApprovalRequired")!;
            property.IsNullable.Should().BeFalse();
            property.GetDefaultValue().Should().Be(true);
        }
        var migration = new InventoryOptionalApprovalSnapshots();
        var columns = migration.UpOperations.OfType<AddColumnOperation>().ToArray();
        columns.Should().HaveCount(3);
        columns.Should().OnlyContain(column => column.Name == "ApprovalRequired" && !column.IsNullable && Equals(column.DefaultValue, true));
        var sql = string.Join("\n", migration.UpOperations.OfType<SqlOperation>().Select(operation => operation.Sql));
        sql.Should().Contain("dbo.WorkflowApprovalRequiredAtSubmission");
        sql.Should().Contain("INV_APPROVAL_SNAPSHOT_INVALID");
        sql.Should().Contain("INV_RETURN_APPROVAL_SNAPSHOT_INVALID");
        sql.Should().Contain("INV_COUNT_APPROVAL_SNAPSHOT_INVALID");
        sql.Should().Contain("i.ProcessedById=a.PostedById");
        sql.Should().NotContain("DISABLE TRIGGER");
        sql.Should().NotContain("UPDATE dbo.PhysicalCounts");
    }

    private static WorkflowIntegrationResult NoWorkflow() => new(new WorkflowExecutionResult
    {
        Success = true, Status = WorkflowInstanceStatus.Completed
    }, WorkflowOutcome.Approved, approvalRequired: false);
}
