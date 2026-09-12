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
    public async Task ExplicitDefinitionSubmissionRemainsGovernedWhenApprovalIsActive()
    {
        var entityId = Guid.NewGuid();
        var definitionId = Guid.NewGuid();
        var workflow = new Mock<IWorkflowService>();
        workflow.Setup(value => value.HasActiveApprovalWorkflowAsync("EmergencyProcurement"))
            .ReturnsAsync(true);
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
            Times.Once);
    }

    [Fact]
    public async Task SelectedDefinitionDoesNotRequireApprovalWhenNoProcessIsActive()
    {
        var workflow = new Mock<IWorkflowService>();
        var service = new WorkflowIntegrationService(workflow.Object, NullLogger<WorkflowIntegrationService>.Instance);
        var result = await service.SubmitAsync("StockAdjustment", Guid.NewGuid(), Guid.NewGuid());

        result.ApprovalRequired.Should().BeFalse();
        result.Outcome.Should().Be(WorkflowOutcome.Approved);
        result.ExecutionResult.WorkflowInstanceId.Should().BeNull();
        workflow.Verify(value => value.StartApprovalWorkflowAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task InFlightApprovalIsNotBypassedWhenDefinitionHasBeenDeactivated()
    {
        var entityId = Guid.NewGuid();
        var instanceId = Guid.NewGuid();
        var workflow = new Mock<IWorkflowService>();
        workflow.Setup(value => value.HasActiveApprovalInstanceAsync("StockAdjustment", entityId)).ReturnsAsync(true);
        workflow.Setup(value => value.StartApprovalWorkflowAsync("StockAdjustment", entityId))
            .ReturnsAsync(new WorkflowExecutionResult { Success = true, Status = WorkflowInstanceStatus.InProgress, WorkflowInstanceId = instanceId });
        var service = new WorkflowIntegrationService(workflow.Object, NullLogger<WorkflowIntegrationService>.Instance);

        var result = await service.SubmitAsync("StockAdjustment", entityId);

        result.ApprovalRequired.Should().BeTrue();
        result.Outcome.Should().Be(WorkflowOutcome.Pending);
        result.ExecutionResult.WorkflowInstanceId.Should().Be(instanceId);
    }

    [Fact]
    public async Task ConfigurationLookupFailureDoesNotBecomeAnApprovalBypass()
    {
        var workflow = new Mock<IWorkflowService>();
        workflow.Setup(value => value.HasActiveApprovalWorkflowAsync("StockAdjustment"))
            .ThrowsAsync(new InvalidOperationException("Configuration unavailable"));
        var service = new WorkflowIntegrationService(workflow.Object, NullLogger<WorkflowIntegrationService>.Instance);

        var act = () => service.SubmitAsync("StockAdjustment", Guid.NewGuid());

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Configuration unavailable");
        workflow.Verify(value => value.StartApprovalWorkflowAsync(It.IsAny<string>(), It.IsAny<Guid>()), Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task EmptyEntityTypeCannotResolveToDirectCompletion(string entityType)
    {
        var service = new WorkflowIntegrationService(Mock.Of<IWorkflowService>(), NullLogger<WorkflowIntegrationService>.Instance);
        var act = () => service.SubmitAsync(entityType, Guid.NewGuid());
        await act.Should().ThrowAsync<ArgumentException>();
    }
}
