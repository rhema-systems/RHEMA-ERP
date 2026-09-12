using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Inventory;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Inventory;

public sealed class InventoryTransferValuationTests
{
    [Theory]
    [InlineData(ValuationMethod.FIFO, 100, 15, 20)]
    [InlineData(ValuationMethod.WeightedAverage, 150, 15, 15)]
    [InlineData(ValuationMethod.StandardCost, 120, 12, 12)]
    public async Task Dispatch_retains_owned_transit_value_and_partial_receipts_settle_original_cost(
        ValuationMethod method, decimal outbound, decimal ownedAverage, decimal sourceAverage)
    {
        await using var f = new Fixture(method);
        await f.Seed();
        (await f.Ship(10)).Should().Be(outbound);
        f.Item.CurrentStock.Should().Be(20);
        f.SourceQuantity.CurrentStock.Should().Be(10);
        f.Item.AverageCost.Should().Be(ownedAverage, "owned goods remain an asset while in transit");
        f.SourceQuantity.AverageCost.Should().Be(sourceAverage, "warehouse average excludes dispatched goods");
        f.DestinationQuantity.AverageCost.Should().Be(0);
        f.Line.UnitCost.Should().Be(777, "draft estimates are not rewritten as fake actual history");
        var first = await f.Receive(3);
        var second = await f.Receive(7);
        (first + second).Should().Be(outbound);
        f.DestinationBalance.QuantityOnHand.Should().Be(10);
        f.DestinationBalance.TotalValue.Should().Be(outbound);
        f.Item.AverageCost.Should().Be(ownedAverage);
        f.Item.CurrentStock.Should().Be(20);
        var ledger = await f.Db.Set<InventoryMovement>().ToListAsync();
        ledger.Should().HaveCount(3);
        ledger.Should().OnlyContain(value => value.ReferenceType == ReferenceType.Transfer && value.ReferenceId == f.Line.Id && value.Notes!.StartsWith("TransferAction:"));
        ledger.Where(value => value.Direction == MovementDirection.Out).Sum(value => value.TotalValue).Should().Be(outbound);
        ledger.Where(value => value.Direction == MovementDirection.In).Sum(value => value.TotalValue).Should().Be(outbound);
        if (method == ValuationMethod.FIFO)
            (await f.Db.Set<InventoryLayer>().Where(value => value.WarehouseId == f.Destination.Id).ToListAsync())
                .Sum(value => value.RemainingValue).Should().Be(outbound);
    }

    [Fact]
    public async Task Two_receipts_before_save_share_one_new_exact_bin_balance()
    {
        await using var f = new Fixture(ValuationMethod.WeightedAverage);
        f.Db.Remove(f.DestinationBalance);
        await f.Seed();
        await f.Ship(5);
        var first = await f.Action(InventoryTransferActionType.Received, 2);
        var second = await f.Action(InventoryTransferActionType.Received, 3);
        (await f.Valuation.ProcessTransferReceiptAsync(f.Line.Id, first.Id, f.Destination.Id, f.DestinationBin.Id, 2)).Should().Be(30);
        (await f.Valuation.ProcessTransferReceiptAsync(f.Line.Id, second.Id, f.Destination.Id, f.DestinationBin.Id, 3)).Should().Be(45);
        f.DestinationQuantity.CurrentStock = f.DestinationQuantity.AvailableStock = 5;
        f.DestinationLocation.Quantity = f.DestinationLocation.AvailableQuantity = 5;
        f.SourceQuantity.AllocatedStock -= 5;
        f.Line.ReceivedQuantity = 5;
        await f.Db.SaveChangesAsync();
        var balances = await f.Db.Set<InventoryBalance>().Where(value => value.InventoryItemId == f.Item.Id &&
            value.WarehouseId == f.Destination.Id && value.LocationId == f.DestinationBin.Id).ToListAsync();
        balances.Should().ContainSingle().Which.TotalValue.Should().Be(75);
        balances.Single().QuantityOnHand.Should().Be(5);
        f.Item.AverageCost.Should().Be(15);
    }

    [Fact]
    public async Task Weighted_average_transfer_preserves_last_cent_instead_of_using_rounded_cache()
    {
        await using var f = new Fixture(ValuationMethod.WeightedAverage);
        f.Item.CurrentStock = f.Item.AvailableStock = 3;
        f.SourceQuantity.CurrentStock = f.SourceQuantity.AvailableStock = 3;
        f.SourceLocation.Quantity = f.SourceLocation.AvailableQuantity = 3;
        f.SourceBalance.QuantityOnHand = f.SourceBalance.QuantityAvailable = 3;
        f.SourceBalance.TotalValue = 10;
        f.SourceBalance.AverageUnitCost = 3.33m;
        await f.Seed();
        (await f.Ship(1)).Should().Be(3.33m);
        (await f.Ship(2)).Should().Be(6.67m);
        (await f.Receive(3)).Should().Be(10);
        f.SourceBalance.TotalValue.Should().Be(0);
        f.DestinationBalance.TotalValue.Should().Be(10);
        f.Item.AverageCost.Should().Be(3.33m);
    }

