using ErpSystem.Api.Services.MobilePos;
using ErpSystem.Core.DTOs.MobilePos;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.HR;
using ErpSystem.Core.Entities.MobilePos;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.MobilePos;

public sealed class MobilePosFoundationServiceTests
{
    [Fact]
    public async Task SaveStoreAsync_ShouldRejectAnUnapprovedDefaultWalkInCustomer()
    {
        await using var fixture = Fixture.Create();
        var customer = fixture.SeedCustomer("Pending");
        await fixture.Db.SaveChangesAsync();

        var action = () => fixture.Service.SaveStoreAsync(null, fixture.StoreInput(customer), CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*must be active, approved, and not blacklisted*");
        (await fixture.Db.MobilePosStores.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task SaveStoreAsync_ShouldPersistTheApprovedDefaultWalkInCustomerAndAuditEvidence()
    {
        await using var fixture = Fixture.Create();
        var customer = fixture.SeedCustomer("Approved");
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.Service.SaveStoreAsync(null, fixture.StoreInput(customer), CancellationToken.None);

        result.Status.Should().Be(MobilePosStoreStatus.Active);
        result.DefaultWalkInBusinessPartnerId.Should().Be(customer.Partner.Id);
        result.DefaultWalkInBusinessPartnerRoleId.Should().Be(customer.Role.Id);
        result.DefaultWalkInCustomerCode.Should().Be("WALK-IN");
        result.DefaultWalkInCustomerName.Should().Be("Default shop customer");
        (await fixture.Db.AuditLogs.SingleAsync()).Action.Should().Be("MobilePOS.Store.Created");
    }

    [Fact]
    public async Task SaveUserStoreAssignmentAsync_ShouldCloseThePreviousAssignment()
    {
        await using var fixture = Fixture.Create();
        var firstStore = fixture.SeedActiveStore("STORE-A");
        var secondStore = fixture.SeedActiveStore("STORE-B");
        fixture.Db.Users.Add(new ApplicationUser
        {
            Id = fixture.ActorId,
            TenantId = fixture.TenantId,
            UserName = "mobile.cashier",
            NormalizedUserName = "MOBILE.CASHIER",
            Email = "cashier@example.invalid",
            IsActive = true
        });
        await fixture.Db.SaveChangesAsync();

        await fixture.Service.SaveUserStoreAssignmentAsync(new MobilePosUserStoreAssignmentUpsertDto
        {
            UserId = fixture.ActorId,
            MobilePosStoreId = firstStore.Id,
            EffectiveFromUtc = DateTime.UtcNow.AddMinutes(-10),
            Reason = "Initial store assignment"
        }, CancellationToken.None);
        await fixture.Service.SaveUserStoreAssignmentAsync(new MobilePosUserStoreAssignmentUpsertDto
        {
            UserId = fixture.ActorId,
            MobilePosStoreId = secondStore.Id,
            EffectiveFromUtc = DateTime.UtcNow,
            Reason = "Transferred to the second store"
        }, CancellationToken.None);

        var assignments = await fixture.Db.MobilePosUserStoreAssignments
            .OrderBy(item => item.CreatedAt)
            .ToListAsync();
        assignments.Should().HaveCount(2);
        assignments.Should().ContainSingle(item => item.IsActive && item.MobilePosStoreId == secondStore.Id);
        assignments.Should().ContainSingle(item => !item.IsActive && item.MobilePosStoreId == firstStore.Id
                                                   && item.EffectiveToUtc.HasValue);
    }

    [Fact]
    public async Task DeviceLifecycle_ShouldRecordAssignmentRevokeGrantsAndRejectFurtherHeartbeats()
    {
        await using var fixture = Fixture.Create();
        var store = fixture.SeedActiveStore("STORE-A");
        var till = fixture.SeedActiveTill(store);
        var policy = new MobilePosOfflinePolicy
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.TenantId,
            Name = "Standard offline policy",
            IsActive = true
        };
        fixture.Db.MobilePosOfflinePolicies.Add(policy);
        await fixture.Db.SaveChangesAsync();

        const string installationId = "install-1234567890-device";
        var requested = await fixture.Service.RequestEnrollmentAsync(new MobilePosDeviceEnrollmentRequestDto
        {
            InstallationId = installationId,
            DeviceName = "Z92S test unit",
            Manufacturer = "ZCS",
            Model = "Z92S",
            OperatingSystemVersion = "14",
            AppVersion = "0.1.0"
        }, CancellationToken.None);
        var device = await fixture.Db.MobilePosDevices.SingleAsync(item => item.Id == requested.Id);
        device.RowVersion = [1, 2, 3, 4, 5, 6, 7, 8];
        await fixture.Db.SaveChangesAsync();

        var approved = await fixture.Service.ApproveDeviceAsync(device.Id, new MobilePosDeviceApprovalDto
        {
            MobilePosStoreId = store.Id,
            MobilePosTillId = till.Id,
            Reason = "Approved for controlled test",
            RowVersion = Convert.ToBase64String(device.RowVersion)
        }, CancellationToken.None);
        approved.Status.Should().Be(MobilePosDeviceStatus.Active);
        (await fixture.Db.MobilePosDeviceAssignmentHistories.SingleAsync()).Should().Match<MobilePosDeviceAssignmentHistory>(history =>
            history.NewStoreId == store.Id && history.NewTillId == till.Id &&
            history.Reason == "Approved for controlled test");

        fixture.Db.MobilePosOfflineGrants.Add(new MobilePosOfflineGrant
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.TenantId,
            UserId = fixture.ActorId,
            MobilePosDeviceId = device.Id,
            MobilePosStoreId = store.Id,
            MobilePosTillId = till.Id,
            CashierTillSessionId = Guid.NewGuid(),
            MobilePosOfflinePolicyId = policy.Id,
            IssuedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddHours(8),
            Status = MobilePosOfflineGrantStatus.Active,
            PolicySnapshotJson = "{}",
            PolicySnapshotHash = new string('A', 64)
        });
        await fixture.Db.SaveChangesAsync();

        var revoked = await fixture.Service.RevokeDeviceAsync(device.Id, new MobilePosDeviceStatusChangeDto
        {
            Reason = "Remote disable test",
            RowVersion = approved.RowVersion.Length == 0
                ? Convert.ToBase64String(device.RowVersion)
                : approved.RowVersion
        }, CancellationToken.None);

        revoked.Status.Should().Be(MobilePosDeviceStatus.Revoked);
        revoked.RevocationEpoch.Should().Be(1);
        (await fixture.Db.MobilePosOfflineGrants.SingleAsync()).Status.Should().Be(MobilePosOfflineGrantStatus.Revoked);
        var heartbeat = () => fixture.Service.RecordHeartbeatAsync(new MobilePosHeartbeatDto
        {
            InstallationId = installationId,
            AppVersion = "0.1.1"
        }, CancellationToken.None);
        await heartbeat.Should().ThrowAsync<UnauthorizedAccessException>().WithMessage("*Revoked*");
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private Fixture(ApplicationDbContext db, Guid tenantId, Guid actorId, MobilePosFoundationService service)
        {
            Db = db;
            TenantId = tenantId;
            ActorId = actorId;
            Service = service;
            LocationId = Guid.NewGuid();
            Db.Locations.Add(new Location
            {
                Id = LocationId,
                TenantId = TenantId,
                Code = "HQ",
                Name = "Head office shop",
                StructureId = Guid.NewGuid(),
                LocationLevelId = Guid.NewGuid(),
                IsActive = true
            });
        }

        public ApplicationDbContext Db { get; }
        public Guid TenantId { get; }
        public Guid ActorId { get; }
        public Guid LocationId { get; }
        public MobilePosFoundationService Service { get; }

        public static Fixture Create()
        {
            var tenantId = Guid.NewGuid();
            var actorId = Guid.NewGuid();
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"mobile-pos-service-{Guid.NewGuid():N}")
                .Options, tenantId);
            var currentUser = new Mock<ICurrentUserService>();
            currentUser.SetupGet(item => item.TenantId).Returns(tenantId);
            currentUser.SetupGet(item => item.UserId).Returns(actorId.ToString());
            currentUser.SetupGet(item => item.UserName).Returns("mobile.admin");
            currentUser.SetupGet(item => item.IpAddress).Returns("127.0.0.1");
            currentUser.SetupGet(item => item.UserAgent).Returns("Mobile POS service test");
            var environment = new Mock<IHostEnvironment>();
            environment.SetupGet(item => item.EnvironmentName).Returns("Testing");
            return new Fixture(db, tenantId, actorId,
                new MobilePosFoundationService(db, currentUser.Object, environment.Object));
        }

