using System.Reflection;
using ErpSystem.Api.Controllers.Procurement;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Procurement;

public sealed class ProcurementSupplierOnboardingTokensControllerTests
{
    [Fact]
    public void ControllerRequiresAuthenticationAndExposesTheCompleteTokenLifecycle()
    {
        var type = typeof(ProcurementSupplierOnboardingTokensController);

        type.GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
        type.GetCustomAttribute<RouteAttribute>()!.Template.Should()
            .Be("api/procurement/supplier-onboarding-tokens");
        type.GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.DeclaringType == type)
            .Select(method => method.Name)
            .Should().Contain([
                "Summary", "IssueOptions", "Search", "Get", "GetForRegistration",
                "PaymentMethods", "Issue", "Reissue", "RecordPayment",
                "Reconcile", "RequestExemption", "DecideExemption"
            ]);
    }

    [Fact]
    public async Task DomainFailuresMapToStructured403404409And422Problems()
    {
        var service = new Mock<IProcurementSupplierOnboardingTokenService>();
        service.Setup(item => item.GetAsync(Guid.Empty, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementSupplierOnboardingTokenNotFoundException(
                "TOKEN_NOT_FOUND", "Missing."));
        service.Setup(item => item.GetSummaryAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementSupplierOnboardingTokenAuthorizationException(
                "Forbidden."));
        service.Setup(item => item.GetPaymentMethodsAsync(
                Guid.Empty, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementSupplierOnboardingTokenConflictException(
                "TOKEN_CONFLICT", "Conflict."));
        service.Setup(item => item.GetForRegistrationAsync(
                Guid.Empty, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementSupplierOnboardingTokenValidationException(
                "REGISTRATION_REQUIRED", "Invalid."));
        var controller = Controller(service);

        ((ObjectResult)await controller.Get(Guid.Empty, default))
            .StatusCode.Should().Be(404);
        ((ObjectResult)await controller.Summary(default))
            .StatusCode.Should().Be(403);
        ((ObjectResult)await controller.PaymentMethods(Guid.Empty, default))
            .StatusCode.Should().Be(409);
        var invalid = (ObjectResult)await controller.GetForRegistration(
            Guid.Empty, default);

        invalid.StatusCode.Should().Be(422);
        invalid.Value.Should().BeAssignableTo<ValidationProblemDetails>()
            .Which.Extensions["code"].Should().Be("REGISTRATION_REQUIRED");
    }

    [Fact]
    public async Task TrustedPaymentActivationDeliversSecretButReturnsOnlyTokenMetadata()
    {
        var tokenId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var token = new ProcurementSupplierOnboardingTokenDto
        {
            Id = tokenId,
            TokenReference = "TOK-PAID-001"
        };
        var service = new Mock<IProcurementSupplierOnboardingTokenService>();
        service.Setup(item => item.ReconcilePaymentAsync(
                tokenId,
                paymentId,
                It.IsAny<ReconcileProcurementSupplierOnboardingPaymentRequest>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementSupplierOnboardingTokenIssueResultDto
            {
                Token = token,
                PlaintextToken = "trusted-activation-secret"
            });
        var access = new Mock<IProcurementSupplierApplicantAccessService>();
        access.Setup(item => item.DeliverApplicationTokenAsync(
                tokenId,
                "trusted-activation-secret",
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SupplierApplicantTokenDeliveryDto
            {
                ApplicantAccessFound = true,
                Delivered = true,
                Status = "Sent"
            });
        var controller = Controller(service, access);

        var result = await controller.Reconcile(
            tokenId,
            paymentId,
            new ReconcileProcurementSupplierOnboardingPaymentRequest
            {
                ReconciliationReference = "BANK-001",
                Notes = "Funds verified.",
                RowVersion = "row-version"
            },
            CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeSameAs(token);
        access.Verify(item => item.DeliverApplicationTokenAsync(
            tokenId,
            "trusted-activation-secret",
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ActivationDeliveryFailureReturnsRecoverableConflict()
    {
        var tokenId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var service = new Mock<IProcurementSupplierOnboardingTokenService>();
        service.Setup(item => item.ReconcilePaymentAsync(
                tokenId,
                paymentId,
                It.IsAny<ReconcileProcurementSupplierOnboardingPaymentRequest>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementSupplierOnboardingTokenIssueResultDto
            {
                Token = new ProcurementSupplierOnboardingTokenDto { Id = tokenId },
                PlaintextToken = "trusted-activation-secret"
            });
        var access = new Mock<IProcurementSupplierApplicantAccessService>();
        access.Setup(item => item.DeliverApplicationTokenAsync(
                tokenId,
                "trusted-activation-secret",
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SupplierApplicantTokenDeliveryDto
            {
                ApplicantAccessFound = true,
                Delivered = false,
                Status = "Failed",
                FailureMessage = "Reissue to retry delivery."
            });
        var controller = Controller(service, access);

        var result = await controller.Reconcile(
            tokenId,
            paymentId,
            new ReconcileProcurementSupplierOnboardingPaymentRequest(),
            CancellationToken.None);

        var conflict = result.Should().BeOfType<ConflictObjectResult>().Subject;
        var problem = conflict.Value.Should().BeAssignableTo<ProblemDetails>().Subject;
        problem.Extensions["code"].Should()
            .Be("SUPPLIER_ONBOARDING_TOKEN_DELIVERY_FAILED");
        problem.Detail.Should().Contain("Retry payment confirmation")
            .And.NotContain("Reissue");
    }

    [Fact]
    public async Task ActivationDeliveryInvariantFailureReturnsRecoverableConflict()
    {
        var tokenId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var service = new Mock<IProcurementSupplierOnboardingTokenService>();
        service.Setup(item => item.ReconcilePaymentAsync(
                tokenId,
                paymentId,
                It.IsAny<ReconcileProcurementSupplierOnboardingPaymentRequest>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementSupplierOnboardingTokenIssueResultDto
            {
                Token = new ProcurementSupplierOnboardingTokenDto { Id = tokenId },
                PlaintextToken = "trusted-activation-secret"
            });
        var access = new Mock<IProcurementSupplierApplicantAccessService>();
        access.Setup(item => item.DeliverApplicationTokenAsync(
                tokenId,
                "trusted-activation-secret",
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementSupplierApplicantAccessException(
                "SUPPLIER_APPLICANT_TOKEN_SECRET_INVALID",
                "The application-token secret does not match the active token.",
                400));
        var controller = Controller(service, access);

        var result = await controller.Reconcile(
            tokenId,
            paymentId,
            new ReconcileProcurementSupplierOnboardingPaymentRequest(),
            CancellationToken.None);

        var conflict = result.Should().BeOfType<ConflictObjectResult>().Subject;
        var problem = conflict.Value.Should().BeAssignableTo<ProblemDetails>().Subject;
        problem.Extensions["code"].Should()
            .Be("SUPPLIER_ONBOARDING_TOKEN_DELIVERY_FAILED");
        problem.Detail.Should().NotContain("secret does not match",
            "internal delivery invariants must not be exposed to the operator");
    }

    private static ProcurementSupplierOnboardingTokensController Controller(
        Mock<IProcurementSupplierOnboardingTokenService> service,
        Mock<IProcurementSupplierApplicantAccessService>? applicantAccess = null) =>
        new(
            service.Object,
            applicantAccess?.Object ??
            Mock.Of<IProcurementSupplierApplicantAccessService>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    TraceIdentifier = "supplier-onboarding-token-test"
                }
            }
        };
}
