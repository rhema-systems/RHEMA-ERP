using System.Reflection;
using System.Text.Json;
using ErpSystem.Api.Controllers.Finance;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Services.Procurement;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ErpSystem.Shared;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Finance;

public sealed class VendorPaymentReadinessControllerTests
{
    [Fact]
    public void ReadinessAndMutationRoutesUseExplicitFinancePolicies()
    {
        var readiness = typeof(VendorPaymentController).GetMethod(
            nameof(VendorPaymentController.GetInvoicePaymentReadiness));
        var allocate = typeof(VendorPaymentController).GetMethod(nameof(VendorPaymentController.Allocate));
        var approve = typeof(PaymentBatchController).GetMethod(nameof(PaymentBatchController.Approve));
        var process = typeof(PaymentBatchController).GetMethod(nameof(PaymentBatchController.Process));

        readiness!.GetCustomAttribute<HttpGetAttribute>()!.Template.Should().Be("invoices/{invoiceId}/readiness");
        readiness.GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(FinancePermissions.ViewFinance);
        allocate!.GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(FinancePermissions.ProcessApPayments);
        approve!.GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(FinancePermissions.ApproveApPayments);
        process!.GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(FinancePermissions.ProcessApPayments);
    }

    [Fact]
    public async Task ReadinessReturnsTenantServiceEvaluationWithoutMutation()
    {
        var invoiceId = Guid.NewGuid();
        var expected = new VendorPaymentInvoiceReadinessDto
        {
            VendorInvoiceId = invoiceId,
            InvoiceNumber = "INV-0505",
            IsPaymentReady = false,
            Message = "Current match evidence is unavailable."
        };
        var service = new Mock<IVendorPaymentService>();
        service.Setup(item => item.GetInvoicePaymentReadinessAsync(invoiceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        var controller = new VendorPaymentController(service.Object);

        var response = await controller.GetInvoicePaymentReadiness(invoiceId);

        response.Result.Should().BeOfType<OkObjectResult>()
            .Which.Value.Should().BeSameAs(expected);
        service.Verify(item => item.GetInvoicePaymentReadinessAsync(
            invoiceId, It.IsAny<CancellationToken>()), Times.Once);
        service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AllocationReturnsStableUnprocessableEntityForHardStop()
    {
        var paymentId = Guid.NewGuid();
        var service = new Mock<IVendorPaymentService>();
        service.Setup(item => item.AllocatePaymentAsync(
                paymentId,
                It.IsAny<List<VendorPaymentAllocationCreateDto>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new VendorPaymentControlException(
                "AP_PAYMENT_READINESS_BLOCKED",
                "The invoice is not payment ready."));
        var controller = new VendorPaymentController(service.Object);

        var response = await controller.Allocate(paymentId, []);

        var blocked = response.Result.Should().BeOfType<UnprocessableEntityObjectResult>().Subject;
        blocked.StatusCode.Should().Be(422);
        var json = JsonSerializer.Serialize(blocked.Value);
        json.Should().Contain("AP_PAYMENT_READINESS_BLOCKED");
        json.Should().Contain("not payment ready");
    }

    [Fact]
    public async Task BatchApprovalReturnsStableUnprocessableEntityForStaleReadiness()
    {
        var batchId = Guid.NewGuid();
        var service = new Mock<IVendorPaymentService>();
        service.Setup(item => item.ApprovePaymentBatchAsync(batchId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new VendorPaymentControlException(
                "AP_PAYMENT_BATCH_INVOICE_BLOCKED",
                "One exact invoice selection is no longer payment ready."));
        var controller = new PaymentBatchController(service.Object);

        var response = await controller.Approve(batchId);

        var blocked = response.Result.Should().BeOfType<UnprocessableEntityObjectResult>().Subject;
        JsonSerializer.Serialize(blocked.Value).Should().Contain("AP_PAYMENT_BATCH_INVOICE_BLOCKED");
    }
}
