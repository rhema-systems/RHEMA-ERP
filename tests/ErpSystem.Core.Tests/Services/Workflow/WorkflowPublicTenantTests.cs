using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Repositories;
using ErpSystem.Core.Interfaces.Services;
using ErpSystem.Core.Interfaces.Workflow;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Workflow;
using ErpSystem.Core.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Workflow;

public class WorkflowPublicTenantTests
{
    [Fact]
    public async Task AnonymousWorkflowUsesResolvedPublicTenant()
    {
        var tenantId = Guid.NewGuid();
        var definition = new WorkflowDefinition
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "EHC Ticket",
            EntityTypeId = Guid.NewGuid(),
            IsActive = true,
            LifecycleStatus = WorkflowDefinitionLifecycleStatus.Published
        };
        var definitions = new Mock<IWorkflowDefinitionRepository>();
        definitions.Setup(repository => repository.GetByNameAsync("EHC Ticket", tenantId, default))
            .ReturnsAsync(definition);
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(user => user.TenantId).Returns((Guid?)null);
        var engine = CreateEngine(definitions.Object, currentUser.Object);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.StartWorkflowForTenantAsync("EHC Ticket", tenantId, Guid.NewGuid(), Guid.NewGuid()));

        Assert.Contains("No start step found", error.Message);
        definitions.Verify(repository => repository.GetByNameAsync("EHC Ticket", tenantId, default), Times.Once);
    }

    [Fact]
    public async Task AuthenticatedCallerCannotStartWorkflowForAnotherTenant()
    {
        var definitions = new Mock<IWorkflowDefinitionRepository>();
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(user => user.TenantId).Returns(Guid.NewGuid());
        var engine = CreateEngine(definitions.Object, currentUser.Object);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            engine.StartWorkflowForTenantAsync("EHC Ticket", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));

        definitions.VerifyNoOtherCalls();
    }

    private static WorkflowEngine CreateEngine(IWorkflowDefinitionRepository definitions, ICurrentUserService currentUser) =>
        new(
            definitions,
            Mock.Of<IWorkflowStepRepository>(),
            Mock.Of<IWorkflowTransitionRepository>(),
            Mock.Of<IWorkflowInstanceRepository>(),
            Mock.Of<IWorkflowStepInstanceRepository>(),
            Mock.Of<IWorkflowApprovalRepository>(),
            Mock.Of<IWorkflowActivityService>(),
            Mock.Of<IWorkflowConditionEvaluator>(),
            Mock.Of<IWorkflowNotificationService>(),
            Mock.Of<IWorkflowApprovalPolicyResolver>(),
            Mock.Of<IWorkflowRuntimeGovernanceService>(),
            Mock.Of<IWorkflowSignatureSubmissionStore>(),
            currentUser,
            NullLogger<WorkflowEngine>.Instance,
            Mock.Of<IProcurementSodPolicy>());
}
