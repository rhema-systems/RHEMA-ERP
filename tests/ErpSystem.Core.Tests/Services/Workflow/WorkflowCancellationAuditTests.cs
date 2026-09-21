using System.Text.Json;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Repositories;
using ErpSystem.Core.Interfaces.Services;
using ErpSystem.Core.Interfaces.Workflow;
using ErpSystem.Core.Services.Workflow;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Workflow;

public class WorkflowCancellationAuditTests
{
    [Fact]
    public async Task StartWorkflow_ActorEligibilityFailureLeavesNoWorkflowRecords()
    {
        var tenantId = Guid.NewGuid();
        var initiatorId = Guid.NewGuid();
        var definitionId = Guid.NewGuid();
        var definition = new WorkflowDefinition
        {
            Id = definitionId,
            TenantId = tenantId,
            Name = "Journal Entry Approval",
            EntityTypeId = Guid.NewGuid(),
            LifecycleStatus = WorkflowDefinitionLifecycleStatus.Published,
            IsActive = true
        };
        var startStep = new WorkflowStep
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WorkflowDefinitionId = definitionId,
            Name = "Draft",
            StepType = WorkflowStepType.Manual,
            IsStartStep = true
        };
        var finalStep = new WorkflowStep
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WorkflowDefinitionId = definitionId,
            Name = "Financial Controller Final Approval",
            StepType = WorkflowStepType.Approval,
            IsRequired = true
        };

        var definitions = new Mock<IWorkflowDefinitionRepository>();
        definitions.Setup(repository => repository.GetWithDetailsAsync(definitionId, default))
            .ReturnsAsync(definition);
        var workflowSteps = new Mock<IWorkflowStepRepository>();
        workflowSteps.Setup(repository => repository.GetStartStepAsync(definitionId, default))
            .ReturnsAsync(startStep);
        workflowSteps.Setup(repository => repository.GetByWorkflowDefinitionAsync(definitionId, default))
            .ReturnsAsync([startStep, finalStep]);
        var instances = new Mock<IWorkflowInstanceRepository>();
        var governance = new Mock<IWorkflowRuntimeGovernanceService>();
        governance.Setup(service => service.EnsureMandatoryApprovalActorsAvailableAsync(
                tenantId,
                initiatorId,
                definition.Name,
                It.Is<IReadOnlyCollection<WorkflowStep>>(steps => steps.Contains(finalStep)),
                default))
            .ThrowsAsync(new InvalidOperationException("No independent final approver."));
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(service => service.TenantId).Returns(tenantId);

        var engine = new WorkflowEngine(
            definitions.Object,
            workflowSteps.Object,
            Mock.Of<IWorkflowTransitionRepository>(),
            instances.Object,
            Mock.Of<IWorkflowStepInstanceRepository>(),
            Mock.Of<IWorkflowApprovalRepository>(),
            Mock.Of<IWorkflowActivityService>(),
            Mock.Of<IWorkflowConditionEvaluator>(),
            Mock.Of<IWorkflowNotificationService>(),
            Mock.Of<IWorkflowApprovalPolicyResolver>(),
            governance.Object,
            Mock.Of<IWorkflowSignatureSubmissionStore>(),
            currentUser.Object,
            Mock.Of<ILogger<WorkflowEngine>>());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.StartWorkflowAsync(definitionId, Guid.NewGuid(), initiatorId));

        Assert.Equal("No independent final approver.", exception.Message);
        instances.Verify(repository => repository.AddAsync(It.IsAny<WorkflowInstance>()), Times.Never);
        instances.Verify(repository => repository.SaveChangesAsync(), Times.Never);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task SingleApproval_ExpiresUnusedSiblingAndCannotSkipConfiguredEndStep(bool hasDeclaredEnd, bool atEnd)
    {
        var tenantId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        var stepId = Guid.NewGuid();
        var stepInstanceId = Guid.NewGuid();
        var approverId = Guid.NewGuid();
        var config = new WorkflowStepConfigurationDto
        {
            ApprovalConfig = new WorkflowApprovalConfigDto
            {
                ApprovalType = WorkflowApprovalType.Single,
                MinApprovalsRequired = 1
            }
        };
        var stepDefinition = new WorkflowStep
        {
            Id = stepId,
            TenantId = tenantId,
            Name = "Accounts Officer Review",
            StepType = WorkflowStepType.Approval,
            IsEndStep = atEnd,
            Configuration = JsonSerializer.Serialize(config)
        };
        var instance = new WorkflowInstance
        {
            Id = workflowId,
            TenantId = tenantId,
            WorkflowDefinitionId = Guid.NewGuid(),
            CurrentStepId = stepId,
            Status = WorkflowInstanceStatus.InProgress
        };
        var step = new WorkflowStepInstance
        {
            Id = stepInstanceId,
            TenantId = tenantId,
            WorkflowInstanceId = workflowId,
            WorkflowStepId = stepId,
            WorkflowStep = stepDefinition,
            Status = WorkflowStepInstanceStatus.Pending
        };
        var selected = new WorkflowApproval
        {
            Id = Guid.NewGuid(), TenantId = tenantId, StepInstanceId = stepInstanceId,
            StepInstance = step, ApproverRole = "Senior Accountant", Status = WorkflowApprovalStatus.Pending
        };
        var sibling = new WorkflowApproval
        {
            Id = Guid.NewGuid(), TenantId = tenantId, StepInstanceId = stepInstanceId,
            StepInstance = step, ApproverRole = "Accounts Officer", Status = WorkflowApprovalStatus.Pending
        };

        var instances = new Mock<IWorkflowInstanceRepository>();
        instances.Setup(repository => repository.GetWithDetailsAsync(workflowId, default)).ReturnsAsync(instance);
        instances.Setup(repository => repository.UpdateAsync(It.IsAny<WorkflowInstance>())).Returns(Task.CompletedTask);
        instances.Setup(repository => repository.SaveChangesAsync()).ReturnsAsync(1);

        var steps = new Mock<IWorkflowStepInstanceRepository>();
        steps.Setup(repository => repository.GetByIdAsync(stepInstanceId)).ReturnsAsync(step);
        steps.Setup(repository => repository.UpdateAsync(It.IsAny<WorkflowStepInstance>())).Returns(Task.CompletedTask);
        steps.Setup(repository => repository.SaveChangesAsync()).ReturnsAsync(1);

        var approvals = new Mock<IWorkflowApprovalRepository>();
        approvals.Setup(repository => repository.GetByStepInstanceAsync(stepInstanceId, default))
            .ReturnsAsync([selected, sibling]);
        approvals.Setup(repository => repository.UpdateAsync(It.IsAny<WorkflowApproval>())).Returns(Task.CompletedTask);
        approvals.Setup(repository => repository.SaveChangesAsync()).ReturnsAsync(1);

        var transitions = new Mock<IWorkflowTransitionRepository>();
        transitions.Setup(repository => repository.GetFromStepAsync(stepId, default))
            .ReturnsAsync([]);

        var activity = new Mock<IWorkflowActivityService>();
        activity.Setup(service => service.LogActivityAsync(
                It.IsAny<Guid>(), It.IsAny<WorkflowActivityType>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<object?>(), default))
            .Returns(Task.CompletedTask);

        var notifications = new Mock<IWorkflowNotificationService>();
        notifications.Setup(service => service.SendWorkflowCompletionNotificationAsync(workflowId))
            .Returns(Task.CompletedTask);
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(service => service.Roles).Returns(["Senior Accountant"]);
        var workflowSteps = new Mock<IWorkflowStepRepository>();
        var definitionSteps = new List<WorkflowStep> { stepDefinition };
        if (hasDeclaredEnd && !atEnd)
            definitionSteps.Add(new WorkflowStep { Id = Guid.NewGuid(), Name = "Independent final approval", IsEndStep = true, StepType = WorkflowStepType.Approval });
        workflowSteps.Setup(repository => repository.GetByWorkflowDefinitionAsync(instance.WorkflowDefinitionId, default))
            .ReturnsAsync(definitionSteps);

        var engine = new WorkflowEngine(
            Mock.Of<IWorkflowDefinitionRepository>(),
            workflowSteps.Object,
            transitions.Object,
            instances.Object,
            steps.Object,
            approvals.Object,
            activity.Object,
            Mock.Of<IWorkflowConditionEvaluator>(),
            notifications.Object,
            Mock.Of<IWorkflowApprovalPolicyResolver>(),
            Mock.Of<IWorkflowRuntimeGovernanceService>(),
            Mock.Of<IWorkflowSignatureSubmissionStore>(),
            currentUser.Object,
            Mock.Of<ILogger<WorkflowEngine>>());

        var result = await engine.ProcessStepAsync(stepInstanceId, approverId, WorkflowStepAction.Complete);

        var completionAllowed = !hasDeclaredEnd || atEnd;
        Assert.Equal(completionAllowed, result.Success);
        Assert.Equal(WorkflowApprovalStatus.Approved, selected.Status);
        Assert.Equal(WorkflowApprovalStatus.Expired, sibling.Status);
        Assert.NotNull(sibling.ProcessedDate);
        Assert.Equal(WorkflowStepInstanceStatus.Completed, step.Status);
        Assert.Equal(completionAllowed ? WorkflowInstanceStatus.Completed : WorkflowInstanceStatus.InProgress, instance.Status);
        notifications.Verify(service => service.SendWorkflowCompletionNotificationAsync(workflowId), completionAllowed ? Times.Once() : Times.Never());
        if (!completionAllowed)
        {
            Assert.Null(instance.CompletedDate);
            Assert.Contains("configured end step", result.Message);
        }
    }

    [Fact]
    public async Task CancelWorkflowAsync_RecordsDedicatedCancellationDateAndTerminalEvidence()
    {
        var tenantId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var instance = new WorkflowInstance
        {
            Id = workflowId,
            TenantId = tenantId,
            Status = WorkflowInstanceStatus.InProgress
        };
        var step = new WorkflowStepInstance
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WorkflowInstanceId = workflowId,
            Status = WorkflowStepInstanceStatus.InProgress
        };
        var approval = new WorkflowApproval
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            StepInstanceId = step.Id,
            StepInstance = step,
            Status = WorkflowApprovalStatus.Pending
        };

        var instances = new Mock<IWorkflowInstanceRepository>();
        instances.Setup(repository => repository.GetWithDetailsAsync(workflowId, default))
            .ReturnsAsync(instance);
        instances.Setup(repository => repository.UpdateAsync(instance)).Returns(Task.CompletedTask);
        instances.Setup(repository => repository.SaveChangesAsync()).ReturnsAsync(1);

        var steps = new Mock<IWorkflowStepInstanceRepository>();
        steps.Setup(repository => repository.GetByWorkflowInstanceAsync(workflowId, default))
            .ReturnsAsync([step]);
        steps.Setup(repository => repository.UpdateAsync(step)).Returns(Task.CompletedTask);
        steps.Setup(repository => repository.SaveChangesAsync()).ReturnsAsync(1);

        var approvals = new Mock<IWorkflowApprovalRepository>();
        approvals.Setup(repository => repository.GetByStatusAsync(WorkflowApprovalStatus.Pending, tenantId, default))
            .ReturnsAsync([approval]);
        approvals.Setup(repository => repository.UpdateAsync(approval)).Returns(Task.CompletedTask);
        approvals.Setup(repository => repository.SaveChangesAsync()).ReturnsAsync(1);

        var activity = new Mock<IWorkflowActivityService>();
        activity.Setup(service => service.LogActivityAsync(
                workflowId,
                WorkflowActivityType.WorkflowCancelled,
                "Workflow cancelled",
                "UAT recall",
                userId,
                null,
                null,
                default))
            .Returns(Task.CompletedTask);

        var engine = new WorkflowEngine(
            Mock.Of<IWorkflowDefinitionRepository>(),
            Mock.Of<IWorkflowStepRepository>(),
            Mock.Of<IWorkflowTransitionRepository>(),
            instances.Object,
            steps.Object,
            approvals.Object,
            activity.Object,
            Mock.Of<IWorkflowConditionEvaluator>(),
            Mock.Of<IWorkflowNotificationService>(),
            Mock.Of<IWorkflowApprovalPolicyResolver>(),
            Mock.Of<IWorkflowRuntimeGovernanceService>(),
            Mock.Of<IWorkflowSignatureSubmissionStore>(),
            Mock.Of<ICurrentUserService>(),
            Mock.Of<ILogger<WorkflowEngine>>());

        await engine.CancelWorkflowAsync(workflowId, userId, "UAT recall");

        Assert.Equal(WorkflowInstanceStatus.Cancelled, instance.Status);
        Assert.NotNull(instance.CancelledDate);
        Assert.Equal(instance.CompletedDate, instance.CancelledDate);
        Assert.Equal(WorkflowStepInstanceStatus.Cancelled, step.Status);
        Assert.Equal(WorkflowApprovalStatus.Expired, approval.Status);
        activity.VerifyAll();
    }
}
