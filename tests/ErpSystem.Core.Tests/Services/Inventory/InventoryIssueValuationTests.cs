using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Inventory;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Inventory;

public sealed class InventoryIssueValuationTests : IDisposable
{
    private readonly ApplicationDbContext _db = new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);
    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _warehouse = Guid.NewGuid();
    private readonly Guid _location = Guid.NewGuid();
    private readonly InventoryItem _item = new() { ItemCode = "ISSUE-PVC", Name = "Issue PVC", StandardCost = 2000m };

    [Theory]
    [InlineData(ValuationMethod.FIFO, 3800, 34200)]
    [InlineData(ValuationMethod.WeightedAverage, 3800, 34200)]
    [InlineData(ValuationMethod.StandardCost, 4000, 36000)]
    public async Task Issue_uses_configured_cost_and_records_closing_balances_once(
        ValuationMethod method, decimal value, decimal closingValue)
    {
        var service = await Setup(method);
        (await Issue(service, 2)).Should().Be(value);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        var balance = await _db.Set<InventoryBalance>().SingleAsync();
        balance.QuantityOnHand.Should().Be(18);
        balance.QuantityAvailable.Should().Be(18);
        balance.TotalValue.Should().Be(closingValue);
        var movement = await _db.Set<InventoryMovement>().SingleAsync();
        movement.RunningBalance.Should().Be(18);
        movement.RunningValue.Should().Be(closingValue);
        movement.TotalValue.Should().Be(value);
        if (method == ValuationMethod.FIFO)
            (await _db.Set<InventoryLayer>().SingleAsync()).RemainingQuantity.Should().Be(18);
    }

    [Fact]
    public async Task Fifo_does_not_consume_other_tenant_or_location_layers()
    {
        var service = await Setup(ValuationMethod.FIFO);
        var ownLayer = await _db.Set<InventoryLayer>().SingleAsync();
        ownLayer.RemainingQuantity = 1;
        ownLayer.RemainingValue = 1900;
        _db.AddRange(Layer(Guid.NewGuid(), _location), Layer(_tenant, Guid.NewGuid()), Layer(_tenant, null));
        await _db.SaveChangesAsync();
        var action = () => Issue(service, 2);
        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*selected stock location*");
        ownLayer.RemainingQuantity.Should().Be(1, "insufficient layers must fail before tracked mutation");
        (await _db.Set<InventoryBalance>().SingleAsync()).QuantityOnHand.Should().Be(20);
        _db.ChangeTracker.Entries<InventoryMovement>().Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Non_positive_quantity_does_not_mutate_stock(decimal quantity)
    {
        var service = await Setup(ValuationMethod.FIFO);
        var action = () => Issue(service, quantity);
        await action.Should().ThrowAsync<ArgumentOutOfRangeException>();
        (await _db.Set<InventoryBalance>().SingleAsync()).QuantityOnHand.Should().Be(20);
    }

    [Theory]
    [InlineData(ValuationMethod.LIFO)]
    [InlineData(ValuationMethod.SpecificIdentification)]
    public async Task Unsupported_method_fails_closed_instead_of_creating_zero_value_issue(ValuationMethod method)
    {
        var service = await Setup(method);
        var action = () => Issue(service, 2);
        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not supported*");
        (await _db.Set<InventoryBalance>().SingleAsync()).QuantityOnHand.Should().Be(20);
        _db.ChangeTracker.Entries<InventoryMovement>().Should().BeEmpty();
    }

    private Task<decimal> Issue(InventoryValuationService service, decimal quantity) => service.ProcessIssueAsync(
        _item.Id, _warehouse, _location, quantity, InventoryMovementType.RequisitionIssue,
        ReferenceType.Requisition, "SIV-TEST", Guid.NewGuid());

    private async Task<InventoryValuationService> Setup(ValuationMethod method)
    {
        _item.TenantId = _tenant;
        _item.ValuationMethod = method;
        var cost = method == ValuationMethod.StandardCost ? 2000m : 1900m;
        _db.AddRange(_item, new InventoryBalance
        {
            TenantId = _tenant, InventoryItemId = _item.Id, WarehouseId = _warehouse, LocationId = _location,
            QuantityOnHand = 20, QuantityAvailable = 20, AverageUnitCost = cost, TotalValue = 20 * cost
        });
        if (method == ValuationMethod.FIFO) _db.Add(Layer(_tenant, _location));
        await _db.SaveChangesAsync();
        var actor = new Mock<ICurrentUserProvider>();
        actor.SetupGet(value => value.TenantId).Returns(_tenant);
        actor.SetupGet(value => value.UserId).Returns(Guid.NewGuid());
        return new InventoryValuationService(new UnitOfWork(_db), NullLogger<InventoryValuationService>.Instance,
            actor.Object, Mock.Of<IProcurementReceiptSourceControlService>());
    }

    private InventoryLayer Layer(Guid tenant, Guid? location) => new()
    {
        TenantId = tenant, InventoryItemId = _item.Id, WarehouseId = _warehouse, LocationId = location,
        LayerNumber = Guid.NewGuid().ToString("N"), LayerDate = DateTime.UtcNow.AddDays(-1),
        OriginalQuantity = 20, RemainingQuantity = 20, UnitCost = 1900, RemainingValue = 38000
    };

    public void Dispose() => _db.Dispose();
}
