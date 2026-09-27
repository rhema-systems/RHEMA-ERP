using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Enums;
using System.Data.SqlTypes;

namespace ErpSystem.Core.Services.Inventory;

/// <summary>Quantity provenance only. Values come from the retained ledger, never today's item price.</summary>
public static class ReceiptStockAttribution
{
    public sealed record Share(Guid WarehouseId, Guid? LocationId, Guid? LayerId, decimal Quantity);
    private sealed record Lot(Guid Origin, Guid? Layer, DateTime LayerDate, DateTime Created, decimal Quantity, Guid? TransitAllocation = null);
    private readonly record struct Bin(Guid Warehouse, Guid? Location);

    public static IReadOnlyList<Share> Resolve(Guid originMovementId, ValuationMethod method,
        IReadOnlyList<InventoryMovement> movements, IReadOnlyList<InventoryBalance> balances,
        IReadOnlyList<InventoryLayer> layers)
    {
        if (method is not (ValuationMethod.FIFO or ValuationMethod.WeightedAverage))
            throw Invalid("Receipt attribution supports FIFO and weighted-average inventory only.");
        if (movements.Count(x => x.Id == originMovementId && x.Direction == MovementDirection.In && x.Quantity > 0) != 1)
            throw Invalid("The original receipt movement is unavailable or duplicated.");
        var bins = new Dictionary<Bin, List<Lot>>();
        var inFlight = new Dictionary<(string Kind, Guid Id), List<Lot>>();
        var layerMap = layers.ToDictionary(x => x.Id);
        var ordered = movements.OrderBy(x => x.CreatedAt).ThenBy(x => x.MovementNumber, StringComparer.Ordinal).ThenBy(x => x.Id).ToArray();
        foreach (var movement in ordered)
        {
            if (!movement.IsPosted || movement.IsDeleted) throw Invalid("Only retained posted movement history can establish receipt attribution.");
            if (movement.Quantity == 0) continue; // Value-only events do not manufacture or consume units.
            if (movement.Quantity < 0) throw Invalid("Movement quantities must retain their original direction and positive magnitude.");
            var bin = new Bin(movement.WarehouseId, movement.LocationId);
            if (!bins.TryGetValue(bin, out var stock)) bins[bin] = stock = [];
            var leg = movement.TransferLeg;
            var isTransfer = movement.MovementType is InventoryMovementType.TransferIn or InventoryMovementType.TransferOut;
            if (isTransfer && (string.IsNullOrWhiteSpace(leg) || !movement.TransferDispatchAllocationId.HasValue))
                throw Invalid("Historical transfer stock needs exact allocation lineage before receipt cost revaluation.");
            if (movement.MovementType is InventoryMovementType.CustomerReturn or InventoryMovementType.RequisitionReturn)
                throw Invalid("Returned stock requires original receipt lineage before receipt cost revaluation.");
            if (movement.MovementType == InventoryMovementType.SupplierReturn && movement.Direction == MovementDirection.In)
                throw Invalid("Reversed supplier returns require original receipt lineage before receipt cost revaluation.");
            if (movement.Direction == MovementDirection.Out)
            {
                var isTransitRelease = isTransfer && leg == "TransitOut";
                var isSelectedLotSale = method == ValuationMethod.FIFO && movement.ReferenceType == ReferenceType.SalesInvoice &&
                    !string.IsNullOrWhiteSpace(movement.LotNumber);
                var claimed = isTransitRelease ? stock.Where(x => x.TransitAllocation == movement.TransferDispatchAllocationId).ToList()
                    : isSelectedLotSale ? stock.Where(x => x.Layer.HasValue && layerMap.TryGetValue(x.Layer.Value, out var lotLayer) &&
                        string.Equals(lotLayer.LotNumber, movement.LotNumber.Trim(), StringComparison.OrdinalIgnoreCase)).ToList() : stock;
                var selectedLayers = isSelectedLotSale ? claimed.Select(x => x.Layer).ToHashSet() : null;
                var taken = Take(claimed, movement.Quantity, method);
                if (isTransitRelease)
                {
                    stock.RemoveAll(x => x.TransitAllocation == movement.TransferDispatchAllocationId);
                    stock.AddRange(claimed);
                }
                else if (isSelectedLotSale)
                {
                    stock.RemoveAll(x => selectedLayers!.Contains(x.Layer));
                    stock.AddRange(claimed);
                }
                if (isTransfer)
                {
                    var key = leg switch
                    {
                        "SourceOut" => ("Dispatch", movement.TransferDispatchAllocationId!.Value),
                        "TransitOut" when movement.TransferReceiptAllocationId.HasValue => ("Receipt", movement.TransferReceiptAllocationId.Value),
                        _ => throw Invalid("The outbound transfer leg has no exact controlled allocation.")
                    };
                    if (!inFlight.TryAdd(key, taken)) throw Invalid("A transfer allocation has duplicate outbound legs.");
                }
            }
            else if (movement.Direction == MovementDirection.In)
            {
                List<Lot> received;
                if (isTransfer)
                {
                    var key = leg switch
                    {
                        "TransitIn" => ("Dispatch", movement.TransferDispatchAllocationId!.Value),
                        "DestinationIn" or "SourceReturn" when movement.TransferReceiptAllocationId.HasValue => ("Receipt", movement.TransferReceiptAllocationId.Value),
                        _ => throw Invalid("The inbound transfer leg has no exact controlled allocation.")
                    };
                    if (!inFlight.Remove(key, out received!) || received.Sum(x => x.Quantity) != movement.Quantity)
                        throw Invalid("Transfer legs do not conserve their source receipt quantities.");
                }
                else received = [new(movement.Id, null, movement.MovementDate, movement.CreatedAt, movement.Quantity)];
                Guid? layerId = null;
                var layerDate = movement.MovementDate;
                var layerCreated = movement.CreatedAt;
                if (method == ValuationMethod.FIFO)
                {
                    // New receipts and typed transfer legs retain the exact created layer.
                    // A legacy receipt is acceptable only when its original layer is unique.
                    var layer = movement.CostLayerId.HasValue && layerMap.TryGetValue(movement.CostLayerId.Value, out var linked)
                        ? linked : layers.SingleOrDefault(x => x.SourceId == movement.ReferenceId &&
                            x.WarehouseId == movement.WarehouseId && x.LocationId == movement.LocationId &&
                            x.OriginalQuantity == movement.Quantity);
                    if (layer is null) throw Invalid("The inbound FIFO movement has no unique retained original layer.");
                    layerId = layer.Id; layerDate = layer.LayerDate; layerCreated = layer.CreatedAt;
                }
                stock.AddRange(received.Select(x => x with { Layer = layerId, LayerDate = layerDate, Created = layerCreated,
                    TransitAllocation = leg == "TransitIn" ? movement.TransferDispatchAllocationId : null }));
            }
            else throw Invalid("The movement direction is unsupported.");
            if (stock.Sum(x => x.Quantity) != movement.RunningBalance)
                throw Invalid("The ordered movement history does not reconcile to its retained closing quantity.");
        }
        if (inFlight.Count != 0) throw Invalid("A posted transfer allocation is missing its paired physical leg.");
        foreach (var bin in bins.Keys.Union(balances.Select(x => new Bin(x.WarehouseId, x.LocationId))))
        {
            var actual = balances.Where(x => x.WarehouseId == bin.Warehouse && x.LocationId == bin.Location).ToArray();
            if (actual.Length > 1 || (actual.FirstOrDefault()?.QuantityOnHand ?? 0) != bins.GetValueOrDefault(bin, []).Sum(x => x.Quantity))
                throw Invalid("Current inventory quantities do not reconcile to the retained movement history.");
        }
        if (method == ValuationMethod.FIFO)
        {
            var remaining = bins.Values.SelectMany(x => x).GroupBy(x => x.Layer).ToDictionary(x => x.Key!.Value, x => x.Sum(y => y.Quantity));
            if (layers.Any(x => remaining.GetValueOrDefault(x.Id) != x.RemainingQuantity))
                throw Invalid("Current FIFO quantities do not reconcile to retained receipt attribution.");
        }
        return bins.SelectMany(pair => pair.Value.Where(x => x.Origin == originMovementId)
            .GroupBy(x => x.Layer).Select(group => new Share(pair.Key.Warehouse, pair.Key.Location, group.Key, group.Sum(x => x.Quantity))))
            .Where(x => x.Quantity > 0).OrderBy(x => x.WarehouseId).ThenBy(x => x.LocationId).ThenBy(x => x.LayerId).ToArray();
    }

