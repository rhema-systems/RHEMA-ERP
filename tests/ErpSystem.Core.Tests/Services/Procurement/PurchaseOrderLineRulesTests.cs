using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.Procurement;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public class PurchaseOrderLineRulesTests
{
    [Fact]
    public void MixedAcceptanceCountsOnlyStockAsStockEligible()
    {
        var inspection = new ProcurementReceiptInspectionCase();
        foreach (var type in new[] { ItemType.StockItem, ItemType.Service, ItemType.NonStock })
        {
            inspection.Lines.Add(new ProcurementReceiptInspectionLine
            {
                ReceivedQuantity = 3, AcceptedQuantity = 3,
                PurchaseOrderReceiptItem = new PurchaseOrderReceiptItem
                {
                    PurchaseOrderItem = new PurchaseOrderItem { LineType = type }
                }
            });
        }
        typeof(ProcurementReceiptInspectionService).GetMethod("Recalculate",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(null, new object[] { inspection });
        Assert.Equal(9, inspection.AcceptedQuantity);
        Assert.Equal(3, inspection.StockEligibleQuantity);
        Assert.Equal(9, inspection.ApEligibleQuantity);
    }

    [Theory]
    [InlineData(ItemType.Service)]
    [InlineData(ItemType.NonStock)]
    public async Task NonStockCannotReachInventoryDependenciesEvenWithCatalogueId(ItemType type)
    {
        // No dependencies are installed: touching stock storage/valuation fails this test.
        var service = (ProcurementReceiptInspectionService)System.Runtime.CompilerServices.RuntimeHelpers
            .GetUninitializedObject(typeof(ProcurementReceiptInspectionService));
        var task = (Task)typeof(ProcurementReceiptInspectionService).GetMethod("PostStockAsync",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(service, new object?[] {
                new PurchaseOrderReceipt(), new PurchaseOrderReceiptItem(),
                new PurchaseOrderItem { LineType = type, InventoryItemId = Guid.NewGuid() },
                1m, null, null, CancellationToken.None
            })!;
        await task;
    }

    [Theory]
    [InlineData(ItemType.StockItem)]
    [InlineData(ItemType.NonStock)]
    [InlineData(ItemType.Service)]
    [InlineData(ItemType.FixedAsset)]
    public void DescriptiveLinesDoNotRequireCatalogueRecords(ItemType type) =>
        Assert.Null(PurchaseOrderLineRules.Validate(type, "Approved source description", 2, "EACH", 25, null, null));

    [Theory]
    [InlineData(ItemType.StockItem, true)]
    [InlineData(ItemType.FixedAsset, true)]
    [InlineData(ItemType.Service, false)]
    [InlineData(ItemType.NonStock, false)]
    public void OnlyPhysicalStockRequiresInventoryPosting(ItemType type, bool expected) =>
        Assert.Equal(expected, PurchaseOrderLineRules.RequiresStock(type));

    [Theory]
    [InlineData(ItemType.Service)]
    [InlineData(ItemType.NonStock)]
    public void LinkedStockCannotBeDisguisedAsANonStockLine(ItemType type) =>
        Assert.NotNull(PurchaseOrderLineRules.Validate(type, "Pipe", 1, "EACH", 2, Guid.NewGuid(), ItemType.StockItem));

    [Fact]
    public void InvalidOrCrossTenantCatalogueReferenceIsRejected() =>
        Assert.NotNull(PurchaseOrderLineRules.Validate(ItemType.StockItem, "Pipe", 1, "EACH", 2, Guid.NewGuid(), null));

    [Theory]
    [InlineData("", 1, "EACH", 2)]
    [InlineData("Pipe", 0, "EACH", 2)]
    [InlineData("Pipe", 1, "", 2)]
    [InlineData("Pipe", 1, "EACH", -1)]
    public void AdHocLinesStillRequireValidCommercialTerms(string description, int quantity, string unit, int price) =>
        Assert.NotNull(PurchaseOrderLineRules.Validate(ItemType.NonStock, description, quantity, unit, price, null, null));

    [Fact]
    public void LegacyRowsAndRequestsDefaultToStockWithoutReinterpretingHistory()
    {
        Assert.Equal(ItemType.StockItem, new PurchaseOrderItem().LineType);
        Assert.Equal(ItemType.StockItem, new CreatePurchaseOrderItemDto().LineType);
        Assert.Null(new CreatePurchaseOrderItemDto().InventoryItemId);
        Assert.NotNull(PurchaseOrderLineRules.Validate((ItemType)99, "Pipe", 1, "EACH", 2, null, null));
    }
}
