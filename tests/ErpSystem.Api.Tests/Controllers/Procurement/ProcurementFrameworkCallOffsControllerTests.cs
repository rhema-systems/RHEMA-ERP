using System.Reflection;
using ErpSystem.Api.Controllers.Procurement;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Procurement;

public sealed class ProcurementFrameworkCallOffsControllerTests
{
    [Fact]
    public void ControllerRequiresInternalAccessAndExposesDedicatedLifecycle()
    {
        var type = typeof(ProcurementFrameworkCallOffsController);

        type.GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should()
            .Be("InternalOnly");
        type.GetCustomAttribute<RouteAttribute>()!.Template.Should()
            .Be("api/procurement/framework-call-offs");
        type.GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.DeclaringType == type)
            .Select(method => method.Name)
            .Should().Contain([
                "Summary",
                "Search",
                "Get",
                "Options",
                "Create",
                "Submit",
                "Decide",
                "Issue",
                "Cancel",
                "ProcessExpiryAlerts"
            ]);
    }

    [Fact]
    public async Task DomainFailuresMapToStructured403404409And422Problems()
    {
        var service = new Mock<IProcurementFrameworkCallOffService>();
        service.Setup(item => item.GetAsync(Guid.Empty, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementFrameworkCallOffNotFoundException(
                "FRAMEWORK_CALL_OFF_NOT_FOUND", "Missing."));
        service.Setup(item => item.GetSummaryAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementFrameworkCallOffAuthorizationException(
                "Forbidden."));
        service.Setup(item => item.SubmitAsync(
                Guid.Empty,
                It.IsAny<ProcurementFrameworkCallOffLifecycleRequest>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementFrameworkCallOffConflictException(
                "FRAMEWORK_CALL_OFF_STALE", "Conflict."));
        service.Setup(item => item.CreateAsync(
                It.IsAny<CreateProcurementFrameworkCallOffRequest>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementFrameworkCallOffValidationException(
                "FRAMEWORK_CALL_OFF_LINES_REQUIRED", "Invalid."));
        var controller = Controller(service);

        AssertProblem(await controller.Get(Guid.Empty, default), 404,
            "FRAMEWORK_CALL_OFF_NOT_FOUND");
        AssertProblem(await controller.Summary(default), 403,
            "FRAMEWORK_CALL_OFF_ACCESS_FORBIDDEN");
        AssertProblem(await controller.Submit(
            Guid.Empty, new ProcurementFrameworkCallOffLifecycleRequest(), default),
            409, "FRAMEWORK_CALL_OFF_STALE");
        AssertProblem(await controller.Create(
            new CreateProcurementFrameworkCallOffRequest(), default),
            422, "FRAMEWORK_CALL_OFF_LINES_REQUIRED");
    }

    [Fact]
    public async Task DecisionUsesOneSharedEndpointAndServerCorrelation()
    {
        var id = Guid.NewGuid();
        ProcurementFrameworkCallOffDecisionRequest? captured = null;
        var service = new Mock<IProcurementFrameworkCallOffService>();
        service.Setup(item => item.DecideAsync(
                id,
                It.IsAny<ProcurementFrameworkCallOffDecisionRequest>(),
                "corr-0402",
                It.IsAny<CancellationToken>()))
            .Callback<Guid, ProcurementFrameworkCallOffDecisionRequest, string,
                CancellationToken>((_, request, _, _) => captured = request)
            .ReturnsAsync(new ProcurementFrameworkCallOffDto
            {
                Id = id,
                Status = ProcurementFrameworkCallOffStatus.Rejected
            });
        var controller = Controller(service);

        var result = await controller.Decide(
            id,
            new ProcurementFrameworkCallOffDecisionRequest
            {
                RowVersion = "row-version",
                Approved = false,
                Comment = "Workflow rejected.",
                Evidence =
                [
                    new ProcurementControlEventEvidenceReference
                    {
                        ReferenceKind =
                            ProcurementControlEvidenceReferenceKind.ExternalReference,
                        Reference = "MINUTE-0402"
                    }
                ]
            },
            default);

        result.Should().BeOfType<OkObjectResult>();
        captured.Should().NotBeNull();
        captured!.Approved.Should().BeFalse();
        captured.Evidence.Should().ContainSingle();
        service.VerifyAll();
    }

    private static void AssertProblem(
        IActionResult result,
        int expectedStatus,
        string expectedCode)
    {
        var problem = result.Should().BeAssignableTo<ObjectResult>().Subject;
        problem.StatusCode.Should().Be(expectedStatus);
        var details = problem.Value.Should().BeAssignableTo<ProblemDetails>().Subject;
        details.Extensions["code"].Should().Be(expectedCode);
        details.Extensions["correlationId"].Should().Be("corr-0402");
    }

    private static ProcurementFrameworkCallOffsController Controller(
        Mock<IProcurementFrameworkCallOffService> service)
    {
        var controller = new ProcurementFrameworkCallOffsController(service.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    TraceIdentifier = "trace-0402"
                }
            }
        };
        controller.Request.Headers["X-Correlation-ID"] = "corr-0402";
        return controller;
    }
}
