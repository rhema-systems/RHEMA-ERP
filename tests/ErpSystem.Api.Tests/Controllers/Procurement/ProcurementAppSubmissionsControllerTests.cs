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

public sealed class ProcurementAppSubmissionsControllerTests
{
    [Fact]
    public void ControllerRequiresAuthenticationAndExposesNoDeleteEndpoint()
    {
        typeof(ProcurementAppSubmissionsController).GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
        typeof(ProcurementAppSubmissionsController).GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.DeclaringType == typeof(ProcurementAppSubmissionsController))
            .Should().NotContain(method => method.Name.Contains("Delete", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AnonymousCallerCannotReadSummary()
    {
        using var factory = CreateFactory(PolicyAuthorizationMode.Challenge);
        using var client = factory.CreateClient();

        (await client.GetAsync("/api/procurement/app-submissions/summary")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AuthorizedReaderCanUseTenantSafeRegisterQueries()
    {
        var id = Guid.NewGuid();
        var service = new Mock<IProcurementAppSubmissionService>();
        service.Setup(item => item.GetSummaryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementAppSubmissionSummaryDto { TotalAttemptCount = 1 });
        service.Setup(item => item.GetPublishedPlanOptionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ProcurementAppSubmissionPlanOptionDto { Id = Guid.NewGuid() }]);
        service.Setup(item => item.SearchAsync(It.IsAny<ProcurementAppSubmissionSearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementAppSubmissionPageDto { TotalCount = 1 });
        service.Setup(item => item.GetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementAppSubmissionDto { Id = id, SubmissionNumber = "APP-2026-00001" });
        using var factory = CreateFactory(PolicyAuthorizationMode.Success, service);
        using var client = factory.CreateClient();

        var responses = new[]
        {
            await client.GetAsync("/api/procurement/app-submissions/summary"),
            await client.GetAsync("/api/procurement/app-submissions/published-plans"),
            await client.GetAsync("/api/procurement/app-submissions?page=1&pageSize=25"),
            await client.GetAsync($"/api/procurement/app-submissions/{id}")
        };

        responses.Should().OnlyContain(response => response.StatusCode == HttpStatusCode.OK);
    }

    [Fact]
    public async Task AuthorizedManagerCanTraverseAllMutationContracts()
    {
        var id = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var service = new Mock<IProcurementAppSubmissionService>();
        var dto = new ProcurementAppSubmissionDto { Id = id, ProcurementPlanId = planId, SubmissionNumber = "APP-2026-00001" };
        service.Setup(item => item.RecordExportAsync(It.IsAny<RecordProcurementAppExportRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(dto);
        service.Setup(item => item.SubmitAsync(id, It.IsAny<SubmitProcurementAppRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(dto);
        service.Setup(item => item.AcknowledgeAsync(id, It.IsAny<AcknowledgeProcurementAppRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(dto);
        service.Setup(item => item.RejectAsync(id, It.IsAny<RejectProcurementAppRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(dto);
        service.Setup(item => item.ResubmitAsync(id, It.IsAny<ResubmitProcurementAppRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(dto);
        using var factory = CreateFactory(PolicyAuthorizationMode.Success, service);
        using var client = factory.CreateClient();
        const string checksum = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

        var responses = new[]
        {
            await client.PostAsync("/api/procurement/app-submissions/exports", Json($"{{\"procurementPlanId\":\"{planId}\",\"exportFileName\":\"app.xlsx\",\"exportFormat\":\"XLSX\",\"exportTemplateVersion\":\"v1\",\"exportChecksumSha256\":\"{checksum}\"}}")),
            await client.PostAsync($"/api/procurement/app-submissions/{id}/submit", Json("{\"externalSubmissionReference\":\"GH-001\",\"submittedAtUtc\":\"2026-07-21T00:00:00Z\",\"rowVersion\":\"AQ==\"}")),
            await client.PostAsync($"/api/procurement/app-submissions/{id}/acknowledge", Json("{\"acknowledgementReference\":\"ACK-001\",\"acknowledgedAtUtc\":\"2026-07-21T00:00:00Z\",\"rowVersion\":\"AQ==\"}")),
            await client.PostAsync($"/api/procurement/app-submissions/{id}/reject", Json("{\"rejectionReference\":\"REJ-001\",\"rejectionReason\":\"Invalid template\",\"rejectedAtUtc\":\"2026-07-21T00:00:00Z\",\"rowVersion\":\"AQ==\"}")),
            await client.PostAsync($"/api/procurement/app-submissions/{id}/resubmit", Json($"{{\"exportFileName\":\"app-r2.xlsx\",\"exportFormat\":\"XLSX\",\"exportTemplateVersion\":\"v2\",\"exportChecksumSha256\":\"{checksum}\",\"rowVersion\":\"AQ==\"}}"))
        };

        responses[0].StatusCode.Should().Be(HttpStatusCode.Created);
        responses.Skip(1).Should().OnlyContain(response => response.StatusCode == HttpStatusCode.OK);
    }

    [Fact]
    public async Task DomainFailuresMapToStructured403404409And422Problems()
    {
        var service = new Mock<IProcurementAppSubmissionService>();
        service.Setup(item => item.GetAsync(It.Is<Guid>(id => id == Guid.Empty), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementAppSubmissionNotFoundException("Missing."));
        service.Setup(item => item.GetAsync(It.Is<Guid>(id => id != Guid.Empty), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementAppSubmissionAuthorizationException("Forbidden."));
        service.Setup(item => item.GetSummaryAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementAppSubmissionConflictException("Conflict."));
        service.Setup(item => item.GetPublishedPlanOptionsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementAppSubmissionValidationException("PLAN_INVALID", "Invalid plan."));
        var controller = Controller(service, "trace-app");

        var missing = (await controller.Get(Guid.Empty, default)).Should().BeAssignableTo<ObjectResult>().Subject;
        var forbidden = (await controller.Get(Guid.NewGuid(), default)).Should().BeAssignableTo<ObjectResult>().Subject;
        var conflict = (await controller.GetSummary(default)).Should().BeAssignableTo<ObjectResult>().Subject;
        var invalid = (await controller.GetPublishedPlans(default)).Should().BeAssignableTo<ObjectResult>().Subject;

        missing.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        forbidden.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        conflict.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        invalid.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
        invalid.Value.Should().BeAssignableTo<ValidationProblemDetails>().Which.Extensions["code"].Should().Be("PLAN_INVALID");
        invalid.Value.Should().BeAssignableTo<ValidationProblemDetails>().Which.Extensions["correlationId"].Should().Be("trace-app");
    }

    private static StringContent Json(string value) => new(value, Encoding.UTF8, "application/json");

    private static ProcurementAppSubmissionsController Controller(Mock<IProcurementAppSubmissionService> service, string traceIdentifier) =>
        new(service.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { TraceIdentifier = traceIdentifier } }
        };

    private static WebApplicationFactory<Program> CreateFactory(
        PolicyAuthorizationMode mode,
        Mock<IProcurementAppSubmissionService>? service = null)
    {
        service ??= new Mock<IProcurementAppSubmissionService>();
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<IPolicyEvaluator>();
                services.RemoveAll<IProcurementAppSubmissionService>();
                services.AddSingleton<IPolicyEvaluator>(new PolicyTestEvaluator(mode));
                services.AddSingleton(service.Object);
            });
        });
    }
}
