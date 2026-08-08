using System.Reflection;
using ErpSystem.Api.Controllers.Inventory;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Interfaces.Inventory;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Inventory;

public sealed class InventoryReplenishmentControllerSecurityTests
{
    [Fact]
    public void Register_requires_internal_authentication_and_has_no_anonymous_override()
    {
        var type = typeof(InventoryReplenishmentController);
        type.GetCustomAttribute<AuthorizeAttribute>(inherit: true)!.Policy.Should().Be("InternalOnly");
        type.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true).Should().BeNull();
        type.GetCustomAttribute<RouteAttribute>()!.Template.Should().Be("api/inventory/replenishment");
    }

    [Theory]
    [InlineData(nameof(InventoryReplenishmentController.Generate), "generate")]
    [InlineData(nameof(InventoryReplenishmentController.Submit), "{id:guid}/submit")]
    [InlineData(nameof(InventoryReplenishmentController.Decide), "{id:guid}/decision")]
    [InlineData(nameof(InventoryReplenishmentController.Convert), "{id:guid}/purchase-requisition")]
    public void Mutations_are_post_only_and_cannot_be_anonymous(string methodName, string template)
    {
        var method = typeof(InventoryReplenishmentController).GetMethod(methodName)!;
        method.GetCustomAttribute<HttpPostAttribute>()!.Template.Should().Be(template);
        method.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true).Should().BeNull();
    }

    [Fact]
    public async Task Generate_delegates_to_the_tenant_service_and_propagates_correlation()
    {
        var service = new Mock<IInventoryReplenishmentService>();
        var expected = new List<InventoryReplenishmentRecommendationDto>
        {
            new() { Id = Guid.NewGuid(), RecommendationNumber = "IRR-TEST-001" }
        };
        service.Setup(value => value.GenerateAsync(
                It.Is<GenerateInventoryReplenishmentRequest>(request => request.CorrelationId == "tdc0612-api"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        var controller = Create(service.Object);
        controller.Request.Headers["X-Correlation-ID"] = "tdc0612-api";

        var result = await controller.Generate(new GenerateInventoryReplenishmentRequest
        {
            WarehouseId = Guid.NewGuid(), DemandWindowDays = 90, IdempotencyKey = "api-generate"
        }, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(expected);
        service.VerifyAll();
    }

    [Fact]
    public async Task Cross_tenant_or_unknown_recommendation_is_a_structured_404()
    {
        var service = new Mock<IInventoryReplenishmentService>();
        service.Setup(value => value.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InventoryReplenishmentNotFoundException(
                "The replenishment recommendation was not found in the current tenant."));
        var controller = Create(service.Object);

        var result = await controller.GetById(Guid.NewGuid(), CancellationToken.None);

        var notFound = result.Result.Should().BeOfType<NotFoundObjectResult>().Subject;
        var problem = notFound.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Status.Should().Be(StatusCodes.Status404NotFound);
        problem.Extensions["code"].Should().Be("INV_REPLENISHMENT_NOT_FOUND");
    }

    [Fact]
    public async Task Stale_row_version_is_a_structured_409()
    {
        var service = new Mock<IInventoryReplenishmentService>();
        service.Setup(value => value.SubmitAsync(It.IsAny<Guid>(),
                It.IsAny<SubmitInventoryReplenishmentRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InventoryReplenishmentControlException(
                "INV_REPLENISHMENT_CONCURRENCY_CONFLICT", "Refresh and retry."));
        var controller = Create(service.Object);

        var result = await controller.Submit(Guid.NewGuid(), new SubmitInventoryReplenishmentRequest
        {
            Reason = "Submit", IdempotencyKey = "stale", RowVersion = "AQ=="
        }, CancellationToken.None);

        var conflict = result.Result.Should().BeOfType<ObjectResult>().Subject;
        conflict.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        var problem = conflict.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Extensions["code"].Should().Be("INV_REPLENISHMENT_CONCURRENCY_CONFLICT");
    }

    private static InventoryReplenishmentController Create(IInventoryReplenishmentService service)
    {
        var controller = new InventoryReplenishmentController(service,
            NullLogger<InventoryReplenishmentController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        controller.HttpContext.TraceIdentifier = "tdc0612-controller-test";
        return controller;
    }
}
