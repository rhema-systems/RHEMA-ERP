using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
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

    [Theory]
    [InlineData(ProcurementMethodType.NationalCompetitiveTendering)]
    [InlineData(ProcurementMethodType.InternationalCompetitiveTendering)]
    [InlineData(ProcurementMethodType.QualityBasedSelection)]
    [InlineData(ProcurementMethodType.QualityAndCostBasedSelection)]
    public void AdvancedStatutoryOrConsultingTenderRequiresControlledPublication(
        ProcurementMethodType method)
    {
        TenderService.RequiresControlledPublication(new ProcurementSourcingCaseEntryGateDto
        {
            SourcingReleaseId = Guid.NewGuid(),
            SourcingCaseId = Guid.NewGuid(),
            HasAdvancedAuthorityRoute = true,
            SelectedMethod = method
        }).Should().BeTrue();
    }

    [Fact]
    public void ReleaseOnlyInvitationToBidDoesNotRequireAdvancedPublicationFields()
    {
        TenderService.RequiresControlledPublication(new ProcurementSourcingCaseEntryGateDto
        {
            SourcingReleaseId = Guid.NewGuid(),
            SourcingCaseId = null,
            SelectedMethod = ProcurementMethodType.NationalCompetitiveTendering
        }).Should().BeFalse();
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

    [Theory]
    [InlineData(ProcurementMethodType.NationalCompetitiveTendering)]
    [InlineData(ProcurementMethodType.InternationalCompetitiveTendering)]
    public void StandardCaseKeepsDocumentControlsWithoutAdvancedPublication(ProcurementMethodType method)
    {
        var gate = new ProcurementSourcingCaseEntryGateDto
        {
            SourcingReleaseId = Guid.NewGuid(), SourcingCaseId = Guid.NewGuid(),
            SelectedMethod = method, HasAdvancedAuthorityRoute = false
        };
        TenderService.UsesAdvancedSourcingControls(gate).Should().BeTrue();
        TenderService.RequiresControlledPublication(gate).Should().BeFalse();
    }

    [Theory]
    [InlineData(ProcurementMethodType.QualityBasedSelection)]
    [InlineData(ProcurementMethodType.QualityAndCostBasedSelection)]
    public void ConsultingMethodsCannotDowngradeTheirControlledEnvelopeRules(ProcurementMethodType method)
    {
        ProcurementTenderRouting.RequiresControlledLifecycle(method, false).Should().BeTrue();
    }

    [Fact]
    public void PartialAuthorityMetadataStillRoutesToAdvancedValidation()
    {
        ProcurementTenderRouting.HasAdvancedAuthority(Guid.NewGuid(), null).Should().BeTrue();
        ProcurementTenderRouting.HasAdvancedAuthority(null, "AUTH-1").Should().BeTrue();
        ProcurementTenderRouting.HasAdvancedAuthority(null, null).Should().BeFalse();
    }

    [Fact]
    public void TenderScheduleRejectsOpeningBeforeDeadlineAtEveryWriteBoundary()
    {
        var deadline = new DateTime(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc);

        var invalid = () => TenderService.ValidateTenderSchedule(
            deadline,
            deadline.AddMinutes(-1));

        invalid.Should().Throw<ProcurementRequisitionSourcingValidationException>()
            .Where(exception => exception.Code == "TENDER_OPENING_BEFORE_DEADLINE");
    }

    [Fact]
    public void TenderScheduleAllowsOpeningAtOrAfterDeadline()
    {
        var deadline = new DateTime(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc);

        var equal = () => TenderService.ValidateTenderSchedule(deadline, deadline);
        var later = () => TenderService.ValidateTenderSchedule(deadline, deadline.AddMinutes(1));

        equal.Should().NotThrow();
        later.Should().NotThrow();
    }
}
