using System.Reflection;
using System.Text.Json;
using ErpSystem.Api.Controllers.Finance;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Procurement;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Controllers.Finance;

public sealed class VendorPaymentSodControllerTests
{
    [Fact]
    public void ManualAndBatchSodRoutesUseExplicitFinancePolicies()
    {
        var submit = typeof(VendorPaymentController).GetMethod(nameof(VendorPaymentController.Submit));
        var paymentReadiness = typeof(VendorPaymentController).GetMethod(nameof(VendorPaymentController.GetPaymentSodReadiness));
        var batchReadiness = typeof(PaymentBatchController).GetMethod(nameof(PaymentBatchController.GetSodReadiness));

        submit!.GetCustomAttribute<HttpPostAttribute>()!.Template.Should().Be("{id}/submit");
        submit.GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(FinancePermissions.ProcessApPayments);
        paymentReadiness!.GetCustomAttribute<HttpGetAttribute>()!.Template.Should().Be("{id}/sod-readiness");
        paymentReadiness.GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(FinancePermissions.ViewFinance);
        batchReadiness!.GetCustomAttribute<AuthorizeAttribute>()!.Policy.Should().Be(FinancePermissions.ViewFinance);
    }

    [Fact]
    public async Task PaymentReadinessReturnsTypedCurrentActorDecision()
    {
        var paymentId = Guid.NewGuid();
        var expected = new ProcurementInvoicePaymentSodReadinessDto
        {
            SourceType = "VendorPayment",
            SourceId = paymentId,
            SourceReference = "VP-0506",
            CurrentActorUserId = Guid.NewGuid(),
            CanApprove = false,
            Code = ProcurementInvoicePaymentSodRules.ConflictCode,
            Message = "Same-user conflict.",
            DecisionKeys = ProcurementInvoicePaymentSodRules.DecisionKeys
        };
        var payments = new Mock<IVendorPaymentService>();
        var sod = new Mock<IProcurementInvoicePaymentSodService>();
        sod.Setup(item => item.GetPaymentReadinessAsync(
                paymentId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        var controller = new VendorPaymentController(payments.Object, sod.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var response = await controller.GetPaymentSodReadiness(paymentId);

        response.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task BatchSameUserConflictReturnsStableUnprocessableEntity()
    {
        var batchId = Guid.NewGuid();
        var payments = new Mock<IVendorPaymentService>();
        var readiness = new ProcurementInvoicePaymentSodReadinessDto
        {
            SourceType = "PaymentBatch",
            SourceId = batchId,
            CanApprove = false,
            Code = ProcurementInvoicePaymentSodRules.ConflictCode,
            Message = "Same-user conflict."
        };
        payments.Setup(item => item.ApprovePaymentBatchAsync(batchId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProcurementInvoicePaymentSodBlockedException(
                ProcurementInvoicePaymentSodRules.ConflictCode,
                readiness.Message,
                readiness));
        var controller = new PaymentBatchController(payments.Object);

        var response = await controller.Approve(batchId);

        var blocked = response.Result.Should().BeOfType<UnprocessableEntityObjectResult>().Subject;
        JsonSerializer.Serialize(blocked.Value).Should().Contain(ProcurementInvoicePaymentSodRules.ConflictCode);
    }

    [Fact]
    public async Task MissingSodServiceFailsClosed()
    {
        var controller = new VendorPaymentController(new Mock<IVendorPaymentService>().Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var response = await controller.GetPaymentSodReadiness(Guid.NewGuid());

        response.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
    }
}
