using System.Text.Json;
using ErpSystem.Api.Services.Sales;
using ErpSystem.Core.DTOs.AR;
using ErpSystem.Core.DTOs.Ehc;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.Sales;
using ErpSystem.Core.Entities.Ehc;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Sales;

public sealed class SalesOrderCustomerDepositServiceTests
{
    [Theory]
    [InlineData("Cash", "GHA-123456789-0")]
    [InlineData("Cheque", "Acme Bank / 0012345678 / CHQ-0042")]
    [InlineData("BankDeposit", "Acme Bank / 0012345678 / DEP-0042")]
    public async Task Deposit_inquiry_preserves_the_exact_tender_reference(
        string tenderType,
        string expectedReference)
    {
        await using var db = Context();
        var tenantId = Guid.NewGuid();
        var order = Order(tenantId, Guid.NewGuid(), Guid.NewGuid());
        var payment = new CustomerPayment
        {
            TenantId = tenantId,
            BusinessPartnerId = order.BusinessPartnerId,
            BusinessPartnerRoleId = Guid.NewGuid(),
            BusinessPartnerArProfileVersionId = Guid.NewGuid(),
            BusinessPartnerCode = "CUS-001",
            BusinessPartnerName = "Estate Customer",
            PaymentNumber = "PAY-001",
            PaymentDate = new DateTime(2026, 10, 8, 10, 30, 0, DateTimeKind.Utc),
            TotalAmount = 2_000m,
            CurrencyCode = "GHS",
            PaymentMethod = tenderType,
            Status = "Posted"
        };
        db.SalesOrders.Add(order);
        db.Set<CustomerPayment>().Add(payment);
        db.SalesOrderCustomerDeposits.Add(new SalesOrderCustomerDeposit
        {
            TenantId = tenantId,
            SalesOrderId = order.Id,
            CustomerPaymentId = payment.Id,
            IdempotencyKey = $"test-{tenderType}",
            TenderType = tenderType,
            IdentificationReference = "GHA-123456789-0",
            ExternalBankName = "Acme Bank",
            ExternalAccountNumber = "0012345678",
            ChequeNumber = "CHQ-0042",
            DepositReference = "DEP-0042",
            PropertyDescription = "Property Deposit - Airport Hills Plot - PLT-007"
        });
        await db.SaveChangesAsync();

        var result = await Service(db, tenantId, Mock.Of<IPaymentService>()).GetAsync(order.Id);

        result.Should().ContainSingle();
        result[0].Reference.Should().Be(expectedReference);
        result[0].PropertyDescription.Should().Be("Property Deposit - Airport Hills Plot - PLT-007");
    }

