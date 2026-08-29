using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using FluentAssertions;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class TenderSourceControlTests
{
    [Fact]
    public void ReleaseOnlyTenderDoesNotInvokeAdvancedSourcingControls()
    {
        TenderService.UsesAdvancedSourcingControls(new ProcurementSourcingCaseEntryGateDto
        {
            SourcingReleaseId = Guid.NewGuid(),
            SourcingCaseId = null
        }).Should().BeFalse();
    }

    [Fact]
    public void SourcingCaseTenderRetainsAdvancedSourcingControls()
    {
        TenderService.UsesAdvancedSourcingControls(new ProcurementSourcingCaseEntryGateDto
        {
            SourcingReleaseId = Guid.NewGuid(),
            SourcingCaseId = Guid.NewGuid()
        }).Should().BeTrue();
    }

    [Fact]
    public void ReleaseOnlyTenderRequiresFutureDeadlineAndValidOpeningSequence()
    {
        var now = DateTime.UtcNow;
        var tender = new Tender
        {
            SourcePurchaseRequisitionId = Guid.NewGuid(),
            SourcingReleaseId = Guid.NewGuid()
        };

        var valid = new PublishTenderDto
        {
            SubmissionDeadline = now.AddDays(1),
            OpeningDate = now.AddDays(1).AddHours(1)
        };

        var action = () => TenderService.ValidateReleaseOnlyPublication(tender, valid, now);
        action.Should().NotThrow();

        var invalid = new PublishTenderDto
        {
            SubmissionDeadline = now.AddDays(1),
            OpeningDate = now.AddHours(1)
        };
        var invalidAction = () => TenderService.ValidateReleaseOnlyPublication(tender, invalid, now);
        invalidAction.Should().Throw<ProcurementRequisitionSourcingValidationException>()
            .Where(exception => exception.Code == "TENDER_OPENING_BEFORE_DEADLINE");
    }
}
