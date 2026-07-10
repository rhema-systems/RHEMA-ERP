using ErpSystem.Api.Services.Workflow;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Repositories;
using ErpSystem.Data;
using ErpSystem.Data.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services;

public class WorkflowDefinitionServiceAdapterCloneTests
{
    [Fact]
    public async Task CloneWorkflowDefinitionDraftAsync_ShouldRemapTransitionsToFinalClonedStepIds()
    {
        await using var context = CreateContext();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var definitionKey = Guid.NewGuid();
        var entityType = new WorkflowEntityType
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "PurchaseOrder",
            Code = "PURCHASE_ORDER",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Tests"
        };
        var source = new WorkflowDefinition
        {
            Id = Guid.NewGuid(),
            DefinitionKey = definitionKey,
            TenantId = tenantId,
            EntityTypeId = entityType.Id,
            Name = "PO Approval",
            Version = 1,
            LifecycleStatus = WorkflowDefinitionLifecycleStatus.Published,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Tests"
        };
        var start = Step(source, "Start", 1, isStart: true);
        var approval = Step(source, "Approval", 2);
        var end = Step(source, "End", 3, isEnd: true);
        var transitionOne = Transition(source, start, approval, "Start to Approval");
        var transitionTwo = Transition(source, approval, end, "Approval to End");

        context.WorkflowEntityTypes.Add(entityType);
        context.WorkflowDefinitions.Add(source);
        context.WorkflowSteps.AddRange(start, approval, end);
        context.WorkflowTransitions.AddRange(transitionOne, transitionTwo);
        await context.SaveChangesAsync();

        var service = CreateService(context, tenantId, userId);

        var draft = await service.CloneWorkflowDefinitionDraftAsync(source.Id, null, userId);

        var clonedStepIds = await context.WorkflowSteps
            .Where(step => step.WorkflowDefinitionId == draft.Id)
            .Select(step => step.Id)
            .ToListAsync();
        var clonedTransitions = await context.WorkflowTransitions
            .Where(transition => transition.WorkflowDefinitionId == draft.Id)
            .ToListAsync();

        draft.DefinitionKey.Should().Be(definitionKey);
        clonedStepIds.Should().HaveCount(3);
        clonedTransitions.Should().HaveCount(2);
        clonedTransitions.SelectMany(transition => new[] { transition.FromStepId, transition.ToStepId })
            .Should()
            .OnlyContain(stepId => clonedStepIds.Contains(stepId));
        clonedTransitions.SelectMany(transition => new[] { transition.FromStepId, transition.ToStepId })
            .Should()
            .NotContain(new[] { start.Id, approval.Id, end.Id });
    }

    private static WorkflowDefinitionServiceAdapter CreateService(
        ApplicationDbContext context,
        Guid tenantId,
        Guid userId)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(service => service.TenantId).Returns(tenantId);
        currentUser.SetupGet(service => service.UserId).Returns(userId.ToString());
        currentUser.SetupGet(service => service.UserName).Returns("workflow.tests");

        return new WorkflowDefinitionServiceAdapter(
            Mock.Of<ErpSystem.Core.Interfaces.Services.IWorkflowDefinitionService>(),
            currentUser.Object,
            new WorkflowDefinitionRepository(context),
            new WorkflowStepRepository(context),
            new WorkflowTransitionRepository(context),
            new WorkflowEntityTypeRepository(context),
            Mock.Of<ILogger<WorkflowDefinitionServiceAdapter>>());
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new ApplicationDbContext(options);
    }

    private static WorkflowStep Step(
        WorkflowDefinition definition,
        string name,
        int order,
        bool isStart = false,
        bool isEnd = false) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = definition.TenantId,
        WorkflowDefinitionId = definition.Id,
        Name = name,
        Order = order,
        StepType = WorkflowStepType.Approval,
        IsStartStep = isStart,
        IsEndStep = isEnd,
        CreatedAt = DateTime.UtcNow,
        CreatedBy = "Tests"
    };

    private static WorkflowTransition Transition(
        WorkflowDefinition definition,
        WorkflowStep from,
        WorkflowStep to,
        string name) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = definition.TenantId,
        WorkflowDefinitionId = definition.Id,
        FromStepId = from.Id,
        ToStepId = to.Id,
        Name = name,
        IsDefault = true,
        CreatedAt = DateTime.UtcNow,
        CreatedBy = "Tests"
    };
}
