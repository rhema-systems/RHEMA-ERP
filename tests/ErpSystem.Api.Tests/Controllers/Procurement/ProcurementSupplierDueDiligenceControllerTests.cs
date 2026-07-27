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

public sealed class ProcurementSupplierDueDiligenceControllerTests
{
    [Fact]
    public void ControllerRequiresAuthenticationAndExposesTheCompleteReviewLifecycle()
    {
        var type = typeof(ProcurementSupplierDueDiligenceController);

        type.GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
        type.GetCustomAttribute<RouteAttribute>()!.Template.Should()
            .Be("api/procurement/supplier-due-diligence");
        type.GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.DeclaringType == type)
            .Select(method => method.Name)
            .Should().Contain([
                "Summary",
                "WorkflowOptions",
                "SupplierOptions",
                "Search",
                "Get",
                "CurrentState",
                "Create",
                "Update",
                "Submit",
                "Approve",
                "Reject",
                "ProcessExpiry"
            ]);
    }

    [Fact]
    public async Task DomainFailuresMapToStructured403404409And422Problems()
    {
        var service = new Mock<IProcurementSupplierDueDiligenceService>();
        service.Setup(item => item.GetAsync(Guid.Empty, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementSupplierDueDiligenceNotFoundException(
                "REVIEW_NOT_FOUND", "Missing."));
        service.Setup(item => item.GetSummaryAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementSupplierDueDiligenceAuthorizationException(
                "Forbidden."));
        service.Setup(item => item.GetCurrentStateAsync(
                Guid.Empty, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementSupplierDueDiligenceConflictException(
                "REVIEW_CONFLICT", "Conflict."));
        service.Setup(item => item.CreateAsync(
                It.IsAny<CreateProcurementSupplierDueDiligenceRequest>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementSupplierDueDiligenceValidationException(
                "DEC011_REQUIRED", "Invalid."));
        var controller = Controller(service);

        ((ObjectResult)await controller.Get(Guid.Empty, default))
            .StatusCode.Should().Be(404);
        ((ObjectResult)await controller.Summary(default))
            .StatusCode.Should().Be(403);
        ((ObjectResult)await controller.CurrentState(Guid.Empty, default))
            .StatusCode.Should().Be(409);
        var invalid = (ObjectResult)await controller.Create(
            new CreateProcurementSupplierDueDiligenceRequest(),
            default);

        invalid.StatusCode.Should().Be(422);
        invalid.Value.Should().BeAssignableTo<ValidationProblemDetails>()
            .Which.Extensions["code"].Should().Be("DEC011_REQUIRED");
    }

    private static ProcurementSupplierDueDiligenceController Controller(
        Mock<IProcurementSupplierDueDiligenceService> service) =>
        new(service.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    TraceIdentifier = "supplier-due-diligence-test"
                }
            }
        };
}
