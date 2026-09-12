using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Services.Procurement;

/// <summary>Catalogue selection is optional during purchasing; stock mapping is a receiving decision.</summary>
public static class PurchaseOrderLineRules
{
    public static bool RequiresStock(ItemType type) => type is ItemType.StockItem or ItemType.FixedAsset;

    public static string? Validate(ItemType type, string? description, decimal quantity,
        string? unit, decimal price, Guid? inventoryId, ItemType? catalogueType)
    {
        if (!Enum.IsDefined(type)) return "Select stock goods, non-stock goods, a service or a fixed asset.";
        if (string.IsNullOrWhiteSpace(description)) return "Enter a description for the purchase-order line.";
        if (quantity <= 0) return "The ordered quantity must be greater than zero.";
        if (string.IsNullOrWhiteSpace(unit)) return "Enter the unit of measure.";
        if (price < 0) return "The unit price cannot be negative.";
        if (inventoryId.HasValue && inventoryId != Guid.Empty && !catalogueType.HasValue)
            return "The selected catalogue item is not available in this tenant.";
        if (catalogueType.HasValue && catalogueType != type)
            return "The line type must match the selected catalogue item. Clear the catalogue selection to enter an ad hoc line.";
        return null;
    }
}
