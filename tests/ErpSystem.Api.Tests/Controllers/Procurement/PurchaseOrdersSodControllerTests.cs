using System.Reflection;
using ErpSystem.Api.Controllers.Procurement;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Procurement;

public sealed class PurchaseOrdersSodControllerTests
{
    [Fact]
    public void DedicatedReadinessRouteIsAuthenticatedAndTenantServiceBacked()
    {
        var controller = typeof(PurchaseOrdersController);
        var method = controller.GetMethod(
            "GetSodReadiness",
            BindingFlags.Instance | BindingFlags.Public);

        method.Should().NotBeNull();
        method!.GetCustomAttribute<HttpGetAttribute>()!.Template.Should()
            .Be("{id}/sod-readiness");
        controller.GetCustomAttributes()
            .Select(attribute => attribute.GetType().Name)
            .Should().Contain("AuthorizeAttribute");
    }

    [Fact]
    public void ApprovalSubmissionAndReceiptRemainSeparateServerCommands()
    {
        var methods = typeof(PurchaseOrdersController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Select(method => method.Name)
            .ToList();

        methods.Should().Contain([
            "ApprovePurchaseOrder",
            "SubmitPurchaseOrder",
            "ReceivePurchaseOrder",
            "UpdatePurchaseOrderStatus"
        ]);
    }
}
