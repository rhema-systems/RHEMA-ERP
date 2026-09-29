using System;
using System.Collections.Generic;
using System.Linq;

namespace ErpSystem.Api.Services.Finance.AR;

internal static class ArDiscountGovernancePolicy
{
    internal const int MinimumReasonLength = 10;
    internal const int MaximumReasonLength = 500;

    internal static bool HasInvoiceDiscount(
        decimal documentDiscount,
        IEnumerable<decimal> lineDiscountPercentages)
    {
        return documentDiscount > 0m || lineDiscountPercentages.Any(value => value > 0m);
    }

    internal static string? NormalizeInvoiceDiscountReason(
        decimal documentDiscount,
        IEnumerable<decimal> lineDiscountPercentages,
        string? reason,
        bool requireUserReason,
        string sourceDescription)
    {
        var normalized = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (normalized?.Length > MaximumReasonLength)
            throw new InvalidOperationException($"Discount reason cannot exceed {MaximumReasonLength} characters.");

        if (!HasInvoiceDiscount(documentDiscount, lineDiscountPercentages))
            return null;

        if (requireUserReason && (normalized?.Length ?? 0) < MinimumReasonLength)
        {
            throw new InvalidOperationException(
                $"Discounted manual AR invoices require a reason of at least {MinimumReasonLength} characters.");
        }

        return normalized ?? $"Governed source pricing adjustment: {sourceDescription}.";
    }

    internal static void RequireTaxAdjustmentForEarlyPaymentDiscount(
        decimal invoiceTaxAmount,
        decimal requestedDiscountAmount,
        string invoiceNumber)
    {
        if (requestedDiscountAmount <= 0m || invoiceTaxAmount <= 0m)
            return;

        throw new InvalidOperationException(
            $"Invoice '{invoiceNumber}' includes VAT or levies. Record an approved sales credit/adjustment note " +
            "to reduce the taxable consideration before applying an early-payment discount. Direct receipt discounts " +
            "are limited to zero-rated or exempt invoices.");
    }
}
