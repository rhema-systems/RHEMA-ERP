using System.Reflection;
using System.Text.Json;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Inventory;
using ErpSystem.Core.Services.Workflow;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Inventory;

public sealed class InventoryTransferAllocationTrackingTests : IDisposable
{
    private readonly ApplicationDbContext _db = new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);
    private readonly Guid _tenant = Guid.NewGuid();
    private readonly InventoryTransferItem _line = new() { InventoryItemId = Guid.NewGuid(), RequestedQuantity = 10, UnitOfMeasure = "EA" };

    [Fact]
    public void Split_serial_picks_consume_only_their_own_identity_and_destination()
    {
        var destination1 = Guid.NewGuid(); var destination2 = Guid.NewGuid();
        var first = Dispatch(Scan("SERIAL-1", 1, Guid.NewGuid()));
        var second = Dispatch(Scan("SERIAL-2", 1, Guid.NewGuid()));
        var pool = new List<InventoryTransactionScanLineDto> { Scan("SERIAL-2", 1, destination2), Scan("SERIAL-1", 1, destination1) };
        Allocate(first, [], 1, destination1, pool).Should().ContainSingle().Which.SerialNumber.Should().Be("SERIAL-1");
        Allocate(second, [], 1, destination2, pool).Should().ContainSingle().Which.SerialNumber.Should().Be("SERIAL-2");
        pool.Sum(x => x.BaseQuantity).Should().Be(0);
    }

    [Fact]
    public void Shared_lot_is_partitioned_across_picks_and_partial_destination_receipts()
    {
        var destination1 = Guid.NewGuid(); var destination2 = Guid.NewGuid();
        var first = Dispatch(Scan(null, 3, Guid.NewGuid()));
        var second = Dispatch(Scan(null, 4, Guid.NewGuid()));
        var pool = new List<InventoryTransactionScanLineDto> { Scan(null, 4, destination1), Scan(null, 2, destination2) };
        var firstReceipt = Allocate(first, [], 3, destination1, pool);
        var secondReceipt = Allocate(second, [], 1, destination1, pool);
        var prior = new InventoryTransferReceiptAllocation { TrackingSnapshotJson = JsonSerializer.Serialize(secondReceipt) };
        Allocate(second, [prior], 2, destination2, pool).Sum(x => x.BaseQuantity).Should().Be(2);
        firstReceipt.Sum(x => x.BaseQuantity).Should().Be(3);
        pool.Sum(x => x.BaseQuantity).Should().Be(0);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Receipt_rejects_reused_serial_or_wrong_destination(bool alreadyReceived)
    {
        var destination = Guid.NewGuid();
        var dispatch = Dispatch(Scan("SERIAL-1", 1, Guid.NewGuid()));
        var prior = alreadyReceived ? new[] { new InventoryTransferReceiptAllocation { TrackingSnapshotJson = dispatch.TrackingSnapshotJson } } : [];
        var pool = new List<InventoryTransactionScanLineDto> { Scan("SERIAL-1", 1, alreadyReceived ? destination : Guid.NewGuid()) };
        Action act = () => Allocate(dispatch, prior, 1, destination, pool);
        act.Should().Throw<TargetInvocationException>().Which.InnerException.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public void Explicit_wrong_lot_is_not_ignored_for_single_lot_dispatch()
    {
        var destination = Guid.NewGuid();
        var scan = Scan(null, 1, destination); scan.LotNumber = "WRONG";
        Action act = () => Allocate(Dispatch(Scan(null, 3, Guid.NewGuid())), [], 1, destination, [scan]);
        act.Should().Throw<TargetInvocationException>().Which.InnerException.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task First_receipt_uses_all_immutable_dispatch_snapshots_and_ignores_transit_in_events()
    {
        var transfer = new InventoryTransfer { TenantId = _tenant, TransferNumber = "TRF-TRACK" };
        _line.TenantId = _tenant; _line.InventoryTransferId = transfer.Id;
        _line.ShipmentScanTrackingLinesJson = JsonSerializer.Serialize(new[] { Scan("LATEST", 1, null) });
        var original = Scan("EARLIER", 1, null);
        var originalDispatch = Dispatch(original);
        _db.Add(originalDispatch);
        _db.Add(Dispatch(Scan("LATEST", 1, null)));
        _db.Add(new InventoryTraceabilityEvent { TenantId = _tenant, InventoryItemId = _line.InventoryItemId,
            ReferenceType = "InventoryTransfer", ReferenceId = transfer.Id, ReferenceLineId = _line.Id,
            Direction = InventoryTrackingDirection.TransferIn, Quantity = 1, SerialNumber = "EARLIER", LotNumber = "LOT" });
        await _db.SaveChangesAsync();
        var service = Service();
        await ValidateReceipt(service, transfer, [original]);
        _db.Add(new InventoryTransferReceiptAllocation { TenantId = _tenant, DispatchAllocationId = originalDispatch.Id,
            Quantity = 1, TrackingSnapshotJson = JsonSerializer.Serialize(new[] { original }) });
        await _db.SaveChangesAsync();
        await FluentActions.Awaiting(() => ValidateReceipt(service, transfer, [original]))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Partial_shipment_scans_can_retain_multiple_source_bins()
    {
        var warehouse = Guid.NewGuid();
        var transfer = new InventoryTransfer { TenantId = _tenant, TransferNumber = "TRF-SPLIT", Status = TransferStatus.Approved,
            SourceWarehouseId = warehouse, DestinationWarehouseId = Guid.NewGuid() };
        _line.TenantId = _tenant; _line.InventoryTransferId = transfer.Id; transfer.Items.Add(_line);
        var first = new WarehouseLocation { TenantId = _tenant, WarehouseId = warehouse, LocationCode = "A", IsActive = true };
        var second = new WarehouseLocation { TenantId = _tenant, WarehouseId = warehouse, LocationCode = "B", IsActive = true };
        _db.Add(transfer); await _db.SaveChangesAsync();
        var locations = new Mock<IWarehouseLocationRepository>();
        locations.Setup(x => x.GetByIdAsync(first.Id)).ReturnsAsync(first);
        locations.Setup(x => x.GetByIdAsync(second.Id)).ReturnsAsync(second);
        var scans = new[] { Scan("SERIAL-1", 1, first.Id), Scan("SERIAL-2", 1, second.Id) };
        await Service(locations.Object).ApplyScanMetadataAsync(transfer.Id, InventoryScanOperation.TransferShipment, scans, Guid.NewGuid());
        _line.SourceLocationId.Should().BeNull();
        var saved = JsonSerializer.Deserialize<List<InventoryTransactionScanLineDto>>(_line.ShipmentScanTrackingLinesJson!)!;
        saved.Sum(x => x.BaseQuantity).Should().Be(2);
        saved.Select(x => x.LocationId).Should().BeEquivalentTo(new Guid?[] { first.Id, second.Id });
    }

    [Fact]
    public void Repeated_serial_scans_are_rejected_even_across_different_bins()
    {
        Action act = () => typeof(InventoryTransferService).GetMethod("EnsureUniqueScanSerials", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [new List<InventoryTransactionScanLineDto> { Scan("SERIAL", 1, Guid.NewGuid()), Scan(" serial ", 1, Guid.NewGuid()) }]);
        act.Should().Throw<TargetInvocationException>().Which.InnerException.Should().BeOfType<InvalidOperationException>();
    }

    [Theory]
    [InlineData("valid")]
    [InlineData("quantity")]
    [InlineData("identity")]
    [InlineData("key")]
    [InlineData("index")]
    public void Internal_transit_authority_binds_exact_snapshot_row_and_event_key(string change)
    {
        var allocation = Guid.NewGuid();
        var row = Scan("SERIAL-1", 1, Guid.NewGuid());
        var request = new InventoryTrackingMutationRequest { InventoryItemId = _line.InventoryItemId,
            ReferenceLineId = _line.Id, Quantity = 1, LotNumber = "LOT", SerialNumber = "SERIAL-1",
            EventKey = $"transfer-allocation:{allocation:N}:transit-in:1" };
        if (change == "quantity") request.Quantity = 0.5m;
        if (change == "identity") request.SerialNumber = "SERIAL-2";
        if (change == "key") request.EventKey = $"transfer-allocation:{Guid.NewGuid():N}:transit-in:1";
        if (change == "index") request.EventKey = $"transfer-allocation:{allocation:N}:transit-in:01";
        Action act = () => typeof(InventoryTrackingControlService).GetMethod("RequireTransitSnapshotRow", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [request, allocation, "transit-in", JsonSerializer.Serialize(new[] { row })]);
        if (change == "valid") act.Should().NotThrow();
        else act.Should().Throw<TargetInvocationException>().Which.InnerException.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public void Json_cannot_grant_internal_transit_allocation_authority()
    {
        var request = JsonSerializer.Deserialize<InventoryTrackingMutationRequest>(
            $$"""{"TransferDispatchAllocationId":"{{Guid.NewGuid()}}","TransferReceiptAllocationId":"{{Guid.NewGuid()}}"}""")!;
        request.TransferDispatchAllocationId.Should().BeNull();
        request.TransferReceiptAllocationId.Should().BeNull();
    }

    [Theory]
    [InlineData(TransferStatus.Received, InventoryTransferActionType.DiscrepancyResolved, false, InventoryTrackingDirection.TransferOut, true)]
    [InlineData(TransferStatus.Received, InventoryTransferActionType.DiscrepancyResolved, true, InventoryTrackingDirection.TransferOut, true)]
    [InlineData(TransferStatus.InTransit, InventoryTransferActionType.DiscrepancyResolved, false, InventoryTrackingDirection.TransferOut, false)]
    [InlineData(TransferStatus.Completed, InventoryTransferActionType.DiscrepancyResolved, true, InventoryTrackingDirection.TransferOut, false)]
    [InlineData(TransferStatus.InTransit, InventoryTransferActionType.Received, false, InventoryTrackingDirection.TransferOut, true)]
    [InlineData(TransferStatus.Received, InventoryTransferActionType.Received, false, InventoryTrackingDirection.TransferOut, false)]
    [InlineData(TransferStatus.InTransit, InventoryTransferActionType.Received, true, InventoryTrackingDirection.TransferOut, false)]
    [InlineData(TransferStatus.InTransit, InventoryTransferActionType.ShipmentReversed, true, InventoryTrackingDirection.TransferOut, true)]
    [InlineData(TransferStatus.InTransit, InventoryTransferActionType.ShipmentReversed, false, InventoryTrackingDirection.TransferOut, false)]
    [InlineData(TransferStatus.InTransit, InventoryTransferActionType.Received, false, InventoryTrackingDirection.TransferIn, false)]
    public void Transit_receipt_lifecycle_distinguishes_discrepancy_resolution_from_normal_receipt(
        TransferStatus status, InventoryTransferActionType action, bool returned, InventoryTrackingDirection direction, bool allowed)
    {
        var result = (bool)typeof(InventoryTrackingControlService).GetMethod("IsTransitReceiptLifecycleAllowed", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [status, action, returned, direction])!;
        result.Should().Be(allowed);
    }

    private InventoryTransactionScanLineDto Scan(string? serial, decimal quantity, Guid? location) => new()
    { DocumentLineId = _line.Id, InventoryItemId = _line.InventoryItemId, BaseQuantity = quantity, LocationId = location, SerialNumber = serial, LotNumber = "LOT" };
    private InventoryTransferDispatchAllocation Dispatch(params InventoryTransactionScanLineDto[] scans) => new()
    { TenantId = _tenant, InventoryTransferItemId = _line.Id, Quantity = scans.Sum(x => x.BaseQuantity), TrackingSnapshotJson = JsonSerializer.Serialize(scans) };
    private List<InventoryTransactionScanLineDto> Allocate(InventoryTransferDispatchAllocation dispatch,
        IReadOnlyList<InventoryTransferReceiptAllocation> prior, decimal quantity, Guid destination, List<InventoryTransactionScanLineDto> pool) =>
        (List<InventoryTransactionScanLineDto>)typeof(InventoryTransferService).GetMethod("ReceiptAllocationTracking", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [_line, dispatch, prior, quantity, destination, pool])!;
    private Task ValidateReceipt(InventoryTransferService service, InventoryTransfer transfer, IReadOnlyList<InventoryTransactionScanLineDto> scans) =>
        (Task)typeof(InventoryTransferService).GetMethod("EnsureReceiptTrackingSnapshotAsync", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(service, [transfer, _line, scans])!;
    private InventoryTransferService Service(IWarehouseLocationRepository? locations = null)
    {
        var current = new Mock<ICurrentUserProvider>(); current.SetupGet(x => x.TenantId).Returns(_tenant);
        var access = new Mock<IProcurementAccessControlService>();
        access.Setup(x => x.EnforceCapabilityAsync(It.IsAny<ProcurementAccessCapabilityRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementAccessCapabilityDecisionDto { Allowed = true });
        return new InventoryTransferService(Mock.Of<IInventoryTransferRepository>(), Mock.Of<IInventoryTransferItemRepository>(),
            Mock.Of<IInventoryItemRepository>(), Mock.Of<IWarehouseRepository>(), locations ?? Mock.Of<IWarehouseLocationRepository>(),
            Mock.Of<IWarehouseQuantityRepository>(), Mock.Of<IInventoryLocationRepository>(), Mock.Of<IStockMovementRepository>(),
            Mock.Of<IConsignmentSettlementService>(), new UnitOfWork(_db), current.Object, Mock.Of<IWorkflowIntegrationService>(),
            Mock.Of<IWorkflowStatusAdapterRegistry>(), Mock.Of<IInventoryTrackingControlService>(), Mock.Of<IInventoryNegativeStockControlService>(),
            access.Object, Mock.Of<IProcurementControlEventService>(), NullLogger<InventoryTransferService>.Instance);
    }
    public void Dispose() => _db.Dispose();
}
