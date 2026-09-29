namespace ErpSystem.Core.Services.Inventory;

public static class PhysicalCountObservationPolicy
{
    public static void Validate(decimal countedQuantity, decimal defectiveQuantity, string? defectiveNotes)
    {
        const decimal maximumQuantity = 99999999999999.9999m;
        if (countedQuantity < 0 || countedQuantity > maximumQuantity || decimal.Round(countedQuantity, 4) != countedQuantity)
            throw new InvalidOperationException("Counted Qty must be non-negative with at most four decimal places.");
        if (defectiveQuantity < 0 || defectiveQuantity > countedQuantity || decimal.Round(defectiveQuantity, 4) != defectiveQuantity)
            throw new InvalidOperationException("Defective Qty must be non-negative, no greater than Counted Qty, and have at most four decimal places.");
        if (defectiveNotes?.Length > 2000)
            throw new InvalidOperationException("Defective notes must be at most 2000 characters.");
    }
}
