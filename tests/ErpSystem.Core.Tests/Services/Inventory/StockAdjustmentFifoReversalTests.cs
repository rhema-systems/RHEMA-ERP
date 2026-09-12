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

public sealed class StockAdjustmentFifoReversalTests
{
    [Fact]
    public async Task Negative_fifo_then_reversal_restores_the_recorded_value_not_rounded_display_price()
    {
        await using var fixture = new Fixture();
        await fixture.SeedAsync();
        var issued = await fixture.Service.ProcessAdjustmentAsync(fixture.Item.Id, fixture.Warehouse.Id, fixture.Bin.Id,
            -20000m, 1.2345m, 20000m, false, "FIFO-REVERSE", fixture.Adjustment.Id);
        await fixture.Unit.SaveChangesAsync();
        issued.Should().Be(24689m);
        fixture.Adjustment.Status = "Reversed";
        await fixture.Unit.SaveChangesAsync();

        var restored = await fixture.ReverseAsync();
        await fixture.Unit.SaveChangesAsync();

        restored.Should().Be(24689m).And.NotBe(20000m * 1.2345m);
        var balance = await fixture.Context.Set<InventoryBalance>().SingleAsync();
        balance.QuantityOnHand.Should().Be(20000m);
        balance.TotalValue.Should().Be(24689m);
        var movements = await fixture.Context.Set<InventoryMovement>().OrderBy(value => value.MovementDate).ToListAsync();
        movements.Should().HaveCount(2);
        movements.Select(value => value.TotalValue).Should().Equal(24689m, 24689m);
        movements.Last().RunningValue.Should().Be(24689m);
        (await fixture.Context.Set<InventoryLayer>().SingleAsync(value => value.RemainingQuantity > 0))
            .RemainingValue.Should().Be(24689m);
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("parent")]
    [InlineData("item")]
    [InlineData("bin")]
    [InlineData("quantity")]
    [InlineData("deleted")]
    [InlineData("not-reversed")]
    [InlineData("wrong-method")]
    public async Task A_reversal_line_cannot_bypass_exact_retained_source_scope(string invalid)
    {
        await using var fixture = new Fixture();
        fixture.Adjustment.Status = "Reversed";
        switch (invalid)
        {
            case "tenant": fixture.Line.TenantId = Guid.NewGuid(); break;
            case "parent": fixture.Line.AdjustmentId = Guid.NewGuid(); fixture.Adjustment.Items.Clear(); break;
            case "item": fixture.Line.InventoryItemId = Guid.NewGuid(); break;
            case "bin": fixture.Line.LocationId = Guid.NewGuid(); break;
            case "quantity": fixture.Line.AdjustmentQuantity = -19999m; break;
            case "deleted": fixture.Line.IsDeleted = true; break;
            case "not-reversed": fixture.Adjustment.Status = "Posted"; break;
            case "wrong-method": fixture.Item.ValuationMethod = ValuationMethod.WeightedAverage; break;
        }
        await fixture.SeedAsync();
        var beforeLayers = await fixture.Context.Set<InventoryLayer>().SumAsync(value => value.RemainingValue);
        Func<Task> reverse = async () => { await fixture.ReverseAsync(); };
        await reverse.Should().ThrowAsync<InvalidOperationException>().WithMessage("The reversal line does not match*");
        (await fixture.Context.Set<InventoryMovement>().CountAsync()).Should().Be(0);
        (await fixture.Context.Set<InventoryLayer>().SumAsync(value => value.RemainingValue)).Should().Be(beforeLayers);
        (await fixture.Context.Set<InventoryBalance>().SingleAsync()).QuantityOnHand.Should().Be(20000m);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public ApplicationDbContext Context { get; } = new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);
        public UnitOfWork Unit { get; }
        public InventoryValuationService Service { get; }
        public InventoryItem Item { get; }
        public Warehouse Warehouse { get; }
        public WarehouseLocation Bin { get; }
        public StockAdjustment Adjustment { get; }
        public StockAdjustmentItem Line { get; }

        public Fixture()
        {
            var tenant = Guid.NewGuid();
            Warehouse = new Warehouse { TenantId = tenant, Code = "FIFO-WH", Name = "FIFO warehouse" };
            Bin = new WarehouseLocation { TenantId = tenant, WarehouseId = Warehouse.Id, LocationCode = "FIFO-BIN" };
            Item = new InventoryItem { TenantId = tenant, ItemCode = "FIFO-REVERSE", Name = "FIFO reversal", ValuationMethod = ValuationMethod.FIFO };
            Adjustment = new StockAdjustment { TenantId = tenant, WarehouseId = Warehouse.Id, Status = "Posted", AdjustmentNumber = "FIFO-REVERSE" };
            Line = new StockAdjustmentItem
            {
                TenantId = tenant, AdjustmentId = Adjustment.Id, InventoryItemId = Item.Id, LocationId = Bin.Id,
                SystemQuantity = 20000m, PhysicalQuantity = 0m, AdjustmentQuantity = -20000m,
                UnitCost = 1.2345m, AdjustmentValue = -24689m
            };
            Adjustment.Items.Add(Line);
            Unit = new UnitOfWork(Context);
            var current = new Mock<ICurrentUserProvider>();
            current.SetupGet(value => value.TenantId).Returns(tenant);
            current.SetupGet(value => value.UserId).Returns(Guid.NewGuid());
            Service = new InventoryValuationService(Unit, NullLogger<InventoryValuationService>.Instance,
                current.Object, Mock.Of<IProcurementReceiptSourceControlService>());
        }

        public async Task SeedAsync()
        {
            Context.AddRange(Warehouse, Bin, Item, Adjustment, Line);
            Context.Add(new InventoryBalance
            {
                TenantId = Item.TenantId, InventoryItemId = Item.Id, WarehouseId = Warehouse.Id, LocationId = Bin.Id,
                QuantityOnHand = 20000m, QuantityAvailable = 20000m, TotalValue = 24689m, AverageUnitCost = 1.23m
            });
            foreach (var (quantity, cost, day) in new[] { (15311m, 1m, 1), (4689m, 2m, 2) })
                Context.Add(new InventoryLayer
                {
                    TenantId = Item.TenantId, InventoryItemId = Item.Id, WarehouseId = Warehouse.Id, LocationId = Bin.Id,
                    LayerNumber = $"FIFO-{day}", LayerDate = new DateTime(2026, 1, day),
                    OriginalQuantity = quantity, RemainingQuantity = quantity, UnitCost = cost, RemainingValue = quantity * cost
                });
            await Context.SaveChangesAsync();
        }

        public Task<decimal> ReverseAsync() => Service.ProcessAdjustmentAsync(Item.Id, Warehouse.Id, Bin.Id,
            20000m, 1.2345m, 0m, false, "FIFO-REVERSE", Adjustment.Id, reversalAdjustmentLineId: Line.Id);

        public async ValueTask DisposeAsync() { Unit.Dispose(); await Context.DisposeAsync(); }
    }
}
