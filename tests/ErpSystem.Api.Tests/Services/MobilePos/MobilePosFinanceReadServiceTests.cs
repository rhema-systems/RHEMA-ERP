using ErpSystem.Api.Services.MobilePos;
using ErpSystem.Core.DTOs.AR;
using ErpSystem.Core.DTOs.MobilePos;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.MobilePos;

public sealed class MobilePosFinanceReadServiceTests
{
    private const string InstallationId = "installation-finance-read";

    [Fact]
    public async Task SearchCustomersAsync_ShouldReturnDefaultOrMatchingApprovedTransactionReadyCustomersOnly()
    {
        await using var fixture = Fixture.Create();
        var defaultCustomer = fixture.SeedCustomer("WALK-IN", "Accra Walk-in Customer", approved: true, withProfile: true);
        var selectedCustomer = fixture.SeedCustomer("CUST-002", "Beta Trading", approved: true, withProfile: true);
        selectedCustomer.Partner.RegistrationStatus = "Active";
        fixture.SeedCustomer("CUST-003", "Beta Pending", approved: false, withProfile: true);
        fixture.SeedCustomer("CUST-004", "Beta No Profile", approved: true, withProfile: false);
        fixture.SetDefaultCustomer(defaultCustomer.Partner.Id, defaultCustomer.Role.Id);
        await fixture.Db.SaveChangesAsync();

        var initial = await fixture.Service.SearchCustomersAsync(InstallationId, null, 20, CancellationToken.None);
        var searched = await fixture.Service.SearchCustomersAsync(InstallationId, "beta", 20, CancellationToken.None);

        initial.Should().ContainSingle(item =>
            item.BusinessPartnerId == defaultCustomer.Partner.Id && item.IsDefaultWalkInCustomer);
        searched.Should().ContainSingle(item =>
            item.BusinessPartnerId == selectedCustomer.Partner.Id && !item.IsDefaultWalkInCustomer);
        searched.Should().OnlyContain(item => item.Name == "Beta Trading");
    }

