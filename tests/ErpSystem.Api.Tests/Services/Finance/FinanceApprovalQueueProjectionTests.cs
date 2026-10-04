using ErpSystem.Api.Controllers.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Finance.FixedAssets;
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
    public async Task Capital_project_submitter_cannot_action_assigned_approval_but_independent_checker_can()
    {
        await using var db = CreateContext();
        var tenantId = Guid.NewGuid();
        var makerId = Guid.NewGuid();
        var checkerId = Guid.NewGuid();
        var approvalId = AddApprovalGraph(db, tenantId,
            WorkflowInstanceStatus.InProgress, WorkflowStepInstanceStatus.Pending,
            approvalIsForCurrentStep: true,
            entityCode: "CapitalProject", initiatorId: makerId,
            approverRole: "Financial Controller");
        var instance = db.WorkflowApprovals.Local.Single(item => item.Id == approvalId)
            .StepInstance.WorkflowInstance;
        db.CapitalProjects.Add(new ErpSystem.Core.Entities.Finance.FixedAssets.CapitalProject
        {
            Id = instance.EntityId,
            TenantId = tenantId,
            ProjectCode = "CP-SOD-001",
            Name = "Capital project maker-checker",
            StartDate = new DateTime(2026, 9, 1),
            TotalBudgetAmount = 10_000m,
            Status = ProjectStatus.PendingApproval,
            RowVersion = Array.Empty<byte>()
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var maker = CreateQueueController(db, tenantId, makerId);
        var makerResponse = await maker.GetPending(CancellationToken.None);
        var makerRows = ((OkObjectResult)makerResponse.Result!).Value
            .Should().BeAssignableTo<IReadOnlyList<FinanceApprovalsController.FinanceApprovalQueueItemDto>>()
            .Which;
        makerRows.Should().ContainSingle().Which.Should().Match<FinanceApprovalsController.FinanceApprovalQueueItemDto>(
            row => row.EntityType == "CapitalProject" && !row.CanApprove && !row.CanReject &&
                   row.ApproveDisabledReason != null && row.ApproveDisabledReason.Contains("submitted"));

        var denied = await maker.Approve(approvalId,
            new FinanceApprovalsController.FinanceApprovalActionRequest { Comments = "self approval" },
            CancellationToken.None);
        var problem = denied.Result.Should().BeAssignableTo<ObjectResult>().Which;
        problem.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        (await db.CapitalProjects.SingleAsync()).Status.Should().Be(ProjectStatus.PendingApproval);

        var checker = CreateQueueController(db, tenantId, checkerId);
        var checkerResponse = await checker.GetPending(CancellationToken.None);
        var checkerRows = ((OkObjectResult)checkerResponse.Result!).Value
            .Should().BeAssignableTo<IReadOnlyList<FinanceApprovalsController.FinanceApprovalQueueItemDto>>()
            .Which;
        checkerRows.Should().ContainSingle().Which.Should().Match<FinanceApprovalsController.FinanceApprovalQueueItemDto>(
            row => row.EntityType == "CapitalProject" && row.CanApprove && row.CanReject);
    }

    [Fact]
    [Trait("Batch", "FinanceApprovalActiveQueue")]
    public async Task Journal_batch_should_be_visible_only_to_checker_and_require_detail_page_decisions()
    {
        await using var db = CreateContext();
        var tenantId = Guid.NewGuid();
        var makerId = Guid.NewGuid();
        var checkerId = Guid.NewGuid();
        var approvalId = AddApprovalGraph(db, tenantId,
            WorkflowInstanceStatus.InProgress, WorkflowStepInstanceStatus.Pending,
            approvalIsForCurrentStep: true,
            entityCode: "JournalBatch", initiatorId: makerId,
            approverRole: "Financial Controller");
        var instance = db.WorkflowApprovals.Local.Single(item => item.Id == approvalId)
            .StepInstance.WorkflowInstance;
        db.JournalBatches.Add(new JournalBatch
        {
            Id = instance.EntityId,
            TenantId = tenantId,
            BatchNumber = "JB-2026-00001",
            Description = "Independent batch review",
            FiscalPeriodId = Guid.NewGuid(),
            AccountingBookId = Guid.NewGuid(),
            BookClassification = "BASE",
            ControlCurrencyCode = "GHS",
            ExpectedDebitTotal = 1_000m,
            ApprovalRequired = true,
            ApprovalStatus = JournalBatchApprovalStatus.PendingApproval,
            PostingStatus = JournalBatchPostingStatus.NotReady,
            ReversalStatus = JournalBatchReversalStatus.NotReversed,
            WorkflowInstanceId = instance.Id,
            CreatedById = makerId,
            SubmittedByUserId = makerId,
            SubmittedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var maker = CreateQueueController(db, tenantId, makerId);
        var makerResponse = await maker.GetPending(CancellationToken.None);
        var makerRows = ((OkObjectResult)makerResponse.Result!).Value
            .Should().BeAssignableTo<IReadOnlyList<FinanceApprovalsController.FinanceApprovalQueueItemDto>>()
            .Which;
        makerRows.Should().BeEmpty();

        var checker = CreateQueueController(db, tenantId, checkerId);
        var checkerResponse = await checker.GetPending(CancellationToken.None);
        var checkerRows = ((OkObjectResult)checkerResponse.Result!).Value
            .Should().BeAssignableTo<IReadOnlyList<FinanceApprovalsController.FinanceApprovalQueueItemDto>>()
            .Which;
        checkerRows.Should().ContainSingle().Which.Should().Match<FinanceApprovalsController.FinanceApprovalQueueItemDto>(
            row => row.EntityType == "JournalBatch" &&
                   row.DocumentType == "Journal Batch" &&
                   row.DecisionOnDetailPage &&
                   row.DetailHref == $"/finance/journal-batches/{instance.EntityId:D}" &&
                   !row.CanApprove &&
                   !row.CanReject);

        var bypass = await checker.Approve(
            approvalId,
            new FinanceApprovalsController.FinanceApprovalActionRequest { Comments = "Bypass item decisions" },
            CancellationToken.None);
        bypass.Result.Should().BeOfType<BadRequestObjectResult>();
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

    [Fact]
    [Trait("Batch", "FinanceApprovalActiveQueue")]
    public async Task Book_period_and_initialization_queue_should_link_independent_checker_to_readiness()
    {
        await using var db = CreateContext();
        var tenantId = Guid.NewGuid();
        var periodMakerId = Guid.NewGuid();
        var initializationMakerId = Guid.NewGuid();
        var checkerId = Guid.NewGuid();
        var periodApprovalId = AddApprovalGraph(db, tenantId,
            WorkflowInstanceStatus.InProgress, WorkflowStepInstanceStatus.Pending,
            approvalIsForCurrentStep: true,
            entityCode: "AccountingBookPeriodLifecycle", initiatorId: periodMakerId,
            approverRole: "Financial Controller");
        var initializationApprovalId = AddApprovalGraph(db, tenantId,
            WorkflowInstanceStatus.InProgress, WorkflowStepInstanceStatus.Pending,
            approvalIsForCurrentStep: true,
            entityCode: "AccountingBookInitialization", initiatorId: initializationMakerId,
            approverRole: "Financial Controller");
        var periodInstance = db.WorkflowApprovals.Local.Single(item => item.Id == periodApprovalId)
            .StepInstance.WorkflowInstance;
        var initializationInstance = db.WorkflowApprovals.Local.Single(item => item.Id == initializationApprovalId)
            .StepInstance.WorkflowInstance;
        var bookId = Guid.NewGuid();
        var fiscalPeriodId = Guid.NewGuid();
        db.AccountingBooks.Add(new AccountingBook
        {
            Id = bookId, TenantId = tenantId, Code = "IFRS", Name = "IFRS Primary",
            LifecycleStatus = AccountingBookLifecycleStatus.Initializing
        });
        db.FiscalPeriods.Add(new FiscalPeriod
        {
            Id = fiscalPeriodId, TenantId = tenantId, FiscalYearId = Guid.NewGuid(),
            PeriodCode = "2026-01", PeriodName = "January 2026", PeriodNumber = 1,
            StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2026, 1, 31)
        });
        db.AccountingBookPeriods.Add(new AccountingBookPeriod
        {
            Id = periodInstance.EntityId, TenantId = tenantId, AccountingBookId = bookId,
            FiscalPeriodId = fiscalPeriodId, PeriodStatus = AccountingBookPeriodStatus.Future,
            PendingStatus = AccountingBookPeriodStatus.Open, RequestedByUserId = periodMakerId,
            RequestedAtUtc = DateTime.UtcNow, WorkflowInstanceId = periodInstance.Id
        });
        db.AccountingBookInitializations.Add(new AccountingBookInitialization
        {
            Id = initializationInstance.EntityId, TenantId = tenantId, AccountingBookId = bookId,
            Version = 1, Mode = AccountingBookInitializationMode.IndependentOpeningBalances,
            InitializationStatus = AccountingBookInitializationStatus.PendingApproval,
            CutoffDate = new DateTime(2025, 12, 31), PreparedByUserId = initializationMakerId,
            PreparedAtUtc = DateTime.UtcNow, WorkflowInstanceId = initializationInstance.Id
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var checker = CreateQueueController(db, tenantId, checkerId);
        var checkerResponse = await checker.GetPending(CancellationToken.None);
        var checkerRows = ((OkObjectResult)checkerResponse.Result!).Value
            .Should().BeAssignableTo<IReadOnlyList<FinanceApprovalsController.FinanceApprovalQueueItemDto>>()
            .Which;
        checkerRows.Should().HaveCount(2);
        checkerRows.Should().ContainSingle(row => row.EntityType == "AccountingBookPeriodLifecycle")
            .Which.Should().Match<FinanceApprovalsController.FinanceApprovalQueueItemDto>(row =>
                row.Reference == "IFRS/2026-01" && row.DecisionOnDetailPage &&
                row.DetailHref == $"/finance/settings/accounting-books/{bookId:D}/readiness" &&
                !row.CanApprove && !row.CanReject);
        checkerRows.Should().ContainSingle(row => row.EntityType == "AccountingBookInitialization")
            .Which.Should().Match<FinanceApprovalsController.FinanceApprovalQueueItemDto>(row =>
                row.Reference == "IFRS/V1" && row.DecisionOnDetailPage &&
                row.DetailHref == $"/finance/settings/accounting-books/{bookId:D}/readiness" &&
                !row.CanApprove && !row.CanReject);

        var periodMaker = CreateQueueController(db, tenantId, periodMakerId);
        var makerResponse = await periodMaker.GetPending(CancellationToken.None);
        var makerRows = ((OkObjectResult)makerResponse.Result!).Value
            .Should().BeAssignableTo<IReadOnlyList<FinanceApprovalsController.FinanceApprovalQueueItemDto>>()
            .Which;
        makerRows.Should().NotContain(row => row.EntityType == "AccountingBookPeriodLifecycle");

        var period = await db.AccountingBookPeriods.SingleAsync();
        var initialization = await db.AccountingBookInitializations.SingleAsync();
        period.PendingStatus = null;
        initialization.InitializationStatus = AccountingBookInitializationStatus.Approved;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var staleResponse = await checker.GetPending(CancellationToken.None);
        var staleRows = ((OkObjectResult)staleResponse.Result!).Value
            .Should().BeAssignableTo<IReadOnlyList<FinanceApprovalsController.FinanceApprovalQueueItemDto>>()
            .Which;
        staleRows.Should().BeEmpty("resolved book decisions cannot reappear from stale workflow rows");
    }

    [Fact]
    [Trait("Batch", "FinanceApprovalActiveQueue")]
    public async Task Retired_applicability_policy_workflows_should_not_appear_in_the_active_queue()
    {
        await using var db = CreateContext();
        var tenantId = Guid.NewGuid();
        var makerId = Guid.NewGuid();
        var checkerId = Guid.NewGuid();
        var approvalId = AddApprovalGraph(db, tenantId,
            WorkflowInstanceStatus.InProgress, WorkflowStepInstanceStatus.Pending,
            approvalIsForCurrentStep: true,
            entityCode: "AccountingBookApplicabilityPolicy", initiatorId: makerId,
            approverRole: "Financial Controller");
        var instance = db.WorkflowApprovals.Local.Single(item => item.Id == approvalId)
            .StepInstance.WorkflowInstance;
        db.AccountingBookApplicabilityPolicies.Add(new AccountingBookApplicabilityPolicy
        {
            Id = instance.EntityId,
            TenantId = tenantId,
            PolicyCode = "TEST",
            Version = 1,
            Name = "Test applicability",
            EffectiveFrom = new DateTime(2026, 9, 20),
            PolicyStatus = AccountingBookApplicabilityPolicyStatus.PendingApproval,
            Reason = "Test governed routing",
            PreparedByUserId = makerId,
            PreparedAtUtc = DateTime.UtcNow,
            WorkflowInstanceId = instance.Id
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var checker = CreateQueueController(db, tenantId, checkerId);
        var checkerResponse = await checker.GetPending(CancellationToken.None);
        var checkerRows = ((OkObjectResult)checkerResponse.Result!).Value
            .Should().BeAssignableTo<IReadOnlyList<FinanceApprovalsController.FinanceApprovalQueueItemDto>>()
            .Which;
        checkerRows.Should().BeEmpty();

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
        workflow.Setup(value => value.CanUserApproveAsync("AccountingBookPeriodLifecycle", It.IsAny<Guid>(), userId))
            .ReturnsAsync(true);
        workflow.Setup(value => value.CanUserApproveAsync("AccountingBookInitialization", It.IsAny<Guid>(), userId))
            .ReturnsAsync(true);
        workflow.Setup(value => value.CanUserApproveAsync("AccountingBookApplicabilityPolicy", It.IsAny<Guid>(), userId))
            .ReturnsAsync(true);
        workflow.Setup(value => value.CanUserApproveAsync("JournalBatch", It.IsAny<Guid>(), userId))
            .ReturnsAsync(true);
        var display = new Mock<IWorkflowEntityDisplayService>();
        display.Setup(value => value.GetEntityDisplayInfoAsync(It.IsAny<string>(), It.IsAny<Guid>()))
            .ReturnsAsync((string entityType, Guid entityId) => new WorkflowEntityDisplayInfo
            {
                EntityType = entityType,
                EntityId = entityId
            });
        return new FinanceApprovalsController(
            db, user.Object, authorization.Object, workflow.Object,
            display.Object, Mock.Of<IJournalEntryService>(),
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
