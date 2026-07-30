using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.Procurement;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementWorksCloseoutRulesTests
{
    [Theory]
    [InlineData("Works", true)]
    [InlineData("works", true)]
    [InlineData("Supply", false)]
    [InlineData("Service", false)]
    [InlineData(null, false)]
    public void WorksClassificationFailsClosed(string? value, bool expected)
    {
        ProcurementWorksCloseoutRules.IsWorks(value).Should().Be(expected);
    }

    [Theory]
    [InlineData("Active", ProcurementWorksCloseoutActionType.InitialTakeover, true)]
    [InlineData("Suspended", ProcurementWorksCloseoutActionType.InitialTakeover, false)]
    [InlineData("Suspended", ProcurementWorksCloseoutActionType.DisputeResolve, true)]
    [InlineData("Suspended", ProcurementWorksCloseoutActionType.Termination, true)]
    [InlineData("Completed", ProcurementWorksCloseoutActionType.Closeout, false)]
    public void ContractStateControlsActionEntry(
        string status,
        ProcurementWorksCloseoutActionType action,
        bool expected)
    {
        ProcurementWorksCloseoutRules.CanSubmit(status, action)
            .Should().Be(expected);
    }

    [Fact]
    public void EveryActionHasDedicatedEvidenceRequirements()
    {
        ProcurementWorksCloseoutRules.RequiredEvidence.Keys
            .Should().BeEquivalentTo(
                Enum.GetValues<ProcurementWorksCloseoutActionType>());
        ProcurementWorksCloseoutRules.RequiredEvidence.Values
            .Should().OnlyContain(items => items.Count >= 2);
        ProcurementWorksCloseoutRules.RequiredEvidence
            [ProcurementWorksCloseoutActionType.Termination]
            .Should().Contain(["termination-notice", "legal-review"]);
    }

    [Theory]
    [InlineData(ProcurementWorksCloseoutActionType.RetentionRelease, true)]
    [InlineData(ProcurementWorksCloseoutActionType.FinalAccount, true)]
    [InlineData(ProcurementWorksCloseoutActionType.Termination, true)]
    [InlineData(ProcurementWorksCloseoutActionType.Closeout, false)]
    [InlineData(ProcurementWorksCloseoutActionType.DefectRectification, false)]
    public void MonetaryActionsAreExplicit(
        ProcurementWorksCloseoutActionType action,
        bool expected)
    {
        ProcurementWorksCloseoutRules.IsMonetary(action).Should().Be(expected);
    }

    [Theory]
    [InlineData("Reported", true)]
    [InlineData("UnderReview", true)]
    [InlineData("InProgress", true)]
    [InlineData("Resolved", false)]
    [InlineData("Closed", false)]
    [InlineData("WarrantyExpired", false)]
    public void OnlyTerminalDefectStatesAreClosed(string status, bool open)
    {
        ProcurementWorksCloseoutRules.IsOpenDefectStatus(status)
            .Should().Be(open);
    }

    [Fact]
    public void PositiveApprovalRequiresIndependentActor()
    {
        var submitter = Guid.NewGuid();
        var creator = Guid.NewGuid();

        ProcurementWorksCloseoutRules.IsIndependent(
            Guid.NewGuid(), submitter, creator).Should().BeTrue();
        ProcurementWorksCloseoutRules.IsIndependent(
            submitter, submitter, creator).Should().BeFalse();
        ProcurementWorksCloseoutRules.IsIndependent(
            creator, submitter, creator).Should().BeFalse();
        ProcurementWorksCloseoutRules.IsIndependent(
            Guid.Empty, submitter, creator).Should().BeFalse();
    }

    [Fact]
    public void AmountComparisonUsesControlledCurrencyPrecision()
    {
        ProcurementWorksCloseoutRules.AmountMatches(100.004m, 100m)
            .Should().BeTrue();
        ProcurementWorksCloseoutRules.AmountMatches(100.02m, 100m)
            .Should().BeFalse();
        ProcurementWorksCloseoutRules.AmountMatches(null, 100m)
            .Should().BeFalse();
    }

    [Fact]
    public void DefectsLiabilityEndIsDerivedFromApprovedTakeover()
    {
        var takeover = new DateTime(2026, 7, 30, 12, 0, 0, DateTimeKind.Utc);

        ProcurementWorksCloseoutRules.DefectsLiabilityEnd(takeover, 365)
            .Should().Be(takeover.AddDays(365));
        ProcurementWorksCloseoutRules.DefectsLiabilityEnd(takeover, 0)
            .Should().Be(takeover);
        ProcurementWorksCloseoutRules.DefectsLiabilityEnd(null, 365)
            .Should().BeNull();
    }

    [Theory]
    [InlineData(ProcurementWorksCloseoutActionStatus.PendingApproval, true)]
    [InlineData(ProcurementWorksCloseoutActionStatus.RevalidationFailed, true)]
    [InlineData(ProcurementWorksCloseoutActionStatus.Approved, false)]
    [InlineData(ProcurementWorksCloseoutActionStatus.Rejected, false)]
    public void OnlyOpenOutcomesCanBeDecided(
        ProcurementWorksCloseoutActionStatus status,
        bool expected)
    {
        ProcurementWorksCloseoutRules.CanDecide(status).Should().Be(expected);
    }
}
