using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.Procurement;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class ProcurementPurchaseOrderSodRulesTests
{
    [Fact]
    public void ParticipantsRemoveMissingAndDuplicateIdentities()
    {
        var requester = Guid.NewGuid();
        var creator = Guid.NewGuid();

        ProcurementPurchaseOrderSodRules.Participants(
                null,
                Guid.Empty,
                requester,
                creator,
                requester)
            .Should().BeEquivalentTo([requester, creator]);
    }

    [Fact]
    public void RequesterOrCreatorIsNotIndependent()
    {
        var requester = Guid.NewGuid();
        var creator = Guid.NewGuid();
        var prohibited = new[] { requester, creator };

        ProcurementPurchaseOrderSodRules.IsActorIndependent(
                requester,
                prohibited)
            .Should().BeFalse();
        ProcurementPurchaseOrderSodRules.IsActorIndependent(
                creator,
                prohibited)
            .Should().BeFalse();
        ProcurementPurchaseOrderSodRules.IsActorIndependent(
                Guid.NewGuid(),
                prohibited)
            .Should().BeTrue();
    }

    [Theory]
    [InlineData(WorkflowOutcome.Approved, true)]
    [InlineData(WorkflowOutcome.Pending, false)]
    [InlineData(WorkflowOutcome.Rejected, false)]
    public void OnlyApprovedSubmissionOutcomeIsAutomaticApproval(
        WorkflowOutcome outcome,
        bool expected)
    {
        ProcurementPurchaseOrderSodRules.IsAutomaticApproval(outcome)
            .Should().Be(expected);
    }

    [Theory]
    [InlineData("AwardAutoApprove", "PO_AUTOMATIC_APPROVAL_PROHIBITED")]
    [InlineData("WorkflowAutoApprove", "PO_AUTOMATIC_APPROVAL_PROHIBITED")]
    [InlineData("DirectStatusApprove", "PO_DIRECT_APPROVAL_PROHIBITED")]
    public void BypassCodeIsStable(string attempt, string expected)
    {
        ProcurementPurchaseOrderSodRules.BypassCode(attempt)
            .Should().Be(expected);
    }

    [Theory]
    [InlineData(ProcurementPurchaseOrderSodRules.CreatePurchaseOrderReceipt)]
    [InlineData(ProcurementPurchaseOrderSodRules.CreateGoodsReceiptNote)]
    [InlineData(ProcurementPurchaseOrderSodRules.InitializeReceiptInspection)]
    [InlineData(ProcurementPurchaseOrderSodRules.SaveReceiptInspection)]
    [InlineData(ProcurementPurchaseOrderSodRules.SubmitReceiptInspection)]
    [InlineData(ProcurementPurchaseOrderSodRules.ApproveReceiptInspection)]
    [InlineData(ProcurementPurchaseOrderSodRules.ConfirmReplacementReceipt)]
    [InlineData(ProcurementPurchaseOrderSodRules.CloseReceiptInspection)]
    [InlineData(ProcurementPurchaseOrderSodRules.PostGoodsReceiptNoteToInventory)]
    public void EveryRegisteredReceiptActionNormalizesExactly(string action)
    {
        ProcurementPurchaseOrderSodRules.NormalizeReceiptAction($" {action} ")
            .Should().Be(action);
        ProcurementPurchaseOrderSodRules.IsReceiptAction(action)
            .Should().BeTrue();
    }

    [Fact]
    public void UnknownReceiptActionFailsClosed()
    {
        var action = () =>
            ProcurementPurchaseOrderSodRules.NormalizeReceiptAction(
                "BypassReceiptSod");

        action.Should().Throw<ArgumentException>()
            .WithMessage("*not registered*");
    }
}
