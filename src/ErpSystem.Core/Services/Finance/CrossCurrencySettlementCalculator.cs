using System;

namespace ErpSystem.Core.Services.Finance;

/// <summary>
/// Performs the currency arithmetic shared by AP and AR settlement.  This class deliberately has
/// no database dependency: services first resolve tenant-owned approved exchange-rate snapshots,
/// then pass the frozen values here.  Keeping the calculation pure makes the accounting rule easy
/// to test and prevents AP and AR from evolving subtly different rounding conventions.
/// </summary>
public static class CrossCurrencySettlementCalculator
{
    private const decimal MoneyTolerance = 0.01m;

    /// <summary>
    /// Builds the immutable values stored on one settlement allocation.
    /// </summary>
    /// <param name="paymentCurrencyAmount">Cash consumed in payment/receipt currency.</param>
    /// <param name="invoiceCurrencyAmount">Cash-equivalent reduction in invoice currency.</param>
    /// <param name="invoiceDeductionAmount">
    /// Non-cash invoice reduction (discount and/or withholding) in invoice currency.
    /// </param>
    /// <param name="paymentExchangeRate">Functional-currency units per payment-currency unit.</param>
    /// <param name="invoiceSettlementExchangeRate">
    /// Functional-currency units per invoice-currency unit at settlement date.
    /// </param>
    public static CrossCurrencySettlementAmounts Calculate(
        string paymentCurrency,
        string invoiceCurrency,
        decimal paymentCurrencyAmount,
        decimal invoiceCurrencyAmount,
        decimal invoiceDeductionAmount,
        decimal paymentExchangeRate,
        decimal invoiceSettlementExchangeRate)
        => CalculateWithDeductions(
            paymentCurrency,
            invoiceCurrency,
            paymentCurrencyAmount,
            invoiceCurrencyAmount,
            invoiceDeductionAmount,
            invoiceWithholdingAmount: 0m,
            invoiceVatWithholdingAmount: 0m,
            paymentExchangeRate,
            invoiceSettlementExchangeRate);

