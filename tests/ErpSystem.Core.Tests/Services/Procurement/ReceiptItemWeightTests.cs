using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Services.Inventory;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ReceiptItemWeightTests
{
    [Theory]
    [InlineData("kg", 2.5, 2.5)]
    [InlineData("g", 2500, 2.5)]
    [InlineData("lb", 10, 4.535924)]
    public void CapturesKilogramsPerStockUnit(string unit, decimal weight, decimal expected)
    {
        var master = new InventoryItem { Weight = weight, WeightUnit = unit, UnitOfMeasure = "EA" };
        ReceiptItemWeight.Capture(master, null).Should().Be(expected);
        master.Weight.Should().Be(weight);
    }

    [Fact]
    public void TransactionOverrideAndLaterMasterEditsDoNotChangeCapturedAllocation()
    {
        var master = new InventoryItem { Weight = 5m, WeightUnit = "kg", UnitOfMeasure = "EA" };
        var receipt = new GoodsReceiptNoteItem
        {
            InventoryItem = master, UnitOfMeasure = "EA", WeightStockUom = "EA",
            UnitWeightKg = ReceiptItemWeight.Capture(master, 1.25m)
        };
        master.Weight.Should().Be(5m);
        master.Weight = 100m;
        // 2 boxes of 12 base units: use base quantity once, without applying the PO conversion to unit weight.
        ReceiptItemWeight.AllocationBasis(receipt, 24m).Should().Be(30m);
    }

    [Fact]
    public void LegacyUnlabelledMasterDoesNotAcquireAnInventedUnit()
    {
        var master = new InventoryItem { Weight = 10m };
        ReceiptItemWeight.Capture(master, null).Should().BeNull();
        var receipt = new GoodsReceiptNoteItem { InventoryItem = master, UnitOfMeasure = "EA", WeightStockUom = "EA" };
        Action allocate = () => ReceiptItemWeight.AllocationBasis(receipt, 1m);
        allocate.Should().Throw<InvalidOperationException>().WithMessage("*captured weight*");
    }

    [Fact]
    public void MismatchedStockUnitCannotSilentlyReinterpretCapturedWeight()
    {
        var receipt = new GoodsReceiptNoteItem { UnitWeightKg = 1m, WeightStockUom = "EA", UnitOfMeasure = "BOX" };
        Action allocate = () => ReceiptItemWeight.AllocationBasis(receipt, 1m);
        allocate.Should().Throw<InvalidOperationException>();
        ReceiptItemWeight.AllocationBasis(receipt, 0m).Should().Be(0);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(0.0000001)]
    public void RejectsInvalidOverridePrecisionAndSign(decimal value)
    {
        Action capture = () => ReceiptItemWeight.Capture(null, value);
        capture.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ExplicitWeightIsPartOfReceiptRetryIdentity()
    {
        var request = new CreateGoodsReceiptNoteDto { Items = [new() { PurchaseOrderItemId = Guid.NewGuid(), ReceivedQuantity = 5m }] };
        var supplier = Guid.NewGuid();
        var original = GoodsReceiptNoteService.BuildIdempotencyRequestHash(request, supplier);
        request.Items[0].UnitWeightKg = 2m;
        var withWeight = GoodsReceiptNoteService.BuildIdempotencyRequestHash(request, supplier);
        withWeight.Should().NotBe(original);
        request.Items[0].UnitWeightKg = 3m;
        GoodsReceiptNoteService.BuildIdempotencyRequestHash(request, supplier).Should().NotBe(withWeight);
        request.Items[0].UnitWeightKg = null;
        GoodsReceiptNoteService.BuildIdempotencyRequestHash(request, supplier).Should().Be(original);
    }
}
