using ErpSystem.Api.Services.MobilePos;
using ErpSystem.Core.Entities;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.MobilePos;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.MobilePos;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.MobilePos;

public sealed class MobilePosCheckoutReadServiceTests
{
    [Fact]
    public async Task SearchCatalogueAsync_ShouldReturnOnlySaleReadyMatchingItems()
    {
        await using var fixture = await Fixture.CreateAsync();

        var results = await fixture.Service.SearchCatalogueAsync(
            "install-01", "service-01", 20, CancellationToken.None);

        results.Should().ContainSingle();
        results[0].InventoryItemId.Should().Be(fixture.ItemId);
        results[0].UnitPrice.Should().Be(100m);
        results[0].CurrencyCode.Should().Be("GHS");
        results[0].IsAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task PreviewAsync_ShouldUseDefaultCustomerAndServerTaxCalculation()
    {
        await using var fixture = await Fixture.CreateAsync();
        var lineId = Guid.NewGuid();

        var result = await fixture.Service.PreviewAsync(new MobilePosSalePreviewRequestDto
        {
            InstallationId = "install-01",
            Lines =
            [
                new MobilePosSalePreviewLineInputDto
                {
                    ClientLineId = lineId,
                    InventoryItemId = fixture.ItemId,
                    Quantity = 1m,
                    DiscountPercentage = 10m
                }
            ]
        }, CancellationToken.None);

        result.UsedStoreDefaultCustomer.Should().BeTrue();
        result.CustomerName.Should().Be("Default Shop Customer");
        result.SubTotal.Should().Be(90m);
        result.DiscountAmount.Should().Be(10m);
        result.TaxAmount.Should().Be(13.5m);
        result.TotalAmount.Should().Be(103.5m);
        result.Lines.Should().ContainSingle(line => line.ClientLineId == lineId
            && line.UnitPrice == 100m
            && line.NetAmount == 90m
            && line.TaxAmount == 13.5m);
        fixture.Taxes.Verify(service => service.CalculateDocumentTaxesAsync(
            It.Is<TaxDocumentCalculationRequestDto>(request =>
                request.BusinessPartnerId == fixture.PartnerId
                && request.CurrencyCode == "GHS"
                && request.Lines.Count == 1
                && request.Lines[0].DocumentLineId == lineId
                && request.Lines[0].BaseAmount == 90m),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PreviewAsync_ShouldRejectDiscountWithoutDynamicPermission()
    {
        await using var fixture = await Fixture.CreateAsync(grantDiscountPermission: false);

        var action = () => fixture.Service.PreviewAsync(new MobilePosSalePreviewRequestDto
        {
            InstallationId = "install-01",
            Lines =
            [
                new MobilePosSalePreviewLineInputDto
                {
                    ClientLineId = Guid.NewGuid(),
                    InventoryItemId = fixture.ItemId,
                    Quantity = 1m,
                    DiscountPercentage = 1m
                }
            ]
        }, CancellationToken.None);

        await action.Should().ThrowAsync<MobilePosCommandRejectedException>()
            .Where(exception => exception.Code == "MOBILE_POS_DISCOUNT_NOT_AUTHORIZED");
        fixture.Taxes.Verify(service => service.CalculateDocumentTaxesAsync(
            It.IsAny<TaxDocumentCalculationRequestDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetEligibleBankAccountsAsync_ShouldReturnOnlyScopedActiveMappedStoreCurrencyAccounts()
    {
        await using var fixture = await Fixture.CreateAsync();

        var results = await fixture.Service.GetEligibleBankAccountsAsync(
            "install-01", CancellationToken.None);

        results.Should().ContainSingle();
        results[0].BankAccountId.Should().Be(fixture.BankAccountId);
        results[0].MaskedAccountNumber.Should().Be("**** 7890");
        results[0].CurrencyCode.Should().Be("GHS");
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private Fixture(
            ApplicationDbContext db,
            MobilePosCheckoutReadService service,
            Mock<ITaxCalculationEngine> taxes,
            Guid itemId,
            Guid partnerId,
            Guid bankAccountId)
        {
            Db = db;
            Service = service;
            Taxes = taxes;
            ItemId = itemId;
            PartnerId = partnerId;
            BankAccountId = bankAccountId;
        }

        public ApplicationDbContext Db { get; }
        public MobilePosCheckoutReadService Service { get; }
        public Mock<ITaxCalculationEngine> Taxes { get; }
        public Guid ItemId { get; }
        public Guid PartnerId { get; }
        public Guid BankAccountId { get; }

        public static async Task<Fixture> CreateAsync(bool grantDiscountPermission = true)
        {
            var tenantId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var storeId = Guid.NewGuid();
            var tillId = Guid.NewGuid();
            var deviceId = Guid.NewGuid();
            var sessionId = Guid.NewGuid();
            var partnerId = Guid.NewGuid();
            var roleId = Guid.NewGuid();
            var itemId = Guid.NewGuid();
            var bankAccountId = Guid.NewGuid();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"mobile-pos-checkout-read-{Guid.NewGuid():N}")
                .Options;
            var db = new ApplicationDbContext(options);
            var now = DateTime.UtcNow;
            var partner = new BusinessPartner
            {
                Id = partnerId,
                TenantId = tenantId,
                PartnerCode = "WALK-IN",
                PartnerName = "Default Shop Customer",
                PartnerType = "Customer",
                RegistrationStatus = "Approved",
                ApprovalStatus = "Approved",
                IsActive = true,
                Currency = "GHS"
            };
            var role = new BusinessPartnerRole
            {
                Id = roleId,
                TenantId = tenantId,
                BusinessPartnerId = partnerId,
                BusinessPartner = partner,
                RoleType = BusinessPartnerRoleType.Customer,
                Status = BusinessPartnerRoleStatus.Active,
                ActiveFromUtc = now.AddDays(-1)
            };
            db.BusinessPartners.Add(partner);
            db.BusinessPartnerRoles.Add(role);
            db.BusinessPartnerArProfileVersions.Add(new BusinessPartnerArProfileVersion
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                BusinessPartnerRoleId = roleId,
                BusinessPartnerRole = role,
                VersionNumber = 1,
                Status = BusinessPartnerFinanceProfileStatus.Approved,
                EffectiveFrom = now.AddDays(-1)
            });
            db.MobilePosStores.Add(new MobilePosStore
            {
                Id = storeId,
                TenantId = tenantId,
                Code = "SHOP-01",
                Name = "Shop 01",
                Status = MobilePosStoreStatus.Active,
                LocationId = Guid.NewGuid(),
                CurrencyCode = "GHS",
                DefaultWalkInBusinessPartnerId = partnerId,
                DefaultWalkInBusinessPartnerRoleId = roleId
            });
            db.InventoryItems.AddRange(
                new InventoryItem
                {
                    Id = itemId,
                    TenantId = tenantId,
                    ItemCode = "SERVICE-01",
                    Name = "Service item",
                    CategoryId = Guid.NewGuid(),
                    UnitOfMeasure = "EA",
                    SalePrice = 100m,
                    SalesAccountId = Guid.NewGuid(),
                    ItemType = ItemType.Service,
                    Status = ItemStatus.Active
                },
                new InventoryItem
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ItemCode = "SERVICE-OLD",
                    Name = "Inactive service",
                    CategoryId = Guid.NewGuid(),
                    UnitOfMeasure = "EA",
                    SalePrice = 50m,
                    SalesAccountId = Guid.NewGuid(),
                    ItemType = ItemType.Service,
                    Status = ItemStatus.Inactive
                });
            db.BankAccounts.AddRange(
                new ErpSystem.Core.Entities.Finance.BankAccount
                {
                    Id = bankAccountId, TenantId = tenantId, AccountNumber = "01234567890",
                    AccountName = "Main collections", BankName = "Ghana Bank", Currency = "GHS",
                    GLAccountId = Guid.NewGuid(), IsActive = true
                },
                new ErpSystem.Core.Entities.Finance.BankAccount
                {
                    Id = Guid.NewGuid(), TenantId = tenantId, AccountNumber = "1111",
                    AccountName = "Inactive", BankName = "Ghana Bank", Currency = "GHS",
                    GLAccountId = Guid.NewGuid(), IsActive = false
                },
                new ErpSystem.Core.Entities.Finance.BankAccount
                {
                    Id = Guid.NewGuid(), TenantId = tenantId, AccountNumber = "2222",
                    AccountName = "Wrong currency", BankName = "Ghana Bank", Currency = "USD",
                    GLAccountId = Guid.NewGuid(), IsActive = true
                },
                new ErpSystem.Core.Entities.Finance.BankAccount
                {
                    Id = Guid.NewGuid(), TenantId = tenantId, AccountNumber = "3333",
                    AccountName = "Unmapped", BankName = "Ghana Bank", Currency = "GHS",
                    IsActive = true
                });
            if (grantDiscountPermission)
            {
                var discountRole = new ApplicationRole("Configurable POS cashier")
                {
                    Id = Guid.NewGuid(),
                    NormalizedName = "CONFIGURABLE POS CASHIER"
                };
                var discountPermission = new Permission
                {
                    Id = Guid.NewGuid(),
                    Name = MobilePosPermissions.ApplyDiscount,
                    DisplayName = "Apply Mobile POS Discount",
                    Category = MobilePosPermissions.CategoryTransactions,
                    IsSystemPermission = true
                };
                db.Roles.Add(discountRole);
                db.Permissions.Add(discountPermission);
                db.UserRoles.Add(new ApplicationUserRole { UserId = userId, RoleId = discountRole.Id });
                db.RolePermissions.Add(new RolePermission
                {
                    RoleId = discountRole.Id,
                    PermissionId = discountPermission.Id,
                    GrantedBy = "Tests"
                });
            }
            await db.SaveChangesAsync();

            var currentUser = new Mock<ICurrentUserService>();
            currentUser.SetupGet(service => service.TenantId).Returns(tenantId);
            currentUser.SetupGet(service => service.UserId).Returns(userId.ToString());
            var foundation = new Mock<IMobilePosFoundationService>();
            foundation.Setup(service => service.GetBootstrapAsync("install-01", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new MobilePosBootstrapDto
                {
                    UserId = userId,
                    Device = new MobilePosDeviceDto { Id = deviceId, Status = MobilePosDeviceStatus.Active },
                    Store = new MobilePosStoreDto
                    {
                        Id = storeId,
                        Code = "SHOP-01",
                        CurrencyCode = "GHS",
                        DefaultWalkInBusinessPartnerId = partnerId,
                        DefaultWalkInBusinessPartnerRoleId = roleId
                    },
                    Till = new MobilePosTillDto { Id = tillId, TillNumber = "TILL-01" },
                    CurrentTillSessionId = sessionId,
                    ServerTimeUtc = now
                });
            var taxes = new Mock<ITaxCalculationEngine>();
            taxes.Setup(service => service.CalculateDocumentTaxesAsync(
                    It.IsAny<TaxDocumentCalculationRequestDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((TaxDocumentCalculationRequestDto request, CancellationToken _) => new TaxCalculationResultDto
                {
                    CurrencyCode = request.CurrencyCode,
                    CurrencyDecimalPlaces = 2,
                    BaseAmount = request.Lines.Sum(line => line.BaseAmount),
                    TotalTaxAmount = 13.5m,
                    GrandTotal = 103.5m,
                    TaxBreakdowns =
                    [
                        new TaxBreakdownDto
                        {
                            DocumentLineId = request.Lines[0].DocumentLineId,
                            TaxId = Guid.NewGuid(),
                            TaxCode = "VAT",
                            TaxName = "VAT",
                            TaxRate = 15m,
                            TaxAmount = 13.5m,
                            TaxableAmount = 90m
                        }
                    ]
                });
            var financeAccess = new Mock<IFinanceAccessScopeService>();
            financeAccess.Setup(service => service.GetPermittedBankAccountIdsAsync(
                    FinanceAccessLevel.Operate, It.IsAny<CancellationToken>()))
                .ReturnsAsync([bankAccountId]);
            var service = new MobilePosCheckoutReadService(
                db, currentUser.Object, foundation.Object, taxes.Object, financeAccess.Object);
            return new Fixture(db, service, taxes, itemId, partnerId, bankAccountId);
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
