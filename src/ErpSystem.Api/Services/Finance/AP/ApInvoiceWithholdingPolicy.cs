using ErpSystem.Core.Entities.Finance;

namespace ErpSystem.Api.Services.Finance.AP;

/// <summary>Payments inherit the invoice decision, never today's supplier defaults.</summary>
internal static class ApInvoiceWithholdingPolicy
{
    internal sealed record Decision(Guid? TaxId, decimal Rate);

    internal static Decision? Resolve(IEnumerable<VendorInvoice> source, Guid? requestedTaxId)
    {
        var invoices = source.DistinctBy(invoice => invoice.Id).ToList();
        var decided = invoices.Where(invoice => invoice.ApplySupplierWithholdingDefaults.HasValue ||
            invoice.WithholdingTaxRateOverride.HasValue || invoice.WithholdingTaxId.HasValue).ToList();
        if (decided.Count == 0) return null; // Preserve older, payment-configured transactions.

        var decisions = decided.Select(invoice =>
        {
            if (invoice.ApplySupplierWithholdingDefaults == false)
                return new Decision(null, 0m);
            if (!invoice.WithholdingTaxId.HasValue)
                throw new InvalidOperationException($"Invoice {invoice.InvoiceNumber} has no confirmed WHT configuration. Review its withholding choice before payment.");
            var rate = invoice.WithholdingTaxRateOverride ?? invoice.WithholdingTaxRate;
            if (rate is < 0m or > 100m)
                throw new InvalidOperationException($"Invoice {invoice.InvoiceNumber} has an invalid WHT rate.");
            return new Decision(invoice.WithholdingTaxId, rate);
        }).Distinct().ToList();

        if (decisions.Count != 1 || (decided.Count != invoices.Count && decisions[0].TaxId.HasValue))
            throw new InvalidOperationException("Selected invoices have different WHT decisions or rates. Record separate payments for each WHT choice and rate.");
        var decision = decisions[0];
        if (!decision.TaxId.HasValue && requestedTaxId.HasValue)
            throw new InvalidOperationException("WHT was disabled on this invoice. Remove payment WHT; the invoice's transaction choice must be retained.");
        if (decision.TaxId.HasValue && requestedTaxId.HasValue && decision.TaxId != requestedTaxId)
            throw new InvalidOperationException("Payment WHT does not match the invoice's confirmed WHT configuration.");
        return decision;
    }
}
