using System.Linq.Expressions;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Inventory;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Inventory;

public sealed class InventoryTransitStockReportTests
{
    [Fact]
    public async Task Split_picks_keep_separate_outstanding_and_retained_value()
    {
        await using var f = new Fixture();
        var first = f.AddTransfer("SPLIT", TransferStatus.InTransit);
        first.Quantity = 60;
        var secondBin = new WarehouseLocation { Id = Guid.NewGuid(), TenantId = f.Tenant,
            WarehouseId = first.SourceInventoryWarehouseId, Warehouse = first.SourceInventoryWarehouse, LocationCode = "SECOND" };
        var second = new InventoryTransferDispatchAllocation { Id = Guid.NewGuid(), TenantId = f.Tenant,
            InventoryTransferAction = first.InventoryTransferAction, InventoryTransferActionId = first.InventoryTransferActionId,
            InventoryTransferItem = first.InventoryTransferItem, InventoryTransferItemId = first.InventoryTransferItemId,
            SourceLocation = secondBin, SourceLocationId = secondBin.Id,
            SourceInventoryWarehouse = first.SourceInventoryWarehouse, SourceInventoryWarehouseId = first.SourceInventoryWarehouseId,
            InTransitLocation = first.InTransitLocation, InTransitLocationId = first.InTransitLocationId, Quantity = 40 };
        f.Db.Add(second);
        f.AddLeg(first, "TransitIn", 60, 600);
        f.AddLeg(first, "TransitOut", 52, 520);
        f.AddLeg(first, "DestinationIn", 52, 520);
        f.AddLeg(second, "TransitIn", 40, 800);
        await f.Db.SaveChangesAsync();

        var result = await f.Service.GetTransitStockAsync();

        Assert.Equal(2, result.Items.Count);
        var remainder = Assert.Single(result.Items, x => x.DispatchAllocationId == first.Id);
        Assert.Equal(8m, remainder.InTransitQuantity);
        Assert.Equal(80m, remainder.InTransitValue);
        var untouched = Assert.Single(result.Items, x => x.DispatchAllocationId == second.Id);
        Assert.Equal(40m, untouched.InTransitQuantity);
        Assert.Equal(800m, untouched.InTransitValue);
        Assert.Equal(0m, untouched.ReceivedQuantity);
    }

    [Fact]
    public async Task Posted_legs_retain_partial_stock_on_closed_document_without_counting_source_or_destination_twice()
    {
        await using var f = new Fixture();
        var allocation = f.AddTransfer("PARTIAL", TransferStatus.Completed);
        f.AddLeg(allocation, "SourceOut", 100, 1250);
        f.AddLeg(allocation, "TransitIn", 100, 1250);
        f.AddLeg(allocation, "TransitOut", 95, 1187.5m);
        f.AddLeg(allocation, "DestinationIn", 92, 1150);
        f.AddLeg(allocation, "SourceReturn", 3, 37.5m);
        f.AddLeg(allocation, "TransitIn", 900, 9999, posted: false);
        f.AddLeg(allocation, "TransitIn", 800, 9999, deleted: true);
        f.AddLeg(allocation, "TransitIn", 700, 9999, tenant: Guid.NewGuid());
        var complete = f.AddTransfer("COMPLETE", TransferStatus.Received);
        f.AddLeg(complete, "TransitIn", 10, 100);
        f.AddLeg(complete, "TransitOut", 10, 100);
        await f.Db.SaveChangesAsync();

        var result = await f.Service.GetTransitStockAsync();

        var row = Assert.Single(result.Items);
        Assert.Equal("PARTIAL", row.TransferNumber);
        Assert.Equal(100m, row.DispatchedQuantity);
        Assert.Equal(92m, row.ReceivedQuantity);
        Assert.Equal(3m, row.ReturnedQuantity);
        Assert.Equal(5m, row.InTransitQuantity);
        Assert.Equal(62.5m, row.InTransitValue);
        Assert.Equal("Carrier at dispatch", row.CarrierName);
        Assert.Equal(0, result.LegacyReconciliationRequiredCount);
    }