    [Fact]
    public async Task Standard_cost_change_before_dispatch_does_not_revalue_retained_stock()
    {
        await using var f = new Fixture(ValuationMethod.StandardCost);
        await f.Seed();
        f.Item.StandardCost = 99;
        await f.Db.SaveChangesAsync();
        (await f.Ship(10)).Should().Be(120);
        (await f.Receive(10)).Should().Be(120);
        f.SourceBalance.TotalValue.Should().Be(120);
        f.DestinationBalance.TotalValue.Should().Be(120);
        f.Item.AverageCost.Should().Be(12);
    }

    [Fact]
    public async Task Different_partial_dispatch_costs_and_receipts_keep_the_last_cent_in_the_carrying_pool()
    {
        await using var f = new Fixture(ValuationMethod.FIFO);
        await f.Seed();
        (await f.Ship(10)).Should().Be(100);
        (await f.Receive(3)).Should().Be(30);
        (await f.Ship(5)).Should().Be(100);
        (await f.Receive(5)).Should().Be(70.83m);
        (await f.Receive(7)).Should().Be(99.17m);
        f.DestinationBalance.TotalValue.Should().Be(200);
        f.Item.AverageCost.Should().Be(15);
        var issued = await f.Valuation.ProcessIssueAsync(f.Item.Id, f.Destination.Id, f.DestinationBin.Id, 15,
            InventoryMovementType.RequisitionIssue, ReferenceType.Requisition, "ISSUE-DESTINATION", Guid.NewGuid());
        issued.Should().Be(200, "the last FIFO quantity must settle each retained layer's cent remainder");
        await f.Db.SaveChangesAsync();
        f.DestinationBalance.TotalValue.Should().Be(0);
        (await f.Db.Set<InventoryLayer>().Where(value => value.WarehouseId == f.Destination.Id).ToListAsync())
            .Should().OnlyContain(value => value.RemainingQuantity == 0 && value.RemainingValue == 0);
    }

    [Fact]
    public async Task Shipment_reversal_restores_original_carrying_value_not_current_standard_cost()
    {
        await using var f = new Fixture(ValuationMethod.StandardCost);
        await f.Seed();
        await f.Ship(10);
        f.Item.StandardCost = 99;
        await f.Db.SaveChangesAsync();
        var action = await f.Action(InventoryTransferActionType.ShipmentReversed, 10);
        var restored = await f.Valuation.ProcessTransferReceiptAsync(f.Line.Id, action.Id, f.Source.Id, f.SourceBin.Id, 10, true);
        restored.Should().Be(120);
        f.SourceQuantity.CurrentStock += 10;
        f.SourceLocation.Quantity += 10;
        f.Line.ShippedQuantity = 0;
        f.Transfer.Status = TransferStatus.Cancelled;
        await f.Db.SaveChangesAsync();
        f.SourceBalance.QuantityOnHand.Should().Be(20);
        f.SourceBalance.TotalValue.Should().Be(240);
        f.Item.AverageCost.Should().Be(12);
        f.Item.StandardCost.Should().Be(99);
    }

    [Fact]
    public async Task Standard_cost_change_during_transit_does_not_revalue_receipt()
    {
        await using var f = new Fixture(ValuationMethod.StandardCost);
        await f.Seed();
        await f.Ship(10);
        f.Item.StandardCost = 99;
        await f.Db.SaveChangesAsync();
        (await f.Receive(10)).Should().Be(120);
        f.DestinationBalance.TotalValue.Should().Be(120);
        f.Item.AverageCost.Should().Be(12);
    }

    [Theory]
    [InlineData("other-actor")]
    [InlineData("wrong-bin")]
    [InlineData("foreign-line")]
    [InlineData("excess-quantity")]
    [InlineData("legacy-no-ledger")]
    public async Task Invalid_receipt_source_fails_without_changing_destination_valuation(string failure)
    {
        await using var f = new Fixture(ValuationMethod.WeightedAverage);
        await f.Seed();
        if (failure != "legacy-no-ledger") await f.Ship(10);
        else { f.Line.ShippedQuantity=10; f.Transfer.Status=TransferStatus.InTransit; await f.Db.SaveChangesAsync(); }
        var action = await f.Action(InventoryTransferActionType.Received, 10);
        if (failure == "other-actor") { action.ActorUserId=Guid.NewGuid(); await f.Db.SaveChangesAsync(); }
        var receive = () => f.Valuation.ProcessTransferReceiptAsync(
            failure == "foreign-line" ? Guid.NewGuid() : f.Line.Id, action.Id, f.Destination.Id,
            failure == "wrong-bin" ? f.SourceBin.Id : f.DestinationBin.Id,
            failure == "excess-quantity" ? 11 : 10);
        await receive.Should().ThrowAsync<InvalidOperationException>();
        f.DestinationBalance.QuantityOnHand.Should().Be(0);
        f.DestinationBalance.TotalValue.Should().Be(0);
    }

