using ErpSystem.Api.Services.Finance.AR;
using ErpSystem.Core.DTOs.AR;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.Finance;

public sealed class CanonicalCustomerProfileReadTests
{
    [Fact]
    public async Task Customer_reads_and_credit_checks_use_effective_approved_profile_not_legacy_defaults()
    {
        await using var fixture = new Fixture();
        await fixture.SeedAsync();

        var customer = await fixture.Service.GetByIdAsync(fixture.Partner.Id);
        var byCode = await fixture.Service.GetByCodeAsync(fixture.Partner.PartnerCode);
        var list = await fixture.Service.GetAllAsync(new CustomerQueryDto());
        var balance = await fixture.Service.GetBalanceAsync(fixture.Partner.Id);
        var credit = await fixture.Service.CheckCreditLimitAsync(fixture.Partner.Id, 501m);

        customer!.CreditLimit.Should().Be(500m);
        customer.PaymentTermId.Should().Be(fixture.Term.Id);
        customer.PaymentTermsDays.Should().Be(45);
        customer.IsActive.Should().BeTrue();
        byCode!.PaymentTermId.Should().Be(fixture.Term.Id);
        list.Items.Single().CreditLimit.Should().Be(500m);
        balance.CreditLimit.Should().Be(500m);
        balance.AvailableCredit.Should().Be(500m);
        credit.IsApproved.Should().BeFalse("legacy 9000 must not authorize more than the approved limit");
    }

    [Theory]
    [InlineData("draft")]
    [InlineData("future")]
    [InlineData("inactive-role")]
    [InlineData("foreign-profile")]
    public async Task Unready_customers_remain_readable_without_eligible_finance_defaults(string reason)
    {
        await using var fixture = new Fixture();
        await fixture.SeedAsync();
        if (reason == "draft") fixture.Profile.Status = BusinessPartnerFinanceProfileStatus.Draft;
        if (reason == "future") fixture.Profile.EffectiveFrom = DateTime.UtcNow.Date.AddDays(1);
        if (reason == "inactive-role") fixture.Role.Status = BusinessPartnerRoleStatus.Inactive;
        if (reason == "foreign-profile") fixture.Profile.TenantId = Guid.NewGuid();
        await fixture.Context.SaveChangesAsync();

        var customer = await fixture.Service.GetByIdAsync(fixture.Partner.Id);
        var balance = await fixture.Service.GetBalanceAsync(fixture.Partner.Id);
        var credit = await fixture.Service.CheckCreditLimitAsync(fixture.Partner.Id, 0m);

        customer.Should().NotBeNull("historical account review remains available");
        customer!.IsActive.Should().BeFalse();
        customer.PaymentTermId.Should().BeNull();
        customer.CreditLimit.Should().Be(0m);
        balance.CreditLimit.Should().Be(0m);
        credit.IsApproved.Should().BeFalse();
        credit.Message.Should().StartWith("AR_");
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly Guid _tenantId = Guid.NewGuid();
        public ApplicationDbContext Context { get; }
        public CustomerService Service { get; }
        public BusinessPartner Partner { get; }
        public BusinessPartnerRole Role { get; }
        public BusinessPartnerArProfileVersion Profile { get; }
        public PaymentTerm Term { get; }

        public Fixture()
        {
            Context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);
            var current = new Mock<ICurrentUserService>();
            current.SetupGet(value => value.TenantId).Returns(_tenantId);
            var settlement = new Mock<ISubledgerSettlementReadModelService>();
            settlement.Setup(value => value.GetBalancesAsync(It.IsAny<string>(), It.IsAny<DateTime>(),
                    It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<SubledgerSettlementBalance>());
            settlement.Setup(value => value.RebuildAsync(It.IsAny<SubledgerSettlementRebuildRequestDto>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SubledgerSettlementRebuildResultDto());
            Service = new CustomerService(new UnitOfWork(Context), current.Object, settlement.Object,
                Mock.Of<IFinanceAccessScopeService>(), NullLogger<CustomerService>.Instance);
            Partner = new BusinessPartner
            {
                Id = Guid.NewGuid(), TenantId = _tenantId, PartnerCode = "CANONICAL-CUSTOMER",
                PartnerName = "Canonical customer", PartnerType = "Customer", IsActive = true,
                RegistrationStatus = "Approved", CreditLimit = 9000m, PaymentTerms = "Net 90",
                PaymentTermId = Guid.NewGuid()
            };
            Role = new BusinessPartnerRole
            {
                Id = Guid.NewGuid(), TenantId = _tenantId, BusinessPartnerId = Partner.Id,
                RoleType = BusinessPartnerRoleType.Customer, Status = BusinessPartnerRoleStatus.Active
            };
            Term = new PaymentTerm
            {
                Id = Guid.NewGuid(), TenantId = _tenantId, Code = "CANONICAL45", Name = "Net 45",
                DueDays = 45, ApplicableTo = "Customer"
            };
            Profile = new BusinessPartnerArProfileVersion
            {
                Id = Guid.NewGuid(), TenantId = _tenantId, BusinessPartnerRoleId = Role.Id,
                Status = BusinessPartnerFinanceProfileStatus.Approved, VersionNumber = 1,
                EffectiveFrom = DateTime.UtcNow.Date.AddDays(-1), PaymentTermId = Term.Id, CreditLimit = 500m
            };
        }

        public async Task SeedAsync()
        {
            Context.BusinessPartners.Add(Partner);
            Context.Set<BusinessPartnerRole>().Add(Role);
            Context.Set<PaymentTerm>().Add(Term);
            Context.Set<BusinessPartnerArProfileVersion>().Add(Profile);
            // A newer approved version which is not effective yet must not supply today's values.
            Context.Set<BusinessPartnerArProfileVersion>().Add(new BusinessPartnerArProfileVersion
            {
                Id = Guid.NewGuid(), TenantId = _tenantId, BusinessPartnerRoleId = Role.Id,
                Status = BusinessPartnerFinanceProfileStatus.Approved, VersionNumber = 2,
                EffectiveFrom = DateTime.UtcNow.Date.AddDays(7), CreditLimit = 7000m
            });
            await Context.SaveChangesAsync();
        }

        public ValueTask DisposeAsync() => Context.DisposeAsync();
    }
}
