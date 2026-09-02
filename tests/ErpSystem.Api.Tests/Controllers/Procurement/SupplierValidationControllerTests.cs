using ErpSystem.Api.Controllers.Procurement;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Procurement;

public sealed class SupplierValidationControllerTests
{
    [Fact]
    public async Task TdcSupplierReviewerReceivesStructuredTenantSafeDecision()
    {
        var fixture = new Fixture(external: false);
        var partnerId = Guid.NewGuid();
        fixture.Validation.Setup(item => item.EvaluateEligibilityAsync(
                It.IsAny<SupplierEligibilityEvaluationRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SupplierValidationResult
            {
                IsValid = true,
                ValidationCode = "ELIGIBLE",
                BusinessPartnerId = partnerId,
                TenantId = fixture.TenantId,
                DecisionHash = new string('A', 64)
            });

        var action = await fixture.Controller.EvaluateEligibility(
            new SupplierEligibilityApiRequest
            {
                BusinessPartnerId = partnerId,
                Boundary = SupplierEligibilityBoundary.Contract
            }, CancellationToken.None);

        var ok = action.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<SupplierValidationResult>()
            .Which.TenantId.Should().Be(fixture.TenantId);
        fixture.Validation.Verify(item => item.EvaluateEligibilityAsync(
            It.Is<SupplierEligibilityEvaluationRequest>(request =>
                request.BusinessPartnerId == partnerId &&
                request.Boundary == SupplierEligibilityBoundary.Contract &&
                !request.RecordAudit),
            It.IsAny<CancellationToken>()), Times.Once);
        fixture.Access.Verify(item => item.CheckCapabilityAsync(
            It.Is<ProcurementAccessCapabilityRequest>(request =>
                request.PermissionCode == "procurement.supplier.review" &&
                request.SourceType == "SupplierEligibility" &&
                request.SourceReference == partnerId.ToString("N")),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LegacyTenantAdministratorWithoutTdcSupplierPermissionIsForbidden()
    {
        var fixture = new Fixture(external: false, grantSupplierPermission: false, legacyTenantAdministrator: true);

        var action = await fixture.Controller.EvaluateEligibility(
            new SupplierEligibilityApiRequest
            {
                BusinessPartnerId = Guid.NewGuid(),
                Boundary = SupplierEligibilityBoundary.StatusReview
            }, CancellationToken.None);

        action.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        fixture.Validation.Verify(item => item.EvaluateEligibilityAsync(
            It.IsAny<SupplierEligibilityEvaluationRequest>(),
            It.IsAny<CancellationToken>()), Times.Never);
        fixture.Access.Verify(item => item.CheckCapabilityAsync(
            It.Is<ProcurementAccessCapabilityRequest>(request =>
                request.PermissionCode == "procurement.supplier.review"),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
        fixture.Access.Verify(item => item.CheckCapabilityAsync(
            It.Is<ProcurementAccessCapabilityRequest>(request =>
                request.PermissionCode == "procurement.supplier.manage"),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExternalPortalIdentityCannotUseInternalEligibilityStatusApi()
    {
        var fixture = new Fixture(external: true);

        var action = await fixture.Controller.EvaluateEligibility(
            new SupplierEligibilityApiRequest
            {
                BusinessPartnerId = Guid.NewGuid(),
                Boundary = SupplierEligibilityBoundary.StatusReview
            }, CancellationToken.None);

        action.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        fixture.Validation.Verify(item => item.EvaluateEligibilityAsync(
            It.IsAny<SupplierEligibilityEvaluationRequest>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private sealed class Fixture
    {
        public Fixture(
            bool external,
            bool grantSupplierPermission = true,
            bool legacyTenantAdministrator = false)
        {
            TenantId = Guid.NewGuid();
            Validation = new Mock<ISupplierValidationService>();
            Access = new Mock<IProcurementAccessControlService>();
            var current = new Mock<ICurrentUserProvider>();
            current.SetupGet(item => item.TenantId).Returns(TenantId);
            current.SetupGet(item => item.UserId).Returns(Guid.NewGuid());
            current.SetupGet(item => item.IsAuthenticated).Returns(true);
            current.SetupGet(item => item.IsExternalUser).Returns(external);
            current.Setup(item => item.HasRole("TenantAdmin")).Returns(legacyTenantAdministrator);
            Access.Setup(item => item.CheckCapabilityAsync(
                    It.IsAny<ProcurementAccessCapabilityRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((ProcurementAccessCapabilityRequest request, string _, CancellationToken _) =>
                    new ProcurementAccessCapabilityDecisionDto
                    {
                        Allowed = grantSupplierPermission,
                        PermissionCode = request.PermissionCode,
                        Message = grantSupplierPermission
                            ? "Allowed by TDC procurement responsibility."
                            : "A TDC supplier-management or supplier-review responsibility is required."
                    });

            Controller = new SupplierValidationController(
                Validation.Object,
                Access.Object,
                current.Object,
                NullLogger<SupplierValidationController>.Instance)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext()
                }
            };
        }

        public Guid TenantId { get; }
        public Mock<IProcurementAccessControlService> Access { get; }
        public Mock<ISupplierValidationService> Validation { get; }
        public SupplierValidationController Controller { get; }
    }
}
