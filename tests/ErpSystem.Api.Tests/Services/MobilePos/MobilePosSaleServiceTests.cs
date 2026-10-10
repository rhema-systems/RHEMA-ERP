using ErpSystem.Api.Services.MobilePos;
using ErpSystem.Core.Entities;
using ErpSystem.Core.DTOs.AR;
using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.DTOs.MobilePos;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.MobilePos;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Finance.Integration;
using ErpSystem.Core.Interfaces;
using ErpSystem.Core.Interfaces.Finance;
using ErpSystem.Data;
using ErpSystem.Shared;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using FinancePaymentMethod = ErpSystem.Core.Entities.Finance.PaymentMethod;

namespace ErpSystem.Api.Tests.Services.MobilePos;

public sealed class MobilePosSaleServiceTests
{
    [Fact]
    public async Task CompleteAsync_ShouldCreateOnePostedInvoiceAndOneCanonicalPaymentPerTenderThenReplay()
    {
        await using var fixture = await Fixture.CreateAsync();
        var request = fixture.ValidRequest();

        var first = await fixture.Service.CompleteAsync(request, CancellationToken.None);
        var replay = await fixture.Service.CompleteAsync(request, CancellationToken.None);

        first.IsReplay.Should().BeFalse();
        replay.IsReplay.Should().BeTrue();
        replay.SaleId.Should().Be(first.SaleId);
        replay.InvoiceId.Should().Be(first.InvoiceId);
        first.Tenders.Should().HaveCount(2);
        first.TotalAmount.Should().Be(100m);
        fixture.Invoices.Verify(service => service.CreateAsync(
            It.IsAny<InvoiceCreateDto>(),
            It.Is<FinancePostingProducerContext>(producer => producer.RouteId == FinanceDimensionRouteId.MobilePosCustomerInvoice),
            It.IsAny<CancellationToken>()), Times.Once);
        fixture.Invoices.Verify(service => service.PostAsync(
            It.IsAny<Guid>(),
            It.Is<FinancePostingProducerContext>(producer => producer.RouteId == FinanceDimensionRouteId.MobilePosCustomerInvoice),
            It.IsAny<CancellationToken>()), Times.Once);
        fixture.Payments.Verify(service => service.CreateAsync(
            It.Is<PaymentCreateDto>(payment => payment.Allocations != null
                && payment.Allocations.Count == 1
                && payment.Allocations[0].InvoiceId == first.InvoiceId),
            It.Is<FinancePostingProducerContext>(producer => producer.RouteId == FinanceDimensionRouteId.MobilePosCustomerPayment),
            It.IsAny<CancellationToken>()), Times.Exactly(2));

        var invoiceCommand = (InvoiceCreateDto)fixture.Invoices.Invocations
            .Single(invocation => invocation.Method.Name == nameof(IInvoiceService.CreateAsync))
            .Arguments[0];
        invoiceCommand.CurrencyCode.Should().Be("GHS");
        invoiceCommand.LineItems.Should().ContainSingle();
        invoiceCommand.LineItems[0].GLAccountId.Should().NotBeEmpty();
        invoiceCommand.LineItems[0].Unit.Should().Be("EA");

        var paymentCommands = fixture.Payments.Invocations
            .Where(invocation => invocation.Method.Name == nameof(IPaymentService.CreateAsync))
            .Select(invocation => (PaymentCreateDto)invocation.Arguments[0])
            .ToArray();
        paymentCommands.Should().HaveCount(2);
        paymentCommands.Sum(payment => payment.TotalAmount).Should().Be(100m);
        paymentCommands.Select(payment => payment.PaymentMethodId).Should()
            .BeEquivalentTo(first.Tenders.Select(tender => tender.PaymentMethodId));
        paymentCommands.Should().OnlyContain(payment =>
            payment.CurrencyCode == "GHS"
            && payment.LiquidityAccountId == fixture.LiquidityAccountId
            && payment.Allocations != null
            && payment.Allocations.Count == 1
            && payment.Allocations[0].InvoiceId == first.InvoiceId
            && payment.Allocations[0].AllocatedAmount == payment.TotalAmount
            && payment.Allocations[0].PaymentCurrencyAmount == payment.TotalAmount);

        var sale = await fixture.Db.MobilePosSales
            .Include(item => item.Lines)
            .Include(item => item.Tenders)
            .SingleAsync();
        sale.UsedStoreDefaultCustomer.Should().BeTrue();
        sale.Status.Should().Be(MobilePosSaleStatus.Completed);
        sale.InvoiceId.Should().Be(first.InvoiceId);
        sale.Lines.Should().ContainSingle();
        sale.Tenders.Should().HaveCount(2);
        sale.Tenders.Should().OnlyContain(tender =>
            tender.Status == MobilePosTenderStatus.Completed
            && tender.CustomerPaymentId != null
            && !string.IsNullOrWhiteSpace(tender.PaymentNumber));
        sale.Tenders.Select(tender => tender.CustomerPaymentId).Should().OnlyHaveUniqueItems();
        var mutation = await fixture.Db.MobileMutationReceipts.SingleAsync();
        mutation.ReplayCount.Should().Be(1);
        mutation.CanonicalInvoiceId.Should().Be(first.InvoiceId);
        foreach (var paymentId in sale.Tenders.Select(tender => tender.CustomerPaymentId!.Value))
            mutation.CanonicalCustomerPaymentIdsJson.Should().Contain(paymentId.ToString());
    }

