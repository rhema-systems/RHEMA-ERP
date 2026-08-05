using System.Text.Json;
using ErpSystem.Api.Controllers.Inventory;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces.Inventory;
using ErpSystem.Core.Interfaces.Procurement;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Inventory;

public sealed class GoodsReceiptNotesSodControllerTests
{
    [Fact]
    public async Task StockPostingReturnsStructuredForbiddenWhenCreatorIsActor()
    {
        var grnId = Guid.NewGuid();
        var service = new Mock<IGoodsReceiptNoteService>();
        var readiness = new ProcurementPurchaseOrderSodReadinessDto
        {
            PurchaseOrderId = Guid.NewGuid(),
            CanReceive = false,
            Code = "PO_SOD_RECEIPT_BLOCKED",
            Message = "The PO creator cannot post the governed receipt.",
            ReceiptActionCoverage = ["PostGoodsReceiptNoteToInventory"]
        };
        service.Setup(item => item.PostToInventoryAsync(
                grnId,
                It.IsAny<Guid>()))
            .ThrowsAsync(new ProcurementPurchaseOrderSodBlockedException(
                "PO_SOD_RECEIPT_BLOCKED",
                readiness.Message,
                readiness));
        var controller = new GoodsReceiptNotesController(
            service.Object,
            NullLogger<GoodsReceiptNotesController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await controller.PostToInventory(grnId);

        var forbidden = result.Should().BeOfType<ObjectResult>().Subject;
        forbidden.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        var json = JsonSerializer.Serialize(forbidden.Value);
        json.Should().Contain("PO_SOD_RECEIPT_BLOCKED");
        json.Should().Contain("PostGoodsReceiptNoteToInventory");
    }
}
