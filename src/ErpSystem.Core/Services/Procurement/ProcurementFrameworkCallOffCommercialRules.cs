namespace ErpSystem.Core.Services.Procurement;

public static class ProcurementFrameworkCallOffCommercialRules
{
    public sealed record FamilyCapacity(
        decimal CeilingAmount,
        decimal CommittedAmount,
        decimal AvailableAmount,
        decimal ProposedAmount,
        bool CanReserve);

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

    public static FamilyCapacity EvaluateFamilyCapacity(
        decimal ceilingAmount,
        decimal committedAmount,
        decimal proposedAmount)
    {
        var ceiling = RoundMoney(ceilingAmount);
        var committed = RoundMoney(Math.Max(0m, committedAmount));
        var proposed = RoundMoney(Math.Max(0m, proposedAmount));
        var available = RoundMoney(Math.Max(0m, ceiling - committed));
        return new FamilyCapacity(
            ceiling,
            committed,
            available,
            proposed,
            proposed <= available);
    }

    private static decimal RoundMoney(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}