    [Fact]
    public async Task GetOutstandingInvoicesAsync_ShouldValidateTheSelectedCustomerThenUseCanonicalPaymentReadService()
    {
        await using var fixture = Fixture.Create();
        var customer = fixture.SeedCustomer("CUST-100", "Approved Customer", approved: true, withProfile: true);
        fixture.SetDefaultCustomer(customer.Partner.Id, customer.Role.Id);
        await fixture.Db.SaveChangesAsync();
        var expected = new List<OutstandingInvoiceDto>
        {
            new()
            {
                Id = Guid.NewGuid(), InvoiceNumber = "INV-100", InvoiceDate = DateTime.UtcNow.Date,
                TotalAmount = 500m, PaidAmount = 200m, BalanceAmount = 300m, CurrencyCode = "GHS"
            }
        };
        fixture.Payments.Setup(service => service.GetOutstandingInvoicesAsync(customer.Partner.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await fixture.Service.GetOutstandingInvoicesAsync(
            InstallationId, customer.Partner.Id, customer.Role.Id, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        fixture.Payments.Verify(service => service.GetOutstandingInvoicesAsync(
            customer.Partner.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetOutstandingInvoicesAsync_ShouldRejectAnIneligibleCustomerWithoutReadingFinanceData()
    {
        await using var fixture = Fixture.Create();
        var customer = fixture.SeedCustomer("CUST-200", "Pending Customer", approved: false, withProfile: true);
        fixture.SetDefaultCustomer(customer.Partner.Id, customer.Role.Id);
        await fixture.Db.SaveChangesAsync();

        var action = () => fixture.Service.GetOutstandingInvoicesAsync(
            InstallationId, customer.Partner.Id, customer.Role.Id, CancellationToken.None);

        await action.Should().ThrowAsync<KeyNotFoundException>().WithMessage("*not active, approved, and transaction ready*");
        fixture.Payments.Verify(service => service.GetOutstandingInvoicesAsync(
            It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private Fixture(
            ApplicationDbContext db,
            Guid tenantId,
            MobilePosBootstrapDto bootstrap,
            Mock<IPaymentService> payments,
            MobilePosFinanceReadService service)
        {
            Db = db;
            TenantId = tenantId;
            Bootstrap = bootstrap;
            Payments = payments;
            Service = service;
        }

        public ApplicationDbContext Db { get; }
        public Guid TenantId { get; }
        public MobilePosBootstrapDto Bootstrap { get; }
        public Mock<IPaymentService> Payments { get; }
        public MobilePosFinanceReadService Service { get; }

        public static Fixture Create()
        {
            var tenantId = Guid.NewGuid();
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"mobile-pos-finance-read-{Guid.NewGuid():N}")
                .Options);
            var bootstrap = new MobilePosBootstrapDto
            {
                UserId = Guid.NewGuid(),
                Store = new MobilePosStoreDto
                {
                    Id = Guid.NewGuid(), CurrencyCode = "GHS",
                    DefaultWalkInBusinessPartnerId = Guid.NewGuid(),
                    DefaultWalkInBusinessPartnerRoleId = Guid.NewGuid()
                },
                Device = new MobilePosDeviceDto { Id = Guid.NewGuid(), Status = MobilePosDeviceStatus.Active },
                Till = new MobilePosTillDto { Id = Guid.NewGuid() },
                ServerTimeUtc = DateTime.UtcNow
            };
            var foundation = new Mock<IMobilePosFoundationService>();
            foundation.Setup(service => service.GetBootstrapAsync(InstallationId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(bootstrap);
            var currentUser = new Mock<ICurrentUserService>();
            currentUser.SetupGet(service => service.TenantId).Returns(tenantId);
            var payments = new Mock<IPaymentService>();
            var service = new MobilePosFinanceReadService(db, currentUser.Object, foundation.Object, payments.Object);
            return new Fixture(db, tenantId, bootstrap, payments, service);
        }

        public (BusinessPartner Partner, BusinessPartnerRole Role) SeedCustomer(
            string code,
            string name,
            bool approved,
            bool withProfile)
        {
            var partner = new BusinessPartner
            {
                Id = Guid.NewGuid(), TenantId = TenantId, PartnerCode = code, PartnerName = name,
                PartnerType = "Customer", RegistrationStatus = approved ? "Approved" : "Pending",
                ApprovalStatus = approved ? "Approved" : "Pending",
                IsActive = true, IsBlacklisted = false, Currency = "GHS",
                PrimaryEmail = $"{code.ToLowerInvariant()}@example.invalid"
            };
            var role = new BusinessPartnerRole
            {
                Id = Guid.NewGuid(), TenantId = TenantId, BusinessPartnerId = partner.Id,
                BusinessPartner = partner, RoleType = BusinessPartnerRoleType.Customer,
                Status = BusinessPartnerRoleStatus.Active, ActiveFromUtc = DateTime.UtcNow.AddDays(-1)
            };
            Db.BusinessPartners.Add(partner);
            Db.BusinessPartnerRoles.Add(role);
            if (withProfile)
            {
                Db.BusinessPartnerArProfileVersions.Add(new BusinessPartnerArProfileVersion
                {
                    Id = Guid.NewGuid(), TenantId = TenantId, BusinessPartnerRoleId = role.Id,
                    BusinessPartnerRole = role, VersionNumber = 1,
                    Status = BusinessPartnerFinanceProfileStatus.Approved,
                    EffectiveFrom = DateTime.UtcNow.AddDays(-1)
                });
            }
            return (partner, role);
        }

        public void SetDefaultCustomer(Guid partnerId, Guid roleId)
        {
            Bootstrap.Store.DefaultWalkInBusinessPartnerId = partnerId;
            Bootstrap.Store.DefaultWalkInBusinessPartnerRoleId = roleId;
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
