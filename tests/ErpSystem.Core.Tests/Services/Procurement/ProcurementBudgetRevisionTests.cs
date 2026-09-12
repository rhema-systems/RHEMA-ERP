using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.DTOs.Workflow;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Core.Services.Workflow;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementBudgetRevisionTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _makerId = Guid.NewGuid();
    private readonly Mock<IProcurementBudgetRepository> _budgets = new();
    private readonly Mock<IProcurementBudgetAllocationRepository> _allocations = new();
    private readonly Mock<IProcurementBudgetRevisionRepository> _revisions = new();
    private readonly Mock<IProcurementPlanRepository> _plans = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IWorkflowIntegrationService> _workflow = new();
    private readonly IWorkflowStatusAdapterRegistry _adapters = new WorkflowStatusAdapterRegistry(
        new IWorkflowStatusAdapter[] { new ProcurementBudgetRevisionWorkflowStatusAdapter(), new ProcurementBudgetWorkflowStatusAdapter() });

    public ProcurementBudgetRevisionTests()
    {
        _unitOfWork.SetupGet(value => value.HasActiveTransaction).Returns(true);
        _unitOfWork.Setup(value => value.AcquireTransactionLockAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWork.Setup(value => value.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _budgets.Setup(value => value.UpdateAsync(It.IsAny<ProcurementBudget>())).Returns(Task.CompletedTask);
        _revisions.Setup(value => value.UpdateAsync(It.IsAny<ProcurementBudgetRevision>())).Returns(Task.CompletedTask);
        _plans.Setup(value => value.GetPlannedBudgetExposureAsync(It.IsAny<Guid>(), null)).ReturnsAsync(0m);
        _allocations.Setup(value => value.GetTotalAllocatedAsync(It.IsAny<Guid>())).ReturnsAsync(0m);
        _revisions.Setup(value => value.GetByBudgetIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(Array.Empty<ProcurementBudgetRevision>());
        _revisions.Setup(value => value.GetNextRevisionNumberAsync(It.IsAny<Guid>())).ReturnsAsync(1);
        _revisions.Setup(value => value.AddAsync(It.IsAny<ProcurementBudgetRevision>()))
            .ReturnsAsync((ProcurementBudgetRevision revision) => revision);
        _workflow.Setup(value => value.HasActiveApprovalWorkflowAsync("ProcurementBudgetRevision"))
            .ReturnsAsync(true);
    }

    [Fact]
    public async Task InitialBudgetDirectSubmissionCapturesPolicyWithoutHumanApproval()
    {
        var budget = ApprovedBudget(100_000m);
        budget.Status = "Draft";
        _budgets.Setup(value => value.GetByIdAsync(budget.Id)).ReturnsAsync(budget);
        _budgets.Setup(value => value.GetWithFullDetailsAsync(budget.Id)).ReturnsAsync(budget);
        _workflow.Setup(value => value.SubmitAsync("ProcurementBudget", budget.Id))
            .ReturnsAsync(new WorkflowIntegrationResult(
                new WorkflowExecutionResult { Success = true, Status = WorkflowInstanceStatus.Completed },
                WorkflowOutcome.Approved, approvalRequired: false));

        var result = await CreateService(_makerId).SubmitForApprovalAsync(budget.Id);

        result.Status.Should().Be("Approved");
        result.ApprovalRequired.Should().BeFalse();
        budget.ApprovalRequired.Should().BeFalse();
        budget.ApprovedById.Should().BeNull();
        budget.ApprovedDate.Should().BeNull();
        budget.AllocatedAmount.Should().Be(100_000m);
    }

    [Fact]
    public async Task ConfiguredWorkflowKeepsApprovedBudgetUnchangedUntilIndependentApproval()
    {
        var budget = ApprovedBudget(100_000m);
        ProcurementBudgetRevision? captured = null;
        _budgets.Setup(value => value.GetByIdAsync(budget.Id)).ReturnsAsync(budget);
        _revisions.Setup(value => value.AddAsync(It.IsAny<ProcurementBudgetRevision>()))
            .Callback<ProcurementBudgetRevision>(revision => captured = revision)
            .ReturnsAsync((ProcurementBudgetRevision revision) => revision);
        _workflow.Setup(value => value.SubmitAsync("ProcurementBudgetRevision", It.IsAny<Guid>()))
            .ReturnsAsync(WorkflowResult(WorkflowOutcome.Pending, WorkflowInstanceStatus.InProgress));

        var result = await CreateService(_makerId).CreateRevisionAsync(budget.Id, Revision(125_000m));

        result.Status.Should().Be("Pending");
        result.RequestedById.Should().Be(_makerId);
        captured.Should().NotBeNull();
        captured!.CreatedById.Should().Be(_makerId);
        budget.AllocatedAmount.Should().Be(100_000m);
        _budgets.Verify(value => value.UpdateAsync(It.IsAny<ProcurementBudget>()), Times.Never);
    }

    [Fact]
    public async Task NoActiveWorkflowAppliesRevisionWithoutClaimingHumanApproval()
    {
        var budget = ApprovedBudget(100_000m);
        _budgets.Setup(value => value.GetByIdAsync(budget.Id)).ReturnsAsync(budget);
        _workflow.Setup(value => value.SubmitAsync("ProcurementBudgetRevision", It.IsAny<Guid>()))
            .ReturnsAsync(new WorkflowIntegrationResult(
                new WorkflowExecutionResult { Success = true, Status = WorkflowInstanceStatus.Completed },
                WorkflowOutcome.Approved, approvalRequired: false));

        var result = await CreateService(_makerId).CreateRevisionAsync(budget.Id, Revision(125_000m));

        result.ApprovalRequired.Should().BeFalse();
        result.Status.Should().Be("Approved");
        result.ApprovedByName.Should().BeNull();
        result.ApprovedDate.Should().BeNull();
        result.RequestedById.Should().Be(_makerId);
        budget.AllocatedAmount.Should().Be(125_000m);
        _revisions.Verify(value => value.UpdateAsync(It.Is<ProcurementBudgetRevision>(revision =>
            !revision.ApprovalRequired && revision.ApprovedById == null && revision.ApprovedDate == null)), Times.Once);
        _budgets.Verify(value => value.UpdateAsync(budget), Times.Once);
    }

    [Fact]
    public async Task ImmediatelyCompletedActiveWorkflowCannotApplyRevision()
    {
        var transactionActive = false;
        _unitOfWork.SetupGet(value => value.HasActiveTransaction).Returns(() => transactionActive);
        _unitOfWork.Setup(value => value.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, It.IsAny<CancellationToken>()))
            .Callback(() => transactionActive = true).Returns(Task.CompletedTask);
        _unitOfWork.Setup(value => value.ExecuteInStrategyAsync(
                It.IsAny<Func<Task<ProcurementBudgetRevisionDto>>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<Task<ProcurementBudgetRevisionDto>> operation, CancellationToken _) => operation());
        _unitOfWork.Setup(value => value.RollbackAsync(It.IsAny<CancellationToken>()))
            .Callback(() => transactionActive = false).Returns(Task.CompletedTask);
        var budget = ApprovedBudget(100_000m);
        _budgets.Setup(value => value.GetByIdAsync(budget.Id)).ReturnsAsync(budget);
        _workflow.Setup(value => value.SubmitAsync("ProcurementBudgetRevision", It.IsAny<Guid>()))
            .ReturnsAsync(new WorkflowIntegrationResult(
                new WorkflowExecutionResult { Success = true, Status = WorkflowInstanceStatus.Completed },
                WorkflowOutcome.Approved,
                approvalRequired: true));

        var action = () => CreateService(_makerId).CreateRevisionAsync(budget.Id, Revision(125_000m));

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*must wait for independent approval*approved budget has not changed*");
        budget.AllocatedAmount.Should().Be(100_000m);
        budget.RemainingAmount.Should().Be(100_000m);
        _budgets.Verify(value => value.UpdateAsync(It.IsAny<ProcurementBudget>()), Times.Never);
        _revisions.Verify(value => value.UpdateAsync(It.IsAny<ProcurementBudgetRevision>()), Times.Never);
        _unitOfWork.Verify(value => value.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(value => value.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PendingRevisionPreventsASecondRequest()
    {
        var budget = ApprovedBudget(100_000m);
        _budgets.Setup(value => value.GetByIdAsync(budget.Id)).ReturnsAsync(budget);
        _revisions.Setup(value => value.GetByBudgetIdAsync(budget.Id))
            .ReturnsAsync(new[] { PendingRevision(budget, _makerId, 125_000m) });

        var action = () => CreateService(_makerId).CreateRevisionAsync(budget.Id, Revision(130_000m));

        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*still pending*");
        _revisions.Verify(value => value.AddAsync(It.IsAny<ProcurementBudgetRevision>()), Times.Never);
        budget.AllocatedAmount.Should().Be(100_000m);
    }

    [Fact]
    public async Task RevisionDecreaseCannotFallBelowLinkedPlanExposure()
    {
        var budget = ApprovedBudget(100_000m);
        _budgets.Setup(value => value.GetByIdAsync(budget.Id)).ReturnsAsync(budget);
        _plans.Setup(value => value.GetPlannedBudgetExposureAsync(budget.Id, null)).ReturnsAsync(80_000m);

        var action = () => CreateService(_makerId).CreateRevisionAsync(budget.Id, Revision(75_000m));

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*linked procurement-plan exposure*");
        _revisions.Verify(value => value.AddAsync(It.IsAny<ProcurementBudgetRevision>()), Times.Never);
    }

    [Fact]
    public async Task RevisionMakerCannotApproveOwnPendingRequest()
    {
        var budget = ApprovedBudget(100_000m);
        var revision = PendingRevision(budget, _makerId, 125_000m);
        _budgets.Setup(value => value.GetByIdAsync(budget.Id)).ReturnsAsync(budget);
        _revisions.Setup(value => value.GetByIdAsync(revision.Id)).ReturnsAsync(revision);

        var action = () => CreateService(_makerId).ApproveRevisionAsync(revision.Id, "self approve");

        await action.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*cannot approve or reject the same revision*");
        _workflow.Verify(value => value.ProcessApprovalAsync(
            It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task IndependentWorkflowApprovalAppliesAmountExactlyOnce()
    {
        var approverId = Guid.NewGuid();
        var budget = ApprovedBudget(100_000m);
        var revision = PendingRevision(budget, _makerId, 125_000m);
        _budgets.Setup(value => value.GetByIdAsync(budget.Id)).ReturnsAsync(budget);
        _revisions.Setup(value => value.GetByIdAsync(revision.Id)).ReturnsAsync(revision);
        _workflow.Setup(value => value.CanUserApproveAsync("ProcurementBudgetRevision", revision.Id, approverId))
            .ReturnsAsync(true);
        _workflow.Setup(value => value.ProcessApprovalAsync(
                "ProcurementBudgetRevision", revision.Id, approverId, "Approve", "within authority"))
            .ReturnsAsync(WorkflowResult(WorkflowOutcome.Approved, WorkflowInstanceStatus.Completed));

        var result = await CreateService(approverId).ApproveRevisionAsync(revision.Id, "within authority");

        result.Status.Should().Be("Approved");
        budget.AllocatedAmount.Should().Be(125_000m);
        revision.ApprovedById.Should().Be(approverId);
        _budgets.Verify(value => value.UpdateAsync(budget), Times.Once);

        var replay = () => CreateService(approverId).ApproveRevisionAsync(revision.Id, "replay");
        await replay.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Only a pending procurement budget revision*");
        _budgets.Verify(value => value.UpdateAsync(budget), Times.Once);
    }

    private ProcurementBudgetService CreateService(Guid userId)
    {
        var currentUser = new Mock<ICurrentUserProvider>();
        currentUser.SetupGet(value => value.UserId).Returns(userId);
        currentUser.SetupGet(value => value.TenantId).Returns(_tenantId);
        currentUser.SetupGet(value => value.Username).Returns($"user-{userId:N}");
        currentUser.SetupGet(value => value.FullName).Returns(userId == _makerId ? "Budget Maker" : "Budget Approver");

        return new ProcurementBudgetService(
            _budgets.Object,
            _allocations.Object,
            _revisions.Object,
            _plans.Object,
            _unitOfWork.Object,
            currentUser.Object,
            _workflow.Object,
            _adapters,
            NullLogger<ProcurementBudgetService>.Instance);
    }

    private ProcurementBudget ApprovedBudget(decimal amount) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = _tenantId,
        BudgetCode = "PB-2026-TEST",
        Title = "Approved procurement budget",
        DepartmentId = Guid.NewGuid(),
        FiscalYear = 2026,
        Currency = "GHS",
        Status = "Approved",
        AllocatedAmount = amount,
        RemainingAmount = amount
    };

    private static ProcurementBudgetRevision PendingRevision(
        ProcurementBudget budget,
        Guid makerId,
        decimal newAmount) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = budget.TenantId,
        ProcurementBudgetId = budget.Id,
        RevisionNumber = 1,
        RevisionType = "Increase",
        PreviousAmount = budget.AllocatedAmount,
        NewAmount = newAmount,
        ChangeAmount = newAmount - budget.AllocatedAmount,
        Reason = "Additional approved scope",
        Status = "Pending",
        CreatedById = makerId,
        CreatedBy = "Budget Maker"
    };

    private static CreateProcurementBudgetRevisionDto Revision(decimal newAmount) => new()
    {
        NewAmount = newAmount,
        Reason = "Additional approved scope"
    };

    private static WorkflowIntegrationResult WorkflowResult(
        WorkflowOutcome outcome,
        WorkflowInstanceStatus status) => new(
        new WorkflowExecutionResult { Success = true, Status = status },
        outcome);
}
