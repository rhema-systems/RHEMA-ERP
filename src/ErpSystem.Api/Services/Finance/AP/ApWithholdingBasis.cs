using ErpSystem.Core.Entities.Finance;

namespace ErpSystem.Api.Services.Finance.AP;

/// <summary>
/// Converts the gross liability being settled into its net supply component. Invoice SubTotal
/// already includes trade discounts; settlement discounts do not rewrite the approved tax invoice.
/// Never infer tax from today's rate master or deduct the invoice trade discount a second time.
/// </summary>
internal static class ApWithholdingBasis
{
    internal static decimal NetSupplyFraction(VendorInvoice invoice)
    {
        if (invoice.TotalAmount <= 0m || invoice.SubTotal < 0m || invoice.TaxAmount < 0m ||
            Math.Abs(Round(invoice.SubTotal + invoice.TaxAmount - invoice.TotalAmount)) > 0.01m)
            throw new InvalidOperationException(
                $"Invoice '{invoice.InvoiceNumber}' has no reconciling net supply/tax evidence for WHT. Correct the source invoice through Finance before payment.");
        return invoice.SubTotal / invoice.TotalAmount;
    }

    internal static decimal FunctionalBase(VendorInvoice invoice, decimal grossSettlementFunctional)
    {
        if (grossSettlementFunctional < 0m)
            throw new InvalidOperationException("A WHT settlement base cannot be negative.");
        return Round(grossSettlementFunctional * NetSupplyFraction(invoice));
    }

    internal static decimal Round(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}
