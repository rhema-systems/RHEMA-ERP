using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.Procurement;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementContractActivationRulesTests
{
    [Theory]
    [InlineData(ProcurementContractActivationStatus.Rejected, true)]
    [InlineData(ProcurementContractActivationStatus.Activated, true)]
    [InlineData(ProcurementContractActivationStatus.Cancelled, true)]
    [InlineData(ProcurementContractActivationStatus.PendingApproval, false)]
    [InlineData(ProcurementContractActivationStatus.Approved, false)]
    [InlineData(ProcurementContractActivationStatus.RevalidationFailed, false)]
    public void TerminalLifecycleIsExplicit(
        ProcurementContractActivationStatus status,
        bool expected)
    {
        ProcurementContractActivationRules.IsTerminal(status)
            .Should().Be(expected);
    }

    [Theory]
    [InlineData("Draft", true)]
    [InlineData("draft", true)]
    [InlineData("PendingSignature", true)]
    [InlineData("Active", false)]
    [InlineData("Completed", false)]
    [InlineData("", false)]
    public void SubmissionRequiresAnEligibleContractStatus(
        string status,
        bool expected)
    {
        ProcurementContractActivationRules.CanSubmitContract(status)
            .Should().Be(expected);
    }

    [Fact]
    public void OnlyPendingRequestCanBeDecided()
    {
        ProcurementContractActivationRules.CanDecide(
                ProcurementContractActivationStatus.PendingApproval)
            .Should().BeTrue();
        ProcurementContractActivationRules.CanDecide(
                ProcurementContractActivationStatus.Approved)
            .Should().BeFalse();
    }

    [Theory]
    [InlineData(ProcurementContractActivationStatus.Approved, true)]
    [InlineData(ProcurementContractActivationStatus.RevalidationFailed, true)]
    [InlineData(ProcurementContractActivationStatus.PendingApproval, false)]
    [InlineData(ProcurementContractActivationStatus.Rejected, false)]
    [InlineData(ProcurementContractActivationStatus.Activated, false)]
    public void ActivationAllowsApprovedAndRecoverableRequestsOnly(
        ProcurementContractActivationStatus status,
        bool expected)
    {
        ProcurementContractActivationRules.CanActivate(status)
            .Should().Be(expected);
    }

    [Fact]
    public void PositiveDecisionMustBeIndependent()
    {
        var creator = Guid.NewGuid();
        var submitter = Guid.NewGuid();
        var checker = Guid.NewGuid();

        ProcurementContractActivationRules.IsIndependent(
                checker, submitter, creator)
            .Should().BeTrue();
        ProcurementContractActivationRules.IsIndependent(
                submitter, submitter, creator)
            .Should().BeFalse();
        ProcurementContractActivationRules.IsIndependent(
                creator, submitter, creator)
            .Should().BeFalse();
        ProcurementContractActivationRules.IsIndependent(
                Guid.Empty, submitter, creator)
            .Should().BeFalse();
    }

    [Theory]
    [InlineData(100.004, 100.00, true)]
    [InlineData(100.005, 100.01, true)]
    [InlineData(100.006, 100.00, false)]
    public void AwardAmountComparisonUsesFinancialPrecision(
        decimal contractValue,
        decimal awardValue,
        bool expected)
    {
        ProcurementContractActivationRules.AmountMatchesAward(
                contractValue, awardValue)
            .Should().Be(expected);
    }
}
