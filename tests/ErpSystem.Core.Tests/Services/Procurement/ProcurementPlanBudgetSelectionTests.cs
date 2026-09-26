using System.Linq.Expressions;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementPlanBudgetSelectionTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _organizationUnitId = Guid.NewGuid();
    private readonly Mock<IProcurementPlanRepository> _plans = new();
    private readonly Mock<IProcurementPlanItemRepository> _items = new();
    private readonly Mock<IProcurementBudgetRepository> _budgets = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    [Fact]
    public async Task UpdateDraftPlanLinksSelectedApprovedBudgetAndInheritsCurrency()
    {
        var plan = DraftPlan();
        var budget = ApprovedBudget(plan.OrganizationUnitId!.Value, plan.FiscalYear);
        _plans.Setup(value => value.GetByIdAsync(plan.Id)).ReturnsAsync(plan);
        _plans.Setup(value => value.GetWithFullDetailsAsync(plan.Id)).ReturnsAsync(plan);
        _plans.Setup(value => value.UpdateAsync(plan)).Returns(Task.CompletedTask);
        _budgets.Setup(value => value.GetByPlanIdAsync(plan.Id))
            .ReturnsAsync(Array.Empty<ProcurementBudget>());
        _budgets.Setup(value => value.GetByIdAsync(budget.Id)).ReturnsAsync(budget);
        _plans.Setup(value => value.GetPlannedBudgetExposureAsync(budget.Id, plan.Id)).ReturnsAsync(0m);
        _items.Setup(value => value.GetByPlanIdAsync(plan.Id))
            .ReturnsAsync(Array.Empty<ProcurementPlanItem>());
        _unitOfWork.Setup(value => value.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await CreateService().UpdateAsync(plan.Id, UpdateFor(plan, budget.Id));

        plan.BudgetId.Should().Be(budget.Id);
        plan.Currency.Should().Be("GHS");
        result.BudgetId.Should().Be(budget.Id);
        result.Currency.Should().Be("GHS");
        _budgets.Verify(value => value.UpdateAsync(It.IsAny<ProcurementBudget>()), Times.Never);
        _unitOfWork.Verify(value => value.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateDraftPlanAllowsBudgetUsedByAnotherPlanWhenCapacityIsSufficient()
    {
        var plan = DraftPlan();
        var budget = ApprovedBudget(plan.OrganizationUnitId!.Value, plan.FiscalYear);
        budget.ProcurementPlanId = Guid.NewGuid();
        _plans.Setup(value => value.GetByIdAsync(plan.Id)).ReturnsAsync(plan);
        _plans.Setup(value => value.GetWithFullDetailsAsync(plan.Id)).ReturnsAsync(plan);
        _plans.Setup(value => value.UpdateAsync(plan)).Returns(Task.CompletedTask);
        _plans.Setup(value => value.GetPlannedBudgetExposureAsync(budget.Id, plan.Id)).ReturnsAsync(20_000m);
        _budgets.Setup(value => value.GetByPlanIdAsync(plan.Id))
            .ReturnsAsync(Array.Empty<ProcurementBudget>());
        _budgets.Setup(value => value.GetByIdAsync(budget.Id)).ReturnsAsync(budget);
        _items.Setup(value => value.GetByPlanIdAsync(plan.Id))
            .ReturnsAsync(Array.Empty<ProcurementPlanItem>());
        _unitOfWork.Setup(value => value.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await CreateService().UpdateAsync(plan.Id, UpdateFor(plan, budget.Id));

        result.BudgetId.Should().Be(budget.Id);
        plan.BudgetId.Should().Be(budget.Id);
        _plans.Verify(value => value.UpdateAsync(plan), Times.Once);
        _budgets.Verify(value => value.UpdateAsync(It.IsAny<ProcurementBudget>()), Times.Never);
    }

    [Fact]
    public async Task UpdateDraftPlanRejectsBudgetWhenOtherPlansExhaustPlanningCapacity()
    {
        var plan = DraftPlan();
        var budget = ApprovedBudget(plan.OrganizationUnitId!.Value, plan.FiscalYear);
        _plans.Setup(value => value.GetByIdAsync(plan.Id)).ReturnsAsync(plan);
        _plans.Setup(value => value.GetPlannedBudgetExposureAsync(budget.Id, plan.Id)).ReturnsAsync(30_000m);
        _budgets.Setup(value => value.GetByPlanIdAsync(plan.Id)).ReturnsAsync(Array.Empty<ProcurementBudget>());
        _budgets.Setup(value => value.GetByIdAsync(budget.Id)).ReturnsAsync(budget);

        var action = () => CreateService().UpdateAsync(plan.Id, UpdateFor(plan, budget.Id));

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*available for procurement planning*");
        _plans.Verify(value => value.UpdateAsync(It.IsAny<ProcurementPlan>()), Times.Never);
    }

    [Fact]
    public async Task AddItemUsesControlledAllocationFromLinkedBudget()
    {
        var plan = DraftPlan();
        plan.TotalEstimatedBudget = 20_000m;
        var budget = ApprovedBudget(plan.OrganizationUnitId!.Value, plan.FiscalYear);
        var allocation = new ProcurementBudgetAllocation
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            ProcurementBudgetId = budget.Id,
            CategoryName = "IT Equipment",
            AllocatedAmount = 15_000m,
            UtilizedAmount = 1_000m
        };
        budget.Allocations.Add(allocation);
        plan.BudgetId = budget.Id;
        plan.Currency = budget.Currency;

        ProcurementPlanItem? addedItem = null;
        _plans.Setup(value => value.GetByIdAsync(plan.Id)).ReturnsAsync(plan);
        _budgets.Setup(value => value.GetWithAllocationsAsync(budget.Id)).ReturnsAsync(budget);
        _items.Setup(value => value.GetByPlanIdAsync(plan.Id)).ReturnsAsync(Array.Empty<ProcurementPlanItem>());
        _items.Setup(value => value.GetPlannedBudgetExposureByAllocationAsync(allocation.Id, null)).ReturnsAsync(2_000m);
        _items.Setup(value => value.AddAsync(It.IsAny<ProcurementPlanItem>()))
            .Callback<ProcurementPlanItem>(value => addedItem = value)
            .ReturnsAsync((ProcurementPlanItem value) => value);
        _unitOfWork.Setup(value => value.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        await CreateService().AddItemAsync(plan.Id, new CreateProcurementPlanItemDto
        {
            ProcurementBudgetId = budget.Id,
            ProcurementBudgetAllocationId = allocation.Id,
            BudgetCategoryName = "untrusted free text",
            ItemDescription = "Wireless Keyboard",
            EstimatedQuantity = 10,
            EstimatedUnitPrice = 490,
            UnitOfMeasure = "EA"
        });

        addedItem.Should().NotBeNull();
        addedItem!.ProcurementBudgetId.Should().Be(budget.Id);
        addedItem.ProcurementBudgetAllocationId.Should().Be(allocation.Id);
        addedItem.BudgetCategoryName.Should().Be("IT Equipment");
        addedItem.ApprovedBudgetAmount.Should().Be(4_900m);
    }

    [Fact]
    public async Task AddItemRejectsAllocationOutsideLinkedBudget()
    {
        var plan = DraftPlan();
        plan.TotalEstimatedBudget = 20_000m;
        var budget = ApprovedBudget(plan.OrganizationUnitId!.Value, plan.FiscalYear);
        budget.Allocations.Add(new ProcurementBudgetAllocation
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            ProcurementBudgetId = budget.Id,
            CategoryName = "IT Equipment",
            AllocatedAmount = 15_000m
        });
        plan.BudgetId = budget.Id;

        _plans.Setup(value => value.GetByIdAsync(plan.Id)).ReturnsAsync(plan);
        _budgets.Setup(value => value.GetWithAllocationsAsync(budget.Id)).ReturnsAsync(budget);
        _items.Setup(value => value.GetByPlanIdAsync(plan.Id)).ReturnsAsync(Array.Empty<ProcurementPlanItem>());

        var action = () => CreateService().AddItemAsync(plan.Id, new CreateProcurementPlanItemDto
        {
            ProcurementBudgetId = budget.Id,
            ProcurementBudgetAllocationId = Guid.NewGuid(),
            ItemDescription = "Wireless Keyboard",
            EstimatedQuantity = 10,
            EstimatedUnitPrice = 490,
            UnitOfMeasure = "EA"
        });

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not part of the approved budget linked to this plan*");
        _items.Verify(value => value.AddAsync(It.IsAny<ProcurementPlanItem>()), Times.Never);
    }

    [Fact]
    public async Task AddItemRejectsExposureAbovePlanTotalBudget()
    {
        var plan = DraftPlan();
        plan.TotalEstimatedBudget = 600m;
        _plans.Setup(value => value.GetByIdAsync(plan.Id)).ReturnsAsync(plan);
        _items.Setup(value => value.GetByPlanIdAsync(plan.Id)).ReturnsAsync(Array.Empty<ProcurementPlanItem>());

        var action = () => CreateService().AddItemAsync(plan.Id, new CreateProcurementPlanItemDto
        {
            ItemDescription = "Wireless Keyboard",
            EstimatedQuantity = 10,
            EstimatedUnitPrice = 490,
            UnitOfMeasure = "EA"
        });

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*exceeds the plan total budget*");
        _items.Verify(value => value.AddAsync(It.IsAny<ProcurementPlanItem>()), Times.Never);
    }

    private ProcurementPlanService CreateService()
    {
        var currentUser = new Mock<ICurrentUserProvider>();
        currentUser.SetupGet(value => value.TenantId).Returns(_tenantId);
        var organizationUnits = new Mock<IGenericRepository<OrganizationUnit>>();
        var activeUnit = new OrganizationUnit
        {
            Id = _organizationUnitId,
            TenantId = _tenantId,
            Code = "FIN",
            Name = "Finance",
            IsActive = true
        };
        organizationUnits
            .Setup(value => value.GetQueryable(It.IsAny<Expression<Func<OrganizationUnit, bool>>>()))
            .Returns((Expression<Func<OrganizationUnit, bool>> predicate) =>
                new[] { activeUnit }.Where(predicate.Compile()).AsAsyncQueryable());
        _unitOfWork.Setup(value => value.Repository<OrganizationUnit>()).Returns(organizationUnits.Object);

        return new ProcurementPlanService(
            _plans.Object,
            _items.Object,
            Mock.Of<IProcurementPlanItemSupplierRepository>(),
            Mock.Of<ITenderService>(),
            Mock.Of<IPurchaseOrderRepository>(),
            Mock.Of<IPurchaseOrderItemRepository>(),
            Mock.Of<IProcurementScheduleService>(),
            _budgets.Object,
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

    private ProcurementPlan DraftPlan() => new()
    {
        Id = Guid.NewGuid(),
        TenantId = _tenantId,
        OrganizationUnitId = _organizationUnitId,
        FiscalYear = 2026,
        PlanNumber = "PP-2026-TEST",
        Title = "Draft plan",
        Status = "Draft",
        Currency = "USD",
        PlanStartDate = new DateTime(2026, 1, 1),
        PlanEndDate = new DateTime(2026, 12, 31),
        PlanDurationYears = 1
    };

    private ProcurementBudget ApprovedBudget(Guid organizationUnitId, int fiscalYear) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = _tenantId,
        OrganizationUnitId = organizationUnitId,
        FiscalYear = fiscalYear,
        BudgetCode = "PB-2026-TEST",
        Title = "Approved budget",
        Status = "Approved",
        Currency = "GHS",
        AllocatedAmount = 100_000m
    };

    private static UpdateProcurementPlanDto UpdateFor(ProcurementPlan plan, Guid budgetId) => new()
    {
        Title = plan.Title,
        OrganizationUnitId = plan.OrganizationUnitId!.Value,
        FiscalYear = plan.FiscalYear,
        PlanningCycle = "Annual",
        PlanStartDate = plan.PlanStartDate,
        PlanEndDate = plan.PlanEndDate,
        PlanDurationYears = 1,
        TotalEstimatedBudget = 75_000m,
        BudgetId = budgetId,
        Currency = plan.Currency
    };
}
