using ErpSystem.Core.Services.Procurement;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementInvoiceThreeWayMatchRulesTests
{
    [Fact]
    public void OnlyPoLinkedNonOpeningInvoiceRequiresControl()
    {
        var purchaseOrderId = Guid.NewGuid();

        ProcurementInvoiceThreeWayMatchRules.IsRequired(purchaseOrderId, false).Should().BeTrue();
        ProcurementInvoiceThreeWayMatchRules.IsRequired(null, false).Should().BeFalse();
        ProcurementInvoiceThreeWayMatchRules.IsRequired(purchaseOrderId, true).Should().BeFalse();
    }

    [Theory]
    [InlineData(101, 100, 1, true)]
    [InlineData(101.01, 100, 1, false)]
    [InlineData(99, 100, 1, true)]
    [InlineData(-1, 100, 1, false)]
    public void PriceToleranceHasAnExactInclusiveBoundary(
        decimal invoicePrice,
        decimal orderPrice,
        decimal tolerance,
        bool expected)
    {
        ProcurementInvoiceThreeWayMatchRules.IsPriceWithinTolerance(
                invoicePrice,
                orderPrice,
                tolerance)
            .Should().Be(expected);
    }

    [Theory]
    [InlineData(101, 100, 1, true)]
    [InlineData(101.01, 100, 1, false)]
    [InlineData(100, 100, 0, true)]
    [InlineData(100.01, 100, 0, false)]
    public void CumulativeQuantityPreventsReuseOfAcceptedReceipts(
        decimal currentPlusPriorInvoiceQuantity,
        decimal independentlyAcceptedQuantity,
        decimal tolerance,
        bool expected)
    {
        ProcurementInvoiceThreeWayMatchRules.IsCumulativeQuantityWithinTolerance(
                currentPlusPriorInvoiceQuantity,
                independentlyAcceptedQuantity,
                tolerance)
            .Should().Be(expected);
    }

    [Fact]
    public void DecisionLineageIsTheCompletePhaseZeroRegister()
    {
        ProcurementInvoiceThreeWayMatchRules.DecisionKeys.Should().Equal(
            Enumerable.Range(1, 14).Select(number => $"DEC-{number:000}"));
    }

    [Fact]
    public void SnapshotHashIsDeterministicAndContentSensitive()
    {
        var first = ProcurementInvoiceThreeWayMatchRules.HashSnapshot(new { line = 1, quantity = 4m });
        var same = ProcurementInvoiceThreeWayMatchRules.HashSnapshot(new { line = 1, quantity = 4m });
        var changed = ProcurementInvoiceThreeWayMatchRules.HashSnapshot(new { line = 1, quantity = 5m });

        first.Should().HaveLength(64).And.Be(same).And.NotBe(changed);
    }
}
