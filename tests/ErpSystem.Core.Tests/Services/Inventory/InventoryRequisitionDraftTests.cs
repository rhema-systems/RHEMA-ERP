using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Interfaces.Projects;
using ErpSystem.Core.Services.Inventory;
using ErpSystem.Data;
using ErpSystem.Data.Repositories.Inventory;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Inventory;

public sealed class InventoryRequisitionDraftTests : IDisposable
{
    private readonly ApplicationDbContext _db = new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);
    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _actor = Guid.NewGuid();
    private readonly Guid _location = Guid.NewGuid();
    private readonly Warehouse _warehouse = new() { Code = "DRAFT-WH", Name = "Draft test warehouse" };
    private readonly InventoryItem _item = new()
    {
        ItemCode = "DRAFT-PVC", Name = "Draft PVC", UnitOfMeasure = "EACH",
        ValuationMethod = ValuationMethod.FIFO, AverageCost = 0, StandardCost = 2000, LastPurchaseCost = 1900
    };

    [Theory]
    [InlineData(ValuationMethod.FIFO, 1900)]
    [InlineData(ValuationMethod.LIFO, 2100)]
    [InlineData(ValuationMethod.WeightedAverage, 2000)]
    [InlineData(ValuationMethod.StandardCost, 2000)]
    public async Task Create_persists_aggregates_and_uses_configured_scoped_cost_without_consuming_stock(
        ValuationMethod method, decimal expectedCost)
    {
        var service = await Setup(method);
        var result = await service.CreateAsync(Request(2));
        _db.ChangeTracker.Clear();
        var saved = await _db.Set<InventoryRequisition>().SingleAsync();
        saved.TotalItems.Should().Be(1);
        saved.TotalQuantity.Should().Be(2);
        saved.TotalValue.Should().Be(expectedCost * 2);
        result.Items.Should().ContainSingle().Which.UnitCost.Should().Be(expectedCost);
        (await _db.Set<InventoryLayer>().Where(x => x.TenantId == _tenant && x.LocationId == _location)
            .SumAsync(x => x.RemainingQuantity)).Should().Be(4);
        (await _db.Set<InventoryMovement>().CountAsync()).Should().Be(0);
        (await _db.Set<InventoryIssueVoucher>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Add_update_remove_merge_unsaved_changes_into_persisted_totals()
    {
        var service = await Setup(ValuationMethod.FIFO);
        var created = await service.CreateAsync(Request(0));
        var added = await service.AddItemAsync(created.Id, new AddRequisitionItemDto
        { InventoryItemId = _item.Id, RequestedQuantity = 2, LocationId = _location });
        _db.ChangeTracker.Clear();
        (await _db.Set<InventoryRequisition>().SingleAsync()).TotalValue.Should().Be(3800);

        var updated = await service.UpdateItemAsync(created.Id, added.Id,
            new UpdateRequisitionItemDto { RequestedQuantity = 4, LocationId = _location });
        updated.UnitCost.Should().Be(2000);
        _db.ChangeTracker.Clear();
        var saved = await _db.Set<InventoryRequisition>().SingleAsync();
        saved.TotalItems.Should().Be(1);
        saved.TotalQuantity.Should().Be(4);
        saved.TotalValue.Should().Be(8000);

        await service.RemoveItemAsync(created.Id, added.Id);
        _db.ChangeTracker.Clear();
        saved = await _db.Set<InventoryRequisition>().SingleAsync();
        saved.TotalItems.Should().Be(0);
        saved.TotalQuantity.Should().Be(0);
        saved.TotalValue.Should().Be(0);
        (await service.GetByIdAsync(created.Id))!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Reads_reconcile_legacy_header_without_rewriting_approved_record()
    {
        var service = await Setup(ValuationMethod.FIFO);
        var result = await service.CreateAsync(Request(2));
        var saved = await _db.Set<InventoryRequisition>().SingleAsync();
        saved.TotalItems = 0;
        saved.TotalQuantity = 0;
        saved.TotalValue = 0;
        saved.Status = RequisitionStatus.Approved;
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var list = await service.GetAllAsync();
        list.Should().ContainSingle().Which.TotalItems.Should().Be(1);
        (await service.GetPendingIssueAsync()).Single().TotalQuantity.Should().Be(2);
        (await service.GetByIdAsync(result.Id))!.TotalValue.Should().Be(3800);
        _db.ChangeTracker.Clear();
        (await _db.Set<InventoryRequisition>().SingleAsync()).TotalItems.Should().Be(0);
    }

    [Fact]
    public async Task Approved_lines_cannot_be_repriced_through_draft_edit()
    {
        var service = await Setup(ValuationMethod.FIFO);
        var result = await service.CreateAsync(Request(2));
        var saved = await _db.Set<InventoryRequisition>().SingleAsync();
        saved.Status = RequisitionStatus.Approved;
        await _db.SaveChangesAsync();
        var action = () => service.UpdateItemAsync(saved.Id, result.Items.Single().Id,
            new UpdateRequisitionItemDto { RequestedQuantity = 4 });
        await action.Should().ThrowAsync<InvalidOperationException>();
        (await _db.Set<InventoryRequisitionItem>().SingleAsync()).UnitCost.Should().Be(1900);
    }

    private CreateInventoryRequisitionDto Request(decimal quantity) => new()
    {
        DepartmentId = Guid.NewGuid(), DepartmentName = "Operations", WarehouseId = _warehouse.Id,
        LocationId = _location, CostCenter = "Operations", Purpose = "Automated test only",
        Items = quantity == 0 ? [] : [new() { InventoryItemId = _item.Id, RequestedQuantity = quantity, LocationId = _location }]
    };

    private async Task<InventoryRequisitionService> Setup(ValuationMethod method)
    {
        _warehouse.TenantId = _tenant;
        _item.TenantId = _tenant;
        _item.ValuationMethod = method;
        var user = new ApplicationUser { Id = _actor, TenantId = _tenant, UserName = "requester", FirstName = "Test", LastName = "Requester" };
        _db.AddRange(user, _warehouse, _item,
            Layer(_tenant, _location, 1900, -2), Layer(_tenant, _location, 2100, -1),
            Layer(Guid.NewGuid(), _location, 1, -10), Layer(_tenant, Guid.NewGuid(), 1, -10),
            new InventoryBalance { TenantId = _tenant, InventoryItemId = _item.Id, WarehouseId = _warehouse.Id,
                LocationId = _location, QuantityOnHand = 4, TotalValue = 8000, AverageUnitCost = 2000 });
        await _db.SaveChangesAsync();
        var current = new Mock<ICurrentUserProvider>();
        current.SetupGet(x => x.UserId).Returns(_actor);
        current.SetupGet(x => x.TenantId).Returns(_tenant);
        var items = new Mock<IInventoryItemRepository>();
        items.Setup(x => x.GetByIdAsync(_item.Id)).ReturnsAsync(_item);
        var warehouses = new Mock<IWarehouseRepository>();
        warehouses.Setup(x => x.GetByIdAsync(_warehouse.Id)).ReturnsAsync(_warehouse);
        var quantities = new Mock<IWarehouseQuantityRepository>();
        quantities.Setup(x => x.GetByWarehouseAndItemAsync(_warehouse.Id, _item.Id))
            .ReturnsAsync(new WarehouseQuantity { TenantId = _tenant, AverageCost = 1900 });
        return new InventoryRequisitionService(
            new InventoryRequisitionRepository(_db), new InventoryRequisitionItemRepository(_db),
            items.Object, warehouses.Object, Mock.Of<IWarehouseLocationRepository>(), quantities.Object,
            Mock.Of<IStockMovementRepository>(), Mock.Of<IConsignmentSettlementService>(),
            Mock.Of<IProjectRepository>(), Mock.Of<IProjectService>(), new UnitOfWork(_db), current.Object,
            Mock.Of<IWorkflowIntegrationService>(), Mock.Of<IWorkflowStatusAdapterRegistry>(),
            Mock.Of<IInventoryTrackingControlService>(), Mock.Of<IInventoryNegativeStockControlService>(),
            Mock.Of<IInventoryProjectReservationService>(), Mock.Of<IProcurementAccessControlService>(),
            Mock.Of<IProcurementControlEventService>(), Mock.Of<IInventoryReturnControlService>(),
            Mock.Of<IInventoryIssueFinanceAssetService>(), Mock.Of<IInventoryValuationService>(),
            NullLogger<InventoryRequisitionService>.Instance);
    }

    private InventoryLayer Layer(Guid tenant, Guid location, decimal cost, int day) => new()
    {
        TenantId = tenant, InventoryItemId = _item.Id, WarehouseId = _warehouse.Id, LocationId = location,
        LayerNumber = Guid.NewGuid().ToString("N"), LayerDate = DateTime.UtcNow.Date.AddDays(day),
        OriginalQuantity = 2, RemainingQuantity = 2, UnitCost = cost, RemainingValue = 2 * cost
    };

    public void Dispose() => _db.Dispose();
}