    [Fact]
    public async Task CompleteAsync_ShouldPersistCanonicalTotalRejectionAndNeverCreatePayments()
    {
        await using var fixture = await Fixture.CreateAsync(invoiceTotal: 105m);
        var request = fixture.ValidRequest();

        var first = () => fixture.Service.CompleteAsync(request, CancellationToken.None);
        var replay = () => fixture.Service.CompleteAsync(request, CancellationToken.None);

        await first.Should().ThrowAsync<MobilePosCommandRejectedException>()
            .Where(exception => exception.Code == "MOBILE_POS_TOTALS_CHANGED" && !exception.IsReplay);
        await replay.Should().ThrowAsync<MobilePosCommandRejectedException>()
            .Where(exception => exception.Code == "MOBILE_POS_TOTALS_CHANGED" && exception.IsReplay);
        fixture.Payments.Verify(service => service.CreateAsync(
            It.IsAny<PaymentCreateDto>(),
            It.IsAny<FinancePostingProducerContext>(),
            It.IsAny<CancellationToken>()), Times.Never);
        (await fixture.Db.MobilePosSales.CountAsync()).Should().Be(0);
        var receipt = await fixture.Db.MobileMutationReceipts.SingleAsync();
        receipt.Status.Should().Be(MobileMutationReceiptStatus.Rejected);
        receipt.ErrorCode.Should().Be("MOBILE_POS_TOTALS_CHANGED");
    }

