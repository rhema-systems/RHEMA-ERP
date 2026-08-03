using System.Reflection;
using ErpSystem.Api.Controllers.Inventory;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Inventory;

public sealed class InventoryTransferControllerSecurityTests
{
    [Fact]
    public void Transfer_controller_requires_internal_tenant_authentication()
    {
        var authorization = typeof(InventoryTransfersController).GetCustomAttributes<AuthorizeAttribute>(inherit: true).Single();
        authorization.Policy.Should().Be("InternalOnly");
        typeof(InventoryTransfersController).GetCustomAttribute<AllowAnonymousAttribute>(inherit: true).Should().BeNull();
    }

    [Theory]
    [InlineData(nameof(InventoryTransfersController.Ship), "{id}/ship")]
    [InlineData(nameof(InventoryTransfersController.ShipWithCosts), "{id}/ship-with-costs")]
    [InlineData(nameof(InventoryTransfersController.SaveShippingCosts), "{id}/save-shipping-costs")]
    [InlineData(nameof(InventoryTransfersController.Receive), "{id}/receive")]
    [InlineData(nameof(InventoryTransfersController.ResolveDiscrepancies), "{id}/resolve-discrepancies")]
    [InlineData(nameof(InventoryTransfersController.Close), "{id}/close")]
    [InlineData(nameof(InventoryTransfersController.Cancel), "{id}/cancel")]
    [InlineData(nameof(InventoryTransfersController.ReverseShipment), "{id}/reverse-shipment")]
    public void Controlled_transfer_mutations_are_post_only_and_have_no_anonymous_override(string methodName, string template)
    {
        var method = typeof(InventoryTransfersController).GetMethod(methodName)!;
        method.GetCustomAttribute<HttpPostAttribute>()!.Template.Should().Be(template);
        method.GetCustomAttribute<AllowAnonymousAttribute>(inherit: true).Should().BeNull();
    }
}
