using System.Reflection;
using ErpSystem.Api.Controllers.Procurement;
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
                "Summary", "Search", "Get", "GetForRegistration",
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

    private static ProcurementSupplierOnboardingTokensController Controller(
        Mock<IProcurementSupplierOnboardingTokenService> service) =>
        new(service.Object)
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
