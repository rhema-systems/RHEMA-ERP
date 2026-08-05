using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Services.Finance;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Finance;

public sealed class VendorInvoiceMatchExceptionRulesTests
{
    [Fact]
    public void RuleUsesAp006AndCompleteDecisionRegister()
    {
        VendorInvoiceMatchExceptionRules.RuleCode.Should().Be("AP-006");
        VendorInvoiceMatchExceptionRules.RuleVersion.Should().Be("TDC-0507");
        VendorInvoiceMatchExceptionRules.DecisionKeys.Should()
            .Equal(Enumerable.Range(1, 14).Select(number => $"DEC-{number:000}"));
        VendorInvoiceMatchExceptionRules.RequiredEvidenceKeys.Should()
            .Equal("ROOT_CAUSE_EVIDENCE", "CORRECTIVE_ACTION_PLAN");
    }

    [Fact]
    public void ExceptionEligibilityRequiresOnlyExceptionableVariances()
    {
        var eligible = new InvoiceMatchingResultDto
        {
            IsRequired = true,
            IsMatched = false,
            Discrepancies = { new MatchingDiscrepancyDto { DiscrepancyType = "Price", ExceptionEligible = true } },
            Checks = { new InvoiceMatchingCheckDto { Passed = false, ExceptionEligible = true } }
        };
        var hardStop = new InvoiceMatchingResultDto
        {
            IsRequired = true,
            IsMatched = false,
            Discrepancies = { new MatchingDiscrepancyDto { DiscrepancyType = "Missing receipt" } },
            Checks = { new InvoiceMatchingCheckDto { Passed = false, ExceptionEligible = false } }
        };

        VendorInvoiceMatchExceptionRules.IsExceptionable(eligible).Should().BeTrue();
        VendorInvoiceMatchExceptionRules.IsExceptionable(hardStop).Should().BeFalse();
    }

    [Fact]
    public void RequestAndApprovalExpireWithoutEnablingASecondMutationPath()
    {
        var now = DateTime.UtcNow;
        VendorInvoiceMatchExceptionRules.EffectiveStatus(
            VendorInvoiceMatchExceptionStatus.PendingApproval, now.AddSeconds(-1), now)
            .Should().Be(VendorInvoiceMatchExceptionStatus.Expired);
        VendorInvoiceMatchExceptionRules.EffectiveStatus(
            VendorInvoiceMatchExceptionStatus.Approved, now.AddSeconds(-1), now)
            .Should().Be(VendorInvoiceMatchExceptionStatus.Expired);
        VendorInvoiceMatchExceptionRules.CanCompleteCorrectiveAction(
            VendorInvoiceMatchExceptionStatus.Approved,
            VendorInvoiceMatchCorrectiveActionStatus.Planned,
            now.AddSeconds(-1), now).Should().BeTrue();
        VendorInvoiceMatchExceptionRules.CanCompleteCorrectiveAction(
            VendorInvoiceMatchExceptionStatus.PendingApproval,
            VendorInvoiceMatchCorrectiveActionStatus.Planned,
            now.AddSeconds(-1), now).Should().BeFalse();
    }

    [Fact]
    public void RequesterInvoiceProcessorAndPriorApproverAreNeverIndependent()
    {
        var requester = Guid.NewGuid();
        var processor = Guid.NewGuid();
        var prior = Guid.NewGuid();
        var independent = Guid.NewGuid();

        VendorInvoiceMatchExceptionRules.IsIndependent(requester, requester, processor, [prior]).Should().BeFalse();
        VendorInvoiceMatchExceptionRules.IsIndependent(processor, requester, processor, [prior]).Should().BeFalse();
        VendorInvoiceMatchExceptionRules.IsIndependent(prior, requester, processor, [prior]).Should().BeFalse();
        VendorInvoiceMatchExceptionRules.IsIndependent(independent, requester, processor, [prior]).Should().BeTrue();
    }
}
