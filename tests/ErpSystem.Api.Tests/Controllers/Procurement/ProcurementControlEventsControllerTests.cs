using System.Net;
using System.Reflection;
using System.Text;
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

public sealed class ProcurementControlEventsControllerTests
{
    [Fact]
    public void EveryEndpointIsReadOnlyAndRequiresAdministratorOrInternalAudit()
    {
        var authorization = typeof(ProcurementControlEventsController).GetCustomAttribute<AuthorizeAttribute>();
        authorization.Should().NotBeNull();
        authorization!.Roles.Should().Be("SuperAdmin,TenantAdmin,TDC_INTERNAL_AUDIT");
        typeof(ProcurementControlEventsController).GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.DeclaringType == typeof(ProcurementControlEventsController))
            .Should().NotContain(method => method.Name.Contains("Create", StringComparison.OrdinalIgnoreCase) ||
                                           method.Name.Contains("Update", StringComparison.OrdinalIgnoreCase) ||
                                           method.Name.Contains("Delete", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AnonymousCallerCannotReadSummary()
    {
        using var factory = CreateFactory(PolicyAuthorizationMode.Challenge);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/procurement/control-events/summary");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AuthorizedReaderCanSearchInspectCorrelateAndVerify()
    {
        var id = Guid.NewGuid();
        var service = new Mock<IProcurementControlEventService>();
        service.Setup(item => item.GetSummaryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementControlEventSummaryDto { TotalCount = 1 });
        service.Setup(item => item.SearchAsync(It.IsAny<ProcurementControlEventSearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementControlEventPageDto { TotalCount = 1 });
        service.Setup(item => item.GetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementControlEventDto { Id = id, EventKey = "event-1" });
        service.Setup(item => item.GetCorrelationAsync("trace-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new ProcurementControlEventDto { Id = id, CorrelationId = "trace-1" } });
        service.Setup(item => item.VerifyIntegrityAsync(1000, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementControlEventIntegrityDto { CheckedCount = 1, ValidCount = 1, IsValid = true });
        using var factory = CreateFactory(PolicyAuthorizationMode.Success, service);
        using var client = factory.CreateClient();

        var summary = await client.GetAsync("/api/procurement/control-events/summary");
        var search = await client.GetAsync("/api/procurement/control-events?page=1&pageSize=25");
        var detail = await client.GetAsync($"/api/procurement/control-events/{id}");
        var correlation = await client.GetAsync("/api/procurement/control-events/correlations/trace-1");
        var integrity = await client.PostAsync("/api/procurement/control-events/integrity/verify?take=1000", null);

        new[] { summary, search, detail, correlation, integrity }
            .Should().OnlyContain(response => response.StatusCode == HttpStatusCode.OK);
    }

    [Fact]
    public async Task GenericClientAppendRouteDoesNotExist()
    {
        using var factory = CreateFactory(PolicyAuthorizationMode.Success);
        using var client = factory.CreateClient();

        var response = await client.PostAsync("/api/procurement/control-events",
            new StringContent("{}", Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task MissingEventMapsToStructuredNotFound()
    {
        var service = new Mock<IProcurementControlEventService>();
        service.Setup(item => item.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementControlEventNotFoundException("Missing event."));
        var controller = Controller(service, "trace-not-found");

        var result = await controller.Get(Guid.NewGuid(), default);

        var response = result.Should().BeAssignableTo<ObjectResult>().Subject;
        response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        var problem = response.Value.Should().BeAssignableTo<ProblemDetails>().Subject;
        problem.Extensions["correlationId"].Should().Be("trace-not-found");
    }

    [Fact]
    public async Task InvalidCorrelationMapsToStructuredUnprocessableEntity()
    {
        var service = new Mock<IProcurementControlEventService>();
        service.Setup(item => item.GetCorrelationAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementControlEventValidationException("CORRELATION_REQUIRED", "CorrelationId is required."));
        var controller = Controller(service, "trace-validation");

        var result = await controller.GetCorrelation("missing", default);

        var response = result.Should().BeAssignableTo<ObjectResult>().Subject;
        response.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
        var problem = response.Value.Should().BeAssignableTo<ProblemDetails>().Subject;
        problem.Extensions["code"].Should().Be("CORRELATION_REQUIRED");
        problem.Extensions["correlationId"].Should().Be("trace-validation");
    }

    private static ProcurementControlEventsController Controller(
        Mock<IProcurementControlEventService> service,
        string traceIdentifier) => new(service.Object)
    {
        ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { TraceIdentifier = traceIdentifier }
        }
    };

    private static WebApplicationFactory<Program> CreateFactory(
        PolicyAuthorizationMode mode,
        Mock<IProcurementControlEventService>? service = null)
    {
        service ??= new Mock<IProcurementControlEventService>();
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<IPolicyEvaluator>();
                services.RemoveAll<IProcurementControlEventService>();
                services.AddSingleton<IPolicyEvaluator>(new PolicyTestEvaluator(mode));
                services.AddSingleton(service.Object);
            });
        });
    }
}
