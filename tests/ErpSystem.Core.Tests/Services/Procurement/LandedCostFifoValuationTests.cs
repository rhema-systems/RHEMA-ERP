using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Services.Inventory;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public class LandedCostFifoValuationTests
{
    [Theory]
    [InlineData(20, 276.54, 0)]
    [InlineData(10, 138.27, 138.27)]
    [InlineData(0, 0, 276.54)]
    public void FreightAndHandlingCountReceiptQuantityOnce(decimal remaining, decimal inventory, decimal variance)
    {
        var line = Guid.NewGuid();
        var charges = new[] { Charge(line, 20, 226.54m), Charge(line, 20, 50) };
        var layer = new InventoryLayer { OriginalQuantity = 20, RemainingQuantity = remaining,
            UnitCost = 1900, RemainingValue = remaining * 1900 };
        var result = LandedCostFifoValuation.Apply(charges, new[] { layer });
        Assert.Equal(inventory, result.Inventory); Assert.Equal(variance, result.Variance);
        Assert.All(charges, a => Assert.Equal(layer.Id, a.InventoryLayerId));
        Assert.Equal(remaining, layer.RemainingQuantity);
        Assert.Equal(remaining * 1900 + inventory, layer.RemainingValue);
    }

    [Fact]
    public void DifferentReceiptLinesKeepTheirOwnCostsAndRemainingQuantities()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        var charges = new[] { Charge(a, 20, 200), Charge(a, 20, 50), Charge(b, 10, 100) };
        var first = new InventoryLayer { OriginalQuantity = 20, RemainingQuantity = 20 };
        var second = new InventoryLayer { OriginalQuantity = 10, RemainingQuantity = 0 };
        var result = LandedCostFifoValuation.Apply(charges, new[] { first, second });
        Assert.Equal(250, result.Inventory); Assert.Equal(100, result.Variance);
        Assert.Equal(250, first.RemainingValue); Assert.Equal(0, second.RemainingValue);
    }

    [Fact]
    public void AmbiguousLayersCannotSilentlyReceiveTargetedCharges()
    {
        Assert.Throws<InvalidOperationException>(() => LandedCostFifoValuation.Apply(
            new[] { Charge(Guid.NewGuid(), 20, 50) }, new[] {
                new InventoryLayer { OriginalQuantity = 20, RemainingQuantity = 20 },
                new InventoryLayer { OriginalQuantity = 20, RemainingQuantity = 5 } }));
    }

    [Fact]
    public void ExistingLayerLinkResolvesEqualSizedLayers()
    {
        var first = new InventoryLayer { OriginalQuantity = 20, RemainingQuantity = 20 };
        var second = new InventoryLayer { OriginalQuantity = 20, RemainingQuantity = 5 };
        var charge = Charge(Guid.NewGuid(), 20, 100); charge.InventoryLayerId = second.Id;
        var result = LandedCostFifoValuation.Apply(new[] { charge }, new[] { first, second });
        Assert.Equal(25, result.Inventory); Assert.Equal(75, result.Variance); Assert.Equal(0, first.RemainingValue);
    }

    [Fact]
    public void ConflictingChargeQuantitiesAreRejected()
    {
        var line = Guid.NewGuid();
        Assert.Throws<InvalidOperationException>(() => LandedCostFifoValuation.Apply(
            new[] { Charge(line, 20, 50), Charge(line, 40, 100) },
            new[] { new InventoryLayer { OriginalQuantity = 20, RemainingQuantity = 20 } }));
    }

    private static LandedCostAllocation Charge(Guid line, decimal quantity, decimal amount) => new() {
        GoodsReceiptNoteItemId = line, Quantity = quantity, AllocatedAmount = amount };
}
