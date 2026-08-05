using System.Reflection;
using ErpSystem.Api.Controllers.Inventory;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Interfaces.Inventory;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Inventory;

public sealed class InventoryAnalyticsControllerSecurityTests
{
    [Fact, Trait("Batch", "TDC-0614")]
    public void Controller_is_internal_only_and_exposes_read_only_route()
    {
        var type = typeof(InventoryAnalyticsController);

        type.GetCustomAttribute<RouteAttribute>()!.Template.Should().Be("api/inventory/analytics");
        type.GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be("InternalOnly");
        var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(method => method.GetCustomAttributes<HttpMethodAttribute>().Any()).ToList();
        methods.Should().ContainSingle();
        methods[0].GetCustomAttribute<HttpGetAttribute>().Should().NotBeNull();
    }

    [Fact, Trait("Batch", "TDC-0614")]
    public async Task Get_delegates_tenant_safe_filters_to_service()
    {
        var warehouseId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var service = new Mock<IInventoryAnalyticsService>();
        service.Setup(value => value.GetAsync(warehouseId, categoryId, 60, 120, 45, 250,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InventoryAnalyticsDto { SlowMovingDays = 60, NonMovingDays = 120 });
        var controller = Controller(service.Object);

        var response = await controller.Get(warehouseId, categoryId, 60, 120, 45, 250);

        response.Result.Should().BeOfType<OkObjectResult>();
        service.VerifyAll();
    }

    [Fact, Trait("Batch", "TDC-0614")]
    public async Task Get_maps_authorization_and_validation_to_structured_problems()
    {
        var service = new Mock<IInventoryAnalyticsService>();
        service.SetupSequence(value => value.GetAsync(null, null, 90, 180, 90, 500,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InventoryAnalyticsAuthorizationException("No assigned warehouse."))
            .ThrowsAsync(new InventoryAnalyticsException("INV_ANALYTICS_THRESHOLDS_INVALID", "Invalid thresholds."));
        var controller = Controller(service.Object);

        var forbidden = await controller.Get();
        var invalid = await controller.Get();

        var forbiddenResult = forbidden.Result.Should().BeOfType<ObjectResult>().Subject;
        forbiddenResult.StatusCode.Should().Be(403);
        ((ProblemDetails)forbiddenResult.Value!).Extensions["code"].Should().Be("INV_ANALYTICS_FORBIDDEN");
        var invalidResult = invalid.Result.Should().BeOfType<UnprocessableEntityObjectResult>().Subject;
        ((ProblemDetails)invalidResult.Value!).Extensions["code"].Should().Be("INV_ANALYTICS_THRESHOLDS_INVALID");
    }

    private static InventoryAnalyticsController Controller(IInventoryAnalyticsService service)
    {
        var controller = new InventoryAnalyticsController(
            service, Mock.Of<ILogger<InventoryAnalyticsController>>());
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { TraceIdentifier = "tdc0614-test" }
        };
        return controller;
    }
}