    [Fact]
    public async Task Duplicate_dispatch_and_receipt_actions_do_not_duplicate_valuation()
    {
        await using var f = new Fixture(ValuationMethod.WeightedAverage);
        await f.Seed();
        var dispatch = await f.Action(InventoryTransferActionType.Dispatched, 10);
        await f.Valuation.ProcessTransferDispatchAsync(f.Line.Id, dispatch.Id, f.Source.Id, f.SourceBin.Id, 10);
        await f.Db.SaveChangesAsync();
        await FluentActions.Awaiting(() => f.Valuation.ProcessTransferDispatchAsync(f.Line.Id, dispatch.Id, f.Source.Id, f.SourceBin.Id, 10))
            .Should().ThrowAsync<InvalidOperationException>();
        f.Line.ShippedQuantity=10; f.Transfer.Status=TransferStatus.InTransit;
        await f.Db.SaveChangesAsync();
        var receipt = await f.Action(InventoryTransferActionType.Received, 5);
        await f.Valuation.ProcessTransferReceiptAsync(f.Line.Id, receipt.Id, f.Destination.Id, f.DestinationBin.Id, 5);
        await FluentActions.Awaiting(() => f.Valuation.ProcessTransferReceiptAsync(f.Line.Id, receipt.Id, f.Destination.Id, f.DestinationBin.Id, 5))
            .Should().ThrowAsync<InvalidOperationException>();
        f.DestinationBalance.QuantityOnHand.Should().Be(5);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public Guid Tenant { get; } = Guid.NewGuid();
        public Guid Actor { get; } = Guid.NewGuid();
        public ApplicationDbContext Db { get; } = new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(value => value.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
        public UnitOfWork Unit { get; }
        public InventoryValuationService Valuation { get; }
        public InventoryItem Item { get; }
        public Warehouse Source { get; }
        public Warehouse Destination { get; }
        public WarehouseLocation SourceBin { get; }
        public WarehouseLocation DestinationBin { get; }
        public WarehouseQuantity SourceQuantity { get; }
        public WarehouseQuantity DestinationQuantity { get; }
        public InventoryLocation SourceLocation { get; }
        public InventoryLocation DestinationLocation { get; }
        public InventoryBalance SourceBalance { get; }
        public InventoryBalance DestinationBalance { get; }
        public InventoryTransfer Transfer { get; }
        public InventoryTransferItem Line { get; }
        private int Sequence;
        public Fixture(ValuationMethod method)
        {
            Unit = new UnitOfWork(Db);
            var actor = new Mock<ICurrentUserProvider>();
            actor.SetupGet(value => value.TenantId).Returns(Tenant);
            actor.SetupGet(value => value.UserId).Returns(Actor);
            Valuation = new InventoryValuationService(Unit, NullLogger<InventoryValuationService>.Instance, actor.Object,
                Mock.Of<IProcurementReceiptSourceControlService>());
            Item = new InventoryItem { TenantId=Tenant, ItemCode="TRANSFER-VALUED", Name="Valued transfer", UnitOfMeasure="EA",
                ValuationMethod=method, StandardCost=12, CurrentStock=20, AvailableStock=20 };
            Source = new Warehouse { TenantId=Tenant, Code="SOURCE", Name="Source" };
            Destination = new Warehouse { TenantId=Tenant, Code="DEST", Name="Destination" };
            SourceBin = new WarehouseLocation { TenantId=Tenant, WarehouseId=Source.Id, LocationCode="SOURCE-BIN", Name="Source bin", IsActive=true };
            DestinationBin = new WarehouseLocation { TenantId=Tenant, WarehouseId=Destination.Id, LocationCode="DEST-BIN", Name="Destination bin", IsActive=true };
            SourceQuantity = new WarehouseQuantity { TenantId=Tenant, WarehouseId=Source.Id, InventoryItemId=Item.Id, CurrentStock=20, AvailableStock=20 };
            DestinationQuantity = new WarehouseQuantity { TenantId=Tenant, WarehouseId=Destination.Id, InventoryItemId=Item.Id };
            SourceLocation = new InventoryLocation { TenantId=Tenant, InventoryItemId=Item.Id, LocationId=SourceBin.Id, Quantity=20, AvailableQuantity=20 };
            DestinationLocation = new InventoryLocation { TenantId=Tenant, InventoryItemId=Item.Id, LocationId=DestinationBin.Id };
            SourceBalance = new InventoryBalance { TenantId=Tenant, WarehouseId=Source.Id, LocationId=SourceBin.Id, InventoryItemId=Item.Id,
                QuantityOnHand=20, QuantityAvailable=20, TotalValue=method == ValuationMethod.StandardCost ? 240 : 300,
                AverageUnitCost=method == ValuationMethod.StandardCost ? 12 : 15 };
            DestinationBalance = new InventoryBalance { TenantId=Tenant, WarehouseId=Destination.Id, LocationId=DestinationBin.Id, InventoryItemId=Item.Id };
            Transfer = new InventoryTransfer { TenantId=Tenant, TransferNumber="TR-VALUED", SourceWarehouseId=Source.Id,
                DestinationWarehouseId=Destination.Id, Status=TransferStatus.Approved, ApprovalRequired=false, RequestedById=Actor };
            Line = new InventoryTransferItem { TenantId=Tenant, InventoryTransferId=Transfer.Id, InventoryItemId=Item.Id,
                RequestedQuantity=20, UnitCost=777, SourceLocationId=SourceBin.Id, DestinationLocationId=DestinationBin.Id };
            Db.AddRange(Item, Source, Destination, SourceBin, DestinationBin, SourceQuantity, DestinationQuantity,
                SourceLocation, DestinationLocation, SourceBalance, DestinationBalance, Transfer, Line);
            if (method == ValuationMethod.FIFO)
                foreach (var cost in new[] { 10m, 20m }) Db.Add(new InventoryLayer { TenantId=Tenant, InventoryItemId=Item.Id,
                    WarehouseId=Source.Id, LocationId=SourceBin.Id, LayerNumber=$"OPEN-{cost}", LayerDate=DateTime.UtcNow.AddDays(-30 + (double)cost),
                    OriginalQuantity=10, RemainingQuantity=10, UnitCost=cost, RemainingValue=10*cost, IsActive=true });
        }
        public async Task Seed() { await Db.SaveChangesAsync(); await Unit.BeginTransactionAsync(System.Data.IsolationLevel.Serializable); }
        public async Task<InventoryTransferAction> Action(InventoryTransferActionType type, decimal quantity)
        {
            var action = new InventoryTransferAction { TenantId=Tenant, InventoryTransferId=Transfer.Id, ActionType=type,
                ActorUserId=Actor, Sequence=++Sequence, OccurredAtUtc=DateTime.UtcNow, IdempotencyKey=Guid.NewGuid().ToString("N"),
                PayloadHash=new string('a',64), IntegrityHash=new string('b',64), CorrelationId="valuation-test" };
            action.Lines.Add(new InventoryTransferActionLine { TenantId=Tenant, InventoryTransferItemId=Line.Id,
                DispatchedQuantity=type is InventoryTransferActionType.Dispatched or InventoryTransferActionType.ShipmentReversed ? quantity : 0,
                ReceivedQuantity=type == InventoryTransferActionType.Received ? quantity : 0, IntegrityHash=new string('c',64) });
            Db.Add(action); await Db.SaveChangesAsync(); return action;
        }
        public async Task<decimal> Ship(decimal quantity)
        {
            var action = await Action(InventoryTransferActionType.Dispatched, quantity);
            var value = await Valuation.ProcessTransferDispatchAsync(Line.Id, action.Id, Source.Id, SourceBin.Id, quantity);
            SourceQuantity.CurrentStock-=quantity; SourceQuantity.AvailableStock-=quantity; SourceQuantity.AllocatedStock+=quantity;
            SourceLocation.Quantity-=quantity; SourceLocation.AvailableQuantity-=quantity;
            Line.ShippedQuantity+=quantity; Transfer.Status=TransferStatus.InTransit;
            await Db.SaveChangesAsync(); return value;
        }
        public async Task<decimal> Receive(decimal quantity)
        {
            var action = await Action(InventoryTransferActionType.Received, quantity);
            var value = await Valuation.ProcessTransferReceiptAsync(Line.Id, action.Id, Destination.Id, DestinationBin.Id, quantity);
            DestinationQuantity.CurrentStock+=quantity; DestinationQuantity.AvailableStock+=quantity;
            DestinationLocation.Quantity+=quantity; DestinationLocation.AvailableQuantity+=quantity;
            SourceQuantity.AllocatedStock-=quantity; Line.ReceivedQuantity+=quantity;
            await Db.SaveChangesAsync(); return value;
        }
        public async ValueTask DisposeAsync() { if(Unit.HasActiveTransaction) await Unit.RollbackAsync(); Unit.Dispose(); await Db.DisposeAsync(); }
    }
}
