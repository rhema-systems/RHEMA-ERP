using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Core.Interfaces.Procurement;
using ErpSystem.Core.Services.Procurement;
using ErpSystem.Data;
using ErpSystem.Data.Repositories.Procurement;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Procurement;

public sealed class BusinessPartnerBankAccountTests
{
    [Fact]
    public async Task DetailReadReturnsAllNormalizedAccountsWithPrimaryFirst()
    {
        var tenantId = Guid.NewGuid();
        var partner = Partner(tenantId);
        partner.BankAccounts.Add(Account(partner, "Secondary Bank", "2002", isPrimary: false));
        partner.BankAccounts.Add(Account(partner, "Primary Bank", "1001", isPrimary: true));

        var (service, repository) = CreateService(partner, tenantId);
        repository.Setup(item => item.GetWithAllRelatedDataAsync(partner.Id)).ReturnsAsync(partner);

        var result = await service.GetByIdAsync(partner.Id);

        result.Should().NotBeNull();
        result!.BankAccounts.Should().HaveCount(2);
        result.BankAccounts.Select(item => item.AccountNumber).Should().Equal("1001", "2002");
        result.BankAccounts[0].IsPrimary.Should().BeTrue();
    }

    [Fact]
    public async Task BankAccountReadFallsBackToLegacyPrimaryFields()
    {
        var tenantId = Guid.NewGuid();
        var partner = Partner(tenantId);
        partner.BankName = "Legacy Bank";
        partner.BankBranch = "Central";
        partner.BankAccountName = "Legacy Supplier";
        partner.BankAccountNumber = "3003";
        partner.BankSwiftCode = "LEGACY01";
        partner.BankIBAN = "LEGACY-IBAN";
        partner.Currency = "GHS";

        var (service, repository) = CreateService(partner, tenantId);
        repository.Setup(item => item.GetWithBankAccountsAsync(partner.Id)).ReturnsAsync(partner);

        var result = (await service.GetBankAccountsAsync(partner.Id)).ToList();

        result.Should().ContainSingle();
        result[0].Id.Should().Be(Guid.Empty);
        result[0].IsPrimary.Should().BeTrue();
        result[0].AccountNumber.Should().Be("3003");
        result[0].BranchName.Should().Be("Central");
    }

    [Fact]
    public async Task ExternalReadReturnsOnlyActiveLinkedPartnerAccountsInCurrentTenant()
    {
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var partner = Partner(tenantId);
        var otherPartner = Partner(otherTenantId);
        partner.BankAccounts.Add(Account(partner, "Visible Bank", "4004", isPrimary: true));
        otherPartner.BankAccounts.Add(Account(otherPartner, "Hidden Bank", "5005", isPrimary: true));

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        await using (var seed = new ApplicationDbContext(options))
        {
            seed.BusinessPartners.AddRange(partner, otherPartner);
            seed.BusinessPartnerUsers.AddRange(
                Link(tenantId, partner.Id, userId),
                Link(otherTenantId, otherPartner.Id, userId));
            await seed.SaveChangesAsync();
        }

        await using var scoped = new ApplicationDbContext(options, tenantId);
        var current = new Mock<ICurrentUserProvider>();
        current.SetupGet(item => item.TenantId).Returns(tenantId);
        current.SetupGet(item => item.UserId).Returns(userId);
        current.SetupGet(item => item.IsExternalUser).Returns(true);
        var repository = new BusinessPartnerRepository(
            scoped,
            current.Object,
            NullLogger<BusinessPartnerRepository>.Instance);

        var visible = await repository.GetWithBankAccountsAsync(partner.Id);
        var hidden = await repository.GetWithBankAccountsAsync(otherPartner.Id);

        visible.Should().NotBeNull();
        visible!.BankAccounts.Should().ContainSingle(item => item.AccountNumber == "4004");
        hidden.Should().BeNull();
    }

    private static (BusinessPartnerService Service, Mock<IBusinessPartnerRepository> Repository)
        CreateService(BusinessPartner partner, Guid tenantId)
    {
        var repository = new Mock<IBusinessPartnerRepository>();
        var current = new Mock<ICurrentUserProvider>();
        current.SetupGet(item => item.TenantId).Returns(tenantId);

        var service = new BusinessPartnerService(
            repository.Object,
            Mock.Of<IBusinessPartnerContactRepository>(),
            current.Object,
            Mock.Of<IWorkflowIntegrationService>(),
            Mock.Of<IWorkflowStatusAdapterRegistry>(),
            Mock.Of<IPaymentTermRepository>(),
            NullLogger<BusinessPartnerService>.Instance,
            Mock.Of<IUnitOfWork>());

        return (service, repository);
    }

    private static BusinessPartner Partner(Guid tenantId) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        PartnerCode = $"SUP-{Guid.NewGuid():N}",
        PartnerName = "Supplier",
        PartnerType = "Supplier",
        RegistrationStatus = "Approved"
    };

    private static BusinessPartnerBankAccount Account(
        BusinessPartner partner,
        string bankName,
        string accountNumber,
        bool isPrimary) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = partner.TenantId,
        BusinessPartnerId = partner.Id,
        BusinessPartner = partner,
        BankName = bankName,
        AccountName = partner.PartnerName,
        AccountNumber = accountNumber,
        Currency = "GHS",
        IsPrimary = isPrimary,
        IsActive = true
    };

    private static BusinessPartnerUser Link(Guid tenantId, Guid partnerId, Guid userId) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        BusinessPartnerId = partnerId,
        UserId = userId,
        Role = "Admin",
        IsActive = true,
        GrantedAt = DateTime.UtcNow
    };
}
