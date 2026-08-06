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
    {
        var normalizedPaymentCurrency = NormalizeCurrency(paymentCurrency);
        var normalizedInvoiceCurrency = NormalizeCurrency(invoiceCurrency);
        var isCrossCurrency = !string.Equals(
            normalizedPaymentCurrency,
            normalizedInvoiceCurrency,
            StringComparison.OrdinalIgnoreCase);

        if (paymentCurrencyAmount < 0m || invoiceCurrencyAmount < 0m)
            throw new InvalidOperationException("Settlement cash amounts cannot be negative.");
        if (invoiceDeductionAmount < 0m)
            throw new InvalidOperationException("Settlement deductions cannot be negative.");
        if (paymentExchangeRate <= 0m || invoiceSettlementExchangeRate <= 0m)
            throw new InvalidOperationException("Settlement exchange rates must be greater than zero.");
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
        var deductionFunctionalAmount = RoundMoney(invoiceDeductionAmount * invoiceSettlementExchangeRate);

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
    decimal SettlementFunctionalAmount,
    decimal InvoiceSettlementFunctionalAmount);
