using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed partial class ProcurementAwardReadinessServiceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AwardReadinessUsesSharedRegistrationEvidenceWithoutOptionalRiskPolicy(bool evidenceReady)
    {
        await using var fixture = new Fixture();
        var registration = AddBaselineRegistration(fixture);
        fixture.EvidencePacks.Setup(item => item.GetRegistrationReadinessAsync(
                registration.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementSupplierEvidenceReadinessDto
            {
                RegistrationId = registration.Id, PackVersionId = Guid.NewGuid(),
                IsBound = true, IsReady = evidenceReady,
                BlockingReasons = evidenceReady ? [] : ["Tax clearance evidence has expired."]
            });
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Service.EvaluateAsync(
            ProcurementAwardReadinessSourceType.Tender, fixture.Tender.Id,
            fixture.Request("baseline-registration"), "baseline-registration");

        var supplier = result.Suppliers.Should().ContainSingle().Subject;
        supplier.IsEligible.Should().Be(evidenceReady);
        if (!evidenceReady)
        {
            supplier.Errors.Should().Contain("Tax clearance evidence has expired.");
            result.Status.Should().Be(ProcurementAwardReadinessDecisionStatus.Blocked);
        }
        supplier.Errors.Should().NotContain(item => item.Contains("risk assessment"));
        fixture.EvidencePacks.Verify(item => item.GetRegistrationReadinessAsync(
            registration.Id, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        fixture.ControlEvents.Verify(item => item.RecordAsync(
            It.Is<ProcurementControlEventWriteRequest>(item => item.EventType == "SupplierEligibility"),
            It.IsAny<CancellationToken>()), Times.Never);
        fixture.ControlEvents.Verify(item => item.RecordAsync(
            It.Is<ProcurementControlEventWriteRequest>(item => item.EventType == "ProcurementAwardReadiness"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AwardReadinessCannotIgnoreApprovedAdverseSupplierReviewWithoutPolicy(bool checkOnly)
    {
        await using var fixture = new Fixture();
        var review = new ProcurementSupplierDueDiligenceReview
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId, BusinessPartnerId = fixture.Partner.Id,
            ReviewReference = "DD-BASELINE", Status = ProcurementSupplierDueDiligenceStatus.Approved,
            Outcome = checkOnly ? ProcurementSupplierDueDiligenceOutcome.Clear : ProcurementSupplierDueDiligenceOutcome.Adverse,
            ApprovedById = fixture.SupplierControllerId, ApprovedAtUtc = DateTime.UtcNow.AddDays(-1),
            IntegrityHash = new string('A', 64), RowVersion = Guid.NewGuid().ToByteArray()
        };
        if (checkOnly)
            review.Checks.Add(new ProcurementSupplierDueDiligenceCheck
            {
                Id = Guid.NewGuid(), TenantId = fixture.TenantId, ReviewId = review.Id,
                CheckType = ProcurementSupplierDueDiligenceCheckType.PpaDebarment,
                Status = ProcurementSupplierDueDiligenceCheckStatus.Adverse,
                SourceName = "Retained review", SourceReference = "DD-BASELINE-CHECK"
            });
        fixture.Context.Add(review);
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Service.EvaluateAsync(
            ProcurementAwardReadinessSourceType.Tender, fixture.Tender.Id,
            fixture.Request("baseline-adverse"), "baseline-adverse");

        var supplier = result.Suppliers.Should().ContainSingle().Subject;
        supplier.IsEligible.Should().BeFalse();
        supplier.Errors.Should().Contain(item => item.Contains("approved supplier review contains an adverse finding"));
        result.Status.Should().Be(ProcurementAwardReadinessDecisionStatus.Blocked);
        (await fixture.Context.Set<ProcurementSupplierDueDiligenceReview>().SingleAsync())
            .IntegrityHash.Should().Be(new string('A', 64));
    }

    [Fact]
    public async Task AwardReadinessDeniesDuplicateLinkedRegistrationBeforeDocumentLookup()
    {
        await using var fixture = new Fixture();
        AddBaselineRegistration(fixture);
        AddBaselineRegistration(fixture);
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Service.EvaluateAsync(
            ProcurementAwardReadinessSourceType.Tender, fixture.Tender.Id,
            fixture.Request("baseline-duplicate"), "baseline-duplicate");

        var supplier = result.Suppliers.Should().ContainSingle().Subject;
        supplier.IsEligible.Should().BeFalse();
        supplier.Errors.Should().Contain("More than one onboarding application is linked to this supplier.");
        fixture.EvidencePacks.Verify(item => item.GetRegistrationReadinessAsync(
            It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ChangingReadyEvidencePackLineageMakesRetainedAwardReadinessStale()
    {
        await using var fixture = new Fixture();
        var registration = AddBaselineRegistration(fixture);
        var evidence = new ProcurementSupplierEvidenceReadinessDto
        {
            RegistrationId = registration.Id, PackVersionId = Guid.NewGuid(),
            PackCode = "GOODS", PackVersion = 1, IsBound = true, IsReady = true
        };
        fixture.EvidencePacks.Setup(item => item.GetRegistrationReadinessAsync(
                registration.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => evidence);
        await fixture.Context.SaveChangesAsync();
        var first = await fixture.Service.EvaluateAsync(
            ProcurementAwardReadinessSourceType.Tender, fixture.Tender.Id,
            fixture.Request("baseline-lineage"), "baseline-lineage");
        var initial = await fixture.Service.GetLatestAsync(
            ProcurementAwardReadinessSourceType.Tender, fixture.Tender.Id);
        initial!.IsCurrent.Should().BeTrue();

        evidence.PackVersionId = Guid.NewGuid();
        evidence.PackVersion = 2;
        var latest = await fixture.Service.GetLatestAsync(
            ProcurementAwardReadinessSourceType.Tender, fixture.Tender.Id);

        latest!.Id.Should().Be(first.Id);
        latest.IsCurrent.Should().BeFalse();
        latest.SourceIntegrityHash.Should().Be(first.SourceIntegrityHash,
            "read-only revalidation must not overwrite the immutable decision");
        (await fixture.Context.ProcurementAwardReadinessDecisions.CountAsync()).Should().Be(1);
    }

    private static BusinessPartnerRegistration AddBaselineRegistration(Fixture fixture)
    {
        var registration = new BusinessPartnerRegistration
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId,
            RegistrationNumber = $"BASELINE-{Guid.NewGuid():N}",
            ApplicantName = fixture.Partner.PartnerName, PartnerType = "Supplier",
            Status = "Approved", BusinessPartnerId = fixture.Partner.Id,
            ApprovedDate = DateTime.UtcNow.AddDays(-2)
        };
        fixture.Context.Add(registration);
        return registration;
    }
}
