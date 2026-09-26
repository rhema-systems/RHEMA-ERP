using ErpSystem.Api.Controllers.Finance;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Procurement;

public sealed class ProcurementInvoiceDistributionControllerTests
{
    [Fact]
    public async Task DistributionWrites_RequireMaintenancePermission()
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(value => value.UserId).Returns(Guid.NewGuid().ToString());
        var service = new Mock<IVendorInvoiceService>(MockBehavior.Strict);
        var controller = new VendorInvoiceController(service.Object, user.Object, db);
        Assert.IsType<ForbidResult>((await controller.SaveDistribution(Guid.NewGuid(), new(), default)).Result);
        Assert.IsType<ForbidResult>((await controller.ResetDistribution(Guid.NewGuid(), new(), default)).Result);
        Assert.IsType<ForbidResult>(await controller.DistributionAccounts(Guid.NewGuid(), default));
        service.VerifyNoOtherCalls();
    }

    [Fact]
    public void DistributionWriteRoutes_AreProcurementOnly()
    {
        var save = typeof(VendorInvoiceController).GetMethod(nameof(VendorInvoiceController.SaveDistribution))!;
        var reset = typeof(VendorInvoiceController).GetMethod(nameof(VendorInvoiceController.ResetDistribution))!;
        Assert.Equal("~/api/procurement/supplier-invoices/{id:guid}/distribution", Assert.Single(save.GetCustomAttributes(typeof(HttpPutAttribute), false).Cast<HttpPutAttribute>()).Template);
        Assert.Equal("~/api/procurement/supplier-invoices/{id:guid}/distribution/reset", Assert.Single(reset.GetCustomAttributes(typeof(HttpPostAttribute), false).Cast<HttpPostAttribute>()).Template);
    }
}
