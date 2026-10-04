using ErpSystem.Core.Inventory;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Inventory;

public sealed class CommercialQuantityPolicyTests
{
    [Theory]
    [InlineData("1.25", 2, "0.25")]
    [InlineData("-1.25", 2, "0.25")]
    [InlineData("0", 0, null)]
    public void Validate_AcceptsWholeIncrements(string quantity, int places, string? increment) =>
        CommercialQuantityPolicy.Validate(decimal.Parse(quantity), places, increment is null ? null : decimal.Parse(increment));

    [Theory]
    [InlineData("1.2", 0, null)]
    [InlineData("1.2", 2, "0.25")]
    [InlineData("-1.2", 2, "0.25")]
    public void Validate_RejectsInvalidIncrements(string quantity, int places, string? increment)
    {
        var act = () => CommercialQuantityPolicy.Validate(decimal.Parse(quantity), places, increment is null ? null : decimal.Parse(increment));
        act.Should().Throw<InvalidOperationException>().WithMessage("*whole multiple*");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(7)]
    public void Configuration_RejectsUnsupportedPrecision(int places) =>
        FluentActions.Invoking(() => CommercialQuantityPolicy.ValidateConfiguration(places, null)).Should().Throw<InvalidOperationException>();

    [Fact]
    public void Configuration_RejectsZeroIncrement() =>
        FluentActions.Invoking(() => CommercialQuantityPolicy.ValidateConfiguration(2, 0m)).Should().Throw<InvalidOperationException>().WithMessage("*greater than zero*");

    [Fact]
    public void ConvertedBaseQuantity_MustSatisfyBaseUomPolicy()
    {
        CommercialQuantityPolicy.Validate(2.5m, 1, 0.5m);
        CommercialQuantityPolicy.Validate(2.5m * 12m, 0, 1m);
    }
}
