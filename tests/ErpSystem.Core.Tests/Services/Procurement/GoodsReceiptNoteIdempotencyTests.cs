using System.Reflection;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Services.Inventory;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class GoodsReceiptNoteIdempotencyTests
{
    [Fact]
    public void Inventory_posting_uses_and_updates_the_receiving_bin_before_directed_putaway()
    {
        var receivingLocationId = Guid.NewGuid();
        var storageLocationId = Guid.NewGuid();
        var grn = new GoodsReceiptNote { ReceivingLocationId = receivingLocationId };
        var line = new GoodsReceiptNoteItem
        {
            StorageLocationId = storageLocationId,
            AcceptedQuantity = 3m,
            UnitCost = 10m
        };
        var location = new InventoryLocation
        {
            LocationId = receivingLocationId,
            Quantity = 2m,
            AllocatedQuantity = 1m,
            AvailableQuantity = 1m,
            AverageCost = 4m
        };
        var movementAtUtc = new DateTime(2026, 8, 3, 18, 30, 0, DateTimeKind.Utc);

        GoodsReceiptNoteService.ResolveReceiptLocation(grn, line)
            .Should().Be(receivingLocationId);
        GoodsReceiptNoteService.ApplyReceiptToInventoryLocation(
            location, line.AcceptedQuantity, line.UnitCost, movementAtUtc);

        location.Quantity.Should().Be(5m);
        location.AvailableQuantity.Should().Be(4m);
        location.AverageCost.Should().Be(7.6m);
        location.LastMovementDate.Should().Be(movementAtUtc);
    }

    [Fact]
    public void DerivedReceiptKeyHashesTheCompleteSourceKeyWithoutTruncationCollisions()
    {
        var method = typeof(GoodsReceiptNoteService).GetMethod(
            "BuildGovernedReceiptIdempotencyKey",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        var commonPrefix = new string('a', 99);

        var first = (string)method.Invoke(null, [$"{commonPrefix}1"])!;
        var second = (string)method.Invoke(null, [$"{commonPrefix}2"])!;
        var retry = (string)method.Invoke(null, [$"{commonPrefix}1"])!;

        first.Should().HaveLength(68).And.StartWith("grn:");
        first.Should().NotBe(second);
        retry.Should().Be(first);
    }

    [Fact]
    public void GoodsReceiptReplayFingerprintBindsAllEffectiveRequestFields()
    {
        var purchaseOrderId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var receivingLocationId = Guid.NewGuid();
        var firstLineId = Guid.NewGuid();
        var firstItemId = Guid.NewGuid();
        var secondLineId = Guid.NewGuid();
        var secondItemId = Guid.NewGuid();

        CreateGoodsReceiptNoteDto Request() => new()
        {
            PurchaseOrderId = purchaseOrderId,
            SupplierId = supplierId,
            WarehouseId = warehouseId,
            ReceivingLocationId = receivingLocationId,
            DeliveryNoteNumber = "DN-001",
            VehicleNumber = "GT-1000",
            DriverName = "Controlled Driver",
            RequiresInspection = true,
            Notes = "Original payload",
            Items =
            [
                new CreateGoodsReceiptNoteItemDto
                {
                    PurchaseOrderItemId = firstLineId,
                    InventoryItemId = firstItemId,
                    ReceivedQuantity = 2m,
                    UnitCost = 10m,
                    StorageLocationId = receivingLocationId,
                    LotNumber = "LOT-1",
                    SerialNumber = "SER-1",
                    ExpiryDate = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    Notes = "Line one"
                },
                new CreateGoodsReceiptNoteItemDto
                {
                    PurchaseOrderItemId = secondLineId,
                    InventoryItemId = secondItemId,
                    ReceivedQuantity = 3m,
                    UnitCost = 12m,
                    StorageLocationId = receivingLocationId,
                    Notes = "Line two"
                }
            ]
        };

        var original = Request();
        var expected = GoodsReceiptNoteService.BuildIdempotencyRequestHash(original, supplierId);
        var reordered = Request();
        reordered.Items.Reverse();
        GoodsReceiptNoteService.BuildIdempotencyRequestHash(reordered, supplierId)
            .Should().Be(expected, "receipt-line order is not part of the effective payload");

        var changedWarehouse = Request();
        changedWarehouse.WarehouseId = Guid.NewGuid();
        var changedSupplier = Request();
        changedSupplier.SupplierId = Guid.NewGuid();
        var changedLine = Request();
        changedLine.Items[0].ReceivedQuantity++;
        var changedLocation = Request();
        changedLocation.Items[0].StorageLocationId = Guid.NewGuid();
        var changedDelivery = Request();
        changedDelivery.DeliveryNoteNumber = "DN-002";
        var changedDeliveryWhitespace = Request();
        changedDeliveryWhitespace.DeliveryNoteNumber = "DN-001 ";

        new[]
        {
            changedWarehouse,
            changedSupplier,
            changedLine,
            changedLocation,
            changedDelivery,
            changedDeliveryWhitespace
        }.Select(request => GoodsReceiptNoteService.BuildIdempotencyRequestHash(request, supplierId))
            .Should().OnlyContain(hash => hash != expected);
    }
}
