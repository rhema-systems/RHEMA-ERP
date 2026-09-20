using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Services.Inventory;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Inventory;

public sealed class WarehouseDefaultLocationServiceTests
{
    [Fact]
    public async Task Warehouse_without_locations_gets_one_idempotent_normal_default()
    {
        await using var f = await Fixture.CreateAsync();
        var first = await f.Service.GetOrCreateAsync(f.Warehouse.Id, f.Actor);
        var second = await f.Service.GetOrCreateAsync(f.Warehouse.Id, f.Actor);
        second.Id.Should().Be(first.Id);
        first.LocationCode.Should().Be("DEFAULT");
        first.IsDefault.Should().BeTrue();
        WarehouseDefaultLocationService.IsEligible(first).Should().BeTrue();
        (await f.Context.Set<WarehouseLocation>().CountAsync(x => x.IsDefault)).Should().Be(1);
        (await f.Context.Set<AuditLog>().CountAsync(x => x.Action == "Warehouse.DefaultBinConfigured")).Should().Be(1);
        (await f.Context.Set<InventoryLocation>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Sole_normal_bin_is_reused_but_multiple_bins_do_not_get_arbitrarily_selected()
    {
        await using var f = await Fixture.CreateAsync();
        var sole = f.Bin("Existing");
        f.Context.Add(sole); await f.Context.SaveChangesAsync();
        (await f.Service.GetOrCreateAsync(f.Warehouse.Id, f.Actor)).Id.Should().Be(sole.Id);
        var secondWarehouse = new Warehouse { TenantId = f.Tenant, Name = "Second", Code = "SECOND", IsActive = true };
        f.Context.Add(secondWarehouse);
        f.Context.Add(new WarehouseLocation { TenantId = f.Tenant, WarehouseId = secondWarehouse.Id, LocationCode = "A" });
        f.Context.Add(new WarehouseLocation { TenantId = f.Tenant, WarehouseId = secondWarehouse.Id, LocationCode = "B" });
        await f.Context.SaveChangesAsync();
        (await f.Service.GetOrCreateAsync(secondWarehouse.Id, f.Actor)).LocationCode.Should().Be("DEFAULT");
    }

    [Fact]
    public async Task Selecting_a_replacement_default_does_not_move_existing_inventory()
    {
        await using var f = await Fixture.CreateAsync();
        var original = f.Bin("A", true); var replacement = f.Bin("B");
        f.Context.AddRange(original, replacement);
        var item = await f.ItemAsync(9);
        f.Context.Add(f.Quantity(item, original, 9)); await f.Context.SaveChangesAsync();
        await f.Service.SetDefaultAsync(replacement, f.Actor);
        original.IsDefault.Should().BeFalse(); replacement.IsDefault.Should().BeTrue();
        var stock = await f.Context.Set<InventoryLocation>().SingleAsync();
        stock.LocationId.Should().Be(original.Id); stock.Quantity.Should().Be(9);
        (await f.Context.Set<WarehouseLocation>().CountAsync(x => x.IsDefault)).Should().Be(1);
        (await f.Context.Set<AuditLog>().AnyAsync(x => x.Action == "Warehouse.DefaultBinChanged")).Should().BeTrue();
    }

    [Fact]
    public async Task New_zero_warehouse_assignment_gets_one_zero_default_bin_assignment()
    {
        await using var f = await Fixture.CreateAsync();
        var item = await f.ItemAsync(0);
        var bin = await f.Service.EnsureItemAssignmentAsync(f.Warehouse.Id, item.Id, f.Actor);
        await f.Service.EnsureItemAssignmentAsync(f.Warehouse.Id, item.Id, f.Actor);
        var stock = await f.Context.Set<InventoryLocation>().SingleAsync();
        stock.LocationId.Should().Be(bin.Id); stock.Quantity.Should().Be(0); stock.AvailableQuantity.Should().Be(0);
        (await f.Context.Set<InventoryBalance>().CountAsync()).Should().Be(0);
        (await f.Context.Set<InventoryLayer>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Fully_located_item_is_not_artificially_assigned_to_another_default_bin()
    {
        await using var f = await Fixture.CreateAsync();
        var defaultBin = f.Bin("Default", true); var other = f.Bin("Other");
        f.Context.AddRange(defaultBin, other);
        var item = await f.ItemAsync(8);
        f.Context.Add(f.Quantity(item, other, 8)); await f.Context.SaveChangesAsync();
        await f.Service.EnsureItemAssignmentAsync(f.Warehouse.Id, item.Id, f.Actor);
        (await f.Context.Set<InventoryLocation>().SingleAsync()).LocationId.Should().Be(other.Id);
    }

    [Fact]
    public async Task Only_unlocated_remainder_is_assigned_and_warehouse_totals_do_not_change()
    {
        await using var f = await Fixture.CreateAsync();
        var defaultBin = f.Bin("Default", true); var other = f.Bin("Other");
        f.Context.AddRange(defaultBin, other);
        var item = await f.ItemAsync(10);
        f.Context.Add(f.Quantity(item, other, 4)); await f.Context.SaveChangesAsync();
        await f.Service.EnsureItemAssignmentAsync(f.Warehouse.Id, item.Id, f.Actor);
        await f.Service.EnsureItemAssignmentAsync(f.Warehouse.Id, item.Id, f.Actor);
        var bins = await f.Context.Set<InventoryLocation>().ToListAsync();
        bins.Single(x => x.LocationId == defaultBin.Id).Quantity.Should().Be(6);
        bins.Single(x => x.LocationId == other.Id).Quantity.Should().Be(4);
        (await f.Context.Set<WarehouseQuantity>().SingleAsync()).CurrentStock.Should().Be(10);
        item.CurrentStock.Should().Be(10);
        (await f.Context.Set<InventoryBalance>().CountAsync()).Should().Be(0, "missing monetary sources must not be invented");
        (await f.Context.Set<AuditLog>().CountAsync(x => x.Action == "Inventory.DefaultBinAssigned")).Should().Be(1);
    }

    [Fact]
    public async Task Exact_unlocated_valuation_and_fifo_layers_are_mapped_without_changing_quantities_or_value()
    {
        await using var f = await Fixture.CreateAsync();
        var bin = f.Bin("Default", true); f.Context.Add(bin);
        var item = await f.ItemAsync(6);
        var balance = new InventoryBalance { TenantId = f.Tenant, InventoryItemId = item.Id, WarehouseId = f.Warehouse.Id,
            QuantityOnHand = 6, QuantityAvailable = 6, AverageUnitCost = 10, TotalValue = 60 };
        var layer = new InventoryLayer { TenantId = f.Tenant, InventoryItemId = item.Id, WarehouseId = f.Warehouse.Id,
            LayerNumber = "Opening", RemainingQuantity = 6, OriginalQuantity = 6, UnitCost = 10, RemainingValue = 60 };
        f.Context.AddRange(balance, layer); await f.Context.SaveChangesAsync();
        await f.Service.EnsureItemAssignmentAsync(f.Warehouse.Id, item.Id, f.Actor);
        balance.LocationId.Should().Be(bin.Id); balance.QuantityOnHand.Should().Be(6); balance.TotalValue.Should().Be(60);
        layer.LocationId.Should().Be(bin.Id); layer.RemainingQuantity.Should().Be(6); layer.RemainingValue.Should().Be(60);
        (await f.Context.Set<WarehouseQuantity>().SingleAsync()).CurrentStock.Should().Be(6);
    }

    [Fact]
    public async Task Mismatching_existing_valuation_rejects_before_operational_or_valuation_quantities_change()
    {
        await using var f = await Fixture.CreateAsync();
        var bin = f.Bin("Default", true); f.Context.Add(bin);
        var item = await f.ItemAsync(6);
        var balance = new InventoryBalance { TenantId = f.Tenant, InventoryItemId = item.Id, WarehouseId = f.Warehouse.Id,
            QuantityOnHand = 5, QuantityAvailable = 5, TotalValue = 50 };
        f.Context.Add(balance); await f.Context.SaveChangesAsync();
        Func<Task> assign = () => f.Service.EnsureItemAssignmentAsync(f.Warehouse.Id, item.Id, f.Actor);
        await assign.Should()
            .ThrowAsync<InvalidOperationException>().WithMessage("*valuation balance*");
        (await f.Context.Set<InventoryLocation>().CountAsync()).Should().Be(0);
        balance.LocationId.Should().BeNull(); balance.QuantityOnHand.Should().Be(5); balance.TotalValue.Should().Be(50);
        (await f.Context.Set<WarehouseQuantity>().SingleAsync()).CurrentStock.Should().Be(6);
    }

    [Theory]
    [InlineData("IsActive")]
    [InlineData("IsConsignmentBin")]
    [InlineData("IsQuarantineLocation")]
    [InlineData("IsInspectionLocation")]
    [InlineData("IsInTransitLocation")]
    [InlineData("IsShippingLocation")]
    [InlineData("IsStagingLocation")]
    [InlineData("IsReturnLocation")]
    [InlineData("IsDamageLocation")]
    public async Task Inactive_and_special_purpose_bins_cannot_be_defaults(string property)
    {
        await using var f = await Fixture.CreateAsync();
        var normal = f.Bin("Default", true); var invalid = f.Bin("Invalid");
        typeof(WarehouseLocation).GetProperty(property)!.SetValue(invalid, property != "IsActive");
        f.Context.AddRange(normal, invalid); await f.Context.SaveChangesAsync();
        Func<Task> select = () => f.Service.SetDefaultAsync(invalid, f.Actor);
        await select.Should().ThrowAsync<InvalidOperationException>();
        normal.IsDefault.Should().BeTrue(); invalid.IsDefault.Should().BeFalse();
    }

    [Fact]
    public async Task Foreign_warehouse_bin_or_actor_cannot_be_used()
    {
        await using var f = await Fixture.CreateAsync();
        var foreign = new Warehouse { TenantId = Guid.NewGuid(), Code = "FOREIGN", Name = "Foreign", IsActive = true };
        f.Context.Add(foreign); await f.Context.SaveChangesAsync();
        Func<Task> foreignWarehouse = () => f.Service.GetOrCreateAsync(foreign.Id, f.Actor);
        Func<Task> wrongActor = () => f.Service.GetOrCreateAsync(f.Warehouse.Id, Guid.NewGuid());
        await foreignWarehouse.Should().ThrowAsync<InvalidOperationException>();
        await wrongActor.Should().ThrowAsync<UnauthorizedAccessException>();
        var foreignBin = f.Bin("Foreign"); foreignBin.TenantId = foreign.TenantId;
        Func<Task> foreignLocation = () => f.Service.SetDefaultAsync(foreignBin, f.Actor);
        await foreignLocation.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Model_enforces_one_active_default_per_tenant_and_warehouse()
    {
        await using var f = await Fixture.CreateAsync();
        var index = f.Context.Model.FindEntityType(typeof(WarehouseLocation))!.GetIndexes()
            .Single(x => x.GetDatabaseName() == "UX_WarehouseLocations_Default");
        index.IsUnique.Should().BeTrue();
        index.Properties.Select(x => x.Name).Should().Equal("TenantId", "WarehouseId");
        index.GetFilter().Should().Contain("[IsDefault] = 1").And.Contain("[IsActive] = 1").And.Contain("[IsDeleted] = 0");
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public Guid Tenant { get; } = Guid.NewGuid();
        public Guid Actor { get; } = Guid.NewGuid();
        public ApplicationDbContext Context { get; }
        public UnitOfWork Unit { get; }
        public WarehouseDefaultLocationService Service { get; }
        public Warehouse Warehouse { get; }
        private Fixture()
        {
            Context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"default-bin-{Guid.NewGuid():N}")
                .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
            Unit = new UnitOfWork(Context);
            var user = new Mock<ICurrentUserProvider>();
            user.SetupGet(x => x.TenantId).Returns(Tenant); user.SetupGet(x => x.UserId).Returns(Actor);
            user.SetupGet(x => x.Username).Returns("default-bin-test"); user.SetupGet(x => x.IsAuthenticated).Returns(true);
            Service = new WarehouseDefaultLocationService(Unit, user.Object);
            Warehouse = new Warehouse { TenantId = Tenant, Code = "TEST", Name = "Test warehouse", IsActive = true };
        }
        public static async Task<Fixture> CreateAsync()
        {
            var f = new Fixture(); f.Context.Add(f.Warehouse); await f.Context.SaveChangesAsync(); return f;
        }
        public WarehouseLocation Bin(string code, bool isDefault = false) => new()
            { TenantId = Tenant, WarehouseId = Warehouse.Id, LocationCode = code, IsDefault = isDefault, IsActive = true, LocationType = "Bin" };
        public async Task<InventoryItem> ItemAsync(decimal quantity)
        {
            var item = new InventoryItem { TenantId = Tenant, ItemCode = "ITEM", Name = "Test item", UnitOfMeasure = "EA",
                ItemType = ItemType.StockItem, Status = ItemStatus.Active, CurrentStock = quantity, AvailableStock = quantity, AverageCost = 10, StandardCost = 10 };
            Context.Add(item); Context.Add(new WarehouseQuantity { TenantId = Tenant, InventoryItemId = item.Id,
                WarehouseId = Warehouse.Id, CurrentStock = quantity, AvailableStock = quantity, AverageCost = 10 });
            await Context.SaveChangesAsync(); return item;
        }
        public InventoryLocation Quantity(InventoryItem item, WarehouseLocation bin, decimal quantity) => new()
            { TenantId = Tenant, InventoryItemId = item.Id, LocationId = bin.Id, Quantity = quantity, AvailableQuantity = quantity, AverageCost = 10 };
        public async ValueTask DisposeAsync() { Unit.Dispose(); await Context.DisposeAsync(); }
    }
}
