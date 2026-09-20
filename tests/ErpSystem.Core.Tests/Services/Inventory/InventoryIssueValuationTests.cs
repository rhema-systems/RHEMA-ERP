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
    [InlineData(ValuationMethod.FIFO, 41800)]
    [InlineData(ValuationMethod.WeightedAverage, 41800)]
    [InlineData(ValuationMethod.StandardCost, 44000)]
    public async Task Receipt_reconciles_available_quantity_and_records_closing_balances_once(
        ValuationMethod method, decimal closingValue)
    {
        var service = await Setup(method);
        var opening = await _db.Set<InventoryBalance>().SingleAsync();
        opening.QuantityAllocated = 3;
        opening.QuantityAvailable = 0; // Existing legacy cache must not survive the new receipt.
        await _db.SaveChangesAsync();

        var variance = await service.ProcessReceiptAsync(_item.Id, _warehouse, _location,
            2, 1900, ReferenceType.PO, "REC-TEST", Guid.NewGuid());
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var closing = await _db.Set<InventoryBalance>().SingleAsync();
        closing.QuantityOnHand.Should().Be(22);
        closing.QuantityAllocated.Should().Be(3);
        closing.QuantityAvailable.Should().Be(19);
        closing.TotalValue.Should().Be(closingValue);
        var movement = await _db.Set<InventoryMovement>().SingleAsync();
        movement.RunningBalance.Should().Be(22);
        movement.RunningValue.Should().Be(closingValue);
        movement.TotalValue.Should().Be(method == ValuationMethod.StandardCost ? 4000 : 3800);
        movement.VarianceAmount.Should().Be(variance);
    }

    [Theory]
    [InlineData(ValuationMethod.FIFO)]
    [InlineData(ValuationMethod.WeightedAverage)]
    [InlineData(ValuationMethod.StandardCost)]
    public async Task Consecutive_receipts_share_one_balance_without_double_counting_history(ValuationMethod method)
    {
        var service = await Setup(method);
        await service.ProcessReceiptAsync(_item.Id, _warehouse, _location, 2, 1900,
            ReferenceType.PO, "REC-FIRST", Guid.NewGuid());
        await _db.SaveChangesAsync();
        await service.ProcessReceiptAsync(_item.Id, _warehouse, _location, 1, 1900,
            ReferenceType.PO, "REC-SECOND", Guid.NewGuid());
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var balance = await _db.Set<InventoryBalance>().SingleAsync();
        balance.QuantityOnHand.Should().Be(23);
        balance.QuantityAvailable.Should().Be(23);
        var first = await _db.Set<InventoryMovement>().SingleAsync(value => value.ReferenceNumber == "REC-FIRST");
        var second = await _db.Set<InventoryMovement>().SingleAsync(value => value.ReferenceNumber == "REC-SECOND");
        first.RunningBalance.Should().Be(22);
        second.RunningBalance.Should().Be(23);
        second.RunningValue.Should().Be(balance.TotalValue);
    }

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

    [Theory]
    [InlineData(ValuationMethod.FIFO, 38000)]
    [InlineData(ValuationMethod.WeightedAverage, 38000)]
    [InlineData(ValuationMethod.StandardCost, 40000)]
    public async Task Return_and_reversal_restore_exact_original_value_once(ValuationMethod method, decimal openingValue)
    {
        var service = await Setup(method);
        var line = await ReturnLine();
        await service.ProcessReturnAsync(line.Id);
        await _db.SaveChangesAsync();
        var balance = await _db.Set<InventoryBalance>().SingleAsync();
        balance.QuantityOnHand.Should().Be(21);
        balance.TotalValue.Should().Be(openingValue + 1900);
        var movement = await _db.Set<InventoryMovement>().SingleAsync();
        movement.RunningValue.Should().Be(openingValue + 1900);
        movement.RunningBalance.Should().Be(21);
        movement.TotalValue.Should().Be(1900);
        Func<Task> duplicate = () => service.ProcessReturnAsync(line.Id);
        await duplicate.Should().ThrowAsync<InvalidOperationException>();
        line.InventoryReturnVoucher.Status = InventoryReturnVoucherStatus.Reversed;
        await _db.SaveChangesAsync();
        await service.ProcessReturnAsync(line.Id, true);
        await _db.SaveChangesAsync();
        balance.QuantityOnHand.Should().Be(20);
        balance.TotalValue.Should().Be(openingValue);
        (await _db.Set<InventoryMovement>().SingleAsync(value => value.IsReversal)).ReversedMovementId.Should().Be(movement.Id);
        if (method == ValuationMethod.FIFO)
            (await _db.Set<InventoryLayer>().SingleAsync(value => value.SourceId == line.Id)).RemainingQuantity.Should().Be(0);
        duplicate = () => service.ProcessReturnAsync(line.Id, true);
        await duplicate.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Return_reversal_cannot_consume_an_unrelated_fifo_layer()
    {
        var service = await Setup(ValuationMethod.FIFO);
        var line = await ReturnLine();
        await service.ProcessReturnAsync(line.Id);
        await _db.SaveChangesAsync();
        var layer = await _db.Set<InventoryLayer>().SingleAsync(value => value.SourceId == line.Id);
        layer.RemainingQuantity = 0;
        layer.RemainingValue = 0;
        layer.IsFullyConsumed = true;
        line.InventoryReturnVoucher.Status = InventoryReturnVoucherStatus.Reversed;
        await _db.SaveChangesAsync();
        Func<Task> reverse = () => service.ProcessReturnAsync(line.Id, true);
        await reverse.Should().ThrowAsync<InvalidOperationException>().WithMessage("*unrelated stock*");
        (await _db.Set<InventoryBalance>().SingleAsync()).QuantityOnHand.Should().Be(21);
        (await _db.Set<InventoryLayer>().SingleAsync(value => value.SourceId != line.Id)).RemainingQuantity.Should().Be(20);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Return_rejects_unapproved_or_other_tenant_source(bool otherTenant)
    {
        var service = await Setup(ValuationMethod.FIFO);
        var line = await ReturnLine();
        if (otherTenant) line.TenantId = Guid.NewGuid();
        else line.InventoryReturnVoucher.Status = InventoryReturnVoucherStatus.PendingApproval;
        await _db.SaveChangesAsync();
        Func<Task> action = () => service.ProcessReturnAsync(line.Id);
        await action.Should().ThrowAsync<InvalidOperationException>();
        (await _db.Set<InventoryBalance>().SingleAsync()).QuantityOnHand.Should().Be(20);
        _db.ChangeTracker.Entries<InventoryMovement>().Should().BeEmpty();
    }

    private async Task<InventoryReturnVoucherLine> ReturnLine()
    {
        var line = new InventoryReturnVoucherLine
        {
            TenantId = _tenant, InventoryItemId = _item.Id, LocationId = _location,
            Quantity = 1, UnitCost = 1900, TotalValue = 1900,
            InventoryReturnVoucher = new InventoryReturnVoucher
            {
                TenantId = _tenant, WarehouseId = _warehouse, VoucherNumber = "SRV-TEST",
                InventoryRequisitionId = Guid.NewGuid(), Status = InventoryReturnVoucherStatus.Posted
            }
        };
        _db.Add(line);
        await _db.SaveChangesAsync();
        return line;
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
