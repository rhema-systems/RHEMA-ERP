using ErpSystem.Core.Entities.Inventory;

namespace ErpSystem.Core.Services.Inventory;

/// <summary>Receipt weights are kilograms per stock base unit, independent of the PO purchase UOM.</summary>
public static class ReceiptItemWeight
{
    public const decimal MaximumWeightKg = 9999999999999999.999999m;

    public static decimal? Capture(InventoryItem? item, decimal? overrideKg)
    {
        if (overrideKg.HasValue) return Validate(overrideKg.Value);
        if (item?.Weight == null || string.IsNullOrWhiteSpace(item.WeightUnit)) return null;
        var kilograms = item.WeightUnit switch
        {
            "kg" => item.Weight.Value,
            "g" => item.Weight.Value / 1000m,
            "lb" => item.Weight.Value * 0.45359237m,
            _ => throw new ArgumentException("Item weight unit must be kg, g or lb.")
        };
        return Validate(decimal.Round(kilograms, 6, MidpointRounding.AwayFromZero));
    }

    public static decimal Validate(decimal kilograms)
    {
        if (kilograms < 0 || kilograms > MaximumWeightKg || decimal.Round(kilograms, 6) != kilograms)
            throw new ArgumentException("Unit weight must be non-negative kilograms with no more than six decimal places.");
        return kilograms;
    }

    public static decimal AllocationBasis(GoodsReceiptNoteItem item, decimal baseQuantity)
    {
        if (baseQuantity <= 0) return 0;
        if (item.UnitWeightKg is not > 0 || string.IsNullOrWhiteSpace(item.WeightStockUom) ||
            !string.Equals(item.WeightStockUom, item.UnitOfMeasure, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Receipt item '{item.ItemCode}' requires a positive captured weight in kg per stock unit before weight allocation.");
        return Validate(item.UnitWeightKg.Value) * baseQuantity;
    }
}
