using System.Net;
using System.Reflection;
using System.Text;
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

public sealed class ProcurementMasterDataChangesControllerTests
{
    [Fact]
    public void ControllerRequiresAuthenticationAndPolicyLifecycleHasExplicitAdminAuthorization()
    {
        typeof(ProcurementMasterDataChangesController).GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
        var methods = typeof(ProcurementMasterDataChangesController).GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.DeclaringType == typeof(ProcurementMasterDataChangesController)).ToDictionary(method => method.Name);

        methods[nameof(ProcurementMasterDataChangesController.CreatePolicy)].GetCustomAttribute<AuthorizeAttribute>()!.Roles.Should().Be("SuperAdmin,TenantAdmin");
        methods[nameof(ProcurementMasterDataChangesController.UpdatePolicy)].GetCustomAttribute<AuthorizeAttribute>()!.Roles.Should().Be("SuperAdmin,TenantAdmin");
        methods[nameof(ProcurementMasterDataChangesController.ActivatePolicy)].GetCustomAttribute<AuthorizeAttribute>()!.Roles.Should().Be("SuperAdmin");
        methods[nameof(ProcurementMasterDataChangesController.RetirePolicy)].GetCustomAttribute<AuthorizeAttribute>()!.Roles.Should().Be("SuperAdmin");
    }

    [Fact]
    public async Task AnonymousCallerCannotReadRegistry()
    {
        using var factory = CreateFactory(PolicyAuthorizationMode.Challenge);
        using var client = factory.CreateClient();

        (await client.GetAsync("/api/procurement/master-data-changes/registry")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AuthorizedReaderCanReadRegistrySummarySearchAndDetail()
    {
        var id = Guid.NewGuid();
        var service = new Mock<IProcurementMasterDataChangeService>();
        service.Setup(item => item.GetRegistryAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[] { new ProcurementMasterDataResourceDefinitionDto() });
        service.Setup(item => item.GetSummaryAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new ProcurementMasterDataChangeSummaryDto());
        service.Setup(item => item.SearchAsync(It.IsAny<ProcurementMasterDataChangeSearchRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ProcurementMasterDataChangePageDto());
        service.Setup(item => item.GetAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(new ProcurementMasterDataChangeDto { Id = id });
        using var factory = CreateFactory(PolicyAuthorizationMode.Success, service);
        using var client = factory.CreateClient();

        var responses = new[]
        {
            await client.GetAsync("/api/procurement/master-data-changes/registry"),
            await client.GetAsync("/api/procurement/master-data-changes/summary"),
            await client.GetAsync("/api/procurement/master-data-changes?page=1&pageSize=25"),
            await client.GetAsync($"/api/procurement/master-data-changes/{id}")
        };

        responses.Should().OnlyContain(response => response.StatusCode == HttpStatusCode.OK);
    }

    [Fact]
    public async Task AuthorizedMakerCanCreateDraftThroughTenantSafeContract()
    {
        var id = Guid.NewGuid();
        var service = new Mock<IProcurementMasterDataChangeService>();
        service.Setup(item => item.SaveDraftAsync(null, It.IsAny<SaveProcurementMasterDataChangeRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementMasterDataChangeDto { Id = id, RequestNumber = "MDC-1" });
        using var factory = CreateFactory(PolicyAuthorizationMode.Success, service);
        using var client = factory.CreateClient();

        var response = await client.PostAsync("/api/procurement/master-data-changes",
            new StringContent("{\"resourceType\":\"SupplierProfile\",\"targetId\":\"00000000-0000-0000-0000-000000000001\",\"proposedChangesJson\":\"{\\\"PartnerName\\\":\\\"New\\\"}\",\"reason\":\"Controlled change\",\"effectiveAtUtc\":\"2026-07-21T00:00:00Z\",\"evidence\":[]}", Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task MissingAndValidationFailuresMapToStructuredProblems()
    {
        var service = new Mock<IProcurementMasterDataChangeService>();
        service.Setup(item => item.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementMasterDataChangeNotFoundException("Missing request."));
        service.Setup(item => item.GetRegistryAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementMasterDataChangeValidationException("RESOURCE_INVALID", "Invalid resource."));
        var controller = Controller(service, "trace-master-data");

        var missing = (await controller.Get(Guid.NewGuid(), default)).Should().BeAssignableTo<ObjectResult>().Subject;
        var invalid = (await controller.GetRegistry(default)).Should().BeAssignableTo<ObjectResult>().Subject;

        missing.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        invalid.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
        invalid.Value.Should().BeAssignableTo<ValidationProblemDetails>().Which.Extensions["code"].Should().Be("RESOURCE_INVALID");
        invalid.Value.Should().BeAssignableTo<ValidationProblemDetails>().Which.Extensions["correlationId"].Should().Be("trace-master-data");
    }

    [Fact]
    public async Task ProtectedMasterDataCreateEndpointsRequireStagedChange()
    {
        var service = new Mock<IProcurementMasterDataChangeService>();
        service.Setup(item => item.CheckDirectMutationAsync(
                It.IsAny<IReadOnlyCollection<ProcurementMasterDataResourceType>>(),
                It.IsAny<Guid?>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementMasterDataDirectMutationDecisionDto
            {
                Allowed = false,
                Code = "STAGED_CHANGE_REQUIRED",
                Message = "Protected master data must use a staged change request.",
                PolicyId = Guid.NewGuid(),
                CorrelationId = "direct-create"
            });
        using var factory = CreateDirectMutationFactory(service);
        using var client = factory.CreateClient();
        var endpoints = new (string Route, ProcurementMasterDataResourceType[] ResourceTypes, string SourceReference)[]
        {
            ("/api/InventoryItems", new[] { ProcurementMasterDataResourceType.InventoryItem }, "InventoryItem.Create"),
            ("/api/inventory/warehouses", new[] { ProcurementMasterDataResourceType.Warehouse }, "Warehouse.Create"),
            ("/api/inventory/warehouse-locations", new[] { ProcurementMasterDataResourceType.WarehouseLocation }, "WarehouseLocation.Create"),
            ("/api/inventory/units-of-measure", new[] { ProcurementMasterDataResourceType.UnitOfMeasure }, "UnitOfMeasure.Create"),
            ("/api/Suppliers", new[] { ProcurementMasterDataResourceType.SupplierProfile, ProcurementMasterDataResourceType.SupplierTaxDetails }, "LegacySupplier.Create"),
            ("/api/procurement/business-partners", new[]
            {
                ProcurementMasterDataResourceType.SupplierProfile,
                ProcurementMasterDataResourceType.SupplierBankDetails,
                ProcurementMasterDataResourceType.SupplierTaxDetails,
                ProcurementMasterDataResourceType.SupplierOwnershipDetails,
                ProcurementMasterDataResourceType.SupplierCategoryAssignments,
                ProcurementMasterDataResourceType.SupplierComplianceStatus
            }, "BusinessPartner.Create")
        };

        foreach (var endpoint in endpoints)
        {
            using var response = await client.PostAsync(
                endpoint.Route,
                new StringContent("{}", Encoding.UTF8, "application/json"));

            response.StatusCode.Should().Be(HttpStatusCode.Conflict, endpoint.Route);
            service.Verify(item => item.CheckDirectMutationAsync(
                    It.Is<IReadOnlyCollection<ProcurementMasterDataResourceType>>(types => types.SequenceEqual(endpoint.ResourceTypes)),
                    It.Is<Guid?>(targetId => targetId == null),
                    endpoint.SourceReference,
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }

    [Fact]
    public async Task ProtectedSupplierSuspendAndActivateRequireComplianceStaging()
    {
        var supplierId = Guid.NewGuid();
        var service = new Mock<IProcurementMasterDataChangeService>();
        service.Setup(item => item.CheckDirectMutationAsync(
                It.IsAny<IReadOnlyCollection<ProcurementMasterDataResourceType>>(),
                supplierId,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcurementMasterDataDirectMutationDecisionDto
            {
                Allowed = false,
                Code = "STAGED_CHANGE_REQUIRED",
                Message = "Protected supplier compliance must use a staged change request.",
                CorrelationId = "supplier-status"
            });
        using var factory = CreateDirectMutationFactory(service);
        using var client = factory.CreateClient();

        using var suspend = await client.PostAsync(
            $"/api/procurement/business-partners/{supplierId}/suspend",
            new StringContent("{\"suspensionReason\":\"Compliance review\"}", Encoding.UTF8, "application/json"));
        using var activate = await client.PostAsync(
            $"/api/procurement/business-partners/{supplierId}/activate",
            new StringContent("{}", Encoding.UTF8, "application/json"));

        suspend.StatusCode.Should().Be(HttpStatusCode.Conflict);
        activate.StatusCode.Should().Be(HttpStatusCode.Conflict);
        service.Verify(item => item.CheckDirectMutationAsync(
                It.Is<IReadOnlyCollection<ProcurementMasterDataResourceType>>(types =>
                    types.SequenceEqual(new[] { ProcurementMasterDataResourceType.SupplierComplianceStatus })),
                supplierId,
                It.Is<string>(source => source == "BusinessPartner.Suspend" || source == "BusinessPartner.Activate"),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    private static ProcurementMasterDataChangesController Controller(Mock<IProcurementMasterDataChangeService> service, string traceIdentifier) =>
        new(service.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { TraceIdentifier = traceIdentifier } }
        };

    private static WebApplicationFactory<Program> CreateFactory(
        PolicyAuthorizationMode mode,
        Mock<IProcurementMasterDataChangeService>? service = null)
    {
        service ??= new Mock<IProcurementMasterDataChangeService>();
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<IPolicyEvaluator>();
                services.RemoveAll<IProcurementMasterDataChangeService>();
                services.AddSingleton<IPolicyEvaluator>(new PolicyTestEvaluator(mode));
                services.AddSingleton(service.Object);
            });
        });
    }

    private static WebApplicationFactory<Program> CreateDirectMutationFactory(
        Mock<IProcurementMasterDataChangeService> service)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<IPolicyEvaluator>();
                services.RemoveAll<IProcurementMasterDataChangeService>();
                services.Configure<ApiBehaviorOptions>(options => options.SuppressModelStateInvalidFilter = true);
                services.AddSingleton<IPolicyEvaluator>(new PolicyTestEvaluator(PolicyAuthorizationMode.Success));
                services.AddSingleton(service.Object);
                services.AddSingleton(Mock.Of<ISupplierContactRepository>());
                services.AddSingleton(Mock.Of<ISupplierItemCatalogRepository>());
            });
        });
    }
}
