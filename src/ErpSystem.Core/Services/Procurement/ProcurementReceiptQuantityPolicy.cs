namespace ErpSystem.Core.Services.Procurement;

/// <summary>Preserves accepted purchase quantity when projecting into the four-decimal stock ledger.</summary>
public static class ProcurementReceiptQuantityPolicy
{
    public static decimal ToBaseQuantity(decimal purchaseQuantity, decimal conversionToBase)
    {
        if (purchaseQuantity <= 0 || conversionToBase <= 0)
            throw new InvalidOperationException("Accepted receipt quantity and unit conversion must be positive.");
        var result = purchaseQuantity * conversionToBase;
        if (result != decimal.Round(result, 4, MidpointRounding.AwayFromZero))
            throw new InvalidOperationException("RCV_BASE_QUANTITY_PRECISION: This quantity and unit conversion produce more than four decimal places in the stock unit. Correct the receipt quantity or unit conversion before accepting stock; no quantity has been rounded.");
        return result;
    }
}
