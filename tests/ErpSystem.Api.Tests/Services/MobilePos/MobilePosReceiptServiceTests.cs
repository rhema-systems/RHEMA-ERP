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
using Moq;
using Xunit;
using FinancePaymentMethod = ErpSystem.Core.Entities.Finance.PaymentMethod;

namespace ErpSystem.Api.Tests.Services.MobilePos;

public sealed class MobilePosReceiptServiceTests
{
    [Fact]
    public async Task GetAsync_ShouldProjectCanonicalSaleInvoiceLinesAndSplitTenders()
    {
        await using var fixture = await Fixture.CreateAsync();

        var receipt = await fixture.Service.GetAsync(
            fixture.SaleId, "installation-123456", CancellationToken.None);

        receipt.CopyType.Should().Be("ORIGINAL");
        receipt.CopyNumber.Should().Be(0);
        receipt.ReprintCount.Should().Be(0);
        receipt.AuditEventId.Should().BeNull();
        receipt.TenantName.Should().Be("Rhema Test Tenant");
        receipt.StoreCode.Should().Be("SHOP-01");
        receipt.LocationName.Should().Be("Accra Shop");
        receipt.TillNumber.Should().Be("TILL-01");
        receipt.CashierName.Should().Be("Mobile Cashier");
        receipt.CustomerCode.Should().Be("WALK-IN");
        receipt.InvoiceNumber.Should().Be("INV-POS-001");
        receipt.InvoiceStatus.Should().Be(nameof(InvoiceStatus.Paid));
        receipt.Lines.Should().ContainSingle().Which.Description.Should().Be("Retail item");
        receipt.Tenders.Should().HaveCount(2);
        receipt.Tenders.Select(item => item.PaymentNumber).Should().Equal("PAY-001", "PAY-002");
        receipt.QrReference.Should().Contain("INV-POS-001").And.Contain(fixture.SaleId.ToString("N"));
    }

    [Fact]
    public async Task RecordReprintAsync_ShouldAppendOneIdempotentAuditEventWithoutChangingCanonicalSale()
    {
        await using var fixture = await Fixture.CreateAsync();
        var request = new MobilePosReceiptReprintRequestDto
        {
            InstallationId = "installation-123456",
            ClientEventId = "reprint-event-001",
            Reason = "Customer requested another copy"
        };

        var first = await fixture.Service.RecordReprintAsync(
            fixture.SaleId, request, CancellationToken.None);
        var replay = await fixture.Service.RecordReprintAsync(
            fixture.SaleId, request, CancellationToken.None);

        first.CopyType.Should().Be("REPRINT");
        first.CopyNumber.Should().Be(1);
        first.ReprintCount.Should().Be(1);
        first.AuditEventId.Should().NotBeNull();
        replay.AuditEventId.Should().Be(first.AuditEventId);
        replay.CopyNumber.Should().Be(1);
        (await fixture.Db.AuditLogs.CountAsync()).Should().Be(1);
        var audit = await fixture.Db.AuditLogs.SingleAsync();
        audit.Action.Should().Be("Reprint");
        audit.Resource.Should().Be("MobilePosReceipt");
        audit.ResourceId.Should().Be(fixture.SaleId.ToString());
        audit.IdempotencyKey.Should().Contain("reprint-event-001");

        var sale = await fixture.Db.MobilePosSales.AsNoTracking().SingleAsync();
        sale.InvoiceNumber.Should().Be("INV-POS-001");
        sale.Status.Should().Be(MobilePosSaleStatus.Completed);
    }

    [Fact]
    public async Task GetAsync_ShouldNotExposeReceiptFromAnotherAssignedStore()
    {
        await using var fixture = await Fixture.CreateAsync(useDifferentAssignedStore: true);

        var action = () => fixture.Service.GetAsync(
            fixture.SaleId, "installation-123456", CancellationToken.None);

        await action.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*assigned store*");
    }

