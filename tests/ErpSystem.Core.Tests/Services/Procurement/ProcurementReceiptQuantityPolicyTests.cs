using ErpSystem.Core.Services.Procurement;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementReceiptQuantityPolicyTests
{
    [Theory]
    [InlineData(2.5, 0.3, 0.75)]
    [InlineData(0.0001, 1, 0.0001)]
    [InlineData(10000, 0.00000001, 0.0001)]
    public void Supported_fractional_conversion_preserves_exact_base_quantity(decimal quantity, decimal factor, decimal expected)
        => Assert.Equal(expected, ProcurementReceiptQuantityPolicy.ToBaseQuantity(quantity, factor));

    [Theory]
    [InlineData(1, 1.234567)]
    [InlineData(1, 0.000001)]
    public void Unrepresentable_stock_quantity_is_rejected_instead_of_silently_rounded(decimal quantity, decimal factor)
        => Assert.Contains("RCV_BASE_QUANTITY_PRECISION", Assert.Throws<InvalidOperationException>(
            () => ProcurementReceiptQuantityPolicy.ToBaseQuantity(quantity, factor)).Message);
}
