using ErpSystem.Api.Controllers.Estate;
using ErpSystem.Core.DTOs.AR;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Estate;
using ErpSystem.Core.Finance.Integration;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Estate;

public sealed class FacilitiesArBillingControllerTests
{
    [Fact]
    public async Task RejectsInvoiceForAnotherPropertyCustomer()
    {
        var tenantId = Guid.NewGuid();
        await using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            tenantId);
        db.EstateManagedAssets.Add(new EstateManagedAsset
        {
            TenantId = tenantId, AssetCode = "UNIT-001", Name = "Unit 1",
            CustomerBusinessPartnerId = Guid.NewGuid()
        });
        await db.SaveChangesAsync();
        var invoices = new Mock<IInvoiceService>();
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(service => service.TenantId).Returns(tenantId);
        var controller = new FacilitiesArBillingController(
            invoices.Object, new Mock<IPaymentService>().Object,
            new Mock<INotificationService>().Object, user.Object, db);

        var result = await controller.CreateInvoice(
            new EstateFacilitiesArInvoiceRequest(
                new InvoiceCreateDto { CustomerId = Guid.NewGuid() }, null, "UNIT-001"),
            CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
        invoices.Verify(service => service.CreateAsync(
            It.IsAny<InvoiceCreateDto>(), It.IsAny<FinancePostingProducerContext>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(false, "Sent")]
    [InlineData(true, "Draft")]
    public async Task InvoiceReleaseRespectsFinanceApproval(bool approvalRequired, string expectedStatus)
    {
        var tenantId = Guid.NewGuid();
        await using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            tenantId);
        var invoice = new InvoiceDto
        {
            Id = Guid.NewGuid(), InvoiceNumber = "FAC-001", CustomerId = Guid.NewGuid(),
            CustomerName = "Customer", TotalAmount = 100m, CurrencyCode = "GHS", Status = "Draft"
        };
        db.EstateManagedAssets.Add(new EstateManagedAsset
        {
            TenantId = tenantId, AssetCode = "UNIT-001", ProjectUnitCode = "APT-101", Name = "Unit 1",
            CustomerBusinessPartnerId = invoice.CustomerId
        });
        await db.SaveChangesAsync();
        var invoices = new Mock<IInvoiceService>();
        invoices.Setup(service => service.CreateAsync(It.IsAny<InvoiceCreateDto>(),
                It.IsAny<FinancePostingProducerContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoice);
        if (approvalRequired)
        {
            invoices.Setup(service => service.SendInvoiceAsync(invoice.Id,
                    It.IsAny<FinancePostingProducerContext>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Submit the invoice to its active approval process before release."));
        }
        else
        {
            invoices.Setup(service => service.SendInvoiceAsync(invoice.Id,
                    It.IsAny<FinancePostingProducerContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new InvoiceDto
                {
                    Id = invoice.Id, InvoiceNumber = invoice.InvoiceNumber, CustomerId = invoice.CustomerId,
                    CustomerName = invoice.CustomerName, TotalAmount = invoice.TotalAmount,
                    CurrencyCode = invoice.CurrencyCode, Status = "Sent"
                });
        }
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(service => service.TenantId).Returns(tenantId);
        var controller = new FacilitiesArBillingController(
            invoices.Object, new Mock<IPaymentService>().Object,
            new Mock<INotificationService>().Object, user.Object, db);

        var result = await controller.CreateInvoice(
            new EstateFacilitiesArInvoiceRequest(
                new InvoiceCreateDto { CustomerId = invoice.CustomerId, Reference = "SERVICE" },
                null, "UNIT-001"),
            CancellationToken.None);

        var returned = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        returned.Value.Should().BeOfType<InvoiceDto>().Subject.Status.Should().Be(expectedStatus);
        invoices.Verify(service => service.CreateAsync(
            It.Is<InvoiceCreateDto>(item => item.Reference == "SERVICE UNIT-001 APT-101"
                && item.Notes != null && item.Notes.Contains("Estate / Facilities")),
            It.Is<FinancePostingProducerContext>(producer => producer.RouteId == FinanceDimensionRouteId.FinanceArCustomerInvoice),
            It.IsAny<CancellationToken>()), Times.Once);
        invoices.Verify(service => service.SendInvoiceAsync(invoice.Id,
            It.Is<FinancePostingProducerContext>(producer => producer.RouteId == FinanceDimensionRouteId.FinanceArCustomerInvoice),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task ReleaseRequiresTheFacilitiesPropertyAndCompletedFinanceApproval(
        bool approvalIncomplete, bool expectedRelease)
    {
        var tenantId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        await using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, tenantId);
        db.EstateManagedAssets.Add(new EstateManagedAsset
        {
            TenantId = tenantId, AssetCode = "FAC-UNIT", Name = "Test unit",
            CustomerBusinessPartnerId = customerId
        });
        await db.SaveChangesAsync();
        var invoice = new InvoiceDto
        {
            Id = invoiceId, InvoiceNumber = "INV-FAC", CustomerId = customerId,
            Reference = "SERVICE FAC-UNIT", Notes = "Source: Estate / Facilities -> Finance AR",
            Status = "PendingApproval"
        };
        var invoices = new Mock<IInvoiceService>();
        invoices.Setup(service => service.GetByIdAsync(invoiceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoice);
        if (approvalIncomplete)
            invoices.Setup(service => service.SendInvoiceAsync(invoiceId,
                    It.IsAny<FinancePostingProducerContext>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("The invoice approval process is not complete."));
        else
            invoices.Setup(service => service.SendInvoiceAsync(invoiceId,
                    It.IsAny<FinancePostingProducerContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new InvoiceDto
                {
                    Id = invoiceId, InvoiceNumber = invoice.InvoiceNumber, CustomerId = customerId,
                    Status = "Sent"
                });
        var user = new Mock<ICurrentUserService>();
        user.SetupGet(service => service.TenantId).Returns(tenantId);
        var controller = new FacilitiesArBillingController(
            invoices.Object, new Mock<IPaymentService>().Object,
            new Mock<INotificationService>().Object, user.Object, db);

        var result = await controller.ReleaseInvoice(
            invoiceId, new EstateFacilitiesReleaseInvoiceRequest("FAC-UNIT"), CancellationToken.None);

        if (expectedRelease)
            result.Result.Should().BeOfType<OkObjectResult>();
        else
            result.Result.Should().BeOfType<ConflictObjectResult>();
        invoices.Verify(service => service.SendInvoiceAsync(invoiceId,
            It.Is<FinancePostingProducerContext>(producer => producer.RouteId == FinanceDimensionRouteId.FinanceArCustomerInvoice),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
