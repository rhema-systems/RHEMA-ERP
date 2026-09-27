using ErpSystem.Core.Services.Inventory;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Inventory;

public sealed class PhysicalCountObservationPolicyTests
{
    [Fact]
    public void DefectiveStockIsPartOfPhysicalQuantityAndDoesNotChangeVariance()
    {
        PhysicalCountObservationPolicy.Validate(100m, 5m, "Five damaged units");
        PhysicalCountObservationPolicy.Validate(0m, 0m, null);
        PhysicalCountObservationPolicy.Validate(1.2345m, 1.2345m, null);
    }

    [Theory]
    [InlineData("100", "-1")]
    [InlineData("100", "101")]
    [InlineData("100", "0.00001")]
    [InlineData("-1", "0")]
    [InlineData("1.00001", "0")]
    public void RejectsInvalidPhysicalOrDefectiveObservation(string physical, string defective) =>
        Assert.Throws<InvalidOperationException>(() => PhysicalCountObservationPolicy.Validate(
            decimal.Parse(physical, System.Globalization.CultureInfo.InvariantCulture),
            decimal.Parse(defective, System.Globalization.CultureInfo.InvariantCulture), null));

    [Fact]
    public void RejectsOverlongDefectNotes() => Assert.Throws<InvalidOperationException>(() =>
        PhysicalCountObservationPolicy.Validate(10m, 2m, new string('x', 2001)));
}
