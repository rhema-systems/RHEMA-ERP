using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using ErpSystem.Api.Controllers.Procurement;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Enums;
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

public sealed class ProcurementSourcingCasesControllerTests
{
    [Fact]
    public void ControllerRequiresAuthenticationAndExposesDedicatedCaseLifecycleRoutes()
    {
        typeof(ProcurementSourcingCasesController).GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
        var route = typeof(ProcurementSourcingCasesController).GetCustomAttribute<RouteAttribute>();
        route.Should().NotBeNull();
        route!.Template.Should().Be("api/procurement/sourcing-cases");
        var methods = typeof(ProcurementSourcingCasesController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.DeclaringType == typeof(ProcurementSourcingCasesController))
            .Select(method => method.Name);
        methods.Should().Contain([
            nameof(ProcurementSourcingCasesController.GetSummary),
            nameof(ProcurementSourcingCasesController.Search),
            nameof(ProcurementSourcingCasesController.GetSourceOptions),
            nameof(ProcurementSourcingCasesController.GetReadiness),
            nameof(ProcurementSourcingCasesController.Get),
            nameof(ProcurementSourcingCasesController.Create),
            nameof(ProcurementSourcingCasesController.Close),
            nameof(ProcurementSourcingCasesController.Cancel)
        ]);
    }

