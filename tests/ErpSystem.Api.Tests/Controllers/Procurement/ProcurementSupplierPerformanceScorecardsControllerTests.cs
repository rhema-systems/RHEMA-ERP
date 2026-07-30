using System.Reflection;
using ErpSystem.Api.Controllers.Procurement;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces.Procurement;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Procurement;

public sealed class ProcurementSupplierPerformanceScorecardsControllerTests
{
    [Fact]
    public void Controller_requires_authentication_and_exposes_the_complete_scorecard_lifecycle()
    {
        var type = typeof(ProcurementSupplierPerformanceScorecardsController);

        type.GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
        type.GetCustomAttribute<RouteAttribute>()!.Template.Should()
            .Be("api/procurement/supplier-performance-scorecards");
        type.GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.DeclaringType == type)
            .Select(method => method.Name)
            .Should().Contain([
                "Summary",
                "SupplierOptions",
                "Search",
                "Get",
                "Current",
                "Calculate"
            ]);
    }

    [Fact]
    public async Task Domain_failures_map_to_structured_403_404_409_and_422_problems()
    {
        var service = new Mock<IProcurementSupplierPerformanceScorecardService>();
        service.Setup(item => item.GetAsync(Guid.Empty, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementSupplierPerformanceNotFoundException(
                "SUPPLIER_PERFORMANCE_NOT_FOUND", "Missing."));
        service.Setup(item => item.GetSummaryAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementSupplierPerformanceAuthorizationException(
                "Forbidden."));
        service.Setup(item => item.GetCurrentStateAsync(
                Guid.Empty, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementSupplierPerformanceConflictException(
                "SUPPLIER_PERFORMANCE_CONFLICT", "Conflict."));
        service.Setup(item => item.CalculateAsync(
                It.IsAny<CalculateProcurementSupplierPerformanceRequest>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementSupplierPerformanceValidationException(
                "SUPPLIER_PERFORMANCE_POLICY_UNAVAILABLE", "Invalid."));
        var controller = Controller(service);

        ((ObjectResult)await controller.Get(Guid.Empty, default))
            .StatusCode.Should().Be(404);
        ((ObjectResult)await controller.Summary(default))
            .StatusCode.Should().Be(403);
        ((ObjectResult)await controller.Current(Guid.Empty, default))
            .StatusCode.Should().Be(409);
        var invalid = (ObjectResult)await controller.Calculate(
            new CalculateProcurementSupplierPerformanceRequest(), default);

        invalid.StatusCode.Should().Be(422);
        invalid.Value.Should().BeAssignableTo<ProblemDetails>()
            .Which.Extensions["code"].Should()
            .Be("SUPPLIER_PERFORMANCE_POLICY_UNAVAILABLE");
    }

    private static ProcurementSupplierPerformanceScorecardsController Controller(
        Mock<IProcurementSupplierPerformanceScorecardService> service) =>
        new(service.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    TraceIdentifier = "supplier-performance-test"
                }
            }
        };
}