    [Fact]
    public async Task Legacy_evidence_is_flagged_without_invented_stock_and_denied_transfers_do_not_leak()
    {
        await using var f = new Fixture();
        var legacy = f.AddTransfer("LEGACY", TransferStatus.InTransit);
        var movement = f.AddLeg(legacy, "SourceOut", 100, 1000);
        movement.TransferDispatchAllocationId = null;
        movement.TransferLeg = null;
        var denied = f.AddTransfer("DENIED", TransferStatus.InTransit);
        f.DeniedWarehouseIds.Add(denied.InventoryTransferItem.InventoryTransfer.DestinationWarehouseId);
        f.AddLeg(denied, "TransitIn", 500, 5000);
        var hiddenLegacy = f.AddLeg(denied, "SourceOut", 500, 5000);
        hiddenLegacy.TransferDispatchAllocationId = null;
        hiddenLegacy.TransferLeg = null;
        await f.Db.SaveChangesAsync();

        var result = await f.Service.GetTransitStockAsync();

        Assert.Empty(result.Items);
        Assert.Equal(1, result.LegacyReconciliationRequiredCount);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public Guid Tenant { get; } = Guid.NewGuid();
        public HashSet<Guid> DeniedWarehouseIds { get; } = [];
        public ApplicationDbContext Db { get; } = new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        public InventoryTransferService Service { get; }

        public Fixture()
        {
            var actor = new Mock<ICurrentUserProvider>();
            actor.SetupGet(x => x.TenantId).Returns(Tenant);
            var repository = new Mock<IInventoryTransferRepository>();
            repository.Setup(x => x.GetQueryable(It.IsAny<Expression<Func<InventoryTransfer, bool>>>()))
                .Returns((Expression<Func<InventoryTransfer, bool>> predicate) => Db.Set<InventoryTransfer>().Where(predicate));
            var access = new Mock<IProcurementAccessControlService>();
            access.Setup(x => x.CheckCapabilityAsync(It.IsAny<ProcurementAccessCapabilityRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ProcurementAccessCapabilityRequest request, string _, CancellationToken _) =>
                    new ProcurementAccessCapabilityDecisionDto { Allowed = request.WarehouseId.HasValue && !DeniedWarehouseIds.Contains(request.WarehouseId.Value) });
            Service = new InventoryTransferService(repository.Object, Mock.Of<IInventoryTransferItemRepository>(),
                Mock.Of<IInventoryItemRepository>(), Mock.Of<IWarehouseRepository>(), Mock.Of<IWarehouseLocationRepository>(),
                Mock.Of<IWarehouseQuantityRepository>(), Mock.Of<IInventoryLocationRepository>(), Mock.Of<IStockMovementRepository>(),
                Mock.Of<IConsignmentSettlementService>(), new UnitOfWork(Db), actor.Object, Mock.Of<IWorkflowIntegrationService>(),
                Mock.Of<IWorkflowStatusAdapterRegistry>(), Mock.Of<IInventoryTrackingControlService>(), Mock.Of<IInventoryNegativeStockControlService>(),
                access.Object, Mock.Of<IProcurementControlEventService>(), NullLogger<InventoryTransferService>.Instance);
        }

        public InventoryTransferDispatchAllocation AddTransfer(string number, TransferStatus status)
        {
            Warehouse Warehouse(string suffix) => new() { Id = Guid.NewGuid(), TenantId = Tenant, Code = number + suffix, Name = suffix };
            var source = Warehouse("SOURCE"); var destination = Warehouse("DEST"); var transit = Warehouse("TRANSIT");
            var sourceBin = new WarehouseLocation { Id = Guid.NewGuid(), TenantId = Tenant, Warehouse = source, WarehouseId = source.Id, LocationCode = "SOURCE" };
            var transitBin = new WarehouseLocation { Id = Guid.NewGuid(), TenantId = Tenant, Warehouse = transit, WarehouseId = transit.Id, LocationCode = "TRANSIT", IsInTransitLocation = true };
            var item = new InventoryItem { Id = Guid.NewGuid(), TenantId = Tenant, ItemCode = number, Name = number, UnitOfMeasure = "EA" };
            var transfer = new InventoryTransfer { Id = Guid.NewGuid(), TenantId = Tenant, TransferNumber = number,
                SourceWarehouse = source, SourceWarehouseId = source.Id, DestinationWarehouse = destination, DestinationWarehouseId = destination.Id,
                Status = status, RowVersion = [1] };
            var line = new InventoryTransferItem { Id = Guid.NewGuid(), TenantId = Tenant, InventoryTransfer = transfer,
                InventoryTransferId = transfer.Id, InventoryItem = item, InventoryItemId = item.Id, ItemCode = item.ItemCode,
                ItemName = item.Name, RequestedQuantity = 100, SourceLocationId = sourceBin.Id };
            transfer.Items.Add(line);
            var action = new InventoryTransferAction { Id = Guid.NewGuid(), TenantId = Tenant, InventoryTransfer = transfer,
                InventoryTransferId = transfer.Id, ActionType = InventoryTransferActionType.Dispatched, OccurredAtUtc = DateTime.UtcNow,
                IdempotencyKey = number, PayloadHash = "hash", IntegrityHash = "hash", CorrelationId = number };
            var allocation = new InventoryTransferDispatchAllocation { Id = Guid.NewGuid(), TenantId = Tenant,
                InventoryTransferAction = action, InventoryTransferActionId = action.Id, InventoryTransferItem = line, InventoryTransferItemId = line.Id,
                SourceLocation = sourceBin, SourceLocationId = sourceBin.Id, SourceInventoryWarehouse = source, SourceInventoryWarehouseId = source.Id,
                InTransitLocation = transitBin, InTransitLocationId = transitBin.Id, Quantity = 100, CarrierName = "Carrier at dispatch" };
            Db.Add(allocation);
            return allocation;
        }

        public InventoryMovement AddLeg(InventoryTransferDispatchAllocation allocation, string leg, decimal quantity, decimal value,
            bool posted = true, bool deleted = false, Guid? tenant = null)
        {
            var line = allocation.InventoryTransferItem;
            var outgoing = leg is "SourceOut" or "TransitOut";
            var warehouseId = leg switch
            {
                "SourceOut" or "SourceReturn" => allocation.SourceInventoryWarehouseId,
                "DestinationIn" => line.InventoryTransfer.DestinationWarehouseId,
                _ => allocation.InTransitLocation.WarehouseId
            };
            Guid? locationId = leg switch
            {
                "SourceOut" or "SourceReturn" => allocation.SourceLocationId,
                "DestinationIn" => null,
                _ => allocation.InTransitLocationId
            };
            var movement = new InventoryMovement { Id = Guid.NewGuid(), TenantId = tenant ?? Tenant,
                MovementNumber = Guid.NewGuid().ToString(), InventoryItemId = line.InventoryItemId,
                WarehouseId = warehouseId, LocationId = locationId,
                ReferenceType = ReferenceType.Transfer, ReferenceId = line.Id, TransferDispatchAllocationId = allocation.Id,
                TransferLeg = leg, MovementType = outgoing ? InventoryMovementType.TransferOut : InventoryMovementType.TransferIn,
                Direction = outgoing ? MovementDirection.Out : MovementDirection.In, Quantity = quantity, TotalValue = value,
                IsPosted = posted, IsDeleted = deleted };
            Db.Add(movement);
            return movement;
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
