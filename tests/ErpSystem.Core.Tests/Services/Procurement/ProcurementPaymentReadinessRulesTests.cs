using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Services.Procurement;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementPaymentReadinessRulesTests
{
    [Theory]
    [InlineData(VendorInvoiceStatus.Approved, true)]
    [InlineData(VendorInvoiceStatus.PartiallyPaid, true)]
    [InlineData(VendorInvoiceStatus.Overdue, true)]
    [InlineData(VendorInvoiceStatus.Draft, false)]
    [InlineData(VendorInvoiceStatus.PendingApproval, false)]
    [InlineData(VendorInvoiceStatus.Paid, false)]
    [InlineData(VendorInvoiceStatus.Voided, false)]
    [InlineData(VendorInvoiceStatus.Rejected, false)]
    [InlineData(VendorInvoiceStatus.OnHold, false)]
    public void PaymentEligibilityIsFailClosed(VendorInvoiceStatus status, bool expected)
    {
        ProcurementPaymentReadinessRules.IsInvoiceStatePaymentEligible(status).Should().Be(expected);
    }

    [Fact]
    public void ActionsCoverEveryPositivePaymentMutationWithoutClaimingTdc0506()
    {
        var actions = new[]
        {
            ProcurementPaymentReadinessRules.AllocateAction,
            ProcurementPaymentReadinessRules.SupplierAdvanceAction,
            ProcurementPaymentReadinessRules.BatchCreateAction,
            ProcurementPaymentReadinessRules.BatchApproveAction,
            ProcurementPaymentReadinessRules.BatchProcessAction,
            ProcurementPaymentReadinessRules.PostAction
        };

        actions.Should().OnlyHaveUniqueItems()
            .And.Contain("PaymentAllocationAuthorized")
            .And.Contain("PaymentBatchInvoiceProcessed")
            .And.Contain("PaymentPostingAuthorized");
        actions.Should().NotContain(action => action.Contains("ApproverSod", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DecisionLineageIsTheCompletePhaseZeroRegister()
    {
        ProcurementPaymentReadinessRules.DecisionKeys.Should().Equal(
            Enumerable.Range(1, 14).Select(number => $"DEC-{number:000}"));
    }

    [Fact]
    public void SnapshotHashIsDeterministicAndContentSensitive()
    {
        var first = ProcurementPaymentReadinessRules.HashSnapshot(new { invoice = "INV-1", amount = 4m });
        var same = ProcurementPaymentReadinessRules.HashSnapshot(new { invoice = "INV-1", amount = 4m });
        var changed = ProcurementPaymentReadinessRules.HashSnapshot(new { invoice = "INV-1", amount = 5m });

        first.Should().HaveLength(64).And.Be(same).And.NotBe(changed);
    }
}
