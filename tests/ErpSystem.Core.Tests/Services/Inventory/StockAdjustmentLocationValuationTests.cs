using System.Globalization;
using System.Reflection;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Inventory;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Inventory;

/// <summary>
/// Tests the line-building boundary without workflow or SQL-trigger substitutes.
/// The matching database trigger is separately exercised against SQL Server.
/// </summary>
public sealed class StockAdjustmentLocationValuationTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public async Task Weighted_average_uses_exact_location_cost_not_item_average_or_caller_cost(int sign)
    {
        await using var fixture = new Fixture();
        fixture.Item.AverageCost = 1907.09m;
        await fixture.AddBalanceAsync(1918.85m);

        var line = await fixture.BuildAsync(sign * 2m, callerCost: 999999m);

        line.UnitCost.Should().Be(1918.85m);
        line.AdjustmentValue.Should().Be(sign * 3837.70m);
        line.SystemQuantity.Should().Be(36m);
        line.PhysicalQuantity.Should().Be(36m + sign * 2m);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public async Task Standard_cost_remains_standard_despite_location_average(int sign)
    {
        await using var fixture = new Fixture();
        fixture.Item.ValuationMethod = ValuationMethod.StandardCost;
        fixture.Item.StandardCost = 24.50m;
        fixture.Item.AverageCost = 99m;
        await fixture.AddBalanceAsync(300m);

        var line = await fixture.BuildAsync(sign * 3m, callerCost: 1m);

        line.UnitCost.Should().Be(24.50m);
        line.AdjustmentValue.Should().Be(sign * 73.50m);
    }

    [Theory]
    [InlineData("40", "30", "20", "40")]
    [InlineData("0", "30", "20", "30")]
    [InlineData("0", "0", "20", "20")]
    public async Task Weighted_average_without_positive_location_cost_uses_governed_item_fallback(
        string average, string standard, string lastPurchase, string expected)
    {
        await using var fixture = new Fixture();
        fixture.Item.AverageCost = Decimal(average);
        fixture.Item.StandardCost = Decimal(standard);
        fixture.Item.LastPurchaseCost = Decimal(lastPurchase);
        await fixture.AddBalanceAsync(0m);

        (await fixture.BuildAsync(2m, callerCost: 555m)).UnitCost.Should().Be(Decimal(expected));
    }

    [Fact]
    public async Task Balance_from_another_location_or_tenant_never_supplies_the_cost()
    {
        await using var fixture = new Fixture();
        fixture.Item.AverageCost = 17.25m;
        await fixture.AddBalanceAsync(800m, locationId: Guid.NewGuid());
        await fixture.AddBalanceAsync(900m, tenantId: Guid.NewGuid());

        (await fixture.BuildAsync(1m)).UnitCost.Should().Be(17.25m);
    }

    [Fact]
    public async Task Positive_fifo_uses_exact_location_average_not_oldest_layer()
    {
        await using var fixture = new Fixture();
        fixture.Item.ValuationMethod = ValuationMethod.FIFO;
        await fixture.AddBalanceAsync(706.87m);
        await fixture.AddLayerAsync(2m, 600m, day: 1);

        var line = await fixture.BuildAsync(3m);

        line.UnitCost.Should().Be(706.87m);
        line.AdjustmentValue.Should().Be(2120.61m);
    }

    [Fact]
    public async Task Negative_fifo_weights_oldest_available_exact_location_layers()
    {
        await using var fixture = new Fixture();
        fixture.Item.ValuationMethod = ValuationMethod.FIFO;
        await fixture.AddBalanceAsync(900m);
        // Insert out of order to prove that receipt date, not retrieval order, controls consumption.
        await fixture.AddLayerAsync(8m, 30m, day: 2);
        await fixture.AddLayerAsync(2m, 10m, day: 1);
        await fixture.AddLayerAsync(100m, 1000m, day: 1, locationId: Guid.NewGuid());
        await fixture.AddLayerAsync(100m, 2000m, day: 1, tenantId: Guid.NewGuid());
        await fixture.AddLayerAsync(100m, 3000m, day: 1, deleted: true);

        var line = await fixture.BuildAsync(-5m);

        line.UnitCost.Should().Be(22m, "two units at 10 and three at 30 are valued at 110");
        line.AdjustmentValue.Should().Be(-110m);
        // Building the approval request values the proposed delta; it must not consume stock.
        fixture.Context.Set<InventoryLayer>().Where(x => !x.IsDeleted).Sum(x => x.RemainingQuantity)
            .Should().Be(210m);
    }

    [Fact]
    public async Task Negative_fifo_only_uses_item_fallback_for_quantity_not_covered_by_layers()
    {
        await using var fixture = new Fixture();
        fixture.Item.ValuationMethod = ValuationMethod.FIFO;
        fixture.Item.AverageCost = 40m;
        await fixture.AddBalanceAsync(900m);
        await fixture.AddLayerAsync(2m, 10m, day: 1);
        await fixture.AddLayerAsync(1m, 12m, day: 2);

        var line = await fixture.BuildAsync(-5m);

        line.UnitCost.Should().Be(22.4m, "(2 * 10 + 1 * 12 + 2 * 40) / 5");
        line.AdjustmentValue.Should().Be(-112m);
    }

    [Fact]
    public async Task Derived_fifo_unit_cost_is_normalized_before_computing_the_stored_line_value()
    {
        await using var fixture = new Fixture();
        fixture.Item.ValuationMethod = ValuationMethod.FIFO;
        await fixture.AddLayerAsync(1m, 1m, day: 1);
        await fixture.AddLayerAsync(2m, 2m, day: 2);

        var line = await fixture.BuildAsync(-3m);

        line.UnitCost.Should().Be(1.6667m);
        line.AdjustmentValue.Should().Be(-5m);
        line.AdjustmentValue.Should().Be(decimal.Round(line.AdjustmentQuantity * line.UnitCost, 2,
            MidpointRounding.AwayFromZero));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public async Task Money_midpoints_use_SQL_compatible_rounding_in_both_directions(int sign)
    {
        await using var fixture = new Fixture();
        await fixture.AddBalanceAsync(0.05m);

        var line = await fixture.BuildAsync(sign * 0.5m);

        line.UnitCost.Should().Be(0.05m);
        line.AdjustmentValue.Should().Be(sign * 0.03m);
    }

    [Fact]
    public async Task Four_decimal_unit_cost_midpoints_round_away_from_zero_before_line_total()
    {
        await using var fixture = new Fixture();
        fixture.Item.ValuationMethod = ValuationMethod.FIFO;
        // AverageUnitCost is stored at scale 2. Exercise a genuine derived
        // four-decimal midpoint from valid retained FIFO layers, not a cached
        // five-decimal average that the real database cannot store.
        await fixture.AddLayerAsync(15311m, 1m, day: 1);
        await fixture.AddLayerAsync(4689m, 2m, day: 2);

        var line = await fixture.BuildAsync(-20000m, systemQuantity: 20000m);

        line.UnitCost.Should().Be(1.2345m);
        line.AdjustmentValue.Should().Be(-24689m,
            "the cent-exact retained layer value must not be changed by rounding its display unit cost");
    }

    [Fact]
    public async Task Tracked_rows_share_one_fifo_plan_and_match_actual_sequential_posting()
    {
        await using var fixture = new Fixture();
        fixture.Item.ValuationMethod = ValuationMethod.FIFO;
        await fixture.AddBalanceAsync(15m);
        await fixture.AddLayerAsync(1m, 10m, 1);
        await fixture.AddLayerAsync(1m, 20m, 2);
        var lines = await fixture.BuildManyAsync([-1m, -1m], systemQuantity: 2m);
        lines.Select(value => value.UnitCost).Should().Equal(10m, 20m);
        lines.Select(value => value.AdjustmentValue).Should().Equal(-10m, -20m);
        lines[1].CreatedAt.Should().BeAfter(lines[0].CreatedAt);
        (await fixture.PostValuationAsync(lines, 2m)).Should().Equal(10m, 20m);
    }

    [Fact]
    public async Task Final_fifo_row_receives_the_retained_cent_remainder()
    {
        await using var fixture = new Fixture();
        fixture.Item.ValuationMethod = ValuationMethod.FIFO;
        await fixture.AddBalanceAsync(0.33m);
        await fixture.AddLayerAsync(3m, 0.33m, 1);
        (await fixture.Context.Set<InventoryLayer>().SingleAsync()).RemainingValue = 1m;
        await fixture.Context.SaveChangesAsync();
        var lines = await fixture.BuildManyAsync([-1m, -1m, -1m], systemQuantity: 3m);
        lines.Select(value => value.AdjustmentValue).Should().Equal(-0.33m, -0.33m, -0.34m);
        (await fixture.PostValuationAsync(lines, 3m)).Should().Equal(0.33m, 0.33m, 0.34m);
    }

    [Fact]
    public async Task Fractional_rows_round_each_consumption_and_settle_the_last_value()
    {
        await using var fixture = new Fixture();
        fixture.Item.ValuationMethod = ValuationMethod.FIFO;
        await fixture.AddBalanceAsync(0.05m);
        await fixture.AddLayerAsync(1m, 0.05m, 1);
        var lines = await fixture.BuildManyAsync([-0.3333m, -0.3333m, -0.3334m], systemQuantity: 1m);
        lines.Select(value => value.AdjustmentValue).Should().Equal(-0.02m, -0.02m, -0.01m);
        (await fixture.PostValuationAsync(lines, 1m)).Should().Equal(0.02m, 0.02m, 0.01m);
    }

    [Fact]
    public async Task Legacy_missing_layer_adoption_uses_the_same_captured_item_fallback()
    {
        await using var fixture = new Fixture();
        fixture.Item.ValuationMethod = ValuationMethod.FIFO;
        fixture.Item.AverageCost = 40m;
        await fixture.AddBalanceAsync(900m);
        await fixture.AddLayerAsync(2m, 10m, 1);
        await fixture.AddLayerAsync(1m, 12m, 2);
        var lines = await fixture.BuildManyAsync([-2m, -3m, -1m], systemQuantity: 6m);
        lines.Select(value => value.AdjustmentValue).Should().Equal(-20m, -92m, -40m);
        (await fixture.PostValuationAsync(lines, 6m)).Should().Equal(20m, 92m, 40m);
        (await fixture.Context.Set<InventoryLayer>().SingleAsync(value => value.SourceType == "LegacyExactBinAdoption"))
            .OriginalQuantity.Should().Be(3m);
    }

    [Fact]
    public async Task Positive_rows_add_a_new_execution_layer_without_reusing_consumed_older_stock()
    {
        await using var fixture = new Fixture();
        fixture.Item.ValuationMethod = ValuationMethod.FIFO;
        await fixture.AddBalanceAsync(13.33m);
        await fixture.AddLayerAsync(2m, 10m, 1);
        await fixture.AddLayerAsync(1m, 20m, 2);
        var lines = await fixture.BuildManyAsync([-1m, 1m, -2m], systemQuantity: 3m);
        lines.Select(value => value.AdjustmentValue).Should().Equal(-10m, 13.33m, -30m);
        (await fixture.PostValuationAsync(lines, 3m)).Should().Equal(10m, 13.33m, 30m);
    }

    [Fact]
    public async Task Planned_sequence_survives_repository_audit_timestamps_and_context_reload()
    {
        await using var fixture = new Fixture();
        fixture.Item.ValuationMethod = ValuationMethod.FIFO;
        await fixture.AddLayerAsync(1m, 10m, 1);
        await fixture.AddLayerAsync(1m, 20m, 2);
        await fixture.AddLayerAsync(1m, 30m, 3);
        var lines = await fixture.BuildManyAsync([-1m, -1m, -1m], 3m);
        var created = lines.Select(value => value.CreatedAt).ToArray();

        var reloaded = await fixture.SavePlanAndReloadAsync(lines);

        reloaded.Select(value => value.CreatedAt).Should().Equal(created);
        reloaded.Select(value => value.LotNumber).Should().Equal("LOT-0", "LOT-1", "LOT-2");
        reloaded.Select(value => value.AdjustmentValue).Should().Equal(-10m, -20m, -30m);
        created[1].Ticks.Should().Be(created[0].Ticks + 1);
        created[2].Ticks.Should().Be(created[0].Ticks + 2);
    }

    [Fact]
    public async Task Rebuilding_after_reorder_or_removal_reprices_survivors_from_unchanged_layers()
    {
        await using var fixture = new Fixture();
        fixture.Item.ValuationMethod = ValuationMethod.FIFO;
        await fixture.AddLayerAsync(1m, 10m, 1);
        await fixture.AddLayerAsync(2m, 20m, 2);

        var first = await fixture.BuildManyAsync([-1m, -2m], 3m);
        var reordered = await fixture.BuildManyAsync([-2m, -1m], 3m);
        var surviving = await fixture.BuildManyAsync([-2m], 3m);

        first.Select(value => value.AdjustmentValue).Should().Equal(-10m, -40m);
        reordered.Select(value => value.AdjustmentValue).Should().Equal(-30m, -20m);
        surviving.Single().AdjustmentValue.Should().Be(-30m, "removing the preceding row must not preserve its consumed-cost offset");
        (await fixture.Context.Set<InventoryLayer>().SumAsync(value => value.RemainingValue)).Should().Be(50m);
        (await fixture.Context.Set<InventoryMovement>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Caller_cost_cannot_bypass_missing_server_valuation()
    {
        await using var fixture = new Fixture();
        fixture.Item.AverageCost = fixture.Item.StandardCost = fixture.Item.LastPurchaseCost = 0;

        var action = () => fixture.BuildAsync(1m, callerCost: 999m);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("A server-derived inventory cost is required for SKU-001.");
    }

    [Theory]
    [InlineData("inactive")]
    [InlineData("other-warehouse")]
    [InlineData("other-tenant")]
    public async Task Valuation_fix_does_not_relax_exact_active_location_scope(string invalidScope)
    {
        await using var fixture = new Fixture();
        if (invalidScope == "inactive") fixture.Location.IsActive = false;
        if (invalidScope == "other-warehouse") fixture.Location.WarehouseId = Guid.NewGuid();
        if (invalidScope == "other-tenant") fixture.Location.TenantId = Guid.NewGuid();

        var action = () => fixture.BuildAsync(1m);

        if (invalidScope == "other-tenant")
            await action.Should().ThrowAsync<ArgumentException>().WithMessage("*current tenant*");
        else
            await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*active*selected warehouse*");
    }

    private static decimal Decimal(string value) => decimal.Parse(value, CultureInfo.InvariantCulture);

    private sealed class Fixture : IAsyncDisposable
    {
        public ApplicationDbContext Context { get; } = new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);
        public InventoryItem Item { get; }
        public WarehouseLocation Location { get; }
        private Warehouse Warehouse { get; }
        private UnitOfWork Unit { get; }
        private StockAdjustmentService Service { get; }

        public Fixture()
        {
            var tenantId = Guid.NewGuid();
            Warehouse = new Warehouse { TenantId = tenantId, Code = "VALUATION", Name = "Valuation warehouse", IsActive = true };
            Location = new WarehouseLocation { TenantId = tenantId, WarehouseId = Warehouse.Id, LocationCode = "LOC-001", IsActive = true };
            Item = new InventoryItem
            {
                TenantId = tenantId, ItemCode = "SKU-001", Name = "PVC Pipe 50mm",
                Status = ItemStatus.Active, ItemType = ItemType.StockItem,
                ValuationMethod = ValuationMethod.WeightedAverage, AverageCost = 1907.09m
            };
            Unit = new UnitOfWork(Context);
            var warehouses = new Mock<IWarehouseRepository>();
            warehouses.Setup(x => x.GetByIdAsync(Warehouse.Id)).ReturnsAsync(Warehouse);
            var locations = new Mock<IWarehouseLocationRepository>();
            locations.Setup(x => x.GetByIdAsync(Location.Id)).ReturnsAsync(Location);
            var items = new Mock<IInventoryItemRepository>();
            items.Setup(x => x.GetByIdAsync(Item.Id)).ReturnsAsync(Item);
            Service = new StockAdjustmentService(Mock.Of<IStockAdjustmentRepository>(), items.Object,
                Mock.Of<IStockMovementRepository>(), Mock.Of<IWarehouseQuantityRepository>(), locations.Object,
                warehouses.Object, Mock.Of<IConsignmentSettlementService>(), Mock.Of<ICurrentUserProvider>(), Unit,
                Mock.Of<IInventoryTrackingControlService>(), Mock.Of<IInventoryNegativeStockControlService>(),
                Mock.Of<IProcurementAccessControlService>(), Mock.Of<IProcurementSodGuardService>(),
                Mock.Of<IWorkflowIntegrationService>(), Mock.Of<IProcurementControlEventService>(),
                Mock.Of<IInventoryAdjustmentFinancePostingService>(), Mock.Of<IInventoryValuationService>(),
                NullLogger<StockAdjustmentService>.Instance);
        }

        public async Task AddBalanceAsync(decimal cost, Guid? locationId = null, Guid? tenantId = null)
        {
            Context.Add(new InventoryBalance
            {
                TenantId = tenantId ?? Item.TenantId, InventoryItemId = Item.Id, WarehouseId = Warehouse.Id,
                LocationId = locationId ?? Location.Id, AverageUnitCost = cost, QuantityOnHand = 36m,
                TotalValue = decimal.Round(36m * cost, 2, MidpointRounding.AwayFromZero)
            });
            await Context.SaveChangesAsync();
        }

        public async Task AddLayerAsync(decimal quantity, decimal cost, int day, Guid? locationId = null,
            Guid? tenantId = null, bool deleted = false)
        {
            Context.Add(new InventoryLayer
            {
                TenantId = tenantId ?? Item.TenantId, InventoryItemId = Item.Id, WarehouseId = Warehouse.Id,
                LocationId = locationId ?? Location.Id, LayerNumber = Guid.NewGuid().ToString("N"),
                LayerDate = new DateTime(2026, 9, day, 0, 0, 0, DateTimeKind.Utc), OriginalQuantity = quantity,
                RemainingQuantity = quantity, UnitCost = cost, RemainingValue = quantity * cost, IsDeleted = deleted
            });
            await Context.SaveChangesAsync();
        }

        public async Task<StockAdjustmentItem> BuildAsync(decimal delta, decimal? callerCost = null, decimal systemQuantity = 36m)
        {
            return (await BuildManyAsync([delta], systemQuantity, callerCost)).Single();
        }

        public async Task<List<StockAdjustmentItem>> BuildManyAsync(decimal[] deltas, decimal systemQuantity, decimal? callerCost = null)
        {
            var stock = await Context.Set<InventoryLocation>().SingleOrDefaultAsync(value =>
                value.TenantId == Item.TenantId && value.InventoryItemId == Item.Id && value.LocationId == Location.Id);
            if (stock is null)
            {
                stock = new InventoryLocation { TenantId = Item.TenantId, InventoryItemId = Item.Id, LocationId = Location.Id, AverageCost = 8888m };
                Context.Add(stock);
            }
            stock.Quantity = systemQuantity;
            await Context.SaveChangesAsync();
            var adjustment = new StockAdjustment
            {
                TenantId = Item.TenantId, WarehouseId = Warehouse.Id, ReasonCode = StockAdjustmentReasonCodes.PhysicalCount
            };
            var method = typeof(StockAdjustmentService).GetMethod("BuildLinesAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
            await (Task)method.Invoke(Service, [adjustment, deltas.Select((delta, index) => new CreateStockAdjustmentItemDto
            {
                InventoryItemId = Item.Id, LocationId = Location.Id, AdjustmentQuantity = delta,
                UnitCost = callerCost, LotNumber = $"LOT-{index}"
            }).ToList()])!;
            return adjustment.Items.ToList();
        }

        public async Task<List<StockAdjustmentItem>> SavePlanAndReloadAsync(List<StockAdjustmentItem> lines)
        {
            Context.AddRange(Item, Warehouse, Location);
            var adjustment = new StockAdjustment
            {
                Id = lines[0].AdjustmentId, TenantId = Item.TenantId, WarehouseId = Warehouse.Id,
                ReasonCode = StockAdjustmentReasonCodes.PhysicalCount, Status = "Draft", AdjustmentNumber = "FIFO-SAVE-ORDER"
            };
            Context.Add(adjustment);
            await Context.SaveChangesAsync();
            await (Task)typeof(StockAdjustmentService).GetMethod("PersistPlannedDraftLinesAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(Service, [adjustment, lines])!;
            Context.ChangeTracker.Clear();
            return await Context.Set<StockAdjustmentItem>().AsNoTracking()
                .Where(value => value.AdjustmentId == adjustment.Id).OrderBy(value => value.CreatedAt).ToListAsync();
        }

        public async Task<List<decimal>> PostValuationAsync(IReadOnlyCollection<StockAdjustmentItem> lines, decimal openingQuantity)
        {
            var fallback = Item.AverageCost > 0 ? Item.AverageCost : Item.StandardCost > 0 ? Item.StandardCost : Item.LastPurchaseCost;
            Context.AddRange(Item, Warehouse, Location);
            var balance = await Context.Set<InventoryBalance>().SingleAsync();
            balance.QuantityOnHand = openingQuantity;
            balance.QuantityAvailable = openingQuantity;
            balance.TotalValue = openingQuantity * balance.AverageUnitCost;
            await Context.SaveChangesAsync();
            var current = new Mock<ICurrentUserProvider>();
            current.SetupGet(value => value.TenantId).Returns(Item.TenantId);
            current.SetupGet(value => value.UserId).Returns(Guid.NewGuid());
            var service = new InventoryValuationService(Unit, NullLogger<InventoryValuationService>.Instance,
                current.Object, Mock.Of<IProcurementReceiptSourceControlService>());
            var values = new List<decimal>();
            var referenceId = Guid.NewGuid();
            var execution = DateTime.UtcNow;
            foreach (var line in lines.OrderBy(value => value.CreatedAt))
            {
                values.Add(await service.ProcessAdjustmentAsync(Item.Id, Warehouse.Id, Location.Id,
                    line.AdjustmentQuantity, line.UnitCost, openingQuantity, false, "FIFO-PLAN-TEST", referenceId,
                    line.LotNumber, valuationTimestamp: execution, fifoOpeningFallbackCost: fallback));
                openingQuantity += line.AdjustmentQuantity;
                await Unit.SaveChangesAsync();
            }
            return values;
        }

        public async ValueTask DisposeAsync()
        {
            Unit.Dispose();
            await Context.DisposeAsync();
        }
    }
}
