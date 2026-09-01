using System.Data;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementPlanNumberReservationTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Mock<IProcurementPlanRepository> _plans = new();
    private readonly Mock<IProcurementPlanItemRepository> _items = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private bool _transactionActive;
    private ProcurementPlan? _createdPlan;

    public ProcurementPlanNumberReservationTests()
    {
        _unitOfWork.SetupGet(value => value.HasActiveTransaction)
            .Returns(() => _transactionActive);
        _unitOfWork
            .Setup(value => value.ExecuteInStrategyAsync(
                It.IsAny<Func<Task<ProcurementPlan>>>(),
                It.IsAny<CancellationToken>()))
            .Returns((Func<Task<ProcurementPlan>> operation, CancellationToken _) => operation());
        _unitOfWork
            .Setup(value => value.BeginTransactionAsync(
                It.IsAny<IsolationLevel>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => _transactionActive = true)
            .Returns(Task.CompletedTask);
        _unitOfWork
            .Setup(value => value.AcquireTransactionLockAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWork
            .Setup(value => value.CommitAsync(It.IsAny<CancellationToken>()))
            .Callback(() => _transactionActive = false)
            .Returns(Task.CompletedTask);
        _unitOfWork
            .Setup(value => value.RollbackAsync(It.IsAny<CancellationToken>()))
            .Callback(() => _transactionActive = false)
            .Returns(Task.CompletedTask);

        _plans.Setup(value => value.GeneratePlanNumberAsync(2026))
            .ReturnsAsync("PP-2026-0010");
        _plans.Setup(value => value.AddAsync(It.IsAny<ProcurementPlan>()))
            .Callback<ProcurementPlan>(plan => _createdPlan = plan)
            .ReturnsAsync((ProcurementPlan plan) => plan);
        _plans.Setup(value => value.GetWithFullDetailsAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => _createdPlan?.Id == id ? _createdPlan : null);
    }

    [Fact]
    public async Task CreateHoldsFiscalYearNumberLockUntilPlanIsCommitted()
    {
        var departmentId = Guid.NewGuid();

        var result = await CreateService().CreateAsync(new CreateProcurementPlanDto
        {
            Title = "Annual procurement plan",
            DepartmentId = departmentId,
            FiscalYear = 2026,
            PlanningCycle = "Annual",
            PlanStartDate = new DateTime(2026, 1, 1),
            PlanEndDate = new DateTime(2026, 12, 31),
            PlanDurationYears = 1,
            Currency = "GHS"
        });

        result.PlanNumber.Should().Be("PP-2026-0010");
        _unitOfWork.Verify(value => value.BeginTransactionAsync(
            IsolationLevel.Serializable,
            It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(value => value.ExecuteInStrategyAsync(
            It.IsAny<Func<Task<ProcurementPlan>>>(),
            It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(value => value.AcquireTransactionLockAsync(
            "PROCUREMENT_PLAN_NUMBER:2026",
            It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(value => value.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExistingCallerTransactionIsJoinedWithoutStartingRetryStrategy()
    {
        _transactionActive = true;

        var result = await CreateService().CreateAsync(new CreateProcurementPlanDto
        {
            Title = "Caller-owned transaction plan",
            DepartmentId = Guid.NewGuid(),
            FiscalYear = 2026,
            PlanningCycle = "Annual",
            PlanStartDate = new DateTime(2026, 1, 1),
            PlanEndDate = new DateTime(2026, 12, 31),
            PlanDurationYears = 1,
            Currency = "GHS"
        });

        result.PlanNumber.Should().Be("PP-2026-0010");
        _unitOfWork.Verify(value => value.ExecuteInStrategyAsync(
            It.IsAny<Func<Task<ProcurementPlan>>>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(value => value.BeginTransactionAsync(
            It.IsAny<IsolationLevel>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(value => value.AcquireTransactionLockAsync(
            "PROCUREMENT_PLAN_NUMBER:2026",
            It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(value => value.SaveChangesAsync(
            It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(value => value.CommitAsync(
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UncertainCommitRetryReturnsFirstCommittedPlanWithoutDuplicateInsert()
    {
        var commitAttempts = 0;
        var strategyAttempts = 0;
        var firstCommitReachedDatabase = false;

        _plans.Setup(value => value.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) =>
                firstCommitReachedDatabase && _createdPlan?.Id == id
                    ? _createdPlan
                    : null);
        _unitOfWork
            .Setup(value => value.ExecuteInStrategyAsync(
                It.IsAny<Func<Task<ProcurementPlan>>>(),
                It.IsAny<CancellationToken>()))
            .Returns(async (Func<Task<ProcurementPlan>> operation, CancellationToken _) =>
            {
                try
                {
                    strategyAttempts++;
                    return await operation();
                }
                catch (TimeoutException)
                {
                    strategyAttempts++;
                    return await operation();
                }
            });
        _unitOfWork
            .Setup(value => value.CommitAsync(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                commitAttempts++;
                _transactionActive = false;
                if (commitAttempts == 1)
                {
                    firstCommitReachedDatabase = true;
                    throw new TimeoutException("Commit acknowledgement was lost.");
                }

                return Task.CompletedTask;
            });

        var result = await CreateService().CreateAsync(new CreateProcurementPlanDto
        {
            Title = "Retry-safe annual plan",
            DepartmentId = Guid.NewGuid(),
            FiscalYear = 2026,
            PlanningCycle = "Annual",
            PlanStartDate = new DateTime(2026, 1, 1),
            PlanEndDate = new DateTime(2026, 12, 31),
            PlanDurationYears = 1,
            Currency = "GHS"
        });

        result.PlanNumber.Should().Be("PP-2026-0010");
        strategyAttempts.Should().Be(2);
        commitAttempts.Should().Be(2);
        _plans.Verify(value => value.AddAsync(It.IsAny<ProcurementPlan>()), Times.Once);
        _unitOfWork.Verify(value => value.BeginTransactionAsync(
            IsolationLevel.Serializable,
            It.IsAny<CancellationToken>()), Times.Exactly(2));
        _unitOfWork.Verify(value => value.AcquireTransactionLockAsync(
            "PROCUREMENT_PLAN_NUMBER:2026",
            It.IsAny<CancellationToken>()), Times.Exactly(2));
        _unitOfWork.Verify(value => value.ClearTrackedChanges(), Times.Once);
    }

    [Fact]
    public async Task AmendmentUsesSameReservedNumberPathAndRetainsSourceLineage()
    {
        var source = new ProcurementPlan
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            DepartmentId = Guid.NewGuid(),
            FiscalYear = 2026,
            PlanNumber = "PP-2026-0008",
            Title = "Published procurement plan",
            Status = "Active",
            Currency = "GHS",
            PlanStartDate = new DateTime(2026, 1, 1),
            PlanEndDate = new DateTime(2026, 12, 31),
            PlanDurationYears = 1,
            RevisionNumber = 1
        };
        _plans.Setup(value => value.GetWithFullDetailsAsync(source.Id)).ReturnsAsync(source);

        var result = await CreateService().CreateAmendmentAsync(source.Id, new CreateProcurementPlanAmendmentDto
        {
            Reason = "Rephase approved delivery dates"
        });

        result.PlanNumber.Should().Be("PP-2026-0010");
        result.PreviousVersionId.Should().Be(source.Id);
        result.RevisionNumber.Should().Be(2);
        _unitOfWork.Verify(value => value.AcquireTransactionLockAsync(
            "PROCUREMENT_PLAN_NUMBER:2026",
            It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(value => value.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(value => value.ExecuteInStrategyAsync(
            It.IsAny<Func<Task<ProcurementPlan>>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private ProcurementPlanService CreateService()
    {
        var currentUser = new Mock<ICurrentUserProvider>();
        currentUser.SetupGet(value => value.TenantId).Returns(_tenantId);
        currentUser.SetupGet(value => value.UserId).Returns(_userId);

        return new ProcurementPlanService(
            _plans.Object,
            _items.Object,
            Mock.Of<IProcurementPlanItemSupplierRepository>(),
            Mock.Of<ITenderService>(),
            Mock.Of<IPurchaseOrderRepository>(),
            Mock.Of<IPurchaseOrderItemRepository>(),
            Mock.Of<IProcurementScheduleService>(),
            Mock.Of<IProcurementBudgetRepository>(),
            Mock.Of<IProcurementBudgetService>(),
            Mock.Of<IWorkflowIntegrationService>(),
            Mock.Of<IWorkflowStatusAdapterRegistry>(),
            Mock.Of<IMarketAnalysisRepository>(),
            Mock.Of<IBusinessPartnerRepository>(),
            Mock.Of<ISupplierReportingService>(),
            Mock.Of<IProcurementPurchaseOrderSourceService>(),
            _unitOfWork.Object,
            currentUser.Object,
            NullLogger<ProcurementPlanService>.Instance);
    }
}
