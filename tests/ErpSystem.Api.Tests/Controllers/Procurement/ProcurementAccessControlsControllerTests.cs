using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using ErpSystem.Api.Controllers.Procurement;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
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

public sealed class ProcurementAccessControlsControllerTests
{
    [Fact]
    public void RuntimeCapabilityRequiresAuthenticationWhileAdministrationUsesAccessPermission()
    {
        typeof(ProcurementAccessControlsController).GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
        typeof(ProcurementAccessControlsController).GetMethod(nameof(ProcurementAccessControlsController.GetReadiness))!
            .GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be("procurement.access.manage");
        typeof(ProcurementAccessControlsController).GetMethod(nameof(ProcurementAccessControlsController.GetAssignments))!
            .GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be("procurement.access.manage");
        typeof(ProcurementAccessControlsController).GetMethod(nameof(ProcurementAccessControlsController.GetAudit))!
            .GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be("procurement.access.manage");
        typeof(ProcurementAccessControlsController).GetMethod(nameof(ProcurementAccessControlsController.EnforceCapability))!
            .GetCustomAttribute<AuthorizeAttribute>().Should().BeNull();
    }

    [Fact]
    public async Task AnonymousCallerCannotReachCapabilityEnforcement()
    {
        using var factory = CreateFactory(PolicyAuthorizationMode.Challenge);
        using var client = factory.CreateClient();

        var response = await client.PostAsync("/api/procurement/access-controls/capabilities/enforce", Json(Request()));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DirectApiDeniedCapabilityReturnsForbiddenDecision()
    {
        var service = new Mock<IProcurementAccessControlService>();
        service.Setup(item => item.EnforceCapabilityAsync(It.IsAny<ProcurementAccessCapabilityRequest>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementAccessCapabilityDecisionDto
            {
                Allowed = false, Code = "ACCESS_WAREHOUSE_DENIED", Message = "Not assigned to this warehouse."
            });
        using var factory = CreateFactory(PolicyAuthorizationMode.Success, service);
        using var client = factory.CreateClient();

        var response = await client.PostAsync("/api/procurement/access-controls/capabilities/enforce", Json(Request()));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("allowed").GetBoolean().Should().BeFalse();
        body.RootElement.GetProperty("code").GetString().Should().Be("ACCESS_WAREHOUSE_DENIED");
    }

    [Fact]
    public async Task AdministratorCanReadReadinessCommitteesAndAudit()
    {
        var service = new Mock<IProcurementAccessControlService>();
        service.Setup(item => item.GetReadinessAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementAccessReadinessDto { RequiredRoleCount = 19, ConfiguredRoleCount = 19, InternalAuditIsReadOnly = true });
        service.Setup(item => item.GetCommitteesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new ProcurementCommitteeDto { Code = "TDC_ETC", RequiredQuorum = 3 } });
        service.Setup(item => item.GetAuditAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new ProcurementAccessAuditDto { Action = "PROCUREMENT_ACCESS_ASSIGNMENT_CREATED" } });
        using var factory = CreateFactory(PolicyAuthorizationMode.Success, service);
        using var client = factory.CreateClient();

        var readiness = await client.GetAsync("/api/procurement/access-controls/readiness");
        var committees = await client.GetAsync("/api/procurement/access-controls/committees");
        var audit = await client.GetAsync("/api/procurement/access-controls/audit");

        readiness.StatusCode.Should().Be(HttpStatusCode.OK);
        committees.StatusCode.Should().Be(HttpStatusCode.OK);
        audit.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ServiceValidationMapsToStructuredUnprocessableEntity()
    {
        var service = new Mock<IProcurementAccessControlService>();
        service.Setup(item => item.CheckCapabilityAsync(It.IsAny<ProcurementAccessCapabilityRequest>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementAccessValidationException("WAREHOUSE_REQUIRED", "Warehouse is required."));
        var controller = new ProcurementAccessControlsController(service.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { TraceIdentifier = "trace-validation" } }
        };

        var result = await controller.CheckCapability(Request(), default);

        var response = result.Should().BeAssignableTo<ObjectResult>().Subject;
        response.StatusCode.Should().Be(422);
        ((ProblemDetails)response.Value!).Extensions["code"].Should().Be("WAREHOUSE_REQUIRED");
        ((ProblemDetails)response.Value!).Extensions["correlationId"].Should().Be("trace-validation");
    }

    [Fact]
    public async Task StaleAssignmentUpdateMapsToStructuredConflict()
    {
        var service = new Mock<IProcurementAccessControlService>();
        service.Setup(item => item.SaveAssignmentAsync(It.IsAny<Guid>(),
                It.IsAny<SaveProcurementResponsibilityAssignmentRequest>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementAccessConflictException("The record changed after it was loaded. Refresh and retry."));
        var controller = new ProcurementAccessControlsController(service.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { TraceIdentifier = "trace-stale" } }
        };

        var result = await controller.UpdateAssignment(Guid.NewGuid(), new SaveProcurementResponsibilityAssignmentRequest
        {
            UserId = Guid.NewGuid(),
            RoleName = "TDC_STORES_OFFICER",
            Reason = "Stale update acceptance check",
            RowVersion = Convert.ToBase64String(new byte[] { 9, 9, 9, 9 })
        }, default);

        var response = result.Should().BeAssignableTo<ObjectResult>().Subject;
        response.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        var problem = response.Value.Should().BeAssignableTo<ProblemDetails>().Subject;
        problem.Detail.Should().Contain("Refresh and retry");
        problem.Extensions["correlationId"].Should().Be("trace-stale");
    }

    private static ProcurementAccessCapabilityRequest Request() => new()
    {
        PermissionCode = "procurement.inventory.read", WarehouseId = Guid.NewGuid(),
        SourceType = "Warehouse", SourceReference = "WH-API-001"
    };

    private static StringContent Json(object value) =>
        new(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json");

    private static WebApplicationFactory<Program> CreateFactory(
        PolicyAuthorizationMode mode,
        Mock<IProcurementAccessControlService>? service = null)
    {
        service ??= new Mock<IProcurementAccessControlService>();
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<IPolicyEvaluator>();
                services.RemoveAll<IProcurementAccessControlService>();
                services.AddSingleton<IPolicyEvaluator>(new PolicyTestEvaluator(mode));
                services.AddSingleton(service.Object);
            });
        });
    }
}