        public (BusinessPartner Partner, BusinessPartnerRole Role) SeedCustomer(string registrationStatus)
        {
            var partner = new BusinessPartner
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                PartnerCode = "WALK-IN",
                PartnerName = "Default shop customer",
                PartnerType = "Customer",
                RegistrationStatus = registrationStatus,
                ApprovalStatus = registrationStatus,
                IsActive = true
            };
            var role = new BusinessPartnerRole
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                BusinessPartnerId = partner.Id,
                BusinessPartner = partner,
                RoleType = BusinessPartnerRoleType.Customer,
                Status = BusinessPartnerRoleStatus.Active,
                ActiveFromUtc = DateTime.UtcNow.AddDays(-1)
            };
            Db.BusinessPartners.Add(partner);
            Db.Set<BusinessPartnerRole>().Add(role);
            Db.BusinessPartnerArProfileVersions.Add(new BusinessPartnerArProfileVersion
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                BusinessPartnerRoleId = role.Id,
                BusinessPartnerRole = role,
                VersionNumber = 1,
                Status = BusinessPartnerFinanceProfileStatus.Approved,
                EffectiveFrom = DateTime.UtcNow.AddDays(-1)
            });
            return (partner, role);
        }

        public MobilePosStoreUpsertDto StoreInput((BusinessPartner Partner, BusinessPartnerRole Role) customer) => new()
        {
            Code = "shop-01",
            Name = "Main shop",
            Status = MobilePosStoreStatus.Active,
            LocationId = LocationId,
            CurrencyCode = "GHS",
            TimeZoneId = "Africa/Accra",
            DefaultWalkInBusinessPartnerId = customer.Partner.Id,
            DefaultWalkInBusinessPartnerRoleId = customer.Role.Id
        };

        public MobilePosStore SeedActiveStore(string code)
        {
            var store = new MobilePosStore
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                Code = code,
                Name = code,
                Status = MobilePosStoreStatus.Active,
                LocationId = LocationId,
                CurrencyCode = "GHS",
                TimeZoneId = "Africa/Accra",
                DefaultWalkInBusinessPartnerId = Guid.NewGuid(),
                DefaultWalkInBusinessPartnerRoleId = Guid.NewGuid()
            };
            Db.MobilePosStores.Add(store);
            return store;
        }

        public MobilePosTill SeedActiveTill(MobilePosStore store)
        {
            var liquidity = new LiquidityAccount
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                Code = $"CASH-{store.Code}",
                Name = $"{store.Name} cash till",
                AccountType = LiquidityAccountType.CashTill,
                Currency = "GHS",
                GLAccountId = Guid.NewGuid(),
                IsActive = true
            };
            var till = new MobilePosTill
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                MobilePosStoreId = store.Id,
                MobilePosStore = store,
                TillNumber = $"TILL-{store.Code}",
                Name = $"{store.Name} till",
                Status = MobilePosTillStatus.Active,
                LiquidityAccountId = liquidity.Id,
                LiquidityAccount = liquidity,
                RowVersion = [1, 2, 3, 4, 5, 6, 7, 8]
            };
            Db.LiquidityAccounts.Add(liquidity);
            Db.MobilePosTills.Add(till);
            return till;
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
