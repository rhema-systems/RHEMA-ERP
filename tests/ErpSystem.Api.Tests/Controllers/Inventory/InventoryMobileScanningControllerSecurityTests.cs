using System.Reflection;
using ErpSystem.Api.Controllers.Inventory;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Entities.Inventory;
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

public sealed class InventoryMobileScanningControllerSecurityTests
{
    [Fact, Trait("Batch", "E2E-024")]
    public void Controller_is_internal_only_and_exposes_one_synchronization_post()
    {
        var type = typeof(InventoryMobileScanningController);

        type.GetCustomAttribute<RouteAttribute>()!.Template.Should().Be("api/inventory/mobile-scanning");
        type.GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be("InternalOnly");
        var synchronize = type.GetMethod(nameof(InventoryMobileScanningController.Synchronize))!;
        synchronize.GetCustomAttribute<HttpPostAttribute>()!.Template.Should().Be("synchronize");
    }

    [Fact, Trait("Batch", "E2E-024")]
    public async Task Synchronize_delegates_the_full_batch_and_server_correlation_to_the_tenant_service()
    {
        var request = new SynchronizeInventoryScanBatchRequest
        {
            DeviceId = "scanner-e2e024", IdempotencyKey = "batch-e2e024",
            Operation = InventoryScanOperation.TransferReceipt, DocumentId = Guid.NewGuid(),
            WarehouseId = Guid.NewGuid(), ApplyTransaction = true,
            Lines = { new InventoryScanInputDto { ClientLineId = Guid.NewGuid(), RawIdentifier = "ITEM-01", Quantity = 1 } }
        };
        var service = new Mock<IInventoryScanningService>();
        service.Setup(value => value.SynchronizeAsync(request, "corr-e2e024", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InventoryScanBatchDto { IdempotencyKey = request.IdempotencyKey, Status = InventoryScanBatchStatus.Applied });
        var controller = Controller(service.Object);

        var response = await controller.Synchronize(request, CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>();
        service.VerifyAll();
    }

    [Fact, Trait("Batch", "E2E-024")]
    public async Task Synchronize_maps_authorization_and_validation_failures_to_structured_problems()
    {
        var request = new SynchronizeInventoryScanBatchRequest();
        var service = new Mock<IInventoryScanningService>();
        service.SetupSequence(value => value.SynchronizeAsync(request, "corr-e2e024", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InventoryScanningAuthorizationException("No assigned warehouse."))
            .ThrowsAsync(new InventoryScanningException("INV_SCAN_DOCUMENT_NOT_FOUND", "Missing transaction."));
        var controller = Controller(service.Object);

        var forbidden = await controller.Synchronize(request, CancellationToken.None);
        var missing = await controller.Synchronize(request, CancellationToken.None);

        var forbiddenResult = forbidden.Result.Should().BeOfType<ObjectResult>().Subject;
        forbiddenResult.StatusCode.Should().Be(403);
        ((ProblemDetails)forbiddenResult.Value!).Extensions["code"].Should().Be("INV_SCAN_FORBIDDEN");
        var missingResult = missing.Result.Should().BeOfType<ObjectResult>().Subject;
        missingResult.StatusCode.Should().Be(404);
        ((ProblemDetails)missingResult.Value!).Extensions["code"].Should().Be("INV_SCAN_DOCUMENT_NOT_FOUND");
    }

    private static InventoryMobileScanningController Controller(IInventoryScanningService service)
    {
        var context = new DefaultHttpContext { TraceIdentifier = "trace-e2e024" };
        context.Request.Headers["X-Correlation-ID"] = "corr-e2e024";
        return new InventoryMobileScanningController(service, Mock.Of<ILogger<InventoryMobileScanningController>>())
        {
            ControllerContext = new ControllerContext { HttpContext = context }
        };
    }
}
