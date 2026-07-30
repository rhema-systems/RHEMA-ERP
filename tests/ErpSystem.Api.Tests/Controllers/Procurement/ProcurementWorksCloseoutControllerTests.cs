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

public sealed class ProcurementWorksCloseoutControllerTests
{
    [Fact]
    public void ControllerUsesInternalPolicyAndDedicatedWorksRoutes()
    {
        var type = typeof(ProcurementWorksCloseoutController);

        type.GetCustomAttribute<AuthorizeAttribute>()!.Policy
            .Should().Be("InternalOnly");
        type.GetCustomAttribute<RouteAttribute>()!.Template
            .Should().Be("api/procurement/works-closeout");
        type.GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.DeclaringType == type)
            .Select(method => method.Name)
            .Should().Contain(["GetOverview", "Submit", "Decide"]);
    }

    [Fact]
    public async Task DomainFailuresMapToStructuredTenantSafeProblems()
    {
        var service = new Mock<IProcurementWorksCloseoutService>();
        service.Setup(item => item.GetOverviewAsync(
                Guid.Empty, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementWorksCloseoutNotFoundException(
                "CONTRACT_NOT_FOUND", "Missing."));
        service.Setup(item => item.SubmitAsync(
                Guid.Empty,
                It.IsAny<SubmitProcurementWorksCloseoutActionRequest>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementWorksCloseoutAuthorizationException(
                "Forbidden."));
        service.Setup(item => item.DecideAsync(
                Guid.Empty,
                It.IsAny<DecideProcurementWorksCloseoutActionRequest>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementWorksCloseoutConflictException(
                "WORKS_CLOSEOUT_BLOCKED", "Blocked."));
        var controller = Controller(service);

        AssertProblem(await controller.GetOverview(Guid.Empty, default),
            404, "CONTRACT_NOT_FOUND");
        AssertProblem(await controller.Submit(
                Guid.Empty, new SubmitProcurementWorksCloseoutActionRequest(),
                default),
            403, "WORKS_CLOSEOUT_ACCESS_FORBIDDEN");
        AssertProblem(await controller.Decide(
                Guid.Empty, new DecideProcurementWorksCloseoutActionRequest(),
                default),
            409, "WORKS_CLOSEOUT_BLOCKED");
    }

    private static ProcurementWorksCloseoutController Controller(
        Mock<IProcurementWorksCloseoutService> service)
    {
        var controller = new ProcurementWorksCloseoutController(service.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
        controller.Request.Headers["X-Correlation-ID"] = "tdc-0409-test";
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
