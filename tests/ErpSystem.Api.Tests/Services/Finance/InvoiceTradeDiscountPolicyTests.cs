using ErpSystem.Api.Services.Finance;
using ErpSystem.Api.Services.Finance.AR;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class InvoiceTradeDiscountPolicyTests
{
    [Theory]
    [InlineData(-0.01)]
    [InlineData(100.01)]
    public void CalculateLineDiscount_ShouldRejectPercentageOutsideClosedRange(decimal percentage)
    {
        var action = () => InvoiceTradeDiscountPolicy.CalculateLineDiscount(100m, percentage, "Invoice");

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*between 0 and 100*");
    }

    [Fact]
    public void AllocateDocumentDiscount_ShouldAllocateProportionallyAndRetainResidual()
    {
        var first = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var second = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var third = Guid.Parse("00000000-0000-0000-0000-000000000003");

        var allocations = InvoiceTradeDiscountPolicy.AllocateDocumentDiscount(
            1m,
            new[] { (first, 1m), (second, 1m), (third, 1m) },
            "Invoice");

        allocations[first].Should().Be(0.33m);
        allocations[second].Should().Be(0.33m);
        allocations[third].Should().Be(0.34m);
        allocations.Values.Sum().Should().Be(1m);
    }

    [Fact]
    public void AllocateDocumentDiscount_ShouldRejectAmountAboveNetEligibleLines()
    {
        var action = () => InvoiceTradeDiscountPolicy.AllocateDocumentDiscount(
            100.01m,
            new[] { (Guid.NewGuid(), 100m) },
            "Invoice");

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*exceeds the eligible source-line amount*");
    }
}

public sealed class ArDiscountGovernancePolicyTests
{
    [Fact]
    public void NormalizeInvoiceDiscountReason_ShouldRequireEvidenceForManualDiscount()
    {
        var action = () => ArDiscountGovernancePolicy.NormalizeInvoiceDiscountReason(
            25m,
            new[] { 0m },
            "short",
            requireUserReason: true,
            "manual AR invoice");

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*reason of at least 10 characters*");
    }

    [Fact]
    public void NormalizeInvoiceDiscountReason_ShouldRetainGovernedSourceEvidence()
    {
        var reason = ArDiscountGovernancePolicy.NormalizeInvoiceDiscountReason(
            0m,
            new[] { 5m },
            null,
            requireUserReason: false,
            "SalesOrderCustomerInvoice");

        reason.Should().Be("Governed source pricing adjustment: SalesOrderCustomerInvoice.");
    }

    [Fact]
    public void RequireTaxAdjustmentForEarlyPaymentDiscount_ShouldBlockTaxableInvoice()
    {
        var action = () => ArDiscountGovernancePolicy.RequireTaxAdjustmentForEarlyPaymentDiscount(
            30m,
            10m,
            "AR-0001");

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*sales credit/adjustment note*");
    }

    [Fact]
    public void RequireTaxAdjustmentForEarlyPaymentDiscount_ShouldAllowZeroRatedInvoice()
    {
        var action = () => ArDiscountGovernancePolicy.RequireTaxAdjustmentForEarlyPaymentDiscount(
            0m,
            10m,
            "AR-0002");

        action.Should().NotThrow();
    }
}