    [Fact]
    public async Task CompleteAsync_ShouldRejectDiscountWithoutDynamicPermissionBeforeCreatingInvoice()
    {
        await using var fixture = await Fixture.CreateAsync(grantDiscountPermission: false);
        var request = fixture.ValidRequest();
        request.Lines[0].DiscountPercentage = 5m;

        var action = () => fixture.Service.CompleteAsync(request, CancellationToken.None);

        await action.Should().ThrowAsync<MobilePosCommandRejectedException>()
            .Where(exception => exception.Code == "MOBILE_POS_DISCOUNT_NOT_AUTHORIZED");
        fixture.Invoices.Verify(service => service.CreateAsync(
            It.IsAny<InvoiceCreateDto>(),
            It.IsAny<FinancePostingProducerContext>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CompleteAsync_ShouldRequireAnEligibleBankAccountBeforeCreatingInvoice()
    {
        await using var fixture = await Fixture.CreateAsync(firstMethodRequiresBankAccount: true);
        var request = fixture.ValidRequest();
        request.Tenders[0].BankAccountId = null;

        var action = () => fixture.Service.CompleteAsync(request, CancellationToken.None);

        await action.Should().ThrowAsync<MobilePosCommandRejectedException>()
            .Where(exception => exception.Code == "MOBILE_POS_TENDER_BANK_ACCOUNT_REQUIRED");
        fixture.Invoices.Verify(service => service.CreateAsync(
            It.IsAny<InvoiceCreateDto>(),
            It.IsAny<FinancePostingProducerContext>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CompleteAsync_ShouldPassEligibleSelectedBankAccountToCanonicalPayment()
    {
        await using var fixture = await Fixture.CreateAsync(firstMethodRequiresBankAccount: true);

        await fixture.Service.CompleteAsync(fixture.ValidRequest(), CancellationToken.None);

        fixture.Payments.Verify(service => service.CreateAsync(
            It.Is<PaymentCreateDto>(payment => payment.BankAccountId == fixture.BankAccountId),
            It.IsAny<FinancePostingProducerContext>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CompleteOfflineAsync_ShouldUseTheSignedContextAndPersistOfflineEvidenceDuringPendingReview()
    {
        await using var fixture = await Fixture.CreateAsync();
        var device = await fixture.Db.MobilePosDevices.SingleAsync();
        var store = await fixture.Db.MobilePosStores.SingleAsync();
        var till = await fixture.Db.MobilePosTills.SingleAsync();
        var session = await fixture.Db.CashierTillSessions.SingleAsync();
        session.Status = CashierTillSessionStatus.PendingReview;
        foreach (var mapping in await fixture.Db.MobilePosTillPaymentMethods.ToListAsync())
            mapping.AllowOffline = true;
        await fixture.Db.SaveChangesAsync();
        var grantId = Guid.NewGuid();
        var authorization = new MobilePosOfflineGrantAuthorization(
            new MobilePosOfflineGrant
            {
                Id = grantId,
                TenantId = store.TenantId,
                MobilePosDeviceId = device.Id,
                MobilePosStoreId = store.Id,
                MobilePosTillId = till.Id,
                CashierTillSessionId = session.Id,
                PolicySnapshotHash = new string('A', 64)
            },
            new MobilePosOfflineGrantPolicySnapshotDto
            {
                AllowedCommandTypes = ["CashSale"],
                AllowDiscounts = false
            });
        var request = fixture.ValidRequest();
        request.OccurredAtUtc = DateTime.UtcNow.AddMinutes(-5);

        var result = await fixture.Service.CompleteOfflineAsync(request, authorization, CancellationToken.None);

        result.IsReplay.Should().BeFalse();
        var sale = await fixture.Db.MobilePosSales.Include(item => item.Tenders).SingleAsync();
        sale.MobilePosOfflineGrantId.Should().Be(grantId);
        sale.OfflinePolicySnapshotHash.Should().Be(new string('A', 64));
        sale.SynchronizedAtUtc.Should().NotBeNull();
        sale.Tenders.Should().OnlyContain(tender => tender.WasRecordedOffline);
        (await fixture.Db.MobileMutationReceipts.SingleAsync()).CommandType.Should().Be("CashSale");
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly Guid _itemId;
        private readonly Guid _firstMethodId;
        private readonly Guid _secondMethodId;
        private readonly bool _firstMethodRequiresBankAccount;

        private Fixture(
            ApplicationDbContext db,
            MobilePosSaleService service,
            Mock<IInvoiceService> invoices,
            Mock<IPaymentService> payments,
            Guid itemId,
            Guid firstMethodId,
            Guid secondMethodId,
            Guid bankAccountId,
            Guid liquidityAccountId,
            bool firstMethodRequiresBankAccount)
        {
            Db = db;
            Service = service;
            Invoices = invoices;
            Payments = payments;
            _itemId = itemId;
            _firstMethodId = firstMethodId;
            _secondMethodId = secondMethodId;
            BankAccountId = bankAccountId;
            LiquidityAccountId = liquidityAccountId;
            _firstMethodRequiresBankAccount = firstMethodRequiresBankAccount;
        }

        public ApplicationDbContext Db { get; }
        public MobilePosSaleService Service { get; }
        public Mock<IInvoiceService> Invoices { get; }
        public Mock<IPaymentService> Payments { get; }
        public Guid BankAccountId { get; }
        public Guid LiquidityAccountId { get; }

        public static async Task<Fixture> CreateAsync(
            decimal invoiceTotal = 100m,
            bool grantDiscountPermission = true,
            bool firstMethodRequiresBankAccount = false)
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
            var itemId = Guid.NewGuid();
            var salesAccountId = Guid.NewGuid();
            var firstMethodId = Guid.NewGuid();
            var secondMethodId = Guid.NewGuid();
            var bankAccountId = Guid.NewGuid();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"mobile-pos-sale-{Guid.NewGuid():N}")
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
            db.InventoryItems.Add(new InventoryItem
            {
                Id = itemId,
                TenantId = tenantId,
                ItemCode = "SERVICE-01",
                Name = "Service item",
                CategoryId = Guid.NewGuid(),
                UnitOfMeasure = "EA",
                SalePrice = 100m,
                SalesAccountId = salesAccountId,
                ItemType = ItemType.Service,
                Status = ItemStatus.Active
            });
            var firstMethod = PaymentMethod(firstMethodId, tenantId,
                firstMethodRequiresBankAccount ? "BANK" : "CASH",
                firstMethodRequiresBankAccount ? "Bank Transfer" : "Cash");
            if (firstMethodRequiresBankAccount)
            {
                firstMethod.Type = PaymentMethodType.BankTransfer;
                firstMethod.RequiresBankAccount = true;
                firstMethod.RequiresReference = true;
                db.BankAccounts.Add(new BankAccount
                {
                    Id = bankAccountId,
                    TenantId = tenantId,
                    AccountNumber = "0123456789",
                    AccountName = "Main collections",
                    BankName = "Ghana Bank",
                    Currency = "GHS",
                    GLAccountId = Guid.NewGuid(),
                    IsActive = true
                });
            }
            var secondMethod = PaymentMethod(secondMethodId, tenantId, "CASH2", "Cash 2");
            db.PaymentMethods.AddRange(firstMethod, secondMethod);
            db.MobilePosTillPaymentMethods.AddRange(
                new MobilePosTillPaymentMethod
                {
                    Id = Guid.NewGuid(), TenantId = tenantId, MobilePosTillId = tillId,
                    PaymentMethodId = firstMethodId, PaymentMethod = firstMethod, AllowOnline = true,
                    AllowOffline = true
                },
                new MobilePosTillPaymentMethod
                {
                    Id = Guid.NewGuid(), TenantId = tenantId, MobilePosTillId = tillId,
                    PaymentMethodId = secondMethodId, PaymentMethod = secondMethod, AllowOnline = true,
                    AllowOffline = true
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
            currentUser.SetupGet(service => service.UserName).Returns("cashier");
            var bootstrap = new MobilePosBootstrapDto
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
            };
            var foundation = new Mock<IMobilePosFoundationService>();
            foundation.Setup(service => service.GetBootstrapAsync("install-01", It.IsAny<CancellationToken>()))
                .ReturnsAsync(bootstrap);
            var invoices = new Mock<IInvoiceService>();
            var invoiceId = Guid.NewGuid();
            InvoiceDto Invoice(string status) => new()
            {
                Id = invoiceId,
                InvoiceNumber = "INV-POS-001",
                BusinessPartnerId = partnerId,
                BusinessPartnerRoleId = roleId,
                CurrencyCode = "GHS",
                SubTotal = invoiceTotal,
                TaxAmount = 0m,
                DiscountAmount = 0m,
                TotalAmount = invoiceTotal,
                Status = status,
                LineItems =
                [
                    new InvoiceLineItemDto
                    {
                        Id = Guid.Empty,
                        InvoiceId = invoiceId,
                        Description = "Service item",
                        Quantity = 1m,
                        UnitPrice = 100m,
                        LineTotal = invoiceTotal,
                        DiscountAmount = 0m,
                        TaxAmount = 0m,
                        Unit = "EA"
                    }
                ]
            };
            invoices.Setup(service => service.CreateAsync(
                    It.IsAny<InvoiceCreateDto>(), It.IsAny<FinancePostingProducerContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((InvoiceCreateDto request, FinancePostingProducerContext _, CancellationToken _) =>
                {
                    var result = Invoice("Draft");
                    result.LineItems[0].Id = request.LineItems[0].Id!.Value;
                    return result;
                });
            invoices.Setup(service => service.PostAsync(
                    invoiceId, It.IsAny<FinancePostingProducerContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Invoice("Sent"));
            invoices.Setup(service => service.GetByIdAsync(
                    invoiceId, It.IsAny<FinancePostingProducerContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Invoice("Sent"));
            var payments = new Mock<IPaymentService>();
            var paymentSequence = 0;
            payments.Setup(service => service.CreateAsync(
                    It.IsAny<PaymentCreateDto>(), It.IsAny<FinancePostingProducerContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((PaymentCreateDto request, FinancePostingProducerContext _, CancellationToken _) =>
                {
                    paymentSequence++;
                    return new CustomerPaymentDto
                    {
                        Id = Guid.NewGuid(),
                        PaymentNumber = $"PAY-{paymentSequence:000}",
                        BusinessPartnerId = partnerId,
                        BusinessPartnerRoleId = roleId,
                        TotalAmount = request.TotalAmount,
                        AllocatedAmount = request.TotalAmount,
                        CurrencyCode = "GHS",
                        PaymentMethodId = request.PaymentMethodId,
                        LiquidityAccountId = request.LiquidityAccountId,
                        BankAccountId = request.BankAccountId,
                        Status = "Posted"
                    };
                });
            var mutations = new MobilePosMutationExecutionService(
                db, new UnitOfWork(db), currentUser.Object);
            var financeAccess = new Mock<IFinanceAccessScopeService>();
            financeAccess.Setup(service => service.GetPermittedBankAccountIdsAsync(
                    FinanceAccessLevel.Operate, It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlyCollection<Guid>?)null);
            var service = new MobilePosSaleService(
                db, currentUser.Object, foundation.Object, mutations, invoices.Object, payments.Object,
                financeAccess.Object);
            return new Fixture(db, service, invoices, payments, itemId, firstMethodId, secondMethodId,
                bankAccountId, liquidityAccountId, firstMethodRequiresBankAccount);
        }

        public MobilePosCompleteSaleRequestDto ValidRequest() => new()
        {
            InstallationId = "install-01",
            ClientMutationId = "mutation-001",
            LocalReference = "POS-LOCAL-001",
            ExpectedSubTotal = 100m,
            ExpectedTaxAmount = 0m,
            ExpectedDiscountAmount = 0m,
            ExpectedTotalAmount = 100m,
            Lines =
            [
                new MobilePosSaleLineInputDto
                {
                    ClientLineId = Guid.NewGuid(),
                    InventoryItemId = _itemId,
                    Quantity = 1m,
                    UnitPrice = 100m
                }
            ],
            Tenders =
            [
                new MobilePosTenderInputDto
                {
                    PaymentMethodId = _firstMethodId,
                    Amount = 60m,
                    BankAccountId = _firstMethodRequiresBankAccount ? BankAccountId : null,
                    ExternalReference = _firstMethodRequiresBankAccount ? "BANK-REF-001" : null
                },
                new MobilePosTenderInputDto { PaymentMethodId = _secondMethodId, Amount = 40m }
            ]
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
