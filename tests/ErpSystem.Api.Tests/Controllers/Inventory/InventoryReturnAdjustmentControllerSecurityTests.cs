using System.Reflection;
using ErpSystem.Api.Controllers.Inventory;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
}