    /// <summary>
    /// Builds settlement evidence when the invoice is reduced by separately accountable
    /// deductions. Each component is rounded independently because discount, WHT and VAT-WHT
    /// post to different accounts and must reconcile individually to their statutory evidence.
    /// All deduction inputs are expressed in invoice currency; their approved conversion is the
    /// same invoice-settlement rate frozen on the allocation.
    /// </summary>
    public static CrossCurrencySettlementAmounts CalculateWithDeductions(
        string paymentCurrency,
        string invoiceCurrency,
        decimal paymentCurrencyAmount,
        decimal invoiceCurrencyAmount,
        decimal invoiceDiscountAmount,
        decimal invoiceWithholdingAmount,
        decimal invoiceVatWithholdingAmount,
        decimal paymentExchangeRate,
        decimal invoiceSettlementExchangeRate)
    {
        var normalizedPaymentCurrency = NormalizeCurrency(paymentCurrency);
        var normalizedInvoiceCurrency = NormalizeCurrency(invoiceCurrency);
        var isCrossCurrency = !string.Equals(
            normalizedPaymentCurrency,
            normalizedInvoiceCurrency,
            StringComparison.OrdinalIgnoreCase);

        if (paymentCurrencyAmount < 0m || invoiceCurrencyAmount < 0m)
            throw new InvalidOperationException("Settlement cash amounts cannot be negative.");
        if (invoiceDiscountAmount < 0m || invoiceWithholdingAmount < 0m || invoiceVatWithholdingAmount < 0m)
            throw new InvalidOperationException("Settlement deductions cannot be negative.");
        if (paymentExchangeRate <= 0m || invoiceSettlementExchangeRate <= 0m)
            throw new InvalidOperationException("Settlement exchange rates must be greater than zero.");
        var invoiceDeductionAmount = invoiceDiscountAmount + invoiceWithholdingAmount + invoiceVatWithholdingAmount;
        if (invoiceCurrencyAmount + invoiceDeductionAmount <= 0m)
            throw new InvalidOperationException("Settlement must reduce the invoice by a positive amount.");
        if (isCrossCurrency && (paymentCurrencyAmount <= 0m || invoiceCurrencyAmount <= 0m))
        {
            // A cross-currency cash conversion cannot be inferred from a zero leg. Requiring both
            // amounts prevents accidental zero-rate settlement while same-currency deduction-only
            // allocations remain compatible with the established AP/AR behavior.
            throw new InvalidOperationException(
                "Cross-currency settlement requires positive payment- and invoice-currency cash amounts.");
        }

        // Same-currency callers historically supplied only AllocatedAmount. Requiring parity here
        // catches accidental use of the new payment amount as a second, inconsistent value while
        // retaining the established same-currency API behavior.
        if (!isCrossCurrency && Math.Abs(RoundMoney(paymentCurrencyAmount - invoiceCurrencyAmount)) > MoneyTolerance)
        {
            throw new InvalidOperationException(
                "Same-currency settlement payment amount must equal the invoice-currency cash allocation.");
        }

        var paymentFunctionalAmount = RoundMoney(paymentCurrencyAmount * paymentExchangeRate);
        // These values are intentionally rounded independently. Combining the deductions before
        // rounding can leave a one-cent difference between the control line and the three GL
        // deduction lines, which would make a valid settlement fail the posting-engine balance.
        var discountFunctionalAmount = RoundMoney(invoiceDiscountAmount * invoiceSettlementExchangeRate);
        var withholdingFunctionalAmount = RoundMoney(invoiceWithholdingAmount * invoiceSettlementExchangeRate);
        var vatWithholdingFunctionalAmount = RoundMoney(invoiceVatWithholdingAmount * invoiceSettlementExchangeRate);
        var deductionFunctionalAmount = RoundMoney(
            discountFunctionalAmount + withholdingFunctionalAmount + vatWithholdingFunctionalAmount);

        // The settlement value is the actual functional value surrendered or received: cash at
        // the payment rate plus non-cash deductions at the invoice settlement rate. This is the
        // amount compared with the invoice's historical carrying value to derive realized FX.
        var settlementFunctionalAmount = RoundMoney(paymentFunctionalAmount + deductionFunctionalAmount);
        var invoiceSettlementFunctionalAmount = RoundMoney(
            (invoiceCurrencyAmount + invoiceDeductionAmount) * invoiceSettlementExchangeRate);

        return new CrossCurrencySettlementAmounts(
            normalizedPaymentCurrency,
            normalizedInvoiceCurrency,
            isCrossCurrency,
            RoundMoney(paymentCurrencyAmount),
            RoundMoney(invoiceCurrencyAmount),
            RoundMoney(invoiceDeductionAmount),
            RoundRate(paymentExchangeRate),
            RoundRate(invoiceSettlementExchangeRate),
            paymentFunctionalAmount,
            deductionFunctionalAmount,
            discountFunctionalAmount,
            withholdingFunctionalAmount,
            vatWithholdingFunctionalAmount,
            settlementFunctionalAmount,
            invoiceSettlementFunctionalAmount);
    }

    private static string NormalizeCurrency(string currency)
    {
        var normalized = currency?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length != 3)
            throw new InvalidOperationException("Settlement currency must be a three-character ISO code.");
        return normalized;
    }

    private static decimal RoundMoney(decimal value)
        => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private static decimal RoundRate(decimal value)
        => decimal.Round(value, 6, MidpointRounding.AwayFromZero);
}

/// <summary>
/// Pure calculation result copied onto an AP or AR allocation as immutable accounting evidence.
/// </summary>
public sealed record CrossCurrencySettlementAmounts(
    string PaymentCurrency,
    string InvoiceCurrency,
    bool IsCrossCurrency,
    decimal PaymentCurrencyAmount,
    decimal InvoiceCurrencyAmount,
    decimal InvoiceDeductionAmount,
    decimal PaymentExchangeRate,
    decimal InvoiceSettlementExchangeRate,
    decimal PaymentFunctionalAmount,
    decimal DeductionFunctionalAmount,
    decimal DiscountFunctionalAmount,
    decimal WithholdingFunctionalAmount,
    decimal VatWithholdingFunctionalAmount,
    decimal SettlementFunctionalAmount,
    decimal InvoiceSettlementFunctionalAmount);
