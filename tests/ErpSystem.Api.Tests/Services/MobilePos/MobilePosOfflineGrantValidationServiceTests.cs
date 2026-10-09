using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Api.Services.MobilePos;
using ErpSystem.Core.DTOs.MobilePos;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.MobilePos;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Interfaces;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace ErpSystem.Api.Tests.Services.MobilePos;

public sealed class MobilePosOfflineGrantValidationServiceTests
{
    [Fact]
    public async Task AuthorizeAsync_ShouldAcceptAnExpiredGrantWhenTheCommandOccurredInsideItsSignedWindow()
    {
        await using var fixture = await Fixture.CreateAsync(MobilePosOfflineGrantStatus.Expired);

        var authorization = await fixture.Service.AuthorizeAsync(fixture.Request(), CancellationToken.None);

        authorization.Grant.Id.Should().Be(fixture.Grant.Id);
        authorization.Policy.AllowedCommandTypes.Should().Contain("CashSale");
    }

    [Fact]
    public async Task AuthorizeAsync_ShouldRejectRevokedGrantAndChangedPolicySnapshot()
    {
        await using var revoked = await Fixture.CreateAsync(MobilePosOfflineGrantStatus.Revoked);
        var revokedAction = () => revoked.Service.AuthorizeAsync(revoked.Request(), CancellationToken.None);
        await revokedAction.Should().ThrowAsync<MobilePosCommandRejectedException>()
            .Where(exception => exception.Code == "MOBILE_POS_OFFLINE_GRANT_REVOKED");

        await using var changed = await Fixture.CreateAsync(MobilePosOfflineGrantStatus.Active);
        changed.Grant.PolicySnapshotJson = changed.Grant.PolicySnapshotJson.Replace("CashSale", "ChangedSale", StringComparison.Ordinal);
        await changed.Db.SaveChangesAsync();
        var changedAction = () => changed.Service.AuthorizeAsync(changed.Request(), CancellationToken.None);
        await changedAction.Should().ThrowAsync<MobilePosCommandRejectedException>()
            .Where(exception => exception.Code == "MOBILE_POS_OFFLINE_POLICY_SNAPSHOT_INVALID");
    }

