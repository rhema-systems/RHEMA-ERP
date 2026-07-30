namespace ErpSystem.Core.Services.Procurement;

public static class ProcurementFrameworkCallOffCommercialRules
{
    public static (decimal Quantity, decimal LineTotal) NormalizeLine(
        decimal quantity,
        decimal unitPrice)
    {
        var normalizedQuantity = decimal.Round(
            quantity,
            4,
            MidpointRounding.AwayFromZero);
        var lineTotal = decimal.Round(
            normalizedQuantity * unitPrice,
            2,
            MidpointRounding.AwayFromZero);
        return (normalizedQuantity, lineTotal);
    }
}
