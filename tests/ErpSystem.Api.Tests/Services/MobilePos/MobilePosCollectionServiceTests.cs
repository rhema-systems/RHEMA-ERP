using ErpSystem.Api.Services.MobilePos;
using ErpSystem.Core.DTOs.AR;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.MobilePos;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.MobilePos;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Finance.Integration;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using FinancePaymentMethod = ErpSystem.Core.Entities.Finance.PaymentMethod;

namespace ErpSystem.Api.Tests.Services.MobilePos;

public sealed class MobilePosCollectionServiceTests
{
    [Fact]
    public async Task CompleteAsync_ShouldAllocateSplitTendersAcrossInvoicesAndReplayWithoutDuplicatePayments()
    {
        await using var fixture = await Fixture.CreateAsync();
        var request = fixture.ValidRequest();

        var first = await fixture.Service.CompleteAsync(request, CancellationToken.None);
        var replay = await fixture.Service.CompleteAsync(request, CancellationToken.None);

        first.IsReplay.Should().BeFalse();
        replay.IsReplay.Should().BeTrue();
        replay.CollectionId.Should().Be(first.CollectionId);
        first.TotalAmount.Should().Be(100m);
        first.Allocations.Should().HaveCount(2);
        first.Tenders.Should().HaveCount(2);
        fixture.Payments.Verify(service => service.CreateAsync(
            It.IsAny<PaymentCreateDto>(),
            It.Is<FinancePostingProducerContext>(producer =>
                producer.RouteId == FinanceDimensionRouteId.MobilePosCustomerPayment),
            It.IsAny<CancellationToken>()), Times.Exactly(2));

        var commands = fixture.Payments.Invocations
            .Where(invocation => invocation.Method.Name == nameof(IPaymentService.CreateAsync))
            .Select(invocation => (PaymentCreateDto)invocation.Arguments[0])
            .ToArray();
        commands[0].TotalAmount.Should().Be(60m);
        commands[0].Allocations.Should().ContainSingle(allocation =>
            allocation.InvoiceId == fixture.FirstInvoiceId
            && allocation.AllocatedAmount == 60m
            && allocation.PaymentCurrencyAmount == 60m);
        commands[1].TotalAmount.Should().Be(40m);
        commands[1].Allocations.Should().BeEquivalentTo(
            new[]
            {
                new { InvoiceId = fixture.FirstInvoiceId, AllocatedAmount = 10m, PaymentCurrencyAmount = (decimal?)10m },
                new { InvoiceId = fixture.SecondInvoiceId, AllocatedAmount = 30m, PaymentCurrencyAmount = (decimal?)30m }
            }, options => options.ExcludingMissingMembers().WithStrictOrdering());
        commands.Should().OnlyContain(command =>
            command.BusinessPartnerId == fixture.BusinessPartnerId
            && command.BusinessPartnerRoleId == fixture.BusinessPartnerRoleId
            && command.CurrencyCode == "GHS"
            && command.LiquidityAccountId == fixture.LiquidityAccountId);

        var collection = await fixture.Db.MobilePosCollections
            .Include(item => item.Allocations)
            .Include(item => item.Tenders)
            .SingleAsync();
        collection.Status.Should().Be(MobilePosCollectionStatus.Completed);
        collection.Allocations.Should().HaveCount(2);
        collection.Tenders.Should().HaveCount(2);
        collection.Tenders.Select(item => item.CustomerPaymentId).Should().OnlyHaveUniqueItems();
        var receipt = await fixture.Db.MobileMutationReceipts.SingleAsync();
        receipt.ReplayCount.Should().Be(1);
        receipt.MobilePosCollectionId.Should().Be(collection.Id);
        receipt.CanonicalInvoiceId.Should().BeNull();
        foreach (var paymentId in collection.Tenders.Select(item => item.CustomerPaymentId))
            receipt.CanonicalCustomerPaymentIdsJson.Should().Contain(paymentId.ToString());
    }

