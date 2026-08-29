using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Services.Workflow;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Workflow;

public sealed class WorkflowIntegrationOptionalApprovalTests
{
    [Fact]
    public async Task SubmitWithoutActiveWorkflowUsesDirectApprovedLifecycle()
    {
        var workflow = new Mock<IWorkflowService>();
        workflow.Setup(value => value.HasActiveApprovalWorkflowAsync("MarketAnalysis"))
            .ReturnsAsync(false);
        var service = new WorkflowIntegrationService(
            workflow.Object,
            NullLogger<WorkflowIntegrationService>.Instance);

        var result = await service.SubmitAsync("MarketAnalysis", Guid.NewGuid());

        result.ExecutionResult.Success.Should().BeTrue();
        result.ExecutionResult.Status.Should().Be(WorkflowInstanceStatus.Completed);
        result.ExecutionResult.WorkflowInstanceId.Should().BeNull();
        result.Outcome.Should().Be(WorkflowOutcome.Approved);
        result.ApprovalRequired.Should().BeFalse();
        workflow.Verify(
            value => value.StartApprovalWorkflowAsync(It.IsAny<string>(), It.IsAny<Guid>()),
            Times.Never);
    }

    [Fact]
    public async Task SubmitWithActiveWorkflowStartsConfiguredApproval()
    {
        var entityId = Guid.NewGuid();
        var workflow = new Mock<IWorkflowService>();
        workflow.Setup(value => value.HasActiveApprovalWorkflowAsync("ProcurementBudget"))
            .ReturnsAsync(true);
        workflow.Setup(value => value.StartApprovalWorkflowAsync("ProcurementBudget", entityId))
            .ReturnsAsync(new WorkflowExecutionResult
            {
                Success = true,
                Status = WorkflowInstanceStatus.InProgress,
                WorkflowInstanceId = Guid.NewGuid()
            });
        var service = new WorkflowIntegrationService(
            workflow.Object,
            NullLogger<WorkflowIntegrationService>.Instance);

        var result = await service.SubmitAsync("ProcurementBudget", entityId);

        result.ApprovalRequired.Should().BeTrue();
        result.Outcome.Should().Be(WorkflowOutcome.Pending);
        workflow.Verify(value => value.StartApprovalWorkflowAsync("ProcurementBudget", entityId), Times.Once);
    }

    [Fact]
    public async Task ExplicitDefinitionSubmissionAlwaysRemainsGoverned()
    {
        var entityId = Guid.NewGuid();
        var definitionId = Guid.NewGuid();
        var workflow = new Mock<IWorkflowService>();
        workflow.Setup(value => value.StartApprovalWorkflowAsync("EmergencyProcurement", entityId, definitionId))
            .ReturnsAsync(new WorkflowExecutionResult
            {
                Success = true,
                Status = WorkflowInstanceStatus.InProgress,
                WorkflowInstanceId = Guid.NewGuid()
            });
        var service = new WorkflowIntegrationService(
            workflow.Object,
            NullLogger<WorkflowIntegrationService>.Instance);

        var result = await service.SubmitAsync("EmergencyProcurement", entityId, definitionId);

        result.ApprovalRequired.Should().BeTrue();
        workflow.Verify(
            value => value.StartApprovalWorkflowAsync("EmergencyProcurement", entityId, definitionId),
            Times.Once);
        workflow.Verify(
            value => value.HasActiveApprovalWorkflowAsync(It.IsAny<string>()),
            Times.Never);
    }
}
