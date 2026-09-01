using System.Text;
using ErpSystem.Api.Authorization;
using ErpSystem.Api.Services.Finance.GL;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Finance.Integration;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class FinanceDimensionCertificationServiceTests
{
    [Fact]
    public void CertificationRoutesUseDedicatedGovernancePermission()
    {
        FinancePermissionPolicyMap.GetRequiredPolicies(
                "FinanceDimensionCertificationsController", "Promote", ["POST"])
            .Should().Equal(FinancePermissions.ManageDimensionCertification);
        FinancePermissionPolicyMap.GetRequiredPolicies(
                "FinanceDimensionCertificationsController", "GetRoutes", ["GET"])
            .Should().Equal(FinancePermissions.ViewFinance);
        FinancePermissionPolicyMap.GetRequiredPolicies(
                "FinanceDimensionCertificationsController", "GetAssessment", ["GET"])
            .Should().Equal(FinancePermissions.ManageDimensionCertification);
        FinancePermissionPolicyMap.GetRequiredPolicies(
                "FinanceDimensionCertificationsController", "ExportReadiness", ["GET"])
            .Should().Equal(FinancePermissions.ManageDimensionCertification);
    }

    [Fact]
    public async Task MissingRouteProviderFailsPromotionClosed()
    {
        await using var db = CreateContext();
        var tenantId = Guid.NewGuid();
        var service = CreateService(db, tenantId);

        var assessment = await service.AssessReadinessAsync(
            FinanceDimensionRouteId.FinanceApVendorInvoice,
            FinanceDimensionCertificationState.Enforced);

        assessment.BlockerCount.Should().Be(1);
        assessment.Blockers.Should().ContainSingle(blocker =>
            blocker.Code == "READINESS_PROVIDER_NOT_INSTALLED");
        var action = () => service.PromoteAsync(
            FinanceDimensionRouteId.FinanceApVendorInvoice,
            Promotion(assessment));
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*blockers remain*");
        db.FinanceDimensionCertificationTransitions.Should().BeEmpty();
    }

    [Fact]
    public async Task ZeroBlockerAssessmentPromotesRecognizedRouteAndConsumesEvidence()
    {
        await using var db = CreateContext();
        var tenantId = Guid.NewGuid();
        var provider = new MutableReadinessProvider(
            FinanceDimensionRouteId.FinanceApSupplierDebitNote, "debit-note:v7");
        var service = CreateService(db, tenantId, provider);

        var assessment = await service.AssessReadinessAsync(
            provider.RouteId, FinanceDimensionCertificationState.Enforced);
        var result = await service.PromoteAsync(provider.RouteId, Promotion(assessment));

        result.State.Should().Be(FinanceDimensionCertificationState.Enforced);
        (await db.FinanceDimensionRouteCertifications.SingleAsync()).State
            .Should().Be(FinanceDimensionCertificationState.Enforced);
        (await db.FinanceDimensionReadinessAssessments.SingleAsync()).ConsumedAt
            .Should().NotBeNull();
        var transition = await db.FinanceDimensionCertificationTransitions.SingleAsync();
        transition.PreviousState.Should().Be(FinanceDimensionCertificationState.CaptureOptional);
        transition.NewState.Should().Be(FinanceDimensionCertificationState.Enforced);
        transition.ReadinessEvidenceHash.Should().Be(assessment.EvidenceHash);

        var downgrade = () => service.AssessReadinessAsync(
            provider.RouteId, FinanceDimensionCertificationState.CaptureOptional);
        await downgrade.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*cannot transition*");
    }

    [Fact]
    public async Task ChangedDataWatermarkMakesAssessmentStale()
    {
        await using var db = CreateContext();
        var tenantId = Guid.NewGuid();
        var provider = new MutableReadinessProvider(
            FinanceDimensionRouteId.FinanceArCustomerInvoice, "customer-invoice:v12");
        var service = CreateService(db, tenantId, provider);
        var assessment = await service.AssessReadinessAsync(
            provider.RouteId, FinanceDimensionCertificationState.Enforced);
        provider.Watermark = "customer-invoice:v13";

        var action = () => service.PromoteAsync(provider.RouteId, Promotion(assessment));

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*stale*");
        db.FinanceDimensionRouteCertifications.Should().BeEmpty();
        (await db.FinanceDimensionReadinessAssessments.SingleAsync()).ConsumedAt.Should().BeNull();
    }

    [Fact]
    public async Task ReadinessCsvNeutralizesSpreadsheetFormulaPayloads()
    {
        await using var db = CreateContext();
        var tenantId = Guid.NewGuid();
        var provider = new MutableReadinessProvider(
            FinanceDimensionRouteId.FinanceApVendorInvoice,
            "invoice:v4",
            [new FinanceDimensionReadinessBlockerDto
            {
                Code = "MISSING_DIMENSION",
                DocumentReference = "=HYPERLINK(\"https://invalid.example\")",
                LifecycleState = "+Draft",
                DimensionIssue = "@PROJECT",
                Message = "-missing project",
                RemediationStatus = "Select a valid value"
            }]);
        var service = CreateService(db, tenantId, provider);
        var assessment = await service.AssessReadinessAsync(
            provider.RouteId, FinanceDimensionCertificationState.Enforced);

        var csv = Encoding.UTF8.GetString(await service.ExportReadinessCsvAsync(assessment.Id));

        csv.Should().Contain("\"'=HYPERLINK");
        csv.Should().Contain("\"'+Draft\"");
        csv.Should().Contain("\"'@PROJECT\"");
        csv.Should().Contain("\"'-missing project\"");
        csv.Should().NotContain("\",=HYPERLINK");
    }

    private static PromoteFinanceDimensionRouteDto Promotion(
        FinanceDimensionReadinessAssessmentDto assessment) => new()
    {
        ReadinessAssessmentId = assessment.Id,
        TargetState = FinanceDimensionCertificationState.Enforced,
        Reason = "Route-level UAT passed with no outstanding blockers."
    };

    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"finance-dimension-certification-{Guid.NewGuid():N}").Options);

    private static FinanceDimensionCertificationService CreateService(
        ApplicationDbContext db,
        Guid tenantId,
        params IFinanceDimensionReadinessProvider[] providers)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(user => user.TenantId).Returns(tenantId);
        currentUser.SetupGet(user => user.UserId).Returns(Guid.NewGuid().ToString());
        currentUser.SetupGet(user => user.UserName).Returns("finance.dimension.governor");
        currentUser.SetupGet(user => user.Claims).Returns(new Dictionary<string, string>());
        var audit = new Mock<IFinanceAuditService>();
        audit.Setup(service => service.RecordAsync(
                It.IsAny<FinanceAuditEventDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuditLog { Id = Guid.NewGuid() });
        return new FinanceDimensionCertificationService(db, currentUser.Object, audit.Object, providers);
    }

    private sealed class MutableReadinessProvider : IFinanceDimensionReadinessProvider
    {
        public MutableReadinessProvider(
            FinanceDimensionRouteId routeId,
            string watermark,
            IReadOnlyList<FinanceDimensionReadinessBlockerDto>? blockers = null)
        {
            RouteId = routeId;
            Watermark = watermark;
            Blockers = blockers ?? [];
        }

        public FinanceDimensionRouteId RouteId { get; }
        public string Watermark { get; set; }
        public IReadOnlyList<FinanceDimensionReadinessBlockerDto> Blockers { get; set; }

        public Task<FinanceDimensionReadinessContribution> EvaluateAsync(
            Guid tenantId,
            FinanceDimensionRouteDefinition route,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new FinanceDimensionReadinessContribution(Watermark, Blockers));
    }
}
