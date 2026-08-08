using System.Reflection;
using System.Text.Json;
using ErpSystem.Api.Controllers.Finance;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Finance;

public sealed class VendorInvoiceMatchExceptionControllerTests
{
    [Theory]
    [InlineData(nameof(VendorInvoiceController.GetMatchExceptions), "{id}/match-exceptions", FinancePermissions.ViewFinance)]
    [InlineData(nameof(VendorInvoiceController.RequestMatchException), "{id}/match-exceptions", FinancePermissions.ManageApInvoices)]
    [InlineData(nameof(VendorInvoiceController.DecideMatchException), "match-exceptions/{exceptionId}/decision", FinancePermissions.ApproveApInvoices)]
    [InlineData(nameof(VendorInvoiceController.CancelMatchException), "match-exceptions/{exceptionId}/cancel", FinancePermissions.ManageApInvoices)]
    [InlineData(nameof(VendorInvoiceController.CompleteMatchExceptionCorrectiveAction), "match-exceptions/{exceptionId}/corrective-action/complete", FinancePermissions.ManageApInvoices)]
    public void RoutesUseExistingFinancePermissions(string action, string route, string permission)
    {
        var method = typeof(VendorInvoiceController).GetMethod(action, BindingFlags.Instance | BindingFlags.Public)
            ?? throw new InvalidOperationException($"Action {action} was not found.");

        var routeAttribute = method.GetCustomAttributes<HttpMethodAttribute>().Single();
        routeAttribute.Template.Should().Be(route);
        method.GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(permission);
    }

    [Fact]
    public async Task RequestDelegatesOnlyToAp006LifecycleWithServerCorrelation()
    {
        var invoiceId = Guid.NewGuid();
        var expected = new VendorInvoiceMatchExceptionDto { Id = Guid.NewGuid(), VendorInvoiceId = invoiceId };
        var lifecycle = new Mock<IVendorInvoiceMatchExceptionService>();
        lifecycle.Setup(service => service.RequestAsync(
                invoiceId,
                It.IsAny<CreateVendorInvoiceMatchExceptionDto>(),
                "trace-0507",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        await using var context = Context();
        var controller = Controller(context, lifecycle.Object);

        var response = await controller.RequestMatchException(
            invoiceId,
            new CreateVendorInvoiceMatchExceptionDto());

        response.Result.Should().BeOfType<CreatedAtActionResult>()
            .Which.Value.Should().BeSameAs(expected);
        lifecycle.VerifyAll();
    }

    [Fact]
    public async Task StableLifecycleConflictIsReturnedWithoutCallingPaymentAllocation()
    {
        var exceptionId = Guid.NewGuid();
        var lifecycle = new Mock<IVendorInvoiceMatchExceptionService>();
        lifecycle.Setup(service => service.DecideAsync(
                exceptionId,
                It.IsAny<DecideVendorInvoiceMatchExceptionDto>(),
                "trace-0507",
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new VendorInvoiceMatchExceptionControlException(
                "AP_MATCH_EXCEPTION_SOD_BLOCKED",
                "The requester cannot approve this exception.",
                StatusCodes.Status403Forbidden));
        await using var context = Context();
        var controller = Controller(context, lifecycle.Object);

        var response = await controller.DecideMatchException(
            exceptionId,
            new DecideVendorInvoiceMatchExceptionDto { Approved = true, Comment = "Approve", RowVersion = "AA==" });

        var blocked = response.Result.Should().BeOfType<ObjectResult>().Subject;
        blocked.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        JsonSerializer.Serialize(blocked.Value).Should().Contain("AP_MATCH_EXCEPTION_SOD_BLOCKED");
    }

    private static VendorInvoiceController Controller(
        ApplicationDbContext context,
        IVendorInvoiceMatchExceptionService lifecycle)
    {
        var controller = new VendorInvoiceController(
            Mock.Of<IVendorInvoiceService>(),
            Mock.Of<ICurrentUserService>(),
            context,
            lifecycle)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { TraceIdentifier = "trace-0507" }
            }
        };
        return controller;
    }

    private static ApplicationDbContext Context() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"tdc0507-controller-{Guid.NewGuid():N}")
            .Options);
}
