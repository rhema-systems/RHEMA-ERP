using ErpSystem.Api.Controllers.Procurement;
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
    public async Task InternalAdministratorReceivesStructuredTenantSafeDecision()
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
        public Fixture(bool external)
        {
            TenantId = Guid.NewGuid();
            Validation = new Mock<ISupplierValidationService>();
            var access = new Mock<IProcurementAccessControlService>();
            var current = new Mock<ICurrentUserProvider>();
            current.SetupGet(item => item.TenantId).Returns(TenantId);
            current.SetupGet(item => item.UserId).Returns(Guid.NewGuid());
            current.SetupGet(item => item.IsAuthenticated).Returns(true);
            current.SetupGet(item => item.IsExternalUser).Returns(external);
            current.Setup(item => item.HasRole("TenantAdmin")).Returns(!external);

            Controller = new SupplierValidationController(
                Validation.Object,
                access.Object,
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
        public Mock<ISupplierValidationService> Validation { get; }
        public SupplierValidationController Controller { get; }
    }
}
