using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using System.Linq.Expressions;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementPlanBudgetSelectionTests : IDisposable
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _organizationUnitId = Guid.NewGuid();
    private readonly ApplicationDbContext _organizations = new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);
    private readonly Mock<IProcurementPlanRepository> _plans = new();
    private readonly Mock<IProcurementPlanItemRepository> _items = new();
    private readonly Mock<IProcurementBudgetRepository> _budgets = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    public ProcurementPlanBudgetSelectionTests()
    {
        _organizations.Set<OrganizationUnit>().Add(new OrganizationUnit
        {
            Id = _organizationUnitId, TenantId = _tenantId, Name = "Procurement", Code = "PROC", IsActive = true
        });
        _organizations.SaveChanges();
        var repository = new Mock<IGenericRepository<OrganizationUnit>>();
        repository.Setup(value => value.GetQueryable(It.IsAny<Expression<Func<OrganizationUnit, bool>>>()))
            .Returns((Expression<Func<OrganizationUnit, bool>> predicate) => _organizations.Set<OrganizationUnit>().Where(predicate));
        _unitOfWork.Setup(value => value.Repository<OrganizationUnit>()).Returns(repository.Object);
    }

    public void Dispose() => _organizations.Dispose();

    [Fact]
    public void PlanLineReferenceMigrationBackfillsWithoutRewritingBudgetRelationships()
    {
        using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=NeverConnected;Integrated Security=True").Options);
        var entity = context.Model.FindEntityType(typeof(ProcurementPlanItem))!;
        var reference = entity.FindProperty(nameof(ProcurementPlanItem.ReferenceNumber))!;
        reference.GetMaxLength().Should().Be(36);
        reference.GetComputedColumnSql().Should().Contain("[Id]");
        reference.GetIsStored().Should().BeTrue();
        entity.GetIndexes().Should().Contain(index => index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(new[] { "TenantId", "ReferenceNumber" }));

        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        var migration = new ErpSystem.Data.Migrations.ProcurementPlanLineReference();
        migration.GetType().GetMethod("Up", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(migration, new object[] { builder });
        builder.Operations.Should().HaveCount(2);
        var column = builder.Operations.OfType<AddColumnOperation>().Single();
        column.Name.Should().Be("ReferenceNumber");
        column.ComputedColumnSql.Should().Be(reference.GetComputedColumnSql());
        column.IsStored.Should().BeTrue();
        var indexSql = builder.Operations.OfType<SqlOperation>().Single().Sql;
        indexSql.Should().Contain("CREATE UNIQUE INDEX [IX_ProcurementPlanItems_TenantId_ReferenceNumber]");
        indexSql.Should().NotContain("WHERE"); // SQL Server cannot filter an index on a computed column.
        context.Database.GetDbConnection().State.Should().Be(System.Data.ConnectionState.Closed);
    }

    [Fact]
    public async Task UpdateDraftPlanLinksSelectedApprovedBudgetAndInheritsCurrency()
    {
        var plan = DraftPlan();
        var budget = ApprovedBudget(plan.DepartmentId, plan.FiscalYear);
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
        var budget = ApprovedBudget(plan.DepartmentId, plan.FiscalYear);
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
        var budget = ApprovedBudget(plan.DepartmentId, plan.FiscalYear);
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
        var budget = ApprovedBudget(plan.DepartmentId, plan.FiscalYear);
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
        var budget = ApprovedBudget(plan.DepartmentId, plan.FiscalYear);
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
        DepartmentId = Guid.NewGuid(),
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

    private ProcurementBudget ApprovedBudget(Guid? departmentId, int fiscalYear) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = _tenantId,
        DepartmentId = departmentId,
        OrganizationUnitId = _organizationUnitId,
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
        DepartmentId = plan.DepartmentId,
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
