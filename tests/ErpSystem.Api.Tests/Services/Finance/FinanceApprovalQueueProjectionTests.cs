using ErpSystem.Api.Controllers.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Workflow;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ErpSystem.Shared;
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

    [Fact]
    [Trait("Batch", "FinanceApprovalActiveQueue")]
    public async Task Book_transition_queue_should_show_only_the_assigned_independent_checker_and_require_book_decision_path()
    {
        await using var db = CreateContext();
        var tenantId = Guid.NewGuid();
        var makerId = Guid.NewGuid();
        var checkerId = Guid.NewGuid();
        var approvalId = AddApprovalGraph(db, tenantId,
            WorkflowInstanceStatus.InProgress, WorkflowStepInstanceStatus.Pending,
            approvalIsForCurrentStep: true,
            entityCode: "AccountingBookLifecycle", initiatorId: makerId,
            approverRole: "Financial Controller");
        var instance = db.WorkflowApprovals.Local.Single(item => item.Id == approvalId)
            .StepInstance.WorkflowInstance;
        db.AccountingBooks.Add(new AccountingBook
        {
            Id = instance.EntityId,
            TenantId = tenantId,
            Code = "IFRS",
            Name = "IFRS Primary",
            LifecycleStatus = AccountingBookLifecycleStatus.Configuring,
            PendingLifecycleStatus = AccountingBookLifecycleStatus.Initializing,
            TransitionRequestedByUserId = makerId,
            TransitionWorkflowInstanceId = instance.Id,
            TransitionRequestedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var checker = CreateQueueController(db, tenantId, checkerId);
        var checkerResponse = await checker.GetPending(CancellationToken.None);
        var checkerRows = ((OkObjectResult)checkerResponse.Result!).Value
            .Should().BeAssignableTo<IReadOnlyList<FinanceApprovalsController.FinanceApprovalQueueItemDto>>()
            .Which;
        checkerRows.Should().ContainSingle().Which.Should().Match<FinanceApprovalsController.FinanceApprovalQueueItemDto>(row =>
            row.Reference == "IFRS" && row.DecisionOnDetailPage &&
            row.DetailHref == "/finance/settings/accounting-books" &&
            !row.CanApprove && !row.CanReject);

        var maker = CreateQueueController(db, tenantId, makerId);
        var makerResponse = await maker.GetPending(CancellationToken.None);
        var makerRows = ((OkObjectResult)makerResponse.Result!).Value
            .Should().BeAssignableTo<IReadOnlyList<FinanceApprovalsController.FinanceApprovalQueueItemDto>>()
            .Which;
        makerRows.Should().BeEmpty();
    }

    private static Guid AddApprovalGraph(
        ApplicationDbContext db,
        Guid tenantId,
        WorkflowInstanceStatus instanceStatus,
        WorkflowStepInstanceStatus stepStatus,
        bool approvalIsForCurrentStep,
        string? entityCode = null,
        Guid? initiatorId = null,
        string? approverRole = null)
    {
        var entityType = new WorkflowEntityType
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = entityCode ?? $"FIN_{Guid.NewGuid():N}",
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
            Id = initiatorId ?? Guid.NewGuid(),
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
            Status = WorkflowApprovalStatus.Pending,
            ApproverRole = approverRole
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

    private static FinanceApprovalsController CreateQueueController(
        ApplicationDbContext db, Guid tenantId, Guid userId)
    {
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(value => value.UserId).Returns(userId.ToString());
        user.SetupGet(value => value.TenantId).Returns(tenantId);
        user.SetupGet(value => value.Roles).Returns(new[] { "Financial Controller" });
        var authorization = new Mock<IAuthorizationService>();
        authorization.Setup(value => value.AuthorizeAsync(
                It.IsAny<System.Security.Claims.ClaimsPrincipal>(),
                It.IsAny<object?>(), It.IsAny<string>()))
            .ReturnsAsync(AuthorizationResult.Success());
        var workflow = new Mock<IWorkflowService>();
        workflow.Setup(value => value.CanUserApproveAsync("AccountingBookLifecycle", It.IsAny<Guid>(), userId))
            .ReturnsAsync(true);
        return new FinanceApprovalsController(
            db, user.Object, authorization.Object, workflow.Object,
            Mock.Of<IWorkflowEntityDisplayService>(), Mock.Of<IJournalEntryService>(),
            Mock.Of<IInvoiceService>(),
            Mock.Of<ErpSystem.Core.Interfaces.Inventory.IInventoryValuationService>(),
            null!, NullLogger<FinanceApprovalsController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"finance-approval-queue-{Guid.NewGuid():N}")
            .Options;
        return new ApplicationDbContext(options);
    }
}
