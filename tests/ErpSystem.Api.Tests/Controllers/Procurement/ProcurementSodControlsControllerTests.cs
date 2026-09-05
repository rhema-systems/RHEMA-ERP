using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using ErpSystem.Api.Controllers.Procurement;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Procurement;

public sealed class ProcurementSodControlsControllerTests
{
    [Fact]
    public void RuntimeGuardRequiresAuthenticationAndAdministrationEndpointsUsePermissions()
    {
        typeof(ProcurementSodControlsController).GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
        typeof(ProcurementSodControlsController).GetMethod(nameof(ProcurementSodControlsController.GetCoverage))!
            .GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be("procurement.audit.read");
        typeof(ProcurementSodControlsController).GetMethod(nameof(ProcurementSodControlsController.ApplyRequired))!
            .GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be("procurement.access.manage");
        typeof(ProcurementSodControlsController).GetMethod(nameof(ProcurementSodControlsController.GetBlockedAttempts))!
            .GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be("procurement.audit.read");
        typeof(ProcurementSodControlsController).GetMethod(nameof(ProcurementSodControlsController.Enforce))!
            .GetCustomAttribute<AuthorizeAttribute>().Should().BeNull();
    }

    [Fact]
    public async Task AnonymousCallerCannotReachTheRuntimeGuard()
    {
        using var factory = CreateFactory(PolicyAuthorizationMode.Challenge);
        using var client = factory.CreateClient();

        var response = await client.PostAsync("/api/procurement/sod-controls/enforce", JsonContent(Request()));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DirectApiConflictReturnsForbiddenAuditedDecision()
    {
        var service = new Mock<IProcurementSodGuardService>();
        service.Setup(item => item.EnforceAsync(It.IsAny<ProcurementSodGuardRequest>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementSodGuardDecisionDto
            {
                Allowed = false, IsHardStop = true, WasAudited = true, Code = "SOD_CONFLICT",
                ControlCode = ProcurementSodRequiredControlRegistry.Definitions[0].Code,
                CorrelationId = "trace-api"
            });
        using var factory = CreateFactory(PolicyAuthorizationMode.Success, service);
        using var client = factory.CreateClient();

        var response = await client.PostAsync("/api/procurement/sod-controls/enforce", JsonContent(Request()));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        payload.RootElement.GetProperty("allowed").GetBoolean().Should().BeFalse();
        payload.RootElement.GetProperty("wasAudited").GetBoolean().Should().BeTrue();
        payload.RootElement.GetProperty("code").GetString().Should().Be("SOD_CONFLICT");
    }

    [Fact]
    public async Task IndependentRuntimeActorReceivesAllowedDecision()
    {
        var service = new Mock<IProcurementSodGuardService>();
        service.Setup(item => item.EnforceAsync(It.IsAny<ProcurementSodGuardRequest>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementSodGuardDecisionDto { Allowed = true, Code = "SOD_ALLOWED" });
        using var factory = CreateFactory(PolicyAuthorizationMode.Success, service);
        using var client = factory.CreateClient();

        var response = await client.PostAsync("/api/procurement/sod-controls/enforce", JsonContent(Request()));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PublicRuntimeGuardCannotBindInternalIndependentActorOverrides()
    {
        ProcurementSodGuardRequest? boundRequest = null;
        var service = new Mock<IProcurementSodGuardService>();
        service.Setup(item => item.EnforceAsync(
                It.IsAny<ProcurementSodGuardRequest>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<ProcurementSodGuardRequest, string, CancellationToken>(
                (request, _, _) => boundRequest = request)
            .ReturnsAsync(new ProcurementSodGuardDecisionDto
            {
                Allowed = true,
                Code = "SOD_ALLOWED"
            });
        using var factory = CreateFactory(PolicyAuthorizationMode.Success, service);
        using var client = factory.CreateClient();
        var injectedActorId = Guid.NewGuid();
        var payload = new Dictionary<string, object?>
        {
            ["controlCode"] =
                ProcurementSodRequiredControlRegistry.Definitions[0].Code,
            ["sourceType"] = "PurchaseRequisition",
            ["sourceReference"] = "PR-API-INJECTION",
            ["prohibitedActorUserIds"] = new[] { Guid.NewGuid() },
            ["independentActorUserIds"] = new[] { injectedActorId },
            ["requireSoleActorConflict"] = true
        };

        var response = await client.PostAsync(
            "/api/procurement/sod-controls/enforce",
            JsonContent(payload));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        boundRequest.Should().NotBeNull();
        boundRequest!.IndependentActorUserIds.Should().BeEmpty();
        boundRequest.RequireSoleActorConflict.Should().BeFalse();
        service.VerifyAll();
    }

    [Fact]
    public async Task AuthorizedAdministratorCanReadSixControlCoverageAndBlockedAttempts()
    {
        var service = new Mock<IProcurementSodGuardService>();
        service.Setup(item => item.GetCoverageAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementSodCoverageDto
            {
                Status = "Complete", IsComplete = true,
                Controls = ProcurementSodRequiredControlRegistry.Definitions.Select(definition =>
                    new ProcurementSodRequiredControlDto { Code = definition.Code, IsConfigured = true, IsHardStop = true }).ToList()
            });
        service.Setup(item => item.GetBlockedAttemptsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new ProcurementSodBypassAuditDto { ControlCode = "SOD-INITIATOR-APPROVER" } });
        using var factory = CreateFactory(PolicyAuthorizationMode.Success, service);
        using var client = factory.CreateClient();

        var coverage = await client.GetAsync("/api/procurement/sod-controls/coverage");
        var audits = await client.GetAsync("/api/procurement/sod-controls/blocked-attempts");

        coverage.StatusCode.Should().Be(HttpStatusCode.OK);
        audits.StatusCode.Should().Be(HttpStatusCode.OK);
        using var coveragePayload = JsonDocument.Parse(await coverage.Content.ReadAsStringAsync());
        coveragePayload.RootElement.GetProperty("controls").GetArrayLength().Should().Be(6);
    }

    [Fact]
    public async Task ValidationFailureMapsToStructuredUnprocessableEntity()
    {
        var service = new Mock<IProcurementSodGuardService>();
        service.Setup(item => item.CheckAsync(It.IsAny<ProcurementSodGuardRequest>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementSodRequestValidationException("CONTROL_UNKNOWN", "Unknown control."));
        var controller = new ProcurementSodControlsController(service.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { TraceIdentifier = "trace-validation" }
            }
        };

        var result = await controller.Check(Request(), default);

        var objectResult = result.Should().BeAssignableTo<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(422);
        ((ProblemDetails)objectResult.Value!).Extensions["correlationId"].Should().Be("trace-validation");
    }

    private static ProcurementSodGuardRequest Request() => new()
    {
        ControlCode = ProcurementSodRequiredControlRegistry.Definitions[0].Code,
        SourceType = "PurchaseRequisition",
        SourceReference = "PR-API-001",
        ProhibitedActorUserIds = new() { Guid.NewGuid() }
    };

    private static StringContent JsonContent(object value) =>
        new(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json");

    private static WebApplicationFactory<Program> CreateFactory(
        PolicyAuthorizationMode mode,
        Mock<IProcurementSodGuardService>? service = null)
    {
        service ??= new Mock<IProcurementSodGuardService>();
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<IPolicyEvaluator>();
                services.RemoveAll<IProcurementSodGuardService>();
                services.AddSingleton<IPolicyEvaluator>(new PolicyTestEvaluator(mode));
                services.AddSingleton(service.Object);
            });
        });
    }
}
