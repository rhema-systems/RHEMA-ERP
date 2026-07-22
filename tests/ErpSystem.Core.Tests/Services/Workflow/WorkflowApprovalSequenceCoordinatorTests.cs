using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.Workflow;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Workflow;

public class WorkflowApprovalSequenceCoordinatorTests
{
    [Fact]
    public void Sequential_mode_only_activates_first_group()
    {
        Assert.Equal(
            WorkflowApprovalStatus.Pending,
            WorkflowApprovalSequenceCoordinator.GetInitialStatus(
                WorkflowApprovalActivationMode.Sequential,
                approvalGroup: 1,
                firstGroup: 1));
        Assert.Equal(
            WorkflowApprovalStatus.Queued,
            WorkflowApprovalSequenceCoordinator.GetInitialStatus(
                WorkflowApprovalActivationMode.Sequential,
                approvalGroup: 2,
                firstGroup: 1));
    }

    [Fact]
    public void Parallel_mode_activates_every_group()
    {
        Assert.Equal(
            WorkflowApprovalStatus.Pending,
            WorkflowApprovalSequenceCoordinator.GetInitialStatus(
                WorkflowApprovalActivationMode.Parallel,
                approvalGroup: 3,
                firstGroup: 1));
    }

    [Fact]
    public void Next_group_is_lowest_queued_group()
    {
        var approvals = new[]
        {
            Approval(3, WorkflowApprovalStatus.Queued),
            Approval(1, WorkflowApprovalStatus.Approved),
            Approval(2, WorkflowApprovalStatus.Queued),
        };

        Assert.Equal(2, WorkflowApprovalSequenceCoordinator.GetNextQueuedGroup(approvals));
    }

    [Fact]
    public void Every_sequential_group_must_be_satisfied()
    {
        var approvals = new[]
        {
            Approval(1, WorkflowApprovalStatus.Approved),
            Approval(2, WorkflowApprovalStatus.Pending),
        };

        Assert.False(WorkflowApprovalSequenceCoordinator.AreAllSequentialGroupsSatisfied(
            WorkflowApprovalType.Single,
            minApprovalsRequired: 1,
            approvals));

        approvals[1].Status = WorkflowApprovalStatus.Approved;

        Assert.True(WorkflowApprovalSequenceCoordinator.AreAllSequentialGroupsSatisfied(
            WorkflowApprovalType.Single,
            minApprovalsRequired: 1,
            approvals));
    }

    private static WorkflowApproval Approval(int group, WorkflowApprovalStatus status) => new()
    {
        Id = Guid.NewGuid(),
        ApprovalGroup = group,
        Status = status,
    };
}
