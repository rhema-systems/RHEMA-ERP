using ErpSystem.Core.Entities.Inventory;

namespace ErpSystem.Core.Services.Inventory;

/// <summary>
/// Read-only simulation of the FIFO owner's sequential quantity/value consumption.
/// Tracking identities deliberately do not partition the cost layers: the authoritative
/// FIFO owner consumes the whole exact item/warehouse/bin scope in chronological order.
/// </summary>
internal sealed class StockAdjustmentFifoDraftPlan
{
    private readonly List<Layer> _layers;
    private readonly decimal _fallback;
    private decimal _openingQuantity;

    public StockAdjustmentFifoDraftPlan(IEnumerable<InventoryLayer> layers, decimal openingQuantity, decimal fallback)
    {
        // The caller obtains rows in database FIFO order, including SQL's Guid tie-break.
        _layers = layers.Select(value => new Layer(value.RemainingQuantity, value.UnitCost,
            value.RemainingValue, value.LayerDate, value.CreatedAt)).ToList();
        _openingQuantity = openingQuantity;
        _fallback = fallback;
    }

    public decimal Plan(decimal delta, decimal positiveUnitCost, DateTime executionOrderTime)
    {
        if (delta == 0) throw new ArgumentOutOfRangeException(nameof(delta));
        var quantity = Math.Abs(delta);
        if (delta > 0)
        {
            var value = quantity * positiveUnitCost;
            AddLayer(new Layer(quantity, decimal.Round(positiveUnitCost, 2, MidpointRounding.AwayFromZero),
                decimal.Round(value, 4, MidpointRounding.AwayFromZero), executionOrderTime, executionOrderTime));
            _openingQuantity += quantity;
            return value;
        }

        // Mirror the posting owner's existing exact-bin adoption; no persisted row is
        // created by this planner. The same fallback is independently derived by SQL.
        var missingOpeningQuantity = Math.Max(0, _openingQuantity - _layers.Sum(value => value.Quantity));
        if (missingOpeningQuantity > 0)
            AddLayer(new Layer(missingOpeningQuantity, decimal.Round(_fallback, 2, MidpointRounding.AwayFromZero),
                decimal.Round(missingOpeningQuantity * _fallback, 4, MidpointRounding.AwayFromZero),
                executionOrderTime, executionOrderTime));

        decimal remaining = quantity;
        decimal total = 0;
        foreach (var layer in _layers)
        {
            if (remaining == 0) break;
            if (layer.Quantity <= 0) continue;
            var taken = Math.Min(remaining, layer.Quantity);
            var value = taken == layer.Quantity ? layer.Value
                : Math.Min(layer.Value, decimal.Round(taken * layer.UnitCost, 2, MidpointRounding.AwayFromZero));
            layer.Quantity -= taken;
            layer.Value -= value;
            total += value;
            remaining -= taken;
        }
        // The negative-stock owner remains authoritative. This only prices any
        // emergency-overridden shortage; it does not authorize a stock decrease.
        total += remaining * _fallback;
        _openingQuantity -= quantity;
        return total;
    }

    private void AddLayer(Layer layer)
    {
        var index = _layers.FindIndex(value => value.LayerDate > layer.LayerDate ||
            (value.LayerDate == layer.LayerDate && value.CreatedAt > layer.CreatedAt));
        if (index < 0) _layers.Add(layer); else _layers.Insert(index, layer);
    }

    private sealed class Layer(decimal quantity, decimal unitCost, decimal value, DateTime layerDate, DateTime createdAt)
    {
        public decimal Quantity { get; set; } = quantity;
        public decimal UnitCost { get; } = unitCost;
        public decimal Value { get; set; } = value;
        public DateTime LayerDate { get; } = layerDate;
        public DateTime CreatedAt { get; } = createdAt;
    }
}
