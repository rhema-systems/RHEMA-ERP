using ErpSystem.Core.Entities.Inventory;

namespace ErpSystem.Core.Services.Inventory;

/// <summary>Receipt quantities belong to receipt lines, not to individual freight/handling charges.</summary>
public static class LandedCostFifoValuation
{
    public static (decimal Inventory, decimal Variance) Apply(
        IEnumerable<LandedCostAllocation> allocations, IReadOnlyCollection<InventoryLayer> layers)
    {
        var receiptLines = allocations.GroupBy(a => a.GoodsReceiptNoteItemId).ToList();
        var available = layers.ToList();
        decimal inventory = 0, variance = 0;
        foreach (var line in receiptLines.OrderByDescending(g => g.First().Quantity))
        {
            var quantities = line.Select(a => a.Quantity).Distinct().ToList();
            if (quantities.Count != 1 || quantities[0] <= 0)
                throw new InvalidOperationException("The landed-cost allocations have inconsistent receipt quantities. Review the receipt before posting.");
            var quantity = quantities[0];
            var amount = Money(line.Sum(a => a.AllocatedAmount));
            var savedIds = line.Where(a => a.InventoryLayerId.HasValue).Select(a => a.InventoryLayerId!.Value).Distinct().ToList();
            if (savedIds.Count > 1)
                throw new InvalidOperationException("The landed-cost receipt line refers to conflicting FIFO layers. Review its inventory history before posting.");
            var candidates = savedIds.Count == 1
                ? available.Where(l => l.Id == savedIds[0]).ToList()
                : available.Where(l => l.OriginalQuantity == quantity).ToList();
            // Do not guess between equal-sized receipt layers: a targeted cost must stay on its own line.
            if (candidates.Count != 1)
                throw new InvalidOperationException("A unique FIFO layer could not be identified for this receipt line. Review its inventory history before posting landed costs.");
            var layer = candidates[0];
            available.Remove(layer);
            foreach (var charge in line) charge.InventoryLayerId = layer.Id;
            var retained = Money(amount * Math.Clamp(layer.RemainingQuantity / quantity, 0m, 1m));
            inventory += retained;
            variance += Money(amount - retained);
            if (layer.RemainingQuantity > 0)
            {
                layer.UnitCost += retained / layer.RemainingQuantity;
                layer.RemainingValue += retained;
            }
        }
        return (inventory, variance);
    }

    private static decimal Money(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);
}
