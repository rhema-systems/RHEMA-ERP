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

public sealed class ProcurementSupplierRiskControllerTests
{
    [Fact]
    public void ControllerRequiresAuthenticationAndExposesTheCompleteRiskLifecycle()
    {
        var type = typeof(ProcurementSupplierRiskController);

        type.GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
        type.GetCustomAttribute<RouteAttribute>()!.Template.Should()
            .Be("api/procurement/supplier-risk");
        type.GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.DeclaringType == type)
            .Select(method => method.Name)
            .Should().Contain([
                "Summary",
                "SupplierOptions",
                "WorkflowOptions",
                "Search",
                "Get",
                "Current",
                "Evaluate",
                "Escalate",
                "Resolve"
            ]);
    }

    [Fact]
    public async Task DomainFailuresMapToStructured403404409And422Problems()
    {
        var service = new Mock<IProcurementSupplierRiskService>();
        service.Setup(item => item.GetAsync(Guid.Empty, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementSupplierRiskNotFoundException(
                "SUPPLIER_RISK_NOT_FOUND", "Missing."));
        service.Setup(item => item.GetSummaryAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementSupplierRiskAuthorizationException("Forbidden."));
        service.Setup(item => item.GetCurrentStateAsync(
                Guid.Empty, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementSupplierRiskConflictException(
                "SUPPLIER_RISK_CONFLICT", "Conflict."));
        service.Setup(item => item.EvaluateAsync(
                It.IsAny<EvaluateProcurementSupplierRiskRequest>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementSupplierRiskValidationException(
                "SUPPLIER_RISK_POLICY_UNAVAILABLE", "Invalid."));
        var controller = Controller(service);

        ((ObjectResult)await controller.Get(Guid.Empty, default))
            .StatusCode.Should().Be(404);
        ((ObjectResult)await controller.Summary(default))
            .StatusCode.Should().Be(403);
        ((ObjectResult)await controller.Current(Guid.Empty, default))
            .StatusCode.Should().Be(409);
        var invalid = (ObjectResult)await controller.Evaluate(
            new EvaluateProcurementSupplierRiskRequest(), default);

        invalid.StatusCode.Should().Be(422);
        invalid.Value.Should().BeAssignableTo<ValidationProblemDetails>()
            .Which.Extensions["code"].Should()
            .Be("SUPPLIER_RISK_POLICY_UNAVAILABLE");
    }

    private static ProcurementSupplierRiskController Controller(
        Mock<IProcurementSupplierRiskService> service) =>
        new(service.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    TraceIdentifier = "supplier-risk-test"
                }
            }
        };
}
