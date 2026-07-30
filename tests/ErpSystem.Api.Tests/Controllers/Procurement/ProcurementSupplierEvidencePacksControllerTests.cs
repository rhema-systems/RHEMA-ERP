using System.Reflection;
using ErpSystem.Api.Controllers.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Procurement;

public sealed class ProcurementSupplierEvidencePacksControllerTests
{
    [Fact]
    public void ControllerRequiresAuthenticationAndExposesLifecycleOptionsAndReadinessRoutes()
    {
        var type = typeof(ProcurementSupplierEvidencePacksController);
        type.GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
        type.GetCustomAttribute<RouteAttribute>()!.Template.Should()
            .Be("api/procurement/supplier-evidence-packs");
        type.GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.DeclaringType == type)
            .Select(method => method.Name)
            .Should().Contain([
                "Summary", "WorkflowOptions", "ConfigurationProfileOptions",
                "Search", "Get", "Create", "Update", "Submit", "Publish",
                "Reject", "Clone", "Retire", "DeleteDraft",
                "RegistrationReadiness"
            ]);
    }

    [Fact]
    public async Task DomainFailuresMapToStructured403404409And422Problems()
    {
        var service = new Mock<IProcurementSupplierEvidencePackService>();
        service.Setup(item => item.GetAsync(Guid.Empty, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementSupplierEvidencePackNotFoundException(
                "PACK_NOT_FOUND", "Missing."));
        service.Setup(item => item.GetSummaryAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementSupplierEvidencePackAuthorizationException("Forbidden."));
        service.Setup(item => item.GetWorkflowOptionsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementSupplierEvidencePackConflictException(
                "PACK_CONFLICT", "Conflict."));
        service.Setup(item => item.GetConfigurationProfileOptionsAsync(
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementSupplierEvidencePackValidationException(
                "PROFILE_REQUIRED", "Invalid."));
        var controller = Controller(service);

        ((ObjectResult)await controller.Get(Guid.Empty, default)).StatusCode.Should().Be(404);
        ((ObjectResult)await controller.Summary(default)).StatusCode.Should().Be(403);
        ((ObjectResult)await controller.WorkflowOptions(default)).StatusCode.Should().Be(409);
        var invalid = (ObjectResult)await controller.ConfigurationProfileOptions(default);
        invalid.StatusCode.Should().Be(422);
        invalid.Value.Should().BeAssignableTo<ValidationProblemDetails>()
            .Which.Extensions["code"].Should().Be("PROFILE_REQUIRED");
    }

    private static ProcurementSupplierEvidencePacksController Controller(
        Mock<IProcurementSupplierEvidencePackService> service) =>
        new(service.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    TraceIdentifier = "supplier-evidence-pack-test"
                }
            }
        };
}
