using ErpSystem.Api.Services.Ehc;
using ErpSystem.Core.DTOs.AR;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Ehc;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Ehc;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Ehc;

public sealed class PropertyEnquiryDepositApplicationTests
{
    [Fact]
    public async Task Unconverted_public_prospect_deposit_is_not_applied()
    {
        await using var db = Context();
        var fixture = Seed(db, EhcPropertyProspectStatuses.Opportunity, customerApproved: false);
        await db.SaveChangesAsync();
        var payments = new Mock<IPaymentService>(MockBehavior.Strict);
        var service = Service(db, fixture.TenantId, payments.Object);

        var result = await service.ApplyToPostedSalesInvoiceAsync(fixture.OrderId, fixture.InvoiceId);

        result.Should().Be(ProspectDepositApplicationResult.None);
        payments.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(100, 250, 100)]
    [InlineData(100, 60, 60)]
    public async Task Converted_customer_advance_is_applied_to_matching_posted_sales_invoice(
        decimal advanceAmount,
        decimal invoiceBalance,
        decimal expectedApplication)
    {
        await using var db = Context();
        var fixture = Seed(db, EhcPropertyProspectStatuses.Converted, customerApproved: true,
            advanceAmount, invoiceBalance);
        await db.SaveChangesAsync();
        PaymentAllocation_CreateDto? allocation = null;
        var payments = new Mock<IPaymentService>(MockBehavior.Strict);
        payments.Setup(service => service.GetOutstandingInvoicesAsync(fixture.BusinessPartnerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new OutstandingInvoiceDto
                {
                    Id = fixture.InvoiceId,
                    BalanceAmount = invoiceBalance,
                    CurrencyCode = "GHS"
                }
            ]);
        payments.Setup(service => service.AllocatePaymentAsync(It.IsAny<PaymentAllocation_CreateDto>(), It.IsAny<CancellationToken>()))
            .Callback<PaymentAllocation_CreateDto, CancellationToken>((request, _) => allocation = request)
            .ReturnsAsync(new PaymentAllocationResultDto { Success = true, TotalAllocated = expectedApplication });
        var service = Service(db, fixture.TenantId, payments.Object);

        var result = await service.ApplyToPostedSalesInvoiceAsync(fixture.OrderId, fixture.InvoiceId);

        result.AppliedReceiptCount.Should().Be(1);
        result.AppliedAmount.Should().Be(expectedApplication);
        allocation.Should().NotBeNull();
        allocation!.CustomerPaymentId.Should().Be(fixture.CustomerPaymentId);
        allocation.Allocations.Should().ContainSingle();
        allocation.Allocations[0].InvoiceId.Should().Be(fixture.InvoiceId);
        allocation.Allocations[0].AllocatedAmount.Should().Be(expectedApplication);
        allocation.Allocations[0].PaymentCurrencyAmount.Should().Be(expectedApplication);
        payments.VerifyAll();
    }

    private static PropertyEnquiryDepositApplicationService Service(
        ApplicationDbContext db,
        Guid tenantId,
        IPaymentService payments)
    {
        var actor = new Mock<ICurrentUserProvider>();
        actor.SetupGet(value => value.TenantId).Returns(tenantId);
        return new(db, actor.Object, payments, NullLogger<PropertyEnquiryDepositApplicationService>.Instance);
    }

    private static ApplicationDbContext Context() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"property-deposit-application-{Guid.NewGuid():N}")
            .Options);

    private static Fixture Seed(
        ApplicationDbContext db,
        string prospectStatus,
        bool customerApproved,
        decimal advanceAmount = 100m,
        decimal invoiceBalance = 250m)
    {
        var tenantId = Guid.NewGuid();
        var businessPartner = new BusinessPartner
        {
            TenantId = tenantId,
            PartnerCode = "CUS-001",
            PartnerName = "Public Enquirer",
            PartnerType = "Customer",
            ApprovalStatus = customerApproved ? "Approved" : "Pending",
            IsActive = true
        };
        var opportunityId = Guid.NewGuid();
        var invoice = new Invoice
        {
            TenantId = tenantId,
            BusinessPartnerId = businessPartner.Id,
            InvoiceNumber = "INV-PROP-001",
            CustomerName = businessPartner.PartnerName,
            InvoiceDate = new DateTime(2026, 10, 1),
            TotalAmount = invoiceBalance,
            CurrencyCode = "GHS",
            Status = InvoiceStatus.Sent,
            JournalEntryId = Guid.NewGuid()
        };
        var order = new SalesOrder
        {
            TenantId = tenantId,
            BusinessPartnerId = businessPartner.Id,
            DocumentNumber = "SO-PROP-001",
            Currency = "GHS",
            OpportunityId = opportunityId,
            InvoiceId = invoice.Id
        };
        var prospect = new EhcPropertyEnquiryProspect
        {
            TenantId = tenantId,
            TicketId = Guid.NewGuid(),
            LeadId = Guid.NewGuid(),
            OpportunityId = opportunityId,
            BusinessPartnerId = businessPartner.Id,
            Status = prospectStatus,
            Currency = "GHS"
        };
        var payment = new CustomerPayment
        {
            TenantId = tenantId,
            PaymentNumber = "PDR-001",
            BusinessPartnerId = businessPartner.Id,
            BusinessPartnerCode = businessPartner.PartnerCode,
            BusinessPartnerName = businessPartner.PartnerName,
            TotalAmount = advanceAmount,
            AllocatedAmount = 0m,
            IsCustomerAdvance = true,
            CurrencyCode = "GHS",
            Status = "Cleared",
            JournalEntryId = Guid.NewGuid()
        };
        var receipt = new ProspectDepositReceipt
        {
            TenantId = tenantId,
            ProspectId = prospect.Id,
            TicketId = prospect.TicketId,
            LeadId = prospect.LeadId,
            OpportunityId = opportunityId,
            BusinessPartnerId = businessPartner.Id,
            ReceiptNumber = payment.PaymentNumber,
            Amount = advanceAmount,
            Currency = "GHS",
            Status = ProspectDepositReceiptStatuses.Cleared,
            ClearedAt = DateTime.UtcNow.AddDays(-1),
            PostingEventId = Guid.NewGuid(),
            JournalEntryId = Guid.NewGuid(),
            CustomerPaymentId = payment.Id,
            TransferredToCustomerAdvanceAt = DateTime.UtcNow
        };
        db.AddRange(businessPartner, invoice, order, prospect, payment, receipt);
        return new(tenantId, businessPartner.Id, order.Id, invoice.Id, payment.Id);
    }

    private sealed record Fixture(
        Guid TenantId,
        Guid BusinessPartnerId,
        Guid OrderId,
        Guid InvoiceId,
        Guid CustomerPaymentId);
}
