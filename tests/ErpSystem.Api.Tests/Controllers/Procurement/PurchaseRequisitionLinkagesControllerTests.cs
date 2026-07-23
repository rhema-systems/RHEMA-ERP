using System.Net;
using System.Reflection;
using ErpSystem.Api.Controllers.Procurement;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Events;
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
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Procurement;

public sealed class PurchaseRequisitionLinkagesControllerTests
{
    [Fact]
    public void ControllerRequiresAuthenticationAndExposesLinkageUpdateOptionsHistoryAndExport()
    {
        typeof(PurchaseRequisitionsController).GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
        var methods = typeof(PurchaseRequisitionsController).GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.DeclaringType == typeof(PurchaseRequisitionsController))
            .Select(method => method.Name);

        methods.Should().Contain([
            nameof(PurchaseRequisitionsController.UpdatePurchaseRequisition),
            nameof(PurchaseRequisitionsController.GetLinkageOptions),
            nameof(PurchaseRequisitionsController.GetLinkageHistory),
            nameof(PurchaseRequisitionsController.ExportPurchaseRequisition)
        ]);
    }

    [Fact]
    public async Task AnonymousCallerCannotReadLinkageOptionsOrAuditHistoryOrExport()
    {
        using var factory = CreateFactory(PolicyAuthorizationMode.Challenge);
        using var client = factory.CreateClient();
        var id = Guid.NewGuid();

        var responses = new[]
        {
            await client.GetAsync("/api/PurchaseRequisitions/linkage-options"),
            await client.GetAsync($"/api/PurchaseRequisitions/{id}/linkage-history"),
            await client.GetAsync($"/api/PurchaseRequisitions/{id}/export")
        };

        responses.Should().OnlyContain(response => response.StatusCode == HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DomainFailuresMapToStructured403And404Problems()
    {
        var service = new Mock<IProcurementRequisitionLinkageService>();
        service.Setup(item => item.GetOptionsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementRequisitionLinkageAuthorizationException("Forbidden."));
        service.Setup(item => item.GetHistoryAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementRequisitionLinkageNotFoundException("PR_NOT_FOUND", "Missing."));
        var controller = Controller(service);

        var forbidden = (ObjectResult)(await controller.GetLinkageOptions(default)).Result!;
        var missing = (ObjectResult)(await controller.GetLinkageHistory(Guid.NewGuid(), default)).Result!;

        forbidden.StatusCode.Should().Be(403);
        forbidden.Value.Should().BeAssignableTo<ProblemDetails>().Which.Extensions["code"].Should().Be("PR_LINKAGE_FORBIDDEN");
        missing.StatusCode.Should().Be(404);
        missing.Value.Should().BeAssignableTo<ProblemDetails>().Which.Extensions["code"].Should().Be("PR_NOT_FOUND");
    }

    private static PurchaseRequisitionsController Controller(Mock<IProcurementRequisitionLinkageService> linkage) => new(
        Mock.Of<IPurchaseRequisitionRepository>(),
        Mock.Of<IPurchaseRequisitionItemRepository>(),
        Mock.Of<IRfqService>(),
        Mock.Of<ITenantContext>(),
        Mock.Of<IUnitOfWork>(),
        Mock.Of<ICurrentUserProvider>(),
        Mock.Of<IWorkflowIntegrationService>(),
        Mock.Of<IWorkflowStatusAdapterRegistry>(),
        Mock.Of<IWorkflowService>(),
        linkage.Object,
        Mock.Of<IProcurementRequisitionSubmissionControlService>(),
        Mock.Of<IProcurementRequisitionBudgetControlService>(),
        Mock.Of<IProcurementRequisitionAuthorityRouteService>(),
        Mock.Of<IProcurementRequisitionSourcingReleaseService>(),
        Mock.Of<IAppEventBus>(),
        NullLogger<PurchaseRequisitionsController>.Instance)
    {
        ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { TraceIdentifier = "trace-pr-linkage" }
        }
    };

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
