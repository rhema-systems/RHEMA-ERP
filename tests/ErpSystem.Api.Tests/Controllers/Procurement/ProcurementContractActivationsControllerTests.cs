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

public sealed class ProcurementContractActivationsControllerTests
{
    [Fact]
    public void ControllerUsesInternalPolicyAndDedicatedLifecycleRoutes()
    {
        var type = typeof(ProcurementContractActivationsController);

        type.GetCustomAttribute<AuthorizeAttribute>()!.Policy
            .Should().Be("InternalOnly");
        type.GetCustomAttribute<RouteAttribute>()!.Template
            .Should().Be("api/procurement/contract-activations");
        type.GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.DeclaringType == type)
            .Select(method => method.Name)
            .Should().Contain([
                "GetOverview",
                "Submit",
                "Decide",
                "Activate"
            ]);
    }

    [Fact]
    public async Task DomainFailuresMapToStructuredTenantSafeProblems()
    {
        var service = new Mock<IProcurementContractActivationService>();
        service.Setup(item => item.GetOverviewAsync(
                Guid.Empty, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementContractActivationNotFoundException(
                "CONTRACT_NOT_FOUND", "Missing."));
        service.Setup(item => item.SubmitAsync(
                Guid.Empty,
                It.IsAny<SubmitProcurementContractActivationRequest>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementContractActivationValidationException(
                "CONTRACT_ACTIVATION_EVIDENCE_REQUIRED", "Invalid."));
        service.Setup(item => item.DecideAsync(
                Guid.Empty,
                It.IsAny<DecideProcurementContractActivationRequest>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementContractActivationAuthorizationException(
                "Forbidden."));
        service.Setup(item => item.ActivateAsync(
                Guid.Empty,
                It.IsAny<ActivateProcurementContractRequest>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementContractActivationConflictException(
                "CONTRACT_ACTIVATION_STALE", "Stale."));
        var controller = Controller(service);

        AssertProblem(
            await controller.GetOverview(Guid.Empty, default),
            404, "CONTRACT_NOT_FOUND");
        AssertProblem(
            await controller.Submit(
                Guid.Empty,
                new SubmitProcurementContractActivationRequest(),
                default),
            422, "CONTRACT_ACTIVATION_EVIDENCE_REQUIRED");
        AssertProblem(
            await controller.Decide(
                Guid.Empty,
                new DecideProcurementContractActivationRequest(),
                default),
            403, "CONTRACT_ACTIVATION_ACCESS_FORBIDDEN");
        AssertProblem(
            await controller.Activate(
                Guid.Empty,
                new ActivateProcurementContractRequest(),
                default),
            409, "CONTRACT_ACTIVATION_STALE");
    }

    private static ProcurementContractActivationsController Controller(
        Mock<IProcurementContractActivationService> service)
    {
        var controller =
            new ProcurementContractActivationsController(service.Object)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext()
                }
            };
        controller.Request.Headers["X-Correlation-ID"] = "tdc-0407-test";
        return controller;
    }

    private static void AssertProblem(
        IActionResult result,
        int status,
        string code)
    {
        var objectResult = result.Should()
            .BeAssignableTo<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(status);
        var problem = objectResult.Value.Should()
            .BeAssignableTo<ProblemDetails>().Subject;
        problem.Extensions["code"].Should().Be(code);
    }
}
