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

public sealed class ProcurementSupplierAvlControllerTests
{
    [Fact]
    public void ControllerRequiresAuthenticationAndExposesTheCompleteAvlLifecycle()
    {
        var type = typeof(ProcurementSupplierAvlController);

        type.GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
        type.GetCustomAttribute<RouteAttribute>()!.Template.Should()
            .Be("api/procurement/supplier-avl");
        type.GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.DeclaringType == type)
            .Select(method => method.Name)
            .Should().Contain([
                "Summary",
                "WorkflowOptions",
                "SupplierOptions",
                "Search",
                "Get",
                "Current",
                "Create",
                "Update",
                "AddEntry",
                "RemoveEntry",
                "Submit",
                "Approve",
                "Reject",
                "Publish",
                "SuspendEntry",
                "ReinstateEntry",
                "ProcessExpiry"
            ]);
    }

    [Fact]
    public async Task DomainFailuresMapToStructured403404409And422Problems()
    {
        var service = new Mock<IProcurementSupplierAvlService>();
        service.Setup(item => item.GetAsync(Guid.Empty, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementSupplierAvlNotFoundException(
                "SUPPLIER_AVL_NOT_FOUND", "Missing."));
        service.Setup(item => item.GetSummaryAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementSupplierAvlAuthorizationException("Forbidden."));
        service.Setup(item => item.GetCurrentStateAsync(
                Guid.Empty, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementSupplierAvlConflictException(
                "SUPPLIER_AVL_CONFLICT", "Conflict."));
        service.Setup(item => item.CreateAsync(
                It.IsAny<CreateProcurementSupplierAvlRequest>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementSupplierAvlValidationException(
                "SUPPLIER_AVL_POLICY_UNAVAILABLE", "Invalid."));
        var controller = Controller(service);

        ((ObjectResult)await controller.Get(Guid.Empty, default))
            .StatusCode.Should().Be(404);
        ((ObjectResult)await controller.Summary(default))
            .StatusCode.Should().Be(403);
        ((ObjectResult)await controller.Current(Guid.Empty, default))
            .StatusCode.Should().Be(409);
        var invalid = (ObjectResult)await controller.Create(
            new CreateProcurementSupplierAvlRequest(), default);

        invalid.StatusCode.Should().Be(422);
        invalid.Value.Should().BeAssignableTo<ValidationProblemDetails>()
            .Which.Extensions["code"].Should()
            .Be("SUPPLIER_AVL_POLICY_UNAVAILABLE");
    }

    private static ProcurementSupplierAvlController Controller(
        Mock<IProcurementSupplierAvlService> service) =>
        new(service.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    TraceIdentifier = "supplier-avl-test"
                }
            }
        };
}