    private static List<Lot> Take(List<Lot> stock, decimal quantity, ValuationMethod method)
    {
        var available = stock.Sum(x => x.Quantity);
        if (available < quantity) throw Invalid("Movement history consumes stock before its receipt; receipt provenance cannot be guessed.");
        var result = new List<Lot>();
        var ordered = method == ValuationMethod.FIFO
            ? stock.OrderBy(x => x.LayerDate).ThenBy(x => x.Created).ThenBy(x => new SqlGuid(x.Layer ?? Guid.Empty)).ToArray()
            : stock.ToArray();
        var remaining = quantity;
        for (var i = 0; i < ordered.Length && remaining > 0; i++)
        {
            var lot = ordered[i];
            var take = quantity == available ? lot.Quantity : method == ValuationMethod.FIFO ? Math.Min(lot.Quantity, remaining)
                : i == ordered.Length - 1 ? remaining : Math.Min(remaining, decimal.Round(quantity * lot.Quantity / available, 12, MidpointRounding.ToZero));
            if (take > lot.Quantity) throw Invalid("Proportional receipt attribution exceeds its source stock.");
            if (take == 0) continue;
            stock.Remove(lot);
            if (lot.Quantity > take) stock.Add(lot with { Quantity = lot.Quantity - take });
            result.Add(lot with { Quantity = take }); remaining -= take;
        }
        if (remaining != 0) throw Invalid("Receipt attribution did not conserve its issued quantity.");
        return result;
    }

    private static InvalidOperationException Invalid(string message) => new($"RECEIPT_STOCK_RECONCILIATION_REQUIRED: {message}");
}
