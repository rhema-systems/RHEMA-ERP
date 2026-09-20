using System.Reflection;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Inventory;
using ErpSystem.Data;
using ErpSystem.Data.Repositories.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Inventory;

public sealed class InventoryTransferNewBinBalanceTests
{
    [Fact]
    public async Task First_receipt_retains_added_state_and_second_receipt_updates_existing_row()
    {
        await using var f = new Fixture();
        await f.Adjust(1);
        var added = Assert.Single(f.Db.ChangeTracker.Entries<InventoryLocation>());
        Assert.Equal(EntityState.Added, added.State);
        Assert.Equal(1m, added.Entity.Quantity);
        Assert.Equal(f.Tenant, added.Entity.TenantId);
        Assert.Equal(f.Actor, added.Entity.CreatedById);
        await f.Db.SaveChangesAsync();
        f.Db.ChangeTracker.Clear();
        f.RecreateService();
        await f.Adjust(1);
        await f.Db.SaveChangesAsync();
        var saved = await f.Db.Set<InventoryLocation>().SingleAsync();
        Assert.Equal(2m, saved.Quantity);
        Assert.Equal(2m, saved.AvailableQuantity);
        Assert.Equal(f.Actor, saved.LastModifiedById);
    }

    [Fact]
    public async Task Two_lines_in_the_same_new_destination_bin_share_one_pending_insert()
    {
        await using var f = new Fixture();
        await f.Adjust(1);
        await f.Adjust(2);
        var added = Assert.Single(f.Db.ChangeTracker.Entries<InventoryLocation>());
        Assert.Equal(EntityState.Added, added.State);
        Assert.Equal(3m, added.Entity.Quantity);
        await f.Db.SaveChangesAsync();
        Assert.Equal(3m, (await f.Db.Set<InventoryLocation>().SingleAsync()).AvailableQuantity);
    }

    [Fact]
    public async Task Existing_bin_scalar_update_preserves_allocations_and_does_not_add_duplicate()
    {
        await using var f = new Fixture();
        f.Db.Add(new InventoryLocation { TenantId = f.Tenant, InventoryItemId = f.Item, LocationId = f.Bin,
            Quantity = 5, AllocatedQuantity = 2, AvailableQuantity = 3 });
        await f.Db.SaveChangesAsync();
        f.Db.ChangeTracker.Clear();
        await f.Adjust(2);
        await f.Db.SaveChangesAsync();
        var saved = await f.Db.Set<InventoryLocation>().SingleAsync();
        Assert.Equal(7m, saved.Quantity);
        Assert.Equal(2m, saved.AllocatedQuantity);
        Assert.Equal(5m, saved.AvailableQuantity);
    }

    [Fact]
    public async Task Zero_increment_does_not_create_a_bin_balance()
    {
        await using var f = new Fixture();
        await f.Adjust(0);
        Assert.Empty(f.Db.ChangeTracker.Entries<InventoryLocation>());
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public Guid Tenant { get; } = Guid.NewGuid();
        public Guid Actor { get; } = Guid.NewGuid();
        public Guid Item { get; } = Guid.NewGuid();
        public Guid Bin { get; } = Guid.NewGuid();
        public ApplicationDbContext Db { get; } = new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);
        private InventoryTransferService Service = null!;
        public Fixture()
        {
            var warehouse = new Warehouse { TenantId = Tenant, Code = "RECEIPT", Name = "Receipt warehouse" };
            Db.AddRange(warehouse,
                new InventoryItem { Id = Item, TenantId = Tenant, ItemCode = "RECEIPT-ITEM", Name = "Receipt item", UnitOfMeasure = "EA" },
                new WarehouseLocation { Id = Bin, TenantId = Tenant, WarehouseId = warehouse.Id,
                    LocationCode = "RECEIPT-BIN", Name = "Receipt bin", IsActive = true });
            RecreateService();
        }
        public void RecreateService()
        {
            var actor = new Mock<ICurrentUserProvider>();
            actor.SetupGet(value => value.TenantId).Returns(Tenant);
            actor.SetupGet(value => value.UserId).Returns(Actor);
            Service = new InventoryTransferService(Mock.Of<IInventoryTransferRepository>(), Mock.Of<IInventoryTransferItemRepository>(),
                Mock.Of<IInventoryItemRepository>(), Mock.Of<IWarehouseRepository>(), Mock.Of<IWarehouseLocationRepository>(),
                Mock.Of<IWarehouseQuantityRepository>(), new InventoryLocationRepository(Db), Mock.Of<IStockMovementRepository>(),
                Mock.Of<IConsignmentSettlementService>(), new UnitOfWork(Db), actor.Object, Mock.Of<IWorkflowIntegrationService>(),
                Mock.Of<IWorkflowStatusAdapterRegistry>(), Mock.Of<IInventoryTrackingControlService>(), Mock.Of<IInventoryNegativeStockControlService>(),
                Mock.Of<IProcurementAccessControlService>(), Mock.Of<IProcurementControlEventService>(), NullLogger<InventoryTransferService>.Instance);
        }
        public Task Adjust(decimal delta) => (Task)typeof(InventoryTransferService)
            .GetMethod("AdjustInventoryLocationQuantityAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(Service, [Bin, Item, delta])!;
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
