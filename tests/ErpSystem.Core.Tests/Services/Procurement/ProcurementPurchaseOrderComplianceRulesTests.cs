using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.Procurement;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementPurchaseOrderComplianceRulesTests
{
    [Theory]
    [InlineData(100, 100, true)]
    [InlineData(100, 75, true)]
    [InlineData(100, 0, true)]
    [InlineData(100, 100.01, false)]
    [InlineData(0, 0, false)]
    [InlineData(100, -1, false)]
    public void ActiveReservationMustCoverCumulativePurchaseOrderExposure(
        decimal reserved,
        decimal exposure,
        bool expected)
    {
        ProcurementPurchaseOrderComplianceRules.IsBudgetExposureCovered(
                reserved,
                exposure)
            .Should().Be(expected);
    }

    [Fact]
    public void ContractSignatureRequiresBothPartiesAndEvidence()
    {
        ProcurementPurchaseOrderComplianceRules.IsContractSignatureComplete(
                DateTime.UtcNow,
                Guid.NewGuid(),
                "TDC signatory",
                DateTime.UtcNow,
                "Supplier signatory",
                true)
            .Should().BeTrue();
    }

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public void ContractSignatureFailsWhenAnyRequiredFamilyIsMissing(
        bool organization,
        bool contractor,
        bool evidence)
    {
        ProcurementPurchaseOrderComplianceRules.IsContractSignatureComplete(
                organization ? DateTime.UtcNow : null,
                organization ? Guid.NewGuid() : null,
                organization ? "TDC signatory" : null,
                contractor ? DateTime.UtcNow : null,
                contractor ? "Supplier signatory" : null,
                evidence)
            .Should().BeFalse();
    }

    [Theory]
    [InlineData(
        ProcurementAwardReadinessPrerequisiteStatus.Passed,
        ProcurementAwardReadinessPrerequisiteStatus.Passed,
        true)]
    [InlineData(
        ProcurementAwardReadinessPrerequisiteStatus.NotApplicable,
        ProcurementAwardReadinessPrerequisiteStatus.NotApplicable,
        true)]
    [InlineData(
        ProcurementAwardReadinessPrerequisiteStatus.Passed,
        ProcurementAwardReadinessPrerequisiteStatus.Failed,
        false)]
    [InlineData(
        ProcurementAwardReadinessPrerequisiteStatus.Failed,
        ProcurementAwardReadinessPrerequisiteStatus.Passed,
        false)]
    public void ReadinessGroupAllowsOnlyPassingOrNotApplicableLineage(
        ProcurementAwardReadinessPrerequisiteStatus groupStatus,
        ProcurementAwardReadinessPrerequisiteStatus itemStatus,
        bool expected)
    {
        var groups = new[]
        {
            new ProcurementAwardReadinessPrerequisiteGroupDto
            {
                Group = ProcurementAwardReadinessPrerequisiteGroup.Evaluation,
                Status = groupStatus,
                Items =
                [
                    new ProcurementAwardReadinessPrerequisiteItemDto
                    {
                        Code = "EVALUATION",
                        Status = itemStatus
                    }
                ]
            }
        };

        ProcurementPurchaseOrderComplianceRules.IsPrerequisiteGroupSatisfied(
                groups,
                ProcurementAwardReadinessPrerequisiteGroup.Evaluation)
            .Should().Be(expected);
    }

    [Fact]
    public void MissingReadinessGroupFailsClosed()
    {
        ProcurementPurchaseOrderComplianceRules.IsPrerequisiteGroupSatisfied(
                [],
                ProcurementAwardReadinessPrerequisiteGroup.Evidence)
            .Should().BeFalse();
    }

    [Theory]
    [InlineData(null, "Preview")]
    [InlineData("", "Preview")]
    [InlineData(" Submit ", "Submit")]
    [InlineData("approve", "Approve")]
    [InlineData("Unknown", "Preview")]
    public void ActionNormalizationIsStable(string? value, string expected)
    {
        ProcurementPurchaseOrderComplianceRules.NormalizeAction(value)
            .Should().Be(expected);
    }
}
