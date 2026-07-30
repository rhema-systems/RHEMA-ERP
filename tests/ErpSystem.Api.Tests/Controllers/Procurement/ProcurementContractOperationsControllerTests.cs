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

public sealed class ProcurementContractOperationsControllerTests
{
    [Fact]
    public void ControllerUsesInternalPolicyAndDedicatedOperationsRoutes()
    {
        var type = typeof(ProcurementContractOperationsController);

        type.GetCustomAttribute<AuthorizeAttribute>()!.Policy
            .Should().Be("InternalOnly");
        type.GetCustomAttribute<RouteAttribute>()!.Template
            .Should().Be("api/procurement/contract-operations");
        type.GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.DeclaringType == type)
            .Select(method => method.Name)
            .Should().Contain(["Search", "Get", "ProcessAlerts"]);
    }

    [Fact]
    public async Task DomainFailuresMapToStructuredTenantSafeProblems()
    {
        var service = new Mock<IProcurementContractOperationsService>();
        service.Setup(item => item.GetAsync(
                Guid.Empty, It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementContractOperationsNotFoundException(
                "CONTRACT_NOT_FOUND", "Missing."));
        service.Setup(item => item.SearchAsync(
                It.IsAny<ProcurementContractOperationsSearchRequest>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new ProcurementContractOperationsAuthorizationException(
                    "Forbidden."));
        service.Setup(item => item.ProcessAlertsAsync(
                It.IsAny<ProcessProcurementContractOperationsAlertsRequest>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new ProcurementContractOperationsValidationException(
                    "CONTRACT_ID_REQUIRED", "Invalid."));
        var controller = Controller(service);

        AssertProblem(await controller.Get(Guid.Empty, default),
            404, "CONTRACT_NOT_FOUND");
        AssertProblem(await controller.Search(
                new ProcurementContractOperationsSearchRequest(), default),
            403, "CONTRACT_OPERATIONS_ACCESS_FORBIDDEN");
        AssertProblem(await controller.ProcessAlerts(
                new ProcessProcurementContractOperationsAlertsRequest(),
                default),
            422, "CONTRACT_ID_REQUIRED");
    }

    private static ProcurementContractOperationsController Controller(
        Mock<IProcurementContractOperationsService> service)
    {
        var controller = new ProcurementContractOperationsController(
            service.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
        controller.Request.Headers["X-Correlation-ID"] = "tdc-0408-test";
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
