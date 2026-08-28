using System.Reflection;
using System.Security.Claims;
using ErpSystem.Api.Controllers.Inventory;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Services.Inventory;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Inventory;

public sealed class StockAdjustmentsOpeningStockControllerTests
{
    [Fact]
    public async Task Dedicated_post_route_forwards_canonical_opening_schedule_to_inventory_service()
    {
        var userId = Guid.NewGuid();
        var request = new CreateOpeningStockAdjustmentDto
        {
            WarehouseId = Guid.NewGuid(),
            OpeningDate = new DateTime(2025, 1, 1),
            BookClassification = "IFRS",
            SourceScheduleReference = "FINDEMO-INV-SCHEDULE-001",
            Description = "Inventory-owned source schedule",
            Items =
            [
                new CreateOpeningStockItemDto
                {
                    InventoryItemId = Guid.NewGuid(),
                    LocationId = Guid.NewGuid(),
                    Quantity = 10m,
                    UnitCost = 18_000m
                }
            ]
        };
        var expected = new StockAdjustmentDetailDto
        {
            Id = Guid.NewGuid(),
            AdjustmentNumber = "ADJ260001",
            ReasonCode = StockAdjustmentReasonCodes.InitialStock,
            Reference = request.SourceScheduleReference,
            BookClassification = request.BookClassification,
            Status = "Draft"
        };
        var service = new Mock<IStockAdjustmentService>(MockBehavior.Strict);
        service.Setup(x => x.CreateOpeningStockAsync(request, userId)).ReturnsAsync(expected);
        var controller = Controller(service.Object, userId);

        var response = await controller.CreateOpeningStock(request);

        var created = response.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        created.ActionName.Should().Be(nameof(StockAdjustmentsController.GetById));
        created.RouteValues!["id"].Should().Be(expected.Id);
        created.Value.Should().BeSameAs(expected);
        typeof(StockAdjustmentsController).GetMethod(nameof(StockAdjustmentsController.CreateOpeningStock))!
            .GetCustomAttribute<HttpPostAttribute>()!.Template.Should().Be("opening-stock");
        service.VerifyAll();
    }

    [Fact]
    public async Task Readiness_route_returns_fail_closed_inventory_options_without_mutating()
    {
        var userId = Guid.NewGuid();
        var expected = new OpeningStockOptionsDto
        {
            IsReady = false,
            RetrievedAtUtc = DateTime.UtcNow,
            Blockers =
            [
                "No active, owned warehouse location is available.",
                "No active, non-deleted stock item is available."
            ]
        };
        var service = new Mock<IStockAdjustmentService>(MockBehavior.Strict);
        service.Setup(x => x.GetOpeningStockOptionsAsync(userId, CancellationToken.None))
            .ReturnsAsync(expected);
        var controller = Controller(service.Object, userId);

        var response = await controller.GetOpeningStockOptions(CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(expected);
        typeof(StockAdjustmentsController).GetMethod(nameof(StockAdjustmentsController.GetOpeningStockOptions))!
            .GetCustomAttribute<HttpGetAttribute>()!.Template.Should().Be("opening-stock/options");
        service.VerifyAll();
    }

    private static StockAdjustmentsController Controller(IStockAdjustmentService service, Guid userId)
    {
        var controller = new StockAdjustmentsController(
            service,
            NullLogger<StockAdjustmentsController>.Instance);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
                    "test"))
            }
        };
        return controller;
    }
}
