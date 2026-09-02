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

    [Fact]
    public void GovernedCommitmentAllowsCoveredPurchaseOrderOrContractExposure()
    {
        var snapshot = ValidCommitment();

        var result = ProcurementPurchaseOrderComplianceRules
            .ValidateCommitment(snapshot);

        result.IsValid.Should().BeTrue();
        result.Code.Should().Be("PO_BUDGET_COMMITMENT_CURRENT");
    }

    [Fact]
    public void GovernedCommitmentRejectsCrossTenantLineage()
    {
        var snapshot = ValidCommitment() with
        {
            CommitmentTenantId = Guid.NewGuid()
        };

        var result = ProcurementPurchaseOrderComplianceRules
            .ValidateCommitment(snapshot);

        result.IsValid.Should().BeFalse();
        result.Code.Should().Be("PO_BUDGET_COMMITMENT_TENANT_MISMATCH");
    }

    [Fact]
    public void GovernedCommitmentRejectsReleasedOrMismatchedLineage()
    {
        var released = ProcurementPurchaseOrderComplianceRules
            .ValidateCommitment(ValidCommitment() with
            {
                CommitmentStatus = ProcurementBudgetCommitmentStatus.Released
            });
        var mismatched = ProcurementPurchaseOrderComplianceRules
            .ValidateCommitment(ValidCommitment() with
            {
                ReleaseCommitmentId = Guid.NewGuid()
            });

        released.Code.Should().Be("PO_BUDGET_COMMITMENT_INACTIVE");
        mismatched.Code.Should().Be("PO_BUDGET_COMMITMENT_LINEAGE_MISMATCH");
    }

    [Fact]
    public void GovernedCommitmentRejectsMissingRequisitionBudgetLineage()
    {
        var result = ProcurementPurchaseOrderComplianceRules
            .ValidateCommitment(ValidCommitment() with
            {
                RequisitionBudgetId = null
            });

        result.IsValid.Should().BeFalse();
        result.Code.Should().Be(
            "PO_BUDGET_COMMITMENT_REQUISITION_BUDGET_MISMATCH");
    }

    [Fact]
    public void GovernedCommitmentRejectsCurrencyAndExposureDrift()
    {
        var currency = ProcurementPurchaseOrderComplianceRules
            .ValidateCommitment(ValidCommitment() with
            {
                RequiredCurrency = "USD"
            });
        var exposure = ProcurementPurchaseOrderComplianceRules
            .ValidateCommitment(ValidCommitment() with
            {
                RequiredExposure = 1000.01m
            });

        currency.Code.Should().Be("PO_BUDGET_COMMITMENT_CURRENCY_MISMATCH");
        exposure.Code.Should().Be("PO_BUDGET_COMMITMENT_INSUFFICIENT");
    }

    private static ProcurementCommitmentLifecycleSnapshot ValidCommitment()
    {
        var tenantId = Guid.NewGuid();
        var requisitionId = Guid.NewGuid();
        var commitmentId = Guid.NewGuid();
        var budgetId = Guid.NewGuid();
        var approvedAt = DateTime.UtcNow.AddDays(-10);
        return new ProcurementCommitmentLifecycleSnapshot(
            tenantId,
            requisitionId,
            tenantId,
            "GHS",
            budgetId,
            tenantId,
            requisitionId,
            commitmentId,
            "BCR-001",
            commitmentId,
            tenantId,
            requisitionId,
            budgetId,
            "BCR-001",
            ProcurementBudgetCommitmentStatus.Reserved,
            1000m,
            0m,
            "GHS",
            budgetId,
            tenantId,
            "Active",
            "GHS",
            0m,
            1000m,
            Guid.NewGuid(),
            approvedAt,
            approvedAt,
            DateTime.UtcNow.AddDays(10),
            750m,
            "GHS",
            DateTime.UtcNow);
    }
}