    [Fact]
    public async Task CompleteAsync_ShouldPersistOverAllocationRejectionBeforeCreatingPayment()
    {
        await using var fixture = await Fixture.CreateAsync();
        var request = fixture.ValidRequest();
        request.Allocations[0].Amount = 101m;
        request.Allocations.RemoveAt(1);
        request.Tenders[0].Amount = 101m;
        request.Tenders.RemoveAt(1);

        var first = () => fixture.Service.CompleteAsync(request, CancellationToken.None);
        var replay = () => fixture.Service.CompleteAsync(request, CancellationToken.None);

        await first.Should().ThrowAsync<MobilePosCommandRejectedException>()
            .Where(exception => exception.Code == "MOBILE_POS_COLLECTION_EXCEEDS_BALANCE" && !exception.IsReplay);
        await replay.Should().ThrowAsync<MobilePosCommandRejectedException>()
            .Where(exception => exception.Code == "MOBILE_POS_COLLECTION_EXCEEDS_BALANCE" && exception.IsReplay);
        fixture.Payments.Verify(service => service.CreateAsync(
            It.IsAny<PaymentCreateDto>(),
            It.IsAny<FinancePostingProducerContext>(),
            It.IsAny<CancellationToken>()), Times.Never);
        (await fixture.Db.MobilePosCollections.CountAsync()).Should().Be(0);
        var receipt = await fixture.Db.MobileMutationReceipts.SingleAsync();
        receipt.Status.Should().Be(MobileMutationReceiptStatus.Rejected);
        receipt.ErrorCode.Should().Be("MOBILE_POS_COLLECTION_EXCEEDS_BALANCE");
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly Guid _firstMethodId;
        private readonly Guid _secondMethodId;

        private Fixture(
            ApplicationDbContext db,
            MobilePosCollectionService service,
            Mock<IPaymentService> payments,
            Guid businessPartnerId,
            Guid businessPartnerRoleId,
            Guid firstInvoiceId,
            Guid secondInvoiceId,
            Guid firstMethodId,
            Guid secondMethodId,
            Guid liquidityAccountId)
        {
            Db = db;
            Service = service;
            Payments = payments;
            BusinessPartnerId = businessPartnerId;
            BusinessPartnerRoleId = businessPartnerRoleId;
            FirstInvoiceId = firstInvoiceId;
            SecondInvoiceId = secondInvoiceId;
            _firstMethodId = firstMethodId;
            _secondMethodId = secondMethodId;
            LiquidityAccountId = liquidityAccountId;
        }

        public ApplicationDbContext Db { get; }
        public MobilePosCollectionService Service { get; }
        public Mock<IPaymentService> Payments { get; }
        public Guid BusinessPartnerId { get; }
        public Guid BusinessPartnerRoleId { get; }
        public Guid FirstInvoiceId { get; }
        public Guid SecondInvoiceId { get; }
        public Guid LiquidityAccountId { get; }

        public static async Task<Fixture> CreateAsync()
        {
            var tenantId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var storeId = Guid.NewGuid();
            var tillId = Guid.NewGuid();
            var deviceId = Guid.NewGuid();
            var sessionId = Guid.NewGuid();
            var liquidityAccountId = Guid.NewGuid();
            var partnerId = Guid.NewGuid();
            var roleId = Guid.NewGuid();
            var profileId = Guid.NewGuid();
            var firstInvoiceId = Guid.NewGuid();
            var secondInvoiceId = Guid.NewGuid();
            var firstMethodId = Guid.NewGuid();
            var secondMethodId = Guid.NewGuid();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"mobile-pos-collection-{Guid.NewGuid():N}")
                .Options;
            var db = new ApplicationDbContext(options);
            var now = DateTime.UtcNow;

            var partner = new BusinessPartner
            {
                Id = partnerId,
                TenantId = tenantId,
                PartnerCode = "CUS-001",
                PartnerName = "Approved Customer",
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
                Id = profileId,
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
            db.MobilePosTills.Add(new MobilePosTill
            {
                Id = tillId,
                TenantId = tenantId,
                MobilePosStoreId = storeId,
                TillNumber = "TILL-01",
                Name = "Till 01",
                Status = MobilePosTillStatus.Active,
                LiquidityAccountId = liquidityAccountId
            });
            db.MobilePosDevices.Add(new MobilePosDevice
            {
                Id = deviceId,
                TenantId = tenantId,
                InstallationIdHash = new string('a', 64),
                DeviceName = "Device 01",
                RequestedByUserId = userId,
                RequestedAtUtc = now,
                MobilePosStoreId = storeId,
                MobilePosTillId = tillId,
                Status = MobilePosDeviceStatus.Active
            });
            db.MobilePosUserStoreAssignments.Add(new MobilePosUserStoreAssignment
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                UserId = userId,
                MobilePosStoreId = storeId,
                EffectiveFromUtc = now.AddHours(-1),
                IsActive = true
            });
            db.CashierTillSessions.Add(new CashierTillSession
            {
                Id = sessionId,
                TenantId = tenantId,
                SessionNumber = "SESSION-01",
                LiquidityAccountId = liquidityAccountId,
                BusinessDate = now.Date,
                Currency = "GHS",
                CashierUserId = userId,
                CashierName = "Cashier",
                Status = CashierTillSessionStatus.Open,
                OpenedAt = now,
                OpenedById = userId
            });
            db.Invoices.AddRange(
                Invoice(firstInvoiceId, tenantId, partnerId, roleId, profileId, "INV-001", 100m, now),
                Invoice(secondInvoiceId, tenantId, partnerId, roleId, profileId, "INV-002", 80m, now));
            var firstMethod = PaymentMethod(firstMethodId, tenantId, "CASH", "Cash");
            var secondMethod = PaymentMethod(secondMethodId, tenantId, "CASH2", "Cash 2");
            db.PaymentMethods.AddRange(firstMethod, secondMethod);
            db.MobilePosTillPaymentMethods.AddRange(
                new MobilePosTillPaymentMethod
                {
                    Id = Guid.NewGuid(), TenantId = tenantId, MobilePosTillId = tillId,
                    PaymentMethodId = firstMethodId, PaymentMethod = firstMethod, AllowOnline = true
                },
                new MobilePosTillPaymentMethod
                {
                    Id = Guid.NewGuid(), TenantId = tenantId, MobilePosTillId = tillId,
                    PaymentMethodId = secondMethodId, PaymentMethod = secondMethod, AllowOnline = true
                });
            await db.SaveChangesAsync();

            var currentUser = new Mock<ICurrentUserService>();
            currentUser.SetupGet(service => service.TenantId).Returns(tenantId);
            currentUser.SetupGet(service => service.UserId).Returns(userId.ToString());
            currentUser.SetupGet(service => service.UserName).Returns("cashier");
            var foundation = new Mock<IMobilePosFoundationService>();
            foundation.Setup(service => service.GetBootstrapAsync("install-01", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new MobilePosBootstrapDto
                {
                    UserId = userId,
                    Device = new MobilePosDeviceDto { Id = deviceId, Status = MobilePosDeviceStatus.Active },
                    Store = new MobilePosStoreDto { Id = storeId, Code = "SHOP-01", CurrencyCode = "GHS" },
                    Till = new MobilePosTillDto { Id = tillId, TillNumber = "TILL-01" },
                    CurrentTillSessionId = sessionId,
                    ServerTimeUtc = now
                });
            var payments = new Mock<IPaymentService>();
            var sequence = 0;
            payments.Setup(service => service.CreateAsync(
                    It.IsAny<PaymentCreateDto>(), It.IsAny<FinancePostingProducerContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((PaymentCreateDto command, FinancePostingProducerContext _, CancellationToken _) =>
                    new CustomerPaymentDto
                    {
                        Id = Guid.NewGuid(),
                        PaymentNumber = $"PAY-{++sequence:000}",
                        BusinessPartnerId = partnerId,
                        BusinessPartnerRoleId = roleId,
                        TotalAmount = command.TotalAmount,
                        AllocatedAmount = command.TotalAmount,
                        CurrencyCode = command.CurrencyCode,
                        PaymentMethodId = command.PaymentMethodId,
                        LiquidityAccountId = command.LiquidityAccountId,
                        BankAccountId = command.BankAccountId,
                        Status = "Posted"
                    });
            var mutations = new MobilePosMutationExecutionService(db, new UnitOfWork(db), currentUser.Object);
            var financeAccess = new Mock<IFinanceAccessScopeService>();
            financeAccess.Setup(service => service.GetPermittedBankAccountIdsAsync(
                    FinanceAccessLevel.Operate, It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlyCollection<Guid>?)null);
            var service = new MobilePosCollectionService(
                db, currentUser.Object, foundation.Object, mutations, payments.Object, financeAccess.Object);
            return new Fixture(db, service, payments, partnerId, roleId, firstInvoiceId, secondInvoiceId,
                firstMethodId, secondMethodId, liquidityAccountId);
        }

        public MobilePosCompleteCollectionRequestDto ValidRequest() => new()
        {
            InstallationId = "install-01",
            ClientMutationId = "collection-mutation-001",
            LocalReference = "COL-LOCAL-001",
            BusinessPartnerId = BusinessPartnerId,
            BusinessPartnerRoleId = BusinessPartnerRoleId,
            Allocations =
            [
                new MobilePosCollectionAllocationInputDto { InvoiceId = FirstInvoiceId, Amount = 70m },
                new MobilePosCollectionAllocationInputDto { InvoiceId = SecondInvoiceId, Amount = 30m }
            ],
            Tenders =
            [
                new MobilePosTenderInputDto { PaymentMethodId = _firstMethodId, Amount = 60m },
                new MobilePosTenderInputDto { PaymentMethodId = _secondMethodId, Amount = 40m }
            ]
        };

        private static Invoice Invoice(
            Guid id,
            Guid tenantId,
            Guid partnerId,
            Guid roleId,
            Guid profileId,
            string number,
            decimal total,
            DateTime now) => new()
        {
            Id = id,
            TenantId = tenantId,
            InvoiceNumber = number,
            BusinessPartnerId = partnerId,
            BusinessPartnerRoleId = roleId,
            BusinessPartnerArProfileVersionId = profileId,
            BusinessPartnerCode = "CUS-001",
            CustomerName = "Approved Customer",
            InvoiceDate = now.Date,
            TotalAmount = total,
            BaseCurrencyAmount = total,
            CurrencyCode = "GHS",
            ExchangeRate = 1m,
            Status = InvoiceStatus.Sent,
            ApprovalRequired = false,
            JournalEntryId = Guid.NewGuid()
        };

        private static FinancePaymentMethod PaymentMethod(Guid id, Guid tenantId, string code, string name) => new()
        {
            Id = id,
            TenantId = tenantId,
            Code = code,
            Name = name,
            Type = PaymentMethodType.Cash,
            IsActive = true,
            RequiresBankAccount = false
        };

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
