using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.Procurement;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed partial class SupplierValidationServiceTests
{
    [Theory]
    [InlineData(SupplierEligibilityBoundary.Award)]
    [InlineData(SupplierEligibilityBoundary.Contract)]
    [InlineData(SupplierEligibilityBoundary.ManualPurchaseOrder)]
    [InlineData(SupplierEligibilityBoundary.FrameworkCallOff)]
    public async Task NoEffectivePolicyPreservesHistoricalRiskWithoutRequiringNewAssessment(
        SupplierEligibilityBoundary boundary)
    {
        await using var fixture = new Fixture();
        var supplier = fixture.AddSupplier();
        var policy = fixture.AddDec011Policy();
        policy.EffectiveTo = DateTime.UtcNow.AddDays(-1);
        var assessment = fixture.AddRiskAssessment(supplier.Id, policy);
        assessment.NextReviewDueAtUtc = DateTime.UtcNow.AddDays(-1);
        assessment.RiskScore = 0;
        assessment.DataComplete = false;
        assessment.EligibilityAction = ProcurementSupplierRiskEligibilityAction.AwardHardStop;
        var originalHash = assessment.IntegrityHash;
        var originalSnapshot = assessment.SnapshotJson;
        await fixture.Context.SaveChangesAsync();
        var request = new SupplierEligibilityEvaluationRequest
        {
            BusinessPartnerId = supplier.Id, Boundary = boundary
        };

        var full = await fixture.Service.EvaluateEligibilityAsync(request);
        var baseline = await fixture.Service.EvaluateBaselineEligibilityAsync(request);

        full.IsValid.Should().BeTrue();
        baseline.IsValid.Should().BeTrue();
        full.RiskPolicyAvailable.Should().BeFalse();
        full.RiskAssessmentId.Should().BeNull();
        full.RiskAssessmentCurrent.Should().BeFalse();
        full.Findings.Should().NotContain(item => item.Blocking);
        var retained = await fixture.Context.Set<ProcurementSupplierRiskAssessment>()
            .AsNoTracking().SingleAsync();
        retained.Id.Should().Be(assessment.Id);
        retained.IntegrityHash.Should().Be(originalHash);
        retained.SnapshotJson.Should().Be(originalSnapshot);
        retained.RiskScore.Should().Be(0);
        retained.DataComplete.Should().BeFalse();
        fixture.ControlEvents.Verify(item => item.RecordAsync(
            It.IsAny<ProcurementControlEventWriteRequest>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(SupplierEligibilityBoundary.Award, false)]
    [InlineData(SupplierEligibilityBoundary.Award, true)]
    [InlineData(SupplierEligibilityBoundary.Contract, false)]
    [InlineData(SupplierEligibilityBoundary.Contract, true)]
    [InlineData(SupplierEligibilityBoundary.ManualPurchaseOrder, false)]
    [InlineData(SupplierEligibilityBoundary.ManualPurchaseOrder, true)]
    [InlineData(SupplierEligibilityBoundary.FrameworkCallOff, false)]
    [InlineData(SupplierEligibilityBoundary.FrameworkCallOff, true)]
    public async Task NoEffectivePolicyStillDeniesApprovedAdverseReviewOrCheck(
        SupplierEligibilityBoundary boundary, bool checkOnly)
    {
        await using var fixture = new Fixture();
        var supplier = fixture.AddSupplier();
        var policy = fixture.AddDec011Policy();
        policy.EffectiveTo = DateTime.UtcNow.AddDays(-1);
        var review = fixture.AddDueDiligenceReview(supplier.Id, policy);
        if (checkOnly)
            review.Checks.First().Status = ProcurementSupplierDueDiligenceCheckStatus.Adverse;
        else
            review.Outcome = ProcurementSupplierDueDiligenceOutcome.Adverse;
        await fixture.Context.SaveChangesAsync();
        var request = new SupplierEligibilityEvaluationRequest
        {
            BusinessPartnerId = supplier.Id, Boundary = boundary
        };

        var full = await fixture.Service.EvaluateEligibilityAsync(request);
        var baseline = await fixture.Service.EvaluateBaselineEligibilityAsync(request);

        foreach (var result in new[] { full, baseline })
        {
            result.IsValid.Should().BeFalse();
            result.Findings.Should().ContainSingle(item =>
                item.Code == "SUPPLIER_DUE_DILIGENCE_ADVERSE" && item.Blocking);
            result.Findings.Should().NotContain(item =>
                item.Code == "SUPPLIER_DUE_DILIGENCE_REQUIRED");
        }
    }

    [Theory]
    [InlineData("draft")]
    [InlineData("foreign")]
    [InlineData("deleted")]
    public async Task NoEffectivePolicyDoesNotPromoteUnapprovedOrOutOfScopeFindings(string variant)
    {
        await using var fixture = new Fixture();
        var supplier = fixture.AddSupplier();
        var policy = fixture.AddDec011Policy();
        policy.EffectiveTo = DateTime.UtcNow.AddDays(-1);
        var review = fixture.AddDueDiligenceReview(supplier.Id, policy);
        review.Outcome = ProcurementSupplierDueDiligenceOutcome.Adverse;
        switch (variant)
        {
            case "draft": review.Status = ProcurementSupplierDueDiligenceStatus.Draft; break;
            case "foreign": review.TenantId = Guid.NewGuid(); break;
            case "deleted": review.IsDeleted = true; break;
        }
        await fixture.Context.SaveChangesAsync();
        var request = new SupplierEligibilityEvaluationRequest
        {
            BusinessPartnerId = supplier.Id, Boundary = SupplierEligibilityBoundary.Award
        };

        (await fixture.Service.EvaluateEligibilityAsync(request)).IsValid.Should().BeTrue();
        (await fixture.Service.EvaluateBaselineEligibilityAsync(request)).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("inactive", "SUPPLIER_INACTIVE")]
    [InlineData("blacklisted", "SUPPLIER_BLACKLISTED")]
    [InlineData("approval", "SUPPLIER_NOT_APPROVED")]
    [InlineData("registration", "REGISTRATION_NOT_APPROVED")]
    [InlineData("type", "SUPPLIER_TYPE_INVALID")]
    [InlineData("foreign", "SUPPLIER_NOT_FOUND")]
    [InlineData("deleted", "SUPPLIER_NOT_FOUND")]
    public async Task BaselineCannotBypassMandatorySupplierState(string variant, string code)
    {
        await using var fixture = new Fixture();
        var supplier = fixture.AddSupplier();
        switch (variant)
        {
            case "inactive": supplier.IsActive = false; break;
            case "blacklisted": supplier.IsBlacklisted = true; break;
            case "approval": supplier.ApprovalStatus = "Pending"; break;
            case "registration": supplier.RegistrationStatus = "Suspended"; break;
            case "type": supplier.PartnerType = "Customer"; break;
            case "foreign": supplier.TenantId = Guid.NewGuid(); break;
            case "deleted": supplier.IsDeleted = true; break;
        }
        await fixture.Context.SaveChangesAsync();
        var request = new SupplierEligibilityEvaluationRequest
        {
            BusinessPartnerId = supplier.Id, Boundary = SupplierEligibilityBoundary.Award
        };

        foreach (var result in new[]
                 {
                     await fixture.Service.EvaluateEligibilityAsync(request),
                     await fixture.Service.EvaluateBaselineEligibilityAsync(request)
                 })
        {
            result.IsValid.Should().BeFalse();
            result.ValidationCode.Should().Be(code);
        }
    }

    [Theory]
    [InlineData(SupplierEligibilityBoundary.Award)]
    [InlineData(SupplierEligibilityBoundary.Contract)]
    [InlineData(SupplierEligibilityBoundary.ManualPurchaseOrder)]
    [InlineData(SupplierEligibilityBoundary.FrameworkCallOff)]
    public async Task BaselineStillRequiresCurrentLinkedRegistrationEvidence(
        SupplierEligibilityBoundary boundary)
    {
        await using var fixture = new Fixture();
        var supplier = fixture.AddSupplier();
        var registration = new BusinessPartnerRegistration
        {
            Id = Guid.NewGuid(), TenantId = fixture.TenantId,
            RegistrationNumber = "BASELINE-APP", ApplicantName = supplier.PartnerName,
            PartnerType = "Supplier", Status = "Approved", BusinessPartnerId = supplier.Id
        };
        fixture.Context.Add(registration);
        fixture.EvidencePacks.Setup(item => item.GetRegistrationReadinessAsync(
                registration.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementSupplierEvidenceReadinessDto
            {
                RegistrationId = registration.Id, IsBound = true, IsReady = false,
                BlockingReasons = ["Tax clearance evidence has expired."]
            });
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Service.EvaluateBaselineEligibilityAsync(
            new SupplierEligibilityEvaluationRequest
            {
                BusinessPartnerId = supplier.Id, Boundary = boundary
            });

        result.IsValid.Should().BeFalse();
        result.ValidationCode.Should().Be("SUPPLIER_EVIDENCE_NOT_READY");
        result.Errors.Should().Contain("Tax clearance evidence has expired.");
    }

    [Fact]
    public async Task FullEnforcementRetainsEffectivePolicyAfterBaselineComposition()
    {
        await using var fixture = new Fixture();
        var supplier = fixture.AddSupplier();
        fixture.AddDec011Policy();
        await fixture.Context.SaveChangesAsync();
        var request = new SupplierEligibilityEvaluationRequest
        {
            BusinessPartnerId = supplier.Id, Boundary = SupplierEligibilityBoundary.Award,
            SourceType = "Tender", SourceReference = "TND-BASELINE", CorrelationId = "baseline-enforce"
        };

        (await fixture.Service.EvaluateBaselineEligibilityAsync(request)).IsValid.Should().BeTrue();
        await fixture.Service.Invoking(service => service.EnforceEligibilityAsync(request))
            .Should().ThrowAsync<SupplierEligibilityException>();
        fixture.ControlEvents.Verify(item => item.RecordAsync(
            It.Is<ProcurementControlEventWriteRequest>(item => item.EventType == "SupplierEligibility"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
