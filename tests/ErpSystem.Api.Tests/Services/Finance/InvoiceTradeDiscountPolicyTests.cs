using ErpSystem.Api.Services.Finance;
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
