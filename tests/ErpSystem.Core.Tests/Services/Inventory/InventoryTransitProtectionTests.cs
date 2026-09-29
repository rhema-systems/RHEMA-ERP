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

public sealed class InventoryTransitProtectionTests
{
    [Theory]
    [InlineData("flag", "receipt")]
    [InlineData("flag", "issue")]
    [InlineData("flag", "adjustment")]
    [InlineData("hierarchy", "receipt")]
    [InlineData("hierarchy", "issue")]
    [InlineData("hierarchy", "adjustment")]
    [InlineData("type", "receipt")]
    [InlineData("type", "issue")]
    [InlineData("type", "adjustment")]
    [InlineData("warehouse", "receipt")]
    [InlineData("warehouse", "issue")]
    [InlineData("warehouse", "adjustment")]
    public async Task Ordinary_valuation_rejects_each_transit_marker_before_mutating_stock(string marker, string operation)
    {
        await using var f = await Fixture.CreateAsync(marker);
        Func<Task> act = operation switch
        {
            "receipt" => () => f.Service.ProcessReceiptAsync(f.Item.Id, f.Warehouse.Id, f.Location.Id,
                1m, 10m, ReferenceType.Adjustment, "ORDINARY", Guid.NewGuid()),
            "issue" => () => f.Service.ProcessIssueAsync(f.Item.Id, f.Warehouse.Id, f.Location.Id,
                1m, InventoryMovementType.TransferOut, ReferenceType.Transfer, "FORGED-TRANSFER", Guid.NewGuid()),
            _ => () => f.Service.ProcessAdjustmentAsync(f.Item.Id, f.Warehouse.Id, f.Location.Id,
                1m, 10m, 0m, false, "ADJUSTMENT", Guid.NewGuid())
        };

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage(InventoryTransitProtection.Message);
        f.Item.IsValuationLocked.Should().BeFalse();
        f.Context.ChangeTracker.Entries().Should().OnlyContain(x => x.State == EntityState.Unchanged);
        f.ReceiptControl.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Ordinary_receipt_into_normal_storage_still_stages_valuation()
    {
        await using var f = await Fixture.CreateAsync("normal");
        await f.Service.ProcessReceiptAsync(f.Item.Id, f.Warehouse.Id, f.Location.Id,
            2m, 10m, ReferenceType.Adjustment, "NORMAL", Guid.NewGuid());
        await f.Unit.SaveChangesAsync();
        var balance = await f.Context.Set<InventoryBalance>().SingleAsync();
        balance.QuantityOnHand.Should().Be(2m);
        balance.TotalValue.Should().Be(20m);
        (await f.Context.Set<InventoryMovement>().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task A_null_bin_does_not_bypass_the_virtual_warehouse_guard()
    {
        await using var f = await Fixture.CreateAsync("warehouse");
        Func<Task> act = () => f.Service.ProcessReceiptAsync(f.Item.Id, f.Warehouse.Id, null,
            1m, 10m, ReferenceType.Adjustment, "NO-BIN", Guid.NewGuid());
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage(InventoryTransitProtection.Message);
    }

    [Fact]
    public async Task Transit_classification_from_another_tenant_is_not_read()
    {
        await using var f = await Fixture.CreateAsync("warehouse");
        (await InventoryTransitProtection.IsProtectedScopeAsync(f.Unit, Guid.NewGuid(), f.Warehouse.Id, f.Location.Id))
            .Should().BeFalse();
    }

    [Fact]
    public async Task Company_valuation_total_retains_transit_stock_even_though_ordinary_operations_cannot_pick_it()
    {
        await using var f = await Fixture.CreateAsync("warehouse");
        var normalWarehouse = new Warehouse { TenantId=f.Tenant, Code="NORMAL", Name="Normal", WarehouseType="Standard" };
        f.Context.Add(normalWarehouse);
        f.Context.AddRange(
            new InventoryBalance { TenantId=f.Tenant, InventoryItemId=f.Item.Id, WarehouseId=f.Warehouse.Id,
                LocationId=f.Location.Id, QuantityOnHand=5m, TotalValue=50m },
            new InventoryBalance { TenantId=f.Tenant, InventoryItemId=f.Item.Id, WarehouseId=normalWarehouse.Id,
                QuantityOnHand=7m, TotalValue=70m });
        await f.Context.SaveChangesAsync();
        (await f.Service.GetInventoryValueAsync()).Should().Be(120m);
        (await f.Service.GetInventoryValueAsync(f.Warehouse.Id)).Should().Be(50m);
    }

    [Fact]
    public async Task Generic_movement_reversal_cannot_reverse_a_transit_leg()
    {
        await using var f = await Fixture.CreateAsync("flag");
        var movement = new InventoryMovement
        {
            TenantId=f.Tenant, InventoryItemId=f.Item.Id, WarehouseId=f.Warehouse.Id, LocationId=f.Location.Id,
            MovementNumber="TRANSIT-LEG", IsPosted=true, Quantity=1m, UnitCost=10m, TotalValue=10m
        };
        f.Context.Add(movement); await f.Context.SaveChangesAsync();
        Func<Task> act = () => f.Service.ReverseMovementAsync(movement.Id, "ordinary reversal");
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage(InventoryTransitProtection.Message);
        (await f.Context.Set<InventoryMovement>().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Generic_reversal_cannot_skip_the_transit_guard_using_a_foreign_tenant_movement()
    {
        await using var f = await Fixture.CreateAsync("flag");
        var movement = new InventoryMovement
        {
            TenantId=Guid.NewGuid(), InventoryItemId=f.Item.Id, WarehouseId=f.Warehouse.Id, LocationId=f.Location.Id,
            MovementNumber="FOREIGN-TRANSIT", IsPosted=true, Quantity=1m, UnitCost=10m, TotalValue=10m
        };
        f.Context.Add(movement); await f.Context.SaveChangesAsync();
        Func<Task> act = () => f.Service.ReverseMovementAsync(movement.Id, "foreign reversal");
        await act.Should().ThrowAsync<KeyNotFoundException>();
        (await f.Context.Set<InventoryMovement>().CountAsync()).Should().Be(1);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public Guid Tenant { get; } = Guid.NewGuid();
        public ApplicationDbContext Context { get; }
        public UnitOfWork Unit { get; }
        public Warehouse Warehouse { get; }
        public WarehouseLocation Location { get; }
        public InventoryItem Item { get; }
        public Mock<IProcurementReceiptSourceControlService> ReceiptControl { get; } = new();
        public InventoryValuationService Service { get; }

        private Fixture(string marker)
        {
            Context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"transit-protection-{Guid.NewGuid():N}").Options);
            Unit = new UnitOfWork(Context);
            Warehouse = new Warehouse { TenantId=Tenant, Code="STORE", Name="Store", WarehouseType=marker == "warehouse" ? "Transit" : "Main", IsActive=true };
            Location = new WarehouseLocation { TenantId=Tenant, WarehouseId=Warehouse.Id, LocationCode="BIN", Name="Bin",
                IsActive=true, IsInTransitLocation=marker == "flag", LocationType=marker == "type" ? "InTransit" : "Bin",
                LocationHierarchyType=marker == "hierarchy" ? WarehouseLocationType.InTransit : null };
            Item = new InventoryItem { TenantId=Tenant, ItemCode="ITEM", Name="Item", UnitOfMeasure="EA", ValuationMethod=ValuationMethod.WeightedAverage };
            var user = new Mock<ICurrentUserProvider>(); user.SetupGet(x => x.TenantId).Returns(Tenant);
            user.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
            Service = new InventoryValuationService(Unit, NullLogger<InventoryValuationService>.Instance, user.Object, ReceiptControl.Object);
        }

        public static async Task<Fixture> CreateAsync(string marker)
        {
            var f = new Fixture(marker); f.Context.AddRange(f.Warehouse, f.Location, f.Item);
            await f.Context.SaveChangesAsync(); return f;
        }

        public async ValueTask DisposeAsync() { Unit.Dispose(); await Context.DisposeAsync(); }
    }
}
