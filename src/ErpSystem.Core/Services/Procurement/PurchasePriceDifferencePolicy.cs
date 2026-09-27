namespace ErpSystem.Core.Services.Procurement;

public static class PurchasePriceDifferencePolicy
{
    public const string RevalueInventory = "RevalueInventory";
    public const string PurchasePriceVariance = "PurchasePriceVariance";

    public static bool IsValid(string? value) => value is RevalueInventory or PurchasePriceVariance;

    public static string Require(string? value) => IsValid(value) ? value! :
        throw new InvalidOperationException("Configure purchase price difference handling in Procurement Settings before posting an invoice cost difference.");
}