    [Fact]
    public async Task GetCollectionAsync_ShouldProjectCanonicalAllocationsAndPayments()
    {
        await using var fixture = await Fixture.CreateAsync();

        var receipt = await fixture.Service.GetCollectionAsync(
            fixture.CollectionId, "installation-123456", CancellationToken.None);

        receipt.ReceiptKind.Should().Be("COLLECTION");
        receipt.CopyType.Should().Be("ORIGINAL");
        receipt.CustomerCode.Should().Be("WALK-IN");
        receipt.TotalAmount.Should().Be(75m);
        receipt.Allocations.Should().ContainSingle().Which.InvoiceNumber.Should().Be("INV-POS-001");
        receipt.Tenders.Should().HaveCount(2);
        receipt.Tenders.Select(item => item.PaymentNumber).Should().Equal("COL-PAY-001", "COL-PAY-002");
        receipt.QrReference.Should().Contain("COLLECTION").And.Contain(fixture.CollectionId.ToString("N"));
    }

    [Fact]
    public async Task RecordCollectionReprintAsync_ShouldAppendOneIdempotentAuditEvent()
    {
        await using var fixture = await Fixture.CreateAsync();
        var request = new MobilePosReceiptReprintRequestDto
        {
            InstallationId = "installation-123456",
            ClientEventId = "collection-reprint-001",
            Reason = "Customer requested another collection receipt"
        };

        var first = await fixture.Service.RecordCollectionReprintAsync(
            fixture.CollectionId, request, CancellationToken.None);
        var replay = await fixture.Service.RecordCollectionReprintAsync(
            fixture.CollectionId, request, CancellationToken.None);

        first.CopyType.Should().Be("REPRINT");
        first.CopyNumber.Should().Be(1);
        replay.AuditEventId.Should().Be(first.AuditEventId);
        (await fixture.Db.AuditLogs.CountAsync(item =>
            item.Resource == "MobilePosCollectionReceipt")).Should().Be(1);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private Fixture(
            ApplicationDbContext db,
            MobilePosReceiptService service,
            Guid saleId,
            Guid collectionId)
        {
            Db = db;
            Service = service;
            SaleId = saleId;
            CollectionId = collectionId;
        }

        public ApplicationDbContext Db { get; }
        public MobilePosReceiptService Service { get; }
        public Guid SaleId { get; }
        public Guid CollectionId { get; }

        public static async Task<Fixture> CreateAsync(bool useDifferentAssignedStore = false)
        {
            var tenantId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var storeId = Guid.NewGuid();
            var tillId = Guid.NewGuid();
            var deviceId = Guid.NewGuid();
            var sessionId = Guid.NewGuid();
            var partnerId = Guid.NewGuid();
            var roleId = Guid.NewGuid();
            var invoiceId = Guid.NewGuid();
            var saleId = Guid.NewGuid();
            var collectionId = Guid.NewGuid();
            var now = DateTime.UtcNow;
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"mobile-pos-receipt-{Guid.NewGuid():N}")
                .Options;
            var db = new ApplicationDbContext(options);

            var tenant = new Tenant
            {
                Id = tenantId,
                Code = "RHEMA-TEST",
                Name = "Rhema Test Tenant"
            };
            var user = new ApplicationUser
            {
                Id = userId,
                UserName = "mobile.cashier",
                FirstName = "Mobile",
                LastName = "Cashier"
            };
            var location = new Location
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Code = "ACC-SHOP",
                Name = "Accra Shop",
                StructureId = Guid.NewGuid(),
                LocationLevelId = Guid.NewGuid()
            };
            var partner = new BusinessPartner
            {
                Id = partnerId,
                TenantId = tenantId,
                PartnerCode = "WALK-IN",
                PartnerName = "Walk-in Customer",
                PartnerType = "Customer",
                RegistrationStatus = "Approved",
                ApprovalStatus = "Approved",
                IsActive = true
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
            var store = new MobilePosStore
            {
                Id = storeId,
                TenantId = tenantId,
                Code = "SHOP-01",
                Name = "Shop 01",
                Status = MobilePosStoreStatus.Active,
                LocationId = location.Id,
                Location = location,
                CurrencyCode = "GHS",
                DefaultWalkInBusinessPartnerId = partnerId,
                DefaultWalkInBusinessPartner = partner,
                DefaultWalkInBusinessPartnerRoleId = roleId,
                DefaultWalkInBusinessPartnerRole = role
            };
            var till = new MobilePosTill
            {
                Id = tillId,
                TenantId = tenantId,
                MobilePosStoreId = storeId,
                MobilePosStore = store,
                TillNumber = "TILL-01",
                Name = "Till 01",
                Status = MobilePosTillStatus.Active,
                LiquidityAccountId = Guid.NewGuid()
            };
            var session = new CashierTillSession
            {
                Id = sessionId,
                TenantId = tenantId,
                SessionNumber = "SHIFT-001",
                LiquidityAccountId = till.LiquidityAccountId,
                BusinessDate = now.Date,
                Currency = "GHS",
                CashierUserId = userId,
                CashierName = "Mobile Cashier",
                Status = CashierTillSessionStatus.Open,
                OpenedAt = now.AddHours(-1),
                OpenedById = userId
            };
            var device = new MobilePosDevice
            {
                Id = deviceId,
                TenantId = tenantId,
                InstallationIdHash = new string('a', 64),
                DeviceName = "Android POS 01",
                RequestedByUserId = userId,
                RequestedAtUtc = now.AddDays(-1),
                MobilePosStoreId = storeId,
                MobilePosStore = store,
                MobilePosTillId = tillId,
                MobilePosTill = till,
                Status = MobilePosDeviceStatus.Active
            };
            var invoice = new Invoice
            {
                Id = invoiceId,
                TenantId = tenantId,
                InvoiceNumber = "INV-POS-001",
                BusinessPartnerId = partnerId,
                BusinessPartner = partner,
                BusinessPartnerRoleId = roleId,
                BusinessPartnerRole = role,
                BusinessPartnerArProfileVersionId = Guid.NewGuid(),
                BusinessPartnerCode = partner.PartnerCode,
                CustomerName = partner.PartnerName,
                InvoiceDate = now,
                CurrencyCode = "GHS",
                SubTotal = 100m,
                TotalAmount = 100m,
                PaidAmount = 100m,
                BaseCurrencyAmount = 100m,
                Status = InvoiceStatus.Paid
            };
            var cash = new FinancePaymentMethod
            {
                Id = Guid.NewGuid(), TenantId = tenantId, Code = "CASH", Name = "Cash",
                Type = PaymentMethodType.Cash, IsActive = true, RequiresBankAccount = false
            };
            var card = new FinancePaymentMethod
            {
                Id = Guid.NewGuid(), TenantId = tenantId, Code = "CARD", Name = "Card",
                Type = PaymentMethodType.Card, IsActive = true, RequiresBankAccount = false
            };
            var sale = new MobilePosSale
            {
                Id = saleId,
                TenantId = tenantId,
                MobilePosStoreId = storeId,
                MobilePosStore = store,
                MobilePosTillId = tillId,
                MobilePosTill = till,
                CashierTillSessionId = sessionId,
                CashierTillSession = session,
                MobilePosDeviceId = deviceId,
                MobilePosDevice = device,
                OperatorUserId = userId,
                OperatorUser = user,
                ClientMutationId = "mutation-001",
                LocalReference = "LOCAL-POS-001",
                BusinessPartnerId = partnerId,
                BusinessPartner = partner,
                BusinessPartnerRoleId = roleId,
                BusinessPartnerRole = role,
                UsedStoreDefaultCustomer = true,
                BusinessDate = now.Date,
                OccurredAtUtc = now,
                CurrencyCode = "GHS",
                SubTotal = 100m,
                TotalAmount = 100m,
                Status = MobilePosSaleStatus.Completed,
                InvoiceId = invoiceId,
                Invoice = invoice,
                InvoiceNumber = invoice.InvoiceNumber,
                SynchronizedAtUtc = now,
                CreatedAt = now,
                CreatedBy = user.UserName,
                CreatedById = userId
            };
            sale.Lines.Add(new MobilePosSaleLine
            {
                Id = Guid.NewGuid(), TenantId = tenantId, Sequence = 1, ClientLineId = Guid.NewGuid(),
                Description = "Retail item", Quantity = 2m, UnitPrice = 50m, LineTotal = 100m,
                UnitOfMeasureCode = "EA", CreatedAt = now, CreatedBy = user.UserName, CreatedById = userId
            });
            sale.Tenders.Add(new MobilePosTender
            {
                Id = Guid.NewGuid(), TenantId = tenantId, Sequence = 1, PaymentMethodId = cash.Id,
                PaymentMethod = cash, Amount = 60m, CustomerPaymentId = Guid.NewGuid(),
                PaymentNumber = "PAY-001", ProviderStatus = "Posted", Status = MobilePosTenderStatus.Completed,
                CreatedAt = now, CreatedBy = user.UserName, CreatedById = userId
            });
            sale.Tenders.Add(new MobilePosTender
            {
                Id = Guid.NewGuid(), TenantId = tenantId, Sequence = 2, PaymentMethodId = card.Id,
                PaymentMethod = card, Amount = 40m, ExternalReference = "CARD-AUTH-001",
                CustomerPaymentId = Guid.NewGuid(), PaymentNumber = "PAY-002", ProviderStatus = "Posted",
                Status = MobilePosTenderStatus.Completed, CreatedAt = now, CreatedBy = user.UserName,
                CreatedById = userId
            });

            var collection = new MobilePosCollection
            {
                Id = collectionId,
                TenantId = tenantId,
                MobilePosStoreId = storeId,
                MobilePosStore = store,
                MobilePosTillId = tillId,
                MobilePosTill = till,
                CashierTillSessionId = sessionId,
                CashierTillSession = session,
                MobilePosDeviceId = deviceId,
                MobilePosDevice = device,
                OperatorUserId = userId,
                OperatorUser = user,
                ClientMutationId = "collection-mutation-001",
                LocalReference = "COL-LOCAL-001",
                BusinessPartnerId = partnerId,
                BusinessPartner = partner,
                BusinessPartnerRoleId = roleId,
                BusinessPartnerRole = role,
                BusinessDate = now.Date,
                OccurredAtUtc = now,
                CurrencyCode = "GHS",
                TotalAmount = 75m,
                Status = MobilePosCollectionStatus.Completed,
                SynchronizedAtUtc = now,
                CreatedAt = now,
                CreatedBy = user.UserName,
                CreatedById = userId
            };
            collection.Allocations.Add(new MobilePosCollectionAllocation
            {
                Id = Guid.NewGuid(), TenantId = tenantId, Sequence = 1, InvoiceId = invoiceId,
                Invoice = invoice, InvoiceNumber = invoice.InvoiceNumber, Amount = 75m,
                CreatedAt = now, CreatedBy = user.UserName, CreatedById = userId
            });
            collection.Tenders.Add(new MobilePosCollectionTender
            {
                Id = Guid.NewGuid(), TenantId = tenantId, Sequence = 1, PaymentMethodId = cash.Id,
                PaymentMethod = cash, Amount = 50m, CustomerPaymentId = Guid.NewGuid(),
                PaymentNumber = "COL-PAY-001", PaymentStatus = "Posted",
                Status = MobilePosTenderStatus.Completed, CreatedAt = now, CreatedBy = user.UserName,
                CreatedById = userId
            });
            collection.Tenders.Add(new MobilePosCollectionTender
            {
                Id = Guid.NewGuid(), TenantId = tenantId, Sequence = 2, PaymentMethodId = card.Id,
                PaymentMethod = card, Amount = 25m, ExternalReference = "COL-AUTH-001",
                CustomerPaymentId = Guid.NewGuid(), PaymentNumber = "COL-PAY-002", PaymentStatus = "Posted",
                Status = MobilePosTenderStatus.Completed, CreatedAt = now, CreatedBy = user.UserName,
                CreatedById = userId
            });

            db.Tenants.Add(tenant);
            db.Users.Add(user);
            db.MobilePosSales.Add(sale);
            db.MobilePosCollections.Add(collection);
            await db.SaveChangesAsync();

            var currentUser = new Mock<ICurrentUserService>();
            currentUser.SetupGet(item => item.TenantId).Returns(tenantId);
            currentUser.SetupGet(item => item.UserId).Returns(userId.ToString());
            currentUser.SetupGet(item => item.UserName).Returns(user.UserName);
            currentUser.SetupGet(item => item.IpAddress).Returns("127.0.0.1");
            currentUser.SetupGet(item => item.UserAgent).Returns("Mobile POS test");
            var foundation = new Mock<IMobilePosFoundationService>();
            foundation.Setup(item => item.GetBootstrapAsync(
                    "installation-123456", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new MobilePosBootstrapDto
                {
                    UserId = userId,
                    Device = new MobilePosDeviceDto { Id = deviceId, DeviceName = device.DeviceName },
                    Store = new MobilePosStoreDto
                    {
                        Id = useDifferentAssignedStore ? Guid.NewGuid() : storeId,
                        Code = store.Code,
                        Name = store.Name
                    },
                    Till = new MobilePosTillDto { Id = tillId, TillNumber = till.TillNumber }
                });

            return new Fixture(
                db,
                new MobilePosReceiptService(db, currentUser.Object, foundation.Object),
                saleId,
                collectionId);
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
