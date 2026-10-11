using System.Linq.Expressions;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Sales;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Numbering;
using ErpSystem.Core.Services.Sales;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ErpSystem.Core.Tests.Services.Sales;

public sealed class SalesOrderCreditAuthorityTests
{
    [Fact]
    public async Task Legacy_business_partner_limit_cannot_authorize_order_above_approved_ar_profile_limit()
    {
        var (service, partnerId) = CreateService(500m);

        var allowed = await service.ValidateCreditLimitAsync(partnerId, 501m);

        allowed.Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(NonEnforcedCreditLimits))]
    public async Task Null_or_zero_approved_ar_profile_limit_authorizes_unlimited_credit(decimal? creditLimit)
    {
        var (service, partnerId) = CreateService(creditLimit);

        var allowed = await service.ValidateCreditLimitAsync(partnerId, 10_000_000m);

        allowed.Should().BeTrue();
    }

    public static TheoryData<decimal?> NonEnforcedCreditLimits => new()
    {
        null,
        0m
    };

    private static (SalesOrderService Service, Guid PartnerId) CreateService(decimal? profileCreditLimit)
    {
        var tenantId = Guid.NewGuid();
        var partnerId = Guid.NewGuid();
        var role = new BusinessPartnerRole
        {
            TenantId = tenantId,
            BusinessPartnerId = partnerId,
            RoleType = BusinessPartnerRoleType.Customer,
            Status = BusinessPartnerRoleStatus.Active
        };
        var partner = new BusinessPartner
        {
            Id = partnerId,
            TenantId = tenantId,
            PartnerCode = "CUS-001",
            PartnerName = "Canonical customer",
            RegistrationStatus = "Approved",
            ApprovalStatus = "Approved",
            IsActive = true,
            CreditLimit = 1_000_000m,
            OutstandingBalance = 0m
        };
        var profile = new BusinessPartnerArProfileVersion
        {
            TenantId = tenantId,
            BusinessPartnerRoleId = role.Id,
            VersionNumber = 1,
            Status = BusinessPartnerFinanceProfileStatus.Approved,
            EffectiveFrom = DateTime.UtcNow.Date.AddDays(-1),
            CreditLimit = profileCreditLimit
        };

        var partnerRepository = new Mock<IGenericRepository<BusinessPartner>>();
        partnerRepository.Setup(item => item.GetByIdAsync(partnerId)).ReturnsAsync(partner);
        var roleRepository = new Mock<IGenericRepository<BusinessPartnerRole>>();
        roleRepository
            .Setup(item => item.FindAsync(It.IsAny<Expression<Func<BusinessPartnerRole, bool>>>()))
            .ReturnsAsync(new[] { role });
        var profileRepository = new Mock<IGenericRepository<BusinessPartnerArProfileVersion>>();
        profileRepository
            .Setup(item => item.FindAsync(It.IsAny<Expression<Func<BusinessPartnerArProfileVersion, bool>>>()))
            .ReturnsAsync(new[] { profile });
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(item => item.Repository<BusinessPartnerRole>()).Returns(roleRepository.Object);
        unitOfWork.Setup(item => item.Repository<BusinessPartnerArProfileVersion>()).Returns(profileRepository.Object);
        var currentUser = new Mock<ICurrentUserProvider>();
        currentUser.SetupGet(item => item.TenantId).Returns(tenantId);

        var service = new SalesOrderService(
            Mock.Of<IGenericRepository<SalesOrder>>(),
            Mock.Of<IGenericRepository<SalesOrderLine>>(),
            Mock.Of<IGenericRepository<SalesOrderStatusHistory>>(),
            partnerRepository.Object,
            Mock.Of<IGenericRepository<Quote>>(),
            Mock.Of<IGenericRepository<PaymentTerm>>(),
            unitOfWork.Object,
            currentUser.Object,
            Mock.Of<IDocumentNumberingService>(),
            Mock.Of<IWorkflowIntegrationService>(),
            Mock.Of<IWorkflowStatusAdapterRegistry>(),
            NullLogger<SalesOrderService>.Instance);

        return (service, partnerId);
    }
}
