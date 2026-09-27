using ErpSystem.Api.Services.Finance.AP;
using FluentAssertions;
using Xunit;
using static ErpSystem.Api.Services.Finance.AP.SupplierInvoiceCostDifferenceCalculator;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class SupplierInvoiceCostDifferenceCalculatorTests
{
    private static Input Example(decimal invoice = 1100, string? policy = "PurchasePriceVariance", decimal retained = 100) =>
        new(100, 0, 100, 1000, 1000, 0, 0, invoice, 1, 100, retained, policy, false);

    [Theory]
    [InlineData(1100, 100)]
    [InlineData(900, -100)]
    [InlineData(1000, 0)]
    public void PpvClearsOriginalAccrualAndSeparatesSignedPriceDifference(decimal invoice, decimal difference)
    {
        var plan = Calculate(Example(invoice));
        plan.ReceiptFunctionalAmount.Should().Be(1000);
        plan.PurchasePriceVarianceAmount.Should().Be(difference);
        plan.InventoryAdjustmentAmount.Should().Be(0);
        plan.ExchangeDifferenceFunctionalAmount.Should().Be(0);
        plan.InvoiceFunctionalAmount.Should().Be(invoice);
    }

    [Theory]
    [InlineData(100, 100, 0)]
    [InlineData(40, 40, 60)]
    [InlineData(1, 1, 99)]
    [InlineData(0, 0, 100)]
    public void RevalueOnlyTheProvedRetainedReceiptFraction(decimal retained, decimal inventory, decimal variance)
    {
        var plan = Calculate(Example(policy: "RevalueInventory", retained: retained));
        plan.InventoryAdjustmentAmount.Should().Be(inventory);
        plan.PurchasePriceVarianceAmount.Should().Be(variance);
    }

    [Fact]
    public void NegativeRevaluationUsesTheSameConservedRemainingFraction()
    {
        var plan = Calculate(Example(900, "RevalueInventory", 25));
        plan.InventoryAdjustmentAmount.Should().Be(-25);
        plan.PurchasePriceVarianceAmount.Should().Be(-75);
    }

    [Fact]
    public void StandardCostRoutesDifferenceToPpvWithoutChangingItsStandard()
    {
        var plan = Calculate(Example(policy: "RevalueInventory") with { StandardCost = true });
        plan.InventoryAdjustmentAmount.Should().Be(0);
        plan.PurchasePriceVarianceAmount.Should().Be(100);
    }

    [Fact]
    public void CurrencyDifferenceIsSeparateFromCommercialPriceDifference()
    {
        var plan = Calculate(Example() with { OriginalReceiptFunctionalAmount = 10000, InvoiceRateToFunctional = 12 });
        plan.ReceiptFunctionalAmount.Should().Be(10000);
        plan.ExchangeDifferenceFunctionalAmount.Should().Be(2000);
        plan.PriceDifferenceFunctionalAmount.Should().Be(1200);
        plan.InvoiceFunctionalAmount.Should().Be(13200);
    }

    [Fact]
    public void MultiplePartialInvoicesConsumeExactOriginalCentsAndDoNotRevalueTheWholeReceiptEachTime()
    {
        var first = Calculate(new(3, 0, 1, 10, 10, 0, 0, 4, 1, 3, 1, "RevalueInventory", false));
        var second = Calculate(new(3, 1, 1, 10, 10, first.ReceiptForeignAmount, first.ReceiptFunctionalAmount, 4, 1, 3, 1, "RevalueInventory", false));
        var last = Calculate(new(3, 2, 1, 10, 10, first.ReceiptForeignAmount + second.ReceiptForeignAmount,
            first.ReceiptFunctionalAmount + second.ReceiptFunctionalAmount, 4, 1, 3, 1, "RevalueInventory", false));
        (first.ReceiptFunctionalAmount + second.ReceiptFunctionalAmount + last.ReceiptFunctionalAmount).Should().Be(10);
        (first.PriceDifferenceFunctionalAmount + second.PriceDifferenceFunctionalAmount + last.PriceDifferenceFunctionalAmount).Should().Be(2);
        last.ReceiptFunctionalAmount.Should().Be(3.34m);
        new[] { first, second, last }.Should().OnlyContain(plan => plan.InventoryAdjustmentAmount < plan.PriceDifferenceFunctionalAmount);
    }

    [Fact]
    public void PriceDifferenceRequiresAnExplicitValidPolicyButEqualCostDoesNot()
    {
        var calculate = () => Calculate(Example(policy: null));
        calculate.Should().Throw<InvalidOperationException>().WithMessage("*Procurement Settings*");
        Calculate(Example(1000, null)).PriceDifferenceFunctionalAmount.Should().Be(0);
    }

    [Fact]
    public void RejectsOverInvoicingImpossibleRetainedStockAndUnprovedPreviousAmounts()
    {
        foreach (var invalid in new[] { Example() with { InvoiceQuantity = 101 }, Example() with { RetainedReceiptBaseQuantity = 101 },
            Example() with { PreviouslyClearedFunctionalAmount = 1 }, Example() with { InvoiceRateToFunctional = 0 },
            Example() with { InvoiceNetForeignAmount = 1100.001m } })
        {
            var calculate = () => Calculate(invalid);
            calculate.Should().Throw<InvalidOperationException>();
        }
    }

    [Theory]
    [InlineData(0.01)]
    [InlineData(-0.01)]
    public void SignedTargetSplitsPreserveTheCentAndNeverAllocateToAZeroQuantity(decimal amount)
    {
        var parts = AllocateSigned(new[] { 0m, 1m, 1m, 1m }, amount);
        parts[0].Should().Be(0);
        parts.Sum().Should().Be(amount);
    }
}