    [Fact]
    public async Task AnonymousCallerCannotReadOrMutateAnySourcingCaseRoute()
    {
        using var factory = CreateFactory(PolicyAuthorizationMode.Challenge);
        using var client = factory.CreateClient();
        var caseId = Guid.NewGuid();
        var requisitionId = Guid.NewGuid();

        var responses = new[]
        {
            await client.GetAsync("/api/procurement/sourcing-cases/summary"),
            await client.GetAsync("/api/procurement/sourcing-cases"),
            await client.GetAsync("/api/procurement/sourcing-cases/source-options"),
            await client.GetAsync($"/api/procurement/sourcing-cases/readiness/{requisitionId}"),
            await client.GetAsync($"/api/procurement/sourcing-cases/{caseId}"),
            await client.PostAsJsonAsync("/api/procurement/sourcing-cases", new { requisitionId }),
            await client.PostAsJsonAsync($"/api/procurement/sourcing-cases/{caseId}/close", new { rowVersion = "AQ==", reason = "Close case" }),
            await client.PostAsJsonAsync($"/api/procurement/sourcing-cases/{caseId}/cancel", new { rowVersion = "AQ==", reason = "Cancel case" })
        };

        responses.Should().OnlyContain(response => response.StatusCode == HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AuthorizedReadCreateAndLifecycleUseStableCorrelationAndDtos()
    {
        var service = new Mock<IProcurementSourcingCaseService>();
        var caseId = Guid.NewGuid();
        var requisitionId = Guid.NewGuid();
        var dto = new ProcurementSourcingCaseDto
        {
            Id = caseId,
            CaseNumber = "SC-PR-API-001-A1",
            RequisitionId = requisitionId,
            RowVersion = "AQ=="
        };
        service.Setup(item => item.GetSummaryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementSourcingCaseSummaryDto { TotalCount = 1 });
        service.Setup(item => item.SearchAsync(It.IsAny<ProcurementSourcingCaseSearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementSourcingCasePageDto { TotalCount = 1, Items = [dto] });
        service.Setup(item => item.GetSourceOptionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        service.Setup(item => item.GetReadinessAsync(requisitionId, ProcurementMethodType.RequestForQuotation, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementSourcingCaseReadinessDto { RequisitionId = requisitionId, CanCreate = true });
        service.Setup(item => item.GetAsync(caseId, It.IsAny<CancellationToken>())).ReturnsAsync(dto);
        service.Setup(item => item.CreateAsync(It.IsAny<CreateProcurementSourcingCaseRequest>(), "trace-case-api", It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);
        service.Setup(item => item.CloseAsync(caseId, It.IsAny<ProcurementSourcingCaseActionRequest>(), "trace-case-api", It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);
        service.Setup(item => item.CancelAsync(caseId, It.IsAny<ProcurementSourcingCaseActionRequest>(), "trace-case-api", It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);
        var controller = Controller(service);

        (await controller.GetSummary(default)).Should().BeOfType<OkObjectResult>();
        (await controller.Search(new ProcurementSourcingCaseSearchRequest(), default)).Should().BeOfType<OkObjectResult>();
        (await controller.GetSourceOptions(default)).Should().BeOfType<OkObjectResult>();
        (await controller.GetReadiness(requisitionId, ProcurementMethodType.RequestForQuotation, null, default)).Should().BeOfType<OkObjectResult>();
        (await controller.Get(caseId, default)).Should().BeOfType<OkObjectResult>();
        var created = await controller.Create(new CreateProcurementSourcingCaseRequest
        {
            RequisitionId = requisitionId,
            SelectedMethod = ProcurementMethodType.RequestForQuotation,
            Justification = "Approved method justification."
        }, default);
        created.Should().BeOfType<CreatedAtActionResult>();
        (await controller.Close(caseId, new ProcurementSourcingCaseActionRequest { RowVersion = "AQ==", Reason = "Source completed." }, default))
            .Should().BeOfType<OkObjectResult>();
        (await controller.Cancel(caseId, new ProcurementSourcingCaseActionRequest { RowVersion = "AQ==", Reason = "Case cancelled." }, default))
            .Should().BeOfType<OkObjectResult>();
        service.Verify(item => item.CreateAsync(It.IsAny<CreateProcurementSourcingCaseRequest>(), "trace-case-api", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TenantAuthorizationNotFoundConflictAndValidationMapStructuredStatusCodes()
    {
        var service = new Mock<IProcurementSourcingCaseService>();
        var caseId = Guid.NewGuid();
        service.Setup(item => item.GetAsync(caseId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementSourcingCaseNotFoundException("SOURCING_CASE_NOT_FOUND", "Missing."));
        service.Setup(item => item.GetSummaryAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementSourcingCaseAuthorizationException("Forbidden."));
        service.Setup(item => item.CloseAsync(caseId, It.IsAny<ProcurementSourcingCaseActionRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementSourcingCaseConflictException("SOURCING_CASE_CONCURRENCY", "Conflict."));
        service.Setup(item => item.CreateAsync(It.IsAny<CreateProcurementSourcingCaseRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementSourcingCaseValidationException("SOURCING_CASE_METHOD_BLOCKED", "Invalid."));
        var controller = Controller(service);

        var missing = (ObjectResult)await controller.Get(caseId, default);
        var forbidden = (ObjectResult)await controller.GetSummary(default);
        var conflict = (ObjectResult)await controller.Close(caseId,
            new ProcurementSourcingCaseActionRequest { RowVersion = "AQ==", Reason = "Try close." }, default);
        var invalid = (ObjectResult)await controller.Create(new CreateProcurementSourcingCaseRequest
        {
            RequisitionId = Guid.NewGuid(), Justification = "Invalid method."
        }, default);

        missing.StatusCode.Should().Be(404);
        missing.Value.Should().BeAssignableTo<ProblemDetails>().Which.Extensions["code"].Should().Be("SOURCING_CASE_NOT_FOUND");
        forbidden.StatusCode.Should().Be(403);
        forbidden.Value.Should().BeAssignableTo<ProblemDetails>().Which.Extensions["code"].Should().Be("SOURCING_CASE_FORBIDDEN");
        conflict.StatusCode.Should().Be(409);
        conflict.Value.Should().BeAssignableTo<ProblemDetails>().Which.Extensions["code"].Should().Be("SOURCING_CASE_CONCURRENCY");
        invalid.StatusCode.Should().Be(422);
        invalid.Value.Should().BeAssignableTo<ValidationProblemDetails>().Which.Extensions["code"].Should().Be("SOURCING_CASE_METHOD_BLOCKED");
    }

    private static ProcurementSourcingCasesController Controller(Mock<IProcurementSourcingCaseService> service)
    {
        var context = new DefaultHttpContext { TraceIdentifier = "trace-case-api" };
        context.Request.Headers["X-Correlation-ID"] = "trace-case-api";
        return new ProcurementSourcingCasesController(service.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = context }
        };
    }

    private static WebApplicationFactory<Program> CreateFactory(PolicyAuthorizationMode mode) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<IPolicyEvaluator>();
                services.AddSingleton<IPolicyEvaluator>(new PolicyTestEvaluator(mode));
            });
        });
}
