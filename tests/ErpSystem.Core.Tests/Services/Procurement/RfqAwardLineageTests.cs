using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class RfqAwardLineageTests
{
    [Fact]
    public void ReleaseOnlyRfqDoesNotRequireAdvancedSourcingCaseControls()
    {
        var rfq = new RequestForQuotation
        {
            SourcePurchaseRequisitionId = Guid.NewGuid(),
            SourcingReleaseId = Guid.NewGuid(),
            SourcingCaseId = null,
            SubmissionDeadline = DateTime.UtcNow.AddDays(2)
        };

        RfqService.UsesAdvancedSourcingControls(rfq).Should().BeFalse();

        var action = () => RfqService.ValidateReleaseOnlyDispatch(
            rfq,
            new[] { Guid.NewGuid() },
            null,
            DateTime.UtcNow);

        action.Should().NotThrow();
    }

    [Fact]
    public void SourcingCaseRfqRetainsAdvancedControls()
    {
        var rfq = new RequestForQuotation
        {
            SourcePurchaseRequisitionId = Guid.NewGuid(),
            SourcingReleaseId = Guid.NewGuid(),
            SourcingCaseId = Guid.NewGuid()
        };

        RfqService.UsesAdvancedSourcingControls(rfq).Should().BeTrue();
    }

    [Fact]
    public void ReleaseOnlyDispatchRequiresCurrentReleaseFutureDeadlineAndRecipient()
    {
        var rfq = new RequestForQuotation
        {
            SourcePurchaseRequisitionId = Guid.NewGuid(),
            SourcingReleaseId = Guid.NewGuid(),
            SubmissionDeadline = new DateTime(2026, 8, 25, 12, 0, 0, DateTimeKind.Utc)
        };

        var action = () => RfqService.ValidateReleaseOnlyDispatch(
            rfq,
            Array.Empty<Guid>(),
            null,
            new DateTime(2026, 8, 25, 12, 0, 0, DateTimeKind.Utc));

        action.Should().Throw<ProcurementRequisitionSourcingValidationException>()
            .Where(exception => exception.Code == "RFQ_DEADLINE_PASSED");
    }

    [Fact]
    public void ReleaseOnlyAwardAcceptsSentRfqAfterDeadline()
    {
        var rfq = new RequestForQuotation
        {
            Status = "Sent",
            SourcePurchaseRequisitionId = Guid.NewGuid(),
            SourcingReleaseId = Guid.NewGuid(),
            SubmissionDeadline = new DateTime(2026, 8, 25, 12, 0, 0, DateTimeKind.Utc)
        };

        var action = () => RfqService.ValidateReleaseOnlyAward(
            rfq,
            new CreatePurchaseOrdersFromRfqDto
            {
                Mode = "WinnerTakesAll",
                QuoteId = Guid.NewGuid()
            },
            new DateTime(2026, 8, 25, 12, 1, 0, DateTimeKind.Utc));

        action.Should().NotThrow();
    }

    [Fact]
    public void ReleaseOnlyAwardRemainsSealedBeforeDeadline()
    {
        var rfq = new RequestForQuotation
        {
            Status = "Sent",
            SourcePurchaseRequisitionId = Guid.NewGuid(),
            SourcingReleaseId = Guid.NewGuid(),
            SubmissionDeadline = new DateTime(2026, 8, 25, 12, 0, 0, DateTimeKind.Utc)
        };

        var action = () => RfqService.ValidateReleaseOnlyAward(
            rfq,
            new CreatePurchaseOrdersFromRfqDto(),
            new DateTime(2026, 8, 25, 11, 59, 0, DateTimeKind.Utc));

        action.Should().Throw<ProcurementRequisitionSourcingValidationException>()
            .Where(exception => exception.Code == "RFQ_AWARD_BEFORE_DEADLINE");
    }

    [Fact]
    public void ReleaseOnlyAwardRequiresApprovedRequisitionAndReleaseLineage()
    {
        var rfq = new RequestForQuotation
        {
            Status = "Sent",
            SubmissionDeadline = new DateTime(2026, 8, 25, 12, 0, 0, DateTimeKind.Utc)
        };

        var action = () => RfqService.ValidateReleaseOnlyAward(
            rfq,
            new CreatePurchaseOrdersFromRfqDto(),
            new DateTime(2026, 8, 25, 12, 1, 0, DateTimeKind.Utc));

        action.Should().Throw<ProcurementRequisitionSourcingValidationException>()
            .Where(exception => exception.Code == "RFQ_SOURCE_LINEAGE_REQUIRED");
    }

    [Fact]
    public void WinnerTakesAllPersistsAwardTimestampAndSupplier()
    {
        var supplierId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var awardedAtUtc = new DateTime(2026, 8, 14, 12, 30, 0, DateTimeKind.Utc);
        var rfq = new RequestForQuotation();

        RfqService.ApplyAwardLineage(rfq, new[] { supplierId }, awardedAtUtc, actorId);

        rfq.Status.Should().Be("Awarded");
        rfq.AwardedAt.Should().Be(awardedAtUtc);
        rfq.AwardedBusinessPartnerId.Should().Be(supplierId);
        rfq.UpdatedAt.Should().Be(awardedAtUtc);
        rfq.LastModifiedById.Should().Be(actorId);
    }

    [Fact]
    public void SplitAwardPersistsTimestampWithoutMisrepresentingOneSupplierAsWinner()
    {
        var awardedAtUtc = new DateTime(2026, 8, 14, 12, 30, 0, DateTimeKind.Utc);
        var rfq = new RequestForQuotation();

        RfqService.ApplyAwardLineage(
            rfq,
            new[] { Guid.NewGuid(), Guid.NewGuid() },
            awardedAtUtc,
            Guid.NewGuid());

        rfq.Status.Should().Be("Awarded");
        rfq.AwardedAt.Should().Be(awardedAtUtc);
        rfq.AwardedBusinessPartnerId.Should().BeNull();
    }

    [Fact]
    public void StatutoryLifecycleDatabaseViolationIsRecognizedForFriendlyValidation()
    {
        var exception = new DbUpdateException(
            "Save failed",
            new InvalidOperationException(
                "RFQ issue terms or statutory lifecycle transition is invalid."));

        RfqService.IsRfqStatutoryLifecycleViolation(exception).Should().BeTrue();
        RfqService.IsRfqStatutoryLifecycleViolation(
            new DbUpdateException("Another database error")).Should().BeFalse();
    }

    [Fact]
    public void PurchaseOrderSourceConstraintViolationIsRecognizedForFriendlyValidation()
    {
        var exception = new DbUpdateException(
            "Save failed",
            new InvalidOperationException(
                "The INSERT statement conflicted with the CHECK constraint \"CK_PurchaseOrders_ApprovedSourceLineage\"."));

        RfqService.IsPurchaseOrderApprovedSourceLineageViolation(exception)
            .Should().BeTrue();
        RfqService.IsPurchaseOrderApprovedSourceLineageViolation(
                new DbUpdateException(
                    "Save failed",
                    new InvalidOperationException(
                        "A complete immutable approved source lineage is required for every purchase order.")))
            .Should().BeTrue("the SQL trigger reports its governed error message instead of the check-constraint name");
        RfqService.IsPurchaseOrderApprovedSourceLineageViolation(
            new DbUpdateException("Another database error")).Should().BeFalse();
    }

    [Fact]
    public void PurchaseOrderBudgetConstraintViolationIsRecognizedForFriendlyValidation()
    {
        var exception = new DbUpdateException(
            "Save failed",
            new InvalidOperationException(
                "Purchase-order issuance requires the exact active tenant budget commitment with sufficient reserved exposure."));

        RfqService.IsPurchaseOrderBudgetCommitmentViolation(exception)
            .Should().BeTrue();
        RfqService.IsPurchaseOrderBudgetCommitmentViolation(
            new DbUpdateException("Another database error")).Should().BeFalse();
    }
}