    [Fact]
    public async Task Cash_deposit_posts_an_unallocated_customer_advance_with_property_lineage()
    {
        await using var db = Context();
        var tenantId = Guid.NewGuid();
        var opportunityId = Guid.NewGuid();
        var partner = ApprovedCustomer(tenantId);
        var order = Order(tenantId, partner.Id, opportunityId);
        var method = new ErpSystem.Core.Entities.Finance.PaymentMethod
        {
            TenantId = tenantId,
            Name = "Cash",
            Code = "CASH",
            Type = PaymentMethodType.Cash,
            IsActive = true,
            RequiresBankAccount = false
        };
        var listing = new EhcPropertyListingContextDto(
            "PublicSite", Guid.NewGuid(), "PLT-007", "Airport Hills Plot", "Sale", "GHS",
            "Accra", 520_000m, Guid.NewGuid(), null, null, "TDC", "Ama Mensah",
            "ama@example.com", "0200000000");
        db.BusinessPartners.Add(partner);
        db.SalesOrders.Add(order);
        db.PaymentMethods.Add(method);
        db.EhcTickets.Add(new EhcTicket
        {
            TenantId = tenantId,
            TicketNumber = "EHC-001",
            TicketType = EhcTicketType.Enquiry,
            Status = EhcTicketStatus.Acknowledged,
            Description = "Property enquiry",
            CrmOpportunityId = opportunityId,
            IdentificationNumber = "GHA-123456789-0",
            PropertyListingContextJson = JsonSerializer.Serialize(listing)
        });
        await db.SaveChangesAsync();

        PaymentCreateDto? captured = null;
        var payments = new Mock<IPaymentService>(MockBehavior.Strict);
        payments.Setup(service => service.CreateAsync(It.IsAny<PaymentCreateDto>(), It.IsAny<CancellationToken>()))
            .Returns<PaymentCreateDto, CancellationToken>(async (request, cancellationToken) =>
            {
                captured = request;
                var payment = new CustomerPayment
                {
                    TenantId = tenantId,
                    BusinessPartnerId = partner.Id,
                    BusinessPartnerRoleId = partner.Roles.Single().Id,
                    BusinessPartnerArProfileVersionId = Guid.NewGuid(),
                    BusinessPartnerCode = partner.PartnerCode,
                    BusinessPartnerName = partner.PartnerName,
                    PaymentNumber = "PAY-002",
                    PaymentDate = request.PaymentDate,
                    TotalAmount = request.TotalAmount,
                    CurrencyCode = request.CurrencyCode,
                    PaymentMethod = request.PaymentMethod,
                    TransactionReference = request.TransactionReference,
                    Status = "Posted",
                    IsCustomerAdvance = true
                };
                var lineage = request.SalesOrderDepositLineage!;
                db.Set<CustomerPayment>().Add(payment);
                db.SalesOrderCustomerDeposits.Add(new SalesOrderCustomerDeposit
                {
                    TenantId = tenantId,
                    SalesOrderId = lineage.SalesOrderId,
                    CustomerPaymentId = payment.Id,
                    IdempotencyKey = lineage.IdempotencyKey,
                    TenderType = lineage.TenderType,
                    IdentificationReference = lineage.IdentificationReference,
                    PropertyDescription = lineage.PropertyDescription
                });
                await db.SaveChangesAsync(cancellationToken);
                return new CustomerPaymentDto
                {
                    Id = payment.Id,
                    PaymentNumber = payment.PaymentNumber,
                    TotalAmount = payment.TotalAmount,
                    CurrencyCode = payment.CurrencyCode,
                    Status = payment.Status
                };
            });
        var service = Service(db, tenantId, payments.Object);

        var result = await service.CreateAsync(order.Id, new CreateSalesOrderCustomerDepositDto
        {
            Amount = 2_000m,
            PaymentMethodId = method.Id,
            IdempotencyKey = "browser-request-001"
        });

        captured.Should().NotBeNull();
        captured!.Allocations.Should().BeEmpty();
        captured.TransactionReference.Should().Be("GHA-123456789-0");
        captured.Notes.Should().Be("Property Deposit - Airport Hills Plot - PLT-007");
        captured.SalesOrderDepositLineage.Should().NotBeNull();
        captured.SalesOrderDepositLineage!.SalesOrderId.Should().Be(order.Id);
        captured.SalesOrderDepositLineage.IdempotencyKey.Should().Be("browser-request-001");
        captured.SalesOrderDepositLineage.TenderType.Should().Be("Cash");
        result.Reference.Should().Be("GHA-123456789-0");
        result.PropertyDescription.Should().Be("Property Deposit - Airport Hills Plot - PLT-007");
        payments.VerifyAll();
    }

    [Fact]
    public async Task Deposit_is_rejected_before_finance_when_customer_is_not_approved()
    {
        await using var db = Context();
        var tenantId = Guid.NewGuid();
        var partner = ApprovedCustomer(tenantId);
        partner.ApprovalStatus = "Pending";
        var order = Order(tenantId, partner.Id, Guid.NewGuid());
        db.BusinessPartners.Add(partner);
        db.SalesOrders.Add(order);
        await db.SaveChangesAsync();
        var payments = new Mock<IPaymentService>(MockBehavior.Strict);

        var action = () => Service(db, tenantId, payments.Object).CreateAsync(order.Id, new()
        {
            Amount = 1_000m,
            PaymentMethodId = Guid.NewGuid(),
            IdempotencyKey = "rejected-customer"
        });

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*approval of the active Customer Business Partner*");
        payments.VerifyNoOtherCalls();
    }

    private static SalesOrderCustomerDepositService Service(
        ApplicationDbContext db,
        Guid tenantId,
        IPaymentService payments)
    {
        var actor = new Mock<ICurrentUserProvider>();
        actor.SetupGet(value => value.TenantId).Returns(tenantId);
        return new SalesOrderCustomerDepositService(db, actor.Object, payments);
    }

    private static ApplicationDbContext Context() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"sales-order-deposits-{Guid.NewGuid():N}")
            .Options);

    private static BusinessPartner ApprovedCustomer(Guid tenantId)
    {
        var partner = new BusinessPartner
        {
            TenantId = tenantId,
            PartnerCode = "CUS-001",
            PartnerName = "Estate Customer",
            PartnerType = "Customer",
            ApprovalStatus = "Approved",
            IsActive = true
        };
        partner.Roles.Add(new BusinessPartnerRole
        {
            TenantId = tenantId,
            BusinessPartnerId = partner.Id,
            RoleType = BusinessPartnerRoleType.Customer,
            Status = BusinessPartnerRoleStatus.Active,
            ActiveFromUtc = DateTime.UtcNow.AddDays(-1)
        });
        return partner;
    }

    private static SalesOrder Order(Guid tenantId, Guid partnerId, Guid opportunityId) => new()
    {
        TenantId = tenantId,
        BusinessPartnerId = partnerId,
        CustomerName = "Estate Customer",
        DocumentNumber = "SO-000001",
        EffectiveDate = new DateTime(2026, 10, 8),
        OpportunityId = opportunityId,
        PropertyReference = "PLT-007",
        Currency = "GHS",
        OrderStatus = SalesOrderStatus.Draft
    };
}
