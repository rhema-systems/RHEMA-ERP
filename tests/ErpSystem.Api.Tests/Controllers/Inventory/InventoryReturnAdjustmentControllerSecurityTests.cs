using System.Reflection;
using System.Security.Claims;
using ErpSystem.Api.Controllers.Inventory;
using ErpSystem.Core.DTOs.Inventory;
using ErpSystem.Core.Services.Inventory;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Inventory;

public sealed class InventoryReturnAdjustmentControllerSecurityTests
{
    [Theory]
    [InlineData(typeof(InventoryRequisitionsController))]
    [InlineData(typeof(StockAdjustmentsController))]
    public void Controlled_inventory_controllers_require_internal_tenant_authentication(Type controllerType)
    {
        var authorization = controllerType.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Single();
        authorization.Policy.Should().Be("InternalOnly");
        controllerType.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true).Should().BeNull();
    }

    [Theory]
    [InlineData(nameof(InventoryRequisitionsController.Return), "{id}/return")]
    [InlineData(nameof(InventoryRequisitionsController.DecideReturnVoucher), "return-vouchers/{voucherId:guid}/decision")]
    [InlineData(nameof(InventoryRequisitionsController.PostReturnVoucher), "return-vouchers/{voucherId:guid}/post")]
    [InlineData(nameof(InventoryRequisitionsController.ReverseReturnVoucher), "return-vouchers/{voucherId:guid}/reverse")]
    public void Return_mutations_are_post_only_and_have_no_anonymous_override(string methodName, string template)
    {
        var method = typeof(InventoryRequisitionsController).GetMethod(methodName)!;
        method.GetCustomAttribute<HttpPostAttribute>()!.Template.Should().Be(template);
        method.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true).Should().BeNull();
    }

    [Theory]
    [InlineData(nameof(StockAdjustmentsController.Submit), "{id}/submit")]
    [InlineData(nameof(StockAdjustmentsController.Decide), "{id}/decision")]
    [InlineData(nameof(StockAdjustmentsController.Post), "{id}/post")]
    [InlineData(nameof(StockAdjustmentsController.Reverse), "{id}/reverse")]
    public void Adjustment_lifecycle_mutations_are_post_only_and_have_no_anonymous_override(string methodName, string template)
    {
        var method = typeof(StockAdjustmentsController).GetMethod(methodName)!;
        method.GetCustomAttribute<HttpPostAttribute>()!.Template.Should().Be(template);
        method.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true).Should().BeNull();
    }

    [Fact]
    public void Return_voucher_download_is_a_protected_tenant_route()
    {
        var method = typeof(InventoryRequisitionsController).GetMethod(nameof(InventoryRequisitionsController.DownloadReturnVoucher))!;
        method.GetCustomAttribute<HttpGetAttribute>()!.Template.Should().Be("return-vouchers/{voucherId:guid}/download");
        method.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true).Should().BeNull();
    }

    [Fact]
    public async Task Adjustment_creation_reports_changed_idempotency_payload_as_conflict()
    {
        var userId = Guid.NewGuid();
        var service = new Mock<IStockAdjustmentService>();
        service.Setup(value => value.CreateAsync(It.IsAny<CreateStockAdjustmentDto>(), userId))
            .ThrowsAsync(new StockAdjustmentIdempotencyConflictException("Idempotency payload changed."));
        var controller = new StockAdjustmentsController(service.Object,
            Mock.Of<ILogger<StockAdjustmentsController>>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) }, "test"))
                }
            }
        };

        var result = await controller.Create(new CreateStockAdjustmentDto());

        result.Result.Should().BeOfType<ConflictObjectResult>()
            .Which.Value.Should().Be("Idempotency payload changed.");
    }
}
