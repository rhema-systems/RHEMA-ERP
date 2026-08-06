using ErpSystem.Core.Services.Finance;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class CrossCurrencySettlementCalculatorTests
{
    [Fact]
    public void FunctionalCurrencyPayment_ShouldPreserveForeignInvoiceReductionAndFunctionalCash()
    {
        // TDC pays a USD 100 supplier invoice using GHS 1,250. The invoice settlement-date rate
        // remains separate from the actual commercial cash conversion used for realized FX.
        var result = CrossCurrencySettlementCalculator.Calculate(
            paymentCurrency: "GHS",
            invoiceCurrency: "USD",
            paymentCurrencyAmount: 1_250m,
            invoiceCurrencyAmount: 100m,
            invoiceDeductionAmount: 0m,
            paymentExchangeRate: 1m,
            invoiceSettlementExchangeRate: 12.40m);

        result.IsCrossCurrency.Should().BeTrue();
        result.PaymentFunctionalAmount.Should().Be(1_250m);
        result.SettlementFunctionalAmount.Should().Be(1_250m);
        result.InvoiceSettlementFunctionalAmount.Should().Be(1_240m);
    }

    [Fact]
    public void ThirdCurrencyPayment_ShouldUsePaymentRateForCashAndInvoiceRateForDeduction()
    {
        // EUR cash settles a USD invoice while USD-denominated WHT is credited separately. The
        // functional settlement value is therefore EUR cash at its rate plus USD WHT at its rate.
        var result = CrossCurrencySettlementCalculator.Calculate(
            paymentCurrency: "EUR",
            invoiceCurrency: "USD",
            paymentCurrencyAmount: 90m,
            invoiceCurrencyAmount: 95m,
            invoiceDeductionAmount: 5m,
            paymentExchangeRate: 14m,
            invoiceSettlementExchangeRate: 12.5m);

        result.IsCrossCurrency.Should().BeTrue();
        result.PaymentFunctionalAmount.Should().Be(1_260m);
        result.DeductionFunctionalAmount.Should().Be(62.5m);
        result.SettlementFunctionalAmount.Should().Be(1_322.5m);
        result.InvoiceSettlementFunctionalAmount.Should().Be(1_250m);
    }

    [Fact]
    public void SameCurrencySettlement_ShouldRejectTwoDifferentCashAmounts()
    {
        var action = () => CrossCurrencySettlementCalculator.Calculate(
            paymentCurrency: "USD",
            invoiceCurrency: "USD",
            paymentCurrencyAmount: 99m,
            invoiceCurrencyAmount: 100m,
            invoiceDeductionAmount: 0m,
            paymentExchangeRate: 12m,
            invoiceSettlementExchangeRate: 12m);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Same-currency settlement payment amount*");
    }

    [Fact]
    public void SameCurrencyDeductionOnly_ShouldRemainSupported()
    {
        // Existing AP/AR workflows may apply an eligible discount or withholding line while the
        // cash portion is allocated elsewhere. FIN-LIM-0022 must not regress that established path.
        var result = CrossCurrencySettlementCalculator.Calculate(
            "GHS", "GHS", 0m, 0m, 25m, 1m, 1m);

        result.PaymentFunctionalAmount.Should().Be(0m);
        result.SettlementFunctionalAmount.Should().Be(25m);
        result.InvoiceSettlementFunctionalAmount.Should().Be(25m);
    }

    [Fact]
    public void LineScopedDeductions_ShouldRetainIndependentlyRoundedFunctionalEvidence()
    {
        // Each deduction posts to a different account. Independent rounding ensures the AP/AR
        // control line equals the exact sum of cash, discount, WHT and VAT-WHT posting lines.
        var result = CrossCurrencySettlementCalculator.CalculateWithDeductions(
            paymentCurrency: "GHS",
            invoiceCurrency: "USD",
            paymentCurrencyAmount: 1_000m,
            invoiceCurrencyAmount: 75m,
            invoiceDiscountAmount: 1.11m,
            invoiceWithholdingAmount: 2.22m,
            invoiceVatWithholdingAmount: 3.33m,
            paymentExchangeRate: 1m,
            invoiceSettlementExchangeRate: 12.345678m);

        result.DiscountFunctionalAmount.Should().Be(13.70m);
        result.WithholdingFunctionalAmount.Should().Be(27.41m);
        result.VatWithholdingFunctionalAmount.Should().Be(41.11m);
        result.DeductionFunctionalAmount.Should().Be(82.22m);
        result.SettlementFunctionalAmount.Should().Be(1_082.22m);
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(100, 0)]
    public void Settlement_ShouldRejectMissingPositiveLeg(decimal paymentAmount, decimal invoiceAmount)
    {
        var action = () => CrossCurrencySettlementCalculator.Calculate(
            "GHS", "USD", paymentAmount, invoiceAmount, 0m, 1m, 12m);

        action.Should().Throw<InvalidOperationException>();
    }
}
