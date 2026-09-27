using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.Inventory;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Inventory;

public sealed class ReceiptStockAttributionTests
{
    private readonly Guid warehouse = Guid.NewGuid();
    private readonly Guid bin = Guid.NewGuid();
    private InventoryMovement Movement(int order, decimal quantity, decimal closing, MovementDirection direction,
        InventoryMovementType type = InventoryMovementType.PurchaseReceipt) => new()
    {
        Id = Guid.NewGuid(), MovementNumber = $"IMV-{order:D5}", CreatedAt = new DateTime(2026, 1, 1).AddSeconds(order),
        MovementDate = new DateTime(2026, 1, 1).AddSeconds(order), WarehouseId = warehouse, LocationId = bin,
        IsPosted = true, Quantity = quantity, RunningBalance = closing, Direction = direction, MovementType = type
    };
    private InventoryBalance Balance(Guid wh, Guid location, decimal quantity) => new() { WarehouseId = wh, LocationId = location, QuantityOnHand = quantity };

    [Fact]
    public void Weighted_average_revalues_only_proportionately_retained_receipt_stock()
    {
        var old = Movement(1, 50, 50, MovementDirection.In);
        var receipt = Movement(2, 100, 150, MovementDirection.In);
        var issue = Movement(3, 60, 90, MovementDirection.Out, InventoryMovementType.RequisitionIssue);
        var shares = ReceiptStockAttribution.Resolve(receipt.Id, ValuationMethod.WeightedAverage, [old, receipt, issue], [Balance(warehouse, bin, 90)], []);
        Assert.Equal(60, shares.Sum(x => x.Quantity));
    }

    [Fact]
    public void Transfers_retain_origin_through_transit_and_partial_destination_receipt()
    {
        var receipt = Movement(1, 100, 100, MovementDirection.In);
        var dispatch = Guid.NewGuid(); var arrival = Guid.NewGuid();
        var transitWarehouse = Guid.NewGuid(); var transitBin = Guid.NewGuid(); var destination = Guid.NewGuid(); var destBin = Guid.NewGuid();
        var sourceOut = Movement(2, 60, 40, MovementDirection.Out, InventoryMovementType.TransferOut);
        sourceOut.TransferDispatchAllocationId = dispatch; sourceOut.TransferLeg = "SourceOut";
        var transitIn = Movement(3, 60, 60, MovementDirection.In, InventoryMovementType.TransferIn);
        transitIn.WarehouseId = transitWarehouse; transitIn.LocationId = transitBin; transitIn.TransferDispatchAllocationId = dispatch; transitIn.TransferLeg = "TransitIn";
        var transitOut = Movement(4, 25, 35, MovementDirection.Out, InventoryMovementType.TransferOut);
        transitOut.WarehouseId = transitWarehouse; transitOut.LocationId = transitBin; transitOut.TransferDispatchAllocationId = dispatch;
        transitOut.TransferReceiptAllocationId = arrival; transitOut.TransferLeg = "TransitOut";
        var destIn = Movement(5, 25, 25, MovementDirection.In, InventoryMovementType.TransferIn);
        destIn.WarehouseId = destination; destIn.LocationId = destBin; destIn.TransferDispatchAllocationId = dispatch;
        destIn.TransferReceiptAllocationId = arrival; destIn.TransferLeg = "DestinationIn";
        var result = ReceiptStockAttribution.Resolve(receipt.Id, ValuationMethod.WeightedAverage,
            [receipt, sourceOut, transitIn, transitOut, destIn],
            [Balance(warehouse, bin, 40), Balance(transitWarehouse, transitBin, 35), Balance(destination, destBin, 25)], []);
        Assert.Equal(100, result.Sum(x => x.Quantity));
        Assert.Equal(35, result.Single(x => x.WarehouseId == transitWarehouse).Quantity);
        Assert.Equal(25, result.Single(x => x.WarehouseId == destination).Quantity);
    }

    [Fact]
    public void Fifo_consumes_oldest_actual_layer_not_last_movement_layer_hint()
    {
        var old = Movement(1, 50, 50, MovementDirection.In);
        var receipt = Movement(2, 100, 150, MovementDirection.In);
        var a = new InventoryLayer { Id = Guid.NewGuid(), WarehouseId = warehouse, LocationId = bin, OriginalQuantity = 50, RemainingQuantity = 0, LayerDate = old.MovementDate };
        var b = new InventoryLayer { Id = Guid.NewGuid(), WarehouseId = warehouse, LocationId = bin, OriginalQuantity = 100, RemainingQuantity = 90, LayerDate = receipt.MovementDate };
        old.CostLayerId = a.Id; receipt.CostLayerId = b.Id;
        var issue = Movement(3, 60, 90, MovementDirection.Out, InventoryMovementType.RequisitionIssue); issue.CostLayerId = b.Id;
        var result = ReceiptStockAttribution.Resolve(receipt.Id, ValuationMethod.FIFO, [old, receipt, issue], [Balance(warehouse, bin, 90)], [a, b]);
        Assert.Equal(90, result.Single().Quantity); Assert.Equal(b.Id, result.Single().LayerId);
    }

