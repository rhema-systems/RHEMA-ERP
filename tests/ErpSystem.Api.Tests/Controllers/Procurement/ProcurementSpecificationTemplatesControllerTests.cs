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

public sealed class ProcurementSpecificationTemplatesControllerTests
{
    [Fact]
    public void ControllerRequiresAuthenticationAndExposesDedicatedLifecycleContract()
    {
        typeof(ProcurementSpecificationTemplatesController).GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
        var methods = typeof(ProcurementSpecificationTemplatesController).GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.DeclaringType == typeof(ProcurementSpecificationTemplatesController))
            .Select(method => method.Name);
        methods.Should().Contain(["Create", "Update", "Submit", "Publish", "Reject", "Clone", "Retire", "DeleteDraft"]);
    }

    [Fact]
    public async Task AnonymousCallerCannotReadRegister()
    {
        using var factory = CreateFactory(PolicyAuthorizationMode.Challenge);
        using var client = factory.CreateClient();

        (await client.GetAsync("/api/procurement/specification-templates/summary")).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AuthorizedUserCanTraverseQueriesAndLifecycleMutations()
    {
        var id = Guid.NewGuid();
        var service = new Mock<IProcurementSpecificationTemplateService>();
        var dto = new ProcurementSpecificationTemplateDto { Id = id, TemplateCode = "GOODS-STD", RowVersion = "AQ==" };
        service.Setup(item => item.GetSummaryAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new ProcurementSpecificationTemplateSummaryDto());
        service.Setup(item => item.GetWorkflowOptionsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        service.Setup(item => item.SearchAsync(It.IsAny<ProcurementSpecificationTemplateSearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementSpecificationTemplatePageDto());
        service.Setup(item => item.GetAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(dto);
        service.Setup(item => item.ValidateAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementSpecificationTemplateValidationDto { TemplateId = id, IsValid = true });
        service.Setup(item => item.CreateAsync(It.IsAny<SaveProcurementSpecificationTemplateRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(dto);
        service.Setup(item => item.UpdateAsync(id, It.IsAny<SaveProcurementSpecificationTemplateRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(dto);
        service.Setup(item => item.SubmitAsync(id, It.IsAny<ProcurementSpecificationTemplateLifecycleRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(dto);
        service.Setup(item => item.PublishAsync(id, It.IsAny<ProcurementSpecificationTemplateLifecycleRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(dto);
        service.Setup(item => item.RejectAsync(id, It.IsAny<ProcurementSpecificationTemplateLifecycleRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(dto);
        service.Setup(item => item.CloneAsync(id, It.IsAny<CloneProcurementSpecificationTemplateRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(dto);
        service.Setup(item => item.RetireAsync(id, It.IsAny<ProcurementSpecificationTemplateLifecycleRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(dto);
        service.Setup(item => item.DeleteDraftAsync(id, It.IsAny<ProcurementSpecificationTemplateLifecycleRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        using var factory = CreateFactory(PolicyAuthorizationMode.Success, service);
        using var client = factory.CreateClient();
        var save = Json("{\"templateCode\":\"GOODS-STD\",\"name\":\"Goods\",\"kind\":0,\"effectiveFromUtc\":\"2026-07-21T00:00:00Z\"}");
        var lifecycle = Json("{\"rowVersion\":\"AQ==\",\"comment\":\"Reviewed\"}");

        var responses = new[]
        {
            await client.GetAsync("/api/procurement/specification-templates/summary"),
            await client.GetAsync("/api/procurement/specification-templates/workflow-options"),
            await client.GetAsync("/api/procurement/specification-templates"),
            await client.GetAsync($"/api/procurement/specification-templates/{id}"),
            await client.GetAsync($"/api/procurement/specification-templates/{id}/validation"),
            await client.PostAsync("/api/procurement/specification-templates", save),
            await client.PutAsync($"/api/procurement/specification-templates/{id}", Json("{\"templateCode\":\"GOODS-STD\",\"name\":\"Goods\",\"kind\":0,\"effectiveFromUtc\":\"2026-07-21T00:00:00Z\",\"rowVersion\":\"AQ==\"}")),
            await client.PostAsync($"/api/procurement/specification-templates/{id}/submit", lifecycle),
            await client.PostAsync($"/api/procurement/specification-templates/{id}/publish", Json("{\"rowVersion\":\"AQ==\"}")),
            await client.PostAsync($"/api/procurement/specification-templates/{id}/reject", Json("{\"rowVersion\":\"AQ==\",\"comment\":\"Revise\"}")),
            await client.PostAsync($"/api/procurement/specification-templates/{id}/clone", Json("{\"rowVersion\":\"AQ==\",\"effectiveFromUtc\":\"2026-08-01T00:00:00Z\",\"changeSummary\":\"Revision\"}")),
            await client.PostAsync($"/api/procurement/specification-templates/{id}/retire", Json("{\"rowVersion\":\"AQ==\",\"comment\":\"Retire\"}")),
            await client.PostAsync($"/api/procurement/specification-templates/{id}/delete-draft", Json("{\"rowVersion\":\"AQ==\"}"))
        };

        responses.Should().OnlyContain(response => response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task DomainFailuresMapToStructured403404409And422Problems()
    {
        var service = new Mock<IProcurementSpecificationTemplateService>();
        service.Setup(item => item.GetAsync(Guid.Empty, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementSpecificationTemplateNotFoundException("Missing."));
        service.Setup(item => item.GetSummaryAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementSpecificationTemplateAuthorizationException("Forbidden."));
        service.Setup(item => item.GetWorkflowOptionsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementSpecificationTemplateConflictException("Conflict."));
        service.Setup(item => item.ValidateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementSpecificationTemplateValidationException("SECTION_REQUIRED", "Invalid."));
        var controller = Controller(service, "trace-spec");

        ((ObjectResult)await controller.Get(Guid.Empty, default)).StatusCode.Should().Be(404);
        ((ObjectResult)await controller.GetSummary(default)).StatusCode.Should().Be(403);
        ((ObjectResult)await controller.GetWorkflowOptions(default)).StatusCode.Should().Be(409);
        var invalid = (ObjectResult)await controller.Validate(Guid.NewGuid(), default);
        invalid.StatusCode.Should().Be(422);
        invalid.Value.Should().BeAssignableTo<ValidationProblemDetails>().Which.Extensions["code"].Should().Be("SECTION_REQUIRED");
    }

    private static StringContent Json(string value) => new(value, Encoding.UTF8, "application/json");

    private static ProcurementSpecificationTemplatesController Controller(Mock<IProcurementSpecificationTemplateService> service,
        string traceIdentifier) => new(service.Object)
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { TraceIdentifier = traceIdentifier } }
    };

    private static WebApplicationFactory<Program> CreateFactory(PolicyAuthorizationMode mode,
        Mock<IProcurementSpecificationTemplateService>? service = null)
    {
        service ??= new Mock<IProcurementSpecificationTemplateService>();
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<IPolicyEvaluator>();
                services.RemoveAll<IProcurementSpecificationTemplateService>();
                services.AddSingleton<IPolicyEvaluator>(new PolicyTestEvaluator(mode));
                services.AddSingleton(service.Object);
            });
        });
    }
}
