using ErpSystem.Core.Services.Procurement;

namespace ErpSystem.Api.Services.Finance.AP;

/// <summary>Pure conservation calculation; its caller supplies locked, proved receipt and remaining-stock evidence.</summary>
public static class SupplierInvoiceCostDifferenceCalculator
{
    public sealed record Input(
        decimal OriginalReceiptQuantity, decimal PreviouslyClearedQuantity, decimal InvoiceQuantity,
        decimal OriginalReceiptForeignAmount, decimal OriginalReceiptFunctionalAmount,
        decimal PreviouslyClearedForeignAmount, decimal PreviouslyClearedFunctionalAmount,
        decimal InvoiceNetForeignAmount, decimal InvoiceRateToFunctional,
        decimal OriginalReceiptBaseQuantity, decimal RetainedReceiptBaseQuantity,
        string? Policy, bool StandardCost, decimal? AllocatedInvoiceFunctionalAmount = null);

    public sealed record Result(decimal ReceiptForeignAmount, decimal ReceiptFunctionalAmount,
        decimal InvoiceFunctionalAmount, decimal PriceDifferenceForeignAmount, decimal PriceDifferenceFunctionalAmount,
        decimal ExchangeDifferenceFunctionalAmount, decimal InventoryAdjustmentAmount, decimal PurchasePriceVarianceAmount);

    public static Result Calculate(Input input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.OriginalReceiptQuantity <= 0 || input.InvoiceQuantity <= 0 || input.PreviouslyClearedQuantity < 0 ||
            input.PreviouslyClearedQuantity + input.InvoiceQuantity > input.OriginalReceiptQuantity ||
            input.OriginalReceiptBaseQuantity <= 0 || input.RetainedReceiptBaseQuantity < 0 ||
            input.RetainedReceiptBaseQuantity > input.OriginalReceiptBaseQuantity || input.InvoiceRateToFunctional <= 0)
            throw new InvalidOperationException("Receipt quantity, remaining stock or invoice currency evidence is invalid.");
        var amounts = new[] { input.OriginalReceiptForeignAmount, input.OriginalReceiptFunctionalAmount,
            input.PreviouslyClearedForeignAmount, input.PreviouslyClearedFunctionalAmount, input.InvoiceNetForeignAmount };
        if (amounts.Any(value => value < 0 || value != Money(value)) ||
            input.PreviouslyClearedForeignAmount > input.OriginalReceiptForeignAmount ||
            input.PreviouslyClearedFunctionalAmount > input.OriginalReceiptFunctionalAmount ||
            (input.PreviouslyClearedQuantity == 0 && (input.PreviouslyClearedForeignAmount != 0 || input.PreviouslyClearedFunctionalAmount != 0)))
            throw new InvalidOperationException("Original receipt and previously cleared amounts must be nonnegative, rounded and consistent.");

        // The last share consumes the exact retained cents. Earlier shares never clear
        // more than the original accrual, including negative price-difference invoices.
        var last = input.InvoiceQuantity == input.OriginalReceiptQuantity - input.PreviouslyClearedQuantity;
        decimal Share(decimal original, decimal previous) => last ? original - previous :
            Math.Min(original - previous, Money(original * input.InvoiceQuantity / input.OriginalReceiptQuantity));
        var foreign = Share(input.OriginalReceiptForeignAmount, input.PreviouslyClearedForeignAmount);
        var functional = Share(input.OriginalReceiptFunctionalAmount, input.PreviouslyClearedFunctionalAmount);
        var translatedInvoice = Money(input.InvoiceNetForeignAmount * input.InvoiceRateToFunctional);
        var invoice = input.AllocatedInvoiceFunctionalAmount ?? translatedInvoice;
        if (invoice < 0 || invoice != Money(invoice) || Math.Abs(invoice - translatedInvoice) > 0.01m)
            throw new InvalidOperationException("Allocated invoice functional cents do not reconcile to the approved document conversion.");
        var translatedReceipt = Money(foreign * input.InvoiceRateToFunctional);
        var fx = translatedReceipt - functional;
        var priceForeign = input.InvoiceNetForeignAmount - foreign;
        // Computing the functional difference from the two complete amounts preserves
        // the final cent rather than translating each difference independently.
        var priceFunctional = invoice - translatedReceipt;
        var inventory = 0m;
        if (priceFunctional != 0)
        {
            var policy = PurchasePriceDifferencePolicy.Require(input.Policy);
            if (policy == PurchasePriceDifferencePolicy.RevalueInventory && !input.StandardCost)
                inventory = Money(priceFunctional * input.RetainedReceiptBaseQuantity / input.OriginalReceiptBaseQuantity);
        }
        var variance = priceFunctional - inventory;
        if (functional + fx + inventory + variance != invoice)
            throw new InvalidOperationException("The invoice cost plan does not conserve its original accrual and payable amounts.");
        return new(foreign, functional, invoice, priceForeign, priceFunctional, fx, inventory, variance);
    }

    public static decimal[] AllocateSigned(IReadOnlyList<decimal> quantities, decimal amount)
    {
        var parts = MonetaryAllocation.Allocate(quantities, Math.Abs(amount));
        return amount < 0 ? parts.Select(value => -value).ToArray() : parts;
    }

    private static decimal Money(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}
