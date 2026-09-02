using ErpSystem.Api.Controllers.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Workflow;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class FinanceApprovalQueueProjectionTests
{
    [Fact]
    [Trait("Batch", "FinanceApprovalActiveQueue")]
    public async Task Pending_queue_should_return_only_active_current_step_approvals_for_the_tenant()
    {
        await using var db = CreateContext();
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();

        var activeCurrent = AddApprovalGraph(
            db,
            tenantId,
            WorkflowInstanceStatus.InProgress,
            WorkflowStepInstanceStatus.Pending,
            approvalIsForCurrentStep: true);
        var waitingCurrent = AddApprovalGraph(
            db,
            tenantId,
            WorkflowInstanceStatus.Waiting,
            WorkflowStepInstanceStatus.InProgress,
            approvalIsForCurrentStep: true);
        _ = AddApprovalGraph(
            db,
            tenantId,
            WorkflowInstanceStatus.Completed,
            WorkflowStepInstanceStatus.Pending,
            approvalIsForCurrentStep: true);
        _ = AddApprovalGraph(
            db,
            tenantId,
            WorkflowInstanceStatus.InProgress,
            WorkflowStepInstanceStatus.Pending,
            approvalIsForCurrentStep: false);
        _ = AddApprovalGraph(
            db,
            tenantId,
            WorkflowInstanceStatus.InProgress,
            WorkflowStepInstanceStatus.Completed,
            approvalIsForCurrentStep: true);
        _ = AddApprovalGraph(
            db,
            otherTenantId,
            WorkflowInstanceStatus.InProgress,
            WorkflowStepInstanceStatus.Pending,
            approvalIsForCurrentStep: true);

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var controller = CreateController(db);

        var result = await controller.QueryPendingApprovals(tenantId)
            .Select(approval => approval.Id)
            .ToListAsync();

        result.Should().BeEquivalentTo(new[] { activeCurrent, waitingCurrent });
        (await db.WorkflowApprovals.CountAsync(approval => approval.Status == WorkflowApprovalStatus.Pending))
            .Should().Be(6, "historical approvals remain immutable even when they are no longer actionable");
    }

    private static Guid AddApprovalGraph(
        ApplicationDbContext db,
        Guid tenantId,
        WorkflowInstanceStatus instanceStatus,
        WorkflowStepInstanceStatus stepStatus,
        bool approvalIsForCurrentStep)
    {
        var entityType = new WorkflowEntityType
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = $"FIN_{Guid.NewGuid():N}",
            Name = "Finance approval test entity"
        };
        var definition = new WorkflowDefinition
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Finance approval queue test",
            EntityTypeId = entityType.Id,
            EntityType = entityType
        };
        var approvalStep = new WorkflowStep
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WorkflowDefinitionId = definition.Id,
            WorkflowDefinition = definition,
            Name = "Approval",
            StepType = WorkflowStepType.Approval,
            Order = 1
        };
        var currentStep = approvalIsForCurrentStep
            ? approvalStep
            : new WorkflowStep
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                WorkflowDefinitionId = definition.Id,
                WorkflowDefinition = definition,
                Name = "Later approval",
                StepType = WorkflowStepType.Approval,
                Order = 2
            };
        var initiator = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserName = $"finance.queue.{Guid.NewGuid():N}",
            NormalizedUserName = $"FINANCE.QUEUE.{Guid.NewGuid():N}",
            FirstName = "Finance",
            LastName = "Submitter"
        };
        var instance = new WorkflowInstance
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WorkflowDefinitionId = definition.Id,
            WorkflowDefinition = definition,
            EntityId = Guid.NewGuid(),
            EntityTypeId = entityType.Id,
            EntityType = entityType,
            InitiatedById = initiator.Id,
            InitiatedBy = initiator,
            Status = instanceStatus,
            CurrentStepId = currentStep.Id,
            CurrentStep = currentStep
        };
        var stepInstance = new WorkflowStepInstance
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            WorkflowInstanceId = instance.Id,
            WorkflowInstance = instance,
            WorkflowStepId = approvalStep.Id,
            WorkflowStep = approvalStep,
            Status = stepStatus
        };
        var approval = new WorkflowApproval
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            StepInstanceId = stepInstance.Id,
            StepInstance = stepInstance,
            Status = WorkflowApprovalStatus.Pending
        };

        db.WorkflowApprovals.Add(approval);
        return approval.Id;
    }

    private static FinanceApprovalsController CreateController(ApplicationDbContext db)
        => new(
            db,
            Mock.Of<ICurrentUserService>(),
            Mock.Of<IAuthorizationService>(),
            Mock.Of<IWorkflowService>(),
            Mock.Of<IWorkflowEntityDisplayService>(),
            Mock.Of<IJournalEntryService>(),
            Mock.Of<IInvoiceService>(),
            Mock.Of<ErpSystem.Core.Interfaces.Inventory.IInventoryValuationService>(),
            null!,
            NullLogger<FinanceApprovalsController>.Instance);

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"finance-approval-queue-{Guid.NewGuid():N}")
            .Options;
        return new ApplicationDbContext(options);
    }
}
