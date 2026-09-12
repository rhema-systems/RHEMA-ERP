using System.Reflection;
using System.Text.Json;
using ErpSystem.Api.Controllers.Finance;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Finance;

public sealed class VendorInvoiceMatchingControllerTests
{
    [Fact]
    public async Task GoodsEntry_ShouldRequireInvoiceWritePermissionAndUseTheInvoiceService()
    {
        var service = new Mock<IVendorInvoiceService>();
        await using var db = Context();
        var deniedUser = new Mock<ICurrentUserService>();
        deniedUser.SetupGet(x => x.UserId).Returns("not-a-user-id");
        var denied = new VendorInvoiceController(service.Object, deniedUser.Object, db);
        (await denied.GetGoodsEntry(Guid.NewGuid(), null, CancellationToken.None)).Result.Should().BeOfType<ForbidResult>();
        service.VerifyNoOtherCalls();
        var po = Guid.NewGuid();
        var expected = new ApGoodsInvoiceEntryDto { PurchaseOrderId = po };
        service.Setup(x => x.GetGoodsInvoiceEntryAsync(po, null, It.IsAny<CancellationToken>())).ReturnsAsync(expected);
        var allowed = new VendorInvoiceController(service.Object, PrivilegedCurrentUser().Object, db);
        (await allowed.GetGoodsEntry(po, null, CancellationToken.None)).Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeSameAs(expected);
    }

    [Fact]
    public void DedicatedReadinessRouteIsAuthenticated()
    {
        var method = typeof(VendorInvoiceController).GetMethod(
            nameof(VendorInvoiceController.GetThreeWayMatchReadiness),
            BindingFlags.Instance | BindingFlags.Public);

        method.Should().NotBeNull();
        method!.GetCustomAttribute<HttpGetAttribute>()!.Template.Should().Be("{id}/match/readiness");
        typeof(VendorInvoiceController).GetCustomAttributes<AuthorizeAttribute>().Should().NotBeEmpty();
    }

    [Fact]
    public async Task SubmitReturnsStableUnprocessableEntityForComplianceHardStop()
    {
        var invoiceId = Guid.NewGuid();
        var service = new Mock<IVendorInvoiceService>();
        service.Setup(item => item.SubmitForApprovalAsync(invoiceId,
                It.IsAny<ErpSystem.Core.Finance.Integration.FinancePostingProducerContext>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new VendorInvoiceMatchControlException(
                "AP_MATCH_CONFIGURATION_REQUIRED",
                "An effective procurement configuration profile is required."));
        await using var db = Context();
        var currentUser = PrivilegedCurrentUser();
        var controller = new VendorInvoiceController(service.Object, currentUser.Object, db);

        var response = await controller.SubmitForApproval(invoiceId);

        var blocked = response.Result.Should().BeOfType<UnprocessableEntityObjectResult>().Subject;
        blocked.StatusCode.Should().Be(422);
        var json = JsonSerializer.Serialize(blocked.Value);
        json.Should().Contain("AP_MATCH_CONFIGURATION_REQUIRED");
        json.Should().Contain("effective procurement configuration profile");
    }

    [Fact]
    public async Task ReadinessReturnsTenantServiceEvaluationWithoutMutation()
    {
        var invoiceId = Guid.NewGuid();
        var expected = new InvoiceMatchingResultDto
        {
            VendorInvoiceId = invoiceId,
            IsRequired = true,
            ApprovalReady = false,
            Message = "Receipt acceptance is incomplete."
        };
        var service = new Mock<IVendorInvoiceService>();
        service.Setup(item => item.GetThreeWayMatchReadinessAsync(invoiceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        await using var db = Context();
        var currentUser = new Mock<ICurrentUserService>();
        var controller = new VendorInvoiceController(service.Object, currentUser.Object, db);

        var response = await controller.GetThreeWayMatchReadiness(invoiceId);

        response.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeSameAs(expected);
        service.Verify(item => item.GetThreeWayMatchReadinessAsync(
            invoiceId, It.IsAny<CancellationToken>()), Times.Once);
        service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task MatchMutationRequiresAnApWriteOrApprovalPermission()
    {
        var service = new Mock<IVendorInvoiceService>();
        await using var db = Context();
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(item => item.UserId).Returns("not-a-user-id");
        currentUser.Setup(item => item.IsInRole(It.IsAny<string>())).Returns(false);
        var controller = new VendorInvoiceController(service.Object, currentUser.Object, db);

        var response = await controller.ThreeWayMatch(Guid.NewGuid());

        response.Result.Should().BeOfType<ForbidResult>();
        service.VerifyNoOtherCalls();
    }

    private static ApplicationDbContext Context()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"tdc0504-controller-{Guid.NewGuid():N}")
            .Options;
        return new ApplicationDbContext(options);
    }

    private static Mock<ICurrentUserService> PrivilegedCurrentUser()
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.Setup(item => item.IsInRole("TenantAdmin")).Returns(true);
        return currentUser;
    }
}