    [Fact]
    public void Transit_release_uses_exact_dispatch_origin_instead_of_blending_other_shipments()
    {
        var first = Movement(1, 100, 100, MovementDirection.In);
        var second = Movement(2, 100, 100, MovementDirection.In); second.LocationId = Guid.NewGuid();
        var dispatchA = Guid.NewGuid(); var dispatchB = Guid.NewGuid(); var arrivalB = Guid.NewGuid();
        var transit = Guid.NewGuid(); var destination = Guid.NewGuid();
        var outA = Movement(3, 100, 0, MovementDirection.Out, InventoryMovementType.TransferOut);
        outA.TransferDispatchAllocationId = dispatchA; outA.TransferLeg = "SourceOut";
        var inA = Movement(4, 100, 100, MovementDirection.In, InventoryMovementType.TransferIn);
        inA.LocationId = transit; inA.TransferDispatchAllocationId = dispatchA; inA.TransferLeg = "TransitIn";
        var outB = Movement(5, 100, 0, MovementDirection.Out, InventoryMovementType.TransferOut);
        outB.LocationId = second.LocationId; outB.TransferDispatchAllocationId = dispatchB; outB.TransferLeg = "SourceOut";
        var inB = Movement(6, 100, 200, MovementDirection.In, InventoryMovementType.TransferIn);
        inB.LocationId = transit; inB.TransferDispatchAllocationId = dispatchB; inB.TransferLeg = "TransitIn";
        var transitOut = Movement(7, 50, 150, MovementDirection.Out, InventoryMovementType.TransferOut);
        transitOut.LocationId = transit; transitOut.TransferDispatchAllocationId = dispatchB;
        transitOut.TransferReceiptAllocationId = arrivalB; transitOut.TransferLeg = "TransitOut";
        var arrived = Movement(8, 50, 50, MovementDirection.In, InventoryMovementType.TransferIn);
        arrived.LocationId = destination; arrived.TransferDispatchAllocationId = dispatchB;
        arrived.TransferReceiptAllocationId = arrivalB; arrived.TransferLeg = "DestinationIn";
        var result = ReceiptStockAttribution.Resolve(first.Id, ValuationMethod.WeightedAverage,
            [first, second, outA, inA, outB, inB, transitOut, arrived],
            [Balance(warehouse, bin, 0), Balance(warehouse, second.LocationId!.Value, 0), Balance(warehouse, transit, 150), Balance(warehouse, destination, 50)], []);
        Assert.Equal(100, result.Single().Quantity);
        Assert.Equal(transit, result.Single().LocationId);
    }

    [Fact]
    public void Sales_selected_lot_consumption_preserves_unselected_original_receipt_provenance()
    {
        var old = Movement(1, 10, 10, MovementDirection.In);
        var selected = Movement(2, 10, 20, MovementDirection.In);
        var oldLayer = new InventoryLayer { WarehouseId = warehouse, LocationId = bin, OriginalQuantity = 10,
            RemainingQuantity = 10, LayerDate = old.MovementDate, LotNumber = "OLD" };
        var selectedLayer = new InventoryLayer { WarehouseId = warehouse, LocationId = bin, OriginalQuantity = 10,
            RemainingQuantity = 0, LayerDate = selected.MovementDate, LotNumber = "SELECTED" };
        old.CostLayerId = oldLayer.Id; selected.CostLayerId = selectedLayer.Id;
        var sale = Movement(3, 10, 10, MovementDirection.Out, InventoryMovementType.SalesIssue);
        sale.ReferenceType = ReferenceType.SalesInvoice; sale.LotNumber = "selected";
        var retained = ReceiptStockAttribution.Resolve(old.Id, ValuationMethod.FIFO,
            [old, selected, sale], [Balance(warehouse, bin, 10)], [oldLayer, selectedLayer]);
        Assert.Equal(10, Assert.Single(retained).Quantity);
        Assert.Empty(ReceiptStockAttribution.Resolve(selected.Id, ValuationMethod.FIFO,
            [old, selected, sale], [Balance(warehouse, bin, 10)], [oldLayer, selectedLayer]));
    }

    [Fact]
    public void Last_issue_consumes_all_fractional_provenance_without_residual()
    {
        var a = Movement(1, 1, 1, MovementDirection.In); var b = Movement(2, 2, 3, MovementDirection.In);
        var issue = Movement(3, 1, 2, MovementDirection.Out, InventoryMovementType.SalesIssue);
        var last = Movement(4, 2, 0, MovementDirection.Out, InventoryMovementType.SalesIssue);
        Assert.Empty(ReceiptStockAttribution.Resolve(a.Id, ValuationMethod.WeightedAverage, [a, b, issue, last], [Balance(warehouse, bin, 0)], []));
    }

    [Fact]
    public void Missing_transfer_lineage_and_mismatched_current_balances_fail_closed()
    {
        var receipt = Movement(1, 10, 10, MovementDirection.In);
        var transfer = Movement(2, 5, 5, MovementDirection.Out, InventoryMovementType.TransferOut);
        Assert.Throws<InvalidOperationException>(() => ReceiptStockAttribution.Resolve(receipt.Id, ValuationMethod.WeightedAverage,
            [receipt, transfer], [Balance(warehouse, bin, 5)], []));
        Assert.Throws<InvalidOperationException>(() => ReceiptStockAttribution.Resolve(receipt.Id, ValuationMethod.WeightedAverage,
            [receipt], [Balance(warehouse, bin, 9)], []));
    }
}