    [Fact]
    public async Task AuthorizeAsync_ShouldEnforceSignedAggregateAndTenderRules()
    {
        await using var fixture = await Fixture.CreateAsync(MobilePosOfflineGrantStatus.Active, priorAmount: 75m);
        var overAggregate = () => fixture.Service.AuthorizeAsync(fixture.Request(amount: 100m), CancellationToken.None);
        await overAggregate.Should().ThrowAsync<MobilePosCommandRejectedException>()
            .Where(exception => exception.Code == "MOBILE_POS_OFFLINE_AGGREGATE_LIMIT_EXCEEDED");

        var unknownTender = fixture.Request(amount: 50m) with
        {
            Tenders = [new MobilePosTenderInputDto { PaymentMethodId = Guid.NewGuid(), Amount = 50m }]
        };
        var tenderAction = () => fixture.Service.AuthorizeAsync(unknownTender, CancellationToken.None);
        await tenderAction.Should().ThrowAsync<MobilePosCommandRejectedException>()
            .Where(exception => exception.Code == "MOBILE_POS_OFFLINE_TENDER_NOT_ALLOWED");
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private Fixture(
            ApplicationDbContext db,
            MobilePosOfflineGrantValidationService service,
            MobilePosOfflineGrantTokenService tokens,
            MobilePosOfflineGrant grant,
            Guid paymentMethodId,
            DateTime occurredAtUtc)
        {
            Db = db;
            Service = service;
            Tokens = tokens;
            Grant = grant;
            PaymentMethodId = paymentMethodId;
            OccurredAtUtc = occurredAtUtc;
        }

        public ApplicationDbContext Db { get; }
        public MobilePosOfflineGrantValidationService Service { get; }
        public MobilePosOfflineGrantTokenService Tokens { get; }
        public MobilePosOfflineGrant Grant { get; }
        public Guid PaymentMethodId { get; }
        public DateTime OccurredAtUtc { get; }

        public static async Task<Fixture> CreateAsync(
            MobilePosOfflineGrantStatus status,
            decimal? priorAmount = null)
        {
            var tenantId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var deviceId = Guid.NewGuid();
            var storeId = Guid.NewGuid();
            var tillId = Guid.NewGuid();
            var sessionId = Guid.NewGuid();
            var policyId = Guid.NewGuid();
            var grantId = Guid.NewGuid();
            var paymentMethodId = Guid.NewGuid();
            var now = DateTime.UtcNow;
            var issuedAt = now.AddMinutes(-30);
            var expiresAt = now.AddMinutes(-10);
            var occurredAt = now.AddMinutes(-20);
            var snapshot = new MobilePosOfflineGrantPolicySnapshotDto
            {
                PolicyId = policyId,
                PolicyName = "Bounded offline sales",
                PolicyVersionUtc = issuedAt,
                CurrencyCode = "GHS",
                DefaultWalkInBusinessPartnerId = Guid.NewGuid(),
                DefaultWalkInBusinessPartnerRoleId = Guid.NewGuid(),
                MaximumTransactionAmount = 100m,
                MaximumAggregateAmount = 150m,
                MaximumTransactionCount = 2,
                MaximumOfflineAgeMinutes = 60,
                AllowProvisionalReceipt = true,
                AllowedCommandTypes = ["CashSale"],
                AllowedPaymentMethods =
                [
                    new MobilePosOfflinePaymentMethodSnapshotDto
                    {
                        PaymentMethodId = paymentMethodId,
                        Code = "CASH",
                        Name = "Cash",
                        Type = PaymentMethodType.Cash.ToString()
                    }
                ]
            };
            var snapshotJson = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            var snapshotHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(snapshotJson)));
            var device = new MobilePosDevice
            {
                Id = deviceId,
                TenantId = tenantId,
                InstallationIdHash = new string('A', 64),
                DeviceName = "Z92S-TEST",
                RequestedByUserId = userId,
                RequestedAtUtc = issuedAt,
                Status = MobilePosDeviceStatus.Active,
                MobilePosStoreId = storeId,
                MobilePosTillId = tillId,
                RevocationEpoch = 4
            };
            var policy = new MobilePosOfflinePolicy
            {
                Id = policyId,
                TenantId = tenantId,
                Name = snapshot.PolicyName,
                IsActive = true
            };
            var grant = new MobilePosOfflineGrant
            {
                Id = grantId,
                TenantId = tenantId,
                UserId = userId,
                MobilePosDeviceId = deviceId,
                MobilePosDevice = device,
                MobilePosStoreId = storeId,
                MobilePosTillId = tillId,
                CashierTillSessionId = sessionId,
                MobilePosOfflinePolicyId = policyId,
                MobilePosOfflinePolicy = policy,
                IssuedAtUtc = issuedAt,
                ExpiresAtUtc = expiresAt,
                Status = status,
                RevocationEpoch = device.RevocationEpoch,
                PolicySnapshotJson = snapshotJson,
                PolicySnapshotHash = snapshotHash
            };
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"mobile-pos-offline-grant-{Guid.NewGuid():N}")
                .Options;
            var db = new ApplicationDbContext(options);
            db.MobilePosDevices.Add(device);
            db.MobilePosOfflinePolicies.Add(policy);
            db.MobilePosOfflineGrants.Add(grant);
            db.CashierTillSessions.Add(new CashierTillSession
            {
                Id = sessionId,
                TenantId = tenantId,
                SessionNumber = "SESSION-001",
                LiquidityAccountId = Guid.NewGuid(),
                BusinessDate = now.Date,
                CashierUserId = userId,
                CashierName = "Offline Cashier",
                OpenedAt = issuedAt,
                OpenedById = userId,
                Status = CashierTillSessionStatus.Open
            });
            if (priorAmount.HasValue)
            {
                db.MobilePosSales.Add(new MobilePosSale
                {
                    Id = Guid.NewGuid(), TenantId = tenantId, MobilePosStoreId = storeId,
                    MobilePosTillId = tillId, CashierTillSessionId = sessionId,
                    MobilePosDeviceId = deviceId, OperatorUserId = userId,
                    ClientMutationId = "previous-mutation", LocalReference = "OFF-OLD-001",
                    BusinessPartnerId = snapshot.DefaultWalkInBusinessPartnerId,
                    BusinessPartnerRoleId = snapshot.DefaultWalkInBusinessPartnerRoleId,
                    BusinessDate = now.Date, OccurredAtUtc = occurredAt,
                    CurrencyCode = "GHS", TotalAmount = priorAmount.Value,
                    Status = MobilePosSaleStatus.Completed, MobilePosOfflineGrantId = grantId
                });
            }
            await db.SaveChangesAsync();

            var currentUser = new Mock<ICurrentUserService>();
            currentUser.SetupGet(item => item.TenantId).Returns(tenantId);
            currentUser.SetupGet(item => item.UserId).Returns(userId.ToString());
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MobilePos:OfflineGrantSigningKey"] = new string('V', 64)
            }).Build();
            var tokens = new MobilePosOfflineGrantTokenService(configuration);
            return new Fixture(
                db,
                new MobilePosOfflineGrantValidationService(db, currentUser.Object, tokens),
                tokens,
                grant,
                paymentMethodId,
                occurredAt);
        }

        public MobilePosOfflineGrantValidationRequest Request(decimal amount = 100m)
        {
            var token = Tokens.Sign(new MobilePosOfflineGrantTokenPayload(
                MobilePosOfflineGrantTokenService.CurrentVersion,
                Grant.Id,
                Grant.TenantId,
                Grant.UserId,
                Grant.MobilePosDeviceId,
                Grant.MobilePosStoreId,
                Grant.MobilePosTillId,
                Grant.CashierTillSessionId,
                Grant.MobilePosOfflinePolicyId,
                Grant.PolicySnapshotHash,
                Grant.RevocationEpoch,
                Grant.IssuedAtUtc,
                Grant.ExpiresAtUtc));
            return new MobilePosOfflineGrantValidationRequest(
                token,
                Grant.Id,
                Grant.MobilePosDeviceId,
                Grant.MobilePosStoreId,
                Grant.MobilePosTillId,
                Grant.CashierTillSessionId,
                "CashSale",
                1,
                OccurredAtUtc,
                amount,
                [new MobilePosTenderInputDto { PaymentMethodId = PaymentMethodId, Amount = amount }]);
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
